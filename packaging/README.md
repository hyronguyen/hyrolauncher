# Windows release packaging

`build-installer.ps1` creates a clean Windows x64 release from the existing C#,
HTML and bundled resources. It never packages `dist`, local accounts, cards,
libraries, browser profiles, caches, test fixtures or real API credentials.

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1 -Version 1.0.0
```

The build automatically calls `packaging/download-tools.ps1` when its tools or
prerequisites are missing. The download script obtains Inno Setup 6.7.3 and the offline .NET Framework 4.8
and WebView2 x64 Evergreen installers from their official publishers. Every
download must have a valid Authenticode signature matching the expected
publisher before it can be used. Download provenance and hashes stay in the
ignored `output/tools/provenance.json` file. Inno Setup is extracted in portable
mode under `output/tools/inno`, without registering a compiler installation.
An installed Inno Setup 6.7+ can instead be supplied with `-CompilerPath`.

Release outputs are under `output/release`:

- `HYROLauncher-Setup-1.0.0.exe`: one offline installer, including prerequisites.
- `HYROLauncher-Portable-1.0.0.zip`: app files; .NET 4.8 and WebView2 must already
  be installed for this portable version.
- `SHA256SUMS.txt`: hashes for both distributable files.
- `payload-manifest.json`: hashes of every clean app payload file.

The installer supports Windows 10 1809 or newer on x64. It defaults to the
current user's `%LOCALAPPDATA%\Programs\HYROLauncher`, creates a Start Menu
entry, offers a desktop shortcut, and registers an uninstaller. It checks both
per-machine and per-user WebView2 installations and runs the unmodified
Microsoft offline installers only when needed. .NET installation requires
Windows administrator permission; if Windows requires a restart, setup pauses
and instructs the user to restart and run the installer again.

Updates and uninstall preserve local progress and API settings. Only bundled
application files are tracked for removal. Empty API text templates and the
public HYTCG config are installed only if the corresponding local file does not
already exist. No real credentials are included. API-dependent features still
require Internet access and any applicable personal configuration.

To validate the final installer:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File packaging/verify-installer.ps1
```

Verification refuses to run if an installed HYRO app already owns the installer
registration. It installs into an isolated workspace directory, verifies every
payload hash, initializes the packaged launcher using actual WebView2 without
claiming the production app's single-instance mutex, reinstalls over synthetic
user data, and uninstalls. It verifies that synthetic user data survives and the
existing `dist` account/library/card files remain unchanged. Reports and setup
logs are under `output/installer-qa`.

The installer itself is not code-signed. Microsoft prerequisite installers keep
their original publisher signatures.
