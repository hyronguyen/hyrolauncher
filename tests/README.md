# Verification

Test sources live here. Production C# sources remain in the project root. All generated reports, screenshots, test executables and isolated accounts go into `output/verification/`.

## Offline smoke checks

```powershell
node tests/run-smoke.cjs
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-storage.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-economy.ps1
```

The JavaScript smoke runner checks dialogs, MP3 playback and BGM ownership, pet movement, sound effects and the browser scrollbar. It does not launch games or call external APIs. Native storage/economy tests compile isolated fixtures with the Windows .NET Framework compiler; they do not modify the real account in `dist`.

## Current card and bookshelf checks

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
node tests/verify-bookshelf-ui.cjs
```

These tests stub the native bridge and block external HTTP requests. `fixtures/MysticUR.png` and `fixtures/StudioUR.png` are local visual inputs, not owned cards or account records.

`verify-bookshelf-webview2.ps1`, `verify-hytcg-dev-webview2.ps1` and `verify-lifestyle-webview2.ps1` exercise actual WebView2 hosts with isolated fixture directories. They require a native Windows desktop session and built WebView2 assemblies. Set `HYRO_TEST_EXE` to choose a candidate; otherwise native helpers use the local check build, falling back to `dist/HYROLauncher.exe`. The dev verifier also accepts `-DeploymentPath`.

## Migration and historical contracts

`verify-hytcg-production-migration.ps1` and `verify-hytcg-deployed.ps1` are manual deployment checks. They inspect local account data; migration works on a cloned account. Do not publish their output.

Older suites are retained to document compatibility and previous contracts. In particular, `verify-hytcg-shapes-upgrades*`, `verify-hytcg-default-layout.cjs`, `verify-default-card-layouts.cjs`, `verify-frame-header-dye*`, `verify-rarity-rim*`, `verify-hytcg-cosmetic-renderer*` and older frame-creation tests can contain expectations for horizontal cards, earlier card layouts or retired frame-shop behavior. They are not part of the default smoke run. Some require first generating a paired native image fixture. Use the current portrait, Studio, rarity and Mystic suites for today's card behavior.

Scripts named `*live*`, and other API integration checks, may call real providers or read local API configuration. Run them deliberately; they are excluded from the offline smoke runner.

`test-runtime.ps1` separates production, test-source and generated-output paths. `test-runtime.cjs` supports portable Playwright resolution and ensures legacy project-relative source reads work from any launch directory. Neither helper contains machine-specific dependency paths.
