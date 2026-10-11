'use strict';
let bookshelfSelectedId = '';
let bookshelfPreviewId = '';
let bookshelfSettingsSaveTimer = 0;
let bookshelfSettingsReflow = 0;
const bookshelfLightCache = new Map();
const bookshelfBlockedArtwork = new Set();
const bookshelfNativeColors = new Map();
const bookshelfNativeColorRequests = new Set();
const bookshelfPendingColorKeys = new Map();
const bookshelfClosingTimers = new WeakMap();
const bookshelfSettingsSpec = [
  ['BookshelfAnimationsEnabled', true],
  ['BookshelfPullDuration', 300, 100, 5000, 'ms'],
  ['BookshelfHoverDuration', 150, 50, 500, 'ms'],
  ['BookshelfPullDistance', 40, 10, 100, 'px'],
  ['BookshelfHoverLift', 4, 0, 15, 'px'],
  ['BookshelfDefaultAngle', 20, 0, 40, '°'],
  ['BookshelfSelectedAngle', 0, 0, 15, '°'],
  ['BookshelfLightingIntensity', 50, 0, 100, '%'],
  ['BookshelfReactiveLighting', true]
];
function bookshelfSettings() {
  return Object.fromEntries(bookshelfSettingsSpec.map(([key, fallback, min, max]) => [key, typeof fallback === 'boolean' ? state.Settings[key] !== false : clampInt(state.Settings[key], fallback, min, max)]));
}
function applyBookshelfSettings() {
  const grid = document.getElementById('grid'), settings = bookshelfSettings(), compact = window.innerWidth <= 600;
  const width = compact ? 130 : 160, depth = compact ? 22 : 26;
  // The setting measures how far the cover turns away from a spine-on case.
  const radians = settings.BookshelfDefaultAngle * Math.PI / 180;
  const slot = Math.max(compact ? 30 : 35, Math.ceil(depth * Math.cos(radians) + width * Math.sin(radians) / (1 + width * Math.cos(radians) / 1100) + 5));
  grid.style.setProperty('--case-slot', slot + 'px');
  grid.style.setProperty('--case-angle', (90 - settings.BookshelfDefaultAngle) + 'deg');
  grid.style.setProperty('--case-selected-angle', settings.BookshelfSelectedAngle + 'deg');
  grid.style.setProperty('--case-pull', settings.BookshelfPullDistance + 'px');
  grid.style.setProperty('--case-hover-lift', settings.BookshelfHoverLift + 'px');
  grid.style.setProperty('--case-pull-duration', settings.BookshelfPullDuration + 'ms');
  grid.style.setProperty('--case-hover-duration', settings.BookshelfHoverDuration + 'ms');
  grid.style.setProperty('--cabinet-light', String(settings.BookshelfLightingIntensity / 100));
  grid.classList.toggle('bookshelfAnimationsOff', !settings.BookshelfAnimationsEnabled);
  grid.classList.toggle('bookshelfReactiveOff', !settings.BookshelfReactiveLighting);
  return {slot, settings, width, depth};
}
function openBookshelfAnimationSettings() {
  const settings = bookshelfSettings();
  bookshelfSettingsSpec.forEach(([key, fallback, min, max, unit]) => {
    const control = document.getElementById('s' + key);
    if (typeof fallback === 'boolean') control.checked = settings[key];
    else { control.value = settings[key]; document.getElementById('v' + key).textContent = settings[key] + (unit === '°' || unit === '%' ? unit : ' ' + unit); }
  });
}
function updateBookshelfAnimationSetting(control) {
  const key = control.dataset.bookshelfSetting, spec = bookshelfSettingsSpec.find(setting => setting[0] === key);
  if (!spec) return;
  state.Settings[key] = typeof spec[1] === 'boolean' ? control.checked : clampInt(control.value, spec[1], spec[2], spec[3]);
  openBookshelfAnimationSettings(); applyBookshelfSettings();
  if (['BookshelfDefaultAngle','BookshelfPullDistance'].includes(key) && isBookshelfLayout()) {
    cancelAnimationFrame(bookshelfSettingsReflow);
    bookshelfSettingsReflow = requestAnimationFrame(() => render(true));
  }
  clearTimeout(bookshelfSettingsSaveTimer);
  bookshelfSettingsSaveTimer = setTimeout(() => post({type:'saveSettings', settings:state.Settings}), 350);
}
function resetBookshelfAnimationSettings() {
  bookshelfSettingsSpec.forEach(([key, fallback]) => { state.Settings[key] = fallback; });
  clearTimeout(bookshelfSettingsSaveTimer);openBookshelfAnimationSettings();applyBookshelfSettings();
  if (isBookshelfLayout()) render(true);
  post({type:'saveSettings',settings:state.Settings});
}
function sampleBookshelfArtwork(book, image) {
  const source = image.currentSrc || image.src;
  if (bookshelfLightCache.has(source)) { book.style.setProperty('--art-light', bookshelfLightCache.get(source));requestBookshelfArtworkColor(book);return; }
  if (!image.complete || !image.naturalWidth || book.classList.contains('locked')) return;
  try {
    const canvas = document.createElement('canvas');canvas.width = canvas.height = 8;
    const context = canvas.getContext('2d', {willReadFrequently:true});context.drawImage(image, 0, 0, 8, 8);
    const pixels = context.getImageData(0, 0, 8, 8).data;let red=0,green=0,blue=0,total=0;
    for (let index=0;index<pixels.length;index+=4) {
      const high=Math.max(pixels[index],pixels[index+1],pixels[index+2]),low=Math.min(pixels[index],pixels[index+1],pixels[index+2]);
      const weight=(high-low+12)*pixels[index+3]/255;
      red+=pixels[index]*weight;green+=pixels[index+1]*weight;blue+=pixels[index+2]*weight;total+=weight;
    }
    if (!total) return;
    const rgb=[red,green,blue].map(value=>Math.max(35,Math.round(value/total))),color=rgb.join(' ');
    if (bookshelfLightCache.size>=512) bookshelfLightCache.delete(bookshelfLightCache.keys().next().value);
    bookshelfLightCache.set(source,color);book.style.setProperty('--art-light',color);
  } catch {
    // Keep remote artwork unchanged and avoid repeating a blocked pixel read on each render.
    if (bookshelfLightCache.size>=512) bookshelfLightCache.delete(bookshelfLightCache.keys().next().value);
    bookshelfLightCache.set(source,'158 157 184');
    if (bookshelfBlockedArtwork.size>=512) bookshelfBlockedArtwork.clear();
    bookshelfBlockedArtwork.add(source);requestBookshelfArtworkColor(book);
  }
}
function requestBookshelfArtworkColor(book) {
  if (!book.classList.contains('selected') || book.classList.contains('locked')) return;
  const image=book.querySelector('.shelfCaseCover img'),source=image.currentSrc||image.src,key=book.dataset.bookCase+'|'+source;
  if (bookshelfNativeColors.has(key)) { book.style.setProperty('--art-light',bookshelfNativeColors.get(key));return; }
  if (!bookshelfBlockedArtwork.has(source) || /^https?:/i.test(source) || bookshelfNativeColorRequests.has(key)) return;
  if (bookshelfNativeColorRequests.size>=512) bookshelfNativeColorRequests.clear();
  bookshelfNativeColorRequests.add(key);bookshelfPendingColorKeys.set(book.dataset.bookCase,key);post({type:'bookshelfArtworkColor',gameId:book.dataset.bookCase});
}
function applyBookshelfNativeArtworkColor(message) {
  const color=String(message.color||''),valid=/^\d{1,3} \d{1,3} \d{1,3}$/.test(color)&&color.split(' ').every(channel=>Number(channel)<=255);
  const requestedKey=bookshelfPendingColorKeys.get(message.gameId);
  if (requestedKey) {
    bookshelfPendingColorKeys.delete(message.gameId);
    if (valid) {
      if (bookshelfNativeColors.size>=512) bookshelfNativeColors.delete(bookshelfNativeColors.keys().next().value);
      bookshelfNativeColors.set(requestedKey,color);
    } else bookshelfNativeColorRequests.delete(requestedKey);
  }
  document.querySelectorAll('[data-book-case]').forEach(book=>{
    if (book.dataset.bookCase!==message.gameId) return;
    const image=book.querySelector('.shelfCaseCover img'),source=image.currentSrc||image.src,key=message.gameId+'|'+source;
    if (requestedKey && key!==requestedKey) return;
    if (!valid) { bookshelfNativeColorRequests.delete(key);return; }
    if (bookshelfNativeColors.size>=512) bookshelfNativeColors.delete(bookshelfNativeColors.keys().next().value);
    bookshelfNativeColors.set(key,color);book.style.setProperty('--art-light',color);
  });
}
function isBookshelfLayout() { return !bigPictureActive && state.Settings.LibraryLayout === 'bookshelf'; }
function bookshelfGames(localGames) {
  const query = document.getElementById('search').value.trim().toLowerCase();
  const steam = state.Settings.SteamId && ['All', 'Favorite', '__steam'].includes(currentCategory)
    ? sortLibraryGames(getSteamGames().filter(game => (currentCategory !== 'Favorite' || game.Favorite) && String(game.Name||'').toLowerCase().includes(query))) : [];
  return [...(currentCategory === '__steam' ? [] : localGames), ...steam];
}
function bookshelfGame(id) { return state.Games.find(game => game.Id === id) || findSteamGame(id); }
function bookshelfCase(game) {
  const locked = isPrivateLocked(game), name = displayName(game);
  const image = locked ? displayIcon(game) : img(game.IconPath || game.BannerPath, assets.banner);
  return `<article class="shelfCase ${game.Id === bookshelfSelectedId ? 'selected' : ''} ${locked ? 'locked' : ''}" data-book-case="${escapeHtml(game.Id)}"><button class="shelfCaseFace" data-book-action="select" data-book-id="${escapeHtml(game.Id)}" aria-label="${escapeHtml(name)}" aria-pressed="${game.Id === bookshelfSelectedId}"><span class="shelfCaseBack" aria-hidden="true"></span><span class="shelfCaseEdge" aria-hidden="true"></span><span class="shelfCaseTop" aria-hidden="true"></span><span class="shelfCaseSpine"><span>HYRO</span><b>${escapeHtml(name)}</b></span><span class="shelfCaseCover"><img loading="lazy" decoding="async" src="${escapeHtml(image)}" ${game.SteamAppId ? `data-book-steam="${game.SteamAppId}"` : ''} alt=""><span class="shelfCaseBrand">${locked ? '◇' : game.SteamAppId ? 'STEAM' : 'HYRO'}</span><strong>${escapeHtml(name)}</strong></span></button><button class="shelfCaseFavorite" data-book-action="favorite" data-book-id="${escapeHtml(game.Id)}" tabindex="${game.Id === bookshelfSelectedId ? 0 : -1}" aria-label="${escapeHtml(game.Favorite ? tr('unfavorite') : tr('favorite'))}" aria-pressed="${!!game.Favorite}">${iconHtml(game.Favorite ? assets.svgUnfavorite : assets.svgFavorite, 'small')}</button></article>`;
}
function renderBookshelfLibrary(localGames) {
  const grid = document.getElementById('grid'), games = bookshelfGames(localGames);
  if (!games.some(game => game.Id === bookshelfSelectedId)) bookshelfSelectedId = '';
  if (!games.some(game => game.Id === bookshelfPreviewId)) bookshelfPreviewId = (games.find(game => !isPrivateLocked(game)) || games[0])?.Id || '';
  const configuration = applyBookshelfSettings();
  const shelves = [];
  if (currentCategory === 'All') {
    const favorites = games.filter(game => game.Favorite && !game.Private);
    if (favorites.length) shelves.push({name:tr('favoriteTag'),category:'Favorite',games:favorites,icon:'♡'});
    const groups = new Map();
    games.filter(game => !game.Favorite || game.Private).forEach(game => {
      const category = game.Private ? 'Private' : game.SteamAppId ? '__steam' : game.Category || 'Games';
      if (!groups.has(category)) groups.set(category, []);groups.get(category).push(game);
    });
    groups.forEach((items, category) => shelves.push({name:category === 'Private' ? tr('privateTag') : category === '__steam' ? 'Steam' : category,category,games:items,icon:category === 'Private' ? '◇' : category === '__steam' ? '◉' : '▦'}));
  } else if (games.length) shelves.push({name:currentCategory === 'Favorite' ? tr('favoriteTag') : currentCategory === 'Private' ? tr('privateTag') : currentCategory === '__steam' ? 'Steam' : currentCategory,category:currentCategory,games,icon:currentCategory === 'Favorite' ? '♡' : '▦'});
  // Fixed slots keep neighboring cases still. The right-hand clearance fits the last open cover.
  const narrow=window.innerWidth<=950, medium=window.innerWidth<=1200, compact=window.innerWidth<=600;
  const wallWidth=grid.clientWidth-(narrow?0:medium?258:294),padding=compact?40:medium?52:60;
  const slot=configuration.slot,gap=3,openReach=Math.ceil((configuration.width+configuration.depth)/(1-configuration.settings.BookshelfPullDistance/1100)-slot+10);
  const columns=Math.max(1,Math.floor((wallWidth-padding-openReach+gap)/(slot+gap)));
  // Native settings/status messages may arrive while a case is moving. Reuse unchanged
  // shelf objects so replacing the DOM cannot skip the remaining pull-out animation.
  const structure=JSON.stringify([columns,configuration.width,configuration.depth,currentCategory,state.Settings.Language,
    shelves.map(shelf=>[shelf.name,shelf.category,shelf.games.map(game=>[game.Id,displayName(game),isPrivateLocked(game),displayIcon(game),game.IconPath,game.BannerPath,game.SteamAppId,!!game.Favorite])]),
    assets.logo,assets.svgFavorite,assets.svgUnfavorite,bookshelfSteamNotice()]);
  if (grid.bookshelfStructure===structure && grid.querySelector('.shelfWall')) {
    renderBookshelfDetail();return;
  }
  grid.bookshelfStructure=structure;
  const rows=items=>{const chunks=[];for(let index=0;index<items.length;index+=columns)chunks.push(items.slice(index,index+columns));return chunks.map(chunk=>`<div class="cabinetCases">${chunk.map(bookshelfCase).join('')}</div>`).join('');};
  grid.innerHTML = `<div class="shelfWall">${shelves.map((shelf,index) => `<section class="gameCabinet" aria-labelledby="cabinet-heading-${index}"><header class="cabinetHeading"><h2 id="cabinet-heading-${index}"><span>${shelf.icon}</span>${escapeHtml(shelf.name)}<small>${shelf.games.length}</small></h2>${currentCategory === 'All' ? `<button data-book-action="category" data-book-category="${escapeHtml(shelf.category)}">${igdbText('Xem tất cả','View all')} <span>↗</span></button>` : ''}</header>${rows(shelf.games)}</section>`).join('') || `<div class="cabinetEmpty">${igdbText('Không tìm thấy game.','No games found.')}</div>`}${state.Settings.SteamId && ['All','__steam'].includes(currentCategory) ? bookshelfSteamNotice() : ''}</div><aside id="bookshelfDetail" class="cabinetDetail" ${games.length ? '' : 'hidden'} aria-label="${igdbText('Game đã chọn','Selected game')}"></aside>`;
  grid.querySelectorAll('.shelfCaseCover img').forEach(image => image.addEventListener('error', () => image.dataset.bookSteam ? steamArtworkFallback(image,Number(image.dataset.bookSteam)) : (image.src = img(assets.logo)), {once:true}));
  grid.querySelectorAll('.shelfCase').forEach(book=>{
    let hash=0;for(const letter of book.dataset.bookCase)hash=(hash*31+letter.charCodeAt(0))|0;
    book.style.setProperty('--spine-hue',String(Math.abs(Math.imul(hash,2654435761))%360));
    book.style.setProperty('--case-variation', ((Math.abs(hash)%3-1)*.25)+'deg');
    book.style.setProperty('--art-light', '158 157 184');
    const logo=book.querySelector('.shelfCaseSpine>span');
    logo.innerHTML='<svg viewBox="0 0 32 36" aria-hidden="true"><text x="3" y="15" fill="currentColor" font-size="14" font-family="Segoe UI" font-weight="900">HY</text><text x="3" y="30" fill="currentColor" font-size="14" font-family="Segoe UI" font-weight="900">RO</text></svg>';
    book.querySelector('.shelfCaseFace').title=book.querySelector('.shelfCaseSpine>b').textContent;
    const image=book.querySelector('.shelfCaseCover img');
    image.addEventListener('load',()=>sampleBookshelfArtwork(book,image));sampleBookshelfArtwork(book,image);
  });
  renderBookshelfDetail();
}
function bookshelfSteamNotice() {
  const data = steamLibrary.steamId === state.Settings.SteamId ? steamLibrary : {status:'loading'};
  if (data.status === 'loading') return `<div class="cabinetNotice" role="status"><span class="cabinetSpinner"></span>${igdbText('Đang tải Steam…','Loading Steam…')}</div>`;
  if (data.status === 'error') return `<div class="cabinetNotice">${igdbText('Chưa đọc được thư viện Steam.','Steam library unavailable.')}<button data-book-action="refreshSteam">${igdbText('Làm mới','Refresh')}</button></div>`;
  return '';
}
function renderBookshelfDetail() {
  const panel = document.getElementById('bookshelfDetail'), game = bookshelfGame(bookshelfSelectedId || bookshelfPreviewId);
  if (!panel || !game) return;
  const locked = isPrivateLocked(game), name = displayName(game), running = runningIds.has(game.Id);
  const artwork = locked ? displayIcon(game) : img(game.BannerPath || game.IconPath, assets.banner);
  const category = locked ? tr('privateTag') : game.SteamAppId ? 'Steam' : game.Category || 'Games';
  const info = !locked && game.IgdbId ? igdbDetails.get(Number(game.IgdbId))?.info : null;
  if (!locked && game.IgdbId && !igdbRequested.has(Number(game.IgdbId))) {igdbRequested.add(Number(game.IgdbId));post({type:'getIgdb',gameId:game.Id});}
  panel.innerHTML = `<div class="cabinetDetailArt"><img decoding="async" src="${escapeHtml(artwork)}" alt=""></div><div class="cabinetDetailBody"><span class="cabinetDetailTag">${escapeHtml(category)}</span><h2>${escapeHtml(name)}</h2><div class="cabinetDetailFacts">${game.SteamAppId && !locked ? `<div><span>${igdbText('Thời gian chơi','Play time')}</span><strong>${(game.PlaytimeMinutes/60).toLocaleString(state.Settings.Language === 'vi' ? 'vi-VN' : 'en-US',{maximumFractionDigits:1})} ${igdbText('giờ','hours')}</strong></div>` : `<div><span>${igdbText('Loại','Type')}</span><strong>${locked ? tr('privateTag') : game.IsUrl ? igdbText('Ứng dụng web','Web app') : igdbText('Ứng dụng trên máy','Local app')}</strong></div>`}<div><span>${igdbText('Bộ sưu tập','Collection')}</span><strong>${game.Favorite ? tr('favoriteTag') : igdbText('Thư viện','Library')}</strong></div></div><div class="cabinetDetailActions"><button class="cabinetPlay" data-book-action="play" data-book-id="${escapeHtml(game.Id)}" ${running && !locked ? 'disabled' : ''}>${iconHtml(assets.svgPlayApp,'small')}${locked ? tr('unlock') : running ? igdbText('Đang chạy','Running') : game.IsUrl ? igdbText('Mở ứng dụng','Open app') : igdbText('Chơi game','Play')}</button><button class="cabinetOpen" data-book-action="details" data-book-id="${escapeHtml(game.Id)}" title="${igdbText('Chi tiết','Details')}">↗</button></div><button class="cabinetDetailFavorite" data-book-action="favorite" data-book-id="${escapeHtml(game.Id)}">${iconHtml(game.Favorite ? assets.svgUnfavorite : assets.svgFavorite,'small')}${game.Favorite ? tr('unfavorite') : tr('favorite')}</button></div>`;
  panel.querySelector('img').addEventListener('error', event => {event.target.src = img(assets.logo);},{once:true});
  if (info) {
    const meta = document.createElement('div');meta.className='cabinetDetailMeta';
    meta.innerHTML=`${[info.ReleaseDate,info.Rating!=null?'★ '+info.Rating+'/100':'',...(info.Genres||[]).slice(0,2)].filter(Boolean).map(value=>`<span>${escapeHtml(value)}</span>`).join('')}${info.Summary?`<p>${escapeHtml(info.Summary)}</p>`:''}`;
    panel.querySelector('.cabinetDetailBody h2').after(meta);
  }
}
function selectBookshelfGame(id) {
  const game = bookshelfGame(id);if (!game || !ensurePrivateAccess(game, () => selectBookshelfGame(id))) return;
  bookshelfPreviewId = id;
  setBookshelfSelection(bookshelfSelectedId === id ? '' : id);
  renderBookshelfDetail();
}
function setBookshelfSelection(id) {
  bookshelfSelectedId = id;
  document.querySelectorAll('[data-book-case]').forEach(element => {
    const active=element.dataset.bookCase===id,wasSelected=element.classList.contains('selected');
    if (active || wasSelected) clearTimeout(bookshelfClosingTimers.get(element));
    element.classList.toggle('selected',active);element.querySelector('.shelfCaseFace').setAttribute('aria-pressed',active);
    element.querySelector('.shelfCaseFavorite').tabIndex=active?0:-1;
    if (active) {element.classList.remove('closing');requestBookshelfArtworkColor(element);}
    else if (wasSelected) {
      element.classList.add('closing');
      const duration=1000*parseFloat(getComputedStyle(element.querySelector('.shelfCaseFace')).transitionDuration)||0;
      bookshelfClosingTimers.set(element,setTimeout(()=>{element.classList.remove('closing');bookshelfClosingTimers.delete(element);},duration+30));
    }
  });
}
function launchBookshelfGame(id) {
  const game = bookshelfGame(id);if (!game || !ensurePrivateAccess(game, () => launchBookshelfGame(id))) return;
  if (game.SteamAppId) {launchSteamGame(game.SteamAppId);return;}
  if (runningIds.has(id)) return;uiEffects.play('start');post({type:'launch',id});
}
document.addEventListener('click', event => {
  if (!isBookshelfLayout()) return;
  const button = event.target.closest('[data-book-action]');
  if (!button) { if (event.target.closest('.cabinetCases') && !event.target.closest('.shelfCase')) setBookshelfSelection('');return; }
  const id = button.dataset.bookId;
  if (button.dataset.bookAction === 'select') selectBookshelfGame(id);
  else if (button.dataset.bookAction === 'play') launchBookshelfGame(id);
  else if (button.dataset.bookAction === 'details') selectGame(id);
  else if (button.dataset.bookAction === 'favorite') toggleFavorite(id);
  else if (button.dataset.bookAction === 'category') selectCategory(button.dataset.bookCategory);
  else if (button.dataset.bookAction === 'refreshSteam') post({type:'refreshSteam'});
});
let bookshelfResizeTimer=0;
window.addEventListener('resize',()=>{clearTimeout(bookshelfResizeTimer);if(isBookshelfLayout())bookshelfResizeTimer=setTimeout(()=>{if(isBookshelfLayout())render(true);},120);});
