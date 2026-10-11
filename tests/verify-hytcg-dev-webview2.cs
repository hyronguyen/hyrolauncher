using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

// This host deliberately never shows the launcher owner: its startup Load/Shown
// handlers can fetch external data. Only the real plugin browser is instantiated.
internal sealed class HyTcgDevWebViewSmoke : Form
{
    private const string CardId="HT-20261011-123456ABCDEF";
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=20000000};
    private readonly string root=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    private readonly List<string> requests=new List<string>();
    private readonly List<string> directories=new List<string>();
    private readonly List<string> phases=new List<string>();
    private readonly List<string> nativeExceptions=new List<string>();
    private Form owner,panel;
    private WebView2 browser;
    private Assembly assembly;
    private object realService,realEconomy;
    private string realRoot,realState;
    private int checks,navigations;

    private HyTcgDevWebViewSmoke()
    {
        Text="Isolated HYTCG native dev bridge verification";
        StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);
        Size=new Size(400,300);Opacity=0;ShowInTaskbar=false;
        Shown+=async delegate {await Run();};
    }
    private static object Call(object target,string name,params object[] args)
    {
        Type type=target is Type?(Type)target:target.GetType();
        MethodInfo method=type.GetMethods(Flags).Where(m=>m.Name==name && m.GetParameters().Length==args.Length)
            .FirstOrDefault(m=>m.GetParameters().Select((p,i)=>args[i]==null || p.ParameterType.IsInstanceOfType(args[i])).All(x=>x));
        if(method==null)throw new Exception("Method missing: "+type.Name+"."+name);
        try{return method.Invoke(target is Type?null:target,args);}catch(TargetInvocationException e){throw e.InnerException;}
    }
    private static object Value(object target,string name)
    {
        var property=target.GetType().GetProperty(name,Flags);
        return property!=null?property.GetValue(target,null):target.GetType().GetField(name,Flags).GetValue(target);
    }
    private static void Field(object target,string name,object value) {target.GetType().GetField(name,Flags).SetValue(target,value);}
    private void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    private static string Hash(string file){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(file)));}
    private Dictionary<string,string> Hashes(){return Directory.GetFiles(realRoot,"*",SearchOption.AllDirectories).ToDictionary(f=>f.Substring(realRoot.Length),Hash,StringComparer.Ordinal);}
    private string SemanticState()
    {
        return json.Serialize(new {
            Gems=Value(Call(realEconomy,"Snapshot"),"Gems"),
            Paid=Value(Call(realEconomy,"Snapshot"),"PaidCredits"),
            Limited=Value(Call(realEconomy,"Snapshot"),"LimitedCredits"),
            Cards=Call(realEconomy,"CollectionCards"),
            Profile=Call(realEconomy,"GameplayProfileSnapshot",CardId),
            Creative=Call(realEconomy,"CreativeSnapshot")
        });
    }
    private void CreateFixture()
    {
        assembly=Assembly.LoadFrom(Path.Combine(root,"HYROLauncher.Check.exe"));
        realRoot=Path.Combine(root,"real-fixture");Directory.CreateDirectory(realRoot);
        Type service=assembly.GetType("HYROLauncherWeb.HyTcgService");
        realService=Activator.CreateInstance(service,Flags,null,new object[]{realRoot},CultureInfo.InvariantCulture);
        Call(realService,"UseReadOnlyConfig",root);realEconomy=Value(realService,"Economy");
        Check((int)Value(Call(realEconomy,"Snapshot"),"Gems")==1800,"Fixture did not start at normal wallet.");
        object reserve=Call(realEconomy,"Reserve",1,"gems");string token=((List<string>)Value(reserve,"Tokens"))[0];Call(realEconomy,"Begin",token);
        var input=new Dictionary<string,object> {
            {"CharacterId",99991},{"Name","Sandbox test"},{"Series","HYTCG verification"},{"Gender","Female"},
            {"Rarity","Common"},{"RarityColor","#94A3B8"},{"ImageSource","acdb"},
            {"SourceImage","https://www.animecharactersdatabase.com/test.png"},{"ProfileImage","https://www.animecharactersdatabase.com/test.png"},
            {"CardId",CardId},{"CreatedAt","2026-10-11T12:00:00Z"},{"Orientation","portrait"},{"FrameLayout","window-v2"}
        };
        object card=json.Deserialize(json.Serialize(input),assembly.GetType("HYROLauncherWeb.HyTcgCard"));Call(realEconomy,"AuthorizeCard",token,card);
        byte[] bytes;using(var bitmap=new Bitmap(750,1050))using(var graphics=Graphics.FromImage(bitmap))using(var stream=new MemoryStream())
        {graphics.Clear(Color.CornflowerBlue);bitmap.Save(stream,ImageFormat.Png);bytes=(byte[])Call(assembly.GetType("HYROLauncherWeb.HyTcgService+Png"),"WithMetadata",stream.ToArray(),json.Serialize(card));}
        byte[] container=(byte[])Call(assembly.GetType("HYROLauncherWeb.HyTcgStorage"),"Protect",bytes);
        string cardPath=Path.Combine(realRoot,"cards","test.hycard");Directory.CreateDirectory(Path.GetDirectoryName(cardPath));File.WriteAllBytes(cardPath,container);
        Call(realEconomy,"Complete",token,card,"test.hycard",container);
        Check(((ICollection)Call(realEconomy,"CollectionCards")).Count==1,"Protected fixture card missing.");
        owner=(Form)Activator.CreateInstance(assembly.GetType("HYROLauncherWeb.WebLauncherForm"),Flags,null,new object[0],CultureInfo.InvariantCulture);
        ((NotifyIcon)Value(owner,"trayIcon")).Visible=false;
        IntPtr unused=owner.Handle;
        Field(owner,"hyTcg",realService);Field(owner,"currentBuiltInPluginId","builtin.hytcg");
        panel=(Form)Activator.CreateInstance(assembly.GetType("HYROLauncherWeb.PluginBrowserWindow"),Flags,null,new object[]{owner},CultureInfo.InvariantCulture);
        Field(owner,"pluginWindow",panel);Field(panel,"docked",false);
        panel.ShowInTaskbar=false;panel.StartPosition=FormStartPosition.Manual;
        panel.Location=new Point(-20000,-20000);panel.Size=new Size(1080,820);panel.Opacity=0;
        browser=(WebView2)Value(panel,"browser");
    }
    private async Task Script(string source){await browser.CoreWebView2.ExecuteScriptAsync(source);}
    private async Task<object> Evaluate(string source)
    {return json.DeserializeObject(await browser.CoreWebView2.ExecuteScriptAsync(source));}
    private async Task Wait(string expression,string description,int timeout=14000)
    {
        DateTime end=DateTime.UtcNow.AddMilliseconds(timeout);
        while(DateTime.UtcNow<end)
        {
            try {if(Convert.ToBoolean(await Evaluate("Boolean("+expression+")")))return;}catch { }
            await Task.Delay(70);
        }
        string diagnostics=await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({url:location.href,ready:document.readyState,badge:document.getElementById('devModeBadge')?.hidden,gems:document.getElementById('gemBalance')?.textContent,probe:window.devProbe,config:typeof tcg==='undefined'?null:tcg.config,session:typeof tcg==='undefined'?null:tcg.session,toast:document.getElementById('toast')?.textContent})");
        throw new Exception("Wait timed out: "+description+"\n"+diagnostics);
    }
    private async Task NativeWait(Func<bool> condition,string description)
    {
        DateTime end=DateTime.UtcNow.AddSeconds(14);
        while(DateTime.UtcNow<end){if(condition())return;await Task.Delay(70);}
        throw new Exception("Native wait timed out: "+description);
    }
    private async Task<object> Lifestyle(string action,Dictionary<string,object> input=null)
    {
        var payload=input??new Dictionary<string,object>();payload["action"]=action;
        await Script("window.devProbe.result=null;window.devProbe.failure=null;call('hytcgLifestyle',"+json.Serialize(payload)+").then(r=>window.devProbe.result=r).catch(e=>window.devProbe.failure=String(e));");
        await Wait("window.devProbe.result || window.devProbe.failure","local lifestyle action "+action);
        Check(await Evaluate("window.devProbe.failure")==null,"Native lifecycle action failed: "+action+" "+await Evaluate("window.devProbe.failure"));
        return await Evaluate("window.devProbe.result");
    }
    private string Operation(object creative){return "SC-"+Value(creative,"NextStudioSequence")+"-"+Guid.NewGuid().ToString("N");}
    private async Task<string> EnterDev()
    {
        string before=(string)await Evaluate("tcg.session");
        await Script("for(let i=0;i<4;i++)document.getElementById('tcgDevIcon').click();");await Task.Delay(120);
        Check(Value(owner,"hyTcgDev")==null,"Four brand clicks enabled dev mode.");
        await Script("document.getElementById('tcgDevIcon').click();");
        await Wait("window.HyTcgDev?.active && tcg.config && tcg.collectionTotal===1 && document.getElementById('gemBalance')?.textContent==='∞'","five clicks enabled native dev mode");
        object dev=Value(owner,"hyTcgDev");Check(dev!=null,"UI dev active without native sandbox.");
        string directory=(string)Value(dev,"DirectoryPath");directories.Add(directory);
        Check(Directory.Exists(directory),"Native sandbox directory missing.");
        Check(!Object.ReferenceEquals(Value(owner,"hyTcg"),realService),"Native service was not swapped.");
        Check((bool)Value(Value(Value(owner,"hyTcg"),"Economy"),"IsDevMode"),"Mounted native economy is not dev.");
        Check(!panel.IsDisposed && !owner.Visible,"Smoke accidentally showed launcher startup owner.");
        Check((string)await Evaluate("tcg.session")!=before,"Dev session reused original renderer session.");
        Check(!(bool)await Evaluate("document.getElementById('devModeBadge').hidden"),"Dev badge stayed hidden.");
        Check((bool)await Evaluate("['gemBalance','freeBalance','paidBalance','limitedBalance','refillCountdown'].every(id=>document.getElementById(id).textContent==='∞')"),"Not every resource became infinite.");
        Check((int)Value(dev,"RemainingSeconds")>=580 && (int)Value(dev,"RemainingSeconds")<=600,"Native dev lifetime is not ten minutes.");
        phases.Add("enter");return directory;
    }
    private async Task Restored(string directory,string phase)
    {
        await Wait("tcg.config && tcg.collectionTotal===1 && !window.HyTcgDev?.active && document.getElementById('gemBalance')?.textContent!=='∞'","normal UI restored after "+phase);
        await NativeWait(()=>!Directory.Exists(directory),"sandbox cleanup after "+phase);
        Check(Value(owner,"hyTcgDev")==null,"Dev session remains after "+phase);
        Check(Object.ReferenceEquals(Value(owner,"hyTcg"),realService),"Original native service was not restored after "+phase);
        Check(SemanticState()==realState,"Original account semantics changed after "+phase);
        Check(((ICollection)Value(Call(realEconomy,"CreativeSnapshot"),"Albums")).Count==0,"Temporary album leaked after "+phase);
        Check((int)Value(Call(realEconomy,"GameplayProfileSnapshot",CardId),"Level")==1,"Dev level leaked after "+phase);
        Check((bool)await Evaluate("document.getElementById('devModeBadge').hidden"),"Dev badge remained after "+phase);
        Check((string)await Evaluate("document.getElementById('gemBalance').textContent.replace(/[^0-9]/g,'')")=="1640","Normal wallet did not restore 1,640 gems.");
        Check((bool)await Evaluate("devProbe.errors.length===0 && devProbe.csp.length===0"),"Restored WebView reported JS/CSP errors after "+phase);
        phases.Add(phase);
    }
    private async Task Run()
    {
        try
        {
            AppDomain.CurrentDomain.FirstChanceException+=delegate(object sender,System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) {
                if(e.Exception.GetType().Name=="HyTcgException")lock(nativeExceptions)nativeExceptions.Add(e.Exception.ToString()+"\n"+Environment.StackTrace);
            };
            CreateFixture();
            var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"webview-profile"));
            Field(owner,"webEnvironment",environment);panel.Show();await (Task)Call(panel,"EnsureWebViewAsync",environment);
            string preload=@"window.devProbe={sent:[],blocked:[],errors:[],csp:[],received:[],result:null,failure:null};
                addEventListener('error',e=>devProbe.errors.push(e.message));
                addEventListener('unhandledrejection',e=>devProbe.errors.push(String(e.reason)));
                addEventListener('securitypolicyviolation',e=>devProbe.csp.push({blocked:e.blockedURI,directive:e.violatedDirective}));
                const nativePost=chrome.webview.postMessage.bind(chrome.webview);
                chrome.webview.postMessage=m=>{devProbe.sent.push(m.type);
                  if(['ready','hytcgInit','hytcgWallet','hytcgCollection','hytcgDevToggle','hytcgPanelSize'].includes(m.type) || (m.type==='hytcgLifestyle' && ['snapshot','roster','profile','albumCreate','dyeBoosterBuy','expeditionStart','expeditionClaim'].includes(m.action)))nativePost(m);
                  else devProbe.blocked.push({type:m.type,action:m.action||''});};
                chrome.webview.addEventListener('message',e=>devProbe.received.push({type:e.data.type,error:e.data.error||'',reason:e.data.reason||''}));";
            await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(preload);
            browser.CoreWebView2.AddWebResourceRequestedFilter("http://*",CoreWebView2WebResourceContext.All);
            browser.CoreWebView2.AddWebResourceRequestedFilter("https://*",CoreWebView2WebResourceContext.All);
            browser.CoreWebView2.WebResourceRequested+=delegate(object sender,CoreWebView2WebResourceRequestedEventArgs e){e.Response=environment.CreateWebResourceResponse(new MemoryStream(),403,"Isolated smoke blocks external resources","");};
            browser.CoreWebView2.WebMessageReceived+=delegate(object sender,CoreWebView2WebMessageReceivedEventArgs e){try {var m=json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);requests.Add(Convert.ToString(m["type"]));}catch {}};
            browser.CoreWebView2.NavigationStarting+=delegate {navigations++;};
            string url=(string)Call(assembly.GetType("HYROLauncherWeb.PluginBrowserWindow"),"LocalPanelUrl","hytcg.html","builtin.hytcg");
            Field(panel,"currentUrl",url);browser.CoreWebView2.Navigate(url);
            await Wait("typeof tcg!=='undefined' && tcg.config && tcg.collectionTotal===1","initial actual native configuration and collection");
            Check((bool)await Evaluate("document.getElementById('devModeBadge').hidden"),"Normal account began in dev mode.");
            Check((int)Value(Call(realEconomy,"Snapshot"),"Gems")==1640,"Native init altered wallet.");
            realState=SemanticState();
            var hashes=Hashes();
            string directory=await EnterDev();
            var devEconomy=Value(Value(owner,"hyTcg"),"Economy");
            await Lifestyle("snapshot");
            Check(((ICollection)Value(Call(devEconomy,"CreativeSnapshot"),"Dyes")).Count==128,"Native sandbox did not unlock all 64+64 dyes.");
            Check((int)Value(Call(devEconomy,"GameplayProfileSnapshot",CardId),"Level")==30,"Native sandbox did not unlock skills and level.");
            await Lifestyle("albumCreate",new Dictionary<string,object>{{"operationId",Operation(Call(devEconomy,"CreativeSnapshot"))},{"name","Temporary bridge album"},{"background","midnight"},{"cover","midnight"}});
            Check(((ICollection)Value(Call(devEconomy,"CreativeSnapshot"),"Albums")).Count==1,"Paid album did not cross real native bridge.");
            await Lifestyle("dyeBoosterBuy",new Dictionary<string,object>{{"operationId",Operation(Call(devEconomy,"CreativeSnapshot"))}});
            Check((string)Value(Value(Call(devEconomy,"CreativeSnapshot"),"LastStudioReceipt"),"Action")=="dyeBoosterBuy","Dye roll did not cross real native bridge.");
            await Lifestyle("expeditionStart",new Dictionary<string,object>{{"routeId","meadow"},{"cardIds",new string[]{CardId}}});
            Check(((ICollection)Value(Call(devEconomy,"GameplaySnapshot"),"Expeditions")).Count==1,"Sandbox expedition did not cross real native bridge.");
            Check(hashes.OrderBy(p=>p.Key).SequenceEqual(Hashes().OrderBy(p=>p.Key)),"Actual native dev bridge wrote original account files.");
            Check((string)await Evaluate("document.getElementById('gemBalance').textContent")=="∞","Paid action lost infinite UI wallet.");
            await Script("document.getElementById('devModeBadge').click();");await Restored(directory,"exit");
            directory=await EnterDev();object session=Value(owner,"hyTcgDev");
            Field(session,"elapsed",new Func<TimeSpan>(delegate{return TimeSpan.FromSeconds(601);}));
            Call(owner,"HyTcgDevTick",null,EventArgs.Empty);await Restored(directory,"expired");
            directory=await EnterDev();Call(owner,"PluginClosedByUser",panel);
            await NativeWait(()=>Value(owner,"hyTcgDev")==null && !Directory.Exists(directory),"panel closure clears sandbox");
            Check(Object.ReferenceEquals(Value(owner,"hyTcg"),realService),"Panel closure did not restore native service.");
            Check(SemanticState()==realState,"Panel closure changed original account.");
            Check((string)Value(owner,"currentBuiltInPluginId")=="","Panel closure kept HYTCG document authorization.");
            phases.Add("panel-close");
            Check(!owner.Visible,"Launcher owner was shown during smoke.");
            Check(!requests.Any(r=>r=="hytcgDiscoveryState" || r=="hytcgRepairCards" || r=="hytcgOpen" || r=="hytcgCharacterInfo"),"External API-producing native request escaped filter.");
            Check(navigations>=6,"Actual native dev switching did not reload renderer scopes.");
            File.WriteAllText(Path.Combine(root,"report.json"),json.Serialize(new {Status="PASS",Checks=checks,Phases=phases,Navigations=navigations,NativeRequests=requests,TemporaryDirectories=directories,OwnerStartupShown=owner.Visible,FixtureRoot=realRoot,UiProbe=await Evaluate("window.devProbe"),Assembly=assembly.ManifestModule.ModuleVersionId}));
            Console.WriteLine("PASS "+checks+" · real WebView2/native dev bridge · "+String.Join(", ",phases));Environment.ExitCode=0;
        }
        catch(Exception error)
        {
            Environment.ExitCode=1;Console.Error.WriteLine(error.ToString());
            try {File.WriteAllText(Path.Combine(root,"failure.json"),json.Serialize(new {Error=error.ToString(),Checks=checks,Phases=phases,Requests=requests,Directories=directories,OwnerShown=owner!=null&&owner.Visible,NativeExceptions=nativeExceptions}));}catch { }
        }
        finally
        {
            try {if(owner!=null)Call(owner,"HyTcgCloseDev");}catch { }
            try {if(panel!=null)Call(panel,"CloseForLauncherExit");}catch { }
            try {if(owner!=null){((NotifyIcon)Value(owner,"trayIcon")).Dispose();owner.Dispose();}}catch { }
            Close();
        }
    }
    [STAThread] public static int Main()
    {Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new HyTcgDevWebViewSmoke());return Environment.ExitCode;}
}
