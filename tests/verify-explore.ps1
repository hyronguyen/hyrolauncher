param([switch]$Live)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFile((Get-TestExecutable))
$igdbType=$assembly.GetType('HYROLauncherWeb.IgdbService',$true)
$exploreType=$assembly.GetType('HYROLauncherWeb.ExploreService',$true)
$parseArgs=New-Object object[] 1
$parseArgs[0]='{"specials":{"items":[{"id":1,"name":"Deal","discount_percent":50,"original_price":20000000,"final_price":10000000,"currency":"VND","header_image":"https://image"},{"id":2,"discount_percent":10,"original_price":10000,"final_price":9000,"currency":"USD"}]}}'
$deals=$exploreType.GetMethod('ParseDeals').Invoke($null,$parseArgs)
if($deals.Count-ne 1 -or $deals[0].Discount-ne 50 -or $deals[0].Price-ne ('100.000 '+[char]0x20ab)) {throw 'Deal price/filter parsing failed'}
Write-Output 'PASS: Steam deal filtering, discount and VND price parsing.'
$html='<a href="https://store.steampowered.com/app/10/Game/" data-ds-appid="10"><span class="title">Game &amp; More</span><div data-discount="50"><div class="discount_original_price">200.000&#8363;</div><div class="discount_final_price">100.000&#8363;</div></div></a>'
$searchArgs=New-Object object[] 2
$searchArgs[0]=[string](@{results_html=$html+$html;total_count=200}|ConvertTo-Json -Compress);$searchArgs[1]=0
$page=$exploreType.GetMethod('ParseSearchDeals').Invoke($null,$searchArgs)
if($page.Games.Count-ne 1 -or !$page.HasMore -or $page.NextOffset-ne 50 -or $page.Games[0].Name-ne 'Game & More') {throw 'Search pagination/dedup parsing failed'}
Write-Output 'PASS: storefront pagination, deduplication and HTML entity decoding.'
if($Live) {
 $rootArgs=New-Object object[] 1;$rootArgs[0]=[string][IO.Path]::Combine($ProjectRoot,'dist')
 $igdb=$igdbType.GetConstructor(@([string])).Invoke($rootArgs)
 $ctorArgs=New-Object object[] 2;$ctorArgs[0]=$rootArgs[0];$ctorArgs[1]=$igdb
 $service=$exploreType.GetConstructor(@([string],$igdbType)).Invoke($ctorArgs)
 foreach($mode in @('new','upcoming','deals','search')) {
  try {
   $requestArgs=New-Object object[] 3;$requestArgs[0]=[string]$mode;$requestArgs[1]=[string]'Genshin Impact';$requestArgs[2]=[bool]$true
   $result=$exploreType.GetMethod('Get').Invoke($service,$requestArgs)
   if(!$result -or $result.Games.Count-eq 0) {throw 'EmptyResults'}
   Write-Output ('PASS: live '+$mode+' returned '+$result.Games.Count+' games.')
   if($mode-eq 'deals') {
    if(!$result.HasMore -or $result.NextOffset-ne 50) {throw 'PaginationMissing'}
    $nextArgs=New-Object object[] 4;$nextArgs[0]='deals';$nextArgs[1]='';$nextArgs[2]=$true;$nextArgs[3]=50
    $next=$exploreType.GetMethod('GetPage').Invoke($service,$nextArgs)
    if($next.Games.Count-eq 0 -or $next.NextOffset-ne 100) {throw 'SecondPageMissing'}
    Write-Output ('PASS: next deal page returned '+$next.Games.Count+' games.')
   }
  } catch {
   $cause=$_.Exception.GetBaseException();$code=$cause.GetType().Name
   if($cause -is [Net.WebException] -and $cause.Response) {$code+=' HTTP '+[int]$cause.Response.StatusCode}
   throw ('Explore '+$mode+' verification failed ('+$code+'). Request/credentials withheld.')
  }
 }
}
