param([string]$DeploymentPath="")
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
$taskWorkspace=$ProjectRoot
$taskDeployment=Join-Path $taskWorkspace 'dist'
if($DeploymentPath){$taskDeployment=[IO.Path]::GetFullPath($DeploymentPath)}
$taskOutput=Join-Path $VerificationRoot ('lifestyle-webview2-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
foreach($taskName in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')) {
 Copy-Item -LiteralPath (Join-Path $taskDeployment $taskName) -Destination (Join-Path $taskOutput $taskName)
}
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskExecutable=Join-Path $taskOutput 'verify.exe'
& $taskCompiler /nologo /target:exe /out:$taskExecutable /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $taskOutput 'Microsoft.Web.WebView2.Core.dll')) ('/reference:'+(Join-Path $taskOutput 'Microsoft.Web.WebView2.WinForms.dll')) (Join-Path $TestSourceRoot 'verify-lifestyle-webview2.cs')
if($LASTEXITCODE -ne 0){throw 'WebView2 smoke host compilation failed'}
$taskProcess=Start-Process -FilePath $taskExecutable -ArgumentList ('"'+$taskDeployment+'"'),('"'+$taskOutput+'"') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskOutput 'stdout.txt') -RedirectStandardError (Join-Path $taskOutput 'stderr.txt') -PassThru
$null=$taskProcess.Handle
if(-not $taskProcess.WaitForExit(45000)){$taskProcess.Kill();throw ('WebView2 smoke timed out: '+$taskOutput)}
Get-Content -LiteralPath (Join-Path $taskOutput 'stdout.txt')
if($taskProcess.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $taskOutput 'stderr.txt');throw ('WebView2 smoke failed: '+$taskOutput)}
Write-Output ('Report: '+(Join-Path $taskOutput 'report.json'))
