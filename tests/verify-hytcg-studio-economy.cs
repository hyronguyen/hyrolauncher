using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal static class VerifyHyTcgStudioEconomy
{
    private static int checks;
    private static readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=32*1024*1024};
    private static void Check(bool ok,string message) {checks++;if(!ok)throw new Exception(message);}
    private static void Error(string code,Action action)
    {try {action();throw new Exception("Expected "+code);}catch(HyTcgException ex){Check(ex.Code==code,code+" got "+ex.Code);}}
    private static Dictionary<string,object> Input(params object[] values)
    {var d=new Dictionary<string,object>();for(int i=0;i<values.Length;i+=2)d[(string)values[i]]=values[i+1];return d;}
    private static Dictionary<string,object> Snapshot(HyTcgEconomy e)
    {return json.Deserialize<Dictionary<string,object>>(json.Serialize(e.CreativeSnapshot()));}
    private static Dictionary<string,object>[] Items(Dictionary<string,object> s,string key)
    {return ((IEnumerable)s[key]).Cast<Dictionary<string,object>>().ToArray();}
    private static string Operation(HyTcgEconomy e)
    {return "SC-"+Snapshot(e)["NextStudioSequence"]+"-"+Guid.NewGuid().ToString("N");}
    private static Dictionary<string,object> Intent(HyTcgEconomy e,params object[] values)
    {var d=Input(values);d["operationId"]=Operation(e);return d;}
    private static byte[] Picture(Color color)
    {using(var b=new Bitmap(750,1050))using(var g=Graphics.FromImage(b))using(var o=new MemoryStream()){g.Clear(color);b.Save(o,ImageFormat.Png);return o.ToArray();}}
    private static byte[] SparsePicture(int count)
    {using(var b=new Bitmap(100,100,PixelFormat.Format32bppArgb))using(var o=new MemoryStream()){for(int i=0;i<count;i++)b.SetPixel(i%100,i/100,Color.FromArgb(i==0?1:255,150,30,80));b.Save(o,ImageFormat.Png);return o.ToArray();}}
    private static HyTcgCard Add(HyTcgEconomy e,string root,int number)
    {
        var c=new HyTcgCard {CardId="HT-20261010-"+number.ToString("X12"),CharacterId=555,Name="Studio economy fixture",Series="Offline",Rarity="Common",RarityColor=HyTcgRules.RarityColor("Common"),CreatedAt="2026-10-10T00:00:00Z",ImageSource="acdb"};
        var r=e.Reserve(1);e.Begin(r.Tokens[0]);e.AuthorizeCard(r.Tokens[0],c);byte[] bytes=HyTcgStorage.Protect(Picture(Color.SlateBlue));
        string file=c.CardId+".hycard";Directory.CreateDirectory(Path.Combine(root,"cards"));File.WriteAllBytes(Path.Combine(root,"cards",file),bytes);e.Complete(r.Tokens[0],c,file,bytes);return c;
    }
    private static int DyeCount(HyTcgEconomy e,string id)
    {var d=Items(Snapshot(e),"Dyes").FirstOrDefault(x=>(string)x["Id"]==id);return d==null?0:Convert.ToInt32(d["Count"]);}
    private static void FixtureVault(HyTcgEconomy e,Action<Dictionary<string,object>> edit)
    {
        if(e.VaultPath.IndexOf("output"+Path.DirectorySeparatorChar+"verification",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Fixture vault path required");
        var p=new HyTcgUserProtector();var v=json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(p.Unprotect(File.ReadAllBytes(e.VaultPath))));edit(v);
        File.WriteAllBytes(e.VaultPath,p.Protect(Encoding.UTF8.GetBytes(json.Serialize(v))));
    }
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);if(root.IndexOf("output"+Path.DirectorySeparatorChar+"verification",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Fixture path required");
            DateTime now=new DateTime(2026,10,10,1,0,0,DateTimeKind.Utc);var e=new HyTcgEconomy(root,()=>now,new HyTcgUserProtector(),()=>TimeSpan.Zero);
            var s=Snapshot(e);Check(((IEnumerable)s["OwnedFrameIds"]).Cast<string>().SequenceEqual(new[]{"classic"}),"Classic is only starter ownership");
            Check(Items(s,"DyePalette").Length==64 && Items(s,"DyePalette").Select(x=>x["Color"]).Distinct().Count()==64,"64 unique native palette colors");
            Check(Enumerable.Range(0,100).Count(i=>HyTcgEconomy.RollFrameDye(12,i).Mystic)==1,"one of 100 native rolls is Mystic");
            Check(HyTcgEconomy.RollFrameDye(63,0).Id=="dye-63-mystic" && HyTcgEconomy.RollFrameDye(63,1).Id=="dye-63","same base color has ordinary and Mystic quantities");
            Error("DyeInvalid",()=>HyTcgEconomy.RollFrameDye(64,0));Error("DyeInvalid",()=>HyTcgEconomy.RollFrameDye(0,100));
            var frames=HyTcgFrameCatalog.All();Check(frames.Count>=10 && frames.All(HyTcgFrameCatalog.Valid),"catalog supplies valid portrait and landscape layouts");
            Check(frames.All(f=>f.PriceGems==2000 && f.Materials.Sum(m=>m.Count)==20),"all frame templates use fixed native creation base and recipe");
            Check(frames.Where(f=>f.Id!="classic").All(f=>f.PriceGems>0 && f.Materials.Count>0),"every nonclassic design requires native gems and materials");
            Check(frames.Where(f=>!HyTcgFrameCatalog.Legacy(f.Id)).Select(f=>json.Serialize(f.Portrait)).Distinct().Count()==5,"designed frames have distinct identity and art layouts");
            var c=Add(e,root,1);var teammate=Add(e,root,2);string identity=json.Serialize(e.ReadOwnedCard(c.CardId));string core=Path.Combine(root,"cards",c.CardId+".hycard");byte[] coreBytes=File.ReadAllBytes(core);
            int initial=e.Snapshot().Gems;
            Error("StudioOperationInvalid",()=>e.CreativeAction("dyeBoosterBuy",Input()));
            Error("FrameNotOwned",()=>e.CreativeAction("cosmeticSave",Intent(e,"cardId",c.CardId,"frame","petal")));
            Error("DyeInvalid",()=>e.CreativeAction("cosmeticSave",Intent(e,"cardId",c.CardId,"dye","#AABBCC")));
            Error("DyeInsufficient",()=>e.CreativeAction("cosmeticSave",Intent(e,"cardId",c.CardId,"dyeId","dye-00")));
            Error("FrameCreationRequired",()=>e.CreativeAction("frameBuy",Intent(e,"frameId","petal")));
            Error("MaterialInsufficient",()=>e.CreativeAction("frameCreate",Intent(e,"frameId","petal")));
            Error("FrameInvalid",()=>e.CreativeAction("frameCreate",Intent(e,"portrait",new HyTcgFrameLayout {ArtworkX=-1})));
            Error("ArtworkInvalid",()=>e.CreativeAction("frameCreate",Intent(e,"artworkId","../outside.png")));
            foreach(double hostile in new[]{Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity,-.001,1.001})
            {
                var bad=json.Deserialize<HyTcgFrameLayout>(json.Serialize(frames[0].Portrait));bad.NameX=hostile;
                Check(!HyTcgFrameCatalog.LayoutValid(bad),"layout rejects nonfinite and outside-card coordinates");
            }
            var tooSmall=json.Deserialize<HyTcgFrameLayout>(json.Serialize(frames[0].Portrait));tooSmall.NumberWidth=.009;
            Check(!HyTcgFrameCatalog.LayoutValid(tooSmall),"layout rejects unreadable undersized box");
            var displayText=json.Deserialize<HyTcgFrameDefinition>(json.Serialize(frames[0]));displayText.CardDescription="First line\nSecond line";
            Check(HyTcgFrameCatalog.Valid(displayText),"artist template allows bounded multiline display description");
            displayText.CardDescription=new string('x',241);Check(!HyTcgFrameCatalog.Valid(displayText),"artist display description remains bounded");
            displayText.CardDescription="bad\u0001";Check(!HyTcgFrameCatalog.Valid(displayText),"artist display description rejects unsupported controls");
            Error("FrameInvalid",()=>e.CreativeAction("frameCreate",Intent(e,"description","control\u0001")));
            Check(e.Snapshot().Gems==initial && Convert.ToInt64(Snapshot(e)["NextStudioSequence"])==1,"failed transactions charge no gems and do not advance sequence");
            var booster=Intent(e);e.CreativeAction("dyeBoosterBuy",booster);s=Snapshot(e);string dyeId=(string)((Dictionary<string,object>)s["LastStudioReceipt"])["DyeId"];
            Check(e.Snapshot().Gems==initial-50 && DyeCount(e,dyeId)==1,"native booster debits50 and awards one protected random dye");
            e.CreativeAction("dyeBoosterBuy",booster);Check(e.Snapshot().Gems==initial-50 && DyeCount(e,dyeId)==1,"booster replay has same result without double debit or RNG reroll");
            var modified=new Dictionary<string,object>(booster);modified["dyeId"]="dye-03";Error("StudioReplayMismatch",()=>e.CreativeAction("dyeBoosterBuy",modified));
            var paint=Intent(e,"cardId",c.CardId,"frame","classic","dyeId",dyeId);e.CreativeAction("cosmeticSave",paint);
            Check(DyeCount(e,dyeId)==0 && e.GetCardCosmetic(c.CardId).DyeId==dyeId && e.Snapshot().Gems==initial-50,"applying dye consumes one previously paid token without extra gem fee");
            e.CreativeAction("cosmeticSave",paint);e.CreativeAction("cosmeticSave",Intent(e,"cardId",c.CardId,"frame","classic","dyeId",dyeId));
            Check(DyeCount(e,dyeId)==0 && e.Snapshot().Gems==initial-50,"same applied dye saves safely at quantity0 and replays do not consume");
            string art=e.StoreCustomArtwork(Picture(Color.Coral));byte[] artBytes=File.ReadAllBytes(Path.Combine(root,"hytcg-artworks",art+".dat"));
            var quote=(HyTcgStudioQuote)e.CreativeQuote(Input("cardId",c.CardId,"dyeId",dyeId,"artworkId",art));
            Check(quote.ArtworkPixels==750*1050 && quote.ArtworkGems==79 && quote.TotalGems==79,"native quote prices visible pixels by ceiling10000");
            var applyArt=Intent(e,"cardId",c.CardId,"frame","classic","dyeId",dyeId,"artworkId",art);e.CreativeAction("cosmeticSave",applyArt);
            Check(e.Snapshot().Gems==initial-129 && DyeCount(e,dyeId)==0,"new custom artwork matches native79gem quote and keeps already applied dye");
            e.CreativeAction("cosmeticSave",applyArt);Check(e.Snapshot().Gems==initial-129,"artwork save retry does not debit twice");
            var preserveArt=Intent(e,"cardId",c.CardId);e.CreativeAction("cosmeticSave",preserveArt);
            var clearArtReplay=new Dictionary<string,object>(preserveArt);clearArtReplay["artworkId"]="";
            Error("StudioReplayMismatch",()=>e.CreativeAction("cosmeticSave",clearArtReplay));
            Check(e.GetCardCosmetic(c.CardId).ArtworkId==art && e.Snapshot().Gems==initial-129,"omitted artwork preserves it and cannot replay as an explicit clear");
            e.CreativeAction("cosmeticReset",Input("cardId",c.CardId));Check(e.GetCardCosmetic(c.CardId)==null && Items(Snapshot(e),"RestorableLooks").Length==1,"free reset remembers exact prior purchased look");
            e.CreativeAction("cosmeticSave",Intent(e,"cardId",c.CardId,"restorePrevious",true));
            Check(e.GetCardCosmetic(c.CardId).ArtworkId==art && DyeCount(e,dyeId)==0 && e.Snapshot().Gems==initial-129,"restoring exact reset look does not rebuy artwork or consume dye");
            var trip=(HyTcgGameplayActionResult)e.GameplayAction("expeditionStart",Input("routeId","meadow","cardIds",new[]{c.CardId,teammate.CardId}));now=now.AddMinutes(5);
            e.GameplayAction("expeditionClaim",Input("expeditionId",trip.Result.ExpeditionId));
            Check(((HyTcgGameplayPage)e.GameplaySnapshot()).Inventory.Single(x=>x.Id=="fiber").Count==2,"exploration earns authoritative crafting materials");
            FixtureVault(e,v=> {v["Gems"]=9000;var gameplay=json.Deserialize<HyTcgGameplayState>(json.Serialize(v["Gameplay"]));gameplay.Inventory["fiber"]=20;gameplay.Inventory["petal"]=10;gameplay.Inventory["crystal"]=10;v["Gameplay"]=gameplay;});
            int beforeFrame=e.Snapshot().Gems;var layout=frames.Single(f=>f.Id=="moonlace").Portrait;layout.DescriptionX=.10;layout.DescriptionY=.91;layout.DescriptionWidth=.80;layout.DescriptionHeight=.01;
            var frameCreate=Intent(e,"frameId","petal","name","Imported fixture frame","description","Local display note\nSecond line","artworkId",art,"portrait",layout,"gems",0,"materials",new string[0]);e.CreativeAction("frameCreate",frameCreate);
            string frameItem=(string)((Dictionary<string,object>)Snapshot(e)["LastStudioReceipt"])["FrameItemId"];
            Check((string)Items(Snapshot(e),"FrameItems").Single()["Description"]=="Local display note\nSecond line","multiline display description is preserved in a created frame");
            var bag=(HyTcgGameplayPage)e.GameplaySnapshot();Check(e.Snapshot().Gems==beforeFrame-2000 && bag.Inventory.Single(x=>x.Id=="fiber").Count==10 && bag.Inventory.Single(x=>x.Id=="petal").Count==5 && bag.Inventory.Single(x=>x.Id=="crystal").Count==5,"frame creation atomically spends2000 and10/5/5materials with overlay import included");
            e.CreativeAction("frameCreate",frameCreate);Check(e.Snapshot().Gems==beforeFrame-2000 && Items(Snapshot(e),"FrameItems").Length==1,"frame creation retry returns one inventory instance without duplicate debit");
            var changedFrame=new Dictionary<string,object>(frameCreate);changedFrame["name"]="Changed intent";Error("StudioReplayMismatch",()=>e.CreativeAction("frameCreate",changedFrame));
            var movedFrame=new Dictionary<string,object>(frameCreate);var moved=json.Deserialize<HyTcgFrameLayout>(json.Serialize(layout));moved.NameX+=.01;movedFrame["portrait"]=moved;Error("StudioReplayMismatch",()=>e.CreativeAction("frameCreate",movedFrame));
            var frameQuote=(HyTcgStudioQuote)e.CreativeQuote(Input("cardId",c.CardId,"frameItemId",frameItem,"dyeId",dyeId,"artworkId",art));
            Check(frameQuote.FrameItemConsumed && frameQuote.TotalGems==0 && !frameQuote.DyeConsumed,"frame apply quote consumes instance and preserves paid dye/art");
            var applyFrame=Intent(e,"cardId",c.CardId,"frameItemId",frameItem,"dyeId",dyeId,"artworkId",art);e.CreativeAction("cosmeticSave",applyFrame);e.CreativeAction("cosmeticSave",applyFrame);
            Check(e.GetCardCosmetic(c.CardId).FrameItemId==frameItem && e.Snapshot().Gems==beforeFrame-2000 && !(bool)Items(Snapshot(e),"FrameItems").Single()["Available"],"one frame instance binds to one card without another gem fee");
            Error("FrameConsumed",()=>e.CreativeAction("cosmeticSave",Intent(e,"cardId",teammate.CardId,"frameItemId",frameItem)));
            e.CreativeAction("cosmeticReset",Input("cardId",c.CardId));e.CreativeAction("cosmeticSave",Intent(e,"cardId",c.CardId,"restorePrevious",true));
            Check(e.GetCardCosmetic(c.CardId).FrameItemId==frameItem && e.Snapshot().Gems==beforeFrame-2000,"exact frame restore never refunds or consumes a second instance");
            var restart=new HyTcgEconomy(root,()=>now,new HyTcgUserProtector(),()=>TimeSpan.Zero);restart.CreativeAction("dyeBoosterBuy",booster);
            Check(restart.GetCardCosmetic(c.CardId).ArtworkId==art && restart.GetCardCosmetic(c.CardId).FrameItemId==frameItem && restart.Snapshot().Gems==beforeFrame-2000,"protected purchase receipts instance bindings and looks survive process restart");
            for(int i=0;i<135;i++)restart.CreativeAction("cosmeticSave",Intent(restart,"cardId",c.CardId));
            Error("StudioRequestExpired",()=>restart.CreativeAction("dyeBoosterBuy",booster));Check(restart.Snapshot().Gems==beforeFrame-2000,"pruned old receipt cannot become a new charged action");
            Check(coreBytes.SequenceEqual(File.ReadAllBytes(core)) && identity==json.Serialize(restart.ReadOwnedCard(c.CardId)) && artBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(root,"hytcg-artworks",art+".dat"))),"shop and dye actions preserve issued card and registered artwork bytes");
            int beforeConcurrent=restart.Snapshot().Gems;var concurrent=Intent(restart);
            System.Threading.Tasks.Parallel.For(0,8,i=>restart.CreativeAction("dyeBoosterBuy",concurrent));
            Check(restart.Snapshot().Gems==beforeConcurrent-50 && Items(Snapshot(restart),"Dyes").Sum(x=>Convert.ToInt32(x["Count"]))==1,"concurrent identical purchase replays debit once and award one dye");
            long expected=Convert.ToInt64(Snapshot(restart)["NextStudioSequence"]);Error("StudioOperationInvalid",()=>restart.CreativeAction("dyeBoosterBuy",Input("operationId","SC-"+(expected+1)+"-"+Guid.NewGuid().ToString("N"))));
            byte[] beforeFullStack=File.ReadAllBytes(restart.VaultPath);
            FixtureVault(restart,v=> {var full=json.Deserialize<HyTcgCreativeState>(json.Serialize(v["Creative"]));foreach(int index in Enumerable.Range(0,64)){full.Dyes[HyTcgEconomy.RollFrameDye(index,0).Id]=999;full.Dyes[HyTcgEconomy.RollFrameDye(index,1).Id]=999;}v["Creative"]=full;});
            Error("DyeLimit",()=>restart.CreativeAction("dyeBoosterBuy",Intent(restart)));Check(restart.Snapshot().Gems==beforeConcurrent-50,"full dye inventory rejects purchase before any gem debit");
            File.WriteAllBytes(restart.VaultPath,beforeFullStack);
            Check(HyTcgEconomy.ArtworkPixelPrice(0)==1 && HyTcgEconomy.ArtworkPixelPrice(10000)==1 && HyTcgEconomy.ArtworkPixelPrice(10001)==2,"native pixel fee has minimum1 and exact ceiling boundaries");
            Error("ArtworkInvalid",()=>HyTcgEconomy.ArtworkPixelPrice(-1));
            Check(HyTcgEconomy.CountVisiblePixels(SparsePicture(2))==2,"sparse canvas counts only nonzero-alpha pixels including partially visible stroke");
            string sparse=restart.StoreCustomArtwork(SparsePicture(2));byte[] quoteBefore=File.ReadAllBytes(restart.VaultPath);
            var sparseQuote=(HyTcgStudioQuote)restart.CreativeQuote(Input("cardId",teammate.CardId,"artworkId",sparse,"pixelCount",0,"gems",0));
            Check(sparseQuote.ArtworkPixels==2 && sparseQuote.TotalGems==1 && File.ReadAllBytes(restart.VaultPath).SequenceEqual(quoteBefore),"quote is read-only and ignores renderer-supplied fake pixel counts and prices");
            FixtureVault(restart,v=> {var edited=json.Deserialize<HyTcgCreativeState>(json.Serialize(v["Creative"]));edited.Artworks.Single(x=>x.Id==art).VisiblePixels=0;v["Creative"]=edited;});
            byte[] beforePixelAudit=File.ReadAllBytes(restart.VaultPath);
            var verifiedQuote=(HyTcgStudioQuote)restart.CreativeQuote(Input("cardId",teammate.CardId,"artworkId",art));
            Check(verifiedQuote.ArtworkPixels==750*1050 && verifiedQuote.TotalGems==79 && File.ReadAllBytes(restart.VaultPath).SequenceEqual(beforePixelAudit),"native quote recomputes authenticated PNG alpha and ignores forged display-cache metadata");
            int beforeVerifiedApply=restart.Snapshot().Gems;restart.CreativeAction("cosmeticSave",Intent(restart,"cardId",teammate.CardId,"artworkId",art));
            Check(restart.Snapshot().Gems==beforeVerifiedApply-79,"apply recomputes protected raster pixels despite a forged cached count");
            int sparseBefore=restart.Snapshot().Gems;restart.CreativeAction("cosmeticSave",Intent(restart,"cardId",teammate.CardId,"artworkId",sparse));
            Check(restart.Snapshot().Gems==sparseBefore-1,"applying sparse artwork debits the same native minimum1quote");
            string empty=restart.StoreCustomArtwork(SparsePicture(0));Check(((HyTcgStudioQuote)restart.CreativeQuote(Input("cardId",teammate.CardId,"artworkId",empty))).ArtworkPixels==0,"fully transparent artwork counts zero visible pixels");
            var classicCreate=Intent(restart,"frameId","classic","name","Classic instance");int classicBefore=restart.Snapshot().Gems;
            System.Threading.Tasks.Parallel.For(0,8,i=>restart.CreativeAction("frameCreate",classicCreate));
            string classicItem=(string)((Dictionary<string,object>)Snapshot(restart)["LastStudioReceipt"])["FrameItemId"];
            Check(restart.Snapshot().Gems==classicBefore-2000 && Items(Snapshot(restart),"FrameItems").Length==2,"concurrent frame creation issues exactly one new instance and one debit");
            restart.CreativeAction("cosmeticReset",Input("cardId",teammate.CardId));restart.CreativeAction("cosmeticSave",Intent(restart,"cardId",teammate.CardId,"frameItemId",classicItem));
            Check(restart.GetCardCosmetic(teammate.CardId).FrameItemId==classicItem && !(bool)Items(Snapshot(restart),"FrameItems").Single(x=>(string)x["Id"]==classicItem)["Available"],"created Classic instance is consumed even when default frame appearance matches");
            Check(Items(Snapshot(restart),"InventoryItems").Any(x=>(string)x["Kind"]=="dye") && Items(Snapshot(restart),"InventoryItems").Any(x=>(string)x["Kind"]=="material") && !Items(Snapshot(restart),"InventoryItems").Any(x=>(string)x["Id"]==frameItem),"consolidated inventory includes native dye/material counts and excludes consumed frame instances");
            FixtureVault(restart,v=>v["Gems"]=0);Error("GemsInsufficient",()=>restart.CreativeAction("dyeBoosterBuy",Intent(restart)));
            Check(restart.Snapshot().Gems==0,"insufficient wallet never goes negative");
            string resources=Path.Combine(root,"artist-catalog");Directory.CreateDirectory(resources);var custom=frames.Single(f=>f.Id=="moonlace");custom.Id="fixture_artist";custom.PriceGems=0;custom.Materials=new List<HyTcgFrameMaterial>();
            File.WriteAllText(Path.Combine(resources,"catalog.json"),json.Serialize(new[]{custom}));var extension=HyTcgFrameCatalog.LoadLocal(resources).Single(f=>f.Id=="fixture_artist");
            Check(extension.PriceGems==2000 && extension.Materials.Any(m=>m.Id=="fiber" && m.Count==10) && extension.Materials.Any(m=>m.Id=="petal" && m.Count==5) && extension.Materials.Any(m=>m.Id=="crystal" && m.Count==5),"local art catalog cannot set free creation prices or recipes");
            custom.PortraitOverlay="../outside.png";File.WriteAllText(Path.Combine(resources,"catalog.json"),json.Serialize(new[]{custom}));Check(!HyTcgFrameCatalog.LoadLocal(resources).Any(f=>f.Id=="fixture_artist"),"catalog rejects path traversal overlay resources");
            string overlayDirectory=HyTcgFrameCatalog.ResourceDirectory();Directory.CreateDirectory(overlayDirectory);File.WriteAllBytes(Path.Combine(overlayDirectory,"fixture_overlay.png"),Picture(Color.FromArgb(50,140,40,190)));
            Check(HyTcgFrameCatalog.ReadOverlay("fixture_overlay.png")!=null && HyTcgFrameCatalog.ReadOverlay("../outside.png")==null,"local overlay loads only bounded raster under trusted frame directory");
            File.WriteAllText(Path.Combine(overlayDirectory,"corrupt.png"),"<svg />");Check(HyTcgFrameCatalog.ReadOverlay("corrupt.png")==null,"corrupt optional overlay falls back without unsafe decode");
            string migrationRoot=Path.Combine(root,"migration");var migration=new HyTcgEconomy(migrationRoot,()=>now,new HyTcgUserProtector(),()=>TimeSpan.Zero);var oldCard=Add(migration,migrationRoot,3);var oldOther=Add(migration,migrationRoot,4);
            migration.CreativeAction("cosmeticReset",Input("cardId",oldCard.CardId));FixtureVault(migration,v=> {var legacy=json.Deserialize<HyTcgCreativeState>(json.Serialize(v["Creative"]));legacy.Albums.Add(new HyTcgAlbum {Id="AL-"+Guid.NewGuid().ToString("N"),Name="Migration",Background="paper",CardIds=new List<string>{oldCard.CardId},CreatedAt=now.ToString("o")});v["Creative"]=legacy;});
            FixtureVault(migration,v=> {var old=json.Deserialize<HyTcgCreativeState>(json.Serialize(v["Creative"]));old.StudioVersion=1;old.FrameItems=null;old.OwnedFrameIds.Add("petal");old.Dyes["dye-00"]=2;old.Cosmetics.Add(new HyTcgCosmetic {CardId=oldCard.CardId,Frame="petal",Dye="#AABBCC",Morph="",MorphMode="none",ArtworkId=""});v["Creative"]=old;});
            byte[] migrationBefore=File.ReadAllBytes(migration.VaultPath);var directMigratedStyle=migration.GetCardCosmetic(oldCard.CardId);
            var migrationView=Snapshot(migration);string oldBound=(string)Items(migrationView,"Cosmetics").Single()["FrameItemId"];
            Check(directMigratedStyle.FrameItemId==oldBound && directMigratedStyle.Dye=="#AABBCC" && File.ReadAllBytes(migration.VaultPath).SequenceEqual(migrationBefore),"first direct image-style read returns migrated consumed instance without changing the vault");
            Check(Items(migrationView,"FrameItems").Count(x=>(bool)x["Available"])==1 && oldBound!="" && File.ReadAllBytes(migration.VaultPath).SequenceEqual(migrationBefore),"version1 read migration preserves applied look and grants one item per previous permanent unlock without writing");
            int oldGems=migration.Snapshot().Gems;migration.CreativeAction("cosmeticSave",Intent(migration,"cardId",oldCard.CardId));
            Check(migration.GetCardCosmetic(oldCard.CardId).FrameItemId==oldBound && migration.GetCardCosmetic(oldCard.CardId).Dye=="#AABBCC" && DyeCount(migration,"dye-00")==2 && migration.Snapshot().Gems==oldGems,"migration commits grandfathered frame color and dye inventory without charging");
            string migratedAvailable=(string)Items(Snapshot(migration),"FrameItems").Single(x=>(bool)x["Available"])["Id"];migration.CreativeAction("cosmeticSave",Intent(migration,"cardId",oldOther.CardId,"frameItemId",migratedAvailable));
            Check(migration.GetCardCosmetic(oldOther.CardId).FrameItemId==migratedAvailable && migration.Snapshot().Gems==oldGems,"previous permanent unlock converts to one consumable inventory frame");
            restart.ResetAccount("delete");Check(Items(Snapshot(restart),"Dyes").Length==0 && Items(Snapshot(restart),"RestorableLooks").Length==0 && ((IEnumerable)Snapshot(restart)["OwnedFrameIds"]).Cast<string>().SequenceEqual(new[]{"classic"}),"account reset clears inventory shop receipts and restores starter ownership");
            Console.WriteLine("HYTCG Studio economy verification PASS: "+checks+" checks (native costs, purchased dye consumption, Mystic odds, resources, replay/restart, grandfathering contract, catalog, reset).");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
