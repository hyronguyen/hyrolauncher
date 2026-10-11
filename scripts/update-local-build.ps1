param([string]$CandidatePath="")
$ErrorActionPreference='Stop'
$taskProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskDist=Join-Path $taskProject 'dist'
$taskExe=Join-Path $taskDist 'HYROLauncher.exe'
$taskCandidate=Join-Path $taskDist 'HYROLauncher.Check.exe'
if($CandidatePath){$taskCandidate=[IO.Path]::GetFullPath($CandidatePath)}
$taskCandidateUi=Join-Path (Split-Path -Parent $taskCandidate) 'ui'
if(!(Test-Path -LiteralPath $taskCandidateUi)){throw 'Verified candidate UI is missing'}
$taskQa=Join-Path (Join-Path $taskProject 'output/verification') ('hytcg-atelier-deployment-'+[Guid]::NewGuid().ToString('N'))
if(!(Test-Path -LiteralPath $taskCandidate)){throw 'Verified candidate is missing'}
Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;using System.Text;
public static class AtelierDeploymentWindow {
 public delegate bool EnumCallback(IntPtr window,IntPtr param);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumCallback callback,IntPtr param);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wparam,IntPtr lparam,uint flags,uint timeout,out IntPtr result);
}
'@
function Close-TaskLauncher {
 foreach($taskProcess in @(Get-Process HYROLauncher -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $taskExe})) {
  $taskLauncherHandles=[Collections.Generic.List[IntPtr]]::new()
  $taskWantedPid=[uint32]$taskProcess.Id
  $taskCallback=[AtelierDeploymentWindow+EnumCallback]{param($window,$param)
   $taskWindowPid=[uint32]0
   $null=[AtelierDeploymentWindow]::GetWindowThreadProcessId($window,[ref]$taskWindowPid)
   if($taskWindowPid -eq $taskWantedPid){$taskTitle=[Text.StringBuilder]::new(260);$null=[AtelierDeploymentWindow]::GetWindowText($window,$taskTitle,260);if($taskTitle.ToString() -eq 'HYROLauncher'){$taskLauncherHandles.Add($window)}}
   return $true
  }
  $null=[AtelierDeploymentWindow]::EnumWindows($taskCallback,[IntPtr]::Zero)
  if($taskLauncherHandles.Count -ne 1){throw 'Launcher has no unique shutdown window; no process was forced closed'}
  $taskLauncherHandle=$taskLauncherHandles[0]
  $taskMessageResult=[IntPtr]::Zero
  # The launcher's documented WndProc sets exitRequested on this notification.
  # This targets only its window; it does not request an OS shutdown.
  if([AtelierDeploymentWindow]::SendMessageTimeout($taskLauncherHandle,0x11,[IntPtr]::Zero,[IntPtr]::Zero,2,5000,[ref]$taskMessageResult) -eq [IntPtr]::Zero){throw 'Launcher shutdown notification failed'}
  if([AtelierDeploymentWindow]::SendMessageTimeout($taskLauncherHandle,0x10,[IntPtr]::Zero,[IntPtr]::Zero,2,5000,[ref]$taskMessageResult) -eq [IntPtr]::Zero -and !$taskProcess.HasExited){throw 'Launcher close notification failed'}
  if(!$taskProcess.WaitForExit(8000)){throw 'Launcher did not finish its graceful shutdown'}
 }
}
Close-TaskLauncher
New-Item -ItemType Directory -Path (Join-Path $taskQa 'cards') -Force | Out-Null
Copy-Item -LiteralPath $taskExe -Destination (Join-Path $taskQa 'HYROLauncher.before.exe')
Copy-Item -LiteralPath (Join-Path $taskDist 'ui') -Destination $taskQa -Recurse
foreach($taskName in @('hytcg-vault.dat','hytcg-vault.stamp')) {
 $taskSource=Join-Path $taskDist $taskName
 if(Test-Path -LiteralPath $taskSource){Copy-Item -LiteralPath $taskSource -Destination (Join-Path $taskQa $taskName)}
}
foreach($taskFile in @(Get-ChildItem -LiteralPath (Join-Path $taskDist 'cards') -File | Where-Object {$_.Extension -in @('.hycard','.png')})) {
 if($taskFile.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked production card file'}
 Copy-Item -LiteralPath $taskFile.FullName -Destination (Join-Path $taskQa 'cards')
}
if(Test-Path -LiteralPath (Join-Path $taskDist 'hytcg-artworks')) {
 Copy-Item -LiteralPath (Join-Path $taskDist 'hytcg-artworks') -Destination $taskQa -Recurse
}
try {
 Copy-Item -LiteralPath $taskCandidate -Destination $taskExe -Force
 Copy-Item -Path (Join-Path $taskCandidateUi '*') -Destination (Join-Path $taskDist 'ui') -Recurse -Force
 if((Get-FileHash -LiteralPath $taskExe).Hash -ne (Get-FileHash -LiteralPath $taskCandidate).Hash){throw 'Candidate copy differs'}
 $taskStarted=Start-Process -FilePath $taskExe -WorkingDirectory $taskDist -WindowStyle Hidden -PassThru
 Start-Sleep -Seconds 3
 $taskStarted.Refresh()
 if($taskStarted.HasExited){throw 'Updated launcher exited during startup'}
 & (Join-Path $taskProject 'tests/verify-hytcg-deployed.ps1') -BeforeClone $taskQa
 if(!$?){throw 'Deployed verification failed'}
 [ordered]@{Result='PASS';CloneRoot=$taskQa;CandidateSha256=(Get-FileHash -LiteralPath $taskExe).Hash;ProcessId=$taskStarted.Id;GracefulShutdown=$true;ProductionCardsCopiedFromFixtures=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskQa 'deployment.json') -Encoding UTF8
 Write-Output ('Deployment report: '+(Join-Path $taskQa 'deployment.json'))
} catch {
 $taskFailure=$_
 Close-TaskLauncher
 Copy-Item -LiteralPath (Join-Path $taskQa 'HYROLauncher.before.exe') -Destination $taskExe -Force
 Copy-Item -Path (Join-Path (Join-Path $taskQa 'ui') '*') -Destination (Join-Path $taskDist 'ui') -Recurse -Force
 Start-Process -FilePath $taskExe -WorkingDirectory $taskDist -WindowStyle Hidden | Out-Null
 throw $taskFailure
}
