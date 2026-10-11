param(
 [string]$Version = '1.0.0',
 [string]$Repository = 'hyronguyen/hyrolauncher',
 [string]$ArtifactDirectory = '',
 [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$taskSafeFailure = 'Release validation failed.'
$taskStreams = New-Object 'System.Collections.Generic.List[System.IO.FileStream]'
$taskClient = $null
$taskToken = $null
function Get-StreamSha256([IO.Stream]$Stream) {
 $Stream.Position = 0
 $taskAlgorithm = [Security.Cryptography.SHA256]::Create()
 try { $taskHashBytes = $taskAlgorithm.ComputeHash($Stream) } finally { $taskAlgorithm.Dispose(); $Stream.Position = 0 }
 return ([BitConverter]::ToString($taskHashBytes).Replace('-','').ToLowerInvariant())
}
function Get-GitHubCredential {
 $taskGit = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
 $taskStart = New-Object Diagnostics.ProcessStartInfo
 $taskStart.FileName = $taskGit
 $taskStart.WorkingDirectory = $taskProjectRoot
 $taskStart.Arguments = 'credential fill'
 $taskStart.UseShellExecute = $false
 $taskStart.CreateNoWindow = $true
 $taskStart.RedirectStandardInput = $true
 $taskStart.RedirectStandardOutput = $true
 $taskStart.RedirectStandardError = $true
 $taskStart.EnvironmentVariables['GIT_TERMINAL_PROMPT'] = '0'
 $taskStart.EnvironmentVariables['GCM_INTERACTIVE'] = 'Never'
 $taskProcess = New-Object Diagnostics.Process
 $taskProcess.StartInfo = $taskStart
 try {
  [void]$taskProcess.Start()
  $taskOutputRead = $taskProcess.StandardOutput.ReadToEndAsync()
  $taskErrorRead = $taskProcess.StandardError.ReadToEndAsync()
  $taskProcess.StandardInput.Write("protocol=https`nhost=github.com`npath=hyronguyen/hyrolauncher.git`n`n")
  $taskProcess.StandardInput.Close()
  if (!$taskProcess.WaitForExit(30000)) { try { $taskProcess.Kill() } catch { }; throw 'Credential lookup timed out.' }
  $taskCredentialText = $taskOutputRead.GetAwaiter().GetResult()
  [void]$taskErrorRead.GetAwaiter().GetResult()
  if ($taskProcess.ExitCode -ne 0) { throw 'No stored GitHub credential is available.' }
  $taskPassword = ''
  foreach ($taskCredentialLine in $taskCredentialText.Split([char]10)) {
   if ($taskCredentialLine.StartsWith('password=', [StringComparison]::Ordinal)) { $taskPassword = $taskCredentialLine.Substring(9).TrimEnd([char]13) }
  }
  $taskCredentialText = $null
  if (!$taskPassword -or $taskPassword.Contains("`r") -or $taskPassword.Contains("`n")) { throw 'No stored GitHub credential is available.' }
  return $taskPassword
 } finally { $taskProcess.Dispose() }
}
function Assert-GitHubEndpoint([uri]$Uri, [switch]$Upload) {
 if ($Uri.Scheme -ne 'https' -or $Uri.Port -ne 443 -or $Uri.UserInfo -or $Uri.Fragment) { throw 'GitHub endpoint validation failed.' }
 if ($Upload) {
  if ($Uri.Host -ne 'uploads.github.com' -or $Uri.AbsolutePath -notmatch '^/repos/hyronguyen/hyrolauncher/releases/[0-9]+/assets$') { throw 'GitHub upload endpoint validation failed.' }
 } elseif ($Uri.Host -ne 'api.github.com' -or !$Uri.AbsolutePath.StartsWith('/repos/hyronguyen/hyrolauncher/', [StringComparison]::Ordinal)) { throw 'GitHub API endpoint validation failed.' }
}
function Invoke-GitHubRequest([string]$Method, [uri]$Uri, [string]$Json = '', [IO.Stream]$Stream = $null, [string]$ContentType = '', [switch]$AllowNotFound) {
 $taskIsUpload = $null -ne $Stream
 Assert-GitHubEndpoint $Uri -Upload:$taskIsUpload
 $taskRequest = New-Object Net.Http.HttpRequestMessage
 $taskRequest.Method = New-Object Net.Http.HttpMethod($Method)
 $taskRequest.RequestUri = $Uri
 $taskResponse = $null
 try {
  if ($taskIsUpload) {
   $Stream.Position = 0
   $taskRequest.Content = New-Object Net.Http.StreamContent($Stream, 81920)
   $taskRequest.Content.Headers.ContentType = New-Object Net.Http.Headers.MediaTypeHeaderValue($ContentType)
   $taskRequest.Content.Headers.ContentLength = $Stream.Length
  } elseif ($Json) { $taskRequest.Content = New-Object Net.Http.StringContent($Json, [Text.Encoding]::UTF8, 'application/json') }
  $taskResponse = $taskClient.SendAsync($taskRequest, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
  $taskStatus = [int]$taskResponse.StatusCode
  if ($AllowNotFound -and $taskStatus -eq 404) { return $null }
  if ($taskStatus -lt 200 -or $taskStatus -ge 300) {
   $script:taskSafeFailure = 'GitHub returned HTTP ' + $taskStatus + ' while publishing the release.'
   throw $script:taskSafeFailure
  }
  if ($taskResponse.Content.Headers.ContentLength -gt 2MB) { throw 'Unexpected GitHub API response size.' }
  $taskResponseText = $taskResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
  if ($taskResponseText.Length -gt 2MB) { throw 'Unexpected GitHub API response size.' }
  $taskDecoded = $taskResponseText | ConvertFrom-Json
  if ($taskDecoded -is [Array]) { foreach ($taskDecodedItem in $taskDecoded) { Write-Output $taskDecodedItem }; return }
  return $taskDecoded
 } finally { if ($taskResponse) { $taskResponse.Dispose() }; $taskRequest.Dispose() }
}
function Assert-RemoteAsset($Asset, $Local) {
 if ($Asset.name -cne $Local.Name -or [long]$Asset.size -ne $Local.Length -or $Asset.state -ne 'uploaded') {
  $script:taskSafeFailure = 'Existing GitHub asset differs from the verified local file: ' + $Local.Name + '. No assets were replaced.'
  throw $script:taskSafeFailure
 }
 if ($Asset.digest -and [string]$Asset.digest -cne ('sha256:' + $Local.Sha256)) {
  $script:taskSafeFailure = 'GitHub asset checksum differs from the verified local file: ' + $Local.Name + '. No assets were replaced.'
  throw $script:taskSafeFailure
 }
}
try {
 if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') { throw 'Version must have the form 1.0.0.' }
 if ($Repository -cne 'hyronguyen/hyrolauncher') { throw 'This publisher is restricted to hyronguyen/hyrolauncher.' }
 if (!$ArtifactDirectory) { $ArtifactDirectory = Join-Path $taskProjectRoot 'output/release' }
 $taskArtifacts = [IO.Path]::GetFullPath($ArtifactDirectory).TrimEnd('\')
 $taskOutputPrefix = [IO.Path]::GetFullPath((Join-Path $taskProjectRoot 'output')).TrimEnd('\') + '\'
 if (!$taskArtifacts.StartsWith($taskOutputPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Release artifacts must be inside project/output.' }
 if (!(Test-Path -LiteralPath $taskArtifacts -PathType Container)) { throw 'Release artifact directory is missing.' }
 $taskProbe = $taskArtifacts
 while ($taskProbe.StartsWith($taskProjectRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
  if ((Test-Path -LiteralPath $taskProbe) -and ((Get-Item -LiteralPath $taskProbe).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Release artifact directories cannot be links.' }
  $taskProbe = [IO.Path]::GetDirectoryName($taskProbe)
 }
 $taskNames = @(('HYROLauncher-Setup-' + $Version + '.exe'), ('HYROLauncher-Portable-' + $Version + '.zip'), 'SHA256SUMS.txt')
 $taskLocalAssets = New-Object 'System.Collections.Generic.List[object]'
 foreach ($taskName in $taskNames) {
  $taskFile = Join-Path $taskArtifacts $taskName
  if (!(Test-Path -LiteralPath $taskFile -PathType Leaf)) { throw ('Missing release artifact: ' + $taskName + '.') }
  if ((Get-Item -LiteralPath $taskFile).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Release artifact files cannot be links.' }
  $taskStream = New-Object IO.FileStream($taskFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
  $taskStreams.Add($taskStream)
  if ($taskStream.Length -le 0 -or $taskStream.Length -gt 2GB) { throw 'Release artifact has an invalid size.' }
  $taskLocalAssets.Add([pscustomobject]@{Name=$taskName; Length=$taskStream.Length; Sha256=(Get-StreamSha256 $taskStream); Stream=$taskStream})
 }
 $taskChecksumAsset = $taskLocalAssets | Where-Object {$_.Name -eq 'SHA256SUMS.txt'} | Select-Object -First 1
 if ($taskChecksumAsset.Length -gt 8192) { throw 'SHA256SUMS.txt is unexpectedly large.' }
 $taskChecksumBytes = New-Object byte[] ([int]$taskChecksumAsset.Length)
 [void]$taskChecksumAsset.Stream.Read($taskChecksumBytes, 0, $taskChecksumBytes.Length)
 $taskChecksumAsset.Stream.Position = 0
 $taskChecksumLines = [Text.Encoding]::UTF8.GetString($taskChecksumBytes).Trim([char]0xFEFF).Split([char]10)
 $taskChecksumNames = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
 foreach ($taskChecksumLine in $taskChecksumLines) {
  if (!$taskChecksumLine.Trim()) { continue }
  if ($taskChecksumLine.TrimEnd([char]13) -notmatch '^([0-9a-fA-F]{64}) [ *]([^/\\]+)$') { throw 'Invalid SHA256SUMS.txt entry.' }
  $taskExpectedHash = $Matches[1].ToLowerInvariant(); $taskExpectedName = $Matches[2]
  if (!$taskChecksumNames.Add($taskExpectedName)) { throw 'Duplicate SHA256SUMS.txt entry.' }
  $taskLocal = $taskLocalAssets | Where-Object {$_.Name -ceq $taskExpectedName -and $_.Name -ne 'SHA256SUMS.txt'} | Select-Object -First 1
  if (!$taskLocal -or $taskLocal.Sha256 -cne $taskExpectedHash) { throw ('Release checksum verification failed for ' + $taskExpectedName + '.') }
 }
 if ($taskChecksumNames.Count -ne 2) { throw 'SHA256SUMS.txt must cover the installer and portable ZIP.' }
 $taskPublicAssets = @($taskLocalAssets | ForEach-Object {[ordered]@{Name=$_.Name; Size=$_.Length}})
 if ($ValidateOnly) { [ordered]@{Validated=$true; Assets=$taskPublicAssets} | ConvertTo-Json -Depth 4; exit 0 }
 $taskSafeFailure = 'GitHub credentials are unavailable. Sign in through Git Credential Manager before publishing.'
 $taskToken = Get-GitHubCredential
 Add-Type -AssemblyName System.Net.Http
 [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
 $taskHandler = New-Object Net.Http.HttpClientHandler
 $taskHandler.AllowAutoRedirect = $false
 $taskClient = New-Object Net.Http.HttpClient($taskHandler, $true)
 $taskClient.Timeout = [TimeSpan]::FromMinutes(30)
 $taskClient.DefaultRequestHeaders.UserAgent.ParseAdd('HYROLauncher-release/1.0')
 $taskClient.DefaultRequestHeaders.Accept.ParseAdd('application/vnd.github+json')
 $taskClient.DefaultRequestHeaders.Add('X-GitHub-Api-Version','2022-11-28')
 $taskClient.DefaultRequestHeaders.Authorization = New-Object Net.Http.Headers.AuthenticationHeaderValue('Bearer',$taskToken)
 $taskTag = 'v' + $Version
 $taskApiRoot = 'https://api.github.com/repos/hyronguyen/hyrolauncher/'
 $taskSafeFailure = 'GitHub release lookup failed. Verify repository access and network connectivity.'
 $taskRelease = Invoke-GitHubRequest 'GET' ([uri]($taskApiRoot + 'releases/tags/' + $taskTag)) -AllowNotFound
 if (!$taskRelease) {
  $taskSafeFailure = 'GitHub release creation failed. Verify that main has been pushed and the credential can write releases.'
  $taskBody = "Windows 10/11 x64 release.`n`n- Offline installer with Microsoft .NET Framework 4.8 and WebView2 prerequisites included.`n- Installs per user under %LOCALAPPDATA%\Programs\HYROLauncher. Windows may request administrator permission if .NET Framework must be installed.`n- Portable ZIP is also available.`n- Updates and uninstall preserve user-created cards, progress and API configuration.`n- SHA256SUMS.txt verifies both downloads."
  $taskReleaseJson = [ordered]@{tag_name=$taskTag; target_commitish='main'; name=('HYRO Launcher ' + $Version); body=$taskBody; draft=$false; prerelease=$false} | ConvertTo-Json -Depth 3
  $taskRelease = Invoke-GitHubRequest 'POST' ([uri]($taskApiRoot + 'releases')) $taskReleaseJson
 }
 if ($taskRelease.tag_name -cne $taskTag -or $taskRelease.draft -or $taskRelease.prerelease -or [string]$taskRelease.id -notmatch '^[0-9]+$') { $taskSafeFailure = 'Existing release state does not match the requested public stable release.'; throw $taskSafeFailure }
 $taskUploadBase = ([string]$taskRelease.upload_url) -replace '\{\?name,label\}$',''
 Assert-GitHubEndpoint ([uri]$taskUploadBase) -Upload
 if (([uri]$taskUploadBase).AbsolutePath -cne ('/repos/hyronguyen/hyrolauncher/releases/' + $taskRelease.id + '/assets')) { throw 'GitHub release upload ID validation failed.' }
 $taskSafeFailure = 'GitHub release asset lookup failed.'
 $taskRemoteAssets = @(Invoke-GitHubRequest 'GET' ([uri]($taskApiRoot + 'releases/' + $taskRelease.id + '/assets?per_page=100')))
 if ($taskRemoteAssets.Count -ge 100) { throw 'Release has too many assets for a bounded upload review.' }
 foreach ($taskLocal in $taskLocalAssets) {
  $taskExisting = @($taskRemoteAssets | Where-Object {$_.name -ceq $taskLocal.Name})
  if ($taskExisting.Count -gt 1) { throw 'Duplicate GitHub release asset names.' }
  if ($taskExisting.Count) { Assert-RemoteAsset $taskExisting[0] $taskLocal; continue }
  $taskSafeFailure = 'GitHub upload failed for ' + $taskLocal.Name + '. Existing assets were kept; rerun to resume.'
  $taskUploadUri = [uri]($taskUploadBase + '?name=' + [uri]::EscapeDataString($taskLocal.Name))
  $taskContentType = if ($taskLocal.Name.EndsWith('.zip')) {'application/zip'} elseif ($taskLocal.Name.EndsWith('.txt')) {'text/plain'} else {'application/octet-stream'}
  $taskUploaded = Invoke-GitHubRequest 'POST' $taskUploadUri -Stream $taskLocal.Stream -ContentType $taskContentType
  Assert-RemoteAsset $taskUploaded $taskLocal
 }
 $taskPublicUrl = 'https://github.com/hyronguyen/hyrolauncher/releases/tag/' + $taskTag
 $taskReport = [ordered]@{Url=$taskPublicUrl; Assets=$taskPublicAssets}
 $taskReportPath = Join-Path $taskArtifacts 'github-release.json'
 if ((Test-Path -LiteralPath $taskReportPath) -and ((Get-Item -LiteralPath $taskReportPath).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Release report cannot be a link.' }
 [IO.File]::WriteAllText($taskReportPath, ($taskReport | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding($false)))
 $taskReport | ConvertTo-Json -Depth 4
} catch { throw $taskSafeFailure }
finally {
 foreach ($taskOpenStream in $taskStreams) { $taskOpenStream.Dispose() }
 if ($taskClient) { $taskClient.Dispose() }
 $taskToken = $null
}
