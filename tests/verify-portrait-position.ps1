param([Parameter(Mandatory=$true)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskOut=Join-Path $VerificationRoot ('hytcg-portrait-position-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskOut | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $taskCompiler /nologo /target:exe "/out:$taskOut/verify.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $TestSourceRoot 'verify-portrait-position.cs')
if($LASTEXITCODE -ne 0){throw 'Portrait/art position native verifier compilation failed.'}
& (Join-Path $taskOut 'verify.exe') ([IO.Path]::GetFullPath($CandidatePath)) $taskOut
if($LASTEXITCODE -ne 0){throw 'Portrait/art position native verification failed; see isolated report.json.'}
Get-Content -LiteralPath (Join-Path $taskOut 'report.json')
