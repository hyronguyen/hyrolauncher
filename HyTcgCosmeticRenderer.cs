using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace HYROLauncherWeb
{
    // A display compositor only: callers authenticate ownership and canonical image
    // bytes before calling. It never writes files or changes the issued card model.
    internal static class HyTcgCosmeticRenderer
    {
        private sealed class CacheEntry
        {
            internal string Data;
            internal bool Thumbnail;
            internal LinkedListNode<string> Node;
        }
        private static readonly object cacheGate=new object();
        private static readonly Dictionary<string,CacheEntry> cache=new Dictionary<string,CacheEntry>(StringComparer.Ordinal);
        private static readonly LinkedList<string> cacheOrder=new LinkedList<string>();
        private static int cacheBytes,cacheThumbnails,cacheDetails;
        private static long cacheHits,cacheMisses,cacheGeneration;
        private static readonly string[] legacyDesigns={"classic","petal","arcane","circuit","royal"};
        private static readonly string[] modes={"none","hue","multiply","color","screen"};
        internal static string Render(HyTcgCard card,string canonicalImageData,HyTcgCosmetic style,string customArtworkData,bool thumbnail)
        {return Render(card,canonicalImageData,style,customArtworkData,thumbnail,null,null);}
        internal static string RenderDevMaterial(HyTcgCard originalCard,HyTcgCard material,string canonicalImageData)
        {
            // A disposable upgrade material gets its own native identity and frame.
            // Extract using the original layout rather than the material's new
            // rarity marker; a metadata rewrite cannot change canonical pixels.
            if(originalCard==null || material==null || originalCard.CharacterId!=material.CharacterId)throw new HyTcgException("CardInvalid");
            byte[] canonical=ImageData(canonicalImageData,16*1024*1024,"CardInvalid");
            int width=HyTcgService.CanvasWidth(originalCard),height=HyTcgService.CanvasHeight(originalCard);
            string artwork;
            using(var input=new MemoryStream(canonical,false))using(var original=Image.FromStream(input,true,true))
            {
                if(original.Width!=width || original.Height!=height)throw new HyTcgException("CardInvalid");
                RectangleF source=CanonicalArtwork(originalCard,width,height);
                if(!UsesWindowLayout(originalCard))
                {
                    // Full-art exports contain baked labels. Their local material
                    // uses only the clean artwork between the brand and lower fade.
                    float top=Math.Max(110,source.Top),bottom=Math.Min(source.Bottom,width>height?height*.47f:height*.60f);
                    source=new RectangleF(source.X,top,source.Width,Math.Max(1,bottom-top));
                }
                using(var pixels=new Bitmap(Math.Max(1,(int)Math.Ceiling(source.Width)),Math.Max(1,(int)Math.Ceiling(source.Height)),PixelFormat.Format32bppArgb))
                using(var graphics=Graphics.FromImage(pixels))using(var output=new MemoryStream())
                {
                    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(original,new RectangleF(0,0,pixels.Width,pixels.Height),source,GraphicsUnit.Pixel);
                    pixels.Save(output,ImageFormat.Png);artwork="data:image/png;base64,"+Convert.ToBase64String(output.ToArray());
                }
            }
            string blank;using(var pixels=new Bitmap(750,1050,PixelFormat.Format32bppArgb))using(var output=new MemoryStream())
            {pixels.Save(output,ImageFormat.Png);blank="data:image/png;base64,"+Convert.ToBase64String(output.ToArray());}
            return Render(material,blank,null,artwork,false,null,null,true);
        }
        internal static string RenderDraftPreview(HyTcgCard card,string canonicalImageData,HyTcgFrameDefinition definition,string overlayData,bool thumbnail,string orientation)
        {
            // Preview either orientation using only authenticated artwork pixels.
            // The owned card, exported bytes and inventory instance stay untouched.
            if(card==null || definition==null || (orientation!="portrait" && orientation!="landscape"))throw new HyTcgException("FrameInvalid");
            byte[] canonical=ImageData(canonicalImageData,16*1024*1024,"CardInvalid");
            int sourceWidth=HyTcgService.CanvasWidth(card),sourceHeight=HyTcgService.CanvasHeight(card);
            var parser=new JavaScriptSerializer {MaxJsonLength=1024*1024};
            var preview=parser.Deserialize<HyTcgCard>(parser.Serialize(card));preview.Orientation=orientation;
            int width=HyTcgService.CanvasWidth(preview),height=HyTcgService.CanvasHeight(preview);
            string artwork,blank;
            using(var input=new MemoryStream(canonical,false))using(var original=Image.FromStream(input,true,true))
            {
                if(original.Width!=sourceWidth || original.Height!=sourceHeight)throw new HyTcgException("CardInvalid");
                RectangleF source=CanonicalArtwork(card,sourceWidth,sourceHeight);
                using(var pixels=new Bitmap(Math.Max(1,(int)Math.Ceiling(source.Width)),Math.Max(1,(int)Math.Ceiling(source.Height)),PixelFormat.Format32bppArgb))
                using(var graphics=Graphics.FromImage(pixels))using(var output=new MemoryStream())
                {
                    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(original,new RectangleF(0,0,pixels.Width,pixels.Height),source,GraphicsUnit.Pixel);
                    pixels.Save(output,ImageFormat.Png);artwork="data:image/png;base64,"+Convert.ToBase64String(output.ToArray());
                }
            }
            using(var pixels=new Bitmap(width,height,PixelFormat.Format32bppArgb))using(var output=new MemoryStream())
            {pixels.Save(output,ImageFormat.Png);blank="data:image/png;base64,"+Convert.ToBase64String(output.ToArray());}
            var neutral=new HyTcgCosmetic {CardId=preview.CardId,Frame=definition.Id,Dye="",Morph="",MorphMode="none",ArtworkId=""};
            return Render(preview,blank,neutral,artwork,thumbnail,definition,overlayData);
        }
        internal static string Render(HyTcgCard card,string canonicalImageData,HyTcgCosmetic style,string customArtworkData,bool thumbnail,HyTcgFrameDefinition definition,string overlayData)
        {return Render(card,canonicalImageData,style,customArtworkData,thumbnail,definition,overlayData,false);}
        internal static string Render(HyTcgCard card,string canonicalImageData,HyTcgCosmetic style,string customArtworkData,bool thumbnail,HyTcgFrameDefinition definition,string overlayData,bool sourceArtwork)
        {
            long generation;lock(cacheGate)generation=cacheGeneration;
            if(card==null || !HyTcgService.Rarities.Contains(card.Rarity) || !Regex.IsMatch(card.CardId??"",@"^HT-\d{8}-[A-F0-9]{12}$"))throw new HyTcgException("CardInvalid");
            int sourceWidth=HyTcgService.CanvasWidth(card),sourceHeight=HyTcgService.CanvasHeight(card),width=750,height=1050;
            byte[] canonical=ImageData(canonicalImageData,16*1024*1024,"CardInvalid");
            bool replacement=!String.IsNullOrEmpty(customArtworkData),custom=replacement && !sourceArtwork;
            // Rarity now owns the frame. Historical inventory frames remain readable
            // account data, but never replace the default geometry or artwork.
            bool defaultWindow=UsesWindowLayout(card);
            HyTcgFrameDefinition frame=defaultWindow?WindowDefinition():FullDefinition();
            if(frame==null || (style!=null && (style.CardId!=card.CardId || !modes.Contains(style.MorphMode)
                || !ColorText(style.Dye) || !ColorText(style.Morph) || (style.MorphMode!="none" && String.IsNullOrEmpty(style.Morph)))))throw new HyTcgException("CosmeticInvalid");
            HyTcgFrameLayout layout=frame.Portrait;ValidateLayout(layout);
            byte[] overlay=null;
            byte[] artwork=replacement?ImageData(customArtworkData,4*1024*1024,"ArtworkInvalid"):null;
            string cacheKey=CacheKey(card,canonical,style,artwork,frame,layout,overlay,thumbnail,sourceArtwork),cached;
            if(cacheKey!=null && Cached(cacheKey,out cached))return cached;
            try
            {
                using(var input=new MemoryStream(canonical,false))
                using(var original=Image.FromStream(input,true,true))
                {
                    if(original.Width!=sourceWidth || original.Height!=sourceHeight || (original.RawFormat.Guid!=ImageFormat.Png.Guid && original.RawFormat.Guid!=ImageFormat.Jpeg.Guid))throw new HyTcgException("CardInvalid");
                    if(style==null && !replacement && !NeedsDefaultRecomposition(card))return thumbnail?HyTcgStorage.Thumbnail(canonical):"data:image/"+(original.RawFormat.Guid==ImageFormat.Jpeg.Guid?"jpeg":"png")+";base64,"+Convert.ToBase64String(canonical);
                    using(var output=new Bitmap(width,height,PixelFormat.Format32bppArgb))
                    using(var graphics=Graphics.FromImage(output))
                    using(var buffer=new MemoryStream())
                    {
                        graphics.SmoothingMode=SmoothingMode.AntiAlias;graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;graphics.CompositingQuality=CompositingQuality.HighQuality;
                        Color rarity=ParseColor(card.RarityColor,ColorTranslator.FromHtml(HyTcgRules.RarityColor(card.Rarity)));
                        // Fresh cards already include authenticated artwork and text.
                        // Dye repaints the perimeter only, leaving every interior pixel
                        // unchanged. Old full-art cards retain their identity when an
                        // original source is unavailable instead of doubling text.
                        bool positioned=style!=null && (style.ArtworkScale>1 || style.ArtworkX!=0 || style.ArtworkY!=0);
                        bool retainInterior=!replacement && !positioned && sourceWidth==width && sourceHeight==height && (!NeedsDefaultRecomposition(card) || !defaultWindow);
                        if(retainInterior)graphics.DrawImageUnscaled(original,0,0);
                        else if(defaultWindow)DrawWindowBackground(graphics,width,height,rarity);
                        else using(var background=new SolidBrush(Color.FromArgb(17,21,33)))using(var shape=Rounded(0,0,width,height,37))graphics.FillPath(background,shape);
                        if(replacement)
                        {
                            using(var artworkInput=new MemoryStream(artwork,false))
                            using(var image=Image.FromStream(artworkInput,true,true))
                            {
                                if(image.Width<1 || image.Height<1 || image.Width>1600 || image.Height>1600
                                    || (image.RawFormat.Guid!=ImageFormat.Png.Guid && image.RawFormat.Guid!=ImageFormat.Jpeg.Guid))throw new HyTcgException("ArtworkInvalid");
                                if(defaultWindow)DrawTemplateArtwork(graphics,image,new RectangleF(0,0,image.Width,image.Height),Box(layout,"Artwork",width,height),true,style);
                                else DrawFullArtwork(graphics,image,new RectangleF(0,0,image.Width,image.Height),width,height,style);
                            }
                        }
                        else if(!retainInterior)
                        {
                            // A new layout reuses only canonical artwork pixels. Old
                            // labels are never treated as character artwork or copied.
                            RectangleF source=CanonicalArtwork(card,sourceWidth,sourceHeight);
                            if(!defaultWindow)source=new RectangleF(source.X,Math.Max(110,source.Top),source.Width,Math.Max(1,(sourceWidth>sourceHeight?sourceHeight*.47f:sourceHeight*.60f)-Math.Max(110,source.Top)));
                            if(defaultWindow)DrawTemplateArtwork(graphics,original,source,Box(layout,"Artwork",width,height),true,style);
                            else DrawFullArtwork(graphics,original,source,width,height,style);
                        }
                        DrawRarityPerimeter(graphics,width,height,card.Rarity,style,rarity);
                        if(!retainInterior && defaultWindow)DrawWindowIdentity(graphics,card,width,height,layout,rarity,custom);
                        else if(!retainInterior)DrawFullIdentity(graphics,card,width,height,custom);
                        if(!retainInterior || NeedsDefaultRecomposition(card))DrawRarityBadge(graphics,card.Rarity,width,height,defaultWindow);
                        // The page or shelf supplies the background outside the physical card.
                        GraphicsState cornerState=graphics.Save();
                        using(var all=new Region(new Rectangle(0,0,width,height)))using(var silhouette=Rounded(0,0,width,height,37))
                        using(var transparent=new SolidBrush(Color.Transparent))
                        {all.Exclude(silhouette);graphics.SetClip(all,CombineMode.Replace);graphics.CompositingMode=CompositingMode.SourceCopy;graphics.FillRectangle(transparent,0,0,width,height);}
                        graphics.Restore(cornerState);
                        output.Save(buffer,ImageFormat.Png);byte[] png=buffer.ToArray();
                        string result=thumbnail?HyTcgStorage.Thumbnail(png):"data:image/png;base64,"+Convert.ToBase64String(png);
                        Cache(cacheKey,result,thumbnail,generation);return result;
                    }
                }
            }
            catch(HyTcgException) {throw;}
            catch {throw new HyTcgException(custom?"ArtworkInvalid":"CardInvalid");}
        }
        private static string CacheKey(HyTcgCard card,byte[] canonical,HyTcgCosmetic style,byte[] artwork,HyTcgFrameDefinition frame,HyTcgFrameLayout layout,byte[] overlay,bool thumbnail,bool sourceArtwork)
        {
            // The caller has already re-authenticated native card/artwork containers.
            // Hash every input that can affect pixels, including a retained upgrade ID.
            var metadata=new StringBuilder();
            foreach(string value in new[]{card.CardId,card.CharacterId.ToString(CultureInfo.InvariantCulture),card.Name,card.Series,card.Rarity,card.RarityColor,
                card.Orientation,card.Gender,card.ImageSource,card.Revision.ToString(CultureInfo.InvariantCulture),style==null?"":style.Frame,style==null?"":style.Dye,
                style==null?"":style.Morph,style==null?"":style.MorphMode,style==null?"":style.DyeId,style==null?"":style.FrameItemId,style!=null && style.Mystic?"mystic":"regular",
                CardLayout(card),CardDescription(card),"portrait-badge-v4",style==null?"1":(style.ArtworkScale==0?1:style.ArtworkScale).ToString("R",CultureInfo.InvariantCulture),
                style==null?"0":style.ArtworkX.ToString("R",CultureInfo.InvariantCulture),style==null?"0":style.ArtworkY.ToString("R",CultureInfo.InvariantCulture),
                frame.Id,frame.Design,frame.CardDescription,frame.PortraitOverlay,frame.LandscapeOverlay,thumbnail?"thumbnail":"detail",sourceArtwork?"original-artwork":"custom-artwork"})
            {string text=value??"";metadata.Append(text.Length.ToString(CultureInfo.InvariantCulture));metadata.Append(':');metadata.Append(text);}
            foreach(var property in typeof(HyTcgFrameLayout).GetProperties().OrderBy(p=>p.Name,StringComparer.Ordinal))
            {metadata.Append(property.Name);metadata.Append(':');metadata.Append(Convert.ToString(property.GetValue(layout,null),CultureInfo.InvariantCulture));metadata.Append(';');}
            using(var sha=SHA256.Create())
            {
                metadata.Append(Convert.ToBase64String(sha.ComputeHash(canonical)));
                if(artwork!=null)metadata.Append(Convert.ToBase64String(sha.ComputeHash(artwork)));
                if(overlay!=null)metadata.Append(Convert.ToBase64String(sha.ComputeHash(overlay)));
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(metadata.ToString())));
            }
        }
        private static bool Cached(string key,out string data)
        {
            lock(cacheGate)
            {
                CacheEntry entry;if(!cache.TryGetValue(key,out entry)) {cacheMisses++;data=null;return false;}
                cacheOrder.Remove(entry.Node);cacheOrder.AddLast(entry.Node);cacheHits++;data=entry.Data;return true;
            }
        }
        private static void Cache(string key,string data,bool thumbnail,long generation)
        {
            if(key==null || data==null || data.Length>16*1024*1024)return;
            lock(cacheGate)
            {
                if(generation!=cacheGeneration)return;
                CacheEntry previous;
                if(cache.TryGetValue(key,out previous))RemoveCached(previous);
                var entry=new CacheEntry {Data=data,Thumbnail=thumbnail,Node=cacheOrder.AddLast(key)};cache[key]=entry;
                cacheBytes+=data.Length*2;if(thumbnail)cacheThumbnails++;else cacheDetails++;
                while(cacheBytes>32*1024*1024 || cacheThumbnails>64 || cacheDetails>12)
                {
                    // Evict the relevant category when its count overflows, otherwise
                    // use combined LRU so full images and thumbnails share one budget.
                    LinkedListNode<string> node=cacheOrder.First;
                    if(cacheBytes<=32*1024*1024)
                        while(node!=null && (cacheThumbnails>64?!cache[node.Value].Thumbnail:cache[node.Value].Thumbnail))node=node.Next;
                    if(node==null)node=cacheOrder.First;RemoveCached(cache[node.Value]);
                }
            }
        }
        private static void RemoveCached(CacheEntry entry)
        {cache.Remove(entry.Node.Value);cacheOrder.Remove(entry.Node);cacheBytes-=entry.Data.Length*2;if(entry.Thumbnail)cacheThumbnails--;else cacheDetails--;}
        internal static void ClearCache()
        {lock(cacheGate) {cacheGeneration++;cache.Clear();cacheOrder.Clear();cacheBytes=0;cacheThumbnails=0;cacheDetails=0;cacheHits=0;cacheMisses=0;}}
        internal static object CacheDiagnostics()
        {lock(cacheGate)return new {Thumbnails=cacheThumbnails,Details=cacheDetails,Bytes=cacheBytes,Hits=cacheHits,Misses=cacheMisses};}
        private static byte[] ImageData(string value,int maximum,string code)
        {
            if(String.IsNullOrEmpty(value) || value.Length>maximum*4/3+128)throw new HyTcgException(code);
            Match match=Regex.Match(value,@"^data:image/(png|jpeg);base64,([A-Za-z0-9+/=\r\n]+)$",RegexOptions.IgnoreCase);
            if(!match.Success)throw new HyTcgException(code);
            byte[] bytes;
            try {bytes=HyTcgStorage.ImageBytes(Convert.FromBase64String(match.Groups[2].Value));}catch {throw new HyTcgException(code);}
            if(bytes.Length<24 || bytes.Length>maximum)throw new HyTcgException(code);
            // Guard dimensions before allocating a bitmap. Registered custom images
            // are PNGs, and canonical card data always uses these bounded dimensions.
            if(bytes[0]==137 && bytes[1]==80 && bytes[2]==78 && bytes[3]==71)
            {
                uint width=HyTcgService.Png.Read32(bytes,16),height=HyTcgService.Png.Read32(bytes,20);
                if(width<1 || height<1 || width>1600 || height>1600)throw new HyTcgException(code);
            }
            else if(bytes[0]==255 && bytes[1]==216)
            {
                int offset=2,width=0,height=0;
                while(offset<bytes.Length)
                {
                    if(bytes[offset++]!=255)throw new HyTcgException(code);
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
                if(width<1 || height<1 || width>1600 || height>1600)throw new HyTcgException(code);
            }
            else throw new HyTcgException(code);
            return bytes;
        }
        private static bool ColorText(string value) {return String.IsNullOrEmpty(value) || Regex.IsMatch(value,@"^#[A-Fa-f0-9]{6}$");}
        private static Color ParseColor(string value,Color fallback) {return !String.IsNullOrEmpty(value) && ColorText(value)?ColorTranslator.FromHtml(value):fallback;}
        private static GraphicsPath Rounded(float x,float y,float width,float height,float radius)
        {
            var path=new GraphicsPath();float diameter=radius*2;
            path.AddArc(x,y,diameter,diameter,180,90);path.AddArc(x+width-diameter,y,diameter,diameter,270,90);
            path.AddArc(x+width-diameter,y+height-diameter,diameter,diameter,0,90);path.AddArc(x,y+height-diameter,diameter,diameter,90,90);path.CloseFigure();return path;
        }
        private static byte[] OverlayBytes(byte[] bytes)
        {return bytes==null?null:ImageData("data:image/png;base64,"+Convert.ToBase64String(bytes),4*1024*1024,"FrameInvalid");}
        private static RectangleF Box(HyTcgFrameLayout layout,string name,int width,int height)
        {
            var type=typeof(HyTcgFrameLayout);
            return new RectangleF((float)Convert.ToDouble(type.GetProperty(name+"X").GetValue(layout,null))*width,
                (float)Convert.ToDouble(type.GetProperty(name+"Y").GetValue(layout,null))*height,
                (float)Convert.ToDouble(type.GetProperty(name+"Width").GetValue(layout,null))*width,
                (float)Convert.ToDouble(type.GetProperty(name+"Height").GetValue(layout,null))*height);
        }
        private static void ValidateLayout(HyTcgFrameLayout layout)
        {
            if(layout==null)throw new HyTcgException("FrameInvalid");
            foreach(string name in new[]{"Artwork","Name","Series","Rarity","Number","Description"})
            {
                RectangleF box=Box(layout,name,1,1);
                if(name=="Description" && box.X==0 && box.Y==0 && box.Width==0 && box.Height==0)continue;
                if(Single.IsNaN(box.X+box.Y+box.Width+box.Height) || Single.IsInfinity(box.X+box.Y+box.Width+box.Height)
                    || box.X<0 || box.Y<0 || box.Width<0.01f || box.Height<0.01f || box.Right>1.00001f || box.Bottom>1.00001f)throw new HyTcgException("FrameInvalid");
            }
        }
        internal static bool UsesWindowLayout(HyTcgCard card)
        {return card!=null && (card.Rarity=="Common" || card.Rarity=="Uncommon" || card.Rarity=="Rare" || card.Rarity=="SR");}
        internal static bool NeedsDefaultRecomposition(HyTcgCard card)
        {return card!=null && (card.Orientation=="landscape" || CardLayout(card)!=(UsesWindowLayout(card)?"window-v4":"full-v4"));}
        private static string CardLayout(HyTcgCard card)
        {string value;return card.Metadata!=null && card.Metadata.TryGetValue("hytcg_card_layout",out value)?value??"":"";}
        private static string CardDescription(HyTcgCard card)
        {
            string value="",candidate;
            if(card.Metadata!=null && ((card.Metadata.TryGetValue("desc",out candidate) && !String.IsNullOrWhiteSpace(candidate))
                || (card.Metadata.TryGetValue("description",out candidate) && !String.IsNullOrWhiteSpace(candidate))))value=candidate;
            if(value.Length>4000)value=value.Substring(0,4000);
            value=Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(value,@"<[^>]*>"," "))??"",@"\s+"," ").Trim();
            if(value=="")value=String.IsNullOrWhiteSpace(card.Series)?"A companion ready for a new adventure.":"A companion from "+card.Series+".";
            return Text(value,600);
        }
        private static HyTcgFrameLayout WindowLayout(bool landscape)
        {
            return landscape?new HyTcgFrameLayout {ArtworkX=0.04,ArtworkY=0.045,ArtworkWidth=0.56,ArtworkHeight=0.89,
                NameX=0.65,NameY=0.075,NameWidth=0.30,NameHeight=0.13,SeriesX=0.65,SeriesY=0.30,SeriesWidth=0.30,SeriesHeight=0.095,
                RarityX=0.65,RarityY=0.222,RarityWidth=0.30,RarityHeight=0.045,NumberX=0.65,NumberY=0.875,NumberWidth=0.30,NumberHeight=0.045,
                DescriptionX=0.65,DescriptionY=0.525,DescriptionWidth=0.30,DescriptionHeight=0.25}:
                new HyTcgFrameLayout {ArtworkX=0.07,ArtworkY=0.035,ArtworkWidth=0.86,ArtworkHeight=0.59,
                    NameX=0.08,NameY=0.65,NameWidth=0.59,NameHeight=0.065,SeriesX=0.08,SeriesY=0.733,SeriesWidth=0.84,SeriesHeight=0.044,
                    RarityX=0.765,RarityY=0.663,RarityWidth=0.16,RarityHeight=0.035,NumberX=0.08,NumberY=0.942,NumberWidth=0.84,NumberHeight=0.021,
                    DescriptionX=0.08,DescriptionY=0.817,DescriptionWidth=0.84,DescriptionHeight=0.103};
        }
        private static HyTcgFrameDefinition WindowDefinition()
        {return new HyTcgFrameDefinition {Id="default-window-v3",Name="Companion",Design="classic",Portrait=WindowLayout(false),Landscape=WindowLayout(true)};}
        private static HyTcgFrameDefinition FullDefinition()
        {
            return new HyTcgFrameDefinition {Id="default-full-v3",Name="Full artwork",Design="classic",
                Portrait=FullLayout(false),Landscape=FullLayout(true)};
        }
        private static HyTcgFrameLayout FullLayout(bool landscape)
        {
            return new HyTcgFrameLayout {ArtworkX=landscape?34d/1050:34d/750,ArtworkY=landscape?34d/750:34d/1050,
                ArtworkWidth=landscape?982d/1050:682d/750,ArtworkHeight=landscape?682d/750:982d/1050,
                NameX=.07,NameY=landscape?.73:.80,NameWidth=.84,NameHeight=.09,
                SeriesX=.07,SeriesY=landscape?.84:.875,SeriesWidth=.84,SeriesHeight=.04,
                RarityX=.79,RarityY=.052,RarityWidth=.14,RarityHeight=.04,
                NumberX=.07,NumberY=.949,NumberWidth=.86,NumberHeight=.021};
        }
        private static RectangleF CanonicalArtwork(HyTcgCard card,int width,int height)
        {
            if(CardLayout(card)=="window-v2" || CardLayout(card)=="window-v3" || CardLayout(card)=="window-v4")return Box(WindowLayout(width>height),"Artwork",width,height);
            if(CardLayout(card)=="full-v3" || CardLayout(card)=="full-v4")return new RectangleF(34,34,width-68,height-68);
            if(CardLayout(card)=="window-v1")return width>height?new RectangleF(width*0.055f,height*0.19f,width*0.55f,height*0.665f):new RectangleF(width*0.07f,height*0.135f,width*0.86f,height*0.47f);
            float bottom=height>width?650:height*0.47f;
            return new RectangleF(24,110,width-48,Math.Max(1,bottom-110));
        }
        private static void DrawWindowBackground(Graphics g,int width,int height,Color rarity)
        {
            using(var path=Rounded(0,0,width,height,37))using(var cream=new SolidBrush(Color.FromArgb(238,234,222)))g.FillPath(cream,path);
        }
        private static Color Blend(Color color,Color target,float amount)
        {return Color.FromArgb((int)(color.R+(target.R-color.R)*amount),(int)(color.G+(target.G-color.G)*amount),(int)(color.B+(target.B-color.B)*amount));}
        private static void DrawRarityPerimeter(Graphics g,int width,int height,string rarity,HyTcgCosmetic style,Color rarityColor)
        {
            int tier=Array.IndexOf(new[]{"Common","Uncommon","Rare","SR","SSR","UR"},rarity);tier=Math.Max(0,tier);
            Color ink=style==null?rarityColor:Morph(ParseColor(style.Dye,rarityColor),style.Morph,style.MorphMode);
            Color light=Blend(ink,Color.White,.55f),shadow=Blend(ink,Color.Black,.64f);
            GraphicsState saved=g.Save();try
            {
                using(var outer=Rounded(2,2,width-4,height-4,35))using(var inner=Rounded(31,31,width-62,height-62,15))using(var rim=new Region(outer))
                {
                    rim.Exclude(inner);g.SetClip(rim,CombineMode.Replace);
                    using(var bevel=new LinearGradientBrush(new Rectangle(0,0,width,height),light,shadow,65f))
                    {
                        bevel.InterpolationColors=new ColorBlend {Positions=new[]{0f,.16f,.39f,.62f,.84f,1f},Colors=new[]{light,ink,Blend(ink,Color.White,.15f),shadow,ink,light}};
                        g.FillRectangle(bevel,0,0,width,height);
                    }
                    using(var edge=new Pen(Blend(ink,Color.Black,.70f),3))g.DrawPath(edge,outer);
                    using(var highlight=new Pen(Color.FromArgb(200,light),2))using(var shape=Rounded(7,7,width-14,height-14,31))g.DrawPath(highlight,shape);
                    using(var edge=new Pen(Color.FromArgb(220,shadow),3))g.DrawPath(edge,inner);
                    if(tier>=1)using(var accent=new Pen(Color.FromArgb(165,light),1.5f))using(var shape=Rounded(23,23,width-46,height-46,21))g.DrawPath(accent,shape);
                    using(var bright=new Pen(light,2))using(var dark=new Pen(shadow,3))using(var fill=new SolidBrush(light))
                    {
                        // All engravings are inside the rim's clip; none can dye the
                        // artwork, footer paper, or printed identity fields.
                        for(int end=0;end<2;end++)for(int side=0;side<2;side++)
                        {
                            float x=side==0?17:width-17,y=end==0?17:height-17,dx=side==0?1:-1,dy=end==0?1:-1;
                            if(tier==1){g.DrawLine(bright,x,y,x+dx*48,y);g.DrawLine(bright,x,y,x,y+dy*48);}
                            else if(tier==2){g.DrawPolygon(bright,new[]{new PointF(x,y-9),new PointF(x+9,y),new PointF(x,y+9),new PointF(x-9,y)});g.DrawLine(dark,x+dx*16,y,x+dx*58,y);}
                            else if(tier==3){Star(g,fill,x,y,7);g.DrawLines(bright,new[]{new PointF(x+dx*12,y),new PointF(x+dx*52,y),new PointF(x+dx*62,y+dy*7)});g.DrawLines(bright,new[]{new PointF(x,y+dy*12),new PointF(x,y+dy*52),new PointF(x+dx*7,y+dy*62)});}
                            else if(tier==4){Star(g,fill,x,y,8);for(int a=0;a<3;a++)g.DrawArc(bright,x+(dx<0?-52:0)+dx*a*3,y+(dy<0?-52:0)+dy*a*3,52,52,side==end?0:90,90);}
                            else if(tier==5){Star(g,fill,x,y,10);g.DrawPolygon(bright,new[]{new PointF(x+dx*18,y),new PointF(x+dx*48,y),new PointF(x+dx*32,y+dy*8)});g.DrawPolygon(bright,new[]{new PointF(x,y+dy*18),new PointF(x,y+dy*48),new PointF(x+dx*8,y+dy*32)});}
                        }
                        if(tier>=2)for(int side=0;side<2;side++)for(int i=0;i<tier-1;i++)
                        {
                            float x=side==0?16:width-16,y=height*(.33f+i*.11f);
                            if(tier==2)g.DrawLine(bright,x,y-8,x,y+8);
                            else if(tier==3){g.DrawEllipse(bright,x-3,y-3,6,6);if(i>0)g.DrawLine(bright,x,y-height*.11f+8,x,y-8);}
                            else {Star(g,fill,x,y,i%2==0?5:3);if(tier==5)g.DrawLine(dark,x,y-11,x,y+11);}
                        }
                        if(tier>=4){Star(g,fill,width/2f,16,tier==5?9:6);g.DrawLine(bright,width/2f-45,16,width/2f-15,16);g.DrawLine(bright,width/2f+15,16,width/2f+45,16);}
                    }
                    if(style!=null && style.Mystic)
                    {
                        using(var glow=new Pen(Color.FromArgb(120,light),8))using(var path=Rounded(16,16,width-32,height-32,24))g.DrawPath(glow,path);
                        using(var sparkle=new SolidBrush(Color.FromArgb(220,Color.White)))for(int i=0;i<8;i++)Star(g,sparkle,i%2==0?17:width-17,height*(.12f+i*.10f),i%3==0?3:2);
                    }
                }
            }
            finally {g.Restore(saved);}
        }
        private static void DrawFullArtwork(Graphics g,Image image,RectangleF source,int width,int height,HyTcgCosmetic style)
        {
            RectangleF box=new RectangleF(34,34,width-68,height-68);GraphicsState saved=g.Save();try
            {
                using(var shape=Rounded(box.X,box.Y,box.Width,box.Height,15))g.SetClip(shape);
                float scale=Math.Max(box.Width/source.Width,box.Height/source.Height)*(float)(style==null || style.ArtworkScale==0?1:style.ArtworkScale),w=source.Width*scale,h=source.Height*scale;
                g.DrawImage(image,new RectangleF(box.X+(box.Width-w)/2+(float)(style==null?0:style.ArtworkX)*box.Width/2,box.Y+(box.Height-h)/2+(float)(style==null?0:style.ArtworkY)*box.Height/2,w,h),source,GraphicsUnit.Pixel);
                using(var top=new LinearGradientBrush(new Rectangle(0,34,width,190),Color.FromArgb(145,7,11,23),Color.Transparent,90f))g.FillRectangle(top,box.X,box.Y,box.Width,190);
                float fadeY=width>height?height*.47f:height*.60f;
                using(var bottom=new LinearGradientBrush(new RectangleF(0,fadeY,width,height-fadeY),Color.Transparent,Color.FromArgb(255,8,13,24),90f))
                {bottom.InterpolationColors=new ColorBlend {Positions=new[]{0f,.45f,.8f,1f},Colors=new[]{Color.FromArgb(0,8,13,24),Color.FromArgb(145,8,13,24),Color.FromArgb(235,8,13,24),Color.FromArgb(255,8,13,24)}};g.FillRectangle(bottom,box.X,fadeY,box.Width,height-fadeY);}
            }
            finally {g.Restore(saved);}
        }
        private static void DrawFullIdentity(Graphics g,HyTcgCard card,int width,int height,bool custom)
        {
            bool landscape=width>height;float x=52;
            using(var font=Typeface(22,FontStyle.Bold))Label(g,"HYTCG",font,Color.FromArgb(242,244,252),x,54);
            using(var font=Typeface(16,FontStyle.Bold))Label(g,"CHARACTER  /  "+card.CharacterId,font,Color.FromArgb(190,205,221),x,landscape?height-232:height-243);
            TemplateLabel(g,card.Name,new RectangleF(x,landscape?height-205:height-215,width-104,92),Color.White,true,180,44);
            TemplateLabel(g,String.IsNullOrWhiteSpace(card.Series)?"Unknown series":card.Series,new RectangleF(x,height-132,width-104,40),Color.FromArgb(199,208,224),false,500,23);
            using(var line=new Pen(Color.FromArgb(75,190,205,221),1))g.DrawLine(line,x,height-72,width-x,height-72);
            using(var font=Typeface(13,FontStyle.Regular))Label(g,card.CardId+(card.Revision>0?" · R"+card.Revision:""),font,Color.FromArgb(180,190,210),x,height-55);
            if(!custom && card.ImageSource=="zerochan")using(var font=Typeface(14,FontStyle.Bold))RightLabel(g,"ALT",font,Color.FromArgb(180,190,210),width-x,height-55);
        }
        private static void RightLabel(Graphics g,string text,Font font,Color color,float right,float y)
        {Label(g,text,font,color,right-Measure(g,text,font),y);}
        private static void DrawRarityBadge(Graphics g,string rarity,int width,int height,bool window)
        {
            int tier=Math.Max(0,Array.IndexOf(new[]{"Common","Uncommon","Rare","SR","SSR","UR"},rarity));
            Color ink=ColorTranslator.FromHtml(new[]{"#9baec8","#6ed4a5","#6cabff","#c093f4","#efc975","#ec91bf"}[tier]);
            float w=180,h=64,x=width-w-52,y=window?height*.652f:40;
            GraphicsState saved=g.Save();try
            {
                g.TranslateTransform(x,y);g.ScaleTransform(w/180,h/64);
                using(var outline=Rounded(1,1,178,62,12))using(var background=new LinearGradientBrush(new Rectangle(0,0,180,64),Color.FromArgb(24,32,48),Color.FromArgb(9,15,27),90f))
                using(var edge=new Pen(ink,2)){g.FillPath(background,outline);g.DrawPath(edge,outline);}
                using(var highlight=new Pen(Color.FromArgb(100,ink),1))g.DrawLine(highlight,13,6,164,6);
                using(var disc=new SolidBrush(Color.FromArgb(38,ink)))using(var edge=new Pen(ink,1.5f))
                {g.FillEllipse(disc,13,14,36,36);g.DrawEllipse(edge,13,14,36,36);}
                using(var fill=new SolidBrush(ink))using(var edge=new Pen(ink,2))
                {
                    if(tier==0)g.FillEllipse(fill,26,27,10,10);
                    else if(tier==1){g.FillEllipse(fill,27,20,8,20);g.DrawLine(edge,25,42,38,22);}
                    else if(tier==2)g.FillPolygon(fill,new[]{new PointF(31,19),new PointF(43,30),new PointF(31,45),new PointF(19,30)});
                    else if(tier==3)Star(g,fill,31,32,13);
                    else if(tier==4){g.FillPolygon(fill,new[]{new PointF(18,25),new PointF(25,31),new PointF(31,18),new PointF(37,31),new PointF(44,25),new PointF(40,43),new PointF(22,43)});g.DrawLine(edge,22,47,40,47);}
                    else{Star(g,fill,31,32,13);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(edge,(float)(31+Math.Cos(a)*17),(float)(32+Math.Sin(a)*17),(float)(31+Math.Cos(a)*21),(float)(32+Math.Sin(a)*21));}}
                }
                using(var font=Typeface(rarity.Length>5?17:22,FontStyle.Bold))using(var brush=new SolidBrush(ink))
                using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                    g.DrawString(rarity,font,brush,new RectangleF(56,5,111,54),format);
            }
            finally{g.Restore(saved);}
        }
        private static void DrawWindowIdentity(Graphics g,HyTcgCard card,int width,int height,HyTcgFrameLayout layout,Color rarity,bool custom)
        {
            bool landscape=width>height;
            TemplateLabel(g,card.Name,Box(layout,"Name",width,height),Color.FromArgb(28,43,59),true,180,40);
            TemplateLabel(g,String.IsNullOrWhiteSpace(card.Series)?"Unknown series":card.Series,Box(layout,"Series",width,height),Color.FromArgb(28,43,59),true,500,24);
            RectangleF description=Box(layout,"Description",width,height);
            using(var line=new Pen(Color.FromArgb(48,58,65,74),1))
            {float y=landscape?height*0.495f:height*0.803f;g.DrawLine(line,description.Left,y,description.Right,y);}
            string character="CHARACTER  /  "+card.CharacterId.ToString(CultureInfo.InvariantCulture)+(String.IsNullOrWhiteSpace(card.Gender)?"":"  ·  "+Text(card.Gender,40));
            using(var font=Typeface(14,FontStyle.Bold))Label(g,character,font,Color.FromArgb(61,70,79),description.Left,landscape?height*0.455f:height*0.786f);
            using(var font=Typeface(landscape?20:22,FontStyle.Regular))
            {
                List<string> lines=TemplateLines(g,CardDescription(card),font,description.Width,landscape?6:4);float lineHeight=font.GetHeight(g);
                GraphicsState saved=g.Save();try {g.SetClip(description);for(int i=0;i<lines.Count;i++)Label(g,lines[i],font,Color.FromArgb(51,57,65),description.X,description.Y+i*lineHeight);}
                finally {g.Restore(saved);}
            }
            TemplateLabel(g,card.CardId+(card.Revision>0?" · R"+card.Revision.ToString(CultureInfo.InvariantCulture):""),Box(layout,"Number",width,height),Color.FromArgb(58,65,74),false,80,14);
            using(var font=Typeface(12,FontStyle.Bold))Label(g,"HYTCG"+(!custom && card.ImageSource=="zerochan"?"  /  ALT":""),font,Color.FromArgb(58,65,74),width*(landscape?0.65f:0.80f),height*(landscape?0.94f:0.936f));
        }
        private static bool LegacyLayout(HyTcgFrameLayout layout,bool landscape)
        {
            // Optional artist resources may change a legacy frame's layout. Such an
            // override composes its declared windows while existing defaults retain
            // already-issued identity pixels and the original classic studio look.
            var expected=new Dictionary<string,double> {{"ArtworkX",0.025},{"ArtworkY",0.025},{"ArtworkWidth",0.95},{"ArtworkHeight",0.95},
                {"NameX",0.067},{"NameY",landscape?0.70:0.78},{"NameWidth",0.84},{"NameHeight",0.10},
                {"SeriesX",0.067},{"SeriesY",landscape?0.82:0.865},{"SeriesWidth",0.84},{"SeriesHeight",0.05},
                {"RarityX",0.76},{"RarityY",0.055},{"RarityWidth",0.17},{"RarityHeight",0.05},
                {"NumberX",0.067},{"NumberY",0.943},{"NumberWidth",0.87},{"NumberHeight",0.024}};
            return layout.DescriptionX==0 && layout.DescriptionY==0 && layout.DescriptionWidth==0 && layout.DescriptionHeight==0
                && expected.All(p=>Math.Abs(Convert.ToDouble(typeof(HyTcgFrameLayout).GetProperty(p.Key).GetValue(layout,null))-p.Value)<0.000001);
        }
        private static Color FrameColor(string design,HyTcgCosmetic style,Color rarity)
        {
            Color color=design=="petal" || design=="sakura"?Color.FromArgb(230,164,196):design=="arcane" || design=="moonlace"?Color.FromArgb(175,147,245)
                :design=="circuit"?Color.FromArgb(113,221,185):design=="royal" || design=="gilded"?Color.FromArgb(229,194,112)
                :design=="starlit"?Color.FromArgb(136,202,241):design=="obsidian"?Color.FromArgb(158,161,200):rarity;
            return style==null?color:Morph(ParseColor(style.Dye,color),style.Morph,style.MorphMode);
        }
        private static void DrawTemplateBackground(Graphics g,int width,int height,HyTcgFrameDefinition frame,HyTcgCosmetic style,Color rarity)
        {
            // Dye decorates the frame, never the card stock or label background.
            Color color=FrameColor(frame.Design,null,rarity);
            using(var path=Rounded(0,0,width,height,Math.Min(width,height)*0.045f))
            using(var background=new LinearGradientBrush(new Rectangle(0,0,width,height),Color.FromArgb(15+color.R/16,18+color.G/16,27+color.B/12),Color.FromArgb(7,11,21),70f))g.FillPath(background,path);
        }
        private static void DrawTemplateArtwork(Graphics g,Image image,RectangleF source,RectangleF box,bool centered=false,HyTcgCosmetic style=null)
        {
            GraphicsState saved=g.Save();
            try
            {
                using(var path=Rounded(box.X,box.Y,box.Width,box.Height,Math.Min(24,Math.Min(box.Width,box.Height)/12)))g.SetClip(path);
                using(var dark=new SolidBrush(centered?Color.FromArgb(246,243,233):Color.FromArgb(18,24,35)))g.FillRectangle(dark,box);
                float scale=(centered?Math.Min(box.Width/source.Width,box.Height/source.Height):Math.Max(box.Width/source.Width,box.Height/source.Height))*(float)(style==null || style.ArtworkScale==0?1:style.ArtworkScale),drawnWidth=source.Width*scale,drawnHeight=source.Height*scale;
                g.DrawImage(image,new RectangleF(box.X+(box.Width-drawnWidth)/2+(float)(style==null?0:style.ArtworkX)*box.Width/2,box.Y+(box.Height-drawnHeight)/2+(float)(style==null?0:style.ArtworkY)*box.Height/2,drawnWidth,drawnHeight),source,GraphicsUnit.Pixel);
                // Only the edge fades; the character's central artwork stays clear.
                if(!centered){float fade=Math.Min(50,box.Height*0.10f);
                using(var shadow=new LinearGradientBrush(new RectangleF(box.X,box.Bottom-fade,box.Width,fade),Color.Transparent,Color.FromArgb(115,7,11,21),90f))g.FillRectangle(shadow,box.X,box.Bottom-fade,box.Width,fade);}
            }
            finally {g.Restore(saved);}
        }
        private static void DrawMysticGlow(Graphics g,int width,int height,Color color,RectangleF artwork,bool mystic,HyTcgFrameLayout layout)
        {
            GraphicsState saved=g.Save();
            try
            {
                // Glow belongs to the background, outside the protected character
                // artwork and native identity. It never changes the rarity color.
                using(var outer=Rounded(0,0,width,height,37))using(var clip=new Region(outer))
                {
                    // A Mystic dye lights only the decorative rim. Keep the full
                    // footer stock neutral, including space between printed labels.
                    clip.Exclude(new RectangleF(35,35,width-70,height-70));
                    RectangleF protectedArtwork=artwork;protectedArtwork.Inflate(2,2);clip.Exclude(protectedArtwork);
                    foreach(string name in new[]{"Name","Series","Rarity","Number","Description"})
                    {RectangleF identity=Box(layout,name,width,height);identity.Inflate(2,2);clip.Exclude(identity);}
                    // Existing canonical badge is slightly different from the old
                    // studio defaults; preserve its printed rarity pixels too.
                    clip.Exclude(new RectangleF(width-270,35,240,70));g.SetClip(clip,CombineMode.Replace);
                }
                using(var path=new GraphicsPath())
                {
                    path.AddEllipse(-width*0.35f,-height*0.22f,width*1.7f,height*0.70f);
                    using(var glow=new PathGradientBrush(path))
                    {glow.CenterColor=Color.FromArgb(mystic?68:32,color);glow.SurroundColors=new[]{Color.FromArgb(0,color)};g.FillPath(glow,path);}
                }
                using(var path=new GraphicsPath())
                {
                    path.AddEllipse(-width*0.10f,height*0.67f,width*1.2f,height*0.65f);
                    using(var glow=new PathGradientBrush(path))
                    {glow.CenterColor=Color.FromArgb(mystic?50:24,color);glow.SurroundColors=new[]{Color.FromArgb(0,color)};g.FillPath(glow,path);}
                }
                if(mystic)
                {
                    Color prism=Hsl((color.GetHue()/360d+0.035)%1,Math.Max(0.25,color.GetSaturation()),Math.Min(0.72,color.GetBrightness()+0.15));
                    using(var spark=new SolidBrush(Color.FromArgb(80,prism)))
                        for(int i=0;i<8;i++)Star(g,spark,i%2==0?width*0.040f:width*0.96f,height*(0.11f+i*0.11f),i%3==0?4:2);
                }
            }
            finally {g.Restore(saved);}
        }
        private static void DrawOverlay(Graphics g,byte[] bytes,int width,int height,HyTcgCosmetic style,Color dye,HyTcgFrameLayout layout)
        {
            using(var input=new MemoryStream(bytes,false))using(var overlay=Image.FromStream(input,true,true))
            {
                GraphicsState saved=g.Save();try
                {
                    using(var outer=Rounded(0,0,width,height,32))using(var clip=new Region(outer))
                    {
                        RectangleF artwork=Box(layout,"Artwork",width,height);artwork.Inflate(2,2);clip.Exclude(artwork);
                        foreach(string name in new[]{"Name","Series","Rarity","Number","Description"})
                        {RectangleF label=Box(layout,name,width,height);if(label.Width>0 && label.Height>0){label.Inflate(3,3);clip.Exclude(label);}}
                        g.SetClip(clip,CombineMode.Replace);
                    }
                    if(style==null || String.IsNullOrEmpty(style.Dye))g.DrawImage(overlay,new Rectangle(0,0,width,height));
                    else using(var attributes=new ImageAttributes())
                    {
                        // Tint only the decorative raster. Equal weighted luminance
                        // gains retain its shading; the alpha row stays unchanged.
                        float red=dye.R/255f,green=dye.G/255f,blue=dye.B/255f;
                        float luminance=red*0.299f+green*0.587f+blue*0.114f;
                        float r=1+0.85f*(red-luminance),b=1+0.85f*(blue-luminance),v=1+0.85f*(green-luminance);
                        float tint=style.Mystic?0.45f:0.30f,keep=1-tint;
                        attributes.SetColorMatrix(new ColorMatrix(new[]{
                            new[]{keep+tint*0.299f*r,tint*0.299f*v,tint*0.299f*b,0f,0f},
                            new[]{tint*0.587f*r,keep+tint*0.587f*v,tint*0.587f*b,0f,0f},
                            new[]{tint*0.114f*r,tint*0.114f*v,keep+tint*0.114f*b,0f,0f},
                            new[]{0f,0f,0f,1f,0f},new[]{0f,0f,0f,0f,1f}}));
                        g.DrawImage(overlay,new Rectangle(0,0,width,height),0,0,overlay.Width,overlay.Height,GraphicsUnit.Pixel,attributes);
                    }
                }
                finally {g.Restore(saved);}
            }
        }
        private static void Star(Graphics g,Brush brush,float x,float y,float radius)
        {g.FillPolygon(brush,new[]{new PointF(x,y-radius),new PointF(x+radius*0.27f,y-radius*0.27f),new PointF(x+radius,y),new PointF(x+radius*0.27f,y+radius*0.27f),new PointF(x,y+radius),new PointF(x-radius*0.27f,y+radius*0.27f),new PointF(x-radius,y),new PointF(x-radius*0.27f,y-radius*0.27f)});}
        private static void DrawTemplateFrame(Graphics g,int width,int height,HyTcgFrameDefinition frame,HyTcgFrameLayout layout,HyTcgCosmetic style,Color rarity)
        {
            RectangleF art=Box(layout,"Artwork",width,height);Color color=FrameColor(frame.Design,style,rarity);float unit=Math.Min(width,height);
            GraphicsState protectedIdentity=g.Save();try
            {
            // Some artist layouts place a label across a window or outer edge.
            // Frame strokes, just like raster overlays, must leave it untouched.
            using(var clip=new Region(new Rectangle(0,0,width,height)))
            {
                foreach(string name in new[]{"Name","Series","Rarity","Number","Description"})
                {RectangleF label=Box(layout,name,width,height);if(label.Width>0 && label.Height>0){label.Inflate(3,3);clip.Exclude(label);}}
                g.SetClip(clip,CombineMode.Replace);
            }
            using(var line=new Pen(color,2))using(var fine=new Pen(Color.FromArgb(120,color),1))using(var fill=new SolidBrush(Color.FromArgb(220,color)))
            {
                using(var outer=Rounded(12,12,width-24,height-24,28))g.DrawPath(line,outer);
                using(var inner=Rounded(20,20,width-40,height-40,23))g.DrawPath(fine,inner);
                using(var window=Rounded(art.X-3,art.Y-3,art.Width+6,art.Height+6,23))g.DrawPath(fine,window);
                GraphicsState saved=g.Save();
                try
                {
                    using(var clip=new Region(new Rectangle(0,0,width,height)))
                    {
                        clip.Exclude(art);
                        foreach(string name in new[]{"Name","Series","Rarity","Number","Description"})
                        {RectangleF label=Box(layout,name,width,height);if(label.Width>0 && label.Height>0){label.Inflate(3,3);clip.Exclude(label);}}
                        g.SetClip(clip,CombineMode.Replace);
                    }
                    if(frame.Design=="moonlace")
                    {
                        using(var lace=new Pen(Color.FromArgb(190,color),1.5f))
                        {
                            for(int side=0;side<2;side++)
                            {float x=side==0?art.Left-11:art.Right+11;for(float y=art.Top+30;y<art.Bottom-20;y+=36)g.DrawArc(lace,x-8,y-14,16,28,side==0?90:270,180);}
                            g.DrawArc(lace,width*0.30f,16,width*0.40f,height*0.075f,185,170);
                        }
                        g.FillEllipse(fill,width*0.10f,30,24,24);using(var cut=new SolidBrush(Color.FromArgb(20,23,41)))g.FillEllipse(cut,width*0.10f+8,26,23,23);
                    }
                    else if(frame.Design=="starlit")
                    {
                        float[] ys={0.16f,0.32f,0.55f,0.72f,0.88f};
                        for(int side=0;side<2;side++)for(int i=0;i<ys.Length;i++)
                        {float x=side==0?width*0.032f:width*0.968f,y=height*ys[i];Star(g,fill,x,y,i%2==0?7:4);if(i>0)g.DrawLine(fine,x,height*ys[i-1]+9,x,y-9);}
                        Star(g,fill,width*0.50f,35,11);Star(g,fill,width*0.42f,35,4);Star(g,fill,width*0.58f,35,4);
                    }
                    else if(frame.Design=="sakura")
                    {
                        for(int side=0;side<2;side++)
                        {
                            float x=side==0?width*0.038f:width*0.962f,direction=side==0?1:-1;
                            g.DrawBezier(line,x,height*0.85f,x+direction*22,height*0.53f,x-direction*16,height*0.25f,x+direction*15,height*0.10f);
                            for(int i=0;i<7;i++)Flower(g,fill,(int)(x+direction*(i%2==0?13:0)),(int)(height*(0.16+i*0.10)));
                        }
                        using(var ribbon=new Pen(Color.FromArgb(85,color),5))g.DrawLine(ribbon,art.Left+25,art.Bottom+13,art.Right-25,art.Bottom+13);
                    }
                    else if(frame.Design=="obsidian")
                    {
                        float bevel=unit*0.065f;using(var bevelPen=new Pen(color,3))g.DrawPolygon(bevelPen,new[]{new PointF(24+bevel,24),new PointF(width-24-bevel,24),new PointF(width-24,24+bevel),new PointF(width-24,height-24-bevel),new PointF(width-24-bevel,height-24),new PointF(24+bevel,height-24),new PointF(24,height-24-bevel),new PointF(24,24+bevel)});
                        for(int i=0;i<4;i++){Diamond(g,fill,29,(int)(height*(0.22+i*0.18)),8);Diamond(g,fill,width-29,(int)(height*(0.22+i*0.18)),8);}
                        g.DrawLine(line,width*0.25f,height-42,width*0.75f,height-42);
                    }
                    else if(frame.Design=="gilded")
                    {
                        using(var gold=new Pen(Color.FromArgb(180,color),1.5f))
                        {
                            using(var outer=Rounded(7,7,width-14,height-14,32))g.DrawPath(gold,outer);
                            for(int side=0;side<2;side++)for(int end=0;end<2;end++)
                            {float x=side==0?18:width-76,y=end==0?18:height-76;g.DrawArc(gold,x,y,58,58,side==0?0:180,270);g.DrawArc(gold,x+8,y+8,42,42,side==0?20:200,245);}
                        }
                        Crown(g,line,fill,width/2,35);for(int i=1;i<6;i++)Diamond(g,fill,width*i/6,height-24,4);
                    }
                }
                finally {g.Restore(saved);}
            }
            }
            finally {g.Restore(protectedIdentity);}
        }
        private static void TemplateLabel(Graphics g,string text,RectangleF box,Color color,bool bold,int maximum,float preferred)
        {
            text=Text(text,maximum);int lineLimit=bold?2:maximum>80?Math.Max(1,Math.Min(3,(int)(box.Height/24))):1;
            float size=Math.Max(8,Math.Min(preferred,box.Height/(lineLimit*1.15f)));
            while(size>8)
            {using(var font=Typeface(size,bold?FontStyle.Bold:FontStyle.Regular))if(Measure(g,text,font)<=box.Width*lineLimit && font.GetHeight(g)*lineLimit<=box.Height)break;size--;}
            using(var font=Typeface(size,bold?FontStyle.Bold:FontStyle.Regular))
            {
                List<string> lines=TemplateLines(g,text,font,box.Width,lineLimit);float lineHeight=font.GetHeight(g),total=lines.Count*lineHeight;
                GraphicsState saved=g.Save();try
                {g.SetClip(box);for(int i=0;i<lines.Count;i++)Label(g,lines[i],font,color,box.X,box.Y+Math.Max(0,(box.Height-total)/2)+i*lineHeight);}
                finally {g.Restore(saved);}
            }
        }
        private static List<string> TemplateLines(Graphics g,string text,Font font,float width,int maximum)
        {
            var lines=new List<string>();string current="";var iterator=StringInfo.GetTextElementEnumerator(text);bool truncated=false;
            while(iterator.MoveNext())
            {
                string element=(string)iterator.Current;
                if(current!="" && Measure(g,current+element,font)>width)
                {
                    int space=current.LastIndexOf(' ');string line=space>0?current.Substring(0,space):current;
                    current=(space>0?current.Substring(space+1):"")+element;lines.Add(line.Trim());
                    if(lines.Count>=maximum) {truncated=true;break;}
                }
                else current+=element;
            }
            if(lines.Count<maximum && current.Trim()!="")lines.Add(current.Trim());
            if(truncated && lines.Count>0)
            {
                string last=lines[lines.Count-1];while(last!="" && Measure(g,last+"…",font)>width)
                {int[] positions=StringInfo.ParseCombiningCharacters(last);last=last.Substring(0,positions[positions.Length-1]);}
                lines[lines.Count-1]=last+"…";
            }
            return lines;
        }
        private static void DrawTemplateIdentity(Graphics g,HyTcgCard card,int width,int height,HyTcgFrameDefinition frame,HyTcgFrameLayout layout,Color rarity,bool custom)
        {
            RectangleF name=Box(layout,"Name",width,height),series=Box(layout,"Series",width,height),badge=Box(layout,"Rarity",width,height),number=Box(layout,"Number",width,height);
            // Identity stays on the neutral stock. Decorative overlays are clipped
            // away from these boxes; labels do not need opaque background plates.
            TemplateLabel(g,card.Name,name,Color.White,true,180,44);
            TemplateLabel(g,String.IsNullOrWhiteSpace(card.Series)?"Unknown series":card.Series,series,Color.FromArgb(199,208,224),false,500,23);
            TemplateLabel(g,card.Rarity,badge,rarity,true,32,25);
            string nativeNumber=card.CardId+(card.Revision>0?" · R"+card.Revision.ToString(CultureInfo.InvariantCulture):"");
            TemplateLabel(g,nativeNumber,number,Color.FromArgb(195,206,221),false,80,14);
            if(layout.DescriptionWidth>0 && layout.DescriptionHeight>0)
            {
                RectangleF description=Box(layout,"Description",width,height);
                TemplateLabel(g,String.IsNullOrWhiteSpace(frame.CardDescription)?CardDescription(card):frame.CardDescription,description,Color.FromArgb(199,208,224),false,600,21);
            }
            RectangleF art=Box(layout,"Artwork",width,height);
            using(var brand=Typeface(11,FontStyle.Bold))Label(g,custom?"HYTCG  /  CUSTOM":"HYTCG  /  "+card.CharacterId.ToString(CultureInfo.InvariantCulture),brand,Color.FromArgb(188,196,213),art.X+8,art.Y+8);
        }
        private static void DrawArtwork(Graphics g,Image image,int width,int height)
        {
            GraphicsState saved=g.Save();
            try
            {
                using(var clip=Rounded(18,18,width-36,height-36,25))g.SetClip(clip);
                using(var dark=new SolidBrush(Color.FromArgb(18,24,35)))g.FillRectangle(dark,18,18,width-36,height-36);
                float scale=Math.Max((width-36)/(float)image.Width,(height-36)/(float)image.Height);
                float drawnWidth=image.Width*scale,drawnHeight=image.Height*scale;
                g.DrawImage(image,new RectangleF(18+(width-36-drawnWidth)/2,18+(height-36-drawnHeight)*0.25f,drawnWidth,drawnHeight));
                using(var top=new LinearGradientBrush(new Rectangle(0,18,width,220),Color.FromArgb(210,7,11,23),Color.FromArgb(0,7,11,23),90f))g.FillRectangle(top,18,18,width-36,220);
                int fadeY=height>width?650:(int)(height*0.47);
                using(var bottom=new LinearGradientBrush(new Rectangle(0,fadeY,width,height-fadeY),Color.Transparent,Color.FromArgb(8,13,24),90f))
                {
                    bottom.InterpolationColors=new ColorBlend {Positions=new[]{0f,0.4f,0.75f,1f},Colors=new[]{Color.FromArgb(0,8,13,24),Color.FromArgb(168,8,13,24),Color.FromArgb(242,8,13,24),Color.FromArgb(255,8,13,24)}};
                    g.FillRectangle(bottom,18,fadeY,width-36,height-fadeY);
                }
            }
            finally {g.Restore(saved);}
        }
        private static Font Typeface(float size,FontStyle weight)
        {
            try {return new Font("Segoe UI",size,weight,GraphicsUnit.Pixel);}
            catch {return new Font(FontFamily.GenericSansSerif,size,weight,GraphicsUnit.Pixel);}
        }
        private static string Text(string value,int maximum)
        {
            value=value??"";var result=new StringBuilder();var elements=StringInfo.GetTextElementEnumerator(value);
            while(elements.MoveNext())
            {
                string element=(string)elements.Current;if(result.Length+element.Length>maximum)break;
                foreach(char character in element)result.Append(Char.IsControl(character)?' ':character);
            }
            return result.ToString().Trim();
        }
        private static float Measure(Graphics g,string text,Font font)
        {using(var format=(StringFormat)StringFormat.GenericTypographic.Clone())return g.MeasureString(text,font,Int32.MaxValue,format).Width;}
        private static List<string> Lines(Graphics g,string text,Font font,float width,int maximum)
        {
            var result=new List<string>();string current="";var iterator=StringInfo.GetTextElementEnumerator(text);bool truncated=false;
            while(iterator.MoveNext())
            {
                string element=(string)iterator.Current;
                if(current!="" && Measure(g,current+element,font)>width)
                {
                    result.Add(current.Trim());current=element;
                    if(result.Count>=maximum) {truncated=true;break;}
                }
                else current+=element;
            }
            if(result.Count<maximum && current!="")result.Add(current.Trim());
            if(truncated && result.Count>0)
            {
                string last=result[result.Count-1];
                while(last!="" && Measure(g,last+"…",font)>width)
                {int[] positions=StringInfo.ParseCombiningCharacters(last);last=last.Substring(0,positions[positions.Length-1]);}
                result[result.Count-1]=last+"…";
            }
            return result;
        }
        private static void Label(Graphics g,string text,Font font,Color color,float x,float y)
        {using(var brush=new SolidBrush(color))using(var format=(StringFormat)StringFormat.GenericTypographic.Clone())g.DrawString(text,font,brush,x,y,format);}
        private static void DrawIdentity(Graphics g,HyTcgCard card,int width,int height,Color rarity)
        {
            using(var brand=Typeface(22,FontStyle.Bold))Label(g,"HYTCG",brand,Color.FromArgb(244,244,255),46,52);
            using(var badgeFont=Typeface(25,FontStyle.Bold))
            {
                float badgeWidth=Math.Max(82,Measure(g,card.Rarity,badgeFont)+37),badgeX=width-46-badgeWidth;
                Label(g,card.Rarity,badgeFont,rarity,badgeX+(badgeWidth-Measure(g,card.Rarity,badgeFont))/2,50);
            }
            using(var character=Typeface(16,FontStyle.Bold))Label(g,"CHARACTER  /  "+card.CharacterId.ToString(CultureInfo.InvariantCulture),character,rarity,46,height-263);
            string name=Text(card.Name,180);float fontSize=44;
            while(fontSize>31) {using(var probe=Typeface(fontSize,FontStyle.Bold))if(Measure(g,name,probe)<width-92)break;fontSize--;}
            using(var nameFont=Typeface(fontSize,FontStyle.Bold))
            {
                List<string> nameLines=Lines(g,name,nameFont,width-92,2);
                for(int i=0;i<nameLines.Count;i++)Label(g,nameLines[i],nameFont,Color.White,46,height-231+i*(fontSize+7));
                using(var series=Typeface(23,FontStyle.Regular))
                {
                    var seriesLines=Lines(g,Text(String.IsNullOrWhiteSpace(card.Series)?"Unknown series":card.Series,500),series,width-92,1);
                    if(seriesLines.Count>0)Label(g,seriesLines[0],series,Color.FromArgb(196,202,217),46,height-(nameLines.Count>1?100:140));
                }
            }
            using(var separator=new Pen(Color.FromArgb(85,rarity),1))g.DrawLine(separator,46,height-67,width-46,height-67);
            using(var id=Typeface(13,FontStyle.Regular))Label(g,card.CardId+(card.Revision>0?" · R"+card.Revision.ToString(CultureInfo.InvariantCulture):""),id,Color.FromArgb(180,190,210),46,height-52);
            using(var custom=Typeface(12,FontStyle.Bold))Label(g,"CUSTOM",custom,rarity,width-46-Measure(g,"CUSTOM",custom),height-52);
        }
        private static void DrawFrame(Graphics g,int width,int height,HyTcgCosmetic style,Color rarity)
        {
            string frame=style==null?"classic":style.Frame;
            Color color=frame=="petal"?Color.FromArgb(230,164,196):frame=="arcane"?Color.FromArgb(175,147,245):frame=="circuit"?Color.FromArgb(113,221,185):frame=="royal"?Color.FromArgb(229,194,112):rarity;
            if(style!=null)color=Morph(ParseColor(style.Dye,color),style.Morph,style.MorphMode);
            using(var rim=new GraphicsPath(FillMode.Alternate))
            using(var outer=Rounded(0,0,width,height,37))
            using(var inner=Rounded(18,18,width-36,height-36,25))
            using(var dark=new SolidBrush(Color.FromArgb(17,21,33)))
            {rim.AddPath(outer,false);rim.AddPath(inner,false);g.FillPath(dark,rim);}
            using(var erase=new Pen(Color.FromArgb(17,21,33),5))using(var path=Rounded(23,23,width-46,height-46,22))g.DrawPath(erase,path);
            using(var pen=new Pen(color,5))using(var outline=Rounded(12,12,width-24,height-24,30))g.DrawPath(pen,outline);
            using(var pen=new Pen(Color.FromArgb(160,color),1))using(var outline=Rounded(23,23,width-46,height-46,22))g.DrawPath(pen,outline);
            if(frame=="classic")
            {using(var pen=new Pen(Color.FromArgb(90,color),1))using(var path=Rounded(6,6,width-12,height-12,34))g.DrawPath(pen,path);return;}
            using(var pen=new Pen(color,2))
            using(var brush=new SolidBrush(color))
            {
                if(frame=="petal")
                {
                    for(int y=140;y<height-100;y+=125) {Flower(g,brush,18,y);Flower(g,brush,width-18,y);}
                    Flower(g,brush,30,30);Flower(g,brush,width-30,30);Flower(g,brush,30,height-30);Flower(g,brush,width-30,height-30);
                }
                else if(frame=="arcane")
                {
                    for(int y=125;y<height-100;y+=90)
                    {
                        Rune(g,pen,18,y);Rune(g,pen,width-18,y);
                    }
                    for(int x=130;x<width-100;x+=100) {Rune(g,pen,x,18);Rune(g,pen,x,height-18);}
                }
                else if(frame=="circuit")
                {
                    for(int side=0;side<2;side++)
                    {
                        int x=side==0?18:width-18,sign=side==0?1:-1;
                        for(int y=105;y<height-100;y+=155)
                        {g.DrawLines(pen,new[]{new Point(x,y),new Point(x+sign*11,y+13),new Point(x+sign*11,y+48),new Point(x,y+62)});g.FillEllipse(brush,x-3,y-3,6,6);}
                        g.DrawLines(pen,new[]{new Point(x,62),new Point(x+sign*16,35),new Point(x+sign*60,35)});
                        g.FillEllipse(brush,x+sign*60-4,31,8,8);
                    }
                }
                else
                {
                    Crown(g,pen,brush,39,39);Crown(g,pen,brush,width-39,39);
                    // Footer crowns stay outside the card ID and CUSTOM text area.
                    Crown(g,pen,brush,28,height-29);Crown(g,pen,brush,width-28,height-29);
                    using(var innerPen=new Pen(Color.FromArgb(185,color),2))using(var path=Rounded(8,8,width-16,height-16,32))g.DrawPath(innerPen,path);
                    for(int y=160;y<height-100;y+=170) {Diamond(g,brush,17,y,6);Diamond(g,brush,width-17,y,6);}
                }
            }
        }
        private static void Flower(Graphics g,Brush brush,int x,int y)
        {for(int i=0;i<5;i++){double angle=i*Math.PI*2/5;g.FillEllipse(brush,x+(float)Math.Cos(angle)*7-4,y+(float)Math.Sin(angle)*7-4,8,8);}using(var core=new SolidBrush(Color.FromArgb(35,24,40)))g.FillEllipse(core,x-3,y-3,6,6);}
        private static void Rune(Graphics g,Pen pen,int x,int y)
        {g.DrawPolygon(pen,new[]{new Point(x,y-8),new Point(x+5,y),new Point(x,y+8),new Point(x-5,y)});g.DrawLine(pen,x-8,y-5,x+8,y+5);}
        private static void Diamond(Graphics g,Brush brush,int x,int y,int size)
        {g.FillPolygon(brush,new[]{new Point(x,y-size),new Point(x+size,y),new Point(x,y+size),new Point(x-size,y)});}
        private static void Crown(Graphics g,Pen pen,Brush brush,int x,int y)
        {g.FillPolygon(brush,new[]{new Point(x-13,y+6),new Point(x-16,y-8),new Point(x-7,y-2),new Point(x,y-15),new Point(x+7,y-2),new Point(x+16,y-8),new Point(x+13,y+6)});g.DrawLine(pen,x-12,y+11,x+12,y+11);}
        private static Color Morph(Color frame,string tint,string mode)
        {
            if(mode=="none" || String.IsNullOrEmpty(tint))return frame;Color color=ParseColor(tint,frame);
            if(mode=="multiply")return Color.FromArgb(frame.R*color.R/255,frame.G*color.G/255,frame.B*color.B/255);
            if(mode=="screen")return Color.FromArgb(255-(255-frame.R)*(255-color.R)/255,255-(255-frame.G)*(255-color.G)/255,255-(255-frame.B)*(255-color.B)/255);
            return Hsl(color.GetHue()/360d,mode=="color"?color.GetSaturation():frame.GetSaturation(),frame.GetBrightness());
        }
        private static Color Hsl(double hue,double saturation,double lightness)
        {
            if(saturation==0) {int grey=(int)Math.Round(lightness*255);return Color.FromArgb(grey,grey,grey);}
            double q=lightness<0.5?lightness*(1+saturation):lightness+saturation-lightness*saturation,p=2*lightness-q;
            return Color.FromArgb(Channel(p,q,hue+1d/3),Channel(p,q,hue),Channel(p,q,hue-1d/3));
        }
        private static int Channel(double p,double q,double hue)
        {if(hue<0)hue+=1;if(hue>1)hue-=1;double value=hue<1d/6?p+(q-p)*6*hue:hue<0.5?q:hue<2d/3?p+(q-p)*(2d/3-hue)*6:p;return Math.Max(0,Math.Min(255,(int)Math.Round(value*255)));}
    }
}
