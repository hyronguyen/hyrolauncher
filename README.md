# HYROLauncher

Portable Windows game menu launcher inspired by cyber cafe launchers, Steam Library, HoYoPlay, and Epic Games Launcher.

[Download the Windows installer](https://github.com/hyronguyen/hyrolauncher/releases/download/v1.0.0/HYROLauncher-Setup-1.0.0.exe) · [Portable ZIP](https://github.com/hyronguyen/hyrolauncher/releases/download/v1.0.0/HYROLauncher-Portable-1.0.0.zip) · [SHA-256 checksums](https://github.com/hyronguyen/hyrolauncher/releases/download/v1.0.0/SHA256SUMS.txt)

The installer includes offline prerequisites. The portable ZIP requires .NET Framework 4.8 and WebView2 to be installed already. API-dependent features still require Internet access and your own service configuration.

App Settings → Appearance → Layout includes Default, Carousel, and **Game shelf**. Game shelf groups local apps, favorites, private games, and Steam into physical case shelves with a selected-game pane. Big Picture uses Carousel and restores the chosen layout on exit. The shelf interaction was inspired by [mawise/bookshelf](https://github.com/mawise/bookshelf); its implementation uses local CSS and JavaScript without an additional library.

Cases show their HYRO spine and part of their original front artwork inside recessed cabinets. Hover or keyboard focus adds a small lift and local light; clicking pulls the rigid case forward and turns the full cover toward the viewer. It stays open until the same case, another case, or an empty shelf area is clicked. Touch and keyboard selection follow the same behavior. Shelf rows reserve room for the open cover and retain vertical scrolling. App Settings → Appearance → Bookshelf Animations controls the two animation durations, distance, angles, lighting, and reduced motion; pull-out/return duration can be set from 100 to 5000 ms. Changes apply immediately and use existing launcher settings storage. Unchanged native state refreshes preserve moving cases instead of snapping their artwork to the final position.

## Build

Windows 10/11 x64, .NET Framework 4.8 and PowerShell 5.1 or later are required. The build restores a pinned WebView2 SDK from NuGet into `.deps`; Power BI is not required. Run in PowerShell:

```powershell
.\build.ps1
```

If PowerShell blocks scripts on your machine, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The executable is created at:

```text
dist\HYROLauncher.exe
```

To create the single-file installer, run `powershell -ExecutionPolicy Bypass -File .\build-installer.ps1`. The result is `output\release\HYROLauncher-Setup-1.0.0.exe`. It includes the offline WebView2 and .NET prerequisites, creates a Start menu shortcut and supports uninstalling. Installation is per user under `%LOCALAPPDATA%\Programs\HYROLauncher`; uninstalling leaves locally created cards and progress in place.

Fresh builds contain blank API credential files. Add your own values under the installed `assets\apikey` folder when using services that need them. `config\examples` contains the public templates; actual keys, accounts, browser profiles and personal libraries are excluded from Git and release packages.

Project layout: production C# files at the root, `ui` for WebView2 screens, `assets` for bundled resources, `tests` for regression suites, `scripts` for dependency/publication tooling, `packaging` for the installer, and `docs`/`licenses` for documentation and notices. `output` contains generated files only. See [test instructions](tests/README.md) and [third-party notices](docs/THIRD-PARTY-NOTICES.md).

## Features

- Add local games/programs: `.exe`, `.bat`, `.cmd`, `.lnk`
- Scan installed games from Steam, Epic Games, and common game folders, then review and choose which games to import
- Add web links such as Steam, Epic, YouTube, or private game dashboards
- Web links are previewed inside the launcher with a native WebView2 child view, plus reload and external-open controls
- Optional arguments and working directory
- Custom icon image per game
- Automatic fallback icon extraction from `.exe` files when no icon is uploaded
- Custom banner/key art per game for the selected-game hero area, including GIF
- Custom hero logo/title image per game; if empty, the launcher renders the game name as text
- Default logo and banner from `assets` when a game has no custom upload
- Custom BGM per game
- Uploaded game icon, banner, hero, and BGM are copied into `assets/library/<gameId>` for portable use
- Asset paths inside `launcher-data.json` are stored relative to the launcher folder when possible
- Replacing an uploaded game asset removes the old file, keeping one icon, one banner, one hero, and one BGM per game
- Custom categories, category picker when adding games, and search
- Favorite games can be pinned to the left icon sidebar
- Sidebar game icons can be reordered by dragging them up or down
- Per-game private mode masks locked game/link cards and requires a PIN before selecting or opening them
- `All` includes Favorite and Private tags; opening Private requires the PIN and then reveals private items
- Private PIN defaults to `123456` and is stored as an MD5 hash in launcher data
- Top plugin toolbar can collapse/expand like a small curtain
- System tray → **API Monitor** captures native API requests only while its window is open; closing stops capture. Session TSV files are stored in `dist/logs`; credentials and response bodies are excluded.
- HYTCG includes CharacterDex (character/series search and verified artwork variants), two daily AniList series banners, and daily game/movie missions. Limited pulls cost 180 gems or one limited credit; 30% use the banner's exact ACDB character pool. Daily resets follow Vietnam time (UTC+7).
- Cards are stored locally as DPAPI-encrypted `.hycard` files in `dist/cards`, with authenticated ownership and in-memory previews. Existing PNG cards migrate without changing artwork or progress. The account reset requires typing `delete`, clears its cards/progress, and creates a fresh account with 1,800 gems.
- Add plugins to the top toolbar as WebView URL plugins or Custom program/script plugins launched by Windows
- Built-in PET plugin: an idle-only launcher companion with size/dwell settings and a reusable pet library. It stays still for 60 seconds by default, fades out, changes position while invisible, then fades back in.
- `All` opens a full library screen with a fixed search/category/action header and a separately scrollable game grid
- App settings include accent color, Default/Carousel library layouts, 2D/3D library card style, max games per row from 1-10, sidebar position, English/Vietnamese language, Disable BGM, and Disable effects
- Export/import launcher backups as `.zip` files containing `launcher-data.json` and `assets/library`
- App settings include startup with Windows, default BGM playback, global BGM disable, and BGM volume
- UI sounds: `assets/start_game.mp3` for Start, `assets/switch_to_game.mp3` for opening game details, `assets/back_to_all_game.mp3` for returning to All, and `assets/click.mp3` for other controls. Disable effects mutes all four.
- Accent color uses a native color picker
- Selected game hero panel with a large Play button
- Running local `.exe` games are detected automatically; games launched from HYROLauncher use a process handle/exit event, with a slower background scan for games opened outside the launcher
- Launcher minimizes automatically after a game or link is launched
- Fixed 1280x720 launcher window to keep banner composition stable
- WebView2 UI for a custom launcher header and smoother GIF/banner rendering
- WebView2 cache/user data is stored in `%LocalAppData%\HYROLauncher\WebView2`, keeping `dist` cleaner
- Delete games from the game settings modal
- Data is saved in `launcher-data.json` next to the executable

## Design Notes

- Explore is available in both library layouts: recent PC releases (last 90 days), upcoming PC games (next 180 days), IGDB search, and current Steam Store offers in Vietnam. Deals are filtered to 20% off or more and show current/original prices; these are not historical-low claims. Steam Store's public storefront endpoint is not a documented Steamworks API and may change; failed fetches fall back to cached results.
- Explore shows recent releases, Steam offers and upcoming games together in horizontal shelves, with IGDB search above. Each shelf loads independently on opening Explore; images load lazily. Steam offers use the full storefront search instead of featured offers, paginate 50 source rows at a time, filter to 20% off or more and deduplicate app IDs. The displayed count is loaded matches, not the total discounted catalog. Metadata is cached for four hours and Steam pages for 30 minutes. Cards open an app detail dialog and link to the original IGDB/Steam page, without buying or launching unowned games.
- My Wishlist appears when a SteamID is configured. The native host calls IWishlistService with the configured Steam API key, then IStoreBrowseService in batches of 50 to fetch Vietnamese prices and artwork. All wishlist games are shown, including discounts below 20% and games without an offer. The header shows the total wishlist count; more pages load on request. Account-specific caches expire after 30 minutes. Inaccessible/empty Steam responses have an app message and do not expose credentials.
- Browse all deals opens a separate Explore screen with a scrollable filter sidebar and result grid. It supports Steam term search, seven sort orders, include/exclude tags, product types, feature/controller categories, supported languages, Steam Deck compatibility, platforms, hiding free games, price limits and minimum discount. Tag/feature/language choices are fetched from Steam's storefront and searchable. Numeric VND prices enforce arbitrary maximum prices locally in addition to Steam's regional price stops. Results paginate on scrolling or using the next-page button; counts refer to loaded matching results, not the storefront's unfiltered total. These public storefront services can change independently of the launcher.
- Big Picture is available from the monitor/controller button, General settings or F11. It fills the current monitor, temporarily uses Carousel and restores the previous window bounds and configured layout on exit. D-pad/left analog navigate; A selects, B backs out, Y opens search, Start opens the launcher menu. A virtual keyboard handles text entry. Standard-mapped browser gamepads are supported; native XInput is a fallback for compatible Windows controllers. Polling runs only in Big Picture and input is ignored when the launcher loses focus. The controller controls the launcher UI, not game input or third-party web pages. Physical-controller compatibility requires device testing.
- Library navigation orders Home, Favorite, Private, Steam, Explore. LB/RB (L1/R1 in PlayStation display mode) switch these tabs with wraparound; PageUp/PageDown provide keyboard equivalents. X/Square toggles the focused game's favorite status. General settings select colored Xbox ABXY or PlayStation Cross/Circle/Square/Triangle hints; this changes display symbols without remapping actions.
- Appearance settings select current scrolling Carousel shelves or all cards in a grid, with an independent 1–10 cards-per-row setting. Steam follows that column count in all-cards mode. Cards use full-cover artwork and a bottom gradient behind names/category/playtime; Default favorite actions remain available as compact overlay icons.
- The IGDB detail panel uses a large screenshot above the tabs. Custom HERO placement is constrained to stay outside the panel, including while dragging/resizing and after screenshot/layout updates.

- Local game details have a collapsible Info/Screenshots panel linked to IGDB. Game Settings → IGDB searches by name; select the correct result and Save. Unlink clears the association on Save. Custom artwork and launch paths stay unchanged.
- Native C# reads `assets/apikey/twitchclient.txt` and `twitchsecret.txt`, gets an app token, and queries IGDB. Tokens/credentials are never sent to WebView or cached metadata. `igdb-cache/<id>.json` stores metadata for seven days; Refresh bypasses this interval, and cached data remains available if the network fails.
- Screenshot thumbnails load lazily; the full image opens in the app viewer with arrow-key navigation and Escape. IGDB descriptions are displayed as supplied, without automatic translation.

- App Settings → General → Steam ID64 connects a Steam library (17 digits, or a numeric steamcommunity.com/profiles URL). Save to fetch; remove the ID to hide Steam games. Game details must be Public for the API to return the library.
- Steam API key is read by the native host from `assets/apikey/steamapikey.txt`. Steam games appear on All and in a dedicated Steam games filter in both layouts, with search, playtime and Refresh. Clicking a game opens it in the installed Steam client.
- Steam library metadata is cached separately in `steam-library-cache.json`, refreshed after six hours, and retained when offline. Account changes never show the previous account's library. Images load lazily.
- Carousel uses full landscape artwork with a fading information overlay for regular and Steam games; the main Favorite shelf keeps portrait cards and scrolling. Default offers 2D, 3D and square Icon styles for regular and Steam games. Steam favorites are saved per account.
- The Steam section header shows the account name/avatar when available, with a logo fallback. Steam card clicks open HYRO's detail screen; only Start launches Steam.

- Steam-style library: game tiles plus a selected-game detail area.
- HoYoPlay-style unified launcher: one app can launch many games and web links.
- Epic-style library basics: search, categories, settings, and portable data.
- The built-in HYRO Buddy PET uses `assets/pets/hyro-buddy-idle.gif`, a transparent eight-frame idle loop created from the supplied robot artwork. The existing default pet ID is retained for saved settings. Walking assets in older pet definitions are ignored.
- The compact PET panel offers enable, companion, size and a 20–180 second dwell interval. Custom pets require only an idle GIF. Hovering over the pet or opening a dialog defers relocation; hiding the launcher stops its movement timer.
- Movie Watchlist has a featured movie, horizontal Continue watching / My list / Completed shelves, search and status filters. Poster clicks open HyBrowser; visible progress/edit buttons work with mouse or keyboard. The quick progress editor preserves artwork and links, and drafts survive native state refreshes and file selection. Movies may be saved without an episode; progress bars appear only when a numeric episode and total are known.
- The pet is above launcher content and supports pointer dragging within the content bounds. Its optional Drag GIF plays while dragging; empty Drag GIF falls back to idle. HYRO Buddy includes an eight-frame transparent `assets/pets/hyro-buddy-drag.gif`. Dropping the pet restarts the dwell timer and suppresses accidental panel clicks.
- Movie Watchlist's menu exports/imports `.xlsx` with all movie fields, frozen headers and filtering. Import previews the movie count and merges by ID or watch link, keeping other existing movies and protected entries. It requires no Excel installation. Poster paths/URLs are retained as references.
- HyBrowser uses vertical tabs. A single movie tab is reused when another watchlist movie opens, with its poster in the tab background. `Episode +1` updates that movie, clears its resume time and marks completed at the known total; ordinary tabs still save a new movie draft. Existing unedited watchlist editors receive progress changes while typed drafts are kept.

For the best result, add a square icon and a wide banner image for each game.
Recommended banner ratio is 16:9, ideally `1920x1080`. PNG, JPG, and GIF are supported.

- Appearance → All app background lets you select a local image/GIF or an available Steam profile background. Local files are copied into `assets/settings` for portable use. Dim uses the active theme color; Remove restores the theme background. These changes apply to the library menu in both layouts. Animated Steam backgrounds pause outside the library and when the app is hidden.
- Steam background selection combines the equipped profile background with background items from the public Steam Community inventory. Results are cached for six hours. A private or inaccessible inventory shows a clear message while retaining any readable profile background. Steam API credentials stay in the native host.
- Movie Watchlist accepts URLs from any website and opens them in HyBrowser. It no longer needs selected movie web apps. Use the bookmark button in HyBrowser to prefill the movie title and URL; episode/time/poster remain editable. Existing saved movies are retained.
- HyBrowser provides close buttons on each tab, middle-click closing, address/search selection, loading/stop controls and resizable window borders. Shortcuts: Ctrl+L/Alt+D address, Ctrl+T new tab, Ctrl+Shift+N private tab, Ctrl+W close, Ctrl+Tab/Ctrl+Shift+Tab switch, Ctrl+R/F5 reload, Esc stop, Alt+Left/Right history.

## Portable Use

Copy `HYROLauncher.exe`, WebView2 DLLs in `dist`, the `ui` folder, the `assets` folder, and `launcher-data.json` together to another machine. Paths to games should match that machine.
