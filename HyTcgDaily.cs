using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace HYROLauncherWeb
{
    public sealed class HyTcgMission
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Kind { get; set; }
        public string GameId { get; set; }
        public int Progress { get; set; }
        public int Target { get; set; }
        public int Reward { get; set; }
        public bool Complete { get; set; }
    }
    public sealed class HyTcgMissionPage
    {
        public string Day { get; set; }
        public List<HyTcgMission> Items { get; set; }
        public bool Complete { get; set; }
    }
    internal sealed partial class HyTcgEconomy
    {
        internal static string DailyKey(DateTime now) { return now.ToUniversalTime().AddHours(7).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture); }
        internal string BannerForToken(string token)
        { return Read(delegate(Vault v,DateTime now) { var p=FindPending(v,token);return p.BannerId??""; }); }
        internal HyTcgWallet BuyLimitedCredits(int count)
        {
            if(count!=1 && count!=10)throw new HyTcgException("PackInvalid");
            return Write(delegate(Vault v,DateTime now) {
                int cost=HyTcgRules.LimitedPackPrice*count;
                if(v.Gems<cost)throw new HyTcgException("GemsInsufficient");
                if(v.LimitedCredits>MaxPaid-count)throw new HyTcgException("CreditsLimit");
                v.Gems-=cost;v.LimitedCredits+=count;return Wallet(v,now);
            });
        }
        internal HyTcgMissionPage EnsureMissions(List<HyTcgMission> eligible)
        {
            return Write(delegate(Vault v,DateTime now) {
                EnsureActivityMissions(v,now);
                // Steam can arrive after launcher startup. Fill missing slots without replacing
                // the day's existing selections or allowing claimed tasks to be rerolled.
                int selected=v.Missions.Count(m=>m.Kind=="game");
                if(selected<3)
                {
                    var choices=(eligible??new List<HyTcgMission>()).Where(m=>m!=null && m.Kind=="game" && ValidGameId(m.GameId) && !String.IsNullOrWhiteSpace(m.Title) && !v.Missions.Any(old=>old.GameId==m.GameId))
                        .GroupBy(m=>m.GameId,StringComparer.Ordinal).Select(g=>g.First()).ToList();
                    using(var rng=RandomNumberGenerator.Create())while(selected<3 && choices.Count>0)
                    {
                        int index=RandomIndex(rng,choices.Count);var game=choices[index];choices.RemoveAt(index);
                        v.Missions.Insert(selected++,new HyTcgMission {Id="game:"+game.GameId,GameId=game.GameId,Title=game.Title.Length>180?game.Title.Substring(0,180):game.Title,Kind="game",Target=1,Reward=60});
                    }
                }
                return MissionPage(v);
            });
        }
        internal HyTcgMissionPage Missions()
        { return Read(delegate(Vault v,DateTime now) { return v.MissionDay==DailyKey(now)?MissionPage(v):new HyTcgMissionPage {Day=DailyKey(now),Items=new List<HyTcgMission>(),Complete=false}; }); }
        internal int AwardGameMission(string gameId)
        {
            return Write(delegate(Vault v,DateTime now) {
                if(v.MissionDay!=DailyKey(now) || v.Missions==null)return 0;
                var item=v.Missions.FirstOrDefault(m=>m.Kind=="game" && m.GameId==gameId);
                if(item==null || item.Complete)return 0;
                AwardMission(v,item);item.Progress=1;return item.Reward;
            });
        }
        internal int AdvanceMovieMission(int seconds)
        {
            if(seconds<1 || seconds>30)throw new HyTcgException("MissionInvalid");
            return Write(delegate(Vault v,DateTime now) {
                if(v.MissionDay!=DailyKey(now) || v.Missions==null)return 0;
                var item=v.Missions.FirstOrDefault(m=>m.Kind=="movie");
                if(item==null || item.Complete)return 0;
                item.Progress=Math.Min(item.Target,item.Progress+seconds);
                if(item.Progress<item.Target)return 0;
                AwardMission(v,item);return item.Reward;
            });
        }
        private static void AwardMission(Vault v,HyTcgMission item)
        { if((long)v.Gems+RefundableGemLiability(v)>MaxGems-item.Reward)throw new HyTcgException("GemsLimit");v.Gems+=item.Reward;item.Complete=true; }
        private static void EnsureActivityMissions(Vault v,DateTime now)
        {
            string day=DailyKey(now);
            if(v.MissionDay!=day || v.Missions==null)
            {v.MissionDay=day;v.Missions=new List<HyTcgMission> {new HyTcgMission {Id="movie20",Title="Movie night",Kind="movie",Target=1200,Reward=30}};}
            foreach(string kind in new[]{"expedition","arena","gift"})if(!v.Missions.Any(m=>m.Kind==kind))
                v.Missions.Add(new HyTcgMission {Id=kind+"1",Kind=kind,Title=kind=="expedition"?"Complete an expedition":kind=="arena"?"Win an arena battle":"Give a companion a gift",Target=1,Reward=30});
        }
        private static int AwardActivityMission(Vault v,DateTime now,string kind)
        {
            EnsureActivityMissions(v,now);var item=v.Missions.FirstOrDefault(m=>m.Kind==kind);
            if(item==null || item.Complete)return 0;AwardMission(v,item);item.Progress=1;return item.Reward;
        }
        private static HyTcgMissionPage MissionPage(Vault v)
        {
            var items=Parser().Deserialize<List<HyTcgMission>>(Parser().Serialize(v.Missions??new List<HyTcgMission>()));
            return new HyTcgMissionPage {Day=v.MissionDay??"",Items=items,Complete=items.Count>0 && items.All(m=>m.Complete)};
        }
        private static int RandomIndex(RandomNumberGenerator rng,int count)
        {
            byte[] bytes=new byte[4];uint value,ceiling=UInt32.MaxValue-UInt32.MaxValue%(uint)count;
            do {rng.GetBytes(bytes);value=BitConverter.ToUInt32(bytes,0);}while(value>=ceiling);
            return (int)(value%(uint)count);
        }
        private static bool ValidGameId(string id)
        { return Regex.IsMatch(id??"",@"^[A-Za-z0-9_-]{1,100}$"); }
        private static void ValidateMissions(Vault v)
        {
            if(v.Missions==null) {if(!String.IsNullOrEmpty(v.MissionDay))throw new HyTcgException("VaultInvalid");return;}
            DateTime date;
            if(!DateTime.TryParseExact(v.MissionDay,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date) || v.Missions.Count>7 || v.Missions.Count(m=>m.Kind=="game")>3)throw new HyTcgException("VaultInvalid");
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var m in v.Missions)
            {
                if(m==null || !seen.Add(m.Id??"") || String.IsNullOrWhiteSpace(m.Title) || m.Title.Length>180 || m.Progress<0 || m.Progress>m.Target || m.Complete!=(m.Progress==m.Target)
                    || (m.Kind=="game"?(m.Id!="game:"+m.GameId || !ValidGameId(m.GameId) || m.Target!=1 || m.Reward!=60):
                    m.Kind=="movie"?(m.Id!="movie20" || m.Target!=1200 || m.Reward!=30):
                    (!new[]{"expedition","arena","gift"}.Contains(m.Kind) || m.Id!=m.Kind+"1" || m.Target!=1 || m.Reward!=30)))throw new HyTcgException("VaultInvalid");
            }
            if(v.Missions.Count(m=>m.Kind=="movie")!=1)throw new HyTcgException("VaultInvalid");
        }
        internal HyTcgWallet ResetAccount(string confirmation)
        {
            if(confirmation!="delete")throw new HyTcgException("ConfirmationRequired");
            lock(gate)
            {
                if(active.Count>0)throw new HyTcgException("Busy");
                var wallet=Write(delegate(Vault v,DateTime now) {
                    var files=v.Cards.Select(c=>c.FileName).Concat(v.Upgrades.SelectMany(u=>new[]{u.OriginalFileName,u.CompletedFileName})).Where(f=>!String.IsNullOrEmpty(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    string directory=Path.Combine(root,"cards");
                    if(Directory.Exists(directory))
                    {
                        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("CardInvalid");
                        foreach(string candidate in Directory.GetFiles(directory))
                        {
                            string extension=Path.GetExtension(candidate);
                            if((extension.Equals(".png",StringComparison.OrdinalIgnoreCase) || extension.Equals(".hycard",StringComparison.OrdinalIgnoreCase)) && (File.GetAttributes(candidate)&FileAttributes.ReparsePoint)==0)
                                files.Add(Path.GetFileName(candidate));
                        }
                        files=files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    }
                    foreach(string file in files)SafeCardPath(file);
                    v.ResetFiles=(v.ResetFiles??new List<string>()).Concat(files).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    ResetCreative(v);ResetGameplay(v);
                    v.Cards.Clear();v.Pending.Clear();v.Upgrades.Clear();v.Compensations=null;v.FreeCredits=HyTcgRules.MaxFreeCredits;v.PaidCredits=0;v.LimitedCredits=0;v.Gems=1800;
                    v.PitySR=0;v.PitySSR=0;v.PityUR=0;v.NextFreeTicks=0;v.MissionDay=null;v.Missions=null;v.MigrationComplete=true;
                    return Wallet(v,now);
                });
                FinishResetDeletes();ClearImageCaches();return wallet;
            }
        }
        internal void FinishResetDeletes()
        {
            Write(delegate(Vault v,DateTime now) {
                if(v.ResetFiles==null)return true;
                var remaining=new List<string>();
                foreach(string file in v.ResetFiles)
                {
                    try {string target=SafeCardPath(file);if(File.Exists(target))File.Delete(target);}
                    catch {remaining.Add(file);}
                }
                v.ResetFiles=remaining;return true;
            });
            FinishCreativeResetDeletes();
        }
    }
}
