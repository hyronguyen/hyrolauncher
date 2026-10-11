param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$taskFramework = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue
if (!$taskFramework -or [int]$taskFramework.Release -lt 528040) { throw 'Install .NET Framework 4.8 or later before building HYROLauncher.' }
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $taskCompiler)) { $taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
if (!(Test-Path -LiteralPath $taskCompiler)) { throw '.NET Framework C# compiler is missing.' }
$taskPin = (Get-Content -LiteralPath (Join-Path $taskRoot 'config/dependencies.json') -Raw | ConvertFrom-Json).WebView2
if ($taskPin.Version -ne '1.0.2365.46' -or $taskPin.PackageUrl -ne 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.2365.46/microsoft.web.webview2.1.0.2365.46.nupkg') { throw 'Unexpected WebView2 dependency configuration.' }
try { $taskPinnedHash = [Convert]::FromBase64String($taskPin.Sha512) } catch { throw 'Invalid pinned WebView2 checksum.' }
if ($taskPinnedHash.Length -ne 64) { throw 'Invalid pinned WebView2 checksum length.' }
$taskDeps = [IO.Path]::GetFullPath((Join-Path $taskRoot '.deps/WebView2'))
if (!$taskDeps.StartsWith($taskRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Dependency directory escaped the project.' }
foreach ($taskCheckPath in @((Join-Path $taskRoot '.deps'), $taskDeps)) {
 if ((Test-Path -LiteralPath $taskCheckPath) -and ((Get-Item -LiteralPath $taskCheckPath).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Dependency directories cannot be links.' }
}
New-Item -ItemType Directory -Path $taskDeps -Force | Out-Null
$taskPackage = Join-Path $taskDeps ('Microsoft.Web.WebView2.' + $taskPin.Version + '.nupkg')
if (!(Test-Path -LiteralPath $taskPackage)) {
 if ($Offline) { throw 'WebView2 SDK is not cached. Run scripts/restore-dependencies.ps1 once with internet access.' }
 $taskTemp = $taskPackage + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
 try {
  [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
  Write-Host ('Downloading official WebView2 SDK ' + $taskPin.Version + '...')
  $taskClient = New-Object Net.WebClient
  try { $taskClient.DownloadFile($taskPin.PackageUrl, $taskTemp) } finally { $taskClient.Dispose() }
  $taskSha = [Security.Cryptography.SHA512]::Create()
  try { $taskActualHash = [Convert]::ToBase64String($taskSha.ComputeHash([IO.File]::ReadAllBytes($taskTemp))) } finally { $taskSha.Dispose() }
  if ($taskActualHash -cne $taskPin.Sha512) { throw 'Downloaded WebView2 SDK failed its pinned SHA-512 verification.' }
  [IO.File]::Move($taskTemp, $taskPackage)
 } finally { if (Test-Path -LiteralPath $taskTemp) { Remove-Item -LiteralPath $taskTemp -Force } }
}
if ((Get-Item -LiteralPath $taskPackage).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Cached SDK package cannot be a link.' }
$taskSha = [Security.Cryptography.SHA512]::Create()
try { $taskActualHash = [Convert]::ToBase64String($taskSha.ComputeHash([IO.File]::ReadAllBytes($taskPackage))) } finally { $taskSha.Dispose() }
if ($taskActualHash -cne $taskPin.Sha512) { throw 'Cached WebView2 SDK failed its pinned SHA-512 verification.' }
$taskSdkRoot = [IO.Path]::GetFullPath((Join-Path $taskDeps $taskPin.Version))
if ((Test-Path -LiteralPath $taskSdkRoot) -and ((Get-Item -LiteralPath $taskSdkRoot).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'SDK directory cannot be a link.' }
New-Item -ItemType Directory -Path $taskSdkRoot -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive = [IO.Compression.ZipFile]::OpenRead($taskPackage)
try {
 $taskEntries = @('lib/net45/Microsoft.Web.WebView2.Core.dll', 'lib/net45/Microsoft.Web.WebView2.WinForms.dll', 'runtimes/win-x64/native/WebView2Loader.dll', 'LICENSE.txt')
 foreach ($taskEntryName in $taskEntries) {
  $taskEntry = $taskArchive.GetEntry($taskEntryName)
  if (!$taskEntry -and $taskEntryName -eq 'LICENSE.txt') { $taskEntry = $taskArchive.GetEntry('LICENSE') }
  if (!$taskEntry) { throw ('WebView2 SDK package is missing ' + $taskEntryName + '.') }
  $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskSdkRoot $taskEntryName))
  if (!$taskDestination.StartsWith($taskSdkRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'SDK entry escaped the destination.' }
  $taskParent = [IO.Path]::GetDirectoryName($taskDestination)
  $taskProbe = $taskParent
  while ($taskProbe.StartsWith($taskSdkRoot, [StringComparison]::OrdinalIgnoreCase)) {
   if ((Test-Path -LiteralPath $taskProbe) -and ((Get-Item -LiteralPath $taskProbe).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'SDK entry path cannot contain links.' }
   if ($taskProbe -eq $taskSdkRoot) { break }
   $taskProbe = [IO.Path]::GetDirectoryName($taskProbe)
  }
  if ((Test-Path -LiteralPath $taskDestination) -and ((Get-Item -LiteralPath $taskDestination).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'SDK file cannot be a link.' }
  New-Item -ItemType Directory -Path $taskParent -Force | Out-Null
  $taskInput = $taskEntry.Open()
  $taskBuffer = New-Object IO.MemoryStream
  try { $taskInput.CopyTo($taskBuffer); $taskExpectedBytes = $taskBuffer.ToArray() } finally { $taskInput.Dispose(); $taskBuffer.Dispose() }
  $taskNeedsWrite = !(Test-Path -LiteralPath $taskDestination)
  if (!$taskNeedsWrite) {
   $taskExistingBytes = [IO.File]::ReadAllBytes($taskDestination)
   $taskNeedsWrite = [Convert]::ToBase64String($taskExistingBytes) -cne [Convert]::ToBase64String($taskExpectedBytes)
  }
  if ($taskNeedsWrite) { [IO.File]::WriteAllBytes($taskDestination, $taskExpectedBytes) }
 }
} finally { $taskArchive.Dispose() }
Write-Host ('Verified WebView2 SDK ' + $taskPin.Version + ' (SHA-512).')
