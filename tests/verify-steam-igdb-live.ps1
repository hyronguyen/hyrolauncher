param([int]$AppId = 620)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference = 'Stop'
if ($AppId -le 0) { throw 'Use a positive Steam app ID.' }
$keyRoot = Join-Path $ProjectRoot 'assets/apikey'
if (!(Test-Path -LiteralPath (Join-Path $keyRoot 'twitchclient.txt')) -or !(Test-Path -LiteralPath (Join-Path $keyRoot 'twitchsecret.txt'))) {
    $keyRoot = Join-Path $ProjectRoot 'dist/assets/apikey'
}
if (!(Test-Path -LiteralPath (Join-Path $keyRoot 'twitchclient.txt')) -or !(Test-Path -LiteralPath (Join-Path $keyRoot 'twitchsecret.txt'))) { throw 'MissingCredentials' }
$probeSource = @'
namespace HYROLauncherWeb {
    public class SteamIgdbLiveProbe : IgdbService {
        public int Queries;
        public SteamIgdbLiveProbe(string root) : base(root) { }
        protected override string Query(string body, string endpoint) { Queries++; return base.Query(body,endpoint); }
    }
}
'@
$apiLogSource = [regex]::Replace((Get-Content -LiteralPath (Join-Path $ProjectRoot 'ApiActivityLog.cs') -Raw),'(?m)^using [^\r\n]+;\r?\n','')
$taskCacheSource=[regex]::Replace((Get-Content -LiteralPath (Join-Path $ProjectRoot 'LauncherCache.cs') -Raw),'(?m)^using [^\r\n]+;\r?\n','')
Add-Type -TypeDefinition ('using System.Diagnostics;using System.Linq;using System.Globalization;' + (Get-Content -LiteralPath (Join-Path $ProjectRoot 'IgdbService.cs') -Raw) + $taskCacheSource + $apiLogSource + $probeSource) -ReferencedAssemblies System.Web.Extensions,System,System.Core
$taskRoot = Join-Path $VerificationRoot ('steam-igdb-live-' + [Guid]::NewGuid().ToString('N'))
$taskKeyFolder = Join-Path $taskRoot 'assets/apikey'
New-Item -ItemType Directory -Path $taskKeyFolder -Force | Out-Null
$taskKeys = @((Join-Path $taskKeyFolder 'twitchclient.txt'),(Join-Path $taskKeyFolder 'twitchsecret.txt'))
try {
    Copy-Item -LiteralPath (Join-Path $keyRoot 'twitchclient.txt') -Destination $taskKeys[0]
    Copy-Item -LiteralPath (Join-Path $keyRoot 'twitchsecret.txt') -Destination $taskKeys[1]
    $service = New-Object HYROLauncherWeb.SteamIgdbLiveProbe($taskRoot)
    $info = $service.GetForSteam($AppId,$false)
    if (!$info -or $info.Id -le 0) { throw 'NotFound' }
    $beforeCache = $service.Queries
    $cached = $service.GetForSteam($AppId,$false)
    if (!$cached -or $cached.Id -ne $info.Id -or $service.Queries -ne $beforeCache) { throw 'CacheFailed' }
    Write-Output ('PASS: Steam #' + $AppId + ' -> IGDB #' + $info.Id + ' (' + $info.Name + '), screenshots=' + $info.Screenshots.Count + ', queries=' + $beforeCache + ', second read=cache only. Isolated metadata cache.')
}
catch {
    $cause = $_.Exception.GetBaseException()
    $code = $cause.GetType().Name
    if ($cause -is [Net.WebException]) { $code += ' ' + $cause.Status; if ($cause.Response) { $code += ' HTTP ' + [int]$cause.Response.StatusCode } }
    if ($cause.Message -in @('MissingCredentials','InvalidCredentials','AuthenticationFailed','RateLimited','NotFound','CacheFailed')) { $code += ' ' + $cause.Message }
    throw ('Steam IGDB live probe failed (' + $code + '). Credentials and request details withheld.')
}
finally {
    foreach ($taskKey in $taskKeys) { if (Test-Path -LiteralPath $taskKey) { Remove-Item -LiteralPath $taskKey -Force } }
}
