const igdbDetails = new Map(), igdbRequested = new Set();
let igdbTab = 'info', igdbCollapsed = false, igdbSearchRequest = '', igdbSearchResults = [], igdbViewerIndex = -1, igdbPreviewIndex = 0;
function igdbText(vi,en) { return state.Settings.Language === 'vi' ? vi : en; }
function igdbErrorText(code) {
  const messages = {
    MissingCredentials: ['Chưa tìm thấy Client ID/Secret trong assets.', 'Client ID/Secret files are missing.'],
    InvalidCredentials: ['Client ID/Secret không đúng định dạng.', 'Invalid Client ID/Secret format.'],
    AuthenticationFailed: ['Không xác thực được IGDB. Kiểm tra Client ID/Secret.', 'IGDB authentication failed. Check Client ID/Secret.'],
    RateLimited: ['IGDB đang giới hạn yêu cầu. Vui lòng thử lại sau.', 'IGDB rate limit reached. Please retry later.'],
    NotFound: ['Không tìm thấy game đã liên kết.', 'The linked game was not found.'],
    SearchLength: ['Tên tìm kiếm cần từ 2 đến 120 ký tự.', 'Enter 2–120 characters.'],
    ConnectionFailed: ['Không kết nối được IGDB.', 'Could not connect to IGDB.']
    ,SteamConnectionFailed: ['Không tải được dữ liệu Steam. Vui lòng thử lại sau.', 'Could not load Steam data. Please retry later.']
    ,WishlistUnavailable: ['Steam chưa trả được wishlist. Danh sách có thể trống hoặc tài khoản không cho phép truy cập; kiểm tra quyền riêng tư và API key.', 'Steam did not return a wishlist. It may be empty or inaccessible; check privacy settings and the API key.']
    ,SteamIdRequired: ['Khai báo Steam ID64 trong Cài đặt để xem wishlist.', 'Set your Steam ID64 in Settings to see your wishlist.']
  };
  return igdbText(...(messages[code] || messages.ConnectionFailed));
}
function renderGameIntel() {
  const panel = document.getElementById('gameIntel'), game = selectedGame;
  const visible = game && !game.IsUrl && !game.SteamAppId && !isPrivateLocked(game) && !document.getElementById('library').classList.contains('show');
  panel.hidden = !visible;
  if (!visible) return;
  panel.classList.toggle('collapsed',igdbCollapsed);
  const id = Number(game.IgdbId) || 0, entry = igdbDetails.get(id), info = entry && entry.info;
  const shots=info && info.Screenshots || [], preview=shots.length ? shots[igdbPreviewIndex % shots.length] : game.BannerPath || info && info.Cover;
  document.getElementById('intelVisual').innerHTML=preview ? `<button class="intelPreview" onclick="${shots.length?'openIgdbViewer('+igdbPreviewIndex % shots.length+')':'openIgdbViewer(-1)'}"><img decoding="async" src="${escapeHtml(img(preview))}" alt="${escapeHtml(game.Name)}"></button>${shots.length>1?`<div class="intelPreviewNav"><button onclick="igdbPreviewIndex=(igdbPreviewIndex+${shots.length}-1)%${shots.length};renderGameIntel();syncHeroLayoutPosition()">‹</button><span>${igdbPreviewIndex % shots.length+1} / ${shots.length}</span><button onclick="igdbPreviewIndex=(igdbPreviewIndex+1)%${shots.length};renderGameIntel();syncHeroLayoutPosition()">›</button></div>`:''}` : '';
  if (id && !igdbRequested.has(id)) { igdbRequested.add(id); post({type:'getIgdb',gameId:game.Id}); }
  document.getElementById('intelHead').innerHTML = `<button class="${igdbTab==='info'?'active':''}" onclick="igdbTab='info';renderGameIntel()">${igdbText('Thông tin','Info')}</button><button class="${igdbTab==='shots'?'active':''}" onclick="igdbTab='shots';renderGameIntel()">${igdbText('Ảnh','Screenshots')}</button><button class="intelRefresh" onclick="refreshGameIntel()" title="${igdbText('Làm mới','Refresh')}">${iconHtml(assets.svgReload,'small')}</button><button onclick="igdbCollapsed=!igdbCollapsed;renderGameIntel()" aria-label="${igdbText('Thu gọn/mở rộng','Collapse/expand')}">${igdbCollapsed?'＋':'−'}</button>`;
  let body = '';
  if (!id) body = `<p>${igdbText('Liên kết game với IGDB để xem thông tin và ảnh.','Link this game to IGDB to view details and screenshots.')}</p><button class="intelLink" onclick="openEditor(selectedGame);openIgdbSearch()">${igdbText('Liên kết IGDB','Link IGDB')}</button>`;
  else if (!info) body = `<p role="status">${entry && entry.error ? escapeHtml(igdbErrorText(entry.error)) : igdbText('Đang tải thông tin game…','Loading game details…')}</p>`;
  else if (igdbTab==='shots') body = info.Screenshots.length ? `<div class="intelShots">${info.Screenshots.map((url,i)=>`<button onclick="openIgdbViewer(${i})"><img loading="lazy" decoding="async" src="${escapeHtml(url.replace('/t_screenshot_big/','/t_screenshot_med/'))}" alt="Screenshot ${i+1}"></button>`).join('')}</div>` : `<p>${igdbText('Chưa có ảnh trên IGDB.','No screenshots available on IGDB.')}</p>`;
  else body = `<div class="intelTags">${[info.ReleaseDate,info.Rating!=null?'★ '+info.Rating+'/100':'',...info.Genres].filter(Boolean).map(value=>`<span>${escapeHtml(value)}</span>`).join('')}</div><p class="intelSummary">${escapeHtml(info.Summary || igdbText('Chưa có mô tả.','No description available.'))}</p><button class="intelLink" onclick="openIgdbViewer(-1)">${igdbText('Xem thêm','Read more')}</button>`;
  if (info && entry.error) body += `<p role="status">${escapeHtml(igdbErrorText(entry.error))} ${igdbText('Đang dùng dữ liệu đã lưu.','Showing cached details.')}</p>`;
  document.getElementById('intelBody').innerHTML = body;
  syncHeroLayoutPosition();
}
function safeHeroPlacement(stage,obstacle,x,y,width,ratio) {
  const left=stage.left+12, top=stage.top+48, right=stage.left+stage.width-12, bottom=stage.top+stage.height-12;
  const clamp=(value,min,max)=>Math.max(min,Math.min(max,value));
  width=Math.min(width,right-left,(bottom-top)/ratio);
  let height=width*ratio;
  x=clamp(x,left+width/2,right-width/2);y=clamp(y,top+height/2,bottom-height/2);
  if(obstacle && x+width/2>obstacle.left-16 && x-width/2<obstacle.right+16 && y+height/2>obstacle.top-16 && y-height/2<obstacle.bottom+16) {
    const above=Math.min(width,Math.max(0,(obstacle.top-16-top)/ratio)), beside=Math.min(width,Math.max(0,right-obstacle.right-16));
    if(above>=beside) {width=above;height=width*ratio;y=obstacle.top-16-height/2;}
    else {width=beside;height=width*ratio;x=obstacle.right+16+width/2;}
    x=clamp(x,left+width/2,right-width/2);y=clamp(y,top+height/2,bottom-height/2);
  }
  return {x,y,width};
}
function constrainHeroToIntel(title,stage) {
  const panel=document.getElementById('gameIntel');
  if(!panel || panel.hidden || document.getElementById('library').classList.contains('show'))return;
  const hero=title.querySelector('.heroLogo');
  const ratio=hero && hero.naturalWidth ? hero.naturalHeight/hero.naturalWidth : .35;
  const result=safeHeroPlacement(stage,panel.getBoundingClientRect(),parseFloat(title.style.getPropertyValue('--hero-left')),parseFloat(title.style.getPropertyValue('--hero-top')),parseFloat(title.style.getPropertyValue('--hero-width')),ratio);
  title.style.setProperty('--hero-left',result.x+'px');title.style.setProperty('--hero-top',result.y+'px');title.style.setProperty('--hero-width',result.width+'px');
}
function refreshGameIntel() {
  if (!selectedGame || !selectedGame.IgdbId) return;
  post({type:'getIgdb',gameId:selectedGame.Id,refresh:true});
}
function igdbEditorLabel() {
  document.getElementById('igdbLinkLabel').textContent = editing && editing.IgdbId ? `IGDB #${editing.IgdbId}` : igdbText('Chưa liên kết','Not linked');
}
function openIgdbSearch() {
  if (!editing) return;
  document.getElementById('igdbQuery').value = document.getElementById('fName').value || editing.Name || '';
  document.getElementById('igdbSearchModal').classList.add('show');
  document.getElementById('igdbQuery').focus();
  searchIgdbGames();
}
function closeIgdbSearch() { igdbSearchRequest=''; document.getElementById('igdbSearchModal').classList.remove('show'); }
function searchIgdbGames() {
  const query = document.getElementById('igdbQuery').value.trim();
  const results = document.getElementById('igdbResults');
  if (query.length<2 || query.length>120) { results.textContent=igdbErrorText('SearchLength'); return; }
  igdbSearchRequest = id();
  results.textContent = igdbText('Đang tìm…','Searching…');
  post({type:'searchIgdb',requestId:igdbSearchRequest,query});
}
function selectIgdbResult(index) {
  if (!editing || !igdbSearchResults[index]) return;
  editing.IgdbId=igdbSearchResults[index].Id;
  igdbEditorLabel(); closeIgdbSearch();
}
function unlinkIgdb() { if(editing) { editing.IgdbId=0;igdbEditorLabel(); } }
function handleIgdbMessage(msg) {
  if (msg.type==='igdbInfo') { igdbDetails.set(msg.id,{info:msg.info,error:msg.error}); renderGameIntel(); syncHeroLayoutPosition(); }
  if (msg.type==='igdbSearch' && msg.requestId===igdbSearchRequest && document.getElementById('igdbSearchModal').classList.contains('show')) {
    const container=document.getElementById('igdbResults');
    igdbSearchResults=msg.games || [];
    if(msg.error) { container.textContent=igdbErrorText(msg.error);return; }
    container.innerHTML=igdbSearchResults.map((game,i)=>`<button class="igdbResult" onclick="selectIgdbResult(${i})">${game.Cover?`<img loading="lazy" src="${escapeHtml(game.Cover)}" alt="">`:''}<span><strong>${escapeHtml(game.Name)}</strong><small>${escapeHtml(game.ReleaseDate || '')} · ${escapeHtml(game.Genres.join(', '))}</small></span></button>`).join('');
    if(!igdbSearchResults.length) container.textContent=igdbText('Không tìm thấy game.','No games found.');
  }
}
function openIgdbViewer(index) {
  const entry=selectedGame && igdbDetails.get(selectedGame.IgdbId), info=entry && entry.info;
  if (!info) return;
  igdbViewerIndex=index;
  const photo=index>=0 && info.Screenshots[index];
  document.getElementById('igdbViewerTitle').textContent=info.Name;
  document.getElementById('igdbViewerImage').hidden=!photo;
  document.getElementById('igdbViewerImage').src=photo || '';
  document.getElementById('igdbLongText').hidden=!!photo;
  document.getElementById('igdbLongText').textContent=[info.Summary,igdbText('Phát hành: ','Released: ')+info.ReleaseDate,igdbText('Thể loại: ','Genres: ')+info.Genres.join(', '),igdbText('Nhà phát triển: ','Developer: ')+info.Developers.join(', '),igdbText('Nhà phát hành: ','Publisher: ')+info.Publishers.join(', ')].filter(Boolean).join('\n\n');
  document.getElementById('igdbViewerPrev').hidden=!photo; document.getElementById('igdbViewerNext').hidden=!photo;
  document.getElementById('igdbViewer').classList.add('show');
  document.getElementById('igdbViewerClose').focus();
}
function moveIgdbViewer(direction) {
  const info=igdbDetails.get(selectedGame.IgdbId).info;
  if(info.Screenshots.length) openIgdbViewer((igdbViewerIndex+direction+info.Screenshots.length)%info.Screenshots.length);
}
function closeIgdbViewer() { document.getElementById('igdbViewer').classList.remove('show'); }
document.addEventListener('keydown',event=>{
  const viewer=document.getElementById('igdbViewer'), search=document.getElementById('igdbSearchModal');
  const modal=viewer.classList.contains('show') ? viewer : search.classList.contains('show') ? search : null;
  if(modal && event.key==='Tab') {
    const controls=[...modal.querySelectorAll('button, input')].filter(control=>!control.hidden && !control.disabled);
    const first=controls[0], last=controls[controls.length-1];
    if(event.shiftKey && document.activeElement===first) {event.preventDefault();last.focus();}
    else if(!event.shiftKey && document.activeElement===last) {event.preventDefault();first.focus();}
  }
  if(document.getElementById('igdbViewer').classList.contains('show')) {
    if(event.key==='Escape') closeIgdbViewer();
    if(igdbViewerIndex>=0 && event.key==='ArrowLeft') moveIgdbViewer(-1);
    if(igdbViewerIndex>=0 && event.key==='ArrowRight') moveIgdbViewer(1);
  } else if(document.getElementById('igdbSearchModal').classList.contains('show') && event.key==='Escape') closeIgdbSearch();
});
