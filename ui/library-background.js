let libraryBackgroundDraft = { path: '', dim: 45 };
let steamBackgroundRequest = '';
let steamBackgroundItems = [];
function bgText(vi, en) { return state.Settings.Language === 'vi' ? vi : en; }
function openLibraryBackgroundSettings() {
  libraryBackgroundDraft = { path: state.Settings.LibraryBackground || '', dim: Math.max(0, Math.min(100, Number(state.Settings.LibraryBackgroundDim) || 0)) };
  renderLibraryBackgroundSettings();
}
function renderLibraryBackgroundSettings() {
  document.getElementById('backgroundSettingsLabel').textContent = bgText('Ảnh nền All app', 'All app background');
  const host = document.getElementById('backgroundSettingsControls');
  const draft = libraryBackgroundDraft;
  host.innerHTML = `<div class="backgroundActions"><button class="smallAction" onclick="openBackgroundPicker()">${bgText(draft.path ? 'Đổi ảnh nền' : 'Chọn ảnh nền', draft.path ? 'Change background' : 'Choose background')}</button>${draft.path ? `<button class="smallAction" onclick="removeLibraryBackground()">${bgText('Xoá nền', 'Remove')}</button>` : ''}</div>${draft.path ? `<div class="backgroundPreview">${backgroundMedia(draft.path)}<div></div></div><label class="backgroundDim"><span>${bgText('Dim theo theme', 'Dim with theme')} <output id="backgroundDimValue">${draft.dim}%</output></span><input type="range" min="0" max="100" value="${draft.dim}" oninput="setLibraryBackgroundDim(this.value)"></label>` : ''}`;
  updateBackgroundPreview();
}
function backgroundMedia(path) {
  const url = escapeHtml(img(path));
  return /\.(webm|mp4)(?:\?|$)/i.test(path) ? `<video src="${url}" muted autoplay loop playsinline preload="metadata"></video>` : `<img src="${url}" alt="" decoding="async">`;
}
function setLibraryBackgroundDim(value) {
  libraryBackgroundDraft.dim = Math.max(0, Math.min(100, Number(value) || 0));
  document.getElementById('backgroundDimValue').textContent = libraryBackgroundDraft.dim + '%'; updateBackgroundPreview();
}
function updateBackgroundPreview() {
  const overlay = document.querySelector('.backgroundPreview>div');
  if (overlay) overlay.style.background = backgroundDimColor(sThemeMode.value, libraryBackgroundDraft.dim);
}
function backgroundDimColor(theme, dim) { return `rgba(${theme === 'light' ? '244,247,251' : '8,10,18'},${Math.max(0, Math.min(100, dim)) / 100})`; }
function removeLibraryBackground() { libraryBackgroundDraft.path = ''; renderLibraryBackgroundSettings(); }
function chooseLibraryBackground(path) {
  if (!libraryBackgroundDraft.path) libraryBackgroundDraft.dim = 45;
  libraryBackgroundDraft.path = path; renderLibraryBackgroundSettings(); closeBackgroundPicker();
}
function openBackgroundPicker() {
  let modal = document.getElementById('backgroundPickerModal');
  if (!modal) {
    modal = document.createElement('div'); modal.id = 'backgroundPickerModal'; modal.className = 'modal'; document.body.appendChild(modal);
  }
  steamBackgroundRequest = '';
  modal.innerHTML = `<div class="dialog backgroundPickerDialog"><h2>${bgText('Chọn ảnh nền', 'Choose a background')}</h2><p>${bgText('Áp dụng cho menu All app ở cả hai layout.', 'Applies to the All app menu in both layouts.')}</p><div class="backgroundSources"><button class="smallAction" onclick="post({type:'chooseFile',kind:'libraryBackground'})">${bgText('Ảnh / GIF từ máy', 'Image / GIF from computer')}</button><button class="smallAction withIcon" onclick="requestSteamBackgrounds()">${iconHtml(assets.svgSteam, 'small')}Steam</button></div><div id="steamBackgroundResults" role="status"></div><div class="dialogActions"><button class="ghost" onclick="closeBackgroundPicker()">${tr('cancel')}</button></div></div>`;
  modal.classList.add('show');
}
function closeBackgroundPicker() { document.getElementById('backgroundPickerModal')?.classList.remove('show'); steamBackgroundRequest = ''; }
function requestSteamBackgrounds(refresh = false) {
  const result = document.getElementById('steamBackgroundResults');
  if (!state.Settings.SteamId) { result.textContent = bgText('Hãy khai báo và lưu Steam ID trong mục Chung trước.', 'Add and save your Steam ID in General first.'); return; }
  result.textContent = bgText('Đang tải nền Steam…', 'Loading Steam backgrounds…');
  steamBackgroundRequest = 'background-' + Date.now();
  post({ type: 'getSteamBackgrounds', requestId: steamBackgroundRequest, refresh });
}
function handleLibraryBackgroundMessage(msg) {
  if (msg.type === 'fileChosen' && msg.kind === 'libraryBackground') {
    if (document.getElementById('backgroundPickerModal')?.classList.contains('show')) chooseLibraryBackground(msg.path);
    return true;
  }
  if (msg.type !== 'steamBackgrounds') return false;
  if (msg.requestId !== steamBackgroundRequest || msg.catalog && msg.catalog.SteamId !== state.Settings.SteamId) return true;
  const result = document.getElementById('steamBackgroundResults'); if (!result) return true;
  steamBackgroundItems = msg.catalog?.Items || [];
  const warning = msg.error ? bgText('Không tải được nền Steam. Hãy thử làm mới.', 'Unable to load Steam backgrounds. Try refreshing.') : msg.catalog?.Warning === 'InventoryUnavailable' ? bgText('Hiển thị nền profile đọc được. Để lấy các nền khác, cần đặt Inventory thành Public; Steam cũng có thể tạm giới hạn truy cập.', 'Showing the available profile background. Other backgrounds require a Public Inventory; Steam may also limit requests.') : msg.catalog?.Warning ? bgText('Một phần dữ liệu được lấy từ bản lưu hoặc chưa tải hết.', 'Some results are cached or incomplete.') : '';
  result.innerHTML = `<div class="backgroundResultsHead"><span>${steamBackgroundItems.length} ${bgText('nền', 'backgrounds')}</span><button class="smallAction" onclick="requestSteamBackgrounds(true)">${tr('refresh')}</button></div>${warning ? `<p class="backgroundNotice">${warning}</p>` : ''}<div class="steamBackgroundGrid">${steamBackgroundItems.map((item, i) => `<button class="steamBackgroundTile" onclick="chooseLibraryBackground(steamBackgroundItems[${i}].Video || steamBackgroundItems[${i}].Image)"><img src="${escapeHtml(item.Image)}" loading="lazy" alt=""><span>${escapeHtml(item.Name || 'Steam background')}${item.Video ? ' · Motion' : ''}</span></button>`).join('')}</div>${!steamBackgroundItems.length && !warning ? `<p>${bgText('Tài khoản chưa có ảnh nền đọc được.', 'No backgrounds available for this account.')}</p>` : ''}`;
  return true;
}
function applyLibraryBackground() {
  const library = document.getElementById('library');
  const path = state.Settings.LibraryBackground || '';
  let media = document.getElementById('libraryBackgroundMedia');
  if (!media && path) { media = document.createElement('div'); media.id = 'libraryBackgroundMedia'; media.className = 'libraryBackgroundMedia'; media.setAttribute('aria-hidden', 'true'); library.prepend(media); }
  library.classList.toggle('hasLibraryBackground', !!path);
  if (!media) return;
  if (!path) { media.remove(); return; }
  if (media.dataset.source !== path) { media.innerHTML = backgroundMedia(path) + '<div class="libraryBackgroundDim"></div>'; media.dataset.source = path; }
  media.querySelector('.libraryBackgroundDim').style.background = backgroundDimColor(state.Settings.ThemeMode, state.Settings.LibraryBackgroundDim || 0);
  syncLibraryBackgroundMotion();
}
function syncLibraryBackgroundMotion() {
  const video = document.querySelector('#libraryBackgroundMedia video');
  if (!video) return;
  if (document.hidden || !document.getElementById('library').classList.contains('show')) video.pause(); else video.play().catch(() => {});
}
document.addEventListener('visibilitychange', syncLibraryBackgroundMotion);
document.addEventListener('DOMContentLoaded', () => {
  const library = document.getElementById('library');
  if (library) new MutationObserver(syncLibraryBackgroundMotion).observe(library, { attributes: true, attributeFilter: ['class'] });
});
