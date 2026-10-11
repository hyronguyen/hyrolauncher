param([string]$BeforeClone='',[switch]$AllowActivity)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$taskDist=Join-Path $ProjectRoot 'dist'
$taskExe=Join-Path $taskDist 'HYROLauncher.exe'
$taskRunning=@(Get-Process HYROLauncher -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $taskExe})
if(!$taskRunning.Count -or !@($taskRunning | Where-Object {$_.Responding}).Count){throw 'Updated launcher is not responding'}
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskExe)
$taskType=$taskAssembly.GetType('HYROLauncherWeb.HyTcgEconomy',$true)
$taskImageDecoder=$taskAssembly.GetType('HYROLauncherWeb.HyTcgStorage',$true).GetMethod('ImageBytes',[Reflection.BindingFlags]'Static,NonPublic')
function Get-PlainCardHash([string]$CardPath) {
 $taskImageArgs=New-Object object[] 1
 $taskImageArgs[0]=[IO.File]::ReadAllBytes($CardPath)
 $taskImageBytes=$taskImageDecoder.Invoke($null,$taskImageArgs)
 $taskImageHasher=[Security.Cryptography.SHA256]::Create()
 try {return [BitConverter]::ToString($taskImageHasher.ComputeHash($taskImageBytes)).Replace('-','')}finally{$taskImageHasher.Dispose()}
}
$taskCtor=$taskType.GetConstructors($taskFlags) | Where-Object {$_.GetParameters().Count -eq 1}
$taskEconomy=$taskCtor.Invoke(@([string]$taskDist))
$taskState=$taskType.GetMethod('Load',$taskFlags).Invoke($taskEconomy,@())
$taskCards=@($taskType.GetMethod('CollectionCards',$taskFlags).Invoke($taskEconomy,@()))
if(@($taskCards | Where-Object {!$_.FileName.EndsWith('.hycard')}).Count){throw 'Owned production card has not been migrated to opaque storage'}
foreach($taskAsset in @('index.html','steam-library.js','bookshelf.js','bookshelf.css','hytcg.html','hytcg.js','hytcg.css','hytcg-renderer.js','hytcg-lifestyle.js','hytcg-lifestyle.css','hytcg-adventure.js','hytcg-adventure.css','hytcg-dev.js','hytcg-dev.css','hytcg-frame-repair.js','hytcg-rarity.js','hytcg-effects.js','hytcg-effects.css','rarities/common.svg','rarities/uncommon.svg','rarities/rare.svg','rarities/sr.svg','rarities/ssr.svg','rarities/ur.svg','scenes/meadow.svg','scenes/observatory.svg','scenes/ruins.svg','scenes/arena.svg','scenes/bond.svg')) {
 if((Get-FileHash -LiteralPath (Join-Path (Join-Path $ProjectRoot 'ui') $taskAsset)).Hash -ne (Get-FileHash -LiteralPath (Join-Path (Join-Path $taskDist 'ui') $taskAsset)).Hash){throw 'Deployed UI asset does not match verified source'}
}
if((Get-FileHash -LiteralPath (Join-Path $ProjectRoot 'assets/icons/hytcg.svg')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskDist 'assets/icons/hytcg.svg')).Hash){throw 'Deployed HYTCG icon does not match verified source'}
if($BeforeClone){
 $taskBeforeRoot=[IO.Path]::GetFullPath($BeforeClone)
 $taskBefore=$taskType.GetConstructors($taskFlags) | Where-Object {$_.GetParameters().Count -eq 1} | ForEach-Object {$_.Invoke(@([string]$taskBeforeRoot))}
 $taskBeforeState=$taskType.GetMethod('Load',$taskFlags).Invoke($taskBefore,@())
 $taskBeforeCards=@($taskType.GetMethod('CollectionCards',$taskFlags).Invoke($taskBefore,@()))
 if(!$AllowActivity) {
  if($taskState.Gems -ne $taskBeforeState.Gems -or $taskState.PaidCredits -ne $taskBeforeState.PaidCredits){throw 'Wallet differs from the pre-deployment verification clone'}
  if($taskState.LimitedCredits -ne $taskBeforeState.LimitedCredits){throw 'Limited credits differ from the pre-deployment verification clone'}
  foreach($taskCounter in @('PitySR','PitySSR','PityUR')){if($taskState.$taskCounter -ne $taskBeforeState.$taskCounter){throw 'Deployment changed a pity counter'}}
  if($taskCards.Count -ne $taskBeforeCards.Count){throw 'Deployment changed card count'}
 }
 $taskCurrentById=@{}
 foreach($taskCard in $taskCards){$taskCurrentById[$taskCard.Card.CardId]=$taskCard}
 foreach($taskCard in $taskBeforeCards){
  $taskCurrent=$taskCurrentById[$taskCard.Card.CardId]
  if(!$taskCurrent) {
   $taskTombstone=@($taskState.Cards | Where-Object {$_.CardId -eq $taskCard.Card.CardId -and $_.Dissolved})
   if($AllowActivity -and $taskTombstone.Count -eq 1){continue}
   throw 'Deployment lost an existing card without a native tombstone'
  }
  if($AllowActivity -and $taskCurrent.Card.Revision -gt $taskCard.Card.Revision){
   $taskOriginalFingerprint=$taskType.GetMethod('Fingerprint',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@($taskCard.Card))
   if(@($taskState.Upgrades | Where-Object {$_.TargetId -eq $taskCard.Card.CardId -and $_.Stage -eq 'complete' -and $_.OriginalFingerprint -eq $taskOriginalFingerprint}).Count){continue}
  }
  if($taskCurrent.FileName -ne $taskCard.FileName){throw 'Deployment changed existing card ownership'}
  if((Get-PlainCardHash $taskCurrent.ImagePath) -ne (Get-PlainCardHash $taskCard.ImagePath)){throw 'Deployment modified an existing rendered card'}
 }
}
$taskCheckedCount=0;if($BeforeClone){$taskCheckedCount=@($taskBeforeCards).Count}
[ordered]@{Result='PASS';ProcessId=$taskRunning[0].Id;Responding=$true;Cards=$taskCards.Count;Gems=$taskState.Gems;PaidCredits=$taskState.PaidCredits;PitySR=$taskState.PitySR;PitySSR=$taskState.PitySSR;PityUR=$taskState.PityUR;AssetsMatch=$true;BeforeCardsChecked=$taskCheckedCount;LiveActivityAllowed=[bool]$AllowActivity;ProductionWritten=$false} | ConvertTo-Json -Compress
