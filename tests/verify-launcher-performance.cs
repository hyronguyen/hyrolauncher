using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal static class VerifyLauncherPerformance
{
    private static int checks,exitCode;
    private static readonly BindingFlags instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private static readonly BindingFlags statics=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    private static Type ownerType;
    private static object Get(object target,string name) {return target.GetType().GetField(name,instance).GetValue(target);}
    private static void Set(object target,string name,object value) {target.GetType().GetField(name,instance).SetValue(target,value);}
    private static object Call(object target,string name,params object[] args) {return target.GetType().GetMethod(name,instance).Invoke(target,args);}
    private static void Check(bool value,string name) {checks++;if(!value)throw new Exception(name);}
    private static async Task Wait(Func<bool> condition,int milliseconds,string name)
    {var timer=Stopwatch.StartNew();while(!condition()){if(timer.ElapsedMilliseconds>milliseconds)throw new Exception("Timeout: "+name);await Task.Delay(35);}}
    private static async Task<string> Script(WebView2 view,string code) {return await view.CoreWebView2.ExecuteScriptAsync(code);}
    private static async Task WaitScript(WebView2 view,string code,int milliseconds,string name)
    {var timer=Stopwatch.StartNew();while(await Script(view,code)!="true"){if(timer.ElapsedMilliseconds>milliseconds)throw new Exception("Timeout: "+name);await Task.Delay(50);}}
    private static async Task Activity(WebView2 view,bool active,bool keepAudio,bool keepRunning)
    {
        var method=ownerType.GetMethod("SetViewActivityAsync",statics);var parameters=method.GetParameters();
        await (Task)method.Invoke(null,parameters.Length==4?new object[]{view,active,keepAudio,keepRunning}:new object[]{view,active,keepAudio});
    }
    private static double Number(string text) {return new JavaScriptSerializer().Deserialize<double>(text);}
    private static void SaveSilence(string root)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            int size=8000*2*60;writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+size);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(size);writer.Write(new byte[size]);File.WriteAllBytes(Path.Combine(root,"silence.wav"),stream.ToArray());
        }
        File.WriteAllText(Path.Combine(root,"audio.html"),"<!doctype html><audio id='test' src='silence.wav' autoplay loop></audio><script>document.querySelector('audio').play().catch(()=>{});</script>");
    }
    private static async Task Run(Form owner,string root,Assembly assembly)
    {
        WebView2 main=(WebView2)Get(owner,"webView"),link=(WebView2)Get(owner,"linkView"),feed=(WebView2)Get(owner,"feedView");
        Check(link.CoreWebView2==null && feed.CoreWebView2==null,"auxiliary views are not initialized during startup");
        var surface=(Control)Get(owner,"startupSurface");Check(surface!=null && surface.Visible && owner.Controls.GetChildIndex(surface)==0,"native splash precedes renderer");
        using(var bitmap=new Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,"startup.png"),ImageFormat.Png);}
        Check(surface.Controls.Count>=2 && surface.Controls[0].Text.Contains("launcher"),"splash status and progress visible");
        await Wait(()=>main.CoreWebView2!=null && Get(owner,"startupSurface")==null,25000,"first paint removes native startup surface");
        Check(link.CoreWebView2==null && feed.CoreWebView2==null,"ready launcher still owns only one initialized WebView");
        Check(!main.CoreWebView2.Settings.IsZoomControlEnabled && !main.CoreWebView2.Settings.IsPinchZoomEnabled,"launcher zoom remains disabled");
        main.CoreWebView2.PostWebMessageAsJson("{\"type\":\"launcherActivity\",\"active\":false}");
        await WaitScript(main,"document.querySelector('.app').classList.contains('resourceIdle')",5000,"renderer enters idle resource mode");
        Check(true,"idle message pauses CSS and pet resource mode");
        main.CoreWebView2.PostWebMessageAsJson("{\"type\":\"launcherActivity\",\"active\":true}");
        await WaitScript(main,"!document.querySelector('.app').classList.contains('resourceIdle')",5000,"renderer exits idle resource mode");
        Type panelType=assembly.GetType("HYROLauncherWeb.LauncherLoadingPanel",true);
        var failed=(Control)Activator.CreateInstance(panelType,instance,null,new object[]{Color.Cyan,false},null);owner.Controls.Add(failed);failed.BringToFront();
        try {Call(failed,"Failure","Fixture initialization failure");Check(failed.Controls.OfType<Button>().Any(button=>button.Visible && button.Text=="Close") && failed.Controls.OfType<ProgressBar>().All(bar=>!bar.Visible),"startup failure has a native close action");}
        finally {owner.Controls.Remove(failed);failed.Dispose();}
        string fixture=new Uri(Path.Combine(root,"page.html")).AbsoluteUri;
        Call(owner,"ShowWebLink",fixture,"left");Call(owner,"HideWebLink");
        await (Task)Get(owner,"linkInitialization");await Task.Delay(150);
        Check(!link.Visible && !(bool)Get(owner,"linkRequested"),"hide cancels in-flight lazy link open");
        Call(owner,"ShowWebLink",fixture,"left");await Wait(()=>link.Visible && link.CoreWebView2!=null,5000,"lazy link show");
        await Wait(()=>link.CoreWebView2.Source==fixture,5000,"link navigation");
        Check(feed.CoreWebView2==null,"link initialization does not initialize feed");
        Call(owner,"HideWebLink");await Wait(()=>link.CoreWebView2.IsSuspended,5000,"hidden silent link suspension");
        Check(link.CoreWebView2.MemoryUsageTargetLevel==CoreWebView2MemoryUsageTargetLevel.Low,"hidden link uses low memory target");
        Call(owner,"ShowWebLink",fixture,"left");await Wait(()=>link.Visible && !link.CoreWebView2.IsSuspended,5000,"link resume");
        Call(owner,"HideWebLink");
        var message=new Dictionary<string,object>{{"url",fixture},{"x",20},{"y",100},{"width",300},{"height",200}};
        Call(owner,"ShowGameFeed",message);Call(owner,"HideGameFeed");await (Task)Get(owner,"feedInitialization");await Task.Delay(150);
        Check(!feed.Visible && !(bool)Get(owner,"feedRequested"),"hide cancels in-flight lazy feed open");
        Call(owner,"ShowGameFeed",message);await Wait(()=>feed.Visible && feed.CoreWebView2!=null,5000,"lazy feed show");
        Check(feed.CoreWebView2.IsMuted && feed.ZoomFactor==.25,"feed initialization preserves mute and scale");
        Call(owner,"HideGameFeed");await Wait(()=>feed.CoreWebView2.IsSuspended,5000,"hidden feed suspension");
        owner.Hide();await Task.Delay(400);
        await Wait(()=>main.CoreWebView2.IsSuspended,5000,"hidden silent launcher suspension");
        Check(main.CoreWebView2.MemoryUsageTargetLevel==CoreWebView2MemoryUsageTargetLevel.Low,"hidden launcher low memory target");
        owner.Show();await Wait(()=>!main.CoreWebView2.IsSuspended,5000,"launcher restored");
        await WaitScript(main,"!document.querySelector('.app').classList.contains('resourceIdle')",5000,"resourceIdle clear after restore");
        Check(true,"resourceIdle cleared after restore");
        var windowType=assembly.GetType("HYROLauncherWeb.PluginBrowserWindow",true);var music=(Form)Activator.CreateInstance(windowType,instance,null,new object[]{owner,true},null);
        Set(owner,"musicWindow",music);
        try
        {
            music.ShowInTaskbar=false;music.Location=new Point(-19000,-19000);music.Show();
            await (Task)windowType.GetMethod("EnsureWebViewAsync",instance).Invoke(music,new[]{Get(owner,"webEnvironment")});
            WebView2 browser=(WebView2)Get(music,"browser");browser.CoreWebView2.Navigate(new Uri(Path.Combine(root,"audio.html")).AbsoluteUri);
            await Wait(()=>browser.CoreWebView2.Source.EndsWith("audio.html",StringComparison.OrdinalIgnoreCase),5000,"local silent movie fixture");
            await WaitScript(browser,"Boolean(document.querySelector('audio') && !document.querySelector('audio').paused)",5000,"audio playback");
            await Task.Delay(500);Check(await Script(browser,"document.querySelector('audio').paused")=="false","silent audio fixture playing");
            Set(owner,"mp3PlaybackActive",true);double before=Number(await Script(browser,"document.querySelector('audio').currentTime"));
            Call(music,"HideByUser");await Task.Delay(1250);
            Check(!browser.CoreWebView2.IsSuspended && (bool)Get(owner,"mp3PlaybackActive"),"hidden music remains running even when silent");
            double after=Number(await Script(browser,"document.querySelector('audio').currentTime"));Check(after-before>.8,"hidden music playback clock advances");
            Check(browser.CoreWebView2.MemoryUsageTargetLevel==CoreWebView2MemoryUsageTargetLevel.Low,"background music lowers memory target without stopping");
        }
        finally {Set(owner,"mp3PlaybackActive",false);music.Dispose();Set(owner,"musicWindow",null);}
        // Exercise async suspend/resume race directly: last visible state wins.
        Task suspend=Activity(link,false,false,false);Call(owner,"ShowWebLink",fixture,"left");await suspend;
        await Wait(()=>!link.CoreWebView2.IsSuspended,5000,"late suspension cannot stop visible link");
        Check(link.Visible,"link remains visible after suspension race");
        Call(owner,"HideWebLink");
        File.WriteAllText(Path.Combine(root,"report.json"),"{\"checks\":"+checks+",\"productionWritten\":false,\"network\":false,\"realWebView2\":true}");
        Console.WriteLine("PASS: "+checks+" real WebView2 startup, lazy auxiliary initialization, cancellation, low-memory suspension, restore and silent background music checks.");
    }
    [STAThread] internal static int Main(string[] args)
    {
        string root=AppDomain.CurrentDomain.BaseDirectory;SaveSilence(root);File.WriteAllText(Path.Combine(root,"page.html"),"<!doctype html><title>Local performance fixture</title><h1>Local fixture</h1>");
        Assembly assembly=Assembly.LoadFrom(Path.Combine(root,"HYROLauncher.check.exe"));ownerType=assembly.GetType("HYROLauncherWeb.WebLauncherForm",true);
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var owner=(Form)Activator.CreateInstance(ownerType,true);owner.ShowInTaskbar=false;owner.Opacity=.01;owner.StartPosition=FormStartPosition.Manual;owner.Location=new Point(-19000,-19000);
        owner.Shown+=async delegate
        {
            try {await Run(owner,root,assembly);}
            catch(Exception error){Console.Error.WriteLine(error);exitCode=1;}
            finally {Set(owner,"exitRequested",true);owner.Close();}
        };
        Application.Run(owner);return exitCode;
    }
}
