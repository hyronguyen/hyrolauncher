using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

// Compile the production HyTcgActions.cs unchanged against a tiny host. Only
// document/post helpers are supplied here; HyTcgOpen, its catch/finally, native
// service and economy all come directly from the real launcher sources.
namespace HYROLauncherWeb
{
    internal sealed class PluginBrowserWindow : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle {get{return IntPtr.Zero;}}
        private readonly List<Dictionary<string,object>> replies=new List<Dictionary<string,object>>();
        internal bool IsDisposed {get{return false;}}
        internal bool IsLocalPanel(string file,string id){return file=="hytcg.html" && id=="builtin.hytcg";}
        internal void ToggleWidePanel(){}
        internal void Post(Dictionary<string,object> value){lock(replies)replies.Add(value);}
        internal Dictionary<string,object> WaitFor(string request,string type="hytcgCard")
        {
            for(int index=0;index<1000;index++)
            {
                lock(replies)
                {
                    var response=replies.FirstOrDefault(value=>Convert.ToString(value["type"])==type && Convert.ToString(value["requestId"])==request);
                    if(response!=null)return response;
                }
                Thread.Sleep(10);
            }
            throw new Exception("Actual bridge did not finish request "+request);
        }
    }
    internal partial class WebLauncherForm
    {
        private const string BuiltInHyTcgPluginId="builtin.hytcg";
        private string currentBuiltInPluginId=BuiltInHyTcgPluginId;
        private PluginBrowserWindow pluginWindow=new PluginBrowserWindow();
        private readonly HashSet<string> hyTcgDiscoveryRequests=new HashSet<string>();
        private bool hyTcgDevTransition,hyTcgDevArmed;
        private object HyTcgDevState(){return new {active=false};}
        private bool HyTcgCheckDevExpiry(){return false;}
        private void HyTcgToggleDev(PluginBrowserWindow window,string session){}
        private IDisposable HyTcgOperation(HyTcgService service){return new NormalFixtureOperation();}
        private sealed class NormalFixtureOperation : IDisposable {public void Dispose(){}}
        internal bool IsDisposed {get{return false;}}
        internal bool IsHandleCreated {get{return true;}}
        private object BeginInvoke(Action action){action();return null;}
        private static string GetString(Dictionary<string,object> value,string key){object raw;return value.TryGetValue(key,out raw)?Convert.ToString(raw):"";}
        private static bool GetBool(Dictionary<string,object> value,string key){bool result;return Boolean.TryParse(GetString(value,key),out result)&&result;}
        private static int ClampInt(string value,int fallback,int min,int max){int result;return Int32.TryParse(value,out result)?Math.Max(min,Math.Min(max,result)):fallback;}
        private bool HandleHyTcgExtra(PluginBrowserWindow window,string session,string type,Dictionary<string,object> message){return false;}
        private HyTcgMissionPage EnsureTcgMissions(){return new HyTcgMissionPage{Day="2026-10-10",Items=new List<HyTcgMission>()};}
        internal WebLauncherForm(HyTcgService service){hyTcg=service;hyTcgSessionId="fixture-session";hyTcgSession=new CancellationTokenSource();}
        internal Dictionary<string,object> TestOpen(string token,string request)
        {hyTcgRequests.Add(request);HyTcgOpen(pluginWindow,hyTcgSessionId,request,token,hyTcgSession.Token);return pluginWindow.WaitFor(request);}
        internal int PendingCount {get{return hyTcgPending.Count;}}
        internal int NetworkSlots {get{return hyTcgNetwork.CurrentCount;}}
        internal int TrackedRequests {get{return hyTcgRequests.Count;}}
        internal void StartWallet(string type,string request,List<string> ids)
        {var payload=new Dictionary<string,object>{{"cardIds",ids.ToArray()},{"cardId",ids[0]}};HyTcgWalletAction(pluginWindow,hyTcgSessionId,request,type,payload);}
        internal Dictionary<string,object> WaitWallet(string type,string request)
        {return pluginWindow.WaitFor(request,type=="hytcgDissolve"?"hytcgDissolved":"hytcgDissolvedMany");}
        internal void SaveCompensated(string id){HyTcgSave(pluginWindow,hyTcgSessionId,id,"data:image/png;base64,bm90YWNhcmQ=");}
        internal void Cancel(){hyTcgSession.Cancel();}
    }
}
internal sealed class OpenBridgeFixture : IHyTcgTransport
{
    internal string Mode;
    internal int CharacterCalls,ImageCalls;
    internal ManualResetEvent Started=new ManualResetEvent(false);
    public string Json(string url,string userAgent,int timeout,CancellationToken token)
    {
        Interlocked.Increment(ref CharacterCalls);Started.Set();token.ThrowIfCancellationRequested();
        if(Mode=="cancel"){token.WaitHandle.WaitOne(5000);token.ThrowIfCancellationRequested();throw new Exception("Cancellation not delivered");}
        if(Mode=="network")throw new WebException("fixture connection failure",WebExceptionStatus.ConnectFailure);
        if(Mode=="rate")throw new HyTcgHttpException(429);
        if(Mode=="forbidden")throw new HyTcgHttpException(403);
        if(Mode=="404")throw new HyTcgHttpException(404);
        if(Mode=="browser")return "<!DOCTYPE html><html><title>Checking browser...</title><body>Make sure cookies are enabled in your browser.</body></html>";
        if(Mode=="profile")return "{\"id\":55,\"name\":\"Character 55\",\"origin\":\"Series 55\",\"character_image\":\"https://ami.animecharactersdatabase.com/55.jpg\"}";
        return "-1";
    }
    public string Image(string url,string userAgent,int timeout,CancellationToken token){Interlocked.Increment(ref ImageCalls);throw new IOException("fixture image unavailable");}
}
internal sealed class WalletCharacterFixture : IHyTcgCharacterTransport
{
    internal bool Block;
    internal int Calls;
    internal ManualResetEvent Started=new ManualResetEvent(false);
    public string Search(string name,CancellationToken token)
    {
        Interlocked.Increment(ref Calls);Started.Set();
        if(Block){token.WaitHandle.WaitOne(5000);token.ThrowIfCancellationRequested();throw new Exception("Warm-up cancellation not delivered");}
        return new JavaScriptSerializer().Serialize(new {data=new {Page=new {characters=new[]{new {id=1555,name=new {full="Hinata Hyuuga",native="",alternative=new string[0]},favourites=12345,description="Trusted native fixture",media=new {nodes=new[]{new {id=20,title=new {english="Naruto",romaji="Naruto",native=""},synonyms=new string[0]}}}}}}}});
    }
    public string Detail(int id,CancellationToken token){throw new Exception("Full native search result should not need a second query");}
}
internal static class VerifyOpenBridge
{
    private static int checks;
    private static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;}
    private static HyTcgService Service(string root,OpenBridgeFixture fixture)
    {
        var service=new HyTcgService(root,fixture);
        service.SaveConfig(new HyTcgConfig{CharacterIdMin=55,CharacterIdMax=55,MaxCharacterAttempts=2,Concurrency=2,RequestTimeoutSeconds=3,RetryDelayMs=100,ZerochanUsername=""});
        return service;
    }
    private static List<string> OwnedCards(HyTcgService service,int count)
    {
        string png;
        using(var bitmap=new Bitmap(750,1050))using(var output=new MemoryStream())
        {using(var graphics=Graphics.FromImage(bitmap))graphics.Clear(Color.MidnightBlue);bitmap.Save(output,ImageFormat.Png);png="data:image/png;base64,"+Convert.ToBase64String(output.ToArray());}
        var ids=new List<string>();
        for(int index=0;index<count;index++)
        {
            var card=new HyTcgCard{CharacterId=1647,Name="Hinata Hyuuga",Series="Naruto",Gender="Female",Rarity="Common",RarityColor=HyTcgRules.RarityColor("Common"),CardId="HT-20261010-"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant(),CreatedAt=DateTime.UtcNow.ToString("o"),SourceImage="https://ami.animecharactersdatabase.com/fixture.jpg",ProfileImage="https://ami.animecharactersdatabase.com/fixture.jpg",ImageSource="acdb",Orientation="portrait"};
            var pull=service.Economy.Reserve(1);service.Economy.Begin(pull.Tokens[0]);service.Economy.AuthorizeCard(pull.Tokens[0],card);service.SaveCard(card,png,pull.Tokens[0]);ids.Add(card.CardId);
        }
        return ids;
    }
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
            foreach(string payment in new[]{"credits","gems"})
            {
                var fixture=new OpenBridgeFixture{Mode="missing"};var service=Service(Path.Combine(root,"missing-"+payment),fixture);var host=new WebLauncherForm(service);
                var reservation=service.Economy.Reserve(1,payment,"");var response=host.TestOpen(reservation.Tokens[0],"miss-"+payment);
                Check(response.ContainsKey("compensated") && (bool)response["compensated"] && (int)response["gemsAwarded"]==160 && Convert.ToString(response["error"])=="","Actual bridge catch returns +160 card outcome for "+payment);
                Check(!response.ContainsKey("card") && !response.ContainsKey("imageData") && host.PendingCount==0 && service.Economy.PendingCards().Count==0,"No fabricated character or recoverable card on miss");
                var wallet=service.Economy.Snapshot();Check(wallet.Gems==(payment=="gems"?1800:1960) && wallet.FreeCredits==(payment=="credits"?4:5),"Miss consumes payment rather than refunding plus gems");
                Check(fixture.CharacterCalls==2 && fixture.ImageCalls==0,"All configured attempts exhausted and no image fetch");
                host.SaveCompensated("HT-20261010-000000000000");Check(!Directory.GetFiles(service.CardsDirectory,"*.hycard").Any(),"Compensated outcome cannot save card");
                int calls=fixture.CharacterCalls;var repeated=host.TestOpen(reservation.Tokens[0],"replay-"+payment);
                Check((bool)repeated["compensated"] && service.Economy.Snapshot().Gems==wallet.Gems && fixture.CharacterCalls==calls,"Actual bridge replay returns receipt without reroll or second award");
                Check(host.NetworkSlots==3,"Actual bridge restores concurrency gate");
            }
            foreach(string mode in new[]{"network","rate","forbidden","browser","profile"})
            {
                var fixture=new OpenBridgeFixture{Mode=mode};var service=Service(Path.Combine(root,mode),fixture);var host=new WebLauncherForm(service);
                var reservation=service.Economy.Reserve(1,"gems","");var response=host.TestOpen(reservation.Tokens[0],mode);
                Check(!response.ContainsKey("compensated") && Convert.ToString(response["error"])!="","Transport/image failure is an error rather than 160-gem miss: "+mode);
                Check(service.Economy.Snapshot().Gems==1800 && service.Economy.Snapshot().FreeCredits==5,"Transport/image failure refunds exact payment: "+mode);
                Check(service.Economy.CharacterMissCompensation(reservation.Tokens[0])==null && host.PendingCount==0 && !response.ContainsKey("card"),"Transport failure has no miss receipt or card: "+mode);
                Check(host.NetworkSlots==3,"Failure releases native concurrency gate: "+mode);
            }
            {
                var fixture=new OpenBridgeFixture{Mode="404"};var service=Service(Path.Combine(root,"404"),fixture);var host=new WebLauncherForm(service);
                var reservation=service.Economy.Reserve(1);var response=host.TestOpen(reservation.Tokens[0],"not-found-404");
                Check(response.ContainsKey("compensated") && fixture.CharacterCalls==2,"Native HTTP404 missing IDs qualify only after full bounded attempts");
            }
            {
                var fixture=new OpenBridgeFixture{Mode="cancel"};var service=Service(Path.Combine(root,"cancel"),fixture);var host=new WebLauncherForm(service);var reservation=service.Economy.Reserve(1,"gems","");
                Dictionary<string,object> response=null;var thread=new Thread(()=>response=host.TestOpen(reservation.Tokens[0],"cancel"));thread.Start();Check(fixture.Started.WaitOne(3000),"Cancellation test reaches actual fetch");host.Cancel();Check(thread.Join(5000),"Canceled bridge completes");
                Check(!response.ContainsKey("compensated") && service.Economy.Snapshot().Gems==1800 && service.Economy.CharacterMissCompensation(reservation.Tokens[0])==null,"Session cancellation refunds and never compensates");
            }
            {
                var service=Service(Path.Combine(root,"wallet-cancel"),new OpenBridgeFixture());var warm=new WalletCharacterFixture{Block=true};service.CharacterTransport=warm;var ids=OwnedCards(service,1);var host=new WebLauncherForm(service);
                host.StartWallet("hytcgDissolve","warm-cancel",ids);Check(warm.Started.WaitOne(3000),"Actual dissolve bridge reaches native metadata warm-up");
                Check(host.TrackedRequests==1,"Actual dissolve remains tracked during awaited metadata call, blocking account reset");
                host.Cancel();var response=host.WaitWallet("hytcgDissolve","warm-cancel");
                Check(Convert.ToString(response["error"])=="TimedOut" && service.Economy.Snapshot().Gems==1800,"Canceled original session does not award dissolution gems");
                Check(service.Economy.ReadOwnedCards(ids).Count==1 && service.Economy.CollectionCards().Count==1,"Canceled original session retains owned card and file");
                Check(host.TrackedRequests==0,"Canceled dissolve clears tracked request in finally");
            }
            {
                var service=Service(Path.Combine(root,"wallet-bonus"),new OpenBridgeFixture());var warm=new WalletCharacterFixture();service.CharacterTransport=warm;var ids=OwnedCards(service,3);var host=new WebLauncherForm(service);
                host.StartWallet("hytcgDissolve","single-bonus",new List<string>{ids[0]});var single=host.WaitWallet("hytcgDissolve","single-bonus");
                Check(Convert.ToString(single["error"])=="" && (int)single["gemsAwarded"]==124 && service.Economy.Snapshot().Gems==1924,"Actual single dissolve warms favourites before atomic bonus payout");
                Check(warm.Calls==1 && host.TrackedRequests==0,"Single warm-up cached and tracked request released");
                host.StartWallet("hytcgDissolveMany","batch-bonus",ids.Skip(1).ToList());var batch=host.WaitWallet("hytcgDissolveMany","batch-bonus");
                Check(Convert.ToString(batch["error"])=="" && (int)batch["gemsAwarded"]==248 && service.Economy.Snapshot().Gems==2172,"Actual batch dissolve uses same cached character bonus for both cards");
                Check(warm.Calls==1 && host.TrackedRequests==0 && service.Economy.CollectionCards().Count==0,"Cached batch makes no second API call and clears request and ownership");
            }
            Console.WriteLine("HYTCG actual open bridge PASS · "+checks+" checks");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
