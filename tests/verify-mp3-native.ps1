
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions,System.Windows.Forms
[AppDomain]::CurrentDomain.SetData('APPBASE',(Join-Path $ProjectRoot 'dist'))
$assembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$stateType=$assembly.GetType('HYROLauncherWeb.LauncherState',$true)
$form=$assembly.GetType('HYROLauncherWeb.WebLauncherForm',$true)
$parser=New-Object Web.Script.Serialization.JavaScriptSerializer
$sample=$parser.Deserialize('{"Games":[],"Plugins":[{"Id":"builtin.mp3","Type":"mp3","Mp3RepeatMode":"one","Mp3Shuffle":true,"Mp3Volume":0,"Mp3Tracks":[{"Id":"track","Title":"Music","Path":"assets/music.mp3"}],"ActiveTrackId":"track"}],"Settings":{"BgmVolume":35}}',$stateType)
$instanceFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$instance=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($form)
$form.GetField('serializer',$instanceFlags).SetValue($instance,$parser)
$sample=$form.GetMethod('Normalize',$instanceFlags).Invoke($instance,@($sample))
$portable=$form.GetMethod('MakePortableState',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@($sample))
$restored=$parser.Deserialize($parser.Serialize($portable),$stateType)
$player=$restored.Plugins | Where-Object Id -eq 'builtin.mp3'
if($player.Mp3RepeatMode -ne 'one' -or !$player.Mp3Shuffle -or $player.Mp3Volume -ne 0 -or $player.Mp3Tracks.Count -ne 1 -or $player.ActiveTrackId -ne 'track') {throw 'Music preferences or library did not survive storage'}
$player.Mp3RepeatMode='bad';$player.Mp3Volume=200
$restored=$form.GetMethod('Normalize',$instanceFlags).Invoke($instance,@($restored))
if($player.Mp3RepeatMode -ne 'off' -or $player.Mp3Volume -ne 100) {throw 'Invalid player preference not normalized'}
$legacy=$parser.Deserialize('{"Games":[],"Plugins":[{"Id":"builtin.mp3","Type":"mp3"}],"Settings":{"BgmVolume":35}}',$stateType)
$legacy=$form.GetMethod('Normalize',$instanceFlags).Invoke($instance,@($legacy))
$player=$legacy.Plugins | Where-Object Id -eq 'builtin.mp3'
if($player.Mp3Volume -ne 35 -or $player.Mp3RepeatMode -ne 'off') {throw 'Legacy player migration failed'}
Write-Output 'PASS: repeat/shuffle/volume and track library persistence; explicit zero volume; preference bounds; legacy migration.'
