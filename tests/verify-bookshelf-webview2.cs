using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

// Runs the real launcher document in an isolated WebView2 profile. There is no
// launcher owner, account, external request, or settings file behind this host.
internal sealed class BookshelfNativeSmoke : Form
{
    private readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=10000000};
    private readonly Dictionary<string,object> report=new Dictionary<string,object>();
    private readonly string root=AppDomain.CurrentDomain.BaseDirectory;
    private readonly WebView2 browser=new WebView2 {Dock=DockStyle.Fill};
    private int checks;
    private BookshelfNativeSmoke()
    {
        Text="Isolated bookshelf WebView2 verification";StartPosition=FormStartPosition.Manual;
        Location=new Point(-20000,-20000);Size=new Size(1500,920);Opacity=0;ShowInTaskbar=false;
        Controls.Add(browser);Shown+=async delegate {await Run();};
    }
    private void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
    private async Task<object> Script(string source){return json.DeserializeObject(await browser.CoreWebView2.ExecuteScriptAsync(source));}
    private async Task<Dictionary<string,object>> Snapshot(){return (Dictionary<string,object>)await Script("(()=>{const e=document.querySelector('[data-book-case=\"qa-1\"]'),f=e.querySelector('.shelfCaseFace'),m=new DOMMatrixReadOnly(getComputedStyle(f).transform);return {axis:m.m11,z:m.m43,y:m.m42,duration:getComputedStyle(f).transitionDuration,selected:e.classList.contains('selected'),same:window.qaCase===e,neighbor:document.querySelector('[data-book-case=\"qa-2\"]').getBoundingClientRect().x};})()");}
    private static double Number(Dictionary<string,object> item,string key){return Convert.ToDouble(item[key]);}
    private async Task SendState(bool effects)
    {
        string svg="data:image/svg+xml,"+Uri.EscapeDataString("<svg xmlns='http://www.w3.org/2000/svg' width='600' height='900'><rect width='600' height='900' fill='#245678'/><text x='20' y='100' fill='white' font-size='40'>Native fixture</text></svg>");
        var games=new object[] {new {Id="qa-1",Name="Native animation",IconPath=svg,BannerPath=svg,Category="Games",Path="C:\\fixture.exe",Favorite=false},new {Id="qa-2",Name="Stable neighbor",IconPath=svg,BannerPath=svg,Category="Games",Path="C:\\neighbor.exe",Favorite=false}};
        browser.CoreWebView2.PostWebMessageAsJson(json.Serialize(new {type="state",state=new {Games=games,Categories=new[]{"Games"},Plugins=new object[0],Settings=new {LibraryLayout="bookshelf",Language="en",ThemeMode="dark",AccentColor="#27aac4",DisableBgm=true,DisableEffects=effects,BookshelfAnimationsEnabled=true,BookshelfPullDuration=5000,BookshelfHoverDuration=260,BookshelfPullDistance=52,BookshelfHoverLift=10,BookshelfDefaultAngle=21,BookshelfSelectedAngle=12}},assets=new {logo=svg,banner=svg},runningIds=new string[0]}));
        await Task.Delay(100);
    }
    private async Task Run()
    {
        try
        {
            bool forced=Environment.GetCommandLineArgs().Length>1 && Environment.GetCommandLineArgs()[1]=="reduced";
            bool baseline=Environment.GetCommandLineArgs().Length>2 && Environment.GetCommandLineArgs()[2]=="baseline";
            var options=new CoreWebView2EnvironmentOptions(forced?"--force-prefers-reduced-motion":"");
            var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"profile"),options);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            browser.CoreWebView2.WebResourceRequested+=delegate(object sender,CoreWebView2WebResourceRequestedEventArgs e){if(e.Request.Uri.StartsWith("http",StringComparison.OrdinalIgnoreCase))e.Response=environment.CreateWebResourceResponse(new MemoryStream(),403,"Fixture blocks network","");};
            var navigation=new TaskCompletionSource<bool>();browser.CoreWebView2.NavigationCompleted+=delegate {navigation.TrySetResult(true);};
            browser.CoreWebView2.Navigate(new Uri(Path.Combine(root,"ui","index.html")).AbsoluteUri);await navigation.Task;
            report["Runtime"]=environment.BrowserVersionString;report["ForcedReducedMotion"]=forced;
            report["PrefersReducedMotion"]=await Script("matchMedia('(prefers-reduced-motion: reduce)').matches");
            await SendState(false);await Task.Delay(400);
            Check(Convert.ToInt32(await Script("document.querySelectorAll('.shelfCase').length"))==2,"Real document did not render two fixture cases.");
            await Script("window.qaCase=document.querySelector('[data-book-case=\"qa-1\"]')");
            var closed=await Snapshot();report["Closed"]=closed;
            await Script("document.querySelector('[data-book-case=\"qa-1\"] .shelfCaseFace').click()");
            await Task.Delay(250);var first=await Snapshot();report["At250ms"]=first;
            await SendState(false);browser.CoreWebView2.PostWebMessageAsJson("{\"type\":\"runningState\",\"runningIds\":[\"qa-2\"]}");
            await Task.Delay(650);var second=await Snapshot();report["At1000msWithNativeRefresh"]=second;
            if(!baseline)
            {
                Check((string)first["duration"]=="5s","Explicit 5000 ms setting was overridden.");
                Check(Number(first,"axis")>Number(closed,"axis")+.01 && Number(first,"axis")<.97,"At 250 ms the selected box jumped to its endpoint.");
                Check(Number(second,"axis")>Number(first,"axis")+.05 && Number(second,"axis")<.975,"At 1000 ms the box was not still moving.");
                Check((bool)second["same"],"Native state refresh replaced the moving case.");
                Check(Number(second,"neighbor")==Number(closed,"neighbor"),"Neighbor shifted during animation.");
            }
            await Task.Delay(4200);var complete=await Snapshot();report["Complete"]=complete;
            if(!baseline)Check(Math.Abs(Number(complete,"axis")-Math.Cos(12*Math.PI/180))<.001 && Math.Abs(Number(complete,"z")-52)<.01,"The animation did not reach the actual selected position.");
            // Global cosmetic-effects disabling must not silence the explicit
            // bookshelf animation option the user has left enabled.
            await SendState(true);await Script("document.querySelector('[data-book-case=\"qa-1\"] .shelfCaseFace').click()");
            await Task.Delay(250);var returning=await Snapshot();report["ReturnWithGlobalEffectsOff"]=returning;
            if(!baseline)
            {
                Check((string)returning["duration"]=="5s","Global effects override disabled the bookshelf animation.");
                Check(Number(returning,"axis")<Number(complete,"axis")-.01 && Number(returning,"axis")>Number(closed,"axis")+.05,"Close jumped to its endpoint.");
                Check((bool)returning["same"],"Close should animate the same object.");
            }
            await Script("state.Settings.BookshelfAnimationsEnabled=false;applyBookshelfSettings();setBookshelfSelection('qa-1')");
            await Task.Delay(80);var disabled=await Snapshot();report["ExplicitDisabled"]=disabled;
            if(!baseline)Check((string)disabled["duration"]=="0s","The dedicated animation toggle no longer disables motion.");
            report["Checks"]=checks;report["Status"]="PASS";File.WriteAllText(Path.Combine(root,"report.json"),json.Serialize(report));
            Console.WriteLine("PASS "+checks+" actual WebView2 bookshelf checks; reduced motion="+report["PrefersReducedMotion"]);Environment.ExitCode=0;
        }
        catch(Exception e){report["Status"]="FAIL";report["Error"]=e.ToString();File.WriteAllText(Path.Combine(root,"report.json"),json.Serialize(report));Console.Error.WriteLine(e);Environment.ExitCode=1;}
        finally{Close();}
    }
    [STAThread] private static void Main(){Application.EnableVisualStyles();Application.Run(new BookshelfNativeSmoke());}
}
