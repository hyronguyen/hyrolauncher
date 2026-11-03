# Verification

Test sources live here. Production C# sources remain in the project root. All generated reports, screenshots, test executables and isolated accounts go into `output/verification/`.

## Offline smoke checks

```powershell
node tests/run-smoke.cjs
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-storage.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-economy.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-publish-release-local.ps1
```

The JavaScript smoke runner checks dialogs, MP3 playback and BGM ownership, pet movement, sound effects and the browser scrollbar. It does not launch games or call external APIs. Native storage/economy tests compile isolated fixtures with the Windows .NET Framework compiler; they do not modify the real account in `dist`.

The release publisher check uses a mock HTTP handler to verify endpoint restrictions, streamed uploads, redacted errors and checksum matching. It does not retrieve credentials or contact GitHub.

## Current card and bookshelf checks

`node tests/verify-cozy-theme-ui.cjs` exercises the saved Cozy and chill theme across Default, Carousel and Game shelf, including wooden shelf materials, responsive bounds, artwork preservation, contrast, existing pull-out/RGB behavior, Explore cards, dialogs and Big Picture. `powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-cozy-theme.ps1 -CandidatePath <built-exe>` checks native theme normalization and persistence with isolated settings fixtures.

Build a candidate, then supply its executable explicitly to the portrait verifier:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-portrait-position.ps1 -CandidatePath "output/candidate/HYROLauncher.Check.exe"
```

Visual tests require Playwright. Install the repository's development dependencies, or set `PLAYWRIGHT_MODULE` to a separately installed Playwright module. The scripts use installed Microsoft Edge on Windows.

```powershell
node tests/verify-hytcg-canvas-portrait.cjs
node tests/verify-hytcg-rarity-emblems.cjs
node tests/verify-hytcg-mystic-ui.cjs
node tests/verify-hytcg-studio-position-ui.cjs
node tests/verify-hytcg-adventure-ui.cjs
node tests/verify-hytcg-arena-animation.cjs
node tests/verify-bookshelf-ui.cjs
```

These tests stub the native bridge and block external HTTP requests. `fixtures/MysticUR.png` and `fixtures/StudioUR.png` are local visual inputs, not owned cards or account records.

The bookshelf UI check covers rigid game-case motion, responsive category shelves, Steam IGDB lazy loading and cached screenshots, viewer isolation during late replies, scrollable detail panels with visible launch actions, and accent/rainbow/synchronized RGB lighting with brightness, speed and visibility controls. Color checks include lamp and wall-wash hue changes, shared timing after shelf refresh, live speed changes after pauses, saved preferences, and unchanged game artwork. Reports and screenshots use `output/verification/bookshelf-ui/`.

`node tests/verify-big-picture-bookshelf-ui.cjs` checks keyboard/controller navigation, fixed shelf slots, selected cases, favorites, detail/back focus and visible Play controls at TV and narrow sizes. `node tests/verify-explore-layout-ui.cjs` verifies that Explore, search, wishlist and Steam Store retain their original artwork cards in every launcher layout, including Game shelf and Big Picture. It also covers single-click details, filters, paging, responsive widths and returning to the 3D library. Both use isolated fixtures and mocked launch/link actions.

`node tests/verify-system-storage-ui.cjs` verifies storage/device tabs, loading, escaped metadata, safe cache IPC, busy/error/success states, keyboard focus, advanced reset confirmations, late responses and fixed footers on narrow screens. `powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-launcher-cache.ps1 -CandidatePath <built-exe>` verifies allowlisted cleanup, preservation of card/account/web data, locked files, path/junction rejection, shared asset references, nested WebView accounting, TTL/LRU limits and atomic cache writes. All deletion targets in these checks are temporary fixtures under `output/verification/`; production storage is not cleaned.

`verify-steam-igdb.ps1` and `verify-steam-igdb.cjs` check exact Steam App ID matching, metadata caching, account changes and the shared image viewer without external requests. `verify-steam-igdb-live.ps1` is an optional read-only API probe using local IGDB configuration and an isolated cache.

The arena animation check measures attack, healing, buff and defense effects through a complete mocked turn. It also decodes the three local MP3s under the panel CSP without playing them. It covers impact timing, command locking, background refresh, local sounds, cancellation, reduced motion and narrow panels; reports and screenshots use `output/verification/latest/arena-*`.

`verify-bookshelf-webview2.ps1`, `verify-hytcg-dev-webview2.ps1` and `verify-lifestyle-webview2.ps1` exercise actual WebView2 hosts with isolated fixture directories. They require a native Windows desktop session and built WebView2 assemblies. Set `HYRO_TEST_EXE` to choose a candidate; otherwise native helpers use the local check build, falling back to `dist/HYROLauncher.exe`. The dev verifier also accepts `-DeploymentPath`.

For the shelf lamp, `verify-bookshelf-webview2.ps1 -RgbOnly` measures actual rendered hue and animation timing in WebView2, then checks brightness, hidden-view and idle pauses. Add `-Reduced` to verify that selecting RGB explicitly still enables color movement under Windows reduced motion. `-Baseline` records a pre-fix sample without enforcing movement.

## Migration and historical contracts

`verify-hytcg-production-migration.ps1` and `verify-hytcg-deployed.ps1` are manual deployment checks. They inspect local account data; migration works on a cloned account. Do not publish their output.

Older suites are retained to document compatibility and previous contracts. In particular, `verify-hytcg-shapes-upgrades*`, `verify-hytcg-default-layout.cjs`, `verify-default-card-layouts.cjs`, `verify-frame-header-dye*`, `verify-rarity-rim*`, `verify-hytcg-cosmetic-renderer*` and older frame-creation tests can contain expectations for horizontal cards, earlier card layouts or retired frame-shop behavior. They are not part of the default smoke run. Some require first generating a paired native image fixture. Use the current portrait, Studio, rarity and Mystic suites for today's card behavior.

Scripts named `*live*`, and other API integration checks, may call real providers or read local API configuration. Run them deliberately; they are excluded from the offline smoke runner.

`test-runtime.ps1` separates production, test-source and generated-output paths. `test-runtime.cjs` supports portable Playwright resolution and ensures legacy project-relative source reads work from any launch directory. Neither helper contains machine-specific dependency paths.
