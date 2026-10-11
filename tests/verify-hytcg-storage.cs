using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HYROLauncherWeb;

internal static class VerifyHyTcgStorage
{
    private static int checks;
    private sealed class InterruptProtector : IHyTcgProtector
    {
        private readonly HyTcgUserProtector inner=new HyTcgUserProtector();
        internal bool FailNext;
        public byte[] Protect(byte[] bytes) { if(FailNext) { FailNext=false;throw new IOException("Simulated interrupted vault write"); }return inner.Protect(bytes); }
        public byte[] Unprotect(byte[] bytes) { return inner.Unprotect(bytes); }
    }
    private sealed class NoNetwork : IHyTcgTransport
    {
        public string Json(string url,string userAgent,int timeout,System.Threading.CancellationToken token) { throw new Exception("No network in storage verifier"); }
        public string Image(string url,string userAgent,int timeout,System.Threading.CancellationToken token) { throw new Exception("No network in storage verifier"); }
    }
    private static void Check(bool value,string name) { checks++;if(!value)throw new Exception(name); }
    private static void Error(string code,Action action)
    {bool rejected=false;try {action();}catch(HyTcgException ex){rejected=ex.Code==code;}Check(rejected,"expected "+code);}
    private static string Hash(byte[] bytes) { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)); }
    private static byte[] Fixture(bool landscape)
    {
        using(var bitmap=new Bitmap(landscape?1050:750,landscape?750:1050))
        using(var graphics=Graphics.FromImage(bitmap))
        using(var output=new MemoryStream())
        { graphics.Clear(Color.MidnightBlue);graphics.FillEllipse(Brushes.Orchid,40,120,350,350);bitmap.Save(output,ImageFormat.Png);return output.ToArray(); }
    }
    private static HyTcgCard Card(string rarity,bool landscape)
    {
        return new HyTcgCard {CharacterId=99991,Name="Storage fixture",Series="Local test",Gender="Female",Rarity=rarity,RarityColor=HyTcgRules.RarityColor(rarity),
            CardId="HT-20261009-"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant(),CreatedAt="2026-10-09T00:00:00Z",ImageSource="acdb",ProfileImage="https://www.animecharactersdatabase.com/uploads/local.png",
            SourceImage="https://www.animecharactersdatabase.com/uploads/local.png",Orientation=landscape?"landscape":"portrait",ArtworkWidth=landscape?1050:750,ArtworkHeight=landscape?750:1050};
    }
    private static HyTcgCard IssuePlain(HyTcgEconomy economy,string root,string rarity,bool landscape,byte[] png)
    {
        var reservation=economy.Reserve(1);string token=reservation.Tokens[0];economy.Begin(token);var card=Card(rarity,landscape);economy.AuthorizeCard(token,card);
        string name=rarity+"_"+card.CardId+".png";Directory.CreateDirectory(Path.Combine(root,"cards"));File.WriteAllBytes(Path.Combine(root,"cards",name),png);economy.Complete(token,card,name,png);return card;
    }
    // This represents an authenticated card created by the old native issuer,
    // before portrait-only exports. The current authorization API intentionally
    // cannot mint it. Use the actual private signing/write transaction in this
    // isolated fixture, instead of relaxing production validation for a test.
    private static HyTcgCard HistoricalLandscape(HyTcgEconomy economy,string root,byte[] png)
    {
        var card=Card("Rare",true);card.Metadata=new Dictionary<string,string>{{"hytcg_card_layout","window-v3"}};
        string name=card.Rarity+"_"+card.CardId+".png";Directory.CreateDirectory(Path.Combine(root,"cards"));File.WriteAllBytes(Path.Combine(root,"cards",name),png);
        MethodInfo write=typeof(HyTcgEconomy).GetMethod("Write",BindingFlags.Instance|BindingFlags.NonPublic).MakeGenericMethod(typeof(bool));
        Type signature=write.GetParameters()[0].ParameterType, vaultType=signature.GetGenericArguments()[0];
        var vault=Expression.Parameter(vaultType,"vault");var time=Expression.Parameter(typeof(DateTime),"time");
        MethodInfo add=typeof(VerifyHyTcgStorage).GetMethod("AddHistoricalOwned",BindingFlags.Static|BindingFlags.NonPublic);
        var callback=Expression.Lambda(signature,Expression.Call(add,Expression.Convert(vault,typeof(object)),Expression.Constant(card),Expression.Constant(name),Expression.Constant(png)),vault,time).Compile();
        write.Invoke(economy,new object[]{callback});return card;
    }
    private static bool AddHistoricalOwned(object vault,HyTcgCard card,string name,byte[] png)
    {
        MethodInfo create=typeof(HyTcgEconomy).GetMethod("CreateOwned",BindingFlags.Static|BindingFlags.NonPublic);
        object owned=create.Invoke(null,new object[]{vault,card,name,png,false});
        ((IList)vault.GetType().GetProperty("Cards").GetValue(vault,null)).Add(owned);return true;
    }
    private static byte[] DataImage(string uri)
    { return Convert.FromBase64String(uri.Substring(uri.IndexOf(',')+1)); }
    private static void CheckWallet(HyTcgWallet before,HyTcgWallet after)
    {
        Check(before.Gems==after.Gems && before.PaidCredits==after.PaidCredits && before.FreeCredits==after.FreeCredits && before.LimitedCredits==after.LimitedCredits,"migration preserves currency");
        Check(before.PitySR==after.PitySR && before.PitySSR==after.PitySSR && before.PityUR==after.PityUR,"migration preserves pity");
    }
    private static void Migration(string root)
    {
        var economy=new HyTcgEconomy(root);byte[] portrait=Fixture(false),landscape=Fixture(true);
        var target=IssuePlain(economy,root,"Common",false,portrait);var material=IssuePlain(economy,root,"Uncommon",false,portrait);var wide=HistoricalLandscape(economy,root,landscape);
        economy.SetFavorite(target.CardId,true);var upgrade=economy.PrepareUpgrade(target.CardId,new List<string>{material.CardId},false);
        economy.FinishMigration();string outsider=Path.Combine(root,"cards","unowned.png");File.WriteAllBytes(outsider,portrait);
        var before=economy.Snapshot();Check(economy.MigrateEncryptedStorage()==3,"migrates three authenticated plaintext cards");CheckWallet(before,economy.Snapshot());
        var cards=economy.CollectionCards();Check(cards.Count==3 && cards.All(c=>c.FileName.EndsWith(".hycard")),"owned inventory is encrypted");
        Check(cards.First(c=>c.Card.CardId==target.CardId).Favorite,"favorite preserved");Check(cards.Count(c=>c.UpgradeLocked)==2,"prepared upgrade locks survive migration");
        Check(File.Exists(outsider),"unowned image left untouched");Check(Directory.GetFiles(Path.Combine(root,"cards"),"*.png").Length==1,"only authenticated plaintext removed");
        foreach(var item in cards)
        {
            byte[] container=File.ReadAllBytes(item.ImagePath);Check(HyTcgStorage.IsContainer(container),"opaque header");
            bool recognized=false;try {using(var stream=new MemoryStream(container))using(var image=Image.FromStream(stream)){recognized=true;}}catch { }
            Check(!recognized,"Explorer/image decoders cannot preview the container");
            Check(Hash(HyTcgStorage.ImageBytes(container))==Hash(item.Card.Orientation=="landscape"?landscape:portrait),"migration retains exact PNG bytes");
        }
        var service=new HyTcgService(root,new NoNetwork(),economy);var page=service.Collection(0,"","",false,"newest");
        Check(page.Items.Count==3 && page.Items.All(c=>c.ImagePath.StartsWith("data:image/png;base64,")),"bounded alpha-capable collection thumbnails use PNG");
        foreach(var item in page.Items)
        {
            using(var stream=new MemoryStream(DataImage(item.ImagePath)))using(var image=Image.FromStream(stream))
                Check(image.Width<=392 && image.Height<=392,"thumbnail size bounded");
            Check(Hash(DataImage(service.ReadCardImage(item.Card.CardId)))==Hash(item.Card.Orientation=="landscape"?landscape:portrait),"full authenticated native image");
        }
        Check(Directory.GetFiles(Path.Combine(root,"cards"),"*.jpg").Length==0,"no thumbnail files");
        economy.AuthorizeUpgrade(upgrade.Token,upgrade.Card);
        var result=service.SaveUpgrade(upgrade.Card,"data:image/png;base64,"+Convert.ToBase64String(portrait),upgrade.Token);
        Check(result.FilePath.EndsWith(".hycard") && File.Exists(result.FilePath),"upgrade saves encrypted replacement");
        Check(economy.CollectionCards().Count==2 && economy.ReadOwnedCard(target.CardId).Rarity=="Uncommon","upgrade consumes material after commit");
        Check(service.SaveUpgrade(upgrade.Card,"data:image/png;base64,"+Convert.ToBase64String(portrait),upgrade.Token).FilePath==result.FilePath,"encrypted upgrade save retry idempotent");
        var saved=new HyTcgEconomy(root);Check(saved.CollectionCards().Count==2,"encrypted ownership survives reload");
        var wideItem=saved.CollectionCards().First(c=>c.Card.CardId==wide.CardId);byte[] original=File.ReadAllBytes(wideItem.ImagePath);
        File.WriteAllBytes(wideItem.ImagePath,HyTcgStorage.Protect(HyTcgStorage.ImageBytes(original)));
        Check(saved.CollectionCards().Count==1,"same plaintext re-encrypted by user still fails ownership hash");
        bool rejected=false;try {saved.ReadCardImage(wide.CardId);}catch(HyTcgException ex){rejected=ex.Code=="CardInvalid";}Check(rejected,"tampered file cannot be read through native bridge");
        File.WriteAllBytes(wideItem.ImagePath,original);var dissolved=saved.Dissolve(wide.CardId);Check(dissolved.FilePath.EndsWith(".hycard") && dissolved.GemsAwarded==8,"encrypted card dissolves with native rarity award");
        Check(saved.ReadCard("../outside.hycard",original)==null,"storage traversal rejected");
    }
    private static void InterruptedMigration(string root)
    {
        var protector=new InterruptProtector();var economy=new HyTcgEconomy(root,delegate{return DateTime.UtcNow;},protector,null);byte[] png=Fixture(false);
        var card=IssuePlain(economy,root,"Common",false,png);economy.FinishMigration();string original=economy.CollectionCards()[0].ImagePath;
        protector.FailNext=true;bool interrupted=false;try{economy.MigrateEncryptedStorage();}catch(HyTcgException ex){interrupted=ex.Code=="VaultUnavailable";}
        Check(interrupted && File.Exists(original),"interrupted ledger commit retains plaintext authority");
        Check(economy.CollectionCards().Count==1 && economy.CollectionCards()[0].FileName.EndsWith(".png"),"interrupted migration preserves old owned card");
        string candidate=Path.ChangeExtension(original,".hycard");Check(File.Exists(candidate),"encrypted candidate durable before ownership transition");byte[] candidateBytes=File.ReadAllBytes(candidate);
        Check(economy.MigrateEncryptedStorage()==1 && !File.Exists(original),"retry commits candidate then deletes old plaintext");
        Check(Hash(File.ReadAllBytes(candidate))==Hash(candidateBytes),"retry reuses exact ciphertext candidate");
        File.WriteAllBytes(original,png);var reloaded=new HyTcgEconomy(root);Check(reloaded.MigrateEncryptedStorage()==0 && !File.Exists(original),"post-commit cleanup resumes after process interruption");
        Check(reloaded.ReadOwnedCard(card.CardId).Name==card.Name,"recovery preserves metadata");
    }
    private static void EncryptedSave(string root)
    {
        var economy=new HyTcgEconomy(root);var service=new HyTcgService(root,new NoNetwork(),economy);byte[] png=Fixture(false);var reservation=economy.Reserve(1);string token=reservation.Tokens[0];economy.Begin(token);var card=Card("SR",true);economy.AuthorizeCard(token,card);
        Check(card.Orientation=="portrait" && card.ArtworkWidth==1050 && card.ArtworkHeight==750,"new pulls keep wide artwork metadata and enforce portrait card dimensions");
        string wideData="data:image/png;base64,"+Convert.ToBase64String(Fixture(true));
        Error("CardInvalid",delegate {service.SaveCard(card,wideData,token);});
        Check(!Directory.Exists(service.CardsDirectory) || Directory.GetFiles(service.CardsDirectory).Length==0,"new landscape export is rejected before creating a card file");
        card.Orientation="landscape";Error("CardInvalid",delegate {service.SaveCard(card,wideData,token);});card.Orientation="portrait";
        string data="data:image/png;base64,"+Convert.ToBase64String(png),saved=service.SaveCard(card,data,token);
        Check(saved.EndsWith(".hycard") && Directory.GetFiles(Path.Combine(root,"cards"),"*.png").Length==0,"new pull has no plaintext export");
        Check(service.SaveCard(card,data,token)==saved,"encrypted pull save retry idempotent");
        Check(economy.CollectionCards().Count==1 && economy.PendingCards().Count==0,"save consumes issued reservation once");
        using(var stream=new MemoryStream(DataImage(service.ReadCardImage(card.CardId))))using(var image=Image.FromStream(stream))Check(image.Width==750 && image.Height==1050,"new full detail rendered PNG");
        Error("CardInvalid",delegate {service.SaveCard(card,"data:image/png;base64,broken",token);});
        var wallet=economy.ResetAccount("delete");Check(wallet.Gems==1800 && economy.CollectionCards().Count==0 && !File.Exists(saved),"account reset removes encrypted cards and resets grant");
    }
    private static void AlphaThumbnails(string root)
    {
        byte[] png;using(var image=new Bitmap(750,1050))using(var graphics=Graphics.FromImage(image))using(var output=new MemoryStream())
        {graphics.Clear(Color.Transparent);graphics.FillRectangle(Brushes.AliceBlue,20,20,710,1010);graphics.FillEllipse(Brushes.Orchid,120,220,350,350);image.Save(output,ImageFormat.Png);png=output.ToArray();}
        var economy=new HyTcgEconomy(root);var card=IssuePlain(economy,root,"Common",false,png);economy.MigrateEncryptedStorage();
        var service=new HyTcgService(root,new NoNetwork(),economy);string preview=service.Collection(0,"","",false,"newest").Items.Single().ImagePath;
        Check(preview.StartsWith("data:image/png;base64,"),"transparent thumbnail format is PNG");
        Check(DataImage(preview).Length<=384*1024,"decrypted thumbnail stays within 384 KB limit");
        using(var stream=new MemoryStream(DataImage(preview)))using(var image=new Bitmap(stream))
        {Check(image.GetPixel(0,0).A==0,"transparent card corner does not become black");Check(image.GetPixel(image.Width/2,image.Height/2).A==255,"opaque artwork remains opaque");}
        string cached=Directory.GetFiles(Path.Combine(root,"hytcg-previews"),"*.hypreview").Single();byte[] protectedPreview=File.ReadAllBytes(cached);
        Check(protectedPreview.Length<=512*1024 && !(protectedPreview[0]==137 && protectedPreview[1]==80),"disk preview is bounded and remains protected, not a plaintext PNG");
        var cold=new HyTcgService(root,new NoNetwork(),new HyTcgEconomy(root));
        Check(cold.Collection(0,"","",false,"newest").Items.Single().ImagePath==preview,"cold collection reuses alpha-preserving protected disk preview");
        protectedPreview[protectedPreview.Length-1]^=1;File.WriteAllBytes(cached,protectedPreview);
        var tampered=new HyTcgService(root,new NoNetwork(),new HyTcgEconomy(root));
        Check(tampered.Collection(0,"","",false,"newest").Items.Single().ImagePath==preview,"tampered protected preview fails authentication and regenerates from signed card");
        Check(Hash(DataImage(tampered.ReadCardImage(card.CardId)))==Hash(png),"preview regeneration never changes signed full artwork");
    }
    private static void ResetExportInterleavings(string root)
    {
        var economy=new HyTcgEconomy(root);var service=new HyTcgService(root,new NoNetwork(),economy);byte[] png=Fixture(false);string data="data:image/png;base64,"+Convert.ToBase64String(png);
        var reservation=economy.Reserve(1);string token=reservation.Tokens[0];economy.Begin(token);var card=Card("Common",false);economy.AuthorizeCard(token,card);
        using(economy.BeginCardExport(token,card))
        {
            Error("Busy",delegate {economy.ResetAccount("delete");});
            Error("Busy",delegate {using(economy.BeginCardExport(token,card)) { }});
            Error("Busy",delegate {economy.Begin(token);});
            Error("Busy",delegate {economy.AuthorizeCard(token,card);});
            // UI refresh cancels fetch work and clears its own saving flags; neither owns
            // or releases the native export lease.
            economy.EndFetch(token);Error("Busy",delegate {economy.ResetAccount("delete");});
        }
        economy.ResetAccount("delete");Error("CardInvalid",delegate {service.SaveCard(card,data,token);});
        Check(!Directory.Exists(service.CardsDirectory) || Directory.GetFiles(service.CardsDirectory).Length==0,"stale issued card cannot create a candidate after reset");

        var queued=economy.Reserve(1);string queuedToken=queued.Tokens[0];economy.Begin(queuedToken);var queuedCard=Card("Common",false);economy.AuthorizeCard(queuedToken,queuedCard);
        var initialized=service.Economy;
        object storageGate=typeof(HyTcgService).GetField("storageLock",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        using(var started=new ManualResetEventSlim(false))
        {
            string error="";Task task;
            Monitor.Enter(storageGate);
            try
            {
                task=Task.Run(delegate {started.Set();try {service.SaveCard(queuedCard,data,queuedToken);}catch(HyTcgException ex){error=ex.Code;}});
                Check(started.Wait(5000),"queued save started");
                economy.ResetAccount("delete");
            }
            finally {Monitor.Exit(storageGate);}
            Check(task.Wait(10000) && error=="CardInvalid","queued pre-reset save revalidates authority before encoding/disk");
        }
        Check(!Directory.Exists(service.CardsDirectory) || Directory.GetFiles(service.CardsDirectory).Length==0,"queued save leaves no post-reset orphan");

        var target=IssuePlain(economy,root,"Common",false,png);var material=IssuePlain(economy,root,"Uncommon",false,png);
        economy.MigrateEncryptedStorage();var upgrade=economy.PrepareUpgrade(target.CardId,new List<string>{material.CardId},false);economy.AuthorizeUpgrade(upgrade.Token,upgrade.Card);
        using(economy.BeginUpgradeExport(upgrade.Token,upgrade.Card))
        {
            Error("Busy",delegate {economy.ResetAccount("delete");});
            Error("Busy",delegate {economy.CancelUpgrade(upgrade.Token);});
            Error("Busy",delegate {using(economy.BeginUpgradeExport(upgrade.Token,upgrade.Card)) { }});
            Error("Busy",delegate {economy.BeginUpgrade(upgrade.Token);});
            Error("Busy",delegate {economy.AuthorizeUpgrade(upgrade.Token,upgrade.Card);});
            economy.EndUpgrade(upgrade.Token);Error("Busy",delegate {economy.ResetAccount("delete");});
        }
        economy.ResetAccount("delete");Error("UpgradeUnavailable",delegate {service.SaveUpgrade(upgrade.Card,data,upgrade.Token);});
        Check(Directory.GetFiles(service.CardsDirectory).Length==0,"stale upgrade cannot create an encrypted candidate after reset");
    }
    internal static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);Migration(Path.Combine(root,"migration"));InterruptedMigration(Path.Combine(root,"interrupted"));EncryptedSave(Path.Combine(root,"save"));AlphaThumbnails(Path.Combine(root,"alpha-previews"));ResetExportInterleavings(Path.Combine(root,"reset-export"));
            Console.WriteLine("HYTCG storage verification passed: "+checks+" checks; DPAPI containers, exact-image migration, interrupted commits, thumbnail bounds, idempotent save/upgrade, signed ownership, reset/export interleavings and stale-token rejection before disk writes.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
