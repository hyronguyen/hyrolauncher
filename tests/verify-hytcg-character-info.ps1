param([switch]$Live)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskInfoRoot=Join-Path $ProjectRoot ('output/verification/hytcg-character-info-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskInfoRoot -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskInfoExe=Join-Path $taskInfoRoot 'verify.exe'
$taskInfoSources=@('ApiActivityLog.cs','HyTcgService.cs','HyTcgStorage.cs','HyTcgAcdbClient.cs','HyTcgRules.cs','HyTcgEconomy.cs','HyTcgDevEconomy.cs','HyTcgFrameArtwork.cs','HyTcgCosmeticRenderer.cs','HyTcgGameplay.cs','HyTcgCreative.cs','HyTcgFrameCatalog.cs','HyTcgDaily.cs','HyTcgDiscovery.cs','HyTcgCharacterInfo.cs','verify-hytcg-character-info.cs') | ForEach-Object {Resolve-TestSource $_}
& $taskCompiler /nologo /target:exe /out:$taskInfoExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll /reference:System.Xml.Linq.dll $taskInfoSources
if($LASTEXITCODE -ne 0){throw 'HYTCG character info verifier compilation failed'}
if($Live){& $taskInfoExe $taskInfoRoot '--live'}else {& $taskInfoExe $taskInfoRoot}
if($LASTEXITCODE -ne 0){throw 'HYTCG character info verification failed'}
