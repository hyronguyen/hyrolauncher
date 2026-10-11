using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace HYROLauncherWeb
{
    public class IgdbGameInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Summary { get; set; }
        public string ReleaseDate { get; set; }
        public double? Rating { get; set; }
        public List<string> Genres { get; set; }
        public List<string> Developers { get; set; }
        public List<string> Publishers { get; set; }
        public List<string> Screenshots { get; set; }
        public string Cover { get; set; }
        public string Url { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    public class IgdbService
    {
        private readonly string root;
        private readonly object gate = new object();
        private string token = "";
        private DateTime tokenExpires;
        private DateTime nextRequest;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        private const string Fields = "fields name,url,summary,first_release_date,rating,genres.name,involved_companies.company.name,involved_companies.developer,involved_companies.publisher,screenshots.image_id,cover.image_id; ";
        public IgdbService(string directory) { root = directory; }
        private static string Text(Dictionary<string, object> data, string key) { return data.ContainsKey(key) ? Convert.ToString(data[key]) : ""; }
        private static IEnumerable Items(Dictionary<string, object> data, string key) { return data.ContainsKey(key) ? data[key] as IEnumerable : null; }
        private static string Image(string imageId, string size)
        { return Regex.IsMatch(imageId ?? "", @"^[a-zA-Z0-9_]+$") ? "https://images.igdb.com/igdb/image/upload/t_" + size + "/" + imageId + ".jpg" : ""; }
        public static List<IgdbGameInfo> ParseGames(string payload)
        {
            var parser = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
            var rows = parser.Deserialize<List<Dictionary<string, object>>>(payload);
            var result = new List<IgdbGameInfo>();
            foreach (var row in rows)
            {
                int id; if (!Int32.TryParse(Text(row, "id"), out id) || id <= 0) continue;
                var game = new IgdbGameInfo { Id = id, Name = Text(row, "name"), Summary = Text(row, "summary"), ReleaseDate = "", Genres = new List<string>(), Developers = new List<string>(), Publishers = new List<string>(), Screenshots = new List<string>(), UpdatedUtc = DateTime.UtcNow };
                long release;
                if (Int64.TryParse(Text(row,"first_release_date"), out release))
                    try { game.ReleaseDate = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(release).ToString("yyyy-MM-dd"); } catch { }
                double rating;
                if (row.ContainsKey("rating") && Double.TryParse(Convert.ToString(row["rating"], System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out rating)) game.Rating = Math.Round(Math.Max(0,Math.Min(100,rating)),1);
                var genres = Items(row,"genres");
                if (genres != null) foreach (Dictionary<string,object> genre in genres) game.Genres.Add(Text(genre,"name"));
                var companies = Items(row,"involved_companies");
                if (companies != null) foreach (Dictionary<string,object> company in companies)
                {
                    var data = company.ContainsKey("company") ? company["company"] as Dictionary<string,object> : null;
                    if (data == null) continue;
                    string name = Text(data,"name");
                    if (Text(company,"developer").Equals("True",StringComparison.OrdinalIgnoreCase) && !game.Developers.Contains(name)) game.Developers.Add(name);
                    if (Text(company,"publisher").Equals("True",StringComparison.OrdinalIgnoreCase) && !game.Publishers.Contains(name)) game.Publishers.Add(name);
                }
                var screenshots = Items(row,"screenshots");
                if (screenshots != null) foreach (Dictionary<string,object> shot in screenshots)
                {
                    string image = Image(Text(shot,"image_id"),"screenshot_big");
                    if (image != "" && game.Screenshots.Count < 24) game.Screenshots.Add(image);
                }
                var cover = row.ContainsKey("cover") ? row["cover"] as Dictionary<string,object> : null;
                game.Cover = cover == null ? "" : Image(Text(cover,"image_id"),"cover_big");
                game.Url = Text(row,"url");
                result.Add(game);
            }
            return result;
        }
        public List<IgdbGameInfo> Search(string query)
        {
            query = (query ?? "").Trim();
            if (query.Length < 2 || query.Length > 120) throw new InvalidOperationException("SearchLength");
            string escaped = query.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r"," ").Replace("\n"," ");
            // Exact names must precede expansions/updates that search relevance may rank higher.
            var games = ParseGames(Query(Fields + "where name = \"" + escaped + "\"; limit 12;"));
            foreach (var candidate in ParseGames(Query(Fields + "search \"" + escaped + "\"; limit 12;")))
                if (!games.Exists(game => game.Id == candidate.Id) && games.Count < 12) games.Add(candidate);
            return games;
        }
        public IgdbGameInfo Get(int id, bool refresh)
        {
            return GetDetail(id, refresh);
        }
        public List<IgdbGameInfo> Explore(string mode)
        {
            long now=(long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
            string filter=mode=="upcoming" ? "first_release_date > "+now+" & first_release_date < "+(now+180L*86400)+"; sort hypes desc;" : "first_release_date >= "+(now-90L*86400)+" & first_release_date <= "+now+"; sort rating_count desc;";
            return ParseGames(Query(Fields+"where platforms = (6) & parent_game = null & version_parent = null & cover != null & "+filter+" limit 24;"));
        }
        private IgdbGameInfo GetDetail(int id, bool refresh)
        {
            if (id <= 0) throw new InvalidOperationException("InvalidId");
            var cached = Cached(id);
            if (!refresh && cached != null && cached.UpdatedUtc > DateTime.UtcNow.AddDays(-7)) return cached;
            var games = ParseGames(Query(Fields + "where id = " + id + "; limit 1;"));
            if (games.Count == 0) throw new InvalidOperationException("NotFound");
            string directory = Path.Combine(root,"igdb-cache"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,id+".json"),new JavaScriptSerializer().Serialize(games[0]));
            return games[0];
        }
        public IgdbGameInfo Cached(int id)
        {
            try {
                var path = Path.Combine(root,"igdb-cache",id+".json");
                var cached = File.Exists(path) ? new JavaScriptSerializer().Deserialize<IgdbGameInfo>(File.ReadAllText(path)) : null;
                return cached != null && cached.Id == id ? cached : null;
            } catch { return null; }
        }
        private string Query(string body)
        {
            lock (gate)
            {
                string clientPath = Path.Combine(root,"assets","apikey","twitchclient.txt"), secretPath = Path.Combine(root,"assets","apikey","twitchsecret.txt");
                if (!File.Exists(clientPath) || !File.Exists(secretPath)) throw new InvalidOperationException("MissingCredentials");
                string client = File.ReadAllText(clientPath).Trim(), secret = File.ReadAllText(secretPath).Trim();
                if (!Regex.IsMatch(client,@"^[a-zA-Z0-9]+$") || !Regex.IsMatch(secret,@"^[a-zA-Z0-9]+$")) throw new InvalidOperationException("InvalidCredentials");
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                for (int attempt=0; attempt<2; attempt++)
                {
                    if (token == "" || DateTime.UtcNow >= tokenExpires)
                    {
                        var response = json.Deserialize<Dictionary<string,object>>(Request("https://id.twitch.tv/oauth2/token", "client_id="+Uri.EscapeDataString(client)+"&client_secret="+Uri.EscapeDataString(secret)+"&grant_type=client_credentials", null, null));
                        token = Text(response,"access_token");
                        int expiry; if (token == "" || !Int32.TryParse(Text(response,"expires_in"),out expiry)) throw new InvalidOperationException("AuthenticationFailed");
                        tokenExpires = DateTime.UtcNow.AddSeconds(Math.Max(1,expiry-60));
                    }
                    int delay = (int)Math.Ceiling((nextRequest-DateTime.UtcNow).TotalMilliseconds);
                    if (delay>0) Thread.Sleep(delay);
                    nextRequest = DateTime.UtcNow.AddMilliseconds(300);
                    try { return Request("https://api.igdb.com/v4/games",body,client,token); }
                    catch (WebException ex)
                    {
                        var response = ex.Response as HttpWebResponse;
                        if (response != null && response.StatusCode == HttpStatusCode.Unauthorized && attempt==0) { token=""; response.Dispose(); continue; }
                        if (response != null && (int)response.StatusCode==429) throw new InvalidOperationException("RateLimited");
                        throw;
                    }
                }
                throw new InvalidOperationException("AuthenticationFailed");
            }
        }
        private static string Request(string url,string body,string client,string bearer)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method="POST"; request.Timeout=20000; request.ReadWriteTimeout=20000; request.AllowAutoRedirect=false;
            request.ContentType=client==null ? "application/x-www-form-urlencoded" : "text/plain";
            if (client!=null) { request.Headers["Client-ID"]=client; request.Headers["Authorization"]="Bearer "+bearer; }
            byte[] bytes=Encoding.UTF8.GetBytes(body); request.ContentLength=bytes.Length;
            return ApiActivityLog.SendText(request,client==null?"Twitch":"IGDB",bytes);
        }
    }
}
