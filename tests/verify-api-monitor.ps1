
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
$taskRoot=Join-Path $ProjectRoot ('output/verification/api-monitor-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$taskExe=Join-Path $taskRoot 'verify.exe'
& $taskCompiler /nologo /target:exe /out:$taskExe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $ProjectRoot 'ApiActivityLog.cs') (Join-Path $ProjectRoot 'ApiLogWindow.cs') (Join-Path $TestSourceRoot 'verify-api-monitor.cs')
if($LASTEXITCODE -ne 0){throw 'Monitor verifier compilation failed'}
& $taskExe $taskRoot
if($LASTEXITCODE -ne 0){throw 'API monitor verification failed'}
