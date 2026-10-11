using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HYROLauncherWeb
{
    internal partial class WebLauncherForm
    {
        private readonly HashSet<string> hyTcgDiscoveryRequests=new HashSet<string>();
        private bool HandleHyTcgExtra(PluginBrowserWindow window,string session,string type,Dictionary<string,object> message)
        {
            if(type!="hytcgDiscoveryState" && type!="hytcgDexSearch" && type!="hytcgDexSeries" && type!="hytcgDexArtwork" && type!="hytcgDexImage" && type!="hytcgDexProfile"
                && type!="hytcgLimitedBuy" && type!="hytcgAccountDelete" && type!="hytcgReadCard" && type!="hytcgRepairCards" && type!="hytcgCharacterInfo" && type!="hytcgDissolvePreview" && type!="hytcgMissionPlay" && type!="hytcgMissionMovie")return false;
            string request=GetString(message,"requestId");if(!Regex.IsMatch(request,@"^[A-Za-z0-9_-]{1,64}$"))return true;
            HyTcgExtra(window,session,request,type,message,hyTcgSession.Token);return true;
        }
        private async void HyTcgExtra(PluginBrowserWindow window,string session,string request,string type,Dictionary<string,object> message,CancellationToken cancellation)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            var reply=new Dictionary<string,object>{{"type",type},{"requestId",request},{"error",""}};
            bool acquired=false,tracked=false;
            try
            {
                if(type=="hytcgAccountDelete")
                {
                    if(hyTcgRequests.Count>0 || hyTcgSaving.Count>0 || hyTcgLifestyleRequests.Count>0)throw new HyTcgException("Busy");
                    if(GetString(message,"confirmation")!="delete")throw new HyTcgException("ConfirmationRequired");
                    service.Economy.ResetAccount("delete");HyTcgEndSession();hyTcgSession=new CancellationTokenSource();
                    hyTcgPending.Clear();hyTcgSaved.Clear();hyTcgCardTokens.Clear();hyTcgReservations.Clear();hyTcgDiscoveryRequests.Clear();
                    tcgMissionCache=null;tcgMovieUncommittedMs=0;tcgMovieLastMs=tcgMovieClock.ElapsedMilliseconds;
                    var page=EnsureTcgMissions();reply["missions"]=page.Items;reply["missionDay"]=page.Day;reply["wallet"]=service.Economy.Snapshot();
                    TcgStartMovieMonitor();
                }
                else if(type=="hytcgLimitedBuy")reply["wallet"]=service.Economy.BuyLimitedCredits(ClampInt(GetString(message,"count"),1,1,10));
                else if(type=="hytcgMissionPlay") {TcgMissionPlay(GetString(message,"gameId"));reply["wallet"]=service.Economy.Snapshot();}
                else if(type=="hytcgMissionMovie") {ShowBuiltInPlugin(BuiltInMoviePluginId);}
                else if(type=="hytcgRepairCards")
                {
                    if(hyTcgDiscoveryRequests.Count>=10 || !hyTcgDiscoveryRequests.Add(request))throw new HyTcgException("Busy");tracked=true;
                    var ids=HyTcgIds(message,"cardIds",2);var images=new List<object>();
                    using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                    {
                        timeout.CancelAfter(55000);
                        foreach(string id in ids)
                        {
                            timeout.Token.ThrowIfCancellationRequested();
                            try
                            {
                                var card=await Task.Run(()=>service.Economy.ReadOwnedCardMetadata(id),timeout.Token);
                                await Task.Run(()=>service.WarmDefaultArtwork(card,timeout.Token),timeout.Token);
                                string image=await Task.Run(()=>service.Economy.ReadLifestyleImage(id,true),timeout.Token);
                                images.Add(new {cardId=id,imageData=image});
                            }
                            catch(HyTcgException) { }
                        }
                    }
                    reply["items"]=images;
                }
                else if(type=="hytcgReadCard")
                {
                    string id=HyTcgCardId(GetString(message,"cardId"));reply["cardId"]=id;
                    var nativeCard=await Task.Run(()=>service.Economy.ReadOwnedCardMetadata(id),cancellation);
                    await Task.Run(()=>service.WarmDefaultArtwork(nativeCard,cancellation),cancellation);
                    reply["imageData"]=await Task.Run(()=>service.Economy.ReadLifestyleImage(id,false),cancellation);
                    reply["cosmetic"]=await Task.Run(()=>service.Economy.GetCardCosmetic(id),cancellation);
                }
                else
                {
                    if(hyTcgDiscoveryRequests.Count>=10 || !hyTcgDiscoveryRequests.Add(request))throw new HyTcgException("Busy");tracked=true;
                    var missions=EnsureTcgMissions();
                    using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                    {
                        timeout.CancelAfter(type=="hytcgDiscoveryState" || type=="hytcgDissolvePreview"?120000:60000);await hyTcgNetwork.WaitAsync(timeout.Token);acquired=true;
                        var config=service.LoadConfig();
                        if(type=="hytcgCharacterInfo")
                        {
                            string id=HyTcgCardId(GetString(message,"cardId"));HyTcgCard card;
                            if(!hyTcgPending.TryGetValue(id,out card))card=await Task.Run(()=>service.Economy.ReadOwnedCardMetadata(id),timeout.Token);
                            reply["info"]=await Task.Run(()=>service.CharacterInfo(card,timeout.Token),timeout.Token);
                            reply["cardId"]=id;reply["salvageValue"]=service.Economy.SalvageValue(card);
                        }
                        else if(type=="hytcgDissolvePreview")
                        {
                            var ids=HyTcgIds(message,"cardIds",100);
                            var cards=await Task.Run(()=>service.Economy.ReadOwnedCards(ids),timeout.Token);
                            await Task.Run(()=>service.WarmCharacterInfo(cards,timeout.Token),timeout.Token);
                            reply["values"]=cards.ToDictionary(card=>card.CardId,card=>service.Economy.SalvageValue(card));
                        }
                        else if(type=="hytcgDiscoveryState")
                        {
                            var banners=await Task.Run(()=>service.DiscoveryBanners(config,timeout.Token),timeout.Token);
                            reply["banners"]=banners.Items;reply["bannerDay"]=banners.Day;reply["stale"]=banners.Stale;
                            reply["missions"]=missions.Items;reply["missionDay"]=missions.Day;reply["wallet"]=service.Economy.Snapshot();reply["limitedCredits"]=service.Economy.Snapshot().LimitedCredits;
                        }
                        else if(type=="hytcgDexSearch" || type=="hytcgDexSeries")
                        {
                            string query=GetString(message,"query").Trim();if(query.Length>180)query=query.Substring(0,180);
                            int offset=ClampInt(GetString(message,"offset"),0,0,10000),seriesId=ClampInt(GetString(message,"seriesId"),0,0,1000000);
                            var result=await Task.Run(()=>type=="hytcgDexSeries"?service.DexSeriesCharacters(seriesId,offset,config,timeout.Token):service.DexSearch(query,GetString(message,"mode"),offset,timeout.Token),timeout.Token);
                            reply["characters"]=result.Characters;reply["series"]=result.Series;reply["offset"]=result.Offset;reply["total"]=result.Total;reply["hasMore"]=result.HasMore;
                        }
                        else if(type=="hytcgDexArtwork")
                        {
                            int id=ClampInt(GetString(message,"characterId"),0,1,1000000),page=ClampInt(GetString(message,"page"),1,1,50);
                            var result=await Task.Run(()=>service.DexArtworks(id,page,config,timeout.Token),timeout.Token);
                            reply["character"]=result.Character;reply["artworks"]=result.Items;reply["hasMore"]=result.HasMore;reply["page"]=result.Page;
                        }
                        else if(type=="hytcgDexImage")
                        {
                            int id=ClampInt(GetString(message,"characterId"),0,1,1000000),art=ClampInt(GetString(message,"artworkId"),0,1,Int32.MaxValue);
                            reply["imageData"]=await Task.Run(()=>service.DexArtworkImage(id,art,GetBool(message,"thumbnail"),config,timeout.Token),timeout.Token);
                        }
                        else if(type=="hytcgDexProfile")
                        {
                            int id=ClampInt(GetString(message,"characterId"),0,1,1000000);
                            reply["imageData"]=await Task.Run(()=>service.DexProfileImage(id,config,timeout.Token),timeout.Token);
                        }
                    }
                    cancellation.ThrowIfCancellationRequested();
                }
            }
            catch(Exception ex)
            {
                reply=new Dictionary<string,object>{{"type",type},{"requestId",request},{"error",HyTcgError(ex)},{"wallet",HyTcgWalletSafe(service)}};
                if(type=="hytcgDiscoveryState")try {var page=service.Economy.Missions();reply["missions"]=page.Items;reply["missionDay"]=page.Day;}catch { }
            }
            finally {if(acquired)hyTcgNetwork.Release();if(tracked && session==hyTcgSessionId)hyTcgDiscoveryRequests.Remove(request);}
            HyTcgReply(window,session,reply);
        
            }
        }
    }
}
