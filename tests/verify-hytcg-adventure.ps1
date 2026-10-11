
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
$taskAdventureRoot=Join-Path $ProjectRoot ('output/verification/hytcg-adventure-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskAdventureRoot -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskExe=Join-Path $taskAdventureRoot 'verify.exe'
$taskSources=@('ApiActivityLog.cs','HyTcgService.cs','HyTcgCharacterInfo.cs','HyTcgDiscovery.cs','HyTcgStorage.cs','HyTcgDaily.cs','HyTcgAcdbClient.cs','HyTcgRules.cs','HyTcgEconomy.cs','HyTcgDevEconomy.cs','HyTcgFrameArtwork.cs','HyTcgCreative.cs','HyTcgFrameCatalog.cs','HyTcgGameplay.cs','HyTcgCosmeticRenderer.cs','verify-hytcg-adventure.cs')
$taskSourcePaths=$taskSources | ForEach-Object {Resolve-TestSource $_}
& $taskCompiler /nologo /langversion:5 /target:exe /out:$taskExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll /reference:System.Xml.Linq.dll $taskSourcePaths
if($LASTEXITCODE -ne 0){throw 'Adventure verifier compilation failed'}
& $taskExe $taskAdventureRoot
if($LASTEXITCODE -ne 0){throw 'Adventure verification failed'}
