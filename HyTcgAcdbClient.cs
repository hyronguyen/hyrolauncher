using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HYROLauncherWeb
{
    internal sealed class HyTcgHttpException : WebException
    {
        internal readonly int StatusCode;
        internal HyTcgHttpException(int status) : base("ACDB HTTP "+status,WebExceptionStatus.ProtocolError) { StatusCode=status; }
    }
    // ACDB explicitly documents curl as a supported API client without cookies.
    // Use the real Windows client, identify HYTCG, and respect its one-request/second policy.
    internal static class HyTcgAcdbClient
    {
        internal const string UserAgent="curl HYTCG/1.0 (HYRO Launcher; ACDB API)";
        private const string StatusMarker="\nHYTCG_HTTP:";
        private static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        private static readonly Stopwatch clock=Stopwatch.StartNew();
        private static long nextStart;
        internal sealed class Reply
        {
            internal int Status;
            internal string ContentType="",Location="";
            internal byte[] Body;
        }
        internal static bool IsAcdb(Uri uri)
        {
            return uri!=null && (uri.Host=="animecharactersdatabase.com" || uri.Host.EndsWith(".animecharactersdatabase.com",StringComparison.Ordinal));
        }
        internal static bool IsBrowserCheck(string body)
        {
            if(String.IsNullOrEmpty(body)) return false;
            string sample=body.Substring(0,Math.Min(body.Length,65536)).TrimStart('\uFEFF',' ','\r','\n','\t').ToLowerInvariant();
            return sample.StartsWith("<",StringComparison.Ordinal) &&
                (sample.Contains("xbotcheck") || sample.Contains("checking browser") || sample.Contains("challenge-platform") || sample.Contains("<title>just a moment"));
        }
        internal static T RunThrottled<T>(Func<T> operation,CancellationToken token)
        {
            gate.Wait(token);
            try
            {
                long delay=nextStart-clock.ElapsedMilliseconds;
                if(delay>0 && token.WaitHandle.WaitOne((int)Math.Min(delay,Int32.MaxValue))) token.ThrowIfCancellationRequested();
                token.ThrowIfCancellationRequested();
                nextStart=clock.ElapsedMilliseconds+1100;
                return operation();
            }
            finally { gate.Release(); }
        }
        internal static byte[] Read(Uri uri,int timeout,int limit,bool image,CancellationToken token,out string contentType)
        {
            contentType="";
            for(int redirect=0;redirect<4;redirect++)
            {
                if(!IsAcdb(uri) || !HyTcgTransport.AllowedImage(uri.AbsoluteUri) || (!image && uri.Host!="www.animecharactersdatabase.com"))
                    throw new HyTcgException("ApiInvalid");
                Uri target=uri;
                Reply reply=RunThrottled(()=> {
                    using(var trace=ApiActivityLog.Begin("ACDB",image?"IMAGE":"GET",target.AbsoluteUri))
                    {
                        try {var fetched=Fetch(target,timeout,limit,image,token);trace.Complete(fetched.Status,fetched.Body.Length);return fetched;}
                        catch(Exception error){trace.Fail(error);throw;}
                    }
                },token);
                ValidateReply(reply);
                if(reply.Status>=300 && reply.Status<400)
                {
                    Uri next;
                    if(!Uri.TryCreate(uri,reply.Location,out next) || !IsAcdb(next) || !HyTcgTransport.AllowedImage(next.AbsoluteUri)) throw new HyTcgException("ApiInvalid");
                    uri=next; continue;
                }
                contentType=reply.ContentType;return reply.Body;
            }
            throw new HyTcgException("ApiInvalid");
        }
        internal static void ValidateReply(Reply reply)
        {
            string probe=Encoding.UTF8.GetString(reply.Body,0,Math.Min(reply.Body.Length,65536));
            if(IsBrowserCheck(probe)) throw new HyTcgException("AcdbBrowserCheck");
            if(reply.Status==429) throw new HyTcgException("AcdbRateLimited");
            if(reply.Status==401 || reply.Status==403) throw new HyTcgException("AcdbUnavailable");
            if(reply.Status<200 || reply.Status>=400) throw new HyTcgHttpException(reply.Status);
        }
        private static Reply Fetch(Uri uri,int timeout,int limit,bool image,CancellationToken token)
        {
            string client=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"curl.exe");
            if(!File.Exists(client)) throw new HyTcgException("AcdbClientUnavailable");
            // No shell, curlrc, automatic redirects, cookie jar or downloaded executable.
            string arguments="--disable --silent --show-error --globoff --compressed --proto "+Quote("=https")
                +" --max-time "+timeout+" --connect-timeout "+Math.Min(timeout,8)+" --max-filesize "+limit
                +" --user-agent "+Quote(UserAgent)+" --header "+Quote("Accept: "+(image?"image/*":"application/json"))
                +" --dump-header - --write-out "+Quote(StatusMarker+"%{http_code}")+" --url "+Quote(uri.AbsoluteUri);
            using(var process=new Process { StartInfo=new ProcessStartInfo(client,arguments) {
                UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true } })
            {
                token.ThrowIfCancellationRequested();
                try { process.Start(); } catch(System.ComponentModel.Win32Exception) { throw new HyTcgException("AcdbClientUnavailable"); }
                Action stop=delegate { try { if(!process.HasExited) process.Kill(); } catch { } };
                using(token.Register(stop))
                {
                    var output=Task.Run(()=>ReadBounded(process.StandardOutput.BaseStream,limit+65536,stop,token));
                    var error=Task.Run(()=>ReadBounded(process.StandardError.BaseStream,65536,stop,token));
                    bool timedOut=!process.WaitForExit((timeout+2)*1000);
                    if(timedOut) stop();
                    try { Task.WaitAll(output,error); }
                    catch(AggregateException ex) { token.ThrowIfCancellationRequested();throw ex.Flatten().InnerExceptions[0]; }
                    byte[] bytes=output.Result;
                    token.ThrowIfCancellationRequested();
                    if(timedOut || !process.HasExited || process.ExitCode==28) throw new WebException("ACDB request timed out",WebExceptionStatus.Timeout);
                    if(process.ExitCode==63) throw new HyTcgException("ArtworkTooLarge");
                    if(process.ExitCode!=0) throw new WebException("ACDB client request failed",WebExceptionStatus.ConnectFailure);
                    return ParseReply(bytes,limit);
                }
            }
        }
        private static byte[] ReadBounded(Stream input,int limit,Action stop,CancellationToken token)
        {
            using(var output=new MemoryStream())
            {
                byte[] buffer=new byte[16384];int count;
                while((count=input.Read(buffer,0,buffer.Length))>0)
                {
                    token.ThrowIfCancellationRequested();
                    if(output.Length+count>limit) { stop();throw new HyTcgException("ArtworkTooLarge"); }
                    output.Write(buffer,0,count);
                }
                return output.ToArray();
            }
        }
        internal static Reply ParseReply(byte[] bytes,int limit)
        {
            int footer=bytes.Length-StatusMarker.Length-3,status;
            if(footer<0 || Encoding.ASCII.GetString(bytes,footer,StatusMarker.Length)!=StatusMarker || !Int32.TryParse(Encoding.ASCII.GetString(bytes,bytes.Length-3,3),out status))
                throw new WebException("Invalid ACDB response",WebExceptionStatus.ProtocolError);
            var reply=new Reply { Status=status };int offset=0;
            // curl can include a proxy CONNECT or informational header before the final response.
            while(offset+5<footer && Encoding.ASCII.GetString(bytes,offset,5)=="HTTP/")
            {
                int end=offset;
                while(end+3<footer && end<65536 && !(bytes[end]==13&&bytes[end+1]==10&&bytes[end+2]==13&&bytes[end+3]==10)) end++;
                if(end>=65536 || end+3>=footer) throw new WebException("Invalid ACDB headers",WebExceptionStatus.ProtocolError);
                string headers=Encoding.ASCII.GetString(bytes,offset,end-offset);
                reply.Location="";reply.ContentType="";
                foreach(string line in headers.Split(new[]{"\r\n"},StringSplitOptions.None))
                {
                    int colon=line.IndexOf(':');if(colon<0)continue;
                    string key=line.Substring(0,colon),value=line.Substring(colon+1).Trim();
                    if(key.Equals("Content-Type",StringComparison.OrdinalIgnoreCase)) reply.ContentType=value;
                    if(key.Equals("Location",StringComparison.OrdinalIgnoreCase)) reply.Location=value;
                }
                offset=end+4;
            }
            int length=footer-offset;
            if(offset==0 || length<0) throw new WebException("Missing ACDB headers",WebExceptionStatus.ProtocolError);
            if(length>limit) throw new HyTcgException("ArtworkTooLarge");
            reply.Body=new byte[length];Buffer.BlockCopy(bytes,offset,reply.Body,0,length);return reply;
        }
        private static string Quote(string value)
        {
            var text=new StringBuilder("\"");int slashes=0;
            foreach(char c in value)
            {
                if(c=='\\') { slashes++;continue; }
                if(c=='\"') text.Append('\\',slashes*2+1);
                else text.Append('\\',slashes);
                text.Append(c);slashes=0;
            }
            text.Append('\\',slashes*2);return text.Append('"').ToString();
        }
    }
}
