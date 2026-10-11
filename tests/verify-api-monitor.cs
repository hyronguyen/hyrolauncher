using System;using System.IO;using System.Linq;using System.Threading.Tasks;using System.Drawing;using System.Windows.Forms;using System.Reflection;using System.Net;using System.Net.Sockets;using System.Text;using HYROLauncherWeb;
internal static class VerifyApiMonitor
{
 private static int checks;private static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 private static void RequestFixture(string method,int code)
 {
  var server=new TcpListener(IPAddress.Loopback,0);server.Start();int port=((IPEndPoint)server.LocalEndpoint).Port;
  var serve=Task.Run(()=>{using(var client=server.AcceptTcpClient())using(var stream=client.GetStream())using(var reader=new StreamReader(stream)){string line;while((line=reader.ReadLine())!=null&&line!=""){}byte[] bytes=Encoding.UTF8.GetBytes("HTTP/1.1 "+code+" Test\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");stream.Write(bytes,0,bytes.Length);}});
  try {
   var request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port+"/?key=HTTP_SECRET");request.Method=method;request.Timeout=5000;request.ServicePoint.Expect100Continue=false;request.Proxy=null;
   byte[] body=method=="POST"?Encoding.UTF8.GetBytes("client_secret=BODY_SECRET"):null;if(body!=null){request.ContentType="application/x-www-form-urlencoded";request.ContentLength=body.Length;}
   try{string text=ApiActivityLog.SendText(request,"Fixture",body);Check(code==200&&text=="OK","Response text changed");}catch(WebException error){if(error.Response!=null)error.Response.Dispose();Check(code==429,"HTTP fixture "+method+" expected "+code+" failed: "+error.Status);}
   Check(serve.Wait(5000),"HTTP fixture did not finish");
  }finally{server.Stop();}
 }
 [STAThread]public static int Main(string[] args)
 {
  try {
   string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);ApiActivityLog.Stop();
   var before=ApiActivityLog.Begin("Steam","GET","https://example.test/?key=SECRET");ApiActivityLog.Note("HYTCG","Closed");
   Check(!Directory.Exists(Path.Combine(root,"logs")),"Closed monitor creates files");
   ApiActivityLog.Start(root);before.Complete(200,20);Check(ApiActivityLog.ReadAfter(0).Length==0,"Request started while closed entered later session");
   string first=ApiActivityLog.FilePath;var stale=ApiActivityLog.Begin("Steam","GET","https://user:pass@example.test/path?KEY=SECRET&client%5Fsecret=HIDDEN&input_json=%7Btoken%7D&character_id=99991#TOKEN");
   stale.Fail(new Exception("NEVER_LOG_THIS_SECRET"));var rows=ApiActivityLog.ReadAfter(0);
   Check(rows.Length==2&&rows[0].Status=="START"&&rows[1].Status=="ERROR","Missing start/end");
   Check(rows.All(row=>!row.Url.Contains("SECRET")&&!row.Url.Contains("HIDDEN")&&!row.Url.Contains("user:pass")&&!row.Url.Contains("TOKEN")&&!row.Url.Contains("%7Btoken%7D")),"Credentials in URLs");
   Check(rows[0].Url.Contains("character_id=99991")&&rows[0].Url.Contains("[redacted]"),"Public query removed or key not masked");
   using(var reader=new StreamReader(new FileStream(first,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)))Check(!reader.ReadToEnd().Contains("NEVER_LOG_THIS_SECRET"),"Exception message exposed");
   var pending=ApiActivityLog.Begin("Zerochan","GET","https://www.zerochan.net/Alina+Gray?json");ApiActivityLog.Stop();long length=new FileInfo(first).Length;
   ApiActivityLog.Note("HYTCG","After close");pending.Complete(200,500);Check(new FileInfo(first).Length==length,"Writes continued after close");
   ApiActivityLog.Start(root);pending.Complete(200,500);Check(ApiActivityLog.ReadAfter(0).Length==0,"Old session completed into a reopened monitor");
   Parallel.For(0,50,index=>{using(var trace=ApiActivityLog.Begin("ACDB","GET","https://www.animecharactersdatabase.com/api_series_characters.php?character_id="+index))trace.Complete(200,index);});
   rows=ApiActivityLog.ReadAfter(0);Check(rows.Length==100&&rows.Select(row=>row.Sequence).Distinct().Count()==100,"Concurrent entries lost or duplicated");
   Check(rows.All(row=>row.Milliseconds>=0)&&rows.Count(row=>row.Status=="200")==50,"Invalid timing/status");
   string second=ApiActivityLog.FilePath;ApiActivityLog.Stop();Check(first!=second,"Reopen overwrites earlier session");
   ApiActivityLog.Start(root);RequestFixture("GET",200);RequestFixture("POST",200);RequestFixture("GET",429);rows=ApiActivityLog.ReadAfter(0);
   Check(rows.Count(row=>row.Status=="200")==2&&rows.Count(row=>row.Status=="429")==1,"Native HTTP helper status logging");
   Check(rows.All(row=>!row.Url.Contains("HTTP_SECRET")&&!row.Detail.Contains("BODY_SECRET")),"HTTP helper exposed credentials");ApiActivityLog.Stop();
   for(int i=0;i<3;i++)using(var window=new ApiLogWindow(root,Color.FromArgb(75,204,233)))
   {
    Check(!ApiActivityLog.Active,"Constructing hidden window starts logging");
    window.ShowInTaskbar=false;window.StartPosition=FormStartPosition.Manual;window.Location=new Point(-20000,-20000);window.Show();Application.DoEvents();
    Check(ApiActivityLog.Active,"Opening window did not start log");
    ApiActivityLog.Note("HYTCG","Alina Gray · ALT");using(var trace=ApiActivityLog.Begin("Zerochan","GET","https://www.zerochan.net/123?json"))trace.Complete(200,8192);
    typeof(ApiLogWindow).GetMethod("RefreshRows",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[0]);
    var list=(ListView)typeof(ApiLogWindow).GetField("list",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
    var filter=(ComboBox)typeof(ApiLogWindow).GetField("filter",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
    var search=(TextBox)typeof(ApiLogWindow).GetField("search",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
    var outcome=(ComboBox)typeof(ApiLogWindow).GetField("outcome",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
    Check(filter.Items.Contains("AniList"),"AniList source filter missing");
    search.Text="no-match";Check(list.Items.Count==0,"Search did not filter requests");search.Text="Alina";Check(list.Items.Count==1,"Activity search failed");search.Text="";
    filter.SelectedItem="Zerochan";Check(list.Items.Count==2,"Source filter failed");outcome.SelectedIndex=1;Check(list.Items.Count==0,"Success requests leaked into errors filter");outcome.SelectedIndex=0;filter.SelectedIndex=0;
    if(i==0)using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,window.Size));bitmap.Save(Path.Combine(root,"api-monitor.png"));}
    window.Close();Application.DoEvents();
    Check(!ApiActivityLog.Active,"Closing window leaves capture active");
   }
   Check(File.ReadAllText("WebLauncher.cs").Contains("menu.Items.Add(\"API Monitor\""),"Tray command missing");
   Console.WriteLine("PASS: "+checks+" checks · closed/open/reopen lifecycle, session boundaries, credential masking, concurrency, disk log and native monitor UI.");return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}finally{ApiActivityLog.Stop();}
 }
}
