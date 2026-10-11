using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace HYROLauncherWeb
{
    public sealed class HyTcgDexSeries
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
    }
    public sealed class HyTcgDexPage
    {
        public List<HyTcgCard> Characters { get; set; }
        public List<HyTcgDexSeries> Series { get; set; }
        public int Offset { get; set; }
        public int Total { get; set; }
        public bool HasMore { get; set; }
    }
    public sealed class HyTcgDexArtwork
    {
        public int Id { get; set; }
        public string Thumbnail { get; set; }
        public string PageUrl { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ImageUrl { get; set; }
    }
    public sealed class HyTcgDexArtworkPage
    {
        public HyTcgCard Character { get; set; }
        public List<HyTcgDexArtwork> Items { get; set; }
        public int Page { get; set; }
        public bool HasMore { get; set; }
    }
    public sealed class HyTcgBanner
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Series { get; set; }
        public int AnimeId { get; set; }
        public int AniListId { get; set; }
        public string ImageUrl { get; set; }
        public string ImageData { get; set; }
        public string Day { get; set; }
        public string ExpiresAt { get; set; }
        public int CharacterCount { get; set; }
    }
    public sealed class HyTcgBannerPage
    {
        public string Day { get; set; }
        public List<HyTcgBanner> Items { get; set; }
        public bool Stale { get; set; }
    }
    internal interface IHyTcgDiscoveryTransport
    {
        string Trending(int page,CancellationToken token);
        string Image(string url,CancellationToken token);
    }
    internal sealed class HyTcgDiscoveryTransport : IHyTcgDiscoveryTransport
    {
        private static readonly SemaphoreSlim queryGate=new SemaphoreSlim(1,1);
        private static DateTime nextQuery=DateTime.MinValue;
        internal static bool AllowedImage(string value)
        {
            Uri uri;
            return Uri.TryCreate(value,UriKind.Absolute,out uri) && uri.Scheme=="https" && uri.IsDefaultPort && uri.UserInfo=="" &&
                (uri.Host=="anilist.co" || uri.Host.EndsWith(".anilist.co",StringComparison.Ordinal));
        }
        public string Trending(int page,CancellationToken token)
        {
            if(page<1 || page>2)throw new ArgumentOutOfRangeException("page");
            const string query="query HYTCGTrending($page:Int!) { Page(page:$page,perPage:50) { media(type:ANIME,sort:TRENDING_DESC,isAdult:false) { id title { romaji english native } synonyms bannerImage coverImage { large } } } }";
            return Query(query,new {page=page},token);
        }
        // Character metadata and daily banners use the same native rate gate.
        internal static string Query(string query,object variables,CancellationToken token)
        {
            if(String.IsNullOrWhiteSpace(query) || query.Length>12000)throw new HyTcgException("AniListUnavailable");
            queryGate.Wait(token);
            try
            {
                int delay=(int)Math.Max(0,(nextQuery-DateTime.UtcNow).TotalMilliseconds);
                if(delay>0 && token.WaitHandle.WaitOne(delay))token.ThrowIfCancellationRequested();
                nextQuery=DateTime.UtcNow.AddMilliseconds(2100);
                ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
                var request=(HttpWebRequest)WebRequest.Create("https://graphql.anilist.co");
                request.Method="POST";request.ContentType="application/json";request.Accept="application/json";
                request.UserAgent="HYTCG/1.0 (HYRO Launcher)";request.Timeout=18000;request.ReadWriteTimeout=18000;
                request.AllowAutoRedirect=false;request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                string payload=new JavaScriptSerializer().Serialize(new {query=query,variables=variables});
                using(token.Register(delegate {request.Abort();}))
                using(var trace=ApiActivityLog.Begin("AniList","POST",request.RequestUri.AbsoluteUri))
                {
                    try
                    {
                        byte[] body=Encoding.UTF8.GetBytes(payload);request.ContentLength=body.Length;
                        using(var output=request.GetRequestStream())output.Write(body,0,body.Length);
                        using(var response=(HttpWebResponse)request.GetResponse())
                        {byte[] bytes=ReadBounded(response,2*1024*1024,token);trace.Complete((int)response.StatusCode,bytes.Length);return Encoding.UTF8.GetString(bytes);}
                    }
                    catch(Exception error){trace.Fail(error);token.ThrowIfCancellationRequested();throw;}
                }
            }
            finally {queryGate.Release();}
        }
        public string Image(string url,CancellationToken token)
        {
            if(!AllowedImage(url))throw new HyTcgException("ArtworkInvalid");
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(url);request.Timeout=15000;request.ReadWriteTimeout=15000;
            request.AllowAutoRedirect=false;request.UserAgent="HYTCG/1.0 (HYRO Launcher)";request.Accept="image/*";
            using(token.Register(delegate {request.Abort();}))
            using(var trace=ApiActivityLog.Begin("AniList","IMAGE",url))
            {
                try
                {
                    using(var response=(HttpWebResponse)request.GetResponse())
                    {
                        byte[] bytes=ReadBounded(response,8*1024*1024,token);
                        string mime=response.ContentType.Split(';')[0].ToLowerInvariant();
                        if(!new[]{"image/png","image/jpeg","image/webp","image/gif"}.Contains(mime))throw new HyTcgException("ArtworkInvalid");
                        trace.Complete((int)response.StatusCode,bytes.Length);return "data:"+mime+";base64,"+Convert.ToBase64String(bytes);
                    }
                }
                catch(Exception error){trace.Fail(error);token.ThrowIfCancellationRequested();throw;}
            }
        }
        private static byte[] ReadBounded(HttpWebResponse response,int max,CancellationToken token)
        {
            if(response.ContentLength>max)throw new HyTcgException("ArtworkTooLarge");
            using(var input=response.GetResponseStream())using(var output=new MemoryStream())
            {byte[] buffer=new byte[16384];int count;while((count=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();if(output.Length+count>max)throw new HyTcgException("ArtworkTooLarge");output.Write(buffer,0,count);}return output.ToArray();}
        }
    }
    internal sealed class HyTcgDiscoveryCache
    {
        public string Day {get;set;}
        public List<HyTcgBanner> Items {get;set;}
        public List<int> PreviousAniListIds {get;set;}
        public List<int> PreviousAnimeIds {get;set;}
        public List<string> PreviousSeries {get;set;}
        public List<Dictionary<string,object>> Trending {get;set;}
    }
    internal sealed partial class HyTcgService
    {
        private readonly object discoveryLock=new object();
        private readonly SemaphoreSlim discoveryGate=new SemaphoreSlim(1,1);
        private readonly Dictionary<int,HyTcgCard> dexCharacters=new Dictionary<int,HyTcgCard>();
        private readonly Dictionary<string,HyTcgDexArtwork> dexArtwork=new Dictionary<string,HyTcgDexArtwork>();
        private readonly Dictionary<int,List<HyTcgCard>> seriesCharacters=new Dictionary<int,List<HyTcgCard>>();
        private readonly Dictionary<string,string> bannerImages=new Dictionary<string,string>();
        private HyTcgDiscoveryCache discoveryCache;
        private DateTime nextDiscoveryAttempt=DateTime.MinValue;
        internal IHyTcgDiscoveryTransport DiscoveryTransport=new HyTcgDiscoveryTransport();
        internal Func<DateTime> DiscoveryNow=delegate {return DateTime.UtcNow;};
        private string DiscoveryPath {get{return Path.Combine(root,".cache","hytcg-discovery.bin");}}
        private static readonly byte[] discoveryEntropy=Encoding.UTF8.GetBytes("HYTCG discovery v1");
        private string DiscoveryDay {get{return DiscoveryNow().AddHours(7).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}}
        private string DiscoveryAcdb(string query,HyTcgConfig config,CancellationToken token)
        {
            string text=transport.Json(AcdbEndpoint+"?"+query,"HYTCG/1.0 (HYRO Launcher)",config.RequestTimeoutSeconds,token);
            if(HyTcgAcdbClient.IsBrowserCheck(text))throw new HyTcgException("AcdbBrowserCheck");return text;
        }
        internal static List<Dictionary<string,object>> DiscoveryRows(string json,string key)
        {
            var value=Parser().DeserializeObject(json) as Dictionary<string,object>;object raw;
            if(value==null || !value.TryGetValue(key,out raw) || !(raw is IEnumerable) || raw is string)return new List<Dictionary<string,object>>();
            return ((IEnumerable)raw).Cast<object>().OfType<Dictionary<string,object>>().Take(2000).ToList();
        }
        internal static List<HyTcgCard> ParseSeriesCharacters(string json,int animeId)
        {
            var value=Parser().Deserialize<Dictionary<string,object>>(json);object raw;
            if(value==null || Number(value,"anime_id")!=animeId || !value.TryGetValue("characters",out raw) || !(raw is IEnumerable))throw new HyTcgException("SeriesNotFound");
            string name=First(value,"anime_name");if(name=="")throw new HyTcgException("SeriesNotFound");
            var cards=new List<HyTcgCard>();
            foreach(var entry in ((IEnumerable)raw).Cast<object>().OfType<Dictionary<string,object>>().Take(2000))
            {
                var merged=new Dictionary<string,object>(entry);merged["anime_id"]=animeId;merged["anime_name"]=name;
                int characterId=Number(merged,"id","character_id");if(characterId<1)continue;
                var card=ParseCharacter(Parser().Serialize(merged),characterId);
                if(card!=null)cards.Add(card);
            }
            return cards.GroupBy(card=>card.CharacterId).Select(group=>group.First()).ToList();
        }
        internal HyTcgDexPage DexSearch(string query,string mode,int offset,CancellationToken token)
        {return DexSearch(query,mode,offset,LoadConfig(),token);}
        internal HyTcgDexPage DexSearch(string query,string mode,int offset,HyTcgConfig config,CancellationToken token)
        {
            query=(query??"").Trim();if(query.Length<2 || query.Length>120)throw new HyTcgException("SearchInvalid");
            if(mode!="character" && mode!="series")throw new HyTcgException("SearchInvalid");
            if(offset<0 || offset>2000 || offset%20!=0)throw new HyTcgException("SearchInvalid");
            var rows=DiscoveryRows(DiscoveryAcdb((mode=="series"?"anime_q=":"character_q=")+Uri.EscapeDataString(query),config,token),"search_results");
            var result=new HyTcgDexPage{Characters=new List<HyTcgCard>(),Series=new List<HyTcgDexSeries>(),Offset=offset};
            if(mode=="series")
            {
                var matches=rows.Where(row=>Number(row,"anime_id")>0 && First(row,"anime_name")!="").GroupBy(row=>Number(row,"anime_id")).Select(group=>group.First()).ToList();
                result.Total=matches.Count;result.Series=matches.Skip(offset).Take(20).Select(row=>new HyTcgDexSeries{Id=Number(row,"anime_id"),Name=First(row,"anime_name"),ImageUrl=NormalizeAcdbImage(First(row,"anime_image"))}).ToList();
            }
            else
            {
                var matches=rows.Where(row=>Number(row,"id","character_id")>0).Select(row=>ParseCharacter(Parser().Serialize(row),Number(row,"id","character_id"))).Where(card=>card!=null).GroupBy(card=>card.CharacterId).Select(group=>group.First()).ToList();
                result.Total=matches.Count;result.Characters=matches.Skip(offset).Take(20).ToList();
                lock(discoveryLock){foreach(var card in result.Characters)CacheDexCharacter(card);}
            }
            result.HasMore=offset+20<result.Total;return result;
        }
        private void CacheDexCharacter(HyTcgCard card)
        {if(dexCharacters.Count>=400 && !dexCharacters.ContainsKey(card.CharacterId))dexCharacters.Remove(dexCharacters.Keys.First());dexCharacters[card.CharacterId]=card;}
        internal HyTcgDexPage DexSeriesCharacters(int seriesId,int offset,HyTcgConfig config,CancellationToken token)
        {
            if(seriesId<1 || offset<0 || offset>2000 || offset%20!=0)throw new HyTcgException("SearchInvalid");
            var cards=ReadSeriesPool(seriesId,config,token);
            var result=new HyTcgDexPage{Characters=cards.Skip(offset).Take(20).ToList(),Series=new List<HyTcgDexSeries>(),Offset=offset,Total=cards.Count,HasMore=offset+20<cards.Count};
            lock(discoveryLock){foreach(var card in result.Characters)CacheDexCharacter(card);}return result;
        }
        private List<HyTcgCard> ReadSeriesPool(int seriesId,HyTcgConfig config,CancellationToken token)
        {
            lock(discoveryLock){List<HyTcgCard> cached;if(seriesCharacters.TryGetValue(seriesId,out cached))return cached;}
            var cards=ParseSeriesCharacters(DiscoveryAcdb("anime_id="+seriesId,config,token),seriesId);
            lock(discoveryLock){if(seriesCharacters.Count>=60)seriesCharacters.Remove(seriesCharacters.Keys.First());seriesCharacters[seriesId]=cards;}return cards;
        }
        private HyTcgCard ReadDexCharacter(int id,HyTcgConfig config,CancellationToken token)
        {
            if(id<1 || id>1000000)throw new HyTcgException("CharacterNotFound");
            lock(discoveryLock){HyTcgCard cached;if(dexCharacters.TryGetValue(id,out cached))return cached;}
            var card=ParseCharacter(DiscoveryAcdb("character_id="+id,config,token),id);if(card==null)throw new HyTcgException("CharacterNotFound");
            lock(discoveryLock){CacheDexCharacter(card);}return card;
        }
        internal HyTcgDexArtworkPage DexArtworks(int characterId,int page,HyTcgConfig config,CancellationToken token)
        {
            if(page<1 || page>20)throw new HyTcgException("SearchInvalid");
            var card=ReadDexCharacter(characterId,config,token);
            var result=new HyTcgDexArtworkPage{Character=card,Items=new List<HyTcgDexArtwork>(),Page=page};
            if(String.IsNullOrWhiteSpace(config.ZerochanUsername))return result;
            string agent="HYTCG - "+config.ZerochanUsername,name=Uri.EscapeDataString(card.Name).Replace("%20","+"),series=Uri.EscapeDataString(card.Series??"").Replace("%20","+");
            List<Dictionary<string,object>> entries;
            try{entries=SearchArtworks("https://www.zerochan.net/"+name+(series==""?"":","+series)+"?json&l=12&p="+page+(series==""?"&strict":""),agent,config,token);}
            catch(OperationCanceledException){throw;}
            catch{entries=new List<Dictionary<string,object>>();}
            entries=entries.Where(entry=>!entry.ContainsKey("tags") || (ArtworkMatchesCharacter(entry,card.Name)&&ArtworkMatchesSeries(entry,card.Series))).ToList();
            if(entries.Count==0 && series!="")entries=SearchArtworks("https://www.zerochan.net/"+name+"?json&strict&l=12&p="+page,agent,config,token);
            foreach(var entry in entries.Take(12))
            {
                token.ThrowIfCancellationRequested();int id=Number(entry,"id");
                if(entry.ContainsKey("tags") && (!ArtworkMatchesCharacter(entry,card.Name)||!ArtworkMatchesSeries(entry,card.Series)))continue;
                try
                {
                    var detail=Parser().Deserialize<Dictionary<string,object>>(ZerochanJson("https://www.zerochan.net/"+id+"?json",agent,config,token));
                    if(detail==null || !SuitableArtwork(detail) || !ArtworkMatchesCharacter(detail,card.Name) || !ArtworkMatchesSeries(detail,card.Series))continue;
                    string image=First(detail,"full","large","medium","thumbnail"),thumb=First(detail,"thumbnail","medium","large","full");
                    if(!HyTcgTransport.AllowedImage(image) || !HyTcgTransport.AllowedImage(thumb))continue;
                    var artwork=new HyTcgDexArtwork{Id=id,Thumbnail=thumb,ImageUrl=image,PageUrl="https://www.zerochan.net/"+id,Width=Number(detail,"width"),Height=Number(detail,"height")};
                    result.Items.Add(artwork);lock(discoveryLock){if(dexArtwork.Count>=500)dexArtwork.Remove(dexArtwork.Keys.First());dexArtwork[characterId+":"+id]=artwork;}
                }
                catch(OperationCanceledException){throw;}
                catch{ /* A malformed gallery entry does not discard its siblings. */ }
            }
            result.HasMore=page<20 && entries.Count>=12;
            ApiActivityLog.Note("HYTCG","CharacterDex #"+characterId+" · verified variants "+result.Items.Count);return result;
        }
        internal string DexArtworkImage(int characterId,int artworkId,HyTcgConfig config,CancellationToken token)
        {return DexArtworkImage(characterId,artworkId,false,config,token);}
        internal string DexArtworkImage(int characterId,int artworkId,HyTcgConfig config,CancellationToken token,bool thumbnail)
        {return DexArtworkImage(characterId,artworkId,thumbnail,config,token);}
        internal string DexArtworkImage(int characterId,int artworkId,bool thumbnail,HyTcgConfig config,CancellationToken token)
        {
            HyTcgDexArtwork artwork;lock(discoveryLock){if(!dexArtwork.TryGetValue(characterId+":"+artworkId,out artwork))throw new HyTcgException("ArtworkInvalid");}
            return transport.Image(thumbnail?artwork.Thumbnail:artwork.ImageUrl,"HYTCG - "+config.ZerochanUsername,config.RequestTimeoutSeconds,token);
        }
        internal string DexProfileImage(int characterId,HyTcgConfig config,CancellationToken token)
        {
            var card=ReadDexCharacter(characterId,config,token);
            return transport.Image(card.ProfileImage,"HYTCG/1.0 (HYRO Launcher)",config.RequestTimeoutSeconds,token);
        }
        internal static List<Dictionary<string,object>> ParseTrending(string json)
        {
            var parsed=Parser().Deserialize<Dictionary<string,object>>(json);object data,page,media;
            var values=parsed!=null&&parsed.TryGetValue("data",out data)?data as Dictionary<string,object>:null;
            values=values!=null&&values.TryGetValue("Page",out page)?page as Dictionary<string,object>:null;
            if(values==null || !values.TryGetValue("media",out media) || !(media is IEnumerable))throw new HyTcgException("AniListUnavailable");
            return ((IEnumerable)media).Cast<object>().OfType<Dictionary<string,object>>().Where(value=>Number(value,"id")>0).Take(50).ToList();
        }
        private void LoadDiscoveryCache()
        {
            if(discoveryCache!=null)return;
            try
            {
                if(File.Exists(DiscoveryPath) && (File.GetAttributes(DiscoveryPath)&FileAttributes.ReparsePoint)==0 && new FileInfo(DiscoveryPath).Length<2*1024*1024)
                    discoveryCache=Parser().Deserialize<HyTcgDiscoveryCache>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(DiscoveryPath),discoveryEntropy,DataProtectionScope.CurrentUser)));
            }
            catch{discoveryCache=null;}
            if(discoveryCache==null)discoveryCache=new HyTcgDiscoveryCache{Items=new List<HyTcgBanner>(),PreviousAniListIds=new List<int>(),PreviousAnimeIds=new List<int>(),PreviousSeries=new List<string>(),Trending=new List<Dictionary<string,object>>()};
            if(discoveryCache.Items==null)discoveryCache.Items=new List<HyTcgBanner>();
            if(discoveryCache.PreviousAniListIds==null)discoveryCache.PreviousAniListIds=new List<int>();
            if(discoveryCache.PreviousAnimeIds==null)discoveryCache.PreviousAnimeIds=new List<int>();
            if(discoveryCache.PreviousSeries==null)discoveryCache.PreviousSeries=new List<string>();
        }
        private void SaveDiscoveryCache()
        {
            string folder=Path.GetDirectoryName(DiscoveryPath);Directory.CreateDirectory(folder);
            if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("SaveFailed");
            string temp=DiscoveryPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Parser().Serialize(discoveryCache)),discoveryEntropy,DataProtectionScope.CurrentUser);
                using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                if(File.Exists(DiscoveryPath)){if((File.GetAttributes(DiscoveryPath)&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("SaveFailed");File.Replace(temp,DiscoveryPath,null);}else File.Move(temp,DiscoveryPath);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        private static List<string> TrendingTitles(Dictionary<string,object> value)
        {
            object raw;var title=value.TryGetValue("title",out raw)?raw as Dictionary<string,object>:null;
            var names=new List<string>{First(title,"english"),First(title,"romaji"),First(title,"native")};
            if(value.TryGetValue("synonyms",out raw)&&raw is IEnumerable&&!(raw is string))names.AddRange(((IEnumerable)raw).Cast<object>().OfType<string>());
            return names.Where(name=>!String.IsNullOrWhiteSpace(name)&&name.Length<=160).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
        }
        private HyTcgBanner ResolveBanner(Dictionary<string,object> media,string day,HyTcgConfig config,CancellationToken token)
        {
            var names=TrendingTitles(media);if(names.Count==0)return null;
            var keys=new HashSet<string>(names.Select(SeriesKey),StringComparer.Ordinal);
            foreach(string title in names.Take(2))
            {
                token.ThrowIfCancellationRequested();var rows=DiscoveryRows(DiscoveryAcdb("anime_q="+Uri.EscapeDataString(title),config,token),"search_results");
                var exact=rows.Where(row=>Number(row,"anime_id")>0 && keys.Contains(SeriesKey(First(row,"anime_name")))).GroupBy(row=>Number(row,"anime_id")).Select(group=>group.First()).ToList();
                if(exact.Count!=1)continue; // Ambiguous season/franchise mappings are skipped, never guessed.
                int animeId=Number(exact[0],"anime_id");
                if(discoveryCache.PreviousAnimeIds.Contains(animeId)||discoveryCache.Items.Any(item=>item.AnimeId==animeId)||discoveryCache.PreviousSeries.Contains(SeriesKey(First(exact[0],"anime_name"))))return null;
                var pool=ReadSeriesPool(animeId,config,token);if(pool.Count==0)continue;
                if(!keys.Contains(SeriesKey(pool[0].Series)))continue;
                object raw;var cover=media.TryGetValue("coverImage",out raw)?raw as Dictionary<string,object>:null;
                string image=First(media,"bannerImage");if(!HyTcgDiscoveryTransport.AllowedImage(image))image=First(cover,"large");
                DateTime next=DateTime.ParseExact(day,"yyyy-MM-dd",CultureInfo.InvariantCulture).AddDays(1).AddHours(-7);
                return new HyTcgBanner{Id="limited-"+day+"-"+Number(media,"id")+"-"+animeId,Title=names[0],Series=pool[0].Series,AnimeId=animeId,AniListId=Number(media,"id"),ImageUrl=HyTcgDiscoveryTransport.AllowedImage(image)?image:"",Day=day,ExpiresAt=DateTime.SpecifyKind(next,DateTimeKind.Utc).ToString("o"),CharacterCount=pool.Count};
            }
            return null;
        }
        internal HyTcgBannerPage DiscoveryBanners(HyTcgConfig config,CancellationToken token)
        {
            discoveryGate.Wait(token);
            try
            {
                LoadDiscoveryCache();string day=DiscoveryDay;bool stale=false;
                if(discoveryCache.Day!=day)
                {
                    discoveryCache.PreviousAniListIds=discoveryCache.Items.Select(item=>item.AniListId).ToList();
                    discoveryCache.PreviousAnimeIds=discoveryCache.Items.Select(item=>item.AnimeId).ToList();
                    discoveryCache.PreviousSeries=discoveryCache.Items.Select(item=>SeriesKey(item.Series)).ToList();
                    discoveryCache.Day=day;discoveryCache.Items.Clear();discoveryCache.Trending=null;nextDiscoveryAttempt=DateTime.MinValue;SaveDiscoveryCache();
                }
                if(discoveryCache.Items.Count<2 && DiscoveryNow()>=nextDiscoveryAttempt)
                {
                    nextDiscoveryAttempt=DiscoveryNow().AddMinutes(2);
                    try
                    {
                        if(discoveryCache.Trending==null || discoveryCache.Trending.Count==0)
                        {
                            var rows=ParseTrending(DiscoveryTransport.Trending(1,token));rows.AddRange(ParseTrending(DiscoveryTransport.Trending(2,token)));
                            discoveryCache.Trending=rows.GroupBy(row=>Number(row,"id")).Select(group=>group.First()).Take(100).ToList();SaveDiscoveryCache();
                        }
                        var candidates=discoveryCache.Trending.Where(media=>!discoveryCache.PreviousAniListIds.Contains(Number(media,"id")) && !discoveryCache.Items.Any(item=>item.AniListId==Number(media,"id"))).ToList();
                        var started=DiscoveryNow();int attempts=0;
                        while(candidates.Count>0 && discoveryCache.Items.Count<2 && attempts++<30 && (DiscoveryNow()-started).TotalSeconds<60)
                        {
                            token.ThrowIfCancellationRequested();int index=RandomIndex(candidates.Count);var media=candidates[index];candidates.RemoveAt(index);
                            var banner=ResolveBanner(media,day,config,token);if(banner==null)continue;
                            discoveryCache.Items.Add(banner);SaveDiscoveryCache();
                            ApiActivityLog.Note("HYTCG","Limited banner "+banner.Title+" → ACDB series #"+banner.AnimeId+" · "+banner.CharacterCount+" characters");
                        }
                    }
                    catch(OperationCanceledException){throw;}
                    catch(Exception error){stale=true;ApiActivityLog.Note("HYTCG","Limited discovery unavailable · "+error.GetType().Name);}
                }
                var items=new List<HyTcgBanner>();
                foreach(var stored in discoveryCache.Items.Take(2))
                {
                    var item=Parser().Deserialize<HyTcgBanner>(Parser().Serialize(stored));string image;
                    if(item.ImageUrl!="" && !bannerImages.TryGetValue(item.Id,out image))
                    {
                        try{image=DiscoveryTransport.Image(item.ImageUrl,token);bannerImages[item.Id]=image;}
                        catch(OperationCanceledException){throw;}
                        catch{image="";}
                    }
                    else bannerImages.TryGetValue(item.Id,out image);
                    item.ImageData=image??"";items.Add(item);
                }
                while(bannerImages.Count>6)bannerImages.Remove(bannerImages.Keys.First());
                return new HyTcgBannerPage{Day=day,Items=items,Stale=stale||items.Count<2};
            }
            finally{discoveryGate.Release();}
        }
        internal HyTcgBanner RequireBanner(string id,HyTcgConfig config,CancellationToken token)
        {
            if(String.IsNullOrWhiteSpace(id)||id.Length>100)throw new HyTcgException("BannerExpired");
            discoveryGate.Wait(token);
            try
            {
                LoadDiscoveryCache();
                if(discoveryCache.Day==DiscoveryDay)
                {
                    var active=discoveryCache.Items.FirstOrDefault(item=>item.Id==id&&item.Day==DiscoveryDay);
                    if(active!=null)return Parser().Deserialize<HyTcgBanner>(Parser().Serialize(active));
                }
            }
            finally{discoveryGate.Release();}
            // Refresh only when the signed native cache has no current-day match.
            // A card roll never downloads banner images again or waits for an unrelated image failure.
            var page=DiscoveryBanners(config,token);var banner=page.Items.FirstOrDefault(item=>item.Id==id&&item.Day==DiscoveryDay);
            if(banner==null)throw new HyTcgException("BannerExpired");return banner;
        }
        internal HyTcgResult OpenLimited(string bannerId,HyTcgConfig config,Action<string,int> progress,CancellationToken token)
        {
            var banner=RequireBanner(bannerId,config,token);
            if(!IsLimitedSeriesRoll(RandomIndex(1000000)))return Open(config,progress,token);
            var pool=ReadSeriesPool(banner.AnimeId,config,token);if(pool.Count==0)throw new HyTcgException("SeriesNotFound");
            var card=Parser().Deserialize<HyTcgCard>(Parser().Serialize(pool[RandomIndex(pool.Count)]));
            if(SeriesKey(card.Series)!=SeriesKey(banner.Series)||card.Metadata==null||!card.Metadata.ContainsKey("anime_id")||card.Metadata["anime_id"]!=banner.AnimeId.ToString(CultureInfo.InvariantCulture))throw new HyTcgException("SeriesNotFound");
            var rarity=PickRarity(config,RandomIndex(1000000)/1000000d);card.Rarity=rarity.Name;card.RarityColor=rarity.Color;
            card.CreatedAt=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);card.CardId="HT-"+DateTime.UtcNow.ToString("yyyyMMdd")+"-"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant();
            card.ImageSource="acdb";card.SourceImage=card.ProfileImage;string data="";
            if(!String.IsNullOrWhiteSpace(config.ZerochanUsername))
            {
                progress("artwork",0);
                try
                {
                    string agent="HYTCG - "+config.ZerochanUsername;var detail=FindArtwork(card,agent,config,token);
                    if(detail!=null){string image=First(detail,"full","medium","large","thumbnail");if(!HyTcgTransport.AllowedImage(image))throw new HyTcgException("ArtworkInvalid");data=transport.Image(image,agent,config.RequestTimeoutSeconds,token);card.ImageSource="zerochan";card.SourceImage=image;card.ArtworkPage="https://www.zerochan.net/"+Number(detail,"id");card.OriginalSource=First(detail,"source");}
                    else card.FallbackReason="ZerochanEmpty";
                }
                catch(OperationCanceledException){throw;}
                catch{card.FallbackReason="ZerochanUnavailable";}
            }
            else card.FallbackReason="ZerochanUsernameMissing";
            if(data==""){progress("profile",0);data=transport.Image(card.ProfileImage,"HYTCG/1.0 (HYRO Launcher)",config.RequestTimeoutSeconds,token);}
            token.ThrowIfCancellationRequested();SetArtworkShape(card,data);
            ApiActivityLog.Note("HYTCG","Limited series hit · "+banner.Series+" · #"+card.CharacterId+" "+card.Name);return new HyTcgResult{Card=card,ImageData=data};
        }
        internal static bool IsLimitedSeriesRoll(int roll)
        {if(roll<0||roll>=1000000)throw new ArgumentOutOfRangeException("roll");return roll<300000;}
    }
}
