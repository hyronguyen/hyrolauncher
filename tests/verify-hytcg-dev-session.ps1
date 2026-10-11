param([Parameter(Mandatory=$true)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskTestRoot=Join-Path $VerificationRoot ('hytcg-dev-session-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskTestRoot | Out-Null
Add-Type -LiteralPath @((Join-Path $TestSourceRoot 'verify-hytcg-dev-economy.cs'),(Join-Path $TestSourceRoot 'verify-hytcg-dev-session.cs')) -ReferencedAssemblies System.dll,System.Core.dll,System.Drawing.dll,System.Web.Extensions.dll
$taskResult=[HyTcgDevSessionCheck]::Run([IO.Path]::GetFullPath($CandidatePath),$taskTestRoot)
$taskResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $VerificationRoot 'hytcg-dev-session-result.json')
$taskResult | ConvertTo-Json -Depth 5
