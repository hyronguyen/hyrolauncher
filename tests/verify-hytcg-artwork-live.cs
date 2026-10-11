using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

// Explicit live check against a copy of the actual candidate assembly. No launcher
// UI is instantiated and the native account path is always a generated QA folder.
// Optional JPEG fixture: Dschwen, Gull portrait ca usa.jpg, CC BY 2.5,
// https://commons.wikimedia.org/wiki/File:Gull_portrait_ca_usa.jpg
internal static class VerifyHyTcgArtworkLive
{
    private static Assembly app;
    private static int checks;
    private const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private static Type Native(string name) {return app.GetType("HYROLauncherWeb."+name,true);}
    private static object Call(string type,string name,object instance,params object[] input)
    {
        MethodInfo method=Native(type).GetMethods(flags).First(m=>m.Name==name && m.GetParameters().Length==input.Length
            && m.GetParameters().Select((p,i)=>input[i]==null || p.ParameterType.IsInstanceOfType(input[i])).All(ok=>ok));
        try {return method.Invoke(instance,input);}catch(TargetInvocationException error) {throw error.InnerException;}
    }
    private static object Get(object value,string name) {return value.GetType().GetProperty(name,flags).GetValue(value,null);}
    private static void Check(bool pass,string name) {checks++;if(!pass)throw new Exception(name);}
    private static void Wait(Task operation,CancellationToken token)
    {if(Task.WaitAny(new[]{operation},15000,token)<0)throw new TimeoutException("Diagnostic stage exceeded 15 seconds");operation.GetAwaiter().GetResult();}
    private static string Diagnose(string url)
    {
        var result=new StringBuilder();
        try
        {
            // This is a failure diagnostic only, with the same native URL/address
            // policy and pinned destination. It reads headers, never a response body.
            Uri uri=(Uri)Call("HyTcgArtworkDownload","ValidateRequestUrl",null,url);string host=uri.IdnHost.Trim('[',']');
            using(var timeout=new CancellationTokenSource(45000))
            {
                var dns=Dns.GetHostAddressesAsync(host);Wait(dns,timeout.Token);var addresses=dns.GetAwaiter().GetResult();
                result.AppendLine("DNS: "+String.Join(", ",addresses.Select(a=>a+" public="+Call("HyTcgArtworkDownload","AllowedAddress",null,a))));
                var pinned=(IPAddress)Call("HyTcgArtworkDownload","ValidatedAddress",null,(object)addresses);result.AppendLine("Pinned: "+pinned);
                using(var client=new TcpClient(pinned.AddressFamily))using(timeout.Token.Register(delegate {try {client.Close();}catch {}}))
                {
                    Wait(client.ConnectAsync(pinned,443),timeout.Token);result.AppendLine("TCP: connected");
                    using(var ssl=new SslStream(client.GetStream(),false))
                    {
                        ssl.ReadTimeout=15000;ssl.WriteTimeout=15000;Wait(ssl.AuthenticateAsClientAsync(host,null,SslProtocols.Tls12,false),timeout.Token);
                        result.AppendLine("TLS: "+ssl.SslProtocol+" authenticated="+ssl.IsAuthenticated+" encrypted="+ssl.IsEncrypted);
                        byte[] request=(byte[])Call("HyTcgArtworkDownload","RequestBytes",null,uri);ssl.Write(request,0,request.Length);ssl.Flush();
                        var head=new MemoryStream();int previous=0;
                        while(head.Length<65536)
                        {
                            timeout.Token.ThrowIfCancellationRequested();int next=ssl.ReadByte();if(next<0)break;head.WriteByte((byte)next);
                            if(previous==13 && next==10 && head.Length>=4)
                            {byte[] bytes=head.GetBuffer();int n=(int)head.Length;if(bytes[n-4]==13 && bytes[n-3]==10)break;}previous=next;
                        }
                        string headers=Encoding.ASCII.GetString(head.ToArray());
                        foreach(string line in headers.Split(new[]{"\r\n"},StringSplitOptions.RemoveEmptyEntries))
                            if(line.StartsWith("HTTP/",StringComparison.Ordinal) || line.StartsWith("Content-Type:",StringComparison.OrdinalIgnoreCase)
                                || line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase) || line.StartsWith("Content-Encoding:",StringComparison.OrdinalIgnoreCase)
                                || line.StartsWith("Transfer-Encoding:",StringComparison.OrdinalIgnoreCase))result.AppendLine(line);
                    }
                }
            }
        }
        catch(Exception error) {result.AppendLine("Diagnostic error: "+error);}
        return result.ToString();
    }
    public static int Main(string[] args)
    {
        string root="";bool logging=false;
        try
        {
            root=Path.GetFullPath(args[1]);string candidate=Path.GetFullPath(args[0]);
            if(!String.Equals(root.TrimEnd(Path.DirectorySeparatorChar),AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)
                || root.IndexOf("hytcg-artwork-live-",StringComparison.OrdinalIgnoreCase)<0
                || !candidate.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Live verifier requires its copied candidate and generated fixture directory");
            app=Assembly.LoadFrom(candidate);Call("ApiActivityLog","Start",null,root);logging=true;
            string account=Path.Combine(root,"account");Directory.CreateDirectory(account);
            object economy=Activator.CreateInstance(Native("HyTcgEconomy"),flags,null,new object[]{account},null);
            object before=Call("HyTcgEconomy","Snapshot",economy);int gems=Convert.ToInt32(Get(before,"Gems")),credits=Convert.ToInt32(Get(before,"FreeCredits"));
            var timer=Stopwatch.StartNew();byte[] source=(byte[])Call("HyTcgArtworkImport","Fetch",null,args[2],CancellationToken.None);timer.Stop();
            Check(source.Length>=24 && source.Length<=8*1024*1024,"Actual candidate fetch obeys image byte bound");
            int sourceWidth,sourceHeight;
            using(var input=new MemoryStream(source,false))using(var image=Image.FromStream(input,true,true))
            {sourceWidth=image.Width;sourceHeight=image.Height;Check(image.RawFormat.Guid==ImageFormat.Png.Guid || image.RawFormat.Guid==ImageFormat.Jpeg.Guid,"TLS response decodes as PNG/JPEG");}
            string artwork=(string)Call("HyTcgEconomy","StoreCustomArtwork",economy,source);
            string data=(string)Call("HyTcgEconomy","ReadCustomArtwork",economy,artwork);
            Check(data.StartsWith("data:image/png;base64,",StringComparison.Ordinal) && artwork.Length==64,"Native import returns content ID and normalized PNG");
            byte[] normalized=Convert.FromBase64String(data.Substring(data.IndexOf(',')+1));int width,height;
            using(var input=new MemoryStream(normalized,false))using(var image=Image.FromStream(input,true,true))
            {width=image.Width;height=image.Height;Check(image.RawFormat.Guid==ImageFormat.Png.Guid && width>=1 && height>=1 && width<=1600 && height<=1600,"Native decoder normalizes bounded dimensions");}
            Check(normalized.Length<=4*1024*1024,"Normalized native PNG stays within persistence cap");
            string container=Path.Combine(account,"hytcg-artworks",artwork+".dat");
            Check(File.Exists(container) && Directory.GetFiles(Path.GetDirectoryName(container),"*.dat").Length==1,"Only fixture artwork is persisted in native protected storage");
            Check(!File.ReadAllBytes(container).SequenceEqual(normalized),"Persisted artwork container protects clear raster bytes");
            Check((string)Call("HyTcgEconomy","StoreCustomArtwork",economy,source)==artwork && Directory.GetFiles(Path.GetDirectoryName(container),"*.dat").Length==1,"Native duplicate import reuses protected artwork");
            object after=Call("HyTcgEconomy","Snapshot",economy);
            Check(Convert.ToInt32(Get(after,"Gems"))==gems && Convert.ToInt32(Get(after,"FreeCredits"))==credits,"Import preserves fixture wallet balance and credits");
            Check(!Directory.Exists(Path.Combine(account,"cards")) || Directory.GetFiles(Path.Combine(account,"cards")).Length==0,"Import creates no issued cards");
            // Decoder output is exported only into this QA folder for inspection.
            File.WriteAllBytes(Path.Combine(root,"normalized-qa.png"),normalized);
            var result=new Dictionary<string,object>{{"Candidate",candidate},{"Source",args[2]},{"SourceBytes",source.Length},{"SourceWidth",sourceWidth},{"SourceHeight",sourceHeight},
                {"NormalizedBytes",normalized.Length},{"NormalizedWidth",width},{"NormalizedHeight",height},{"FetchMilliseconds",timer.ElapsedMilliseconds},{"ArtworkId",artwork},{"Checks",checks},
                {"SourceAttribution",args[2]=="https://upload.wikimedia.org/wikipedia/commons/9/9a/Gull_portrait_ca_usa.jpg"
                    ?"Gull photo by Dschwen: https://commons.wikimedia.org/wiki/File:Gull_portrait_ca_usa.jpg ; https://creativecommons.org/licenses/by/2.5/ ; native PNG normalization and resizing"
                    :"Public QA source: "+args[2]}};
            string report=new JavaScriptSerializer().Serialize(result);File.WriteAllText(Path.Combine(root,"result.json"),report);Console.WriteLine(report);
            Console.WriteLine("HYTCG actual candidate live HTTPS artwork PASS · "+checks+" checks · pinned TLS1.2 fetch/native decode/protected fixture persistence");return 0;
        }
        catch(Exception error)
        {
            string detail=error.ToString();Console.Error.WriteLine(detail);
            if(root!="" && Directory.Exists(root))File.WriteAllText(Path.Combine(root,"failure.txt"),detail);
            if(app!=null && args.Length>2)
            {string diagnostic=Diagnose(args[2]);Console.Error.WriteLine(diagnostic);if(root!="" && Directory.Exists(root))File.WriteAllText(Path.Combine(root,"network-diagnostic.txt"),diagnostic);}
            return 1;
        }
        finally {if(logging)try {Call("ApiActivityLog","Stop",null);}catch {}}
    }
}
