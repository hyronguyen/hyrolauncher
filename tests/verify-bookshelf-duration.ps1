param([Parameter(Mandatory=$true)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Web.Extensions
$taskAssembly=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $CandidatePath).Path)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Public,Instance,Static'
$taskFormType=$taskAssembly.GetType('HYROLauncherWeb.WebLauncherForm')
$taskForm=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskFormType)
$taskStateType=$taskAssembly.GetType('HYROLauncherWeb.LauncherState')
$taskSettingsType=$taskAssembly.GetType('HYROLauncherWeb.LauncherSettings')
$taskNormalize=$taskFormType.GetMethod('Normalize',$taskFlags)
$taskSerializer=New-Object System.Web.Script.Serialization.JavaScriptSerializer
$taskChecks=0
function Assert-Duration($condition,[string]$label) {if(-not $condition){throw $label};$script:taskChecks++}
function Normalize-Duration([int]$duration) {
  $taskInput=[Activator]::CreateInstance($taskStateType)
  $taskInput.Settings=[Activator]::CreateInstance($taskSettingsType)
  $taskInput.Settings.BookshelfPullDuration=$duration
  $taskNormalize.Invoke($taskForm,([object[]]@($taskInput)))
}
$taskDefault=[Activator]::CreateInstance($taskSettingsType)
Assert-Duration ($taskDefault.BookshelfPullDuration-eq 300) 'Existing default remains 300ms.'
foreach($taskDuration in @(100,300,1000,2500,5000)) {
  $taskNormalized=Normalize-Duration $taskDuration
  Assert-Duration ($taskNormalized.Settings.BookshelfPullDuration-eq $taskDuration) "Native normalization preserves $taskDuration ms."
}
Assert-Duration ((Normalize-Duration 0).Settings.BookshelfPullDuration-eq 100) 'Native normalization bounds values below 100ms.'
Assert-Duration ((Normalize-Duration 5001).Settings.BookshelfPullDuration-eq 5000) 'Native normalization bounds values above 5000ms.'
Assert-Duration ((Normalize-Duration ([int]::MaxValue)).Settings.BookshelfPullDuration-eq 5000) 'Large values remain bounded.'
$taskJson=$taskSerializer.Serialize((Normalize-Duration 5000))
$taskRoundTrip=$taskSerializer.Deserialize($taskJson,$taskStateType)
$taskRoundTrip=$taskNormalize.Invoke($taskForm,([object[]]@($taskRoundTrip)))
Assert-Duration ($taskRoundTrip.Settings.BookshelfPullDuration-eq 5000) 'Five-second preference survives native JSON settings reload.'
Assert-Duration ($taskRoundTrip.Settings.BookshelfAnimationsEnabled) 'Animation setting remains enabled after reload.'
$taskOutput=Join-Path $VerificationRoot 'bookshelf-duration-native'
[IO.Directory]::CreateDirectory($taskOutput)|Out-Null
@{Result='PASS';Checks=$taskChecks;ProductionDataAccessed=$false;MaxMilliseconds=5000;DefaultMilliseconds=300}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOutput 'report.json') -Encoding UTF8
Write-Output "PASS native bookshelf duration: $taskChecks checks"
