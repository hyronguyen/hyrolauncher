param([string]$CandidatePath="")
. (Join-Path $PSScriptRoot 'test-runtime.ps1')

$ErrorActionPreference='Stop'
# Read the running version under its native mutex, then migrate only an isolated clone.
# Never serialize the decrypted ledger or its key into verification output.
$taskFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$taskProductionRoot=Join-Path $ProjectRoot 'dist'
$taskCloneRoot=Join-Path $ProjectRoot ('output/verification/hytcg-production-migration-'+[Guid]::NewGuid().ToString('N'))
$taskNewAssembly=[Reflection.Assembly]::LoadFrom($(if($CandidatePath){[IO.Path]::GetFullPath($CandidatePath)}else{Join-Path $taskProductionRoot 'HYROLauncher.check.exe'}))
$taskStorageType=$taskNewAssembly.GetType('HYROLauncherWeb.HyTcgStorage',$true)
$taskImageDecoder=$taskStorageType.GetMethod('ImageBytes',[Reflection.BindingFlags]'Static,NonPublic')
function Get-PlainCardHash([string]$CardPath) {
 $taskImageArgs=New-Object object[] 1
 $taskImageArgs[0]=[IO.File]::ReadAllBytes($CardPath)
 $taskImageBytes=$taskImageDecoder.Invoke($null,$taskImageArgs)
 $taskImageHasher=[Security.Cryptography.SHA256]::Create()
 try {return [BitConverter]::ToString($taskImageHasher.ComputeHash($taskImageBytes)).Replace('-','')}finally{$taskImageHasher.Dispose()}
}
$taskOldAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $taskProductionRoot 'HYROLauncher.exe'))
$taskOldType=$taskOldAssembly.GetType('HYROLauncherWeb.HyTcgEconomy',$true)
$taskOldCtor=$taskOldType.GetConstructors($taskFlags) | Where-Object {$_.GetParameters().Count -eq 1}
$taskOld=$taskOldCtor.Invoke(@([string]$taskProductionRoot))
$taskGate=$taskOldType.GetField('gate',$taskFlags).GetValue($taskOld)
$taskLease=$null
$taskOriginalHashes=@{}
$taskOriginalPlainHashes=@{}
$taskOriginalModels=@{}
$taskOriginalArtworkHashes=@{}
$taskCreativeBefore=$null
[int]$taskCreativeVersionBefore=0
[Threading.Monitor]::Enter($taskGate)
try {
 $taskLease=$taskOldType.GetMethod('EnterMutex',$taskFlags).Invoke($taskOld,@())
 $taskBefore=$taskOldType.GetMethod('Load',$taskFlags).Invoke($taskOld,@())
 if($taskBefore.Creative){$taskCreativeVersionBefore=[int]$taskBefore.Creative.StudioVersion}
 $taskProductionLedgerHashes=@{}
 foreach($taskLedgerName in @('hytcg-vault.dat','hytcg-vault.stamp')) {$taskProductionLedgerHashes[$taskLedgerName]=(Get-FileHash -LiteralPath (Join-Path $taskProductionRoot $taskLedgerName) -Algorithm SHA256).Hash}
 $taskCreativeMethodBefore=$taskOldType.GetMethod('CreativeSnapshot',$taskFlags)
 if($taskCreativeMethodBefore){$taskCreativeBefore=$taskCreativeMethodBefore.Invoke($taskOld,@())}
 $taskOwnedBefore=@($taskOldType.GetMethod('CollectionCards',$taskFlags).Invoke($taskOld,@()))
 foreach($taskItem in $taskOwnedBefore) {
  $taskOriginalPlainHashes[$taskItem.Card.CardId]=Get-PlainCardHash $taskItem.ImagePath
  $taskOriginalModels[$taskItem.Card.CardId]=$taskOldType.GetMethod('Fingerprint',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@($taskItem.Card))
 }
 New-Item -ItemType Directory -Path (Join-Path $taskCloneRoot 'cards') -Force | Out-Null
 foreach($taskLedgerName in @('hytcg-vault.dat','hytcg-vault.stamp')) {
  Copy-Item -LiteralPath (Join-Path $taskProductionRoot $taskLedgerName) -Destination (Join-Path $taskCloneRoot $taskLedgerName)
 }
 foreach($taskFile in (Get-ChildItem -LiteralPath (Join-Path $taskProductionRoot 'cards') -File | Where-Object {$_.Extension -in @('.png','.hycard')})) {
  if(($taskFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Unexpected linked card file'}
  $taskOriginalHashes[$taskFile.Name]=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
  Copy-Item -LiteralPath $taskFile.FullName -Destination (Join-Path $taskCloneRoot 'cards')
 }
 $taskArtworkSource=Join-Path $taskProductionRoot 'hytcg-artworks'
 if(Test-Path -LiteralPath $taskArtworkSource) {
  if(((Get-Item -LiteralPath $taskArtworkSource).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Unexpected linked artwork directory'}
  New-Item -ItemType Directory -Path (Join-Path $taskCloneRoot 'hytcg-artworks') -Force | Out-Null
  foreach($taskArtworkFile in (Get-ChildItem -LiteralPath $taskArtworkSource -File -Filter '*.dat')) {
   if(($taskArtworkFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Unexpected linked artwork file'}
   $taskOriginalArtworkHashes[$taskArtworkFile.Name]=(Get-FileHash -LiteralPath $taskArtworkFile.FullName -Algorithm SHA256).Hash
   Copy-Item -LiteralPath $taskArtworkFile.FullName -Destination (Join-Path $taskCloneRoot 'hytcg-artworks')
  }
 }
 foreach($taskLedgerName in $taskProductionLedgerHashes.Keys) {
  if((Get-FileHash -LiteralPath (Join-Path $taskProductionRoot $taskLedgerName) -Algorithm SHA256).Hash -ne $taskProductionLedgerHashes[$taskLedgerName]){throw 'Read-only production snapshot changed the ledger'}
 }
} finally {
 if($taskLease){$taskLease.Dispose()}
 [Threading.Monitor]::Exit($taskGate)
}
$taskNewType=$taskNewAssembly.GetType('HYROLauncherWeb.HyTcgEconomy',$true)
$taskNewCtor=$taskNewType.GetConstructors($taskFlags) | Where-Object {$_.GetParameters().Count -eq 1}
$taskNew=$taskNewCtor.Invoke(@([string]$taskCloneRoot))
$taskEncryptedCount=$taskNewType.GetMethod('MigrateEncryptedStorage',$taskFlags).Invoke($taskNew,@())
$taskWallet=$taskNewType.GetMethod('Snapshot',$taskFlags).Invoke($taskNew,@())
$taskOwnedAfter=@($taskNewType.GetMethod('CollectionCards',$taskFlags).Invoke($taskNew,@()))
$taskBeforeIds=@($taskOwnedBefore | ForEach-Object {$_.Card.CardId} | Sort-Object)
$taskAfterIds=@($taskOwnedAfter | ForEach-Object {$_.Card.CardId} | Sort-Object)
if($taskOwnedAfter.Count -ne $taskOwnedBefore.Count -or (($taskBeforeIds -join ',') -ne ($taskAfterIds -join ','))){throw 'Migration changed card ownership'}
if($taskWallet.Gems -ne $taskBefore.Gems -or $taskWallet.PaidCredits -ne $taskBefore.PaidCredits){throw 'Migration changed gems or paid credits'}
if($taskWallet.LimitedCredits -ne [int]$taskBefore.LimitedCredits){throw 'Migration changed limited credits'}
if($taskWallet.PitySR -ne [int]$taskBefore.PitySR -or $taskWallet.PitySSR -ne [int]$taskBefore.PitySSR -or $taskWallet.PityUR -ne [int]$taskBefore.PityUR){throw 'Migration changed pity counters'}
$taskExpectedFlags=@{}
foreach($taskOwnedItem in $taskOwnedBefore){$taskExpectedFlags[$taskOwnedItem.Card.CardId]=@{Favorite=[bool]$taskOwnedItem.Favorite;UpgradeLocked=[bool]$taskOwnedItem.UpgradeLocked;FileName=[string]$taskOwnedItem.FileName}}
foreach($taskOwnedItem in $taskOwnedAfter) {
 $taskExpected=$taskExpectedFlags[$taskOwnedItem.Card.CardId]
 $taskExpectedName=[IO.Path]::ChangeExtension($taskExpected.FileName,'.hycard')
 if($taskOwnedItem.Favorite -ne $taskExpected.Favorite -or $taskOwnedItem.UpgradeLocked -ne $taskExpected.UpgradeLocked -or $taskOwnedItem.FileName -ne $taskExpectedName){throw 'Migration changed card flags or file ownership'}
 if((Get-PlainCardHash $taskOwnedItem.ImagePath) -ne $taskOriginalPlainHashes[$taskOwnedItem.Card.CardId]){throw 'Migration changed rendered card pixels or embedded metadata'}
 if($taskNewType.GetMethod('Fingerprint',[Reflection.BindingFlags]'Static,NonPublic').Invoke($null,@($taskOwnedItem.Card)) -ne $taskOriginalModels[$taskOwnedItem.Card.CardId]){throw 'Migration changed card metadata'}
 if($taskExpected.FileName.EndsWith('.png') -and (Test-Path -LiteralPath (Join-Path (Join-Path $taskCloneRoot 'cards') $taskExpected.FileName))){throw 'Authenticated plaintext export was not removed after durable migration'}
}
foreach($taskFileName in $taskOriginalHashes.Keys) {
 $taskRetained=Join-Path (Join-Path $taskCloneRoot 'cards') $taskFileName
 if((Test-Path -LiteralPath $taskRetained) -and (Get-FileHash -LiteralPath $taskRetained -Algorithm SHA256).Hash -ne $taskOriginalHashes[$taskFileName]){throw 'Migration modified a retained source or encrypted card'}
}
$taskNewType.GetMethod('CollectionCards',$taskFlags).Invoke($taskNew,@()) | Out-Null
$taskCloneReadHashes=@{}
foreach($taskLedgerName in @('hytcg-vault.dat','hytcg-vault.stamp')) {$taskCloneReadHashes[$taskLedgerName]=(Get-FileHash -LiteralPath (Join-Path $taskCloneRoot $taskLedgerName) -Algorithm SHA256).Hash}
$taskCreativeAfter=$taskNewType.GetMethod('CreativeSnapshot',$taskFlags).Invoke($taskNew,@())
$taskAlbumsPreserved=0
if($taskCreativeBefore){foreach($taskAlbumBefore in @($taskCreativeBefore.Albums)) {
 $taskAlbumAfter=@($taskCreativeAfter.Albums | Where-Object {$_.Id -ceq $taskAlbumBefore.Id})
 if($taskAlbumAfter.Count -ne 1){throw 'Album migration changed album ownership'}
 $taskAlbumAfter=$taskAlbumAfter[0]
 if($taskAlbumAfter.Name -cne $taskAlbumBefore.Name -or $taskAlbumAfter.Background -cne $taskAlbumBefore.Background -or $taskAlbumAfter.Capacity -lt $taskAlbumBefore.CardIds.Count){throw 'Album migration changed design or lost slots'}
 for($taskSlot=0;$taskSlot -lt $taskAlbumBefore.CardIds.Count;$taskSlot++){if($taskAlbumAfter.CardIds[$taskSlot] -cne $taskAlbumBefore.CardIds[$taskSlot]){throw 'Album migration changed card arrangement'}}
 $taskAlbumsPreserved++
}}
$taskOldLooks=@{}
if($taskCreativeBefore){foreach($taskLook in @($taskCreativeBefore.Cosmetics)){$taskOldLooks[[string]$taskLook.CardId]=$taskLook}}
$taskActiveLooks=0
$taskComposedPreviews=0
Add-Type -AssemblyName System.Drawing
foreach($taskOwnedItem in $taskOwnedAfter) {
 $taskCardId=[string]$taskOwnedItem.Card.CardId
 $taskLook=$taskNewType.GetMethod('GetCardCosmetic',$taskFlags).Invoke($taskNew,@($taskCardId))
 $taskOldLook=$taskOldLooks[$taskCardId]
 if([bool]$taskLook -ne [bool]$taskOldLook){throw 'Creative read changed whether a card has an active look'}
 if($taskOldLook) {
  $taskActiveLooks++
  foreach($taskLookField in @('Dye','Morph','MorphMode','ArtworkId','DyeId','Mystic')) {
   if([string]$taskLook.$taskLookField -cne [string]$taskOldLook.$taskLookField){throw 'Creative migration changed an applied look field'}
  }
  if($taskLook.Frame -ne 'classic' -or $taskLook.FrameItemId){throw 'Retired frame leaked into a current rarity appearance'}
 }
 $taskPreview=$taskNewType.GetMethod('ReadLifestyleImage',$taskFlags).Invoke($taskNew,@($taskCardId,$true))
 if(!$taskPreview.StartsWith('data:image/png;base64,',[StringComparison]::Ordinal)){throw 'Creative preview returned an unexpected image format'}
 $taskPreviewBytes=[Convert]::FromBase64String($taskPreview.Substring($taskPreview.IndexOf(',')+1))
 $taskPreviewStream=New-Object IO.MemoryStream (,$taskPreviewBytes)
 try {
  $taskPreviewImage=[Drawing.Image]::FromStream($taskPreviewStream,$true,$true)
  try {if($taskPreviewImage.Width -lt 1 -or $taskPreviewImage.Height -lt 1 -or $taskPreviewImage.Width -gt 420 -or $taskPreviewImage.Height -gt 420){throw 'Creative preview dimensions exceeded the thumbnail bound'}}finally{$taskPreviewImage.Dispose()}
 }finally{$taskPreviewStream.Dispose()}
 $taskComposedPreviews++
}
if($taskCreativeBefore) {
 $taskDyesBefore=@($taskCreativeBefore.Dyes | ForEach-Object {([string]$_.Id)+':'+([int]$_.Count)} | Sort-Object)
 $taskDyesAfter=@($taskCreativeAfter.Dyes | ForEach-Object {([string]$_.Id)+':'+([int]$_.Count)} | Sort-Object)
 if(($taskDyesBefore -join ',') -cne ($taskDyesAfter -join ',')){throw 'Creative migration changed dye inventory'}
 if($taskCreativeAfter.FrameMode -ne 'rarity' -or @($taskCreativeAfter.FrameItems).Count -ne 0 -or @($taskCreativeAfter.FrameCatalog).Count -ne 0){throw 'Retired frames are still exposed in Studio'}
 $taskRawAfter=$taskNewType.GetMethod('Load',$taskFlags).Invoke($taskNew,@())
 foreach($taskPreservedField in @('Cosmetics','ResetCosmetics','FrameItems','StudioReceipts')) {
  $taskBeforeRecords=@($taskBefore.Creative.$taskPreservedField)
  $taskAfterRecords=@($taskRawAfter.Creative.$taskPreservedField)
  if($taskBeforeRecords.Count -ne $taskAfterRecords.Count){throw 'Read-only display migration changed protected record counts'}
  $taskProjectedRecords=@()
  for($taskRecord=0;$taskRecord -lt $taskBeforeRecords.Count;$taskRecord++) {
   $taskProperties=@($taskBeforeRecords[$taskRecord].PSObject.Properties | ForEach-Object {$_.Name})
   $taskProjectedRecords+=($taskAfterRecords[$taskRecord] | Select-Object -Property $taskProperties)
   foreach($taskPositionField in @('ArtworkScale','ArtworkX','ArtworkY')) {
    if($taskPreservedField -in @('Cosmetics','ResetCosmetics') -and $taskPositionField -notin $taskProperties -and $taskAfterRecords[$taskRecord].$taskPositionField -ne 0){throw 'Missing legacy artwork position acquired a non-default value'}
   }
  }
  $taskRawBeforeJson=ConvertTo-Json -InputObject $taskBeforeRecords -Depth 24 -Compress
  $taskRawAfterJson=ConvertTo-Json -InputObject $taskProjectedRecords -Depth 24 -Compress
  if($taskRawBeforeJson -cne $taskRawAfterJson){throw 'Read-only display migration changed historical protected creative records'}
 }
}
foreach($taskArtworkName in $taskOriginalArtworkHashes.Keys) {
 $taskArtworkClone=Join-Path (Join-Path $taskCloneRoot 'hytcg-artworks') $taskArtworkName
 if((Get-FileHash -LiteralPath $taskArtworkClone -Algorithm SHA256).Hash -ne $taskOriginalArtworkHashes[$taskArtworkName]){throw 'Creative reads changed protected artwork bytes'}
}
foreach($taskLedgerName in $taskCloneReadHashes.Keys) {
 if((Get-FileHash -LiteralPath (Join-Path $taskCloneRoot $taskLedgerName) -Algorithm SHA256).Hash -ne $taskCloneReadHashes[$taskLedgerName]){throw 'Creative snapshot or preview wrote the cloned ledger'}
}
$taskReloaded=$taskNewCtor.Invoke(@([string]$taskCloneRoot))
if(@($taskNewType.GetMethod('CollectionCards',$taskFlags).Invoke($taskReloaded,@())).Count -ne $taskOwnedAfter.Count){throw 'Migrated ledger did not survive reload'}
$taskReport=[ordered]@{Result='PASS';OwnedCards=$taskOwnedAfter.Count;ExportFiles=$taskOriginalHashes.Count;EncryptedCards=[int]$taskEncryptedCount;Gems=$taskWallet.Gems;PaidCredits=$taskWallet.PaidCredits;PngHashesPreserved=$true;MetadataPreserved=$true;ProtectedArtworks=$taskOriginalArtworkHashes.Count;ActiveLooksPreserved=$taskActiveLooks;AlbumsPreserved=$taskAlbumsPreserved;CreativePreviews=$taskComposedPreviews;DyeInventoryPreserved=$true;FrameInventoryMigrationVerified=$true;CreativeReadsWritten=$false;ProductionWritten=$false;CloneRoot=$taskCloneRoot}
$taskReport | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskCloneRoot 'verification.json') -Encoding UTF8
$taskReport | ConvertTo-Json -Compress
