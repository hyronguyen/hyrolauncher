using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal sealed class LifestyleWebViewSmoke : Form
{
    readonly WebView2 view = new WebView2();
    readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 16000000 };
    readonly string deployed, output, image;
    readonly List<string> requests = new List<string>();
    readonly List<string> paidOperations = new List<string>();
    readonly Dictionary<string, int> materials = new Dictionary<string, int> { { "fiber", 10 }, { "petal", 10 }, { "crystal", 10 } };
    readonly Dictionary<string, string> previews = new Dictionary<string, string>();
    int checks, drawings, fixtureGems = 6000, dyeCount, sequence = 1, draftPreviewRequests, savedItemPreviewRequests, artworkPixels;
    object lastReceipt;
    string savedFrame = "classic", savedFrameItem = "", savedDye = "", savedArtwork = "";
    Dictionary<string, object> frameItem;
    bool draftLandscape, frameAvailable;
    bool navigationReady;
    string session;

    LifestyleWebViewSmoke(string deployedFolder, string outputFolder)
    {
        deployed = deployedFolder; output = outputFolder;
        image = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(deployed, "assets", "default-logo.png")));
        string verification = Path.GetDirectoryName(output);
        string previewFolder = null;
        foreach (string folder in Directory.GetDirectories(verification, "hytcg-cosmetic-renderer-*"))
            if (File.Exists(Path.Combine(folder, "template-moonlace-portrait.png")) && (previewFolder == null || Directory.GetLastWriteTimeUtc(folder) > Directory.GetLastWriteTimeUtc(previewFolder))) previewFolder = folder;
        if (previewFolder != null) foreach (string frame in new[] { "classic", "petal", "arcane", "circuit", "royal", "moonlace", "starlit", "sakura", "obsidian", "gilded" })
        {
            string file = Path.Combine(previewFolder, frame == "classic" || frame == "petal" || frame == "arcane" || frame == "circuit" || frame == "royal" ? frame + ".png" : "template-" + frame + "-portrait.png");
            if (File.Exists(file)) previews[frame] = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(file));
        }
        Text = "Isolated HYTCG WebView2 verification";
        ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        Location = new Point(-20000, -20000); ClientSize = new Size(500, 840); Opacity = 0;
        view.Dock = DockStyle.Fill; Controls.Add(view);
        Shown += async delegate { await Verify(); };
    }

    object Wallet() { return new { Gems = fixtureGems, FreeCredits = 5, PaidCredits = 0, PackPrice = 160, LimitedPackPrice = 180, MaxFreeCredits = 5, ServerNow = DateTime.UtcNow.ToString("o"), NextFreeAt = DateTime.UtcNow.AddHours(1).ToString("o") }; }
    object Card() { return new { CardId = "HT-WEBVIEW-FIXTURE", Name = "WebView fixture", Series = "Isolated runtime", Rarity = "SSR", CharacterId = 200, Orientation = "portrait", Favorite = false }; }
    object Profile() { return new { CardId = "HT-WEBVIEW-FIXTURE", Name = "WebView fixture", Rarity = "SSR", CharacterId = 200, Level = 1, Xp = 0, NextLevelXp = 100, Effort = 25, Attack = 80, Defense = 20, Hp = 130, Bond = 0, BondLevel = 0, Busy = false, DatesRemaining = 2 }; }
    object Dye(int index, int count)
    {
        int[] channels = { 64, 128, 192, 255 };
        string color = "#" + channels[(index >> 4) & 3].ToString("X2") + channels[(index >> 2) & 3].ToString("X2") + channels[index & 3].ToString("X2");
        return new { Id = "dye-" + index.ToString("D2"), Name = color, Color = color, Mystic = false, Count = count };
    }
    object Frame(string id, string name, int price, string first, int count, string second = "", int secondCount = 0)
    {
        var cost = new List<object>();
        if (first != "") cost.Add(new { Id = first, Name = first == "fiber" ? "Frame fiber" : first == "petal" ? "Sakura petals" : "Arcane crystal", Count = count });
        if (second != "") cost.Add(new { Id = second, Name = second == "fiber" ? "Frame fiber" : second == "petal" ? "Sakura petals" : "Arcane crystal", Count = secondCount });
        return new { Id = id, Name = name, Description = "Dedicated artwork window and identity layout.", Design = id, PriceGems = price, GemPrice = price, Materials = cost };
    }
    object Creative()
    {
        var palette = new List<object>(); for (int i = 0; i < 64; i++) palette.Add(Dye(i, 0));
        return new {
            Albums = new object[] { new { Id = "AL-webview-fixture", Name = "Runtime album", Background = "aurora", CardIds = new[] { "HT-WEBVIEW-FIXTURE" } } },
            Cosmetics = savedFrame == "classic" && savedDye == "" && savedArtwork == "" ? new object[0] : new object[] { new { CardId = "HT-WEBVIEW-FIXTURE", Frame = savedFrame, FrameItemId = savedFrameItem, Dye = savedDye == "" ? "" : "#4040FF", DyeId = savedDye, Mystic = false, ArtworkId = savedArtwork, Morph = "", MorphMode = "none" } },
            FrameCatalog = new object[] { Frame("classic", "Classic", 0, "", 0), Frame("petal", "Petal", 80, "fiber", 1), Frame("arcane", "Arcane", 120, "crystal", 1), Frame("circuit", "Circuit", 100, "fiber", 1), Frame("royal", "Royal", 160, "petal", 1, "crystal", 1), Frame("moonlace", "Moonlace", 180, "fiber", 4, "petal", 2), Frame("starlit", "Starlit", 220, "fiber", 2, "crystal", 3), Frame("sakura", "Sakura Scroll", 200, "petal", 4, "fiber", 2), Frame("obsidian", "Obsidian Gate", 260, "crystal", 4, "fiber", 2), Frame("gilded", "Gilded Crest", 320, "crystal", 4, "petal", 4) },
            FrameItems = frameItem == null ? new object[0] : new object[] { frameItem }, FrameCreationPrice = 2000, FrameCreationMaterials = new[] { new { Id = "fiber", Count = 10 }, new { Id = "petal", Count = 5 }, new { Id = "crystal", Count = 5 } },
            DyePalette = palette, Dyes = dyeCount > 0 ? new object[] { Dye(3, dyeCount) } : new object[0], DyeBoosterPrice = 50, MysticChancePercent = 1, NextStudioSequence = sequence, LastStudioReceipt = lastReceipt, RestorableLooks = new object[0]
        };
    }
    Dictionary<string, object> Snapshot()
    {
        return new Dictionary<string, object> {
            {"creative", Creative()},
            {"roster", new[] { Card() }}, {"wallet", Wallet()}, {"serverNow", DateTime.UtcNow.ToString("o")},
            {"gameplay", new { Day = "2026-10-10", ServerNow = DateTime.UtcNow.ToString("o"), NextResetAt = DateTime.UtcNow.AddHours(1).ToString("o"), ExpeditionStartsRemaining = 6, ExpeditionActiveLimit = 3, DateDailyRemaining = 6, Profiles = new[] { Profile() }, Routes = new[] { new { Id = "meadow", Name = "Moonlit meadow", DurationSeconds = 300, MinEffort = 5, RewardGems = 8, RewardXp = 20, RewardItems = new object[0] } }, Expeditions = new object[0], Inventory = new[] { new { Id = "training", Name = "Training manual", Kind = "training", Count = 3 }, new { Id = "fiber", Name = "Frame fiber", Kind = "material", Count = materials["fiber"] }, new { Id = "petal", Name = "Sakura petals", Kind = "material", Count = materials["petal"] }, new { Id = "crystal", Name = "Arcane crystal", Kind = "material", Count = materials["crystal"] } }, Fight = new { Stage = 1, MaxStage = 20, DailyRemaining = 3, EnemyName = "Training sentinel", EnemyHp = 145, EnemyAttack = 17, EnemyDefense = 8 } }}
        };
    }
    void Reply(Dictionary<string, object> reply) { view.CoreWebView2.PostWebMessageAsJson(json.Serialize(reply)); }
    void Message(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = json.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
        string type = Convert.ToString(message["type"]);
        if (message.ContainsKey("session")) session = Convert.ToString(message["session"]);
        if (type == "ready") return;
        if (type == "hytcgInit")
        {
            Reply(new Dictionary<string, object> { { "type", "state" }, { "state", new { Settings = new { Language = "en", ThemeMode = "dark", AccentColor = "#c49bf7" } } } });
            Reply(new Dictionary<string, object> { { "type", "hytcgConfig" }, { "session", session }, { "config", new { Concurrency = 2, DropRates = new[] { new { Name = "SSR", Color = "#c49bf7", Weight = 1 } } } }, { "wallet", Wallet() } });
            return;
        }
        var reply = new Dictionary<string, object>(message);
        reply["wallet"] = Wallet();
        if (type == "hytcgLifestyle")
        {
            string action = Convert.ToString(message["action"]); requests.Add(action);
            if (action == "dyeBoosterBuy" || action == "frameCreate" || action == "cosmeticSave")
            {
                string operation = Convert.ToString(message["operationId"]);
                if (!System.Text.RegularExpressions.Regex.IsMatch(operation, "^SC-" + sequence + "-[a-f0-9]{32}$")) throw new InvalidOperationException("Invalid paid UI sequence: " + operation);
                paidOperations.Add(operation); sequence++;
                int spent = 0;
                if (action == "dyeBoosterBuy") { fixtureGems -= 50; dyeCount++; spent = 50; }
                if (action == "frameCreate")
                {
                    if (Convert.ToString(message["frameId"]) != "moonlace" || !message.ContainsKey("portrait") || !message.ContainsKey("landscape")) throw new InvalidOperationException("Unexpected fixture frame creation.");
                    fixtureGems -= 2000; materials["fiber"] -= 10; materials["petal"] -= 5; materials["crystal"] -= 5; spent = 2000;
                    frameItem = new Dictionary<string, object> { { "Id", "FI-webview-fixture" }, { "FrameId", "moonlace" }, { "Name", message["name"] }, { "Portrait", message["portrait"] }, { "Landscape", message["landscape"] }, { "Description", message["description"] }, { "ArtworkId", "" }, { "Available", true }, { "AppliedCardId", "" } }; frameAvailable = true;
                }
                if (action == "cosmeticSave")
                {
                    savedFrameItem = Convert.ToString(message["frameItemId"]); savedFrame = "moonlace"; savedDye = Convert.ToString(message["dyeId"]); savedArtwork = Convert.ToString(message["artworkId"]);
                    if (savedFrameItem != "FI-webview-fixture" || savedDye != "dye-03" || savedArtwork != "isolated-webview-artwork" || dyeCount < 1 || !frameAvailable) throw new InvalidOperationException("Paid look did not cross the native JSON bridge correctly.");
                    spent = Math.Max(1, (int)Math.Ceiling(artworkPixels / 10000d)); fixtureGems -= spent; dyeCount--; frameAvailable = false; frameItem["Available"] = false; frameItem["AppliedCardId"] = "HT-WEBVIEW-FIXTURE";
                }
                lastReceipt = new { OperationId = operation, Action = action, DyeId = action == "dyeBoosterBuy" ? "dye-03" : savedDye, FrameId = action == "frameCreate" ? "moonlace" : savedFrame, FrameItemId = action == "frameCreate" ? "FI-webview-fixture" : savedFrameItem, CardId = action == "cosmeticSave" ? "HT-WEBVIEW-FIXTURE" : "", GemsSpent = spent, NoOp = false };
            }
            foreach (var entry in Snapshot()) reply[entry.Key] = entry.Value;
            if (action == "profile") { reply["profile"] = Profile(); reply["card"] = Card(); }
            if (action == "quote")
            {
                bool newArtwork = message.ContainsKey("artworkId") && Convert.ToString(message["artworkId"]) != "" && Convert.ToString(message["artworkId"]) != savedArtwork;
                int cost = newArtwork ? Math.Max(1, (int)Math.Ceiling(artworkPixels / 10000d)) : 0;
                reply["quote"] = new { ArtworkPixels = artworkPixels, ArtworkColors = 1, ArtworkGems = cost, TotalGems = cost, DyeConsumed = message.ContainsKey("dyeId") && Convert.ToString(message["dyeId"]) != "" && Convert.ToString(message["dyeId"]) != savedDye, FrameItemConsumed = message.ContainsKey("frameItemId") && Convert.ToString(message["frameItemId"]) != "" && frameAvailable };
            }
            if (action == "artworkDraw")
            {
                string png = Convert.ToString(message["png"]);
                if (!png.StartsWith("data:image/png;base64,")) throw new InvalidOperationException("Drawing did not cross the native JSON bridge as a PNG.");
                artworkPixels = 0; using (var stream = new MemoryStream(Convert.FromBase64String(png.Substring(22)))) using (var bitmap = new Bitmap(stream)) for (int py = 0; py < bitmap.Height; py++) for (int px = 0; px < bitmap.Width; px++) if (bitmap.GetPixel(px, py).A != 0) artworkPixels++;
                drawings++; reply["artworkId"] = "isolated-webview-artwork"; reply["imageData"] = png;
            }
        }
        else if (type == "hytcgLifestyleImage")
        {
            string frame = message.ContainsKey("previewFrameId") ? Convert.ToString(message["previewFrameId"]) : savedFrame;
            if (message.ContainsKey("previewFrameDraft")) { var draft = (Dictionary<string, object>)message["previewFrameDraft"]; frame = Convert.ToString(draft["frameId"]); if (!draft.ContainsKey("portrait") || !draft.ContainsKey("landscape") || !draft.ContainsKey("description")) throw new InvalidOperationException("Draft layout DTO missing."); draftPreviewRequests++; draftLandscape |= Convert.ToString(message["previewOrientation"]) == "landscape"; }
            if (message.ContainsKey("previewFrameItemId")) savedItemPreviewRequests++;
            reply["imageData"] = previews.ContainsKey(frame) ? previews[frame] : image;
        }
        else if (type == "hytcgCollection") reply["page"] = new { Items = new object[0], Total = 1, HasMore = false };
        else if (type == "hytcgDiscoveryState") { reply["banners"] = new object[0]; reply["missions"] = new object[0]; }
        Reply(reply);
    }
    async Task<string> Script(string code) { return await view.CoreWebView2.ExecuteScriptAsync(code); }
    async Task Wait(string expression, string name)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline)
        {
            if (await Script("Boolean(" + expression + ")") == "true") return;
            await Task.Delay(50);
        }
        throw new InvalidOperationException("Timed out: " + name);
    }
    async Task Check(string expression, string name)
    {
        if (await Script("Boolean(" + expression + ")") != "true") throw new InvalidOperationException(name);
        checks++;
    }
    async Task Mouse(string type, double x, double y)
    {
        var payload = new Dictionary<string, object> { { "type", type }, { "x", x }, { "y", y } };
        if (type != "mouseMoved") { payload["button"] = "left"; payload["clickCount"] = 1; }
        await view.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", json.Serialize(payload));
    }
    async Task Verify()
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(output, "isolated-webview-profile"), null);
            await view.EnsureCoreWebView2Async(environment);
            view.CoreWebView2.Settings.AreDevToolsEnabled = false;
            view.CoreWebView2.WebMessageReceived += Message;
            view.CoreWebView2.NavigationCompleted += delegate(object sender, CoreWebView2NavigationCompletedEventArgs e) { navigationReady = e.IsSuccess; };
            view.CoreWebView2.AddWebResourceRequestedFilter("https://*", CoreWebView2WebResourceContext.All);
            view.CoreWebView2.AddWebResourceRequestedFilter("http://*", CoreWebView2WebResourceContext.All);
            view.CoreWebView2.WebResourceRequested += delegate(object sender, CoreWebView2WebResourceRequestedEventArgs e) { e.Response = environment.CreateWebResourceResponse(new MemoryStream(new byte[0]), 403, "Isolated fixture blocks network", "Content-Type: text/plain"); };
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.webviewSmoke={errors:[],csp:[]};addEventListener('error',e=>webviewSmoke.errors.push(e.message));addEventListener('unhandledrejection',e=>webviewSmoke.errors.push(String(e.reason)));addEventListener('securitypolicyviolation',e=>webviewSmoke.csp.push(e.violatedDirective+':'+e.blockedURI));");
            view.CoreWebView2.Navigate(new Uri(Path.Combine(deployed, "ui", "hytcg.html")).AbsoluteUri);
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (!navigationReady && DateTime.UtcNow < deadline) await Task.Delay(50);
            if (!navigationReady) throw new InvalidOperationException("Deployed HTML navigation failed."); checks++;
            await Wait("window.HyTcgLifestyle && tcg.config", "deployed scripts/config"); checks++;
            await Script("document.getElementById('lifestyleTab').click()");
            await Wait("document.querySelector('.albumClosed')", "native snapshot reply/album");
            await Check("tcg.view==='lifestyle' && document.querySelectorAll('#lifestyleNav button').length===6", "Atelier navigation failed.");
            foreach (string tab in new[] { "studio", "expedition", "inventory", "arena", "dating", "album" })
            {
                await Script("document.querySelector('[data-life-view=\"" + tab + "\"]').click()");
                await Check("document.querySelector('#lifestyleContent .lifeSection') && document.querySelector('[data-life-view=\"" + tab + "\"]').classList.contains('active')", "Atelier tab failed: " + tab);
            }
            await Script("document.querySelector('[data-life-view=\"inventory\"]').click()");
            await Check("document.querySelector('.lifeDyeBooster').textContent.includes('64 colors') && document.querySelector('[data-life-action=\"dyeBoosterBuy\"]').textContent.includes('50')", "Paid dye booster price/palette failed.");
            await Check("document.querySelector('[data-life-item=\"training\"]') && document.querySelector('[data-life-item=\"fiber\"]') && document.querySelectorAll('[data-life-inventory-filter]').length===5", "Unified inventory items/filters failed.");
            await Script("document.querySelector('#boosterTab').click()");
            await Wait("!document.querySelector('#dyeBoosterMount button').disabled", "Booster enable after refresh");
            await Script("document.querySelector('#dyeBoosterMount [data-life-action=\"dyeBoosterBuy\"]').click()");
            await Wait("tcg.wallet.Gems===5950", "Dye booster native reply/wallet"); checks++;
            await Script("document.querySelector('[data-life-view=\"inventory\"]').click()");
            await Wait("!document.querySelector('[data-life-action=\"createFrame\"]').disabled", "Inventory ready for frame creation");
            await Script("document.querySelector('[data-life-action=\"createFrame\"]').click()");
            await Check("document.querySelectorAll('#lifeFrameTemplate option').length===10 && !document.querySelector('[data-life-studio-panel=\"shop\"]') && document.querySelector('.lifeFrameRecipe').textContent.includes('2,000')", "Frame creator/template/price failed.");
            await Script("document.querySelector('#lifeFrameTemplate').value='moonlace';document.querySelector('#lifeFrameTemplate').dispatchEvent(new Event('change',{bubbles:true}));document.querySelector('#lifeFrameName').value='WebView frame';document.querySelector('#lifeFrameName').dispatchEvent(new Event('input',{bubbles:true}));document.querySelector('#lifeFrameDescription').value='Custom layout';document.querySelector('#lifeFrameDescription').dispatchEvent(new Event('input',{bubbles:true}));");
            await Wait("document.querySelector('#lifeFrameComposite').naturalWidth>0", "Native draft image preview");
            await Script("document.querySelector('[data-life-region=\"Name\"]').scrollIntoView({block:'center'})");
            var region = json.Deserialize<Dictionary<string, object>>(await Script("(()=>{const r=document.querySelector('[data-life-region=\"Name\"]').getBoundingClientRect();return {x:r.left+15,y:r.top+8,left:parseFloat(document.querySelector('[data-life-region=\"Name\"]').style.left)};})()"));
            double rx = Convert.ToDouble(region["x"]), ry = Convert.ToDouble(region["y"]);
            await Mouse("mouseMoved", rx, ry); await Mouse("mousePressed", rx, ry); await Mouse("mouseMoved", rx + 10, ry + 8); await Mouse("mouseReleased", rx + 10, ry + 8);
            await Check("parseFloat(document.querySelector('[data-life-region=\"Name\"]').style.left)>"+Convert.ToDouble(region["left"]).ToString(System.Globalization.CultureInfo.InvariantCulture), "Frame metadata pointer positioning failed.");
            await Script("document.querySelector('[data-life-frame-orientation=\"landscape\"]').click()");
            await Wait("document.querySelector('#lifeFrameStage.landscape') && document.querySelector('#lifeFrameComposite').naturalWidth>0", "Landscape frame draft preview");
            if (draftPreviewRequests < 1) throw new InvalidOperationException("Frame draft preview did not cross native JSON bridge."); checks++;
            await Task.Delay(250); if (!draftLandscape) throw new InvalidOperationException("Frame draft orientation was not sent."); checks++;
            using (var capture = File.Create(Path.Combine(output, "webview2-frame-creator.png"))) await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, capture);
            await Script("document.querySelector('[data-life-action=\"frameCreate\"]').click()");
            await Wait("tcg.wallet.Gems===3950 && document.querySelector('[data-life-item=\"FI-webview-fixture\"]') && document.querySelector('#lifestyleContent').getAttribute('aria-busy')==='false'", "Frame creation inventory/wallet"); checks++;
            await Script("document.querySelector('[data-life-item=\"FI-webview-fixture\"]').click()");
            await Wait("document.querySelector('#lifeStudioBase').dataset.lifePreviewFrameItem==='FI-webview-fixture' && document.querySelector('#lifeSaveSummary').textContent.includes('Uses 1 frame')", "Created frame preview/consumption quote"); checks++;
            await Script("document.querySelector('[data-life-action=\"chooseDye\"]').click();document.querySelector('[data-life-item-pick=\"dye-03\"]').click()");
            await Wait("document.querySelector('#lifeSaveSummary').textContent.includes('Uses 1 dye') && document.querySelector('#lifeStudioBase').dataset.lifePreviewDye==='dye-03'", "Inventory dye preview/consumption quote"); checks++;
            await Script("document.querySelector('[data-life-view=\"studio\"]').click()");
            await Wait("document.querySelector('[data-adventure-action=\"artMode\"][data-mode=\"sketch\"]') && !document.querySelector('[data-adventure-action=\"artMode\"][data-mode=\"sketch\"]').disabled", "Studio ready for sketch");
            await Script("document.querySelector('[data-adventure-action=\"artMode\"][data-mode=\"sketch\"]').click();document.querySelector('#lifeCanvas').scrollIntoView({block:'center'});");
            await Wait("document.querySelector('#lifeCanvas').getBoundingClientRect().height>0", "canvas layout");
            await Check("document.querySelector('#lifeBrushColor') && document.querySelector('#lifeBrushSize') && document.querySelector('#lifeEraser') && document.querySelector('#lifeCanvasClear')", "Paint toolbar controls are missing.");
            var point = json.Deserialize<Dictionary<string, object>>(await Script("(()=>{const r=document.querySelector('#lifeCanvas').getBoundingClientRect();return {x:r.left+30,y:r.top+40};})()"));
            double x = Convert.ToDouble(point["x"]), y = Convert.ToDouble(point["y"]);
            await Script("document.querySelector('#lifeBrushColor').value='#53a6d8';document.querySelector('#lifeBrushSize').value='30'");
            await Mouse("mouseMoved", x, y); await Mouse("mousePressed", x, y); await Mouse("mouseMoved", x + 35, y + 35); await Mouse("mouseReleased", x + 35, y + 35);
            await Check("(()=>{const c=document.querySelector('#lifeCanvas'),r=c.getBoundingClientRect(),p=c.getContext('2d').getImageData(Math.round(30*c.width/r.width),Math.round(40*c.height/r.height),1,1).data;return p[0]===83&&p[1]===166&&p[2]===216&&p[3]===255;})()", "Brush color/size painting failed.");
            await Script("document.querySelector('#lifeEraser').click()");
            await Check("document.querySelector('#lifeEraser').getAttribute('aria-pressed')==='true'", "Eraser toggle failed.");
            await Mouse("mouseMoved", x, y); await Mouse("mousePressed", x, y); await Mouse("mouseReleased", x, y);
            await Check("(()=>{const c=document.querySelector('#lifeCanvas'),r=c.getBoundingClientRect(),p=c.getContext('2d').getImageData(Math.round(30*c.width/r.width),Math.round(40*c.height/r.height),1,1).data;return p[3]===0;})()", "Canvas erasing failed.");
            await Script("document.querySelector('#lifeCanvasClear').click();document.querySelector('#lifeEraser').click();document.querySelector('#lifeBrushColor').value='#c49bf7'");
            await Check("(()=>{const c=document.querySelector('#lifeCanvas'),p=c.getContext('2d').getImageData(599,839,1,1).data;return p[3]===0;})()", "Canvas clear failed.");
            await Mouse("mouseMoved", x, y); await Mouse("mousePressed", x, y); await Mouse("mouseMoved", x + 35, y + 35); await Mouse("mouseReleased", x + 35, y + 35);
            await Check("(()=>{const c=document.querySelector('#lifeCanvas'),r=c.getBoundingClientRect(),p=c.getContext('2d').getImageData(Math.round(30*c.width/r.width),Math.round(40*c.height/r.height),1,1).data;return p[0]===196&&p[1]===155&&p[2]===247&&p[3]===255;})()", "WebView2 pointer painting failed.");
            await Script("document.querySelector('[data-life-action=\"artworkDraw\"]').click()");
            await Wait("document.querySelector('#lifeStudioBase') && document.querySelector('#lifeStudioBase').dataset.lifePreviewArtwork==='isolated-webview-artwork'", "PNG native round trip");
            if (drawings != 1 || !requests.Contains("snapshot")) throw new InvalidOperationException("Native JSON fixture transport was not exercised."); checks++;
            await Wait("document.querySelector('#lifeSaveSummary').textContent.includes('pixels') && document.querySelector('#lifeSaveSummary').textContent.includes('Uses 1 dye') && document.querySelector('[data-life-action=\"cosmeticSave\"]').textContent.endsWith('1')", "Pixel-priced drawing fee/dye quote"); checks++;
            using (var capture = File.Create(Path.Combine(output, "webview2-paint-toolbar.png"))) await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, capture);
            await Script("document.querySelector('[data-life-action=\"cosmeticSave\"]').click()");
            await Wait("tcg.wallet.Gems===3949 && document.querySelector('[data-life-action=\"cosmeticSave\"]').disabled && document.querySelector('#lifestyleContent').getAttribute('aria-busy')==='false'", "Paid custom artwork save/wallet"); checks++;
            await Check("document.querySelector('#lifeStudioBase').dataset.lifePreviewFrame==='moonlace' && document.querySelector('#lifeStudioBase').dataset.lifePreviewDye==='dye-03' && document.querySelector('#lifeStudioBase').dataset.lifePreviewArtwork==='isolated-webview-artwork'", "Saved native cosmetic fields failed.");
            if (dyeCount != 0 || fixtureGems != 3949 || paidOperations.Count != 3 || sequence != 4 || materials["fiber"] != 0 || materials["petal"] != 5 || materials["crystal"] != 5 || frameAvailable || savedItemPreviewRequests < 1) throw new InvalidOperationException("Isolated fixture economy transport mismatch."); checks++;
            await Check("webviewSmoke.errors.length===0 && webviewSmoke.csp.length===0", "Runtime/script/CSP errors: " + await Script("JSON.stringify(webviewSmoke)"));
            await Script("document.querySelector('#lifeStudioPreview').scrollIntoView({block:'center'})");
            using (var capture = File.Create(Path.Combine(output, "webview2-studio.png"))) await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, capture);
            File.WriteAllText(Path.Combine(output, "report.json"), json.Serialize(new { status = "PASS", checks = checks, realWebView2 = true, browserVersion = environment.BrowserVersionString, deployedHtml = Path.Combine(deployed, "ui", "hytcg.html"), isolatedProfile = true, networkBlocked = true, productionWritten = false, fixtureGems = fixtureGems, paidOperationIds = paidOperations, nativeFixtureRequests = requests, diagnostics = await Script("webviewSmoke") }));
            Console.WriteLine("PASS " + checks + " deployed WebView2 runtime/CSP/Atelier/painting checks (" + environment.BrowserVersionString + ")");
            Environment.ExitCode = 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error); File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Environment.ExitCode = 1;
        }
        finally { Close(); }
    }
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LifestyleWebViewSmoke(args[0], args[1]));
    }
}
