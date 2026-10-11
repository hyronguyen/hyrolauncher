param([string]$ExecutableName='HYROLauncher.exe',[string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath($PSScriptRoot)
if([IO.Path]::GetFileName($ExecutableName) -ne $ExecutableName -or [IO.Path]::GetExtension($ExecutableName) -ne '.exe'){throw 'ExecutableName must be a plain .exe filename.'}
$taskOut=Join-Path $taskRoot 'dist'
if($OutputDirectory){
 $taskOut=[IO.Path]::GetFullPath($OutputDirectory)
 $taskOutputPrefix=[IO.Path]::GetFullPath((Join-Path $taskRoot 'output')).TrimEnd('\')+'\'
 if(!$taskOut.StartsWith($taskOutputPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Custom build output must be inside project/output.'}
}
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(!(Test-Path -LiteralPath $taskCompiler)){$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'}
if(!(Test-Path -LiteralPath $taskCompiler)){throw 'Install .NET Framework 4.8 before building.'}
& (Join-Path $taskRoot 'scripts/restore-dependencies.ps1')
$taskSdk=Join-Path $taskRoot '.deps/WebView2/1.0.2365.46'
$taskCore=Join-Path $taskSdk 'lib/net45/Microsoft.Web.WebView2.Core.dll'
$taskForms=Join-Path $taskSdk 'lib/net45/Microsoft.Web.WebView2.WinForms.dll'
$taskLoader=Join-Path $taskSdk 'runtimes/win-x64/native/WebView2Loader.dll'
foreach($taskDependency in @($taskCore,$taskForms,$taskLoader)){if(!(Test-Path -LiteralPath $taskDependency)){throw 'Restored WebView2 SDK is incomplete.'}}
New-Item -ItemType Directory -Path $taskOut -Force | Out-Null
$taskReferences=@('System.dll','System.Core.dll','System.Security.dll','System.Drawing.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Windows.Forms.dll','System.Web.Extensions.dll',$taskCore,$taskForms)
$taskArguments=@('/nologo','/langversion:5','/target:winexe','/platform:x64','/optimize+','/debug-',('/out:'+(Join-Path $taskOut $ExecutableName)),('/win32icon:'+(Join-Path $taskRoot 'assets/app.ico')))
$taskArguments+=@($taskReferences | ForEach-Object {'/reference:'+$_})
$taskSources=@(Get-ChildItem -LiteralPath $taskRoot -File -Filter '*.cs' | Where-Object {$_.Name -notlike 'verify-*'} | Sort-Object Name)
$taskArguments+=@($taskSources | ForEach-Object {$_.FullName})
& $taskCompiler $taskArguments
if($LASTEXITCODE -ne 0){throw 'HYROLauncher compilation failed.'}
$taskAssetDest=Join-Path $taskOut 'assets'
New-Item -ItemType Directory -Path $taskAssetDest -Force | Out-Null
# Copy only bundled resources; never local keys or user libraries.
foreach($taskFolder in @('builtin-plugins','icons','pets','hytcg')){
 $taskDestination=[IO.Path]::GetFullPath((Join-Path $taskAssetDest $taskFolder))
 if(!$taskDestination.StartsWith([IO.Path]::GetFullPath($taskAssetDest).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Asset target escaped build folder.'}
 if(Test-Path -LiteralPath $taskDestination){
  if((Get-Item -LiteralPath $taskDestination).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Cannot mirror a linked asset folder.'}
  Remove-Item -LiteralPath $taskDestination -Recurse -Force
 }
 Copy-Item -LiteralPath (Join-Path (Join-Path $taskRoot 'assets') $taskFolder) -Destination $taskDestination -Recurse
}
foreach($taskAsset in @('app.ico','hyro-logo.svg','default-logo.png','default-banner.png','default BGM.mp3','start_game.mp3','switch_to_game.mp3','back_to_all_game.mp3','click.mp3')){
 Copy-Item -LiteralPath (Join-Path (Join-Path $taskRoot 'assets') $taskAsset) -Destination $taskAssetDest -Force
}
$taskKeyDestination=Join-Path $taskAssetDest 'apikey'
New-Item -ItemType Directory -Path $taskKeyDestination -Force | Out-Null
foreach($taskExample in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'config/examples') -File){
 $taskConfigDestination=Join-Path $taskKeyDestination $taskExample.Name
 if(!(Test-Path -LiteralPath $taskConfigDestination)){Copy-Item -LiteralPath $taskExample.FullName -Destination $taskConfigDestination}
}
foreach($taskDependency in @($taskCore,$taskForms,$taskLoader)){Copy-Item -LiteralPath $taskDependency -Destination $taskOut -Force}
Copy-Item -LiteralPath (Join-Path $taskRoot 'ui') -Destination $taskOut -Recurse -Force
$taskLicenseDestination=Join-Path $taskOut 'licenses'
New-Item -ItemType Directory -Path $taskLicenseDestination -Force | Out-Null
if(Test-Path -LiteralPath (Join-Path $taskRoot 'licenses')){Copy-Item -Path (Join-Path $taskRoot 'licenses/*') -Destination $taskLicenseDestination -Force}
$taskSdkLicense=Get-ChildItem -LiteralPath $taskSdk -File | Where-Object {$_.Name -match '^LICENSE(\.txt)?$'} | Select-Object -First 1
if($taskSdkLicense){Copy-Item -LiteralPath $taskSdkLicense.FullName -Destination (Join-Path $taskLicenseDestination 'WebView2-LICENSE.txt') -Force}
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/THIRD-PARTY-NOTICES.md') -Destination $taskOut -Force
Write-Output ('Built: '+(Join-Path $taskOut $ExecutableName))