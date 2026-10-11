
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions,System.Drawing
[AppDomain]::CurrentDomain.SetData('APPBASE',(Join-Path $ProjectRoot 'dist'))
$assembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$timing=$assembly.GetType('HYROLauncherWeb.PetAnimationTiming',$true)
$normalize=$timing.GetMethod('Normalize')
foreach($pair in @(@(0,0),@(-1,0),@(5,20),@(25,30),@(500,500),@([int]::MaxValue,2000))) {
 if($normalize.Invoke($null,@([int]$pair[0])) -ne $pair[1]) {throw 'Frame delay normalization failed'}
}
$directory=Join-Path $ProjectRoot 'output/verification/pet-timing'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$report=@()
foreach($name in @('hyro-buddy','kafka','kled-skaarl')) {
 foreach($state in @('idle','drag')) {
  $source=Join-Path $ProjectRoot ('assets/pets/'+$name+'-'+$state+'.gif')
  $before=(Get-FileHash -LiteralPath $source).Hash
  $arguments=New-Object object[] 3
  $arguments[0]=[string]$source; $arguments[1]=[int]300; $arguments[2]=[string]$directory
  $path=$timing.GetMethod('PlaybackPath').Invoke($null,$arguments)
  $image=[Drawing.Image]::FromFile($path)
  try {
   if($image.GetFrameCount([Drawing.Imaging.FrameDimension]::Time) -ne 8) {throw 'Retiming lost frames'}
   $delays=$image.GetPropertyItem(0x5100).Value
   for($i=0;$i -lt 8;$i++) {if([BitConverter]::ToInt32($delays,$i*4) -ne 30) {throw 'Frame delay did not update every frame'}}
  } finally {$image.Dispose()}
  if($before -ne (Get-FileHash -LiteralPath $source).Hash) {throw 'Original GIF was modified'}
  $arguments[1]=[int]0
  if($timing.GetMethod('PlaybackPath').Invoke($null,$arguments) -ne $source) {throw 'Original timing did not restore source'}
  $report+=@{source=$source;timed=$path}
 }
}
$argsRetime=New-Object object[] 2
$argsRetime[0]=[Convert]::FromBase64String('R0lGODdhAQABAIAAAAAAAP///ywAAAAAAQABAAACAkQBADs='); $argsRetime[1]=[int]250
$fixture=$timing.GetMethod('Retime').Invoke($null,$argsRetime)
[IO.File]::WriteAllBytes((Join-Path $directory 'no-control.gif'),$fixture)
if($fixture[4] -ne [byte][char]'9') {throw 'No-control GIF was not upgraded to GIF89a'}
$argsRetime[0]=[byte[]](1,2,3)
$rejected=$false
try {$timing.GetMethod('Retime').Invoke($null,$argsRetime) | Out-Null} catch {$rejected=$true}
if(!$rejected) {throw 'Malformed GIF accepted'}
$parser=New-Object Web.Script.Serialization.JavaScriptSerializer
$stateType=$assembly.GetType('HYROLauncherWeb.LauncherState',$true)
$sample=$parser.Deserialize('{"Games":[],"Plugins":[{"Id":"builtin.pet","Type":"pet","PetFrameDelayMs":300,"Pets":[{"Id":"k","IdleGifPath":"assets/pets/kafka-idle.gif","DragGifPath":"assets/pets/kafka-drag.gif"}]}],"Settings":{}}',$stateType)
$form=$assembly.GetType('HYROLauncherWeb.WebLauncherForm',$true)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$form.GetMethod('PreparePetPlayback',$flags).Invoke($null,@($sample))
if(!$sample.Plugins[0].Pets[0].IdlePlaybackPath.EndsWith('-300.gif')) {throw 'Launcher snapshot missing timed animation'}
$portable=$form.GetMethod('MakePortableState',$flags).Invoke($null,@($sample))
if($portable.Plugins[0].PetFrameDelayMs -ne 300 -or $portable.Plugins[0].Pets[0].IdlePlaybackPath -or !$portable.Plugins[0].Pets[0].IdleGifPath.EndsWith('kafka-idle.gif')) {throw 'Persistence lost delay or replaced original source'}
$sample.Plugins[0].PetIdleFrameDelayMs=0
$sample.Plugins[0].PetDragFrameDelayMs=550
$sample.Plugins[0].PetSize=300
$sample.Plugins[0].PetStaySeconds=10
$form.GetMethod('PreparePetPlayback',$flags).Invoke($null,@($sample))
if(!$sample.Plugins[0].Pets[0].IdlePlaybackPath.EndsWith('kafka-idle.gif') -or !$sample.Plugins[0].Pets[0].DragPlaybackPath.EndsWith('-550.gif')) {throw 'Independent playback delays failed'}
$portable=$form.GetMethod('MakePortableState',$flags).Invoke($null,@($sample))
$restored=$parser.Deserialize($parser.Serialize($portable),$stateType)
if($restored.Plugins[0].PetIdleFrameDelayMs -ne 0 -or $restored.Plugins[0].PetDragFrameDelayMs -ne 550 -or $restored.Plugins[0].PetSize -ne 300 -or $restored.Plugins[0].PetStaySeconds -ne 10) {throw 'Independent settings lost on JSON roundtrip'}
# Exercise real state normalization without constructing a native window.
$instance=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($form)
$instanceFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$form.GetField('serializer',$instanceFlags).SetValue($instance,$parser)
$normalizeState=$form.GetMethod('Normalize',$instanceFlags)
$legacy=$parser.Deserialize('{"Games":[],"Plugins":[{"Id":"builtin.pet","Type":"pet","PetSize":150,"PetStaySeconds":20,"PetFrameDelayMs":2000}],"Settings":{}}',$stateType)
$normalized=$normalizeState.Invoke($instance,@($legacy))
$pet=$normalized.Plugins | Where-Object Id -eq 'builtin.pet'
if($pet.PetIdleFrameDelayMs -ne 2000 -or $pet.PetDragFrameDelayMs -ne 2000 -or $pet.PetSize -ne 150 -or $pet.PetStaySeconds -ne 20) {throw 'Legacy migration changed existing preferences'}
$pet.PetIdleFrameDelayMs=0; $pet.PetDragFrameDelayMs=550; $pet.PetSize=300; $pet.PetStaySeconds=10
$normalized=$normalizeState.Invoke($instance,@($normalized))
if($pet.PetIdleFrameDelayMs -ne 0 -or $pet.PetDragFrameDelayMs -ne 550 -or $pet.PetSize -ne 300 -or $pet.PetStaySeconds -ne 10) {throw 'Normalization lost explicit original timing or new bounds'}
$pet.PetSize=999; $pet.PetStaySeconds=1
$normalized=$normalizeState.Invoke($instance,@($normalized))
if($pet.PetSize -ne 300 -or $pet.PetStaySeconds -ne 10) {throw 'New setting bounds not enforced'}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'report.json') -Encoding UTF8
Write-Output 'PASS: GIF timing/pixel preservation, independent playback and persistence, legacy migration, explicit original timing, 300px/10s normalization bounds.'
