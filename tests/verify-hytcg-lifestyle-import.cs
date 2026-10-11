using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

// Exercise the built application's actual internal import/storage/display APIs.
// Never instantiate the launcher UI or make a public network request.
internal static class VerifyHyTcgLifestyleImport
{
    private static Assembly app;
    private static int checks;
    private const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private static Type Native(string name) {return app.GetType("HYROLauncherWeb."+name,true);}
    private static void Check(bool ok,string message) {checks++;if(!ok)throw new Exception(message);}
    private static object Call(string type,string name,object instance,params object[] input)
    {
        MethodInfo method=Native(type).GetMethods(flags).First(m=>m.Name==name && m.GetParameters().Length==input.Length
            && m.GetParameters().Select((p,i)=>input[i]==null || p.ParameterType.IsInstanceOfType(input[i])).All(ok=>ok));
        try {return method.Invoke(instance,input);}catch(TargetInvocationException ex) {ExceptionDispatchInfo.Capture(ex.InnerException).Throw();throw;}
    }
    private static void Error(string code,Action action)
    {try {action();throw new Exception("Expected "+code);}catch(Exception ex) {Check(ex.Message==code,code+" got "+ex.Message);}}
    private static void Set(object value,string name,object field) {value.GetType().GetProperty(name).SetValue(value,field,null);}
    private static object Get(object value,string name) {return value.GetType().GetProperty(name,flags).GetValue(value,null);}
    private static byte[] ImageBytes(int width,int height,Color color,ImageFormat format)
    {using(var bitmap=new Bitmap(width,height))using(var graphics=Graphics.FromImage(bitmap))using(var output=new MemoryStream()){graphics.Clear(color);bitmap.Save(output,format);return output.ToArray();}}
    private static Dictionary<string,object> Input(params object[] fields)
    {var result=new Dictionary<string,object>();for(int i=0;i<fields.Length;i+=2)result[(string)fields[i]]=fields[i+1];return result;}
    private static object Studio(object economy,string action,Dictionary<string,object> input)
    {
        try {input["operationId"]="SC-"+Get(Call("HyTcgEconomy","CreativeSnapshot",economy),"NextStudioSequence")+"-"+Guid.NewGuid().ToString("N");return Call("HyTcgEconomy","CreativeAction",economy,action,input);}
        catch(Exception ex) {throw new Exception("Candidate Studio action: "+action,ex);}
    }
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[1]);if(root.IndexOf("output"+Path.DirectorySeparatorChar+"verification",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Import fixture must stay in output/verification.");
            Directory.CreateDirectory(root);app=Assembly.LoadFrom(Path.GetFullPath(args[0]));
            foreach(string ip in new[]{"0.0.0.0","10.0.0.2","127.0.0.1","169.254.169.254","172.16.0.1","172.31.255.254","192.168.1.1","100.64.0.1","198.18.0.1","192.0.2.1","192.88.99.1","198.51.100.1","203.0.113.1","224.0.0.1","255.255.255.255","::1","fc00::1","fe80::1","ff02::1","::ffff:127.0.0.1","::ffff:8.8.8.8","2001::1","2001:db8::1","2002::1","3fff::1"})
                Check(!(bool)Call("HyTcgArtworkImport","PublicAddress",null,IPAddress.Parse(ip)),"blocked non-public "+ip);
            foreach(string ip in new[]{"1.1.1.1","8.8.8.8","172.15.255.255","172.32.0.1","2001:4860:4860::8888"})
                Check((bool)Call("HyTcgArtworkImport","PublicAddress",null,IPAddress.Parse(ip)),"accept public unicast "+ip);
            foreach(string url in new[]{"http://example.com/art.png","file:///C:/Windows/win.ini","https://localhost/art.png","https://127.0.0.1/art.png","https://10.0.0.1/art.png","https://169.254.169.254/art.png","https://2130706433/art.png","https://user:secret@example.com/art.png","https://example.com:444/art.png","https://example.com./art.png","https://[::ffff:127.0.0.1]/art.png"})
                Error("ArtworkUrlInvalid",()=>Call("HyTcgArtworkImport","ValidateUrl",null,url));
            Check(((Uri)Call("HyTcgArtworkImport","ValidateUrl",null,"https://example.com/folder/art.jpg?width=600")).Scheme=="https","public HTTPS image URL syntax accepted without network");
            Error("ArtworkUrlInvalid",()=>Call("HyTcgArtworkImport","Fetch",null,"https://127.0.0.1/image.png",CancellationToken.None));
            Error("ArtworkInvalid",()=>Call("HyTcgArtworkImport","Drawing",null,"https://example.com/image.png"));
            Error("ArtworkInvalid",()=>Call("HyTcgArtworkImport","Drawing",null,"data:image/png;base64,%%%"));
            Error("ArtworkInvalid",()=>Call("HyTcgArtworkImport","Drawing",null,"data:image/png;base64,"+new string('A',8*1024*1024*4/3+200)));
            object economy=Activator.CreateInstance(Native("HyTcgEconomy"),flags,null,new object[]{root},null);
            byte[] png=ImageBytes(150,90,Color.FromArgb(100,20,80,200),ImageFormat.Png),jpeg=ImageBytes(150,90,Color.Teal,ImageFormat.Jpeg);
            byte[] decoded=(byte[])Call("HyTcgArtworkImport","Drawing",null,"data:image/png;base64,"+Convert.ToBase64String(png));
            Check(decoded.SequenceEqual(png),"drawing bridge passes bounded raster bytes to native normalizer");
            if(Native("HyTcgArtworkImport").GetMethod("FileBytes",flags)!=null)
            {
                string picked=Path.Combine(root,"picked.png");File.WriteAllBytes(picked,png);
                Check(((byte[])Call("HyTcgArtworkImport","FileBytes",null,picked,CancellationToken.None)).SequenceEqual(png),"file picker reads selected image through bounded native reader");
                string empty=Path.Combine(root,"empty.png");File.WriteAllBytes(empty,new byte[1]);Error("ArtworkInvalid",()=>Call("HyTcgArtworkImport","FileBytes",null,empty,CancellationToken.None));
                string oversized=Path.Combine(root,"oversized.png");using(var output=new FileStream(oversized,FileMode.CreateNew))output.SetLength(8*1024*1024+1);
                Error("ArtworkInvalid",()=>Call("HyTcgArtworkImport","FileBytes",null,oversized,CancellationToken.None));
                using(var cancellation=new CancellationTokenSource())
                {
                    cancellation.Cancel();bool stopped=false;
                    try {Call("HyTcgArtworkImport","FileBytes",null,picked,cancellation.Token);}catch(OperationCanceledException) {stopped=true;}
                    Check(stopped,"bounded local reader respects import cancellation");
                }
            }
            string artworkId=(string)Call("HyTcgEconomy","StoreCustomArtwork",economy,jpeg);
            string normalized=(string)Call("HyTcgEconomy","ReadCustomArtwork",economy,artworkId);
            Check(normalized.StartsWith("data:image/png;base64,") && Directory.GetFiles(Path.Combine(root,"hytcg-artworks"),"*.dat").Length==1,"JPEG import persists normalized protected PNG");
            Error("ArtworkInvalid",()=>Call("HyTcgEconomy","StoreCustomArtwork",economy,new byte[]{137,80,78,71,13,10,26,10}));
            Error("ArtworkInvalid",()=>Call("HyTcgEconomy","StoreCustomArtwork",economy,new byte[8*1024*1024+1]));
            object card=Activator.CreateInstance(Native("HyTcgCard"));
            string cardId="HT-20261010-AABBCCDDEEFF";Set(card,"CardId",cardId);Set(card,"CharacterId",555);Set(card,"Name","Integrated studio fixture 玉藻前");Set(card,"Series","Local fixture");
            Set(card,"Rarity","Common");Set(card,"RarityColor","#b1becd");Set(card,"CreatedAt",DateTime.UtcNow.ToString("o"));Set(card,"ImageSource","acdb");
            object reserve=Call("HyTcgEconomy","Reserve",economy,1);string token=((List<string>)Get(reserve,"Tokens"))[0];Call("HyTcgEconomy","Begin",economy,token);Call("HyTcgEconomy","AuthorizeCard",economy,token,card);
            byte[] canonical=ImageBytes(750,1050,Color.DarkSlateBlue,ImageFormat.Png),protectedCard=(byte[])Call("HyTcgStorage","Protect",null,canonical);
            Directory.CreateDirectory(Path.Combine(root,"cards"));string name=cardId+".hycard",path=Path.Combine(root,"cards",name);File.WriteAllBytes(path,protectedCard);
            Call("HyTcgEconomy","Complete",economy,token,card,name,protectedCard);
            int gemsBefore=Convert.ToInt32(Get(Call("HyTcgEconomy","Snapshot",economy),"Gems"));
            Studio(economy,"dyeBoosterBuy",Input());
            string dyeId=(string)Get(Get(Call("HyTcgEconomy","CreativeSnapshot",economy),"LastStudioReceipt"),"DyeId");
            object quote=Call("HyTcgEconomy","CreativeQuote",economy,Input("cardId",cardId,"frame","classic","dyeId",dyeId,"artworkId",artworkId));
            int artworkGems=Convert.ToInt32(Get(quote,"TotalGems"));
            Check(Convert.ToInt32(Get(quote,"ArtworkPixels"))==150*90 && artworkGems==2,"actual candidate quote counts imported image pixels and prices by ceiling10000");
            var studioSave=Input("cardId",cardId,"frame","classic","dyeId",dyeId,"artworkId",artworkId);Studio(economy,"cosmeticSave",studioSave);
            Check(Convert.ToInt32(Get(Call("HyTcgEconomy","Snapshot",economy),"Gems"))==gemsBefore-50-artworkGems,"actual candidate booster and artwork charge native50 plus quoted pixel fee");
            Call("HyTcgEconomy","CreativeAction",economy,"cosmeticSave",studioSave);
            Check(Convert.ToInt32(Get(Call("HyTcgEconomy","Snapshot",economy),"Gems"))==gemsBefore-50-artworkGems,"actual candidate applied-look receipt prevents duplicate save charges");
            byte[] before=File.ReadAllBytes(path);int files=Directory.GetFiles(Path.Combine(root,"cards")).Length;
            string display=(string)Call("HyTcgEconomy","ReadLifestyleImage",economy,cardId,false),thumbnail=(string)Call("HyTcgEconomy","ReadLifestyleImage",economy,cardId,true);
            using(var stream=new MemoryStream(Convert.FromBase64String(display.Substring(display.IndexOf(',')+1))))using(var image=Image.FromStream(stream))Check(image.Width==750 && image.Height==1050,"integrated native display uses canonical card dimensions");
            Check(thumbnail.StartsWith("data:image/jpeg;base64,") && display!="data:image/png;base64,"+Convert.ToBase64String(canonical),"integrated image bridge displays saved custom studio style");
            Check(before.SequenceEqual(File.ReadAllBytes(path)) && files==Directory.GetFiles(Path.Combine(root,"cards")).Length,"style composition never writes or replaces core card files");
            if(Native("HyTcgCosmeticRenderer").GetMethod("CacheDiagnostics",flags)!=null)
                Check(Convert.ToInt32(Get(Call("HyTcgCosmeticRenderer","CacheDiagnostics",null),"Details"))>0,"actual native renderer caches authenticated display pixels");
            byte[] corruptCard=(byte[])before.Clone();corruptCard[corruptCard.Length-1]^=1;File.WriteAllBytes(path,corruptCard);
            Error("CardInvalid",()=>Call("HyTcgEconomy","ReadLifestyleImage",economy,cardId,false));File.WriteAllBytes(path,before);
            string protectedArt=Path.Combine(root,"hytcg-artworks",artworkId+".dat");byte[] originalArt=File.ReadAllBytes(protectedArt),corruptArt=(byte[])originalArt.Clone();
            corruptArt[corruptArt.Length-1]^=1;File.WriteAllBytes(protectedArt,corruptArt);
            Error("ArtworkInvalid",()=>Call("HyTcgEconomy","ReadLifestyleImage",economy,cardId,false));File.WriteAllBytes(protectedArt,originalArt);
            Error("CardUnavailable",()=>Call("HyTcgEconomy","ReadLifestyleImage",economy,"HT-20261010-000000000000",false));
            Call("HyTcgEconomy","ResetAccount",economy,"delete");Check(!File.Exists(path) && Directory.GetFiles(Path.Combine(root,"hytcg-artworks"),"*.dat").Length==0,"native reset removes imported files from fixture account");
            if(Native("HyTcgCosmeticRenderer").GetMethod("CacheDiagnostics",flags)!=null)
            {
                object state=Call("HyTcgCosmeticRenderer","CacheDiagnostics",null);
                Check(Convert.ToInt32(Get(state,"Details"))==0 && Convert.ToInt32(Get(state,"Thumbnails"))==0 && Convert.ToInt32(Get(state,"Bytes"))==0,"actual account reset clears retained cosmetic render pixels");
            }
            Console.WriteLine("HYTCG lifestyle import verification PASS: "+checks+" checks (actual candidate assembly, private URL rejection, public address syntax, bounded drawing, corrupt/oversize rejection, JPEG normalization, protected persistence, authenticated composition).");return 0;
        }
        catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
