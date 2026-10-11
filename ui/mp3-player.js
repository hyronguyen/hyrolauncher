// The native host grants playback only after launcher background music has paused.
let mp3LoadedId='', mp3Request='', mp3Pending='', mp3Playing=false, mp3Muted=false;
let mp3SearchText='', mp3ShuffleBag=[], mp3History=[], mp3AudioBound=false;
let mp3FeedbackText='';
let mp3ShufflePlayed=new Set();
const mp3Durations=new Map();
const mp3DurationRequested=new Set();
let mp3DurationBusy=false;
function musicIcon(name) {
  const paths={play:'<path d="m9 5 10 7-10 7z"/>',pause:'<path d="M8 5v14M16 5v14"/>',previous:'<path d="M5 5v14m14-14-10 7 10 7z"/>',next:'<path d="M19 5v14M5 5l10 7-10 7z"/>',shuffle:'<path d="M3 6h3c5 0 7 12 12 12h3m-4-4 4 4-4 4M3 18h3c2 0 3-2 4-4m4-4c1-2 2-4 4-4h3m-4-4 4 4-4 4"/>',repeat:'<path d="M4 9V6h15l-3-3m4 12v3H5l3 3M4 6 1 9m19 9 3-3"/>',volume:'<path d="M3 9h4l5-4v14l-5-4H3zm13-2a7 7 0 0 1 0 10m3-13a11 11 0 0 1 0 16"/>',mute:'<path d="M3 9h4l5-4v14l-5-4H3zm13 0 6 6m0-6-6 6"/>',music:'<path d="M9 18V5l12-2v13M9 8l12-2"/><ellipse cx="6" cy="18" rx="3" ry="3"/><ellipse cx="18" cy="16" rx="3" ry="3"/>',plus:'<path d="M12 5v14M5 12h14"/>',trash:'<path d="M4 6h16M9 6V3h6v3M6 6l1 15h10l1-15M10 10v7m4-7v7"/>',search:'<circle cx="10" cy="10" r="6"/><path d="m15 15 5 5"/>',stop:'<rect x="6" y="6" width="12" height="12" rx="2"/>'};
  return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name]||paths.music}</svg>`;
}
function musicTime(seconds) {
  if(!Number.isFinite(seconds)||seconds<0) return '0:00';
  const total=Math.floor(seconds); return `${Math.floor(total/60)}:${String(total%60).padStart(2,'0')}`;
}
function musicRepeat(p=plugin()) { return ['all','one'].includes(p?.Mp3RepeatMode)?p.Mp3RepeatMode:'off'; }
function musicVolume(p=plugin()) { return Math.max(0,Math.min(100,p?.Mp3Volume ?? state.Settings.BgmVolume ?? 35)); }
function currentMusicTrack() { const p=plugin(); return (p?.Mp3Tracks||[]).find(t=>t.Id===p.ActiveTrackId) || p?.Mp3Tracks?.[0]; }
function trackButtonCall(trackId) { return `decodeURIComponent('${encodeURIComponent(trackId).replace(/'/g,'%27')}')`; }
function renderMp3(p) {
  bindMusicAudio();
  audio.volume=mp3Muted?0:musicVolume(p)/100;
  const tracks=p.Mp3Tracks||[],track=currentMusicTrack(),repeat=musicRepeat(p);
  const controlsDisabled=tracks.length?'':'disabled';
  app.innerHTML=`<header class="musicHeader"><div><span class="musicEyebrow">HYRO MUSIC</span><h1>${tr('Your music','Nhạc của bạn')}</h1></div><button class="primary musicAdd" onclick="chooseTrack()">${musicIcon('plus')}<span>${tr('Add tracks','Thêm bài')}</span></button></header>
    <div class="musicBody"><section class="musicPlayer ${mp3Playing?'isPlaying':''}" aria-label="${tr('Now playing','Đang phát')}">
    <div class="musicPlayerTop"><span>${tr('NOW PLAYING','ĐANG PHÁT')}</span><span id="musicStatus" role="status"></span></div>
    <div class="musicRecord" aria-hidden="true"><div class="musicRecordLabel">${musicIcon('music')}<span>HYRO</span></div></div>
    <div class="musicSong"><h2 id="musicTitle">${esc(track?.Title||tr('Choose your first track','Chọn bài nhạc đầu tiên'))}</h2><p>${track?tr('Local library','Thư viện nhạc của bạn'):tr('Add multiple tracks and enjoy your playlist.','Thêm nhiều bài để tạo playlist của bạn.')}</p></div>
    <div class="musicSeek"><input id="musicSeek" type="range" min="0" max="1000" value="0" step="1" disabled aria-label="${tr('Seek position','Vị trí phát')}" oninput="seekMusic(this.value)"><div><span id="musicElapsed">0:00</span><span id="musicDuration">0:00</span></div></div>
    <div class="musicControls"><button class="musicIconButton ${p.Mp3Shuffle?'active':''}" onclick="toggleMusicShuffle()" aria-label="${tr('Shuffle','Trộn bài')}" aria-pressed="${!!p.Mp3Shuffle}" title="${tr('Shuffle','Trộn bài')}" ${controlsDisabled}>${musicIcon('shuffle')}</button><button class="musicIconButton" onclick="previousMusic()" aria-label="${tr('Previous track','Bài trước')}" title="${tr('Previous track','Bài trước')}" ${controlsDisabled}>${musicIcon('previous')}</button><button class="musicPlay" id="musicPlay" onclick="toggleMusic()" aria-label="${tr('Play','Phát')}" ${controlsDisabled}>${musicIcon(mp3Playing?'pause':'play')}</button><button class="musicIconButton" onclick="nextMusic()" aria-label="${tr('Next track','Bài tiếp')}" title="${tr('Next track','Bài tiếp')}" ${controlsDisabled}>${musicIcon('next')}</button><button class="musicIconButton ${repeat!=='off'?'active':''}" onclick="cycleMusicRepeat()" aria-label="${tr('Repeat','Lặp')}" title="${tr('Repeat','Lặp')}: ${repeat}" ${controlsDisabled}>${musicIcon('repeat')}${repeat==='one'?'<sup>1</sup>':''}</button></div>
    <div class="musicBottom"><button class="musicIconButton" onclick="stopMusic()" aria-label="${tr('Stop playback','Dừng phát')}" title="${tr('Stop playback','Dừng phát')}" ${controlsDisabled}>${musicIcon('stop')}</button><button class="musicIconButton" id="musicMute" onclick="toggleMusicMute()" aria-label="${tr('Mute','Tắt tiếng')}" aria-pressed="${mp3Muted}">${musicIcon(mp3Muted?'mute':'volume')}</button><input type="range" id="musicVolume" min="0" max="100" value="${musicVolume(p)}" aria-label="${tr('Volume','Âm lượng')}" oninput="setMusicVolume(this.value)" onchange="savePlugin(plugin())"><span id="musicVolumeValue">${mp3Muted?0:musicVolume(p)}%</span><span class="musicRepeatLabel">${repeat==='one'?tr('Repeat one','Lặp một bài'):repeat==='all'?tr('Repeat playlist','Lặp danh sách'):tr('Repeat off','Tắt lặp')}</span></div>
    </section><section class="musicQueue"><div class="musicQueueHeading"><h2>${tr('Playlist','Danh sách phát')}</h2><span>${tracks.length} ${tr('tracks','bài')}</span></div><label class="musicSearch">${musicIcon('search')}<input id="musicSearch" placeholder="${tr('Find a track','Tìm bài nhạc')}" value="${esc(mp3SearchText)}" oninput="updateMusicSearch(this.value)"></label><div id="musicTracks" class="musicTracks"></div><p id="musicFeedback" class="musicFeedback" role="status">${esc(mp3FeedbackText)}</p></section></div>`;
  renderMusicQueue(); syncMusicPlayback();queueMusicDurations();
}
function renderMusicQueue() {
  const host=document.getElementById('musicTracks');if(!host)return;
  const p=plugin(),tracks=(p?.Mp3Tracks||[]).filter(t=>!mp3SearchText||String(t.Title).toLowerCase().includes(mp3SearchText.toLowerCase()));
  host.innerHTML=tracks.map((t,index)=>{
    const active=t.Id===p.ActiveTrackId;
    return `<article class="musicTrack ${active?'selected':''}"><button class="musicTrackFace" onclick="playTrack(${trackButtonCall(t.Id)})" aria-label="${esc((active&&mp3Playing?tr('Pause','Tạm dừng'):tr('Play','Phát'))+' '+t.Title)}"><span class="musicTrackArt">${active&&mp3Playing?'<i></i><i></i><i></i>':musicIcon('music')}</span><span class="musicTrackText"><strong>${esc(t.Title)}</strong><small>${active?tr('Selected track','Bài đang chọn'):tr('Local audio','Nhạc trong máy')}</small></span><span class="musicTrackTime">${mp3Durations.has(t.Id)?musicTime(mp3Durations.get(t.Id)):'—:—'}</span></button><button class="musicTrackDelete" onclick="deleteTrack(${trackButtonCall(t.Id)})" aria-label="${esc(tr('Remove','Xóa')+' '+t.Title)}" title="${tr('Remove track','Xóa bài')}">${musicIcon('trash')}</button></article>`;
  }).join('')||`<div class="musicEmpty">${musicIcon('music')}<h3>${tr('No tracks found','Chưa có bài nhạc')}</h3><p>${mp3SearchText?tr('Try another search.','Thử từ khóa khác.'):tr('Add your music to start listening.','Thêm nhạc của bạn để bắt đầu nghe.')}</p></div>`;
}
function updateMusicSearch(value) { mp3SearchText=value;renderMusicQueue(); }
function queueMusicDurations() {
  if(mp3DurationBusy||typeof Audio!=='function')return;
  const track=(plugin()?.Mp3Tracks||[]).find(t=>!mp3Durations.has(t.Id)&&!mp3DurationRequested.has(t.Id)&&!/^https?:/i.test(t.Path));
  if(!track)return;
  mp3DurationBusy=true;mp3DurationRequested.add(track.Id);
  const probe=new Audio();probe.preload='metadata';
  let finished=false;
  const finish=()=>{
    if(finished)return;finished=true;clearTimeout(timeout);
    if(Number.isFinite(probe.duration)&&probe.duration>0)mp3Durations.set(track.Id,probe.duration);
    probe.onloadedmetadata=null;probe.onerror=null;probe.removeAttribute('src');probe.load();
    mp3DurationBusy=false;renderMusicQueue();queueMusicDurations();
  };
  const timeout=setTimeout(finish,8000);probe.onloadedmetadata=finish;probe.onerror=finish;
  probe.src=mediaUrl(track.Path);
}
function chooseTrack() { post({type:'chooseFile',kind:'mp3Track'}); }
function addTracks(paths) {
  const p=plugin();if(!p)return;
  const tracks=p.Mp3Tracks||(p.Mp3Tracks=[]);
  const existing=new Set(tracks.map(t=>String(t.Path).toLowerCase())),added=[];
  for(const path of paths||[]) {
    if(!path||existing.has(path.toLowerCase()))continue;
    existing.add(path.toLowerCase());const track={Id:id(),Title:fileNameNoExt(path),Path:path};tracks.push(track);added.push(track);
  }
  if(!added.length)return;
  if(!p.ActiveTrackId)p.ActiveTrackId=added[0].Id;
  mp3ShuffleBag=[];mp3ShufflePlayed=new Set(mp3LoadedId?[mp3LoadedId]:[]);savePlugin(p);renderMp3(p);
  musicFeedback(`${tr('Added','Đã thêm')} ${added.length} ${tr('tracks','bài nhạc')}`);
}
function addTrack(path) { addTracks([path]); }
function musicFeedback(message) { mp3FeedbackText=message;const node=document.getElementById('musicFeedback');if(node)node.textContent=message; }
function requestMusicPlayback() {
  mp3Request=id();mp3Pending=mp3Request;
  post({type:'mp3Playback',pluginId,playing:true,requestId:mp3Request});syncMusicPlayback();
}
function receiveMusicGrant(message) {
  if(!mp3Pending||message.requestId!==mp3Pending)return;
  const request=mp3Pending;mp3Pending='';
  audio.play().then(()=>{if(!mp3Request)audio.pause();}).catch(()=>{
    if(request!==mp3Request)return;
    audio.pause();releaseMusicPlayback();musicFeedback(tr('Cannot play this file. Check its location and format.','Không phát được bài này. Kiểm tra đường dẫn và định dạng.'));syncMusicPlayback();
  });
}
function releaseMusicPlayback() {
  const request=mp3Request;mp3Request='';mp3Pending='';mp3Playing=false;
  if(request)post({type:'mp3Playback',pluginId,playing:false,requestId:request});
}
function playTrack(trackId,force=false,recordHistory=true) {
  const p=plugin(),t=(p?.Mp3Tracks||[]).find(t=>t.Id===trackId);if(!t)return;
  musicFeedback('');
  if(!force && mp3LoadedId===trackId && (!audio.paused||mp3Pending)) { pauseMusic();return; }
  if(mp3LoadedId!==trackId) {
    if(recordHistory&&mp3LoadedId)mp3History.push(mp3LoadedId);
    audio.pause();mp3Playing=false;mp3LoadedId=trackId;audio.src=mediaUrl(t.Path);audio.load();
  } else if(force) audio.currentTime=0;
  p.ActiveTrackId=trackId;audio.volume=mp3Muted?0:musicVolume(p)/100;
  mp3ShufflePlayed.add(trackId);
  requestMusicPlayback();savePlugin(p);renderMp3(p);
}
function pauseMusic() { audio.pause();releaseMusicPlayback();syncMusicPlayback();renderMusicQueue(); }
function stopMusic() { pauseMusic();audio.currentTime=0;updateMusicProgress(); }
function toggleMusic() { const track=currentMusicTrack();if(track)playTrack(track.Id); }
function nextMusic(automatic=false) {
  const p=plugin(),tracks=p?.Mp3Tracks||[];if(!tracks.length){pauseMusic();return;}
  const current=tracks.findIndex(t=>t.Id===mp3LoadedId),repeat=musicRepeat(p);
  if(automatic&&repeat==='one') {playTrack(mp3LoadedId,true,false);return;}
  let next;
  if(p.Mp3Shuffle) {
    mp3ShuffleBag=mp3ShuffleBag.filter(trackId=>trackId!==mp3LoadedId&&tracks.some(t=>t.Id===trackId));
    if(!mp3ShuffleBag.length) {
      if(automatic&&repeat==='off'&&tracks.every(t=>mp3ShufflePlayed.has(t.Id))){pauseMusic();return;}
      if(tracks.every(t=>mp3ShufflePlayed.has(t.Id)))mp3ShufflePlayed=new Set([mp3LoadedId]);
      mp3ShuffleBag=tracks.filter(t=>t.Id!==mp3LoadedId&&!mp3ShufflePlayed.has(t.Id)).map(t=>t.Id);
      for(let i=mp3ShuffleBag.length-1;i>0;i--){const j=Math.floor(Math.random()*(i+1));[mp3ShuffleBag[i],mp3ShuffleBag[j]]=[mp3ShuffleBag[j],mp3ShuffleBag[i]];}
      if(!mp3ShuffleBag.length) {if(automatic&&repeat==='off'){pauseMusic();return;}next=tracks[0].Id;}
    }
    next=next||mp3ShuffleBag.shift();
  } else {
    if(automatic&&current===tracks.length-1&&repeat==='off'){pauseMusic();return;}
    next=tracks[(current+1)%tracks.length].Id;
  }
  playTrack(next,true);
}
function previousMusic() {
  if(audio.currentTime>3){audio.currentTime=0;updateMusicProgress();return;}
  const tracks=plugin()?.Mp3Tracks||[];if(!tracks.length)return;
  let previous;
  if(plugin().Mp3Shuffle) {while(mp3History.length&&!previous){const candidate=mp3History.pop();if(tracks.some(t=>t.Id===candidate))previous=candidate;}}
  if(!previous){const current=tracks.findIndex(t=>t.Id===mp3LoadedId);previous=tracks[(current-1+tracks.length)%tracks.length].Id;}
  playTrack(previous,true,false);
}
function cycleMusicRepeat() { const p=plugin();if(!p)return;p.Mp3RepeatMode={off:'all',all:'one',one:'off'}[musicRepeat(p)];savePlugin(p);renderMp3(p); }
function toggleMusicShuffle() { const p=plugin();if(!p)return;p.Mp3Shuffle=!p.Mp3Shuffle;mp3ShuffleBag=[];mp3History=[];mp3ShufflePlayed=new Set(mp3LoadedId?[mp3LoadedId]:[]);savePlugin(p);renderMp3(p); }
function seekMusic(value) { if(Number.isFinite(audio.duration)&&audio.duration>0)audio.currentTime=Math.max(0,Math.min(audio.duration,Number(value)/1000*audio.duration));updateMusicProgress(); }
function setMusicVolume(value) {const p=plugin();if(!p)return;p.Mp3Volume=Math.max(0,Math.min(100,Number(value)||0));mp3Muted=false;audio.volume=p.Mp3Volume/100;syncMusicVolume();}
function toggleMusicMute() {mp3Muted=!mp3Muted;audio.volume=mp3Muted?0:musicVolume()/100;syncMusicVolume();}
function syncMusicVolume() {
  const output=document.getElementById('musicVolumeValue'),mute=document.getElementById('musicMute');
  if(output)output.textContent=(mp3Muted?0:musicVolume())+'%';
  if(mute){mute.innerHTML=musicIcon(mp3Muted?'mute':'volume');mute.setAttribute('aria-pressed',String(mp3Muted));}
}
function updateMusicProgress() {
  const seek=document.getElementById('musicSeek'),elapsed=document.getElementById('musicElapsed'),duration=document.getElementById('musicDuration');
  const seconds=Number.isFinite(audio.duration)?audio.duration:0;
  if(seek){seek.disabled=!seconds;seek.value=String(seconds?Math.round(audio.currentTime/seconds*1000):0);seek.style.setProperty('--progress',(seconds?audio.currentTime/seconds*100:0)+'%');}
  if(elapsed)elapsed.textContent=musicTime(audio.currentTime);if(duration)duration.textContent=musicTime(seconds);
}
function syncMusicPlayback() {
  const player=document.querySelector('.musicPlayer'),button=document.getElementById('musicPlay'),status=document.getElementById('musicStatus');
  if(player)player.classList.toggle('isPlaying',mp3Playing);
  if(button){button.innerHTML=musicIcon(mp3Playing?'pause':'play');button.setAttribute('aria-label',mp3Playing?tr('Pause','Tạm dừng'):tr('Play','Phát'));}
  if(status)status.textContent=mp3Pending?tr('Loading…','Đang tải…'):mp3Playing?tr('Playing','Đang phát'):tr('Paused','Tạm dừng');
  updateMusicProgress();
}
function bindMusicAudio() {
  if(mp3AudioBound)return;mp3AudioBound=true;
  audio.addEventListener('play',()=>{mp3Playing=true;syncMusicPlayback();renderMusicQueue();});
  audio.addEventListener('pause',()=>{mp3Playing=false;syncMusicPlayback();});
  audio.addEventListener('timeupdate',updateMusicProgress);
  audio.addEventListener('loadedmetadata',()=>{if(Number.isFinite(audio.duration)&&mp3LoadedId)mp3Durations.set(mp3LoadedId,audio.duration);updateMusicProgress();renderMusicQueue();});
  audio.addEventListener('ended',()=>nextMusic(true));
  audio.addEventListener('error',()=>{audio.pause();releaseMusicPlayback();syncMusicPlayback();musicFeedback(tr('Audio unavailable. Choose another file or add it again.','Không đọc được file nhạc. Chọn bài khác hoặc thêm lại file.'));});
  window.addEventListener('pagehide',()=>{audio.pause();releaseMusicPlayback();});
  document.addEventListener('keydown',event=>{
    if(plugin()?.Type!=='mp3'||/INPUT|TEXTAREA|SELECT|BUTTON/.test(event.target?.tagName)||event.ctrlKey||event.altKey||event.metaKey)return;
    if(event.code==='Space'){event.preventDefault();toggleMusic();}
    else if(event.key==='ArrowRight'){event.preventDefault();seekMusic((audio.currentTime+5)/(audio.duration||1)*1000);}
    else if(event.key==='ArrowLeft'){event.preventDefault();seekMusic((audio.currentTime-5)/(audio.duration||1)*1000);}
  });
}
async function deleteTrack(trackId) {
  const p=plugin();if(!p||!await appConfirm(tr('Remove this track from your playlist?','Xóa bài này khỏi danh sách phát?')))return;
  p.Mp3Tracks=(p.Mp3Tracks||[]).filter(t=>t.Id!==trackId);
  if(mp3LoadedId===trackId){stopMusic();mp3LoadedId='';audio.removeAttribute('src');audio.load();}
  if(p.ActiveTrackId===trackId)p.ActiveTrackId=p.Mp3Tracks[0]?.Id||'';
  mp3ShuffleBag=mp3ShuffleBag.filter(t=>t!==trackId);mp3History=mp3History.filter(t=>t!==trackId);
  savePlugin(p);renderMp3(p);
}
