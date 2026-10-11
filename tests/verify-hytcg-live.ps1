
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
$taskLiveRoot=Join-Path $ProjectRoot 'output/verification/hytcg-live'
New-Item -ItemType Directory -Path $taskLiveRoot -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskLiveExe=Join-Path $taskLiveRoot 'verify.exe'
& $taskCompiler /nologo /target:exe /out:$taskLiveExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll /reference:System.Xml.Linq.dll (Join-Path $ProjectRoot 'ApiActivityLog.cs') (Join-Path $ProjectRoot 'HyTcgService.cs') (Join-Path $ProjectRoot 'HyTcgCharacterInfo.cs') (Join-Path $ProjectRoot 'HyTcgDiscovery.cs') (Join-Path $ProjectRoot 'HyTcgDaily.cs') (Join-Path $ProjectRoot 'HyTcgStorage.cs') (Join-Path $ProjectRoot 'HyTcgAcdbClient.cs') (Join-Path $ProjectRoot 'HyTcgRules.cs') (Join-Path $ProjectRoot 'HyTcgEconomy.cs') (Join-Path $ProjectRoot 'HyTcgDevEconomy.cs') (Join-Path $ProjectRoot 'HyTcgFrameArtwork.cs') (Join-Path $ProjectRoot 'HyTcgCosmeticRenderer.cs') (Join-Path $ProjectRoot 'HyTcgGameplay.cs') (Join-Path $ProjectRoot 'HyTcgCreative.cs') (Join-Path $ProjectRoot 'HyTcgFrameCatalog.cs') (Join-Path $TestSourceRoot 'verify-hytcg-live.cs')
if($LASTEXITCODE -ne 0){throw 'HYTCG live verifier compilation failed'}
& $taskLiveExe $taskLiveRoot
if($LASTEXITCODE -ne 0){throw 'HYTCG live verification failed'}
