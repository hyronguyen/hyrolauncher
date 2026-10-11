using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HYROLauncherWeb
{
    internal partial class WebLauncherForm
    {
        private readonly System.Windows.Forms.Timer tcgDayTimer=new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer tcgMovieTimer=new System.Windows.Forms.Timer();
        private readonly Stopwatch tcgMovieClock=Stopwatch.StartNew();
        private string tcgMissionDay="";
        private long tcgMovieLastMs;
        private int tcgMovieUncommittedMs;
        private bool tcgMovieReady;
        private HyTcgMissionPage tcgMissionCache;
        private HyTcgMissionPage EnsureTcgMissions()
        {
            if(tcgMissionCache!=null && tcgMissionCache.Day==HyTcgEconomy.DailyKey(DateTime.UtcNow))return tcgMissionCache;
            if(hyTcg==null)hyTcg=new HyTcgService(AppDomain.CurrentDomain.BaseDirectory);
            var eligible=new List<HyTcgMission>();
            if(state!=null && state.Games!=null)
                foreach(var game in state.Games.Where(g=>!g.Private && !g.IsUrl && String.Equals(g.Category,"Games",StringComparison.OrdinalIgnoreCase)))
                    eligible.Add(new HyTcgMission {GameId=game.Id,Title=game.Name,Kind="game"});
            var missionSteam=steamLibrary;
            if((missionSteam==null || missionSteam.Games==null || missionSteam.Games.Count==0) && state!=null && state.Settings!=null && !String.IsNullOrEmpty(state.Settings.SteamId))
            {
                try
                {
                    string cache=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"steam-library-cache.json");
                    if(File.Exists(cache) && new FileInfo(cache).Length<8*1024*1024)
                    {
                        var saved=serializer.Deserialize<SteamLibraryCache>(File.ReadAllText(cache));
                        if(saved!=null && saved.SteamId==state.Settings.SteamId && saved.Games!=null)missionSteam=saved;
                    }
                }
                catch { }
            }
            if(missionSteam!=null && missionSteam.Games!=null)
                foreach(var game in missionSteam.Games)eligible.Add(new HyTcgMission {GameId="steam-"+game.AppId,Title=game.Name,Kind="game"});
            var page=hyTcg.Economy.EnsureMissions(eligible);tcgMissionDay=page.Day;tcgMissionCache=page;return page;
        }
        private void InitializeTcgDaily()
        {
            System.Threading.Tasks.Task.Run(()=>HyTcgDevSession.CleanupStale(Path.Combine(Path.GetTempPath(),"HYRO-HYTCG-Dev")));
            try {EnsureTcgMissions();hyTcg.Economy.FinishResetDeletes();}catch { }
            tcgDayTimer.Tick+=delegate {
                tcgDayTimer.Stop();
                if(hyTcgDevTransition){ScheduleTcgDay();return;}
                try {tcgMissionCache=null;EnsureTcgMissions();tcgMovieUncommittedMs=0;TcgStartMovieMonitor();SendTcgMissionUpdate();}catch { }
                ScheduleTcgDay();
            };
            tcgMovieTimer.Interval=1000;tcgMovieTimer.Tick+=delegate {TcgMovieTick();};ScheduleTcgDay();
        }
        private void ScheduleTcgDay()
        {
            DateTime local=DateTime.UtcNow.AddHours(7);double delay=(local.Date.AddDays(1)-local).TotalMilliseconds+150;
            tcgDayTimer.Interval=(int)Math.Max(1000,Math.Min(Int32.MaxValue,delay));tcgDayTimer.Start();
        }
        private void TcgGamePlayed(string gameId)
        {
            if(hyTcgDevTransition)return;
            try
            {
                var page=EnsureTcgMissions();
                if(page.Complete || !page.Items.Any(m=>m.GameId==gameId && !m.Complete))return;
                if(hyTcg.Economy.AwardGameMission(gameId)>0){tcgMissionCache=hyTcg.Economy.Missions();SendTcgMissionUpdate();}
            }
            catch { }
        }
        private void TcgMissionSourcesChanged()
        {
            if(hyTcgDevTransition)return;
            if(tcgMissionCache!=null && tcgMissionCache.Items.Count(m=>m.Kind=="game")==3)return;
            tcgMissionCache=null;
            try {EnsureTcgMissions();SendTcgMissionUpdate();}catch { }
        }
        private void TcgStartMovieMonitor()
        {
            if(hyTcgDevTransition)return;
            try
            {
                var page=EnsureTcgMissions();var mission=page.Items.FirstOrDefault(m=>m.Kind=="movie");
                if(mission==null || mission.Complete || hyrowserWindow==null || hyrowserWindow.IsDisposed || !hyrowserWindow.HasMovieTab)return;
                tcgMovieLastMs=tcgMovieClock.ElapsedMilliseconds;tcgMovieReady=hyrowserWindow.MovieTabReady;tcgMovieTimer.Start();
            }
            catch { }
        }
        private void TcgMovieTick()
        {
            if(hyTcgDevTransition)return;
            bool present=hyrowserWindow!=null && !hyrowserWindow.IsDisposed && hyrowserWindow.HasMovieTab;
            bool ready=present && hyrowserWindow.MovieTabReady;
            long tick=tcgMovieClock.ElapsedMilliseconds,delta=Math.Max(0,Math.Min(2000,tick-tcgMovieLastMs));tcgMovieLastMs=tick;
            if(tcgMovieReady && ready)tcgMovieUncommittedMs+=(int)delta;
            tcgMovieReady=ready;
            if(tcgMovieUncommittedMs>=10000 || !present)
            {
                int seconds=Math.Min(30,tcgMovieUncommittedMs/1000);
                if(seconds>0)
                {
                    try
                    {
                        if(HyTcgEconomy.DailyKey(DateTime.UtcNow)!=tcgMissionDay) {EnsureTcgMissions();tcgMovieUncommittedMs=0;}
                        else {hyTcg.Economy.AdvanceMovieMission(seconds);tcgMovieUncommittedMs-=seconds*1000;tcgMissionCache=hyTcg.Economy.Missions();}
                        SendTcgMissionUpdate();
                    }
                    catch {tcgMovieTimer.Stop();return;}
                }
                try {if(!present || hyTcg.Economy.Missions().Items.Any(m=>m.Kind=="movie" && m.Complete))tcgMovieTimer.Stop();}catch {tcgMovieTimer.Stop();}
            }
        }
        private void SendTcgMissionUpdate()
        {
            if(!HyTcgDocument(pluginWindow))return;
            try {
                var page=hyTcg.Economy.Missions();
                HyTcgReply(pluginWindow,hyTcgSessionId,new Dictionary<string,object>{{"type","hytcgMissionUpdate"},{"missions",page.Items},{"missionDay",page.Day},{"wallet",hyTcg.Economy.Snapshot()}});
            }catch { }
        }
        private void TcgMissionPlay(string gameId)
        {
            var page=EnsureTcgMissions();var mission=page.Items.FirstOrDefault(m=>m.Kind=="game" && m.GameId==gameId && !m.Complete);
            if(mission==null)throw new HyTcgException("MissionUnavailable");
            if(gameId.StartsWith("steam-",StringComparison.Ordinal))
            {
                int appId;if(!Int32.TryParse(gameId.Substring(6),out appId) || steamLibrary==null || !steamLibrary.Games.Any(g=>g.AppId==appId))throw new HyTcgException("MissionUnavailable");
                LaunchSteamGame(appId);
            }
            else {var game=state.Games.Find(g=>g.Id==gameId && !g.Private);if(game==null)throw new HyTcgException("MissionUnavailable");Launch(game.Id);}
        }
        private void LaunchSteamGame(int appId)
        {
            try {Process.Start(new ProcessStartInfo("steam://rungameid/"+appId) {UseShellExecute=true});TcgGamePlayed("steam-"+appId);}
            catch(Exception ex) {Notify("Could not launch:\n"+ex.Message);}
        }
    }
}
