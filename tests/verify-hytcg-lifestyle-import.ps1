param([string]$Candidate)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
if(!$Candidate){$Candidate=Get-TestExecutable}
$taskQaRoot=Join-Path $ProjectRoot ('output/verification/hytcg-lifestyle-import-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskQaRoot -Force | Out-Null
$taskCandidateCopy=Join-Path $taskQaRoot 'HYROLauncher.Check.exe'
Copy-Item -LiteralPath $Candidate -Destination $taskCandidateCopy
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskQaExe=Join-Path $taskQaRoot 'verify.exe'
& $taskCompiler /nologo /target:exe /out:$taskQaExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll (Join-Path $TestSourceRoot 'verify-hytcg-lifestyle-import.cs')
if($LASTEXITCODE -ne 0){throw 'HYTCG lifestyle import verifier compilation failed'}
& $taskQaExe $taskCandidateCopy (Join-Path $taskQaRoot 'account')
if($LASTEXITCODE -ne 0){throw 'HYTCG lifestyle import verification failed'}
