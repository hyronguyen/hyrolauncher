using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HYROLauncherWeb
{
    // The disposable copy is never mounted as the real account. No dev file is
    // imported back, even after an interrupted request or a launcher restart.
    internal sealed class HyTcgDevSession : IDisposable
    {
        internal const int DurationSeconds=600;
        private readonly Func<TimeSpan> elapsed;
        private readonly HyTcgEconomy devEconomy;
        private volatile bool closed;
        internal readonly HyTcgService RealService,Service;
        internal readonly string DirectoryPath;
        internal HyTcgDevSession(HyTcgService real,string configDirectory,string sandboxParent,Func<TimeSpan> clock)
        {
            RealService=real;
            string parent=Path.GetFullPath(sandboxParent);
            if(Directory.Exists(parent) && (File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked dev directory");
            Directory.CreateDirectory(parent);
            DirectoryPath=Path.Combine(parent,Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                // The clone requires an empty directory. Record ownership after
                // cloning; startup only purges sessions whose process has ended.
                var economy=real.Economy.CreateDevSandbox(DirectoryPath);devEconomy=economy;
                using(var process=Process.GetCurrentProcess())
                    File.WriteAllText(Path.Combine(DirectoryPath,".dev-owner"),process.Id+"\n"+process.StartTime.ToUniversalTime().Ticks);
                var timer=Stopwatch.StartNew();elapsed=clock??delegate{return timer.Elapsed;};
                economy.DevSessionAlive=delegate{return !Expired;};
                Service=new HyTcgService(DirectoryPath,new HyTcgTransport(),economy);
                Service.UseReadOnlyConfig(configDirectory);
            }
            catch { DeleteDirectory(DirectoryPath);throw; }
        }
        internal bool Expired {get{return closed || elapsed().TotalSeconds>=DurationSeconds;}}
        internal int RemainingSeconds {get{return Expired?0:(int)Math.Ceiling(DurationSeconds-Math.Max(0,elapsed().TotalSeconds));}}
        internal void Close() {closed=true;if(devEconomy!=null)devEconomy.CloseDevMode();}
        public void Dispose() {Close();DeleteDirectory(DirectoryPath);}
        internal static void CleanupStale(string sandboxParent)
        {
            string parent=Path.GetFullPath(sandboxParent);
            if(!Directory.Exists(parent) || (File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)return;
            foreach(string child in Directory.GetDirectories(parent))
            {
                if(!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(child),"^[a-f0-9]{32}$"))continue;
                try
                {
                    if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)continue;
                    string owner=Path.Combine(child,".dev-owner");
                    if(!File.Exists(owner)) {if(Directory.GetCreationTimeUtc(child)>DateTime.UtcNow.AddDays(-1))continue;}
                    else
                    {
                        if((File.GetAttributes(owner)&FileAttributes.ReparsePoint)!=0 || new FileInfo(owner).Length>100)continue;
                        var parts=File.ReadAllLines(owner);int pid;long started;
                        if(parts.Length!=2 || !Int32.TryParse(parts[0],out pid) || !Int64.TryParse(parts[1],out started))continue;
                        bool running=false;
                        try {using(var process=Process.GetProcessById(pid))running=process.StartTime.ToUniversalTime().Ticks==started;}
                        catch(ArgumentException) {} catch {running=true;}
                        if(running)continue;
                    }
                    DeleteDirectory(child);
                }
                catch { }
            }
        }
        private static void DeleteDirectory(string directory)
        {
            // Only this session's random child is eligible. Never traverse links.
            string full=Path.GetFullPath(directory);
            string parent=Path.GetDirectoryName(full);
            if(parent==null || !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full),"^[a-f0-9]{32}$"))return;
            if(!Directory.Exists(full))return;
            DeleteTree(full,full+Path.DirectorySeparatorChar);
        }
        private static void DeleteTree(string directory,string boundary)
        {
            string full=Path.GetFullPath(directory);
            if(full+Path.DirectorySeparatorChar!=boundary && !full.StartsWith(boundary,StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid dev cleanup path");
            if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0) {Directory.Delete(full,false);return;}
            foreach(string file in System.IO.Directory.GetFiles(full))File.Delete(file);
            foreach(string child in System.IO.Directory.GetDirectories(full))DeleteTree(child,boundary);
            System.IO.Directory.Delete(full,false);
        }
    }

    internal partial class WebLauncherForm
    {
        private HyTcgDevSession hyTcgDev;
        private bool hyTcgDevTransition,hyTcgDevArmed;
        private int hyTcgDevGeneration;
        private readonly System.Windows.Forms.Timer hyTcgDevTimer=new System.Windows.Forms.Timer();
        private readonly object hyTcgOperationGate=new object();
        private readonly Dictionary<HyTcgService,int> hyTcgOperations=new Dictionary<HyTcgService,int>();
        private sealed class TcgOperation : IDisposable
        {
            private Action finish;
            internal TcgOperation(Action callback) {finish=callback;}
            public void Dispose() {var callback=Interlocked.Exchange(ref finish,null);if(callback!=null)callback();}
        }
        private IDisposable HyTcgOperation(HyTcgService service)
        {
            lock(hyTcgOperationGate) {int count;hyTcgOperations.TryGetValue(service,out count);hyTcgOperations[service]=count+1;}
            return new TcgOperation(delegate {lock(hyTcgOperationGate) {int count=hyTcgOperations[service]-1;if(count==0)hyTcgOperations.Remove(service);else hyTcgOperations[service]=count;}});
        }
        private async Task HyTcgDrain(HyTcgService service)
        {
            while(true) {lock(hyTcgOperationGate) {if(!hyTcgOperations.ContainsKey(service))return;}await Task.Delay(50).ConfigureAwait(false);}
        }
        private object HyTcgDevState()
        {return new {active=hyTcgDev!=null,remainingSeconds=hyTcgDev==null?0:hyTcgDev.RemainingSeconds,durationSeconds=HyTcgDevSession.DurationSeconds};}
        private void HyTcgDevShortcut()
        {
            if(hyTcgDevTransition)return;
            if(HyTcgDocument(pluginWindow) && hyTcgSession!=null && !hyTcgSession.IsCancellationRequested)
                HyTcgToggleDev(pluginWindow,hyTcgSessionId);
            else {hyTcgDevArmed=true;ShowBuiltInPlugin(BuiltInHyTcgPluginId);}
        }
        private void HyTcgClearRuntime()
        {
            hyTcgPending.Clear();hyTcgRequests.Clear();hyTcgSaved.Clear();hyTcgSaving.Clear();hyTcgCardTokens.Clear();hyTcgReservations.Clear();
            hyTcgDiscoveryRequests.Clear();hyTcgLifestyleRequests.Clear();tcgMissionCache=null;
            tcgMovieUncommittedMs=0;tcgMovieLastMs=tcgMovieClock.ElapsedMilliseconds;tcgMovieReady=false;
        }
        private void HyTcgPostRuntime(PluginBrowserWindow window,string previousSession,string reason,string error)
        {
            if(!HyTcgDocument(window))return;
            var page=EnsureTcgMissions();var config=hyTcg.LoadConfig();
            window.Post(new Dictionary<string,object>{{"type","hytcgDevState"},{"session",previousSession},{"nextSession",hyTcgSessionId},
                {"dev",HyTcgDevState()},{"reason",reason},{"config",HyTcgUiConfig(config)},{"wallet",hyTcg.Economy.Snapshot()},
                {"missions",page.Items},{"missionDay",page.Day},{"recoveries",hyTcg.Economy.PendingCards()},{"upgrades",hyTcg.Economy.PendingUpgrades()},
                {"cardsDirectory",hyTcg.CardsDirectory},{"error",error}});
        }
        private async void HyTcgToggleDev(PluginBrowserWindow window,string previousSession)
        {
            if(hyTcgDevTransition || !HyTcgDocument(window))return;
            if(hyTcgDev!=null) {HyTcgExitDev(true,"exit");return;}
            hyTcgDevTransition=true;int generation=++hyTcgDevGeneration;var real=hyTcg;
            window.Post(new Dictionary<string,object>{{"type","hytcgDevTransition"},{"session",previousSession}});
            tcgMovieTimer.Stop();HyTcgEndSession();hyTcgSessionId="";
            HyTcgDevSession created=null;string error="";
            try
            {
                await HyTcgDrain(real);
                if(generation!=hyTcgDevGeneration || !HyTcgDocument(window))return;
                string parent=Path.Combine(Path.GetTempPath(),"HYRO-HYTCG-Dev");
                created=await Task.Run(()=>new HyTcgDevSession(real,AppDomain.CurrentDomain.BaseDirectory,parent,null));
                if(generation!=hyTcgDevGeneration || !HyTcgDocument(window)) {created.Dispose();created=null;return;}
                hyTcgDev=created;hyTcg=created.Service;created=null;
                hyTcgDevTimer.Interval=1000;hyTcgDevTimer.Tick-=HyTcgDevTick;hyTcgDevTimer.Tick+=HyTcgDevTick;hyTcgDevTimer.Start();
            }
            catch(Exception ex) {error=HyTcgError(ex);hyTcg=real;if(created!=null)created.Dispose();}
            finally
            {
                if(generation==hyTcgDevGeneration)
                {
                    HyTcgClearRuntime();hyTcgSession=new CancellationTokenSource();hyTcgSessionId=Guid.NewGuid().ToString("N");hyTcgDevTransition=false;
                    try {HyTcgPostRuntime(window,previousSession,"enter",error);TcgStartMovieMonitor();}catch { }
                }
            }
        }
        private void HyTcgDevTick(object sender,EventArgs args)
        {if(hyTcgDev!=null && hyTcgDev.Expired)HyTcgExitDev(true,"expired");}
        private bool HyTcgCheckDevExpiry()
        {if(hyTcgDev==null || !hyTcgDev.Expired)return false;HyTcgExitDev(true,"expired");return true;}
        private void HyTcgExitDev(bool notify,string reason)
        {
            ++hyTcgDevGeneration;hyTcgDevArmed=false;hyTcgDevTransition=false;
            var dev=hyTcgDev;if(dev==null)return;
            string previous=hyTcgSessionId;hyTcgDevTimer.Stop();tcgMovieTimer.Stop();
            if(hyTcgSession!=null)hyTcgSession.Cancel();dev.Close();
            hyTcgDev=null;hyTcg=dev.RealService;HyTcgClearRuntime();
            hyTcgSession=new CancellationTokenSource();hyTcgSessionId=Guid.NewGuid().ToString("N");
            if(notify)try {HyTcgPostRuntime(pluginWindow,previous,reason,"");TcgStartMovieMonitor();}catch { }
            HyTcgRemoveDev(dev);
        }
        private async void HyTcgRemoveDev(HyTcgDevSession dev)
        {
            try {await HyTcgDrain(dev.Service).ConfigureAwait(false);await Task.Run(()=>dev.Dispose()).ConfigureAwait(false);}catch { }
        }
        private void HyTcgCloseDev()
        {HyTcgExitDev(HyTcgDocument(pluginWindow),"exit");hyTcgDevTimer.Stop();}
    }
}
