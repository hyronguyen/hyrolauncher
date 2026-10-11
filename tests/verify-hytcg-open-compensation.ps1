
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
$taskBridgeRoot=Join-Path $ProjectRoot ('output/verification/hytcg-open-compensation-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskBridgeRoot -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskBridgeExe=Join-Path $taskBridgeRoot 'verify.exe'
$taskBridgeSources=@('ApiActivityLog.cs','HyTcgService.cs','HyTcgStorage.cs','HyTcgAcdbClient.cs','HyTcgRules.cs','HyTcgEconomy.cs','HyTcgDevEconomy.cs','HyTcgFrameArtwork.cs','HyTcgGameplay.cs','HyTcgCreative.cs','HyTcgFrameCatalog.cs','HyTcgDaily.cs','HyTcgDiscovery.cs','HyTcgCharacterInfo.cs','HyTcgActions.cs','HyTcgLifestyleActions.cs','HyTcgCosmeticRenderer.cs','verify-hytcg-open-compensation.cs') | ForEach-Object {Resolve-TestSource $_}
if(Test-Path -LiteralPath (Join-Path $ProjectRoot 'HyTcgArtworkDownload.cs')) {$taskBridgeSources+=(Join-Path $ProjectRoot 'HyTcgArtworkDownload.cs')}
& $taskCompiler /nologo /target:exe /out:$taskBridgeExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll /reference:System.Xml.Linq.dll $taskBridgeSources
if($LASTEXITCODE -ne 0){throw 'HYTCG actual open bridge verifier compilation failed'}
& $taskBridgeExe $taskBridgeRoot
if($LASTEXITCODE -ne 0){throw 'HYTCG actual open bridge verification failed'}
