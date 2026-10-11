using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HYROLauncherWeb
{
    // Resolve once, validate every returned address, and connect to an IPAddress rather
    // than a host string. TLS still authenticates the original DNS hostname. No proxy,
    // second DNS lookup, cookies, credentials, certificate callback or global TLS state.
    internal static class HyTcgArtworkDownload
    {
        internal const int MaxBytes=8*1024*1024;
        private const int MaxHeaders=64*1024,MaxLine=8192,MaxHeaderCount=100,MaxChunks=8192;
        private const int OperationMilliseconds=15000,TotalMilliseconds=45000;
        internal sealed class Response
        {
            internal int Status;
            internal string Location;
            internal byte[] Body;
        }
        private sealed class Head
        {
            internal int Status;
            internal Dictionary<string,string> Fields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        }

        internal static byte[] Fetch(string url,CancellationToken cancellation)
        {
            Uri current=ValidateRequestUrl(url);
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(TotalMilliseconds);var token=timeout.Token;
                for(int redirect=0;redirect<=3;redirect++)
                {
                    token.ThrowIfCancellationRequested();
                    using(var trace=ApiActivityLog.Begin("HYTCG Studio","IMAGE",LogUrl(current)))
                    {
                        try
                        {
                            Response response=Request(current,token);
                            if(IsRedirect(response.Status))
                            {
                                if(redirect==3)throw new HyTcgException("ArtworkUrlInvalid");
                                Uri next=RedirectUrl(current,response.Location);
                                trace.Complete(response.Status,0);current=next;continue;
                            }
                            trace.Complete(response.Status,response.Body.Length);return response.Body;
                        }
                        catch(Exception error)
                        {
                            trace.Fail(error);cancellation.ThrowIfCancellationRequested();
                            if(token.IsCancellationRequested)throw new HyTcgException("ArtworkUnavailable");
                            if(error is IOException || error is SocketException || error is AuthenticationException || error is ObjectDisposedException)throw new HyTcgException("ArtworkUnavailable");
                            throw;
                        }
                    }
                }
            }
            throw new HyTcgException("ArtworkUrlInvalid");
        }

        // Shared with the import wrapper so URL literals and DNS answers use one policy.
        // Conservatively reject special-purpose and transition networks, including their
        // globally reachable exceptions: an image importer has no need to contact them.
        internal static bool AllowedAddress(IPAddress address)
        {
            if(address==null || IPAddress.IsLoopback(address) || address.IsIPv4MappedToIPv6)return false;
            byte[] b=address.GetAddressBytes();
            if(address.AddressFamily==AddressFamily.InterNetwork)
                return b[0]!=0 && b[0]!=10 && b[0]!=127 && b[0]<224 && !(b[0]==169 && b[1]==254)
                    && !(b[0]==172 && b[1]>=16 && b[1]<=31) && !(b[0]==192 && b[1]==168)
                    && !(b[0]==100 && b[1]>=64 && b[1]<=127) && !(b[0]==198 && (b[1]==18 || b[1]==19))
                    && !(b[0]==192 && b[1]==0 && (b[2]==0 || b[2]==2))
                    && !(b[0]==192 && b[1]==88 && b[2]==99)
                    && !(b[0]==198 && b[1]==51 && b[2]==100) && !(b[0]==203 && b[1]==0 && b[2]==113);
            if(address.AddressFamily!=AddressFamily.InterNetworkV6 || address.ScopeId!=0 || (b[0]&0xe0)!=0x20)return false;
            return !(b[0]==0x20 && b[1]==0x01 && b[2]<2) // IETF protocol assignments, including Teredo.
                && !(b[0]==0x20 && b[1]==0x02) // 6to4 can embed a private IPv4 destination.
                && !(b[0]==0x20 && b[1]==0x01 && b[2]==0x0d && b[3]==0xb8)
                && !(b[0]==0x3f && b[1]==0xff && (b[2]&0xf0)==0) // 3fff::/20 documentation.
                && !((b[8]==0 || b[8]==2) && b[9]==0 && b[10]==0x5e && b[11]==0xfe); // ISATAP.
        }
        internal static Uri ValidateRequestUrl(string text)
        {
            if(String.IsNullOrEmpty(text) || text.Length>2048 || UnsafeText(text) || UnsafeEscaped(text) || text.IndexOf('\\')>=0)throw new HyTcgException("ArtworkUrlInvalid");
            Uri uri=HyTcgArtworkImport.ValidateUrl(text);
            if(uri.Fragment!="" || uri.UserInfo!="" || uri.Host.EndsWith(".",StringComparison.Ordinal))throw new HyTcgException("ArtworkUrlInvalid");
            string decoded;
            try {decoded=Uri.UnescapeDataString(uri.PathAndQuery);}catch {throw new HyTcgException("ArtworkUrlInvalid");}
            foreach(char c in decoded)if(c<0x20 || c==0x7f || (c>=0x80 && c<=0x9f))throw new HyTcgException("ArtworkUrlInvalid");
            if(decoded.IndexOf('\\')>=0)throw new HyTcgException("ArtworkUrlInvalid");
            string host=Host(uri);IPAddress literal;
            if(IPAddress.TryParse(host,out literal) && !AllowedAddress(literal))throw new HyTcgException("ArtworkUrlInvalid");
            // Reject local resolver suffixes before invoking DNS even if a malicious DNS
            // server happens to return a public answer for a name normally used locally.
            if(host.Equals("localhost",StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost",StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".local",StringComparison.OrdinalIgnoreCase) || host.EndsWith(".internal",StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".home.arpa",StringComparison.OrdinalIgnoreCase))throw new HyTcgException("ArtworkUrlInvalid");
            return uri;
        }
        internal static Uri RedirectUrl(Uri current,string location)
        {
            if(current==null || String.IsNullOrEmpty(location) || location.Length>2048 || UnsafeText(location) || UnsafeEscaped(location) || location.IndexOf('\\')>=0)throw new HyTcgException("ArtworkUrlInvalid");
            Uri next;if(!Uri.TryCreate(current,location,out next))throw new HyTcgException("ArtworkUrlInvalid");
            return ValidateRequestUrl(next.AbsoluteUri);
        }
        private static bool UnsafeText(string value)
        {
            foreach(char c in value)if(c<=0x20 || c==0x7f || (c>=0x80 && c<=0x9f))return true;return false;
        }
        private static bool UnsafeEscaped(string value)
        {
            string decoded;try {decoded=Uri.UnescapeDataString(value);}catch {return true;}
            foreach(char c in decoded)if(c<0x20 || c==0x7f || (c>=0x80 && c<=0x9f) || c=='\\')return true;return false;
        }
        private static string Host(Uri uri)
        {
            string host;
            try {host=uri.IdnHost.Trim('[',']');}catch {throw new HyTcgException("ArtworkUrlInvalid");}
            if(host.Length<3 || host.Length>253 || UnsafeText(host))throw new HyTcgException("ArtworkUrlInvalid");
            foreach(char c in host)if(c>0x7f)throw new HyTcgException("ArtworkUrlInvalid");return host;
        }
        internal static IPAddress ValidatedAddress(IPAddress[] addresses)
        {
            if(addresses==null || addresses.Length<1 || addresses.Length>64)throw new HyTcgException("ArtworkUrlInvalid");
            IPAddress selected=null;
            foreach(var address in addresses)
            {
                if(!AllowedAddress(address))throw new HyTcgException("ArtworkUrlInvalid");
                if(selected==null || (selected.AddressFamily!=AddressFamily.InterNetwork && address.AddressFamily==AddressFamily.InterNetwork))selected=address;
            }
            return selected;
        }
        private static IPAddress Resolve(Uri uri,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();string host=Host(uri);IPAddress literal;
            if(IPAddress.TryParse(host,out literal))return ValidatedAddress(new[] {literal});
            var operation=Dns.GetHostAddressesAsync(host);
            Wait(operation,token);return ValidatedAddress(operation.GetAwaiter().GetResult());
        }
        private static void Wait(Task operation,CancellationToken token)
        {
            try
            {
                int result=Task.WaitAny(new[] {operation},OperationMilliseconds,token);
                token.ThrowIfCancellationRequested();if(result<0)throw new HyTcgException("ArtworkUnavailable");
            }
            finally
            {
                // Framework Task APIs finish their own APM End/handle cleanup. Observe a
                // late fault after timeout/cancellation, including non-abortable DNS.
                if(operation.IsFaulted) {var ignored=operation.Exception;}
                else if(!operation.IsCompleted)operation.ContinueWith(delegate(Task completed) {var ignored=completed.Exception;},CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            }
        }
        private static Response Request(Uri uri,CancellationToken token)
        {
            IPAddress pinned=Resolve(uri,token);string host=Host(uri);
            using(var client=new TcpClient(pinned.AddressFamily))
            using(token.Register(delegate {try {client.Close();}catch {}}))
            {
                try
                {
                    client.NoDelay=true;client.ReceiveTimeout=OperationMilliseconds;client.SendTimeout=OperationMilliseconds;
                    Task connection=client.ConnectAsync(pinned,443);Wait(connection,token);connection.GetAwaiter().GetResult();
                    using(var ssl=new SslStream(client.GetStream(),false))
                    {
                        ssl.ReadTimeout=OperationMilliseconds;ssl.WriteTimeout=OperationMilliseconds;
                        // Default SslStream certificate validation checks chain and original
                        // hostname. This explicitly enables TLS1.2 without changing globals.
                        Task handshake=ssl.AuthenticateAsClientAsync(host,null,SslProtocols.Tls12,false);
                        Wait(handshake,token);handshake.GetAwaiter().GetResult();
                        if(!ssl.IsAuthenticated || !ssl.IsEncrypted || !ssl.IsSigned)throw new HyTcgException("ArtworkUnavailable");
                        byte[] request=RequestBytes(uri);token.ThrowIfCancellationRequested();ssl.Write(request,0,request.Length);ssl.Flush();
                        return ReadResponse(ssl,token);
                    }
                }
                catch(Exception error)
                {
                    token.ThrowIfCancellationRequested();
                    if(error is HyTcgException)throw;
                    if(error is IOException || error is SocketException || error is AuthenticationException || error is ObjectDisposedException)throw new HyTcgException("ArtworkUnavailable");
                    throw;
                }
            }
        }
        internal static byte[] RequestBytes(Uri uri)
        {
            uri=ValidateRequestUrl(uri.OriginalString);string host=Host(uri),path=uri.PathAndQuery;
            IPAddress literal;if(IPAddress.TryParse(host,out literal) && literal.AddressFamily==AddressFamily.InterNetworkV6)host="["+host+"]";
            if(path.Length==0)path="/";
            foreach(char c in path)if(c<0x21 || c>0x7e)throw new HyTcgException("ArtworkUrlInvalid");
            string text="GET "+path+" HTTP/1.1\r\nHost: "+host+"\r\nUser-Agent: HYROLauncher/HYTCG Cosmetic Studio\r\nAccept: image/*\r\nAccept-Encoding: identity\r\nConnection: close\r\n\r\n";
            return Encoding.ASCII.GetBytes(text);
        }

        // Independently testable parser used verbatim on the pinned TLS stream.
        internal static Response ReadResponse(Stream input,CancellationToken token)
        {
            if(input==null)throw new ArgumentNullException("input");
            int headerBytes=0;Head head=null;
            for(int interim=0;interim<4;interim++)
            {
                head=ReadHead(input,token,ref headerBytes);
                if(head.Status<100 || head.Status>=200)break;
                if(head.Status==101 || head.Fields.ContainsKey("Content-Length") || head.Fields.ContainsKey("Transfer-Encoding") || interim==3)throw new HyTcgException("ArtworkInvalid");
            }
            if(head.Fields.ContainsKey("Content-Length") && head.Fields.ContainsKey("Transfer-Encoding"))throw new HyTcgException("ArtworkInvalid");
            string location;
            if(IsRedirect(head.Status))
            {
                if(!head.Fields.TryGetValue("Location",out location) || location.Length==0)throw new HyTcgException("ArtworkUrlInvalid");
                return new Response {Status=head.Status,Location=location,Body=new byte[0]};
            }
            if(head.Status!=200)throw new HyTcgException("ArtworkUnavailable");
            string contentType;
            if(!head.Fields.TryGetValue("Content-Type",out contentType))throw new HyTcgException("ArtworkInvalid");
            contentType=contentType.Split(';')[0].Trim();
            if(!contentType.StartsWith("image/",StringComparison.OrdinalIgnoreCase) || contentType.Length<=6 || !Token(contentType.Substring(6)))throw new HyTcgException("ArtworkInvalid");
            string encoding;
            if(head.Fields.TryGetValue("Content-Encoding",out encoding) && !encoding.Equals("identity",StringComparison.OrdinalIgnoreCase))throw new HyTcgException("ArtworkInvalid");
            string transfer,length;bool chunked=head.Fields.TryGetValue("Transfer-Encoding",out transfer),sized=head.Fields.TryGetValue("Content-Length",out length);
            if(chunked && (sized || !transfer.Equals("chunked",StringComparison.OrdinalIgnoreCase)))throw new HyTcgException("ArtworkInvalid");
            using(var output=new MemoryStream())
            {
                if(chunked)ReadChunks(input,output,token,headerBytes);
                else if(sized)
                {
                    long size;if(!DecimalLength(length,out size) || size<1 || size>MaxBytes)throw new HyTcgException("ArtworkInvalid");
                    CopyExactly(input,output,(int)size,token);
                }
                else CopyUntilClosed(input,output,token);
                if(output.Length<1)throw new HyTcgException("ArtworkInvalid");
                return new Response {Status=head.Status,Location="",Body=output.ToArray()};
            }
        }
        private static bool IsRedirect(int status) {return status==301 || status==302 || status==303 || status==307 || status==308;}
        internal static string LogUrl(Uri uri)
        {
            // Arbitrary image hosts can put signatures under any query key. Keep the
            // endpoint useful for diagnostics while masking the entire query payload.
            return uri.GetLeftPart(UriPartial.Path)+(uri.Query.Length==0?"":"?[redacted]");
        }
        private static Head ReadHead(Stream input,CancellationToken token,ref int bytes)
        {
            string status=Line(input,token,ref bytes);
            if(status.Length<12 || (status.Substring(0,8)!="HTTP/1.1" && status.Substring(0,8)!="HTTP/1.0") || status[8]!=' '
                || status[9]<'1' || status[9]>'5' || status[10]<'0' || status[10]>'9' || status[11]<'0' || status[11]>'9' || (status.Length>12 && status[12]!=' '))throw new HyTcgException("ArtworkInvalid");
            var head=new Head {Status=(status[9]-'0')*100+(status[10]-'0')*10+status[11]-'0'};
            for(int fields=0;fields<=MaxHeaderCount;fields++)
            {
                string line=Line(input,token,ref bytes);if(line.Length==0)return head;
                if(fields==MaxHeaderCount)throw new HyTcgException("ArtworkInvalid");
                string name,value;Field(line,out name,out value);
                if(head.Fields.ContainsKey(name))
                {
                    if(Critical(name))throw new HyTcgException("ArtworkInvalid");head.Fields[name]+=", "+value;
                }
                else head.Fields.Add(name,value);
            }
            throw new HyTcgException("ArtworkInvalid");
        }
        private static string Line(Stream input,CancellationToken token,ref int total)
        {
            var text=new StringBuilder();
            while(true)
            {
                token.ThrowIfCancellationRequested();int value=input.ReadByte();
                if(value<0 || ++total>MaxHeaders)throw new HyTcgException("ArtworkInvalid");
                if(value==13)
                {
                    token.ThrowIfCancellationRequested();if(input.ReadByte()!=10 || ++total>MaxHeaders)throw new HyTcgException("ArtworkInvalid");return text.ToString();
                }
                if((value<32 && value!=9) || value>126 || text.Length>=MaxLine)throw new HyTcgException("ArtworkInvalid");text.Append((char)value);
            }
        }
        private static void Field(string line,out string name,out string value)
        {
            int colon=line.IndexOf(':');
            if(colon<1 || !Token(line.Substring(0,colon)))throw new HyTcgException("ArtworkInvalid");
            name=line.Substring(0,colon);value=line.Substring(colon+1).Trim(' ','\t');
        }
        private static bool Token(string value)
        {
            if(value.Length==0)return false;
            foreach(char c in value)
                if(!((c>='a' && c<='z') || (c>='A' && c<='Z') || (c>='0' && c<='9') || "!#$%&'*+-.^_`|~".IndexOf(c)>=0))return false;
            return true;
        }
        private static bool Critical(string name)
        {return name.Equals("Content-Length",StringComparison.OrdinalIgnoreCase) || name.Equals("Transfer-Encoding",StringComparison.OrdinalIgnoreCase)
            || name.Equals("Content-Encoding",StringComparison.OrdinalIgnoreCase) || name.Equals("Content-Type",StringComparison.OrdinalIgnoreCase) || name.Equals("Location",StringComparison.OrdinalIgnoreCase);}
        private static bool DecimalLength(string value,out long length)
        {
            length=0;if(value.Length<1 || value.Length>10)return false;
            foreach(char c in value) {if(c<'0' || c>'9')return false;length=length*10+(c-'0');if(length>MaxBytes)return false;}return true;
        }
        private static void CopyExactly(Stream input,MemoryStream output,int remaining,CancellationToken token)
        {
            if(remaining<0 || output.Length+remaining>MaxBytes)throw new HyTcgException("ArtworkInvalid");byte[] buffer=new byte[8192];
            while(remaining>0)
            {
                token.ThrowIfCancellationRequested();int read=input.Read(buffer,0,Math.Min(buffer.Length,remaining));
                if(read<1)throw new HyTcgException("ArtworkInvalid");output.Write(buffer,0,read);remaining-=read;
            }
        }
        private static void CopyUntilClosed(Stream input,MemoryStream output,CancellationToken token)
        {
            byte[] buffer=new byte[8192];
            while(true)
            {
                token.ThrowIfCancellationRequested();int read=input.Read(buffer,0,(int)Math.Min(buffer.Length,MaxBytes+1-output.Length));
                if(read==0)return;if(output.Length+read>MaxBytes)throw new HyTcgException("ArtworkInvalid");output.Write(buffer,0,read);
            }
        }
        private static void ReadChunks(Stream input,MemoryStream output,CancellationToken token,int headerBytes)
        {
            int metadata=headerBytes;
            for(int chunks=0;chunks<=MaxChunks;chunks++)
            {
                string line=Line(input,token,ref metadata);int semicolon=line.IndexOf(';');string sizeText=semicolon<0?line:line.Substring(0,semicolon);ulong size;
                if(sizeText.Length<1 || sizeText.Length>16 || !Hex(sizeText) || !UInt64.TryParse(sizeText,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out size))throw new HyTcgException("ArtworkInvalid");
                if(size==0) {ReadTrailers(input,token,ref metadata);return;}
                if(chunks==MaxChunks || size>(ulong)(MaxBytes-output.Length))throw new HyTcgException("ArtworkInvalid");
                CopyExactly(input,output,(int)size,token);
                token.ThrowIfCancellationRequested();int first=input.ReadByte();token.ThrowIfCancellationRequested();int second=input.ReadByte();metadata+=2;
                if(first!=13 || second!=10 || metadata>MaxHeaders)throw new HyTcgException("ArtworkInvalid");
            }
            throw new HyTcgException("ArtworkInvalid");
        }
        private static bool Hex(string value)
        {foreach(char c in value)if(!((c>='0' && c<='9') || (c>='a' && c<='f') || (c>='A' && c<='F')))return false;return true;}
        private static void ReadTrailers(Stream input,CancellationToken token,ref int bytes)
        {
            for(int count=0;count<=MaxHeaderCount;count++)
            {
                string line=Line(input,token,ref bytes);if(line.Length==0)return;
                if(count==MaxHeaderCount)throw new HyTcgException("ArtworkInvalid");string name,value;Field(line,out name,out value);
                if(Critical(name) || name.Equals("Host",StringComparison.OrdinalIgnoreCase) || name.Equals("Connection",StringComparison.OrdinalIgnoreCase) || name.Equals("Trailer",StringComparison.OrdinalIgnoreCase))throw new HyTcgException("ArtworkInvalid");
            }
            throw new HyTcgException("ArtworkInvalid");
        }
    }
}
