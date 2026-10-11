
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
$taskQaRoot=Join-Path $ProjectRoot ('output/verification/hytcg-cosmetic-renderer-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskQaRoot -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskQaExe=Join-Path $taskQaRoot 'verify.exe'
$taskSources=@('ApiActivityLog.cs','HyTcgService.cs','HyTcgCharacterInfo.cs','HyTcgDiscovery.cs','HyTcgStorage.cs','HyTcgDaily.cs','HyTcgAcdbClient.cs','HyTcgRules.cs','HyTcgEconomy.cs','HyTcgDevEconomy.cs','HyTcgFrameArtwork.cs','HyTcgCreative.cs','HyTcgGameplay.cs','HyTcgFrameCatalog.cs','HyTcgCosmeticRenderer.cs','verify-hytcg-cosmetic-renderer.cs')
$taskSourcePaths=$taskSources | ForEach-Object {Resolve-TestSource $_}
& $taskCompiler /nologo /target:exe /out:$taskQaExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll /reference:System.Xml.Linq.dll $taskSourcePaths
if($LASTEXITCODE -ne 0){throw 'HYTCG cosmetic renderer verifier compilation failed'}
& $taskQaExe $taskQaRoot
if($LASTEXITCODE -ne 0){throw 'HYTCG cosmetic renderer verification failed'}
