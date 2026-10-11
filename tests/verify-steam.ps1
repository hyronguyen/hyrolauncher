param([string]$ExecutableName = 'HYROLauncher.exe', [switch]$Live)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $ProjectRoot ('dist\'+$ExecutableName)))
$service = $assembly.GetType('HYROLauncherWeb.SteamLibraryService', $true)
$normalize = $service.GetMethod('NormalizeId')
if ($normalize.Invoke($null,@('76561198000000000')) -ne '76561198000000000') { throw 'Steam ID rejected' }
if ($normalize.Invoke($null,@('https://steamcommunity.com/profiles/76561198000000000/')) -ne '76561198000000000') { throw 'Profile URL rejected' }
foreach ($value in @('123','76561198000000000&key=secret','https://example.com/profiles/76561198000000000')) {
 if ($normalize.Invoke($null,@($value)) -ne '') { throw 'Invalid Steam ID accepted' }
}
$parse = $service.GetMethod('ParseGames')
$games = $parse.Invoke($null,@('{"response":{"game_count":3,"games":[{"appid":20,"name":"Z","playtime_forever":120},{"appid":10,"name":"A","playtime_forever":-1},{"appid":20,"name":"Duplicate"}]}}'))
if ($games.Count -ne 2 -or $games[0].Name -ne 'A' -or $games[0].PlaytimeMinutes -ne 0 -or $games[1].PlaytimeMinutes -ne 120) { throw 'Game parsing, sorting or deduplication failed' }
if ($parse.Invoke($null,@('{"response":{"game_count":0}}')).Count -ne 0) { throw 'Empty library failed' }
$rejected = $false
try { $null = $parse.Invoke($null,@('{"response":{}}')) } catch { $rejected = $true }
if (!$rejected) { throw 'Private library should be reported as unavailable' }
$settingsType = $assembly.GetType('HYROLauncherWeb.LauncherSettings', $true)
$settings = [Activator]::CreateInstance($settingsType)
$settings.SteamId = '76561198000000000'
$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$restored = $serializer.Deserialize($serializer.Serialize($settings),$settingsType)
if ($restored.SteamId -ne $settings.SteamId) { throw 'Settings roundtrip failed' }
Write-Output 'PASS: ID/profile validation, unsafe input rejection, library parsing, sorting, duplicates, empty/private library, persisted Steam ID.'
$parseProfile = $service.GetMethod('ParseProfile')
$profile = $parseProfile.Invoke($null,@('{"response":{"players":[{"personaname":"My account","avatarfull":"https://avatars.steamstatic.com/avatar.jpg"}]}}'))
if ($profile.Name -ne 'My account' -or !$profile.Avatar.StartsWith('https://')) { throw 'Profile parsing failed' }
$profile = $parseProfile.Invoke($null,@('{"response":{"players":[{"avatarfull":"javascript:bad"}]}}'))
if ($profile.Avatar -ne '') { throw 'Invalid avatar URL accepted' }
Write-Output 'PASS: profile name/avatar parsing and invalid avatar URL rejection.'
if ($Live) {
 $statePath = Join-Path $ProjectRoot 'dist\launcher-data.json'
 $accountId = [string](Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json).Settings.SteamId
 if (!$accountId) { throw 'No configured account for live verification.' }
 $fetchArgs = New-Object object[] 2
 $fetchArgs[0] = [string]$accountId
 $fetchArgs[1] = [string][IO.Path]::Combine($ProjectRoot, 'assets\apikey\steamapikey.txt')
 try { $library = $service.GetMethod('FetchLibrary').Invoke($null,$fetchArgs) }
 catch {
  $cause = $_.Exception.GetBaseException()
  $failureCode = $cause.GetType().Name
  if ($cause -is [Net.WebException]) { $failureCode += ': '+$cause.Status; if ($cause.Response) { $failureCode += ' HTTP '+[int]$cause.Response.StatusCode } }
  if ($cause -is [InvalidOperationException] -and $cause.Message -in @('InvalidId','MissingKey','InvalidKey','LibraryUnavailable')) { $failureCode += ': '+$cause.Message }
  throw ('Live Steam request failed ('+$failureCode+'). Request details and API key have been withheld.')
 }
 Write-Output ('PASS: live Steam API returned '+$library.Games.Count+' games; profile name available='+[bool]$library.Profile.Name+'; avatar available='+[bool]$library.Profile.Avatar)
}
