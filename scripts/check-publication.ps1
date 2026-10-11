param([string]$RootPath = '', [string]$RepositoryRoot = '')
$ErrorActionPreference = 'Stop'
if (!$RepositoryRoot) { $RepositoryRoot = Split-Path -Parent $PSScriptRoot }
$taskRepo = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
$taskCredentials = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($taskPrivateDirectory in @((Join-Path $taskRepo 'assets/apikey'), (Join-Path $taskRepo 'dist/assets/apikey'))) {
 if (!(Test-Path -LiteralPath $taskPrivateDirectory)) { continue }
 foreach ($taskPrivateFile in Get-ChildItem -LiteralPath $taskPrivateDirectory -File -Filter '*.txt') {
  $taskPrivateValue = [IO.File]::ReadAllText($taskPrivateFile.FullName).Trim()
  if ($taskPrivateValue.Length -ge 8 -and $taskPrivateValue -notmatch '^(YOUR_|REPLACE_|<)') { [void]$taskCredentials.Add($taskPrivateValue) }
 }
}
$taskFindings = New-Object 'System.Collections.Generic.List[object]'
$taskInputs = New-Object 'System.Collections.Generic.List[object]'
function Add-Finding([string]$File, [string]$Category) { $taskFindings.Add([ordered]@{File=$File; Category=$Category}) }
function Read-GitBytes([string[]]$Arguments) {
 $taskGitExe = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
 $taskInfo = New-Object Diagnostics.ProcessStartInfo
 $taskInfo.FileName = $taskGitExe
 $taskQuoted = foreach ($taskArgument in $Arguments) { '"' + [regex]::Replace([regex]::Replace($taskArgument, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"' }
 $taskInfo.Arguments = $taskQuoted -join ' '
 $taskInfo.UseShellExecute = $false
 $taskInfo.CreateNoWindow = $true
 $taskInfo.RedirectStandardOutput = $true
 $taskInfo.RedirectStandardError = $true
 $taskProcess = New-Object Diagnostics.Process
 $taskProcess.StartInfo = $taskInfo
 $taskBytesStream = New-Object IO.MemoryStream
 try {
  [void]$taskProcess.Start()
  $taskErrorRead = $taskProcess.StandardError.ReadToEndAsync()
  $taskProcess.StandardOutput.BaseStream.CopyTo($taskBytesStream)
  $taskProcess.WaitForExit()
  [void]$taskErrorRead.GetAwaiter().GetResult()
  if ($taskProcess.ExitCode -ne 0) { throw 'Cannot inspect the Git staging index.' }
  return ,$taskBytesStream.ToArray()
 } finally { $taskBytesStream.Dispose(); $taskProcess.Dispose() }
}
if ($RootPath) {
 $taskReleaseRoot = [IO.Path]::GetFullPath($RootPath).TrimEnd('\')
 if (!(Test-Path -LiteralPath $taskReleaseRoot -PathType Container)) { throw 'Release scan directory does not exist.' }
 if ((Get-Item -LiteralPath $taskReleaseRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Release scan directory cannot be a link.' }
 $taskDirectories = New-Object 'System.Collections.Generic.Stack[string]'
 $taskDirectories.Push($taskReleaseRoot)
 while ($taskDirectories.Count) {
  foreach ($taskEntry in Get-ChildItem -LiteralPath $taskDirectories.Pop() -Force) {
   $taskRelative = $taskEntry.FullName.Substring($taskReleaseRoot.Length + 1).Replace('\', '/')
   if (($taskEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Add-Finding $taskRelative 'LinkedPath'; continue }
   if ($taskEntry.PSIsContainer) { $taskDirectories.Push($taskEntry.FullName); continue }
   $taskInputs.Add([pscustomobject]@{Relative=$taskRelative; FullName=$taskEntry.FullName; Blob=''; Length=$taskEntry.Length})
  }
 }
} else {
 $taskIndexBytes = Read-GitBytes @('-C', $taskRepo, 'ls-files', '--stage', '-z')
 $taskIndex = [Text.Encoding]::UTF8.GetString($taskIndexBytes)
 foreach ($taskIndexRow in $taskIndex.Split([char]0)) {
  if (!$taskIndexRow) { continue }
  if ($taskIndexRow -notmatch '^(\d+) ([0-9a-f]{40,64}) (\d)\t(.+)$') { throw 'Cannot parse the Git staging index.' }
  $taskMode=$Matches[1]; $taskBlob=$Matches[2]; $taskStage=$Matches[3]; $taskRelative=$Matches[4]
  if ($taskStage -ne '0') { Add-Finding $taskRelative 'UnmergedIndexEntry'; continue }
  if ($taskMode -notin @('100644','100755')) { Add-Finding $taskRelative 'UnsupportedGitEntry'; continue }
  $taskInputs.Add([pscustomobject]@{Relative=$taskRelative; FullName=''; Blob=$taskBlob; Length=0})
 }
}
$taskScanned = 0
$taskAllowedConfig = @('aniClient.txt','aniSecret.txt','steamapikey.txt','twitchclient.txt','twitchsecret.txt','zerochanuser.txt','hytcgconfig.json')
foreach ($taskInput in $taskInputs) {
 $taskRelative = $taskInput.Relative.Replace('\', '/')
 if ($taskRelative -match '(^|/)(dist|output|releases?|\.deps|\.tools|node_modules|\.git|\.cache|cache|cards|hytcg-artworks|hytcg-previews|logs|igdb-cache|explore-cache|background-cache|\.sentry-native)(/|$)' -or $taskRelative -match '(^|/)assets/(library|music|user-pets)(/|$)' -or $taskRelative -match '(^|/)(launcher-data[^/]*\.json|hytcg-vault\.[^/]+|steam-library-cache\.json)$' -or $taskRelative -match '\.(hycard|hyart|hythumb|hylook|pfx|p12|pem|bak|pdb)$' -or $taskRelative -match '(^|/)\.env($|\.(?!example$))') { Add-Finding $taskRelative 'LocalOrSensitiveFile' }
 if (!$RootPath -and $taskRelative -match '(^|/)assets/apikey(/|$)') { Add-Finding $taskRelative 'PrivateConfigurationPath' }
 if ($taskInput.FullName -and $taskInput.Length -gt 120MB) { Add-Finding $taskRelative 'UnexpectedLargeFile'; continue }
 [byte[]]$taskBytes = @()
 if ($taskInput.Blob) { $taskBytes = Read-GitBytes @('-C',$taskRepo,'cat-file','blob',$taskInput.Blob) }
 else { $taskBytes = [IO.File]::ReadAllBytes($taskInput.FullName) }
 $taskText = [Text.Encoding]::UTF8.GetString($taskBytes)
 $taskWide = [Text.Encoding]::Unicode.GetString($taskBytes)
 foreach ($taskCredential in $taskCredentials) {
  if ($taskText.IndexOf($taskCredential, [StringComparison]::Ordinal) -ge 0 -or $taskWide.IndexOf($taskCredential, [StringComparison]::Ordinal) -ge 0) { Add-Finding $taskRelative 'KnownLocalCredential'; break }
 }
 if ($taskText -match '(gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}|AKIA[0-9A-Z]{16}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----)') { Add-Finding $taskRelative 'CredentialTokenOrPrivateKey' }
 if ([IO.Path]::GetExtension($taskRelative) -match '^\.(cs|js|cjs|mjs|py|ps1|md|txt|json|yml|yaml|html|css|xml|iss|cmd)$' -and $taskText -match '(?i)C:[/\\]Users[/\\][a-z0-9_.-]+[/\\]') { Add-Finding $taskRelative 'PersonalMachinePath' }
 if ($taskRelative -match '(^|/)(assets/apikey|config/examples)/([^/]+)$') {
  $taskConfigName = $Matches[3]
  if ($taskAllowedConfig -cnotcontains $taskConfigName) { Add-Finding $taskRelative 'UnexpectedConfigFile' }
  elseif ($taskConfigName -ne 'hytcgconfig.json' -and $taskText.Trim([char]0xFEFF).Trim()) { Add-Finding $taskRelative 'NonEmptyCredentialTemplate' }
  elseif ($taskConfigName -eq 'hytcgconfig.json') {
   try {
    $taskConfig = $taskText | ConvertFrom-Json
    $taskExpected = @('CharacterIdMin','CharacterIdMax','MaxCharacterAttempts','Concurrency','RequestTimeoutSeconds','RetryDelayMs')
    if (@($taskConfig.PSObject.Properties.Name | Where-Object { $_ -notin $taskExpected }).Count -or $taskConfig.CharacterIdMin -lt 1 -or $taskConfig.CharacterIdMax -gt 1000000 -or $taskConfig.CharacterIdMax -lt $taskConfig.CharacterIdMin -or $taskConfig.MaxCharacterAttempts -lt 1 -or $taskConfig.MaxCharacterAttempts -gt 20 -or $taskConfig.Concurrency -lt 1 -or $taskConfig.Concurrency -gt 3 -or $taskConfig.RequestTimeoutSeconds -lt 3 -or $taskConfig.RequestTimeoutSeconds -gt 20 -or $taskConfig.RetryDelayMs -lt 100 -or $taskConfig.RetryDelayMs -gt 3000) { Add-Finding $taskRelative 'InvalidPublicTcgConfiguration' }
   } catch { Add-Finding $taskRelative 'InvalidPublicTcgConfiguration' }
  }
 }
 $taskScanned++
}
if (!$taskInputs.Count) { Add-Finding '(scan)' 'NoFilesToInspect' }
[ordered]@{Passed=($taskFindings.Count -eq 0); ScannedFiles=$taskScanned; Findings=$taskFindings.ToArray()} | ConvertTo-Json -Depth 5
if ($taskFindings.Count) { exit 2 }
