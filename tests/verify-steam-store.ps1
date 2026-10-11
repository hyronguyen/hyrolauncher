param([switch]$Live)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions
$assembly=[Reflection.Assembly]::LoadFile((Get-TestExecutable))
$store=$assembly.GetType('HYROLauncherWeb.SteamStoreService',$true)
$filterType=$assembly.GetType('HYROLauncherWeb.SteamDealFilters',$true)
$parser=New-Object System.Web.Script.Serialization.JavaScriptSerializer
function Invoke-StoreMethod($name,[object[]]$arguments,$target=$null){$store.GetMethod($name).Invoke($target,$arguments)}
$wishlist=Invoke-StoreMethod 'ParseWishlist' @([string]'{"response":{"items":[{"appid":10},{"appid":10},{"appid":20},{"appid":0}]}}')
if($wishlist.Count-ne 2){throw 'Wishlist dedup failed'}
try {Invoke-StoreMethod 'ParseWishlist' @([string]'{"response":{}}');throw 'Missing private/empty wishlist handling'}catch {if($_.Exception.GetBaseException().Message-ne 'WishlistUnavailable'){throw}}
$items=Invoke-StoreMethod 'ParseStoreItems' @([string]'{"response":{"store_items":[{"appid":10,"success":1,"name":"Discount","basic_info":{"short_description":"Description"},"best_purchase_option":{"discount_pct":10,"formatted_final_price":"90.000 VND","formatted_original_price":"100.000 VND"}},{"appid":20,"success":1,"name":"Normal","best_purchase_option":{"formatted_final_price":"200.000 VND"}},{"appid":10,"success":1}]}}')
if($items.Count-ne 2 -or $items[0].Discount-ne 10 -or $items[1].Price-ne '200.000 VND'){throw 'Wishlist discounts/regular prices failed'}
$html='<div class="tab_filter_control_row" data-param="tags" data-value="19" data-loc="Action &amp; Combat"></div><div class="tab_filter_control_row" data-param="supportedlang" data-value="english" data-loc="English"></div>'
$options=Invoke-StoreMethod 'ParseFilters' @([string]$html)
if($options.Count-ne 5 -or $options[0].Name-ne 'Action & Combat'){throw 'Filter metadata parsing failed'}
$filters=$parser.Deserialize('{"Term":"Cyberpunk & test","Sort":"invalid","Tags":"19,21,19,bad","MinDiscount":200,"MaxPrice":-1,"Os":"win","Types":"998"}',$filterType)
$queryArgs=New-Object object[] 2;$queryArgs[0]=$filters;$queryArgs[1]=0
$query=Invoke-StoreMethod 'DealQuery' $queryArgs
if(!$query.Contains('term=Cyberpunk%20%26%20test') -or !$query.Contains('tags=19%2C21') -or $query.Contains('sort_by=invalid') -or $query.Contains('maxprice=-1')){throw 'Filter whitelist/encoding failed'}
Write-Output 'PASS: wishlist IDs, duplicate removal, unavailable response, discounted and regular prices, Steam filter metadata, filter validation and encoding.'
if($Live){
 try {
  $ctor=New-Object object[] 1;$ctor[0]=[string][IO.Path]::Combine($ProjectRoot,'dist');$service=$store.GetConstructor(@([string])).Invoke($ctor)
  $options=Invoke-StoreMethod 'Filters' @([bool]$true) $service
  if($options.Count-lt 20){throw 'SteamFiltersUnavailable'}
  Write-Output ('PASS: live Steam filters: '+$options.Count+' options in '+(@($options.Group|Select-Object -Unique).Count)+' groups.')
  $liveFilters=$parser.Deserialize('{"Term":"Cyberpunk","Sort":"Price_ASC","Types":"998","Os":"win","MinDiscount":20,"MaxPrice":350000}',$filterType)
  $arguments=New-Object object[] 3;$arguments[0]=$liveFilters;$arguments[1]=0;$arguments[2]=$true
  $deals=Invoke-StoreMethod 'Deals' $arguments $service
  if($deals.Games.Count-eq 0){throw 'NoFilteredDeals'}
  foreach($game in $deals.Games){if($null-eq $game.PriceVnd){throw 'MissingNumericPrice'};if($game.PriceVnd-gt 350000 -or $game.Discount-lt 20){throw 'PriceDiscountFilterNotApplied'}}
  Write-Output ('PASS: live filtered Steam search: '+$deals.Games.Count+' games.')
  $data=$parser.DeserializeObject([IO.File]::ReadAllText((Join-Path $ProjectRoot 'dist\launcher-data.json')))
  $account=[string]$data.Settings.SteamId
  if($account){
   $request=New-Object object[] 3;$request[0]=[string]$account;$request[1]=0;$request[2]=$true
   $first=Invoke-StoreMethod 'Wishlist' $request $service
   $seen=New-Object 'System.Collections.Generic.HashSet[int]';$discounted=0
   foreach($game in $first.Games){[void]$seen.Add($game.Id);if($game.Discount-gt 0){$discounted++}}
   if($first.HasMore){$request[1]=$first.NextOffset;$next=Invoke-StoreMethod 'Wishlist' $request $service;foreach($game in $next.Games){[void]$seen.Add($game.Id);if($game.Discount-gt 0){$discounted++}}}
   Write-Output ('PASS: configured wishlist: '+$seen.Count+' unique games, '+$discounted+' currently discounted.')
  }
 }catch {throw ('Live Steam Store verification failed: '+$_.Exception.GetBaseException().GetType().Name+'. Request URLs and credentials withheld.')}
}
