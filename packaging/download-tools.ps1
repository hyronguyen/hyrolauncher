param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'output\tools' }
$downloadRoot = [IO.Path]::GetFullPath($OutputDirectory)
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'output')).TrimEnd('\') + '\'
if (!$downloadRoot.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Tool downloads must stay under project output.' }
New-Item -ItemType Directory -Force -Path $downloadRoot | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'
$packages = @(
    @{ File = 'innosetup-6.7.3.exe'; Url = 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe'; Publisher = 'Pyrsys'; Kind = 'build-tool' },
    @{ File = 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'; Url = 'https://go.microsoft.com/fwlink/?linkid=2124701'; Publisher = 'Microsoft Corporation'; Kind = 'offline-prerequisite' },
    @{ File = 'NDP48-x86-x64-AllOS-ENU.exe'; Url = 'https://go.microsoft.com/fwlink/?linkid=2088631'; Publisher = 'Microsoft Corporation'; Kind = 'offline-prerequisite' }
)
$records = @()
foreach ($package in $packages) {
    $path = Join-Path $downloadRoot $package.File
    if (!(Test-Path -LiteralPath $path)) {
        Write-Host ('Downloading official package: ' + $package.File)
        $temporary = $path + '.download'
        Invoke-WebRequest -UseBasicParsing -Uri $package.Url -OutFile $temporary -TimeoutSec 900
        $downloadSignature = Get-AuthenticodeSignature -LiteralPath $temporary
        if ($downloadSignature.Status -ne 'Valid' -or !$downloadSignature.SignerCertificate.Subject.Contains($package.Publisher)) {
            throw ('Downloaded publisher signature verification failed: ' + $package.File + ' (' + $downloadSignature.Status + ')')
        }
        Move-Item -LiteralPath $temporary -Destination $path
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne 'Valid' -or !$signature.SignerCertificate.Subject.Contains($package.Publisher)) {
        throw ('Publisher signature verification failed: ' + $package.File + ' (' + $signature.Status + ')')
    }
    $records += [pscustomobject]@{
        file = $package.File; source = $package.Url; kind = $package.Kind
        bytes = (Get-Item -LiteralPath $path).Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        publisher = $signature.SignerCertificate.Subject
        version = (Get-Item -LiteralPath $path).VersionInfo.ProductVersion
    }
}
$records | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $downloadRoot 'provenance.json') -Encoding UTF8
Write-Host 'Official downloads verified.'
