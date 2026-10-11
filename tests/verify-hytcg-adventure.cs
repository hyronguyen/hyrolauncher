using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal static class VerifyHyTcgAdventure
{
    private static int checks;
    private static DateTime now=new DateTime(2026,10,11,0,0,0,DateTimeKind.Utc);
    private static readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=64*1024*1024};
    private static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    private static void Error(string code,Action run){try{run();throw new Exception("Expected "+code);}catch(HyTcgException e){Check(e.Code==code,code+" got "+e.Code);}}
    private static Dictionary<string,object> I(params object[] f){var d=new Dictionary<string,object>();for(int i=0;i<f.Length;i+=2)d.Add((string)f[i],f[i+1]);return d;}
    private static HyTcgEconomy New(string root){return new HyTcgEconomy(root,()=>now,new HyTcgUserProtector(),()=>TimeSpan.Zero);}
    private static Dictionary<string,object> Creative(HyTcgEconomy e){return json.Deserialize<Dictionary<string,object>>(json.Serialize(e.CreativeSnapshot()));}
    private static Dictionary<string,object>[] Rows(Dictionary<string,object> d,string k){return ((IEnumerable)d[k]).Cast<Dictionary<string,object>>().ToArray();}
    private static Dictionary<string,object> Intent(HyTcgEconomy e,params object[] f){var d=I(f);d["operationId"]="SC-"+Creative(e)["NextStudioSequence"]+"-"+Guid.NewGuid().ToString("N");return d;}
    private static HyTcgGameplayPage State(HyTcgEconomy e){return (HyTcgGameplayPage)e.GameplaySnapshot();}
    private static HyTcgGameplayActionResult Act(HyTcgEconomy e,string a,params object[] f){return (HyTcgGameplayActionResult)e.GameplayAction(a,I(f));}
    private static void Fixture(HyTcgEconomy e,Action<Dictionary<string,object>> edit){if(!e.VaultPath.Contains(Path.Combine("output","verification")))throw new Exception("Isolated fixture required");var p=new HyTcgUserProtector();var v=json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(p.Unprotect(File.ReadAllBytes(e.VaultPath))));edit(v);File.WriteAllBytes(e.VaultPath,p.Protect(Encoding.UTF8.GetBytes(json.Serialize(v))));}
    private static byte[] Picture(int w,int h,Func<int,int,Color> pixel){using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb))using(var o=new MemoryStream()){for(int y=0;y<h;y++)for(int x=0;x<w;x++)b.SetPixel(x,y,pixel(x,y));b.Save(o,ImageFormat.Png);return o.ToArray();}}
    private static HyTcgCard Card(HyTcgEconomy e,string root,int i){var c=new HyTcgCard{CardId="HT-20261011-"+i.ToString("X12"),CharacterId=300+i,Name="Adventure "+i,Series="Offline",Rarity="UR",RarityColor=HyTcgRules.RarityColor("UR"),CreatedAt=now.ToString("o"),ImageSource="acdb"};var r=e.Reserve(1);e.Begin(r.Tokens[0]);e.AuthorizeCard(r.Tokens[0],c);byte[] bytes=HyTcgStorage.Protect(Picture(750,1050,(x,y)=>Color.SlateBlue));string name=c.CardId+".hycard";Directory.CreateDirectory(Path.Combine(root,"cards"));File.WriteAllBytes(Path.Combine(root,"cards",name),bytes);e.Complete(r.Tokens[0],c,name,bytes);return c;}
    private static void Albums(HyTcgEconomy e,string root,HyTcgCard c)
    {
        Fixture(e,v=>v["Gems"]=100000);int initial=e.Snapshot().Gems;
        Error("AlbumPurchaseRequired",()=>e.CreativeAction("albumSave",I("name","Free bypass","cardIds",new string[0])));
        Error("StudioOperationInvalid",()=>e.CreativeAction("albumCreate",I("name","Free bypass")));
        var buy=Intent(e,"name","Adventure journal","cover","aurora","background","paper","capacity",120,"price",0);e.CreativeAction("albumCreate",buy);
        var album=Rows(Creative(e),"Albums").Single();string id=(string)album["Id"];
        Check(Convert.ToInt32(album["Capacity"])==4 && ((IEnumerable)album["CardIds"]).Cast<object>().Count()==4,"paid album has exactly 4 slots despite forged capacity");
        Check(e.Snapshot().Gems==initial-2000 && (string)album["Cover"]=="aurora" && (string)album["Background"]=="paper","native album purchase price/design");
        e.CreativeAction("albumCreate",buy);Check(e.Snapshot().Gems==initial-2000 && Rows(Creative(e),"Albums").Length==1,"album replay cannot duplicate or spend twice");
        buy["name"]="Changed";Error("StudioReplayMismatch",()=>e.CreativeAction("albumCreate",buy));
        var slots=Intent(e,"albumId",id,"count",9,"gems",0);e.CreativeAction("albumSlots",slots);e.CreativeAction("albumSlots",slots);
        Check(Convert.ToInt32(Rows(Creative(e),"Albums").Single()["Capacity"])==13 && e.Snapshot().Gems==initial-3800,"9 slots cost 1800 once");
        e.CreativeAction("albumSave",I("albumId",id,"name","Named","cardIds",new[]{c.CardId,"",""}));
        album=Rows(Creative(e),"Albums").Single();Check(((IEnumerable)album["CardIds"]).Cast<string>().First()==c.CardId && ((IEnumerable)album["CardIds"]).Cast<string>().Count()==13,"arrangement preserves paid capacity");
        Error("AlbumRestyleRequired",()=>e.CreativeAction("albumSave",I("albumId",id,"name","Named","background","sakura","cardIds",new[]{c.CardId})));
        var restyle=Intent(e,"albumId",id,"cover","sakura","background","forest");e.CreativeAction("albumRestyle",restyle);e.CreativeAction("albumRestyle",restyle);
        Check(e.Snapshot().Gems==initial-4000,"restyle price exactly200 and idempotent");
        e.CreativeAction("albumRestyle",Intent(e,"albumId",id,"cover","sakura","background","forest"));Check(e.Snapshot().Gems==initial-4000,"unchanged style does not charge");
        Error("GameplayInvalid",()=>e.CreativeAction("albumSlots",Intent(e,"albumId",id,"count",0)));
        Error("AlbumCardsLimit",()=>e.CreativeAction("albumSave",I("albumId",id,"name","Named","cardIds",Enumerable.Repeat("",14).ToArray())));
        Fixture(e,v=>v["Gems"]=0);Error("GemsInsufficient",()=>e.CreativeAction("albumCreate",Intent(e,"name","No balance")));Check(Rows(Creative(e),"Albums").Length==1,"failed purchase atomic");
        // Migrate the old album while preserving the user's existing slot order, no charge.
        Fixture(e,v=>{v["Gems"]=100000;var creative=(Dictionary<string,object>)v["Creative"];var a=((IEnumerable)creative["Albums"]).Cast<Dictionary<string,object>>().Single();a["Version"]=0;a["Capacity"]=0;a["Cover"]=null;a["CardIds"]=new[]{c.CardId,""};});
        album=Rows(Creative(New(root)),"Albums").Single();Check(Convert.ToInt32(album["Capacity"])==4 && ((IEnumerable)album["CardIds"]).Cast<string>().First()==c.CardId,"legacy album migration preserves owned arrangement");
    }
    private static void Artwork(HyTcgEconomy e,HyTcgCard c)
    {
        string art=e.StoreCustomArtwork(Picture(128,128,(x,y)=>Color.FromArgb(255,x*2,y*2,(x+y)%256)));
        byte[] bytes=Convert.FromBase64String(e.ReadCustomArtwork(art).Split(',')[1]);var colors=new HashSet<int>();
        bool paletteValid=true;
        using(var stream=new MemoryStream(bytes))using(var b=new Bitmap(stream)){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var p=b.GetPixel(x,y);colors.Add(p.ToArgb());paletteValid=paletteValid && p.R%85==0 && p.G%85==0 && p.B%85==0;}}
        Check(paletteValid,"every imported pixel uses the quantized RGB palette");
        Check(colors.Count<=64 && colors.Count>1,"imports use64 colors maximum");
        var quote=(HyTcgStudioQuote)e.CreativeQuote(I("cardId",c.CardId,"artworkId",art,"artworkPixels",1,"colors",1));
        Check(quote.ArtworkColors==colors.Count && quote.ArtworkPixels==16384 && quote.TotalGems==(16384*colors.Count+9999)/10000,"authenticated pixels times actualcolors ignored renderer counts");
        int before=e.Snapshot().Gems;var pay=Intent(e,"cardId",c.CardId,"artworkId",art);e.CreativeAction("cosmeticSave",pay);e.CreativeAction("cosmeticSave",pay);
        Check(e.Snapshot().Gems==before-quote.TotalGems,"color-weighted artwork charged once");Check(((HyTcgStudioQuote)e.CreativeQuote(I("cardId",c.CardId,"artworkId",art))).TotalGems==0,"same artwork free noop");
        string sparse=e.StoreCustomArtwork(Picture(100,100,(x,y)=>x<50?Color.Red:Color.Transparent));var sparseQuote=(HyTcgStudioQuote)e.CreativeQuote(I("cardId",c.CardId,"artworkId",sparse));
        Check(sparseQuote.ArtworkPixels==5000 && sparseQuote.ArtworkColors==1 && sparseQuote.TotalGems==1,"transparentpixels excluded and onecolor fee rounds once");
        string blank=e.StoreCustomArtwork(Picture(10,10,(x,y)=>Color.Transparent));var empty=(HyTcgStudioQuote)e.CreativeQuote(I("cardId",c.CardId,"artworkId",blank));Check(empty.TotalGems==0 && empty.ArtworkColors==0,"blank sketch haszero fee");
    }
    private static void Arena(HyTcgEconomy e,string root,HyTcgCard[] cards)
    {
        var daily=e.EnsureMissions(Enumerable.Range(0,3).Select(i=>new HyTcgMission{GameId="g"+i,Title="Game "+i,Kind="game"}).ToList());Check(daily.Items.Count==7,"7 daily missions");
        var p=State(e).Profiles.Single(x=>x.CardId==cards[0].CardId);Check(p.Skills.Count==7 && p.EquippedSkills.SequenceEqual(new[]{"strike","guard"}),"nativelevel1 locked skills");
        Error("SkillsInvalid",()=>Act(e,"skillsSave","cardId",p.CardId,"skillIds",new[]{"strike","nova"}));
        Fixture(e,v=>{var gameplay=(Dictionary<string,object>)v["Gameplay"];var progress=(Dictionary<string,object>)((Dictionary<string,object>)gameplay["Profiles"])[p.CardId];progress["Xp"]=9180;});
        Act(e,"skillsSave","cardId",p.CardId,"skillIds",new[]{"strike","guard","mend","nova"});Check(State(New(root)).Profiles.First(x=>x.CardId==p.CardId).EquippedSkills.Contains("nova"),"level18 learned/equipped skills persist");
        Error("SkillsInvalid",()=>Act(e,"skillsSave","cardId",p.CardId,"skillIds",new[]{"guard"}));
        int before=e.Snapshot().Gems;var start=Act(e,"fight","stage",1,"cardIds",cards.Select(x=>x.CardId).ToArray(),"win",true,"gems",999999);
        var b=start.State.Battle;Check(start.Result.Kind=="battleStart" && !b.Complete && b.Turn==1 && b.EnemyHp==145 && start.Wallet.Gems==before,"battle starts without auto payout");
        Check(cards.All(c=>!e.CanDissolve(c.CardId)) && State(e).Profiles.All(x=>x.Busy),"battle locks team");
        Error("BattleActive",()=>Act(e,"fight","stage",1,"cardIds",new[]{p.CardId}));Error("CardLocked",()=>Act(e,"date","cardId",p.CardId,"activity","chat"));
        Error("CardLocked",()=>Act(e,"skillsSave","cardId",p.CardId,"skillIds",new[]{"strike"}));
        Error("SkillUnavailable",()=>Act(e,"battleTurn","battleId",b.Id,"turn",b.Turn,"skillId","forged"));
        Error("BattleTurnExpired",()=>Act(e,"battleTurn","battleId",b.Id,"turn",b.Turn+1,"skillId","strike"));
        Error("BattleTargetInvalid",()=>Act(e,"battleTurn","battleId",b.Id,"turn",b.Turn,"skillId","strike","targetId",p.CardId));
        var guard=I("battleId",b.Id,"turn",b.Turn,"skillId","guard");e.GameplayAction("battleTurn",guard);var after=State(e).Battle;
        Check(after.Turn==2 && after.Events.Count==2 && after.Events[0].Kind=="guard" && after.Events[1].ActorId=="enemy","playerturn then opponent response");
        string stable=json.Serialize(after);e.GameplayAction("battleTurn",guard);Check(json.Serialize(State(e).Battle)==stable,"duplicate move haszero extra effect");
        Check(json.Serialize(State(New(root)).Battle)==stable,"activebattle survives restart");
        // Allies act in order; return to the trained unit to exercise support.
        Act(e,"battleTurn","battleId",after.Id,"turn",after.Turn,"skillId","guard");after=State(e).Battle;
        Act(e,"battleTurn","battleId",after.Id,"turn",after.Turn,"skillId","guard");after=State(e).Battle;
        var heal=Act(e,"battleTurn","battleId",after.Id,"turn",after.Turn,"skillId","mend","targetId",cards[1].CardId);Check(heal.State.Battle.Events[0].Kind=="heal" && heal.State.Battle.Team[0].Energy<6,"support skill usesenergy and heals teammate");
        HyTcgGameplayActionResult outcome=null;for(int i=0;i<60 && !State(e).Battle.Complete;i++){after=State(e).Battle;string skill=after.Team[after.Actor].Skills.Contains("nova") && after.Team[after.Actor].Energy>=4?"nova":"strike";outcome=Act(e,"battleTurn","battleId",after.Id,"turn",after.Turn,"skillId",skill);}
        Check(State(e).Battle.Complete && State(e).Battle.Win && outcome.Result.Win,"player choices complete nativevictory");
        Check(e.Snapshot().Gems==before+9+30 && e.Missions().Items.Single(m=>m.Kind=="arena").Complete,"victory plusonce30daily reward");Check(cards.All(c=>e.CanDissolve(c.CardId)),"completion unlocks cards");
        int awarded=e.Snapshot().Gems;Act(e,"battleForfeit","battleId",after.Id);Check(e.Snapshot().Gems==awarded,"finishedbattle cannotaward twice");
        var retry=Act(e,"fight","stage",1,"cardIds",new[]{p.CardId});Act(e,"battleForfeit","battleId",retry.State.Battle.Id);Check(State(e).Fight.DailyRemaining==1 && e.Snapshot().Gems==awarded,"forfeit consumesone attempt andnoreward");
        var date=Act(e,"date","cardId",p.CardId,"activity","gift");Check(e.Snapshot().Gems==awarded+30 && e.Missions().Items.Single(m=>m.Kind=="gift").Complete,"gift daily nativeonce");
        var trip=Act(e,"expeditionStart","routeId","meadow","cardIds",new[]{p.CardId});now=now.AddMinutes(5);int old=e.Snapshot().Gems;var claim=Act(e,"expeditionClaim","expeditionId",trip.Result.ExpeditionId);
        Check(claim.Wallet.Gems==old+claim.Result.GemsAwarded+30 && e.Missions().Items.Single(m=>m.Kind=="expedition").Complete,"completedexpedition daily30");
        Error("ExpeditionClaimed",()=>Act(e,"expeditionClaim","expeditionId",trip.Result.ExpeditionId));
        now=now.AddDays(1);var reset=e.EnsureMissions(new List<HyTcgMission>());Check(reset.Items.All(m=>!m.Complete) && State(e).Fight.DailyRemaining==3,"dailyreset persists progressandrefreshesrewards");
    }
    public static int Main(string[] args)
    {
        try{string root=Path.GetFullPath(args[0]);if(!root.Contains(Path.Combine("output","verification")))throw new Exception("Fixture root required");var e=New(root);var cards=Enumerable.Range(1,3).Select(i=>Card(e,root,i)).ToArray();Albums(e,root,cards[0]);Artwork(e,cards[0]);Arena(e,root,cards);
            var r=e.Reserve(1,"gems");e.Begin(r.Tokens[0]);int before=e.Snapshot().Gems;var miss=e.CompensateCharacterMiss(r.Tokens[0]);Check(miss.GemsAwarded==160 && e.Snapshot().Gems==before+160,"characterexhaustion compensates160");e.CompensateCharacterMiss(r.Tokens[0]);Check(e.Snapshot().Gems==before+160,"compensation nativeonce");
            Console.WriteLine("HYTCG adventure: "+checks+" checks passed. Fixture: "+root);File.WriteAllText(Path.Combine(root,"report.json"),json.Serialize(new{checks=checks,passed=true}));return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
