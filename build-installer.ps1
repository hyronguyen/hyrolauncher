param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')][string]$Version = '1.0.0',
    [string]$CompilerPath = '',
    [string]$ToolsDirectory = '',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$release = Join-Path $root 'output\release'
$buildOutput = Join-Path $release 'build'
$payload = Join-Path $release 'HYROLauncher'
if (!$ToolsDirectory) { $ToolsDirectory = Join-Path $root 'output\tools' }
$tools = [IO.Path]::GetFullPath($ToolsDirectory)
New-Item -ItemType Directory -Force -Path $release | Out-Null

function Get-SafeTree([string]$Path) {
    $start = Get-Item -LiteralPath $Path -Force
    if ($start.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked release/source directory: ' + $Path) }
    $pending = New-Object 'System.Collections.Generic.Stack[string]'
    $entries = New-Object 'System.Collections.Generic.List[object]'
    $pending.Push($start.FullName)
    while ($pending.Count -gt 0) {
        foreach ($entry in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked release/source content: ' + $entry.FullName) }
            $entries.Add($entry)
            if ($entry.PSIsContainer) { $pending.Push($entry.FullName) }
        }
    }
    return @($entries.ToArray())
}

function Clear-ReleaseDirectory([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $boundary = [IO.Path]::GetFullPath($release).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Release cleanup escaped output/release.' }
    if (Test-Path -LiteralPath $resolved) {
        $null = Get-SafeTree $resolved
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $resolved | Out-Null
}

if (!$CompilerPath) {
    foreach ($candidate in @((Join-Path $tools 'inno\ISCC.exe'), 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe', 'C:\Program Files\Inno Setup 6\ISCC.exe')) {
        if (Test-Path -LiteralPath $candidate) { $CompilerPath = $candidate; break }
    }
}
$downloadsMissing = !(Test-Path -LiteralPath (Join-Path $tools 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe')) -or !(Test-Path -LiteralPath (Join-Path $tools 'NDP48-x86-x64-AllOS-ENU.exe'))
if (!$CompilerPath -and !(Test-Path -LiteralPath (Join-Path $tools 'innosetup-6.7.3.exe'))) { $downloadsMissing = $true }
if ($downloadsMissing) { & (Join-Path $root 'packaging\download-tools.ps1') -OutputDirectory $tools }

foreach ($prerequisite in @('MicrosoftEdgeWebView2RuntimeInstallerX64.exe', 'NDP48-x86-x64-AllOS-ENU.exe')) {
    $file = Join-Path $tools $prerequisite
    if (!(Test-Path -LiteralPath $file)) { throw ('Missing offline prerequisite. Run packaging/download-tools.ps1 first: ' + $prerequisite) }
    $signature = Get-AuthenticodeSignature -LiteralPath $file
    if ($signature.Status -ne 'Valid' -or !$signature.SignerCertificate.Subject.Contains('Microsoft Corporation')) { throw ('Invalid Microsoft signature: ' + $prerequisite) }
}

if (!$CompilerPath) {
    $compilerSetup = Join-Path $tools 'innosetup-6.7.3.exe'
    if (!(Test-Path -LiteralPath $compilerSetup)) { throw 'Install Inno Setup 6.7+ or run packaging/download-tools.ps1 first.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $compilerSetup
    if ($signature.Status -ne 'Valid' -or !$signature.SignerCertificate.Subject.Contains('Pyrsys')) { throw 'Inno Setup publisher signature is invalid.' }
    $portableCompiler = Join-Path $tools 'inno'
    New-Item -ItemType Directory -Force -Path $portableCompiler | Out-Null
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/PORTABLE=1', ('/DIR="' + $portableCompiler + '"'))
    $process = Start-Process -FilePath $compilerSetup -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw ('Portable Inno Setup extraction failed: ' + $process.ExitCode) }
    $CompilerPath = Join-Path $portableCompiler 'ISCC.exe'
}
if (!(Test-Path -LiteralPath $CompilerPath)) { throw 'Inno Setup compiler was not found.' }

if (!$SkipBuild) {
    Clear-ReleaseDirectory $buildOutput
    & (Join-Path $root 'build.ps1') -ExecutableName 'HYROLauncher.exe' -OutputDirectory $buildOutput
}
if (!(Test-Path -LiteralPath (Join-Path $buildOutput 'HYROLauncher.exe'))) { throw 'Clean release compilation is missing.' }
Clear-ReleaseDirectory $payload

# Deliberate allowlist: never package the running dist directory, user's library,
# vault/cards, browser profiles, caches, logs, test fixtures, or real API settings.
foreach ($name in @('HYROLauncher.exe', 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'WebView2Loader.dll')) {
    Copy-Item -LiteralPath (Join-Path $buildOutput $name) -Destination $payload
}
foreach ($directory in @('ui')) {
    $null = Get-SafeTree (Join-Path $root $directory)
    Copy-Item -LiteralPath (Join-Path $root $directory) -Destination $payload -Recurse
}
$assetOutput = Join-Path $payload 'assets'
New-Item -ItemType Directory -Force -Path $assetOutput | Out-Null
foreach ($directory in @('builtin-plugins', 'hytcg', 'icons', 'pets')) {
    $null = Get-SafeTree (Join-Path $root ('assets\' + $directory))
    Copy-Item -LiteralPath (Join-Path $root ('assets\' + $directory)) -Destination $assetOutput -Recurse
}
foreach ($name in @('app.ico', 'default-banner.png', 'default-logo.png', 'hyro-logo.svg', 'back_to_all_game.mp3', 'click.mp3', 'default BGM.mp3', 'start_game.mp3', 'switch_to_game.mp3')) {
    Copy-Item -LiteralPath (Join-Path $root ('assets\' + $name)) -Destination $assetOutput
}
$apiOutput = Join-Path $assetOutput 'apikey'
New-Item -ItemType Directory -Force -Path $apiOutput | Out-Null
foreach ($name in @('aniClient.txt', 'aniSecret.txt', 'steamapikey.txt', 'twitchclient.txt', 'twitchsecret.txt', 'zerochanuser.txt', 'hytcgconfig.json')) {
    $example = Join-Path (Join-Path $root 'config\examples') $name
    if ($name.EndsWith('.txt') -and (Get-Item -LiteralPath $example).Length -ne 0) { throw ('Public API credential template must be empty: ' + $name) }
    Copy-Item -LiteralPath $example -Destination $apiOutput
}
Copy-Item -LiteralPath (Join-Path $root 'packaging\HYROLauncher.exe.config') -Destination $payload
Copy-Item -LiteralPath (Join-Path $buildOutput 'THIRD-PARTY-NOTICES.md') -Destination $payload
Copy-Item -LiteralPath (Join-Path $buildOutput 'licenses') -Destination $payload -Recurse

$payloadBoundary = [IO.Path]::GetFullPath($payload).TrimEnd('\') + '\'
$files = @(Get-SafeTree $payload | Where-Object { !$_.PSIsContainer })
$manifest = @()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($payloadBoundary.Length).Replace('\', '/')
    if ($relative -match '(^|/)(launcher-data\.json|hytcg-vault\.|cards|hytcg-artworks|hytcg-previews|cache|logs|\.cache|\.sentry-native)(/|$)') { throw ('Private file in release: ' + $relative) }
    if ($relative.StartsWith('assets/apikey/')) {
        $example = Join-Path (Join-Path $root 'config\examples') $file.Name
        if (!(Test-Path -LiteralPath $example) -or (Get-FileHash -LiteralPath $example -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw ('Private API setting in release: ' + $relative) }
    }
    $manifest += [pscustomobject]@{ path = $relative; bytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $release 'payload-manifest.json') -Encoding UTF8

& $CompilerPath '/Qp' ('/DAppVersion=' + $Version) ('/DPayloadDir=' + $payload) ('/DToolsDir=' + $tools) ('/DReleaseDir=' + $release) (Join-Path $root 'packaging\launcher.iss')
if ($LASTEXITCODE -ne 0) { throw ('Inno Setup compilation failed: ' + $LASTEXITCODE) }
$setup = Join-Path $release ('HYROLauncher-Setup-' + $Version + '.exe')
if (!(Test-Path -LiteralPath $setup)) { throw 'Installer output is missing.' }

$portable = Join-Path $release ('HYROLauncher-Portable-' + $Version + '.zip')
if (Test-Path -LiteralPath $portable) { Remove-Item -LiteralPath $portable -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($payload, $portable, [IO.Compression.CompressionLevel]::Optimal, $true)
$artifacts = @($setup, $portable)
$checksums = foreach ($file in $artifacts) { (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($file) }
$checksums | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ASCII
$artifacts | ForEach-Object { Write-Host ('Release artifact: ' + $_ + ' (' + (Get-Item -LiteralPath $_).Length + ' bytes)') }
