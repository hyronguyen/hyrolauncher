using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HYROLauncherWeb;

internal static class VerifyHyTcgDaily
{
    private static int checks;
    private static void Check(bool ok,string message) {checks++;if(!ok)throw new Exception(message);}
    private static void Error(string code,Action action) {try {action();throw new Exception("Expected "+code);}catch(HyTcgException ex){Check(ex.Code==code,code+" got "+ex.Code);}}
    private static readonly List<HyTcgMission> games=Enumerable.Range(1,20).Select(i=>new HyTcgMission {GameId="g"+i,Title="Game "+i,Kind="game"}).ToList();
    private sealed class Clock
    {
        internal DateTime Now=new DateTime(2026,10,9,16,59,59,DateTimeKind.Utc);
        internal HyTcgEconomy Create(string root) {return new HyTcgEconomy(root,()=>Now,new HyTcgUserProtector(),()=>TimeSpan.Zero);}
    }
    internal static int Main(string[] args)
    {
        try
        {
            var clock=new Clock();string root=Path.GetFullPath(args[0]);var economy=clock.Create(root);
            var late=clock.Create(Path.Combine(root,"late-library"));
            Check(late.EnsureMissions(new List<HyTcgMission>()).Items.Count==4,"startup before library has movie only");
            for(int i=0;i<40;i++)late.AdvanceMovieMission(30);
            var fill=late.EnsureMissions(games.Take(1).ToList());string fixedId=fill.Items.First().GameId;
            late.AwardGameMission(fixedId);fill=late.EnsureMissions(games);
            Check(fill.Items.Count==7 && fill.Items.First().GameId==fixedId && fill.Items.First().Complete && fill.Items.Single(m=>m.Kind=="movie").Complete,"late library fills slots andpreserves claimedtasks");
            Check(late.EnsureMissions(games.Skip(10).ToList()).Items.Select(m=>m.Id).SequenceEqual(fill.Items.Select(m=>m.Id)),"complete selectioncannotreroll afterlibrarychange");
            Check(economy.Snapshot().Gems==1800,"fresh account starts1800");
            var day=economy.EnsureMissions(games);Check(day.Day=="2026-10-09" && day.Items.Count==7,"UTC+7 day andthreegames");
            Check(day.Items.Where(m=>m.Kind=="game").Select(m=>m.GameId).Distinct().Count()==3,"distinctselectedgames");
            var same=economy.EnsureMissions(new List<HyTcgMission>());Check(same.Items.Select(m=>m.Id).SequenceEqual(day.Items.Select(m=>m.Id)),"same dayselectionstable");
            Check(economy.AwardGameMission("unknown")==0 && economy.Snapshot().Gems==1800,"unknownplay cannotaward");
            foreach(var m in day.Items.Where(m=>m.Kind=="game"))
            {Check(economy.AwardGameMission(m.GameId)==60,"game60");Check(economy.AwardGameMission(m.GameId)==0,"gameonce");}
            Error("MissionInvalid",()=>economy.AdvanceMovieMission(31));Error("MissionInvalid",()=>economy.AdvanceMovieMission(0));
            for(int i=0;i<39;i++)Check(economy.AdvanceMovieMission(30)==0,"noearlymovie");
            economy.AdvanceMovieMission(29);Check(economy.Missions().Items.Single(m=>m.Kind=="movie").Progress==1199,"persist1199sec");
            var resumed=clock.Create(root);Check(resumed.Missions().Items.Single(m=>m.Kind=="movie").Progress==1199,"movieprogresssurvivesrestart");
            Check(resumed.AdvanceMovieMission(1)==30 && resumed.Snapshot().Gems==2010,"20minutes30gems");
            Check(!resumed.Missions().Complete,"activity missions remain after game/movie complete");Check(resumed.AdvanceMovieMission(30)==0 && resumed.Snapshot().Gems==2010,"movieonce");
            clock.Now=clock.Now.AddSeconds(1);Check(resumed.Missions().Items.Count==0,"midnightoldmissions notclaimable");
            day=resumed.EnsureMissions(games);Check(day.Day=="2026-10-10" && day.Items.All(m=>m.Progress==0 && !m.Complete),"dailyreset");
            var buy=resumed.BuyLimitedCredits(10);Check(buy.LimitedCredits==10 && buy.Gems==210 && buy.LimitedPackPrice==180,"tenlimitedcost1800");
            var reserved=resumed.Reserve(10,"limited","LB-20261010-2");Check(reserved.Wallet.LimitedCredits==0 && reserved.Wallet.FreeCredits==5,"limitedseparatefromnormal");
            Check(resumed.BannerForToken(reserved.Tokens[0])=="LB-20261010-2","tokenbindsbanner");
            resumed.CancelReservation(reserved.ReservationId);Check(resumed.Snapshot().LimitedCredits==10,"refundlimitedexactly");
            resumed.CancelReservation(reserved.ReservationId);Check(resumed.Snapshot().LimitedCredits==10,"refundonce");
            var direct=resumed.Reserve(1,"limitedGems","LB-20261010-2");Check(direct.Wallet.Gems==30 && direct.Wallet.LimitedCredits==10,"directlimited180");
            resumed.Refund(direct.Tokens[0]);Check(resumed.Snapshot().Gems==210,"directlimitedrefund180");
            Error("GemsInsufficient",()=>resumed.Reserve(10,"limitedGems","LB-20261010-2"));
            Error("BannerUnavailable",()=>resumed.Reserve(1,"limited","../bad"));Error("PackInvalid",()=>resumed.BuyLimitedCredits(2));
            Error("ConfirmationRequired",()=>resumed.ResetAccount("DELETE"));Error("ConfirmationRequired",()=>resumed.ResetAccount(" delete"));
            var pending=resumed.Reserve(1);resumed.Begin(pending.Tokens[0]);Error("Busy",()=>resumed.ResetAccount("delete"));resumed.EndFetch(pending.Tokens[0]);
            var native=new HyTcgCard {CharacterId=99991,Name="Test character",Series="Test series",Gender="Female",Rarity="Common",RarityColor="#b1becd",CardId="HT-20261010-ABCDEF123456",CreatedAt="2026-10-10T00:00:00Z",ImageSource="acdb",SourceImage="https://www.animecharactersdatabase.com/uploads/test.png",ProfileImage="https://www.animecharactersdatabase.com/uploads/test.png"};
            resumed.AuthorizeCard(pending.Tokens[0],native);byte[] png;
            using(var image=new Bitmap(750,1050))using(var graphics=Graphics.FromImage(image))using(var buffer=new MemoryStream()) {graphics.Clear(Color.DarkSlateBlue);image.Save(buffer,ImageFormat.Png);png=buffer.ToArray();}
            var service=new HyTcgService(root,new HyTcgTransport(),resumed);string path=service.SaveCard(native,"data:image/png;base64,"+Convert.ToBase64String(png),pending.Tokens[0]);
            Check(File.Exists(path) && path.EndsWith(".hycard"),"encryptedownedfixture");
            string orphan=Path.Combine(root,"cards","orphan.hycard");File.WriteAllBytes(orphan,new byte[]{1,2,3});
            var reset=resumed.ResetAccount("delete");Check(reset.Gems==1800 && reset.LimitedCredits==0 && reset.PaidCredits==0 && reset.FreeCredits==5 && reset.PitySR==0 && reset.PitySSR==0 && reset.PityUR==0,"resetnewaccount");
            Check(!File.Exists(orphan),"reset removesuncommittedcandidate");
            Check(!File.Exists(path) && resumed.CollectionCards().Count==0 && resumed.PendingCards().Count==0 && resumed.PendingUpgrades().Count==0,"resetdeletescardsandintents");
            var after=clock.Create(root);Check(after.Snapshot().Gems==1800 && after.CollectionCards().Count==0,"resetdurablevalidstamp");
            after.EnsureMissions(games);var target=after.Missions().Items.First(m=>m.Kind=="game");int awarded=0;
            Parallel.For(0,12,i=>Interlocked.Add(ref awarded,after.AwardGameMission(target.GameId)));
            Check(awarded==60 && after.Snapshot().Gems==1860,"concurrentmissionrewardonce");
            after.BuyLimitedCredits(10);int successful=0;
            Parallel.For(0,15,i=>{try {after.Reserve(1,"limited","daily");Interlocked.Increment(ref successful);}catch(HyTcgException ex){if(ex.Code!="CreditsInsufficient")throw;}});
            Check(successful==10 && after.Snapshot().LimitedCredits==0,"concurrentlimited nooverspend");
            Console.WriteLine("HYTCG daily verification PASS: "+checks+" checks (missions, UTC+7 reset, limited currency, account deletion, encrypted files, concurrency).");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
