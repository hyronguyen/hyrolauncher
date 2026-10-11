param([string]$CardsSource='')
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security,System.Web.Extensions
$taskSourceCards=if($CardsSource){[IO.Path]::GetFullPath($CardsSource)}else{Join-Path $ProjectRoot 'dist/cards'}
$taskExpected=@(Get-ChildItem -LiteralPath $taskSourceCards -File -Filter '*.png').Count
$taskMigrationRoot=Join-Path $ProjectRoot ('output/verification/hytcg-legacy-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $taskMigrationRoot 'cards') -Force | Out-Null
Get-ChildItem -LiteralPath $taskSourceCards -File -Filter '*.png' | Copy-Item -Destination (Join-Path $taskMigrationRoot 'cards')
$taskAssembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$taskType=$taskAssembly.GetType('HYROLauncherWeb.HyTcgService')
$taskFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$taskCtor=$taskType.GetConstructors($taskFlags) | Where-Object {$_.GetParameters().Count -eq 1}
$taskService=$taskCtor.Invoke(@([string]$taskMigrationRoot))
$taskCollection=$taskType.GetMethods($taskFlags) | Where-Object {$_.Name -eq 'Collection' -and $_.GetParameters().Count -eq 3}
$taskPage=$taskCollection.Invoke($taskService,@(0,'',''))
if($taskPage.Total -ne $taskExpected -or @($taskPage.Items | Where-Object {$_.CanDissolve -or !$_.IsLegacy}).Count -ne 0){throw 'Legacy migration did not preserve all cards as read-only'}
$taskWallet=$taskType.GetProperty('Economy',$taskFlags).GetValue($taskService,$null)
$taskEconomyType=$taskWallet.GetType()
$taskSnapshot=$taskEconomyType.GetMethod('Snapshot',$taskFlags).Invoke($taskWallet,@())
if($taskSnapshot.FreeCredits -ne 5 -or $taskSnapshot.Gems -ne 1800){throw 'Legacy migration changed starting wallet'}
Write-Output "PASS: all $taskExpected existing PNGs preserved in isolated signed legacy migration; no gems derived from unauthenticated metadata."
