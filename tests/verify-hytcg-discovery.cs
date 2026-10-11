using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal sealed class DiscoveryFixture : IHyTcgTransport, IHyTcgDiscoveryTransport
{
    internal int TrendingCalls,CharacterCalls,SeriesCalls,ArtCalls;
    internal bool Unavailable,Ambiguous;
    internal string LastSearch;
    internal readonly JavaScriptSerializer JsonParser=new JavaScriptSerializer();
    public string Trending(int page,CancellationToken token)
    {
        TrendingCalls++;if(Unavailable)throw new IOException("fixture unavailable");
        var entries=new List<object>();for(int id=(page-1)*50+1;id<=page*50;id++)entries.Add(new {id=id,title=new {english="Series "+id,romaji="Anime "+id},synonyms=new[]{"Series "+id},bannerImage="https://s4.anilist.co/image-"+id+".jpg"});
        return JsonParser.Serialize(new {data=new {Page=new {media=entries}}});
    }
    public string Json(string url,string agent,int timeout,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(url.Contains("animecharactersdatabase"))
        {
            if(url.Contains("anime_q="))
            {
                LastSearch=url;string name=Uri.UnescapeDataString(url.Split('=').Last());int id=Int32.Parse(name.Split(' ').Last());
                var results=new List<object>{new {anime_id=id,anime_name="Series "+id,anime_image="https://ami.animecharactersdatabase.com/anime.jpg"}};
                if(Ambiguous)results.Add(new {anime_id=id+1000,anime_name="Series "+id,anime_image="https://ami.animecharactersdatabase.com/anime.jpg"});
                return JsonParser.Serialize(new {search_results=results});
            }
            if(url.Contains("anime_id="))
            {SeriesCalls++;int id=Int32.Parse(url.Split('=').Last());return JsonParser.Serialize(new {anime_id=id,anime_name="Series "+id,characters=new[]{new {id=id*10,name="Character "+id,gender="Female",character_image="https://ami.animecharactersdatabase.com/char.jpg"}}});}
            if(url.Contains("character_q="))
            {LastSearch=url;var rows=new List<object>();for(int id=1;id<=27;id++)rows.Add(new {id=id,name="Character "+id,anime_name="Series 1",anime_id=1,character_image="https://ami.animecharactersdatabase.com/char.jpg"});return JsonParser.Serialize(new {search_results=rows});}
            CharacterCalls++;int charId=Int32.Parse(url.Split('=').Last());return JsonParser.Serialize(new {id=charId,name="Character 1",origin="Series 1",character_image="https://ami.animecharactersdatabase.com/char.jpg"});
        }
        ArtCalls++;
        if(url.Contains("l=12"))return "{\"items\":[{\"id\":1},{\"id\":2},{\"id\":3}]}";
        int art=Int32.Parse(new Uri(url).AbsolutePath.Trim('/'));
        return JsonParser.Serialize(new {id=art,full="https://static.zerochan.net/character.full."+art+".jpg",thumbnail="https://s1.zerochan.net/character.240."+art+".jpg",tags=new[]{art==2?"Character 999":"Character 1",art==1?"Series 999":"Series 1"},width=600,height=800});
    }
    public string Image(string url,CancellationToken token){return "data:image/jpeg;base64,fixture";}
    public string Image(string url,string agent,int timeout,CancellationToken token){return "data:image/png;base64,fixture";}
}
internal static class VerifyDiscovery
{
    private static int checks;
    private static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;}
    private static void Throws(Action action,string code){try{action();throw new Exception("Expected "+code);}catch(HyTcgException error){Check(error.Code==code,code);}}
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
            if(args.Length>1 && args[1]=="--live")return Live(root);
            var config=new HyTcgConfig{CharacterIdMin=55,CharacterIdMax=55,MaxCharacterAttempts=2,Concurrency=2,RequestTimeoutSeconds=15,RetryDelayMs=100,ZerochanUsername=""};
            var fixture=new DiscoveryFixture();var service=new HyTcgService(root,fixture);service.DiscoveryTransport=fixture;
            DateTime now=new DateTime(2026,10,9,16,59,0,DateTimeKind.Utc);service.DiscoveryNow=()=>now;
            var page=service.DiscoveryBanners(config,CancellationToken.None);
            Check(page.Day=="2026-10-09"&&page.Items.Count==2&&!page.Stale,"Two current-day banners");
            Check(fixture.TrendingCalls==2,"Top 100 in two paged public queries");
            Check(page.Items.Select(item=>item.AnimeId).Distinct().Count()==2,"Distinct exact ACDB series");
            Check(page.Items.All(item=>item.ExpiresAt.StartsWith("2026-10-09T17:00:00")),"UTC+7 midnight expiry");
            Check(page.Items.All(item=>item.CharacterCount==1&&item.ImageData.StartsWith("data:image/")),"Series pools and native banner images");
            string previous=page.Items[0].Id;
            var persisted=File.ReadAllBytes(Path.Combine(root,".cache","hytcg-discovery.bin"));Check(!System.Text.Encoding.UTF8.GetString(persisted).Contains("Series"),"Encrypted local metadata cache");
            var other=new HyTcgService(root,fixture);other.DiscoveryTransport=fixture;other.DiscoveryNow=()=>now;
            var again=other.DiscoveryBanners(config,CancellationToken.None);Check(again.Items[0].Id==previous&&fixture.TrendingCalls==2,"Stable banners across restart without another trending call");
            now=now.AddMinutes(2);var tomorrow=service.DiscoveryBanners(config,CancellationToken.None);
            Check(tomorrow.Day=="2026-10-10"&&tomorrow.Items.Count==2,"Daily rollover");
            Check(!tomorrow.Items.Any(item=>page.Items.Any(old=>old.AnimeId==item.AnimeId||old.AniListId==item.AniListId)),"No yesterday overlap");
            Throws(()=>service.RequireBanner(previous,config,CancellationToken.None),"BannerExpired");
            var search=service.DexSearch("Character + name","character",0,config,CancellationToken.None);Check(search.Characters.Count==20&&search.Total==27&&search.HasMore,"CharacterDex paging");
            Check(fixture.LastSearch.Contains("%2B"),"Query characters URL encoded");
            Check(HyTcgService.DiscoveryRows("-1","search_results").Count==0,"ACDB no match response handled");
            Check(service.DexSearch("Character","character",20,config,CancellationToken.None).Characters.Count==7,"Last CharacterDex page");
            var series=service.DexSeriesCharacters(4,0,config,CancellationToken.None).Characters.Single();Check(series.Series=="Series 4"&&series.Metadata["anime_id"]=="4","Parent series metadata propagated");
            Throws(()=>service.DexSearch("a","character",0,config,CancellationToken.None),"SearchInvalid");
            Throws(()=>service.DexSearch("hello","unexpected",0,config,CancellationToken.None),"SearchInvalid");
            var rows=HyTcgService.ParseSeriesCharacters("{\"anime_id\":2,\"anime_name\":\"A\",\"characters\":[{\"id\":5,\"name\":\"B\",\"character_image\":\"https://ami.animecharactersdatabase.com/b.jpg\"}]}",2);Check(rows.Count==1&&rows[0].Series=="A","Parse real parent/child series response");
            Throws(()=>HyTcgService.ParseSeriesCharacters("{\"anime_id\":3,\"anime_name\":\"A\",\"characters\":[]}",2),"SeriesNotFound");
            config.ZerochanUsername="fixture";var arts=service.DexArtworks(1,1,config,CancellationToken.None);
            Check(arts.Items.Count==1&&arts.Items[0].Id==3,"Gallery rejects wrong series and wrong character");
            Check(service.DexArtworkImage(1,3,config,CancellationToken.None).StartsWith("data:image/"),"Approved gallery artwork load");
            Throws(()=>service.DexArtworkImage(1,2,config,CancellationToken.None),"ArtworkInvalid");
            Check(HyTcgService.IsLimitedSeriesRoll(0)&&HyTcgService.IsLimitedSeriesRoll(299999)&&!HyTcgService.IsLimitedSeriesRoll(300000)&&!HyTcgService.IsLimitedSeriesRoll(999999),"Exact 30 percent limited roll boundary");
            config.ZerochanUsername="";int hits=0;
            for(int index=0;index<350;index++){var pull=service.OpenLimited(tomorrow.Items[0].Id,config,(phase,attempt)=>{},CancellationToken.None);if(pull.Card.Metadata!=null&&pull.Card.Metadata.ContainsKey("anime_id"))hits++;Check(pull.Card.Rarity!=""&&pull.Card.CardId!="","Limited card initialized");}
            Check(hits>65&&hits<150,"Limited 30 percent distribution");
            fixture.Unavailable=true;now=now.AddDays(1);var unavailable=service.DiscoveryBanners(config,CancellationToken.None);Check(unavailable.Stale&&unavailable.Items.Count==0,"API outage does not sell yesterday's banner");
            Throws(()=>service.RequireBanner(tomorrow.Items[0].Id,config,CancellationToken.None),"BannerExpired");
            Check(!HyTcgDiscoveryTransport.AllowedImage("https://anilist.co.evil.test/image")&&!HyTcgDiscoveryTransport.AllowedImage("https://s4.anilist.co:8443/image")&&HyTcgDiscoveryTransport.AllowedImage("https://s4.anilist.co/image"),"AniList image allowlist");
            var ambiguous=new DiscoveryFixture{Ambiguous=true};var noGuess=new HyTcgService(Path.Combine(root,"ambiguous"),ambiguous);noGuess.DiscoveryTransport=ambiguous;noGuess.DiscoveryNow=()=>now;
            var unmatched=noGuess.DiscoveryBanners(config,CancellationToken.None);Check(unmatched.Items.Count==0&&unmatched.Stale,"Duplicate exact title mappings are skipped");
            Console.WriteLine("HYTCG discovery PASS · "+checks+" checks · "+hits+" / 350 limited hits");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static int Live(string root)
    {
        ApiActivityLog.Start(root);
        try
        {
            string username=File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(),"assets","apikey","zerochanuser.txt")).Trim();
            var config=new HyTcgConfig{CharacterIdMin=1,CharacterIdMax=1000000,MaxCharacterAttempts=15,Concurrency=2,RequestTimeoutSeconds=15,RetryDelayMs=100,ZerochanUsername=username};
            var service=new HyTcgService(root);
            using(var timeout=new CancellationTokenSource(180000))
            {
                Console.WriteLine("Public AniList / ACDB discovery started");
                var banners=service.DiscoveryBanners(config,timeout.Token);
                Console.WriteLine("Banners "+banners.Items.Count+" · "+banners.Day+" · Stale "+banners.Stale);
                foreach(var banner in banners.Items)Console.WriteLine(banner.Title+" → "+banner.Series+" #"+banner.AnimeId+" · "+banner.CharacterCount+" characters · "+banner.ImageData.Length+" image chars");
                Check(banners.Items.Count==2,"Live two matching banner series");
                Check(banners.Items.All(banner=>banner.CharacterCount>0&&banner.ImageData.StartsWith("data:image/")),"Live native banner images");
                var series=service.DexSearch("Naruto","series",0,config,timeout.Token);
                Console.WriteLine("Dex Naruto series "+series.Total);Check(series.Series.Any(item=>item.Name=="Naruto"),"Live series search");
                var characters=service.DexSeriesCharacters(1122,0,config,timeout.Token);Check(characters.Characters.Count>0&&characters.Characters.All(card=>card.Series=="Naruto"&&card.Metadata["anime_id"]=="1122"),"Live exact series character pool");
                var search=service.DexSearch("Alina Gray","character",0,config,timeout.Token);Check(search.Characters.Any(card=>card.CharacterId==99991),"Live character search");
                var profile=service.DexProfileImage(99991,config,timeout.Token);Check(profile.StartsWith("data:image/"),"Live native profile image");
                var arts=service.DexArtworks(99991,1,config,timeout.Token);Console.WriteLine("Dex Alina Gray variants "+arts.Items.Count);Check(arts.Items.Count>0,"Live exact character and series artwork variants");
                Check(service.DexArtworkImage(99991,arts.Items[0].Id,true,config,timeout.Token).StartsWith("data:image/"),"Live native approved artwork thumbnail");
                File.WriteAllText(Path.Combine(root,"report.json"),new JavaScriptSerializer().Serialize(new {Status="PASS",Banners=banners.Items.Select(item=>new {item.Title,item.Series,item.AnimeId,item.CharacterCount}),DexSeries=series.Total,DexCharacters=characters.Total,DexArtwork=arts.Items.Count,Checks=checks}));
            }
            Console.WriteLine("HYTCG discovery live PASS · "+checks+" checks");return 0;
        }
        finally{ApiActivityLog.Stop();}
    }
}
