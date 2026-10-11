using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HYROLauncherWeb
{
    internal partial class WebLauncherForm
    {
        private HyTcgService hyTcg;
        private CancellationTokenSource hyTcgSession;
        private string hyTcgSessionId="";
        private readonly SemaphoreSlim hyTcgNetwork=new SemaphoreSlim(3,3);
        private readonly HashSet<string> hyTcgRequests=new HashSet<string>();
        private readonly Dictionary<string,HyTcgCard> hyTcgPending=new Dictionary<string,HyTcgCard>();
        private readonly Dictionary<string,string> hyTcgSaved=new Dictionary<string,string>();
        private readonly Dictionary<string,string> hyTcgCardTokens=new Dictionary<string,string>();
        private readonly HashSet<string> hyTcgReservations=new HashSet<string>();
        private readonly HashSet<string> hyTcgSaving=new HashSet<string>();

        private bool HyTcgDocument(PluginBrowserWindow window)
        {
            return window!=null && window==pluginWindow && !window.IsDisposed && currentBuiltInPluginId==BuiltInHyTcgPluginId
                && window.IsLocalPanel("hytcg.html",BuiltInHyTcgPluginId);
        }
        private void HyTcgReply(PluginBrowserWindow window,string session,Dictionary<string,object> payload)
        {
            if(IsDisposed || session!=hyTcgSessionId || !HyTcgDocument(window)) return;
            payload["session"]=session; window.Post(payload);
        }
        private void HyTcgProgress(PluginBrowserWindow window,string session,string request,string phase,int attempt)
        {
            if(IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)delegate {
                HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgProgress"},{"requestId",request},{"phase",phase},{"attempt",attempt}});
            }); } catch { }
        }
        private static string HyTcgError(Exception error)
        {
            var known=error as HyTcgException;
            if(known!=null)
            {
                switch(known.Code) { case "CreditsInsufficient":return "NoCredits";case "GemsInsufficient":return "InsufficientGems";case "VaultInvalid":case "VaultUnavailable":case "ClockInvalid":return "WalletInvalid";case "CardUnavailable":return "CardAlreadyDissolved";case "PackInvalid":return "TokenInvalid";default:return known.Code; }
            }
            if(error is OperationCanceledException) return "TimedOut";
            if(error is IOException || error is UnauthorizedAccessException) return "SaveFailed";
            return "ConnectionFailed";
        }
        private object HyTcgUiConfig(HyTcgConfig config)
        {
            return new Dictionary<string,object>{{"Concurrency",config.Concurrency},{"MaxCharacterAttempts",config.MaxCharacterAttempts},{"DropRates",HyTcgRules.DropRates()}};
        }
        private object HyTcgWalletSafe(HyTcgService service=null)
        {
            try { return (service??hyTcg).Economy.Snapshot(); } catch { return null; }
        }
        private void HyTcgEndSession()
        {
            if(hyTcgSession!=null)hyTcgSession.Cancel();
            if(hyTcg!=null)foreach(string reservation in hyTcgReservations.ToArray())try {hyTcg.Economy.CancelReservation(reservation);}catch { }
            hyTcgReservations.Clear();
        }
        private void HandleHyTcgMessage(Dictionary<string,object> message,PluginBrowserWindow window)
        {
            if(!HyTcgDocument(window)) return;
            string type=GetString(message,"type"),session=GetString(message,"session");
            if(!Regex.IsMatch(session,@"^[A-Za-z0-9_-]{8,64}$")) return;
            if(hyTcgDevTransition)return;
            if(HyTcgCheckDevExpiry())return;
            if(type=="hytcgInit")
            {
                HyTcgEndSession();
                hyTcgSession=new CancellationTokenSource(); hyTcgSessionId=session;
                hyTcgPending.Clear(); hyTcgRequests.Clear(); hyTcgSaved.Clear(); hyTcgSaving.Clear();hyTcgCardTokens.Clear();
                hyTcgDiscoveryRequests.Clear();
                if(hyTcg==null) hyTcg=new HyTcgService(AppDomain.CurrentDomain.BaseDirectory);
                try
                {
                    var config=hyTcg.LoadConfig();hyTcg.Economy.RecoverUnused();var recoveries=hyTcg.Economy.PendingCards();
                    foreach(var item in recoveries) {hyTcgPending[item.Card.CardId]=item.Card;hyTcgCardTokens[item.Card.CardId]=item.Token;}
                    var missions=EnsureTcgMissions();
                    HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgConfig"},{"config",HyTcgUiConfig(config)},{"wallet",hyTcg.Economy.Snapshot()},{"missions",missions.Items},{"missionDay",missions.Day},{"recoveries",recoveries},{"upgrades",hyTcg.Economy.PendingUpgrades()},{"cardsDirectory",hyTcg.CardsDirectory},{"dev",HyTcgDevState()},{"error",""}});
                    if(hyTcgDevArmed) {hyTcgDevArmed=false;HyTcgToggleDev(window,session);}
                }
                catch(Exception ex) { HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgConfig"},{"error",HyTcgError(ex)}}); }
                return;
            }
            if(session!=hyTcgSessionId || hyTcg==null || hyTcgSession==null) return;
            if(type=="hytcgDevToggle") {HyTcgToggleDev(window,session);return;}
            if(type=="hytcgPanelSize") {window.ToggleWidePanel();return;}
            if(HandleHyTcgLifestyle(window,session,type,message))return;
            if(HandleHyTcgExtra(window,session,type,message))return;
            string request=GetString(message,"requestId");
            if(type=="hytcgOpen")
            {
                if(!Regex.IsMatch(request,@"^[A-Za-z0-9_-]{1,64}$") || hyTcgRequests.Count>=10 || !hyTcgRequests.Add(request)) return;
                HyTcgOpen(window,session,request,GetString(message,"token"),hyTcgSession.Token);
            }
            else if(type=="hytcgSave") HyTcgSave(window,session,GetString(message,"cardId"),GetString(message,"png"));
            else if(type=="hytcgFallback") HyTcgFallback(window,session,request,GetString(message,"cardId"),hyTcgSession.Token);
            else if(type=="hytcgCollection") HyTcgCollection(window,session,request,ClampInt(GetString(message,"offset"),0,0,1000000),GetString(message,"rarity"),GetString(message,"query"),GetBool(message,"favoritesOnly"),GetString(message,"sort"));
            else if(type=="hytcgUpgradeOptions")
            {
                if(!Regex.IsMatch(request,@"^[A-Za-z0-9_-]{1,64}$"))return;
                HyTcgUpgradeOptions(window,session,request,message);
            }
            else if(type=="hytcgUpgradePrepare" || type=="hytcgUpgradeResume")
            {
                if(!Regex.IsMatch(request,@"^[A-Za-z0-9_-]{1,64}$"))return;
                if(hyTcgRequests.Count>=10 || !hyTcgRequests.Add(request)) {HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgUpgradeReady"},{"requestId",request},{"error","Busy"}});return;}
                HyTcgPrepareUpgrade(window,session,request,message,type=="hytcgUpgradeResume",hyTcgSession.Token);
            }
            else if(type=="hytcgUpgradeSave")
            {
                if(!Regex.IsMatch(request,@"^[A-Za-z0-9_-]{1,64}$"))return;
                HyTcgSaveUpgrade(window,session,request,GetString(message,"upgradeToken"),GetString(message,"png"));
            }
            else if(type=="hytcgWallet" || type=="hytcgReserve" || type=="hytcgBuyPull" || type=="hytcgDissolve" || type=="hytcgDissolveMany" || type=="hytcgFavorite" || type=="hytcgUpgradeCancel" || type=="hytcgCancelReservation")
            {
                if(type!="hytcgCancelReservation" && !Regex.IsMatch(request,@"^[A-Za-z0-9_-]{1,64}$"))return;
                HyTcgWalletAction(window,session,request,type,message);
            }
            else if(type=="hytcgFolder")
            {
                try { Directory.CreateDirectory(hyTcg.CardsDirectory); Process.Start("explorer.exe","\""+hyTcg.CardsDirectory+"\""); } catch { }
            }
        }
        private async void HyTcgWalletAction(PluginBrowserWindow window,string session,string request,string type,Dictionary<string,object> message)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            string replyType=type=="hytcgReserve"?"hytcgReserved":type=="hytcgDissolve"?"hytcgDissolved":type=="hytcgDissolveMany"?"hytcgDissolvedMany":type=="hytcgFavorite"?"hytcgFavorite":type=="hytcgUpgradeCancel"?"hytcgUpgradeCancelled":"hytcgWallet";
            var reply=new Dictionary<string,object>{{"type",replyType},{"requestId",request},{"error",""}};
            var walletSession=hyTcgSession;var cancellation=walletSession.Token;
            bool tracked=false;
            try
            {
                if(type=="hytcgReserve" || type=="hytcgDissolve" || type=="hytcgDissolveMany")
                {
                    if(hyTcgRequests.Count>=10 || !hyTcgRequests.Add(request))throw new HyTcgException("Busy");tracked=true;
                }
                if(type=="hytcgReserve")
                {
                    int count;if(!Int32.TryParse(GetString(message,"count"),out count) || (count!=1&&count!=10))throw new HyTcgException("PackInvalid");
                    string payment=GetString(message,"payment");if(payment=="")payment="credits";
                    string bannerId=GetString(message,"bannerId");
                    var reserveToken=cancellation;
                    if(payment=="limited" || payment=="limitedGems")await Task.Run(()=>service.RequireBanner(bannerId,service.LoadConfig(),reserveToken),reserveToken);
                    reserveToken.ThrowIfCancellationRequested();if(session!=hyTcgSessionId)throw new OperationCanceledException();
                    var reservation=service.Economy.Reserve(count,payment,bannerId);hyTcgReservations.Add(reservation.ReservationId);
                    reply["reservationId"]=reservation.ReservationId;reply["tokens"]=reservation.Tokens;reply["wallet"]=reservation.Wallet;
                }
                else if(type=="hytcgBuyPull")reply["wallet"]=service.Economy.BuyCredit();
                else if(type=="hytcgCancelReservation")
                {
                    string reservation=GetString(message,"reservationId");
                    if(!Regex.IsMatch(reservation,@"^HR-[a-f0-9]{32}$"))return;
                    reply["wallet"]=service.Economy.CancelReservation(reservation);hyTcgReservations.Remove(reservation);
                }
                else if(type=="hytcgFavorite")
                {
                    string id=HyTcgCardId(GetString(message,"cardId"));
                    bool favorite=GetBool(message,"favorite");
                    reply["wallet"]=service.Economy.SetFavorite(id,favorite);reply["cardId"]=id;reply["favorite"]=favorite;
                }
                else if(type=="hytcgUpgradeCancel")
                {
                    string token=HyTcgUpgradeToken(GetString(message,"upgradeToken"));
                    reply["wallet"]=service.Economy.CancelUpgrade(token);reply["upgradeToken"]=token;
                }
                else if(type=="hytcgDissolveMany")
                {
                    var ids=HyTcgIds(message,"cardIds",100);
                    if(ids.Any(id=>hyTcgSaving.Contains(id)))throw new HyTcgException("Busy");
                    await HyTcgWarmDissolveCards(service,ids,90000,cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    var dissolved=await Task.Run(()=>{cancellation.ThrowIfCancellationRequested();return service.Economy.DissolveMany(ids);},cancellation);
                    HyTcgDeleteExports(service,dissolved.FilePaths);
                    cancellation.ThrowIfCancellationRequested();
                    if(session==hyTcgSessionId)HyTcgForgetCards(dissolved.CardIds);
                    reply["cardIds"]=dissolved.CardIds;reply["gemsAwarded"]=dissolved.GemsAwarded;reply["wallet"]=dissolved.Wallet;
                }
                else if(type=="hytcgDissolve")
                {
                    string id=GetString(message,"cardId");if(!Regex.IsMatch(id,@"^HT-\d{8}-[A-F0-9]{12}$"))throw new HyTcgException("CardInvalid");
                    if(hyTcgSaving.Contains(id))throw new HyTcgException("Busy");
                    await HyTcgWarmDissolveCards(service,new List<string>{id},45000,cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    var dissolved=await Task.Run(()=>{cancellation.ThrowIfCancellationRequested();return service.Economy.Dissolve(id);},cancellation);
                    // The durable tombstone/gem award is already committed, even if file deletion fails.
                    HyTcgDeleteExports(service,new[]{dissolved.FilePath});
                    cancellation.ThrowIfCancellationRequested();
                    if(session==hyTcgSessionId)HyTcgForgetCards(new[]{id});
                    reply["cardId"]=id;reply["gemsAwarded"]=dissolved.GemsAwarded;reply["wallet"]=dissolved.Wallet;
                }
                else reply["wallet"]=service.Economy.Snapshot();
            }
            catch(Exception ex) {reply["error"]=(type=="hytcgDissolve" || type=="hytcgDissolveMany") && ex is HyTcgException && ((HyTcgException)ex).Code=="CardInvalid"?"CardTampered":HyTcgError(ex);reply["wallet"]=HyTcgWalletSafe(service);}
            finally {if(tracked && Object.ReferenceEquals(walletSession,hyTcgSession))hyTcgRequests.Remove(request);}
            HyTcgReply(window,session,reply);
        
            }
        }
        private async Task HyTcgWarmDissolveCards(HyTcgService service,List<string> ids,int timeoutMs,CancellationToken cancellation)
        {
            using(HyTcgOperation(service)) {
            var cards=await Task.Run(()=>service.Economy.ReadOwnedCards(ids),cancellation);
            using(var enrichment=CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                enrichment.CancelAfter(timeoutMs);
                try {await Task.Run(()=>service.WarmCharacterInfo(cards,enrichment.Token),enrichment.Token);}
                catch(OperationCanceledException) {cancellation.ThrowIfCancellationRequested();}
            }
            cancellation.ThrowIfCancellationRequested();
        
            }
        }
        private async void HyTcgOpen(PluginBrowserWindow window,string session,string request,string token,CancellationToken sessionToken)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            bool acquired=false,issued=false,began=false;
            try
            {
                var previous=service.Economy.CharacterMissCompensation(token);
                if(previous!=null)
                {HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgCard"},{"requestId",request},{"compensated",true},{"gemsAwarded",previous.GemsAwarded},{"wallet",previous.Wallet},{"error",""}});return;}
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(sessionToken))
                {
                    timeout.CancelAfter(60000);
                    await hyTcgNetwork.WaitAsync(timeout.Token); acquired=true;
                    if(!Regex.IsMatch(token??"",@"^HC-[a-f0-9]{32}$"))throw new HyTcgException("TokenInvalid");
                    var recovery=service.Economy.PendingCards().FirstOrDefault(item=>item.Token==token);issued=recovery!=null;
                    service.Economy.Begin(token);began=true;
                    var config=service.LoadConfig();
                    string bannerId=service.Economy.BannerForToken(token);
                    var result=await Task.Run(()=>recovery!=null?service.Resume(recovery.Card,config,timeout.Token):bannerId!=""?service.OpenLimited(bannerId,config,(phase,attempt)=>HyTcgProgress(window,session,request,phase,attempt),timeout.Token):service.Open(config,(phase,attempt)=>HyTcgProgress(window,session,request,phase,attempt),timeout.Token),timeout.Token);
                    service.Economy.AuthorizeCard(token,result.Card);issued=true;
                    if(session!=hyTcgSessionId || !HyTcgDocument(window)) return;
                    if(hyTcgPending.Count>=256) { hyTcgPending.Clear(); hyTcgSaved.Clear();hyTcgCardTokens.Clear(); }
                    hyTcgPending[result.Card.CardId]=result.Card;
                    hyTcgCardTokens[result.Card.CardId]=token;
                    HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgCard"},{"requestId",request},{"card",result.Card},{"imageData",result.ImageData},{"wallet",HyTcgWalletSafe(service)},{"recoverable",true},{"error",""}});
                }
            }
            catch(Exception ex)
            {
                var known=ex as HyTcgException;
                if(began&&!issued&&known!=null&&known.Code=="CharacterNotFound"&&!sessionToken.IsCancellationRequested)
                {
                    try
                    {
                        var compensation=service.Economy.CompensateCharacterMiss(token);
                        HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgCard"},{"requestId",request},{"compensated",true},{"gemsAwarded",compensation.GemsAwarded},{"wallet",compensation.Wallet},{"error",""}});
                        return;
                    }
                    catch(Exception awardError) {ex=awardError;}
                }
                if(began&&!issued)try {service.Economy.Refund(token);}catch { }
                HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgCard"},{"requestId",request},{"recoverable",issued},{"wallet",HyTcgWalletSafe(service)},{"error",HyTcgError(ex)}});
            }
            finally {if(began&&hyTcg!=null)try {service.Economy.EndFetch(token);}catch { }if(acquired)hyTcgNetwork.Release();if(session==hyTcgSessionId)hyTcgRequests.Remove(request);}
        
            }
        }
        private async void HyTcgFallback(PluginBrowserWindow window,string session,string request,string cardId,CancellationToken token)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            HyTcgCard card;string cardToken;
            if(!hyTcgPending.TryGetValue(cardId,out card) || !hyTcgCardTokens.TryGetValue(cardId,out cardToken) || hyTcgSaving.Contains(cardId)) return;
            bool acquired=false;
            try
            {
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(20000);
                    await hyTcgNetwork.WaitAsync(timeout.Token); acquired=true;
                    string data=await Task.Run(()=>service.Fallback(card,service.LoadConfig(),timeout.Token),timeout.Token);
                    service.Economy.AuthorizeCard(cardToken,card);
                    HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgCard"},{"requestId",request},{"card",card},{"imageData",data},{"error",""}});
                }
            }
            catch(Exception ex) { HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgCard"},{"requestId",request},{"error",HyTcgError(ex)}}); }
            finally { if(acquired) hyTcgNetwork.Release(); }
        
            }
        }
        private async void HyTcgSave(PluginBrowserWindow window,string session,string cardId,string png)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            HyTcgCard card;string cardToken;
            if(!hyTcgPending.TryGetValue(cardId,out card) || !hyTcgCardTokens.TryGetValue(cardId,out cardToken) || !hyTcgSaving.Add(cardId)) return;
            try
            {
                string path;
                path=await Task.Run(()=>service.SaveCard(card,png,cardToken));
                if(session==hyTcgSessionId) hyTcgSaved[cardId]=path;
                HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgSaved"},{"cardId",cardId},{"path",path},{"wallet",HyTcgWalletSafe(service)},{"error",""}});
            }
            catch(Exception ex) { HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgSaved"},{"cardId",cardId},{"error",HyTcgError(ex)}}); }
            finally { if(session==hyTcgSessionId) hyTcgSaving.Remove(cardId); }
        
            }
        }
        private static string HyTcgCardId(string id)
        {
            if(!Regex.IsMatch(id??"",@"^HT-\d{8}-[A-F0-9]{12}$"))throw new HyTcgException("CardInvalid");
            return id;
        }
        private static string HyTcgUpgradeToken(string token)
        {
            if(!Regex.IsMatch(token??"",@"^HU-[a-f0-9]{32}$"))throw new HyTcgException("UpgradeUnavailable");
            return token;
        }
        private static List<string> HyTcgIds(Dictionary<string,object> message,string key,int maximum)
        {
            object raw;
            if(!message.TryGetValue(key,out raw) || raw is string || !(raw is IEnumerable))throw new HyTcgException("CardInvalid");
            var ids=new List<string>();
            foreach(object item in (IEnumerable)raw)
            {
                if(!(item is string) || ids.Count>=maximum)throw new HyTcgException("CardInvalid");
                string id=HyTcgCardId((string)item);if(ids.Contains(id))throw new HyTcgException("CardInvalid");ids.Add(id);
            }
            if(ids.Count==0)throw new HyTcgException("CardInvalid");return ids;
        }
        private void HyTcgForgetCards(IEnumerable<string> ids)
        {
            foreach(string id in ids) {hyTcgPending.Remove(id);hyTcgCardTokens.Remove(id);hyTcgSaved.Remove(id);}
        }
        private void HyTcgDeleteExports(HyTcgService service,IEnumerable<string> paths)
        {
            // Paths come from a committed native receipt. Check the exact cards directory again before deleting.
            string directory;
            try
            {
                directory=Path.GetFullPath(service.CardsDirectory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
                if(!Directory.Exists(directory) || (File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return;
            }
            catch {return;}
            foreach(string path in paths)
            {
                try
                {
                    string full=Path.GetFullPath(path);
                    if(!String.Equals(Path.GetDirectoryName(full),directory,StringComparison.OrdinalIgnoreCase))continue;
                    if(File.Exists(full) && (File.GetAttributes(full)&FileAttributes.ReparsePoint)==0)File.Delete(full);
                }
                catch { } // A retained export cannot resurrect a native tombstone.
            }
        }
        private async void HyTcgUpgradeOptions(PluginBrowserWindow window,string session,string request,Dictionary<string,object> message)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            var reply=new Dictionary<string,object>{{"type","hytcgUpgradeOptions"},{"requestId",request},{"error",""}};
            try
            {
                string id=HyTcgCardId(GetString(message,"cardId")),query=GetString(message,"query").Trim();if(query.Length>180)query=query.Substring(0,180);
                int offset=ClampInt(GetString(message,"offset"),0,0,100000);bool sameCharacter=GetBool(message,"sameCharacterOnly");
                if(service.Economy.IsDevMode)service.Economy.EnsureDevUpgradeMaterial(id);
                var catalog=await Task.Run(()=>service.Economy.CollectionCards());
                var target=catalog.FirstOrDefault(item=>item.Card.CardId==id);
                if(target==null)throw new HyTcgException("CardUnavailable");
                if(target.UpgradeLocked)throw new HyTcgException("CardLocked");
                if(target.IsLegacy)throw new HyTcgException("LegacyCard");
                int next=Array.IndexOf(HyTcgService.Rarities,target.Card.Rarity)+1;
                if(next>=HyTcgService.Rarities.Length)throw new HyTcgException("UpgradeMax");
                string rarity=HyTcgService.Rarities[next];
                var materials=catalog.Where(item=>item.Card.CardId!=id && item.CanDissolve && !item.UpgradeLocked && item.Card.Rarity==rarity
                    && (!sameCharacter || item.Card.CharacterId==target.Card.CharacterId)
                    && (query=="" || (item.Card.Name??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 || (item.Card.Series??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 || item.Card.CharacterId.ToString()==query)).ToList();
                var page=materials.Skip(offset).Take(60).ToList();
                service.PrepareCollectionImages(page);
                reply["card"]=target.Card;reply["targetRarity"]=rarity;reply["materials"]=page;reply["offset"]=offset;reply["total"]=materials.Count;reply["hasMore"]=offset+page.Count<materials.Count;
            }
            catch(Exception ex) {reply["error"]=HyTcgError(ex);}
            HyTcgReply(window,session,reply);
        
            }
        }
        private async void HyTcgPrepareUpgrade(PluginBrowserWindow window,string session,string request,Dictionary<string,object> message,bool resume,CancellationToken sessionToken)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            bool acquired=false,began=false;HyTcgUpgrade intent=null;
            var reply=new Dictionary<string,object>{{"type","hytcgUpgradeReady"},{"requestId",request},{"error",""}};
            try
            {
                intent=resume?service.Economy.GetUpgrade(HyTcgUpgradeToken(GetString(message,"upgradeToken"))):service.Economy.PrepareUpgrade(HyTcgCardId(GetString(message,"cardId")),HyTcgIds(message,"materialIds",10),GetBool(message,"changeArtwork"));
                reply["upgradeToken"]=intent.Token;
                if(intent.Stage=="complete")throw new HyTcgException("UpgradeUnavailable");
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(sessionToken))
                {
                    timeout.CancelAfter(60000);
                    await hyTcgNetwork.WaitAsync(timeout.Token);acquired=true;
                    service.Economy.BeginUpgrade(intent.Token);began=true;
                    // Authorized recovery reloads the saved artwork choice, never draws a fresh variant.
                    intent=service.Economy.GetUpgrade(intent.Token);
                    var config=service.LoadConfig();
                    var result=await Task.Run(()=>service.PrepareUpgrade(intent,config,timeout.Token),timeout.Token);
                    service.Economy.AuthorizeUpgrade(intent.Token,result.Card);
                    reply["card"]=result.Card;reply["imageData"]=result.ImageData;reply["wallet"]=HyTcgWalletSafe(service);
                }
            }
            catch(Exception ex) {reply["error"]=HyTcgError(ex);reply["recoverable"]=intent!=null;reply["wallet"]=HyTcgWalletSafe(service);}
            finally
            {
                if(began)try {service.Economy.EndUpgrade(intent.Token);}catch { }
                if(acquired)hyTcgNetwork.Release();if(session==hyTcgSessionId)hyTcgRequests.Remove(request);
            }
            HyTcgReply(window,session,reply);
        
            }
        }
        private async void HyTcgSaveUpgrade(PluginBrowserWindow window,string session,string request,string token,string png)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            bool began=false,saving=false;string cardId="";
            var reply=new Dictionary<string,object>{{"type","hytcgUpgradeSaved"},{"requestId",request},{"error",""}};
            try
            {
                token=HyTcgUpgradeToken(token);reply["upgradeToken"]=token;
                var intent=service.Economy.GetUpgrade(token);cardId=intent.Card.CardId;
                if(!hyTcgSaving.Add(cardId))throw new HyTcgException("Busy");saving=true;
                if(intent.Stage!="complete") {service.Economy.BeginUpgrade(token);began=true;}
                intent=service.Economy.GetUpgrade(token);
                if(intent.Stage!="authorized" && intent.Stage!="complete")throw new HyTcgException("UpgradeInvalid");
                var result=await Task.Run(()=>service.SaveUpgrade(intent.Card,png,token));
                HyTcgDeleteExports(service,result.ConsumedFilePaths.Concat(new[]{result.PreviousFilePath}));
                if(session==hyTcgSessionId)HyTcgForgetCards(result.ConsumedIds.Concat(new[]{cardId}));
                reply["card"]=result.Card;reply["path"]=result.FilePath;reply["consumedIds"]=result.ConsumedIds;reply["wallet"]=result.Wallet;
            }
            catch(Exception ex) {reply["error"]=HyTcgError(ex);reply["wallet"]=HyTcgWalletSafe(service);}
            finally {if(began)try {service.Economy.EndUpgrade(token);}catch { }if(saving&&session==hyTcgSessionId)hyTcgSaving.Remove(cardId);}
            HyTcgReply(window,session,reply);
        
            }
        }
        private async void HyTcgCollection(PluginBrowserWindow window,string session,string request,int offset,string rarity,string query,bool favoritesOnly,string sort)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            try
            {
                if(query.Length>180) query=query.Substring(0,180);
                if(rarity!="" && Array.IndexOf(HyTcgService.Rarities,rarity)<0) rarity="";
                var page=await Task.Run(()=>service.Collection(offset,rarity,query,favoritesOnly,sort));
                await Task.Run(()=>service.Economy.ApplyLifestylePreviews(page));
                HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgCollection"},{"requestId",request},{"page",page},{"error",""}});
            }
            catch(Exception ex) { HyTcgReply(window,session,new Dictionary<string,object>{{"type","hytcgCollection"},{"requestId",request},{"error",HyTcgError(ex)}}); }
        
            }
        }
    }
}
