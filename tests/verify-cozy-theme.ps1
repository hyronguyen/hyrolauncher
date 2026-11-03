param([Parameter(Mandatory=$true)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Web.Extensions
$taskAssembly=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $CandidatePath).Path)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Public,Instance,Static'
$taskFormType=$taskAssembly.GetType('HYROLauncherWeb.WebLauncherForm',$true)
# Avoid the real launcher constructor: it loads/saves user data and creates its
# tray icon. These fixtures exercise the native settings path in memory only.
$taskForm=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskFormType)
$taskStateType=$taskAssembly.GetType('HYROLauncherWeb.LauncherState',$true)
$taskSettingsType=$taskAssembly.GetType('HYROLauncherWeb.LauncherSettings',$true)
$taskNormalize=$taskFormType.GetMethod('Normalize',$taskFlags)
$taskModeNormalize=$taskFormType.GetMethod('NormalizeThemeMode',$taskFlags)
$taskWindowsNormalize=$taskFormType.GetMethod('NormalizeThemeModeForWindows',$taskFlags)
$taskCurrentMode=$taskFormType.GetMethod('CurrentThemeMode',$taskFlags)
$taskSerializer=New-Object System.Web.Script.Serialization.JavaScriptSerializer
$taskChecks=0
function Check-Cozy([bool]$condition,[string]$message){if(!$condition){throw $message};$script:taskChecks++}
foreach($taskPair in @(@('cozy','cozy'),@(' Cozy ','cozy'),@('COZY','cozy'),@('dark','dark'),@('light','light'),@('translucent','translucent'),@('invalid','dark'),@('','dark'),@($null,'dark'))){
 $taskInput=New-Object object[] 1;$taskInput[0]=$taskPair[0]
 Check-Cozy ($taskModeNormalize.Invoke($null,$taskInput)-eq $taskPair[1]) ('Native theme normalization: '+$taskPair[0])
 Check-Cozy ($taskWindowsNormalize.Invoke($null,$taskInput)-eq $taskPair[1]) ('Auxiliary windows inherit normalized theme: '+$taskPair[0])
}
foreach($taskLayout in @('default','carousel','bookshelf')){
 $taskState=[Activator]::CreateInstance($taskStateType)
 $taskState.Settings=[Activator]::CreateInstance($taskSettingsType)
 $taskState.Settings.ThemeMode='cozy';$taskState.Settings.LibraryLayout=$taskLayout
 $taskState.Settings.AccentColor='#EC936F';$taskState.Settings.Language='vi'
 $taskNormalized=$taskNormalize.Invoke($taskForm,[object[]]@($taskState))
 Check-Cozy ($taskNormalized.Settings.ThemeMode-eq 'cozy') ('Cozy survives native settings normalization for '+$taskLayout)
 Check-Cozy ($taskNormalized.Settings.LibraryLayout-eq $taskLayout) ('Cozy preserves selected library layout: '+$taskLayout)
 $taskJson=$taskSerializer.Serialize($taskNormalized)
 $taskReloaded=$taskSerializer.Deserialize($taskJson,$taskStateType)
 $taskReloaded=$taskNormalize.Invoke($taskForm,[object[]]@($taskReloaded))
 Check-Cozy ($taskReloaded.Settings.ThemeMode-eq 'cozy') ('Cozy persists through JSON save/reload for '+$taskLayout)
 Check-Cozy ($taskReloaded.Settings.AccentColor-eq '#EC936F') 'Cozy preserves the existing user accent color.'
 $taskFormType.GetField('state',$taskFlags).SetValue($taskForm,$taskReloaded)
 Check-Cozy ($taskCurrentMode.Invoke($taskForm,$null)-eq 'cozy') 'Current native theme is cozy for subsequent plugin windows.'
}
Write-Output ('PASS: Cozy native settings/window inheritance, '+$taskChecks+' checks; in-memory fixtures, no user data or WebView profile access.')
