using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HYROLauncherWeb;

internal static class VerifyHyTcgPerformance
{
    private static int checks;
    private sealed class NoNetwork : IHyTcgTransport
    {
        public string Json(string url,string userAgent,int timeout,System.Threading.CancellationToken token) {throw new Exception("Unexpected network");}
        public string Image(string url,string userAgent,int timeout,System.Threading.CancellationToken token) {throw new Exception("Unexpected network");}
    }
    private static void Check(bool value,string name) {checks++;if(!value)throw new Exception(name);}
    private static long Counter(HyTcgEconomy economy,string name) {return (long)typeof(HyTcgEconomy).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(economy);}
    private static long Time(Action action) {var timer=Stopwatch.StartNew();action();return timer.ElapsedMilliseconds;}
    private static byte[] Fixture()
    {
        using(var bitmap=new Bitmap(750,1050))using(var graphics=Graphics.FromImage(bitmap))using(var output=new MemoryStream())
        {
            graphics.Clear(Color.FromArgb(24,30,49));var random=new Random(620);
            for(int i=0;i<7000;i++)using(var brush=new SolidBrush(Color.FromArgb(random.Next(256),random.Next(256),random.Next(256))))
                graphics.FillRectangle(brush,random.Next(750),random.Next(1050),random.Next(2,22),random.Next(2,22));
            bitmap.Save(output,ImageFormat.Png);return output.ToArray();
        }
    }
    private static HyTcgCard Card(int index)
    {
        return new HyTcgCard {CharacterId=10000+index,Name="Fixture "+index.ToString("D3"),Series="Cache fixture",Rarity="Common",RarityColor=HyTcgRules.RarityColor("Common"),
            CardId="HT-20261010-"+index.ToString("X12"),CreatedAt="2026-10-10T00:00:00Z",ImageSource="acdb",Orientation="portrait",ProfileImage="https://www.animecharactersdatabase.com/uploads/fixture.png",
            SourceImage="https://www.animecharactersdatabase.com/uploads/fixture.png"};
    }
    private static void CreateFixture(string root,byte[] png)
    {
        var economy=new HyTcgEconomy(root);economy.FinishMigration();Directory.CreateDirectory(Path.Combine(root,"cards"));
        object vault=typeof(HyTcgEconomy).GetMethod("Load",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(economy,null);
        var cards=(IList)vault.GetType().GetProperty("Cards").GetValue(vault,null);
        var create=typeof(HyTcgEconomy).GetMethod("CreateOwned",BindingFlags.NonPublic|BindingFlags.Static);
        for(int i=0;i<120;i++)
        {
            HyTcgCard card=Card(i);byte[] bytes=HyTcgStorage.Protect(png);string file="Common_fixture_"+i.ToString("D3")+".hycard";
            File.WriteAllBytes(Path.Combine(root,"cards",file),bytes);cards.Add(create.Invoke(null,new[]{vault,card,file,bytes,(object)(i==119)}));
        }
        typeof(HyTcgEconomy).GetMethod("Persist",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(economy,new[]{vault});
    }
    private static void Clone(string source,string destination)
    {
        Directory.CreateDirectory(Path.Combine(destination,"cards"));
        foreach(string path in Directory.GetFiles(source))File.Copy(path,Path.Combine(destination,Path.GetFileName(path)));
        foreach(string path in Directory.GetFiles(Path.Combine(source,"cards")))File.Copy(path,Path.Combine(destination,"cards",Path.GetFileName(path)));
    }
    private static byte[] Decode(string image) {return Convert.FromBase64String(image.Substring(image.IndexOf(',')+1));}
    private static void MainChecks(string root)
    {
        byte[] png=Fixture();string source=Path.Combine(root,"source"),oldRoot=Path.Combine(root,"baseline"),newRoot=Path.Combine(root,"optimized");
        CreateFixture(source,png);Clone(source,oldRoot);Clone(source,newRoot);
        var baseline=new HyTcgEconomy(oldRoot);long oldCold=Time(delegate {var items=baseline.CollectionCards().Take(40).ToList();baseline.PopulateImagePreviews(items);Check(items.Count==40,"baseline fixture");});
        var economy=new HyTcgEconomy(newRoot);var service=new HyTcgService(newRoot,new NoNetwork(),economy);var initialized=service.Economy;
        var metadata=economy.ReadOwnedCardMetadata(Card(0).CardId);
        Check(metadata.CharacterId==10000 && metadata.Name=="Fixture 000" && metadata.Series=="Cache fixture","readonly enrichment receives native-issued metadata");
        metadata.Name="Mutated copy";Check(economy.ReadOwnedCardMetadata(Card(0).CardId).Name=="Fixture 000","metadata caller cannot mutate native card identity");
        Check(economy.ReadOwnedCardMetadata(Card(119).CardId).CharacterId==10119,"readonly legacy enrichment is allowed");
        Check(Counter(economy,"imageReads")==0 && Counter(economy,"imageDecryptions")==0,"metadata enrichment does not touch or decrypt images");
        object metadataVault=typeof(HyTcgEconomy).GetMethod("Load",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(economy,null);
        object metadataOwned=((IList)metadataVault.GetType().GetProperty("Cards").GetValue(metadataVault,null))[0];
        var signedCard=(HyTcgCard)metadataOwned.GetType().GetProperty("Card").GetValue(metadataOwned,null);string originalName=signedCard.Name;
        signedCard.Name="Wrong identity";typeof(HyTcgEconomy).GetMethod("Persist",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(economy,new[]{metadataVault});
        bool invalidMetadata=false;try {economy.ReadOwnedCardMetadata(Card(0).CardId);}catch(HyTcgException ex){invalidMetadata=ex.Code=="CardInvalid";}
        Check(invalidMetadata,"readonly metadata still authenticates model fingerprint and ownership signature");
        signedCard.Name=originalName;typeof(HyTcgEconomy).GetMethod("Persist",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(economy,new[]{metadataVault});
        var signatureProperty=metadataOwned.GetType().GetProperty("Signature");string originalSignature=(string)signatureProperty.GetValue(metadataOwned,null);
        signatureProperty.SetValue(metadataOwned,new string('A',64),null);typeof(HyTcgEconomy).GetMethod("Persist",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(economy,new[]{metadataVault});
        invalidMetadata=false;try {economy.ReadOwnedCardMetadata(Card(0).CardId);}catch(HyTcgException ex){invalidMetadata=ex.Code=="CardInvalid";}
        Check(invalidMetadata,"readonly metadata rejects forged ownership HMAC");
        signatureProperty.SetValue(metadataOwned,originalSignature,null);typeof(HyTcgEconomy).GetMethod("Persist",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(economy,new[]{metadataVault});
        HyTcgCollectionPage page=null;long cold=Time(delegate {page=service.Collection(0,"","",false,"name");});
        Check(page.Items.Count==40 && page.Total==120 && page.HasMore && page.NextOffset==40,"native metadata pagination");
        Check(Counter(economy,"imageReads")==40 && Counter(economy,"imageDecryptions")==40 && Counter(economy,"previewGenerations")==40,"cold page touches only 40 cards");
        long warm=Time(delegate {page=service.Collection(0,"","",false,"name");});
        Check(Counter(economy,"imageReads")==80 && Counter(economy,"imageDecryptions")==40 && Counter(economy,"previewGenerations")==40 && Counter(economy,"previewMemoryHits")==40,"warm page reauthenticates bytes without decryption or resize");
        string cacheDirectory=Path.Combine(newRoot,"hytcg-previews");var files=Directory.GetFiles(cacheDirectory,"*.hypreview");Check(files.Length==40,"encrypted persistent previews");
        foreach(string path in files.Take(3))
        {
            bool image=false;try {using(var stream=File.OpenRead(path))using(var decoded=Image.FromStream(stream))image=true;}catch { }
            Check(!image,"previews cannot be opened as pictures");
        }
        var reopened=new HyTcgEconomy(newRoot);var reopenedService=new HyTcgService(newRoot,new NoNetwork(),reopened);initialized=reopenedService.Economy;
        long restart=Time(delegate {page=reopenedService.Collection(0,"","",false,"name");});
        Check(Counter(reopened,"previewDiskHits")==40 && Counter(reopened,"imageDecryptions")==0 && Counter(reopened,"previewGenerations")==0,"restart reads authenticated protected previews without full card decryption");
        string detail=null;long detailCold=Time(delegate {detail=reopenedService.ReadCardImage(page.Items[0].Card.CardId);});long detailWarm=Time(delegate {reopenedService.ReadCardImage(page.Items[0].Card.CardId);});
        Check(Counter(reopened,"imageDecryptions")==1 && Counter(reopened,"detailMemoryHits")==1,"detail decrypts once and verifies every read");
        Check(Decode(detail).SequenceEqual(png),"detail retains exact original bytes");
        var first=page.Items[0];string cardPath=Path.Combine(newRoot,"cards",first.FileName);byte[] original=File.ReadAllBytes(cardPath);DateTime stamp=File.GetLastWriteTimeUtc(cardPath);
        byte[] changed=(byte[])original.Clone();changed[changed.Length-1]^=1;File.WriteAllBytes(cardPath,changed);File.SetLastWriteTimeUtc(cardPath,stamp);
        bool rejected=false;try {reopenedService.ReadCardImage(first.Card.CardId);}catch(HyTcgException ex){rejected=ex.Code=="CardInvalid";}
        Check(rejected,"same-length timestamp-preserving tamper rejects warm detail");
        Check(reopened.ReadOwnedCardMetadata(first.Card.CardId).Name==first.Card.Name,"readonly metadata remains available without trusting a damaged image");
        page=reopenedService.Collection(0,"","",false,"name");Check(page.Items.Count==39 && page.NextOffset==40 && page.Items.All(i=>i.Card.CardId!=first.Card.CardId),"tampered preview omitted with stable next offset");
        var second=reopenedService.Collection(page.NextOffset,"","",false,"name");Check(second.Items.Count==40 && !second.Items.Any(i=>page.Items.Any(old=>old.Card.CardId==i.Card.CardId)),"bad file cannot duplicate next-page cards");
        File.WriteAllBytes(cardPath,original);
        files=Directory.GetFiles(cacheDirectory,"*.hypreview");string corrupt=files[0];byte[] encrypted=File.ReadAllBytes(corrupt);encrypted[encrypted.Length-1]^=1;File.WriteAllBytes(corrupt,encrypted);
        // Clear only RAM through a fresh native instance; malformed signed cache is rebuilt.
        var rebuild=new HyTcgEconomy(newRoot);var rebuiltService=new HyTcgService(newRoot,new NoNetwork(),rebuild);initialized=rebuiltService.Economy;
        for(int offset=0;offset<120;offset+=40)rebuiltService.Collection(offset,"","",false,"name");
        Check(Counter(rebuild,"previewGenerations")>=1,"tampered preview cache is rejected and regenerated");
        string a=files[1],b=files[2];byte[] aa=File.ReadAllBytes(a),bb=File.ReadAllBytes(b);File.WriteAllBytes(a,bb);File.WriteAllBytes(b,aa);
        var swapped=new HyTcgEconomy(newRoot);var swapService=new HyTcgService(newRoot,new NoNetwork(),swapped);initialized=swapService.Economy;
        // Validate every page: shuffled ciphertext cannot be reused for another source hash.
        for(int offset=0;offset<120;offset+=40)swapService.Collection(offset,"","",false,"name");
        Check(Counter(swapped,"previewGenerations")>=2,"cache HMAC binds previews to their source ciphertext");
        object cache=typeof(HyTcgEconomy).GetField("imagePreviewCache",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(swapped);
        int cacheBytes=(int)cache.GetType().GetField("bytes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cache);
        var cacheItems=(IDictionary)cache.GetType().GetField("values",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cache);
        Check(cacheBytes<=8*1024*1024 && cacheItems.Count<=200,"preview RAM stays within count and byte budgets");
        swapped.ResetAccount("delete");Check(!Directory.GetFiles(cacheDirectory,"*.hypreview").Any(),"account deletion also removes encrypted previews");
        Check(!Directory.GetFiles(Path.Combine(newRoot,"cards")).Any(),"account reset removes owned cards");
        bool removed=false;try {swapped.ReadOwnedCardMetadata(Card(0).CardId);}catch(HyTcgException ex){removed=ex.Code=="CardUnavailable";}
        Check(removed,"deleted account cannot read old ownership metadata");
        string stats="{\"checks\":"+checks+",\"cards\":120,\"fixtureBytes\":"+png.Length+",\"baselineColdMs\":"+oldCold+",\"optimizedColdMs\":"+cold+",\"warmMs\":"+warm+",\"restartMs\":"+restart+",\"detailColdMs\":"+detailCold+",\"detailWarmMs\":"+detailWarm+"}";
        File.WriteAllText(Path.Combine(root,"report.json"),stats,Encoding.UTF8);Console.WriteLine(stats);
    }
    internal static int Main(string[] args)
    {
        try {string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);MainChecks(root);Console.WriteLine("HYTCG performance/security verification passed: "+checks+" checks.");return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
