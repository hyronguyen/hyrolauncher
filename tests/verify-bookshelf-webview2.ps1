param([switch]$Reduced,[switch]$Baseline)
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskWorkspace=$ProjectRoot
$taskRuntime=Split-Path -Parent (Get-TestExecutable)
$taskOutput=Join-Path $VerificationRoot ('bookshelf-webview2-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
foreach($taskName in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')) {Copy-Item -LiteralPath (Join-Path $taskRuntime $taskName) -Destination (Join-Path $taskOutput $taskName)}
Copy-Item -LiteralPath (Join-Path $taskWorkspace 'ui') -Destination (Join-Path $taskOutput 'ui') -Recurse
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskExecutable=Join-Path $taskOutput 'verify.exe'
& $taskCompiler /nologo /target:exe /out:$taskExecutable /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $taskOutput 'Microsoft.Web.WebView2.Core.dll')) ('/reference:'+(Join-Path $taskOutput 'Microsoft.Web.WebView2.WinForms.dll')) (Join-Path $TestSourceRoot 'verify-bookshelf-webview2.cs')
if($LASTEXITCODE -ne 0){throw 'Native bookshelf WebView2 host compilation failed'}
$taskArguments=@('normal');if($Reduced){$taskArguments=@('reduced')};if($Baseline){$taskArguments+=@('baseline')}
$taskProcess=Start-Process -FilePath $taskExecutable -ArgumentList $taskArguments -WorkingDirectory $taskOutput -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskOutput 'stdout.txt') -RedirectStandardError (Join-Path $taskOutput 'stderr.txt') -PassThru
$null=$taskProcess.Handle
if(-not $taskProcess.WaitForExit(30000)){$taskProcess.Kill();throw ('Native bookshelf WebView2 timed out: '+$taskOutput)}
Get-Content -LiteralPath (Join-Path $taskOutput 'stdout.txt')
if($taskProcess.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $taskOutput 'stderr.txt');throw ('Native bookshelf WebView2 failed: '+$taskOutput)}
Get-Content -LiteralPath (Join-Path $taskOutput 'report.json')
Write-Output ('Report: '+(Join-Path $taskOutput 'report.json'))
