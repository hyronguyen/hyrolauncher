let steamLibrary = { steamId: '', games: [], status: 'idle', error: '' };
function steamText(vi, en) { return state.Settings.Language === 'vi' ? vi : en; }
function getSteamGames() {
  if (steamLibrary.steamId !== state.Settings.SteamId) return [];
  return steamLibrary.games.map(game => ({
    Id: `steam-${steamLibrary.steamId}-${game.AppId}`, SteamAppId: game.AppId, SteamAccountId: steamLibrary.steamId,
    Name: game.Name, BannerPath: game.Image, IconPath: `https://cdn.akamai.steamstatic.com/steam/apps/${game.AppId}/library_600x900.jpg`,
    Category: 'Steam', IsUrl: false, Path: `steam://rungameid/${game.AppId}`, PlaytimeMinutes: game.PlaytimeMinutes,
    Favorite: (state.Settings.SteamFavorites || []).includes(`${steamLibrary.steamId}:${game.AppId}`)
  }));
}
function findSteamGame(id) { return getSteamGames().find(game => game.Id === id) || null; }
function steamArtworkFallback(image, appId) {
  image.onerror = null;
  const game = steamLibrary.games.find(game => game.AppId === appId);
  image.src = game ? game.Image : img(assets.logo);
}
function toggleSteamFavorite(game) {
  const key = `${game.SteamAccountId}:${game.SteamAppId}`;
  const favorites = new Set(state.Settings.SteamFavorites || []);
  if (favorites.has(key)) favorites.delete(key); else favorites.add(key);
  state.Settings.SteamFavorites = [...favorites];
  if (selectedGame && selectedGame.Id === game.Id) selectedGame.Favorite = favorites.has(key);
  post({ type: 'saveSettings', settings: state.Settings });
  render();
}
function landscapeLibraryCard(game) {
  const locked = isPrivateLocked(game);
  const image = locked ? displayIcon(game) : img(game.BannerPath || game.IconPath, assets.banner);
  const info = locked ? '' : game.SteamAppId ? `${(game.PlaytimeMinutes / 60).toLocaleString(state.Settings.Language === 'vi' ? 'vi' : 'en', { maximumFractionDigits: 1 })} ${steamText('giờ đã chơi', 'hours played')}` : game.Category || 'Games';
  return `<article class="card carouselCard landscapeCard steamCard ${locked ? 'privateLocked' : ''}"><button class="cardFace" onclick="selectGame('${game.Id}')"><img loading="lazy" decoding="async" src="${escapeHtml(image)}" alt="" onerror="this.onerror=null;this.src=img(assets.logo)"><div class="steamCardInfo"><strong>${escapeHtml(displayName(game))}</strong><span>${escapeHtml(info)}</span></div></button><button class="carouselFavorite" title="${escapeHtml(game.Favorite ? tr('unfavorite') : tr('favorite'))}" aria-label="${escapeHtml(game.Favorite ? tr('unfavorite') : tr('favorite'))}" onclick="toggleFavorite('${game.Id}')">${iconHtml(game.Favorite ? assets.svgUnfavorite : assets.svgFavorite, 'small')}</button></article>`;
}
function steamNavigation() {
  if (!state.Settings.SteamId) return;
  const button = document.createElement('button');
  const carousel = isCarouselLayout();
  button.className = 'cat' + (carousel ? '' : ' withIcon') + (currentCategory === '__steam' ? ' active' : '');
  if (carousel) button.textContent = 'Steam';
  else button.innerHTML = labelWithIcon('Steam', assets.svgSteam);
  button.dataset && (button.dataset.category='__steam');
  button.onclick = () => selectCategory('__steam');
  document.getElementById('categories').insertBefore(button, document.getElementById('categories').children[3] || null);
}
function renderSteamLibrary() {
  const section = document.getElementById('steamLibrary');
  if (isBookshelfLayout()) {section.hidden=true;section.innerHTML='';document.getElementById('grid').style.display=currentCategory==='__explore'?'none':'';return;}
  const configured = !!state.Settings.SteamId;
  if (!configured && currentCategory === '__steam') currentCategory = 'All';
  const visible = configured && ['All', '__steam', 'Favorite'].includes(currentCategory);
  section.hidden = !visible;
  document.getElementById('grid').style.display = ['__steam','__explore'].includes(currentCategory) ? 'none' : '';
  if (!visible) { section.innerHTML = ''; return; }
  const data = steamLibrary.steamId === state.Settings.SteamId ? steamLibrary : { games: [], status: 'loading' };
  const query = document.getElementById('search').value.trim().toLowerCase();
  const games = sortLibraryGames(getSteamGames().filter(game => (currentCategory !== 'Favorite' || game.Favorite) && String(game.Name).toLowerCase().includes(query)));
  const carousel = isCarouselLayout();
  const profile = data.profile || {};
  const avatar = profile.Avatar || img(assets.logo);
  const errors = {
    LibraryUnavailable: steamText('Không đọc được thư viện. Kiểm tra Steam ID và đặt Game details thành Public trong Steam Privacy Settings.', 'Library unavailable. Check your Steam ID and set Game details to Public in Steam Privacy Settings.'),
    MissingKey: steamText('Không tìm thấy Steam API key trong assets/apikey/steamapikey.txt.', 'Steam API key file is missing from assets/apikey/steamapikey.txt.'),
    InvalidKey: steamText('Steam API key không đúng định dạng.', 'Steam API key has an invalid format.'),
    InvalidId: steamText('Steam ID64 không hợp lệ.', 'Invalid Steam ID64.'),
    ConnectionFailed: steamText('Không kết nối được Steam API. Kiểm tra mạng và API key rồi thử lại.', 'Could not connect to Steam API. Check your connection and API key, then retry.')
  };
  const notice = data.status === 'loading' ? steamText('Đang tải thư viện Steam…', 'Loading Steam library…') : data.status === 'error' ? (errors[data.error] || errors.ConnectionFailed) : '';
  const accountHeading = `<div class="steamHeading"><img class="steamAvatar" src="${escapeHtml(avatar)}" alt="" onerror="this.onerror=null;this.src=img(assets.logo)"><div class="steamAccount"><span class="steamEyebrow">STEAM LIBRARY</span><h2>${escapeHtml(profile.Name || 'Steam games')}</h2><p>${steamText('Thư viện tài khoản', 'Account library')} · <span>${data.games.length} ${steamText('game', 'games')}</span></p></div><button class="smallAction withIcon" onclick="post({type:'refreshSteam'})" ${data.status === 'loading' ? 'disabled' : ''}>${iconHtml(assets.svgReload, 'small')}${steamText('Làm mới', 'Refresh')}</button></div>`;
  const heading = ['Favorite','Private'].includes(currentCategory) ? '<div class="carouselHeading"><h2>Steam</h2></div>' : accountHeading;
  section.innerHTML = `${heading}${notice ? `<p class="steamNotice" role="status">${escapeHtml(notice)}${data.games.length ? ' ' + steamText('Đang hiển thị bản đã lưu.', 'Showing the saved library.') : ''}</p>` : ''}<div class="steamGames ${carousel ? 'steamLandscapeGrid' : 'grid steamDefaultGrid'}">${games.map(game => carousel ? landscapeLibraryCard(game) : libraryCard(game).replace('<img loading=', `<img onerror="steamArtworkFallback(this,${game.SteamAppId})" loading=`)).join('')}</div>${!games.length && data.status === 'ready' ? `<p class="steamNotice">${query || currentCategory === 'Favorite' ? steamText('Không có game khớp bộ lọc.', 'No matching games.') : steamText('Thư viện Steam chưa có game.', 'Your Steam library is empty.')}</p>` : ''}`;
  if(carousel && state.Settings.CarouselDisplay==='all'){const cards=section.querySelector('.steamGames');cards.style.gridTemplateColumns=`repeat(${clampInt(state.Settings.CarouselColumns,5,1,10)},minmax(0,1fr))`;}
  if (!carousel) layoutGrid();
}
function launchSteamGame(appId) {
  if (steamLibrary.steamId !== state.Settings.SteamId || !steamLibrary.games.some(game => game.AppId === appId)) return;
  uiEffects.play('start');
  post({ type: 'launchSteam', appId });
}
