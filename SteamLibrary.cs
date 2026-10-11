using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace HYROLauncherWeb
{
    public class SteamGame
    {
        public int AppId { get; set; }
        public string Name { get; set; }
        public int PlaytimeMinutes { get; set; }
        public string Image { get; set; }
    }

    public class SteamLibraryCache
    {
        public string SteamId { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public List<SteamGame> Games { get; set; }
        public SteamProfile Profile { get; set; }
    }

    public class SteamProfile
    {
        public string Name { get; set; }
        public string Avatar { get; set; }
    }

    internal static class SteamLibraryService
    {
        public static string NormalizeId(string value)
        {
            string id = (value ?? "").Trim();
            Match profile = Regex.Match(id, @"^https?://steamcommunity\.com/profiles/(\d{17})/?$", RegexOptions.IgnoreCase);
            if (profile.Success) id = profile.Groups[1].Value;
            return Regex.IsMatch(id, @"^7656119\d{10}$") ? id : "";
        }

        public static List<SteamGame> ParseGames(string json)
        {
            JavaScriptSerializer parser = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
            Dictionary<string, object> root = parser.Deserialize<Dictionary<string, object>>(json);
            Dictionary<string, object> response = root != null && root.ContainsKey("response") ? root["response"] as Dictionary<string, object> : null;
            if (response == null || !response.ContainsKey("game_count"))
                throw new InvalidOperationException("LibraryUnavailable");
            List<SteamGame> result = new List<SteamGame>();
            HashSet<int> seen = new HashSet<int>();
            IEnumerable games = response.ContainsKey("games") ? response["games"] as IEnumerable : null;
            if (games != null) foreach (object item in games)
            {
                Dictionary<string, object> game = item as Dictionary<string, object>;
                int appId;
                if (game == null || !game.ContainsKey("appid") || !Int32.TryParse(Convert.ToString(game["appid"]), out appId) || appId <= 0 || !seen.Add(appId)) continue;
                int minutes = 0;
                if (game.ContainsKey("playtime_forever")) Int32.TryParse(Convert.ToString(game["playtime_forever"]), out minutes);
                result.Add(new SteamGame { AppId = appId, Name = game.ContainsKey("name") ? Convert.ToString(game["name"]) : "Steam " + appId,
                    PlaytimeMinutes = Math.Max(0, minutes), Image = "https://cdn.akamai.steamstatic.com/steam/apps/" + appId + "/header.jpg" });
            }
            result.Sort(delegate(SteamGame a, SteamGame b) { return StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name); });
            return result;
        }

        public static List<SteamGame> Fetch(string steamId, string keyPath)
        {
            if (NormalizeId(steamId) == "") throw new InvalidOperationException("InvalidId");
            if (!File.Exists(keyPath)) throw new InvalidOperationException("MissingKey");
            string key = File.ReadAllText(keyPath).Trim();
            if (!Regex.IsMatch(key, @"^[a-fA-F0-9]{32}$")) throw new InvalidOperationException("InvalidKey");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            // The key remains in the native host and is never sent to WebView or error messages.
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key=" + Uri.EscapeDataString(key) + "&steamid=" + steamId + "&include_appinfo=1&include_played_free_games=1&format=json");
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            request.AllowAutoRedirect = false;
            return ParseGames(ApiActivityLog.ReadText(request,"Steam Library"));
        }

        public static SteamProfile ParseProfile(string json)
        {
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            var response = root != null && root.ContainsKey("response") ? root["response"] as Dictionary<string, object> : null;
            IEnumerable players = response != null && response.ContainsKey("players") ? response["players"] as IEnumerable : null;
            if (players != null) foreach (object item in players)
            {
                var player = item as Dictionary<string, object>;
                if (player == null) continue;
                string avatar = player.ContainsKey("avatarfull") ? Convert.ToString(player["avatarfull"]) : "";
                Uri uri;
                if (!Uri.TryCreate(avatar, UriKind.Absolute, out uri) || uri.Scheme != "https") avatar = "";
                return new SteamProfile { Name = player.ContainsKey("personaname") ? Convert.ToString(player["personaname"]) : "", Avatar = avatar };
            }
            return new SteamProfile { Name = "", Avatar = "" };
        }

        public static SteamLibraryCache FetchLibrary(string steamId, string keyPath)
        {
            var library = new SteamLibraryCache { SteamId = steamId, Games = Fetch(steamId, keyPath), UpdatedUtc = DateTime.UtcNow };
            try
            {
                string key = File.ReadAllText(keyPath).Trim();
                var request = (HttpWebRequest)WebRequest.Create("https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key=" + Uri.EscapeDataString(key) + "&steamids=" + steamId);
                request.Timeout = 10000; request.ReadWriteTimeout = 10000; request.AllowAutoRedirect = false;
                library.Profile = ParseProfile(ApiActivityLog.ReadText(request,"Steam Profile"));
            }
            catch { library.Profile = new SteamProfile { Name = "", Avatar = "" }; }
            return library;
        }
    }
}
