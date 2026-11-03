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
Assert-Duration ($taskDefault.BookshelfBacklightMode-eq 'accent') 'Existing accounts default to accent backlighting.'
foreach($taskMode in @('accent','rgb','rgb-cycle','invalid','')) {
 $taskInput=[Activator]::CreateInstance($taskStateType)
 $taskInput.Settings=[Activator]::CreateInstance($taskSettingsType)
 $taskInput.Settings.BookshelfBacklightMode=$taskMode
 $taskNormalized=$taskNormalize.Invoke($taskForm,([object[]]@($taskInput)))
 $taskExpected=if($taskMode-in @('rgb','rgb-cycle')){$taskMode}else{'accent'}
 Assert-Duration ($taskNormalized.Settings.BookshelfBacklightMode-eq $taskExpected) 'Backlight modes are bounded to supported options.'
}
foreach($taskMode in @('rgb','rgb-cycle')) {
 $taskInput.Settings.BookshelfBacklightMode=$taskMode
 $taskReloaded=$taskSerializer.Deserialize($taskSerializer.Serialize($taskInput),$taskStateType)
 Assert-Duration ($taskNormalize.Invoke($taskForm,([object[]]@($taskReloaded))).Settings.BookshelfBacklightMode-eq $taskMode) "$taskMode preference survives native JSON settings reload."
}
Assert-Duration ($taskDefault.BookshelfRgbSpeed-eq 100) 'Existing accounts default to normal RGB speed.'
$taskLegacyState=$taskSerializer.Deserialize('{"Settings":{"BookshelfBacklightMode":"rgb-cycle"}}',$taskStateType)
Assert-Duration ($taskNormalize.Invoke($taskForm,([object[]]@($taskLegacyState))).Settings.BookshelfRgbSpeed-eq 100) 'Settings saved before the RGB speed option retain normal speed.'
foreach($taskRgbSpeed in @(25,50,100,200,400,0,401,[int]::MaxValue)) {
 $taskInput=[Activator]::CreateInstance($taskStateType)
 $taskInput.Settings=[Activator]::CreateInstance($taskSettingsType)
 $taskInput.Settings.BookshelfRgbSpeed=$taskRgbSpeed
 $taskNormalized=$taskNormalize.Invoke($taskForm,([object[]]@($taskInput)))
 $taskExpected=[Math]::Max(25,[Math]::Min(400,$taskRgbSpeed))
 Assert-Duration ($taskNormalized.Settings.BookshelfRgbSpeed-eq $taskExpected) "Native RGB speed preserves valid values and bounds $taskRgbSpeed percent."
}
foreach($taskRgbSpeed in @(25,200,400)) {
 $taskInput.Settings.BookshelfRgbSpeed=$taskRgbSpeed
 $taskReloaded=$taskSerializer.Deserialize($taskSerializer.Serialize($taskInput),$taskStateType)
 Assert-Duration ($taskNormalize.Invoke($taskForm,([object[]]@($taskReloaded))).Settings.BookshelfRgbSpeed-eq $taskRgbSpeed) "RGB speed $taskRgbSpeed percent survives native JSON settings reload."
}
$taskOutput=Join-Path $VerificationRoot 'bookshelf-duration-native'
[IO.Directory]::CreateDirectory($taskOutput)|Out-Null
@{Result='PASS';Checks=$taskChecks;ProductionDataAccessed=$false;MaxMilliseconds=5000;DefaultMilliseconds=300}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOutput 'report.json') -Encoding UTF8
Write-Output "PASS native bookshelf duration: $taskChecks checks"
