param([Parameter(Mandatory=$true)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskAssembly=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $CandidatePath).Path)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Public,Instance,Static'
$taskReader=$taskAssembly.GetType('HYROLauncherWeb.BookshelfArtworkLighting').GetMethod('ReadColor',$taskFlags)
$taskOutput=Join-Path $VerificationRoot 'bookshelf-lighting-native'
[IO.Directory]::CreateDirectory($taskOutput)|Out-Null
$taskChecks=0
function Assert-Bookshelf($condition,[string]$label) { if(-not $condition){throw $label}; $script:taskChecks++ }
function Write-BookshelfFixture([string]$path,[Drawing.Color]$color,[int]$width=64,[int]$height=64) {
  $taskBitmap=New-Object Drawing.Bitmap($width,$height)
  $taskGraphics=[Drawing.Graphics]::FromImage($taskBitmap)
  try { $taskGraphics.Clear($color);$taskBitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png) }
  finally { $taskGraphics.Dispose();$taskBitmap.Dispose() }
}
function Read-BookshelfColor([string]$path) { $taskReader.Invoke($null,([object[]]@([string]$path))) }
$taskArt=Join-Path $taskOutput 'solid.png'
Write-BookshelfFixture $taskArt ([Drawing.Color]::FromArgb(37,128,186))
Assert-Bookshelf ((Read-BookshelfColor $taskArt)-eq '37 128 186') 'Native pixel sample preserves original artwork RGB.'
Assert-Bookshelf ((Read-BookshelfColor $taskArt)-eq '37 128 186') 'Cached color remains stable.'
Write-BookshelfFixture $taskArt ([Drawing.Color]::FromArgb(232,91,66))
[IO.File]::SetLastWriteTimeUtc($taskArt,[DateTime]::UtcNow.AddSeconds(1))
Assert-Bookshelf ((Read-BookshelfColor $taskArt)-eq '232 91 66') 'Image changes invalidate cached artwork color.'
$taskEmpty=Join-Path $taskOutput 'transparent.png';Write-BookshelfFixture $taskEmpty ([Drawing.Color]::Transparent)
Assert-Bookshelf ((Read-BookshelfColor $taskEmpty)-eq '') 'Transparent artwork produces no ambient light.'
$taskWide=Join-Path $taskOutput 'too-wide.png';Write-BookshelfFixture $taskWide ([Drawing.Color]::Blue) 8193 1
Assert-Bookshelf ((Read-BookshelfColor $taskWide)-eq '') 'Excessive bitmap dimensions are bounded.'
$taskBroken=Join-Path $taskOutput 'malformed.png';[IO.File]::WriteAllText($taskBroken,'Not an image.')
Assert-Bookshelf ((Read-BookshelfColor $taskBroken)-eq '') 'Malformed files remain harmless.'
Assert-Bookshelf ((Read-BookshelfColor (Join-Path $taskOutput 'missing.png'))-eq '') 'Missing image keeps neutral fallback.'
Assert-Bookshelf ((Read-BookshelfColor '\\127.0.0.1\share\unreadable.png')-eq '') 'Color reads do not access network shares.'
$taskLarge=Join-Path $taskOutput 'oversize.png';$taskFile=[IO.File]::Open($taskLarge,[IO.FileMode]::Create)
try {$taskFile.SetLength(16MB+1)} finally {$taskFile.Dispose()}
Assert-Bookshelf ((Read-BookshelfColor $taskLarge)-eq '') 'File reads are capped at 16MB.'
$taskFormType=$taskAssembly.GetType('HYROLauncherWeb.WebLauncherForm')
$taskForm=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskFormType)
$taskStateType=$taskAssembly.GetType('HYROLauncherWeb.LauncherState')
$taskGameType=$taskAssembly.GetType('HYROLauncherWeb.GameEntry')
$taskSettingsType=$taskAssembly.GetType('HYROLauncherWeb.LauncherSettings')
$taskState=[Activator]::CreateInstance($taskStateType)
$taskGames=[Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType(@($taskGameType)))
$taskGame=[Activator]::CreateInstance($taskGameType);$taskGame.Id='approved';$taskGame.IconPath=$taskArt;$taskGames.Add($taskGame)
$taskPrivate=[Activator]::CreateInstance($taskGameType);$taskPrivate.Id='private';$taskPrivate.IconPath=$taskArt;$taskPrivate.Private=$true;$taskGames.Add($taskPrivate)
$taskRemote=[Activator]::CreateInstance($taskGameType);$taskRemote.Id='remote';$taskRemote.IconPath='https://example.test/art.png';$taskGames.Add($taskRemote)
$taskState.Games=$taskGames;$taskState.Settings=[Activator]::CreateInstance($taskSettingsType)
$taskFormType.GetField('state',$taskFlags).SetValue($taskForm,$taskState)
$taskPathMethod=$taskFormType.GetMethod('BookshelfArtworkPath',$taskFlags)
Assert-Bookshelf ($taskPathMethod.Invoke($taskForm,@('approved'))-eq $taskArt) 'Authoritative game ID resolves approved artwork.'
Assert-Bookshelf ($taskPathMethod.Invoke($taskForm,@('private'))-eq '') 'Private artwork cannot leak through a color request.'
Assert-Bookshelf ($taskPathMethod.Invoke($taskForm,@('remote'))-eq '') 'Native helper never downloads remote artwork.'
Assert-Bookshelf ($taskPathMethod.Invoke($taskForm,([object[]]@([string]$taskArt)))-eq '') 'Forged renderer file paths are rejected.'
Assert-Bookshelf ($taskPathMethod.Invoke($taskForm,@('unknown'))-eq '') 'Unknown game IDs cannot read files.'
$taskSteamType=$taskAssembly.GetType('HYROLauncherWeb.SteamGame')
$taskSteamList=[Activator]::CreateInstance([Collections.Generic.List``1].MakeGenericType(@($taskSteamType)))
$taskSteam=[Activator]::CreateInstance($taskSteamType);$taskSteam.AppId=123;$taskSteam.Image=$taskArt;$taskSteamList.Add($taskSteam)
$taskSteamCache=[Activator]::CreateInstance($taskAssembly.GetType('HYROLauncherWeb.SteamLibraryCache'));$taskSteamCache.SteamId='76561198000000000';$taskSteamCache.Games=$taskSteamList
$taskState.Settings.SteamId=$taskSteamCache.SteamId
$taskFormType.GetField('steamLibrary',$taskFlags).SetValue($taskForm,$taskSteamCache)
Assert-Bookshelf ($taskPathMethod.Invoke($taskForm,@('steam-76561198000000000-123'))-eq $taskArt) 'Approved cached Steam artwork is supported.'
$taskState.Settings.SteamId='76561198000000001'
Assert-Bookshelf ($taskPathMethod.Invoke($taskForm,@('steam-76561198000000000-123'))-eq '') 'Steam artwork requires matching current account.'
@{Result='PASS';Checks=$taskChecks;ProductionDataAccessed=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOutput 'report.json') -Encoding UTF8
Write-Output "PASS native bookshelf lighting: $taskChecks checks"
