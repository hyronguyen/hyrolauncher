param(
    [string]$Candidate,
    [string]$Url='https://raw.githubusercontent.com/github/explore/main/topics/csharp/csharp.png'
)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
if(!$Candidate){$Candidate=Get-TestExecutable}
$taskLiveArtworkQaRoot=Join-Path $ProjectRoot ('output/verification/hytcg-artwork-live-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskLiveArtworkQaRoot -Force | Out-Null
$taskLiveArtworkCandidate=Join-Path $taskLiveArtworkQaRoot 'HYROLauncher.Check.exe'
Copy-Item -LiteralPath $Candidate -Destination $taskLiveArtworkCandidate
$taskLiveArtworkCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskLiveArtworkExe=Join-Path $taskLiveArtworkQaRoot 'verify.exe'
& $taskLiveArtworkCompiler /nologo /langversion:5 /target:exe /out:$taskLiveArtworkExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $TestSourceRoot 'verify-hytcg-artwork-live.cs')
if($LASTEXITCODE -ne 0){throw 'HYTCG live artwork verifier compilation failed'}
& $taskLiveArtworkExe $taskLiveArtworkCandidate $taskLiveArtworkQaRoot $Url
if($LASTEXITCODE -ne 0){throw ('HYTCG live artwork verification failed; diagnostics in '+$taskLiveArtworkQaRoot)}
