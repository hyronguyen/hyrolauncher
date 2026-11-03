param([string]$CandidatePath='')
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions
if(!$CandidatePath){$CandidatePath=Get-TestExecutable}
$taskAssembly=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($CandidatePath))
$taskType=$taskAssembly.GetType('HYROLauncherWeb.LauncherCache',$true)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Public,Static,Instance'
$taskRoot=Join-Path $VerificationRoot ('launcher-cache-'+[Guid]::NewGuid().ToString('N'))
$taskApp=Join-Path $taskRoot 'app';$taskWeb=Join-Path $taskRoot 'web';$taskOutside=Join-Path $taskRoot 'outside'
foreach($taskDir in @($taskApp,$taskWeb,$taskOutside)){New-Item -ItemType Directory -Path $taskDir -Force | Out-Null}
$taskChecks=0
function Check-Cache([bool]$condition,[string]$message){if(!$condition){throw $message};$script:taskChecks++}
function Call-Cache([string]$method,[object[]]$arguments){
 $nativeMethod=$taskType.GetMethod($method,$taskFlags);$nativeParameters=$nativeMethod.GetParameters();$nativeArgs=New-Object object[] $arguments.Count
 for($index=0;$index-lt $arguments.Count;$index++){$nativeArgs[$index]=[Convert]::ChangeType($arguments[$index],$nativeParameters[$index].ParameterType,[Globalization.CultureInfo]::InvariantCulture)}
 $nativeMethod.Invoke($null,$nativeArgs)
}
function Write-Fixture([string]$relative,[string]$value='fixture',[string]$base=$taskApp){
 $path=Join-Path $base $relative;New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($path)) -Force | Out-Null;[IO.File]::WriteAllText($path,$value);return $path
}
function Bytes-Fixture([string]$path){[IO.File]::ReadAllBytes($path)}
function Field-Cache($object,[string]$name){$object.GetType().GetField($name,$taskFlags).GetValue($object)}
$taskHash='a'*64
$taskPreserve=@(
 (Write-Fixture 'cards/UR_original.hycard' 'owned-card'),
 (Write-Fixture '.cache/hytcg-frame-artwork/frame.bin' 'imported-frame'),
 (Write-Fixture '.cache/hytcg-discovery.bin' 'banner-history'),
 (Write-Fixture '.cache/hytcg-characters/favourites.bin' 'favourites'),
 (Write-Fixture '.cache/hytcg-characters/character.bin' 'character-metadata'),
 (Write-Fixture 'hytcg-account.bin' 'wallet'),
 (Write-Fixture 'launcher-data.json' 'settings'),
 (Write-Fixture 'steam-library-cache.json' 'owned-games'),
 (Write-Fixture 'assets/settings/background.png' 'imported-art'),
 (Write-Fixture 'assets/library/owned/logo.png' 'owned-game-art'),
 (Write-Fixture 'assets/apikey/secret.txt' 'test-key'),
 (Write-Fixture 'igdb-cache/keep-imported.png' 'not-cache'),
 (Write-Fixture 'explore-cache/notes.json' 'not-cache'),
 (Write-Fixture 'hytcg-previews/card.hycard' 'not-cache'),
 (Write-Fixture 'EBWebView/Default/Network/Cookies' 'login' $taskWeb),
 (Write-Fixture 'EBWebView/Default/Local Storage/leveldb/state' 'site-progress' $taskWeb),
 (Write-Fixture 'EBWebView/Default/IndexedDB/db/value' 'site-database' $taskWeb),
 (Write-Fixture 'EBWebView/Default/Login Data' 'saved-passwords' $taskWeb),
 (Write-Fixture 'EBWebView/Default/Service Worker/CacheStorage/offline' 'site-offline' $taskWeb)
)
$taskBefore=@{};foreach($taskPath in $taskPreserve){$taskBefore[$taskPath]=[Convert]::ToBase64String((Bytes-Fixture $taskPath))}
$taskDisposable=@(
 (Write-Fixture 'igdb-cache/2.json' ''),
 (Write-Fixture 'igdb-cache/72.json' '{"id":72}'),
 (Write-Fixture 'igdb-cache/steam-620.json' '{"AppId":620}'),
 (Write-Fixture ("explore-cache/$taskHash.json") '{}'),
 (Write-Fixture 'background-cache/76561198000000000.json' '{}'),
 (Write-Fixture ("hytcg-previews/$taskHash.hypreview") 'encrypted-preview'),
 (Write-Fixture ("cache/pet-timing/$taskHash-200.gif") 'derived-gif'),
 (Write-Fixture 'EBWebView/Default/Cache/Cache_Data/item' 'web-image-cache' $taskWeb),
 (Write-Fixture 'EBWebView/Default/Code Cache/js/script' 'compiled-js' $taskWeb),
 (Write-Fixture 'EBWebView/GPUCache/shader' 'compiled-shader' $taskWeb)
)
$taskExpected=0L;foreach($taskPath in $taskDisposable){$taskExpected+=(Get-Item -LiteralPath $taskPath).Length}
$taskCategories=Call-Cache 'Inspect' @($taskApp,$taskWeb)
Check-Cache (($taskCategories | Measure-Object Bytes -Sum).Sum -eq $taskExpected) 'Only allowlisted recreatable files appear as reclaimable.'
Check-Cache ($taskCategories.Count-eq 6) 'All six cache categories are exposed.'
Check-Cache (($taskCategories | Measure-Object Files -Sum).Sum-eq $taskDisposable.Count) 'Zero-byte cache files are included in file counts.'
$taskStorage=Call-Cache 'ReadStorage' @($taskApp,$taskWeb)
Check-Cache ((Field-Cache $taskStorage 'OwnedBytes') -gt $taskExpected) 'Single-pass storage includes preserved application data.'
Check-Cache ((Field-Cache $taskStorage 'CardBytes')-eq (Get-Item -LiteralPath $taskPreserve[0]).Length) 'Owned card bytes are counted separately.'
Check-Cache ((Field-Cache $taskStorage 'Categories' | Measure-Object Bytes -Sum).Sum-eq $taskExpected) 'Single-pass cache categorization matches targeted inspection.'
$taskNestedApp=Join-Path $taskRoot 'nested-app';$taskNestedWeb=Join-Path $taskNestedApp 'browser-data'
$taskNestedOwned=Write-Fixture 'cards/a.hycard' 'owned' $taskNestedApp
$taskNestedCache=Write-Fixture 'EBWebView/Default/Cache/entry' 'web-cache' $taskNestedWeb
$taskNestedSnapshot=Call-Cache 'ReadStorage' @($taskNestedApp,$taskNestedWeb)
Check-Cache ((Field-Cache $taskNestedSnapshot 'OwnedBytes')-eq (Get-Item -LiteralPath $taskNestedOwned).Length) 'A nested WebView profile is excluded from owned application bytes.'
Check-Cache ((Field-Cache $taskNestedSnapshot 'WebBytes')-eq (Get-Item -LiteralPath $taskNestedCache).Length) 'Nested WebView storage is measured once.'
Check-Cache ((Field-Cache $taskNestedSnapshot 'Categories' | Measure-Object Bytes -Sum).Sum-eq (Get-Item -LiteralPath $taskNestedCache).Length) 'Nested WebView cache categories do not double-count.'
Check-Cache (!(Call-Cache 'SafePath' @($taskApp,(Join-Path $taskOutside 'file')))) 'Sibling path traversal is rejected.'
Check-Cache (!(Call-Cache 'SafePath' @($taskApp,(Join-Path $taskApp '../outside/file')))) 'Relative parent traversal is rejected.'
$taskExternal=Write-Fixture ("$taskHash.hypreview") 'external-owned-card' $taskOutside
$taskLink=Join-Path $taskApp 'hytcg-previews/linked'
New-Item -ItemType Junction -Path $taskLink -Target $taskOutside | Out-Null
Check-Cache (!(Call-Cache 'SafePath' @($taskApp,(Join-Path $taskLink "$taskHash.hypreview")))) 'Junction traversal is rejected.'
Check-Cache (!(Call-Cache 'SafePath' @($taskLink,$taskLink))) 'A linked cache root is rejected.'
$taskLinkedRoot=Join-Path $taskApp 'cache'
$taskNestedOutside=Join-Path $taskOutside 'pet-timing';New-Item -ItemType Directory -Path $taskNestedOutside -Force | Out-Null
$taskExternalPet=Write-Fixture "pet-timing/$taskHash-300.gif" 'external-pet' $taskOutside
$taskLocked=$taskDisposable[1];$taskStream=[IO.File]::Open($taskLocked,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
try {$taskClean=Call-Cache 'Clean' @($taskApp,$taskWeb);Check-Cache ($taskClean.SkippedFiles-ge 1) 'Locked cache entries are skipped, never force-closed.'} finally {$taskStream.Dispose()}
Check-Cache ($taskClean.FreedBytes-eq ($taskExpected-(Get-Item -LiteralPath $taskLocked).Length)) 'Freed disk bytes report successful deletions only.'
Check-Cache ($taskClean.DeletedFiles-eq ($taskDisposable.Count-1)) 'Every unlocked eligible cache file is removed.'
foreach($taskPath in $taskPreserve){Check-Cache ((Test-Path -LiteralPath $taskPath) -and [Convert]::ToBase64String((Bytes-Fixture $taskPath))-eq $taskBefore[$taskPath]) ('Preserve '+[IO.Path]::GetFileName($taskPath))}
Check-Cache (([IO.File]::ReadAllText($taskExternal))-eq 'external-owned-card') 'External junction target survives cleanup.'
Check-Cache (([IO.File]::ReadAllText($taskExternalPet))-eq 'external-pet') 'Unrelated external resource survives cleanup.'
$taskAgain=Call-Cache 'Clean' @($taskApp,$taskWeb)
Check-Cache ($taskAgain.DeletedFiles-eq 1 -and $taskAgain.FreedBytes-gt 0) 'Retry cleans a formerly locked file.'
Check-Cache ((Call-Cache 'Clean' @($taskApp,$taskWeb)).FreedBytes-eq 0) 'Cleanup is idempotent.'
$taskJson=Join-Path $taskApp 'igdb-cache/100.json'
Check-Cache (Call-Cache 'WriteText' @($taskApp,$taskJson,'{"id":100}')) 'Atomic metadata cache write succeeds.'
Check-Cache ((Call-Cache 'ReadText' @($taskApp,$taskJson,65536))-eq '{"id":100}') 'Bounded metadata read round-trips.'
Check-Cache ($null-eq (Call-Cache 'ReadText' @($taskApp,$taskJson,1))) 'Oversized cache is rejected before text decoding.'
Check-Cache (!(Call-Cache 'WriteText' @($taskApp,(Join-Path $taskApp '../outside/steal.json'),'not-allowed'))) 'Cache writes cannot escape the application root.'
$taskEvict=Join-Path $taskApp 'explore-cache'
for($taskIndex=0;$taskIndex-lt 330;$taskIndex++){
 $taskName=$taskIndex.ToString('x64')+'.json';$taskPath=Write-Fixture ('explore-cache/'+$taskName) 'metadata';[IO.File]::SetLastWriteTimeUtc($taskPath,[DateTime]::UtcNow.AddHours(-2));[IO.File]::SetLastAccessTimeUtc($taskPath,[DateTime]::UtcNow.AddHours(-2))
}
$taskHot=Join-Path $taskEvict ((0).ToString('x64')+'.json');Call-Cache 'Touch' @($taskApp,$taskHot) | Out-Null
$taskExpired=Join-Path $taskEvict ((1).ToString('x64')+'.json');[IO.File]::SetLastWriteTimeUtc($taskExpired,[DateTime]::UtcNow.AddDays(-10));[IO.File]::SetLastAccessTimeUtc($taskExpired,[DateTime]::UtcNow.AddDays(-10))
$taskIncomplete=Write-Fixture ('explore-cache/'+$taskHash+'.json.'+('f'*32)+'.tmp') 'abandoned';[IO.File]::SetLastWriteTimeUtc($taskIncomplete,[DateTime]::UtcNow.AddDays(-2))
$taskActiveWrite=Write-Fixture ('explore-cache/'+$taskHash+'.json.'+('e'*32)+'.tmp') 'active'
Call-Cache 'Maintain' @($taskApp,$taskEvict,$true) | Out-Null
Check-Cache ((Get-ChildItem -LiteralPath $taskEvict -File | Where-Object Name -Match '^[a-f0-9]{64}\.json$').Count-le 300) 'Explore metadata disk cache is bounded.'
Check-Cache (Test-Path -LiteralPath $taskHot) 'Recently accessed metadata is retained.'
Check-Cache (!(Test-Path -LiteralPath $taskExpired)) 'Expired unused metadata is evicted.'
Check-Cache (!(Test-Path -LiteralPath $taskIncomplete)) 'Abandoned atomic writes are removed.'
Check-Cache (Test-Path -LiteralPath $taskActiveWrite) 'Recent cache writes are preserved.'
Check-Cache (Test-Path -LiteralPath (Join-Path $taskEvict 'notes.json')) 'Cache maintenance preserves unrecognized files.'
$taskOrphan=Join-Path $taskApp 'assets/library/orphan';Write-Fixture 'assets/library/orphan/deep/image.png' 'orphan' | Out-Null
Check-Cache (Call-Cache 'DeleteUnusedAssetDirectory' @((Join-Path $taskApp 'assets/library'),$taskOrphan)) 'Unreferenced asset cleanup deletes only a checked child tree.'
Check-Cache (!(Call-Cache 'DeleteUnusedAssetDirectory' @((Join-Path $taskApp 'assets/library'),$taskOutside))) 'Unreferenced asset cleanup rejects an external directory.'
$taskOrphanLinked=Join-Path $taskApp 'assets/library/orphan-linked';New-Item -ItemType Directory -Path $taskOrphanLinked -Force | Out-Null
New-Item -ItemType Junction -Path (Join-Path $taskOrphanLinked 'outside-link') -Target $taskOutside | Out-Null
Check-Cache (!(Call-Cache 'DeleteUnusedAssetDirectory' @((Join-Path $taskApp 'assets/library'),$taskOrphanLinked))) 'Linked children are preserved, never recursively traversed.'
Check-Cache (([IO.File]::ReadAllText($taskExternal))-eq 'external-owned-card') 'Unused asset cleanup preserves external linked files.'
$taskFormType=$taskAssembly.GetType('HYROLauncherWeb.WebLauncherForm',$true)
$taskReferenced=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$taskReferenceTree=New-Object 'System.Collections.Generic.Dictionary[string,object]'
$taskSharedAssetRoot=Join-Path $taskApp 'assets/library'
$taskReferenceTree.Add('Games',@(@{IconPath=(Join-Path $taskSharedAssetRoot 'retired-owner/image.png')}))
$taskReferenceTree.Add('Plugins',@(@{Mp3Tracks=@(@{Path=(Join-Path $taskSharedAssetRoot 'shared-music/song.mp3')});WatchItems=@(@{PosterPath=(Join-Path $taskSharedAssetRoot 'shared-poster/cover.png')})}))
$taskReferenceTree.Add('Settings',@{LibraryBackground=(Join-Path $taskSharedAssetRoot 'shared-background/cover.png')})
$taskReferenceParser=New-Object System.Web.Script.Serialization.JavaScriptSerializer
$taskNativeReferenceTree=$taskReferenceParser.DeserializeObject(($taskReferenceTree | ConvertTo-Json -Depth 12 -Compress))
$taskFormType.GetMethod('CollectReferencedAssetIds',$taskFlags).Invoke($null,[object[]]@($taskNativeReferenceTree.PSObject.BaseObject,[string]$taskSharedAssetRoot,$taskReferenced.PSObject.BaseObject)) | Out-Null
Check-Cache ($taskReferenced.Contains('retired-owner')) 'Referenced artwork is preserved even when its original game ID was removed.'
Check-Cache ($taskReferenced.Contains('shared-music')) 'An active music track preserves its referenced asset folder.'
Check-Cache ($taskReferenced.Contains('shared-poster')) 'An active watchlist poster preserves its referenced asset folder.'
Check-Cache ($taskReferenced.Contains('shared-background')) 'An active library background preserves its referenced asset folder.'
$taskSource=Get-Content -LiteralPath (Join-Path $ProjectRoot 'WebLauncher.cs') -Raw
Check-Cache ($taskSource-match 'CoreWebView2BrowsingDataKinds\.DiskCache') 'Manual cleanup uses the native disk-cache-only API.'
Check-Cache ($taskSource-match 'systemStorageTask=Task\.Run') 'Disk storage scans execute away from the UI thread.'
Check-Cache ($taskSource-match 'systemStorageScannedUtc<DateTime\.UtcNow\.AddSeconds\(-60\)') 'System storage snapshots are coalesced and cached.'
[IO.File]::WriteAllText((Join-Path $taskRoot 'report.json'),('{"checks":'+$taskChecks+',"freedBytes":'+$taskClean.FreedBytes+',"preservedFiles":'+$taskPreserve.Count+'}'))
Write-Output ('PASS: Launcher cache safety/performance, '+$taskChecks+' checks; isolated fixtures at '+$taskRoot)
