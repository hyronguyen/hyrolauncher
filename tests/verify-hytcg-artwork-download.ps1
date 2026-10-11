
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
$taskArtworkQaRoot=Join-Path $ProjectRoot ('output/verification/hytcg-artwork-download-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskArtworkQaRoot -Force | Out-Null
$taskArtworkCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskArtworkExe=Join-Path $taskArtworkQaRoot 'verify.exe'
# The existing open-bridge fixture supplies only the minimal host types. Its Main is
# not executed; all import, networking and parser methods are actual native sources.
$taskArtworkSources=@('ApiActivityLog.cs','HyTcgService.cs','HyTcgStorage.cs','HyTcgAcdbClient.cs','HyTcgRules.cs','HyTcgEconomy.cs','HyTcgDevEconomy.cs','HyTcgFrameArtwork.cs','HyTcgGameplay.cs','HyTcgCreative.cs','HyTcgFrameCatalog.cs','HyTcgDaily.cs','HyTcgDiscovery.cs','HyTcgCharacterInfo.cs','HyTcgActions.cs','HyTcgLifestyleActions.cs','HyTcgCosmeticRenderer.cs','HyTcgArtworkDownload.cs','verify-hytcg-open-compensation.cs','verify-hytcg-artwork-download.cs') | ForEach-Object {Resolve-TestSource $_}
& $taskArtworkCompiler /nologo /langversion:5 /target:exe /main:VerifyHyTcgArtworkDownload /out:$taskArtworkExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll /reference:System.Xml.Linq.dll $taskArtworkSources
if($LASTEXITCODE -ne 0){throw 'HYTCG artwork downloader verifier compilation failed'}
& $taskArtworkExe $taskArtworkQaRoot
if($LASTEXITCODE -ne 0){throw 'HYTCG artwork downloader verification failed'}
