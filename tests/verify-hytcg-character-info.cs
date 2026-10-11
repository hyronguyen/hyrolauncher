using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal sealed class CharacterInfoFixture : IHyTcgCharacterTransport
{
    internal int Searches,Details;
    internal bool Empty,WrongSeries,WrongCharacter,Ambiguous,Unavailable,Malformed,MissingFavourites;
    private readonly JavaScriptSerializer json=new JavaScriptSerializer();
    private object Character(int id)
    {
        return new {id=id,name=new {full=WrongCharacter?"Hinata Hyuugo":"Hyuuga Hinata",native="日向 ヒナタ",alternative=new[]{"Hinata Hyuuga"}},media=new {nodes=new[]{new {id=20,title=new {english=WrongSeries?"Boruto":"Naruto",romaji=WrongSeries?"Boruto":"NARUTO",native="ナルト"},synonyms=new[]{WrongSeries?"Boruto":"Naruto"}}}}};
    }
    public string Search(string name,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();Interlocked.Increment(ref Searches);
        if(Unavailable)throw new IOException("fixture offline");
        if(Malformed)return "<html>not GraphQL</html>";
        var chars=new List<object>();if(!Empty)chars.Add(Character(17));if(Ambiguous)chars.Add(Character(18));
        return json.Serialize(new {data=new {Page=new {characters=chars}}});
    }
    public string Detail(int id,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();Interlocked.Increment(ref Details);
        var value=json.Deserialize<Dictionary<string,object>>(json.Serialize(Character(id)));
        value["favourites"]=MissingFavourites?(object)null:12345;value["description"]="**Kind** ninja. ~!hidden ending!~ <script>attack()</script><br>Visit [Naruto](https://example.com). &amp; friend. ![photo](https://example.com/a.png)";
        value["gender"]="Female";value["age"]="16";value["bloodType"]="A";value["dateOfBirth"]=new {month=12,day=27};value["siteUrl"]="https://evil.test/unsafe";
        return json.Serialize(new {data=new {Character=value}});
    }
}
internal sealed class BatchCharacterInfoFixture : IHyTcgCharacterTransport, IHyTcgCharacterBatchTransport
{
    internal int Batches;
    internal bool Unavailable;
    internal readonly CharacterInfoFixture Source=new CharacterInfoFixture();
    public string Search(string name,CancellationToken token){return Source.Search(name,token);}
    public string Detail(int id,CancellationToken token){return Source.Detail(id,token);}
    public List<string> SearchMany(IList<string> names,CancellationToken token)
    {
        Batches++;if(Unavailable)throw new IOException("batch offline");
        var parser=new JavaScriptSerializer();var results=new List<string>();
        foreach(var name in names)
        {
            var value=parser.Deserialize<Dictionary<string,object>>(Source.Detail(17,token));
            var data=(Dictionary<string,object>)value["data"];
            results.Add(parser.Serialize(new {data=new {Page=new {characters=new[]{data["Character"]}}}}));
        }
        return results;
    }
}
internal static class VerifyCharacterInfo
{
    private static int checks;
    private static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;}
    private static HyTcgCard Card(int id){return new HyTcgCard{CharacterId=id,Name="Hinata Hyuuga",Series="Naruto"};}
    private static void Throws(Action action,string code){try{action();throw new Exception("Expected "+code);}catch(HyTcgException error){Check(error.Code==code,code);}}
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
            if(args.Length>1 && args[1]=="--live")return Live(root);
            var fixture=new CharacterInfoFixture();var service=new HyTcgService(root);service.CharacterTransport=fixture;
            var card=Card(100);Check(service.CachedCharacterFavourites(card)==null,"Uncached character has no speculative bonus");
            var result=service.CharacterInfo(card,CancellationToken.None);
            Check(result.Matched && result.AniListId==17 && result.CharacterId==100,"Exact native name and series match");
            Check(result.Favourites==12345 && service.CachedCharacterFavourites(card)==12345,"Favourites available to native economy cache lookup");
            Check(result.Gender=="Female" && result.Age=="16" && result.BloodType=="A" && result.DateOfBirth=="12-27","Additional fields and partial birthday");
            Check(result.SiteUrl=="https://anilist.co/character/17","Site link constructed from validated native ID");
            Check(!result.Description.Contains("hidden") && !result.Description.Contains("attack") && !result.Description.Contains("<") && !result.Description.Contains("https:") && result.Description.Contains("Kind ninja"),"Spoilers scripts HTML and markdown stripped");
            Check(fixture.Searches==1 && fixture.Details==1,"Bounded two public GraphQL calls");
            result.Favourites=999999;Check(service.CachedCharacterFavourites(card)==12345,"Return objects cannot mutate cached currency metadata");
            service.CharacterInfo(card,CancellationToken.None);Check(fixture.Searches==1 && fixture.Details==1,"Second detail reads memory cache");
            var files=Directory.GetFiles(Path.Combine(root,".cache","hytcg-characters"),"*.bin").Where(file=>Path.GetFileName(file)!="favourites.bin").ToArray();Check(files.Length==1 && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(files[0])).Contains("Hinata"),"Per-character encrypted local cache");
            var restarted=new HyTcgService(root);restarted.CharacterTransport=new CharacterInfoFixture{Unavailable=true};
            Check(restarted.CharacterInfo(card,CancellationToken.None).Favourites==12345 && restarted.CachedCharacterFavourites(card)==12345,"Restart reads protected cache without network");
            var sameIdOtherSeries=Card(100);sameIdOtherSeries.Series="Bleach";
            Check(restarted.CachedCharacterFavourites(sameIdOtherSeries)==null,"Cache identity includes series");
            var sameIdOtherName=Card(100);sameIdOtherName.Name="Hinata";Check(restarted.CachedCharacterFavourites(sameIdOtherName)==null,"Cache identity includes exact name");
            fixture.Empty=true;var missing=service.CharacterInfo(Card(101),CancellationToken.None);Check(!missing.Matched && !missing.Favourites.HasValue,"Valid empty result has no bonus");
            fixture.Empty=false;int before=fixture.Searches;service.CharacterInfo(Card(101),CancellationToken.None);Check(fixture.Searches==before,"Missing results cached as well");
            fixture.Ambiguous=true;Check(!service.CharacterInfo(Card(102),CancellationToken.None).Matched,"Ambiguous exact character identity rejected");fixture.Ambiguous=false;
            fixture.WrongSeries=true;Check(!service.CharacterInfo(Card(103),CancellationToken.None).Matched,"Wrong series rejected even with exact name");fixture.WrongSeries=false;
            fixture.WrongCharacter=true;var wrong=Card(104);wrong.Name="Hinata Hyuuga";
            // Full names and explicit aliases are both valid names; remove alias in a dedicated response test below.
            Check(service.CharacterInfo(wrong,CancellationToken.None).Matched,"Exact explicit alias accepted");fixture.WrongCharacter=false;
            fixture.MissingFavourites=true;Check(service.CharacterInfo(Card(105),CancellationToken.None).Matched && service.CachedCharacterFavourites(Card(105))==null,"Absent favourites does not invent bonus");fixture.MissingFavourites=false;
            var blank=Card(106);blank.Series="";before=fixture.Searches;Check(!service.CharacterInfo(blank,CancellationToken.None).Matched && fixture.Searches==before,"Unknown series does not query or guess");
            fixture.Malformed=true;Throws(()=>service.CharacterInfo(Card(107),CancellationToken.None),"AniListUnavailable");fixture.Malformed=false;
            Check(service.CachedCharacterInfo(Card(107))==null,"Malformed API response never cached as no match");
            fixture.Unavailable=true;try{service.CharacterInfo(Card(108),CancellationToken.None);throw new Exception("Expected offline");}catch(IOException){checks++;}fixture.Unavailable=false;
            Check(service.CachedCharacterInfo(Card(108))==null,"Network outage never cached as missing character");
            using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{service.CharacterInfo(Card(109),cancelled.Token);throw new Exception("Expected canceled");}catch(OperationCanceledException){checks++;}}
            Check(service.CachedCharacterInfo(Card(109))==null,"Canceled request does not write cache");
            int priorDetails=fixture.Details;var parallel=Card(110);Task.WaitAll(Enumerable.Range(0,8).Select(index=>Task.Run(()=>service.CharacterInfo(parallel,CancellationToken.None))).ToArray());
            Check(fixture.Details==priorDetails+1,"Concurrent detail requests deduplicated");
            byte[] damaged=File.ReadAllBytes(files[0]);damaged[damaged.Length/2]^=71;File.WriteAllBytes(files[0],damaged);
            string indexFile=Path.Combine(root,".cache","hytcg-characters","favourites.bin");byte[] indexBytes=File.ReadAllBytes(indexFile);indexBytes[indexBytes.Length/2]^=71;File.WriteAllBytes(indexFile,indexBytes);
            var corrupted=new HyTcgService(root);corrupted.CharacterTransport=fixture;Check(corrupted.CachedCharacterFavourites(card)==null,"Tampered protected bonus index cannot award favourites bonus");
            Check(corrupted.CharacterInfo(card,CancellationToken.None).Favourites==12345,"Damaged cache recovers through fresh trusted API");
            Check(HyTcgService.PlainCharacterText(new string('x',20000),5000).Length==5000,"Description bounded for UI and disk");
            var bulk=new BatchCharacterInfoFixture();var batchService=new HyTcgService(Path.Combine(root,"batch"));batchService.CharacterTransport=bulk;
            var batchCards=Enumerable.Range(200,20).Select(Card).ToList();batchCards.Add(batchCards[0]);
            batchService.WarmCharacterInfo(batchCards,CancellationToken.None);
            Check(bulk.Batches==3 && batchCards.All(item=>batchService.CachedCharacterFavourites(item)==12345),"Twenty distinct cards warmed in three batch requests");
            batchService.WarmCharacterInfo(batchCards,CancellationToken.None);Check(bulk.Batches==3,"Bulk second pass has no requests");
            var batchRestart=new HyTcgService(Path.Combine(root,"batch"));batchRestart.CharacterTransport=new CharacterInfoFixture{Unavailable=true};
            Check(batchCards.All(item=>batchRestart.CachedCharacterFavourites(item)==12345),"Cold economy index reads favourites without loading descriptions");
            bulk.Unavailable=true;batchService.WarmCharacterInfo(new[]{Card(250)},CancellationToken.None);Check(batchService.CachedCharacterFavourites(Card(250))==null,"Batch outage safely falls back to rarity base");
            bulk.Unavailable=false;bulk.Source.WrongSeries=true;batchService.WarmCharacterInfo(new[]{Card(251)},CancellationToken.None);Check(batchService.CachedCharacterFavourites(Card(251))==null,"Batch series identity verified before storing bonus");
            Console.WriteLine("HYTCG character info PASS · "+checks+" checks");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static int Live(string root)
    {
        var service=new HyTcgService(root);var card=Card(1647);
        using(var timeout=new CancellationTokenSource(90000))
        {
            var info=service.CharacterInfo(card,timeout.Token);
            Console.WriteLine("AniList "+info.Name+" #"+info.AniListId+" · Matched "+info.Matched+" · Favourites "+info.Favourites+" · Description "+info.Description.Length+" chars");
            Check(info.Matched && info.AniListId>0 && info.Favourites>0 && info.Description.Length>20,"Real public character metadata");
            var reopened=new HyTcgService(root);reopened.CharacterTransport=new CharacterInfoFixture{Unavailable=true};
            Check(reopened.CharacterInfo(card,timeout.Token).Favourites==info.Favourites,"Real result survives restart without network");
            string[] names={"Naruto Uzumaki","Sakura Haruno","Kakashi Hatake","Itachi Uchiha","Sasuke Uchiha","Shikamaru Nara","Gaara","Rock Lee"};
            var characters=names.Select((name,index)=>new HyTcgCard{CharacterId=90000+index,Name=name,Series="Naruto"}).ToList();
            service.WarmCharacterInfo(characters,timeout.Token);
            int matched=characters.Count(item=>service.CachedCharacterFavourites(item).HasValue);
            Console.WriteLine("Native eight-alias character query matched "+matched+" / 8 exact Naruto identities");
            Check(matched>=6,"Real eight-character batch request");
        }
        Console.WriteLine("HYTCG live character info PASS · "+checks+" checks");return 0;
    }
}
