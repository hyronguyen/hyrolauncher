
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions,System.Drawing
[AppDomain]::CurrentDomain.SetData('APPBASE',(Join-Path $ProjectRoot 'dist'))
$assembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$formType=$assembly.GetType('HYROLauncherWeb.WebLauncherForm',$true)
$pluginType=$assembly.GetType('HYROLauncherWeb.PluginEntry',$true)
$plugin=[Activator]::CreateInstance($pluginType)
$plugin.Id='builtin.pet'; $plugin.ActivePetId='bundled.kled-skaarl'; $plugin.PetEnabled=$false
$ensure=$formType.GetMethod('EnsureDefaultPet',[Reflection.BindingFlags]'Static,NonPublic')
$ensure.Invoke($null,@($plugin))
$ensure.Invoke($null,@($plugin))
if($plugin.Pets.Count -ne 3) {throw 'Bundled pets missing or duplicated'}
if($plugin.ActivePetId -ne 'bundled.kled-skaarl' -or $plugin.PetEnabled) {throw 'Bundled pets changed user preferences'}
foreach($id in @('bundled.kafka','bundled.kled-skaarl')) {
 $pet=$plugin.Pets | Where-Object Id -eq $id
 if(!$pet -or $pet.WalkGifPath) {throw 'New pet should have only idle and drag'}
 foreach($path in @($pet.IdleGifPath,$pet.DragGifPath)) {
  if(!(Test-Path -LiteralPath $path)) {throw 'Bundled animation path missing'}
  $image=[Drawing.Image]::FromFile($path)
  try {
   if($image.Width -ne 220 -or $image.Height -ne 280 -or $image.GetFrameCount([Drawing.Imaging.FrameDimension]::Time) -ne 8) {throw 'Invalid animation geometry or frame count'}
  } finally {$image.Dispose()}
 }
}
$kafka=$plugin.Pets | Where-Object Id -eq 'bundled.kafka'
$kafka.Name='My Kafka'; $kafka.IdleGifPath='assets/pets/my-idle.gif'
$ensure.Invoke($null,@($plugin))
if($kafka.Name -ne 'My Kafka' -or $kafka.IdleGifPath -ne 'assets/pets/my-idle.gif') {throw 'Seed overwrote pet edits'}
$serializer=New-Object Web.Script.Serialization.JavaScriptSerializer
$restored=$serializer.Deserialize($serializer.Serialize($plugin),$pluginType)
if($restored.Pets.Count -ne 3 -or $restored.ActivePetId -ne 'bundled.kled-skaarl') {throw 'Pet library failed persistence roundtrip'}
Write-Output 'PASS: two new companions seeded once, eight-frame idle/drag GIFs, no walk, selection and edits preserved, JSON persistence.'
