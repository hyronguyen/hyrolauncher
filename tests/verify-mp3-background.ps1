
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions,System.Windows.Forms
[AppDomain]::CurrentDomain.SetData('APPBASE',(Join-Path $ProjectRoot 'dist'))
$assembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$ownerType=$assembly.GetType('HYROLauncherWeb.WebLauncherForm',$true)
$windowType=$assembly.GetType('HYROLauncherWeb.PluginBrowserWindow',$true)
$stateType=$assembly.GetType('HYROLauncherWeb.LauncherState',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$parser=New-Object Web.Script.Serialization.JavaScriptSerializer
$owner=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($ownerType)
$webAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $ProjectRoot 'dist/Microsoft.Web.WebView2.WinForms.dll'))
$view=[Activator]::CreateInstance($webAssembly.GetType('Microsoft.Web.WebView2.WinForms.WebView2',$true))
$ownerType.GetField('webView',$flags).SetValue($owner,$view)
$ownerType.GetField('serializer',$flags).SetValue($owner,$parser)
$sample=$parser.Deserialize('{"Games":[{"Id":"public","Name":"Visible"},{"Id":"secret","Name":"Private","Private":true}],"Plugins":[{"Id":"builtin.mp3","Type":"mp3","Mp3Tracks":[]},{"Id":"builtin.pet","Type":"pet","Pets":[]}],"Settings":{"PrivatePinHash":"hidden"}}',$stateType)
$ownerType.GetField('state',$flags).SetValue($owner,$sample)
$ownerType.GetField('currentBuiltInPluginId',$flags).SetValue($owner,'builtin.pet')
$ownerType.GetField('mp3PlaybackActive',$flags).SetValue($owner,$true)
$ownerType.GetField('mp3PlaybackRequest',$flags).SetValue($owner,'playing-request')
$ownerType.GetField('mp3PlaybackPluginId',$flags).SetValue($owner,'builtin.mp3')
$music=[Activator]::CreateInstance($windowType,@($owner,$true))
$other=[Activator]::CreateInstance($windowType,@($owner,$false))
$ownerType.GetField('musicWindow',$flags).SetValue($owner,$music)
$ownerType.GetField('pluginWindow',$flags).SetValue($owner,$other)
try {
 if(![bool]$windowType.GetField('musicPlayer',$flags).GetValue($music) -or [bool]$windowType.GetField('musicPlayer',$flags).GetValue($other)) {throw 'Music player not isolated from general plugins'}
 $uri='file:///test/plugin.html?id=builtin.mp3'
 $windowType.GetField('currentUrl',$flags).SetValue($music,$uri)
 $windowType.GetMethod('HideByUser',$flags).Invoke($music,@())
 if($windowType.GetField('currentUrl',$flags).GetValue($music) -ne $uri -or !$ownerType.GetField('mp3PlaybackActive',$flags).GetValue($owner)) {throw 'Closing player discarded playback or document'}
 if($ownerType.GetField('currentBuiltInPluginId',$flags).GetValue($owner) -ne 'builtin.pet') {throw 'Music close overwrote general plugin state'}
 $closing=New-Object Windows.Forms.FormClosingEventArgs ([Windows.Forms.CloseReason]::UserClosing),$false
 $eventArgs=New-Object object[] 1; $eventArgs[0]=$closing.PSObject.BaseObject
 [Windows.Forms.Form].GetMethod('OnFormClosing',$flags).Invoke($music,$eventArgs)
 if(!$closing.Cancel -or !$ownerType.GetField('mp3PlaybackActive',$flags).GetValue($owner)) {throw 'Alt+F4 disposed music player'}
 $music.ReloadPlugin()
 if($windowType.GetField('currentUrl',$flags).GetValue($music) -ne $uri -or !$ownerType.GetField('mp3PlaybackActive',$flags).GetValue($owner)) {throw 'Refresh interrupted music playback'}
 $closed=New-Object Windows.Forms.FormClosedEventArgs ([Windows.Forms.CloseReason]::UserClosing)
 $eventArgs[0]=$closed.PSObject.BaseObject
 [Windows.Forms.Form].GetMethod('OnFormClosed',$flags).Invoke($other,$eventArgs)
 if(!$ownerType.GetField('mp3PlaybackActive',$flags).GetValue($owner)) {throw 'Other plugin close released MP3 ownership'}
 foreach($id in @('builtin.mp3','builtin.pet')) {
  $snapshot=$ownerType.GetMethod('CreatePluginPanelState',$flags).Invoke($owner,@($id))
  if($snapshot.Plugins.Count -ne 1 -or $snapshot.Plugins[0].Id -ne $id -or $snapshot.Games.Count -ne 1 -or $snapshot.Settings.PrivatePinHash) {throw 'Independent panel state or private filtering failed'}
 }
 $ownerType.GetMethod('HandlePluginMessage',$flags).Invoke($owner,@('{"type":"mp3Playback","pluginId":"builtin.mp3","requestId":"playing-request","playing":false}',$other))
 if(!$ownerType.GetField('mp3PlaybackActive',$flags).GetValue($owner)) {throw 'Another plugin stopped music'}
 $ownerType.GetMethod('HandlePluginMessage',$flags).Invoke($owner,@('{"type":"mp3Playback","pluginId":"builtin.mp3","requestId":"playing-request","playing":false}',$music))
 if($ownerType.GetField('mp3PlaybackActive',$flags).GetValue($owner)) {throw 'Explicit Pause/Stop did not release music ownership'}
 Write-Output 'PASS: dedicated music document, close/Alt+F4/refresh keep playback, other plugin close cannot stop music, per-window private state, only player Pause/Stop releases BGM.'
} finally { $music.Dispose();$other.Dispose();$view.Dispose() }
