
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions,System.Drawing,System.Windows.Forms
$assembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$serializer=New-Object Web.Script.Serialization.JavaScriptSerializer
$pluginType=$assembly.GetType('HYROLauncherWeb.PluginEntry',$true)
$plugin=$serializer.Deserialize('{"WatchItems":[{"Id":"a","Title":"Phim ti\u1ebfng Vi\u1ec7t \ud83c\udfac & \u65e5\u672c\u8a9e","Episode":"3","TotalEpisodes":12,"Status":"watching","Url":"https://example.com/film?a=1&b=2","PosterPath":"assets/poster.jpg"},{"Id":"b","Title":"=NOT_A_FORMULA()","Episode":"","Status":"planned"}]}',$pluginType)
$codec=$assembly.GetType('HYROLauncherWeb.MovieWatchlistWorkbook',$true)
$target=Join-Path $ProjectRoot 'output/verification/watchlist-roundtrip.xlsx'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
$argsExport=New-Object object[] 2; $argsExport[0]=[string]$target; $argsExport[1]=$plugin.WatchItems
$codec.GetMethod('Export').Invoke($null,$argsExport)
$items=$codec.GetMethod('Import').Invoke($null,@([string]$target))
if($items.Count -ne 2 -or $items[0].Title -ne $plugin.WatchItems[0].Title -or $items[0].Url -ne $plugin.WatchItems[0].Url -or $items[0].TotalEpisodes -ne 12 -or $items[1].Title -ne '=NOT_A_FORMULA()') {throw 'Excel roundtrip lost metadata'}
$form=$assembly.GetType('HYROLauncherWeb.WebLauncherForm',$true)
$next=$form.GetMethod('NextMovieEpisode',[Reflection.BindingFlags]'Static,NonPublic')
foreach($sample in @(@('29',30),@('Episode 3',4),@('',1))) {if($next.Invoke($null,@([string]$sample[0])) -ne $sample[1]) {throw 'Episode increment failed'}}
$browserType=$assembly.GetType('HYROLauncherWeb.HyrowserWindow',$true)
$browser=[Activator]::CreateInstance($browserType,@('HyBrowser verification'))
try {
 $flags=[Reflection.BindingFlags]'Instance,NonPublic'
 $sidebar=$browserType.GetField('tabsHost',$flags).GetValue($browser)
 if($sidebar.Dock -ne [Windows.Forms.DockStyle]::Left -or $sidebar.Width -ne 184) {throw 'Compact vertical tab sidebar missing'}
 if(!$browserType.GetProperty('AdvanceMovie') -or !$browserType.GetMethod('OpenMovie') -or !$browserType.GetMethod('UpdateMovieContext')) {throw 'Movie browser lifecycle missing'}
 $tabType=$browserType.GetNestedType('BrowserTab',[Reflection.BindingFlags]'NonPublic')
 if(!$tabType.GetField('Movie') -or !$tabType.GetField('Poster')) {throw 'Movie tab metadata missing'}
 $movieTab=[Activator]::CreateInstance($tabType)
 $movieTab.Id='movie-tab'; $movieTab.Title='Movie tab'; $movieTab.Movie=$plugin.WatchItems[0]
 $normalTab=[Activator]::CreateInstance($tabType)
 $normalTab.Id='normal-tab'; $normalTab.Title='Search the web'
 $tabs=$browserType.GetField('tabs',$flags).GetValue($browser)
 $tabs.Add($movieTab); $tabs.Add($normalTab)
 $browserType.GetField('activeTab',$flags).SetValue($browser,$movieTab)
 $updated=$serializer.Deserialize('{"WatchItems":[{"Id":"a","Title":"Movie preview","Episode":"4","PosterPath":"assets/poster.jpg"}]}',$pluginType).WatchItems[0]
 $browserType.GetMethod('UpdateMovieContext').Invoke($browser,@($updated))
 if($tabs.Count -ne 2 -or $movieTab.Movie.Episode -ne '4' -or $normalTab.Movie -ne $null) {throw 'Movie progress created a duplicate tab or changed a normal tab'}
 $watchButton=$browserType.GetField('watchButton',$flags).GetValue($browser)
 if($watchButton.Text -ne 'Episode +1') {throw 'Movie save action is not labeled as episode +1'}
 if($movieTab.Button.Height -ne 68 -or $normalTab.Button.Height -ne 36) {throw 'Expanded tabs are not compact'}
 $toggle=$browserType.GetMethod('ToggleTabsSidebar',$flags)
 $toggle.Invoke($browser,@())
 if($sidebar.Width -ne 60 -or $movieTab.Button.Height -ne 44 -or $normalTab.Button.Height -ne 36) {throw 'Collapsed tab rail dimensions failed'}
 foreach($tab in @($movieTab,$normalTab)) {
  if(!$tab.Button.Compact -or $tab.Button.Controls[0].Visible) {throw 'Collapsed tab content overlaps the rail'}
 }
 if($tabs.Count -ne 2 -or $movieTab.Movie.Episode -ne '4') {throw 'Collapsing tabs changed movie context'}
 $toggle.Invoke($browser,@())
 if($sidebar.Width -ne 184 -or $movieTab.Button.Compact -or $movieTab.Button.Height -ne 68) {throw 'Expanding tabs did not restore compact sidebar'}
} finally {$browser.Dispose()}
$gif=[Drawing.Image]::FromFile((Join-Path $ProjectRoot 'assets/pets/hyro-buddy-drag.gif'))
try {if($gif.GetFrameCount([Drawing.Imaging.FrameDimension]::Time) -ne 8) {throw 'Drag animation missing'}} finally {$gif.Dispose()}
Write-Output 'PASS: XLSX metadata/Unicode/blank-episode roundtrip, episode +1, vertical browser sidebar/movie context APIs, eight-frame drag GIF.'
