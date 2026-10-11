
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
$assembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$browserType=$assembly.GetType('HYROLauncherWeb.HyrowserWindow',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$window=[Activator]::CreateInstance($browserType,@('HyBrowser'))
function Layout-All($control) {
 $control.PerformLayout()
 foreach($child in $control.Controls) {Layout-All $child}
}
try {
 $tabType=$browserType.GetNestedType('BrowserTab',[Reflection.BindingFlags]'NonPublic')
 $tabs=$browserType.GetField('tabs',$flags).GetValue($window)
 for($i=0;$i -lt 30;$i++) {
  $tab=[Activator]::CreateInstance($tabType)
  $tab.Id='tab-'+$i; $tab.Title='Example long page title '+$i
  $tabs.Add($tab)
 }
 $viewport=$browserType.GetField('tabStrip',$flags).GetValue($window)
 $sidebar=$browserType.GetField('tabsHost',$flags).GetValue($window)
 $toggle=$browserType.GetMethod('ToggleTabsSidebar',$flags)
 foreach($width in @(820,1180)) {
  $window.ClientSize=New-Object Drawing.Size($width,600)
  Layout-All $window
  $browserType.GetMethod('RenderTabs',$flags).Invoke($window,@())
  foreach($mode in @(0,1,2)) {
   if($mode -gt 0) {$toggle.Invoke($window,@()); Layout-All $window}
   if($viewport.AutoScroll -or $viewport.HorizontalScroll.Visible -or $viewport.VerticalScroll.Visible) {throw 'Native scrollbar reappeared'}
   foreach($tab in $tabs) {
    if($tab.Button.Right -gt $viewport.ClientSize.Width -or $tab.Button.Left -lt 0) {throw 'Tab overflows horizontally'}
   }
   $viewport.ScrollWheel(-120)
   if($viewport.ScrollOffset -le 0) {throw 'Wheel cannot reach overflowing tabs'}
   $viewport.Reveal(($tabs.Count-1)*41,36)
   if($tabs[$tabs.Count-1].Button.Bottom -gt $viewport.ClientSize.Height) {throw 'Last tab cannot be revealed'}
   $viewport.Reveal(0,36)
   if($viewport.ScrollOffset -ne 0 -or $tabs[0].Button.Top -ne 0) {throw 'Scroll did not return to first tab'}
   $actions=$browserType.GetField('tabActions',$flags).GetValue($window)
   foreach($name in @('newTabButton','privateTabButton')) {
    $action=$browserType.GetField($name,$flags).GetValue($window)
    if($action.Right -gt $actions.ClientSize.Width -or $action.Bottom -gt $actions.ClientSize.Height) {throw 'Sidebar action is clipped'}
   }
   $toggleButton=$browserType.GetField('toggleTabsButton',$flags).GetValue($window)
   if($toggleButton.Right -gt $toggleButton.Parent.ClientSize.Width) {throw 'Sidebar toggle overflows'}
  }
 }
 $viewport.Configure(20)
 if($viewport.ScrollOffset -ne 0) {throw 'Removing overflow did not reset scroll'}
 $script=$browserType.GetField('BrowserScrollbarScript',[Reflection.BindingFlags]'Static,NonPublic').GetRawConstantValue()
 if(!$script.Contains('scrollbar-width:thin') -or $script.Contains('overflow:hidden')) {throw 'Page scrollbar styling disabled scrolling'}
 # Exercise the real custom tab painter directly; DrawToBitmap omits hidden children.
 $movieType=$assembly.GetType('HYROLauncherWeb.MovieWatchItem',$true)
 $movie=[Activator]::CreateInstance($movieType)
 $movie.Title='Example movie title'; $movie.Episode='29'
 $tabs[0].Movie=$movie
 $browserType.GetField('activeTab',$flags).SetValue($window,$tabs[0])
 $browserType.GetMethod('RenderTabs',$flags).Invoke($window,@())
 $bitmap=New-Object Drawing.Bitmap(184,172)
 $graphics=[Drawing.Graphics]::FromImage($bitmap)
 try {
  $graphics.Clear([Drawing.Color]::FromArgb(16,23,37))
  $top=8
  for($i=0;$i -lt 3;$i++) {
   $button=$tabs[$i].Button
   $tile=New-Object Drawing.Bitmap($button.Width,$button.Height)
   $tileGraphics=[Drawing.Graphics]::FromImage($tile)
   try {
    if($button.Region) {$tileGraphics.SetClip($button.Region,[Drawing.Drawing2D.CombineMode]::Replace)}
    $paint=New-Object Windows.Forms.PaintEventArgs($tileGraphics,$button.ClientRectangle)
    $paintArgs=New-Object object[] 1
    $paintArgs[0]=$paint.PSObject.BaseObject
    $button.GetType().GetMethod('OnPaint',$flags).Invoke($button,$paintArgs)
    $graphics.DrawImageUnscaled($tile,8,$top)
   } finally {$tileGraphics.Dispose();$tile.Dispose()}
   $top+=$button.Height+5
  }
  $destination=Join-Path $ProjectRoot 'output/verification/hybrowser-tab-paint.png'
  $bitmap.Save($destination,[Drawing.Imaging.ImageFormat]::Png)
 } finally {$graphics.Dispose();$bitmap.Dispose()}
} finally {$window.Dispose()}
Write-Output 'PASS: 30 tabs at 820/1180 widths; collapse/expand, wheel/reveal, no native scrollbars, no clipped actions, scrolling preserved.'
