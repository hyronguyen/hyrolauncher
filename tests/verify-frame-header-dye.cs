using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

class FrameHeaderDyeVerification
{
    static Assembly app; static Type cardType,cosmeticType,rendererType,serviceType;static int checks;
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;}
    static void Set(object target,string key,object value){target.GetType().GetProperty(key).SetValue(target,value,null);}
    static object Card(string rarity,string orientation,string marker)
    {
        var card=Activator.CreateInstance(cardType);Set(card,"CardId","HT-20261011-ABCDEF123456");Set(card,"CharacterId",99991);Set(card,"Name","Meredy");Set(card,"Series","Tales of Eternia");Set(card,"Gender","Female");Set(card,"Rarity",rarity);Set(card,"RarityColor","#78ceae");Set(card,"Orientation",orientation);Set(card,"ImageSource","zerochan");Set(card,"SourceImage","https://www.zerochan.net/99991.jpg");
        Set(card,"Metadata",new Dictionary<string,string>{{"hytcg_card_layout",marker},{"desc","A travelling companion from Tales of Eternia."}});return card;
    }
    static object Style(string dye,bool mystic,string frame)
    {
        var style=Activator.CreateInstance(cosmeticType);Set(style,"CardId","HT-20261011-ABCDEF123456");Set(style,"Frame",frame);Set(style,"FrameItemId","");Set(style,"DyeId",dye==""?"":"dy-qa");Set(style,"Dye",dye);Set(style,"Mystic",mystic);Set(style,"Morph","");Set(style,"MorphMode","none");Set(style,"ArtworkId","");return style;
    }
    static string Data(Bitmap image){using(var bytes=new MemoryStream()){image.Save(bytes,ImageFormat.Png);return "data:image/png;base64,"+Convert.ToBase64String(bytes.ToArray());}}
    static Bitmap Pixels(string data){using(var input=new MemoryStream(Convert.FromBase64String(data.Substring(data.IndexOf(',')+1))))using(var image=Image.FromStream(input)){return new Bitmap(image);}}
    static string Render(object card,string canonical,object style,string source,bool original)
    {
        return (string)rendererType.GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{cardType,typeof(string),cosmeticType,typeof(string),typeof(bool),app.GetType("HYROLauncherWeb.HyTcgFrameDefinition"),typeof(string),typeof(bool)},null)
            .Invoke(null,new object[]{card,canonical,style,source,false,null,null,original});
    }
    static string Canonical(bool landscape)
    {
        int width=landscape?1050:750,height=landscape?750:1050;
        using(var pixels=new Bitmap(width,height))using(var g=Graphics.FromImage(pixels))
        {g.Clear(Color.FromArgb(238,234,222));g.FillRectangle(Brushes.Black,30,30,width-60,landscape?91:98);g.FillRectangle(Brushes.Blue,landscape?57:52,landscape?142:141,landscape?577:645,landscape?499:494);return Data(pixels);}
    }
    static string Original(bool landscape)
    {
        using(var pixels=new Bitmap(landscape?900:300,landscape?300:900))using(var g=Graphics.FromImage(pixels))
        {g.Clear(Color.Blue);g.FillRectangle(Brushes.Red,0,0,pixels.Width,35);g.FillRectangle(Brushes.Lime,0,pixels.Height-35,pixels.Width,35);return Data(pixels);}
    }
    static int ColorCount(Bitmap image,Color color,int x,int y,int width,int height)
    {int result=0;for(int yy=y;yy<y+height;yy++)for(int xx=x;xx<x+width;xx++)if(image.GetPixel(xx,yy).ToArgb()==color.ToArgb())result++;return result;}
    static void RegionEqual(Bitmap first,Bitmap second,Rectangle box,string name)
    {bool same=true;for(int y=box.Top;y<box.Bottom && same;y++)for(int x=box.Left;x<box.Right;x++)if(first.GetPixel(x,y).ToArgb()!=second.GetPixel(x,y).ToArgb()){same=false;break;}Check(same,name);}
    static void Main(string[] args)
    {
        app=Assembly.LoadFrom(args[0]);cardType=app.GetType("HYROLauncherWeb.HyTcgCard");cosmeticType=app.GetType("HYROLauncherWeb.HyTcgCosmetic");rendererType=app.GetType("HYROLauncherWeb.HyTcgCosmeticRenderer");serviceType=app.GetType("HYROLauncherWeb.HyTcgService");Directory.CreateDirectory(args[1]);
        foreach(string rarity in new[]{"Common","Uncommon","Rare","SR"})foreach(string orientation in new[]{"portrait","landscape"})
        {
            bool landscape=orientation=="landscape";object card=Card(rarity,orientation,"window-v1");string canonical=Canonical(landscape),original=Original(landscape);
            using(var repaired=Pixels(Render(card,canonical,null,original,true)))using(var regular=Pixels(Render(card,canonical,Style("#FF0000",false,"classic"),original,true)))using(var mystic=Pixels(Render(card,canonical,Style("#FF0000",true,"classic"),original,true)))
            {
                Check(repaired.Width==(landscape?1050:750) && repaired.Height==(landscape?750:1050),rarity+" output orientation");
                Check(ColorCount(repaired,Color.Red,landscape?43:53,landscape?34:37,landscape?586:644,landscape?665:618)>200,"source head visible "+rarity+orientation);
                Check(ColorCount(repaired,Color.Lime,landscape?43:53,landscape?34:37,landscape?586:644,landscape?665:618)>200,"source feet visible "+rarity+orientation);
                Check(repaired.GetPixel(landscape?30:375,landscape?20:25).R>200,"no old header "+rarity+orientation);
                Rectangle stock=landscape?new Rectangle(680,45,310,640):new Rectangle(40,675,670,330);
                RegionEqual(repaired,regular,stock,"regular dye only frame "+rarity+orientation);
                RegionEqual(repaired,mystic,stock,"mystic only frame "+rarity+orientation);
                Check(ColorCount(regular,Color.Red,0,0,regular.Width,regular.Height)>ColorCount(repaired,Color.Red,0,0,repaired.Width,repaired.Height),"red frame applied "+rarity+orientation);
                repaired.Save(Path.Combine(args[1],rarity+"-"+orientation+"-repaired.png"));regular.Save(Path.Combine(args[1],rarity+"-"+orientation+"-dye.png"));
            }
            using(var withoutNetwork=Pixels(Render(card,canonical,Style("#FF0000",false,"classic"),null,false)))
            {Check(withoutNetwork.GetPixel(landscape?30:375,landscape?20:25).R>200,"fallback canonical removes header "+rarity+orientation);}
        }
        foreach(string frame in new[]{"moonlace","starlit","sakura","obsidian","gilded"})
        {
            object card=Card("SSR","portrait","");string canonical=Canonical(false);
            using(var neutral=Pixels(Render(card,canonical,Style("",false,frame),null,false)))using(var dye=Pixels(Render(card,canonical,Style("#FF0000",false,frame),null,false)))
            {
                var definition=app.GetType("HYROLauncherWeb.HyTcgFrameCatalog").GetMethod("Find",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{frame});object layout=definition.GetType().GetProperty("Portrait").GetValue(definition,null);
                foreach(string field in new[]{"Name","Series","Rarity","Number"})
                {
                    int x=(int)Math.Ceiling(Convert.ToDouble(layout.GetType().GetProperty(field+"X").GetValue(layout,null))*750),y=(int)Math.Ceiling(Convert.ToDouble(layout.GetType().GetProperty(field+"Y").GetValue(layout,null))*1050);
                    int w=(int)Math.Floor(Convert.ToDouble(layout.GetType().GetProperty(field+"Width").GetValue(layout,null))*750)-1,h=(int)Math.Floor(Convert.ToDouble(layout.GetType().GetProperty(field+"Height").GetValue(layout,null))*1050)-1;
                    RegionEqual(neutral,dye,new Rectangle(x,y,w,h),"template dye stable "+frame+" "+field);
                }
                Check(neutral.GetPixel(375,1010).ToArgb()==dye.GetPixel(375,1010).ToArgb(),"template stock neutral "+frame);
            }
        }
        Check((bool)rendererType.GetMethod("NeedsDefaultRecomposition",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{Card("Common","portrait","window-v1")}),"legacy window v1 upgraded display");
        Check(!(bool)rendererType.GetMethod("NeedsDefaultRecomposition",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new[]{Card("Common","portrait","window-v2")}),"window v2 fast path");
        string cacheRoot=Path.Combine(args[1],"isolated-cache");Directory.CreateDirectory(cacheRoot);object service=Activator.CreateInstance(serviceType,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{cacheRoot},null);
        object cacheCard=Card("Common","portrait","window-v1");byte[] sourcePixels;
        using(var source=new Bitmap(8,12))using(var drawing=Graphics.FromImage(source))using(var output=new MemoryStream()){drawing.Clear(Color.FromArgb(255,17,61,97));source.Save(output,ImageFormat.Png);sourcePixels=output.ToArray();}
        string cacheKey;using(var hash=SHA256.Create())cacheKey=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("99991\nhttps://www.zerochan.net/99991.jpg"))).Replace("-","").ToLowerInvariant();
        string cacheDirectory=Path.Combine(cacheRoot,".cache","hytcg-frame-artwork");Directory.CreateDirectory(cacheDirectory);string cacheFile=Path.Combine(cacheDirectory,cacheKey+".bin");
        byte[] encrypted=ProtectedData.Protect(sourcePixels,Encoding.UTF8.GetBytes("HYTCG original frame artwork v2\n"+cacheKey),DataProtectionScope.CurrentUser);File.WriteAllBytes(cacheFile,encrypted);
        var lookup=serviceType.GetMethod("CachedDefaultArtwork",BindingFlags.NonPublic|BindingFlags.Instance);string cached=(string)lookup.Invoke(service,new[]{cacheCard});
        Check(!String.IsNullOrEmpty(cached),"original artwork disk cache loads locally");using(var cachePixels=Pixels(cached)){Check(cachePixels.GetPixel(2,2).ToArgb()==Color.FromArgb(255,17,61,97).ToArgb(),"cache preserves artwork colors");}
        Check(!Encoding.ASCII.GetString(File.ReadAllBytes(cacheFile)).Contains("PNG"),"raw artwork cache not plain PNG");
        encrypted[encrypted.Length-3]^=93;File.WriteAllBytes(cacheFile,encrypted);object otherService=Activator.CreateInstance(serviceType,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{cacheRoot},null);
        Check(lookup.Invoke(otherService,new[]{cacheCard})==null,"tampered cache falls back safely");
        Set(cacheCard,"SourceImage","http://localhost/private.png");Check(lookup.Invoke(service,new[]{cacheCard})==null,"original repair refuses private/non-HTTPS source");
        File.WriteAllText(Path.Combine(args[1],"report.json"),new JavaScriptSerializer().Serialize(new{Passed=true,Checks=checks}));Console.WriteLine("PASS frame header and dye: "+checks+" checks");
    }
}
