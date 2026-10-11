using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HYROLauncherWeb
{
    public sealed class HyTcgLifestyleCard
    {
        public string CardId { get; set; }
        public int CharacterId { get; set; }
        public string Name { get; set; }
        public string Series { get; set; }
        public string Rarity { get; set; }
        public string RarityColor { get; set; }
        public string Orientation { get; set; }
        public string ImageSource { get; set; }
        public bool Favorite { get; set; }
        public bool Busy { get; set; }
    }
    internal sealed partial class HyTcgEconomy
    {
        internal object LifestyleRoster(string query,int offset,int count)
        {
            if((query??"").Length>180 || offset<0 || offset>100000 || count<1 || count>500)throw new HyTcgException("SearchInvalid");
            return Read(delegate(Vault v,DateTime now) {
                string search=(query??"").Trim();
                var cards=CollectionIndex(v).Where(c=>search=="" || (c.Card.Name+" "+c.Card.Series+" "+c.Card.CardId).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
                    .OrderByDescending(c=>c.Card.CreatedAt).ThenBy(c=>c.Card.CardId,StringComparer.Ordinal).ToList();
                int start=Math.Min(offset,cards.Count),next=Math.Min(start+count,cards.Count);
                return new {Items=cards.Skip(start).Take(count).Select(c=>new HyTcgLifestyleCard {CardId=c.Card.CardId,CharacterId=c.Card.CharacterId,Name=c.Card.Name,Series=c.Card.Series,
                    Rarity=c.Card.Rarity,RarityColor=c.Card.RarityColor,Orientation=c.Card.Orientation,ImageSource=c.Card.ImageSource,Favorite=c.Favorite,Busy=c.UpgradeLocked}).ToList(),
                    Total=cards.Count,Offset=start,NextOffset=next,HasMore=next<cards.Count};
            });
        }
        internal string ReadLifestyleImage(string cardId,bool thumbnail)
        {
            return Read(delegate(Vault v,DateTime now) {return LifestyleImage(v,ImageOwner(v,cardId),thumbnail);});
        }
        internal object ReadLifestylePreview(string cardId,string frameId,string dyeId,string artworkId,bool originalArtwork,bool thumbnail)
        {return ReadLifestylePreview(cardId,frameId,dyeId,artworkId,originalArtwork,thumbnail,"",null);}
        internal object ReadLifestylePreview(string cardId,string frameId,string dyeId,string artworkId,bool originalArtwork,bool thumbnail,string frameItemId,Dictionary<string,object> frameDraft)
        {return ReadLifestylePreview(cardId,frameId,dyeId,artworkId,originalArtwork,thumbnail,frameItemId,frameDraft,"");}
        internal object ReadLifestylePreview(string cardId,string frameId,string dyeId,string artworkId,bool originalArtwork,bool thumbnail,string frameItemId,Dictionary<string,object> frameDraft,string previewOrientation)
        {return ReadLifestylePreview(cardId,frameId,dyeId,artworkId,originalArtwork,thumbnail,frameItemId,frameDraft,previewOrientation,null);}
        internal object ReadLifestylePreview(string cardId,string frameId,string dyeId,string artworkId,bool originalArtwork,bool thumbnail,string frameItemId,Dictionary<string,object> frameDraft,string previewOrientation,Dictionary<string,object> position)
        {
            return Read(delegate(Vault v,DateTime now) {
                Owned owned=ImageOwner(v,cardId);EnsureCreative(v);EnsureStudio(v.Creative);
                if(frameDraft!=null || !String.IsNullOrEmpty(frameItemId) || (!String.IsNullOrEmpty(frameId) && frameId!="classic"))throw new HyTcgException("FrameRetired");
                frameId="classic";
                var previous=RarityCosmetic(v.Creative.Cosmetics.FirstOrDefault(c=>c.CardId==cardId));
                if(String.IsNullOrEmpty(dyeId) && previous!=null)dyeId=previous.DyeId??"";
                var dye=String.IsNullOrEmpty(dyeId)?null:FrameDye(dyeId);
                if(!String.IsNullOrEmpty(dyeId))
                {
                    int quantity;
                    if(dye==null || ((!v.Creative.Dyes.TryGetValue(dyeId,out quantity) || quantity<1) && (previous==null || previous.DyeId!=dyeId)))throw new HyTcgException("DyeInsufficient");
                }
                if(originalArtwork)artworkId="";
                else if(artworkId==null)artworkId=previous==null?"":previous.ArtworkId;
                bool legacy=previous!=null && previous.DyeId==null && previous.Frame==frameId && previous.ArtworkId==artworkId;
                var style=new HyTcgCosmetic {CardId=cardId,Frame=frameId,FrameItemId=frameItemId??"",DyeId=legacy && dye==null?null:dyeId,
                    Dye=dye==null?(legacy?previous.Dye:""):dye.Color,Mystic=dye!=null && dye.Mystic,
                    Morph=dye==null && legacy?previous.Morph:"",MorphMode=dye==null && legacy?previous.MorphMode:"none",ArtworkId=artworkId??"",
                    ArtworkScale=CreativeNumber(position??new Dictionary<string,object>(),"artworkScale",previous==null?1:previous.ArtworkScale,1,4),
                    ArtworkX=CreativeNumber(position??new Dictionary<string,object>(),"artworkX",previous==null?0:previous.ArtworkX,-1,1),
                    ArtworkY=CreativeNumber(position??new Dictionary<string,object>(),"artworkY",previous==null?0:previous.ArtworkY,-1,1)};
                byte[] container=ReadOwnedContainer(v,owned);string image;
                if(!imageDetailCache.TryGet(owned.PngHash,out image))
                {image="data:image/png;base64,"+Convert.ToBase64String(DecryptOwnedImage(owned,container));imageDetailCache.Add(owned.PngHash,image);}
                string artwork=String.IsNullOrEmpty(style.ArtworkId)?null:"data:image/png;base64,"+Convert.ToBase64String(ReadArtworkPng(v,FindArtwork(v.Creative,style.ArtworkId)));
                bool sourceArtwork=artwork==null; if(sourceArtwork)artwork=DefaultArtwork(owned.Card,style.ArtworkScale>1 || style.ArtworkX!=0 || style.ArtworkY!=0);
                return new {imageData=HyTcgCosmeticRenderer.Render(owned.Card,image,style,artwork,thumbnail,null,null,sourceArtwork),cosmetic=style};
            });
        }
        private string LifestyleImage(Vault v,Owned owned,bool thumbnail)
        {
            var card=owned.Card;var style=v.Creative==null?null:RarityCosmetic(v.Creative.Cosmetics.FirstOrDefault(c=>c.CardId==card.CardId));
            if(style==null && thumbnail && !HyTcgCosmeticRenderer.NeedsDefaultRecomposition(card))
            {
                var items=new List<HyTcgCollectionItem> {new HyTcgCollectionItem {Card=card}};
                PopulateImagePreviews(v,items);if(items.Count==0)throw new HyTcgException("CardUnavailable");return items[0].ImagePath;
            }
            byte[] container=ReadOwnedContainer(v,owned);string image;
            if(!imageDetailCache.TryGet(owned.PngHash,out image))
            {image="data:image/png;base64,"+Convert.ToBase64String(DecryptOwnedImage(owned,container));imageDetailCache.Add(owned.PngHash,image);}
            string artwork=style==null || String.IsNullOrEmpty(style.ArtworkId)?null:"data:image/png;base64,"+Convert.ToBase64String(ReadArtworkPng(v,FindArtwork(v.Creative,style.ArtworkId)));
            bool sourceArtwork=artwork==null;if(sourceArtwork)artwork=DefaultArtwork(card,style!=null && (style.ArtworkScale>1 || style.ArtworkX!=0 || style.ArtworkY!=0));
            return HyTcgCosmeticRenderer.Render(card,image,style,artwork,thumbnail,null,null,sourceArtwork);
        }
        internal void ApplyLifestylePreviews(HyTcgCollectionPage page)
        {
            Read(delegate(Vault v,DateTime now) {
            var styles=v.Creative==null?new Dictionary<string,HyTcgCosmetic>():v.Creative.Cosmetics.ToDictionary(c=>c.CardId,StringComparer.Ordinal);
            foreach(var item in page.Items)
            {
                try {HyTcgCosmetic style;if(styles.TryGetValue(item.Card.CardId,out style))item.Cosmetic=RarityCosmetic(style);
                    if(style!=null || HyTcgCosmeticRenderer.NeedsDefaultRecomposition(item.Card))item.ImagePath=LifestyleImage(v,ImageOwner(v,item.Card.CardId),true);}
                catch(HyTcgException) { } // Original protected card remains viewable if a cosmetic file is unavailable.
            }
            return true;
            });
        }
    }

    internal static class HyTcgArtworkImport
    {
        internal const int MaxBytes=8*1024*1024;
        internal static bool PublicAddress(IPAddress address)
        {
            return HyTcgArtworkDownload.AllowedAddress(address);
        }
        internal static Uri ValidateUrl(string text)
        {
            Uri uri;if(String.IsNullOrWhiteSpace(text) || text.Length>2048 || !Uri.TryCreate(text,UriKind.Absolute,out uri)
                || uri.Scheme!="https" || uri.Port!=443 || uri.UserInfo!="" || uri.Host.Length<3 || uri.Host.EndsWith(".",StringComparison.Ordinal)
                || uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase) || (uri.HostNameType!=UriHostNameType.IPv6 && uri.Host.IndexOf('.')<0))throw new HyTcgException("ArtworkUrlInvalid");
            IPAddress literal;if(IPAddress.TryParse(uri.Host.Trim('[',']'),out literal) && !PublicAddress(literal))throw new HyTcgException("ArtworkUrlInvalid");
            return uri;
        }
        internal static byte[] Fetch(string url,CancellationToken token)
        { return HyTcgArtworkDownload.Fetch(url,token); }
        internal static byte[] Drawing(string value)
        {
            const string prefix="data:image/png;base64,";
            if(value==null || !value.StartsWith(prefix,StringComparison.Ordinal) || value.Length>MaxBytes*4/3+100)throw new HyTcgException("ArtworkInvalid");
            try {byte[] bytes=Convert.FromBase64String(value.Substring(prefix.Length));if(bytes.Length>MaxBytes)throw new HyTcgException("ArtworkInvalid");return bytes;}
            catch(FormatException) {throw new HyTcgException("ArtworkInvalid");}
        }
        internal static byte[] FileBytes(string path,CancellationToken token)
        {
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var output=new MemoryStream())
            {
                if(file.Length<24 || file.Length>MaxBytes)throw new HyTcgException("ArtworkInvalid");
                byte[] buffer=new byte[8192];int count;
                while((count=file.Read(buffer,0,buffer.Length))>0)
                {token.ThrowIfCancellationRequested();if(output.Length+count>MaxBytes)throw new HyTcgException("ArtworkInvalid");output.Write(buffer,0,count);}
                return output.ToArray();
            }
        }
    }

    internal partial class WebLauncherForm
    {
        private readonly SemaphoreSlim hyTcgLifestyleGate=new SemaphoreSlim(2,2);
        private readonly HashSet<string> hyTcgLifestyleRequests=new HashSet<string>();
        private bool HandleHyTcgLifestyle(PluginBrowserWindow window,string session,string type,Dictionary<string,object> input)
        {
            if(type!="hytcgLifestyle" && type!="hytcgLifestyleImage")return false;
            string request=GetString(input,"requestId");
            if(!Regex.IsMatch(request,@"^[A-Za-z0-9_-]{1,64}$"))return true;
            if(hyTcgLifestyleRequests.Count>=8 || !hyTcgLifestyleRequests.Add(request))
            {HyTcgReply(window,session,new Dictionary<string,object>{{"type",type},{"requestId",request},{"error","Busy"}});return true;}
            HyTcgLifestyle(window,session,type,request,input,hyTcgSession.Token);return true;
        }
        private async void HyTcgLifestyle(PluginBrowserWindow window,string session,string type,string request,Dictionary<string,object> input,CancellationToken cancellation)
        {
            var service=hyTcg;
            using(HyTcgOperation(service)) {
            var reply=new Dictionary<string,object>{{"type",type},{"requestId",request},{"error",""}};bool acquired=false;
            try
            {
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    timeout.CancelAfter(45000);await hyTcgLifestyleGate.WaitAsync(timeout.Token);acquired=true;
                    var token=timeout.Token;string action=GetString(input,"action");
                    if(type=="hytcgLifestyleImage")
                    {
                        string id=HyTcgCardId(GetString(input,"cardId")),artworkId=GetString(input,"artworkId");
                        // A selected card must still belong to this native account.
                        var nativeCard=await Task.Run(()=>service.Economy.ReadOwnedCardMetadata(id),token);
                        if(!GetBool(input,"original"))
                        {
                            var applied=await Task.Run(()=>service.Economy.GetCardCosmetic(id),token);
                            bool positioned=applied!=null && (applied.ArtworkScale>1 || applied.ArtworkX!=0 || applied.ArtworkY!=0);
                            double positionValue;
                            foreach(string key in new[]{"artworkScale","artworkX","artworkY"})if(input.ContainsKey(key) && Double.TryParse(GetString(input,key),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out positionValue)
                                && positionValue!=(key=="artworkScale"?1:0))positioned=true;
                            await Task.Run(()=>service.WarmDefaultArtwork(nativeCard,token,positioned),token);
                        }
                        string previewFrame=GetString(input,"previewFrameId");
                        object rawDraft;Dictionary<string,object> previewDraft=input.TryGetValue("previewFrameDraft",out rawDraft)?rawDraft as Dictionary<string,object>:null;
                        if(input.ContainsKey("previewFrameDraft") && previewDraft==null)throw new HyTcgException("FrameInvalid");
                        string previewItem=GetString(input,"previewFrameItemId");
                        if(previewFrame!="" || previewItem!="" || previewDraft!=null)
                        {
                            var preview=await Task.Run(()=>service.Economy.ReadLifestylePreview(id,previewFrame,GetString(input,"previewDyeId"),input.ContainsKey("previewArtworkId")?GetString(input,"previewArtworkId"):null,GetBool(input,"previewOriginalArtwork"),GetBool(input,"thumbnail"),previewItem,previewDraft,GetString(input,"previewOrientation"),input),token);
                            reply["imageData"]=preview.GetType().GetProperty("imageData").GetValue(preview,null);
                            reply["cosmetic"]=preview.GetType().GetProperty("cosmetic").GetValue(preview,null);
                        }
                        else reply["imageData"]=await Task.Run(()=>artworkId!=""?service.Economy.ReadCustomArtwork(artworkId):GetBool(input,"original")?service.Economy.ReadCardImage(id):service.Economy.ReadLifestyleImage(id,GetBool(input,"thumbnail")),token);
                    }
                    else if(action=="roster")
                    {
                        var nativeRoster=await Task.Run(()=>service.Economy.LifestyleRoster(GetString(input,"query"),ClampInt(GetString(input,"offset"),0,0,100000),ClampInt(GetString(input,"count"),500,1,500)),token);
                        CopyLifestyleRoster(reply,nativeRoster);
                        var profiles=(HyTcgGameplayRosterPage)await Task.Run(()=>service.Economy.GameplayRoster(GetString(input,"query"),ClampInt(GetString(input,"offset"),0,0,100000),500),token);
                        reply["profilePage"]=profiles;reply["profiles"]=profiles.Items;
                    }
                    else if(action=="profile")
                    {
                        string id=HyTcgCardId(GetString(input,"cardId"));
                        reply["profile"]=await Task.Run(()=>service.Economy.GameplayProfileSnapshot(id),token);
                        CopyLifestyleRoster(reply,await Task.Run(()=>service.Economy.LifestyleRoster(id,0,1),token));
                        reply["card"]=((IEnumerable<HyTcgLifestyleCard>)reply["roster"]).FirstOrDefault();
                    }
                    else if(action=="quote")reply["quote"]=await Task.Run(()=>service.Economy.CreativeQuote(input),token);
                    else
                    {
                        if(action=="artworkPick" || action=="artworkUrl" || action=="artworkDraw")
                        {
                            byte[] bytes;
                            if(action=="artworkPick")
                            {
                                using(var dialog=new OpenFileDialog {Title="Cosmetic Studio · Artwork",Filter="Images|*.png;*.jpg;*.jpeg",Multiselect=false,CheckFileExists=true})
                                {
                                    if(dialog.ShowDialog(window)!=DialogResult.OK)throw new HyTcgException("ArtworkCancelled");
                                    string selectedFile=dialog.FileName;
                                    bytes=await Task.Run(()=>HyTcgArtworkImport.FileBytes(selectedFile,token),token);
                                }
                            }
                            else if(action=="artworkUrl")bytes=await Task.Run(()=>HyTcgArtworkImport.Fetch(GetString(input,"url"),token),token);
                            else bytes=HyTcgArtworkImport.Drawing(GetString(input,"png"));
                            string id=await Task.Run(()=>service.Economy.StoreCustomArtwork(bytes),token);
                            reply["artworkId"]=id;reply["imageData"]=await Task.Run(()=>service.Economy.ReadCustomArtwork(id),token);
                        }
                        else if(action=="albumSave" || action=="albumDelete" || action=="albumCreate" || action=="albumSlots" || action=="albumRestyle" || action=="cosmeticSave" || action=="cosmeticReset" || action=="dyeBoosterBuy" || action=="frameCreate" || action=="frameBuy")
                            await Task.Run(()=>service.Economy.CreativeAction(action,input),token);
                        else if(action=="expeditionStart" || action=="expeditionClaim" || action=="inventoryUse" || action=="fight" || action=="date" || action=="battleTurn" || action=="battleForfeit" || action=="skillsSave")
                        {
                            var result=(HyTcgGameplayActionResult)await Task.Run(()=>service.Economy.GameplayAction(action,input),token);
                            reply["result"]=result.Result;reply["wallet"]=result.Wallet;
                        }
                        else if(action!="snapshot")throw new HyTcgException("GameplayInvalid");
                        await Task.Run(()=>FillLifestyleSnapshot(reply,service),token);
                    }
                }
            }
            catch(Exception ex) {reply["error"]=HyTcgError(ex);reply["wallet"]=HyTcgWalletSafe(service);}
            finally {if(acquired)hyTcgLifestyleGate.Release();if(session==hyTcgSessionId)hyTcgLifestyleRequests.Remove(request);}
            HyTcgReply(window,session,reply);
        
            }
        }
        private void FillLifestyleSnapshot(Dictionary<string,object> reply,HyTcgService service)
        {
            reply["gameplay"]=service.Economy.GameplaySnapshot();reply["creative"]=service.Economy.CreativeSnapshot();
            reply["wallet"]=service.Economy.Snapshot();CopyLifestyleRoster(reply,service.Economy.LifestyleRoster("",0,500));
            var missions=service.Economy.Missions();reply["missions"]=missions.Items;reply["missionDay"]=missions.Day;
        }
        private static void CopyLifestyleRoster(Dictionary<string,object> reply,object native)
        {
            var type=native.GetType();reply["roster"]=type.GetProperty("Items").GetValue(native,null);
            reply["rosterTotal"]=type.GetProperty("Total").GetValue(native,null);reply["nextOffset"]=type.GetProperty("NextOffset").GetValue(native,null);reply["hasMore"]=type.GetProperty("HasMore").GetValue(native,null);
        }
    }
}
