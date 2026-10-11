param([string]$DeploymentPath='')
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskWorkspace=$ProjectRoot
$taskCandidate=Split-Path -Parent (Get-TestExecutable)
if($DeploymentPath){$taskCandidate=[IO.Path]::GetFullPath($DeploymentPath)}
$taskOutput=Join-Path $VerificationRoot ('hytcg-dev-webview2-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskCandidateExe=Join-Path $taskCandidate 'HYROLauncher.Check.exe'
if(!(Test-Path -LiteralPath $taskCandidateExe)){$taskCandidateExe=Join-Path $taskCandidate 'HYROLauncher.exe'}
Copy-Item -LiteralPath $taskCandidateExe -Destination (Join-Path $taskOutput 'HYROLauncher.Check.exe')
foreach($taskName in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')) {
 Copy-Item -LiteralPath (Join-Path $taskCandidate $taskName) -Destination (Join-Path $taskOutput $taskName)
}
Copy-Item -LiteralPath (Join-Path $taskCandidate 'ui') -Destination (Join-Path $taskOutput 'ui') -Recurse
$taskAssets=Join-Path $taskOutput 'assets'
New-Item -ItemType Directory -Path $taskAssets -Force | Out-Null
foreach($taskAsset in Get-ChildItem -LiteralPath (Join-Path $taskCandidate 'assets')) {
 if($taskAsset.Name -eq 'apikey'){continue}
 Copy-Item -LiteralPath $taskAsset.FullName -Destination (Join-Path $taskAssets $taskAsset.Name) -Recurse
}
$taskKeys=Join-Path $taskAssets 'apikey'
New-Item -ItemType Directory -Path $taskKeys -Force | Out-Null
$taskFixtureConfig='{"CharacterIdMin":1,"CharacterIdMax":999999,"MaxCharacterAttempts":12,"Concurrency":2,"RequestTimeoutSeconds":10,"RetryDelayMs":100}'
[IO.File]::WriteAllText((Join-Path $taskKeys 'hytcgconfig.json'),$taskFixtureConfig)
[IO.File]::WriteAllText((Join-Path $taskKeys 'zerochanuser.txt'),'WebViewFixture')
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskExecutable=Join-Path $taskOutput 'verify.exe'
& $taskCompiler /nologo /target:exe /out:$taskExecutable /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $taskOutput 'Microsoft.Web.WebView2.Core.dll')) ('/reference:'+(Join-Path $taskOutput 'Microsoft.Web.WebView2.WinForms.dll')) (Join-Path $TestSourceRoot 'verify-hytcg-dev-webview2.cs')
if($LASTEXITCODE -ne 0){throw 'Native dev WebView2 host compilation failed'}
$taskProcess=Start-Process -FilePath $taskExecutable -WorkingDirectory $taskOutput -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskOutput 'stdout.txt') -RedirectStandardError (Join-Path $taskOutput 'stderr.txt') -PassThru
$null=$taskProcess.Handle
if(-not $taskProcess.WaitForExit(55000)){$taskProcess.Kill();throw ('Native dev WebView2 timed out: '+$taskOutput)}
Get-Content -LiteralPath (Join-Path $taskOutput 'stdout.txt')
if($taskProcess.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $taskOutput 'stderr.txt');throw ('Native dev WebView2 failed: '+$taskOutput)}
Write-Output ('Report: '+(Join-Path $taskOutput 'report.json'))
