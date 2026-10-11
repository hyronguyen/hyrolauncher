using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace HYROLauncherWeb
{
    internal sealed class ApiLogEntry
    {
        internal long Sequence,RequestId,Milliseconds,Bytes;
        internal DateTime Time;
        internal string Source,Method,Status,Url,Detail;
        internal string Line() { return String.Join("\t",new[]{Time.ToString("yyyy-MM-dd HH:mm:ss.fff"),RequestId.ToString(),Source,Method,Status,Milliseconds.ToString(),Bytes.ToString(),Url,Detail}); }
    }
    // Capture is opt-in and session-scoped. A request begun while closed stays unlogged.
    internal static class ApiActivityLog
    {
        private static readonly object gate=new object();
        private static readonly Queue<ApiLogEntry> entries=new Queue<ApiLogEntry>();
        private static long generation,sequence,requestId;
        private static bool active;
        private static StreamWriter writer;
        private static string filePath="",fileError="";
        internal static bool Active { get {lock(gate)return active;} }
        internal static string FilePath {get {lock(gate)return filePath;} }
        internal static string FileError {get {lock(gate)return fileError;} }
        internal static void Start(string root)
        {
            lock(gate)
            {
                if(active)return;
                string folder=Path.Combine(Path.GetFullPath(root),"logs");Directory.CreateDirectory(folder);
                if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked log folder");
                string path=Path.Combine(folder,"api-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".tsv");
                writer=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(true));
                writer.WriteLine("Time\tRequest\tSource\tMethod\tStatus\tDurationMs\tBytes\tURL\tDetail");writer.Flush();
                filePath=path;fileError="";entries.Clear();generation++;active=true;
            }
        }
        internal static void Stop()
        {
            lock(gate){active=false;generation++;if(writer!=null){try{writer.Dispose();}catch{}writer=null;}entries.Clear();}
        }
        internal static ApiLogEntry[] ReadAfter(long after)
        {lock(gate){var result=new List<ApiLogEntry>();foreach(var entry in entries)if(entry.Sequence>after)result.Add(entry);return result.ToArray();}}
        internal static Trace Begin(string source,string method,string url)
        {
            lock(gate)
            {
                if(!active)return new Trace(0,0,"","","");
                long id=++requestId;string safe=SafeUrl(url);Write(generation,id,source,method,"START",0,0,safe,"");
                return new Trace(generation,id,source,method,safe);
            }
        }
        internal static void Note(string source,string detail)
        {lock(gate){if(active)Write(generation,0,source,"—","INFO",0,0,"",detail);}}
        private static string Clean(string value,int max)
        {string text=(value??"").Replace('\t',' ').Replace('\r',' ').Replace('\n',' ');return text.Length>max?text.Substring(0,max)+"…":text;}
        internal static string SafeUrl(string value)
        {
            Uri uri;if(!Uri.TryCreate(value,UriKind.Absolute,out uri))return "[invalid URL]";
            string path=uri.GetLeftPart(UriPartial.Path);if(uri.UserInfo!="")path=uri.Scheme+"://"+uri.Host+(uri.IsDefaultPort?"":":"+uri.Port)+uri.AbsolutePath;
            var pairs=new List<string>();
            foreach(string part in uri.Query.TrimStart('?').Split('&'))
            {
                if(part=="")continue;int equals=part.IndexOf('=');string key=equals<0?part:part.Substring(0,equals),decoded;
                try{decoded=Uri.UnescapeDataString(key).Replace('+',' ');}catch{decoded=key;}
                // Query payloads may embed credentials; log endpoint/operation, never bodies/headers.
                bool sensitive=Regex.IsMatch(decoded,@"key|secret|token|password|authorization|cookie|credential|client.?id|input.?json|signature|^sig$",RegexOptions.IgnoreCase);
                pairs.Add(sensitive?key+"=[redacted]":part);
            }
            return Clean(path+(pairs.Count==0?"":"?"+String.Join("&",pairs.ToArray())),2500);
        }
        private static void Write(long session,long id,string source,string method,string status,long elapsed,long bytes,string url,string detail)
        {
            lock(gate)
            {
                if(!active||session!=generation)return;
                var entry=new ApiLogEntry {Sequence=++sequence,RequestId=id,Time=DateTime.Now,Source=Clean(source,80),Method=Clean(method,16),Status=Clean(status,80),Milliseconds=elapsed,Bytes=bytes,Url=url,Detail=Clean(detail,2500)};
                entries.Enqueue(entry);while(entries.Count>2000)entries.Dequeue();
                if(writer!=null)try{writer.WriteLine(entry.Line());writer.Flush();}catch{fileError="Không ghi được file log";try{writer.Dispose();}catch{}writer=null;}
            }
        }
        internal sealed class Trace:IDisposable
        {
            private readonly long session,id;private readonly string source,method,url;private readonly Stopwatch timer;private bool ended;
            internal Trace(long session,long id,string source,string method,string url){this.session=session;this.id=id;this.source=source;this.method=method;this.url=url;if(session!=0)timer=Stopwatch.StartNew();}
            internal void Complete(int status,long bytes){Finish(status.ToString(),bytes,"");}
            internal void Fail(Exception error)
            {
                var web=error as WebException;var response=web==null?null:web.Response as HttpWebResponse;
                string status=response==null?"ERROR":((int)response.StatusCode).ToString();
                string detail=error is OperationCanceledException?"Canceled":web!=null?web.Status.ToString():error.GetType().Name;
                Finish(status,0,detail);
            }
            private void Finish(string status,long bytes,string detail){if(ended)return;ended=true;if(session!=0)Write(session,id,source,method,status,timer.ElapsedMilliseconds,bytes,url,detail);}
            public void Dispose(){if(!ended)Finish("ERROR",0,"Request interrupted");}
        }
        internal static string ReadText(HttpWebRequest request,string source) {return SendText(request,source,null);}
        internal static byte[] ReadBytes(HttpWebRequest request,string source,int limit)
        {
            using(var trace=Begin(source,"IMAGE",request.RequestUri.AbsoluteUri))
            {
                try
                {
                    using(var response=(HttpWebResponse)request.GetResponse())using(var input=response.GetResponseStream())using(var output=new MemoryStream())
                    {
                        byte[] buffer=new byte[8192];int count;while((count=input.Read(buffer,0,buffer.Length))>0){if(output.Length+count>limit)throw new IOException("Response too large");output.Write(buffer,0,count);}
                        trace.Complete((int)response.StatusCode,output.Length);return output.ToArray();
                    }
                }
                catch(Exception error){trace.Fail(error);throw;}
            }
        }
        internal static string SendText(HttpWebRequest request,string source,byte[] body)
        {
            using(var trace=Begin(source,request.Method,request.RequestUri.AbsoluteUri))
            {
                try
                {
                    if(body!=null)using(var stream=request.GetRequestStream())stream.Write(body,0,body.Length);
                    using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))
                    {string text=reader.ReadToEnd();trace.Complete((int)response.StatusCode,Encoding.UTF8.GetByteCount(text));return text;}
                }
                catch(Exception error){trace.Fail(error);throw;}
            }
        }
    }
}
