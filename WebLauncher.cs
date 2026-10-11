using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HYROLauncherWeb
{
    internal static class WebProgram
    {
        private const string SingleInstanceMutexName = "Local\\HYROLauncherWeb.SingleInstance";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, SingleInstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    FocusExistingLauncher();
                    return;
                }

                // Give all launcher and plugin windows a shared, explicit taskbar identity.
                // Without this, Explorer can keep the icon cached for the previous executable.
                SetCurrentProcessExplicitAppUserModelID("HYRO.Launcher");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new WebLauncherForm());
                GC.KeepAlive(mutex);
            }
        }

        private static void FocusExistingLauncher()
        {
            try
            {
                Process current = Process.GetCurrentProcess();
                foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id == current.Id) continue;
                    if (process.MainWindowHandle == IntPtr.Zero) continue;
                    ShowWindow(process.MainWindowHandle, 9);
                    SetForegroundWindow(process.MainWindowHandle);
                    break;
                }
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }

    public class GameEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int IgdbId { get; set; }
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string IconPath { get; set; }
        public string BannerPath { get; set; }
        public string HeroPath { get; set; }
        public int HeroX { get; set; }
        public int HeroY { get; set; }
        public int HeroSize { get; set; }
        public string BgmPath { get; set; }
        public string FeedUrl { get; set; }
        public string GameAccentColor { get; set; }
        public string Category { get; set; }
        public bool IsUrl { get; set; }
        public bool Favorite { get; set; }
        public bool Private { get; set; }
        public int SidebarOrder { get; set; }
    }

    public class PetDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IdleGifPath { get; set; }
        public string DragGifPath { get; set; }
        public string IdlePlaybackPath { get; set; }
        public string DragPlaybackPath { get; set; }
        public string WalkGifPath { get; set; }
    }

    public class Mp3Track
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
    }

    public class MovieWatchItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string PosterPath { get; set; }
        public string Episode { get; set; }
        public string Status { get; set; }
        public int TotalEpisodes { get; set; }
        public string WatchTime { get; set; }
        public string Url { get; set; }
        public string SourceGameId { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class PluginEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string IconPath { get; set; }
        public string Target { get; set; }
        public bool Pinned { get; set; }
        public int PluginOrder { get; set; }
        public bool BuiltIn { get; set; }
        public bool PetEnabled { get; set; }
        public int PetSize { get; set; }
        public int PetSpeed { get; set; }
        public int PetStaySeconds { get; set; }
        public int PetFrameDelayMs { get; set; }
        public int? PetIdleFrameDelayMs { get; set; }
        public int? PetDragFrameDelayMs { get; set; }
        public string PetColor { get; set; }
        public string ActivePetId { get; set; }
        public List<PetDefinition> Pets { get; set; }
        public List<Mp3Track> Mp3Tracks { get; set; }
        public string ActiveTrackId { get; set; }
        public string Mp3RepeatMode { get; set; }
        public bool Mp3Shuffle { get; set; }
        public int? Mp3Volume { get; set; }
        public List<string> MovieWebsiteGameIds { get; set; }
        public List<MovieWatchItem> WatchItems { get; set; }
    }

    public class LauncherSettings
    {
        public LauncherSettings()
        {
            BookshelfAnimationsEnabled=true;BookshelfPullDuration=300;BookshelfHoverDuration=150;BookshelfPullDistance=40;
            BookshelfHoverLift=4;BookshelfDefaultAngle=20;BookshelfSelectedAngle=0;BookshelfLightingIntensity=50;BookshelfReactiveLighting=true;
        }
        public string Title { get; set; }
        public string AccentColor { get; set; }
        public string LibraryLayout { get; set; }
        public bool BookshelfAnimationsEnabled { get; set; }
        public int BookshelfPullDuration { get; set; }
        public int BookshelfHoverDuration { get; set; }
        public int BookshelfPullDistance { get; set; }
        public int BookshelfHoverLift { get; set; }
        public int BookshelfDefaultAngle { get; set; }
        public int BookshelfSelectedAngle { get; set; }
        public int BookshelfLightingIntensity { get; set; }
        public bool BookshelfReactiveLighting { get; set; }
        public string CarouselDisplay { get; set; }
        public int CarouselColumns { get; set; }
        public string ControllerGlyphs { get; set; }
        public string SteamId { get; set; }
        public List<string> SteamFavorites { get; set; }
        public string ThemeMode { get; set; }
        public string LibraryBackground { get; set; }
        public int LibraryBackgroundDim { get; set; }
        public string CardStyle { get; set; }
        public int MaxGridColumns { get; set; }
        public string SidebarPosition { get; set; }
        public string Language { get; set; }
        public bool DisableBgm { get; set; }
        public bool DisableEffects { get; set; }
        public bool StartupWithWindows { get; set; }
        public int BgmVolume { get; set; }
        public string PrivatePinHash { get; set; }
        public bool PluginToolbarCollapsed { get; set; }
    }

    public class LauncherState
    {
        public List<GameEntry> Games { get; set; }
        public List<PluginEntry> Plugins { get; set; }
        public List<string> Categories { get; set; }
        public LauncherSettings Settings { get; set; }
    }

    internal partial class WebLauncherForm : Form
    {
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly WebView2 webView = new LauncherWebView();
        private readonly WebView2 linkView = new LauncherWebView();
        private readonly WebView2 feedView = new LauncherWebView();
        private readonly System.Windows.Forms.Timer runningTimer = new System.Windows.Forms.Timer();
        private readonly Dictionary<string, Process> launchedProcesses = new Dictionary<string, Process>();
        private const string DefaultPrivatePinHash = "e10adc3949ba59abbe56e057f20f883e";
        private const string BuiltInPetPluginId = "builtin.pet";
        private const string BuiltInMp3PluginId = "builtin.mp3";
        private const string BuiltInMoviePluginId = "builtin.movie-watchlist";
        private const string BuiltInHyrowserPluginId = "builtin.hyrowser";
        private const string BuiltInHyTcgPluginId = "builtin.hytcg";
        private const string RetiredHyroFtgPluginId = "builtin.hyroftg";
        private const string DefaultPetId = "default.hyro-rider";
        private bool isScanningGames;
        private LauncherState state;
        private string dataPath;
        private string currentWebLinkUrl = "";
        private string currentFeedUrl = "";
        private string currentPluginUrl = "";
        private string currentBuiltInPluginId = "";
        private bool mp3PlaybackActive;
        private string mp3PlaybackRequest = "";
        private string mp3PlaybackPluginId = "";
        private string currentSidebarPosition = "left";
        private bool webLinkFullscreen;
        private CoreWebView2Environment webEnvironment;
        private PluginBrowserWindow pluginWindow;
        private PluginBrowserWindow musicWindow;
        private HyrowserWindow hyrowserWindow;
        private NotifyIcon trayIcon;
        private ApiLogWindow apiLogWindow;
        private bool exitRequested;
        private readonly IgdbService igdb = new IgdbService(AppDomain.CurrentDomain.BaseDirectory);
        private readonly HashSet<int> igdbPending = new HashSet<int>();
        private SteamLibraryCache steamLibrary = new SteamLibraryCache { Games = new List<SteamGame>() };
        private string steamStatus = "idle";
        private string steamError = "";
        private int steamRequestVersion;
        private DateTime steamNextRefresh = DateTime.MinValue;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        public WebLauncherForm()
        {
            Text = "HYROLauncher";
            ClientSize = new Size(1280, 720);
            MinimumSize = Size;
            MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(8, 10, 18);

            string iconPath = Path.Combine(AssetsDir, "app.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);

            dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher-data.json");
            state = LoadState();
            SaveState();

            Controls.Add(webView);
            Controls.Add(linkView);
            Controls.Add(feedView);
            webView.Dock = DockStyle.Fill;
            linkView.Visible = false;
            feedView.Visible = false;
            InitializeStartupSurface();
            InitializeTrayIcon(iconPath);
            InitializeBigPicture();
            runningTimer.Interval = 10000;
            runningTimer.Tick += delegate { if (Visible && WindowState != FormWindowState.Minimized) SendRunningState(); };
            Load += OnLoaded;
            FormClosing += OnLauncherFormClosing;
            Resize += delegate { ApplyRoundedWindowRegion(); if (Visible && WindowState != FormWindowState.Minimized && webView.CoreWebView2 != null) SendRunningState(); };
            VisibleChanged += delegate { if (Visible && WindowState != FormWindowState.Minimized && webView.CoreWebView2 != null) SendRunningState(); };
            VisibleChanged += delegate {SyncLauncherActivity();};
            Resize += delegate {SyncLauncherActivity();};
            Shown += delegate { ApplyRoundedWindowRegion(); InitializeTcgDaily(); };
            FormClosed += delegate
            {
                HyTcgCloseDev();hyTcgDevTimer.Dispose();
                HyTcgEndSession();
                tcgDayTimer.Stop();tcgMovieTimer.Stop();tcgDayTimer.Dispose();tcgMovieTimer.Dispose();
                runningTimer.Stop();
                foreach (Process process in launchedProcesses.Values)
                {
                    try { process.Dispose(); } catch { }
                }
                launchedProcesses.Clear();
                runningTimer.Dispose();
                try { linkView.Dispose(); } catch { }
                try { feedView.Dispose(); } catch { }
                try { if (trayIcon != null) trayIcon.Dispose(); } catch { }
                try { if(apiLogWindow!=null)apiLogWindow.Close(); } catch { } ApiActivityLog.Stop();
                try { if (pluginWindow != null) pluginWindow.Close(); } catch { }
                try { if (musicWindow != null) musicWindow.CloseForLauncherExit(); } catch { }
                try { if (hyrowserWindow != null) hyrowserWindow.Close(); } catch { }
            };
        }

        private void ApplyRoundedWindowRegion()
        {
            if(bigPictureEnabled) {if(Region!=null){var old=Region;Region=null;old.Dispose();}return;}
            ApplyRoundedRegion(this, 18);
        }

        internal static void ApplyRoundedRegion(Form form, int radius)
        {
            if (form == null || form.Width <= 0 || form.Height <= 0) return;
            IntPtr handle = CreateRoundRectRgn(0, 0, form.Width + 1, form.Height + 1, radius, radius);
            if (handle == IntPtr.Zero) return;
            Region oldRegion = form.Region;
            form.Region = Region.FromHrgn(handle);
            DeleteObject(handle);
            if (oldRegion != null) oldRegion.Dispose();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_QUERYENDSESSION = 0x0011;
            const int WM_ENDSESSION = 0x0016;
            if (m.Msg == WM_QUERYENDSESSION || m.Msg == WM_ENDSESSION) exitRequested = true;
            base.WndProc(ref m);
        }

        private async void OnLoaded(object sender, EventArgs e)
        {
          try {
            CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            string webViewUserData = WebViewUserDataDir;
            Directory.CreateDirectory(webViewUserData);
            webEnvironment = await CoreWebView2Environment.CreateAsync(null, webViewUserData, options);
            await webView.EnsureCoreWebView2Async(webEnvironment);
            ApplyWindowVisuals(state == null || state.Settings == null ? "dark" : state.Settings.ThemeMode);
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            webView.CoreWebView2.Settings.IsPinchZoomEnabled = false;
            webView.ZoomFactor = 1;
            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            LayoutLinkView(currentSidebarPosition);

            string uiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui", "index.html");
            webView.CoreWebView2.Navigate(new Uri(uiPath).AbsoluteUri);
            webView.CoreWebView2.NavigationCompleted += delegate
            {
                SendState();
                SendBigPictureState();
                runningTimer.Start();
                SyncLauncherActivity();
            };
          } catch(Exception error) {if(startupSurface!=null)startupSurface.Failure((state.Settings.Language=="vi"?"Không thể khởi động WebView2. ":"Unable to start WebView2. ")+error.Message);}
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Dictionary<string, object> msg = serializer.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
            string type = GetString(msg, "type");

            if (type == "launcherRendered") FinishStartupSurface();
            else if (type == "ready") SendState();
            else if (type == "launch") Launch(GetString(msg, "id"));
            else if (type == "saveGame") SaveGame(msg);
            else if (type == "searchIgdb") SearchIgdb(msg);
            else if (type == "getIgdb") GetIgdb(msg);
            else if (type == "getExplore") GetExplore(msg);
            else if (type == "getSteamWishlist" || type == "getSteamDeals" || type == "getSteamFilters") GetSteamStore(msg,type);
            else if (type == "setBigPicture") SetBigPicture(GetBool(msg,"enabled"));
            else if (type == "savePlugin") SavePlugin(msg, false);
            else if (type == "saveSettings") SaveSettings(msg);
            else if (type == "bookshelfArtworkColor") HandleBookshelfArtworkColor(msg);
            else if (type == "getSteamBackgrounds") GetLibraryBackgrounds(msg);
            else if (type == "openHyrowser") OpenHyrowserUrl(GetString(msg, "url"));
            else if (type == "openWatchlistMovie") OpenWatchlistMovie(GetString(msg, "itemId"));
            else if (type == "refreshSteam") RefreshSteamLibrary(true);
            else if (type == "launchSteam")
            {
                int appId;
                if (Int32.TryParse(GetString(msg, "appId"), out appId) && steamLibrary.Games.Exists(g => g.AppId == appId)) LaunchSteamGame(appId);
            }
            else if (type == "saveSidebarOrder") SaveSidebarOrder(msg);
            else if (type == "showWebLink") ShowWebLink(GetString(msg, "url"), GetString(msg, "sidebarPosition"));
            else if (type == "hideWebLink") HideWebLink();
            else if (type == "layoutWebLink") LayoutLinkView(GetString(msg, "sidebarPosition"));
            else if (type == "setWebLinkFullscreen") SetWebLinkFullscreen(GetBool(msg, "enabled"));
            else if (type == "reloadWebLink") ReloadWebLink();
            else if (type == "showGameFeed") ShowGameFeed(msg);
            else if (type == "hideGameFeed") HideGameFeed();
            else if (type == "layoutGameFeed") LayoutGameFeed(msg);
            else if (type == "reloadGameFeed") ReloadGameFeed();
            else if (type == "showPluginWeb") ShowPluginWeb(GetString(msg, "url"), GetString(msg, "title"));
            else if (type == "showBuiltInPlugin") ShowBuiltInPlugin(GetString(msg, "id"));
            else if (type == "hytcgDevShortcut") HyTcgDevShortcut();
            else if (type == "hidePluginWeb") HidePluginWeb();
            else if (type == "layoutPluginWeb") DockPluginWindow();
            else if (type == "reloadPluginWeb") ReloadPluginWeb();
            else if (type == "exportData") ExportData();
            else if (type == "importData") ImportData();
            else if (type == "validatePin") ValidatePin(msg);
            else if (type == "changePin") ChangePin(msg);
            else if (type == "getSystemInfo") SendSystemInfo();
            else if (type == "cleanupUnusedAssets") CleanupUnusedAssets();
            else if (type == "clearWebPageData") ClearWebPageData();
            else if (type == "setWindowVisuals") ApplyWindowVisuals(GetString(msg, "themeMode"));
            else if (type == "launchPlugin") LaunchPlugin(GetString(msg, "id"));
            else if (type == "openExternal") OpenExternal(GetString(msg, "target"));
            else if (type == "scanGames") ScanGames();
            else if (type == "importScannedGames") ImportScannedGames(msg);
            else if (type == "deleteGame") DeleteGame(GetString(msg, "id"));
            else if (type == "deletePlugin") DeletePlugin(GetString(msg, "id"));
            else if (type == "addCategory") AddCategory(GetString(msg, "name"));
            else if (type == "chooseFile")
            {
                string kind = GetString(msg, "kind");
                ChooseFile(kind, GetString(msg, "gameId"), false);
            }
            else if (type == "mp3BgmReady")
            {
                if(mp3PlaybackActive && GetString(msg,"requestId")==mp3PlaybackRequest && musicWindow!=null && !musicWindow.IsDisposed)
                    musicWindow.Post(new Dictionary<string,object>{{"type","mp3PlaybackGranted"},{"requestId",mp3PlaybackRequest}});
            }
            else if (type == "minimize") WindowState = FormWindowState.Minimized;
            else if (type == "close") Close();
            else if (type == "drag")
            {
                if(bigPictureEnabled)return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        private void SendState()
        {
            PreparePetPlayback(state);
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["type"] = "state";
            payload["state"] = state;
            payload["runningIds"] = GetRunningGameIds();
            payload["mp3Playing"] = mp3PlaybackActive;
            payload["mp3RequestId"] = mp3PlaybackRequest;
            payload["assets"] = new Dictionary<string, string>
            {
                { "logo", Path.Combine(AssetsDir, "default-logo.png") },
                { "banner", Path.Combine(AssetsDir, "default-banner.png") },
                { "all", Path.Combine(AssetsDir, "all.png") },
                { "add", Path.Combine(AssetsDir, "add.png") },
                { "edit", Path.Combine(AssetsDir, "edit_game.png") },
                { "iconAllGame", Path.Combine(AssetsDir, "icons", "all-game.svg") },
                { "iconAdd", Path.Combine(AssetsDir, "icons", "add.svg") },
                { "iconEditGame", Path.Combine(AssetsDir, "icons", "app-game-edit.svg") },
                { "iconSettings", Path.Combine(AssetsDir, "icons", "launcher-setting.svg") },
                { "iconMinimize", Path.Combine(AssetsDir, "icons", "minimize.svg") },
                { "iconClose", Path.Combine(AssetsDir, "icons", "close.svg") },
                { "iconPluginUp", Path.Combine(AssetsDir, "icons", "plugin-up.svg") },
                { "iconPluginDown", Path.Combine(AssetsDir, "icons", "plugin-down.svg") },
                { "svgLogo", ReadSvgIcon("hyro-logo.svg") },
                { "svgAllGame", ReadSvgIcon("all-game.svg") },
                { "svgAdd", ReadSvgIcon("add.svg") },
                { "svgEditGame", ReadSvgIcon("app-game-edit.svg") },
                { "svgSettings", ReadSvgIcon("launcher-setting.svg") },
                { "svgMinimize", ReadSvgIcon("minimize.svg") },
                { "svgClose", ReadSvgIcon("close.svg") },
                { "svgPluginUp", ReadSvgIcon("plugin-up.svg") },
                { "svgPluginDown", ReadSvgIcon("plugin-down.svg") },
                { "svgAddCategory", ReadSvgIcon("add-category.svg") },
                { "svgScanGame", ReadSvgIcon("scan-game.svg") },
                { "svgFavorite", ReadSvgIcon("favorite.svg") },
                { "svgUnfavorite", ReadSvgIcon("unfavorite.svg") },
                { "svgFolderFavorite", ReadSvgIcon("folder-favorite.svg") },
                { "svgFolderPrivate", ReadSvgIcon("folder-private.svg") },
                { "svgPrivate", ReadSvgIcon("private.svg") },
                { "svgDelete", ReadSvgIcon("delete.svg") },
                { "svgSave", ReadSvgIcon("save.svg") },
                { "svgPlayApp", ReadSvgIcon("play-app.svg") },
                { "svgWatchlist", ReadSvgIcon("bookmark-plus.svg") },
                { "svgOpenExternal", ReadSvgIcon("open-external-link.svg") },
                { "svgReload", ReadSvgIcon("reload.svg") },
                { "svgDock", ReadSvgIcon("dock.svg") },
                { "svgSearch", ReadSvgIcon("search.svg") },
                { "svgSteam", ReadSvgIcon("steam.svg") },
                { "svgPet", ReadSvgIcon("pet.svg") },
                { "svgPopcorn", ReadSvgIcon("popcorn.svg") },
                { "svgMusic", ReadSvgIcon("music.svg") },
                { "svgHyrowser", ReadSvgIcon("hyrowser.svg") },
                { "svgHyTcg", ReadSvgIcon("hytcg.svg") },
                { "defaultBgm", Path.Combine(AssetsDir, "default BGM.mp3") },
                { "click", Path.Combine(AssetsDir, "click.mp3") },
                { "startGameEffect", Path.Combine(AssetsDir, "start_game.mp3") },
                { "switchGameEffect", Path.Combine(AssetsDir, "switch_to_game.mp3") },
                { "backToAllEffect", Path.Combine(AssetsDir, "back_to_all_game.mp3") }
            };
            Post(payload);
            RefreshSteamLibrary(false);
        }

        private async void GetSteamStore(Dictionary<string,object> msg,string type)
        {
            string requestId=GetString(msg,"requestId"),account=state.Settings.SteamId??"";
            var service=new SteamStoreService(AppDomain.CurrentDomain.BaseDirectory);
            int offset=ClampInt(GetString(msg,"offset"),0,0,100000);bool refresh=GetBool(msg,"refresh");
            ExploreSnapshot cached=null;
            try {
                if(type=="getSteamFilters") {
                    var options=await Task.Run(()=>service.Filters(refresh));
                    if(!IsDisposed)Post(new Dictionary<string,object>{{"type","steamFilters"},{"requestId",requestId},{"options",options},{"error",""}});return;
                }
                var filters=serializer.Deserialize<SteamDealFilters>(GetString(msg,"filters")==""?"{}":GetString(msg,"filters"));
                cached=service.Cached(type=="getSteamWishlist"?"wishlist:"+account+":"+offset:service.DealsKey(filters,offset));
                var snapshot=await Task.Run(()=>type=="getSteamWishlist"?service.Wishlist(account,offset,refresh):service.Deals(filters,offset,refresh));
                if(!IsDisposed)Post(new Dictionary<string,object>{{"type","explore"},{"requestId",requestId},{"snapshot",snapshot},{"steamId",account},{"error",""}});
            } catch(Exception ex) {
                string error=ex.Message=="WishlistUnavailable"?"WishlistUnavailable":ex.Message=="SteamIdRequired"?"SteamIdRequired":"SteamConnectionFailed";
                if(!IsDisposed)Post(new Dictionary<string,object>{{"type",type=="getSteamFilters"?"steamFilters":"explore"},{"requestId",requestId},{"snapshot",cached},{"steamId",account},{"error",error}});
            }
        }

        private async void GetExplore(Dictionary<string,object> msg)
        {
            string mode=GetString(msg,"mode"),query=GetString(msg,"query"),requestId=GetString(msg,"requestId");
            var service=new ExploreService(AppDomain.CurrentDomain.BaseDirectory,igdb);
            int offset=mode=="deals"?ClampInt(GetString(msg,"offset"),0,0,100000):0;
            var cached=service.CachedPage(mode,query,offset);
            try {
                bool refresh=GetBool(msg,"refresh");
                var snapshot=await Task.Run(()=>service.GetPage(mode,query,refresh,offset));
                if(!IsDisposed) Post(new Dictionary<string,object>{{"type","explore"},{"requestId",requestId},{"snapshot",snapshot},{"error",""}});
            } catch(Exception ex) {
                if(!IsDisposed) Post(new Dictionary<string,object>{{"type","explore"},{"requestId",requestId},{"snapshot",cached},{"error",mode=="deals"?"SteamConnectionFailed":IgdbError(ex)}});
            }
        }

        private async void SearchIgdb(Dictionary<string,object> msg)
        {
            string requestId = GetString(msg,"requestId");
            try
            {
                string query = GetString(msg,"query");
                var games = await Task.Run(() => igdb.Search(query));
                if (!IsDisposed) Post(new Dictionary<string,object> { {"type","igdbSearch"},{"requestId",requestId},{"games",games},{"error",""} });
            }
            catch(Exception ex)
            {
                if (!IsDisposed) Post(new Dictionary<string,object> { {"type","igdbSearch"},{"requestId",requestId},{"error",IgdbError(ex)} });
            }
        }
        private async void GetIgdb(Dictionary<string,object> msg)
        {
            GameEntry game = state.Games.Find(g => g.Id == GetString(msg,"gameId"));
            if (game==null || game.IgdbId<=0) return;
            int id=game.IgdbId;
            if (!igdbPending.Add(id)) return;
            IgdbGameInfo cached=igdb.Cached(id);
            if (cached!=null) Post(new Dictionary<string,object> { {"type","igdbInfo"},{"id",id},{"info",cached},{"error",""} });
            try
            {
                bool force=GetBool(msg,"refresh");
                var info=await Task.Run(() => igdb.Get(id,force));
                if (!IsDisposed) Post(new Dictionary<string,object> { {"type","igdbInfo"},{"id",id},{"info",info},{"error",""} });
            }
            catch(Exception ex)
            {
                if (!IsDisposed) Post(new Dictionary<string,object> { {"type","igdbInfo"},{"id",id},{"info",cached},{"error",IgdbError(ex)} });
            }
            finally { igdbPending.Remove(id); }
        }
        private static string IgdbError(Exception ex)
        {
            if (ex is InvalidOperationException && new List<string> {"MissingCredentials","InvalidCredentials","AuthenticationFailed","RateLimited","NotFound","SearchLength"}.Contains(ex.Message)) return ex.Message;
            var network=ex as System.Net.WebException;
            var response=network==null ? null : network.Response as System.Net.HttpWebResponse;
            if(response!=null && ((int)response.StatusCode==401 || (int)response.StatusCode==403)) return "AuthenticationFailed";
            return "ConnectionFailed";
        }

        private void SendSteamLibrary()
        {
            if (IsDisposed || webView.CoreWebView2 == null) return;
            Post(new Dictionary<string, object> { { "type", "steamLibrary" }, { "steamId", steamLibrary.SteamId ?? "" },
                { "games", steamLibrary.Games }, { "profile", steamLibrary.Profile }, { "status", steamStatus }, { "error", steamError }, { "updatedUtc", steamLibrary.UpdatedUtc.ToString("o") } });
        }

        private async void RefreshSteamLibrary(bool force)
        {
            string account = state.Settings.SteamId ?? "";
            if (steamLibrary.SteamId != account)
            {
                steamRequestVersion++;
                steamLibrary = new SteamLibraryCache { SteamId = account, Games = new List<SteamGame>() };
                steamStatus = "idle"; steamError = ""; steamNextRefresh = DateTime.MinValue;
                try
                {
                    string cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steam-library-cache.json");
                    if (File.Exists(cachePath))
                    {
                        SteamLibraryCache cached = serializer.Deserialize<SteamLibraryCache>(File.ReadAllText(cachePath));
                        if (cached != null && cached.SteamId == account && cached.Games != null)
                        { steamLibrary = cached; steamStatus = "ready"; steamNextRefresh = cached.Profile == null ? DateTime.MinValue : cached.UpdatedUtc.AddHours(6); TcgMissionSourcesChanged(); }
                    }
                }
                catch { }
            }
            SendSteamLibrary();
            if (account == "" || steamStatus == "loading" || (!force && DateTime.UtcNow < steamNextRefresh)) return;
            int version = ++steamRequestVersion;
            steamStatus = "loading"; steamError = ""; SendSteamLibrary();
            string keyPath = Path.Combine(AssetsDir, "apikey", "steamapikey.txt");
            try
            {
                SteamLibraryCache library = await Task.Run(() => SteamLibraryService.FetchLibrary(account, keyPath));
                if (IsDisposed || version != steamRequestVersion || account != state.Settings.SteamId) return;
                steamLibrary = library;
                TcgMissionSourcesChanged();
                steamStatus = "ready"; steamNextRefresh = DateTime.UtcNow.AddHours(6);
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steam-library-cache.json"), serializer.Serialize(steamLibrary)); } catch { }
            }
            catch (Exception ex)
            {
                if (IsDisposed || version != steamRequestVersion || account != state.Settings.SteamId) return;
                steamStatus = "error";
                steamError = ex is InvalidOperationException ? ex.Message : "ConnectionFailed";
                steamNextRefresh = DateTime.UtcNow.AddMinutes(5);
            }
            SendSteamLibrary();
        }

        private static string ReadSvgIcon(string name)
        {
            try
            {
                string path = Path.Combine(AssetsDir, "icons", name);
                return File.Exists(path) ? File.ReadAllText(path) : "";
            }
            catch { return ""; }
        }

        private void SendRunningState()
        {
            Post(new Dictionary<string, object>
            {
                { "type", "runningState" },
                { "runningIds", GetRunningGameIds() }
            });
        }

        private void InitializeTrayIcon(string iconPath)
        {
            trayIcon = new NotifyIcon();
            trayIcon.Text = "HYROLauncher";
            trayIcon.Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
            trayIcon.Visible = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open HYROLauncher", null, delegate { ShowLauncherWindow(); });
            menu.Items.Add("Hide", null, delegate { Hide(); });
            menu.Items.Add("API Monitor", null, delegate {
                if(apiLogWindow==null||apiLogWindow.IsDisposed)apiLogWindow=new ApiLogWindow(AppDomain.CurrentDomain.BaseDirectory,CurrentAccentColor());
                apiLogWindow.Show();if(apiLogWindow.WindowState==FormWindowState.Minimized)apiLogWindow.WindowState=FormWindowState.Normal;apiLogWindow.BringToFront();apiLogWindow.Activate();
            });
            menu.Items.Add("Exit", null, delegate { exitRequested = true; Close(); });
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate { ShowLauncherWindow(); };
        }

        private void OnLauncherFormClosing(object sender, FormClosingEventArgs e)
        {
            if (exitRequested || e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing) return;
            e.Cancel = true;
            Hide();
            try
            {
                if (trayIcon != null) trayIcon.ShowBalloonTip(1200, "HYROLauncher", "Launcher is still running in the system tray.", ToolTipIcon.Info);
            }
            catch { }
        }

        private void ShowLauncherWindow()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        private void SendSystemInfo()
        {
            Dictionary<string, object> info = new Dictionary<string, object>();
            info["MachineName"] = Environment.MachineName;
            info["UserName"] = Environment.UserName;
            info["OS"] = Environment.OSVersion.VersionString;
            info["Cpu"] = ReadCpuName();
            info["Cores"] = Environment.ProcessorCount.ToString();
            info["Memory"] = FormatBytes(GetTotalPhysicalMemoryBytes());
            Rectangle bounds = Screen.PrimaryScreen == null ? Rectangle.Empty : Screen.PrimaryScreen.Bounds;
            info["Display"] = bounds.Width > 0 ? bounds.Width + " x " + bounds.Height : "";
            info["AppPath"] = Application.ExecutablePath;
            info["DataPath"] = dataPath;
            info["AssetsPath"] = AssetsDir;
            info["WebViewData"] = WebViewUserDataDir;
            ulong dataFileSize = GetFileSize(dataPath);
            ulong assetsSize = GetDirectorySize(AssetsDir);
            ulong libraryAssetsSize = GetDirectorySize(LibraryAssetsDir);
            ulong pluginAssetsSize = GetDirectorySize(PluginAssetsDir);
            ulong builtInAssetsSize = GetDirectorySize(BuiltInPluginPacksDir);
            ulong webViewDataSize = GetDirectorySize(WebViewUserDataDir);
            info["LauncherStorageSize"] = FormatBytes(dataFileSize + assetsSize + webViewDataSize);
            info["DataFileSize"] = FormatBytes(dataFileSize);
            info["AssetsSize"] = FormatBytes(assetsSize);
            info["LibraryAssetsSize"] = FormatBytes(libraryAssetsSize);
            info["PluginAssetsSize"] = FormatBytes(pluginAssetsSize);
            info["BuiltInAssetsSize"] = FormatBytes(builtInAssetsSize);
            info["WebViewDataSize"] = FormatBytes(webViewDataSize);
            info["AssetFilesCount"] = CountFiles(AssetsDir).ToString();
            info["GamesCount"] = state == null || state.Games == null ? "0" : state.Games.Count.ToString();
            info["PluginsCount"] = state == null || state.Plugins == null ? "0" : state.Plugins.Count.ToString();
            Post(new Dictionary<string, object> { { "type", "systemInfo" }, { "info", info } });
        }

        private void CleanupUnusedAssets()
        {
            int deleted = 0;
            deleted += CleanupUnusedAssetFolders(LibraryAssetsDir, GetValidGameAssetIds());
            deleted += CleanupUnusedAssetFolders(PluginAssetsDir, GetValidPluginAssetIds());
            Notify("Cleaned unused launcher assets: " + deleted + " folder(s).");
            SendSystemInfo();
        }

        private HashSet<string> GetValidGameAssetIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (state == null || state.Games == null) return ids;
            foreach (GameEntry game in state.Games)
            {
                if (game == null || String.IsNullOrWhiteSpace(game.Id)) continue;
                ids.Add(SafeAssetId(game.Id));
            }
            return ids;
        }

        private HashSet<string> GetValidPluginAssetIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (state == null || state.Plugins == null) return ids;
            foreach (PluginEntry plugin in state.Plugins)
            {
                if (plugin == null || String.IsNullOrWhiteSpace(plugin.Id)) continue;
                ids.Add(SafeAssetId(plugin.Id));
            }
            return ids;
        }

        private static int CleanupUnusedAssetFolders(string rootDir, HashSet<string> validIds)
        {
            int deleted = 0;
            try
            {
                if (!Directory.Exists(rootDir)) return 0;
                string root = EnsureTrailingSeparator(Path.GetFullPath(rootDir));
                foreach (string dir in Directory.GetDirectories(rootDir))
                {
                    string full = Path.GetFullPath(dir);
                    if (!IsUnderDirectory(full, root)) continue;
                    string name = Path.GetFileName(full);
                    if (validIds.Contains(name)) continue;
                    try
                    {
                        Directory.Delete(full, true);
                        deleted++;
                    }
                    catch { }
                }
            }
            catch { }
            return deleted;
        }

        private async void ClearWebPageData()
        {
            try
            {
                await ClearBrowsingDataForViewAsync(webView);
                await ClearBrowsingDataForViewAsync(linkView);
                await ClearBrowsingDataForViewAsync(feedView);
                if (pluginWindow != null && !pluginWindow.IsDisposed) await pluginWindow.ClearBrowsingDataAsync();
                if (musicWindow != null && !musicWindow.IsDisposed) await musicWindow.ClearBrowsingDataAsync();
                if (hyrowserWindow != null && !hyrowserWindow.IsDisposed) await hyrowserWindow.ClearBrowsingDataAsync();
            }
            catch { }

            int deleted = DeleteKnownWebViewDataFolders(WebViewUserDataDir);
            Notify("Cleared webpage data. Removed " + deleted + " cache folder(s). Restart launcher if a few WebView files are still locked.");
            SendSystemInfo();
        }

        internal static Task ClearBrowsingDataForViewAsync(WebView2 view)
        {
            try
            {
                if (view == null || view.CoreWebView2 == null) return Task.FromResult(0);
                object core = view.CoreWebView2;
                object profile = core.GetType().GetProperty("Profile") == null ? null : core.GetType().GetProperty("Profile").GetValue(core, null);
                if (profile == null) return Task.FromResult(0);
                System.Reflection.MethodInfo method = profile.GetType().GetMethod("ClearBrowsingDataAsync", Type.EmptyTypes);
                if (method == null) return Task.FromResult(0);
                Task task = method.Invoke(profile, null) as Task;
                return task ?? Task.FromResult(0);
            }
            catch { return Task.FromResult(0); }
        }

        private static int DeleteKnownWebViewDataFolders(string rootDir)
        {
            if (String.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir)) return 0;
            string[] names =
            {
                "Cache", "Code Cache", "GPUCache", "DawnCache", "ShaderCache",
                "Service Worker\\CacheStorage", "Service Worker\\ScriptCache",
                "IndexedDB", "Local Storage", "Session Storage", "blob_storage",
                "Network\\Cache"
            };
            int deleted = 0;
            foreach (string profileDir in GetWebViewProfileDirs(rootDir))
            {
                foreach (string name in names)
                {
                    string target = Path.Combine(profileDir, name);
                    if (TryDeleteDirectorySafe(rootDir, target)) deleted++;
                }
            }
            return deleted;
        }

        private static List<string> GetWebViewProfileDirs(string rootDir)
        {
            List<string> dirs = new List<string>();
            try
            {
                dirs.Add(rootDir);
                foreach (string dir in Directory.GetDirectories(rootDir))
                {
                    string name = Path.GetFileName(dir);
                    if (String.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("HYRO", StringComparison.OrdinalIgnoreCase))
                    {
                        dirs.Add(dir);
                    }
                }
            }
            catch { }
            return dirs;
        }

        private static bool TryDeleteDirectorySafe(string rootDir, string targetDir)
        {
            try
            {
                if (!Directory.Exists(targetDir)) return false;
                string root = EnsureTrailingSeparator(Path.GetFullPath(rootDir));
                string target = Path.GetFullPath(targetDir);
                if (!IsUnderDirectory(target, root)) return false;
                Directory.Delete(target, true);
                return true;
            }
            catch { return false; }
        }

        private static string ReadCpuName()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    object value = key == null ? null : key.GetValue("ProcessorNameString");
                    return value == null ? "" : value.ToString().Trim();
                }
            }
            catch { return ""; }
        }

        private static ulong GetTotalPhysicalMemoryBytes()
        {
            try
            {
                MEMORYSTATUSEX status = new MEMORYSTATUSEX();
                status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                return GlobalMemoryStatusEx(ref status) ? status.ullTotalPhys : 0;
            }
            catch { return 0; }
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes == 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024d && unit < units.Length - 1)
            {
                value /= 1024d;
                unit++;
            }
            return value.ToString(unit == 0 ? "0" : "0.##") + " " + units[unit];
        }

        private static ulong GetFileSize(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return 0;
                return (ulong)new FileInfo(path).Length;
            }
            catch { return 0; }
        }

        private static ulong GetDirectorySize(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return 0;
                ulong total = 0;
                Queue<string> queue = new Queue<string>();
                queue.Enqueue(path);
                while (queue.Count > 0)
                {
                    string dir = queue.Dequeue();
                    try
                    {
                        foreach (string file in Directory.GetFiles(dir))
                        {
                            try { total += (ulong)new FileInfo(file).Length; }
                            catch { }
                        }
                        foreach (string subDir in Directory.GetDirectories(dir)) queue.Enqueue(subDir);
                    }
                    catch { }
                }
                return total;
            }
            catch { return 0; }
        }

        private static int CountFiles(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return 0;
                int total = 0;
                Queue<string> queue = new Queue<string>();
                queue.Enqueue(path);
                while (queue.Count > 0)
                {
                    string dir = queue.Dequeue();
                    try
                    {
                        total += Directory.GetFiles(dir).Length;
                        foreach (string subDir in Directory.GetDirectories(dir)) queue.Enqueue(subDir);
                    }
                    catch { }
                }
                return total;
            }
            catch { return 0; }
        }

        private async void ShowWebLink(string url, string sidebarPosition)
        {
            if (String.IsNullOrWhiteSpace(url)) return;
            linkRequested=true;int version=++linkRequestVersion;
            try {await EnsureAuxiliaryView(false);}catch {return;}
            if(IsDisposed || !linkRequested || version!=linkRequestVersion)return;
            linkView.CoreWebView2.Resume();
            currentSidebarPosition = NormalizeSidebarPosition(sidebarPosition);
            LayoutLinkView(currentSidebarPosition);
            if (linkView.CoreWebView2 != null && !String.Equals(currentWebLinkUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                linkView.CoreWebView2.Navigate(url);
                currentWebLinkUrl = url;
            }
            linkView.Visible = true;
            linkView.BringToFront();
            SyncLauncherActivity();
        }

        private void HideWebLink()
        {
            linkRequested=false;++linkRequestVersion;
            linkView.Visible = false;
            webLinkFullscreen = false;
            LayoutLinkView(currentSidebarPosition);
            SyncLauncherActivity();
        }

        private void SetWebLinkFullscreen(bool enabled)
        {
            webLinkFullscreen = enabled;
            LayoutLinkView(currentSidebarPosition);
            if (linkView.Visible) linkView.BringToFront();
        }

        private void ReloadWebLink()
        {
            try
            {
                if (linkView.CoreWebView2 == null || String.IsNullOrWhiteSpace(currentWebLinkUrl)) return;
                try { linkView.CoreWebView2.Reload(); }
                catch { linkView.CoreWebView2.Navigate(currentWebLinkUrl); }
            }
            catch { }
        }

        private void LayoutLinkView(string sidebarPosition)
        {
            currentSidebarPosition = NormalizeSidebarPosition(sidebarPosition);
            if (webLinkFullscreen)
            {
                int fullscreenTop = 52;
                linkView.Bounds = new Rectangle(0, fullscreenTop, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height - fullscreenTop));
                return;
            }
            int left = currentSidebarPosition == "left" ? 72 : 0;
            int right = currentSidebarPosition == "right" ? 72 : 0;
            int topOffset = currentSidebarPosition == "top" ? 72 : 0;
            int bottom = currentSidebarPosition == "bottom" ? 72 : 0;
            int top = topOffset + 94;
            int width = Math.Max(1, ClientSize.Width - left - right);
            int height = Math.Max(1, ClientSize.Height - top - bottom);
            linkView.Bounds = new Rectangle(left, top, width, height);
        }

        private async void ShowGameFeed(Dictionary<string, object> msg)
        {
            string url = GetString(msg, "url");
            if (String.IsNullOrWhiteSpace(url)) return;
            feedRequested=true;int version=++feedRequestVersion;
            try {await EnsureAuxiliaryView(true);}catch {return;}
            if(IsDisposed || !feedRequested || version!=feedRequestVersion)return;
            feedView.CoreWebView2.Resume();
            LayoutGameFeed(msg);
            if (feedView.CoreWebView2 != null && !String.Equals(currentFeedUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                feedView.CoreWebView2.IsMuted = true;
                feedView.ZoomFactor = 0.25;
                feedView.CoreWebView2.Navigate(url);
                currentFeedUrl = url;
            }
            feedView.Visible = true;
            feedView.BringToFront();
            SyncLauncherActivity();
        }

        private void HideGameFeed()
        {
            feedRequested=false;++feedRequestVersion;
            feedView.Visible = false;
            SyncLauncherActivity();
        }

        private void ReloadGameFeed()
        {
            try
            {
                if (feedView.CoreWebView2 == null || String.IsNullOrWhiteSpace(currentFeedUrl)) return;
                try { feedView.CoreWebView2.Reload(); }
                catch { feedView.CoreWebView2.Navigate(currentFeedUrl); }
            }
            catch { }
        }

        private void LayoutGameFeed(Dictionary<string, object> msg)
        {
            int x = GetPixel(msg, "x");
            int y = GetPixel(msg, "y");
            int width = Math.Max(1, GetPixel(msg, "width"));
            int height = Math.Max(1, GetPixel(msg, "height"));
            if (width <= 1 || height <= 1) return;

            x = Math.Max(0, Math.Min(ClientSize.Width - 1, x));
            y = Math.Max(0, Math.Min(ClientSize.Height - 1, y));
            width = Math.Max(1, Math.Min(ClientSize.Width - x, width));
            height = Math.Max(1, Math.Min(ClientSize.Height - y, height));
            feedView.Bounds = new Rectangle(x, y, width, height);
            if (feedView.Visible) feedView.BringToFront();
        }

        private async void ShowPluginWeb(string url, string title)
        {
            if (String.IsNullOrWhiteSpace(url)) return;
            if(currentBuiltInPluginId==BuiltInHyTcgPluginId){HyTcgCloseDev();HyTcgEndSession();}
            if(musicWindow!=null && !musicWindow.IsDisposed) musicWindow.Hide();
            if (pluginWindow == null || pluginWindow.IsDisposed)
            {
                pluginWindow = new PluginBrowserWindow(this);
                pluginWindow.FormClosed += delegate { pluginWindow = null; currentPluginUrl = ""; };
            }
            pluginWindow.SetAccent(CurrentAccentColor());
            await pluginWindow.EnsureWebViewAsync(webEnvironment);
            pluginWindow.ShowPlugin(title, url);
            if (!String.Equals(currentPluginUrl, url, StringComparison.OrdinalIgnoreCase)) currentPluginUrl = url;
            currentBuiltInPluginId = "";
        }

        private async void ShowBuiltInPlugin(string id)
        {
            PluginEntry plugin = state.Plugins.Find(p => p.Id == id);
            if (plugin == null || !plugin.BuiltIn) return;
            if(currentBuiltInPluginId==BuiltInHyTcgPluginId && id!=BuiltInHyTcgPluginId)
            {HyTcgCloseDev();if(plugin.Type!="mp3")HyTcgEndSession();}
            if(String.Equals(plugin.Type,"mp3",StringComparison.OrdinalIgnoreCase))
            {
                ShowMusicPlayer(plugin);
                return;
            }
            if(musicWindow!=null && !musicWindow.IsDisposed) musicWindow.Hide();
            if (String.Equals(plugin.Type, "hyrowser", StringComparison.OrdinalIgnoreCase) || String.Equals(plugin.Id, BuiltInHyrowserPluginId, StringComparison.OrdinalIgnoreCase))
            {
                ShowHyrowser(plugin);
                return;
            }
            if (pluginWindow == null || pluginWindow.IsDisposed)
            {
                pluginWindow = new PluginBrowserWindow(this);
                pluginWindow.FormClosed += delegate { pluginWindow = null; currentPluginUrl = ""; currentBuiltInPluginId = ""; };
            }
            pluginWindow.SetAccent(CurrentAccentColor());
            await pluginWindow.EnsureWebViewAsync(webEnvironment);
            currentBuiltInPluginId = plugin.Id;
            currentPluginUrl = "";
            bool navigated=pluginWindow.ShowPlugin(plugin.Name, PluginBrowserWindow.LocalPanelUrl(plugin.Id==BuiltInHyTcgPluginId ? "hytcg.html" : "plugin.html",plugin.Id));
            if (!navigated) SendPluginState();
        }

        private async void ShowMusicPlayer(PluginEntry plugin)
        {
            if(pluginWindow!=null && !pluginWindow.IsDisposed) pluginWindow.Hide();
            if(musicWindow==null || musicWindow.IsDisposed)
            {
                musicWindow=new PluginBrowserWindow(this,true);
                musicWindow.FormClosed+=delegate { musicWindow=null; };
            }
            musicWindow.SetAccent(CurrentAccentColor());
            await musicWindow.EnsureWebViewAsync(webEnvironment);
            string panelPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui","plugin.html");
            bool navigated=musicWindow.ShowPlugin(plugin.Name,new Uri(panelPath).AbsoluteUri+"?id="+Uri.EscapeDataString(plugin.Id));
            if(!navigated) SendPanelState(musicWindow,plugin.Id);
        }

        private async void ShowHyrowser(PluginEntry plugin)
        {
            if(musicWindow!=null && !musicWindow.IsDisposed) musicWindow.Hide();
            EnsureHyrowserWindow();
            await hyrowserWindow.EnsureWebViewAsync(webEnvironment);
            string target = plugin == null || String.IsNullOrWhiteSpace(plugin.Target) ? "https://www.google.com" : plugin.Target;
            hyrowserWindow.ShowBrowser(target);
            currentBuiltInPluginId = "";
            currentPluginUrl = "";
        }

        internal Color CurrentAccentColor()
        {
            string value = state == null || state.Settings == null ? "" : state.Settings.AccentColor;
            return ParseHexColor(value, Color.FromArgb(68, 168, 255));
        }

        internal string CurrentThemeMode()
        {
            return state == null || state.Settings == null ? "dark" : state.Settings.ThemeMode;
        }

        internal static string NormalizeThemeModeForWindows(string value)
        {
            return NormalizeThemeMode(value);
        }

        internal static Color ParseHexColor(string value, Color fallback)
        {
            value = NormalizeHexColor(value);
            if (String.IsNullOrWhiteSpace(value)) return fallback;
            try { return ColorTranslator.FromHtml(value); }
            catch { return fallback; }
        }

        private void HidePluginWeb()
        {
            // The main UI only owns the embedded/custom web panel. Built-in tools
            // remain open through main-window state/layout refreshes.
            if (!String.IsNullOrWhiteSpace(currentBuiltInPluginId)) return;
            if (pluginWindow != null && !pluginWindow.IsDisposed) pluginWindow.Hide();
        }

        internal void PluginClosedByUser(PluginBrowserWindow sourceWindow)
        {
            if(sourceWindow!=musicWindow)
            {
                if(currentBuiltInPluginId==BuiltInHyTcgPluginId) {HyTcgCloseDev();HyTcgEndSession();}
                currentPluginUrl = "";
                currentBuiltInPluginId = "";
            }
            Post(new Dictionary<string, object> { { "type", "pluginClosed" } });
        }

        internal void PlayUiClick()
        {
            if (state == null || state.Settings.DisableEffects) return;
            Post(new Dictionary<string, object> { { "type", "uiEffect" } });
        }

        internal void PluginReady(PluginBrowserWindow sourceWindow)
        {
            if(sourceWindow==musicWindow) SendPanelState(musicWindow,BuiltInMp3PluginId);
            else SendPluginState();
        }

        internal void PluginPlaybackStopped()
        {
            if(!mp3PlaybackActive) return;
            mp3PlaybackActive=false; mp3PlaybackRequest=""; mp3PlaybackPluginId="";
            Post(new Dictionary<string,object>{{"type","mp3PlaybackState"},{"playing",false}});
        }

        internal void HandlePluginMessage(string json,PluginBrowserWindow sourceWindow)
        {
            if(String.IsNullOrEmpty(json) || json.Length>12*1024*1024) return;
            Dictionary<string, object> msg;
            try { msg = new JavaScriptSerializer { MaxJsonLength=12*1024*1024 }.Deserialize<Dictionary<string, object>>(json); }
            catch { return; }
            if(msg==null) return;
            string type = GetString(msg, "type");
            if(type.StartsWith("hytcg",StringComparison.Ordinal)) { HandleHyTcgMessage(msg,sourceWindow); return; }
            if (type == "ready") PluginReady(sourceWindow);
            else if (type == "savePlugin") SavePlugin(msg, true);
            else if (type == "previewPetTiming") PreviewPetTiming(msg);
            else if (type == "mp3Playback")
            {
                if(sourceWindow!=musicWindow) return;
                string sourceId=GetString(msg,"pluginId"),request=GetString(msg,"requestId");
                PluginEntry source=state.Plugins.Find(p=>p.Id==sourceId && p.Type=="mp3");
                if(source==null) return;
                if(GetBool(msg,"playing"))
                {
                    mp3PlaybackPluginId=sourceId; mp3PlaybackRequest=request; mp3PlaybackActive=true;
                    Post(new Dictionary<string,object>{{"type","mp3PlaybackState"},{"playing",true},{"requestId",request}});
                }
                else if(sourceId==mp3PlaybackPluginId && request==mp3PlaybackRequest) PluginPlaybackStopped();
            }
            else if (type == "chooseFile")
            {
                ChooseFile(GetString(msg, "kind"), "", true,sourceWindow);
            }
            else if (type == "openMovieFromPlugin") OpenWatchlistMovie(GetString(msg, "itemId"));
            else if (type == "exportWatchlistExcel") ExportWatchlistExcel();
            else if (type == "importWatchlistExcel") PreviewWatchlistImport();
            else if (type == "commitWatchlistImport") CommitWatchlistImport(GetString(msg, "requestId"));
            else if (type == "openExternal") OpenExternal(GetString(msg, "target"));
        }

        private void SendPluginState()
        {
            SendPanelState(pluginWindow,currentBuiltInPluginId);
            SendPanelState(musicWindow,BuiltInMp3PluginId);
        }

        private void SendPanelState(PluginBrowserWindow targetWindow,string panelPluginId)
        {
            if (targetWindow == null || targetWindow.IsDisposed || String.IsNullOrWhiteSpace(panelPluginId)) return;
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["type"] = "state";
            payload["pluginId"] = panelPluginId;
            payload["state"] = CreatePluginPanelState(panelPluginId);
            payload["assets"] = new Dictionary<string, string>
            {
                { "logo", Path.Combine(AssetsDir, "default-logo.png") },
                { "svgAdd", ReadSvgIcon("add.svg") },
                { "click", Path.Combine(AssetsDir, "click.mp3") },
                { "startGameEffect", Path.Combine(AssetsDir, "start_game.mp3") },
                { "switchGameEffect", Path.Combine(AssetsDir, "switch_to_game.mp3") },
                { "backToAllEffect", Path.Combine(AssetsDir, "back_to_all_game.mp3") },
                { "svgSave", ReadSvgIcon("save.svg") },
                { "svgDelete", ReadSvgIcon("delete.svg") },
                { "svgWatchlist", ReadSvgIcon("bookmark-plus.svg") },
                { "svgPopcorn", ReadSvgIcon("popcorn.svg") },
                { "svgMusic", ReadSvgIcon("music.svg") },
                { "svgPet", ReadSvgIcon("pet.svg") }
            };
            targetWindow.Post(payload);
            if (pendingBrowserWatch != null && panelPluginId == BuiltInMoviePluginId)
            {
                targetWindow.Post(pendingBrowserWatch);
                pendingBrowserWatch = null;
            }
        }

        private LauncherState CreatePluginPanelState(string panelPluginId=null)
        {
            panelPluginId=panelPluginId ?? currentBuiltInPluginId;
            LauncherState panelState = serializer.Deserialize<LauncherState>(serializer.Serialize(state));
            if (panelState == null) panelState = new LauncherState();
            if (panelState.Games == null) panelState.Games = new List<GameEntry>();
            if (panelState.Plugins == null) panelState.Plugins = new List<PluginEntry>();

            HashSet<string> privateGameIds = GetPrivateGameIds(state == null ? null : state.Games);
            panelState.Games.RemoveAll(game => game == null || game.Private);
            panelState.Plugins.RemoveAll(plugin => plugin == null || !String.Equals(plugin.Id, panelPluginId, StringComparison.OrdinalIgnoreCase));
            if (panelState.Settings != null) panelState.Settings.PrivatePinHash = "";

            PluginEntry activePanelPlugin = panelState.Plugins.Find(plugin => String.Equals(plugin.Id, panelPluginId, StringComparison.OrdinalIgnoreCase));
            if (activePanelPlugin != null && String.Equals(activePanelPlugin.Type, "movie-watchlist", StringComparison.OrdinalIgnoreCase))
            {
                if (activePanelPlugin.MovieWebsiteGameIds == null) activePanelPlugin.MovieWebsiteGameIds = new List<string>();
                if (activePanelPlugin.WatchItems == null) activePanelPlugin.WatchItems = new List<MovieWatchItem>();
                activePanelPlugin.MovieWebsiteGameIds.RemoveAll(id => privateGameIds.Contains(id));
                activePanelPlugin.WatchItems.RemoveAll(item => IsPrivateMovieWatchItem(item, privateGameIds));
            }
            PreparePetPlayback(panelState);
            return panelState;
        }

        private static HashSet<string> GetPrivateGameIds(List<GameEntry> games)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (games == null) return ids;
            foreach (GameEntry game in games)
            {
                if (game != null && game.Private && !String.IsNullOrWhiteSpace(game.Id)) ids.Add(game.Id);
            }
            return ids;
        }

        private bool IsPrivateMovieWatchItem(MovieWatchItem item, HashSet<string> privateGameIds)
        {
            if (item == null) return false;
            if (!String.IsNullOrWhiteSpace(item.SourceGameId) && privateGameIds.Contains(item.SourceGameId)) return true;
            if (String.IsNullOrWhiteSpace(item.Url) || state == null || state.Games == null) return false;

            Uri itemUri;
            if (!Uri.TryCreate(item.Url, UriKind.Absolute, out itemUri)) return false;
            foreach (GameEntry game in state.Games)
            {
                if (game == null || !game.Private || !game.IsUrl || String.IsNullOrWhiteSpace(game.Path)) continue;
                Uri gameUri;
                if (!Uri.TryCreate(game.Path, UriKind.Absolute, out gameUri)) continue;
                if (String.Equals(itemUri.Scheme, gameUri.Scheme, StringComparison.OrdinalIgnoreCase)
                    && String.Equals(itemUri.Host, gameUri.Host, StringComparison.OrdinalIgnoreCase)
                    && itemUri.Port == gameUri.Port) return true;
            }
            return false;
        }

        private void ReloadPluginWeb()
        {
            try
            {
                if (pluginWindow != null && !pluginWindow.IsDisposed) pluginWindow.ReloadPlugin();
            }
            catch { }
        }

        private void DockPluginWindow()
        {
            if (pluginWindow != null && !pluginWindow.IsDisposed) pluginWindow.DockToOwner();
        }

        private void ExportData()
        {
            SaveState();
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Export HYROLauncher backup";
                dlg.Filter = "HYROLauncher backup (*.zip)|*.zip";
                dlg.FileName = "HYROLauncher-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string tempDir = Path.Combine(Path.GetTempPath(), "HYROLauncherBackup_" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(tempDir);
                    if (File.Exists(dataPath)) File.Copy(dataPath, Path.Combine(tempDir, "launcher-data.json"), true);
                    if (Directory.Exists(LibraryAssetsDir)) CopyDirectory(LibraryAssetsDir, Path.Combine(tempDir, "assets", "library"), true);
                    if (Directory.Exists(PluginAssetsDir)) CopyDirectory(PluginAssetsDir, Path.Combine(tempDir, "assets", "plugins"), true);
                    if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);
                    ZipFile.CreateFromDirectory(tempDir, dlg.FileName, CompressionLevel.Fastest, false);
                    Notify("Backup exported.");
                }
                catch (Exception ex)
                {
                    Notify("Could not export backup:\n" + ex.Message);
                }
                finally
                {
                    TryDeleteDirectory(tempDir);
                }
            }
        }

        private void ImportData()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Restore HYROLauncher backup";
                dlg.Filter = "HYROLauncher backup (*.zip)|*.zip";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string tempDir = Path.Combine(Path.GetTempPath(), "HYROLauncherRestore_" + Guid.NewGuid().ToString("N"));
                try
                {
                    ZipFile.ExtractToDirectory(dlg.FileName, tempDir);
                    string importedData = FindFile(tempDir, "launcher-data.json");
                    if (String.IsNullOrWhiteSpace(importedData)) throw new InvalidOperationException("Backup does not contain launcher-data.json.");

                    File.Copy(importedData, dataPath, true);
                    string importedLibrary = FindDirectory(tempDir, "library");
                    string importedPlugins = FindDirectory(tempDir, "plugins");
                    ReplaceLibraryAssets(importedLibrary);
                    ReplacePluginAssets(importedPlugins);

                    state = LoadState();
                    SaveState();
                    HideWebLink();
                    SendState();
                    Notify("Backup restored.");
                }
                catch (Exception ex)
                {
                    Notify("Could not restore backup:\n" + ex.Message);
                }
                finally
                {
                    TryDeleteDirectory(tempDir);
                }
            }
        }

        private void ValidatePin(Dictionary<string, object> msg)
        {
            string pin = GetString(msg, "pin");
            Post(new Dictionary<string, object>
            {
                { "type", "pinResult" },
                { "requestId", GetString(msg, "requestId") },
                { "gameId", GetString(msg, "gameId") },
                { "ok", IsValidPin(pin) }
            });
        }

        private void ChangePin(Dictionary<string, object> msg)
        {
            string current = GetString(msg, "currentPin");
            string next = GetString(msg, "newPin");
            if (!IsValidPin(current))
            {
                Post(new Dictionary<string, object> { { "type", "pinChangeResult" }, { "ok", false }, { "message", "Current PIN is not correct." } });
                return;
            }
            if (String.IsNullOrWhiteSpace(next))
            {
                Post(new Dictionary<string, object> { { "type", "pinChangeResult" }, { "ok", false }, { "message", "New PIN cannot be empty." } });
                return;
            }
            state.Settings.PrivatePinHash = HashPin(next);
            SaveState();
            Post(new Dictionary<string, object> { { "type", "pinChangeResult" }, { "ok", true }, { "message", "Private PIN updated." } });
        }

        private void SaveGame(Dictionary<string, object> msg)
        {
            Dictionary<string, object> gameObj = msg["game"] as Dictionary<string, object>;
            if (gameObj == null) return;

            GameEntry game = new GameEntry();
            game.Id = GetString(gameObj, "Id");
            if (String.IsNullOrWhiteSpace(game.Id)) game.Id = Guid.NewGuid().ToString("N");
            game.Name = GetString(gameObj, "Name");
            game.IgdbId = ClampInt(GetString(gameObj, "IgdbId"), 0, 0, Int32.MaxValue);
            game.Path = GetString(gameObj, "Path");
            game.Arguments = GetString(gameObj, "Arguments");
            game.WorkingDirectory = GetString(gameObj, "WorkingDirectory");
            game.FeedUrl = GetString(gameObj, "FeedUrl");
            game.Category = String.IsNullOrWhiteSpace(GetString(gameObj, "Category")) ? "Games" : GetString(gameObj, "Category");
            game.IsUrl = GetBool(gameObj, "IsUrl") || IsWebViewTarget(game.Path);
            game.Favorite = GetBool(gameObj, "Favorite");
            game.Private = GetBool(gameObj, "Private");
            game.HeroX = ClampInt(GetString(gameObj, "HeroX"), 0, 0, 100);
            game.HeroY = ClampInt(GetString(gameObj, "HeroY"), 0, 0, 100);
            game.HeroSize = ClampInt(GetString(gameObj, "HeroSize"), 0, 0, 200);
            game.GameAccentColor = NormalizeHexColor(GetString(gameObj, "GameAccentColor"));
            GameEntry existing = state.Games.Find(g => g.Id == game.Id);
            int sidebarOrder;
            game.SidebarOrder = Int32.TryParse(GetString(gameObj, "SidebarOrder"), out sidebarOrder) && sidebarOrder > 0 ? sidebarOrder : (existing == null ? NextSidebarOrder() : existing.SidebarOrder);
            game.IconPath = StoreGameAsset(GetString(gameObj, "IconPath"), game.Id, "icon", existing == null ? "" : existing.IconPath);
            game.BannerPath = StoreGameAsset(GetString(gameObj, "BannerPath"), game.Id, "banner", existing == null ? "" : existing.BannerPath);
            game.HeroPath = StoreGameAsset(GetString(gameObj, "HeroPath"), game.Id, "hero", existing == null ? "" : existing.HeroPath);
            game.BgmPath = StoreGameAsset(GetString(gameObj, "BgmPath"), game.Id, "bgm", existing == null ? "" : existing.BgmPath);
            if (String.IsNullOrWhiteSpace(game.IconPath) && !game.IsUrl) game.IconPath = ExtractExeIcon(game.Path, game.Id);

            int index = state.Games.FindIndex(g => g.Id == game.Id);
            if (index >= 0) state.Games[index] = game;
            else state.Games.Add(game);

            TcgMissionSourcesChanged();
            SaveState();
            SendState();
            SendPluginState();
        }

        private void SaveSidebarOrder(Dictionary<string, object> msg)
        {
            object idsObj;
            if (!msg.TryGetValue("ids", out idsObj) || idsObj is string) return;
            IEnumerable ids = idsObj as IEnumerable;
            if (ids == null) return;

            int order = 1;
            foreach (object idObj in ids)
            {
                string id = Convert.ToString(idObj);
                GameEntry game = state.Games.Find(g => g.Id == id);
                if (game == null) continue;
                game.SidebarOrder = order++;
            }

            SaveState();
            SendState();
        }

        private void ScanGames()
        {
            if (isScanningGames) return;
            isScanningGames = true;
            Post(new Dictionary<string, object> { { "type", "scanStarted" } });

            ThreadPool.QueueUserWorkItem(delegate
            {
                List<GameEntry> discovered = DiscoverInstalledGames();
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        List<GameEntry> candidates = FilterNewDiscoveredGames(discovered);
                        isScanningGames = false;
                        Post(new Dictionary<string, object>
                        {
                            { "type", "scanResult" },
                            { "found", discovered.Count },
                            { "candidates", candidates }
                        });
                    }));
                }
                catch
                {
                    isScanningGames = false;
                }
            });
        }

        private void ImportScannedGames(Dictionary<string, object> msg)
        {
            object gamesObj;
            if (!msg.TryGetValue("games", out gamesObj) || gamesObj is string) return;
            IEnumerable gameItems = gamesObj as IEnumerable;
            if (gameItems == null) return;

            List<GameEntry> games = new List<GameEntry>();
            foreach (object item in gameItems)
            {
                Dictionary<string, object> gameObj = item as Dictionary<string, object>;
                if (gameObj == null) continue;
                GameEntry game = new GameEntry();
                game.Name = GetString(gameObj, "Name");
                game.Path = GetString(gameObj, "Path");
                game.WorkingDirectory = GetString(gameObj, "WorkingDirectory");
                game.Category = String.IsNullOrWhiteSpace(GetString(gameObj, "Category")) ? "Games" : GetString(gameObj, "Category");
                game.Arguments = "";
                game.IconPath = "";
                game.BannerPath = "";
                game.HeroPath = "";
                game.BgmPath = "";
                games.Add(game);
            }

            int added = AddDiscoveredGames(games);
            if (added > 0) SaveState();
            SendState();
            Post(new Dictionary<string, object>
            {
                { "type", "importResult" },
                { "added", added }
            });
        }

        private List<GameEntry> FilterNewDiscoveredGames(List<GameEntry> discovered)
        {
            List<GameEntry> candidates = new List<GameEntry>();
            if (discovered == null || discovered.Count == 0) return candidates;

            HashSet<string> existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GameEntry game in state.Games)
            {
                string path = NormalizeExecutablePath(game.Path);
                if (!String.IsNullOrWhiteSpace(path)) existingPaths.Add(path);
            }

            foreach (GameEntry game in discovered)
            {
                string path = NormalizeExecutablePath(game.Path);
                if (String.IsNullOrWhiteSpace(path) || existingPaths.Contains(path)) continue;
                candidates.Add(game);
                existingPaths.Add(path);
            }
            return candidates;
        }

        private int AddDiscoveredGames(List<GameEntry> discovered)
        {
            if (discovered == null || discovered.Count == 0) return 0;
            HashSet<string> existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GameEntry game in state.Games)
            {
                string path = NormalizeExecutablePath(game.Path);
                if (!String.IsNullOrWhiteSpace(path)) existingPaths.Add(path);
            }

            int added = 0;
            foreach (GameEntry game in discovered)
            {
                string path = NormalizeExecutablePath(game.Path);
                if (String.IsNullOrWhiteSpace(path) || existingPaths.Contains(path)) continue;
                game.Id = Guid.NewGuid().ToString("N");
                game.Category = String.IsNullOrWhiteSpace(game.Category) ? "Games" : game.Category;
                game.IsUrl = false;
                game.Favorite = false;
                game.Private = false;
                game.SidebarOrder = NextSidebarOrder();
                game.IconPath = ExtractExeIcon(game.Path, game.Id);
                state.Games.Add(game);
                existingPaths.Add(path);
                added++;
            }
            return added;
        }

        private static List<GameEntry> DiscoverInstalledGames()
        {
            List<GameEntry> games = new List<GameEntry>();
            HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DiscoverSteamGames(games, seenPaths);
            DiscoverEpicGames(games, seenPaths);
            DiscoverCommonFolderGames(games, seenPaths);
            return games;
        }

        private static void DiscoverSteamGames(List<GameEntry> games, HashSet<string> seenPaths)
        {
            foreach (string steamApps in GetSteamAppsFolders())
            {
                try
                {
                    string commonDir = Path.Combine(steamApps, "common");
                    if (!Directory.Exists(commonDir)) continue;
                    foreach (string manifest in Directory.GetFiles(steamApps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
                    {
                        string text = File.ReadAllText(manifest);
                        string name = ExtractVdfValue(text, "name");
                        string installDir = ExtractVdfValue(text, "installdir");
                        if (String.IsNullOrWhiteSpace(installDir)) continue;
                        string gameDir = Path.Combine(commonDir, installDir);
                        string exe = FindBestExecutable(gameDir, String.IsNullOrWhiteSpace(name) ? installDir : name);
                        AddDiscoveredGame(games, seenPaths, name, exe, gameDir, "Games");
                    }
                }
                catch { }
            }
        }

        private static List<string> GetSteamAppsFolders()
        {
            List<string> folders = new List<string>();
            AddSteamAppsFolder(folders, Path.Combine(GetRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"), "steamapps"));
            AddSteamAppsFolder(folders, Path.Combine(GetRegistryString(Registry.LocalMachine, @"Software\Valve\Steam", "InstallPath"), "steamapps"));
            AddSteamAppsFolder(folders, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps"));
            AddSteamAppsFolder(folders, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps"));

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    AddSteamAppsFolder(folders, Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps"));
                }
                catch { }
            }

            List<string> allFolders = new List<string>(folders);
            foreach (string steamApps in folders)
            {
                foreach (string library in ReadSteamLibraryFolders(steamApps)) AddSteamAppsFolder(allFolders, library);
            }
            return allFolders;
        }

        private static IEnumerable<string> ReadSteamLibraryFolders(string steamApps)
        {
            List<string> folders = new List<string>();
            try
            {
                string vdf = Path.Combine(steamApps, "libraryfolders.vdf");
                if (!File.Exists(vdf)) return folders;
                string text = File.ReadAllText(vdf);
                foreach (Match match in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
                {
                    string path = match.Groups[1].Value.Replace(@"\\", @"\");
                    folders.Add(Path.Combine(path, "steamapps"));
                }
                foreach (Match match in Regex.Matches(text, "\"\\d+\"\\s+\"([A-Za-z]:[^\"\r\n]+)\"", RegexOptions.IgnoreCase))
                {
                    string path = match.Groups[1].Value.Replace(@"\\", @"\");
                    folders.Add(Path.Combine(path, "steamapps"));
                }
            }
            catch { }
            return folders;
        }

        private static void AddSteamAppsFolder(List<string> folders, string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
                string full = Path.GetFullPath(path);
                foreach (string existing in folders)
                {
                    if (String.Equals(existing, full, StringComparison.OrdinalIgnoreCase)) return;
                }
                folders.Add(full);
            }
            catch { }
        }

        private static void DiscoverEpicGames(List<GameEntry> games, HashSet<string> seenPaths)
        {
            try
            {
                string manifestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (!Directory.Exists(manifestDir)) return;
                JavaScriptSerializer json = new JavaScriptSerializer();
                foreach (string manifest in Directory.GetFiles(manifestDir, "*.item", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        Dictionary<string, object> obj = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifest));
                        string name = GetString(obj, "DisplayName");
                        string install = GetString(obj, "InstallLocation");
                        string launch = GetString(obj, "LaunchExecutable");
                        string exe = ResolveLaunchExecutable(install, launch);
                        if (String.IsNullOrWhiteSpace(exe)) exe = FindBestExecutable(install, name);
                        AddDiscoveredGame(games, seenPaths, name, exe, install, "Games");
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void DiscoverCommonFolderGames(List<GameEntry> games, HashSet<string> seenPaths)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    string root = drive.RootDirectory.FullName;
                    string[] parents = new string[]
                    {
                        Path.Combine(root, "Games"),
                        Path.Combine(root, "Game"),
                        Path.Combine(root, "Epic Games"),
                        Path.Combine(root, "GOG Games"),
                        Path.Combine(root, "Riot Games"),
                        Path.Combine(root, "Battle.net"),
                        Path.Combine(root, "XboxGames"),
                        Path.Combine(root, "SteamLibrary", "steamapps", "common")
                    };
                    foreach (string parent in parents) DiscoverGameFolders(parent, games, seenPaths);
                }
                catch { }
            }
        }

        private static void DiscoverGameFolders(string parent, List<GameEntry> games, HashSet<string> seenPaths)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) return;
                foreach (string dir in Directory.GetDirectories(parent))
                {
                    string name = Path.GetFileName(dir);
                    string exe = FindBestExecutable(dir, name);
                    AddDiscoveredGame(games, seenPaths, name, exe, dir, "Games");
                }
            }
            catch { }
        }

        private static void AddDiscoveredGame(List<GameEntry> games, HashSet<string> seenPaths, string name, string exePath, string workingDir, string category)
        {
            string normalized = NormalizeExecutablePath(exePath);
            if (String.IsNullOrWhiteSpace(normalized) || seenPaths.Contains(normalized)) return;
            seenPaths.Add(normalized);
            GameEntry game = new GameEntry();
            game.Name = String.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(normalized) : name.Trim();
            game.Path = normalized;
            game.Arguments = "";
            game.WorkingDirectory = Directory.Exists(workingDir) ? workingDir : Path.GetDirectoryName(normalized);
            game.IconPath = "";
            game.BannerPath = "";
            game.HeroPath = "";
            game.BgmPath = "";
            game.Category = category;
            game.IsUrl = false;
            game.Favorite = false;
            game.Private = false;
            games.Add(game);
        }

        private void AddCategory(string name)
        {
            name = (name ?? "").Trim();
            if (String.IsNullOrWhiteSpace(name)) return;
            if (state.Categories == null) state.Categories = new List<string>();
            if (!state.Categories.Exists(c => String.Equals(c, name, StringComparison.OrdinalIgnoreCase))) state.Categories.Add(name);
            SaveState();
            SendState();
        }

        private void DeleteGame(string id)
        {
            state.Games.RemoveAll(g => g.Id == id);
            DeleteGameAssetDirectory(id);
            SaveState();
            SendState();
        }

        private void SavePlugin(Dictionary<string, object> msg, bool fromPluginWindow)
        {
            Dictionary<string, object> pluginObj = msg["plugin"] as Dictionary<string, object>;
            if (pluginObj == null) return;

            PluginEntry plugin = new PluginEntry();
            plugin.Id = GetString(pluginObj, "Id");
            if (String.IsNullOrWhiteSpace(plugin.Id)) plugin.Id = Guid.NewGuid().ToString("N");
            PluginEntry existing = state.Plugins.Find(p => p.Id == plugin.Id);
            plugin.Name = String.IsNullOrWhiteSpace(GetString(pluginObj, "Name")) ? "Plugin" : GetString(pluginObj, "Name");
            plugin.Type = NormalizePluginType(GetString(pluginObj, "Type"));
            plugin.Target = GetString(pluginObj, "Target");
            plugin.Pinned = GetBool(pluginObj, "Pinned");
            plugin.BuiltIn = existing != null && existing.BuiltIn;
            plugin.PetEnabled = GetBool(pluginObj, "PetEnabled");
            plugin.PetSize = ClampInt(GetString(pluginObj, "PetSize"), 78, 44, 300);
            plugin.PetSpeed = ClampInt(GetString(pluginObj, "PetSpeed"), 45, 10, 100);
            plugin.PetStaySeconds = ClampInt(GetString(pluginObj, "PetStaySeconds"), 60, 10, 180);
            plugin.PetFrameDelayMs = PetAnimationTiming.Normalize(ClampInt(GetString(pluginObj,"PetFrameDelayMs"),0,0,2000));
            plugin.PetIdleFrameDelayMs = PetAnimationTiming.Normalize(ClampInt(GetString(pluginObj,"PetIdleFrameDelayMs"),plugin.PetIdleFrameDelayMs ?? plugin.PetFrameDelayMs,0,2000));
            plugin.PetDragFrameDelayMs = PetAnimationTiming.Normalize(ClampInt(GetString(pluginObj,"PetDragFrameDelayMs"),plugin.PetDragFrameDelayMs ?? plugin.PetFrameDelayMs,0,2000));
            plugin.PetColor = String.IsNullOrWhiteSpace(NormalizeHexColor(GetString(pluginObj, "PetColor"))) ? "#44A8FF" : NormalizeHexColor(GetString(pluginObj, "PetColor"));
            plugin.ActivePetId = GetString(pluginObj, "ActivePetId");
            plugin.Pets = ReadPetDefinitions(pluginObj, plugin.Id, existing);
            plugin.Mp3Tracks = ReadMp3Tracks(pluginObj, plugin.Id, existing);
            plugin.ActiveTrackId = GetString(pluginObj, "ActiveTrackId");
            string repeat=GetString(pluginObj,"Mp3RepeatMode");
            plugin.Mp3RepeatMode=repeat=="all" || repeat=="one" ? repeat : "off";
            plugin.Mp3Shuffle=GetBool(pluginObj,"Mp3Shuffle");
            plugin.Mp3Volume=ClampInt(GetString(pluginObj,"Mp3Volume"),existing==null ? state.Settings.BgmVolume : existing.Mp3Volume ?? state.Settings.BgmVolume,0,100);
            plugin.MovieWebsiteGameIds = ReadStringList(pluginObj, "MovieWebsiteGameIds");
            plugin.WatchItems = ReadMovieWatchItems(pluginObj, plugin.Id, existing);
            if (fromPluginWindow && existing != null && String.Equals(plugin.Type, "movie-watchlist", StringComparison.OrdinalIgnoreCase))
            {
                PreservePrivateMovieData(existing, plugin);
            }
            if (String.Equals(plugin.Id, BuiltInPetPluginId, StringComparison.OrdinalIgnoreCase))
            {
                plugin.Name = "PET";
                plugin.Type = "pet";
                if (String.IsNullOrWhiteSpace(plugin.IconPath)) plugin.IconPath = Path.Combine(AssetsDir, "icons", "pet.svg");
                EnsureDefaultPet(plugin);
            }
            if (plugin.Pets.Count > 0 && !plugin.Pets.Exists(p => p.Id == plugin.ActivePetId)) plugin.ActivePetId = plugin.Pets[0].Id;
            if (plugin.Mp3Tracks.Count > 0 && !plugin.Mp3Tracks.Exists(t => t.Id == plugin.ActiveTrackId)) plugin.ActiveTrackId = plugin.Mp3Tracks[0].Id;
            int order;
            plugin.PluginOrder = Int32.TryParse(GetString(pluginObj, "PluginOrder"), out order) && order > 0 ? order : (existing == null ? NextPluginOrder() : existing.PluginOrder);
            plugin.IconPath = StorePluginAsset(GetString(pluginObj, "IconPath"), plugin.Id, existing == null ? "" : existing.IconPath);
            DeleteRemovedPetAssets(existing, plugin);
            DeleteRemovedMp3Assets(existing, plugin);
            DeleteRemovedMoviePosterAssets(existing, plugin);

            int index = state.Plugins.FindIndex(p => p.Id == plugin.Id);
            if (index >= 0) state.Plugins[index] = plugin;
            else state.Plugins.Add(plugin);

            SaveState();
            SendState();
            SendPluginState();
        }

        private void PreservePrivateMovieData(PluginEntry existing, PluginEntry updated)
        {
            if (existing == null || updated == null) return;
            if (updated.MovieWebsiteGameIds == null) updated.MovieWebsiteGameIds = new List<string>();
            if (updated.WatchItems == null) updated.WatchItems = new List<MovieWatchItem>();

            HashSet<string> privateGameIds = GetPrivateGameIds(state == null ? null : state.Games);
            updated.MovieWebsiteGameIds.RemoveAll(id => privateGameIds.Contains(id));
            updated.WatchItems.RemoveAll(item => IsPrivateMovieWatchItem(item, privateGameIds));

            if (existing.MovieWebsiteGameIds != null)
            {
                foreach (string id in existing.MovieWebsiteGameIds)
                {
                    if (privateGameIds.Contains(id) && !updated.MovieWebsiteGameIds.Contains(id)) updated.MovieWebsiteGameIds.Add(id);
                }
            }
            if (existing.WatchItems != null)
            {
                foreach (MovieWatchItem item in existing.WatchItems)
                {
                    if (!IsPrivateMovieWatchItem(item, privateGameIds)) continue;
                    if (!updated.WatchItems.Exists(candidate => candidate != null && candidate.Id == item.Id)) updated.WatchItems.Add(item);
                }
            }
        }

        private void DeletePlugin(string id)
        {
            state.Plugins.RemoveAll(p => p.Id == id);
            DeletePluginAssetDirectory(id);
            SaveState();
            SendState();
        }

        private void SaveSettings(Dictionary<string, object> msg)
        {
            Dictionary<string, object> settingsObj = msg["settings"] as Dictionary<string, object>;
            if (settingsObj == null) return;
            string steamInput = GetString(settingsObj, "SteamId").Trim();
            string steamId = SteamLibraryService.NormalizeId(steamInput);
            if (steamInput != "" && steamId == "") { Notify("Steam ID64 must contain 17 digits (7656119...)."); SendState(); return; }
            if (!SaveLibraryBackground(settingsObj)) return;
            state.Settings.SteamId = steamId;
            state.Settings.SteamFavorites = ReadStringList(settingsObj, "SteamFavorites");
            state.Settings.Title = String.IsNullOrWhiteSpace(GetString(settingsObj, "Title")) ? "HYROLauncher" : GetString(settingsObj, "Title");
            state.Settings.AccentColor = String.IsNullOrWhiteSpace(NormalizeHexColor(GetString(settingsObj, "AccentColor"))) ? "#44A8FF" : NormalizeHexColor(GetString(settingsObj, "AccentColor"));
            state.Settings.LibraryLayout = NormalizeLibraryLayout(GetString(settingsObj, "LibraryLayout"));
            if(settingsObj.ContainsKey("BookshelfAnimationsEnabled"))state.Settings.BookshelfAnimationsEnabled=GetBool(settingsObj,"BookshelfAnimationsEnabled");
            if(settingsObj.ContainsKey("BookshelfReactiveLighting"))state.Settings.BookshelfReactiveLighting=GetBool(settingsObj,"BookshelfReactiveLighting");
            state.Settings.BookshelfPullDuration=ClampInt(GetString(settingsObj,"BookshelfPullDuration"),state.Settings.BookshelfPullDuration,100,5000);
            state.Settings.BookshelfHoverDuration=ClampInt(GetString(settingsObj,"BookshelfHoverDuration"),state.Settings.BookshelfHoverDuration,50,500);
            state.Settings.BookshelfPullDistance=ClampInt(GetString(settingsObj,"BookshelfPullDistance"),state.Settings.BookshelfPullDistance,10,100);
            state.Settings.BookshelfHoverLift=ClampInt(GetString(settingsObj,"BookshelfHoverLift"),state.Settings.BookshelfHoverLift,0,15);
            state.Settings.BookshelfDefaultAngle=ClampInt(GetString(settingsObj,"BookshelfDefaultAngle"),state.Settings.BookshelfDefaultAngle,0,40);
            state.Settings.BookshelfSelectedAngle=ClampInt(GetString(settingsObj,"BookshelfSelectedAngle"),state.Settings.BookshelfSelectedAngle,0,15);
            state.Settings.BookshelfLightingIntensity=ClampInt(GetString(settingsObj,"BookshelfLightingIntensity"),state.Settings.BookshelfLightingIntensity,0,100);
            state.Settings.CarouselDisplay=GetString(settingsObj,"CarouselDisplay")=="all"?"all":"scroll";
            state.Settings.CarouselColumns=ClampInt(GetString(settingsObj,"CarouselColumns"),5,1,10);
            state.Settings.ControllerGlyphs=GetString(settingsObj,"ControllerGlyphs")=="playstation"?"playstation":"xbox";
            state.Settings.ThemeMode = NormalizeThemeMode(GetString(settingsObj, "ThemeMode"));
            state.Settings.CardStyle = NormalizeCardStyle(GetString(settingsObj, "CardStyle"));
            state.Settings.MaxGridColumns = NormalizeMaxGridColumns(GetString(settingsObj, "MaxGridColumns"));
            state.Settings.SidebarPosition = NormalizeSidebarPosition(GetString(settingsObj, "SidebarPosition"));
            state.Settings.Language = String.IsNullOrWhiteSpace(GetString(settingsObj, "Language")) ? "en" : GetString(settingsObj, "Language");
            state.Settings.DisableBgm = GetBool(settingsObj, "DisableBgm");
            state.Settings.DisableEffects = GetBool(settingsObj, "DisableEffects");
            state.Settings.StartupWithWindows = GetBool(settingsObj, "StartupWithWindows");
            state.Settings.PluginToolbarCollapsed = GetBool(settingsObj, "PluginToolbarCollapsed");
            int volume;
            state.Settings.BgmVolume = Int32.TryParse(GetString(settingsObj, "BgmVolume"), out volume) ? Math.Max(0, Math.Min(100, volume)) : 35;
            ApplyStartupSetting(state.Settings.StartupWithWindows);
            Color accent = CurrentAccentColor();
            if (pluginWindow != null && !pluginWindow.IsDisposed) pluginWindow.SetAccent(accent);
            if (musicWindow != null && !musicWindow.IsDisposed) musicWindow.SetAccent(accent);
            if (hyrowserWindow != null && !hyrowserWindow.IsDisposed) hyrowserWindow.SetAccent(accent);
            ApplyWindowVisuals(state.Settings.ThemeMode);
            SaveState();
            SendState();
            SendPluginState();
        }

        private void ApplyWindowVisuals(string themeMode)
        {
            bool translucent = String.Equals(NormalizeThemeMode(themeMode), "translucent", StringComparison.OrdinalIgnoreCase);
            Color keyColor = Color.FromArgb(1, 2, 3);
            Color transparentBack = Color.Transparent;
            Color normalBack = Color.FromArgb(8, 10, 18);
            BackColor = translucent ? keyColor : normalBack;
            TransparencyKey = translucent ? keyColor : Color.Empty;
            try
            {
                if (webView != null) webView.DefaultBackgroundColor = translucent ? transparentBack : normalBack;
                if (linkView != null) linkView.DefaultBackgroundColor = Color.White;
                if (feedView != null) feedView.DefaultBackgroundColor = Color.White;
            }
            catch { }
            if (pluginWindow != null && !pluginWindow.IsDisposed) pluginWindow.ApplyWindowVisuals(themeMode);
            if (musicWindow != null && !musicWindow.IsDisposed) musicWindow.ApplyWindowVisuals(themeMode);
            ApplyRoundedWindowRegion();
        }

        private void ChooseFile(string kind, string gameId, bool replyToPluginWindow,PluginBrowserWindow replyWindow=null)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                if (kind == "exe") dlg.Filter = "Programs (*.exe;*.bat;*.cmd;*.lnk)|*.exe;*.bat;*.cmd;*.lnk|All files (*.*)|*.*";
                else if (kind == "icon") dlg.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico|All files (*.*)|*.*";
                else if (kind == "banner") dlg.Filter = "Images and GIF (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*";
                else if (kind == "libraryBackground") dlg.Filter = "Images and GIF (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif";
                else if (kind == "hero") dlg.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*";
                else if (kind == "bgm") dlg.Filter = "Audio (*.mp3;*.wav;*.wma)|*.mp3;*.wav;*.wma|All files (*.*)|*.*";
                else if (kind == "pluginIcon") dlg.Filter = "Images (*.svg;*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.svg;*.png;*.jpg;*.jpeg;*.bmp;*.ico|All files (*.*)|*.*";
                else if (kind == "petIdleGif" || kind == "petDragGif" || kind == "petWalkGif") dlg.Filter = "Animated GIF (*.gif)|*.gif|All files (*.*)|*.*";
                else if (kind == "mp3Track") dlg.Filter = "Audio (*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac)|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac|All files (*.*)|*.*";
                else if (kind == "moviePoster") dlg.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*";
                else if (kind == "pluginFile") dlg.Filter = "Plugin files (*.exe;*.bat;*.cmd;*.lnk)|*.exe;*.bat;*.cmd;*.lnk|All files (*.*)|*.*";

                dlg.Multiselect = kind == "mp3Track";
                if (dlg.ShowDialog(replyWindow!=null?(IWin32Window)replyWindow:this) != DialogResult.OK) return;
                string path = dlg.FileName;
                Dictionary<string, object> payload = new Dictionary<string, object> { { "type", "fileChosen" }, { "kind", kind }, { "path", path } };
                if(kind=="mp3Track") payload["paths"]=dlg.FileNames;
                PluginBrowserWindow targetWindow=replyWindow ?? pluginWindow;
                if (replyToPluginWindow && targetWindow != null && !targetWindow.IsDisposed) targetWindow.Post(payload);
                else Post(payload);
            }
        }

        private void Launch(string id)
        {
            GameEntry game = state.Games.Find(g => g.Id == id);
            if (game == null) return;
            try
            {
                if (!game.IsUrl && IsGameRunning(game, GetRunningExecutablePaths(), launchedProcesses))
                {
                    TcgGamePlayed(game.Id);
                    SendRunningState();
                    return;
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = game.Path;
                psi.Arguments = game.Arguments ?? "";
                psi.UseShellExecute = true;
                if (!String.IsNullOrWhiteSpace(game.WorkingDirectory)) psi.WorkingDirectory = game.WorkingDirectory;
                Process process = Process.Start(psi);
                TcgGamePlayed(game.Id);
                TrackLaunchedProcess(game.Id, process);
                SendRunningState();
                WindowState = FormWindowState.Minimized;
            }
            catch (Exception ex)
            {
                Notify("Could not launch:\n" + ex.Message);
            }
        }

        private void LaunchPlugin(string id)
        {
            PluginEntry plugin = state.Plugins.Find(p => p.Id == id);
            if (plugin == null || String.IsNullOrWhiteSpace(plugin.Target)) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = plugin.Target;
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Notify("Could not open plugin:\n" + ex.Message);
            }
        }

        private void OpenExternal(string target)
        {
            if (String.IsNullOrWhiteSpace(target)) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = target;
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch { }
        }

        private LauncherState LoadState()
        {
            try
            {
                if (File.Exists(dataPath))
                {
                    LauncherState loaded = serializer.Deserialize<LauncherState>(File.ReadAllText(dataPath));
                    return Normalize(loaded);
                }
            }
            catch { }
            return Normalize(new LauncherState());
        }

        private void SaveState()
        {
            Normalize(state);
            File.WriteAllText(dataPath, serializer.Serialize(MakePortableState(state)));
            TcgMissionSourcesChanged();
        }

        private LauncherState Normalize(LauncherState input)
        {
            if (input == null) input = new LauncherState();
            if (input.Games == null) input.Games = new List<GameEntry>();
            if (input.Plugins == null) input.Plugins = new List<PluginEntry>();
            if (input.Categories == null) input.Categories = new List<string>();
            if (!input.Categories.Exists(c => String.Equals(c, "Games", StringComparison.OrdinalIgnoreCase))) input.Categories.Add("Games");
            if (input.Settings == null) input.Settings = new LauncherSettings();
            input.Settings.SteamId = SteamLibraryService.NormalizeId(input.Settings.SteamId);
            if (input.Settings.SteamFavorites == null) input.Settings.SteamFavorites = new List<string>();
            if (String.IsNullOrWhiteSpace(input.Settings.Title)) input.Settings.Title = "HYROLauncher";
            input.Settings.AccentColor = String.IsNullOrWhiteSpace(NormalizeHexColor(input.Settings.AccentColor)) ? "#44A8FF" : NormalizeHexColor(input.Settings.AccentColor);
            input.Settings.LibraryLayout = NormalizeLibraryLayout(input.Settings.LibraryLayout);
            input.Settings.BookshelfPullDuration=Math.Max(100,Math.Min(5000,input.Settings.BookshelfPullDuration));
            input.Settings.BookshelfHoverDuration=Math.Max(50,Math.Min(500,input.Settings.BookshelfHoverDuration));
            input.Settings.BookshelfPullDistance=Math.Max(10,Math.Min(100,input.Settings.BookshelfPullDistance));
            input.Settings.BookshelfHoverLift=Math.Max(0,Math.Min(15,input.Settings.BookshelfHoverLift));
            input.Settings.BookshelfDefaultAngle=Math.Max(0,Math.Min(40,input.Settings.BookshelfDefaultAngle));
            input.Settings.BookshelfSelectedAngle=Math.Max(0,Math.Min(15,input.Settings.BookshelfSelectedAngle));
            input.Settings.BookshelfLightingIntensity=Math.Max(0,Math.Min(100,input.Settings.BookshelfLightingIntensity));
            input.Settings.CarouselDisplay=input.Settings.CarouselDisplay=="all"?"all":"scroll";
            input.Settings.CarouselColumns=input.Settings.CarouselColumns>0?ClampInt(input.Settings.CarouselColumns.ToString(),5,1,10):5;
            input.Settings.ControllerGlyphs=input.Settings.ControllerGlyphs=="playstation"?"playstation":"xbox";
            input.Settings.ThemeMode = NormalizeThemeMode(input.Settings.ThemeMode);
            input.Settings.LibraryBackground = ResolveStoredAssetPath(input.Settings.LibraryBackground);
            input.Settings.LibraryBackgroundDim = Math.Max(0, Math.Min(100, input.Settings.LibraryBackgroundDim));
            input.Settings.CardStyle = NormalizeCardStyle(input.Settings.CardStyle);
            input.Settings.MaxGridColumns = NormalizeMaxGridColumns(input.Settings.MaxGridColumns.ToString());
            input.Settings.SidebarPosition = NormalizeSidebarPosition(input.Settings.SidebarPosition);
            if (String.IsNullOrWhiteSpace(input.Settings.Language)) input.Settings.Language = "en";
            if (input.Settings.BgmVolume < 0 || input.Settings.BgmVolume > 100) input.Settings.BgmVolume = 35;
            if (String.IsNullOrWhiteSpace(input.Settings.PrivatePinHash)) input.Settings.PrivatePinHash = DefaultPrivatePinHash;
            EnsureBuiltInPluginsFromPacks(input);
            for (int i = 0; i < input.Games.Count; i++)
            {
                input.Games[i].IconPath = ResolveStoredAssetPath(input.Games[i].IconPath);
                input.Games[i].BannerPath = ResolveStoredAssetPath(input.Games[i].BannerPath);
                input.Games[i].HeroPath = ResolveStoredAssetPath(input.Games[i].HeroPath);
                input.Games[i].BgmPath = ResolveStoredAssetPath(input.Games[i].BgmPath);
                input.Games[i].IsUrl = input.Games[i].IsUrl || IsWebViewTarget(input.Games[i].Path);
                input.Games[i].HeroX = Math.Max(0, Math.Min(100, input.Games[i].HeroX));
                input.Games[i].HeroY = Math.Max(0, Math.Min(100, input.Games[i].HeroY));
                input.Games[i].HeroSize = Math.Max(0, Math.Min(200, input.Games[i].HeroSize));
                input.Games[i].GameAccentColor = NormalizeHexColor(input.Games[i].GameAccentColor);
                if (input.Games[i].SidebarOrder <= 0) input.Games[i].SidebarOrder = i + 1;
            }
            for (int i = 0; i < input.Plugins.Count; i++)
            {
                input.Plugins[i].IconPath = ResolveStoredAssetPath(input.Plugins[i].IconPath);
                input.Plugins[i].Type = NormalizePluginType(input.Plugins[i].Type);
                if (String.Equals(input.Plugins[i].Id, BuiltInPetPluginId, StringComparison.OrdinalIgnoreCase))
                {
                    input.Plugins[i].Name = String.IsNullOrWhiteSpace(input.Plugins[i].Name) ? "PET" : input.Plugins[i].Name;
                    input.Plugins[i].Type = "pet";
                    if (String.IsNullOrWhiteSpace(input.Plugins[i].IconPath)) input.Plugins[i].IconPath = Path.Combine(AssetsDir, "icons", "pet.svg");
                    EnsureDefaultPet(input.Plugins[i]);
                }
                if (input.Plugins[i].PetSize <= 0) input.Plugins[i].PetSize = 78;
                if (input.Plugins[i].PetSpeed <= 0) input.Plugins[i].PetSpeed = 45;
                input.Plugins[i].PetSize = Math.Max(44, Math.Min(300, input.Plugins[i].PetSize));
                input.Plugins[i].PetSpeed = Math.Max(10, Math.Min(100, input.Plugins[i].PetSpeed));
                input.Plugins[i].PetStaySeconds = input.Plugins[i].PetStaySeconds <= 0 ? 60 : Math.Max(10, Math.Min(180, input.Plugins[i].PetStaySeconds));
                input.Plugins[i].PetFrameDelayMs = PetAnimationTiming.Normalize(input.Plugins[i].PetFrameDelayMs);
                input.Plugins[i].PetIdleFrameDelayMs = PetAnimationTiming.Normalize(input.Plugins[i].PetIdleFrameDelayMs ?? input.Plugins[i].PetFrameDelayMs);
                input.Plugins[i].PetDragFrameDelayMs = PetAnimationTiming.Normalize(input.Plugins[i].PetDragFrameDelayMs ?? input.Plugins[i].PetFrameDelayMs);
                input.Plugins[i].PetColor = String.IsNullOrWhiteSpace(NormalizeHexColor(input.Plugins[i].PetColor)) ? "#44A8FF" : NormalizeHexColor(input.Plugins[i].PetColor);
                if (input.Plugins[i].Pets == null) input.Plugins[i].Pets = new List<PetDefinition>();
                foreach (PetDefinition pet in input.Plugins[i].Pets)
                {
                    pet.IdleGifPath = ResolveStoredAssetPath(pet.IdleGifPath);
                    pet.DragGifPath = ResolveStoredAssetPath(pet.DragGifPath);
                    pet.WalkGifPath = ResolveStoredAssetPath(pet.WalkGifPath);
                }
                if (input.Plugins[i].Pets.Count > 0 && !input.Plugins[i].Pets.Exists(p => p.Id == input.Plugins[i].ActivePetId)) input.Plugins[i].ActivePetId = input.Plugins[i].Pets[0].Id;
                if (input.Plugins[i].Mp3Tracks == null) input.Plugins[i].Mp3Tracks = new List<Mp3Track>();
                string repeat=input.Plugins[i].Mp3RepeatMode;
                input.Plugins[i].Mp3RepeatMode=repeat=="all" || repeat=="one" ? repeat : "off";
                input.Plugins[i].Mp3Volume=Math.Max(0,Math.Min(100,input.Plugins[i].Mp3Volume ?? input.Settings.BgmVolume));
                foreach (Mp3Track track in input.Plugins[i].Mp3Tracks)
                {
                    track.Path = ResolveStoredAssetPath(track.Path);
                }
                if (input.Plugins[i].Mp3Tracks.Count > 0 && !input.Plugins[i].Mp3Tracks.Exists(t => t.Id == input.Plugins[i].ActiveTrackId)) input.Plugins[i].ActiveTrackId = input.Plugins[i].Mp3Tracks[0].Id;
                if (input.Plugins[i].MovieWebsiteGameIds == null) input.Plugins[i].MovieWebsiteGameIds = new List<string>();
                if (input.Plugins[i].WatchItems == null) input.Plugins[i].WatchItems = new List<MovieWatchItem>();
                foreach (MovieWatchItem item in input.Plugins[i].WatchItems)
                {
                    item.PosterPath = ResolveStoredAssetPath(item.PosterPath);
                    item.Status = NormalizeMovieStatus(item.Status, item.Episode, item.WatchTime);
                    item.TotalEpisodes = Math.Max(0, Math.Min(10000, item.TotalEpisodes));
                }
                if (input.Plugins[i].PluginOrder <= 0) input.Plugins[i].PluginOrder = i + 1;
            }
            return input;
        }

        private void EnsureBuiltInPluginsFromPacks(LauncherState input)
        {
            Dictionary<string, PluginEntry> packs = LoadBuiltInPluginPacks();
            input.Plugins.RemoveAll(p => IsRetiredBuiltInPlugin(p));
            input.Plugins.RemoveAll(p => IsBuiltInPluginId(p.Id) && !packs.ContainsKey(p.Id));

            int maxOrder = 0;
            foreach (PluginEntry plugin in input.Plugins) if (plugin.PluginOrder > maxOrder) maxOrder = plugin.PluginOrder;

            foreach (PluginEntry pack in packs.Values)
            {
                PluginEntry existing = input.Plugins.Find(p => String.Equals(p.Id, pack.Id, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    pack.PluginOrder = ++maxOrder;
                    input.Plugins.Add(pack);
                    existing = pack;
                }
                else
                {
                    existing.BuiltIn = true;
                    existing.Name = String.IsNullOrWhiteSpace(existing.Name) ? pack.Name : existing.Name;
                    existing.Type = pack.Type;
                    existing.IconPath = String.IsNullOrWhiteSpace(existing.IconPath) ? pack.IconPath : existing.IconPath;
                    if (String.IsNullOrWhiteSpace(existing.Target)) existing.Target = pack.Target;
                    if (existing.PluginOrder <= 0) existing.PluginOrder = ++maxOrder;
                    if (existing.PetSize <= 0) existing.PetSize = pack.PetSize;
                    if (existing.PetSpeed <= 0) existing.PetSpeed = pack.PetSpeed;
                    if (existing.PetStaySeconds <= 0) existing.PetStaySeconds = pack.PetStaySeconds;
                    if (String.IsNullOrWhiteSpace(existing.PetColor)) existing.PetColor = pack.PetColor;
                }

                if (String.Equals(existing.Id, BuiltInPetPluginId, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Name = String.IsNullOrWhiteSpace(existing.Name) ? "PET" : existing.Name;
                    existing.Type = "pet";
                    if (String.IsNullOrWhiteSpace(existing.IconPath)) existing.IconPath = Path.Combine(AssetsDir, "icons", "pet.svg");
                    EnsureDefaultPet(existing);
                }
                if (existing.Mp3Tracks == null) existing.Mp3Tracks = new List<Mp3Track>();
                if (existing.MovieWebsiteGameIds == null) existing.MovieWebsiteGameIds = new List<string>();
                if (existing.WatchItems == null) existing.WatchItems = new List<MovieWatchItem>();
            }
        }

        private Dictionary<string, PluginEntry> LoadBuiltInPluginPacks()
        {
            Dictionary<string, PluginEntry> output = new Dictionary<string, PluginEntry>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(BuiltInPluginPacksDir)) return output;
            foreach (string dir in Directory.GetDirectories(BuiltInPluginPacksDir))
            {
                try
                {
                    string manifestPath = Path.Combine(dir, "plugin.json");
                    if (!File.Exists(manifestPath)) continue;
                    Dictionary<string, object> manifest = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath));
                    string pluginId = GetString(manifest, "Id");
                    if (String.IsNullOrWhiteSpace(pluginId)) continue;
                    string iconPath = GetString(manifest, "IconPath");
                    if (!String.IsNullOrWhiteSpace(iconPath) && !Path.IsPathRooted(iconPath)) iconPath = Path.GetFullPath(Path.Combine(dir, iconPath));
                    string target = GetString(manifest, "Target");
                    if (!String.IsNullOrWhiteSpace(target) && !Path.IsPathRooted(target) && !Regex.IsMatch(target, "^[a-zA-Z][a-zA-Z0-9+.-]*:")) target = Path.GetFullPath(Path.Combine(dir, target));
                    output[pluginId] = new PluginEntry
                    {
                        Id = pluginId,
                        Name = String.IsNullOrWhiteSpace(GetString(manifest, "Name")) ? "Plugin" : GetString(manifest, "Name"),
                        Type = NormalizePluginType(GetString(manifest, "Type")),
                        IconPath = iconPath,
                        Target = target,
                        Pinned = GetBool(manifest, "Pinned"),
                        BuiltIn = true,
                        PetEnabled = GetBool(manifest, "PetEnabled"),
                        PetSize = ClampInt(GetString(manifest, "PetSize"), 78, 44, 300),
                        PetSpeed = ClampInt(GetString(manifest, "PetSpeed"), 45, 10, 100),
                        PetStaySeconds = ClampInt(GetString(manifest, "PetStaySeconds"), 60, 10, 180),
                        PetColor = String.IsNullOrWhiteSpace(NormalizeHexColor(GetString(manifest, "PetColor"))) ? "#44A8FF" : NormalizeHexColor(GetString(manifest, "PetColor")),
                        ActivePetId = DefaultPetId,
                        Pets = new List<PetDefinition>(),
                        Mp3Tracks = new List<Mp3Track>(),
                        MovieWebsiteGameIds = new List<string>(),
                        WatchItems = new List<MovieWatchItem>()
                    };
                }
                catch { }
            }
            return output;
        }

        private static bool IsBuiltInPluginId(string id)
        {
            return !String.IsNullOrWhiteSpace(id) && id.StartsWith("builtin.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRetiredBuiltInPlugin(PluginEntry plugin)
        {
            if (plugin == null) return false;
            return String.Equals(plugin.Id, RetiredHyroFtgPluginId, StringComparison.OrdinalIgnoreCase)
                || String.Equals(plugin.Type, "free-games", StringComparison.OrdinalIgnoreCase)
                || String.Equals(plugin.Name, "HyroFTG", StringComparison.OrdinalIgnoreCase);
        }

        private static PetDefinition CreateDefaultPet()
        {
            return new PetDefinition
            {
                Id = DefaultPetId,
                Name = "HYRO Buddy",
                IdleGifPath = DefaultPetIdleGifPath,
                DragGifPath = DefaultPetDragGifPath,
                WalkGifPath = DefaultPetWalkGifPath
            };
        }

        private static void EnsureDefaultPet(PluginEntry plugin)
        {
            if (plugin.Pets == null) plugin.Pets = new List<PetDefinition>();
            PetDefinition defaultPet = plugin.Pets.Find(p => String.Equals(p.Id, DefaultPetId, StringComparison.OrdinalIgnoreCase));
            if (defaultPet == null)
            {
                defaultPet = CreateDefaultPet();
                plugin.Pets.Insert(0, defaultPet);
            }
            else
            {
                defaultPet.Name = "HYRO Buddy";
                defaultPet.IdleGifPath = DefaultPetIdleGifPath;
                defaultPet.DragGifPath = DefaultPetDragGifPath;
                defaultPet.WalkGifPath = DefaultPetWalkGifPath;
            }
            if (String.IsNullOrWhiteSpace(plugin.ActivePetId)) plugin.ActivePetId = DefaultPetId;
            EnsureBundledPet(plugin,"bundled.kafka","Kafka","kafka");
            EnsureBundledPet(plugin,"bundled.kled-skaarl","Kled & Skaarl","kled-skaarl");
        }

        private static void EnsureBundledPet(PluginEntry plugin,string id,string name,string assetName)
        {
            // Seed new companions without changing the user's selection or edits.
            if(plugin.Pets.Exists(p=>String.Equals(p.Id,id,StringComparison.OrdinalIgnoreCase))) return;
            string idle=Path.Combine(AssetsDir,"pets",assetName+"-idle.gif");
            string drag=Path.Combine(AssetsDir,"pets",assetName+"-drag.gif");
            if(!File.Exists(idle)||!File.Exists(drag)) return;
            plugin.Pets.Add(new PetDefinition { Id=id,Name=name,IdleGifPath=idle,DragGifPath=drag,WalkGifPath="" });
        }

        private static LauncherState MakePortableState(LauncherState input)
        {
            LauncherState output = new LauncherState();
            output.Categories = input.Categories == null ? new List<string>() : new List<string>(input.Categories);
            output.Settings = new JavaScriptSerializer().Deserialize<LauncherSettings>(new JavaScriptSerializer().Serialize(input.Settings));
            output.Settings.LibraryBackground = MakePortableAssetPath(input.Settings.LibraryBackground);
            output.Games = new List<GameEntry>();
            output.Plugins = new List<PluginEntry>();

            foreach (GameEntry game in input.Games ?? new List<GameEntry>())
            {
                output.Games.Add(new GameEntry
                {
                    Id = game.Id,
                    Name = game.Name,
                    IgdbId = game.IgdbId,
                    Path = game.Path,
                    Arguments = game.Arguments,
                    WorkingDirectory = game.WorkingDirectory,
                    IconPath = MakePortableAssetPath(game.IconPath),
                    BannerPath = MakePortableAssetPath(game.BannerPath),
                    HeroPath = MakePortableAssetPath(game.HeroPath),
                    HeroX = game.HeroX,
                    HeroY = game.HeroY,
                    HeroSize = game.HeroSize,
                    BgmPath = MakePortableAssetPath(game.BgmPath),
                    FeedUrl = game.FeedUrl,
                    GameAccentColor = game.GameAccentColor,
                    Category = game.Category,
                    IsUrl = game.IsUrl,
                    Favorite = game.Favorite,
                    Private = game.Private,
                    SidebarOrder = game.SidebarOrder
                });
            }
            foreach (PluginEntry plugin in input.Plugins ?? new List<PluginEntry>())
            {
                output.Plugins.Add(new PluginEntry
                {
                    Id = plugin.Id,
                    Name = plugin.Name,
                    Type = plugin.Type,
                    IconPath = MakePortableAssetPath(plugin.IconPath),
                    Target = plugin.Target,
                    Pinned = plugin.Pinned,
                    PluginOrder = plugin.PluginOrder,
                    BuiltIn = plugin.BuiltIn,
                    PetEnabled = plugin.PetEnabled,
                    PetSize = plugin.PetSize,
                    PetSpeed = plugin.PetSpeed,
                    PetStaySeconds = plugin.PetStaySeconds,
                    PetFrameDelayMs = plugin.PetFrameDelayMs,
                    PetIdleFrameDelayMs = plugin.PetIdleFrameDelayMs,
                    PetDragFrameDelayMs = plugin.PetDragFrameDelayMs,
                    PetColor = plugin.PetColor,
                    ActivePetId = plugin.ActivePetId,
                    Pets = (plugin.Pets ?? new List<PetDefinition>()).ConvertAll(delegate(PetDefinition pet)
                    {
                        return new PetDefinition
                        {
                            Id = pet.Id,
                            Name = pet.Name,
                            IdleGifPath = MakePortableAssetPath(pet.IdleGifPath),
                            DragGifPath = MakePortableAssetPath(pet.DragGifPath),
                            WalkGifPath = MakePortableAssetPath(pet.WalkGifPath)
                        };
                    }),
                    Mp3Tracks = (plugin.Mp3Tracks ?? new List<Mp3Track>()).ConvertAll(delegate(Mp3Track track)
                    {
                        return new Mp3Track
                        {
                            Id = track.Id,
                            Title = track.Title,
                            Path = MakePortableAssetPath(track.Path)
                        };
                    }),
                    ActiveTrackId = plugin.ActiveTrackId,
                    Mp3RepeatMode = plugin.Mp3RepeatMode,
                    Mp3Shuffle = plugin.Mp3Shuffle,
                    Mp3Volume = plugin.Mp3Volume,
                    MovieWebsiteGameIds = plugin.MovieWebsiteGameIds == null ? new List<string>() : new List<string>(plugin.MovieWebsiteGameIds),
                    WatchItems = (plugin.WatchItems ?? new List<MovieWatchItem>()).ConvertAll(delegate(MovieWatchItem item)
                    {
                        return new MovieWatchItem
                        {
                            Id = item.Id,
                            Title = item.Title,
                            PosterPath = MakePortableAssetPath(item.PosterPath),
                            Episode = item.Episode,
                            Status = item.Status,
                            TotalEpisodes = item.TotalEpisodes,
                            WatchTime = item.WatchTime,
                            Url = item.Url,
                            SourceGameId = item.SourceGameId,
                            UpdatedAt = item.UpdatedAt
                        };
                    })
                });
            }
            return output;
        }

        private static string ResolveStoredAssetPath(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path)) return "";
                path = path.Trim();
                if (IsRemoteUrl(path)) return path;
                if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
                return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
            }
            catch { return path; }
        }

        private static string MakePortableAssetPath(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path)) return "";
                if (IsRemoteUrl(path)) return path;
                string fullPath = Path.GetFullPath(path);
                string baseDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString())) baseDir += Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)) return path;
                Uri baseUri = new Uri(baseDir);
                Uri pathUri = new Uri(fullPath);
                string relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(pathUri).ToString());
                return relative.Replace('/', Path.DirectorySeparatorChar);
            }
            catch { return path; }
        }

        private static bool IsRemoteUrl(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim();
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("blob:", StringComparison.OrdinalIgnoreCase);
        }

        private int NextSidebarOrder()
        {
            int max = 0;
            foreach (GameEntry game in state.Games)
            {
                if (game.SidebarOrder > max) max = game.SidebarOrder;
            }
            return max + 1;
        }

        private int NextPluginOrder()
        {
            int max = 0;
            foreach (PluginEntry plugin in state.Plugins)
            {
                if (plugin.PluginOrder > max) max = plugin.PluginOrder;
            }
            return max + 1;
        }

        private void Post(Dictionary<string, object> payload)
        {
            if (webView.CoreWebView2 == null) return;
            webView.CoreWebView2.PostWebMessageAsJson(serializer.Serialize(payload));
        }

        private List<string> GetRunningGameIds()
        {
            List<string> ids = new List<string>();
            CleanupLaunchedProcesses();
            if (!state.Games.Exists(g => !g.IsUrl && !String.IsNullOrWhiteSpace(NormalizeExecutablePath(g.Path)))) return ids;
            HashSet<string> runningPaths = GetRunningExecutablePaths();
            foreach (GameEntry game in state.Games)
            {
                if (IsGameRunning(game, runningPaths, launchedProcesses)) ids.Add(game.Id);
            }
            return ids;
        }

        private static bool IsGameRunning(GameEntry game, HashSet<string> runningPaths, Dictionary<string, Process> launched)
        {
            if (game == null || game.IsUrl || runningPaths == null) return false;
            Process process;
            if (launched != null && launched.TryGetValue(game.Id, out process))
            {
                try
                {
                    if (process != null && !process.HasExited) return true;
                }
                catch { }
            }
            string path = NormalizeExecutablePath(game.Path);
            return !String.IsNullOrWhiteSpace(path) && runningPaths.Contains(path);
        }

        private void TrackLaunchedProcess(string gameId, Process process)
        {
            if (String.IsNullOrWhiteSpace(gameId) || process == null) return;
            try
            {
                Process oldProcess;
                if (launchedProcesses.TryGetValue(gameId, out oldProcess))
                {
                    try { oldProcess.Dispose(); } catch { }
                    launchedProcesses.Remove(gameId);
                }

                process.EnableRaisingEvents = true;
                process.Exited += delegate
                {
                    try
                    {
                        BeginInvoke(new Action(delegate
                        {
                            RemoveTrackedProcess(gameId);
                            SendRunningState();
                        }));
                    }
                    catch { }
                };
                launchedProcesses[gameId] = process;
            }
            catch
            {
                try { process.Dispose(); } catch { }
            }
        }

        private void RemoveTrackedProcess(string gameId)
        {
            Process process;
            if (!launchedProcesses.TryGetValue(gameId, out process)) return;
            launchedProcesses.Remove(gameId);
            try { process.Dispose(); } catch { }
        }

        private void CleanupLaunchedProcesses()
        {
            List<string> ended = new List<string>();
            foreach (KeyValuePair<string, Process> pair in launchedProcesses)
            {
                try
                {
                    if (pair.Value == null || pair.Value.HasExited) ended.Add(pair.Key);
                }
                catch { ended.Add(pair.Key); }
            }
            foreach (string id in ended) RemoveTrackedProcess(id);
        }

        private static HashSet<string> GetRunningExecutablePaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.MainModule == null) continue;
                    string path = NormalizeExecutablePath(process.MainModule.FileName);
                    if (!String.IsNullOrWhiteSpace(path)) paths.Add(path);
                }
                catch { }
                finally
                {
                    process.Dispose();
                }
            }
            return paths;
        }

        private static string NormalizeExecutablePath(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path)) return "";
                path = path.Trim().Trim('"');
                if (!String.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) return "";
                return Path.GetFullPath(path);
            }
            catch { return ""; }
        }

        private static string ExtractVdfValue(string text, string key)
        {
            if (String.IsNullOrWhiteSpace(text) || String.IsNullOrWhiteSpace(key)) return "";
            Match match = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "";
        }

        private static string GetRegistryString(RegistryKey root, string path, string name)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(path))
                {
                    if (key == null) return "";
                    object value = key.GetValue(name);
                    return value == null ? "" : Convert.ToString(value);
                }
            }
            catch { return ""; }
        }

        private static string ResolveLaunchExecutable(string installDir, string launchExecutable)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(installDir) || String.IsNullOrWhiteSpace(launchExecutable)) return "";
                string launch = launchExecutable.Trim().Trim('"').Replace('/', '\\').TrimStart('\\');
                string path = Path.IsPathRooted(launch) ? launch : Path.Combine(installDir, launch);
                path = NormalizeExecutablePath(path);
                return File.Exists(path) ? path : "";
            }
            catch { return ""; }
        }

        private static string FindBestExecutable(string folder, string displayName)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return "";
                List<string> candidates = new List<string>();
                CollectExecutableCandidates(folder, folder, 0, 3, candidates);
                string best = "";
                int bestScore = Int32.MinValue;
                foreach (string exe in candidates)
                {
                    int score = ScoreExecutable(folder, exe, displayName);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = exe;
                    }
                }
                return bestScore < -200 ? "" : best;
            }
            catch { return ""; }
        }

        private static void CollectExecutableCandidates(string root, string current, int depth, int maxDepth, List<string> candidates)
        {
            if (candidates.Count >= 120 || depth > maxDepth || IsIgnoredDirectory(current)) return;
            try
            {
                foreach (string exe in Directory.GetFiles(current, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    if (candidates.Count >= 120) return;
                    candidates.Add(exe);
                }
                if (depth == maxDepth) return;
                foreach (string dir in Directory.GetDirectories(current))
                {
                    if (candidates.Count >= 120) return;
                    CollectExecutableCandidates(root, dir, depth + 1, maxDepth, candidates);
                }
            }
            catch { }
        }

        private static bool IsIgnoredDirectory(string path)
        {
            string name = Path.GetFileName(path) ?? "";
            string lower = name.ToLowerInvariant();
            return lower == "_commonredist" || lower == "redist" || lower == "redistributables" ||
                   lower == "directx" || lower == "support" || lower == "extras" || lower == "tools" ||
                   lower == "engine" || lower == "binaries" && path.ToLowerInvariant().Contains("\\engine\\");
        }

        private static int ScoreExecutable(string root, string exe, string displayName)
        {
            string file = Path.GetFileNameWithoutExtension(exe) ?? "";
            string fullLower = exe.ToLowerInvariant();
            string fileLower = file.ToLowerInvariant();
            string nameLower = (displayName ?? "").ToLowerInvariant();
            string rootNameLower = (Path.GetFileName(root) ?? "").ToLowerInvariant();

            int score = 0;
            if (String.Equals(Path.GetDirectoryName(exe), root, StringComparison.OrdinalIgnoreCase)) score += 220;
            if (!String.IsNullOrWhiteSpace(nameLower) && NormalizedContains(fileLower, nameLower)) score += 160;
            if (!String.IsNullOrWhiteSpace(rootNameLower) && NormalizedContains(fileLower, rootNameLower)) score += 140;
            if (fileLower.Contains("launcher")) score += 45;
            if (fullLower.Contains("\\win64\\") || fullLower.Contains("\\x64\\")) score += 25;
            if (fullLower.Contains("\\shipping\\")) score += 20;

            if (fileLower.Contains("crash") || fileLower.Contains("report") || fileLower.Contains("setup") ||
                fileLower.Contains("install") || fileLower.Contains("unins") || fileLower.Contains("uninstall") ||
                fileLower.Contains("redist") || fileLower.Contains("vcredist") || fileLower.Contains("dxsetup") ||
                fileLower.Contains("server") || fileLower.Contains("editor") || fileLower.Contains("benchmark"))
            {
                score -= 500;
            }

            try
            {
                long mb = new FileInfo(exe).Length / (1024 * 1024);
                score += (int)Math.Min(90, mb);
            }
            catch { }
            return score;
        }

        private static bool NormalizedContains(string value, string query)
        {
            value = NormalizeName(value);
            query = NormalizeName(query);
            return !String.IsNullOrWhiteSpace(value) && !String.IsNullOrWhiteSpace(query) &&
                   (value.Contains(query) || query.Contains(value));
        }

        private static string NormalizeName(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            return Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "");
        }

        private static string StoreGameAsset(string sourcePath, string gameId, string kind, string previousPath)
        {
            try
            {
                string safeGameId = String.IsNullOrWhiteSpace(gameId) ? Guid.NewGuid().ToString("N") : gameId;
                string targetDir = Path.Combine(LibraryAssetsDir, safeGameId);
                Directory.CreateDirectory(targetDir);

                if (String.IsNullOrWhiteSpace(sourcePath))
                {
                    DeleteStoredAsset(previousPath, targetDir, kind);
                    DeleteAssetsByKind(targetDir, kind, "");
                    return "";
                }

                sourcePath = sourcePath.Trim();
                if (IsRemoteUrl(sourcePath))
                {
                    DeleteStoredAsset(previousPath, targetDir, kind);
                    DeleteAssetsByKind(targetDir, kind, "");
                    return sourcePath;
                }

                if (!File.Exists(sourcePath)) return sourcePath;
                string fullSource = Path.GetFullPath(sourcePath);
                string ext = Path.GetExtension(sourcePath);
                if (String.IsNullOrWhiteSpace(ext)) ext = ".asset";
                string targetPath = Path.Combine(targetDir, kind + ext.ToLowerInvariant());

                string fullTargetDir = Path.GetFullPath(targetDir);
                if (fullSource.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(fullSource).StartsWith(kind + ".", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteAssetsByKind(targetDir, kind, fullSource);
                    return fullSource;
                }

                DeleteStoredAsset(previousPath, targetDir, kind);
                DeleteAssetsByKind(targetDir, kind, targetPath);
                File.Copy(sourcePath, targetPath, true);
                return targetPath;
            }
            catch { return sourcePath; }
        }

        private static string LocalPathFromMaybeFileUrl(string value)
        {
            value = (value ?? "").Trim();
            if (!value.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return value;
            try
            {
                Uri uri = new Uri(value);
                return uri.LocalPath;
            }
            catch { return value; }
        }

        private static string StorePluginAsset(string sourcePath, string pluginId, string previousPath)
        {
            return StorePluginAsset(sourcePath, pluginId, previousPath, "icon");
        }

        private static List<PetDefinition> ReadPetDefinitions(Dictionary<string, object> pluginObj, string pluginId, PluginEntry existing)
        {
            List<PetDefinition> pets = new List<PetDefinition>();
            object rawPets;
            if (pluginObj == null || !pluginObj.TryGetValue("Pets", out rawPets) || rawPets == null) return pets;
            IEnumerable items = rawPets as IEnumerable;
            if (items == null || rawPets is string) return pets;

            foreach (object item in items)
            {
                Dictionary<string, object> petObj = item as Dictionary<string, object>;
                if (petObj == null) continue;
                string petId = GetString(petObj, "Id");
                if (String.IsNullOrWhiteSpace(petId)) petId = Guid.NewGuid().ToString("N");
                PetDefinition previous = existing == null || existing.Pets == null ? null : existing.Pets.Find(p => p.Id == petId);
                string idleSource = GetString(petObj, "IdleGifPath");
                string walkSource = GetString(petObj, "WalkGifPath");
                pets.Add(new PetDefinition
                {
                    Id = petId,
                    Name = String.IsNullOrWhiteSpace(GetString(petObj, "Name")) ? "Pet" : GetString(petObj, "Name"),
                    IdleGifPath = IsDefaultPetAsset(petId, idleSource, true) ? DefaultPetIdleGifPath : StorePetAnimationAsset(idleSource, pluginId, petId, "idle", previous == null ? "" : previous.IdleGifPath),
                    DragGifPath = petId == DefaultPetId ? DefaultPetDragGifPath : StorePetAnimationAsset(GetString(petObj, "DragGifPath"), pluginId, petId, "drag", previous == null ? "" : previous.DragGifPath),
                    WalkGifPath = IsDefaultPetAsset(petId, walkSource, false) ? DefaultPetWalkGifPath : StorePetAnimationAsset(walkSource, pluginId, petId, "walk", previous == null ? "" : previous.WalkGifPath)
                });
            }
            return pets;
        }

        private static List<Mp3Track> ReadMp3Tracks(Dictionary<string, object> pluginObj, string pluginId, PluginEntry existing)
        {
            List<Mp3Track> tracks = new List<Mp3Track>();
            object rawTracks;
            if (pluginObj == null || !pluginObj.TryGetValue("Mp3Tracks", out rawTracks) || rawTracks == null) return tracks;
            IEnumerable items = rawTracks as IEnumerable;
            if (items == null || rawTracks is string) return tracks;
            foreach (object item in items)
            {
                Dictionary<string, object> trackObj = item as Dictionary<string, object>;
                if (trackObj == null) continue;
                string trackId = GetString(trackObj, "Id");
                if (String.IsNullOrWhiteSpace(trackId)) trackId = Guid.NewGuid().ToString("N");
                Mp3Track previous = existing == null || existing.Mp3Tracks == null ? null : existing.Mp3Tracks.Find(t => t.Id == trackId);
                tracks.Add(new Mp3Track
                {
                    Id = trackId,
                    Title = String.IsNullOrWhiteSpace(GetString(trackObj, "Title")) ? "Track" : GetString(trackObj, "Title"),
                    Path = StorePluginAsset(GetString(trackObj, "Path"), pluginId, previous == null ? "" : previous.Path, "track-" + SafeAssetId(trackId))
                });
            }
            return tracks;
        }

        private static List<string> ReadStringList(Dictionary<string, object> source, string key)
        {
            List<string> values = new List<string>();
            object raw;
            if (source == null || !source.TryGetValue(key, out raw) || raw == null) return values;
            IEnumerable items = raw as IEnumerable;
            if (items == null || raw is string) return values;
            foreach (object item in items)
            {
                string value = Convert.ToString(item);
                if (!String.IsNullOrWhiteSpace(value) && !values.Exists(v => String.Equals(v, value, StringComparison.OrdinalIgnoreCase))) values.Add(value);
            }
            return values;
        }

        private static List<MovieWatchItem> ReadMovieWatchItems(Dictionary<string, object> pluginObj, string pluginId, PluginEntry existing)
        {
            List<MovieWatchItem> watchItems = new List<MovieWatchItem>();
            object rawItems;
            if (pluginObj == null || !pluginObj.TryGetValue("WatchItems", out rawItems) || rawItems == null) return watchItems;
            IEnumerable items = rawItems as IEnumerable;
            if (items == null || rawItems is string) return watchItems;
            foreach (object item in items)
            {
                Dictionary<string, object> itemObj = item as Dictionary<string, object>;
                if (itemObj == null) continue;
                string itemId = GetString(itemObj, "Id");
                if (String.IsNullOrWhiteSpace(itemId)) itemId = Guid.NewGuid().ToString("N");
                string title = GetString(itemObj, "Title").Trim();
                string episode = GetString(itemObj, "Episode").Trim();
                if (String.IsNullOrWhiteSpace(title)) continue;
                MovieWatchItem previous = existing == null || existing.WatchItems == null ? null : existing.WatchItems.Find(w => w.Id == itemId);
                watchItems.Add(new MovieWatchItem
                {
                    Id = itemId,
                    Title = title,
                    PosterPath = StorePluginAsset(GetString(itemObj, "PosterPath"), pluginId, previous == null ? "" : previous.PosterPath, "poster-" + SafeAssetId(itemId)),
                    Episode = episode,
                    Status = NormalizeMovieStatus(GetString(itemObj, "Status"), episode, GetString(itemObj, "WatchTime")),
                    TotalEpisodes = ClampInt(GetString(itemObj, "TotalEpisodes"), 0, 0, 10000),
                    WatchTime = GetString(itemObj, "WatchTime"),
                    Url = GetString(itemObj, "Url"),
                    SourceGameId = GetString(itemObj, "SourceGameId"),
                    UpdatedAt = String.IsNullOrWhiteSpace(GetString(itemObj, "UpdatedAt")) ? DateTime.UtcNow.ToString("o") : GetString(itemObj, "UpdatedAt")
                });
            }
            return watchItems;
        }

        private static string NormalizeMovieStatus(string status, string episode, string watchTime)
        {
            if (status == "planned" || status == "watching" || status == "completed") return status;
            string progress = (episode ?? "").Trim().ToLowerInvariant();
            if (progress == "done" || progress == "completed" || progress == "finished" || progress == "hoàn thành" || progress == "đã xem") return "completed";
            return String.IsNullOrWhiteSpace(progress) && String.IsNullOrWhiteSpace(watchTime) ? "planned" : "watching";
        }

        private static string StorePetAnimationAsset(string sourcePath, string pluginId, string petId, string kind, string previousPath)
        {
            try
            {
                string safePluginId = SafeAssetId(pluginId);
                string safePetId = SafeAssetId(petId);
                string targetDir = Path.Combine(PluginAssetsDir, safePluginId, "pets", safePetId);
                Directory.CreateDirectory(targetDir);
                if (String.IsNullOrWhiteSpace(sourcePath))
                {
                    DeleteStoredAsset(previousPath, targetDir, kind);
                    DeleteAssetsByKind(targetDir, kind, "");
                    return "";
                }
                if (!File.Exists(sourcePath)) return sourcePath;
                string extension = Path.GetExtension(sourcePath);
                if (!String.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase)) extension = ".gif";
                string targetPath = Path.Combine(targetDir, kind + extension.ToLowerInvariant());
                string fullSource = Path.GetFullPath(sourcePath);
                string fullTargetDir = Path.GetFullPath(targetDir);
                if (fullSource.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(fullSource).StartsWith(kind + ".", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteAssetsByKind(targetDir, kind, fullSource);
                    return fullSource;
                }
                DeleteStoredAsset(previousPath, targetDir, kind);
                DeleteAssetsByKind(targetDir, kind, targetPath);
                File.Copy(sourcePath, targetPath, true);
                return targetPath;
            }
            catch { return sourcePath; }
        }

        private static void DeleteRemovedPetAssets(PluginEntry previous, PluginEntry current)
        {
            if (previous == null || previous.Pets == null) return;
            List<PetDefinition> currentPets = current.Pets ?? new List<PetDefinition>();
            foreach (PetDefinition oldPet in previous.Pets)
            {
                if (String.Equals(oldPet.Id, DefaultPetId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!currentPets.Exists(p => p.Id == oldPet.Id)) DeletePetAssetDirectory(current.Id, oldPet.Id);
            }
        }

        private static void DeleteRemovedMp3Assets(PluginEntry previous, PluginEntry current)
        {
            if (previous == null || previous.Mp3Tracks == null) return;
            List<Mp3Track> currentTracks = current.Mp3Tracks ?? new List<Mp3Track>();
            foreach (Mp3Track oldTrack in previous.Mp3Tracks)
            {
                if (currentTracks.Exists(t => t.Id == oldTrack.Id)) continue;
                DeletePluginAssetByKind(current.Id, "track-" + SafeAssetId(oldTrack.Id));
            }
        }

        private static void DeleteRemovedMoviePosterAssets(PluginEntry previous, PluginEntry current)
        {
            if (previous == null || previous.WatchItems == null) return;
            List<MovieWatchItem> currentItems = current.WatchItems ?? new List<MovieWatchItem>();
            foreach (MovieWatchItem oldItem in previous.WatchItems)
            {
                if (currentItems.Exists(w => w.Id == oldItem.Id)) continue;
                DeletePluginAssetByKind(current.Id, "poster-" + SafeAssetId(oldItem.Id));
            }
        }

        private static void DeletePluginAssetByKind(string pluginId, string kind)
        {
            try
            {
                string targetDir = Path.Combine(PluginAssetsDir, SafeAssetId(pluginId));
                DeleteAssetsByKind(targetDir, kind, "");
            }
            catch { }
        }

        private static void DeletePetAssetDirectory(string pluginId, string petId)
        {
            try
            {
                string petsRoot = Path.GetFullPath(Path.Combine(PluginAssetsDir, SafeAssetId(pluginId), "pets"));
                if (!petsRoot.EndsWith(Path.DirectorySeparatorChar.ToString())) petsRoot += Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(Path.Combine(petsRoot, SafeAssetId(petId)));
                if (target.StartsWith(petsRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, true);
            }
            catch { }
        }

        private static string SafeAssetId(string value)
        {
            string safe = Regex.Replace(value ?? "", "[^a-zA-Z0-9._-]", "_");
            return String.IsNullOrWhiteSpace(safe) ? Guid.NewGuid().ToString("N") : safe;
        }

        private static bool IsDefaultPetAsset(string petId, string path, bool idle)
        {
            if (!String.Equals(petId, DefaultPetId, StringComparison.OrdinalIgnoreCase)) return false;
            string expected = idle ? DefaultPetIdleGifPath : DefaultPetWalkGifPath;
            try { return String.Equals(Path.GetFullPath(path ?? ""), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "";
            char separator = Path.DirectorySeparatorChar;
            return path.EndsWith(separator.ToString()) ? path : path + separator;
        }

        private static bool IsUnderDirectory(string path, string rootWithSeparator)
        {
            if (String.IsNullOrWhiteSpace(path) || String.IsNullOrWhiteSpace(rootWithSeparator)) return false;
            string fullPath = Path.GetFullPath(path);
            string root = EnsureTrailingSeparator(Path.GetFullPath(rootWithSeparator));
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private static string StorePluginAsset(string sourcePath, string pluginId, string previousPath, string kind)
        {
            try
            {
                kind = String.IsNullOrWhiteSpace(kind) ? "asset" : kind.Trim().ToLowerInvariant();
                string safePluginId = String.IsNullOrWhiteSpace(pluginId) ? Guid.NewGuid().ToString("N") : pluginId;
                string targetDir = Path.Combine(PluginAssetsDir, safePluginId);
                Directory.CreateDirectory(targetDir);

                if (String.IsNullOrWhiteSpace(sourcePath))
                {
                    DeleteStoredAsset(previousPath, targetDir, kind);
                    DeleteAssetsByKind(targetDir, kind, "");
                    return "";
                }

                sourcePath = sourcePath.Trim();
                if (IsRemoteUrl(sourcePath))
                {
                    DeleteStoredAsset(previousPath, targetDir, kind);
                    DeleteAssetsByKind(targetDir, kind, "");
                    return sourcePath;
                }

                if (!File.Exists(sourcePath)) return sourcePath;
                string fullSource = Path.GetFullPath(sourcePath);
                string ext = Path.GetExtension(sourcePath);
                if (String.IsNullOrWhiteSpace(ext)) ext = ".asset";
                string targetPath = Path.Combine(targetDir, kind + ext.ToLowerInvariant());
                string fullTargetDir = Path.GetFullPath(targetDir);
                if (fullSource.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(fullSource).StartsWith(kind + ".", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteAssetsByKind(targetDir, kind, fullSource);
                    return fullSource;
                }

                DeleteStoredAsset(previousPath, targetDir, kind);
                DeleteAssetsByKind(targetDir, kind, targetPath);
                File.Copy(sourcePath, targetPath, true);
                return targetPath;
            }
            catch { return sourcePath; }
        }

        private static void DeleteStoredAsset(string assetPath, string targetDir, string kind)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath)) return;
                string fullAsset = Path.GetFullPath(assetPath);
                string fullTargetDir = Path.GetFullPath(targetDir);
                if (!fullAsset.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase)) return;
                if (!Path.GetFileName(fullAsset).StartsWith(kind + ".", StringComparison.OrdinalIgnoreCase)) return;
                File.Delete(fullAsset);
            }
            catch { }
        }

        private static void DeleteAssetsByKind(string targetDir, string kind, string keepPath)
        {
            try
            {
                if (!Directory.Exists(targetDir)) return;
                string fullKeep = String.IsNullOrWhiteSpace(keepPath) ? "" : Path.GetFullPath(keepPath);
                foreach (string path in Directory.GetFiles(targetDir, kind + ".*"))
                {
                    string fullPath = Path.GetFullPath(path);
                    if (!String.IsNullOrWhiteSpace(fullKeep) && String.Equals(fullPath, fullKeep, StringComparison.OrdinalIgnoreCase)) continue;
                    File.Delete(fullPath);
                }
            }
            catch { }
        }

        private static void DeleteGameAssetDirectory(string gameId)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(gameId)) return;
                string libraryRoot = Path.GetFullPath(LibraryAssetsDir);
                string targetDir = Path.GetFullPath(Path.Combine(LibraryAssetsDir, gameId));
                if (!targetDir.StartsWith(libraryRoot, StringComparison.OrdinalIgnoreCase)) return;
                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            }
            catch { }
        }

        private static void DeletePluginAssetDirectory(string pluginId)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(pluginId)) return;
                string pluginRoot = Path.GetFullPath(PluginAssetsDir);
                string targetDir = Path.GetFullPath(Path.Combine(PluginAssetsDir, pluginId));
                if (!targetDir.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase)) return;
                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            }
            catch { }
        }

        private static string ExtractExeIcon(string exePath, string gameId)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return "";
                Icon icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null) return "";
                string targetDir = Path.Combine(LibraryAssetsDir, gameId);
                Directory.CreateDirectory(targetDir);
                string targetPath = Path.Combine(targetDir, "icon.png");
                DeleteAssetsByKind(targetDir, "icon", targetPath);
                using (Bitmap bitmap = icon.ToBitmap())
                {
                    bitmap.Save(targetPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                icon.Dispose();
                return targetPath;
            }
            catch { return ""; }
        }

        internal static string AssetsDir
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets"); }
        }

        private static string LibraryAssetsDir
        {
            get { return Path.Combine(AssetsDir, "library"); }
        }

        private static string PluginAssetsDir
        {
            get { return Path.Combine(AssetsDir, "plugins"); }
        }

        private static string BuiltInPluginPacksDir
        {
            get { return Path.Combine(AssetsDir, "builtin-plugins"); }
        }

        private static string WebViewUserDataDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HYROLauncher", "WebView2"); }
        }

        private static string DefaultPetIdleGifPath
        {
            get { return Path.Combine(AssetsDir, "pets", "hyro-buddy-idle.gif"); }
        }

        private static string DefaultPetWalkGifPath
        {
            get { return Path.Combine(AssetsDir, "pets", "hyro-buddy-walk.gif"); }
        }

        private static string DefaultPetDragGifPath
        {
            get { return Path.Combine(AssetsDir, "pets", "hyro-buddy-drag.gif"); }
        }

        private static void ApplyStartupSetting(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (enabled) key.SetValue("HYROLauncher", "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue("HYROLauncher", false);
                }
            }
            catch { }
        }

        private void Notify(string message)
        {
            Post(new Dictionary<string, object>
            {
                { "type", "notice" },
                { "message", message }
            });
        }

        private static void CopyDirectory(string sourceDir, string targetDir, bool overwrite)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string target = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, target, overwrite);
            }
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)), overwrite);
            }
        }

        private static string FindFile(string root, string fileName)
        {
            try
            {
                string direct = Path.Combine(root, fileName);
                if (File.Exists(direct)) return direct;
                string[] matches = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                return matches.Length == 0 ? "" : matches[0];
            }
            catch { return ""; }
        }

        private static string FindDirectory(string root, string directoryName)
        {
            try
            {
                string directLibrary = Path.Combine(root, "assets", directoryName);
                if (Directory.Exists(directLibrary)) return directLibrary;
                string direct = Path.Combine(root, directoryName);
                if (Directory.Exists(direct)) return direct;
                string[] matches = Directory.GetDirectories(root, directoryName, SearchOption.AllDirectories);
                return matches.Length == 0 ? "" : matches[0];
            }
            catch { return ""; }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(path) && Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch { }
        }

        private static void ReplaceLibraryAssets(string importedLibrary)
        {
            string assetsRoot = Path.GetFullPath(AssetsDir);
            if (!assetsRoot.EndsWith(Path.DirectorySeparatorChar.ToString())) assetsRoot += Path.DirectorySeparatorChar;
            string libraryRoot = Path.GetFullPath(LibraryAssetsDir);
            if (!libraryRoot.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid library asset path.");

            if (Directory.Exists(libraryRoot)) Directory.Delete(libraryRoot, true);
            Directory.CreateDirectory(libraryRoot);
            if (!String.IsNullOrWhiteSpace(importedLibrary) && Directory.Exists(importedLibrary)) CopyDirectory(importedLibrary, libraryRoot, true);
        }

        private static void ReplacePluginAssets(string importedPlugins)
        {
            string assetsRoot = Path.GetFullPath(AssetsDir);
            if (!assetsRoot.EndsWith(Path.DirectorySeparatorChar.ToString())) assetsRoot += Path.DirectorySeparatorChar;
            string pluginRoot = Path.GetFullPath(PluginAssetsDir);
            if (!pluginRoot.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid plugin asset path.");

            if (Directory.Exists(pluginRoot)) Directory.Delete(pluginRoot, true);
            Directory.CreateDirectory(pluginRoot);
            if (!String.IsNullOrWhiteSpace(importedPlugins) && Directory.Exists(importedPlugins)) CopyDirectory(importedPlugins, pluginRoot, true);
        }

        private static string NormalizeCardStyle(string value)
        {
            if (String.Equals(value, "icon", StringComparison.OrdinalIgnoreCase)) return "icon";
            return String.Equals(value, "3d", StringComparison.OrdinalIgnoreCase) ? "3d" : "2d";
        }

        private static string NormalizeLibraryLayout(string value)
        {
            if(String.Equals(value,"bookshelf",StringComparison.OrdinalIgnoreCase))return "bookshelf";
            return String.Equals(value,"carousel",StringComparison.OrdinalIgnoreCase)?"carousel":"default";
        }

        private static string NormalizeHexColor(string value)
        {
            value = (value ?? "").Trim();
            return Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$") ? value : "";
        }

        private static int ClampInt(string value, int fallback, int min, int max)
        {
            int parsed;
            if (!Int32.TryParse(value, out parsed)) return fallback;
            return Math.Max(min, Math.Min(max, parsed));
        }

        private static int NormalizeMaxGridColumns(string value)
        {
            int columns;
            if (!Int32.TryParse(value, out columns)) columns = 5;
            return Math.Max(1, Math.Min(10, columns));
        }

        private static string NormalizeSidebarPosition(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            if (value == "right" || value == "top" || value == "bottom") return value;
            return "left";
        }

        private static string NormalizeThemeMode(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            if (value == "light" || value == "translucent") return value;
            return "dark";
        }

        private static bool IsWebViewTarget(string value)
        {
            value = (value ?? "").Trim();
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePluginType(string value)
        {
            if (String.Equals(value, "hytcg", StringComparison.OrdinalIgnoreCase)) return "hytcg";
            if (String.Equals(value, "pet", StringComparison.OrdinalIgnoreCase)) return "pet";
            if (String.Equals(value, "mp3", StringComparison.OrdinalIgnoreCase)) return "mp3";
            if (String.Equals(value, "movie", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "movie-watchlist", StringComparison.OrdinalIgnoreCase)) return "movie-watchlist";
            if (String.Equals(value, "hyrowser", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "browser", StringComparison.OrdinalIgnoreCase)) return "hyrowser";
            return String.Equals(value, "custom", StringComparison.OrdinalIgnoreCase) ? "custom" : "webview";
        }

        private bool IsValidPin(string pin)
        {
            string hash = String.IsNullOrWhiteSpace(state.Settings.PrivatePinHash) ? DefaultPrivatePinHash : state.Settings.PrivatePinHash;
            return String.Equals(HashPin(pin), hash, StringComparison.OrdinalIgnoreCase);
        }

        private static string HashPin(string pin)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(pin ?? ""));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return "";
            return Convert.ToString(dict[key]);
        }

        private static bool GetBool(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return false;
            bool value;
            return Boolean.TryParse(Convert.ToString(dict[key]), out value) && value;
        }

        private static int GetPixel(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return 0;
            try { return (int)Math.Round(Convert.ToDouble(dict[key])); }
            catch { return 0; }
        }
    }

    internal class RoundedPanel : Panel
    {
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }

        public RoundedPanel()
        {
            Radius = 16;
            FillColor = Color.FromArgb(16, 22, 34);
            BorderColor = Color.FromArgb(40, 50, 68);
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (GraphicsPath path = RoundedRect(rect, Radius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor, 1f))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Invalidate();
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int r = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height)));
            int d = r * 2;
            GraphicsPath path = new GraphicsPath();
            if (r <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal class RoundedButton : Button
    {
        public int Radius { get; set; }
        public Color OutlineColor { get; set; }
        public float OutlineWidth { get; set; }

        public RoundedButton()
        {
            Radius = 10;
            OutlineColor = Color.Transparent;
            OutlineWidth = 0f;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (OutlineWidth <= 0f || OutlineColor.A == 0) return;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (GraphicsPath path = CreatePath(rect, Radius))
            using (Pen pen = new Pen(OutlineColor, OutlineWidth))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }

        private void UpdateRegion()
        {
            Rectangle rect = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));
            using (GraphicsPath path = CreatePath(rect, Radius))
            {
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null) oldRegion.Dispose();
            }
        }

        protected static GraphicsPath CreatePath(Rectangle bounds, int radius)
        {
            int r = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            int d = r * 2;
            GraphicsPath path = new GraphicsPath();
            if (r <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class HyrowserWindow : Form
    {
        public Action UiClick { get; set; }
        public Action<string, string> SaveToWatchlist { get; set; }
        public Action<string, string> AdvanceMovie { get; set; }
        private readonly ToolTip browserTips = new ToolTip();
        private readonly Panel loadingBar = new Panel { Dock = DockStyle.Top, Height = 3, Visible = false };
        private readonly List<BrowserTab> tabs = new List<BrowserTab>();
        private readonly Label titleLabel = new Label();
        private readonly TextBox addressBox = new TextBox();
        private readonly RoundedPanel tabsHost = new RoundedPanel();
        private readonly HyBrowserTabsViewport tabStrip = new HyBrowserTabsViewport();
        private readonly Label tabsLabel = new Label();
        private FlowLayoutPanel tabActions;
        private TableLayoutPanel tabsLayout;
        private Button newTabButton;
        private Button privateTabButton;
        private Button toggleTabsButton;
        private bool tabsCollapsed;
        private bool layingOutTabs;
        private readonly RoundedPanel navHost = new RoundedPanel();
        private readonly RoundedPanel addressHost = new RoundedPanel();
        private readonly Panel browserHost = new Panel();
        private readonly Button backButton;
        private readonly Button forwardButton;
        private readonly Button reloadButton;
        private readonly Button closeTabButton;
        private Button watchButton;
        private readonly string title;
        private CoreWebView2Environment normalEnvironment;
        private BrowserTab activeTab;
        internal bool HasMovieTab { get {return tabs.Exists(t=>t.Movie!=null && t.View!=null && !t.View.IsDisposed); } }
        internal bool MovieTabReady { get {return tabs.Exists(t=>t.Movie!=null && !t.Loading && t.View!=null && !t.View.IsDisposed && t.View.CoreWebView2!=null && t.View.Source!=null && (t.View.Source.Scheme=="https" || t.View.Source.Scheme=="http"));} }
        private string homeUrl = "https://www.google.com";
        private bool initialized;
        private Color accentColor = Color.FromArgb(68, 168, 255);

        private sealed class BrowserTab
        {
            public string Id;
            public string Title;
            public bool IsPrivate;
            public bool Loading;
            public WebView2 View;
            public RoundedButton Button;
            public MovieWatchItem Movie;
            public string PendingUrl;
            public int PosterVersion;
            public Image Poster;
        }

        private sealed class BrowserTabButton : RoundedButton
        {
            public Image Poster;
            public string Caption;
            public string Subtitle;
            public bool Compact;
            private bool hovered;
            public BrowserTabButton() { DoubleBuffered=true; }
            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered=true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered=false; Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                Color fillColor=hovered ? Color.FromArgb(Math.Min(255,BackColor.R+8),Math.Min(255,BackColor.G+8),Math.Min(255,BackColor.B+10)) : BackColor;
                using(var fill=new SolidBrush(fillColor)) e.Graphics.FillRectangle(fill,ClientRectangle);
                if(Poster!=null)
                {
                    float scale=Math.Max((float)Width/Poster.Width,(float)Height/Poster.Height);
                    int width=(int)(Poster.Width*scale),height=(int)(Poster.Height*scale);
                    e.Graphics.DrawImage(Poster,new Rectangle((Width-width)/2,(Height-height)/2,width,height));
                    using(var shade=new LinearGradientBrush(ClientRectangle,Color.FromArgb(95,12,18,30),Color.FromArgb(235,12,18,30),90F)) e.Graphics.FillRectangle(shade,ClientRectangle);
                }
                if(Compact)
                {
                    TextRenderer.DrawText(e.Graphics,Caption,Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
                    PaintOutline(e.Graphics);
                    return;
                }
                Rectangle textBounds=new Rectangle(10,String.IsNullOrEmpty(Subtitle)?0:Height-45,Math.Max(1,Width-39),String.IsNullOrEmpty(Subtitle)?Height:23);
                TextRenderer.DrawText(e.Graphics,Caption??"New tab",Font,textBounds,ForeColor,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
                if(!String.IsNullOrEmpty(Subtitle)) using(var small=new Font("Segoe UI",8F)) TextRenderer.DrawText(e.Graphics,Subtitle,small,new Rectangle(12,Height-23,Width-24,18),Color.FromArgb(192,211,232),TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
                PaintOutline(e.Graphics);
            }
            private void PaintOutline(Graphics graphics)
            {
                if(OutlineWidth<=0 || Width<3 || Height<3) return;
                using(var path=CreatePath(new Rectangle(1,1,Width-3,Height-3),Radius))
                using(var pen=new Pen(OutlineColor,OutlineWidth)) graphics.DrawPath(pen,path);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public HyrowserWindow(string windowTitle)
        {
            title = String.IsNullOrWhiteSpace(windowTitle) ? "HYROWSER" : windowTitle;
            Text = title;
            string iconPath = Path.Combine(WebLauncherForm.AssetsDir, "app.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(12, 18, 30);
            Size = new Size(1180, 760);
            MinimumSize = new Size(820, 520);
            Font = new Font("Segoe UI", 9F);
            MaximizedBounds = Screen.FromControl(this).WorkingArea;

            RoundedPanel titleBar = new RoundedPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(16, 4, 8, 4),
                BackColor = Color.FromArgb(12, 18, 30),
                FillColor = Color.FromArgb(12, 18, 30),
                BorderColor = Color.FromArgb(12, 18, 30),
                Radius = 0
            };
            titleBar.MouseDown += BeginDrag;

            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Text = title;
            titleLabel.ForeColor = Color.FromArgb(208, 220, 236);
            titleLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Padding = new Padding(6, 0, 0, 0);
            titleLabel.MouseDown += BeginDrag;
            titleLabel.DoubleClick += delegate { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; };
            titleBar.Controls.Add(titleLabel);

            Button closeButton = CreateTitleButton("X", "Close", "x.svg");
            closeButton.Click += delegate { Close(); };
            titleBar.Controls.Add(closeButton);

            Button maximizeButton = CreateTitleButton("□", "Maximize / restore", "maximize-2.svg");
            maximizeButton.Click += delegate { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; };
            titleBar.Controls.Add(maximizeButton);

            Button minimizeButton = CreateTitleButton("-", "Minimize", "minimize-2.svg");
            minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };
            titleBar.Controls.Add(minimizeButton);

            tabsHost.Dock = DockStyle.Left;
            tabsHost.Width = 184;
            tabsHost.Padding = new Padding(8, 8, 8, 8);
            tabsHost.BackColor = Color.FromArgb(16, 23, 37);
            tabsHost.FillColor = Color.FromArgb(16, 23, 37);
            tabsHost.BorderColor = Color.FromArgb(16, 23, 37);
            tabsHost.Radius = 0;

            tabStrip.Dock = DockStyle.Fill;
            tabStrip.Padding = new Padding(0);
            tabStrip.Margin = new Padding(0);
            tabStrip.AutoScroll = false;
            tabStrip.BackColor = Color.FromArgb(16,23,37);
            tabStrip.Resize += delegate { LayoutTabButtons(); };
            tabStrip.OffsetChanged += delegate { LayoutTabButtons(); };

            tabActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                Margin = new Padding(0),
                WrapContents = false,
                AutoScroll = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };

            newTabButton = CreateTabActionButton("+", "New tab", "add.svg");
            newTabButton.Width = 80;
            newTabButton.Margin = new Padding(0, 1, 4, 1);
            newTabButton.Text = "New tab";
            newTabButton.ImageAlign = ContentAlignment.MiddleLeft;
            newTabButton.TextImageRelation = TextImageRelation.ImageBeforeText;
            newTabButton.Click += delegate { CreateTab(homeUrl, false); };
            tabActions.Controls.Add(newTabButton);

            privateTabButton = CreateTabActionButton("Private", "New private tab", "private.svg");
            privateTabButton.Width = 80;
            privateTabButton.Margin = new Padding(0, 1, 0, 1);
            privateTabButton.Text = "Private";
            privateTabButton.ImageAlign = ContentAlignment.MiddleLeft;
            privateTabButton.TextImageRelation = TextImageRelation.ImageBeforeText;
            privateTabButton.Click += delegate { CreateTab(homeUrl, true); };
            tabActions.Controls.Add(privateTabButton);

            closeTabButton = CreateTabActionButton("Close tab", "Close current tab", "close-tab.svg");
            closeTabButton.Width = 48;
            closeTabButton.Visible = false;
            closeTabButton.Click += delegate { CloseActiveTab(); };
            tabActions.Controls.Add(closeTabButton);

            tabsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                Margin = new Padding(0),
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            tabsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tabsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            tabsLayout.Controls.Add(tabStrip, 0, 0);
            tabsLayout.Controls.Add(tabActions, 0, 1);
            tabsHost.Controls.Add(tabsLayout);
            Panel tabsHeader = new Panel { Dock=DockStyle.Top, Height=30, BackColor=Color.Transparent };
            tabsLabel.Text="TABS";
            tabsLabel.Dock=DockStyle.Fill;
            tabsLabel.ForeColor=Color.FromArgb(138,154,179);
            tabsLabel.Font=new Font("Segoe UI",7.5F);
            tabsLabel.TextAlign=ContentAlignment.MiddleLeft;
            tabsLabel.Padding=new Padding(3,0,0,0);
            tabsHeader.Controls.Add(tabsLabel);
            toggleTabsButton=new RoundedButton { Text="‹",Dock=DockStyle.Right,Width=30,Font=new Font("Segoe UI",13F),ForeColor=Color.FromArgb(178,199,222),BackColor=Color.FromArgb(26,36,53),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,Radius=8 };
            toggleTabsButton.FlatAppearance.BorderSize=0;
            toggleTabsButton.AccessibleName="Collapse tabs";
            browserTips.SetToolTip(toggleTabsButton,"Collapse tabs");
            toggleTabsButton.Click+=delegate { if(UiClick!=null) UiClick(); ToggleTabsSidebar(); };
            tabsHeader.Controls.Add(toggleTabsButton);
            tabsHost.Controls.Add(tabsHeader);

            navHost.Dock = DockStyle.Top;
            navHost.Height = 52;
            navHost.Padding = new Padding(8, 5, 8, 7);
            navHost.BackColor = Color.FromArgb(20, 28, 43);
            navHost.FillColor = Color.FromArgb(20, 28, 43);
            navHost.BorderColor = Color.FromArgb(20, 28, 43);
            navHost.Radius = 0;

            TableLayoutPanel navBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                BackColor = Color.Transparent,
                ColumnCount = 8,
                RowCount = 1,
                Margin = new Padding(0)
            };
            navBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
            navBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
            navBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
            navBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
            navBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            navBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));
            navBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));
            navBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));
            navBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            navBar.ColumnStyles[7].Width = 104F;

            Button externalButton = CreateNavButton("Open", "Open external", "open.svg");
            externalButton.Dock = DockStyle.Fill;
            externalButton.Click += delegate { OpenExternal(); };
            navBar.Controls.Add(externalButton, 5, 0);

            Button goButton = CreateNavButton("Go", "Go", "go.svg");
            goButton.Dock = DockStyle.Fill;
            goButton.Click += delegate { NavigateFromAddress(); };
            navBar.Controls.Add(goButton, 6, 0);
            watchButton = CreateNavButton("Save", "Save page to Movie Watchlist", "bookmark-plus.svg");
            watchButton.Dock = DockStyle.Fill;
            watchButton.TextImageRelation = TextImageRelation.ImageBeforeText;
            watchButton.Font = new Font("Segoe UI",8.5F,FontStyle.Bold);
            watchButton.BackColor = Color.FromArgb(34,66,76);
            ((RoundedButton)watchButton).OutlineColor=Color.FromArgb(67,109,126);
            ((RoundedButton)watchButton).OutlineWidth=1F;
            watchButton.Click += delegate {
                var core = ActiveCore();
                if (core == null || String.IsNullOrWhiteSpace(core.Source)) return;
                if (activeTab.Movie != null && AdvanceMovie != null) AdvanceMovie(activeTab.Movie.Id,core.Source);
                else if (SaveToWatchlist != null) SaveToWatchlist(core.DocumentTitle,core.Source);
            };
            navBar.Controls.Add(watchButton, 7, 0);

            addressBox.Dock = DockStyle.Fill;
            addressBox.BorderStyle = BorderStyle.None;
            addressBox.BackColor = Color.FromArgb(27, 37, 54);
            addressBox.ForeColor = Color.FromArgb(220,230,243);
            addressBox.Font = new Font("Segoe UI", 9.5F);
            addressBox.Margin = new Padding(0);
            addressBox.AccessibleName = "Address or search";
            addressBox.Enter += delegate { addressBox.BeginInvoke(new Action(addressBox.SelectAll)); };
            addressBox.MouseDown += delegate { if (!addressBox.Focused) { addressBox.Focus(); addressBox.SelectAll(); } };
            addressBox.GotFocus += delegate { addressHost.BorderColor = accentColor; addressHost.Invalidate(); };
            addressBox.LostFocus += delegate { addressHost.BorderColor = SoftAccentBorder(); addressHost.Invalidate(); };
            addressBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                NavigateFromAddress();
            };
            addressHost.Dock = DockStyle.Fill;
            addressHost.Margin = new Padding(8, 1, 8, 1);
            addressHost.Padding = new Padding(13, 9, 13, 0);
            addressHost.BackColor = Color.Transparent;
            addressHost.FillColor = Color.FromArgb(27, 37, 54);
            addressHost.BorderColor = Color.FromArgb(45, 57, 78);
            addressHost.Radius = 10;
            addressHost.Controls.Add(addressBox);
            navBar.Controls.Add(addressHost, 4, 0);

            reloadButton = CreateNavButton("Reload", "Reload", "reload.svg");
            reloadButton.Dock = DockStyle.Fill;
            reloadButton.Click += delegate { Reload(); };
            navBar.Controls.Add(reloadButton, 3, 0);

            Button homeButton = CreateNavButton("Home", "Home", "home.svg");
            homeButton.Dock = DockStyle.Fill;
            homeButton.Click += delegate { Navigate(homeUrl); };
            navBar.Controls.Add(homeButton, 2, 0);

            forwardButton = CreateNavButton(">", "Forward", "redo.svg");
            forwardButton.Dock = DockStyle.Fill;
            forwardButton.Click += delegate { try { if (ActiveCore() != null && ActiveCore().CanGoForward) ActiveCore().GoForward(); } catch { } };
            navBar.Controls.Add(forwardButton, 1, 0);

            backButton = CreateNavButton("<", "Back", "undo.svg");
            backButton.Dock = DockStyle.Fill;
            backButton.Click += delegate { try { if (ActiveCore() != null && ActiveCore().CanGoBack) ActiveCore().GoBack(); } catch { } };
            navBar.Controls.Add(backButton, 0, 0);
            navHost.Controls.Add(navBar);

            browserHost.Dock = DockStyle.Fill;
            browserHost.BackColor = Color.FromArgb(12,18,30);
            browserHost.Resize += delegate
            {
                foreach (BrowserTab tab in tabs) FitTabView(tab);
            };
            Panel workspace = new Panel { Dock=DockStyle.Fill,BackColor=Color.FromArgb(16,23,37),Padding=new Padding(0,0,6,6) };
            Panel content = new Panel { Dock=DockStyle.Fill,BackColor=Color.FromArgb(20,28,43) };
            content.Controls.Add(browserHost);
            loadingBar.BackColor = accentColor;
            content.Controls.Add(loadingBar);
            content.Controls.Add(navHost);
            workspace.Controls.Add(content);
            workspace.Controls.Add(tabsHost);
            Controls.Add(workspace);
            Controls.Add(titleBar);

            FormClosed += delegate { CloseAllTabs(); browserTips.Dispose(); };
        }

        public void SetAccent(Color color)
        {
            accentColor = color;
            loadingBar.BackColor = color;
            addressHost.BorderColor = SoftAccentBorder();
            addressHost.Invalidate();
            ApplyAccentToButtons(Controls);
            RenderTabs();
        }

        public async Task ClearBrowsingDataAsync()
        {
            foreach (BrowserTab tab in tabs)
            {
                try { await WebLauncherForm.ClearBrowsingDataForViewAsync(tab.View); }
                catch { }
            }
        }

        public async Task EnsureWebViewAsync(CoreWebView2Environment environment)
        {
            if (initialized) return;
            normalEnvironment = environment;
            initialized = true;
            await Task.FromResult(0);
        }

        public void ShowBrowser(string startUrl)
        {
            string url = NormalizeBrowserInput(startUrl);
            if (!String.IsNullOrWhiteSpace(url)) homeUrl = url;
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            if (tabs.Count == 0) CreateTab(homeUrl, false);
            BringToFront();
            Activate();
        }

        public void OpenPage(string url)
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            foreach (var tab in tabs) if (tab.View != null && tab.View.Source != null && tab.View.Source.AbsoluteUri == url) { ActivateTab(tab); BringToFront(); return; }
            CreateTab(url, false); BringToFront();
        }
        public void OpenMovie(MovieWatchItem movie)
        {
            if(movie==null) return;
            if(!Visible) Show();
            if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal;
            BrowserTab tab=tabs.Find(t=>t.Movie!=null);
            if(tab==null) CreateTab(movie.Url,false,movie);
            else
            {
                bool sameMovie=tab.Movie.Id==movie.Id;
                SetMovieContext(tab,movie);
                tab.PendingUrl=movie.Url;
                if(!sameMovie || tab.View.Source==null || tab.View.Source.AbsoluteUri!=movie.Url) Navigate(tab,movie.Url);
                ActivateTab(tab);
            }
            BringToFront(); Activate();
        }
        public void UpdateMovieContext(MovieWatchItem movie)
        {
            BrowserTab tab=tabs.Find(t=>t.Movie!=null&&t.Movie.Id==movie.Id);
            if(tab!=null) { SetMovieContext(tab,movie); if(tab==activeTab) RefreshActiveChromeAccent(); SyncBrowserState(tab); RenderTabs(); }
        }
        private void SetMovieContext(BrowserTab tab,MovieWatchItem movie)
        {
            bool artworkChanged=tab.Movie==null || tab.Movie.PosterPath!=movie.PosterPath;
            tab.Movie=new MovieWatchItem { Id=movie.Id,Title=movie.Title,Episode=movie.Episode,PosterPath=movie.PosterPath,Url=movie.Url,TotalEpisodes=movie.TotalEpisodes,Status=movie.Status };
            if(artworkChanged) LoadMoviePoster(tab,movie.PosterPath);
            RenderTabs();
        }
        private async void LoadMoviePoster(BrowserTab tab,string path)
        {
            int version=++tab.PosterVersion;
            Image old=tab.Poster; tab.Poster=null; if(old!=null) old.Dispose();
            Image image=await Task.Run(delegate {
                try
                {
                    Uri uri;
                    if(Uri.TryCreate(path,UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https"))
                    {
                        var request=(System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri); request.Timeout=8000; request.ReadWriteTimeout=8000;
                        using(var buffer=new MemoryStream(ApiActivityLog.ReadBytes(request,"HyBrowser Poster",12*1024*1024)))
                        {
                            buffer.Position=0;using(Image decoded=Image.FromStream(buffer)) return (Image)new Bitmap(decoded);
                        }
                    }
                    if(File.Exists(path)) using(Image decoded=Image.FromFile(path)) return (Image)new Bitmap(decoded);
                }
                catch { }
                return (Image)null;
            });
            if(IsDisposed||!tabs.Contains(tab)||version!=tab.PosterVersion) { if(image!=null) image.Dispose(); return; }
            tab.Poster=image; RenderTabs();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleBrowserShortcut(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }
        private static bool IsBrowserShortcut(Keys keys)
        {
            return keys == (Keys.Control | Keys.L) || keys == (Keys.Alt | Keys.D) || keys == (Keys.Control | Keys.T)
                || keys == (Keys.Control | Keys.Shift | Keys.N) || keys == (Keys.Control | Keys.W)
                || keys == (Keys.Control | Keys.Tab) || keys == (Keys.Control | Keys.Shift | Keys.Tab)
                || keys == (Keys.Control | Keys.R) || keys == Keys.F5 || keys == Keys.Escape
                || keys == (Keys.Alt | Keys.Left) || keys == (Keys.Alt | Keys.Right);
        }
        private bool HandleBrowserShortcut(Keys keys)
        {
            if (!IsBrowserShortcut(keys)) return false;
            if (keys == (Keys.Control | Keys.L) || keys == (Keys.Alt | Keys.D)) { addressBox.Focus(); addressBox.SelectAll(); }
            else if (keys == (Keys.Control | Keys.T)) CreateTab(homeUrl, false);
            else if (keys == (Keys.Control | Keys.Shift | Keys.N)) CreateTab(homeUrl, true);
            else if (keys == (Keys.Control | Keys.W)) CloseActiveTab();
            else if (keys == (Keys.Control | Keys.Tab) || keys == (Keys.Control | Keys.Shift | Keys.Tab))
            { if (tabs.Count > 0) ActivateTab(tabs[(tabs.IndexOf(activeTab) + (keys.HasFlag(Keys.Shift) ? tabs.Count - 1 : 1)) % tabs.Count]); }
            else if (keys == Keys.Escape) { if (ActiveCore() != null) ActiveCore().Stop(); }
            else if (keys == (Keys.Alt | Keys.Left)) { if (ActiveCore() != null && ActiveCore().CanGoBack) ActiveCore().GoBack(); }
            else if (keys == (Keys.Alt | Keys.Right)) { if (ActiveCore() != null && ActiveCore().CanGoForward) ActiveCore().GoForward(); }
            else if (ActiveCore() != null) ActiveCore().Reload();
            return true;
        }
        protected override void WndProc(ref Message msg)
        {
            base.WndProc(ref msg);
            if (msg.Msg != 0x84 || WindowState != FormWindowState.Normal) return;
            var point = PointToClient(new Point(unchecked((short)(long)msg.LParam), unchecked((short)((long)msg.LParam >> 16))));
            bool left = point.X < 6, right = point.X >= ClientSize.Width - 6, top = point.Y < 6, bottom = point.Y >= ClientSize.Height - 6;
            if (left || right || top || bottom) msg.Result = (IntPtr)(top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : 11);
        }

        private Button CreateTitleButton(string text, string tooltip, string iconName)
        {
            RoundedButton button = new RoundedButton
            {
                Dock = DockStyle.Right,
                Width = 40,
                Height = 34,
                Text = "",
                ForeColor = Color.FromArgb(178,199,222),
                BackColor = Color.FromArgb(12,18,30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Radius = 8,
                Margin = new Padding(2)
            };
            button.Tag = iconName;
            button.Image = SvgIconRenderer.Render(ResolveHyrowserIcon(iconName), 18, button.ForeColor);
            button.Click += delegate { if (UiClick != null) UiClick(); };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = iconName == "x.svg" ? Color.FromArgb(176, 48, 62) : Color.FromArgb(31, 40, 57);
            button.FlatAppearance.MouseDownBackColor = iconName == "x.svg" ? Color.FromArgb(142, 36, 49) : Color.FromArgb(41, 52, 72);
            browserTips.SetToolTip(button, tooltip);
            return button;
        }

        private Button CreateTabActionButton(string text, string tooltip, string iconName)
        {
            RoundedButton button = new RoundedButton
            {
                Width = 38,
                Height = 34,
                Text = "",
                ForeColor = Color.FromArgb(178,199,222),
                BackColor = Color.FromArgb(26,36,53),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 1, 7, 1),
                Radius = 10,
                OutlineColor = Color.FromArgb(42, 54, 74),
                OutlineWidth = 1f
            };
            button.Tag = iconName;
            button.Image = SvgIconRenderer.Render(ResolveHyrowserIcon(iconName), 17, button.ForeColor);
            button.Click += delegate { if (UiClick != null) UiClick(); };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 48, 67);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(49, 63, 86);
            browserTips.SetToolTip(button, tooltip);
            return button;
        }

        private Button CreateNavButton(string text, string tooltip, string iconName)
        {
            RoundedButton button = new RoundedButton
            {
                Width = 38,
                Height = 40,
                Text = "",
                ForeColor = Color.FromArgb(178,199,222),
                BackColor = Color.FromArgb(20,28,43),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(1),
                Radius = 9
            };
            button.Tag = iconName;
            button.Image = SvgIconRenderer.Render(ResolveHyrowserIcon(iconName), 18, button.ForeColor);
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(31, 42, 59);
            button.Click += delegate { if (UiClick != null) UiClick(); };
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(42, 55, 76);
            browserTips.SetToolTip(button, tooltip);
            return button;
        }

        private void ApplyAccentToButtons(Control.ControlCollection controls)
        {
            Color activeAccent = ActiveAccentColor();
            foreach (Control control in controls)
            {
                Button button = control as Button;
                if (button != null && button.Tag is string)
                {
                    string iconName = (string)button.Tag;
                    Color iconColor=button==watchButton ? activeAccent : Color.FromArgb(178,199,222);
                    button.ForeColor = iconColor;
                    Image previous=button.Image;
                    button.Image = SvgIconRenderer.Render(ResolveHyrowserIcon(iconName), 18, iconColor);
                    if(previous!=null) previous.Dispose();
                }
                if (control.HasChildren) ApplyAccentToButtons(control.Controls);
            }
        }

        private Color ActiveAccentColor()
        {
            return activeTab != null && activeTab.IsPrivate ? Color.FromArgb(154, 126, 255) : accentColor;
        }

        private Color SoftAccentBorder()
        {
            return activeTab!=null&&activeTab.IsPrivate ? Color.FromArgb(89,72,124) : Color.FromArgb(49,66,88);
        }

        private static string ResolveHyrowserIcon(string iconName)
        {
            string pluginIcon = Path.Combine(WebLauncherForm.AssetsDir, "builtin-plugins", "hyrowser", "plugin asset", iconName);
            if (File.Exists(pluginIcon)) return pluginIcon;
            return Path.Combine(WebLauncherForm.AssetsDir, "icons", iconName);
        }

        private void NavigateFromAddress()
        {
            Navigate(addressBox.Text);
        }

        private async void CreateTab(string url, bool isPrivate, MovieWatchItem movie = null)
        {
            if (!initialized || normalEnvironment == null) return;

            BrowserTab tab = new BrowserTab();
            tab.Id = Guid.NewGuid().ToString("N");
            tab.Title = isPrivate ? "Private tab" : "New tab";
            tab.IsPrivate = isPrivate;
            tab.PendingUrl = url;
            tab.View = new WebView2();
            tab.View.Dock = DockStyle.Fill;
            tab.View.Visible = true;
            tab.View.BackColor = Color.FromArgb(7, 10, 18);

            tabs.Add(tab);
            if (movie != null) SetMovieContext(tab,movie);
            browserHost.Controls.Add(tab.View);
            FitTabView(tab);
            ActivateTab(tab);

            CoreWebView2ControllerOptions controllerOptions = null;
            if (isPrivate)
            {
                controllerOptions = normalEnvironment.CreateCoreWebView2ControllerOptions();
                controllerOptions.IsInPrivateModeEnabled = true;
                controllerOptions.ProfileName = "HYROPrivate";
            }

            try
            {
                if (controllerOptions == null) await tab.View.EnsureCoreWebView2Async(normalEnvironment);
                else await tab.View.EnsureCoreWebView2Async(normalEnvironment, controllerOptions);
            }
            catch
            {
                if (!IsDisposed && tabs.Contains(tab)) { tab.Title = "Unable to open tab"; tab.Loading = false; RenderTabs(); }
                return;
            }
            if (IsDisposed || tab.View.IsDisposed || !tabs.Contains(tab)) return;
            FitTabView(tab);
            tab.View.Visible = tab == activeTab;
            if (tab == activeTab) tab.View.BringToFront();
            tab.View.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            // Style only the page's scrollbars; preserve its content and scrolling.
            try { await tab.View.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BrowserScrollbarScript); } catch { }
            if (IsDisposed || tab.View.IsDisposed || !tabs.Contains(tab)) return;
            tab.View.CoreWebView2.SourceChanged += delegate { SyncBrowserState(tab); };
            tab.View.CoreWebView2.HistoryChanged += delegate { SyncBrowserState(tab); };
            tab.View.CoreWebView2.NavigationStarting += delegate { tab.Loading = true; SyncBrowserState(tab); };
            tab.View.CoreWebView2.NavigationCompleted += delegate { tab.Loading = false; SyncBrowserState(tab); };
            tab.View.KeyDown += delegate(object sender, KeyEventArgs e) {
                Keys keys = e.KeyData;
                if (!IsBrowserShortcut(keys)) return;
                e.Handled = true;
                e.SuppressKeyPress = true;
                BeginInvoke(new Action(delegate { if (!IsDisposed) HandleBrowserShortcut(keys); }));
            };
            tab.View.CoreWebView2.NewWindowRequested += delegate(object sender, CoreWebView2NewWindowRequestedEventArgs e)
            {
                string popupUrl = e.Uri;
                e.Handled = true;
                BeginInvoke(new Action(delegate { CreateTab(popupUrl, tab.IsPrivate); }));
            };
            tab.View.CoreWebView2.DocumentTitleChanged += delegate
            {
                try
                {
                    string docTitle = tab.View.CoreWebView2.DocumentTitle;
                    tab.Title = String.IsNullOrWhiteSpace(docTitle) ? (tab.IsPrivate ? "Private tab" : "New tab") : docTitle;
                    SyncBrowserState(tab);
                    RenderTabs();
                }
                catch { }
            };
            if (IsDisposed || tab.View.IsDisposed || !tabs.Contains(tab)) return;
            Navigate(tab, tab.PendingUrl);
        }

        private void ActivateTab(BrowserTab tab)
        {
            if (tab == null) return;
            if (tab == activeTab)
            {
                RefreshActiveChromeAccent();
                SyncBrowserState(tab);
                RevealActiveTab();
                return;
            }
            BrowserTab previous = activeTab;
            activeTab = tab;
            RefreshActiveChromeAccent();
            browserHost.SuspendLayout();
            try
            {
                if (previous != null && previous.View != null) previous.View.Visible = false;
                if (tab.View != null)
                {
                    FitTabView(tab);
                    tab.View.Visible = true;
                    tab.View.BringToFront();
                }
            }
            finally
            {
                browserHost.ResumeLayout(true);
                browserHost.PerformLayout();
            }
            SyncBrowserState(tab);
            RenderTabs();
            RevealActiveTab();
        }

        private const string BrowserScrollbarScript = @"(()=>{const install=()=>{if(document.getElementById('hyro-scrollbar-style'))return;const style=document.createElement('style');style.id='hyro-scrollbar-style';style.textContent='html{scrollbar-width:thin!important;scrollbar-color:rgba(112,126,145,.55) transparent!important}::-webkit-scrollbar{width:6px!important;height:6px!important;background:transparent!important}::-webkit-scrollbar-track{background:transparent!important}::-webkit-scrollbar-thumb{background:rgba(112,126,145,.55)!important;border-radius:8px!important}::-webkit-scrollbar-thumb:hover{background:rgba(112,126,145,.8)!important}::-webkit-scrollbar-button{display:none!important;width:0!important;height:0!important}';(document.head||document.documentElement).appendChild(style);};if(document.documentElement)install();else document.addEventListener('DOMContentLoaded',install,{once:true});})();";

        private void RevealActiveTab()
        {
            int top=0;
            foreach(BrowserTab tab in tabs)
            {
                if(tab==activeTab) { tabStrip.Reveal(top,TabHeight(tab)); break; }
                top+=TabHeight(tab)+5;
            }
        }

        private void RefreshActiveChromeAccent()
        {
            bool movie=activeTab!=null&&activeTab.Movie!=null;
            watchButton.Text=movie?"Episode +1":"Save movie";
            watchButton.AccessibleName=movie?"Save watched episode and add one":"Save page to Movie Watchlist";
            browserTips.SetToolTip(watchButton,movie?"Save watched episode · Next: "+WebLauncherForm.NextMovieEpisode(activeTab.Movie.Episode):"Save page to Movie Watchlist");
            addressHost.BorderColor = SoftAccentBorder();
            addressHost.Invalidate();
            ApplyAccentToButtons(Controls);
        }

        private void FitTabView(BrowserTab tab)
        {
            if (tab == null || tab.View == null || tab.View.IsDisposed) return;
            Rectangle bounds = browserHost.ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            tab.View.Bounds = bounds;
        }

        private void RenderTabs()
        {
            tabStrip.SuspendLayout();
            try
            {
                for (int i = 0; i < tabs.Count; i++)
                {
                    BrowserTab tab = tabs[i];
                    if (tab.Button == null || tab.Button.IsDisposed)
                    {
                        tab.Button = CreateTabButton(tab);
                        tabStrip.Controls.Add(tab.Button);
                    }
                    UpdateTabButton(tab);
                }
                LayoutTabButtons();
                closeTabButton.Enabled = tabs.Count > 0;
            }
            finally
            {
                tabStrip.ResumeLayout(false);
            }
        }

        private void LayoutTabButtons()
        {
            if(layingOutTabs) return;
            layingOutTabs=true;
            try
            {
                const int gap=5;
                int totalHeight=0;
                foreach(BrowserTab tab in tabs) totalHeight+=TabHeight(tab)+gap;
                tabStrip.Configure(Math.Max(0,totalHeight-gap));
                int tabWidth=Math.Max(20,tabStrip.ClientSize.Width-(totalHeight>tabStrip.ClientSize.Height ? 7 : 0));
                int y=-tabStrip.ScrollOffset;
                foreach(BrowserTab tab in tabs)
                {
                    if(tab.Button==null||tab.Button.IsDisposed) continue;
                    int height=TabHeight(tab);
                    tab.Button.Bounds=new Rectangle(0,y,tabWidth,height);
                    y+=height+gap;
                }
            }
            finally { layingOutTabs=false; }
        }

        private int TabHeight(BrowserTab tab)
        {
            return tab.Movie==null ? 36 : (tabsCollapsed ? 44 : 68);
        }

        private void ToggleTabsSidebar()
        {
            tabsCollapsed=!tabsCollapsed;
            tabsHost.SuspendLayout();
            tabsHost.Width=tabsCollapsed ? 60 : 184;
            tabsLabel.Visible=!tabsCollapsed;
            toggleTabsButton.Dock=tabsCollapsed ? DockStyle.Fill : DockStyle.Right;
            toggleTabsButton.Text=tabsCollapsed ? "›" : "‹";
            toggleTabsButton.AccessibleName=tabsCollapsed ? "Expand tabs" : "Collapse tabs";
            browserTips.SetToolTip(toggleTabsButton,toggleTabsButton.AccessibleName);
            tabActions.FlowDirection=tabsCollapsed ? FlowDirection.TopDown : FlowDirection.LeftToRight;
            tabsLayout.RowStyles[1].Height=tabsCollapsed ? 76F : 38F;
            foreach(Button action in new Button[] {newTabButton,privateTabButton})
            {
                action.Width=tabsCollapsed ? 40 : 80;
                action.Text=tabsCollapsed ? "" : (action==newTabButton ? "New tab" : "Private");
                action.ImageAlign=tabsCollapsed ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
                action.TextImageRelation=tabsCollapsed ? TextImageRelation.Overlay : TextImageRelation.ImageBeforeText;
            }
            tabsHost.ResumeLayout(true);
            RenderTabs();
            RevealActiveTab();
        }

        private RoundedButton CreateTabButton(BrowserTab tab)
        {
            RoundedButton button = new BrowserTabButton
            {
                Width = tab.IsPrivate ? 174 : 154,
                Height = 35,
                Text = "",
                ForeColor = Color.FromArgb(205, 213, 228),
                BackColor = Color.FromArgb(15, 21, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 30, 0),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 7, 0),
                Radius = 8,
                OutlineColor = Color.FromArgb(37, 48, 67),
                OutlineWidth = 1f
            };
            button.Tag = tab;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 59, 78);
            button.Click += delegate { if (UiClick != null) UiClick(); ActivateTab(tab); };
            button.MouseUp += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Middle) CloseTab(tab); };
            button.MouseWheel += delegate(object sender,MouseEventArgs e) { tabStrip.ScrollWheel(e.Delta); var handled=e as HandledMouseEventArgs; if(handled!=null) handled.Handled=true; };
            var close = new Button { Text = "×", Size = new Size(22, 23), Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.FromArgb(195, 205, 220), Cursor = Cursors.Hand, TabStop = false, AccessibleName = "Close tab" };
            close.FlatAppearance.BorderSize = 0;
            button.Controls.Add(close);
            close.Location = new Point(button.Width - 26, (button.Height - close.Height) / 2);
            button.Resize += delegate { close.Location = new Point(button.Width - 26, (button.Height - close.Height) / 2); };
            close.Click += delegate { CloseTab(tab); };
            browserTips.SetToolTip(close, "Close tab · Ctrl+W");
            return button;
        }

        private void UpdateTabButton(BrowserTab tab)
        {
            RoundedButton button = tab == null ? null : tab.Button;
            if (button == null || button.IsDisposed) return;
            bool isActive = tab == activeTab;
            var painted = button as BrowserTabButton;
            button.Text = "";
            if(painted!=null)
            {
                painted.Compact=tabsCollapsed;
                painted.Caption = tabsCollapsed ? (tabs.IndexOf(tab)+1).ToString() : (tab.Movie==null?(tab.IsPrivate?"Private · ":"")+tab.Title:tab.Movie.Title);
                painted.Subtitle = tab.Movie==null||tabsCollapsed?"":"Episode "+(String.IsNullOrWhiteSpace(tab.Movie.Episode)?"—":tab.Movie.Episode);
                painted.Poster = tab.Poster;
            }
            foreach(Control child in button.Controls) child.Visible=!tabsCollapsed;
            button.ForeColor = isActive ? Color.White : Color.FromArgb(190, 200, 216);
            button.BackColor = isActive ? Color.FromArgb(38,55,76) : Color.FromArgb(23,33,49);
            button.OutlineColor = isActive
                ? (tab.IsPrivate ? Color.FromArgb(142, 104, 255) : accentColor)
                : Color.FromArgb(34,47,65);
            button.Invalidate();
            browserTips.SetToolTip(button, (tab.IsPrivate ? "Private · " : "") + (tab.Movie==null ? tab.Title : tab.Movie.Title+" · Episode "+tab.Movie.Episode));
        }

        private static string ShortTitle(string value)
        {
            value = String.IsNullOrWhiteSpace(value) ? "New tab" : value.Trim();
            return value.Length > 18 ? value.Substring(0, 17) + "..." : value;
        }

        private void CloseActiveTab()
        {
            CloseTab(activeTab);
        }
        private void CloseTab(BrowserTab closing)
        {
            if (closing == null || !tabs.Contains(closing)) return;
            bool wasActive = closing == activeTab;
            int index = tabs.IndexOf(closing);
            tabs.Remove(closing);
            DisposeTab(closing);
            if (wasActive) activeTab = null;
            if (tabs.Count == 0)
            {
                Close();
                return;
            }
            if (index >= tabs.Count) index = tabs.Count - 1;
            if (wasActive) ActivateTab(tabs[index]); else RenderTabs();
        }

        private void CloseAllTabs()
        {
            BrowserTab[] copy = tabs.ToArray();
            tabs.Clear();
            foreach (BrowserTab tab in copy) DisposeTab(tab);
        }

        private void DisposeTab(BrowserTab tab)
        {
            try
            {
                if (tab.Button != null)
                {
                    tabStrip.Controls.Remove(tab.Button);
                    tab.Button.Dispose();
                    tab.Button = null;
                }
                if (tab.View != null)
                {
                    browserHost.Controls.Remove(tab.View);
                    tab.View.Dispose();
                }
                if(tab.Poster!=null) { tab.Poster.Dispose(); tab.Poster=null; }
            }
            catch { }
        }

        private void Navigate(string input)
        {
            string url = NormalizeBrowserInput(input);
            Navigate(activeTab, url);
        }

        private void Navigate(BrowserTab tab, string input)
        {
            string url = NormalizeBrowserInput(input);
            if (tab == null || tab.View == null || tab.View.CoreWebView2 == null || String.IsNullOrWhiteSpace(url)) return;
            if (tab == activeTab) addressBox.Text = url;
            tab.View.CoreWebView2.Navigate(url);
        }

        private void Reload()
        {
            try
            {
                if (ActiveCore() == null) return;
                if (activeTab.Loading) ActiveCore().Stop(); else ActiveCore().Reload();
            }
            catch { }
        }

        private void OpenExternal()
        {
            try
            {
                string url = activeTab == null || activeTab.View == null || activeTab.View.Source == null ? addressBox.Text : activeTab.View.Source.ToString();
                url = NormalizeBrowserInput(url);
                if (String.IsNullOrWhiteSpace(url)) return;
                ProcessStartInfo info = new ProcessStartInfo(url);
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch { }
        }

        private void SyncBrowserState(BrowserTab tab)
        {
            try
            {
                if (tab == null || tab.View == null || tab != activeTab) return;
                if (tab.View.Source != null && !addressBox.Focused) addressBox.Text = tab.View.Source.ToString();
                CoreWebView2 core = tab.View.CoreWebView2;
                if (core != null)
                {
                    backButton.Enabled = core.CanGoBack;
                    forwardButton.Enabled = core.CanGoForward;
                }
                titleLabel.Text = "HyBrowser" + (tab.IsPrivate ? "  ·  Private" : "") + (tab.Loading ? "  ·  Loading…" : "");
                browserTips.SetToolTip(watchButton,tab.Movie==null?"Save page to Movie Watchlist":"Save watched episode · Next: "+WebLauncherForm.NextMovieEpisode(tab.Movie.Episode));
                watchButton.AccessibleName=tab.Movie==null?"Save page to Movie Watchlist":"Save watched episode and add one";
                watchButton.Text=tab.Movie==null?"Save movie":"Episode +1";
                loadingBar.Visible = tab.Loading;
                string icon = tab.Loading ? "x.svg" : "reload.svg";
                if (Convert.ToString(reloadButton.Tag) != icon)
                {
                    reloadButton.Tag = icon;
                    Image old = reloadButton.Image;
                    reloadButton.Image = SvgIconRenderer.Render(ResolveHyrowserIcon(icon), 18, Color.FromArgb(178,199,222));
                    if (old != null) old.Dispose();
                }
                browserTips.SetToolTip(reloadButton, tab.Loading ? "Stop · Esc" : "Reload · Ctrl+R");
            }
            catch { }
        }

        private CoreWebView2 ActiveCore()
        {
            if (activeTab == null || activeTab.View == null) return null;
            return activeTab.View.CoreWebView2;
        }

        private static string NormalizeBrowserInput(string value)
        {
            value = (value ?? "").Trim();
            if (String.IsNullOrWhiteSpace(value)) return "";
            if (Regex.IsMatch(value, "^[a-zA-Z][a-zA-Z0-9+.-]*:")) return value;
            if (value.IndexOf(' ') < 0 && value.IndexOf('.') > 0) return "https://" + value;
            return "https://www.google.com/search?q=" + Uri.EscapeDataString(value);
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0);
        }
    }

    internal sealed class PluginBrowserWindow : Form
    {
        private readonly WebLauncherForm ownerForm;
        private readonly WebView2 browser = new LauncherWebView();
        private readonly Label titleLabel = new Label();
        private readonly Size defaultDockSize = new Size(520, 640);
        private readonly bool musicPlayer;
        private bool closingWithLauncher;
        private Size rememberedPanelSize = new Size(520, 640);
        private Color accentColor = Color.FromArgb(68, 168, 255);
        private string currentUrl = "";
        private bool docked = true;
        private bool widePanel;
        private Button expandPanelButton;
        private Size compactPanelSize = new Size(520,640);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public PluginBrowserWindow(WebLauncherForm owner) : this(owner,false) { }

        public PluginBrowserWindow(WebLauncherForm owner,bool persistentMusicPlayer)
        {
            ownerForm = owner;
            musicPlayer=persistentMusicPlayer;
            Text = "HYRO Plugin";
            string iconPath = Path.Combine(WebLauncherForm.AssetsDir, "app.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(10, 13, 23);
            Size = defaultDockSize;
            MinimumSize = new Size(420, 320);
            Resize += delegate { WebLauncherForm.ApplyRoundedRegion(this, 18); };
            Shown += delegate { WebLauncherForm.ApplyRoundedRegion(this, 18); };

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Color.FromArgb(18, 22, 33)
            };
            header.MouseDown += BeginDrag;

            titleLabel.Dock = DockStyle.Fill;
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Padding = new Padding(14, 0, 0, 0);
            titleLabel.MouseDown += BeginDrag;
            header.Controls.Add(titleLabel);

            Button closeButton = CreateHeaderButton(musicPlayer ? "Hide player" : "Close", "close.svg");
            closeButton.Click += delegate { HideByUser(); };
            header.Controls.Add(closeButton);

            Button reloadButton = CreateHeaderButton("Reload", "reload.svg");
            reloadButton.Click += delegate { ReloadPlugin(); };
            header.Controls.Add(reloadButton);

            Button externalButton = CreateHeaderButton("Open external", "open-external-link.svg");
            externalButton.Click += delegate { OpenExternal(); };
            header.Controls.Add(externalButton);

            Button dockButton = CreateHeaderButton("Dock", "dock.svg");
            dockButton.Click += delegate { DockToOwner(); };
            header.Controls.Add(dockButton);

            expandPanelButton=CreateHeaderButton("Expand / restore panel", "dock.svg");
            expandPanelButton.Visible=false;
            expandPanelButton.Click+=delegate { ToggleWidePanel(); };
            header.Controls.Add(expandPanelButton);

            browser.Dock = DockStyle.Fill;
            Controls.Add(browser);
            Controls.Add(header);
            VisibleChanged+=async delegate {await WebLauncherForm.SetViewActivityAsync(browser,Visible&&WindowState!=FormWindowState.Minimized,musicPlayer,musicPlayer);};
            Resize+=async delegate {await WebLauncherForm.SetViewActivityAsync(browser,Visible&&WindowState!=FormWindowState.Minimized,musicPlayer,musicPlayer);};

            ownerForm.LocationChanged += OwnerMoved;
            ownerForm.SizeChanged += OwnerMoved;
            ownerForm.Resize += OwnerResized;
            Resize += RememberPanelSize;
            FormClosed += delegate
            {
                if(musicPlayer) ownerForm.PluginPlaybackStopped();
                ownerForm.LocationChanged -= OwnerMoved;
                ownerForm.SizeChanged -= OwnerMoved;
                ownerForm.Resize -= OwnerResized;
                Resize -= RememberPanelSize;
                try { browser.Dispose(); } catch { }
            };
            FormClosing+=delegate(object sender,FormClosingEventArgs e)
            {
                if(musicPlayer && !closingWithLauncher && e.CloseReason==CloseReason.UserClosing)
                {
                    e.Cancel=true;
                    HideByUser();
                }
            };
        }

        public void CloseForLauncherExit()
        {
            closingWithLauncher=true;
            Close();
        }

        public async Task EnsureWebViewAsync(CoreWebView2Environment environment)
        {
            if (browser.CoreWebView2 != null) return;
            await browser.EnsureCoreWebView2Async(environment);
            ApplyWindowVisuals(ownerForm.CurrentThemeMode());
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            browser.CoreWebView2.Settings.IsZoomControlEnabled = false;
            browser.CoreWebView2.Settings.IsPinchZoomEnabled = false;
            browser.ZoomFactor = 1;
            browser.CoreWebView2.WebMessageReceived += delegate(object sender, CoreWebView2WebMessageReceivedEventArgs e)
            {
                if(!String.Equals(e.Source,browser.CoreWebView2.Source,StringComparison.OrdinalIgnoreCase)) return;
                ownerForm.HandlePluginMessage(e.WebMessageAsJson,this);
            };
            browser.CoreWebView2.ProcessFailed += delegate { if(musicPlayer) ownerForm.PluginPlaybackStopped(); };
            browser.CoreWebView2.NavigationCompleted += delegate { if(musicPlayer) ownerForm.PluginPlaybackStopped(); ownerForm.PluginReady(this); };
        }

        public Task ClearBrowsingDataAsync()
        {
            return WebLauncherForm.ClearBrowsingDataForViewAsync(browser);
        }

        public bool ShowPlugin(string title, string url)
        {
            bool tcgPanel=url.IndexOf("hytcg.html",StringComparison.OrdinalIgnoreCase)>=0;
            if(!tcgPanel && widePanel) {widePanel=false;rememberedPanelSize=compactPanelSize;}
            expandPanelButton.Visible=tcgPanel;
            titleLabel.Text = String.IsNullOrWhiteSpace(title) ? "Plugin" : title;
            if (!Visible || docked) DockToOwner();
            if (!Visible) Show();
            bool navigated=false;
            if (browser.CoreWebView2 != null && !String.Equals(currentUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                browser.CoreWebView2.Navigate(url);
                currentUrl = url;
                navigated=true;
            }
            BringToFront();
            return navigated;
        }

        public void DockToOwner()
        {
            docked = true;
            Rectangle workArea = Screen.FromControl(ownerForm).WorkingArea;
            int gap = 8;
            Size targetSize = widePanel ? new Size(Math.Min(1120,workArea.Width-32),Math.Min(880,workArea.Height-32)) : rememberedPanelSize.Width > 0 && rememberedPanelSize.Height > 0 ? rememberedPanelSize : defaultDockSize;
            int width = Math.Max(MinimumSize.Width, targetSize.Width);
            int height = Math.Max(MinimumSize.Height, targetSize.Height);
            width = Math.Min(width, workArea.Width);
            height = Math.Min(height, workArea.Height);
            int left = ownerForm.Right + gap;
            if (left + width > workArea.Right) left = ownerForm.Left - width - gap;
            if (left < workArea.Left) left = Math.Max(workArea.Left, workArea.Right - width);
            int top = Math.Max(workArea.Top, Math.Min(ownerForm.Top, workArea.Bottom - height));
            Size = new Size(width, height);
            Location = new Point(left, top);
            if (!Visible) Show();
            BringToFront();
        }

        private void RememberPanelSize(object sender, EventArgs e)
        {
            if(widePanel)return;
            if (WindowState != FormWindowState.Normal) return;
            if (Width < MinimumSize.Width || Height < MinimumSize.Height) return;
            rememberedPanelSize = Size;
        }

        internal void ToggleWidePanel()
        {
            if(currentUrl.IndexOf("hytcg.html",StringComparison.OrdinalIgnoreCase)<0)return;
            if(!widePanel)compactPanelSize=Size;
            widePanel=!widePanel;rememberedPanelSize=compactPanelSize;DockToOwner();
        }

        public void ReloadPlugin()
        {
            if(musicPlayer) { ownerForm.PluginReady(this); return; }
            try
            {
                if (browser.CoreWebView2 == null || String.IsNullOrWhiteSpace(currentUrl)) return;
                try { browser.CoreWebView2.Reload(); }
                catch { browser.CoreWebView2.Navigate(currentUrl); }
            }
            catch { }
        }

        internal static string LocalPanelUrl(string fileName,string pluginId)
        {
            string url=new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui",fileName)).AbsoluteUri+"?id="+Uri.EscapeDataString(pluginId);
            if(fileName=="hytcg.html" && pluginId=="builtin.hytcg") url+="&v="+typeof(PluginBrowserWindow).Assembly.ManifestModule.ModuleVersionId.ToString("N");
            return url;
        }

        internal bool IsLocalPanel(string fileName,string pluginId)
        {
            if(browser.CoreWebView2==null) return false;
            string expected=LocalPanelUrl(fileName,pluginId);
            return String.Equals(browser.CoreWebView2.Source,expected,StringComparison.OrdinalIgnoreCase);
        }

        public void Post(Dictionary<string, object> payload)
        {
            try
            {
                if (browser.CoreWebView2 == null) return;
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength=20*1024*1024 };
                browser.CoreWebView2.PostWebMessageAsJson(serializer.Serialize(payload));
            }
            catch { }
        }

        private void HideByUser()
        {
            if(!musicPlayer) currentUrl = "";
            Hide();
            ownerForm.PluginClosedByUser(this);
        }

        private void OpenExternal()
        {
            if (String.IsNullOrWhiteSpace(currentUrl)) return;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(currentUrl);
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch { }
        }

        private Button CreateHeaderButton(string tooltip, string iconName)
        {
            Button button = new Button
            {
                Dock = DockStyle.Right,
                Width = 38,
                Height = 46,
                Text = "",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(18, 22, 33),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                ImageAlign = ContentAlignment.MiddleCenter,
                TextImageRelation = TextImageRelation.Overlay
            };
            button.Tag = iconName;
            button.ForeColor = accentColor;
            button.Image = SvgIconRenderer.Render(Path.Combine(WebLauncherForm.AssetsDir, "icons", iconName), 18, accentColor);
            button.Click += delegate { ownerForm.PlayUiClick(); };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 255, 255, 255);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 255, 255, 255);
            ToolTip tip = new ToolTip();
            tip.SetToolTip(button, tooltip);
            return button;
        }

        public void SetAccent(Color color)
        {
            accentColor = color;
            ApplyAccentToButtons(Controls);
        }

        public void ApplyWindowVisuals(string themeMode)
        {
            bool translucent = String.Equals(WebLauncherForm.NormalizeThemeModeForWindows(themeMode), "translucent", StringComparison.OrdinalIgnoreCase);
            Color keyColor = Color.FromArgb(1, 2, 3);
            BackColor = translucent ? keyColor : Color.FromArgb(10, 13, 23);
            TransparencyKey = translucent ? keyColor : Color.Empty;
            try { browser.DefaultBackgroundColor = translucent ? Color.Transparent : Color.FromArgb(11, 15, 25); } catch { }
            WebLauncherForm.ApplyRoundedRegion(this, 18);
        }

        private void ApplyAccentToButtons(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                Button button = control as Button;
                if (button != null && button.Tag is string)
                {
                    string iconName = (string)button.Tag;
                    button.ForeColor = accentColor;
                    button.Image = SvgIconRenderer.Render(Path.Combine(WebLauncherForm.AssetsDir, "icons", iconName), 18, accentColor);
                }
                if (control.HasChildren) ApplyAccentToButtons(control.Controls);
            }
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            docked = false;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0);
        }

        private void OwnerMoved(object sender, EventArgs e)
        {
            if (ownerForm.WindowState == FormWindowState.Minimized) return;
            if (docked && Visible) DockToOwner();
        }

        private void OwnerResized(object sender, EventArgs e)
        {
            if (ownerForm.WindowState == FormWindowState.Minimized) return;
            if (docked && !Visible && !String.IsNullOrWhiteSpace(currentUrl)) Show();
            if (docked && Visible) DockToOwner();
        }
    }

    internal static class SvgIconRenderer
    {
        public static Bitmap Render(string path, int size, Color color)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Pen pen = new Pen(color, Math.Max(1.6f, size / 12f)))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                string name = Path.GetFileNameWithoutExtension(path ?? "").ToLowerInvariant();
                if (name == "close" || name == "x") DrawClose(g, pen, size);
                else if (name == "reload") DrawReload(g, pen, size);
                else if (name == "open-external-link" || name == "open") DrawOpenExternal(g, pen, size);
                else if (name == "dock") DrawDock(g, pen, size);
                else if (name == "add") DrawAdd(g, pen, size);
                else if (name == "minimize" || name == "minimize-2") DrawMinimize(g, pen, size);
                else if (name == "maximize-2") DrawMaximize(g, pen, size);
                else if (name == "home") DrawHome(g, pen, size);
                else if (name == "undo") DrawBack(g, pen, size);
                else if (name == "redo") DrawForward(g, pen, size);
                else if (name == "go") DrawGo(g, pen, size);
                else if (name == "private") DrawPrivate(g, pen, size);
                else if (name == "close-tab") DrawCloseTab(g, pen, size);
                else if (name == "search") DrawSearch(g, pen, size);
                else if (name == "bookmark-plus") DrawBookmarkPlus(g,pen,size);
                else DrawFallback(g, pen, size);
            }
            return bitmap;
        }

        private static float S(int size, float value)
        {
            return size * value / 24f;
        }

        private static void DrawBookmarkPlus(Graphics g,Pen pen,int size)
        {
            g.DrawLines(pen,new PointF[] {new PointF(S(size,13),S(size,3)),new PointF(S(size,5),S(size,3)),new PointF(S(size,5),S(size,21)),new PointF(S(size,12),S(size,17)),new PointF(S(size,19),S(size,21)),new PointF(S(size,19),S(size,12))});
            g.DrawLine(pen,S(size,18),S(size,3),S(size,18),S(size,9));
            g.DrawLine(pen,S(size,15),S(size,6),S(size,21),S(size,6));
        }

        private static void DrawClose(Graphics g, Pen pen, int size)
        {
            g.DrawLine(pen, S(size, 6), S(size, 6), S(size, 18), S(size, 18));
            g.DrawLine(pen, S(size, 18), S(size, 6), S(size, 6), S(size, 18));
        }

        private static void DrawReload(Graphics g, Pen pen, int size)
        {
            // Match reload.svg: a counterclockwise loop ending at the upper-left arrow.
            using (GraphicsPath loop = new GraphicsPath())
            {
                loop.AddBezier(S(size, 3), S(size, 12), S(size, 3), S(size, 17), S(size, 7), S(size, 21), S(size, 12), S(size, 21));
                loop.AddBezier(S(size, 12), S(size, 21), S(size, 17), S(size, 21), S(size, 21), S(size, 17), S(size, 21), S(size, 12));
                loop.AddBezier(S(size, 21), S(size, 12), S(size, 21), S(size, 7), S(size, 17), S(size, 3), S(size, 12), S(size, 3));
                loop.AddBezier(S(size, 12), S(size, 3), S(size, 8), S(size, 3), S(size, 5.5f), S(size, 5.5f), S(size, 3), S(size, 8));
                g.DrawPath(pen, loop);
            }
            g.DrawLines(pen, new PointF[] { new PointF(S(size, 3), S(size, 3)), new PointF(S(size, 3), S(size, 8)), new PointF(S(size, 8), S(size, 8)) });
        }

        private static void DrawOpenExternal(Graphics g, Pen pen, int size)
        {
            g.DrawLine(pen, S(size, 17), S(size, 12), S(size, 17), S(size, 18));
            g.DrawArc(pen, S(size, 5), S(size, 8), S(size, 12), S(size, 11), 0, 90);
            g.DrawArc(pen, S(size, 5), S(size, 8), S(size, 12), S(size, 11), 90, 180);
            g.DrawLine(pen, S(size, 11), S(size, 13), S(size, 19), S(size, 5));
            g.DrawLine(pen, S(size, 14), S(size, 5), S(size, 19), S(size, 5));
            g.DrawLine(pen, S(size, 19), S(size, 5), S(size, 19), S(size, 10));
        }

        private static void DrawDock(Graphics g, Pen pen, int size)
        {
            RectangleF rect = new RectangleF(S(size, 4), S(size, 5), S(size, 16), S(size, 11));
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            g.DrawLine(pen, S(size, 3), S(size, 20), S(size, 21), S(size, 20));
        }

        private static void DrawAdd(Graphics g, Pen pen, int size)
        {
            g.DrawLine(pen, S(size, 12), S(size, 6), S(size, 12), S(size, 18));
            g.DrawLine(pen, S(size, 6), S(size, 12), S(size, 18), S(size, 12));
        }

        private static void DrawMinimize(Graphics g, Pen pen, int size)
        {
            g.DrawLine(pen, S(size, 7), S(size, 12), S(size, 17), S(size, 12));
        }

        private static void DrawMaximize(Graphics g, Pen pen, int size)
        {
            RectangleF rect = new RectangleF(S(size, 6), S(size, 6), S(size, 12), S(size, 12));
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        private static void DrawHome(Graphics g, Pen pen, int size)
        {
            PointF[] roof = new PointF[]
            {
                new PointF(S(size, 4), S(size, 11)),
                new PointF(S(size, 12), S(size, 5)),
                new PointF(S(size, 20), S(size, 11))
            };
            g.DrawLines(pen, roof);
            g.DrawRectangle(pen, S(size, 7), S(size, 11), S(size, 10), S(size, 8));
        }

        private static void DrawBack(Graphics g, Pen pen, int size)
        {
            g.DrawLine(pen, S(size, 15), S(size, 6), S(size, 9), S(size, 12));
            g.DrawLine(pen, S(size, 9), S(size, 12), S(size, 15), S(size, 18));
        }

        private static void DrawForward(Graphics g, Pen pen, int size)
        {
            g.DrawLine(pen, S(size, 9), S(size, 6), S(size, 15), S(size, 12));
            g.DrawLine(pen, S(size, 15), S(size, 12), S(size, 9), S(size, 18));
        }

        private static void DrawGo(Graphics g, Pen pen, int size)
        {
            g.DrawLine(pen, S(size, 7), S(size, 12), S(size, 17), S(size, 12));
            g.DrawLine(pen, S(size, 13), S(size, 8), S(size, 17), S(size, 12));
            g.DrawLine(pen, S(size, 13), S(size, 16), S(size, 17), S(size, 12));
        }

        private static void DrawPrivate(Graphics g, Pen pen, int size)
        {
            g.DrawLine(pen, S(size, 7), S(size, 9), S(size, 17), S(size, 9));
            g.DrawLine(pen, S(size, 9), S(size, 9), S(size, 10), S(size, 5));
            g.DrawLine(pen, S(size, 14), S(size, 5), S(size, 15), S(size, 9));
            g.DrawEllipse(pen, S(size, 5), S(size, 13), S(size, 6), S(size, 5));
            g.DrawEllipse(pen, S(size, 13), S(size, 13), S(size, 6), S(size, 5));
            g.DrawLine(pen, S(size, 11), S(size, 15.5f), S(size, 13), S(size, 15.5f));
        }

        private static void DrawCloseTab(Graphics g, Pen pen, int size)
        {
            RectangleF rect = new RectangleF(S(size, 4), S(size, 6), S(size, 16), S(size, 12));
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            g.DrawLine(pen, S(size, 9), S(size, 10), S(size, 15), S(size, 16));
            g.DrawLine(pen, S(size, 15), S(size, 10), S(size, 9), S(size, 16));
        }

        private static void DrawSearch(Graphics g, Pen pen, int size)
        {
            g.DrawEllipse(pen, S(size, 5), S(size, 5), S(size, 10), S(size, 10));
            g.DrawLine(pen, S(size, 14), S(size, 14), S(size, 20), S(size, 20));
        }

        private static void DrawFallback(Graphics g, Pen pen, int size)
        {
            g.DrawEllipse(pen, S(size, 5), S(size, 5), S(size, 14), S(size, 14));
        }
    }
}
