let systemStorageInfo = null, systemStorageTab = 'storage', systemCacheBusy = false, systemCacheResult = null, systemPreviousFocus = null;
function systemText(vi,en) { return igdbText(vi,en); }
function openSystemInfo(refresh = false) {
  const modal=document.getElementById('systemModal'), wasOpen=modal.classList.contains('show');
  if(!wasOpen){systemPreviousFocus=document.activeElement;systemStorageTab='storage';systemCacheResult=null;}
  modal.classList.add('show');renderSystemInfo();
  if(!wasOpen)document.getElementById('systemOkBtn').focus();
  post({type:'getSystemInfo',...(refresh?{refresh:true}:{})});
}
function showSystemInfo(info) {
  systemStorageInfo=info;
  if(document.getElementById('systemModal').classList.contains('show'))renderSystemInfo();
}
function closeSystemInfo() {
  document.getElementById('systemModal').classList.remove('show');
  if(systemPreviousFocus?.isConnected)systemPreviousFocus.focus();
}
function setSystemInfoTab(tab) {
  systemStorageTab=tab==='device'?'device':'storage';renderSystemInfo();
}
function systemInfoItem(label,value,extra='') {
  return `<div class="systemItem ${extra}"><div class="systemLabel">${escapeHtml(label)}</div><div class="systemValue">${escapeHtml(value??'—')}</div></div>`;
}
function systemCleanupStatus() {
  if(systemCacheBusy)return systemText('Đang dọn cache…','Clearing cache…');
  if(!systemCacheResult)return '';
  if(systemCacheResult.error)return systemText('Chưa dọn được cache. Hãy thử lại.','Could not clear cache. Please retry.');
  const kept=Number(systemCacheResult.skippedFiles)||0;
  return systemText('Đã giải phóng ','Freed ')+(systemCacheResult.freedSize||'0 B')+(kept?systemText(` · ${kept} file đang dùng được giữ lại`,` · Kept ${kept} files in use`):'');
}
function renderSystemInfo() {
  const body=document.getElementById('systemGrid'),active=document.activeElement;
  const focusId=body.contains(active)?active.id:'',summaryKey=active?.dataset?.systemSection;
  const open=[...body.querySelectorAll('details[open]')].map(node=>node.dataset.systemSection);
  renderSystemInfoContent();
  for(const key of open) {const section=body.querySelector(`details[data-system-section="${key}"]`);if(section)section.open=true;}
  const restored=focusId?document.getElementById(focusId):summaryKey?body.querySelector(`summary[data-system-section="${summaryKey}"]`):null;
  if(restored){if(!restored.disabled)restored.focus();else document.getElementById('systemOkBtn').focus();}
}
function renderSystemInfoContent() {
  const info=systemStorageInfo, body=document.getElementById('systemGrid');
  document.getElementById('systemTitle').textContent=tr('systemInfo');
  document.getElementById('systemRefreshBtn').textContent=tr('refresh');
  document.getElementById('systemRefreshBtn').disabled=systemCacheBusy;
  document.getElementById('systemOkBtn').textContent=tr('ok');
  const tabs=document.getElementById('systemTabs');
  if(!tabs.children.length)tabs.innerHTML=['storage','device'].map(key=>`<button data-system-tab="${key}" role="tab" aria-controls="systemGrid" onclick="setSystemInfoTab('${key}')"></button>`).join('');
  for(const button of tabs.children){button.textContent=button.dataset.systemTab==='storage'?systemText('Dung lượng','Storage'):tr('machineSection');button.setAttribute('aria-selected',String(systemStorageTab===button.dataset.systemTab));}
  if(!info){body.innerHTML=`<div class="systemLoading" role="status"><span class="cabinetSpinner"></span>${escapeHtml(systemText('Đang kiểm tra dung lượng…','Checking storage…'))}</div>`;return;}
  if(systemStorageTab==='device') {
    const rows=[['machineName',info.MachineName],['userName',info.UserName],['os',info.OS],['cpu',info.Cpu],['cores',info.Cores],['memory',info.Memory],['display',info.Display],['gamesCount',info.GamesCount],['pluginsCount',info.PluginsCount]].filter(row=>row[1]!==undefined&&row[1]!==null&&row[1]!=='');
    body.innerHTML=`<div class="systemDeviceGrid">${rows.map(([label,value])=>systemInfoItem(tr(label),value)).join('')}</div>`;
    return;
  }
  const labels={igdb:'IGDB',explore:'Explore',backgrounds:systemText('Ảnh nền Steam','Steam backgrounds'),previews:systemText('Ảnh xem trước card','Card previews'),pet:systemText('Hoạt ảnh mascot','Mascot animations'),web:'WebView'};
  const categories=Array.isArray(info.CacheCategories)?info.CacheCategories:[];
  const rows=[['assetsSize',info.AssetsSize],['libraryAssetsSize',info.LibraryAssetsSize],['pluginAssetsSize',info.PluginAssetsSize],['builtInAssetsSize',info.BuiltInAssetsSize],['dataFileSize',info.DataFileSize],['webViewDataSize',info.WebViewDataSize]].filter(row=>row[1]!=null);
  const paths=[['appPath',info.AppPath],['dataPath',info.DataPath],['assetsPath',info.AssetsPath],['webViewData',info.WebViewData]].filter(row=>row[1]);
  body.innerHTML=`${info.StorageError?`<p class="systemScanError" role="status">${escapeHtml(systemText('Chưa đọc được dung lượng. Nhấn Làm mới để thử lại.','Storage could not be read. Select Refresh to retry.'))}</p>`:''}<div class="systemStorageOverview">${systemInfoItem(tr('launcherStorageSize'),info.LauncherStorageSize,'storageTotal')}${systemInfoItem(systemText('Có thể giải phóng','Reclaimable'),info.ReclaimableSize||info.CacheSize||'—','storageReclaimable')}${systemInfoItem('HYTCG cards',info.CardsSize||'—')}</div>
    <section class="systemCachePanel"><header><div><h3>Cache</h3><p>${escapeHtml(systemText('Giữ nguyên card, dữ liệu và đăng nhập.','Keeps your cards, data and sign-ins.'))}</p></div><button id="systemCacheBtn" class="primary" onclick="cleanLauncherCache()" ${systemCacheBusy?'disabled':''}>${systemCacheBusy?'<span class="cabinetSpinner"></span>':''}${escapeHtml(systemCacheBusy?systemText('Đang dọn…','Clearing…'):systemText('Giải phóng cache','Clear cache'))}</button></header>
    <div class="systemCacheRows">${categories.map(item=>`<div class="systemCacheRow"><span class="systemCacheIcon" aria-hidden="true">${({igdb:'◈',explore:'⌕',backgrounds:'▧',previews:'▣',pet:'✦',web:'◎'})[item.Id]||'◈'}</span><span><strong>${escapeHtml(labels[item.Id]||item.Name||'Cache')}</strong><small>${escapeHtml(Number(item.Files)||0)} ${escapeHtml(systemText('file','files'))}</small></span><b>${escapeHtml(item.Size||'0 B')}</b></div>`).join('')}</div>
    <div id="systemCacheStatus" class="systemCacheStatus ${systemCacheResult?.error?'error':''}" role="status" aria-live="polite">${escapeHtml(systemCleanupStatus())}</div></section>
    <details class="systemStorageDetails" data-system-section="breakdown"><summary data-system-section="breakdown">${escapeHtml(systemText('Chi tiết dung lượng','Storage breakdown'))}</summary><div class="systemDeviceGrid">${rows.map(([label,value])=>systemInfoItem(tr(label),value)).join('')}</div></details>
    <details class="systemStorageDetails" data-system-section="advanced"><summary data-system-section="advanced">${escapeHtml(systemText('Tuỳ chọn khác','More options'))}</summary><div class="systemAdvancedActions"><button id="systemCleanupBtn" class="smallAction" onclick="cleanupUnusedAssets()" ${systemCacheBusy?'disabled':''}>${escapeHtml(tr('cleanupAssets'))}</button><button id="systemClearWebBtn" class="danger" onclick="clearWebPageData()" ${systemCacheBusy?'disabled':''}>${escapeHtml(tr('clearWebData'))}</button></div><div class="systemPaths">${paths.map(([label,value])=>systemInfoItem(tr(label),value)).join('')}</div></details>`;
}
function cleanLauncherCache() {
  if(systemCacheBusy)return;
  systemCacheBusy=true;systemCacheResult=null;renderSystemInfo();
  post({type:'cleanLauncherCache'});
}
function handleCacheCleanup(msg) {
  systemCacheBusy=!!msg.busy;
  if(!systemCacheBusy)systemCacheResult=msg;
  if(document.getElementById('systemModal').classList.contains('show'))renderSystemInfo();
}
async function cleanupUnusedAssets() {
  if(systemCacheBusy)return;
  if(!await appConfirm(tr('cleanupAssetsConfirm')))return;
  post({type:'cleanupUnusedAssets'});
}
async function clearWebPageData() {
  if(systemCacheBusy)return;
  if(!await appConfirm(tr('clearWebDataConfirm')))return;
  post({type:'clearWebPageData'});
}
document.addEventListener('keydown',event=>{
  if(event.defaultPrevented||event.target.closest?.('.appDialogLayer'))return;
  const modal=document.getElementById('systemModal');if(!modal?.classList.contains('show'))return;
  if(event.key==='Escape'){event.preventDefault();closeSystemInfo();}
  if(event.key==='Tab') {
    const controls=[...modal.querySelectorAll('button,summary')].filter(node=>!node.disabled&&node.getClientRects().length);
    const first=controls[0],last=controls[controls.length-1];if(!first)return;
    if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}
    else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}
  }
});
