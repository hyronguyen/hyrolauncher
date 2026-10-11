using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal static class VerifyHyTcgCosmeticRenderer
{
    private static int checks;
    private static void Check(bool ok,string message) {checks++;if(!ok)throw new Exception(message);}
    // CLR reflection warm-up can change JavaScriptSerializer property order.
    // Immutability checks compare parsed values so only actual data changes fail.
    private static bool SameJson(string one,string two)
    {var parser=new JavaScriptSerializer();return SameValue(parser.DeserializeObject(one),parser.DeserializeObject(two));}
    private static bool SameValue(object one,object two)
    {
        if(Object.ReferenceEquals(one,two))return true;if(one==null || two==null)return false;
        var left=one as IDictionary<string,object>;var right=two as IDictionary<string,object>;
        if(left!=null || right!=null)return left!=null && right!=null && left.Count==right.Count && left.All(pair=>right.ContainsKey(pair.Key) && SameValue(pair.Value,right[pair.Key]));
        var leftArray=one as object[];var rightArray=two as object[];
        if(leftArray!=null || rightArray!=null)return leftArray!=null && rightArray!=null && leftArray.Length==rightArray.Length && leftArray.Select((value,index)=>SameValue(value,rightArray[index])).All(value=>value);
        return Object.Equals(one,two);
    }
    private static void Error(string code,Action action) {try {action();throw new Exception("Expected "+code);}catch(HyTcgException ex) {Check(ex.Code==code,code+" got "+ex.Code);}}
    private static string Picture(int width,int height,Color color)
    {using(var bitmap=new Bitmap(width,height))using(var graphics=Graphics.FromImage(bitmap))using(var output=new MemoryStream()){graphics.Clear(color);bitmap.Save(output,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(output.ToArray());}}
    private static byte[] Bytes(string data) {return Convert.FromBase64String(data.Substring(data.IndexOf(',')+1));}
    private static string NoisePicture()
    {
        using(var image=new Bitmap(750,1050,PixelFormat.Format32bppArgb))using(var output=new MemoryStream())
        {
            BitmapData locked=image.LockBits(new Rectangle(0,0,750,1050),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try {byte[] pixels=new byte[locked.Stride*1050];new Random(555).NextBytes(pixels);Marshal.Copy(pixels,0,locked.Scan0,pixels.Length);}
            finally {image.UnlockBits(locked);}
            image.Save(output,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(output.ToArray());
        }
    }
    private static string Hash(string data) {using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Bytes(data)));}
    private static Bitmap BitmapData(string data) {using(var stream=new MemoryStream(Bytes(data)))using(var original=Image.FromStream(stream))return new Bitmap(original);}
    private static HyTcgCard Card(bool landscape)
    {return new HyTcgCard {CardId="HT-20261010-ABCDEF123456",CharacterId=555,Name="A companion with a rather long name 玉藻前 タマモノマエ",Series="A native renderer fixture series with more text than a short label",Rarity="SSR",RarityColor=HyTcgRules.RarityColor("SSR"),Orientation=landscape?"landscape":"portrait",Revision=2,ImageSource="zerochan"};}
    private static HyTcgCosmetic Style(HyTcgCard card,string frame)
    {return new HyTcgCosmetic {CardId=card.CardId,Frame=frame,Dye="",Morph="",MorphMode="none",ArtworkId=""};}
    private static Rectangle PixelBox(HyTcgFrameLayout layout,string name,int width,int height)
    {var type=layout.GetType();return new Rectangle((int)(Convert.ToDouble(type.GetProperty(name+"X").GetValue(layout,null))*width),(int)(Convert.ToDouble(type.GetProperty(name+"Y").GetValue(layout,null))*height),
        (int)(Convert.ToDouble(type.GetProperty(name+"Width").GetValue(layout,null))*width),(int)(Convert.ToDouble(type.GetProperty(name+"Height").GetValue(layout,null))*height));}
    private static int Bright(Bitmap bitmap,Rectangle box)
    {int count=0;for(int y=box.Top;y<box.Bottom;y++)for(int x=box.Left;x<box.Right;x++){Color c=bitmap.GetPixel(x,y);if(c.R>130 || c.G>130 || c.B>130)count++;}return count;}
    private static bool SameBox(Bitmap one,Bitmap two,Rectangle box)
    {for(int y=box.Top;y<box.Bottom;y++)for(int x=box.Left;x<box.Right;x++)if(one.GetPixel(x,y)!=two.GetPixel(x,y))return false;return true;}
    private static int Dark(Bitmap bitmap,Rectangle box)
    {int count=0;for(int y=box.Top;y<box.Bottom;y++)for(int x=box.Left;x<box.Right;x++){Color c=bitmap.GetPixel(x,y);if(c.A>200 && c.R<110 && c.G<110 && c.B<110)count++;}return count;}
    private static bool ExactRarity(Bitmap bitmap,Rectangle box,Color color)
    {for(int y=box.Top;y<box.Bottom;y++)for(int x=box.Left;x<box.Right;x++)if(bitmap.GetPixel(x,y).ToArgb()==color.ToArgb())return true;return false;}
    private static string CanonicalMarkers(int width,int height)
    {
        using(var bitmap=new Bitmap(width,height))using(var graphics=Graphics.FromImage(bitmap))using(var output=new MemoryStream())
        {graphics.Clear(Color.DarkSlateBlue);graphics.FillRectangle(Brushes.Red,0,0,width,105);graphics.FillRectangle(Brushes.Lime,0,height>width?650:height*0.47f,width,height);bitmap.Save(output,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(output.ToArray());}
    }
    private static void Templates(string root)
    {
        var definitions=HyTcgFrameCatalog.All().Where(f=>!HyTcgFrameCatalog.Legacy(f.Id)).ToList();
        Check(definitions.Count==5 && definitions.All(HyTcgFrameCatalog.Valid),"five native shop templates have validated bounded layouts");
        var parser=new JavaScriptSerializer();Check(definitions.Select(f=>parser.Serialize(f.Portrait)).Distinct().Count()==5,"each shop design owns a distinct portrait layout");
        foreach(bool landscape in new[]{false,true})
        {
            var card=Card(landscape);int width=landscape?1050:750,height=landscape?750:1050;string canonical=CanonicalMarkers(width,height),custom=Picture(400,600,Color.Teal);var outputs=new HashSet<string>();
            string metadata=parser.Serialize(card);
            foreach(var frame in definitions)
            {
                HyTcgFrameLayout layout=landscape?frame.Landscape:frame.Portrait;var style=Style(card,frame.Id);string image=HyTcgCosmeticRenderer.Render(card,canonical,style,custom,false);outputs.Add(Hash(image));
                using(var bitmap=BitmapData(image))
                {
                    Rectangle art=PixelBox(layout,"Artwork",width,height);
                    Check(bitmap.Width==width && bitmap.Height==height && bitmap.GetPixel(art.Left+art.Width/2,art.Top+art.Height/2).ToArgb()==Color.Teal.ToArgb(),frame.Id+" uses declared "+(landscape?"landscape":"portrait")+" art window");
                    Check(Bright(bitmap,PixelBox(layout,"Name",width,height))>0 && Bright(bitmap,PixelBox(layout,"Series",width,height))>0 && Bright(bitmap,PixelBox(layout,"Number",width,height))>0,frame.Id+" native identity is visible inside its declared boxes");
                    Rectangle badge=PixelBox(layout,"Rarity",width,height);Color rarity=ColorTranslator.FromHtml(card.RarityColor);bool nativeRarity=false;
                    for(int y=badge.Top;y<badge.Bottom && !nativeRarity;y++)for(int x=badge.Left;x<badge.Right;x++)if(bitmap.GetPixel(x,y).ToArgb()==rarity.ToArgb())nativeRarity=true;
                    Check(nativeRarity,frame.Id+" retains native rarity label color");
                }
                string cropped=HyTcgCosmeticRenderer.Render(card,canonical,style,null,false);
                using(var bitmap=BitmapData(cropped)){Rectangle art=PixelBox(layout,"Artwork",width,height);Color center=bitmap.GetPixel(art.Left+art.Width/2,art.Top+art.Height/2);Check(center.ToArgb()==Color.DarkSlateBlue.ToArgb(),frame.Id+" canonical recomposition excludes old header/footer identity pixels");}
                File.WriteAllBytes(Path.Combine(root,"template-"+frame.Id+(landscape?"-landscape":"-portrait")+".png"),Bytes(image));
            }
            Check(outputs.Count==5 && SameJson(parser.Serialize(card),metadata),"five template designs remain distinct without changing native "+(landscape?"landscape":"portrait")+" identity");
        }
        var portrait=Card(false);string original=Picture(750,1050,Color.DarkSlateBlue),artwork=Picture(400,600,Color.Teal);HyTcgFrameDye dye=HyTcgEconomy.RollFrameDye(48,1),mystic=HyTcgEconomy.RollFrameDye(48,0);
        var look=Style(portrait,"moonlace");look.Dye=dye.Color;string plain=HyTcgCosmeticRenderer.Render(portrait,original,look,artwork,false);
        look.DyeId=dye.Id;string regular=HyTcgCosmeticRenderer.Render(portrait,original,look,artwork,false);look.DyeId=mystic.Id;look.Mystic=true;string rare=HyTcgCosmeticRenderer.Render(portrait,original,look,artwork,false);
        Check(Hash(plain)!=Hash(regular) && Hash(regular)!=Hash(rare),"every owned dye adds a glow and Mystic intensifies its distinct output");
        var layoutGlow=HyTcgFrameCatalog.Find("moonlace").Portrait;
        using(var baseline=BitmapData(plain))using(var normal=BitmapData(regular))using(var intensified=BitmapData(rare))
        {
            Rectangle art=PixelBox(layoutGlow,"Artwork",750,1050),badge=PixelBox(layoutGlow,"Rarity",750,1050);
            Check(SameBox(baseline,normal,art) && SameBox(normal,intensified,art),"regular and Mystic glow protect central character artwork pixels");
            Check(SameBox(baseline,normal,badge) && SameBox(normal,intensified,badge),"dyed glow never recolors the actual native rarity label");
            Color before=baseline.GetPixel(150,770),tinted=normal.GetPixel(150,770),strong=intensified.GetPixel(150,770);
            Check(tinted.R>before.R && strong.R>tinted.R,"background glow follows the selected red dye and has bounded Mystic intensity");
        }
        File.WriteAllBytes(Path.Combine(root,"mystic-moonlace.png"),Bytes(rare));
        // Optional artist catalog/resources exist only next to this fixture executable.
        string directory=HyTcgFrameCatalog.ResourceDirectory();Directory.CreateDirectory(directory);string catalog=Path.Combine(directory,"catalog.json");
        var cacheField=typeof(HyTcgFrameCatalog).GetField("catalog",BindingFlags.NonPublic|BindingFlags.Static);object previous=cacheField.GetValue(null);
        try
        {
            var extension=parser.Deserialize<HyTcgFrameDefinition>(parser.Serialize(HyTcgFrameCatalog.Find("moonlace")));extension.Id="artist_fixture";extension.Name="Local QA overlay";extension.Design="starlit";extension.PortraitOverlay="portrait-qa.png";extension.LandscapeOverlay="landscape-qa.png";
            string[] paths={Path.Combine(directory,extension.PortraitOverlay),Path.Combine(directory,extension.LandscapeOverlay)};
            for(int i=0;i<2;i++)
            {using(var bitmap=new Bitmap(i==0?750:1050,i==0?1050:750))using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Transparent);using(var brush=new SolidBrush(i==0?Color.Blue:Color.Green))graphics.FillRectangle(brush,22,200,15,50);bitmap.Save(paths[i],ImageFormat.Png);}}
            File.WriteAllText(catalog,parser.Serialize(new[]{extension}));cacheField.SetValue(null,null);
            Check(HyTcgFrameCatalog.Find(extension.Id).PriceGems==2000,"optional local frame extensions cannot invent native frame creation prices");
            string first=HyTcgCosmeticRenderer.Render(portrait,original,Style(portrait,extension.Id),artwork,false);
            using(var bitmap=BitmapData(first))Check(bitmap.GetPixel(28,220).ToArgb()==Color.Blue.ToArgb() && bitmap.GetPixel(350,350).ToArgb()==Color.Teal.ToArgb(),"trusted portrait PNG overlay preserves transparency and artwork");
            var landscape=Card(true);string second=HyTcgCosmeticRenderer.Render(landscape,Picture(1050,750,Color.DarkSlateBlue),Style(landscape,extension.Id),artwork,false);
            using(var bitmap=BitmapData(second))Check(bitmap.GetPixel(28,220).ToArgb()==Color.Green.ToArgb(),"landscape rendering selects its own trusted overlay resource");
            extension.Portrait.ArtworkX=0.18;extension.Portrait.ArtworkWidth=0.70;File.WriteAllText(catalog,parser.Serialize(new[]{extension}));cacheField.SetValue(null,null);
            string moved=HyTcgCosmeticRenderer.Render(portrait,original,Style(portrait,extension.Id),artwork,false);Check(Hash(moved)!=Hash(first),"layout replacement changes geometry without stale cached pixels");
            using(var bitmap=new Bitmap(750,1050))using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Transparent);graphics.FillRectangle(Brushes.Red,22,200,15,50);bitmap.Save(paths[0],ImageFormat.Png);}
            string replaced=HyTcgCosmeticRenderer.Render(portrait,original,Style(portrait,extension.Id),artwork,false);
            using(var bitmap=BitmapData(replaced))Check(bitmap.GetPixel(28,220).ToArgb()==Color.Red.ToArgb(),"cache fingerprint includes changed local overlay bytes");
            Color colored=Color.FromArgb(65,105,155);
            using(var bitmap=new Bitmap(750,1050))using(var graphics=Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                using(var color=new SolidBrush(colored))graphics.FillRectangle(color,22,200,15,50);
                using(var light=new SolidBrush(Color.FromArgb(170,170,170)))graphics.FillRectangle(light,22,260,15,50);
                using(var shadow=new SolidBrush(Color.FromArgb(35,35,35)))graphics.FillRectangle(shadow,22,320,15,50);
                using(var alpha=new SolidBrush(Color.FromArgb(96,colored)))graphics.FillRectangle(alpha,22,380,15,50);
                foreach(string name in new[]{"Name","Series","Rarity","Number"})
                {Rectangle box=PixelBox(extension.Portrait,name,750,1050);box.Inflate(12,12);graphics.FillRectangle(Brushes.Black,box);}
                bitmap.Save(paths[0],ImageFormat.Png);
            }
            var tintStyle=Style(portrait,extension.Id);string untouched=HyTcgCosmeticRenderer.Render(portrait,original,tintStyle,artwork,false);
            tintStyle.Dye="#FF0000";tintStyle.DyeId=dye.Id;string coloredDye=HyTcgCosmeticRenderer.Render(portrait,original,tintStyle,artwork,false);
            tintStyle.Mystic=true;string coloredMystic=HyTcgCosmeticRenderer.Render(portrait,original,tintStyle,artwork,false);
            using(var plainOverlay=BitmapData(untouched))using(var dyedOverlay=BitmapData(coloredDye))using(var rareOverlay=BitmapData(coloredMystic))
            {
                Color native=plainOverlay.GetPixel(28,220),normal=dyedOverlay.GetPixel(28,220),rareTint=rareOverlay.GetPixel(28,220);
                Check(native.ToArgb()==colored.ToArgb(),"colored overlay keeps exact native pixels when no dye is selected");
                Check(normal!=native && normal.R>native.R && rareTint.R>normal.R,"regular and Mystic dyes tint the colored decorative raster toward their selected color");
                Func<Color,double> luminance=c=>c.R*0.299+c.G*0.587+c.B*0.114;
                Check(Math.Abs(luminance(normal)-luminance(native))<2 && Math.Abs(luminance(rareTint)-luminance(native))<2,"overlay tint preserves colored source luminance and shading");
                Check(Math.Abs(luminance(dyedOverlay.GetPixel(28,280))-170)<2 && Math.Abs(luminance(dyedOverlay.GetPixel(28,340))-35)<2,"overlay dye preserves both light detail and dark shadows");
                Rectangle art=PixelBox(extension.Portrait,"Artwork",750,1050);art.Inflate(-24,-24);
                Check(SameBox(plainOverlay,dyedOverlay,art) && SameBox(dyedOverlay,rareOverlay,art),"overlay tint never filters character artwork pixels");
                Check(new[]{"Name","Series","Rarity","Number"}.All(name=>SameBox(plainOverlay,dyedOverlay,PixelBox(extension.Portrait,name,750,1050)) && SameBox(dyedOverlay,rareOverlay,PixelBox(extension.Portrait,name,750,1050))),"overlay tint leaves native labels and rarity badge pixels unchanged");
            }
            var drawOverlay=typeof(HyTcgCosmeticRenderer).GetMethod("DrawOverlay",BindingFlags.NonPublic|BindingFlags.Static);byte[] overlayBytes=File.ReadAllBytes(paths[0]);
            using(var alphaPlain=new Bitmap(750,1050))using(var alphaTinted=new Bitmap(750,1050))
            {
                using(var graphics=Graphics.FromImage(alphaPlain))drawOverlay.Invoke(null,new object[]{graphics,overlayBytes,750,1050,null,Color.Red});
                using(var graphics=Graphics.FromImage(alphaTinted))drawOverlay.Invoke(null,new object[]{graphics,overlayBytes,750,1050,tintStyle,Color.Red});
                Check(alphaPlain.GetPixel(28,400).A==96 && alphaTinted.GetPixel(28,400).A==96 && alphaTinted.GetPixel(350,350).A==0,"decorative dye matrix preserves partial and fully transparent overlay alpha");
                Check(alphaTinted.GetPixel(28,340).A==255 && alphaPlain.GetPixel(28,340).A==255,"decorative dye matrix preserves opaque shadow alpha");
            }
            var legacy=HyTcgFrameCatalog.All().Single(f=>f.Id=="classic");legacy.PortraitOverlay="legacy-portrait-qa.png";legacy.LandscapeOverlay="legacy-landscape-qa.png";
            foreach(bool horizontal in new[]{false,true})
            {
                int width=horizontal?1050:750,height=horizontal?750:1050;string resource=horizontal?legacy.LandscapeOverlay:legacy.PortraitOverlay;
                File.WriteAllBytes(Path.Combine(directory,resource),Bytes(Picture(width,height,Color.Black)));
            }
            File.WriteAllText(catalog,parser.Serialize(new[]{legacy}));cacheField.SetValue(null,null);
            foreach(bool horizontal in new[]{false,true})
            {
                var native=Card(horizontal);int width=horizontal?1050:750,height=horizontal?750:1050;string canonical=CanonicalMarkers(width,height),identity=parser.Serialize(native);
                string overlaid=HyTcgCosmeticRenderer.Render(native,canonical,Style(native,"classic"),null,false);var layout=horizontal?legacy.Landscape:legacy.Portrait;
                using(var bitmap=BitmapData(overlaid))
                {
                    Check(bitmap.GetPixel(width/2,height/2).ToArgb()==Color.Black.ToArgb(),"opaque legacy "+(horizontal?"landscape":"portrait")+" overlay is actually applied");
                    Check(Bright(bitmap,PixelBox(layout,"Name",width,height))>0 && Bright(bitmap,PixelBox(layout,"Series",width,height))>0 && Bright(bitmap,PixelBox(layout,"Number",width,height))>0,"native name, series and card number are redrawn above an opaque legacy overlay");
                    Rectangle badge=PixelBox(layout,"Rarity",width,height);Color rarity=ColorTranslator.FromHtml(native.RarityColor);bool exact=false;
                    for(int y=badge.Top;y<badge.Bottom && !exact;y++)for(int x=badge.Left;x<badge.Right;x++)if(bitmap.GetPixel(x,y).ToArgb()==rarity.ToArgb())exact=true;
                    Check(exact,"opaque legacy overlay cannot obscure the native rarity label/color");
                }
                Check(SameJson(parser.Serialize(native),identity) && HyTcgCosmeticRenderer.Render(native,canonical,null,null,false)==canonical,"legacy overlay composition preserves canonical bytes and native card identity");
                File.WriteAllBytes(Path.Combine(root,"opaque-legacy-"+(horizontal?"landscape":"portrait")+".png"),Bytes(overlaid));
            }
            extension.Portrait.ArtworkX=Double.NaN;File.WriteAllText(catalog,"[]");cacheField.SetValue(null,null);Check(!HyTcgFrameCatalog.Valid(extension),"nonfinite optional normalized layouts are rejected");
        }
        finally {cacheField.SetValue(null,previous);HyTcgCosmeticRenderer.ClearCache();}
    }
    private static void DefaultLayoutsAndInstances(string root)
    {
        var parser=new JavaScriptSerializer();var layoutMethod=typeof(HyTcgCosmeticRenderer).GetMethod("WindowLayout",BindingFlags.NonPublic|BindingFlags.Static);
        var layouts=new Dictionary<string,HyTcgFrameLayout>();
        foreach(bool landscape in new[]{false,true})
        {
            int width=landscape?1050:750,height=landscape?750:1050;var layout=(HyTcgFrameLayout)layoutMethod.Invoke(null,new object[]{landscape});layouts[landscape?"landscape":"portrait"]=layout;
            string canonical=CanonicalMarkers(width,height);
            foreach(string rarity in new[]{"Common","Uncommon","Rare","SR"})
            {
                var card=Card(landscape);card.Rarity=rarity;card.RarityColor=HyTcgRules.RarityColor(rarity);
                card.Metadata=new Dictionary<string,string>{{"desc","<b>A fixture companion</b> protects the moonlit garden &amp; welcomes a new adventure."}};string metadata=parser.Serialize(card);
                string rendered=HyTcgCosmeticRenderer.Render(card,canonical,null,null,false);
                Check(HyTcgCosmeticRenderer.UsesWindowLayout(card) && HyTcgCosmeticRenderer.NeedsDefaultRecomposition(card) && Hash(rendered)!=Hash(canonical),rarity+" uses the new default layout without replacing old canonical bytes");
                using(var bitmap=BitmapData(rendered))
                {
                    Rectangle art=PixelBox(layout,"Artwork",width,height);
                    Check(bitmap.GetPixel(art.Left+art.Width/2,art.Top+art.Height/2).ToArgb()==Color.DarkSlateBlue.ToArgb(),rarity+" recomposes only canonical artwork without old header/footer markers");
                    Check(Bright(bitmap,PixelBox(layout,"Name",width,height))>0 && Dark(bitmap,PixelBox(layout,"Series",width,height))>0 && Dark(bitmap,PixelBox(layout,"Number",width,height))>0,"default header, series footer and native number remain legible in "+(landscape?"landscape":"portrait"));
                    Rectangle description=landscape?new Rectangle((int)(width*.65),(int)(height*.44),(int)(width*.29),(int)(height*.35)):new Rectangle((int)(width*.09),(int)(height*.75),(int)(width*.82),(int)(height*.155));
                    Check(Dark(bitmap,description)>0 && ExactRarity(bitmap,PixelBox(layout,"Rarity",width,height),ColorTranslator.FromHtml(card.RarityColor)),"default card renders native description and exact native rarity color");
                }
                Check(SameJson(parser.Serialize(card),metadata) && Hash(canonical)==Hash(CanonicalMarkers(width,height)),"default native layout preserves protected card metadata and original image");
                var changed=parser.Deserialize<HyTcgCard>(metadata);changed.Metadata["desc"]="A changed protected description is visible in a new cached render.";
                Check(Hash(HyTcgCosmeticRenderer.Render(changed,canonical,null,null,false))!=Hash(rendered),"default render cache includes native description metadata");
                changed=parser.Deserialize<HyTcgCard>(metadata);changed.Gender="Female";
                Check(Hash(HyTcgCosmeticRenderer.Render(changed,canonical,null,null,false))!=Hash(rendered),"default render cache includes the displayed native gender");
                changed=parser.Deserialize<HyTcgCard>(metadata);changed.ImageSource="acdb";
                Check(Hash(HyTcgCosmeticRenderer.Render(changed,canonical,null,null,false))!=Hash(rendered),"default render cache includes ALT artwork attribution");
                var defaultStyle=Style(card,"classic");defaultStyle.Dye="#FF8080";defaultStyle.DyeId="dye-fixture";
                string dyedDefault=HyTcgCosmeticRenderer.Render(card,canonical,defaultStyle,null,false);
                using(var bitmap=BitmapData(dyedDefault))Check(Dark(bitmap,PixelBox(layout,"Description",width,height))>0,"a default dye retains the lower-rarity header/footer/description layout");
                string replacement=Picture(320,640,Color.Teal),customDefault=HyTcgCosmeticRenderer.Render(card,canonical,defaultStyle,replacement,false);
                using(var bitmap=BitmapData(customDefault))
                {Rectangle art=PixelBox(layout,"Artwork",width,height);Check(bitmap.GetPixel(art.Left+art.Width/2,art.Top+art.Height/2).ToArgb()==Color.Teal.ToArgb() && Dark(bitmap,PixelBox(layout,"Description",width,height))>0,"replacement artwork respects the lower-rarity default art window and description");}
                Check(HyTcgCosmeticRenderer.Render(changed,canonical,defaultStyle,replacement,false)==customDefault,"custom default artwork suppresses previous ALT attribution");
                card.Metadata["hytcg_card_layout"]="window-v1";
                Check(!HyTcgCosmeticRenderer.NeedsDefaultRecomposition(card) && HyTcgCosmeticRenderer.Render(card,rendered,null,null,false)==rendered,"new native-marked window export is displayed once without cropping/recomposing it again");
                string framed=HyTcgCosmeticRenderer.Render(card,rendered,Style(card,"moonlace"),null,false);var framedLayout=landscape?HyTcgFrameCatalog.Find("moonlace").Landscape:HyTcgFrameCatalog.Find("moonlace").Portrait;
                using(var bitmap=BitmapData(framed)){Rectangle art=PixelBox(framedLayout,"Artwork",width,height);Check(bitmap.GetPixel(art.Left+art.Width/2,art.Top+art.Height/2).ToArgb()==Color.DarkSlateBlue.ToArgb(),"custom frame extracts the artwork window from a new native-marked low-rarity export");}
                File.WriteAllBytes(Path.Combine(root,"default-"+rarity.ToLowerInvariant()+(landscape?"-landscape":"-portrait")+".png"),Bytes(rendered));
            }
            foreach(string rarity in new[]{"SSR","UR"})
            {var card=Card(landscape);card.Rarity=rarity;card.RarityColor=HyTcgRules.RarityColor(rarity);Check(!HyTcgCosmeticRenderer.UsesWindowLayout(card) && HyTcgCosmeticRenderer.Render(card,canonical,null,null,false)==canonical,"high rarity "+rarity+" keeps the existing canonical full-bleed image");}
            var upgrade=Card(landscape);upgrade.Rarity="SR";Check(HyTcgCosmeticRenderer.NeedsDefaultRecomposition(upgrade),"SR upgrade target uses window layout");upgrade.Rarity="SSR";upgrade.Revision=3;
            Check(!HyTcgCosmeticRenderer.NeedsDefaultRecomposition(upgrade) && HyTcgCosmeticRenderer.Render(upgrade,canonical,null,null,false)==canonical,"upgrade to SSR returns to canonical full bleed using native rarity");
        }
        File.WriteAllText(Path.Combine(root,"default-layouts.json"),parser.Serialize(layouts));
        var nativeCard=Card(false);string original=CanonicalMarkers(750,1050),custom=Picture(200,300,Color.Teal);
        var frame=HyTcgFrameCatalog.Find("moonlace");frame.Portrait.ArtworkX=.15;frame.Portrait.ArtworkWidth=.70;frame.Portrait.NameX=.18;frame.Portrait.NameWidth=.64;
        frame.Portrait.ArtworkHeight=.60;frame.Portrait.DescriptionX=.16;frame.Portrait.DescriptionY=.70;frame.Portrait.DescriptionWidth=.68;frame.Portrait.DescriptionHeight=.045;
        frame.CardDescription="A protected custom frame description positioned by its native item layout.";
        var style=Style(nativeCard,"moonlace");style.FrameItemId="FI-"+Guid.NewGuid().ToString("N");string frameAuthority=parser.Serialize(frame),styleAuthority=parser.Serialize(style),cardAuthority=parser.Serialize(nativeCard);
        string overlay;
        using(var bitmap=new Bitmap(750,1050))using(var graphics=Graphics.FromImage(bitmap))using(var stream=new MemoryStream())
        {graphics.Clear(Color.Transparent);graphics.FillRectangle(Brushes.Blue,22,200,15,50);bitmap.Save(stream,ImageFormat.Png);overlay="data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());}
        string resolved=HyTcgCosmeticRenderer.Render(nativeCard,original,style,custom,false,frame,overlay);
        using(var bitmap=BitmapData(resolved))
        {Rectangle art=PixelBox(frame.Portrait,"Artwork",750,1050);Check(bitmap.GetPixel(art.Left+art.Width/2,art.Top+art.Height/2).ToArgb()==Color.Teal.ToArgb() && bitmap.GetPixel(28,220).ToArgb()==Color.Blue.ToArgb(),"protected custom frame instance uses native-resolved layout and overlay raster");Check(Bright(bitmap,PixelBox(frame.Portrait,"Name",750,1050))>0 && ExactRarity(bitmap,PixelBox(frame.Portrait,"Rarity",750,1050),ColorTranslator.FromHtml(nativeCard.RarityColor)),"custom instance positions native identity above its decorative overlay");}
        Check(SameJson(parser.Serialize(frame),frameAuthority) && SameJson(parser.Serialize(style),styleAuthority) && SameJson(parser.Serialize(nativeCard),cardAuthority),"custom instance render does not mutate native frame/item/card authority");
        using(var bitmap=BitmapData(resolved))Check(Bright(bitmap,PixelBox(frame.Portrait,"Description",750,1050))>0,"protected custom description is rendered inside its normalized description position");
        var descriptionChanged=parser.Deserialize<HyTcgFrameDefinition>(parser.Serialize(frame));descriptionChanged.CardDescription="An independently changed custom label.";
        Check(Hash(HyTcgCosmeticRenderer.Render(nativeCard,original,style,custom,false,descriptionChanged,overlay))!=Hash(resolved),"custom description text participates in native render cache");
        var moved=parser.Deserialize<HyTcgFrameDefinition>(parser.Serialize(frame));moved.Portrait.ArtworkX=.20;moved.Portrait.ArtworkWidth=.65;
        Check(Hash(HyTcgCosmeticRenderer.Render(nativeCard,original,style,custom,false,moved,overlay))!=Hash(resolved),"protected per-instance geometry participates in native render cache");
        Error("FrameInvalid",()=>HyTcgCosmeticRenderer.Render(nativeCard,original,style,custom,false,frame,"https://invalid.example/frame.png"));
        moved.Portrait.ArtworkWidth=1;Error("FrameInvalid",()=>HyTcgCosmeticRenderer.Render(nativeCard,original,style,custom,false,moved,overlay));
        File.WriteAllBytes(Path.Combine(root,"protected-frame-instance.png"),Bytes(resolved));
        foreach(bool originalLandscape in new[]{false,true})
        {
            var owned=Card(originalLandscape);owned.Rarity="SR";owned.RarityColor=HyTcgRules.RarityColor("SR");
            string fullBleed=CanonicalMarkers(originalLandscape?1050:750,originalLandscape?750:1050);
            foreach(bool windowCanonical in new[]{false,true})
            {
                string source=windowCanonical?HyTcgCosmeticRenderer.Render(owned,fullBleed,null,null,false):fullBleed;
                if(windowCanonical)owned.Metadata=new Dictionary<string,string>{{"hytcg_card_layout","window-v1"}};
                string before=parser.Serialize(owned),sourceHash=Hash(source),frameHash=parser.Serialize(frame);
                foreach(string orientation in new[]{"portrait","landscape"})
                {
                    int width=orientation=="portrait"?750:1050,height=orientation=="portrait"?1050:750;
                    string preview=HyTcgCosmeticRenderer.RenderDraftPreview(owned,source,frame,overlay,false,orientation);
                    using(var bitmap=BitmapData(preview))
                    {Rectangle art=PixelBox(orientation=="portrait"?frame.Portrait:frame.Landscape,"Artwork",width,height);Check(bitmap.Width==width && bitmap.Height==height && bitmap.GetPixel(art.Left+art.Width/2,art.Top+art.Height/2).ToArgb()==Color.DarkSlateBlue.ToArgb(),"readonly "+orientation+" draft preview extracts only owned "+(windowCanonical?"window":"full-bleed")+" artwork from "+(originalLandscape?"landscape":"portrait")+" canonical image");}
                    string thumb=HyTcgCosmeticRenderer.RenderDraftPreview(owned,source,frame,overlay,true,orientation);
                    using(var bitmap=BitmapData(thumb))Check(bitmap.Width==(orientation=="portrait"?280:392) && bitmap.Height==(orientation=="portrait"?392:280),"cross-orientation draft preview preserves native thumbnail dimensions");
                    File.WriteAllBytes(Path.Combine(root,"draft-"+(originalLandscape?"landscape":"portrait")+"-"+(windowCanonical?"window":"full")+"-to-"+orientation+".png"),Bytes(preview));
                }
                Check(SameJson(before,parser.Serialize(owned)),"draft preview leaves native card unchanged");
                Check(sourceHash==Hash(source),"draft preview leaves canonical pixels unchanged");
                Check(SameJson(frameHash,parser.Serialize(frame)),"draft preview leaves frame definition unchanged");
            }
        }
        Error("FrameInvalid",()=>HyTcgCosmeticRenderer.RenderDraftPreview(nativeCard,original,frame,overlay,false,"square"));
        Error("CardInvalid",()=>HyTcgCosmeticRenderer.RenderDraftPreview(nativeCard,Picture(1050,750,Color.Blue),frame,overlay,false,"landscape"));
    }
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);if(root.IndexOf("output"+Path.DirectorySeparatorChar+"verification",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Renderer fixture must stay in output/verification.");
            Directory.CreateDirectory(root);var card=Card(false);var parser=new JavaScriptSerializer();string identity=parser.Serialize(card);
            string canonical=Picture(750,1050,Color.DarkSlateBlue),transparent=Picture(640,640,Color.Transparent),custom=Picture(640,320,Color.Teal);
            string original=HyTcgCosmeticRenderer.Render(card,canonical,null,null,false);Check(original==canonical,"unmodified canonical picture stays byte-identical");
            var hashes=new HashSet<string>();
            foreach(string frame in new[]{"classic","petal","arcane","circuit","royal"})
            {
                string rendered=HyTcgCosmeticRenderer.Render(card,canonical,Style(card,frame),null,false);hashes.Add(Hash(rendered));
                using(var bitmap=BitmapData(rendered)) {Check(bitmap.Width==750 && bitmap.Height==1050,frame+" portrait dimensions");Check(bitmap.GetPixel(350,350).ToArgb()==Color.DarkSlateBlue.ToArgb(),frame+" frame leaves card interior unchanged");}
                File.WriteAllBytes(Path.Combine(root,frame+".png"),Bytes(rendered));
            }
            Check(hashes.Count==5,"all five frame presets produce distinct geometry");
            var style=Style(card,"classic");style.Dye="#FF0000";string dyed=HyTcgCosmeticRenderer.Render(card,canonical,style,null,false);
            using(var bitmap=BitmapData(dyed)) {Color rim=bitmap.GetPixel(12,200);Check(rim.R>200 && rim.G<20 && rim.B<20,"dye colors the frame rim");Check(bitmap.GetPixel(350,350).ToArgb()==Color.DarkSlateBlue.ToArgb(),"dye never alters canonical art");}
            var modes=new HashSet<string>();style.Dye="#93B6CD";style.Morph="#E36A42";
            foreach(string mode in new[]{"none","hue","multiply","color","screen"})
            {
                style.MorphMode=mode;string rendered=HyTcgCosmeticRenderer.Render(card,canonical,style,null,false);modes.Add(Hash(rendered));
                using(var bitmap=BitmapData(rendered))Check(bitmap.GetPixel(350,350).ToArgb()==Color.DarkSlateBlue.ToArgb(),mode+" morph affects frame only");
            }
            Check(modes.Count==5,"all morph blend modes have distinct output");
            style=Style(card,"royal");style.Dye="#E85555";
            string customCard=HyTcgCosmeticRenderer.Render(card,canonical,style,custom,false);File.WriteAllBytes(Path.Combine(root,"custom-portrait.png"),Bytes(customCard));
            using(var bitmap=BitmapData(customCard))
            {
                Check(bitmap.GetPixel(350,350).ToArgb()==Color.Teal.ToArgb(),"custom image crops to fill artwork area");
                Check(bitmap.GetPixel(350,1000).R<30 && bitmap.GetPixel(350,1000).G<35,"bottom gradient keeps native labels readable");
                Check(bitmap.GetPixel(30,30).ToArgb()!=Color.Teal.ToArgb(),"custom art respects rounded frame and corner geometry");
            }
            string classicCustom=HyTcgCosmeticRenderer.Render(card,canonical,Style(card,"classic"),custom,false);
            using(var royalBitmap=BitmapData(customCard))using(var classicBitmap=BitmapData(classicCustom))
            {
                bool footerSame=true;
                for(int y=990;y<1020;y++)for(int x=46;x<704;x++)if(royalBitmap.GetPixel(x,y)!=classicBitmap.GetPixel(x,y))footerSame=false;
                Check(footerSame,"royal corners cannot obscure card ID or CUSTOM labels");
            }
            string transparentCard=HyTcgCosmeticRenderer.Render(card,canonical,style,transparent,false);
            using(var bitmap=BitmapData(transparentCard))Check(bitmap.GetPixel(350,350).ToArgb()==Color.FromArgb(18,24,35).ToArgb(),"transparent custom image cannot expose canonical art or ALT attribution");
            string thumbnail=HyTcgCosmeticRenderer.Render(card,canonical,style,custom,true);
            using(var bitmap=BitmapData(thumbnail))Check(thumbnail.StartsWith("data:image/jpeg;base64,") && bitmap.Height==392 && bitmap.Width==280,"native thumbnail preserves portrait ratio");
            var landscape=Card(true);string landscapeCanonical=Picture(1050,750,Color.DarkSlateBlue);
            string landscapeRender=HyTcgCosmeticRenderer.Render(landscape,landscapeCanonical,Style(landscape,"circuit"),custom,false);File.WriteAllBytes(Path.Combine(root,"custom-landscape.png"),Bytes(landscapeRender));
            using(var bitmap=BitmapData(landscapeRender))Check(bitmap.Width==1050 && bitmap.Height==750,"landscape output preserves native dimensions");
            Check(SameJson(parser.Serialize(card),identity) && Hash(canonical)==Hash(original),"renderer leaves card model and canonical bytes untouched");
            Error("CardInvalid",()=>HyTcgCosmeticRenderer.Render(card,landscapeCanonical,style,null,false));
            Error("CardInvalid",()=>HyTcgCosmeticRenderer.Render(card,"https://example.com/card.png",style,null,false));
            Error("ArtworkInvalid",()=>HyTcgCosmeticRenderer.Render(card,canonical,style,"data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=",false));
            Error("ArtworkInvalid",()=>HyTcgCosmeticRenderer.Render(card,canonical,style,Picture(1700,1,Color.Blue),false));
            var badStyle=Style(card,"classic");badStyle.CardId="HT-20261010-000000000000";Error("CosmeticInvalid",()=>HyTcgCosmeticRenderer.Render(card,canonical,badStyle,null,false));
            badStyle=Style(card,"classic");badStyle.Dye="red";Error("CosmeticInvalid",()=>HyTcgCosmeticRenderer.Render(card,canonical,badStyle,null,false));
            badStyle=Style(card,"classic");badStyle.MorphMode="unknown";Error("CosmeticInvalid",()=>HyTcgCosmeticRenderer.Render(card,canonical,badStyle,null,false));
            HyTcgCosmeticRenderer.ClearCache();style=Style(card,"petal");
            string first=HyTcgCosmeticRenderer.Render(card,canonical,style,custom,true),second=HyTcgCosmeticRenderer.Render(card,canonical,style,custom,true);
            var diagnostics=parser.Deserialize<Dictionary<string,object>>(parser.Serialize(HyTcgCosmeticRenderer.CacheDiagnostics()));
            Check(Object.ReferenceEquals(first,second) && Convert.ToInt64(diagnostics["Hits"])==1 && Convert.ToInt64(diagnostics["Misses"])==1,"repeated verified pixels use native render cache");
            style.Morph="#224488";style.MorphMode="multiply";string changed=HyTcgCosmeticRenderer.Render(card,canonical,style,custom,true);
            Check(Hash(changed)!=Hash(first),"cache key includes all saved style inputs");
            Check(Hash(HyTcgCosmeticRenderer.Render(card,canonical,style,transparent,true))!=Hash(changed),"cache key includes custom artwork bytes");
            for(int i=0;i<75;i++) {style.Dye="#"+(0xAB0000+i).ToString("X6");HyTcgCosmeticRenderer.Render(card,canonical,style,custom,true);}
            for(int i=0;i<14;i++) {style.Dye="#"+(0xCD0000+i).ToString("X6");HyTcgCosmeticRenderer.Render(card,canonical,style,custom,false);}
            diagnostics=parser.Deserialize<Dictionary<string,object>>(parser.Serialize(HyTcgCosmeticRenderer.CacheDiagnostics()));
            Check(Convert.ToInt32(diagnostics["Thumbnails"])<=64 && Convert.ToInt32(diagnostics["Details"])<=12 && Convert.ToInt32(diagnostics["Bytes"])<=32*1024*1024,"renderer LRU respects separate entry caps and combined32MB memory budget");
            HyTcgCosmeticRenderer.ClearCache();string noise=NoisePicture();style=Style(card,"classic");
            for(int i=0;i<6;i++) {style.Dye="#"+(0xAB5522+i).ToString("X6");HyTcgCosmeticRenderer.Render(card,noise,style,null,false);}
            diagnostics=parser.Deserialize<Dictionary<string,object>>(parser.Serialize(HyTcgCosmeticRenderer.CacheDiagnostics()));
            Check(Convert.ToInt32(diagnostics["Details"])<6 && Convert.ToInt32(diagnostics["Bytes"])<=32*1024*1024,"large detailed images trigger memory eviction before the count limit");
            HyTcgCosmeticRenderer.ClearCache();diagnostics=parser.Deserialize<Dictionary<string,object>>(parser.Serialize(HyTcgCosmeticRenderer.CacheDiagnostics()));
            Check(Convert.ToInt32(diagnostics["Thumbnails"])==0 && Convert.ToInt32(diagnostics["Details"])==0 && Convert.ToInt32(diagnostics["Bytes"])==0,"reset hook clears all retained render pixels");
            Templates(root);
            DefaultLayoutsAndInstances(root);
            Console.WriteLine("HYTCG cosmetic renderer verification PASS: "+checks+" checks (legacy compatibility, catalog layout templates, dye/Mystic glow, overlay transparency, identity, native thumbnails and bounded cache).");
            Console.WriteLine("Visual fixtures: "+root);return 0;
        }
        catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
