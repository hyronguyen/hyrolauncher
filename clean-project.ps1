param([switch]$Preview,[switch]$KeepCheckBuild)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$taskPrefix=$taskRoot+'\'
$taskTargets=New-Object 'System.Collections.Generic.List[string]'
function Add-CleanupTarget([string]$relative){
 $taskPath=[IO.Path]::GetFullPath((Join-Path $taskRoot $relative))
 if(!$taskPath.StartsWith($taskPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup escaped workspace.'}
 if(Test-Path -LiteralPath $taskPath){$taskTargets.Add($taskPath)}
}
$taskVerification=Join-Path $taskRoot 'output/verification'
if(Test-Path -LiteralPath $taskVerification){
 if(@(Get-ChildItem -LiteralPath $taskVerification -File | Where-Object {$_.Name -like 'verify-*'}).Count){throw 'Move regression sources into tests before cleanup.'}
 foreach($taskDirectory in Get-ChildItem -LiteralPath $taskVerification -Directory){if($taskDirectory.Name -ne 'latest'){Add-CleanupTarget ('output/verification/'+$taskDirectory.Name)}}
 foreach($taskName in @('HyTcgCreative.before-diagnostics.cs','inspect-import-creative.ps1','restore-creative-read-snapshot.ps1','lifestyle-paid-tests.txt','adventure-ui-checks.inc','audit-publication.ps1','deploy-angled-shelf.ps1','capture-tcg-runtime.ps1')){Add-CleanupTarget ('output/verification/'+$taskName)}
}
foreach($taskName in @('output/pet-source','output/check-tui-mau-V500526017297.sql','output/reorganize-test-sources.ps1','package-buddy-animation.py','package-kafka-kled-pets.py','package-pet-drag.py','inspect-logo.ps1','render-mp3-preview.cjs','refresh-launcher-logo.ps1')){Add-CleanupTarget $taskName}
if(!$KeepCheckBuild){Add-CleanupTarget 'dist/HYROLauncher.Check.exe'}
$taskRunning=@(Get-Process -ErrorAction SilentlyContinue | ForEach-Object {try{$_.Path}catch{}} | Where-Object {$_})
$taskFiles=0;$taskBytes=0L;$taskSkipped=@()
foreach($taskTarget in $taskTargets){
 if(@($taskRunning | Where-Object {$_ -eq $taskTarget -or $_.StartsWith($taskTarget.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){$taskSkipped+=$taskTarget;continue}
 $taskEntry=Get-Item -LiteralPath $taskTarget
 if($taskEntry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked cleanup target.'}
 $taskStack=New-Object 'System.Collections.Generic.Stack[System.IO.DirectoryInfo]'
 $taskCount=0;$taskSize=0L
 if($taskEntry.PSIsContainer){$taskStack.Push($taskEntry)}else{$taskCount=1;$taskSize=$taskEntry.Length}
 while($taskStack.Count){
  foreach($taskChild in Get-ChildItem -LiteralPath $taskStack.Pop().FullName -Force){
   if($taskChild.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked cleanup descendant.'}
   if(![IO.Path]::GetFullPath($taskChild.FullName).StartsWith($taskPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup descendant escaped workspace.'}
   if($taskChild.PSIsContainer){$taskStack.Push($taskChild)}else{$taskCount++;$taskSize+=$taskChild.Length}
  }
 }
 if(!$Preview){Remove-Item -LiteralPath $taskTarget -Recurse -Force}
 $taskFiles+=$taskCount;$taskBytes+=$taskSize
}
$taskReport=[ordered]@{Preview=[bool]$Preview;TargetCount=$taskTargets.Count;Files=$taskFiles;FreedMB=[Math]::Round($taskBytes/1MB,2);SkippedRunningFiles=$taskSkipped;RuntimeDataPreserved=$true;RegressionSourcesPreserved=$true}
if(!$Preview){$taskReport | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRoot 'output/verification/latest/cleanup.json') -Encoding UTF8}
$taskReport | ConvertTo-Json -Compress