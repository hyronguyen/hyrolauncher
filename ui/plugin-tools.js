// Built-in Movie Watchlist and PET panels. Native storage remains the source of truth.
let movieFilter = 'all';
let movieEditorMode = 'details';
let movieSaved = false;
let petPanelDraft = null;
let petPreviewDragging = false;
let petTimingPreview = null;
let petTimingRequest = '';
let petTimingTimer = null;
function petFrameDelay(value) {
  const delay=boundedNumber(value,0,0,2000);
  return delay===0 ? 0 : Math.max(20,Math.round(delay/10)*10);
}
function petAnimationDelay(settings, dragging = petPreviewDragging) {
  return petFrameDelay((dragging ? settings?.PetDragFrameDelayMs : settings?.PetIdleFrameDelayMs) ?? settings?.PetFrameDelayMs ?? 0);
}
function switchPetPreviewAnimation() {
  syncPetPanelDraft();
  petPreviewDragging = !petPreviewDragging;
  const input = document.getElementById('petFrameDelay');
  if (input) input.value = String(petAnimationDelay(petPanelDraft));
  syncPetPreview();
}
function petTimingKey(pet) { return `${pet?.Id||''}|${pet?.IdleGifPath||''}|${pet?.DragGifPath||''}|${petAnimationDelay(petPanelDraft,false)}|${petAnimationDelay(petPanelDraft,true)}`; }
function petPreviewGif(pet) {
  const delay=petAnimationDelay(petPanelDraft);
  if(petTimingPreview?.key===petTimingKey(pet)) return petPreviewDragging ? petTimingPreview.dragPath : petTimingPreview.idlePath;
  if(delay===0) return petPreviewDragging ? pet?.DragGifPath||pet?.IdleGifPath : pet?.IdleGifPath;
  if(delay===petAnimationDelay(plugin())) return petPreviewDragging ? pet?.DragPlaybackPath||pet?.DragGifPath||pet?.IdleGifPath : pet?.IdlePlaybackPath||pet?.IdleGifPath;
  return petPreviewDragging ? pet?.DragGifPath||pet?.IdleGifPath : pet?.IdleGifPath;
}
function queuePetTimingPreview(pet) {
  const key=petTimingKey(pet);
  if(!pet?.IdleGifPath || petTimingPreview?.key===key || petTimingRequest===key) return;
  clearTimeout(petTimingTimer);
  petTimingRequest=key;
  petTimingTimer=setTimeout(()=>post({type:'previewPetTiming',requestId:key,idlePath:pet.IdleGifPath,dragPath:pet.DragGifPath||pet.IdleGifPath,idleDelayMs:petAnimationDelay(petPanelDraft,false),dragDelayMs:petAnimationDelay(petPanelDraft,true)}),180);
}
function receivePetTimingPreview(message) {
  if(message.requestId!==petTimingRequest || message.requestId!==petTimingKey(currentPetForPreview())) return;
  petTimingPreview={key:message.requestId,idlePath:message.idlePath,dragPath:message.dragPath};
  syncPetPreview();
}
function resetPetFrameDelay() {
  const input=document.getElementById('petFrameDelay'); if(input) input.value='0';
  syncPetPreview();
}
function watchIcon(name) {
  const paths={search:'<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 4.5 4.5"/>',plus:'<path d="M12 5v14M5 12h14"/>',more:'<circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/>',edit:'<path d="m15 5 4 4M4 20l4-1L20 7a2.8 2.8 0 0 0-4-4L4 15z"/>',progress:'<path d="M4 7v5h5M4 12a8 8 0 1 0 2-6M12 8v5l3 2"/>',play:'<path d="m8 5 11 7-11 7z"/>',download:'<path d="M12 3v12m-5-5 5 5 5-5M4 16v5h16v-5"/>',upload:'<path d="M12 16V4m-5 5 5-5 5 5M4 16v5h16v-5"/>'};
  return `<svg class="watchIcon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name]||paths.more}</svg>`;
}
const movieStatuses = ['watching', 'planned', 'completed'];
const fieldValue = (name, fallback = '') => document.getElementById(name)?.value ?? fallback;
const boundedNumber = (value, fallback, min, max) => Math.max(min, Math.min(max, parseInt(value, 10) || fallback));

function movieStatus(item) {
  if (movieStatuses.includes(item.Status)) return item.Status;
  if (/^(done|completed|finished|hoàn thành|đã xem)$/i.test((item.Episode || '').trim())) return 'completed';
  return item.Episode || item.WatchTime ? 'watching' : 'planned';
}
function movieStatusLabel(status) {
  return status === 'watching' ? tr('Continue watching', 'Đang xem') : status === 'completed' ? tr('Completed', 'Đã xem') : tr('My list', 'Muốn xem');
}
function movieProgress(item) {
  const total = boundedNumber(item.TotalEpisodes, 0, 0, 10000);
  const episode = /^\d+$/.test(String(item.Episode || '').trim()) ? Number(item.Episode) : 0;
  return total > 0 && episode > 0 ? Math.min(100, episode / total * 100) : null;
}
function movieMeta(item) {
  const episode = movieStatus(item) !== 'completed' && item.Episode ? `${tr('Episode', 'Tập')} ${item.Episode}${item.TotalEpisodes > 0 ? ' / ' + item.TotalEpisodes : ''}` : movieStatusLabel(movieStatus(item));
  return [episode, item.WatchTime].filter(Boolean).join(' · ');
}
function movieImage(item, eager = false) {
  return item.PosterPath ? `<img ${eager ? '' : 'loading="lazy"'} decoding="async" src="${esc(mediaUrl(item.PosterPath))}" alt="" onerror="this.hidden=true">` : '';
}
function movieButton(item, fn, title, content, cls = 'ghost') {
  return `<button class="${cls}" data-id="${esc(item.Id)}" onclick="${fn}(this.dataset.id)" title="${esc(title)}" aria-label="${esc(title)}">${content}</button>`;
}
function renderMovieItem(item) {
  const progress = movieProgress(item);
  return `<article class="watchCard">
    <button class="watchPoster" data-id="${esc(item.Id)}" onclick="openMovie(this.dataset.id)" aria-label="${esc(tr('Watch ', 'Xem ') + item.Title)}">
      <span class="watchPlaceholder">${esc((item.Title || '?').slice(0, 1))}</span>${movieImage(item)}
      <span class="watchPlay" aria-hidden="true">▶</span><span class="watchCaption"><strong>${esc(item.Title)}</strong><small>${esc(movieMeta(item))}</small></span>
      ${progress !== null ? `<span class="watchProgress"><i style="width:${progress}%"></i></span>` : ''}
    </button>
    <div class="watchCardActions">${movieButton(item, 'editWatchProgress', tr('Update progress', 'Cập nhật tiến độ'), watchIcon('progress') + tr('Progress', 'Tiến độ'), 'small')}${movieButton(item, 'editWatchItem', tr('Edit movie', 'Sửa phim'), watchIcon('edit'), 'small')}</div>
  </article>`;
}
function renderMovie(p) {
  if (watchDraft && document.getElementById('mEpisode')) syncWatchDraft();
  const scroll = document.getElementById('watchBody')?.scrollTop || 0;
  const all = [...(p.WatchItems || [])].sort((a, b) => String(b.UpdatedAt || '').localeCompare(String(a.UpdatedAt || '')));
  const items = all.filter(item => (!search || `${item.Title || ''} ${item.Episode || ''}`.toLowerCase().includes(search.trim().toLowerCase())) && (movieFilter === 'all' || movieStatus(item) === movieFilter));
  const featured = all.find(item => movieStatus(item) === 'watching') || all[0];
  const shelves = movieFilter === 'all' && !search.trim();
  app.innerHTML = `<header class="watchHeader"><div><div class="title">${tr('Your library', 'Phim của bạn')}</div><div class="sub">${all.length} ${tr('saved movies', 'phim đã lưu')} · HyBrowser</div></div><div class="watchHeaderActions"><button class="primary" onclick="newWatchItem()">${watchIcon('plus')} ${tr('Add movie', 'Thêm phim')}</button><details class="watchDataMenu"><summary aria-label="${tr('Import or export Excel', 'Nhập hoặc xuất Excel')}">${watchIcon('more')}</summary><div><button onclick="this.closest('details').open=false;post({type:'exportWatchlistExcel'})">${watchIcon('download')}${tr('Export Excel', 'Xuất Excel')}</button><button onclick="requestMovieImport(this)">${watchIcon('upload')}${tr('Import Excel', 'Nhập Excel')}</button></div></details></div></header>
    <div id="watchBody" class="body watchBody">
      <div class="watchToolbar"><div class="watchSearch"><span>${watchIcon('search')}</span><input id="movieSearchInput" type="search" value="${esc(search)}" placeholder="${tr('Search your movies', 'Tìm phim của bạn')}" aria-label="${tr('Search movies', 'Tìm phim')}" oninput="updateMovieSearch(this)"></div>
        <nav class="watchFilters" aria-label="${tr('Movie status', 'Trạng thái phim')}">${['all', ...movieStatuses].map(status => `<button class="watchFilter ${movieFilter === status ? 'active' : ''}" aria-pressed="${movieFilter === status}" data-status="${status}" onclick="setMovieFilter(this.dataset.status)">${status === 'all' ? tr('All', 'Tất cả') : movieStatusLabel(status)}<span>${status === 'all' ? all.length : all.filter(i => movieStatus(i) === status).length}</span></button>`).join('')}</nav></div>
      ${shelves && featured ? `<section class="watchFeature">${movieImage(featured, true)}<div class="watchFeatureShade"></div><div class="watchFeatureContent"><span class="watchEyebrow">${esc(movieStatusLabel(movieStatus(featured)))}</span><h2>${esc(featured.Title)}</h2><p>${esc(movieMeta(featured))}</p><div class="actions">${movieButton(featured, 'openMovie', tr('Open in HyBrowser', 'Mở bằng HyBrowser'), '▶ ' + tr('Watch now', 'Xem ngay'), 'primary')}${movieButton(featured, 'editWatchProgress', tr('Update progress', 'Cập nhật tiến độ'), tr('Progress', 'Tiến độ'))}</div></div></section>` : ''}
      ${!items.length ? `<div class="watchEmpty"><span aria-hidden="true">▤</span><h3>${tr('No movies here yet', 'Chưa có phim ở đây')}</h3><p>${search ? tr('Try another title or filter.', 'Thử tên phim hoặc bộ lọc khác.') : tr('Save any movie link. Open and continue in HyBrowser.', 'Lưu liên kết phim, mở và xem tiếp bằng HyBrowser.')}</p><button class="ghost" onclick="newWatchItem()">＋ ${tr('Add movie', 'Thêm phim')}</button></div>` : shelves ? movieStatuses.map(status => {
        const row = items.filter(item => movieStatus(item) === status);
        return row.length ? `<section class="watchSection"><header><h3>${movieStatusLabel(status)} <span>${row.length}</span></h3><button class="watchSeeAll" data-status="${status}" onclick="setMovieFilter(this.dataset.status)">${tr('See all', 'Xem tất cả')} →</button></header><div class="watchShelf">${row.map(renderMovieItem).join('')}</div></section>` : '';
      }).join('') : `<section class="watchSection"><header><h3>${movieFilter === 'all' ? tr('Search results', 'Kết quả tìm kiếm') : movieStatusLabel(movieFilter)} <span>${items.length}</span></h3></header><div class="watchGrid">${items.map(renderMovieItem).join('')}</div></section>`}
      <p class="watchFootnote">${tr('Open a movie in HyBrowser. Save watched episode to advance by one.', 'Mở phim bằng HyBrowser. Bấm lưu tập đã xem để tăng thêm 1 tập.')}</p>
    </div><div id="movieEditor" class="editor movieEditorModal"></div>`;
  const body = document.getElementById('watchBody');
  if (body) body.scrollTop = scroll;
  renderMovieEditor(!!watchDraft);
}
async function requestMovieImport(button) {
  button?.closest('details')?.removeAttribute('open');
  if(draftChanged() && !await appConfirm(tr('Save or close your movie draft before importing. Discard it now?', 'Bạn còn bản nháp chưa lưu. Bỏ bản nháp để nhập Excel?'))) return;
  watchDraft=null; renderMovieEditor(false); post({type:'importWatchlistExcel'});
}
async function confirmMovieImport(msg) {
  if(!msg.count) { appAlert(tr('This workbook has no movies.', 'Tệp này chưa có phim.')); return; }
  const message=tr(`Import ${msg.count} movies from ${msg.fileName}? Matching IDs or watch links are updated. Other movies are kept.`, `Nhập ${msg.count} phim từ ${msg.fileName}? Phim trùng mã hoặc link sẽ được cập nhật; các phim khác vẫn giữ nguyên.`);
  if(await appConfirm(message)) post({type:'commitWatchlistImport',requestId:msg.requestId});
}
function setMovieFilter(value) {
  movieFilter = value;
  render();
  const body = document.getElementById('watchBody');
  if (body) body.scrollTop = 0;
}
function updateMovieSearch(input) {
  search = input.value;
  const position = input.selectionStart;
  render();
  const field = document.getElementById('movieSearchInput');
  if (field) { field.focus(); field.setSelectionRange(position, position); }
}
function emptyWatchItem() { return { Id:id(), Title:'', PosterPath:'', Episode:'', WatchTime:'', Url:'', SourceGameId:'', Status:'planned', TotalEpisodes:0, UpdatedAt:new Date().toISOString() }; }
async function receiveBrowserWatch(msg) {
  if (watchDraft && !await appConfirm(tr('Replace the current editor draft with this page?', 'Thay bản nháp hiện tại bằng trang này?'))) return;
  const existing = (plugin().WatchItems || []).find(item => item.Url === msg.url);
  watchDraft = existing ? { ...existing } : { ...emptyWatchItem(), Title:msg.title || '', Url:msg.url || '' };
  movieEditorMode = 'details'; movieSaved = false;
  renderMovieEditor(true); focusMovieEditor();
}
function draftChanged() {
  if (!watchDraft) return false;
  syncWatchDraft();
  const saved = (plugin()?.WatchItems || []).find(item => item.Id === watchDraft.Id);
  const keys = ['Title', 'PosterPath', 'Episode', 'WatchTime', 'Url', 'TotalEpisodes'];
  return keys.some(key => String(watchDraft[key] || '') !== String(saved?.[key] || '')) || movieStatus(watchDraft) !== (saved ? movieStatus(saved) : 'planned');
}
async function replaceMovieDraft(next, mode = 'details') {
  if (draftChanged() && !await appConfirm(tr('Discard your unsaved changes?', 'Bỏ các thay đổi chưa lưu?'))) return;
  watchDraft = next; movieEditorMode = mode; movieSaved = false;
  renderMovieEditor(true); focusMovieEditor();
}
function newWatchItem() { return replaceMovieDraft(emptyWatchItem()); }
function editWatchItem(itemId) { const item = (plugin().WatchItems || []).find(i => i.Id === itemId); if (item) return replaceMovieDraft({ ...item }); }
function editWatchProgress(itemId) { const item = (plugin().WatchItems || []).find(i => i.Id === itemId); if (item) return replaceMovieDraft({ ...item }, 'progress'); }
function focusMovieEditor() { document.getElementById(movieEditorMode === 'progress' ? 'mEpisode' : 'mTitle')?.focus(); }
async function closeMovieEditor() {
  if (draftChanged() && !await appConfirm(tr('Discard your unsaved changes?', 'Bỏ các thay đổi chưa lưu?'))) return;
  watchDraft = null; renderMovieEditor(false);
  document.getElementById('movieSearchInput')?.focus();
}
function renderMovieEditor(show) {
  const host = document.getElementById('movieEditor'); if (!host) return;
  host.classList.toggle('show', !!show && !!watchDraft);
  if (!show || !watchDraft) { host.innerHTML = ''; return; }
  const quick = movieEditorMode === 'progress';
  host.innerHTML = `<form class="watchEditorCard" role="dialog" aria-modal="true" aria-labelledby="watchEditorTitle" onsubmit="event.preventDefault();saveWatch()">
    <header><div><h3 id="watchEditorTitle">${quick ? tr('Update progress', 'Cập nhật tiến độ') : tr('Movie details', 'Thông tin phim')}</h3><p>${quick ? esc(watchDraft.Title) : tr('Movie links open in HyBrowser.', 'Liên kết phim được mở bằng HyBrowser.')}</p></div><button type="button" class="ghost" onclick="closeMovieEditor()" aria-label="${tr('Close', 'Đóng')}">×</button></header>
    <div class="watchEditorFields">
    ${quick ? '' : `<div class="row"><label for="mTitle">${tr('Title', 'Tên phim')}</label><input id="mTitle" value="${esc(watchDraft.Title)}" required maxlength="250" placeholder="${tr('Movie or series title', 'Tên phim hoặc bộ phim')}"></div>
    <div class="row"><label for="mUrl">${tr('Watch link', 'Link xem phim')}</label><input id="mUrl" value="${esc(watchDraft.Url)}" placeholder="https://..."></div>
    <div class="row"><label for="mPoster">${tr('Poster', 'Ảnh phim')}</label><div class="inline"><input id="mPoster" value="${esc(watchDraft.PosterPath)}" placeholder="${tr('Image URL or local file', 'Link ảnh hoặc tệp trên máy')}"><button type="button" class="ghost" onclick="choosePoster()" aria-label="${tr('Choose poster', 'Chọn ảnh')}">…</button></div></div>`}
    <div class="row"><label for="mStatus">${tr('Status', 'Trạng thái')}</label><select id="mStatus">${movieStatuses.map(status => `<option value="${status}" ${movieStatus(watchDraft) === status ? 'selected' : ''}>${movieStatusLabel(status)}</option>`).join('')}</select></div>
    <div class="watchEditorPair"><label for="mEpisode">${tr('Current episode', 'Tập đang xem')}<input id="mEpisode" value="${esc(watchDraft.Episode)}" placeholder="${tr('Optional', 'Không bắt buộc')}"></label><label for="mTotal">${tr('Total episodes', 'Tổng số tập')}<input id="mTotal" type="number" min="0" max="10000" value="${watchDraft.TotalEpisodes || ''}" placeholder="—"></label></div>
    <div class="row"><label for="mTime">${tr('Resume at', 'Xem tới')}</label><input id="mTime" value="${esc(watchDraft.WatchTime)}" placeholder="00:24:30"></div>
    </div><footer><span id="movieSaveFeedback" role="status">${movieSaved ? '✓ ' + tr('Saved', 'Đã lưu') : ''}</span><div class="actions">${!quick && (plugin()?.WatchItems || []).some(i => i.Id === watchDraft.Id) ? `<button type="button" class="danger" onclick="deleteWatch()">${tr('Delete', 'Xóa')}</button>` : ''}<button type="button" class="ghost" onclick="closeMovieEditor()">${tr('Close', 'Đóng')}</button><button class="primary" type="submit">${tr('Save', 'Lưu')}</button></div></footer></form>`;
  host.onkeydown = event => {
    if (event.key === 'Escape') { event.preventDefault(); closeMovieEditor(); }
    if (event.key === 'Tab') {
      const controls = [...host.querySelectorAll('button,input,select')];
      const first = controls[0], last = controls[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    }
  };
  host.oninput = () => { movieSaved = false; const label = document.getElementById('movieSaveFeedback'); if (label) label.textContent = ''; };
}
function syncWatchDraft() {
  if (!watchDraft) return;
  [['mTitle','Title'],['mPoster','PosterPath'],['mEpisode','Episode'],['mTime','WatchTime'],['mUrl','Url']].forEach(([field,key]) => { const input = document.getElementById(field); if (input) watchDraft[key] = input.value; });
  watchDraft.Status = fieldValue('mStatus', movieStatus(watchDraft));
  watchDraft.TotalEpisodes = boundedNumber(fieldValue('mTotal', watchDraft.TotalEpisodes), 0, 0, 10000);
}
function choosePoster() { syncWatchDraft(); post({ type:'chooseFile', kind:'moviePoster' }); }
function saveWatch() {
  const p = plugin(); syncWatchDraft(); if (!p || !watchDraft) return;
  if (!watchDraft.Title.trim()) { appAlert(tr('Movie name is required.', 'Tên phim là bắt buộc.')); return; }
  watchDraft.Title = watchDraft.Title.trim(); watchDraft.Url = watchDraft.Url.trim();
  watchDraft.UpdatedAt = new Date().toISOString();
  p.WatchItems = p.WatchItems || [];
  const index = p.WatchItems.findIndex(i => i.Id === watchDraft.Id);
  if (index >= 0) p.WatchItems[index] = { ...watchDraft }; else p.WatchItems.unshift({ ...watchDraft });
  movieSaved = true; savePlugin(p); render(); focusMovieEditor();
}
async function deleteWatch() {
  const p = plugin(); if (!p || !watchDraft || !await appConfirm(tr('Delete this movie?', 'Xóa phim này?'))) return;
  p.WatchItems = (p.WatchItems || []).filter(i => i.Id !== watchDraft.Id);
  savePlugin(p); watchDraft = null; render();
}
function openMovie(itemId) {
  const item = (plugin()?.WatchItems || []).find(i => i.Id === itemId);
  if (!item?.Url?.trim()) { editWatchItem(itemId); appAlert(tr('Add a watch link to open this movie in HyBrowser.', 'Thêm link xem phim để mở bằng HyBrowser.')); return; }
  uiEffects.play('switch'); post({ type:'openMovieFromPlugin', itemId });
}

function syncPetPanelDraft() {
  if (!petPanelDraft || !document.getElementById('petEnabled')) return;
  petPanelDraft.PetEnabled = document.getElementById('petEnabled').checked;
  petPanelDraft.PetSize = boundedNumber(fieldValue('petSize'), 78, 44, 300);
  petPanelDraft.PetStaySeconds = boundedNumber(fieldValue('petStay'), 60, 10, 180);
  petPanelDraft[petPreviewDragging ? 'PetDragFrameDelayMs' : 'PetIdleFrameDelayMs'] = petFrameDelay(fieldValue('petFrameDelay',petAnimationDelay(petPanelDraft)));
  petPanelDraft.ActivePetId = fieldValue('activePet', petPanelDraft.ActivePetId);
}
function renderPet(p) {
  syncPetPanelDraft();
  if (!petPanelDraft || petPanelDraft.Id !== p.Id) petPanelDraft = { ...p, PetStaySeconds:p.PetStaySeconds || 60, PetIdleFrameDelayMs:petAnimationDelay(p,false), PetDragFrameDelayMs:petAnimationDelay(p,true) };
  const pets = p.Pets || [];
  app.innerHTML = `<header class="watchHeader"><div><div class="title">${tr('Your companion', 'Bạn đồng hành')}</div><div class="sub">${tr('Idle · Fade & reappear', 'Idle · Mờ dần & xuất hiện lại')}</div></div><button class="ghost" onclick="newPet()">＋ ${tr('New pet', 'Thêm pet')}</button></header>
    <div class="body petBody"><section class="petPreview"><div class="petPreviewInfo"><span id="petPreviewName"></span><button class="petIdleBadge" onclick="switchPetPreviewAnimation()" aria-pressed="${petPreviewDragging}" title="${tr('Preview and edit idle / drag timing', 'Chuyển xem và chỉnh delay idle / bị kéo')}">${petPreviewDragging ? 'DRAG' : 'IDLE'} ⇄</button></div><div id="petPreviewActor"></div></section>
    <section class="petControlCard"><label class="petEnableRow" for="petEnabled"><span><strong>${tr('Show pet', 'Hiển thị pet')}</strong><small>${tr('Drag your pet to move it. Open PET from the plugins menu to adjust settings.', 'Kéo pet để di chuyển. Mở PET trong menu plugin để chỉnh thiết lập.')}</small></span><input id="petEnabled" type="checkbox" ${petPanelDraft.PetEnabled ? 'checked' : ''} oninput="syncPetPreview()"></label>
    <div class="petSelectRow"><label for="activePet">${tr('Companion', 'Pet đang dùng')}</label><select id="activePet" ${petDraft ? 'disabled' : ''} onchange="syncPetPreview()">${pets.map(x => `<option value="${esc(x.Id)}" ${x.Id === petPanelDraft.ActivePetId ? 'selected' : ''}>${esc(x.Name || 'Pet')}</option>`).join('')}</select><button class="ghost" onclick="editPet()" ${petDraft || petPanelDraft.ActivePetId === 'default.hyro-rider' ? 'disabled' : ''}>${tr('Edit', 'Sửa')}</button></div>
    <div class="petSlider"><label for="petSize">${tr('Size', 'Kích cỡ')}<output id="petSizeValue"></output></label><input id="petSize" type="range" min="44" max="300" value="${petPanelDraft.PetSize || 78}" oninput="syncPetPreview()"></div>
    <div class="petSlider"><label for="petFrameDelay"><span id="petFrameDelayLabel"></span><output id="petFrameDelayValue"></output></label><div class="petTimingControl"><input id="petFrameDelay" type="number" min="0" max="2000" step="10" value="${petAnimationDelay(petPanelDraft)}" oninput="syncPetPreview()" aria-label="${tr('Frame delay in milliseconds','Delay mỗi frame tính bằng mili giây')}"><span>ms</span><button class="ghost" onclick="resetPetFrameDelay()">${tr('Original','Gốc')}</button></div><p>${tr('Switch IDLE / DRAG above to edit each animation separately. 0 keeps its original timing; 20–2000 ms slows each frame.','Chuyển IDLE / DRAG phía trên để chỉnh riêng từng animation. 0 dùng thời gian gốc; 20–2000 ms là delay mỗi frame.')}</p></div>
    <div class="petSlider"><label for="petStay">${tr('Stay at each position', 'Đứng yên tại mỗi vị trí')}<output id="petStayValue"></output></label><input id="petStay" type="range" min="10" max="180" step="10" value="${petPanelDraft.PetStaySeconds}" oninput="syncPetPreview()"><p>${tr('The pet rests here, fades out, then reappears elsewhere. No walking.', 'Pet đứng yên, mờ dần rồi xuất hiện ở chỗ khác.')}</p></div></section>
    <div id="petEditor" class="editor petEditCard"></div><footer class="petPanelFooter"><span id="petSaveFeedback" role="status"></span><button class="primary" onclick="savePetPanel()">${tr('Save settings', 'Lưu thiết lập')}</button></footer></div>`;
  renderPetEditor(!!petDraft); syncPetPreview();
}
function currentPetForPreview() { const p = plugin(); return petDraft || (p?.Pets || []).find(x => x.Id === petPanelDraft?.ActivePetId) || p?.Pets?.[0]; }
function petPreviewHtml(pet) {
  const hasGif = !!pet?.IdleGifPath;
  const size = Math.min(120,petPanelDraft?.PetSize || 78);
  const gif=petPreviewGif(pet);
  return `<div class="previewPet ${hasGif ? 'hasGif' : ''}" style="--pet-size:${size}px"><div class="petVisual">${hasGif ? `<img class="petGif petIdleGif" src="${esc(mediaUrl(gif))}" alt="${esc(pet.Name || 'Pet')}">` : ''}<div class="petFallback"></div></div></div>`;
}
function syncPetPreview() {
  syncPetPanelDraft();
  const pet = currentPetForPreview();
  const actor = document.getElementById('petPreviewActor'); if (!actor) return;
  const key = `${pet?.Id || ''}|${petPreviewGif(pet)||''}`;
  if (actor.dataset.petKey !== key) { actor.innerHTML = petPreviewHtml(pet); actor.dataset.petKey = key; }
  actor.querySelector('.previewPet')?.style.setProperty('--pet-size', `${Math.min(120,petPanelDraft.PetSize)}px`);
  actor.style.opacity = petPanelDraft.PetEnabled ? '1' : '.45';
  const label = document.getElementById('petPreviewName'); if (label) label.textContent = pet?.Name || 'Pet';
  const badge=actor.parentElement?.querySelector('.petIdleBadge'); if(badge) { badge.textContent=(petPreviewDragging?'DRAG':'IDLE')+' ⇄'; badge.setAttribute('aria-pressed',String(petPreviewDragging)); }
  const sizeLabel = document.getElementById('petSizeValue'); if (sizeLabel) sizeLabel.textContent = petPanelDraft.PetSize + ' px';
  const stayLabel = document.getElementById('petStayValue'); if (stayLabel) stayLabel.textContent = petPanelDraft.PetStaySeconds + ' ' + tr('s', 'giây');
  const timingLabel=document.getElementById('petFrameDelayLabel'); if(timingLabel) timingLabel.textContent=tr('Frame delay','Delay mỗi frame')+' · '+(petPreviewDragging?'DRAG':'IDLE');
  const delay=petAnimationDelay(petPanelDraft);
  const frameLabel=document.getElementById('petFrameDelayValue'); if(frameLabel) frameLabel.textContent=delay ? delay+' ms / frame' : tr('Original GIF','Theo GIF gốc');
  queuePetTimingPreview(pet);
  const saved = document.getElementById('petSaveFeedback'); if (saved) saved.textContent = '';
}
function savePetPanel() {
  const p = plugin(); if (!p) return; syncPetPanelDraft();
  Object.assign(p, { PetEnabled:petPanelDraft.PetEnabled, PetSize:petPanelDraft.PetSize, PetStaySeconds:petPanelDraft.PetStaySeconds, PetIdleFrameDelayMs:petAnimationDelay(petPanelDraft,false), PetDragFrameDelayMs:petAnimationDelay(petPanelDraft,true), ActivePetId:petPanelDraft.ActivePetId });
  savePlugin(p); const feedback = document.getElementById('petSaveFeedback'); if (feedback) feedback.textContent = '✓ ' + tr('Saved', 'Đã lưu');
}
async function newPet() {
  if (petDraft && !await appConfirm(tr('Discard this pet draft?', 'Bỏ bản nháp pet này?'))) return;
  syncPetPanelDraft(); petDraft = { Id:id(), Name:'', IdleGifPath:'', DragGifPath:'', WalkGifPath:'' }; render();
  document.getElementById('petName')?.focus();
}
function editPet() { const pet = currentPetForPreview(); if (pet && pet.Id !== 'default.hyro-rider') { syncPetPanelDraft(); petDraft = { ...pet }; render(); } }
function renderPetEditor(show) {
  const host = document.getElementById('petEditor'); if (!host) return;
  host.classList.toggle('show', !!show && !!petDraft);
  if (!show || !petDraft) { host.innerHTML = ''; return; }
  host.innerHTML = `<h3>${tr('Custom companion', 'Pet của bạn')}</h3><div class="row"><label for="petName">${tr('Name', 'Tên')}</label><input id="petName" value="${esc(petDraft.Name)}" oninput="syncPetDraft();syncPetPreview()"></div>
    <div class="row"><label for="petIdle">Idle GIF</label><div class="inline"><input id="petIdle" value="${esc(petDraft.IdleGifPath)}" oninput="syncPetDraft();syncPetPreview()"><button class="ghost" onclick="choosePetGif('petIdleGif')" aria-label="${tr('Choose idle GIF', 'Chọn GIF idle')}">…</button></div></div>
    <div class="row"><label for="petDrag">${tr('Drag GIF', 'GIF bị kéo')}</label><div class="inline"><input id="petDrag" value="${esc(petDraft.DragGifPath)}" placeholder="${tr('Optional · uses idle if empty', 'Không bắt buộc · dùng idle nếu trống')}" oninput="syncPetDraft()"><button class="ghost" onclick="choosePetGif('petDragGif')" aria-label="${tr('Choose drag GIF', 'Chọn GIF bị kéo')}">…</button></div></div>
    <div class="actions"><button class="ghost" onclick="cancelPetEditor()">${tr('Cancel', 'Hủy')}</button><button class="primary" onclick="savePet()">${tr('Save pet', 'Lưu pet')}</button></div>`;
}
function cancelPetEditor() { petDraft = null; render(); }
function choosePetGif(kind) { syncPetDraft(); syncPetPanelDraft(); post({ type:'chooseFile', kind }); }
function syncPetDraft() {
  if (!petDraft) return;
  petDraft.Name = fieldValue('petName', petDraft.Name); petDraft.IdleGifPath = fieldValue('petIdle', petDraft.IdleGifPath);
  petDraft.DragGifPath = fieldValue('petDrag', petDraft.DragGifPath || '');
}
function savePet() {
  const p = plugin(); syncPetDraft(); syncPetPanelDraft(); if (!p || !petDraft) return;
  if (!petDraft.IdleGifPath.trim()) { appAlert(tr('Choose an idle GIF.', 'Chọn GIF idle cho pet.')); return; }
  if (!petDraft.Name.trim()) petDraft.Name = 'Pet';
  p.Pets = p.Pets || []; const index = p.Pets.findIndex(x => x.Id === petDraft.Id);
  if (index >= 0) p.Pets[index] = { ...petDraft }; else p.Pets.push({ ...petDraft });
  petPanelDraft.ActivePetId = petDraft.Id;
  // Commit the pet together with the current controls; state messages keep drafts intact.
  petDraft = null;
  const selected = document.getElementById('activePet'); if (selected) selected.innerHTML = `<option value="${esc(petPanelDraft.ActivePetId)}">Pet</option>`;
  savePetPanel(); render();
}
