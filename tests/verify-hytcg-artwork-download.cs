using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using HYROLauncherWeb;

internal static class VerifyHyTcgArtworkDownload
{
    private static int checks;
    private static void Check(bool condition,string name) {checks++;if(!condition)throw new Exception(name);}
    private static void Error(string code,Action action)
    {try {action();throw new Exception("Expected "+code);}catch(HyTcgException ex) {Check(ex.Code==code,"Expected "+code+" got "+ex.Code);} }
    private static byte[] Bytes(string value) {return Encoding.ASCII.GetBytes(value);}
    private static HyTcgArtworkDownload.Response Parse(string value)
    {using(var input=new MemoryStream(Bytes(value)))return HyTcgArtworkDownload.ReadResponse(input,CancellationToken.None);}
    private static void Invalid(string value) {Error("ArtworkInvalid",delegate {Parse(value);});}
    private static string Head(string fields) {return "HTTP/1.1 200 OK\r\nContent-Type: image/png\r\n"+fields+"\r\n";}
    private static string Chunked(string body) {return Head("Transfer-Encoding: chunked\r\n")+body;}
    private sealed class TrackedStream : Stream
    {
        private readonly Stream source;
        internal int MaxRequest,BulkBytes;
        internal Action OnRead;
        internal TrackedStream(Stream stream) {source=stream;}
        public override bool CanRead {get{return true;}}
        public override bool CanSeek {get{return false;}}
        public override bool CanWrite {get{return false;}}
        public override long Length {get{throw new NotSupportedException();}}
        public override long Position {get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
        public override int Read(byte[] buffer,int offset,int count)
        {MaxRequest=Math.Max(MaxRequest,count);int n=source.Read(buffer,offset,count);BulkBytes+=n;if(OnRead!=null)OnRead();return n;}
        public override int ReadByte() {return source.ReadByte();}
        public override void Flush() { }
        public override long Seek(long offset,SeekOrigin origin) {throw new NotSupportedException();}
        public override void SetLength(long value) {throw new NotSupportedException();}
        public override void Write(byte[] buffer,int offset,int count) {throw new NotSupportedException();}
        protected override void Dispose(bool disposing) {if(disposing)source.Dispose();base.Dispose(disposing);}
    }
    private sealed class PrefixStream : Stream
    {
        private readonly byte[] prefix;
        private int prefixOffset;
        private long remaining;
        internal PrefixStream(string head,long bodyBytes) {prefix=Bytes(head);remaining=bodyBytes;}
        public override bool CanRead {get{return true;}}
        public override bool CanSeek {get{return false;}}
        public override bool CanWrite {get{return false;}}
        public override long Length {get{throw new NotSupportedException();}}
        public override long Position {get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
        public override int Read(byte[] buffer,int offset,int count)
        {
            int copied=0;
            while(copied<count && prefixOffset<prefix.Length)buffer[offset+copied++]=prefix[prefixOffset++];
            int body=(int)Math.Min(count-copied,remaining);Array.Clear(buffer,offset+copied,body);remaining-=body;return copied+body;
        }
        public override void Flush() { }
        public override long Seek(long offset,SeekOrigin origin) {throw new NotSupportedException();}
        public override void SetLength(long value) {throw new NotSupportedException();}
        public override void Write(byte[] buffer,int offset,int count) {throw new NotSupportedException();}
    }
    private static void TestAddressPolicy()
    {
        string[] blocked={"0.0.0.0","0.9.8.7","10.0.0.1","127.0.0.1","169.254.169.254","172.16.0.1","172.31.255.255","192.168.0.1","100.64.0.1","100.127.255.255",
            "192.0.0.9","192.0.2.1","192.88.99.1","198.18.0.1","198.19.255.255","198.51.100.1","203.0.113.1","224.0.0.1","240.0.0.1","255.255.255.255",
            "::","::1","::ffff:127.0.0.1","::ffff:8.8.8.8","fc00::1","fd00::1","fe80::1","fe80::1%2","ff02::1","64:ff9b::808:808","64:ff9b:1::1","100::1",
            "2001::1","2001:1::1","2001:2::1","2001:10::1","2001:20::1","2001:30::1","2001:1ff::1","2001:db8::1","2002:7f00:1::1","2002:808:808::1","3fff::1","3fff:fff::1","2001:4860::5efe:7f00:1"};
        foreach(string text in blocked)Check(!HyTcgArtworkDownload.AllowedAddress(IPAddress.Parse(text)),"blocked destination "+text);
        foreach(string text in new[] {"8.8.8.8","1.1.1.1","93.184.216.34","172.15.0.1","172.32.0.1","100.63.255.255","100.128.0.1","192.175.48.1","198.20.0.1","2001:4860:4860::8888","2606:4700:4700::1111","2001:200::1","3fff:1000::1"})
            Check(HyTcgArtworkDownload.AllowedAddress(IPAddress.Parse(text)),"public destination "+text);
        Check(!HyTcgArtworkDownload.AllowedAddress(null),"null destination");
        Error("ArtworkUrlInvalid",delegate {HyTcgArtworkDownload.ValidatedAddress(new IPAddress[0]);});
        Error("ArtworkUrlInvalid",delegate {HyTcgArtworkDownload.ValidatedAddress(new[] {IPAddress.Parse("8.8.8.8"),IPAddress.Loopback});});
        Error("ArtworkUrlInvalid",delegate {HyTcgArtworkDownload.ValidatedAddress(Enumerable.Repeat(IPAddress.Parse("8.8.8.8"),65).ToArray());});
        var chosen=HyTcgArtworkDownload.ValidatedAddress(new[] {IPAddress.Parse("2001:4860::8888"),IPAddress.Parse("8.8.8.8")});Check(chosen.Equals(IPAddress.Parse("8.8.8.8")),"pin one already validated public address");
    }
    private static void TestUrlsAndRequests()
    {
        string[] invalid={"http://example.com/image.png","file:///C:/image.png","https://user:password@example.com/image.png","https://example.com:444/image.png","https://example.com/image.png#fragment",
            "https://example.com./image.png","https://localhost/image.png","https://printer.local/image.png","https://device.internal/image.png","https://x.home.arpa/image.png","https://a.localhost/image.png",
            "https://127.0.0.1/image.png","https://2130706433/image.png","https://0x7f000001/image.png","https://169.254.169.254/image.png","https://100.100.100.200/image.png","https://[::1]/image.png",
            "https://[2002:7f00:1::1]/image.png","https://example.com/a\\b","https://example.com/a%5cb","https://example.com/a\r\nb","https://example.com/a%0d%0ab","https://example.com/\u0085","https://example.com/%C2%85","https://example.com/a b"};
        var timer=Stopwatch.StartNew();
        foreach(string url in invalid)
        {
            try {HyTcgArtworkDownload.ValidateRequestUrl(url);throw new Exception("Invalid URL accepted: "+url);}
            catch(HyTcgException ex) {Check(ex.Code=="ArtworkUrlInvalid","URL rejection before DNS "+url);}
            Error("ArtworkUrlInvalid",delegate {HyTcgArtworkDownload.Fetch(url,CancellationToken.None);});
        }
        Check(timer.ElapsedMilliseconds<5000,"blocked URL literals before any DNS/connect");
        var valid=HyTcgArtworkDownload.ValidateRequestUrl("https://example.com/scene%20image.png?token=secret");Check(valid.Scheme=="https","encoded path spaces allowed");
        var next=HyTcgArtworkDownload.RedirectUrl(valid,"/next.png?sig=secret");Check(next.Host=="example.com" && next.AbsolutePath=="/next.png","relative redirects validate afresh");
        Error("ArtworkUrlInvalid",delegate {HyTcgArtworkDownload.RedirectUrl(valid,"http://example.com/image.png");});
        Error("ArtworkUrlInvalid",delegate {HyTcgArtworkDownload.RedirectUrl(valid,"https://127.0.0.1/image.png");});
        Error("ArtworkUrlInvalid",delegate {HyTcgArtworkDownload.RedirectUrl(valid,"//169.254.169.254/image.png");});
        Error("ArtworkUrlInvalid",delegate {HyTcgArtworkDownload.RedirectUrl(valid,"https://example.com/%0Aevil");});
        Error("ArtworkUrlInvalid",delegate {HyTcgArtworkDownload.RedirectUrl(valid,"https://user@example.com/image.png");});
        string request=Encoding.ASCII.GetString(HyTcgArtworkDownload.RequestBytes(valid));
        Check(request.StartsWith("GET /scene%20image.png?token=secret HTTP/1.1\r\nHost: example.com\r\n"),"escaped origin-form path and original hostname");
        Check(request.Contains("Accept: image/*\r\n") && request.Contains("Accept-Encoding: identity\r\n") && request.Contains("Connection: close\r\n"),"bounded image request asks for identity and close");
        Check(!request.Contains("Authorization:") && !request.Contains("Cookie:") && !request.Contains("Referer:"),"no ambient credentials or referral headers");
        Check(Encoding.ASCII.GetString(HyTcgArtworkDownload.RequestBytes(new Uri("https://例え.com/%E6%97%A5.png"))).Contains("Host: xn--r8jz45g.com\r\n"),"IDN hostname converted to ASCII for Host and TLS");
        Check(ApiActivityLog.SafeUrl(valid.AbsoluteUri).Contains("token=[redacted]"),"API log masks URL token");
        Check(HyTcgArtworkDownload.LogUrl(new Uri("https://example.com/image.png?custom-auth=topsecret&token=secret"))=="https://example.com/image.png?[redacted]","import log masks all arbitrary query keys and payloads");
    }
    private static void TestHeadersAndBodies()
    {
        Check(Encoding.ASCII.GetString(Parse(Head("Content-Length: 4\r\n")+"Png!").Body)=="Png!","content length body exact");
        Check(Parse("HTTP/1.0 200 OK\r\ncontent-type: IMAGE/PNG; charset=binary\r\nContent-Encoding: identity\r\nContent-Length: 4\r\n\r\nPng!").Body.Length==4,"HTTP1.0 and case insensitive MIME fields");
        Check(Parse(Head("")+"Png!").Body.Length==4,"bounded connection-close framing");
        byte[] binary={0,0x85,0xff,13};
        using(var input=new MemoryStream(Bytes(Head("Content-Length: 4\r\n")).Concat(binary).ToArray()))
            Check(HyTcgArtworkDownload.ReadResponse(input,CancellationToken.None).Body.SequenceEqual(binary),"binary image bytes preserved independently of header character rules");
        using(var input=new MemoryStream(Bytes("HTTP/1.1 200 OK\r\nX-Test: ").Concat(new byte[] {0x85}).Concat(Bytes("\r\nContent-Type: image/png\r\nContent-Length: 4\r\n\r\nPng!")).ToArray()))
            Error("ArtworkInvalid",delegate {HyTcgArtworkDownload.ReadResponse(input,CancellationToken.None);});
        Check(Parse("HTTP/1.1 100 Continue\r\n\r\n"+Head("Content-Length: 4\r\n")+"Png!").Body.Length==4,"bounded interim headers");
        Check(Parse("HTTP/1.1 302 Found\r\nLocation: /next.png\r\nContent-Length: 50000000\r\n\r\n").Location=="/next.png","redirect body is skipped and connection closed");
        foreach(string fields in new[] {"Content-Length: -1\r\n","Content-Length: +4\r\n","Content-Length: 4.0\r\n","Content-Length: 4,4\r\n","Content-Length: 0\r\n","Content-Length: 9999999999\r\n",
            "Content-Length: 4\r\nContent-Length: 4\r\n","Content-Length: 4\r\nContent-Length: 5\r\n","Content-Length: 4\r\nTransfer-Encoding: chunked\r\n",
            "Transfer-Encoding: gzip, chunked\r\n","Transfer-Encoding: identity\r\n","Content-Encoding: gzip\r\nContent-Length: 4\r\n","Content-Encoding: br\r\nContent-Length: 4\r\n",
            "Content-Type: image/jpeg\r\nContent-Length: 4\r\n","Content-Length : 4\r\n"," Content-Length: 4\r\n","X-Test: value\n","X-Test: value\rnext\r\n","X-Test: \u0001\r\n"})Invalid(Head(fields)+"Png!");
        Invalid("HTTP/2 200\r\nContent-Type: image/png\r\n\r\nPng!");
        Invalid("HTTP/1.1 200OK\r\nContent-Type: image/png\r\n\r\nPng!");
        Invalid("HTTP/1.1 101 Switching Protocols\r\n\r\n");
        Invalid("HTTP/1.1 100 Continue\r\nContent-Length: 0\r\n\r\n"+Head("Content-Length: 4\r\n")+"Png!");
        Invalid(String.Concat(Enumerable.Repeat("HTTP/1.1 103 Early Hints\r\n\r\n",4))+Head("Content-Length: 4\r\n")+"Png!");
        Invalid("HTTP/1.1 200 OK\r\n\r\nPng!");
        Invalid("HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\n\r\nPng!");
        Invalid("HTTP/1.1 200 OK\r\nContent-Type: image/\r\n\r\nPng!");
        Invalid("HTTP/1.1 200 OK\r\nContent-Type: image/a b\r\n\r\nPng!");
        Invalid(Head("Content-Length: 5\r\n")+"Png!");
        Invalid("HTTP/1.1 200 OK\r\nContent-Type: image/png\r\n");
        Invalid(Head("X-Test: "+new string('a',8193)+"\r\n")+"Png!");
        Invalid(Head(String.Concat(Enumerable.Range(0,101).Select(i=>"X-"+i+": v\r\n")))+"Png!");
        Invalid(Head(String.Concat(Enumerable.Range(0,9).Select(i=>"X-"+i+": "+new string('a',7900)+"\r\n")))+"Png!");
        Error("ArtworkUnavailable",delegate {Parse("HTTP/1.1 404 Not Found\r\nContent-Length: 999999999\r\n\r\n");});
        Error("ArtworkUrlInvalid",delegate {Parse("HTTP/1.1 302 Found\r\n\r\n");});
        // Declared length overflow is rejected before reading or allocating body data.
        using(var input=new TrackedStream(new PrefixStream(Head("Content-Length: "+(HyTcgArtworkDownload.MaxBytes+1)+"\r\n"),HyTcgArtworkDownload.MaxBytes+1)))
        {Error("ArtworkInvalid",delegate {HyTcgArtworkDownload.ReadResponse(input,CancellationToken.None);});Check(input.BulkBytes==0,"oversize CL rejected before body read");}
        using(var input=new TrackedStream(new PrefixStream(Head("Content-Length: "+HyTcgArtworkDownload.MaxBytes+"\r\n"),HyTcgArtworkDownload.MaxBytes)))
        {Check(HyTcgArtworkDownload.ReadResponse(input,CancellationToken.None).Body.Length==HyTcgArtworkDownload.MaxBytes,"exact8MB content length accepted");Check(input.MaxRequest<=8192,"bounded CL read blocks");}
        using(var input=new TrackedStream(new PrefixStream(Head(""),HyTcgArtworkDownload.MaxBytes+1)))
        {Error("ArtworkInvalid",delegate {HyTcgArtworkDownload.ReadResponse(input,CancellationToken.None);});Check(input.BulkBytes==HyTcgArtworkDownload.MaxBytes+1 && input.MaxRequest<=8192,"connection close overflow bounded to one detection byte");}
    }
    private static void TestChunks()
    {
        Check(Encoding.ASCII.GetString(Parse(Chunked("2;extension=yes\r\nPn\r\n2\r\ng!\r\n0\r\nX-Checksum: ok\r\n\r\n")).Body)=="Png!","chunk extensions and safe trailers");
        foreach(string body in new[] {"G\r\nx\r\n0\r\n\r\n","+1\r\nx\r\n0\r\n\r\n","1 \r\nx\r\n0\r\n\r\n","1\r\nx\n0\r\n\r\n","5\r\nPng!","1\r\nx\r\n","0\r\n\r\n","1\r\nx\r\n0\r\n",
            "1\r\nx\r\n0\r\nContent-Length: 1\r\n\r\n","1\r\nx\r\n0\r\nTransfer-Encoding: identity\r\n\r\n","1\r\nx\r\n0\r\nContent-Encoding: identity\r\n\r\n",
            "1\r\nx\r\n0\r\nHost: localhost\r\n\r\n","1\r\nx\r\n0\r\n X-Trailer: invalid\r\n\r\n","10000000000000000\r\n","FFFFFFFFFFFFFFFF\r\n","800001\r\n"})Invalid(Chunked(body));
        Invalid(Chunked(String.Concat(Enumerable.Repeat("1\r\nx\r\n",8193))+"0\r\n\r\n"));
        Invalid(Chunked("1\r\nx\r\n0\r\n"+String.Concat(Enumerable.Range(0,101).Select(i=>"X-"+i+": v\r\n"))+"\r\n"));
        Invalid(Chunked(String.Concat(Enumerable.Repeat("1;"+new string('a',7900)+"\r\nx\r\n",9))+"0\r\n\r\n"));
        Invalid(Chunked("1\r\nx\r\n0\r\n"+String.Concat(Enumerable.Range(0,9).Select(i=>"X-"+i+": "+new string('a',7900)+"\r\n"))+"\r\n"));
        // Two legal individual sizes cannot bypass the aggregate body limit.
        using(var input=new MemoryStream())
        {
            byte[] first=Bytes(Chunked("400000\r\n")),zeros=new byte[4*1024*1024],last=Bytes("\r\n400001\r\n");
            input.Write(first,0,first.Length);input.Write(zeros,0,zeros.Length);input.Write(last,0,last.Length);input.Position=0;
            Error("ArtworkInvalid",delegate {HyTcgArtworkDownload.ReadResponse(input,CancellationToken.None);});
        }
    }
    private static void TestCancellation()
    {
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();using(var input=new TrackedStream(new MemoryStream(Bytes(Head("Content-Length: 4\r\n")+"Png!"))))
            {
                try {HyTcgArtworkDownload.ReadResponse(input,canceled.Token);throw new Exception("Canceled parser continued");}
                catch(OperationCanceledException) {Check(input.BulkBytes==0,"cancellation before header/body reads");}
            }
            var timer=Stopwatch.StartNew();try {HyTcgArtworkDownload.Fetch("https://example.com/image.png",canceled.Token);throw new Exception("Canceled fetch continued");}
            catch(OperationCanceledException) {Check(timer.ElapsedMilliseconds<1000,"canceled fetch exits before DNS");}
        }
        using(var canceled=new CancellationTokenSource())using(var input=new TrackedStream(new MemoryStream(Bytes(Head("Content-Length: 20000\r\n")+new string('a',20000)))))
        {
            input.OnRead=canceled.Cancel;try {HyTcgArtworkDownload.ReadResponse(input,canceled.Token);throw new Exception("Body cancellation ignored");}
            catch(OperationCanceledException) {Check(input.BulkBytes<=8192,"body cancellation checked each bounded block");}
        }
    }
    internal static int Main(string[] args)
    {
        try
        {
            TestAddressPolicy();TestUrlsAndRequests();TestHeadersAndBodies();TestChunks();TestCancellation();
            Console.WriteLine("HYTCG pinned artwork downloader PASS: "+checks+" offline checks; public-IP policy, URL/redirect guards, exact-host request, bounded CL/chunk/trailer parser and cancellation. No live network or production writes.");return 0;
        }
        catch(Exception error) {Console.Error.WriteLine(error);return 1;}
    }
}
