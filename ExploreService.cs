using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace HYROLauncherWeb
{
    public class ExploreGame
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Image { get; set; }
        public string Summary { get; set; }
        public string ReleaseDate { get; set; }
        public double? Rating { get; set; }
        public string Source { get; set; }
        public string Url { get; set; }
        public int Discount { get; set; }
        public string Price { get; set; }
        public string OriginalPrice { get; set; }
        public decimal? PriceVnd { get; set; }
    }
    public class ExploreSnapshot
    {
        public DateTime UpdatedUtc { get; set; }
        public List<ExploreGame> Games { get; set; }
        public bool HasMore { get; set; }
        public int NextOffset { get; set; }
        public int TotalCount { get; set; }
    }
    public class ExploreService
    {
        private readonly string root;
        private readonly IgdbService igdb;
        public ExploreService(string directory,IgdbService client) { root=directory; igdb=client; }
        private string CachePath(string mode,string query)
        {
            using(var hash=SHA256.Create()) return Path.Combine(root,"explore-cache",BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(mode+":"+(query??"").Trim().ToLowerInvariant()))).Replace("-","")+".json");
        }
        public ExploreSnapshot Cached(string mode,string query)
        { try { string path=CachePath(mode,query);return File.Exists(path)?new JavaScriptSerializer().Deserialize<ExploreSnapshot>(File.ReadAllText(path)):null; } catch {return null;} }
        public ExploreSnapshot Get(string mode,string query,bool refresh)
        { return GetPage(mode,query,refresh,0); }
        public ExploreSnapshot CachedPage(string mode,string query,int offset)
        { return Cached(mode,mode=="deals"?"catalog-v2:"+offset:query); }
        public ExploreSnapshot GetPage(string mode,string query,bool refresh,int offset)
        {
            if(mode!="new" && mode!="upcoming" && mode!="deals" && mode!="search") throw new InvalidOperationException("InvalidMode");
            offset=mode=="deals"?Math.Max(0,Math.Min(100000,offset)):0;
            var cached=CachedPage(mode,query,offset);
            if(!refresh && cached!=null && cached.UpdatedUtc>DateTime.UtcNow.AddMinutes(mode=="deals"?30:240)) return cached;
            List<ExploreGame> games;
            ExploreSnapshot page=null;
            if(mode=="deals") {
                ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
                var request=(HttpWebRequest)WebRequest.Create("https://store.steampowered.com/search/results/?start="+offset+"&count=50&sort_by=Reviews_DESC&specials=1&category1=998&os=win&cc=vn&l=english&infinite=1");
                request.Timeout=20000;request.ReadWriteTimeout=20000;request.AllowAutoRedirect=false;
                page=ParseSearchDeals(ApiActivityLog.ReadText(request,"Steam Explore"),offset);
                games=page.Games;
            } else {
                games=new List<ExploreGame>();
                foreach(var game in mode=="search"?igdb.Search(query):igdb.Explore(mode)) games.Add(new ExploreGame {Id=game.Id,Name=game.Name,Image=game.Screenshots.Count>0?game.Screenshots[0]:game.Cover,Summary=game.Summary,ReleaseDate=game.ReleaseDate,Rating=game.Rating,Source="IGDB",Url=game.Url,Price="",OriginalPrice=""});
            }
            var result=new ExploreSnapshot {UpdatedUtc=DateTime.UtcNow,Games=games,HasMore=page!=null && page.HasMore,NextOffset=page==null?0:page.NextOffset};
            string path=CachePath(mode,mode=="deals"?"catalog-v2:"+offset:query);Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,new JavaScriptSerializer().Serialize(result));
            return result;
        }
        private static string Text(Dictionary<string,object> row,string name) { return row.ContainsKey(name)?Convert.ToString(row[name]):""; }
        private static int Number(Dictionary<string,object> row,string name) {int n;return Int32.TryParse(Text(row,name),out n)?n:0;}
        private static string Money(int amount,string currency)
        {
            decimal value=amount/100m;
            return currency=="VND"?value.ToString("N0",CultureInfo.GetCultureInfo("vi-VN"))+" ₫":currency=="USD"?"$"+value.ToString("N2",CultureInfo.InvariantCulture):value.ToString("N2",CultureInfo.InvariantCulture)+" "+currency;
        }
        private static string HtmlText(string value) {return WebUtility.HtmlDecode(Regex.Replace(value??"","<[^>]+>","")).Trim();}
        private static string HtmlClass(string html,string name)
        {return HtmlText(Regex.Match(html,"<(?:div|span)\\b[^>]*class=\"[^\"]*\\b"+name+"\\b[^\"]*\"[^>]*>([\\s\\S]*?)</(?:div|span)>",RegexOptions.IgnoreCase).Groups[1].Value);}
        public static ExploreSnapshot ParseSearchDeals(string payload,int offset)
        {return ParseSearchDealsFiltered(payload,offset,20);}
        public static ExploreSnapshot ParseSearchDealsFiltered(string payload,int offset,int minDiscount)
        {
            var data=new JavaScriptSerializer {MaxJsonLength=8*1024*1024}.Deserialize<Dictionary<string,object>>(payload);
            if(!data.ContainsKey("results_html") || !data.ContainsKey("total_count"))throw new InvalidOperationException("DealsUnavailable");
            var games=new List<ExploreGame>();var seen=new HashSet<int>();
            string html=Text(data,"results_html");
            foreach(Match row in Regex.Matches(html,@"<a\b(?<attrs>[^>]*\bdata-ds-appid=""[^""]+""[^>]*)>(?<body>[\s\S]*?)</a>",RegexOptions.IgnoreCase))
            {
                string attrs=row.Groups["attrs"].Value,body=row.Groups["body"].Value;
                Match app=Regex.Match(attrs,@"href=""https://store\.steampowered\.com/app/(\d+)/",RegexOptions.IgnoreCase);
                int id,discount;
                if(!Int32.TryParse(app.Groups[1].Value,out id) || !Int32.TryParse(Regex.Match(body,@"data-discount=""(\d+)""").Groups[1].Value,out discount) || discount<Math.Max(1,minDiscount) || !seen.Add(id))continue;
                string name=HtmlClass(body,"title"),price=HtmlClass(body,"discount_final_price"),original=HtmlClass(body,"discount_original_price");
                if(name=="" || price=="" || original=="")continue;
                // Keep Steam's already-formatted regional prices; never infer currency from a number.
                decimal minorPrice;
                decimal? priceVnd=Decimal.TryParse(Regex.Match(body,@"data-price-final=""(\d+)""").Groups[1].Value,NumberStyles.Number,CultureInfo.InvariantCulture,out minorPrice)?(decimal?)(minorPrice/100m):null;
                games.Add(new ExploreGame {Id=id,Name=name,Image="https://cdn.akamai.steamstatic.com/steam/apps/"+id+"/header.jpg",Source="Steam",Url="https://store.steampowered.com/app/"+id+"/",Discount=discount,Price=price,OriginalPrice=original,PriceVnd=priceVnd,Summary="",ReleaseDate=""});
            }
            int total=Number(data,"total_count");
            return new ExploreSnapshot {Games=games,UpdatedUtc=DateTime.UtcNow,NextOffset=offset+50,HasMore=offset+50<total};
        }
        public static List<ExploreGame> ParseDeals(string payload)
        {
            var root=new JavaScriptSerializer {MaxJsonLength=4*1024*1024}.Deserialize<Dictionary<string,object>>(payload);
            var specials=root.ContainsKey("specials")?root["specials"] as Dictionary<string,object>:null;
            if(specials==null || !specials.ContainsKey("items")) throw new InvalidOperationException("DealsUnavailable");
            var result=new List<ExploreGame>();var seen=new HashSet<int>();
            foreach(Dictionary<string,object> row in (IEnumerable)specials["items"])
            {
                int id=Number(row,"id"),discount=Number(row,"discount_percent");
                if(id<=0 || discount<20 || Number(row,"final_price")<0 || Number(row,"original_price")<=0 || Text(row,"currency")=="" || !seen.Add(id)) continue;
                result.Add(new ExploreGame {Id=id,Name=Text(row,"name"),Image=Text(row,"header_image"),Source="Steam",Url="https://store.steampowered.com/app/"+id+"/",Discount=Math.Min(100,discount),Price=Money(Number(row,"final_price"),Text(row,"currency")),OriginalPrice=Money(Number(row,"original_price"),Text(row,"currency")),Summary="",ReleaseDate=""});
            }
            result.Sort(delegate(ExploreGame a,ExploreGame b){return b.Discount.CompareTo(a.Discount);});return result;
        }
    }
}
