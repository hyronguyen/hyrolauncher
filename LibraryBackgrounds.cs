using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HYROLauncherWeb
{
    public class LibraryBackgroundItem
    {
        public string Name { get; set; }
        public string Image { get; set; }
        public string Video { get; set; }
    }
    public class LibraryBackgroundCatalog
    {
        public string SteamId { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string Warning { get; set; }
        public List<LibraryBackgroundItem> Items { get; set; }
    }
    internal static class SteamBackgroundService
    {
        private static readonly JavaScriptSerializer Parser = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
        private static string Str(Dictionary<string, object> data, string key) { return data != null && data.ContainsKey(key) ? Convert.ToString(data[key]) : ""; }
        public static string MediaUrl(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            if (value.StartsWith("items/", StringComparison.Ordinal)) value = "https://cdn.akamai.steamstatic.com/steamcommunity/public/images/" + value;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "https") return "";
            string host = uri.Host.ToLowerInvariant();
            return host.EndsWith(".steamstatic.com") || host.EndsWith(".steamusercontent.com") || host == "steamcommunity-a.akamaihd.net" ? uri.AbsoluteUri : "";
        }
        private static string Fetch(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 15000; req.ReadWriteTimeout = 15000; req.AllowAutoRedirect = false;
            return ApiActivityLog.ReadText(req,"Steam Backgrounds");
        }
        public static LibraryBackgroundItem ParseEquipped(string json)
        {
            var root = Parser.Deserialize<Dictionary<string, object>>(json);
            var response = root != null && root.ContainsKey("response") ? root["response"] as Dictionary<string, object> : null;
            var item = response != null && response.ContainsKey("profile_background") ? response["profile_background"] as Dictionary<string, object> : null;
            string image = MediaUrl(Str(item, "image_large"));
            if (image == "") return null;
            return new LibraryBackgroundItem { Name = Str(item, "name"), Image = image, Video = MediaUrl(Str(item, "movie_webm")) };
        }
        public static List<LibraryBackgroundItem> ParseInventory(string json, out string next)
        {
            var root = Parser.Deserialize<Dictionary<string, object>>(json);
            if (Str(root, "success") != "1") throw new InvalidOperationException("InventoryUnavailable");
            next = Str(root, "more_items") == "1" ? Str(root, "last_assetid") : "";
            var descriptions = root.ContainsKey("descriptions") ? root["descriptions"] as IEnumerable : null;
            var result = new List<LibraryBackgroundItem>();
            if (descriptions != null) foreach (var raw in descriptions)
            {
                var item = raw as Dictionary<string, object>; if (item == null) continue;
                bool background = false;
                var tags = item.ContainsKey("tags") ? item["tags"] as IEnumerable : null;
                if (tags != null) foreach (var tag in tags) if (Str(tag as Dictionary<string, object>, "internal_name") == "item_class_3") background = true;
                if (!background) continue;
                string image = "";
                foreach (string field in new [] { "actions", "owner_actions" })
                {
                    var actions = item.ContainsKey(field) ? item[field] as IEnumerable : null;
                    if (actions != null) foreach (var action in actions)
                    {
                        string candidate = MediaUrl(Str(action as Dictionary<string, object>, "link"));
                        if (candidate != "") { image = candidate; break; }
                    }
                }
                if (image != "") result.Add(new LibraryBackgroundItem { Name = Str(item, "name"), Image = image, Video = "" });
            }
            return result;
        }
        public static LibraryBackgroundCatalog Get(string account, string directory, bool refresh)
        {
            if (SteamLibraryService.NormalizeId(account) == "") throw new InvalidOperationException("SteamIdRequired");
            string cachePath = Path.Combine(directory, "background-cache", account + ".json");
            LibraryBackgroundCatalog cached = null;
            try { if (File.Exists(cachePath)) cached = Parser.Deserialize<LibraryBackgroundCatalog>(File.ReadAllText(cachePath)); } catch { }
            if (!refresh && cached != null && cached.UpdatedUtc > DateTime.UtcNow.AddHours(-6)) return cached;
            var catalog = new LibraryBackgroundCatalog { SteamId = account, UpdatedUtc = DateTime.UtcNow, Items = new List<LibraryBackgroundItem>(), Warning = "" };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string key = File.ReadAllText(Path.Combine(directory, "assets", "apikey", "steamapikey.txt")).Trim();
                if (Regex.IsMatch(key, "^[a-fA-F0-9]{32}$"))
                {
                    var equipped = ParseEquipped(Fetch("https://api.steampowered.com/IPlayerService/GetProfileItemsEquipped/v1/?steamid=" + account + "&key=" + key));
                    if (equipped != null) { catalog.Items.Add(equipped); seen.Add(equipped.Image); }
                }
            }
            catch { }
            try
            {
                string cursor = ""; var cursors = new HashSet<string>();
                do
                {
                    var items = ParseInventory(Fetch("https://steamcommunity.com/inventory/" + account + "/753/6?l=english&count=5000" + (cursor == "" ? "" : "&start_assetid=" + cursor)), out cursor);
                    foreach (var item in items) if (seen.Add(item.Image)) catalog.Items.Add(item);
                } while (cursor != "" && cursors.Add(cursor) && cursors.Count < 20);
                if (cursor != "") catalog.Warning = "InventoryPartial";
            }
            catch { catalog.Warning = "InventoryUnavailable"; }
            if (catalog.Items.Count == 0 && cached != null) { cached.Warning = "CachedBackgrounds"; return cached; }
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)); File.WriteAllText(cachePath, Parser.Serialize(catalog));
            return catalog;
        }
    }
    internal partial class WebLauncherForm
    {
        private Dictionary<string, object> pendingBrowserWatch;
        private async void GetLibraryBackgrounds(Dictionary<string, object> msg)
        {
            string account = state.Settings.SteamId, requestId = GetString(msg, "requestId");
            try
            {
                var catalog = await Task.Run(() => SteamBackgroundService.Get(account, AppDomain.CurrentDomain.BaseDirectory, GetBool(msg, "refresh")));
                if (!IsDisposed) Post(new Dictionary<string, object> { { "type", "steamBackgrounds" }, { "requestId", requestId }, { "catalog", catalog }, { "error", "" } });
            }
            catch (Exception ex) { if (!IsDisposed) Post(new Dictionary<string, object> { { "type", "steamBackgrounds" }, { "requestId", requestId }, { "error", ex.Message == "SteamIdRequired" ? "SteamIdRequired" : "SteamConnectionFailed" } }); }
        }
        private bool SaveLibraryBackground(Dictionary<string, object> settings)
        {
            string source = GetString(settings, "LibraryBackground");
            try
            {
                string path = "";
                if (!String.IsNullOrWhiteSpace(source))
                {
                    path = SteamBackgroundService.MediaUrl(source);
                    if (path == "")
                    {
                        source = ResolveStoredAssetPath(source);
                        string ext = Path.GetExtension(source).ToLowerInvariant();
                        if (!File.Exists(source) || !Regex.IsMatch(ext, @"^\.(png|jpg|jpeg|bmp|gif|webp)$")) throw new IOException();
                        using (var image = System.Drawing.Image.FromFile(source)) { if (image.Width < 1 || image.Height < 1) throw new IOException(); }
                        string folder = Path.Combine(AssetsDir, "settings"); Directory.CreateDirectory(folder);
                        path = Path.Combine(folder, "library-background" + ext);
                        if (!String.Equals(Path.GetFullPath(source), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) File.Copy(source, path, true);
                        path = MakePortableAssetPath(path);
                    }
                }
                state.Settings.LibraryBackground = path;
                state.Settings.LibraryBackgroundDim = ClampInt(GetString(settings, "LibraryBackgroundDim"), 45, 0, 100);
                return true;
            }
            catch { Notify(state.Settings.Language == "vi" ? "Không đọc được ảnh nền. Hãy chọn ảnh hoặc GIF khác." : "Unable to read the background. Choose another image or GIF."); return false; }
        }
        private async void OpenHyrowserUrl(string url)
        {
            Uri uri; if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https")) { Notify("Enter a valid http/https URL."); return; }
            EnsureHyrowserWindow();
            await hyrowserWindow.EnsureWebViewAsync(webEnvironment);
            hyrowserWindow.OpenPage(uri.AbsoluteUri);
        }
        private void EnsureHyrowserWindow()
        {
            if (hyrowserWindow == null || hyrowserWindow.IsDisposed)
            {
                hyrowserWindow = new HyrowserWindow("HyBrowser");
                hyrowserWindow.FormClosed += delegate { hyrowserWindow = null; };
            }
            hyrowserWindow.SetAccent(CurrentAccentColor()); hyrowserWindow.UiClick = PlayUiClick;
            hyrowserWindow.SaveToWatchlist = AddBrowserWatch;
            hyrowserWindow.AdvanceMovie = AdvanceBrowserMovie;
        }
        private void AddBrowserWatch(string pageTitle, string url)
        {
            pendingBrowserWatch = new Dictionary<string, object> { { "type", "browserWatch" }, { "title", pageTitle }, { "url", url } };
            ShowBuiltInPlugin(BuiltInMoviePluginId);
        }
    }
}
