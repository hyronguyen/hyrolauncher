
. (Join-Path $PSScriptRoot 'test-runtime.ps1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing,System.Web.Extensions
[AppDomain]::CurrentDomain.SetData('APPBASE',(Join-Path $ProjectRoot 'dist'))
$taskAssembly=[Reflection.Assembly]::LoadFrom((Get-TestExecutable))
$taskFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$taskServiceType=$taskAssembly.GetType('HYROLauncherWeb.HyTcgService',$true)
$taskCardType=$taskAssembly.GetType('HYROLauncherWeb.HyTcgCard',$true)
$taskRoot=Join-Path $ProjectRoot ('output/verification/hytcg-integration-'+[Guid]::NewGuid().ToString('N'))
$taskCtor=$taskServiceType.GetConstructors($taskFlags) | Where-Object {$_.GetParameters().Count -eq 1}
$taskCtorArgs=New-Object object[] 1
$taskCtorArgs[0]=[string]$taskRoot
$taskService=$taskCtor.Invoke($taskCtorArgs)
$taskCard=[Activator]::CreateInstance($taskCardType)
$taskCard.CharacterId=55
$taskCard.Name='Hinata Hyuuga'
$taskCard.Series='Naruto'
$taskCard.Gender='Female'
$taskCard.Rarity='Common'
$taskCard.RarityColor='#b1becd'
$taskCard.ImageSource='zerochan'
$taskCard.CardId='HT-20261008-000000000000'
$taskCard.CreatedAt=[DateTime]::UtcNow.ToString('o')
$taskSourcePng=Join-Path $taskRoot 'fixture.png'
[IO.Directory]::CreateDirectory($taskRoot) | Out-Null
$taskBitmap=New-Object Drawing.Bitmap(750,1050)
$taskGraphics=[Drawing.Graphics]::FromImage($taskBitmap)
try{$taskGraphics.Clear([Drawing.Color]::MidnightBlue);$taskGraphics.FillRectangle([Drawing.Brushes]::Turquoise,70,70,610,220);$taskBitmap.Save($taskSourcePng,[Drawing.Imaging.ImageFormat]::Png)}finally{$taskGraphics.Dispose();$taskBitmap.Dispose()}
$taskPng='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($taskSourcePng))
$taskEconomy=$taskServiceType.GetProperty('Economy',$taskFlags).GetValue($taskService,$null)
$taskEconomyType=$taskEconomy.GetType()
$taskReserve=$taskEconomyType.GetMethods($taskFlags) | Where-Object {$_.Name -eq 'Reserve' -and $_.GetParameters().Count -eq 1}
$taskCollection=$taskServiceType.GetMethods($taskFlags) | Where-Object {$_.Name -eq 'Collection' -and $_.GetParameters().Count -eq 3}
$taskReservation=$taskReserve.Invoke($taskEconomy,@(1))
$taskToken=[string]$taskReservation.Tokens[0]
$null=$taskEconomyType.GetMethod('Begin',$taskFlags).Invoke($taskEconomy,@($taskToken))
$taskAuthorizeArgs=New-Object object[] 2
$taskAuthorizeArgs[0]=$taskToken
$taskAuthorizeArgs[1]=$taskCard.PSObject.BaseObject
$null=$taskEconomyType.GetMethod('AuthorizeCard',$taskFlags).Invoke($taskEconomy,$taskAuthorizeArgs)
$taskSaveArgs=New-Object object[] 3
$taskSaveArgs[0]=$taskCard.PSObject.BaseObject
$taskSaveArgs[1]=[string]$taskPng
$taskSaveArgs[2]=$taskToken
$taskSaved=$taskServiceType.GetMethod('SaveCard',$taskFlags).Invoke($taskService,$taskSaveArgs)
$taskPage=$taskCollection.Invoke($taskService,@(0,'',''))
if($taskPage.Total -ne 1 -or $taskPage.Items[0].Card.CardId -ne $taskCard.CardId -or $taskPage.Items[0].Card.Name -ne $taskCard.Name){throw 'Canvas PNG/native save/Collection integration failed'}
if(!$taskPage.Items[0].CanDissolve -or $taskPage.Items[0].IsLegacy -or $taskEconomyType.GetMethod('Snapshot',$taskFlags).Invoke($taskEconomy,@()).FreeCredits -ne 4){throw 'Native ownership/wallet integration failed'}
$taskBefore=New-Object Drawing.Bitmap($taskSourcePng)
$taskStoredImage=$taskServiceType.GetMethod('ReadCardImage',$taskFlags).Invoke($taskService,@([string]$taskCard.CardId))
$taskStoredBytes=[Convert]::FromBase64String($taskStoredImage.Substring($taskStoredImage.IndexOf(',')+1))
$taskStoredStream=New-Object IO.MemoryStream(,$taskStoredBytes)
$taskAfter=New-Object Drawing.Bitmap($taskStoredStream)
try {
 foreach($taskPoint in @(@(100,100),@(350,500),@(650,800),@(100,940))){
  if($taskBefore.GetPixel($taskPoint[0],$taskPoint[1]).ToArgb() -ne $taskAfter.GetPixel($taskPoint[0],$taskPoint[1]).ToArgb()){throw 'Native export changed rendered card pixels'}
 }
}finally{$taskBefore.Dispose();$taskAfter.Dispose();$taskStoredStream.Dispose()}
$taskDissolved=$taskEconomyType.GetMethod('Dissolve',$taskFlags).Invoke($taskEconomy,@([string]$taskCard.CardId))
if($taskDissolved.GemsAwarded -lt 1 -or $taskEconomyType.GetMethod('Snapshot',$taskFlags).Invoke($taskEconomy,@()).Gems -ne (1800+$taskDissolved.GemsAwarded)){throw 'Native dissolve wallet integration failed'}
$taskAfterDissolve=$taskCollection.Invoke($taskService,@(0,'',''))
if($taskAfterDissolve.Total -ne 0){throw 'Dissolved tombstone did not hide retained PNG'}
$taskFormType=$taskAssembly.GetType('HYROLauncherWeb.WebLauncherForm',$true)
$taskStateType=$taskAssembly.GetType('HYROLauncherWeb.LauncherState',$true)
$taskOwner=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($taskFormType)
$taskFormType.GetField('hyTcg',$taskFlags).SetValue($taskOwner,$taskService)
$taskNetwork=New-Object Threading.SemaphoreSlim -ArgumentList 3,3
$taskFormType.GetField('hyTcgNetwork',$taskFlags).SetValue($taskOwner,$taskNetwork)
$taskFormType.GetField('hyTcgSessionId',$taskFlags).SetValue($taskOwner,'different-session')
$taskDuplicate=$taskReserve.Invoke($taskEconomy,@(1))
$taskDuplicateToken=[string]$taskDuplicate.Tokens[0]
$null=$taskEconomyType.GetMethod('Begin',$taskFlags).Invoke($taskEconomy,@($taskDuplicateToken))
$taskBalanceBefore=$taskEconomyType.GetMethod('Snapshot',$taskFlags).Invoke($taskEconomy,@()).FreeCredits
$taskOpenArgs=New-Object object[] 5
$taskOpenArgs[0]=$null
$taskOpenArgs[1]='replay-test-session'
$taskOpenArgs[2]='replay-test-request'
$taskOpenArgs[3]=$taskDuplicateToken
$taskOpenArgs[4]=[Threading.CancellationToken]::None
$null=$taskFormType.GetMethod('HyTcgOpen',$taskFlags).Invoke($taskOwner,$taskOpenArgs)
if($taskEconomyType.GetMethod('Snapshot',$taskFlags).Invoke($taskEconomy,@()).FreeCredits -ne $taskBalanceBefore){throw 'Duplicate native token refunded original active request'}
try {
 $null=$taskEconomyType.GetMethod('Begin',$taskFlags).Invoke($taskEconomy,@($taskDuplicateToken))
 throw 'Duplicate native token cleared original active request'
}catch{if(!$_.Exception.ToString().Contains('Busy')){throw}}
$null=$taskEconomyType.GetMethod('Refund',$taskFlags).Invoke($taskEconomy,@($taskDuplicateToken))
$taskParser=New-Object Web.Script.Serialization.JavaScriptSerializer
$taskFormType.GetField('serializer',$taskFlags).SetValue($taskOwner,$taskParser)
$taskState=$taskParser.Deserialize('{"Games":[],"Plugins":[],"Settings":{}}',$taskStateType)
$taskNormalizeArgs=New-Object object[] 1
$taskNormalizeArgs[0]=$taskState.PSObject.BaseObject
$taskNormalized=$taskFormType.GetMethod('Normalize',$taskFlags).Invoke($taskOwner,$taskNormalizeArgs)
$taskPlugin=$taskNormalized.Plugins | Where-Object {$_.Id -eq 'builtin.hytcg'}
if(!$taskPlugin -or $taskPlugin.Type -ne 'hytcg' -or !$taskPlugin.BuiltIn -or !(Test-Path -LiteralPath $taskPlugin.IconPath)){throw 'HYTCG manifest did not register in launcher'}
$taskStaticFlags=[Reflection.BindingFlags]'Static,NonPublic'
$taskReadIds=$taskFormType.GetMethod('HyTcgIds',$taskStaticFlags)
$taskBridgeIds=@(0..99 | ForEach-Object {'HT-20261008-'+$_.ToString('X12')})
foreach($taskLimit in @(1,10,100)) {
 $taskPayload=$taskParser.DeserializeObject((@{cardIds=@($taskBridgeIds | Select-Object -First $taskLimit)} | ConvertTo-Json -Compress))
 $taskIdsArgs=New-Object object[] 3
 $taskIdsArgs[0]=$taskPayload;$taskIdsArgs[1]='cardIds';$taskIdsArgs[2]=$taskLimit
 $taskIdsResult=$taskReadIds.Invoke($null,$taskIdsArgs)
 if($taskIdsResult.Count -ne $taskLimit){throw 'Bridge did not parse a valid JSON card list'}
}
foreach($taskInvalid in @(
 @{cardIds=@($taskBridgeIds[0],$taskBridgeIds[0])},
 @{cardIds=@('../cards/fake.png')},
 @{cardIds=$taskBridgeIds[0]},
 @{cardIds=@($taskBridgeIds | Select-Object -First 11)},
 @{cardIds=@()}
)) {
 $taskInvalidPayload=$taskParser.DeserializeObject(($taskInvalid | ConvertTo-Json -Compress))
 $taskIdsArgs[0]=$taskInvalidPayload;$taskIdsArgs[2]=10
 try {$null=$taskReadIds.Invoke($null,$taskIdsArgs);throw 'Bridge accepted an invalid card list'}
 catch {if(!$_.Exception.ToString().Contains('CardInvalid')){throw}}
}
Write-Output 'PASS: Canvas PNG -> native signed export/ownership -> Collection -> dissolve/gems/tombstone; duplicate token cannot refund an active request; launcher registers HYTCG; new batch/upgrade bridge parses JSON lists and rejects invalid IDs, duplicates and out-of-limit materials.'
