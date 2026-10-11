param([switch]$Live)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Web.Extensions
$assembly=[Reflection.Assembly]::LoadFile((Get-TestExecutable))
$type=$assembly.GetType('HYROLauncherWeb.IgdbService',$true)
$games=$type.GetMethod('ParseGames').Invoke($null,@('[{"id":1,"name":"Game","summary":"Summary","first_release_date":0,"rating":80.123,"genres":[{"name":"RPG"}],"involved_companies":[{"company":{"name":"Studio"},"developer":true,"publisher":true}],"screenshots":[{"image_id":"sc123"},{"image_id":"../bad"}],"cover":{"image_id":"co123"}}]'))
if($games.Count-ne 1 -or $games[0].ReleaseDate-ne '1970-01-01' -or $games[0].Rating-ne 80.1 -or $games[0].Screenshots.Count-ne 1 -or $games[0].Developers[0]-ne 'Studio') { throw 'IGDB parsing failed' }
$ctorArgs=New-Object object[] 1
$ctorArgs[0]=[string][IO.Path]::Combine($ProjectRoot,'dist')
$service=$type.GetConstructor(@([string])).Invoke($ctorArgs)
Write-Output 'PASS: IGDB metadata, release date, rating, companies, screenshot URL filtering.'
if($Live) {
 try {
  $searchArgs=New-Object object[] 1; $searchArgs[0]=[string]'Genshin Impact'
  $matches=$type.GetMethod('Search').Invoke($service,$searchArgs)
  $exact=@($matches | Where-Object { $_.Name -eq 'Genshin Impact' })
  if($exact.Count-ne 1) { Write-Output ('Search returned '+$matches.Count+' candidates, exact matches='+$exact.Count); $matches | Select-Object Id,Name | ConvertTo-Json; throw 'ExactMatchRequired' }
  $detailArgs=New-Object object[] 2; $detailArgs[0]=[int]$exact[0].Id; $detailArgs[1]=[bool]$true
  $detail=$type.GetMethod('Get').Invoke($service,$detailArgs)
  $cached=$type.GetMethod('Cached').Invoke($service,@([int]$detail.Id))
  if(!$cached -or $cached.Id-ne $detail.Id) { throw 'CacheFailed' }
  Write-Output ('PASS: live IGDB found Genshin Impact #'+$detail.Id+', '+$detail.Screenshots.Count+' screenshots, metadata cached.')
 } catch {
  $cause=$_.Exception.GetBaseException()
  $code=$cause.GetType().Name
  if($cause -is [Net.WebException]) { $code+=' '+$cause.Status; if($cause.Response) {$code+=' HTTP '+[int]$cause.Response.StatusCode} }
  if($cause -is [InvalidOperationException] -and $cause.Message -in @('MissingCredentials','InvalidCredentials','AuthenticationFailed','RateLimited','NotFound')) {$code+=' '+$cause.Message}
  if($cause.Message -in @('ExactMatchRequired','CacheFailed')) {$code+=' '+$cause.Message}
  throw ('IGDB verification failed ('+$code+'). Credentials and request details withheld.')
 }
}
