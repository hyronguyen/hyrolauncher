using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal static class VerifyHyTcgEconomy
{
    private static int assertions;
    private sealed class TestClock
    {
        internal DateTime Now=new DateTime(2026,10,8,0,0,0,DateTimeKind.Utc);
        internal TimeSpan Elapsed=TimeSpan.Zero;
        internal HyTcgEconomy Create(string root)
        {
            // These older economic scenarios start with a deliberately empty test wallet;
            // the new-account 1,800-gem grant is verified separately by the daily verifier.
            bool existed=File.Exists(Path.Combine(root,"hytcg-vault.dat"));var protector=new HyTcgUserProtector();
            var economy=new HyTcgEconomy(root,delegate { return Now; },protector,delegate { return Elapsed; });
            if(!existed)
            {
                var parser=new JavaScriptSerializer {MaxJsonLength=64*1024*1024};string path=Path.Combine(root,"hytcg-vault.dat");
                var fixture=parser.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(path))));
                fixture["Gems"]=0;File.WriteAllBytes(path,protector.Protect(Encoding.UTF8.GetBytes(parser.Serialize(fixture))));
            }
            return economy;
        }
    }
    private static void Check(bool condition,string name) { assertions++; if(!condition) throw new Exception(name); }
    private static void Error(string code,Action action)
    { try { action(); throw new Exception("Expected "+code); } catch(HyTcgException ex) { Check(ex.Code==code,"Expected "+code+" got "+ex.Code); } }
    private static HyTcgCard Card(string rarity)
    {
        return new HyTcgCard { CharacterId=99991,Name="Alina Gray",Series="Magia Record",Gender="Female",Rarity=rarity,RarityColor="#ffffff",
            CardId="HT-20261008-"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant(),CreatedAt="2026-10-08T00:00:00Z",ImageSource="zerochan",
            SourceImage="https://static.zerochan.net/Alina.Gray.full.100.png",ProfileImage="https://www.animecharactersdatabase.com/uploads/image.png",ArtworkPage="https://www.zerochan.net/100",
            Metadata=new Dictionary<string,string> { { "description","local native character" } } };
    }
    private static byte[] Png()
    {
        using(var image=new Bitmap(750,1050)) using(var g=Graphics.FromImage(image)) using(var output=new MemoryStream())
        { g.Clear(Color.DarkSlateBlue); image.Save(output,ImageFormat.Png); return output.ToArray(); }
    }
    private static string WritePng(string root,HyTcgCard card,byte[] bytes)
    { Directory.CreateDirectory(Path.Combine(root,"cards")); string name=card.Rarity+"_"+card.CardId+".png"; File.WriteAllBytes(Path.Combine(root,"cards",name),bytes); return name; }
    private static HyTcgCard Issue(HyTcgEconomy economy,string root,string rarity,byte[] png)
    {
        HyTcgReservation reservation=economy.Reserve(1); string token=reservation.Tokens[0]; economy.Begin(token); var card=Card(rarity);
        economy.AuthorizeCard(token,card); string file=WritePng(root,card,png); economy.Complete(token,card,file,png); return card;
    }
    internal static int Main(string[] args)
    {
        try
        {
            string qaRoot=Path.GetFullPath(args[0]); Directory.CreateDirectory(qaRoot); byte[] png=Png();
            VerifyCredits(Path.Combine(qaRoot,"credits"),png);
            VerifyReservations(Path.Combine(qaRoot,"reservations"),png);
            VerifyGemsAndCards(Path.Combine(qaRoot,"gems"),png);
            VerifyLegacyAndProtection(Path.Combine(qaRoot,"protection"),png);
            VerifyConcurrency(Path.Combine(qaRoot,"concurrency"));
            VerifyPity(Path.Combine(qaRoot,"pity"),png);
            VerifyDirectGems(Path.Combine(qaRoot,"direct-gems"),png);
            VerifyBulkAndFavorites(Path.Combine(qaRoot,"bulk"),png);
            VerifyUpgrades(Path.Combine(qaRoot,"upgrades"),png);
            VerifyVersionOne(Path.Combine(qaRoot,"v1"),png);
            VerifyCharacterSalvage(Path.Combine(qaRoot,"character-salvage"),png);
            VerifyCharacterMissCompensation(Path.Combine(qaRoot,"character-miss"),png);
            Console.WriteLine("HYTCG economy verification passed: "+assertions+" assertions; DPAPI, pity, direct gems, batch redemption, favorites, durable portrait upgrades, wide-art normalization and v1 compatibility."); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void VerifyCredits(string root,byte[] png)
    {
        var clock=new TestClock(); var economy=clock.Create(root);
        Check(economy.Snapshot().FreeCredits==5,"welcome5"); Check(economy.Snapshot().NextFreeAt=="","cap has no countdown");
        Error("CreditsInsufficient",delegate { economy.Reserve(10); }); Error("PackInvalid",delegate { economy.Reserve(2); });
        for(int i=0;i<5;i++) Issue(economy,root,"Common",png);
        Check(economy.Snapshot().FreeCredits==0,"five pulls consume5"); Error("CreditsInsufficient",delegate { economy.Reserve(1); });
        clock.Now=clock.Now.AddMinutes(19); Check(economy.Snapshot().FreeCredits==0,"no early regeneration");
        clock.Now=clock.Now.AddMinutes(1); Check(economy.Snapshot().FreeCredits==1,"20 minute grant");
        clock.Now=clock.Now.AddMinutes(60); Check(economy.Snapshot().FreeCredits==4,"offline accumulated grants");
        clock.Now=clock.Now.AddDays(30); Check(economy.Snapshot().FreeCredits==5,"offline cap5 discards excess");
        Issue(economy,root,"Common",png); Check(economy.Snapshot().FreeCredits==4,"spending afterlongcap no bankedoverflow");
        clock.Now=clock.Now.AddMinutes(19); Check(economy.Snapshot().FreeCredits==4,"fresh countdown aftercap");
        DateTime highWater=clock.Now; clock.Now=clock.Now.AddDays(-10); Check(economy.Snapshot().FreeCredits==4,"wallclock rollback doesnot mint");
        clock.Elapsed=TimeSpan.FromMinutes(20); var monotonic=economy.Snapshot();
        Check(monotonic.ServerNow.CompareTo(highWater.ToString("o"))>=0 && monotonic.FreeCredits==5,"monotonic elapsed stillregenerates afterwallclockrollback");
        clock.Now=highWater.AddMinutes(1); Check(economy.Snapshot().FreeCredits==5,"reaches original trusteddeadline");
        var reloaded=clock.Create(root); Check(reloaded.Snapshot().FreeCredits==5,"durable freebalance");
        Error("GemsInsufficient",delegate { reloaded.BuyCredit(); });
    }
    private static void VerifyReservations(string root,byte[] png)
    {
        var clock=new TestClock(); var economy=clock.Create(root); var reservation=economy.Reserve(1); string token=reservation.Tokens[0];
        Check(reservation.Wallet.FreeCredits==4,"reserve debit durable"); economy.Begin(token); Error("Busy",delegate { economy.Begin(token); });
        HyTcgCard native=Card("UR"); economy.AuthorizeCard(token,native); Check(economy.PendingCards().Count==1,"persist native cardbefore reveal");
        Error("CardAlreadyIssued",delegate { economy.Refund(token); });
        economy.CancelReservation(reservation.ReservationId); Check(economy.Snapshot().FreeCredits==4,"closecannotrefund issuedcard");
        economy.RecoverUnused(); Check(economy.Snapshot().FreeCredits==4,"recoverdoesnotrerollissued");
        var restarted=clock.Create(root); var pending=restarted.PendingCards(); Check(pending.Count==1 && pending[0].Card.CardId==native.CardId,"restart resumes exactcard");
        HyTcgCard changed=Card("Common"); changed.CardId=native.CardId; Error("CardInvalid",delegate { restarted.AuthorizeCard(token,changed); });
        native.ImageSource="acdb"; native.SourceImage=native.ProfileImage; native.FallbackReason="ZerochanUnavailable"; native.ArtworkPage="";
        restarted.Begin(token); restarted.AuthorizeCard(token,native); Check(restarted.PendingCards()[0].Card.ImageSource=="acdb","native fallback changes artworkonly");
        string name=WritePng(root,native,png); restarted.Complete(token,native,name,png); Check(restarted.PendingCards().Count==0,"save commitsownership clearspending");
        Check(restarted.Complete(token,native,name,png).FreeCredits==4,"save retry idempotent");
        var failed=restarted.Reserve(1); restarted.Begin(failed.Tokens[0]); restarted.EndFetch(failed.Tokens[0]); restarted.Refund(failed.Tokens[0]);
        Check(restarted.Snapshot().FreeCredits==4,"failed api refunded"); restarted.Refund(failed.Tokens[0]); Check(restarted.Snapshot().FreeCredits==4,"refund replay no duplicate");
        var unused=restarted.Reserve(1); restarted.CancelReservation(unused.ReservationId); restarted.CancelReservation(unused.ReservationId);
        Check(restarted.Snapshot().FreeCredits==4,"cancelunused exactly once");
        var crashed=restarted.Reserve(1); restarted.Begin(crashed.Tokens[0]); var startup=clock.Create(root); startup.RecoverUnused(); startup.RecoverUnused();
        Check(startup.Snapshot().FreeCredits==4,"startupfailedfetch refundexactly once");
        var unauthorized=Card("UR"); string unauthorizedName=WritePng(root,unauthorized,png);
        Error("CardInvalid",delegate { startup.Complete("fake-token",unauthorized,unauthorizedName,png); });
        Check(startup.ReadCard(unauthorizedName,png)==null,"unsigned injected png unowned");
    }
    private static void VerifyGemsAndCards(string root,byte[] png)
    {
        var clock=new TestClock(); var economy=clock.Create(root);
        int[] rewards={1,3,8,20,60,160}; int expected=0;
        for(int i=0;i<HyTcgService.Rarities.Length;i++)
        {
            if(i==5) { clock.Now=clock.Now.AddMinutes(20); economy.Snapshot(); }
            string rarity=HyTcgService.Rarities[i]; HyTcgCard card=Issue(economy,root,rarity,png); string name=rarity+"_"+card.CardId+".png";
            Check(economy.CanDissolve(card.CardId),"native owned redeemable "+rarity);
            Check(economy.ReadCard(name,png).CardId==card.CardId,"authenticated collection card "+rarity);
            var dissolved=economy.Dissolve(card.CardId); expected+=rewards[i];
            Check(dissolved.GemsAwarded==rewards[i] && dissolved.Wallet.Gems==expected,"salvage reward "+rarity);
            Check(economy.ReadCard(name,png)==null && !economy.CanDissolve(card.CardId),"tombstone hides evenretained png");
            Error("CardUnavailable",delegate { economy.Dissolve(card.CardId); });
            // Retaining or copying the same image never removes its committed tombstone.
            File.Copy(Path.Combine(root,"cards",name),Path.Combine(root,"cards","copy_"+name));
            Check(economy.ReadCard("copy_"+name,png)==null,"copiedpng no newownership");
        }
        Check(expected==252,"known hardcoded rewardtotal"); var bought=economy.BuyCredit();
        Check(bought.Gems==92 && bought.PaidCredits==1,"buy exactly160 foronecredit");
        Error("GemsInsufficient",delegate { economy.BuyCredit(); });
        var paid=economy.Reserve(1); Check(paid.Wallet.PaidCredits==0,"paid creditusedwhenfree0");
        economy.Refund(paid.Tokens[0]); Check(economy.Snapshot().PaidCredits==1,"paidrefund remains paid");
        var reloaded=clock.Create(root); Check(reloaded.Snapshot().Gems==92 && reloaded.Snapshot().PaidCredits==1,"persist gems andpaidcredits");
        // Sixteen more UR redemptions fund an x10; all10 are persistedbefore native fetches.
        for(int i=0;i<10;i++)
        {
            var added=Issue(reloaded,root,"UR",png); reloaded.Dissolve(added.CardId); reloaded.BuyCredit();
        }
        // The existing paid credit remains1, so accumulate9 after using1 eachtime isincorrect;
        // deliberately buyadditionalcredits with fresh regeneratedUR cards instead.
        for(int i=0;i<9;i++) { clock.Now=clock.Now.AddMinutes(20); var added=Issue(reloaded,root,"UR",png); reloaded.Dissolve(added.CardId); reloaded.BuyCredit(); }
        Check(reloaded.Snapshot().PaidCredits==10,"ten purchasedcredits accumulated separatefromfreecap");
        var ten=reloaded.Reserve(10); Check(ten.Tokens.Count==10 && ten.Wallet.PaidCredits==0,"x10 atomically debits10");
        reloaded.CancelReservation(ten.ReservationId); Check(reloaded.Snapshot().PaidCredits==10,"x10 unused refund exactsources");
    }
    private static void VerifyLegacyAndProtection(string root,byte[] png)
    {
        var clock=new TestClock(); var economy=clock.Create(root); var legacy=Card("UR"); string legacyName=WritePng(root,legacy,png);
        Check(economy.TryRegisterLegacy(legacy,legacyName,png),"one time legacy registration"); Check(!economy.CanDissolve(legacy.CardId),"legacy noawardfromuntrustedrarity");
        var legacyCollection=economy.CollectionCards(); Check(legacyCollection.Count==1 && legacyCollection[0].IsLegacy && !legacyCollection[0].CanDissolve && legacyCollection[0].SalvageValue==0,"single-read legacy collection readonly");
        Error("LegacyCard",delegate { economy.Dissolve(legacy.CardId); }); economy.FinishMigration(); Check(economy.MigrationComplete,"migrationdurablefinished");
        var forged=Card("UR"); string forgedName=WritePng(root,forged,png); Check(!economy.TryRegisterLegacy(forged,forgedName,png),"noinjection aftermigration");
        Check(economy.ReadCard(forgedName,png)==null,"unregisteredpngnotcollection");
        HyTcgCard owned=Issue(economy,root,"UR",png); string name="UR_"+owned.CardId+".png"; string full=Path.Combine(root,"cards",name);
        byte[] altered=(byte[])png.Clone(); altered[altered.Length-20]^=1; File.WriteAllBytes(full,altered);
        Check(economy.ReadCard(name,altered)==null,"modified png rejected"); Error("CardInvalid",delegate { economy.Dissolve(owned.CardId); });
        Check(economy.CollectionCards().Count==1,"single-read collection excludesmodifiedcardandforeignpng");
        File.WriteAllBytes(full,png); Check(economy.CanDissolve(owned.CardId),"restoringauthentic undissolvedpngsafe");
        Check(economy.CollectionCards().Count==2 && economy.CollectionCards().Any(c=>c.Card.CardId==owned.CardId && c.CanDissolve && c.SalvageValue==160),"single-read collection validownednativeauthority");
        byte[] protectedState=File.ReadAllBytes(economy.VaultPath); string printable=Encoding.UTF8.GetString(protectedState);
        Check(!printable.Contains("FreeCredits") && !printable.Contains(owned.CardId),"vault storesno plaintextbalancesorcardmodels");
        protectedState[protectedState.Length/2]^=1; File.WriteAllBytes(economy.VaultPath,protectedState);
        Error("VaultInvalid",delegate { clock.Create(root); }); Error("VaultInvalid",delegate { economy.Snapshot(); });
        Check(File.ReadAllBytes(economy.VaultPath).SequenceEqual(protectedState),"corruptvault never silentlyreset");
        string deletedRoot=Path.Combine(root,"deleted"); var deleted=clock.Create(deletedRoot); File.Delete(deleted.VaultPath);
        Error("VaultInvalid",delegate { deleted.Snapshot(); });
        Error("VaultInvalid",delegate { clock.Create(deletedRoot); }); Check(File.Exists(deleted.StampPath),"establishedmarker rejects missingvault onrestart");
        string stampRoot=Path.Combine(root,"stamp"); var stamp=clock.Create(stampRoot); byte[] changedStamp=File.ReadAllBytes(stamp.StampPath); changedStamp[changedStamp.Length/2]^=1; File.WriteAllBytes(stamp.StampPath,changedStamp);
        Error("VaultInvalid",delegate { clock.Create(stampRoot); });
        string swappedRoot=Path.Combine(root,"swap"); var swap=clock.Create(swappedRoot); File.Copy(stamp.VaultPath,swap.VaultPath,true);
        Error("VaultInvalid",delegate { clock.Create(swappedRoot); });
    }
    private static void VerifyConcurrency(string root)
    {
        var clock=new TestClock(); var economy=clock.Create(root); var reservations=new List<HyTcgReservation>(); int rejected=0; object gate=new object();
        Parallel.For(0,20,delegate(int i)
        {
            try { var reserved=economy.Reserve(1); lock(gate) reservations.Add(reserved); }
            catch(HyTcgException ex) { if(ex.Code!="CreditsInsufficient") throw; lock(gate) rejected++; }
        });
        Check(reservations.Count==5 && rejected==15 && economy.Snapshot().FreeCredits==0,"parallel reserve cannot overspend5");
        Check(reservations.SelectMany(r=>r.Tokens).Distinct().Count()==5,"parallel reservationtokens unique");
        Parallel.ForEach(reservations,delegate(HyTcgReservation r) { economy.CancelReservation(r.ReservationId); });
        Check(economy.Snapshot().FreeCredits==5,"parallel refunds restore exactbalance once");
    }
    private static void VerifyPity(string root,byte[] png)
    {
        var clock=new TestClock(); var economy=clock.Create(root);
        Check(economy.Snapshot().PitySRLimit==50 && economy.Snapshot().PitySSRLimit==100 && economy.Snapshot().PityURLimit==500,"compiled pity thresholds");
        var failed=economy.Reserve(1); economy.Begin(failed.Tokens[0]); economy.EndFetch(failed.Tokens[0]); economy.Refund(failed.Tokens[0]);
        Check(economy.Snapshot().PitySR==0 && economy.Snapshot().PitySSR==0 && economy.Snapshot().PityUR==0,"fetch failures do not advance pity");
        for(int i=1;i<=500;i++)
        {
            clock.Now=clock.Now.AddMinutes(20);
            var reservation=economy.Reserve(1); string token=reservation.Tokens[0]; economy.Begin(token); var card=Card("Common");
            economy.AuthorizeCard(token,card);
            string expected=i%500==0?"UR":i%100==0?"SSR":i%50==0?"SR":"Common";
            Check(card.Rarity==expected && card.RarityColor==HyTcgRules.RarityColor(expected),"native pity tier/color at pull"+i);
            var wallet=economy.Snapshot();
            Check(wallet.PitySR==i%50 && wallet.PitySSR==i%100 && wallet.PityUR==i%500,"three tier-or-higher miss counters at pull"+i);
            if(i==49 || i==50 || i==99 || i==100 || i==499 || i==500)
            {
                var recovered=clock.Create(root); var pending=recovered.PendingCards().Single(c=>c.Token==token);
                Check(pending.Card.Rarity==expected,"restart retains guaranteed native card"+i);
                recovered.Begin(token); recovered.AuthorizeCard(token,pending.Card);
                var replay=recovered.Snapshot();
                Check(replay.PitySR==wallet.PitySR && replay.PitySSR==wallet.PitySSR && replay.PityUR==wallet.PityUR,"authorize replay/fallback never advances pity"+i);
            }
            string file=WritePng(root,card,png); economy.Complete(token,card,file,png);
        }
        Check(economy.CollectionCards().Count==500,"500 authorized pulls committed as owned cards");
        var natural=Issue(economy,root,"SSR",png);
        Check(natural.Rarity=="SSR" && economy.Snapshot().PitySR==0 && economy.Snapshot().PitySSR==0 && economy.Snapshot().PityUR==1,"natural SSR resets lower counters only");
        clock.Now=clock.Now.AddMinutes(20); Issue(economy,root,"SR",png);
        Check(economy.Snapshot().PitySR==0 && economy.Snapshot().PitySSR==1 && economy.Snapshot().PityUR==2,"natural SR resets SR but counts higher misses");
        clock.Now=clock.Now.AddMinutes(20); Issue(economy,root,"UR",png);
        Check(economy.Snapshot().PitySR==0 && economy.Snapshot().PitySSR==0 && economy.Snapshot().PityUR==0,"natural UR resets all pity");
    }
    private static void VerifyDirectGems(string root,byte[] png)
    {
        var clock=new TestClock(); var economy=clock.Create(root); var fund=Issue(economy,root,"UR",png); economy.Dissolve(fund.CardId);
        var before=economy.Snapshot(); var pull=economy.Reserve(1,"gems");
        Check(pull.Wallet.Gems==0 && pull.Wallet.FreeCredits==before.FreeCredits && pull.Wallet.PaidCredits==0,"direct gem pull costs160 without credit conversion");
        economy.Begin(pull.Tokens[0]); economy.EndFetch(pull.Tokens[0]); economy.Refund(pull.Tokens[0]); economy.Refund(pull.Tokens[0]);
        Check(economy.Snapshot().Gems==160 && economy.Snapshot().PaidCredits==0,"failed gem fetch refunds160 gems exactly once");
        Error("GemsInsufficient",delegate { economy.Reserve(10,"gems"); });
        Check(economy.Snapshot().Gems==160,"insufficient x10 gems is atomic");
        Error("PaymentInvalid",delegate { economy.Reserve(1,"fake"); });
        for(int i=0;i<10;i++) { clock.Now=clock.Now.AddMinutes(20); var card=Issue(economy,root,"UR",png); economy.Dissolve(card.CardId); }
        before=economy.Snapshot(); var ten=economy.Reserve(10,"gems");
        Check(ten.Tokens.Count==10 && ten.Wallet.Gems==before.Gems-1600 && ten.Wallet.FreeCredits==before.FreeCredits,"x10 direct gems atomic1600 debit");
        economy.CancelReservation(ten.ReservationId); economy.CancelReservation(ten.ReservationId);
        Check(economy.Snapshot().Gems==before.Gems && economy.Snapshot().PaidCredits==before.PaidCredits,"unused gem reservation refunds original currency");
        pull=economy.Reserve(1,"gems"); economy.Begin(pull.Tokens[0]); var issued=Card("Common"); economy.AuthorizeCard(pull.Tokens[0],issued);
        Error("CardAlreadyIssued",delegate { economy.Refund(pull.Tokens[0]); });
        var restart=clock.Create(root); restart.RecoverUnused();
        Check(restart.Snapshot().Gems==before.Gems-160 && restart.PendingCards().Single().Card.CardId==issued.CardId,"issued direct gem card cannot reroll/refund after restart");
    }
    private static void VerifyBulkAndFavorites(string root,byte[] png)
    {
        var clock=new TestClock(); var economy=clock.Create(root);
        var common=Issue(economy,root,"Common",png); var rare=Issue(economy,root,"Rare",png); var ur=Issue(economy,root,"UR",png);
        economy.SetFavorite(common.CardId,true);
        Check(economy.CollectionCards().Single(c=>c.Card.CardId==common.CardId).Favorite,"favorite stored separately from card signature");
        var restarted=clock.Create(root); Check(restarted.CollectionCards().Single(c=>c.Card.CardId==common.CardId).Favorite,"favorite survives native restart");
        var ownedFile=economy.CollectionCards().Single(c=>c.Card.CardId==ur.CardId).ImagePath;
        byte[] altered=(byte[])png.Clone(); altered[altered.Length-20]^=1; File.WriteAllBytes(ownedFile,altered);
        Error("CardInvalid",delegate { economy.DissolveMany(new List<string> {common.CardId,ur.CardId}); });
        Check(economy.CanDissolve(common.CardId) && economy.Snapshot().Gems==0,"batch checks all signatures before any award/tombstone");
        Error("CardInvalid",delegate { economy.SetFavorite(ur.CardId,true); });
        Error("CardInvalid",delegate { economy.DissolveMany(new List<string> {common.CardId,common.CardId}); });
        Error("CardInvalid",delegate { economy.DissolveMany(new List<string>()); });
        Error("CardInvalid",delegate { economy.DissolveMany(Enumerable.Range(0,101).Select(i=>"card"+i).ToList()); });
        Error("CardUnavailable",delegate { economy.DissolveMany(new List<string> {common.CardId,"unknown"}); });
        Check(economy.CanDissolve(common.CardId) && economy.Snapshot().Gems==0,"missing card leaves entire batch unchanged");
        File.WriteAllBytes(ownedFile,png); economy.SetFavorite(common.CardId,false);
        Check(!economy.CollectionCards().Single(c=>c.Card.CardId==common.CardId).Favorite,"favorite toggle preserves valid PNG ownership");
        var result=economy.DissolveMany(new List<string> {common.CardId,rare.CardId,ur.CardId});
        Check(result.GemsAwarded==169 && result.Wallet.Gems==169 && result.CardIds.Count==3 && result.FilePaths.Count==3,"one batch returns native summed rewards and paths");
        Check(economy.CollectionCards().Count==0,"batch tombstones all cards even before image deletion");
        Error("CardUnavailable",delegate { economy.DissolveMany(result.CardIds); });
        Check(economy.Snapshot().Gems==169,"batch replay cannot award twice");
    }
    private static HyTcgCard IssueCharacter(HyTcgEconomy economy,TestClock clock,string root,string rarity,int characterId,byte[] png)
    {
        clock.Now=clock.Now.AddMinutes(20); var reservation=economy.Reserve(1); string token=reservation.Tokens[0]; economy.Begin(token);
        var card=Card(rarity); card.CharacterId=characterId; economy.AuthorizeCard(token,card); string file=WritePng(root,card,png); economy.Complete(token,card,file,png); return card;
    }
    private static void VerifyUpgrades(string root,byte[] png)
    {
        var clock=new TestClock(); var economy=clock.Create(root);
        var target=IssueCharacter(economy,clock,root,"Common",99991,png); var same=IssueCharacter(economy,clock,root,"Uncommon",99991,png);
        var wrong=IssueCharacter(economy,clock,root,"Rare",99992,png); var other=IssueCharacter(economy,clock,root,"Common",99992,png);
        Error("UpgradeMaterialsInvalid",delegate { economy.PrepareUpgrade(target.CardId,new List<string> {wrong.CardId},false); });
        Error("UpgradeMaterialsInvalid",delegate { economy.PrepareUpgrade(target.CardId,new List<string> {target.CardId},false); });
        Error("UpgradeMaterialsInvalid",delegate { economy.PrepareUpgrade(target.CardId,new List<string> {same.CardId,same.CardId},false); });
        economy.SetFavorite(target.CardId,true);
        var prepared=economy.PrepareUpgrade(target.CardId,new List<string> {same.CardId},false);
        Check(prepared.Card.Rarity=="Uncommon" && prepared.Card.Revision==1 && prepared.Card.CardId==target.CardId && prepared.Stage=="prepared","same-character next-tier material prepares one tier/revision");
        Check(economy.PrepareUpgrade(target.CardId,new List<string> {same.CardId},false).Token==prepared.Token,"prepare retry idempotent pending token");
        Check(!economy.CanDissolve(target.CardId) && !economy.CanDissolve(same.CardId),"pending target/material locked against dissolve");
        Check(economy.CollectionCards().Single(c=>c.Card.CardId==target.CardId).UpgradeLocked,"native collection exposes pending upgrade lock");
        Error("CardLocked",delegate { economy.DissolveMany(new List<string> {other.CardId,same.CardId}); });
        Check(economy.CanDissolve(other.CardId) && economy.Snapshot().Gems==0,"locked card rejects entire dissolve batch");
        Error("CardLocked",delegate { economy.PrepareUpgrade(other.CardId,new List<string> {same.CardId},false); });
        economy.BeginUpgrade(prepared.Token); Error("Busy",delegate {economy.BeginUpgrade(prepared.Token);}); Error("Busy",delegate {economy.CancelUpgrade(prepared.Token);});
        economy.AuthorizeUpgrade(prepared.Token,prepared.Card);
        var restarted=clock.Create(root); var recovery=restarted.PendingUpgrades().Single();
        Check(recovery.Token==prepared.Token && recovery.Stage=="authorized" && recovery.Card.SourceImage==target.SourceImage,"restart resumes authorized upgrade with unchanged artwork");
        economy.SetFavorite(target.CardId,false); economy.SetFavorite(target.CardId,true);
        string newName="Uncommon_"+target.CardId+"_R1.png"; File.WriteAllBytes(Path.Combine(root,"cards",newName),png);
        var upgraded=restarted.CompleteUpgrade(prepared.Token,recovery.Card,newName,png);
        Check(upgraded.ConsumedIds.SequenceEqual(new[] {same.CardId}) && upgraded.Card.Rarity=="Uncommon" && upgraded.Card.Revision==1 && upgraded.Wallet.Gems==0,"upgrade consumes material atomically without salvage gems");
        Check(restarted.ReadOwnedCard(target.CardId).Rarity=="Uncommon" && restarted.CollectionCards().Single(c=>c.Card.CardId==target.CardId).Favorite,"upgraded target preserves cardId and live favorite");
        Check(restarted.ReadCard("Common_"+target.CardId+".png",png)==null && !restarted.CanDissolve(same.CardId),"old export and consumed material cannot become owned again");
        var replay=restarted.CompleteUpgrade(prepared.Token,recovery.Card,newName,png); restarted.CancelUpgrade(prepared.Token);
        Check(replay.ConsumedIds.Count==1 && restarted.Snapshot().Gems==0 && restarted.PendingUpgrades().Count==0,"commit retry/cancel never consumes twice or reverts completed upgrade");
        Error("CardUnavailable",delegate { restarted.Dissolve(same.CardId); });
        Error("UpgradeMaterialsInvalid",delegate { restarted.PrepareUpgrade(target.CardId,new List<string> {wrong.CardId},false); });
        var materials=new List<string>();
        for(int i=0;i<10;i++) materials.Add(IssueCharacter(restarted,clock,root,"Rare",100000+i,png).CardId);
        prepared=restarted.PrepareUpgrade(target.CardId,materials,true);
        var changed=prepared.Card; changed.SourceImage="https://static.zerochan.net/Alina.Gray.full.200.png"; changed.ArtworkPage="https://www.zerochan.net/200";
        changed.Orientation="landscape"; changed.ArtworkWidth=1400; changed.ArtworkHeight=900;
        restarted.BeginUpgrade(prepared.Token); restarted.AuthorizeUpgrade(prepared.Token,changed);
        changed=restarted.PendingUpgrades().Single().Card;
        Check(changed.Orientation=="portrait" && changed.ArtworkWidth==1400 && changed.ArtworkHeight==900,"native authorization preserves wide artwork dimensions while enforcing portrait card export");
        byte[] changedPortrait;
        using(var image=new Bitmap(750,1050)) using(var g=Graphics.FromImage(image)) using(var output=new MemoryStream())
        {g.Clear(Color.MediumPurple);image.Save(output,ImageFormat.Png);changedPortrait=output.ToArray();}
        string changedName="Rare_"+target.CardId+"_R2.png"; File.WriteAllBytes(Path.Combine(root,"cards",changedName),changedPortrait);
        Error("CardInvalid",delegate { restarted.CompleteUpgrade(prepared.Token,changed,changedName,png); });
        var damaged=restarted.CollectionCards().First(c=>c.Card.CardId==materials[0]).ImagePath;
        byte[] damagedPng=(byte[])png.Clone(); damagedPng[damagedPng.Length-20]^=1; File.WriteAllBytes(damaged,damagedPng);
        Error("CardInvalid",delegate { restarted.CompleteUpgrade(prepared.Token,changed,changedName,changedPortrait); });
        Check(restarted.ReadOwnedCard(target.CardId).Rarity=="Uncommon" && restarted.PendingUpgrades().Count==1,"failed commit leaves prior card and pending upgrade untouched");
        File.WriteAllBytes(damaged,png); var second=restarted.CompleteUpgrade(prepared.Token,changed,changedName,changedPortrait);
        Check(second.ConsumedIds.Count==10 && second.Card.Rarity=="Rare" && second.Card.Revision==2 && second.Card.Orientation=="portrait","ten next-tier materials upgrade with native portrait export");
        Check(second.Wallet.Gems==0 && restarted.CollectionCards().Count==3,"all ten materials tombstoned with no gem award");
        Check(restarted.ReadCard(changedName,changedPortrait)!=null && restarted.CollectionCards().Single(c=>c.Card.CardId==target.CardId).Favorite,"portrait signature validates and favorite survives second upgrade");
        var cancelTarget=IssueCharacter(restarted,clock,root,"Common",200001,png); var cancelMaterial=IssueCharacter(restarted,clock,root,"Uncommon",200001,png);
        prepared=restarted.PrepareUpgrade(cancelTarget.CardId,new List<string> {cancelMaterial.CardId},true);
        restarted.AuthorizeUpgrade(prepared.Token,prepared.Card); restarted.CancelUpgrade(prepared.Token); restarted.CancelUpgrade(prepared.Token);
        Check(restarted.CanDissolve(cancelTarget.CardId) && restarted.CanDissolve(cancelMaterial.CardId) && restarted.ReadOwnedCard(cancelTarget.CardId).Rarity=="Common","authorized cancel releases target/material without consuming either");
        var max=IssueCharacter(restarted,clock,root,"UR",200002,png);
        Error("UpgradeMax",delegate { restarted.PrepareUpgrade(max.CardId,new List<string> {cancelMaterial.CardId},false); });
        var legacy=Card("Common"); var legacyFile=WritePng(root,legacy,png); restarted.TryRegisterLegacy(legacy,legacyFile,png);
        Error("LegacyCard",delegate { restarted.PrepareUpgrade(legacy.CardId,new List<string> {cancelMaterial.CardId},false); });
    }
    private static void VerifyVersionOne(string root,byte[] png)
    {
        var clock=new TestClock(); var economy=clock.Create(root); var card=Issue(economy,root,"Rare",png);
        var protector=new HyTcgUserProtector(); var parser=new JavaScriptSerializer {MaxJsonLength=64*1024*1024};
        var state=parser.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(economy.VaultPath))));
        foreach(string key in new[] {"Upgrades","PitySR","PitySSR","PityUR"})state.Remove(key);
        var cards=(System.Collections.IEnumerable)state["Cards"];
        foreach(var value in cards)
        {
            var owned=(Dictionary<string,object>)value; owned.Remove("Favorite"); var model=(Dictionary<string,object>)owned["Card"];
            foreach(string key in new[] {"Orientation","ArtworkWidth","ArtworkHeight","Revision"})model.Remove(key);
        }
        File.WriteAllBytes(economy.VaultPath,protector.Protect(Encoding.UTF8.GetBytes(parser.Serialize(state))));
        var migrated=clock.Create(root); var collection=migrated.CollectionCards();
        Check(collection.Count==1 && collection[0].Card.CardId==card.CardId && !collection[0].Favorite && !collection[0].UpgradeLocked,"old v1 card signatures and collection remain valid");
        Check(migrated.Snapshot().PitySR==0 && migrated.Snapshot().PitySSR==0 && migrated.Snapshot().PityUR==0 && migrated.PendingUpgrades().Count==0,"v1 progression defaults zero without resetting wallet");
        Check(migrated.Snapshot().FreeCredits==4 && migrated.Dissolve(card.CardId).GemsAwarded==8,"v1 balances/ownership survive new optional fields");
    }
    // Test fixtures edit only an isolated, test-created DPAPI wallet to reach otherwise
    // impractical limit cases. Application code never exposes these values to JavaScript.
    private static void FixtureFields(HyTcgEconomy economy,params object[] fields)
    {
        var protector=new HyTcgUserProtector();var parser=new JavaScriptSerializer {MaxJsonLength=64*1024*1024};
        var state=parser.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(economy.VaultPath))));
        for(int i=0;i<fields.Length;i+=2)state[(string)fields[i]]=fields[i+1];
        File.WriteAllBytes(economy.VaultPath,protector.Protect(Encoding.UTF8.GetBytes(parser.Serialize(state))));
    }
    private static void VerifyCharacterSalvage(string root,byte[] png)
    {
        var clock=new TestClock();var economy=clock.Create(root);
        var ur=Issue(economy,root,"UR",png);var common=Issue(economy,root,"Common",png);var ssr=Issue(economy,root,"SSR",png);
        Check(economy.ReadOwnedCards(new List<string>{ur.CardId,common.CardId}).Count==2,"batch enrichment metadata uses authenticated native ownership");
        Error("CardInvalid",delegate {economy.ReadOwnedCards(new List<string>{ur.CardId,ur.CardId});});
        Error("CardUnavailable",delegate {economy.ReadOwnedCards(new List<string>{"unknown"});});
        string metaFile=Path.Combine(root,"cards","UR_"+ur.CardId+".png");string movedFile=metaFile+".test";
        File.Move(metaFile,movedFile);
        try
        {
            Check(economy.ReadOwnedCards(new List<string>{ur.CardId}).Single().CharacterId==ur.CharacterId,"warming enrichment does not decrypt or require an image file");
            Error("CardUnavailable",delegate {economy.Dissolve(ur.CardId);});
        }
        finally {File.Move(movedFile,metaFile);}
        Check(economy.CollectionCards().Single(c=>c.Card.CardId==ur.CardId).SalvageValue==160,"uncached favourites keep native base reward");
        economy.CharacterFavouritesLookup=delegate(HyTcgCard card) {return null;};
        Check(economy.SalvageValue(ur)==160,"unmatched character favourites ignored");
        ur.Metadata["favourites"]="99999999";
        Check(economy.SalvageValue(ur)==160,"renderer or arbitrary card metadata never grants favourites bonus");
        economy.CharacterFavouritesLookup=delegate(HyTcgCard card) {return 99;};
        Check(economy.SalvageValue(ur)==160,"favourites below100 truncate fractional gems");
        economy.CharacterFavouritesLookup=delegate(HyTcgCard card) {return 199;};
        Check(economy.SalvageValue(ur)==161,"favourites bonus uses integer floor");
        economy.CharacterFavouritesLookup=delegate(HyTcgCard card) {return -1;};
        Check(economy.SalvageValue(ur)==160,"negative favourites cannot reduce award");
        economy.CharacterFavouritesLookup=delegate(HyTcgCard card) {return Int32.MaxValue;};
        Check(economy.SalvageValue(ur)==160,"unbounded favourites safely ignored");
        economy.CharacterFavouritesLookup=delegate(HyTcgCard card) {throw new IOException("cache unavailable");};
        Check(economy.SalvageValue(ur)==160,"cache error falls back to base");
        economy.CharacterFavouritesLookup=delegate(HyTcgCard card) {return 200;};
        var catalog=economy.CollectionCards();
        Check(catalog.Single(c=>c.Card.CardId==ur.CardId).SalvageValue==162 && catalog.Single(c=>c.Card.CardId==common.CardId).SalvageValue==3,"collection exposes exact current native per-character value");
        Check(economy.Dissolve(ur.CardId).GemsAwarded==162,"individual transaction uses favourite bonus");
        Check(economy.DissolveMany(new List<string>{common.CardId,ssr.CardId}).GemsAwarded==65,"batch transaction sums favourite bonuses once per card");
        Check(economy.Snapshot().Gems==227,"native protected wallet persists all bonus awards");
        Error("CardUnavailable",delegate {economy.DissolveMany(new List<string>{common.CardId,ssr.CardId});});
        Check(economy.Snapshot().Gems==227,"batch replay never re-awards bonus");
        Error("CardUnavailable",delegate {economy.ReadOwnedCards(new List<string>{common.CardId});});
        var limitCard=Issue(economy,root,"UR",png);FixtureFields(economy,"Gems",1000000000-180);
        var refundable=economy.Reserve(1,"limitedGems","test-banner");
        // The 180-gem debit makes enough space for a 162-gem award, but the limited
        // reservation must retain capacity to refund. Every pending currency counts.
        FixtureFields(economy,"Gems",1000000000-180);
        Error("GemsLimit",delegate {economy.Dissolve(limitCard.CardId);});
        Check(economy.CanDissolve(limitCard.CardId),"overflow rejection preserves card ownership");
        Check(economy.Refund(refundable.Tokens[0]).Gems==1000000000,"limited refund capacity survives failed dissolve");
        byte[] validVault=File.ReadAllBytes(economy.VaultPath);var protector=new HyTcgUserProtector();var parser=new JavaScriptSerializer {MaxJsonLength=64*1024*1024};
        var state=parser.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(protector.Unprotect(validVault)));
        var owned=((System.Collections.IEnumerable)state["Cards"]).Cast<Dictionary<string,object>>().Single(c=>(string)c["CardId"]==limitCard.CardId);
        owned["Signature"]=new string('0',64);File.WriteAllBytes(economy.VaultPath,protector.Protect(Encoding.UTF8.GetBytes(parser.Serialize(state))));
        Error("CardInvalid",delegate {economy.ReadOwnedCards(new List<string>{limitCard.CardId});});
        File.WriteAllBytes(economy.VaultPath,validVault);
    }
    private static void VerifyCharacterMissCompensation(string root,byte[] png)
    {
        foreach(string currency in new[]{"credits","paid","gems","limited","limitedGems"})
        {
            string caseRoot=Path.Combine(root,currency);var clock=new TestClock();var economy=clock.Create(caseRoot);FixtureFields(economy,"Gems",5000);
            if(currency=="paid")FixtureFields(economy,"FreeCredits",0,"PaidCredits",1);
            if(currency=="limited")economy.BuyLimitedCredits(1);
            string payment=currency=="paid"?"credits":currency;
            var pull=economy.Reserve(1,payment,currency.StartsWith("limited",StringComparison.Ordinal)?"test-banner":"");var debited=pull.Wallet;
            string token=pull.Tokens[0];
            Error("CardInvalid",delegate {economy.CompensateCharacterMiss(token);});
            economy.Begin(token);
            var result=economy.CompensateCharacterMiss(token);
            Check(result.GemsAwarded==160 && result.Wallet.Gems==debited.Gems+160,"miss awards160 after consumed "+currency+" pull");
            Check(result.Wallet.FreeCredits==debited.FreeCredits && result.Wallet.PaidCredits==debited.PaidCredits && result.Wallet.LimitedCredits==debited.LimitedCredits,"miss does not refund "+currency+" debit");
            Check(result.Wallet.PitySR==0 && result.Wallet.PitySSR==0 && result.Wallet.PityUR==0,"miss has no issued card or pity roll");
            economy.Refund(token);economy.CancelReservation(pull.ReservationId);economy.RecoverUnused();
            Check(economy.Snapshot().Gems==result.Wallet.Gems && economy.Snapshot().FreeCredits==result.Wallet.FreeCredits,"refund/cancel/recovery cannot resurrect consumed "+currency+" pull");
            Check(economy.CompensateCharacterMiss(token).Wallet.Gems==result.Wallet.Gems,"miss completion replay idempotent "+currency);
            Error("CardInvalid",delegate {economy.Begin(token);});
            var restarted=clock.Create(caseRoot);
            Check(restarted.CharacterMissCompensation(token).GemsAwarded==160 && restarted.CompensateCharacterMiss(token).Wallet.Gems==result.Wallet.Gems,"terminal receipt survives crash/restart "+currency);
            Check(restarted.PendingCards().Count==0,"miss never creates fake card "+currency);
            Check(restarted.CharacterMissCompensation("HC-"+new string('a',32))==null,"unknown token has no terminal reward "+currency);
            Error("CardInvalid",delegate {restarted.CompensateCharacterMiss("HC-"+new string('a',32));});
        }
        var concurrencyClock=new TestClock();var concurrent=concurrencyClock.Create(Path.Combine(root,"concurrent"));
        var reservation=concurrent.Reserve(1);string replayToken=reservation.Tokens[0];concurrent.Begin(replayToken);
        Parallel.For(0,32,delegate(int unused) {Check(concurrent.CompensateCharacterMiss(replayToken).GemsAwarded==160,"concurrent receipt result");});
        Check(concurrent.Snapshot().Gems==160 && concurrent.Snapshot().FreeCredits==4,"concurrent completion consumes and awards once");
        concurrent.ResetAccount("delete");Check(concurrent.CharacterMissCompensation(replayToken)==null,"account reset clears miss receipt");
        Error("CardInvalid",delegate {concurrent.CompensateCharacterMiss(replayToken);});
        Check(concurrent.Snapshot().Gems==1800,"old-token replay cannot mint after account reset");
        var issued=concurrent.Reserve(1);string issuedToken=issued.Tokens[0];concurrent.Begin(issuedToken);concurrent.AuthorizeCard(issuedToken,Card("Common"));
        Error("CardAlreadyIssued",delegate {concurrent.CompensateCharacterMiss(issuedToken);});
        Check(concurrent.PendingCards().Count==1,"compensation cannot replace issued card");
        var normal=concurrent.Reserve(1);concurrent.Begin(normal.Tokens[0]);concurrent.EndFetch(normal.Tokens[0]);
        Error("CardInvalid",delegate {concurrent.CompensateCharacterMiss(normal.Tokens[0]);});
        Check(concurrent.Refund(normal.Tokens[0]).Gems==1800,"ordinary API failure/cancel can refund without compensation");
        var overflowClock=new TestClock();var overflow=overflowClock.Create(Path.Combine(root,"overflow"));FixtureFields(overflow,"Gems",1000000000);
        var pendingLimited=overflow.Reserve(1,"limitedGems","test-banner");var free=overflow.Reserve(1);overflow.Begin(free.Tokens[0]);
        Error("GemsLimit",delegate {overflow.CompensateCharacterMiss(free.Tokens[0]);});
        Check(overflow.CharacterMissCompensation(free.Tokens[0])==null,"failed compensation has no terminal receipt");
        Check(overflow.Refund(free.Tokens[0]).FreeCredits==5 && overflow.Refund(pendingLimited.Tokens[0]).Gems==1000000000,"compensation limit failure leaves both pending refunds intact");
    }
}
