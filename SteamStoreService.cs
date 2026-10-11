using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace HYROLauncherWeb
{
    public class SteamFilterOption
    {
        public string Group { get; set; }
        public string Value { get; set; }
        public string Name { get; set; }
    }
    public class SteamDealFilters
    {
        public string Term { get; set; }
        public string Sort { get; set; }
        public string Tags { get; set; }
        public string ExcludeTags { get; set; }
        public string Types { get; set; }
        public string Features { get; set; }
        public string Languages { get; set; }
        public string Deck { get; set; }
        public string Os { get; set; }
        public int MaxPrice { get; set; }
        public int MinDiscount { get; set; }
        public bool HideFree { get; set; }
    }
    public class SteamStoreService
    {
        private readonly string root;
        private readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=8*1024*1024};
        public SteamStoreService(string directory) {root=directory;}
        private string CacheFile(string key) {
            using(var hash=System.Security.Cryptography.SHA256.Create()) return Path.Combine(root,"explore-cache",BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes("store-v2:"+key))).Replace("-","")+".json");
        }
        public ExploreSnapshot Cached(string key) {try {string path=CacheFile(key);return File.Exists(path)?json.Deserialize<ExploreSnapshot>(File.ReadAllText(path)):null;}catch{return null;}}
        private void Save(string key,ExploreSnapshot result) {string path=CacheFile(key);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,json.Serialize(result));}
        private static string Fetch(string url) {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(url);request.Timeout=20000;request.ReadWriteTimeout=20000;request.AllowAutoRedirect=false;
            request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
            return ApiActivityLog.ReadText(request,"Steam Store");
        }
        private static string Text(Dictionary<string,object> row,string field) {return row!=null && row.ContainsKey(field)?Convert.ToString(row[field]):"";}
        private static int Number(Dictionary<string,object> row,string field) {int n;return Int32.TryParse(Text(row,field),out n)?n:0;}
        private static Dictionary<string,object> Child(Dictionary<string,object> row,string field) {return row!=null && row.ContainsKey(field)?row[field] as Dictionary<string,object>:null;}
        private static string Tokens(string value,bool numeric) {
            var result=new List<string>();foreach(string part in (value??"").Split(','))if(result.Count<100 && Regex.IsMatch(part,numeric?"^[0-9]{1,8}$":"^[a-z_]{1,32}$") && !result.Contains(part))result.Add(part);
            result.Sort(StringComparer.Ordinal);return String.Join(",",result.ToArray());
        }
        public static SteamDealFilters NormalizeFilters(SteamDealFilters input) {
            input=input??new SteamDealFilters();string term=(input.Term??"").Trim();if(term.Length>120)term=term.Substring(0,120);
            return new SteamDealFilters {Term=term,Sort=Regex.IsMatch(input.Sort??"","^(Reviews_DESC|Released_DESC|Name_ASC|Price_ASC|Price_DESC|DeckCompatDate_DESC)$")?input.Sort:"",Tags=Tokens(input.Tags,true),ExcludeTags=Tokens(input.ExcludeTags,true),Types=Tokens(input.Types,true),Features=Tokens(input.Features,true),Languages=Tokens(input.Languages,false),Deck=Tokens(input.Deck,true),Os=Tokens(input.Os,false),MaxPrice=Math.Max(0,Math.Min(100000000,input.MaxPrice)),MinDiscount=Math.Max(0,Math.Min(100,input.MinDiscount)),HideFree=input.HideFree};
        }
        public static string DealQuery(SteamDealFilters input,int offset) {
            var f=NormalizeFilters(input);string query="start="+Math.Max(0,Math.Min(100000,offset))+"&count=50&specials=1&cc=vn&l=english&infinite=1";
            var fields=new Dictionary<string,string>{{"term",f.Term},{"sort_by",f.Sort},{"tags",f.Tags},{"untags",f.ExcludeTags},{"category1",f.Types},{"category2",f.Features},{"supportedlang",f.Languages},{"deck_compatibility",f.Deck},{"os",f.Os}};
            foreach(var item in fields)if(item.Value!="")query+="&"+item.Key+"="+Uri.EscapeDataString(item.Value);
            // Steam accepts its regional price stops; keep arbitrary user limits exact by filtering the returned numeric price too.
            if(f.MaxPrice>0 && f.MaxPrice<=1200000)query+="&maxprice="+(int)(Math.Ceiling(f.MaxPrice/100000m)*100000);if(f.HideFree)query+="&hidef2p=1";return query;
        }
        public string DealsKey(SteamDealFilters input,int offset) {return "deals:"+json.Serialize(NormalizeFilters(input))+":"+Math.Max(0,Math.Min(100000,offset));}
        public ExploreSnapshot Deals(SteamDealFilters input,int offset,bool refresh) {
            var filters=NormalizeFilters(input);offset=Math.Max(0,Math.Min(100000,offset));string key=DealsKey(filters,offset);var cached=Cached(key);
            if(!refresh && cached!=null && cached.UpdatedUtc>DateTime.UtcNow.AddMinutes(-30))return cached;
            var result=ExploreService.ParseSearchDealsFiltered(Fetch("https://store.steampowered.com/search/results/?"+DealQuery(filters,offset)),offset,filters.MinDiscount);
            if(filters.MaxPrice>0)result.Games.RemoveAll(game=>!game.PriceVnd.HasValue || game.PriceVnd.Value>filters.MaxPrice);
            Save(key,result);return result;
        }
        public static List<SteamFilterOption> ParseFilters(string html) {
            var options=new List<SteamFilterOption>();var seen=new HashSet<string>();
            foreach(Match row in Regex.Matches(html,@"<div\b[^>]*class=""[^""]*tab_filter_control_row[^""]*""[^>]*>",RegexOptions.IgnoreCase)) {
                string group=Regex.Match(row.Value,@"data-param=""([^""]+)""").Groups[1].Value,value=Regex.Match(row.Value,@"data-value=""([^""]+)""").Groups[1].Value,name=WebUtility.HtmlDecode(Regex.Match(row.Value,@"data-loc=""([^""]+)""").Groups[1].Value);
                if(!Regex.IsMatch(group,"^(tags|category1|category2|supportedlang|deck_compatibility|os)$") || !Regex.IsMatch(value,group=="supportedlang" || group=="os"?"^[a-z_]{1,32}$":"^[0-9]{1,8}$") || name=="" || !seen.Add(group+":"+value))continue;
                options.Add(new SteamFilterOption {Group=group,Value=value,Name=name});
            }
            foreach(string platform in new[]{"win","mac","linux"})if(seen.Add("os:"+platform))options.Add(new SteamFilterOption {Group="os",Value=platform,Name=platform=="win"?"Windows":platform=="mac"?"macOS":"Linux / SteamOS"});
            return options;
        }
        public List<SteamFilterOption> Filters(bool refresh) {
            string path=CacheFile("filters");List<SteamFilterOption> cached=null;
            try {if(File.Exists(path)){cached=json.Deserialize<List<SteamFilterOption>>(File.ReadAllText(path));if(!refresh && File.GetLastWriteTimeUtc(path)>DateTime.UtcNow.AddDays(-7))return cached;}}catch{}
            try {var result=ParseFilters(Fetch("https://store.steampowered.com/search/?specials=1&cc=vn&l=english"));if(result.Count<20)throw new InvalidOperationException("SteamFiltersUnavailable");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,json.Serialize(result));return result;}catch{if(cached!=null)return cached;throw;}
        }
        public static List<int> ParseWishlist(string payload) {
            var data=new JavaScriptSerializer {MaxJsonLength=8*1024*1024}.Deserialize<Dictionary<string,object>>(payload);var response=Child(data,"response");
            // Steam returns an empty response for unavailable/private lists as well as some empty lists.
            if(response==null || !response.ContainsKey("items"))throw new InvalidOperationException("WishlistUnavailable");
            var ids=new List<int>();var seen=new HashSet<int>();var items=response["items"] as IEnumerable;
            if(items!=null)foreach(var item in items){var row=item as Dictionary<string,object>;int id=Number(row,"appid");if(id>0 && seen.Add(id))ids.Add(id);}return ids;
        }
        public static List<ExploreGame> ParseStoreItems(string payload) {
            var data=new JavaScriptSerializer {MaxJsonLength=8*1024*1024}.Deserialize<Dictionary<string,object>>(payload);var response=Child(data,"response");
            if(response==null || !response.ContainsKey("store_items"))throw new InvalidOperationException("SteamConnectionFailed");
            var games=new List<ExploreGame>();var seen=new HashSet<int>();foreach(var item in (IEnumerable)response["store_items"]) {
                var row=item as Dictionary<string,object>;int id=Number(row,"appid");if(id<=0 || Number(row,"success")!=1 || !seen.Add(id))continue;
                Dictionary<string,object> purchase=Child(row,"best_purchase_option"),basic=Child(row,"basic_info"),assets=Child(row,"assets"),release=Child(row,"release");
                string art="https://cdn.akamai.steamstatic.com/steam/apps/"+id+"/header.jpg",format=Text(assets,"asset_url_format"),header=Text(assets,"header");
                if(format.StartsWith("steam/apps/",StringComparison.Ordinal) && header!="" && !header.Contains(".."))art="https://shared.fastly.steamstatic.com/store_item_assets/"+format.Replace("${FILENAME}",header);
                int timestamp=Number(release,"steam_release_date");
                games.Add(new ExploreGame {Id=id,Name=Text(row,"name"),Image=art,Source="Steam",Url="https://store.steampowered.com/app/"+id+"/",Summary=Text(basic,"short_description"),ReleaseDate=timestamp>0?new DateTime(1970,1,1).AddSeconds(timestamp).ToString("yyyy-MM-dd"):"",Discount=Number(purchase,"discount_pct"),Price=Text(purchase,"formatted_final_price"),OriginalPrice=Text(purchase,"formatted_original_price")});
            }return games;
        }
        public ExploreSnapshot Wishlist(string account,int offset,bool refresh) {
            account=SteamLibraryService.NormalizeId(account);if(account=="")throw new InvalidOperationException("SteamIdRequired");offset=Math.Max(0,Math.Min(100000,offset));string key="wishlist:"+account+":"+offset;var cached=Cached(key);
            if(!refresh && cached!=null && cached.UpdatedUtc>DateTime.UtcNow.AddMinutes(-30))return cached;
            string wishlistUrl="https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid="+account;
            string keyPath=Path.Combine(root,"assets","apikey","steamapikey.txt");
            if(File.Exists(keyPath)){string apiKey=File.ReadAllText(keyPath).Trim();if(Regex.IsMatch(apiKey,"^[a-fA-F0-9]{32}$"))wishlistUrl+="&key="+Uri.EscapeDataString(apiKey);}
            // Authentication stays in the native host, including for the account owner's private wishlist.
            var ids=ParseWishlist(Fetch(wishlistUrl));var batch=new List<Dictionary<string,object>>();
            for(int i=offset;i<Math.Min(ids.Count,offset+50);i++)batch.Add(new Dictionary<string,object>{{"appid",ids[i]}});
            var games=new List<ExploreGame>();if(batch.Count>0){var input=new Dictionary<string,object>{{"ids",batch},{"context",new Dictionary<string,object>{{"language","english"},{"country_code","VN"}}},{"data_request",new Dictionary<string,object>{{"include_basic_info",true},{"include_assets",true},{"include_release",true}}}};games=ParseStoreItems(Fetch("https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json="+Uri.EscapeDataString(json.Serialize(input))));}
            var result=new ExploreSnapshot {Games=games,UpdatedUtc=DateTime.UtcNow,NextOffset=offset+50,HasMore=offset+50<ids.Count,TotalCount=ids.Count};Save(key,result);return result;
        }
    }
}
