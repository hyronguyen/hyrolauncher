
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
$taskRoot=Join-Path $ProjectRoot ('output/verification/launcher-performance-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
Copy-Item -LiteralPath (Get-TestExecutable) -Destination (Join-Path $taskRoot 'HYROLauncher.Check.exe')
foreach($taskName in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll')) {
 Copy-Item -LiteralPath (Join-Path $ProjectRoot ('dist/'+$taskName)) -Destination (Join-Path $taskRoot $taskName)
}
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'ui') -Destination $taskRoot -Recurse
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'assets/icons') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'assets/app.ico') -Destination (Join-Path $taskRoot 'assets/app.ico')
Copy-Item -Path (Join-Path $ProjectRoot 'assets/icons/*') -Destination (Join-Path $taskRoot 'assets/icons')
[IO.File]::WriteAllText((Join-Path $taskRoot 'launcher-data.json'),'{"Games":[],"Plugins":[],"Settings":{"Title":"HYRO fixture","Language":"en","SteamId":"","AccentColor":"#4BC9E6","DisableBgm":true,"LibraryLayout":"default"}}')
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskExe=Join-Path $taskRoot 'verify.exe'
& $taskCompiler /nologo /target:exe /out:$taskExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll ('/reference:'+(Join-Path $taskRoot 'Microsoft.Web.WebView2.Core.dll')) ('/reference:'+(Join-Path $taskRoot 'Microsoft.Web.WebView2.WinForms.dll')) (Join-Path $TestSourceRoot 'verify-launcher-performance.cs')
if($LASTEXITCODE -ne 0){throw 'Launcher performance verifier compilation failed'}
& $taskExe
if($LASTEXITCODE -ne 0){throw 'Launcher performance verification failed'}
