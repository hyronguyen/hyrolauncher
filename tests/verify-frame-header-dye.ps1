param([Parameter(Mandatory=$true)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskRoot=$ProjectRoot
$taskResult=Join-Path $VerificationRoot 'frame-header-dye'
New-Item -ItemType Directory -Force -Path $taskResult | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $taskCompiler /nologo /target:exe "/out:$taskResult/verify.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll (Join-Path $TestSourceRoot 'verify-frame-header-dye.cs')
if($LASTEXITCODE -ne 0){throw 'Frame verification compilation failed.'}
& (Join-Path $taskResult 'verify.exe') ([IO.Path]::GetFullPath($CandidatePath)) $taskResult
if($LASTEXITCODE -ne 0){throw 'Frame header/dye verification failed.'}
