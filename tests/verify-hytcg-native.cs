using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;
using System.Web.Script.Serialization;
using HYROLauncherWeb;

internal sealed class FixtureTransport : IHyTcgTransport
{
    internal int AcdbRequests,ZerochanRequests,ImageRequests;
    internal int InvalidBeforeValid;
    internal bool ZerochanFails,EmptyArtwork,ArtworkFails,AcdbFails,WrongSeries,MissingSeries,JsonMalformed;
    internal string AcdbBody,AcdbCode;
    internal int AcdbHttpStatus;
    internal string LastSearch,LastAgent;
    internal static string Character(int id) { return "{\"id\":"+id+",\"name\":\"Hinata Hyuuga\",\"origin\":\"Naruto\",\"gender\":\"Female\",\"character_image\":\"http://ami.animecharactersdatabase.com/./images/55.jpg\",\"desc\":\"A character from Naruto.\"}"; }
    public string Json(string url,string agent,int timeout,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(url.Contains("animecharactersdatabase.com"))
        {
            AcdbRequests++;
            if(AcdbCode!=null) throw new HyTcgException(AcdbCode);
            if(AcdbHttpStatus!=0) throw new HyTcgHttpException(AcdbHttpStatus);
            if(AcdbFails) throw new System.Net.WebException("fixture connection unavailable");
            int id=Int32.Parse(url.Split('=').Last());
            if(AcdbBody!=null) return AcdbBody;
            return AcdbRequests<=InvalidBeforeValid?"-1":Character(id);
        }
        ZerochanRequests++;LastAgent=agent;
        if(ZerochanFails) throw new System.Net.WebException("fixture Zerochan unavailable");
        if(url.Contains("l=50")) {LastSearch=url;if(JsonMalformed)return url.Contains("?xml")?"<rss xmlns:media='http://search.yahoo.com/mrss/'><channel><item><link>http://www.zerochan.net/3725745</link><media:keywords>Naruto, Hinata Hyuuga</media:keywords></item></channel></rss>":"invalid JSON";return EmptyArtwork?"{\"items\":[]}":"{\"items\":[{\"id\":3085577},{\"id\":3725745}]}";}
        return "{\"full\":\"https://static.zerochan.net/Hinata.full.3085577.png\",\"tags\":[\"Hinata Hyuuga\""+(MissingSeries?"":",\""+(WrongSeries?"Bleach":"Naruto")+"\"")+"],\"source\":\"https://www.pixiv.net/artworks/123\"}";
    }
    public string Image(string url,string agent,int timeout,CancellationToken token)
    {
        ImageRequests++;token.ThrowIfCancellationRequested();
        if(ArtworkFails&&url.Contains("zerochan"))throw new IOException("fixture missing image");
        return "data:image/png;base64,fixture";
    }
}
internal static class VerifyHyTcg
{
    private static void Check(bool ok,string reason) {if(!ok)throw new Exception(reason);}
    private static void Throws(Action action,string code)
    {
        try{action();throw new Exception("Expected "+code);}catch(HyTcgException ex){Check(ex.Code==code,"Unexpected error "+ex.Code);}
    }
    public static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
            var config=new JavaScriptSerializer().Deserialize<HyTcgConfig>(File.ReadAllText("assets/builtin-plugins/hytcg/config.json"));
            config.CharacterIdMin=55;config.CharacterIdMax=55;config.RetryDelayMs=100;
            HyTcgService.ValidateConfig(config);
            Check(HyTcgService.PickRarity(config,0).Name=="Common","Common boundary");
            Check(HyTcgService.PickRarity(config,.65).Name=="Uncommon","Uncommon boundary");
            Check(HyTcgService.PickRarity(config,.87).Name=="Rare","Rare boundary");
            Check(HyTcgService.PickRarity(config,.943).Name=="SR","SR boundary");
            Check(HyTcgService.PickRarity(config,.983).Name=="SSR","SSR boundary");
            Check(HyTcgService.PickRarity(config,.999999).Name=="UR","UR boundary");
            var sample=new Dictionary<string,int>();foreach(string r in HyTcgService.Rarities)sample[r]=0;
            for(int i=0;i<12000;i++)sample[HyTcgService.PickRarity(config,HyTcgService.RandomIndex(1000000)/1000000d).Name]++;
            foreach(var rate in config.DropRates)Check(Math.Abs(sample[rate.Name]/120d-rate.Weight)<2,"Random distribution: "+rate.Name);
            config.ZerochanUsername="HYTCG-Test";
            var invalid=new JavaScriptSerializer().Deserialize<HyTcgConfig>(new JavaScriptSerializer().Serialize(config));invalid.DropRates[0].Weight=44;
            Check(HyTcgService.PickRarity(invalid,.64).Name=="Common"&&invalid.DropRates[0].Weight==65,"Config can alter compiled odds");
            Check(HyTcgService.ParseCharacter("-1",55)==null,"Negative API response");
            Check(HyTcgService.ParseCharacter("<html>blocked</html>",55)==null,"HTML API response");
            string challenge="<!DOCTYPE html><html><head><title>Checking browser...</title></head><body><img src=\"/xbotcheck-image.svg\"></body></html>";
            Check(HyTcgAcdbClient.IsBrowserCheck(challenge)&&!HyTcgAcdbClient.IsBrowserCheck(FixtureTransport.Character(55)),"Browser check classification");
            foreach(int httpStatus in new[]{200,503})
            {
                byte[] raw=Encoding.UTF8.GetBytes("HTTP/1.1 200 Connection established\r\n\r\nHTTP/2 "+httpStatus+"\r\nContent-Type: text/html\r\n\r\n"+challenge+"\nHYTCG_HTTP:"+httpStatus);
                var reply=HyTcgAcdbClient.ParseReply(raw,4096);
                Check(reply.Status==httpStatus&&Encoding.UTF8.GetString(reply.Body)==challenge&&reply.ContentType=="text/html","curl HTTP/body decoding");
                Throws(()=>HyTcgAcdbClient.ValidateReply(reply),"AcdbBrowserCheck");
            }
            Throws(()=>HyTcgAcdbClient.ValidateReply(new HyTcgAcdbClient.Reply {Status=429,Body=new byte[0]}),"AcdbRateLimited");
            byte[] binary=new byte[]{137,80,78,71,13,10,0,255,200};
            byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: image/png\r\n\r\n"),footer=Encoding.ASCII.GetBytes("\nHYTCG_HTTP:200");
            byte[] imageReply=header.Concat(binary).Concat(footer).ToArray();
            Check(HyTcgAcdbClient.ParseReply(imageReply,100).Body.SequenceEqual(binary),"curl binary artwork was corrupted");
            Throws(()=>HyTcgAcdbClient.ParseReply(imageReply,3),"ArtworkTooLarge");
            var starts=new List<long>();var clock=Stopwatch.StartNew();
            Task.WaitAll(Enumerable.Range(0,3).Select(i=>Task.Run(()=>HyTcgAcdbClient.RunThrottled(()=>{lock(starts)starts.Add(clock.ElapsedMilliseconds);return 0;},CancellationToken.None))).ToArray());
            starts.Sort();Check(starts[1]-starts[0]>=1000&&starts[2]-starts[1]>=1000,"Concurrent ACDB requests exceed one per second");
            using(var cancelWait=new CancellationTokenSource())
            {
                cancelWait.CancelAfter(30);var wait=Stopwatch.StartNew();
                try {HyTcgAcdbClient.RunThrottled<int>(()=>{throw new Exception("Canceled request started");},cancelWait.Token);throw new Exception("Rate wait ignored cancellation");}
                catch(OperationCanceledException) {Check(wait.ElapsedMilliseconds<500,"Slow ACDB rate-wait cancellation");}
            }
            Check(HyTcgService.ParseCharacter(FixtureTransport.Character(99),55)==null,"Mismatched character ID");
            Check(HyTcgService.ParseArtworks("{\"items\":[{\"id\":1,\"tags\":[\"Nude\"]},{\"id\":2,\"tags\":[\"Portrait\"]}]}").Count==1,"Explicit gallery artwork was selected");
            var character=HyTcgService.ParseCharacter(FixtureTransport.Character(55),55);
            Check(character.Name=="Hinata Hyuuga"&&character.Series=="Naruto"&&character.ProfileImage.StartsWith("https:"),"Character parsing + HTTPS normalization");
            Check(HyTcgService.ParseCharacter(FixtureTransport.Character(55).Replace("ami.animecharactersdatabase.com","evil.local"),55)==null,"Untrusted artwork host accepted");
            Check(!HyTcgTransport.AllowedImage("https://zerochan.net.evil.local/1.png")&&!HyTcgTransport.AllowedImage("file:///C:/test.png")&&!HyTcgTransport.AllowedImage("https://user@static.zerochan.net/test.png"),"Image URL allowlist");
            var transport=new FixtureTransport {InvalidBeforeValid=2};var service=new HyTcgService(root,transport);
            var opened=service.Open(config,(phase,attempt)=>{},CancellationToken.None);
            Check(transport.AcdbRequests==3&&opened.Card.CharacterId==55,"Invalid IDs were not retried");
            Check(opened.Card.ImageSource=="zerochan"&&transport.LastSearch.Contains("Hinata+Hyuuga,Naruto?json")&&transport.LastAgent=="HYTCG - HYTCG-Test","Zerochan variant / character+series encoding / identity");
            transport=new FixtureTransport {JsonMalformed=true};opened=new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None);
            Check(opened.Card.ImageSource=="zerochan"&&transport.LastSearch.Contains("?xml"),"XML API fallback did not recover ALT");
            Check(HyTcgService.ParseArtworkXml("<rss xmlns:media='http://search.yahoo.com/mrss/'><channel><item><link>http://www.zerochan.net/1</link><media:keywords>Naruto, Nude</media:keywords></item><item><link>https://evil.local/2</link></item></channel></rss>").Count==0,"XML content/host filters");
            try{HyTcgService.ParseArtworkXml("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///C:/secret'>]><rss><item><link>&x;</link></item></rss>");throw new Exception("XML entities accepted");}catch(System.Xml.XmlException){}
            transport=new FixtureTransport {WrongSeries=true};opened=new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None);
            Check(opened.Card.ImageSource=="acdb" && transport.ImageRequests==1 && transport.ZerochanRequests<=5,"Wrong-series art was accepted / candidate budget");
            transport=new FixtureTransport {MissingSeries=true};opened=new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None);
            Check(opened.Card.ImageSource=="acdb","Unverified series was accepted");
            var tagParser=new JavaScriptSerializer();
            Check(HyTcgService.ArtworkMatchesSeries(tagParser.Deserialize<Dictionary<string,object>>("{\"tags\":[\"Honkai: Star Rail\"]}"),"Honkai Star Rail"),"Punctuation normalization");
            Check(HyTcgService.ArtworkMatchesSeries(tagParser.Deserialize<Dictionary<string,object>>("{\"anime\":\"Boku no Hero Academia\"}"),"My Hero Academia"),"Known series alias");
            Check(!HyTcgService.ArtworkMatchesSeries(tagParser.Deserialize<Dictionary<string,object>>("{\"tags\":[\"Sword Art Online Alternative\"]}"),"Sword Art Online"),"Unrelated partial series accepted");
            Check(HyTcgService.ArtworkMatchesCharacter(tagParser.Deserialize<Dictionary<string,object>>("{\"primary\":\"Hyuuga Hinata\"}"),"Hinata Hyuuga"),"Surname order prevents artwork");
            Check(!HyTcgService.ArtworkMatchesCharacter(tagParser.Deserialize<Dictionary<string,object>>("{\"tags\":[\"Uzumaki Naruto\",\"Naruto\"]}"),"Hinata Hyuuga"),"Wrong character from same series accepted");
            Check(HyTcgService.ArtworkMatchesSeries(tagParser.Deserialize<Dictionary<string,object>>("{\"tags\":[\"Magia Record: Mahou Shoujo Madoka☆Magica Gaiden\"]}"),"Puella Magi Madoka Magica Side Story: Magia Record"),"ACDB/Zerochan full series titles prevent ALT");
            transport=new FixtureTransport {ZerochanFails=true};opened=new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None);
            Check(opened.Card.ImageSource=="acdb"&&opened.Card.FallbackReason=="ZerochanUnavailable","API fallback");
            transport=new FixtureTransport {EmptyArtwork=true};opened=new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None);
            Check(opened.Card.ImageSource=="acdb"&&opened.Card.FallbackReason=="ZerochanEmpty","Empty search fallback");
            transport=new FixtureTransport {ArtworkFails=true};opened=new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None);
            Check(opened.Card.ImageSource=="acdb"&&transport.ImageRequests==2,"Artwork download fallback");
            config.ZerochanUsername="";transport=new FixtureTransport();service=new HyTcgService(root,transport);
            opened=service.Open(config,(p,a)=>{},CancellationToken.None);
            Check(transport.ZerochanRequests==0&&opened.Card.ImageSource=="acdb","No anonymous Zerochan requests");
            transport=new FixtureTransport {InvalidBeforeValid=100};
            Throws(()=>new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None),"CharacterNotFound");
            Check(transport.AcdbRequests==15,"Retry budget exceeded");
            transport=new FixtureTransport {AcdbFails=true};
            Throws(()=>new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None),"AcdbUnavailable");
            Check(transport.AcdbRequests==3,"Repeated outage was hammered");
            transport=new FixtureTransport {AcdbBody=challenge};
            Throws(()=>new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None),"AcdbBrowserCheck");
            Check(transport.AcdbRequests==1&&transport.ZerochanRequests==0,"Browser check retried as missing ID");
            transport=new FixtureTransport {AcdbCode="AcdbBrowserCheck"};
            Throws(()=>new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None),"AcdbBrowserCheck");
            Check(transport.AcdbRequests==1,"Native browser error swallowed by retry loop");
            transport=new FixtureTransport {AcdbHttpStatus=404};
            Throws(()=>new HyTcgService(root,transport).Open(config,(p,a)=>{},CancellationToken.None),"CharacterNotFound");
            Check(transport.AcdbRequests==15,"404 incorrectly treated as connection outage");
            using(var canceled=new CancellationTokenSource()){canceled.Cancel();try{service.Open(config,(p,a)=>{},canceled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
            service.SaveConfig(config);Check(service.LoadConfig().DropRates[5].Weight==.6,"Config roundtrip / fixed UR odds");
            DateTime economyNow=DateTime.UtcNow;
            var wallet=new HyTcgEconomy(root,()=>economyNow,new HyTcgUserProtector(),()=>TimeSpan.Zero);wallet.FinishMigration();
            service=new HyTcgService(root,transport,wallet);
            string png;
            using(var bitmap=new Bitmap(750,1050))using(var graphics=Graphics.FromImage(bitmap))using(var output=new MemoryStream())
            {graphics.Clear(Color.DarkViolet);bitmap.Save(output,ImageFormat.Png);png="data:image/png;base64,"+Convert.ToBase64String(output.ToArray());}
            opened.Card.Name="../Hinata: 日向 \\ test";opened.Card.Series="NARUTO 日本語";
            opened.Card.Rarity="Common";opened.Card.RarityColor="#b1becd";
            var reserve=wallet.Reserve(1);wallet.Begin(reserve.Tokens[0]);wallet.AuthorizeCard(reserve.Tokens[0],opened.Card);
            string saved=service.SaveCard(opened.Card,png,reserve.Tokens[0]),again=service.SaveCard(opened.Card,png,reserve.Tokens[0]);
            Check(saved==again&&Path.GetDirectoryName(saved)==service.CardsDirectory,"Idempotent save / path safety");
            Check(!Path.GetFileName(saved).Contains(".."),"Unsafe filename");
            using(var stream=new MemoryStream(HyTcgStorage.ImageBytes(File.ReadAllBytes(saved))))using(var image=Image.FromStream(stream))Check(image.Width==750&&image.Height==1050,"Encrypted PNG dimensions");
            var recovered=new JavaScriptSerializer().Deserialize<HyTcgCard>(HyTcgService.Png.Metadata(saved));
            Check(recovered.Name==opened.Card.Name&&recovered.Series==opened.Card.Series&&recovered.CardId==opened.Card.CardId,"PNG Unicode metadata roundtrip");
            Throws(()=>service.SaveCard(opened.Card,"data:image/png;base64,NOTPNG",reserve.Tokens[0]),"CardInvalid");
            File.WriteAllText(Path.Combine(service.CardsDirectory,"broken.png"),"not png");
            for(int i=0;i<43;i++){economyNow=economyNow.AddMinutes(20);opened.Card.CardId="HT-20261008-"+i.ToString("X12");opened.Card.Name="Fixture "+(43-i).ToString("D2");opened.Card.Rarity="Rare";var ticket=wallet.Reserve(1);wallet.Begin(ticket.Tokens[0]);wallet.AuthorizeCard(ticket.Tokens[0],opened.Card);service.SaveCard(opened.Card,png,ticket.Tokens[0]);}
            var page=service.Collection(0,"","");Check(page.Total==44&&page.Items.Count==40&&page.HasMore,"Collection first page / ignores broken files");
            var second=service.Collection(40,"","");Check(second.Items.Count==4&&!second.HasMore,"Collection next page");
            Check(service.Collection(0,"Rare","日本語").Total==43,"Collection rarity / text filter");
            var namePage=service.Collection(0,"","",false,"name").Items.Concat(service.Collection(40,"","",false,"name").Items).Select(item=>item.Card.Name).ToArray();
            Check(namePage.SequenceEqual(namePage.OrderBy(name=>name,StringComparer.OrdinalIgnoreCase)),"Name sorting happens after pagination");
            var rarityPage=service.Collection(0,"","",false,"rarity").Items.Concat(service.Collection(40,"","",false,"rarity").Items).ToArray();
            Check(rarityPage.Take(43).All(item=>item.Card.Rarity=="Rare")&&rarityPage[43].Card.Rarity=="Common","Rarity sorting happens after pagination");
            Check(Directory.GetFiles(service.CardsDirectory).All(f=>Path.GetExtension(f)==".hycard" || Path.GetFileName(f)=="broken.png"),"Unexpected metadata database or sidecar");
            Console.WriteLine("PASS: local RNG, ID/retry, ACDB 200/503 browser-check detection, shared 1 rps/cancellation, curl binary decoding/limits, outage/404, Zerochan variants/fallback, safe PNG/metadata/Collection.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
