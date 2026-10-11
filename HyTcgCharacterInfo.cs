using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace HYROLauncherWeb
{
    public sealed class HyTcgCharacterInfo
    {
        public int CharacterId {get;set;}
        public int AniListId {get;set;}
        public bool Matched {get;set;}
        public string Name {get;set;}
        public string NativeName {get;set;}
        public string Series {get;set;}
        public string Description {get;set;}
        public int? Favourites {get;set;}
        public string SiteUrl {get;set;}
        public string Gender {get;set;}
        public string Age {get;set;}
        public string BloodType {get;set;}
        public string DateOfBirth {get;set;}
        public string CachedAt {get;set;}
    }
    internal interface IHyTcgCharacterTransport
    {
        string Search(string name,CancellationToken token);
        string Detail(int id,CancellationToken token);
    }
    internal interface IHyTcgCharacterBatchTransport
    {
        List<string> SearchMany(IList<string> names,CancellationToken token);
    }
    internal sealed class HyTcgCharacterTransport : IHyTcgCharacterTransport, IHyTcgCharacterBatchTransport
    {
        private const string Fields="id name { full native alternative } favourites description(asHtml:false) gender age bloodType dateOfBirth { year month day } siteUrl media(page:1,perPage:25,sort:POPULARITY_DESC) { nodes { id title { english romaji native } synonyms } }";
        public string Search(string name,CancellationToken token)
        {
            string query="query HYTCGCharacterSearch($name:String!) { Page(page:1,perPage:25) { characters(search:$name,sort:SEARCH_MATCH) { "+Fields+" } } }";
            return HyTcgDiscoveryTransport.Query(query,new {name=name},token);
        }
        public string Detail(int id,CancellationToken token)
        {
            string query="query HYTCGCharacterInfo($id:Int!) { Character(id:$id) { "+Fields+" } }";
            return HyTcgDiscoveryTransport.Query(query,new {id=id},token);
        }
        public List<string> SearchMany(IList<string> names,CancellationToken token)
        {
            if(names==null || names.Count<1 || names.Count>8)throw new ArgumentOutOfRangeException("names");
            var declarations=new List<string>();var selections=new List<string>();var variables=new Dictionary<string,object>();
            for(int index=0;index<names.Count;index++)
            {
                string key="n"+index;declarations.Add("$"+key+":String!");variables[key]=names[index];
                selections.Add("c"+index+": Page(page:1,perPage:25) { characters(search:$"+key+",sort:SEARCH_MATCH) { "+Fields+" } }");
            }
            string response=HyTcgDiscoveryTransport.Query("query HYTCGCharacters("+String.Join(",",declarations.ToArray())+") { "+String.Join(" ",selections.ToArray())+" }",variables,token);
            var parser=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=3*1024*1024};var result=parser.DeserializeObject(response) as Dictionary<string,object>;object raw;
            if(result==null || result.ContainsKey("errors") || !result.TryGetValue("data",out raw) || !(raw is Dictionary<string,object>))throw new HyTcgException("AniListUnavailable");
            var data=(Dictionary<string,object>)raw;var rows=new List<string>();
            for(int index=0;index<names.Count;index++)
            {if(!data.TryGetValue("c"+index,out raw) || !(raw is Dictionary<string,object>))throw new HyTcgException("AniListUnavailable");rows.Add(parser.Serialize(new {data=new {Page=raw}}));}
            return rows;
        }
    }
    internal sealed class HyTcgCharacterCacheEntry
    {
        public string Identity {get;set;}
        public HyTcgCharacterInfo Info {get;set;}
    }
    internal sealed partial class HyTcgService
    {
        private readonly object characterInfoLock=new object();
        private readonly SemaphoreSlim characterInfoGate=new SemaphoreSlim(1,1);
        private readonly Dictionary<string,HyTcgCharacterCacheEntry> characterInfoMemory=new Dictionary<string,HyTcgCharacterCacheEntry>();
        private readonly Queue<string> characterInfoOrder=new Queue<string>();
        private static readonly byte[] characterInfoEntropy=Encoding.UTF8.GetBytes("HYTCG AniList character cache v1");
        private static readonly byte[] characterFavouritesEntropy=Encoding.UTF8.GetBytes("HYTCG AniList favourites cache v1");
        private Dictionary<string,int> characterFavourites;
        internal IHyTcgCharacterTransport CharacterTransport=new HyTcgCharacterTransport();
        private string CharacterInfoDirectory {get{return Path.Combine(root,".cache","hytcg-characters");}}
        private static string CharacterIdentity(HyTcgCard card)
        {
            if(card==null || card.CharacterId<1 || String.IsNullOrWhiteSpace(card.Name) || card.Name.Length>180 || (card.Series??"").Length>240)throw new HyTcgException("CharacterNotFound");
            return card.CharacterId.ToString(CultureInfo.InvariantCulture)+"\n"+CharacterKey(card.Name)+"\n"+SeriesKey(card.Series);
        }
        private static string CharacterCacheKey(string identity)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-","").ToLowerInvariant();}
        private static HyTcgCharacterInfo CopyCharacterInfo(HyTcgCharacterInfo info)
        {return Parser().Deserialize<HyTcgCharacterInfo>(Parser().Serialize(info));}
        internal int? CachedCharacterFavourites(HyTcgCard card)
        {
            try
            {
                string key=CharacterCacheKey(CharacterIdentity(card));
                lock(characterInfoLock){LoadCharacterFavourites();int value;return characterFavourites.TryGetValue(key,out value)?(int?)value:null;}
            }
            catch{return null;}
        }
        internal HyTcgCharacterInfo CachedCharacterInfo(HyTcgCard card)
        {return ReadCharacterInfo(CharacterIdentity(card));}
        private HyTcgCharacterInfo ReadCharacterInfo(string identity)
        {
            string key=CharacterCacheKey(identity);
            lock(characterInfoLock)
            {
                HyTcgCharacterCacheEntry entry;
                if(characterInfoMemory.TryGetValue(key,out entry))return CopyCharacterInfo(entry.Info);
                string file=Path.Combine(CharacterInfoDirectory,key+".bin");
                try
                {
                    if(!File.Exists(file) || !SafeCharacterCacheDirectory(false) || (File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0 || new FileInfo(file).Length>65536)return null;
                    byte[] bytes=ProtectedData.Unprotect(File.ReadAllBytes(file),characterInfoEntropy,DataProtectionScope.CurrentUser);
                    entry=Parser().Deserialize<HyTcgCharacterCacheEntry>(Encoding.UTF8.GetString(bytes));
                    if(entry==null || entry.Identity!=identity || !ValidCharacterInfo(entry.Info))return null;
                    RememberCharacterInfo(key,entry);return CopyCharacterInfo(entry.Info);
                }
                catch{return null;}
            }
        }
        private static bool ValidCharacterInfo(HyTcgCharacterInfo info)
        {
            DateTime cached;
            if(info==null || info.CharacterId<1 || (info.Description??"").Length>5000 || (info.Name??"").Length>180 || (info.NativeName??"").Length>180 || (info.Series??"").Length>240 || !DateTime.TryParse(info.CachedAt,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out cached))return false;
            if(!info.Matched)return info.AniListId==0 && !info.Favourites.HasValue && String.IsNullOrEmpty(info.SiteUrl);
            Uri uri;
            return info.AniListId>0 && (!info.Favourites.HasValue || info.Favourites>=0) && Uri.TryCreate(info.SiteUrl,UriKind.Absolute,out uri) && uri.Scheme=="https" && uri.Host=="anilist.co" && uri.IsDefaultPort && uri.UserInfo=="" && uri.AbsolutePath=="/character/"+info.AniListId;
        }
        private bool SafeCharacterCacheDirectory(bool create)
        {
            string cache=Path.Combine(root,".cache");
            if(Directory.Exists(cache) && (File.GetAttributes(cache)&FileAttributes.ReparsePoint)!=0)return false;
            if(Directory.Exists(CharacterInfoDirectory) && (File.GetAttributes(CharacterInfoDirectory)&FileAttributes.ReparsePoint)!=0)return false;
            if(create)Directory.CreateDirectory(CharacterInfoDirectory);
            return Directory.Exists(CharacterInfoDirectory);
        }
        private void RememberCharacterInfo(string key,HyTcgCharacterCacheEntry entry)
        {
            LoadCharacterFavourites();
            if(entry.Info.Matched && entry.Info.Favourites.HasValue)characterFavourites[key]=entry.Info.Favourites.Value;else characterFavourites.Remove(key);
            if(!characterInfoMemory.ContainsKey(key))characterInfoOrder.Enqueue(key);
            characterInfoMemory[key]=entry;
            while(characterInfoMemory.Count>256 && characterInfoOrder.Count>0)characterInfoMemory.Remove(characterInfoOrder.Dequeue());
        }
        private void LoadCharacterFavourites()
        {
            if(characterFavourites!=null)return;
            characterFavourites=new Dictionary<string,int>(StringComparer.Ordinal);
            string path=Path.Combine(CharacterInfoDirectory,"favourites.bin");
            try
            {
                if(!File.Exists(path) || !SafeCharacterCacheDirectory(false) || (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0 || new FileInfo(path).Length>1024*1024)return;
                var values=Parser().Deserialize<Dictionary<string,int>>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),characterFavouritesEntropy,DataProtectionScope.CurrentUser)));
                if(values==null || values.Count>3000 || values.Any(pair=>!Regex.IsMatch(pair.Key,@"^[0-9a-f]{64}$") || pair.Value<0))return;
                characterFavourites=values;
            }
            catch{}
        }
        private void SaveCharacterFavourites()
        {
            string path=Path.Combine(CharacterInfoDirectory,"favourites.bin"),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Parser().Serialize(characterFavourites)),characterFavouritesEntropy,DataProtectionScope.CurrentUser);
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
                if(File.Exists(path)){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("SaveFailed");File.Replace(temp,path,null);}else File.Move(temp,path);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        private void SaveCharacterInfo(string identity,HyTcgCharacterInfo info)
        {
            string key=CharacterCacheKey(identity);
            var entry=new HyTcgCharacterCacheEntry{Identity=identity,Info=CopyCharacterInfo(info)};
            lock(characterInfoLock)
            {
                if(!SafeCharacterCacheDirectory(true))throw new HyTcgException("SaveFailed");
                string file=Path.Combine(CharacterInfoDirectory,key+".bin"),temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Parser().Serialize(entry)),characterInfoEntropy,DataProtectionScope.CurrentUser);
                    if(bytes.Length>65536)throw new HyTcgException("SaveFailed");
                    using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
                    if(File.Exists(file))
                    {if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new HyTcgException("SaveFailed");File.Replace(temp,file,null);}
                    else File.Move(temp,file);
                    RememberCharacterInfo(key,entry);
                    // Bound disk metadata without touching cards, wallet or other API caches.
                    var files=new DirectoryInfo(CharacterInfoDirectory).GetFiles("*.bin").Where(value=>Regex.IsMatch(value.Name,@"^[0-9a-f]{64}\.bin$") && (value.Attributes&FileAttributes.ReparsePoint)==0).OrderByDescending(value=>value.LastWriteTimeUtc).ToList();
                    foreach(var old in files.Skip(3000))try{old.Delete();characterFavourites.Remove(Path.GetFileNameWithoutExtension(old.Name));}catch{}
                    SaveCharacterFavourites();
                }
                finally{if(File.Exists(temp))File.Delete(temp);}
            }
        }
        internal HyTcgCharacterInfo CharacterInfo(HyTcgCard card,CancellationToken token)
        {
            string identity=CharacterIdentity(card);token.ThrowIfCancellationRequested();
            var cached=ReadCharacterInfo(identity);if(cached!=null)return cached;
            characterInfoGate.Wait(token);
            try
            {
                cached=ReadCharacterInfo(identity);if(cached!=null)return cached;
                var info=new HyTcgCharacterInfo{CharacterId=card.CharacterId,Name=card.Name,Series=card.Series??"",NativeName="",Description="",SiteUrl="",Gender="",Age="",BloodType="",DateOfBirth="",CachedAt=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)};
                // Blank series cannot establish identity safely across unrelated franchises.
                if(!String.IsNullOrWhiteSpace(card.Series))
                {
                    var candidates=CharacterSearchRows(CharacterTransport.Search(card.Name,token));
                    var matched=candidates.Where(value=>MatchesCharacterInfo(value,card)).GroupBy(value=>Number(value,"id")).Select(group=>group.First()).Take(2).ToList();
                    if(matched.Count==1)
                    {
                        var detail=matched[0].ContainsKey("description") && matched[0].ContainsKey("favourites")?matched[0]:CharacterDetailRow(CharacterTransport.Detail(Number(matched[0],"id"),token));
                        if(detail==null || Number(detail,"id")!=Number(matched[0],"id"))throw new HyTcgException("AniListUnavailable");
                        if(MatchesCharacterInfo(detail,card))info=ParseCharacterInfo(detail,card);
                    }
                }
                token.ThrowIfCancellationRequested();SaveCharacterInfo(identity,info);return CopyCharacterInfo(info);
            }
            finally{characterInfoGate.Release();}
        }
        internal void WarmCharacterInfo(IEnumerable<HyTcgCard> cards,CancellationToken token)
        {
            if(cards==null)return;
            var missing=new List<HyTcgCard>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var card in cards.Take(100))
            {
                token.ThrowIfCancellationRequested();string identity;
                try{identity=CharacterIdentity(card);}catch{continue;}
                if(seen.Add(identity) && ReadCharacterInfo(identity)==null)missing.Add(card);
            }
            var batch=CharacterTransport as IHyTcgCharacterBatchTransport;
            if(batch==null)
            {
                foreach(var card in missing)try{CharacterInfo(card,token);}catch(OperationCanceledException){throw;}catch{return;}
                return;
            }
            characterInfoGate.Wait(token);
            try
            {
                missing=missing.Where(card=>ReadCharacterInfo(CharacterIdentity(card))==null).ToList();
                for(int offset=0;offset<missing.Count;offset+=8)
                {
                    token.ThrowIfCancellationRequested();var chunk=missing.Skip(offset).Take(8).ToList();
                    var known=chunk.Where(card=>!String.IsNullOrWhiteSpace(card.Series)).ToList();
                    List<string> responses;
                    try{responses=known.Count==0?new List<string>():batch.SearchMany(known.Select(card=>card.Name).ToList(),token);}
                    catch(OperationCanceledException){throw;}catch{return;}
                    if(responses.Count!=known.Count)return;
                    for(int index=0;index<chunk.Count;index++)
                    {
                        var card=chunk[index];var info=new HyTcgCharacterInfo{CharacterId=card.CharacterId,Name=card.Name,Series=card.Series??"",NativeName="",Description="",SiteUrl="",Gender="",Age="",BloodType="",DateOfBirth="",CachedAt=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)};
                        try
                        {
                            if(!String.IsNullOrWhiteSpace(card.Series))
                            {
                                var candidates=CharacterSearchRows(responses[known.IndexOf(card)]).Where(value=>MatchesCharacterInfo(value,card)).GroupBy(value=>Number(value,"id")).Select(group=>group.First()).Take(2).ToList();
                                if(candidates.Count==1)
                                {
                                    var detail=candidates[0].ContainsKey("description") && candidates[0].ContainsKey("favourites")?candidates[0]:CharacterDetailRow(CharacterTransport.Detail(Number(candidates[0],"id"),token));
                                    if(detail==null || Number(detail,"id")!=Number(candidates[0],"id"))continue;
                                    if(MatchesCharacterInfo(detail,card))info=ParseCharacterInfo(detail,card);
                                }
                            }
                            token.ThrowIfCancellationRequested();SaveCharacterInfo(CharacterIdentity(card),info);
                        }
                        catch(OperationCanceledException){throw;}catch{ /* One character failure does not invalidate the rest. */ }
                    }
                }
            }
            finally{characterInfoGate.Release();}
        }
        private static Dictionary<string,object> CharacterData(string json)
        {
            Dictionary<string,object> value;
            try{value=Parser().DeserializeObject(json) as Dictionary<string,object>;}catch{throw new HyTcgException("AniListUnavailable");}
            object errors,data;
            if(value==null || (value.TryGetValue("errors",out errors) && errors!=null) || !value.TryGetValue("data",out data) || !(data is Dictionary<string,object>))throw new HyTcgException("AniListUnavailable");
            return (Dictionary<string,object>)data;
        }
        private static List<Dictionary<string,object>> CharacterSearchRows(string json)
        {
            var data=CharacterData(json);object raw;
            var page=data.TryGetValue("Page",out raw)?raw as Dictionary<string,object>:null;
            if(page==null || !page.TryGetValue("characters",out raw) || !(raw is IEnumerable) || raw is string)throw new HyTcgException("AniListUnavailable");
            return ((IEnumerable)raw).Cast<object>().OfType<Dictionary<string,object>>().Where(value=>Number(value,"id")>0).Take(25).ToList();
        }
        private static Dictionary<string,object> CharacterDetailRow(string json)
        {var data=CharacterData(json);object raw;return data.TryGetValue("Character",out raw)?raw as Dictionary<string,object>:null;}
        private static bool MatchesCharacterInfo(Dictionary<string,object> value,HyTcgCard card)
        {
            object raw;
            var name=value!=null && value.TryGetValue("name",out raw)?raw as Dictionary<string,object>:null;
            var names=new List<string>{First(name,"full"),First(name,"native")};
            if(name!=null && name.TryGetValue("alternative",out raw) && raw is IEnumerable && !(raw is string))names.AddRange(((IEnumerable)raw).Cast<object>().OfType<string>().Take(30));
            string expected=CharacterKey(card.Name);
            if(expected=="" || !names.Any(text=>CharacterKey(text)==expected))return false;
            var media=value.TryGetValue("media",out raw)?raw as Dictionary<string,object>:null;
            if(media==null || !media.TryGetValue("nodes",out raw) || !(raw is IEnumerable) || raw is string)return false;
            string series=SeriesKey(card.Series);
            return series!="" && ((IEnumerable)raw).Cast<object>().OfType<Dictionary<string,object>>().Take(25).Any(item=>TrendingTitles(item).Any(title=>SeriesKey(title)==series));
        }
        private static HyTcgCharacterInfo ParseCharacterInfo(Dictionary<string,object> detail,HyTcgCard card)
        {
            object raw;var name=detail.TryGetValue("name",out raw)?raw as Dictionary<string,object>:null;
            var birthday=detail.TryGetValue("dateOfBirth",out raw)?raw as Dictionary<string,object>:null;
            int month=Number(birthday,"month"),day=Number(birthday,"day"),year=Number(birthday,"year");
            int? favourites=null;
            if(detail.TryGetValue("favourites",out raw) && raw!=null){int count;if(Int32.TryParse(Convert.ToString(raw,CultureInfo.InvariantCulture),out count) && count>=0)favourites=count;}
            int id=Number(detail,"id");
            return new HyTcgCharacterInfo{CharacterId=card.CharacterId,AniListId=id,Matched=true,Name=PlainCharacterText(First(name,"full"),180),NativeName=PlainCharacterText(First(name,"native"),180),Series=card.Series??"",Favourites=favourites,Description=PlainCharacterText(First(detail,"description"),5000),Gender=PlainCharacterText(First(detail,"gender"),80),Age=PlainCharacterText(First(detail,"age"),100),BloodType=PlainCharacterText(First(detail,"bloodType"),20),DateOfBirth=month>=1&&month<=12&&day>=1&&day<=31?(year>0?year.ToString("D4",CultureInfo.InvariantCulture)+"-":"")+month.ToString("D2",CultureInfo.InvariantCulture)+"-"+day.ToString("D2",CultureInfo.InvariantCulture):"",SiteUrl="https://anilist.co/character/"+id,CachedAt=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)};
        }
        internal static string PlainCharacterText(string value,int max)
        {
            string text=value??"";if(text.Length>40000)text=text.Substring(0,40000);
            text=Regex.Replace(text,@"~!.*?!~","",RegexOptions.Singleline);
            text=Regex.Replace(text,@"<(script|style)\b[^>]*>.*?</\1>","",RegexOptions.IgnoreCase|RegexOptions.Singleline);
            text=Regex.Replace(text,@"<\s*(br|/p|/div)\s*/?>","\n",RegexOptions.IgnoreCase);
            text=Regex.Replace(text,@"<[^>]+>","");
            text=Regex.Replace(text,@"!\[[^\]]*\]\([^)]*\)","");
            text=Regex.Replace(text,@"\[([^\]]+)\]\([^)]*\)","$1");
            text=WebUtility.HtmlDecode(text).Replace("**","").Replace("__","");
            text=Regex.Replace(text,@"[\x00-\x08\x0b\x0c\x0e-\x1f]","");
            text=Regex.Replace(text,@"[ \t]+"," ");text=Regex.Replace(text,@"\n\s*\n(?:\s*\n)+","\n\n").Trim();
            return text.Length>max?text.Substring(0,max).TrimEnd():text;
        }
    }
}
