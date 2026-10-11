param([string]$InstallerPath = '', [string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!$InstallerPath) { $InstallerPath = Join-Path $root ('output\release\HYROLauncher-Setup-' + $Version + '.exe') }
$installer = [IO.Path]::GetFullPath($InstallerPath)
$outputBoundary = [IO.Path]::GetFullPath((Join-Path $root 'output')).TrimEnd('\') + '\'
if (!$installer.StartsWith($outputBoundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Verify an installer under project output only.' }
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{58D33B1E-B32E-4DBA-A064-F8DF578E6B0F}_is1'
if (Test-Path -LiteralPath $uninstallKey) { throw 'HYRO is already installed. This verification will not modify an existing installed application.' }
$qa = Join-Path $root ('output\installer-qa\' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $qa 'app'
New-Item -ItemType Directory -Force -Path $qa | Out-Null
$checks = New-Object 'System.Collections.Generic.List[object]'
function Assert-Check([bool]$Condition, [string]$Name) {
    $checks.Add([pscustomobject]@{ name = $Name; passed = $Condition })
    if (!$Condition) { throw ('Installer verification failed: ' + $Name) }
}
function File-State([string]$Directory) {
    $state = @{}
    if (Test-Path -LiteralPath $Directory) {
        foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File -Force) {
            if ($file.FullName -match '[\\/](hytcg-vault\.(dat|stamp)|launcher-data\.json|cards|hytcg-artworks|assets[\\/]apikey)([\\/]|$)') {
                $state[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            }
        }
    }
    return $state
}
function Run-Setup([string]$LogName) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/TASKS=', ('/DIR="' + $app + '"'), ('/LOG="' + (Join-Path $qa $LogName) + '"'))
    $process = Start-Process -FilePath $installer -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    Assert-Check ($process.ExitCode -eq 0) ('Setup exits successfully: ' + $LogName)
}
$production = File-State (Join-Path $root 'dist')
$manifest = Get-Content -LiteralPath (Join-Path $root 'output\release\payload-manifest.json') -Raw | ConvertFrom-Json
$installed = $false
try {
    Run-Setup 'install.log'
    $installed = $true
    foreach ($entry in $manifest) {
        $path = Join-Path $app $entry.path
        Assert-Check (Test-Path -LiteralPath $path) ('Installed payload exists: ' + $entry.path)
        Assert-Check ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.sha256) ('Installed payload matches: ' + $entry.path)
    }
    Assert-Check (Test-Path -LiteralPath (Join-Path $app 'unins000.exe')) 'Uninstaller installed'
    Assert-Check (Test-Path -LiteralPath $uninstallKey) 'Registered in Windows Apps'
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $app 'cards'))) 'No user cards included'
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $app 'launcher-data.json'))) 'No user library included'
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $app 'hytcg-vault.dat'))) 'No user account included'
    Assert-Check (!(Get-Content -LiteralPath (Join-Path $qa 'install.log') -Raw).Contains('Installing Microsoft')) 'Existing prerequisites skipped'

    # A host invokes the same packaged form without claiming the production
    # launcher's single-instance mutex. BaseDirectory remains the isolated app.
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $hostPath = Join-Path $app 'InstallerSmokeHost.exe'
    & $compiler /nologo /platform:x64 /target:winexe (('/out:' + $hostPath)) /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll ('/reference:' + (Join-Path $app 'Microsoft.Web.WebView2.Core.dll')) ('/reference:' + (Join-Path $app 'Microsoft.Web.WebView2.WinForms.dll')) (Join-Path $PSScriptRoot 'verify-installer-smoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Installer smoke host compilation failed.' }
    $smokeReport = Join-Path $qa 'startup.json'
    $process = Start-Process -FilePath $hostPath -ArgumentList ('"' + $smokeReport + '"') -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Packaged app startup timed out.' }
    Assert-Check ($process.ExitCode -eq 0) 'Packaged app initializes real WebView2'
    $smoke = Get-Content -LiteralPath $smokeReport -Raw | ConvertFrom-Json
    Assert-Check ($smoke.ready -eq $true) 'Packaged launcher UI is ready'
    Remove-Item -LiteralPath $hostPath -Force

    New-Item -ItemType Directory -Force -Path (Join-Path $app 'cards') | Out-Null
    [IO.File]::WriteAllText((Join-Path $app 'cards\local-progress.sentinel'), 'local card ownership sentinel')
    [IO.File]::WriteAllText((Join-Path $app 'hytcg-vault.dat'), 'local account sentinel')
    [IO.File]::WriteAllText((Join-Path $app 'launcher-data.json'), 'local library sentinel')
    [IO.File]::WriteAllText((Join-Path $app 'assets\apikey\steamapikey.txt'), 'private local credential sentinel')
    $userFiles = File-State $app
    Run-Setup 'upgrade.log'
    foreach ($path in $userFiles.Keys) {
        Assert-Check ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $userFiles[$path]) ('Upgrade preserves user file: ' + $path.Substring($app.Length + 1))
    }
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $qa 'uninstall.log') + '"'))
    $process = Start-Process -FilePath (Join-Path $app 'unins000.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    Assert-Check ($process.ExitCode -eq 0) 'Uninstall exits successfully'
    $installed = $false
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $app 'HYROLauncher.exe'))) 'Uninstall removes launcher binary'
    Assert-Check (!(Test-Path -LiteralPath (Join-Path $app 'ui\index.html'))) 'Uninstall removes bundled UI'
    Assert-Check (!(Test-Path -LiteralPath $uninstallKey)) 'Uninstall removes Windows Apps registration'
    foreach ($path in $userFiles.Keys) {
        Assert-Check (Test-Path -LiteralPath $path) ('Uninstall retains user file: ' + $path.Substring($app.Length + 1))
        Assert-Check ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $userFiles[$path]) ('Uninstall preserves user content: ' + $path.Substring($app.Length + 1))
    }
    $after = File-State (Join-Path $root 'dist')
    Assert-Check ($after.Count -eq $production.Count) 'Existing launcher private file count is unchanged'
    foreach ($path in $production.Keys) { Assert-Check ($after[$path] -eq $production[$path]) ('Existing launcher file is untouched: ' + [IO.Path]::GetFileName($path)) }
    [pscustomobject]@{ success = $true; checks = $checks.Count; installerSha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant(); installDirectory = $app; startup = $smoke; results = @($checks.ToArray()) } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $qa 'report.json') -Encoding UTF8
    Write-Output ('Installer verification PASS: ' + $checks.Count + ' checks; ' + (Join-Path $qa 'report.json'))
} finally {
    if ($installed -and (Test-Path -LiteralPath (Join-Path $app 'unins000.exe'))) {
        Start-Process -FilePath (Join-Path $app 'unins000.exe') -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -WindowStyle Hidden -Wait
    }
}
