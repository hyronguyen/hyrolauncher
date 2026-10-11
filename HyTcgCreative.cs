using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HYROLauncherWeb
{
    // Cosmetic state is separate from native card fingerprints and economic signatures.
    // Artwork metadata is in the protected account vault; picture bytes stay local.
    public sealed class HyTcgCreativeState
    {
        public List<HyTcgAlbum> Albums { get; set; }
        public List<HyTcgCosmetic> Cosmetics { get; set; }
        public List<HyTcgCustomArtwork> Artworks { get; set; }
        public List<string> ResetArtworkIds { get; set; }
        public int StudioVersion { get; set; }
        public Dictionary<string,int> Dyes { get; set; }
        public List<string> OwnedFrameIds { get; set; }
        public long StudioSequence { get; set; }
        public List<HyTcgStudioReceipt> StudioReceipts { get; set; }
        public HyTcgStudioReceipt LastStudioReceipt { get; set; }
        public List<HyTcgCosmetic> ResetCosmetics { get; set; }
        public List<HyTcgFrameItem> FrameItems { get; set; }
    }
    public sealed class HyTcgAlbum
    {
        public int Version { get; set; }
        public int Capacity { get; set; }
        public string Cover { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Background { get; set; }
        public List<string> CardIds { get; set; }
        public string CreatedAt { get; set; }
    }
    public sealed class HyTcgCosmetic
    {
        public string CardId { get; set; }
        public string Frame { get; set; }
        public string Dye { get; set; }
        public string Morph { get; set; }
        public string MorphMode { get; set; }
        public string ArtworkId { get; set; }
        public string DyeId { get; set; }
        public bool Mystic { get; set; }
        public string FrameItemId { get; set; }
        public double ArtworkScale { get; set; }
        public double ArtworkX { get; set; }
        public double ArtworkY { get; set; }
    }
    public sealed class HyTcgFrameItem
    {
        public string Id { get; set; }
        public string FrameId { get; set; }
        public string Design { get; set; }
        public string PortraitOverlay { get; set; }
        public string LandscapeOverlay { get; set; }
        public string Name { get; set; }
        public string ArtworkId { get; set; }
        public string Description { get; set; }
        public HyTcgFrameLayout Portrait { get; set; }
        public HyTcgFrameLayout Landscape { get; set; }
        public string AppliedCardId { get; set; }
        public string CreatedAt { get; set; }
        public bool Grandfathered { get; set; }
        public bool Available { get {return AppliedCardId=="";} }
    }
    public sealed class HyTcgStudioQuote
    {
        public int ArtworkColors { get; set; }
        public int ArtworkPixels { get; set; }
        public int ArtworkGems { get; set; }
        public int TotalGems { get; set; }
        public bool FrameItemConsumed { get; set; }
        public bool DyeConsumed { get; set; }
        public bool NoOp { get; set; }
    }
    public sealed class HyTcgInventoryEntry
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public string Color { get; set; }
        public bool Mystic { get; set; }
        public string FrameId { get; set; }
        public string FrameItemId { get; set; }
    }
    public sealed class HyTcgFrameDye
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public bool Mystic { get; set; }
        public int Count { get; set; }
    }
    public sealed class HyTcgStudioReceipt
    {
        public string AlbumId { get; set; }
        public int ArtworkColors { get; set; }
        public string OperationId { get; set; }
        public string InputHash { get; set; }
        public string Action { get; set; }
        public string DyeId { get; set; }
        public string FrameId { get; set; }
        public string CardId { get; set; }
        public int GemsSpent { get; set; }
        public bool NoOp { get; set; }
        public string CreatedAt { get; set; }
        public string FrameItemId { get; set; }
        public int ArtworkPixels { get; set; }
        public int HashVersion { get; set; }
    }
    public sealed class HyTcgCustomArtwork
    {
        public int? Colors { get; set; }
        public string Id { get; set; }
        public string ContainerHash { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Bytes { get; set; }
        public string CreatedAt { get; set; }
        public int? VisiblePixels { get; set; }
    }
    public sealed class HyTcgCreativePreset
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    internal sealed partial class HyTcgEconomy
    {
        private const int MaxCreativeAlbums=24,MaxAlbumSlots=120,MaxCreativeArtworks=64;
        internal const int AlbumPrice=2000,AlbumSlotPrice=200,AlbumRestylePrice=200;
        private const int MaxStudioCharge=16384;
        internal const int DyeBoosterPrice=50,ArtworkApplyPrice=0,FrameCreationPrice=2000,ArtworkPixelsPerGem=10000;
        private const int MaxDyeQuantity=999,MaxStudioReceipts=128,MaxOwnedFrames=64;
        private const long MaxStudioSequence=1000000000000L;
        private const int MaxArtworkInputBytes=8*1024*1024,MaxArtworkPngBytes=4*1024*1024,MaxArtworkContainerBytes=MaxArtworkPngBytes+65536;
        private static readonly string[] creativeFrames={"classic","petal","arcane","circuit","royal"};
        private static readonly string[] creativeBackgrounds={"midnight","sakura","forest","aurora","royal","paper"};
        private static readonly string[] creativeMorphModes={"none","hue","multiply","color","screen"};
        private static readonly byte[] artworkMagic=Encoding.ASCII.GetBytes("HYTCG-ART1\0");

        internal object CreativeSnapshot()
        {
            return Read(delegate(Vault v,DateTime now) { return CreativeView(v); });
        }
        internal HyTcgCosmetic GetCardCosmetic(string cardId)
        {
            return Read(delegate(Vault v,DateTime now)
            {
                // Read native ownership metadata, including while a companion is busy.
                // Rendering a style does not initialize or mutate account state.
                ImageOwner(v,cardId);
                HyTcgCosmetic style=v.Creative==null?null:EnsureCreative(v).Cosmetics.FirstOrDefault(c=>c.CardId==cardId);
                if(style==null)return null;
                return RarityCosmetic(style);
            });
        }
        internal object CreativeAction(string action,Dictionary<string,object> input)
        {
            if(input==null)throw new HyTcgException("CosmeticInvalid");
            // Frame geometry now follows native rarity. Historical frame records
            // remain readable for account compatibility, but cannot be created or attached.
            if(action=="frameCreate" || action=="frameBuy")throw new HyTcgException("FrameRetired");
            return Write(delegate(Vault v,DateTime now)
            {
                HyTcgCreativeState state=EnsureCreative(v);
                bool studio=action=="dyeBoosterBuy" || action=="cosmeticSave" || action=="albumCreate" || action=="albumSlots" || action=="albumRestyle";
                string operation="",inputHash="";HyTcgStudioReceipt receipt=null;
                if(studio)
                {
                    operation=CreativeText(input,"operationId","");long sequence=StudioOperationSequence(operation);
                    inputHash=StudioInputHash(action,input);
                    receipt=state.StudioReceipts.FirstOrDefault(r=>r.OperationId==operation);
                    if(receipt!=null)
                    {
                        if(receipt.HashVersion==0 && !input.ContainsKey("artworkScale") && !input.ContainsKey("artworkX") && !input.ContainsKey("artworkY"))inputHash=LegacyStudioInputHash(action,input);
                        if(receipt.InputHash!=inputHash || receipt.Action!=action)throw new HyTcgException("StudioReplayMismatch");
                        state.LastStudioReceipt=receipt;return CreativeView(v);
                    }
                    if(sequence<=state.StudioSequence)throw new HyTcgException("StudioRequestExpired");
                    if(sequence!=state.StudioSequence+1 || sequence>MaxStudioSequence)throw new HyTcgException("StudioOperationInvalid");
                    receipt=new HyTcgStudioReceipt {OperationId=operation,InputHash=inputHash,HashVersion=2,Action=action,DyeId="",FrameId="",FrameItemId="",CardId="",CreatedAt=now.ToString("o",CultureInfo.InvariantCulture)};
                }
                if(action=="albumCreate")
                {
                    string name=CreativeText(input,"name","").Trim(),background=CreativeText(input,"background","midnight"),cover=CreativeText(input,"cover","midnight");
                    if(!ValidAlbumName(name) || !creativeBackgrounds.Contains(background) || !creativeBackgrounds.Contains(cover))throw new HyTcgException("AlbumInvalid");
                    if(state.Albums.Count>=MaxCreativeAlbums)throw new HyTcgException("AlbumLimit");SpendStudioGems(v,AlbumPrice);
                    var album=new HyTcgAlbum {Version=1,Capacity=4,Cover=cover,Id="AL-"+Guid.NewGuid().ToString("N"),Name=name,Background=background,
                        CardIds=Enumerable.Repeat("",4).ToList(),CreatedAt=now.ToString("o",CultureInfo.InvariantCulture)};
                    state.Albums.Add(album);receipt.AlbumId=album.Id;receipt.GemsSpent=AlbumPrice;
                }
                else if(action=="albumSlots" || action=="albumRestyle")
                {
                    string id=CreativeText(input,"albumId","");var album=state.Albums.FirstOrDefault(a=>a.Id==id);if(album==null)throw new HyTcgException("AlbumUnavailable");
                    receipt.AlbumId=id;
                    if(action=="albumSlots")
                    {
                        int count=GameplayInt(input,"count",1,12);if(album.Capacity>MaxAlbumSlots-count)throw new HyTcgException("AlbumCardsLimit");
                        receipt.GemsSpent=count*AlbumSlotPrice;SpendStudioGems(v,receipt.GemsSpent);album.Capacity+=count;album.CardIds.AddRange(Enumerable.Repeat("",count));
                    }
                    else
                    {
                        string background=CreativeText(input,"background",album.Background),cover=CreativeText(input,"cover",album.Cover);
                        if(!creativeBackgrounds.Contains(background) || !creativeBackgrounds.Contains(cover))throw new HyTcgException("AlbumInvalid");
                        receipt.NoOp=background==album.Background && cover==album.Cover;
                        if(!receipt.NoOp){SpendStudioGems(v,AlbumRestylePrice);receipt.GemsSpent=AlbumRestylePrice;album.Background=background;album.Cover=cover;}
                    }
                }
                else if(action=="albumSave")
                {
                    string albumId=CreativeText(input,"albumId",""),name=CreativeText(input,"name","").Trim();
                    if(!ValidAlbumName(name))throw new HyTcgException("AlbumInvalid");
                    List<string> slots=CreativeSlots(input);
                    foreach(string cardId in slots.Where(id=>id!=""))CreativeOwned(v,cardId,true);
                    HyTcgAlbum album=null;
                    if(albumId!="")
                    {
                        if(!ValidAlbumId(albumId))throw new HyTcgException("AlbumInvalid");
                        album=state.Albums.FirstOrDefault(a=>a.Id==albumId);
                        if(album==null)throw new HyTcgException("AlbumUnavailable");
                    }
                    else throw new HyTcgException("AlbumPurchaseRequired");
                    if(slots.Count>album.Capacity)throw new HyTcgException("AlbumCardsLimit");
                    string background=CreativeText(input,"background",album.Background),cover=CreativeText(input,"cover",album.Cover);
                    if(background!=album.Background || cover!=album.Cover)throw new HyTcgException("AlbumRestyleRequired");
                    album.Name=name;album.CardIds=slots.Concat(Enumerable.Repeat("",album.Capacity-slots.Count)).ToList();
                }
                else if(action=="albumDelete")
                {
                    string albumId=CreativeText(input,"albumId","");
                    if(!ValidAlbumId(albumId))throw new HyTcgException("AlbumInvalid");
                    if(state.Albums.RemoveAll(a=>a.Id==albumId)==0)throw new HyTcgException("AlbumUnavailable");
                }
                else if(action=="cosmeticSave")
                {
                    var plan=PlanCosmetic(v,input);var cosmetic=plan.Cosmetic;string cardId=cosmetic.CardId;
                    receipt.NoOp=plan.Quote.NoOp;receipt.CardId=cardId;receipt.FrameId=cosmetic.Frame;receipt.DyeId=cosmetic.DyeId??"";
                    receipt.FrameItemId=cosmetic.FrameItemId??"";receipt.ArtworkPixels=plan.Quote.ArtworkPixels;receipt.ArtworkColors=plan.Quote.ArtworkColors;
                    if(!plan.Quote.NoOp)
                    {
                        SpendStudioGems(v,plan.Quote.TotalGems);receipt.GemsSpent=plan.Quote.TotalGems;
                        if(plan.Quote.DyeConsumed)state.Dyes[cosmetic.DyeId]--;
                        state.Cosmetics.RemoveAll(c=>c.CardId==cardId);state.Cosmetics.Add(cosmetic);
                        state.ResetCosmetics.RemoveAll(c=>c.CardId==cardId);
                    }
                }
                else if(action=="dyeBoosterBuy")
                {
                    HyTcgFrameDye dye;using(var rng=RandomNumberGenerator.Create())dye=RollFrameDye(RandomIndex(rng,64),RandomIndex(rng,100));
                    int quantity;state.Dyes.TryGetValue(dye.Id,out quantity);if(quantity>=MaxDyeQuantity)throw new HyTcgException("DyeLimit");
                    SpendStudioGems(v,DyeBoosterPrice);state.Dyes[dye.Id]=quantity+1;receipt.DyeId=dye.Id;receipt.GemsSpent=DyeBoosterPrice;
                }
                else if(action=="cosmeticReset")
                {
                    string cardId=CreativeText(input,"cardId","");CreativeOwned(v,cardId);
                    var previous=state.Cosmetics.FirstOrDefault(c=>c.CardId==cardId);
                    if(previous!=null) {state.ResetCosmetics.RemoveAll(c=>c.CardId==cardId);state.ResetCosmetics.Add(previous);}
                    state.Cosmetics.RemoveAll(c=>c.CardId==cardId);
                }
                else throw new HyTcgException("CosmeticInvalid");
                if(studio)
                {
                    state.StudioSequence=StudioOperationSequence(operation);state.StudioReceipts.Add(receipt);
                    if(state.StudioReceipts.Count>MaxStudioReceipts)state.StudioReceipts.RemoveAt(0);state.LastStudioReceipt=receipt;
                }
                return CreativeView(v);
            });
        }
        private Owned CreativeOwned(Vault v,string cardId,bool allowGameplayBusy=false)
        {
            if(!ValidCreativeCardId(cardId))throw new HyTcgException("CardUnavailable");
            Owned owned=VerifiedOwned(v,cardId,true);
            // Albums only arrange owned cards. An expedition can keep its existing
            // team lock while its card is placed in a display; upgrades still lock it.
            bool upgrading=v.Upgrades.Any(u=>u.Stage!="complete" && (u.TargetId==cardId || u.MaterialIds.Contains(cardId)));
            if(upgrading || (!allowGameplayBusy && IsLocked(v,cardId)))throw new HyTcgException("CardLocked");
            return owned;
        }
        private sealed class CosmeticPlan
        {internal HyTcgCosmetic Cosmetic;internal HyTcgStudioQuote Quote;}
        private static HyTcgCosmetic RarityCosmetic(HyTcgCosmetic source)
        {
            if(source==null)return null;
            return new HyTcgCosmetic {CardId=source.CardId,Frame="classic",FrameItemId="",Dye=source.Dye,DyeId=source.DyeId,
                Morph=source.Morph,MorphMode=source.MorphMode,ArtworkId=source.ArtworkId,Mystic=source.Mystic,
                ArtworkScale=source.ArtworkScale==0?1:source.ArtworkScale,ArtworkX=source.ArtworkX,ArtworkY=source.ArtworkY};
        }
        internal object CreativeQuote(Dictionary<string,object> input)
        {
            if(input==null)throw new HyTcgException("CosmeticInvalid");
            return Read(delegate(Vault v,DateTime now) {EnsureCreative(v);return PlanCosmetic(v,input).Quote;});
        }
        private CosmeticPlan PlanCosmetic(Vault v,Dictionary<string,object> input)
        {
            var state=EnsureCreative(v);string cardId=CreativeText(input,"cardId","");CreativeOwned(v,cardId);
            var active=RarityCosmetic(state.Cosmetics.FirstOrDefault(c=>c.CardId==cardId));var remembered=RarityCosmetic(state.ResetCosmetics.FirstOrDefault(c=>c.CardId==cardId));
            bool restore=CreativeBool(input,"restorePrevious");if(restore && remembered==null)throw new HyTcgException("CosmeticInvalid");
            var previous=restore?remembered:active;string itemId=CreativeText(input,"frameItemId","");
            string frame=CreativeText(input,"frame","classic");
            if(itemId!="" || frame!="classic")throw new HyTcgException("FrameRetired");
            string dyeId=CreativeText(input,"dyeId","");if(dyeId=="" && previous!=null)dyeId=previous.DyeId??"";
            var dye=dyeId==""?null:FrameDye(dyeId);if(dyeId!="" && dye==null)throw new HyTcgException("DyeInvalid");
            string artworkId=CreativeText(input,"artworkId",previous==null?"":previous.ArtworkId);
            bool legacy=previous!=null && previous.DyeId==null && previous.Frame==frame && (previous.FrameItemId??"")==itemId && previous.ArtworkId==artworkId;
            var cosmetic=new HyTcgCosmetic {CardId=cardId,Frame=frame,FrameItemId=itemId,DyeId=legacy && dye==null?null:dyeId,
                Dye=dye==null?(legacy?previous.Dye:""):dye.Color,Mystic=dye!=null && dye.Mystic,
                Morph=dye==null && legacy?previous.Morph:"",MorphMode=dye==null && legacy?previous.MorphMode:"none",ArtworkId=artworkId,
                ArtworkScale=CreativeNumber(input,"artworkScale",previous==null?1:previous.ArtworkScale,1,4),
                ArtworkX=CreativeNumber(input,"artworkX",previous==null?0:previous.ArtworkX,-1,1),
                ArtworkY=CreativeNumber(input,"artworkY",previous==null?0:previous.ArtworkY,-1,1)};
            if(CreativeText(input,"dye",cosmetic.Dye).ToUpperInvariant()!=cosmetic.Dye || CreativeText(input,"morph",cosmetic.Morph).ToUpperInvariant()!=cosmetic.Morph
                || CreativeText(input,"morphMode",cosmetic.MorphMode)!=cosmetic.MorphMode)throw new HyTcgException("DyeInvalid");
            if(!ValidCosmetic(cosmetic) || (restore && !SameCosmetic(remembered,cosmetic)))throw new HyTcgException("CosmeticInvalid");
            int pixels=0,colors=0;if(artworkId!="") {var art=FindArtwork(state,artworkId);byte[] png=ReadArtworkPng(v,art);pixels=CountArtworkPixels(png,out colors);art.VisiblePixels=pixels;art.Colors=colors;}
            var basis=active??(SameCosmetic(remembered,cosmetic)?remembered:null);bool same=SameCosmetic(active,cosmetic);
            bool consumeDye=!same && dye!=null && (basis==null || basis.DyeId!=dye.Id);int quantity;
            if(consumeDye && (!state.Dyes.TryGetValue(dye.Id,out quantity) || quantity<1))throw new HyTcgException("DyeInsufficient");
            int cost=!same && artworkId!="" && (basis==null || basis.ArtworkId!=artworkId)?ArtworkPixelColorPrice(pixels,colors):0;
            return new CosmeticPlan {Cosmetic=cosmetic,Quote=new HyTcgStudioQuote {ArtworkPixels=pixels,ArtworkColors=colors,ArtworkGems=cost,TotalGems=cost,
                DyeConsumed=consumeDye,FrameItemConsumed=false,NoOp=same}};
        }
        private static HyTcgFrameItem FindFrameItem(HyTcgCreativeState state,string id)
        {
            if(!ValidFrameItemId(id))throw new HyTcgException("FrameUnavailable");
            var item=state.FrameItems.FirstOrDefault(f=>f.Id==id);if(item==null)throw new HyTcgException("FrameUnavailable");return item;
        }
        internal static HyTcgFrameItem CreateFrameDraft(Dictionary<string,object> input)
        {
            string frameId=CreativeText(input,"frameId","moonlace");var definition=HyTcgFrameCatalog.Find(frameId);
            if(definition==null)throw new HyTcgException("FrameUnavailable");string name=CreativeText(input,"name",definition.Name).Trim();
            string description=CreativeText(input,"description","");if(!ValidAlbumName(name) || !ValidFrameDescription(description))throw new HyTcgException("FrameInvalid");
            string artwork=CreativeText(input,"artworkId","");if(artwork!="" && !ValidArtworkId(artwork))throw new HyTcgException("ArtworkInvalid");
            return new HyTcgFrameItem {FrameId=frameId,Design=definition.Design,PortraitOverlay=definition.PortraitOverlay??"",LandscapeOverlay=definition.LandscapeOverlay??"",
                Name=name,Description=description,ArtworkId=artwork,AppliedCardId="",
                Portrait=FrameInputLayout(input,"portrait",definition.Portrait),Landscape=FrameInputLayout(input,"landscape",definition.Landscape)};
        }
        private static HyTcgFrameLayout FrameInputLayout(Dictionary<string,object> input,string key,HyTcgFrameLayout fallback)
        {
            object value;try
            {
                var layout=input.TryGetValue(key,out value) && value!=null?Parser().Deserialize<HyTcgFrameLayout>(Parser().Serialize(value)):
                    Parser().Deserialize<HyTcgFrameLayout>(Parser().Serialize(fallback));
                if(!HyTcgFrameCatalog.LayoutValid(layout))throw new HyTcgException("FrameInvalid");return layout;
            }
            catch(HyTcgException) {throw;}catch {throw new HyTcgException("FrameInvalid");}
        }
        private static HyTcgFrameDefinition ResolveFrameDefinition(Vault v,HyTcgCosmetic style)
        {
            if(style==null)return HyTcgFrameCatalog.Find("classic");EnsureCreative(v);
            var definition=HyTcgFrameCatalog.Find(style.Frame);
            if(!String.IsNullOrEmpty(style.FrameItemId))
            {
                var item=FindFrameItem(v.Creative,style.FrameItemId);if(item.FrameId!=style.Frame || (item.AppliedCardId!="" && item.AppliedCardId!=style.CardId))throw new HyTcgException("FrameConsumed");
                definition=new HyTcgFrameDefinition {Id=item.FrameId,Name=item.Name,Description="Created inventory frame.",Design=item.Design,PriceGems=FrameCreationPrice,
                    Materials=HyTcgFrameCatalog.CreationMaterials(),Portrait=item.Portrait,Landscape=item.Landscape,CardDescription=item.Description??"",
                    PortraitOverlay=item.PortraitOverlay,LandscapeOverlay=item.LandscapeOverlay};
                // A created overlay is a protected artwork reference, never a file path.
                if(item.ArtworkId!="") {definition.PortraitOverlay="";definition.LandscapeOverlay="";}
            }
            if(definition==null)throw new HyTcgException("FrameUnavailable");
            return definition;
        }
        private string FrameOverlayData(Vault v,HyTcgCosmetic style)
        {
            if(style==null || String.IsNullOrEmpty(style.FrameItemId))return null;EnsureCreative(v);
            var item=FindFrameItem(v.Creative,style.FrameItemId);if(item.ArtworkId=="")return null;
            return "data:image/png;base64,"+Convert.ToBase64String(ReadArtworkPng(v,FindArtwork(v.Creative,item.ArtworkId)));
        }
        internal static int ArtworkPixelPrice(int pixels)
        {if(pixels<0 || pixels>1600*1600)throw new HyTcgException("ArtworkInvalid");return Math.Max(1,(pixels+ArtworkPixelsPerGem-1)/ArtworkPixelsPerGem);}
        internal static int ArtworkPixelColorPrice(int pixels,int colors)
        {if(pixels<0 || pixels>1600*1600 || colors<0 || colors>64 || (pixels>0 && colors==0))throw new HyTcgException("ArtworkInvalid");return pixels==0?0:Math.Max(1,(int)(((long)pixels*colors+ArtworkPixelsPerGem-1)/ArtworkPixelsPerGem));}
        private static int ArtworkVisiblePixels(HyTcgCustomArtwork art,byte[] png)
        {
            // VisiblePixels is a display cache only. Every economic quote and apply
            // derives its price from the authenticated raster instead of trusting
            // editable metadata or a pixel count supplied by the WebView.
            int pixels=CountVisiblePixels(png);art.VisiblePixels=pixels;return pixels;
        }
        internal static int CountVisiblePixels(byte[] png)
        {int colors;return CountArtworkPixels(png,out colors);}
        private static int CountArtworkPixels(byte[] png,out int colors)
        {
            using(var stream=new MemoryStream(png,false))using(var source=Image.FromStream(stream,true,true))
            {
                if(source.Width<1 || source.Height<1 || source.Width>1600 || source.Height>1600)throw new HyTcgException("ArtworkInvalid");
                using(var bitmap=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb))
                {
                    using(var graphics=Graphics.FromImage(bitmap)) {graphics.CompositingMode=CompositingMode.SourceCopy;graphics.DrawImageUnscaled(source,0,0);}
                    var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                    try {int count=0;var palette=new HashSet<int>();byte[] row=new byte[bitmap.Width*4];for(int y=0;y<bitmap.Height;y++)
                        {Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);for(int x=0;x<row.Length;x+=4)if(row[x+3]!=0){count++;if(palette.Count<64)palette.Add(row[x]|row[x+1]<<8|row[x+2]<<16);}}colors=palette.Count;return count;}
                    finally {bitmap.UnlockBits(data);}
                }
            }
        }
        private object CreativeView(Vault v)
        {
            HyTcgCreativeState state=v.Creative??NewCreative();
            EnsureStudio(state);
            var owned=new HashSet<string>(v.Cards.Where(c=>!c.Dissolved && ImageMetadataValid(v,c)).Select(c=>c.CardId),StringComparer.Ordinal);
            var cosmetics=new List<object>();
            foreach(HyTcgCosmetic c in state.Cosmetics.Where(c=>owned.Contains(c.CardId)))
            {
                // Custom images are requested lazily through ReadCustomArtwork. A
                // snapshot stays small even when one drawing is reused on many cards.
                cosmetics.Add(RarityCosmetic(c));
            }
            var inventory=state.Dyes.Where(p=>p.Value>0).OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=> {var dye=FrameDye(p.Key);
                return new HyTcgInventoryEntry {Id=p.Key,Kind="dye",Name=dye.Name,Color=dye.Color,Mystic=dye.Mystic,Count=p.Value};}).ToList();
            foreach(string id in GameplayItemIds)
            {int count=0;if(v.Gameplay!=null)v.Gameplay.Inventory.TryGetValue(id,out count);var item=GameplayItem(id,count);
                inventory.Add(new HyTcgInventoryEntry {Id=id,Kind=item.Kind=="material"?"material":"supply",Name=item.Name,Count=count});}
            return new
            {
                Albums=state.Albums.Select(a=>new HyTcgAlbum {Version=a.Version,Capacity=a.Capacity,Cover=a.Cover,Id=a.Id,Name=a.Name,Background=a.Background,
                    CardIds=a.CardIds.Select(id=>id!="" && owned.Contains(id)?id:"").ToList(),CreatedAt=a.CreatedAt}).ToList(),
                Cosmetics=cosmetics,
                FrameMode="rarity",Frames=CreativePresets(new[]{"classic"}),Backgrounds=CreativePresets(creativeBackgrounds),
                FrameCatalog=new List<HyTcgFrameDefinition>(),OwnedFrameIds=new List<string>{"classic"},
                FrameItems=new List<HyTcgFrameItem>(),InventoryItems=inventory,
                Artworks=state.Artworks.Select(a=>new {Id=a.Id,Width=a.Width,Height=a.Height,VisiblePixels=a.VisiblePixels,Colors=a.Colors}).ToList(),
                DyePalette=Enumerable.Range(0,64).Select(i=>RollFrameDye(i,1)).ToList(),
                Dyes=state.Dyes.Where(p=>p.Value>0).OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=> {var dye=FrameDye(p.Key);dye.Count=p.Value;return dye;}).ToList(),
                DyeBoosterPrice=DyeBoosterPrice,ArtworkApplyPrice=ArtworkApplyPrice,ArtworkPixelsPerGem=ArtworkPixelsPerGem,
                ArtworkPaletteSize=64,AlbumPrice=AlbumPrice,AlbumSlotPrice=AlbumSlotPrice,AlbumRestylePrice=AlbumRestylePrice,MaxAlbumSlots=MaxAlbumSlots,
                FrameCreationPrice=0,FrameCreationMaterials=new List<HyTcgFrameMaterial>(),MysticChancePercent=1,
                NextStudioSequence=state.StudioSequence+1,LastStudioReceipt=state.LastStudioReceipt,
                RestorableLooks=state.ResetCosmetics.Where(c=>owned.Contains(c.CardId)).Select(RarityCosmetic).ToList()
            };
        }
        internal static HyTcgFrameDye RollFrameDye(int colorIndex,int mysticRoll)
        {
            if(colorIndex<0 || colorIndex>=64 || mysticRoll<0 || mysticRoll>=100)throw new HyTcgException("DyeInvalid");
            int[] channels={64,128,192,255};string color="#"+channels[(colorIndex>>4)&3].ToString("X2",CultureInfo.InvariantCulture)
                +channels[(colorIndex>>2)&3].ToString("X2",CultureInfo.InvariantCulture)+channels[colorIndex&3].ToString("X2",CultureInfo.InvariantCulture);
            bool mystic=mysticRoll==0;return new HyTcgFrameDye {Id="dye-"+colorIndex.ToString("D2",CultureInfo.InvariantCulture)+(mystic?"-mystic":""),
                Name=(mystic?"Mystic ":"")+color,Color=color,Mystic=mystic};
        }
        private static HyTcgFrameDye FrameDye(string id)
        {
            Match match=Regex.Match(id??"",@"^dye-(\d{2})(-mystic)?$");int index;
            if(!match.Success || !Int32.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out index) || index>=64)return null;
            return RollFrameDye(index,match.Groups[2].Success?0:1);
        }
        private static void SpendStudioGems(Vault v,int gems)
        {if(gems<0 || gems>MaxStudioCharge)throw new HyTcgException("CosmeticInvalid");if(v.Gems<gems)throw new HyTcgException("GemsInsufficient");v.Gems-=gems;}
        private static long StudioOperationSequence(string operation)
        {
            Match match=Regex.Match(operation??"",@"^SC-([1-9]\d{0,12})-[a-f0-9]{32}$");long value;
            if(!match.Success || !Int64.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out value)
                || value<1 || value>MaxStudioSequence)throw new HyTcgException("StudioOperationInvalid");return value;
        }
        private static string StudioInputHash(string action,Dictionary<string,object> input)
        {
            var text=new StringBuilder(action);foreach(string key in new[]{"cardId","frameId","frameItemId","frame","dyeId","artworkId","name","description","dye","morph","morphMode"})
            {
                string value=CreativeText(input,key,"");object raw;bool present=input.TryGetValue(key,out raw) && raw!=null;
                // Omitted artwork preserves the applied picture; an explicit empty
                // string clears it. Bind presence so one intent cannot replay as both.
                text.Append('\n');text.Append(key);text.Append(':');text.Append(present?'1':'0');
                text.Append(':');text.Append(value.Length);text.Append(':');text.Append(value);
            }
            foreach(string key in new[]{"artworkScale","artworkX","artworkY"})if(input.ContainsKey(key))
                text.Append('\n').Append(key).Append(':').Append(CreativeNumber(input,key,key=="artworkScale"?1:0,key=="artworkScale"?1:-1,key=="artworkScale"?4:1).ToString("R",CultureInfo.InvariantCulture));
            text.Append("\nrestorePrevious:");text.Append(CreativeBool(input,"restorePrevious")?"1":"0");
            if(action.StartsWith("album",StringComparison.Ordinal))
            {foreach(string key in new[]{"albumId","cover","background"}){string value=CreativeText(input,key,"");text.Append('\n').Append(key).Append(':').Append(value.Length).Append(':').Append(value);}if(action=="albumSlots")text.Append("\ncount:").Append(GameplayInt(input,"count",1,12));}
            foreach(string key in new[]{"portrait","landscape"})
            {
                object value;text.Append('\n');text.Append(key);text.Append(':');
                if(input.TryGetValue(key,out value) && value!=null)text.Append(Parser().Serialize(FrameInputLayout(input,key,null)));
            }
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        private static string LegacyStudioInputHash(string action,Dictionary<string,object> input)
        {
            var text=new StringBuilder(action);foreach(string key in new[]{"cardId","frameId","frame","dyeId","artworkId","dye","morph","morphMode"})
            {string value=CreativeText(input,key,"");object raw;bool present=input.TryGetValue(key,out raw) && raw!=null;text.Append('\n');text.Append(key);text.Append(':');text.Append(present?'1':'0');text.Append(':');text.Append(value.Length);text.Append(':');text.Append(value);}
            text.Append("\nrestorePrevious:");text.Append(CreativeBool(input,"restorePrevious")?"1":"0");return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        private static bool CreativeBool(Dictionary<string,object> input,string name)
        {object value;if(!input.TryGetValue(name,out value))return false;if(!(value is bool))throw new HyTcgException("CosmeticInvalid");return (bool)value;}
        private static bool SameCosmetic(HyTcgCosmetic before,HyTcgCosmetic after)
        {
            if(before==null)return after.Frame=="classic" && String.IsNullOrEmpty(after.FrameItemId) && after.Dye=="" && after.Morph=="" && after.MorphMode=="none" && after.ArtworkId=="" && !after.Mystic && (after.ArtworkScale==0 || after.ArtworkScale==1) && after.ArtworkX==0 && after.ArtworkY==0;
            return before.Frame==after.Frame && before.Dye==after.Dye && before.Morph==after.Morph && before.MorphMode==after.MorphMode
                && before.ArtworkId==after.ArtworkId && (before.DyeId??"")==(after.DyeId??"") && before.Mystic==after.Mystic && (before.FrameItemId??"")==(after.FrameItemId??"")
                && (before.ArtworkScale==0?1:before.ArtworkScale)==(after.ArtworkScale==0?1:after.ArtworkScale) && before.ArtworkX==after.ArtworkX && before.ArtworkY==after.ArtworkY;
        }
        private static double CreativeNumber(Dictionary<string,object> input,string name,double fallback,double minimum,double maximum)
        {
            object raw;if(!input.TryGetValue(name,out raw))return fallback;
            if(raw==null || raw is string || raw is bool || !(raw is IConvertible))throw new HyTcgException("CosmeticInvalid");
            double value;try{value=Convert.ToDouble(raw,CultureInfo.InvariantCulture);}catch{throw new HyTcgException("CosmeticInvalid");}
            if(Double.IsNaN(value) || Double.IsInfinity(value) || value<minimum || value>maximum)throw new HyTcgException("CosmeticInvalid");
            return Math.Round(value,4);
        }
        private static List<HyTcgCreativePreset> CreativePresets(string[] ids)
        {return ids.Select(id=>new HyTcgCreativePreset {Id=id,Name=Char.ToUpperInvariant(id[0])+id.Substring(1)}).ToList();}
        private static string CreativeText(Dictionary<string,object> input,string name,string fallback)
        {
            object value;if(!input.TryGetValue(name,out value) || value==null)return fallback;
            var text=value as string;if(text==null || text.Length>256)throw new HyTcgException("CosmeticInvalid");return text;
        }
        private static List<string> CreativeSlots(Dictionary<string,object> input)
        {
            object value;if(!input.TryGetValue("cardIds",out value) || value==null || value is string)throw new HyTcgException("AlbumInvalid");
            var items=value as IEnumerable;if(items==null)throw new HyTcgException("AlbumInvalid");
            var slots=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(object item in items)
            {
                string id=item==null?"":item as string;
                if(id==null || slots.Count>=MaxAlbumSlots || (id!="" && (!ValidCreativeCardId(id) || !seen.Add(id))))throw new HyTcgException("AlbumInvalid");
                slots.Add(id);
            }
            return slots;
        }
        private static bool ValidCreativeCardId(string id) {return Regex.IsMatch(id??"",@"^HT-\d{8}-[A-F0-9]{12}$");}
        private static bool ValidAlbumId(string id) {return Regex.IsMatch(id??"",@"^AL-[a-f0-9]{32}$");}
        private static bool ValidArtworkId(string id) {return Regex.IsMatch(id??"",@"^[a-f0-9]{64}$");}
        private static bool ValidAlbumName(string name) {return !String.IsNullOrWhiteSpace(name) && name.Length<=64 && !name.Any(Char.IsControl);}
        private static bool ValidFrameDescription(string value)
        {return value!=null && value.Length<=240 && !value.Any(c=>Char.IsControl(c) && c!='\r' && c!='\n' && c!='\t');}
        private static bool ValidCreativeColor(string color) {return color!=null && (color=="" || Regex.IsMatch(color,@"^#[A-F0-9]{6}$"));}
        private static bool ValidCosmetic(HyTcgCosmetic c)
        {
            if(c==null)return false;var dye=String.IsNullOrEmpty(c.DyeId)?null:FrameDye(c.DyeId);
            bool dyeValid=c.DyeId==null?!c.Mystic:c.DyeId==""?c.Dye=="" && c.Morph=="" && c.MorphMode=="none" && !c.Mystic:
                dye!=null && c.Dye==dye.Color && c.Mystic==dye.Mystic && c.Morph=="" && c.MorphMode=="none";
            return dyeValid && ValidCreativeCardId(c.CardId) && ValidFrameId(c.Frame) && ValidCreativeColor(c.Dye) && ValidCreativeColor(c.Morph)
                && creativeMorphModes.Contains(c.MorphMode) && (c.MorphMode=="none" || c.Morph!="")
                && c.ArtworkId!=null && (c.ArtworkId=="" || ValidArtworkId(c.ArtworkId)) && (String.IsNullOrEmpty(c.FrameItemId) || ValidFrameItemId(c.FrameItemId))
                && !Double.IsNaN(c.ArtworkScale+c.ArtworkX+c.ArtworkY) && !Double.IsInfinity(c.ArtworkScale+c.ArtworkX+c.ArtworkY)
                && (c.ArtworkScale==0 || (c.ArtworkScale>=1 && c.ArtworkScale<=4)) && Math.Abs(c.ArtworkX)<=1 && Math.Abs(c.ArtworkY)<=1;
        }
        private static HyTcgCreativeState NewCreative()
        {var state=new HyTcgCreativeState {Albums=new List<HyTcgAlbum>(),Cosmetics=new List<HyTcgCosmetic>(),Artworks=new List<HyTcgCustomArtwork>(),ResetArtworkIds=new List<string>()};EnsureStudio(state);return state;}
        private static HyTcgCreativeState EnsureCreative(Vault v) {if(v.Creative==null)v.Creative=NewCreative();EnsureStudio(v.Creative);return v.Creative;}
        private static bool ValidFrameId(string id) {return Regex.IsMatch(id??"",@"^[a-z][a-z0-9_-]{1,31}$");}
        private static bool ValidFrameItemId(string id) {return Regex.IsMatch(id??"",@"^FI-[a-f0-9]{32}$");}
        private static void EnsureStudio(HyTcgCreativeState state)
        {
            foreach(var album in state.Albums)if(album.Version==0){album.Version=1;album.Capacity=Math.Max(4,album.CardIds.Count);album.Cover=album.Background;album.CardIds.AddRange(Enumerable.Repeat("",album.Capacity-album.CardIds.Count));}
            bool paidVersion=state.StudioVersion==1;
            if(state.StudioVersion==0)
            {
                state.StudioVersion=1;state.Dyes=new Dictionary<string,int>(StringComparer.Ordinal);state.OwnedFrameIds=new List<string> {"classic"};
                foreach(var cosmetic in state.Cosmetics)if(!state.OwnedFrameIds.Contains(cosmetic.Frame))state.OwnedFrameIds.Add(cosmetic.Frame);
                state.StudioReceipts=new List<HyTcgStudioReceipt>();state.ResetCosmetics=new List<HyTcgCosmetic>();
            }
            if(state.ResetCosmetics==null)state.ResetCosmetics=new List<HyTcgCosmetic>();
            if(state.StudioVersion!=1)return;
            state.FrameItems=new List<HyTcgFrameItem>();
            foreach(var cosmetic in state.Cosmetics.Concat(state.ResetCosmetics))
            {
                cosmetic.FrameItemId="";if(cosmetic.Frame=="classic")continue;
                string id=MigrationFrameId("bound:"+cosmetic.CardId+":"+cosmetic.Frame);var item=state.FrameItems.FirstOrDefault(f=>f.Id==id);
                if(item==null) {item=MigrationFrame(cosmetic.Frame,id);item.AppliedCardId=cosmetic.CardId;state.FrameItems.Add(item);}cosmetic.FrameItemId=id;
            }
            if(paidVersion)foreach(string frame in state.OwnedFrameIds.Where(f=>f!="classic"))state.FrameItems.Add(MigrationFrame(frame,MigrationFrameId("inventory:"+frame)));
            state.StudioVersion=2;
        }
        private static string MigrationFrameId(string key) {return "FI-"+Hash(Encoding.UTF8.GetBytes("HYTCG migrated frame:"+key)).ToLowerInvariant().Substring(0,32);}
        private static HyTcgFrameItem MigrationFrame(string frame,string id)
        {
            var definition=HyTcgFrameCatalog.Find(frame)??HyTcgFrameCatalog.Find("classic");return new HyTcgFrameItem {Id=id,FrameId=frame,Design=definition.Design,
                PortraitOverlay=definition.PortraitOverlay??"",LandscapeOverlay=definition.LandscapeOverlay??"",Name=definition.Name,Description="",ArtworkId="",
                Portrait=definition.Portrait,Landscape=definition.Landscape,AppliedCardId="",Grandfathered=true,CreatedAt=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)};
        }
        private static HyTcgCustomArtwork FindArtwork(HyTcgCreativeState state,string id)
        {var artwork=state.Artworks.FirstOrDefault(a=>a.Id==id);if(artwork==null)throw new HyTcgException("ArtworkUnavailable");return artwork;}
        private static bool ValidCreativeDate(string value)
        {DateTime date;return DateTime.TryParseExact(value,"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out date) && date.Kind==DateTimeKind.Utc;}
        private static void ValidateCreative(Vault v)
        {
            HyTcgCreativeState state=v.Creative;if(state==null)return;
            ValidateStudio(state);
            if(state.Albums==null || state.Cosmetics==null || state.Artworks==null || state.ResetArtworkIds==null
                || state.Albums.Count>MaxCreativeAlbums || state.Cosmetics.Count>MaxCards || state.Artworks.Count>MaxCreativeArtworks || state.ResetArtworkIds.Count>MaxCreativeArtworks)
                throw new HyTcgException("VaultInvalid");
            var cards=new HashSet<string>(v.Cards.Select(c=>c.CardId),StringComparer.Ordinal);
            var albums=new HashSet<string>(StringComparer.Ordinal);
            foreach(HyTcgAlbum album in state.Albums)
            {
                if(album==null || !ValidAlbumId(album.Id) || !albums.Add(album.Id) || !ValidAlbumName(album.Name) || !creativeBackgrounds.Contains(album.Background)
                    || !ValidCreativeDate(album.CreatedAt) || album.CardIds==null || album.CardIds.Count>MaxAlbumSlots || (album.Version!=0 && album.Version!=1)
                    || (album.Version==1 && (album.Capacity<4 || album.Capacity>MaxAlbumSlots || album.CardIds.Count!=album.Capacity || !creativeBackgrounds.Contains(album.Cover))))throw new HyTcgException("VaultInvalid");
                var slots=new HashSet<string>(StringComparer.Ordinal);
                foreach(string id in album.CardIds)if(id==null || (id!="" && (!cards.Contains(id) || !slots.Add(id))))throw new HyTcgException("VaultInvalid");
            }
            var artworks=new HashSet<string>(StringComparer.Ordinal);
            foreach(HyTcgCustomArtwork artwork in state.Artworks)
                if(artwork==null || !ValidArtworkId(artwork.Id) || !artworks.Add(artwork.Id) || !ValidHash(artwork.ContainerHash)
                    || artwork.Width<1 || artwork.Height<1 || artwork.Width>1600 || artwork.Height>1600 || artwork.Bytes<24 || artwork.Bytes>MaxArtworkPngBytes || !ValidCreativeDate(artwork.CreatedAt)
                    || (artwork.VisiblePixels.HasValue && (artwork.VisiblePixels.Value<0 || artwork.VisiblePixels.Value>artwork.Width*artwork.Height))
                    || (artwork.Colors.HasValue && (artwork.Colors.Value<0 || artwork.Colors.Value>64)))
                    throw new HyTcgException("VaultInvalid");
            var styled=new HashSet<string>(StringComparer.Ordinal);
            foreach(HyTcgCosmetic c in state.Cosmetics)
                if(!ValidCosmetic(c) || !cards.Contains(c.CardId) || !styled.Add(c.CardId) || (c.ArtworkId!="" && !artworks.Contains(c.ArtworkId)))throw new HyTcgException("VaultInvalid");
            if(state.ResetCosmetics!=null)foreach(var c in state.ResetCosmetics)
                if(!cards.Contains(c.CardId) || (c.ArtworkId!="" && !artworks.Contains(c.ArtworkId)))throw new HyTcgException("VaultInvalid");
            if(state.StudioVersion==2)
            {
                foreach(var item in state.FrameItems)if((item.AppliedCardId!="" && !cards.Contains(item.AppliedCardId)) || (item.ArtworkId!="" && !artworks.Contains(item.ArtworkId)))throw new HyTcgException("VaultInvalid");
                foreach(var c in state.Cosmetics.Concat(state.ResetCosmetics))
                {
                    if(c.Frame=="classic" && String.IsNullOrEmpty(c.FrameItemId))continue;
                    var item=state.FrameItems.FirstOrDefault(f=>f.Id==c.FrameItemId);
                    if(item==null || item.FrameId!=c.Frame || item.AppliedCardId!=c.CardId)throw new HyTcgException("VaultInvalid");
                }
            }
            var reset=new HashSet<string>(StringComparer.Ordinal);
            foreach(string id in state.ResetArtworkIds)if(!ValidArtworkId(id) || !reset.Add(id) || artworks.Contains(id))throw new HyTcgException("VaultInvalid");
        }
        private static void ValidateStudio(HyTcgCreativeState state)
        {
            // Old applied Dye/Morph fields are preserved exactly, with no retroactive
            // charges. New economics initialize only in memory or on the next write.
            if(state.StudioVersion==0)
            {
                if(state.Dyes!=null || state.OwnedFrameIds!=null || state.StudioReceipts!=null || state.StudioSequence!=0 || state.LastStudioReceipt!=null || state.ResetCosmetics!=null || state.FrameItems!=null)
                    throw new HyTcgException("VaultInvalid");return;
            }
            if((state.StudioVersion!=1 && state.StudioVersion!=2) || state.Dyes==null || state.Dyes.Count>128 || state.OwnedFrameIds==null || state.OwnedFrameIds.Count>MaxOwnedFrames
                || state.StudioReceipts==null || state.StudioReceipts.Count>MaxStudioReceipts || state.StudioSequence<0 || state.StudioSequence>MaxStudioSequence
                || !state.OwnedFrameIds.Contains("classic") || state.OwnedFrameIds.Any(id=>!ValidFrameId(id))
                || state.OwnedFrameIds.Distinct(StringComparer.Ordinal).Count()!=state.OwnedFrameIds.Count)throw new HyTcgException("VaultInvalid");
            if(state.StudioVersion==1 && state.FrameItems!=null)throw new HyTcgException("VaultInvalid");
            if(state.StudioVersion==2)
            {
                if(state.FrameItems==null || state.FrameItems.Count>MaxCards*2+256 || state.FrameItems.Select(f=>f==null?"":f.Id).Distinct(StringComparer.Ordinal).Count()!=state.FrameItems.Count)throw new HyTcgException("VaultInvalid");
                foreach(var item in state.FrameItems)if(item==null || !ValidFrameItemId(item.Id) || !ValidFrameId(item.FrameId) || !ValidAlbumName(item.Name)
                    || !HyTcgFrameCatalog.DesignKnown(item.Design) || !HyTcgFrameCatalog.OverlayValid(item.PortraitOverlay) || !HyTcgFrameCatalog.OverlayValid(item.LandscapeOverlay)
                    || !ValidFrameDescription(item.Description) || item.ArtworkId==null || (item.ArtworkId!="" && !ValidArtworkId(item.ArtworkId))
                    || item.AppliedCardId==null || (item.AppliedCardId!="" && !ValidCreativeCardId(item.AppliedCardId)) || !ValidCreativeDate(item.CreatedAt)
                    || !HyTcgFrameCatalog.LayoutValid(item.Portrait) || !HyTcgFrameCatalog.LayoutValid(item.Landscape))throw new HyTcgException("VaultInvalid");
            }
            if(state.ResetCosmetics!=null && (state.ResetCosmetics.Count>MaxCards || state.ResetCosmetics.Any(c=>!ValidCosmetic(c))
                || state.ResetCosmetics.Select(c=>c.CardId).Distinct(StringComparer.Ordinal).Count()!=state.ResetCosmetics.Count))throw new HyTcgException("VaultInvalid");
            foreach(var dye in state.Dyes)if(FrameDye(dye.Key)==null || dye.Value<0 || dye.Value>MaxDyeQuantity)throw new HyTcgException("VaultInvalid");
            var ids=new HashSet<string>(StringComparer.Ordinal);long previous=0;
            foreach(var receipt in state.StudioReceipts)
            {
                if(receipt==null || !ids.Add(receipt.OperationId) || !ValidHash(receipt.InputHash) || !ValidCreativeDate(receipt.CreatedAt)
                    || (receipt.Action!="dyeBoosterBuy" && receipt.Action!="frameBuy" && receipt.Action!="frameCreate" && receipt.Action!="cosmeticSave" && receipt.Action!="albumCreate" && receipt.Action!="albumSlots" && receipt.Action!="albumRestyle")
                    || receipt.DyeId==null || (receipt.DyeId!="" && FrameDye(receipt.DyeId)==null) || receipt.FrameId==null || (receipt.FrameId!="" && !ValidFrameId(receipt.FrameId))
                    || receipt.CardId==null || (receipt.CardId!="" && !ValidCreativeCardId(receipt.CardId)) || receipt.GemsSpent<0 || receipt.GemsSpent>MaxStudioCharge || receipt.ArtworkColors<0 || receipt.ArtworkColors>64
                    || (receipt.HashVersion!=0 && receipt.HashVersion!=2) || (!String.IsNullOrEmpty(receipt.FrameItemId) && !ValidFrameItemId(receipt.FrameItemId)) || receipt.ArtworkPixels<0 || receipt.ArtworkPixels>1600*1600
                    || (receipt.NoOp && receipt.GemsSpent!=0))throw new HyTcgException("VaultInvalid");
                long sequence;try {sequence=StudioOperationSequence(receipt.OperationId);}catch {throw new HyTcgException("VaultInvalid");}
                if(sequence<=previous || sequence>state.StudioSequence)throw new HyTcgException("VaultInvalid");previous=sequence;
                if(receipt.Action=="dyeBoosterBuy" && (receipt.DyeId=="" || receipt.FrameId!="" || receipt.CardId!="" || receipt.GemsSpent!=DyeBoosterPrice || receipt.NoOp))throw new HyTcgException("VaultInvalid");
                if(receipt.Action=="cosmeticSave" && (receipt.CardId=="" || receipt.FrameId==""))throw new HyTcgException("VaultInvalid");
                if(receipt.Action.StartsWith("album",StringComparison.Ordinal) && (!ValidAlbumId(receipt.AlbumId) || receipt.CardId!="" || receipt.FrameId!=""
                    || (receipt.Action=="albumCreate" && (receipt.GemsSpent!=AlbumPrice || receipt.NoOp))
                    || (receipt.Action=="albumSlots" && (receipt.GemsSpent<AlbumSlotPrice || receipt.GemsSpent>12*AlbumSlotPrice || receipt.GemsSpent%AlbumSlotPrice!=0 || receipt.NoOp))
                    || (receipt.Action=="albumRestyle" && receipt.GemsSpent!=(receipt.NoOp?0:AlbumRestylePrice))))throw new HyTcgException("VaultInvalid");
                if(receipt.Action=="frameCreate" && (receipt.FrameId=="" || String.IsNullOrEmpty(receipt.FrameItemId) || receipt.CardId!="" || receipt.GemsSpent!=FrameCreationPrice || receipt.NoOp))throw new HyTcgException("VaultInvalid");
            }
            if((state.StudioSequence==0)!=(state.StudioReceipts.Count==0) || (state.StudioReceipts.Count>0 && previous!=state.StudioSequence)
                || (state.StudioSequence==0)!=(state.LastStudioReceipt==null)
                || (state.LastStudioReceipt!=null && !state.StudioReceipts.Any(r=>SameStudioReceipt(r,state.LastStudioReceipt))))throw new HyTcgException("VaultInvalid");
        }
        private static bool SameStudioReceipt(HyTcgStudioReceipt a,HyTcgStudioReceipt b)
        {
            // Reflection warming may change JavaScriptSerializer property order.
            // Economic equality depends on values, never a JSON property sequence.
            return a!=null && b!=null && a.OperationId==b.OperationId && a.InputHash==b.InputHash && a.Action==b.Action
                && a.DyeId==b.DyeId && a.FrameId==b.FrameId && a.CardId==b.CardId && a.GemsSpent==b.GemsSpent
                && a.NoOp==b.NoOp && a.CreatedAt==b.CreatedAt && a.FrameItemId==b.FrameItemId
                && a.ArtworkPixels==b.ArtworkPixels && a.HashVersion==b.HashVersion && a.AlbumId==b.AlbumId && a.ArtworkColors==b.ArtworkColors;
        }

        // Both URL imports and drawings pass through the same bounded raster decoder.
        // It removes metadata and normalizes to a PNG before any artwork is registered.
        internal string StoreCustomArtwork(byte[] bytes)
        {
            int width,height;byte[] png=NormalizeCustomArtwork(bytes,out width,out height);
            string id=Hash(png).ToLowerInvariant();
            FinishCreativeResetDeletes();
            return Write(delegate(Vault v,DateTime now)
            {
                HyTcgCreativeState state=EnsureCreative(v);
                HyTcgCustomArtwork existing=state.Artworks.FirstOrDefault(a=>a.Id==id);
                if(existing!=null) {ReadArtworkPng(v,existing);return id;}
                if(state.Artworks.Count>=MaxCreativeArtworks)throw new HyTcgException("ArtworkLimit");
                string destination=SafeArtworkPath(id);byte[] container;
                if(File.Exists(destination))
                {
                    container=ReadArtworkContainer(destination);
                    if(!EqualHash(Hash(DecodeArtwork(v,id,container)),Hash(png)))throw new HyTcgException("ArtworkInvalid");
                }
                else
                {
                    if(Directory.GetFiles(Path.GetDirectoryName(destination),"*.dat").Length>=MaxCreativeArtworks)throw new HyTcgException("ArtworkLimit");
                    container=ProtectArtwork(v,id,png);
                    string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                    try {WriteImageTemp(temporary,container);File.Move(temporary,destination);}
                    finally {if(File.Exists(temporary))try {File.Delete(temporary);}catch { }}
                }
                int colors;int pixels=CountArtworkPixels(png,out colors);state.Artworks.Add(new HyTcgCustomArtwork {Id=id,ContainerHash=Hash(container),Width=width,Height=height,VisiblePixels=pixels,Colors=colors,Bytes=png.Length,CreatedAt=now.ToString("o",CultureInfo.InvariantCulture)});
                return id;
            });
        }
        internal string ReadCustomArtwork(string artworkId)
        {
            if(!ValidArtworkId(artworkId))throw new HyTcgException("ArtworkInvalid");
            return Read(delegate(Vault v,DateTime now)
            {
                if(v.Creative==null)throw new HyTcgException("ArtworkUnavailable");
                return "data:image/png;base64,"+Convert.ToBase64String(ReadArtworkPng(v,FindArtwork(v.Creative,artworkId)));
            });
        }
        private string SafeArtworkDirectory()
        {
            // Check every ancestor, including the launcher root, so a junction cannot
            // turn this local artwork store into a write outside the intended account.
            for(DirectoryInfo parent=new DirectoryInfo(root);parent!=null;parent=parent.Parent)
                if(parent.Exists && (parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("ArtworkInvalid");
            string directory=Path.GetFullPath(Path.Combine(root,"hytcg-artworks"));
            if(!directory.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new HyTcgException("ArtworkInvalid");
            if(Directory.Exists(directory) && (File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("ArtworkInvalid");
            Directory.CreateDirectory(directory);
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("ArtworkInvalid");
            return directory;
        }
        private string SafeArtworkPath(string id)
        {
            if(!ValidArtworkId(id))throw new HyTcgException("ArtworkInvalid");
            string directory=SafeArtworkDirectory(),full=Path.GetFullPath(Path.Combine(directory,id+".dat"));
            if(!full.StartsWith(directory+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
                || (File.Exists(full) && (File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0))throw new HyTcgException("ArtworkInvalid");
            return full;
        }
        private byte[] ProtectArtwork(Vault v,string id,byte[] png)
        {
            byte[] prefix=Encoding.ASCII.GetBytes(Hash(Convert.FromBase64String(v.Key))+"\n"+id+"\n");
            var clear=new byte[prefix.Length+png.Length];Buffer.BlockCopy(prefix,0,clear,0,prefix.Length);Buffer.BlockCopy(png,0,clear,prefix.Length,png.Length);
            byte[] encrypted;
            try {encrypted=protector.Protect(clear);}catch {throw new HyTcgException("SaveFailed");}
            var container=new byte[artworkMagic.Length+encrypted.Length];Buffer.BlockCopy(artworkMagic,0,container,0,artworkMagic.Length);Buffer.BlockCopy(encrypted,0,container,artworkMagic.Length,encrypted.Length);
            if(container.Length>MaxArtworkContainerBytes)throw new HyTcgException("ArtworkInvalid");return container;
        }
        private static byte[] ReadArtworkContainer(string full)
        {
            var file=new FileInfo(full);if(!file.Exists)throw new HyTcgException("ArtworkUnavailable");
            if((file.Attributes&FileAttributes.ReparsePoint)!=0 || file.Length<artworkMagic.Length+24 || file.Length>MaxArtworkContainerBytes)throw new HyTcgException("ArtworkInvalid");
            // Recheck the opened handle and bound every read. A local file changing
            // between FileInfo and opening must never force an oversized allocation.
            using(var input=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var output=new MemoryStream())
            {
                if(input.Length<artworkMagic.Length+24 || input.Length>MaxArtworkContainerBytes)throw new HyTcgException("ArtworkInvalid");
                byte[] buffer=new byte[8192];int count;
                while((count=input.Read(buffer,0,buffer.Length))>0)
                {
                    if(output.Length+count>MaxArtworkContainerBytes)throw new HyTcgException("ArtworkInvalid");
                    output.Write(buffer,0,count);
                }
                if(output.Length<artworkMagic.Length+24)throw new HyTcgException("ArtworkInvalid");return output.ToArray();
            }
        }
        private byte[] DecodeArtwork(Vault v,string id,byte[] container)
        {
            try
            {
                if(container.Length<=artworkMagic.Length || !container.Take(artworkMagic.Length).SequenceEqual(artworkMagic))throw new HyTcgException("ArtworkInvalid");
                byte[] encrypted=new byte[container.Length-artworkMagic.Length];Buffer.BlockCopy(container,artworkMagic.Length,encrypted,0,encrypted.Length);
                byte[] clear=protector.Unprotect(encrypted),prefix=Encoding.ASCII.GetBytes(Hash(Convert.FromBase64String(v.Key))+"\n"+id+"\n");
                if(clear.Length<=prefix.Length || clear.Length>MaxArtworkPngBytes+prefix.Length || !clear.Take(prefix.Length).SequenceEqual(prefix))throw new HyTcgException("ArtworkInvalid");
                byte[] png=new byte[clear.Length-prefix.Length];Buffer.BlockCopy(clear,prefix.Length,png,0,png.Length);
                int width,height;ReadArtworkDimensions(png,out width,out height);
                if(png[0]!=137 || width>1600 || height>1600 || Hash(png).ToLowerInvariant()!=id)throw new HyTcgException("ArtworkInvalid");
                return png;
            }
            catch(HyTcgException) {throw;}
            catch {throw new HyTcgException("ArtworkInvalid");}
        }
        private byte[] ReadArtworkPng(Vault v,HyTcgCustomArtwork artwork)
        {
            byte[] container=ReadArtworkContainer(SafeArtworkPath(artwork.Id));
            if(!EqualHash(artwork.ContainerHash,Hash(container)))throw new HyTcgException("ArtworkInvalid");
            byte[] png=DecodeArtwork(v,artwork.Id,container);int width,height;ReadArtworkDimensions(png,out width,out height);
            if(png.Length!=artwork.Bytes || width!=artwork.Width || height!=artwork.Height)throw new HyTcgException("ArtworkInvalid");return png;
        }
        private static void QuantizeArtwork64(Bitmap bitmap)
        {
            var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try {byte[] row=new byte[bitmap.Width*4];for(int y=0;y<bitmap.Height;y++)
                {Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);for(int x=0;x<row.Length;x+=4)
                    {if(row[x+3]==0){row[x]=row[x+1]=row[x+2]=0;continue;}for(int c=0;c<3;c++)row[x+c]=(byte)(Math.Min(3,(row[x+c]+42)/85)*85);}
                Marshal.Copy(row,0,IntPtr.Add(data.Scan0,y*data.Stride),row.Length);}}
            finally {bitmap.UnlockBits(data);}
        }
        internal static byte[] NormalizeCustomArtwork(byte[] bytes,out int width,out int height)
        {return NormalizeCustomArtwork(bytes,out width,out height,true);}
        internal static byte[] NormalizeCustomArtwork(byte[] bytes,out int width,out int height,bool quantize)
        {
            width=0;height=0;
            if(bytes==null || bytes.Length<24 || bytes.Length>MaxArtworkInputBytes)throw new HyTcgException("ArtworkInvalid");
            int originalWidth,originalHeight;ReadArtworkDimensions(bytes,out originalWidth,out originalHeight);
            try
            {
                using(var input=new MemoryStream(bytes,false))
                using(var image=Image.FromStream(input,true,true))
                {
                    if((image.RawFormat.Guid!=ImageFormat.Png.Guid && image.RawFormat.Guid!=ImageFormat.Jpeg.Guid)
                        || image.Width!=originalWidth || image.Height!=originalHeight)throw new HyTcgException("ArtworkInvalid");
                    double ratio=Math.Min(1d,1600d/Math.Max(image.Width,image.Height));
                    width=Math.Max(1,(int)Math.Round(image.Width*ratio));height=Math.Max(1,(int)Math.Round(image.Height*ratio));
                    while(true)
                    {
                        byte[] png;
                        using(var normalized=new Bitmap(width,height,PixelFormat.Format32bppArgb))
                        using(var output=new MemoryStream())
                        {
                            using(var graphics=Graphics.FromImage(normalized)){
                            graphics.CompositingMode=CompositingMode.SourceCopy;graphics.CompositingQuality=CompositingQuality.HighQuality;
                            graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                            graphics.DrawImage(image,new Rectangle(0,0,width,height),0,0,image.Width,image.Height,GraphicsUnit.Pixel);
                            }
                            if(quantize)QuantizeArtwork64(normalized);
                            normalized.Save(output,ImageFormat.Png);png=output.ToArray();
                        }
                        if(png.Length<=MaxArtworkPngBytes)return png;
                        if(Math.Max(width,height)<128)throw new HyTcgException("ArtworkInvalid");
                        width=Math.Max(1,(int)(width*0.8));height=Math.Max(1,(int)(height*0.8));
                    }
                }
            }
            catch(HyTcgException) {throw;}
            catch {throw new HyTcgException("ArtworkInvalid");}
        }
        private static void ReadArtworkDimensions(byte[] bytes,out int width,out int height)
        {
            width=0;height=0;
            if(bytes==null || bytes.Length<24)throw new HyTcgException("ArtworkInvalid");
            if(bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))
            {
                if(bytes[12]!=73 || bytes[13]!=72 || bytes[14]!=68 || bytes[15]!=82)throw new HyTcgException("ArtworkInvalid");
                uint pngWidth=HyTcgService.Png.Read32(bytes,16),pngHeight=HyTcgService.Png.Read32(bytes,20);
                if(pngWidth>4096 || pngHeight>4096)throw new HyTcgException("ArtworkInvalid");
                width=(int)pngWidth;height=(int)pngHeight;
            }
            else if(bytes[0]==255 && bytes[1]==216)
            {
                int offset=2;
                while(offset<bytes.Length)
                {
                    if(bytes[offset++]!=255)throw new HyTcgException("ArtworkInvalid");
                    while(offset<bytes.Length && bytes[offset]==255)offset++;
                    if(offset>=bytes.Length)break;int marker=bytes[offset++];
                    if(marker==217 || marker==218)break;
                    if(marker==1 || (marker>=208 && marker<=216))continue;
                    if(offset+2>bytes.Length)break;int length=bytes[offset]*256+bytes[offset+1];
                    if(length<2 || length>bytes.Length-offset)break;
                    if((marker>=192 && marker<=195) || (marker>=197 && marker<=199) || (marker>=201 && marker<=203) || (marker>=205 && marker<=207))
                    {if(length<8)break;height=bytes[offset+3]*256+bytes[offset+4];width=bytes[offset+5]*256+bytes[offset+6];break;}
                    offset+=length;
                }
            }
            if(width<1 || height<1 || width>4096 || height>4096)throw new HyTcgException("ArtworkInvalid");
        }

        private void ResetCreative(Vault v)
        {
            var queued=new HashSet<string>((v.Creative??NewCreative()).ResetArtworkIds,StringComparer.Ordinal);
            if(v.Creative!=null)foreach(HyTcgCustomArtwork artwork in v.Creative.Artworks)queued.Add(artwork.Id);
            string directory=Path.Combine(root,"hytcg-artworks");
            if(Directory.Exists(directory))
            {
                SafeArtworkDirectory();
                foreach(string file in Directory.GetFiles(directory,"*.dat"))
                {
                    string id=Path.GetFileNameWithoutExtension(file);
                    if(ValidArtworkId(id) && (File.GetAttributes(file)&FileAttributes.ReparsePoint)==0)queued.Add(id);
                }
            }
            if(queued.Count>MaxCreativeArtworks)throw new HyTcgException("ArtworkLimit");
            foreach(string id in queued)SafeArtworkPath(id);
            v.Creative=NewCreative();v.Creative.ResetArtworkIds=queued.ToList();
        }
        internal void FinishCreativeResetDeletes()
        {
            bool pending=Read(delegate(Vault v,DateTime now) {return v.Creative!=null && v.Creative.ResetArtworkIds.Count>0;});
            if(!pending)return;
            Write(delegate(Vault v,DateTime now)
            {
                var remaining=new List<string>();
                foreach(string id in v.Creative.ResetArtworkIds)
                {
                    try {string full=SafeArtworkPath(id);if(File.Exists(full))File.Delete(full);}
                    catch {remaining.Add(id);}
                }
                v.Creative.ResetArtworkIds=remaining;return true;
            });
        }
    }
}

