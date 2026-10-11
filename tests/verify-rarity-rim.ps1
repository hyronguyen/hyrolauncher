param([Parameter(Mandatory=$true)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskOut=Join-Path $VerificationRoot 'rarity-rim-native'
New-Item -ItemType Directory -Force -Path $taskOut | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $taskCompiler /nologo /target:exe "/out:$taskOut/verify.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $TestSourceRoot 'verify-rarity-rim.cs')
if($LASTEXITCODE -ne 0){throw 'Rarity rim verification compilation failed.'}
& (Join-Path $taskOut 'verify.exe') ([IO.Path]::GetFullPath($CandidatePath)) (Join-Path $VerificationRoot 'rarity-rim-ui') $taskOut
if($LASTEXITCODE -ne 0){throw 'Rarity rim verification failed.'}
