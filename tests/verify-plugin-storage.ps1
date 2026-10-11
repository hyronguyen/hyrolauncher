
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions
$assembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$form=$assembly.GetType('HYROLauncherWeb.WebLauncherForm',$true)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$serializer=New-Object Web.Script.Serialization.JavaScriptSerializer
$inputData=$serializer.DeserializeObject('{"WatchItems":[{"Id":"new-film","Title":"Movie without episode","Episode":"","Status":"planned","TotalEpisodes":12},{"Id":"watching-film","Title":"Series","Episode":"3","TotalEpisodes":10},{"Id":"finished-film","Title":"Old movie","Episode":"Done"}]}')
$arguments=New-Object object[] 3
$arguments[0]=$inputData; $arguments[1]='builtin.movie-watchlist'; $arguments[2]=$null
$items=$form.GetMethod('ReadMovieWatchItems',$flags).Invoke($null,$arguments)
if($items.Count -ne 3) {throw 'A movie without an episode was dropped'}
if($items[0].Status -ne 'planned' -or $items[0].TotalEpisodes -ne 12) {throw 'Movie status or episode total was lost'}
if($items[1].Status -ne 'watching' -or $items[2].Status -ne 'completed') {throw 'Legacy status migration failed'}
$pluginType=$assembly.GetType('HYROLauncherWeb.PluginEntry',$true)
$plugin=[Activator]::CreateInstance($pluginType)
$plugin.PetStaySeconds=120
$plugin.WatchItems=$items
$roundtrip=$serializer.Deserialize($serializer.Serialize($plugin),$pluginType)
if($roundtrip.PetStaySeconds -ne 120 -or $roundtrip.WatchItems[0].TotalEpisodes -ne 12 -or $roundtrip.WatchItems[2].Status -ne 'completed') {throw 'Plugin settings failed JSON roundtrip'}
Write-Output 'PASS: native retains movies without episode, status migration, episode totals, pet dwell and plugin JSON roundtrip.'
