
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
$taskLifestyleQaRoot=Join-Path $ProjectRoot ('output/verification/hytcg-lifestyle-bridge-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskLifestyleQaRoot -Force | Out-Null
$taskLifestyleCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskLifestyleExe=Join-Path $taskLifestyleQaRoot 'verify.exe'
# Only document/post host methods are stubbed. The dispatcher, asynchronous RPC
# handlers, economy transactions, protected storage and image compositor are real.
$taskLifestyleHost=Join-Path $taskLifestyleQaRoot 'fixture-host.cs'
$taskLifestyleHostText=Get-Content -LiteralPath (Join-Path $TestSourceRoot 'verify-hytcg-open-compensation.cs') -Raw
$taskLifestyleHostText=$taskLifestyleHostText.Replace('        private readonly HashSet<string> hyTcgDiscoveryRequests=new HashSet<string>();','').Replace('        private bool HandleHyTcgExtra(PluginBrowserWindow window,string session,string type,Dictionary<string,object> message){return false;}','')
Set-Content -LiteralPath $taskLifestyleHost -Value $taskLifestyleHostText -Encoding UTF8
$taskLifestyleSources=@('ApiActivityLog.cs','HyTcgService.cs','HyTcgStorage.cs','HyTcgAcdbClient.cs','HyTcgRules.cs','HyTcgEconomy.cs','HyTcgDevEconomy.cs','HyTcgFrameArtwork.cs','HyTcgGameplay.cs','HyTcgCreative.cs','HyTcgFrameCatalog.cs','HyTcgDaily.cs','HyTcgDiscovery.cs','HyTcgCharacterInfo.cs','HyTcgActions.cs','HyTcgDiscoveryActions.cs','HyTcgLifestyleActions.cs','HyTcgCosmeticRenderer.cs','HyTcgArtworkDownload.cs','verify-hytcg-lifestyle-bridge.cs') | ForEach-Object {Resolve-TestSource $_}
$taskLifestyleSources += $taskLifestyleHost
& $taskLifestyleCompiler /nologo /langversion:5 /target:exe /main:VerifyHyTcgLifestyleBridge /out:$taskLifestyleExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll /reference:System.Xml.Linq.dll $taskLifestyleSources
if($LASTEXITCODE -ne 0){throw 'HYTCG lifestyle bridge verifier compilation failed'}
& $taskLifestyleExe $taskLifestyleQaRoot
if($LASTEXITCODE -ne 0){throw 'HYTCG lifestyle bridge verification failed'}
