param([Parameter(Mandatory=$true)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskProjectRoot=$ProjectRoot
$taskTestRoot=Join-Path $VerificationRoot ('hytcg-dev-economy-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskTestRoot | Out-Null
Add-Type -LiteralPath (Join-Path $TestSourceRoot 'verify-hytcg-dev-economy.cs') -ReferencedAssemblies System.dll,System.Core.dll,System.Drawing.dll,System.Web.Extensions.dll
$taskResult=[HyTcgDevEconomyCheck]::Run([IO.Path]::GetFullPath($CandidatePath),$taskTestRoot)
$taskResult | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $VerificationRoot 'hytcg-dev-economy-result.json')
$taskResult | ConvertTo-Json -Depth 4
