. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
$fixtureSource = @'
namespace HYROLauncherWeb {
    internal static class ApiActivityLog {
        public static string SendText(System.Net.HttpWebRequest request, string provider, byte[] bytes) { throw new System.Exception("UnexpectedNetwork"); }
    }
    public class SteamIgdbFixture : IgdbService {
        public int Requests;
        public bool Fail;
        public string ExternalRows = "[]";
        public string DetailRows = "[]";
        public string LastExternalQuery = "";
        public SteamIgdbFixture(string root) : base(root) { }
        protected override string Query(string body, string endpoint) {
            Requests++;
            if (Fail) throw new System.InvalidOperationException("RateLimited");
            if (endpoint == "external_games") { LastExternalQuery = body; return ExternalRows; }
            if (endpoint != "games") throw new System.Exception("InvalidEndpoint");
            return DetailRows;
        }
    }
}
'@
$taskCacheSource=[regex]::Replace((Get-Content -LiteralPath (Join-Path $ProjectRoot 'LauncherCache.cs') -Raw),'(?m)^using [^\r\n]+;\r?\n','')
Add-Type -TypeDefinition ('using System.Linq;using System.Globalization;' + (Get-Content -LiteralPath (Join-Path $ProjectRoot 'IgdbService.cs') -Raw) + $taskCacheSource + $fixtureSource) -ReferencedAssemblies System.Web.Extensions,System,System.Core
$taskRoot = Join-Path $VerificationRoot ('steam-igdb-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
$checks = 0
function Check([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:checks++
}
$service = New-Object HYROLauncherWeb.SteamIgdbFixture($taskRoot)
$service.ExternalRows = '[{"game":72,"uid":"620","external_game_source":1},{"game":999,"uid":"620","external_game_source":5},{"game":888,"uid":"621","external_game_source":1}]'
$service.DetailRows = '[{"id":72,"name":"Portal 2","summary":"Fixture summary","genres":[{"name":"Puzzle"}],"screenshots":[{"image_id":"shot72"}],"cover":{"image_id":"cover72"}}]'
$info = $service.GetForSteam(620,$false)
Check ($info.Id -eq 72 -and $info.Screenshots.Count -eq 1) 'Steam app must resolve to its exact IGDB release and screenshots.'
Check ($service.Requests -eq 2) 'First selection should make only one mapping and one detail request.'
Check ($service.LastExternalQuery -match 'external_game_source = 1 & uid = "620"' -and $service.LastExternalQuery -notmatch 'search') 'Steam resolution must filter exact app ID/source, never fuzzy title search.'
Check ($service.CachedSteam(620).Id -eq 72) 'Saved Steam link must read its cached metadata.'
$again = $service.GetForSteam(620,$false)
Check ($again.Id -eq 72 -and $service.Requests -eq 2) 'Repeated selection should reuse the cache.'
$reopened = New-Object HYROLauncherWeb.SteamIgdbFixture($taskRoot)
Check ($reopened.GetForSteam(620,$false).Id -eq 72 -and $reopened.Requests -eq 0) 'Steam metadata should survive restart without another API request.'
$service.GetForSteam(620,$true) | Out-Null
Check ($service.Requests -eq 4) 'Explicit refresh should update mapping and metadata.'
Check ([HYROLauncherWeb.IgdbService]::ParseSteamGameId('[{"uid":"620","game":1,"external_game_source":5}]',620) -eq 0) 'Another store with the same ID must not match.'
Check ([HYROLauncherWeb.IgdbService]::ParseSteamGameId('[{"uid":"620","game":1,"external_game_source":1},{"uid":"620","game":2,"external_game_source":1}]',620) -eq 0) 'Ambiguous mappings must not attach another game.'
$service.ExternalRows = '[]'
Check ($null -eq $service.GetForSteam(999,$false)) 'Missing mapping should leave Steam details unavailable.'
$requestCount = $service.Requests
Check ($null -eq $service.GetForSteam(999,$false) -and $service.Requests -eq $requestCount) 'Not found should be cached instead of retrying each selection.'
$reopened = New-Object HYROLauncherWeb.SteamIgdbFixture($taskRoot)
Check ($null -eq $reopened.GetForSteam(999,$false) -and $reopened.Requests -eq 0) 'Not-found cache should survive restart.'
$serializer = New-Object Web.Script.Serialization.JavaScriptSerializer
$negativePath = Join-Path $taskRoot 'igdb-cache/steam-999.json'
[IO.File]::WriteAllText($negativePath,$serializer.Serialize(@{AppId=999;IgdbId=0;UpdatedUtc=[DateTime]::UtcNow.AddDays(-2)}))
$service.GetForSteam(999,$false) | Out-Null
Check ($service.Requests -eq $requestCount + 1) 'Expired negative cache should retry after one day.'
$service.Fail = $true
try { $service.GetForSteam(555,$false) | Out-Null; throw 'Expected rate limit'; }
catch { Check ($_.Exception.GetBaseException().Message -eq 'RateLimited') 'API errors must retain the actionable error code.' }
Check (!(Test-Path -LiteralPath (Join-Path $taskRoot 'igdb-cache/steam-555.json'))) 'Connection/API errors must not become a not-found cache.'
try { $service.GetForSteam(620,$true) | Out-Null; throw 'Expected rate limit'; }
catch { Check ($_.Exception.GetBaseException().Message -eq 'RateLimited') 'Refresh failure must return its API error.' }
Check ($service.CachedSteam(620).Id -eq 72) 'Failed refresh must preserve existing metadata/screenshots.'
$service.Fail = $false
[IO.File]::WriteAllText((Join-Path $taskRoot 'igdb-cache/steam-555.json'),$serializer.Serialize(@{AppId=777;IgdbId=72;UpdatedUtc=[DateTime]::UtcNow}))
Check ($null -eq $service.CachedSteam(555)) 'A cache for another app must not be accepted.'
try { $service.GetForSteam(0,$false) | Out-Null; throw 'Expected invalid ID'; }
catch { Check ($_.Exception.GetBaseException().Message -eq 'InvalidId') 'Invalid app ID should fail before issuing a request.' }
Write-Output ('PASS: ' + $checks + ' Steam IGDB mapping/cache checks; isolated fixtures, no credentials or network.')
