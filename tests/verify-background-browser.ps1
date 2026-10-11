param([switch]$Live)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,System.Web.Extensions
$assembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$backgrounds=$assembly.GetType('HYROLauncherWeb.SteamBackgroundService',$true)
$media=$backgrounds.GetMethod('MediaUrl')
foreach($bad in @('http://cdn.akamai.steamstatic.com/image.jpg','https://steamstatic.com.evil.test/image.jpg','file:///C:/secret','javascript:bad')) {
 if($media.Invoke($null,@($bad)) -ne '') {throw 'Untrusted background URL accepted'}
}
$equipped=$backgrounds.GetMethod('ParseEquipped').Invoke($null,@('{"response":{"profile_background":{"name":"My background","image_large":"items/10/image.jpg","movie_webm":"items/10/motion.webm"}}}'))
if(!$equipped.Image.EndsWith('/items/10/image.jpg') -or !$equipped.Video.EndsWith('/items/10/motion.webm')) {throw 'Profile background parsing failed'}
$arguments=New-Object object[] 2
$arguments[0]='{"success":1,"more_items":1,"last_assetid":"100","descriptions":[{"name":"Background","tags":[{"internal_name":"item_class_3"}],"actions":[{"link":"https://community.fastly.steamstatic.com/image.jpg"}]},{"name":"Not a background","actions":[{"link":"https://community.fastly.steamstatic.com/other.jpg"}]}]}'
$arguments[1]=''
$items=$backgrounds.GetMethod('ParseInventory').Invoke($null,$arguments)
if($items.Count -ne 1 -or $arguments[1] -ne '100') {throw 'Inventory filtering or pagination failed'}
$settingsType=$assembly.GetType('HYROLauncherWeb.LauncherSettings',$true)
$settings=[Activator]::CreateInstance($settingsType)
$settings.LibraryBackground='assets/settings/library-background.gif';$settings.LibraryBackgroundDim=65;$settings.CardStyle='icon'
$parser=New-Object Web.Script.Serialization.JavaScriptSerializer
$restored=$parser.Deserialize($parser.Serialize($settings),$settingsType)
if($restored.LibraryBackgroundDim -ne 65 -or $restored.LibraryBackground -ne $settings.LibraryBackground) {throw 'Background settings were lost'}
$browserType=$assembly.GetType('HYROLauncherWeb.HyrowserWindow',$true)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$shortcuts=$browserType.GetMethod('IsBrowserShortcut',$flags)
foreach($key in @(([Windows.Forms.Keys]::Control -bor [Windows.Forms.Keys]::L),([Windows.Forms.Keys]::Control -bor [Windows.Forms.Keys]::T),([Windows.Forms.Keys]::Control -bor [Windows.Forms.Keys]::W),([Windows.Forms.Keys]::Control -bor [Windows.Forms.Keys]::Tab),([Windows.Forms.Keys]::Control -bor [Windows.Forms.Keys]::Shift -bor [Windows.Forms.Keys]::Tab),[Windows.Forms.Keys]::F5)) {
 if(!$shortcuts.Invoke($null,@([Windows.Forms.Keys]$key))) {throw 'Browser shortcut missing'}
}
if($shortcuts.Invoke($null,@([Windows.Forms.Keys]::A))) {throw 'Ordinary typing intercepted'}
$normalizer=$browserType.GetMethod('NormalizeBrowserInput',$flags)
if($normalizer.Invoke($null,@('example.com')) -ne 'https://example.com') {throw 'Address normalization failed'}
if(!$normalizer.Invoke($null,@('find a movie')).StartsWith('https://www.google.com/search?q=')) {throw 'Address search failed'}
$window=[Activator]::CreateInstance($browserType,@('Test browser'))
try {
 $instanceFlags=[Reflection.BindingFlags]'Instance,NonPublic'
 $address=$browserType.GetField('addressBox',$instanceFlags).GetValue($window)
 if($address.AccessibleName -ne 'Address or search') {throw 'Address control missing'}
 if(!$browserType.GetProperty('SaveToWatchlist')) {throw 'Browser watchlist action missing'}
} finally {$window.Dispose()}
foreach($name in @('hyro-buddy-idle.gif','hyro-buddy-walk.gif')) {
 $image=[Drawing.Image]::FromFile((Join-Path $ProjectRoot ('assets/pets/'+$name)))
 try {if($image.GetFrameCount([Drawing.Imaging.FrameDimension]::Time) -ne 8) {throw 'Pet animation frames missing'}} finally {$image.Dispose()}
}
Write-Output 'PASS: Steam media validation, profile/inventory parsing, pagination, settings persistence, browser shortcuts/address controls, two eight-frame GIFs.'
if($Live) {
 $account=(Get-Content -LiteralPath (Join-Path $ProjectRoot 'dist/launcher-data.json') -Raw | ConvertFrom-Json).Settings.SteamId
 try {
  $fetchArgs=New-Object object[] 3
  $fetchArgs[0]=[string]$account;$fetchArgs[1]=[string](Join-Path $ProjectRoot 'dist');$fetchArgs[2]=[bool]$true
  $catalog=$backgrounds.GetMethod('Get').Invoke($null,$fetchArgs)
  Write-Output ('Live Steam backgrounds: '+$catalog.Items.Count+'; warning: '+$catalog.Warning)
  if($catalog.Items.Count -gt 0) {
   foreach($uri in @($catalog.Items[0].Image,$catalog.Items[0].Video)) {
    if(!$uri) {continue}
    try {$response=Invoke-WebRequest -UseBasicParsing -Uri $uri -Method Head -TimeoutSec 20; Write-Output ('Steam media status: '+$response.StatusCode)} catch {Write-Output 'Steam media request failed'}
   }
  }
 } catch {Write-Output 'Live background request unavailable'}
}
