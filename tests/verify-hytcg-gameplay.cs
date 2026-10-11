using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal static class VerifyHyTcgGameplay
{
    private static int assertions;
    private sealed class TestClock
    {
        internal DateTime Now=new DateTime(2026,10,8,0,0,0,DateTimeKind.Utc);
        internal HyTcgEconomy Create(string root)
        { return new HyTcgEconomy(root,delegate {return Now;},new HyTcgUserProtector(),delegate {return TimeSpan.Zero;}); }
    }
    private static void Check(bool ok,string name) {assertions++;if(!ok)throw new Exception(name);}
    private static void Error(string code,Action run)
    { try {run();throw new Exception("Expected "+code);}catch(HyTcgException ex) {Check(ex.Code==code,"Expected "+code+" got "+ex.Code);} }
    private static Dictionary<string,object> Input(params object[] fields)
    {var input=new Dictionary<string,object>();for(int i=0;i<fields.Length;i+=2)input.Add((string)fields[i],fields[i+1]);return input;}
    private static HyTcgGameplayActionResult Action(HyTcgEconomy economy,string kind,params object[] fields)
    {var result=(HyTcgGameplayActionResult)economy.GameplayAction(kind,Input(fields));if(kind=="fight")for(int i=0;i<60 && !result.State.Battle.Complete;i++){var b=result.State.Battle;result=(HyTcgGameplayActionResult)economy.GameplayAction("battleTurn",Input("battleId",b.Id,"turn",b.Turn,"skillId","strike"));}return result;}
    private static HyTcgGameplayPage State(HyTcgEconomy economy) {return (HyTcgGameplayPage)economy.GameplaySnapshot();}
    private static byte[] Png()
    {
        using(var bitmap=new Bitmap(750,1050))using(var output=new MemoryStream())
        {bitmap.Save(output,ImageFormat.Png);return output.ToArray();}
    }
    private static HyTcgCard Issue(HyTcgEconomy economy,string root,string rarity,int character,byte[] png)
    {
        var reserved=economy.Reserve(1);string token=reserved.Tokens[0];economy.Begin(token);
        var card=new HyTcgCard {CardId="HT-20261008-"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant(),CharacterId=character,Name="Local companion "+character,
            Rarity=rarity,RarityColor=HyTcgRules.RarityColor(rarity),Series="Native test",CreatedAt="2026-10-08T00:00:00Z",Gender="Female"};
        economy.AuthorizeCard(token,card);string file=rarity+"_"+card.CardId+".png";Directory.CreateDirectory(Path.Combine(root,"cards"));File.WriteAllBytes(Path.Combine(root,"cards",file),png);
        economy.Complete(token,card,file,png);return card;
    }
    private static void Fixture(string root,Action<Dictionary<string,object>> change)
    {
        var parser=new JavaScriptSerializer {MaxJsonLength=64*1024*1024};var protector=new HyTcgUserProtector();string file=Path.Combine(root,"hytcg-vault.dat");
        var vault=parser.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(protector.Unprotect(File.ReadAllBytes(file))));change(vault);
        File.WriteAllBytes(file,protector.Protect(Encoding.UTF8.GetBytes(parser.Serialize(vault))));
    }
    private static void TestNativeState(string root,byte[] png)
    {
        var clock=new TestClock();var economy=clock.Create(root);int gems=economy.Snapshot().Gems;
        var starter=State(economy);Check(starter.Inventory.Single(i=>i.Id=="training").Count==3,"starter manuals");
        Check(starter.Inventory.Single(i=>i.Id=="tea").Count==3 && starter.Inventory.Single(i=>i.Id=="gift").Count==1,"starter date supplies");
        Check(starter.Profiles.Count==0 && economy.Snapshot().Gems==gems,"migration leaves balances and ownership intact");
        Check(starter.Routes.Count==3 && starter.Fight.DailyRemaining==3 && starter.DateDailyRemaining==6 && starter.ExpeditionStartsRemaining==6,"bounded daily defaults");
        var cards=new List<HyTcgCard> {Issue(economy,root,"UR",71,png),Issue(economy,root,"UR",72,png),Issue(economy,root,"UR",73,png),Issue(economy,root,"Common",74,png),Issue(economy,root,"Uncommon",74,png)};
        Check(State(economy).Profiles.Count==5,"profiles native ownership");string first=cards[0].CardId;
        var before=State(economy).Profiles.Single(p=>p.CardId==first);var trained=Action(economy,"inventoryUse","itemId","training","cardId",first,"xp",999999);
        var after=trained.State.Profiles.Single(p=>p.CardId==first);
        Check(after.Xp==80 && after.Level==2 && after.Attack>before.Attack && after.Effort>before.Effort,"manual native xp and stat growth");
        Check(trained.Wallet.Gems==gems && trained.State.Inventory.Single(i=>i.Id=="training").Count==2,"manual consume atomic and ignores forged xp");
        Error("ItemInvalid",delegate {Action(economy,"date","cardId",first,"activity","tea","itemId","gift");});
        Check(State(economy).Inventory.Single(i=>i.Id=="tea").Count==3 && State(economy).DateDailyRemaining==6,"invalid date does not consume");
        var chat=Action(economy,"date","cardId",first,"activity","chat","bond",1000);Check(chat.Result.BondAwarded>=12 && chat.Result.BondAwarded<=18,"native chat affinity");
        var tea=Action(economy,"inventoryUse","cardId",first,"itemId","tea");Check(tea.Result.Kind=="date" && tea.State.Inventory.Single(i=>i.Id=="tea").Count==2,"tea use capped dating");
        Check(tea.State.Profiles.Single(p=>p.CardId==first).DatesRemaining==0,"per companion date cap");
        Error("DateDailyLimit",delegate {Action(economy,"date","cardId",first,"activity","chat");});
        var started=Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {first},"rewardGems",999999);string expedition=started.Result.ExpeditionId;
        var trip=started.State.Expeditions.Single(e=>e.Id==expedition);Check(trip.RewardGems<=12 && !trip.Ready && trip.CardIds.Count==1,"native exact expedition reward");
        Check(started.State.Profiles.Single(p=>p.CardId==first).Busy && !economy.CanDissolve(first),"expedition card lock");
        Error("CardLocked",delegate {economy.Dissolve(first);});
        Error("CardLocked",delegate {Action(economy,"inventoryUse","cardId",first,"itemId","training");});
        Error("CardLocked",delegate {Action(economy,"fight","stage",1,"cardIds",new[] {first});});
        Error("ExpeditionNotReady",delegate {Action(economy,"expeditionClaim","expeditionId",expedition);});
        clock.Now=clock.Now.AddSeconds(300);Check(State(economy).Expeditions.Single(e=>e.Id==expedition).Ready,"native deadline readiness");
        var receipt=Action(economy,"expeditionClaim","expeditionId",expedition);Check(receipt.Wallet.Gems==gems+trip.RewardGems+30 && receipt.Result.GemsAwarded==trip.RewardGems,"native reward and once-daily mission paid");
        Check(!receipt.State.Profiles.Single(p=>p.CardId==first).Busy && receipt.Result.XpAwarded==20,"claim xp unlock");
        Error("ExpeditionClaimed",delegate {Action(economy,"expeditionClaim","expeditionId",expedition);});
        Check(economy.Snapshot().Gems==receipt.Wallet.Gems,"once only expedition payout");
        Error("TeamInvalid",delegate {Action(economy,"fight","stage",1,"cardIds",new[] {first,first});});
        Error("FightStageLocked",delegate {Action(economy,"fight","stage",20,"cardIds",new[] {first});});
        Error("GameplayInvalid",delegate {Action(economy,"fight","stage",1.5,"cardIds",new[] {first});});
        var team=cards.Take(3).Select(c=>c.CardId).ToArray();var battle=Action(economy,"fight","stage",1,"cardIds",team,"win",true,"reward",999999);
        Check(battle.Result.Win && battle.Result.GemsAwarded==9 && battle.Result.CombatLog.Count>0 && battle.Result.Rounds<=20,"native turn based combat outcome");
        Check(battle.State.Fight.Stage==2 && battle.State.Fight.DailyRemaining==2 && battle.State.Fight.LastResult.Win,"progression and last result");
        Check(battle.Wallet.Gems==receipt.Wallet.Gems+9+30 && battle.Result.XpAwarded==66,"fight native currency, daily reward and xp");
        Action(economy,"fight","stage",1,"cardIds",team);Action(economy,"fight","stage",1,"cardIds",team);
        Error("FightDailyLimit",delegate {Action(economy,"fight","stage",1,"cardIds",team);});
        // Rarity upgrade replaces card metadata but retains this durable ID's progression.
        string target=cards[3].CardId,material=cards[4].CardId;Action(economy,"inventoryUse","cardId",target,"itemId","training");Action(economy,"date","cardId",target,"activity","chat");
        var profile=State(economy).Profiles.Single(p=>p.CardId==target);var upgrade=economy.PrepareUpgrade(target,new List<string> {material},false);
        Error("CardLocked",delegate {Action(economy,"date","cardId",target,"activity","chat");});
        economy.BeginUpgrade(upgrade.Token);economy.AuthorizeUpgrade(upgrade.Token,upgrade.Card);string next="upgrade_"+target+".png";File.WriteAllBytes(Path.Combine(root,"cards",next),png);
        economy.CompleteUpgrade(upgrade.Token,upgrade.Card,next,png);var upgraded=State(economy).Profiles.Single(p=>p.CardId==target);
        Check(upgraded.Xp==profile.Xp && upgraded.Bond==profile.Bond && upgraded.Attack>profile.Attack,"upgrade retains xp bond and improves native stats");
        Check(State(economy).Profiles.All(p=>p.CardId!=material),"consumed material not playable");
        var reloaded=clock.Create(root);Check(State(reloaded).Fight.DailyRemaining==0 && State(reloaded).Profiles.Single(p=>p.CardId==target).Bond==profile.Bond,"restart durability");
        clock.Now=clock.Now.Date.AddHours(16).AddMinutes(59);State(reloaded);Check(State(reloaded).Fight.DailyRemaining==0,"before UTC7 reset capped");
        clock.Now=clock.Now.AddMinutes(1);var reset=State(reloaded);
        Check(reset.Day=="2026-10-09" && reset.Fight.DailyRemaining==3 && reset.DateDailyRemaining==6 && reset.ExpeditionStartsRemaining==6,"UTC7 daily reset");
        Check(reset.Profiles.Single(p=>p.CardId==target).Bond==profile.Bond && reset.Profiles.Single(p=>p.CardId==target).DatesRemaining==2,"daily reset keeps bond and refreshes per card limits");
        clock.Now=clock.Now.AddDays(-2);Check(State(reloaded).Day=="2026-10-09","clock rollback cannot refresh limits");
        reloaded.ResetAccount("delete");var deleted=State(reloaded);Check(deleted.Profiles.Count==0 && deleted.Expeditions.Count==0 && deleted.History.Count==0 && deleted.Inventory.Single(i=>i.Id=="training").Count==3,"account reset clears gameplay and renews starter supplies");
    }
    private static void TestLimitsAndConcurrency(string root,byte[] png)
    {
        var clock=new TestClock();var economy=clock.Create(root);var cards=new[] {Issue(economy,root,"UR",81,png),Issue(economy,root,"UR",82,png),Issue(economy,root,"UR",83,png)};
        var trips=new List<string>();foreach(var card in cards)trips.Add(Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {card.CardId}).Result.ExpeditionId);
        Error("ExpeditionActiveLimit",delegate {Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {cards[0].CardId});});
        clock.Now=clock.Now.AddSeconds(300);int initial=economy.Snapshot().Gems,paid=0,duplicate=0;
        Parallel.For(0,2,delegate(int index) {
            try {Action(economy,"expeditionClaim","expeditionId",trips[0]);System.Threading.Interlocked.Increment(ref paid);}
            catch(HyTcgException ex) {if(ex.Code!="ExpeditionClaimed")throw;System.Threading.Interlocked.Increment(ref duplicate);}
        });
        Check(paid==1 && duplicate==1 && economy.Snapshot().Gems-initial<=42,"concurrent expedition plus daily claim exactly once");
        foreach(string id in trips.Skip(1))Action(economy,"expeditionClaim","expeditionId",id);
        for(int i=0;i<3;i++)
        {
            string id=Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {cards[0].CardId}).Result.ExpeditionId;clock.Now=clock.Now.AddSeconds(300);Action(economy,"expeditionClaim","expeditionId",id);
        }
        Error("ExpeditionDailyLimit",delegate {Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {cards[0].CardId});});
        Check(State(economy).ExpeditionStartsRemaining==0,"six starts daily cap");
        foreach(var card in cards) {Action(economy,"date","cardId",card.CardId,"activity","chat");Action(economy,"date","cardId",card.CardId,"activity","chat");}
        Check(State(economy).DateDailyRemaining==0,"account date cap");
        Error("DateDailyLimit",delegate {Action(economy,"date","cardId",cards[0].CardId,"activity","gift");});
        // Preserve an active expedition across reset and process restart.
        clock.Now=clock.Now.Date.AddDays(1);string active=Action(economy,"expeditionStart","routeId","ruins","cardIds",cards.Select(c=>c.CardId).ToArray()).Result.ExpeditionId;
        var restart=clock.Create(root);Check(State(restart).Profiles.All(p=>p.Busy),"restart preserves active locks");
        clock.Now=clock.Now.AddDays(1);Check(State(restart).ExpeditionStartsRemaining==6 && State(restart).Profiles.All(p=>p.Busy),"daily reset retains active locks");
        var result=Action(restart,"expeditionClaim","expeditionId",active);Check(result.Result.GemsAwarded>=30 && result.Result.GemsAwarded<=45 && result.State.Inventory.Single(i=>i.Id=="gift").Count==2,"long expedition native gift and bounded reward");
    }
    private static void TestValidationAndCaps(string root,byte[] png)
    {
        var clock=new TestClock();var economy=clock.Create(root);var card=Issue(economy,root,"UR",91,png);State(economy);
        string trip=Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {card.CardId}).Result.ExpeditionId;clock.Now=clock.Now.AddSeconds(300);
        Fixture(root,delegate(Dictionary<string,object> vault) {var state=(Dictionary<string,object>)vault["Gameplay"];var inventory=(Dictionary<string,object>)state["Inventory"];inventory["training"]=999;inventory["fiber"]=999;});
        var full=Action(economy,"expeditionClaim","expeditionId",trip);Check(full.State.Inventory.Single(i=>i.Id=="training").Count==999 && full.Result.Items.Count==0,"full stack preserves claim and reports exact received items");
        Fixture(root,delegate(Dictionary<string,object> vault) {var state=(Dictionary<string,object>)vault["Gameplay"];var profiles=(Dictionary<string,object>)state["Profiles"];var progress=(Dictionary<string,object>)profiles[card.CardId];progress["Xp"]=26100;progress["Bond"]=1000;});
        var maximum=State(economy).Profiles.Single();Check(maximum.Level==30 && maximum.BondLevel==5 && maximum.XpToNextLevel==0,"bounded max progression");
        Error("LevelMaximum",delegate {Action(economy,"inventoryUse","cardId",card.CardId,"itemId","training");});
        Error("BondMaximum",delegate {Action(economy,"date","cardId",card.CardId,"activity","chat");});
        int supplies=State(economy).Inventory.Single(i=>i.Id=="training").Count;
        Fixture(root,delegate(Dictionary<string,object> vault) {vault["Gems"]=1000000000;});
        string capped=Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {card.CardId}).Result.ExpeditionId;clock.Now=clock.Now.AddSeconds(300);
        Error("GemsLimit",delegate {Action(economy,"expeditionClaim","expeditionId",capped);});
        Check(!State(economy).Expeditions.Single(e=>e.Id==capped).Claimed && State(economy).Inventory.Single(i=>i.Id=="training").Count==supplies,"failed award atomic keeps claim and inventory");
        Fixture(root,delegate(Dictionary<string,object> vault) {var state=(Dictionary<string,object>)vault["Gameplay"];var inventory=(Dictionary<string,object>)state["Inventory"];inventory["tea"]=-1;});
        Error("VaultInvalid",delegate {State(economy);});
    }
    private static void TestFailureRewards(string root,byte[] png)
    {
        var clock=new TestClock();var economy=clock.Create(root);var card=Issue(economy,root,"Common",101,png);State(economy);
        Error("EffortInsufficient",delegate {Action(economy,"expeditionStart","routeId","ruins","cardIds",new[] {card.CardId});});
        Error("RouteInvalid",delegate {Action(economy,"expeditionStart","routeId","forged","cardIds",new[] {card.CardId});});
        Error("CardUnavailable",delegate {Action(economy,"date","cardId","HT-20261008-FFFFFFFFFFFF","activity","chat");});
        Check(State(economy).ExpeditionStartsRemaining==6 && State(economy).DateDailyRemaining==6,"invalid requests preserve daily quotas");
        Fixture(root,delegate(Dictionary<string,object> vault) {var state=(Dictionary<string,object>)vault["Gameplay"];state["HighestClearedStage"]=19;});
        int gems=economy.Snapshot().Gems;var defeat=Action(economy,"fight","stage",20,"cardIds",new[] {card.CardId},"win",true,"gems",999999);
        Check(!defeat.Result.Win && defeat.Result.EnemyHp>0 && defeat.Result.GemsAwarded==0 && defeat.Wallet.Gems==gems,"defeat never awards renderer supplied gems or win");
        Check(defeat.Result.XpAwarded==5 && defeat.State.Fight.DailyRemaining==2 && defeat.State.Fight.HighestClearedStage==19,"defeat small practice xp and attempt cost");
        // Each inventory unit is an atomic native spend, including repeat actions.
        for(int i=0;i<3;i++)Action(economy,"inventoryUse","cardId",card.CardId,"itemId","training");
        Error("ItemInsufficient",delegate {Action(economy,"inventoryUse","cardId",card.CardId,"itemId","training");});
        Check(State(economy).Profiles.Single().Xp==245,"inventory cannot be reused after depletion");
    }
    private static void TestSharedLocks(string root,byte[] png)
    {
        var clock=new TestClock();var economy=clock.Create(root);var target=Issue(economy,root,"SSR",121,png);var material=Issue(economy,root,"UR",121,png);
        economy.CreativeAction("cosmeticSave",Input("cardId",target.CardId,"frame","classic","operationId","SC-1-"+Guid.NewGuid().ToString("N")));
        economy.CreativeSnapshot();Fixture(root,vault=> {var state=(Dictionary<string,object>)vault["Creative"];var list=(IList)state["Albums"];list.Add(Input("Id","AL-"+Guid.NewGuid().ToString("N"),"Name","Shared state fixture","Background","sakura","CardIds",new[]{target.CardId,material.CardId},"CreatedAt",clock.Now.ToString("o")));});
        string expedition=Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {target.CardId,material.CardId}).Result.ExpeditionId;
        Error("CardLocked",delegate {economy.PrepareUpgrade(target.CardId,new List<string> {material.CardId},false);});
        Error("CardLocked",delegate {economy.CreativeAction("cosmeticReset",Input("cardId",target.CardId));});
        clock.Now=clock.Now.AddSeconds(300);Action(economy,"expeditionClaim","expeditionId",expedition);
        var upgrade=economy.PrepareUpgrade(target.CardId,new List<string> {material.CardId},false);
        Error("CardLocked",delegate {Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {target.CardId});});
        Error("CardLocked",delegate {Action(economy,"expeditionStart","routeId","meadow","cardIds",new[] {material.CardId});});
        Error("CardLocked",delegate {economy.CreativeAction("cosmeticReset",Input("cardId",material.CardId));});
        economy.CancelUpgrade(upgrade.Token);
        Check(State(economy).Profiles.All(p=>!p.Busy),"upgrade cancellation releases gameplay and creative locks");
        economy.PrepareUpgrade(target.CardId,new List<string> {material.CardId},false);
        Fixture(root,delegate(Dictionary<string,object> vault) {
            var state=(Dictionary<string,object>)vault["Gameplay"];var expeditions=(IList)state["Expeditions"];var record=(Dictionary<string,object>)expeditions[0];record["Claimed"]=false;record["ClaimedAt"]="";
        });
        Error("VaultInvalid",delegate {State(economy);});
    }
    private static void TestBoundedRoster(string root,byte[] png)
    {
        var clock=new TestClock();var economy=clock.Create(root);var oldest=Issue(economy,root,"Common",131,png);
        // Build native-signed metadata only in this temporary test account. The extra
        // 500 records intentionally have no PNG files, proving roster views stay lazy.
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;var state=typeof(HyTcgEconomy).GetMethod("Load",flags).Invoke(economy,new object[0]);
        var owned=(IList)state.GetType().GetProperty("Cards").GetValue(state,null);var create=typeof(HyTcgEconomy).GetMethod("CreateOwned",BindingFlags.NonPublic|BindingFlags.Static);
        var parser=new JavaScriptSerializer();
        for(int i=1;i<=500;i++)
        {
            var card=parser.Deserialize<HyTcgCard>(parser.Serialize(oldest));card.CardId="HT-20261008-"+i.ToString("X12");card.CharacterId=200+i;card.Name="Paged companion "+i.ToString("D4");
            owned.Add(create.Invoke(null,new object[] {state,card,"fixture_"+i+".png",png,false}));
        }
        typeof(HyTcgEconomy).GetMethod("Persist",flags).Invoke(economy,new[] {state});
        var page=State(economy);Check(page.Profiles.Count==500 && page.ProfilesTotal==501 && page.ProfilesHasMore,"snapshot roster bounded to500 metadata");
        Check(page.Profiles[0].Name=="Paged companion 0500" && page.Profiles.All(p=>p.CardId!=oldest.CardId),"default roster newest first");
        Fixture(root,delegate(Dictionary<string,object> vault) {var game=(Dictionary<string,object>)vault["Gameplay"];Check(((Dictionary<string,object>)game["Profiles"]).Count==500,"progress records initialize only viewed cards");});
        var remainder=(HyTcgGameplayRosterPage)economy.GameplayRoster("",500,100);
        Check(remainder.Total==501 && remainder.Items.Count==1 && remainder.Items[0].CardId==oldest.CardId && remainder.Offset==500 && remainder.NextOffset==501 && !remainder.HasMore,"roster pagination reaches cards beyond500");
        var found=(HyTcgGameplayRosterPage)economy.GameplayRoster("paged companion 0001",0,25);
        Check(found.Items.Count==1 && found.Total==1 && found.Items[0].CharacterId==201,"roster case insensitive native search without PNG");
        var selected=(HyTcgGameplayProfile)economy.GameplayProfileSnapshot(oldest.CardId);Check(selected.Level==1,"exact selected profile beyond500");
        var action=Action(economy,"inventoryUse","cardId",oldest.CardId,"itemId","training");
        var result=(HyTcgGameplayProfile)economy.GameplayProfileSnapshot(oldest.CardId);
        Check(result.Xp==80 && action.State.Profiles.Count==500 && action.State.Profiles.All(p=>p.CardId!=oldest.CardId),"actions accept authenticated selection beyond current page");
        Error("GameplayInvalid",delegate {economy.GameplayRoster("",0,501);});
        Error("GameplayInvalid",delegate {economy.GameplayRoster(new string('x',81),0,25);});
        Error("GameplayInvalid",delegate {economy.GameplayRoster("",-1,25);});
    }
    private static void TestFrameMaterials(string root,byte[] png)
    {
        var clock=new TestClock();var economy=clock.Create(root);var cards=new[] {Issue(economy,root,"UR",301,png),Issue(economy,root,"UR",302,png),Issue(economy,root,"UR",303,png)};
        var initial=State(economy);Check(initial.Inventory.Where(i=>i.Kind=="material").Count()==3 && initial.Inventory.Where(i=>i.Kind=="material").All(i=>i.Count==0),"crafting resources start empty without gifting currency");
        var trained=Action(economy,"inventoryUse","itemId","training","cardId",cards[0].CardId);int xp=trained.State.Profiles.Single(p=>p.CardId==cards[0].CardId).Xp;
        Fixture(root,delegate(Dictionary<string,object> vault) {var state=(Dictionary<string,object>)vault["Gameplay"];state["Version"]=1;var inventory=(Dictionary<string,object>)state["Inventory"];inventory.Remove("fiber");inventory.Remove("petal");inventory.Remove("crystal");});
        var migrated=State(clock.Create(root));Check(migrated.Inventory.Count==6 && migrated.Inventory.Single(i=>i.Id=="training").Count==2 && migrated.Profiles.Single(p=>p.CardId==cards[0].CardId).Xp==xp,"version1 migration preserves supplies and progression and adds zero resources");
        Check(economy.Snapshot().Gems==1800 && migrated.ExpeditionStartsRemaining==6,"inventory migration preserves wallet and quotas");
        string[] routes={"meadow","observatory","ruins"},resources={"fiber","petal","crystal"};
        for(int i=0;i<routes.Length;i++)
        {
            var definition=migrated.Routes.Single(r=>r.Id==routes[i]);Check(definition.RewardItems.Single(item=>item.Kind=="material").Id==resources[i] && definition.RewardItems.Single(item=>item.Kind=="material").Count==2,"route declares exact crafting resource: "+routes[i]);
            string expedition=Action(economy,"expeditionStart","routeId",routes[i],"cardIds",cards.Select(c=>c.CardId).ToArray()).Result.ExpeditionId;
            clock.Now=clock.Now.AddSeconds(definition.DurationSeconds);var claimed=Action(economy,"expeditionClaim","expeditionId",expedition,"resource",999999);
            Check(claimed.Result.Items.Single(item=>item.Id==resources[i]).Count==2 && claimed.State.Inventory.Single(item=>item.Id==resources[i]).Count==2,"native claim awards exact resource and ignores supplied count: "+resources[i]);
            Error("ExpeditionClaimed",delegate {Action(economy,"expeditionClaim","expeditionId",expedition);});
            Check(State(economy).Inventory.Single(item=>item.Id==resources[i]).Count==2,"resource claim is once-only: "+resources[i]);
            int before=State(economy).Profiles.Single(p=>p.CardId==cards[0].CardId).Xp;
            Error("ItemInvalid",delegate {Action(economy,"inventoryUse","itemId",resources[i],"cardId",cards[0].CardId);});
            Check(State(economy).Inventory.Single(item=>item.Id==resources[i]).Count==2 && State(economy).Profiles.Single(p=>p.CardId==cards[0].CardId).Xp==before,"materials cannot be converted into XP via inventoryUse");
        }
        string capped=Action(economy,"expeditionStart","routeId","meadow","cardIds",new[]{cards[0].CardId}).Result.ExpeditionId;
        Fixture(root,delegate(Dictionary<string,object> vault) {var state=(Dictionary<string,object>)vault["Gameplay"];((Dictionary<string,object>)state["Inventory"])["fiber"]=998;});
        clock.Now=clock.Now.AddSeconds(300);var cappedReceipt=Action(economy,"expeditionClaim","expeditionId",capped);
        Check(cappedReceipt.State.Inventory.Single(i=>i.Id=="fiber").Count==999 && cappedReceipt.Result.Items.Single(i=>i.Id=="fiber").Count==1,"resource stack cap reports actual award without trapping claims");
        var restart=State(clock.Create(root));Check(restart.Inventory.Single(i=>i.Id=="fiber").Count==999 && restart.Inventory.Single(i=>i.Id=="petal").Count==2,"crafting resources persist across account restart");
        Fixture(root,delegate(Dictionary<string,object> vault) {var state=(Dictionary<string,object>)vault["Gameplay"];((Dictionary<string,object>)state["Inventory"])["crystal"]=1000;});
        Error("VaultInvalid",delegate {State(economy);});
    }
    internal static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);byte[] png=Png();
            TestNativeState(Path.Combine(root,"native"),png);TestLimitsAndConcurrency(Path.Combine(root,"limits"),png);TestValidationAndCaps(Path.Combine(root,"validation"),png);TestFailureRewards(Path.Combine(root,"failure"),png);
            TestSharedLocks(Path.Combine(root,"shared-locks"),png);TestBoundedRoster(Path.Combine(root,"roster"),png);TestFrameMaterials(Path.Combine(root,"frame-materials"),png);
            Console.WriteLine("HYTCG gameplay passed: "+assertions+" assertions; native stats, inventory, expeditions, concurrent claims, PvE, affinity, upgrades, UTC+7 resets, shared creative locks and bounded searchable lazy roster.");return 0;
        }
        catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
