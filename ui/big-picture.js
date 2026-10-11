let bigPictureActive=false,bigPictureFrame=0,bigPictureNativePad={connected:false},bigPictureFocusKey='',bigPictureKeyboardTarget=null;
const bigPictureHeld=new Map();
function isCarouselLayout(){return bigPictureActive || state.Settings.LibraryLayout==='carousel';}
function controllerGlyph(action){const ps=state.Settings.ControllerGlyphs==='playstation',buttons=ps?{accept:['✕','ps-cross'],back:['○','ps-circle'],favorite:['□','ps-square'],search:['△','ps-triangle']}:{accept:['A','xbox-a'],back:['B','xbox-b'],favorite:['X','xbox-x'],search:['Y','xbox-y']};const glyph=buttons[action];return `<b class="controllerGlyph ${glyph[1]}">${glyph[0]}</b>`;}
function renderControllerHints(){
 const hints=document.querySelector('#bigPictureFooter .bigPictureHints'),ps=state.Settings.ControllerGlyphs==='playstation';
 if(hints)hints.innerHTML=`<span><b>↕ ↔</b>${igdbText('Di chuyển','Navigate')}</span><span>${controllerGlyph('accept')}${igdbText('Chọn','Select')}</span><span>${controllerGlyph('back')}${igdbText('Quay lại','Back')}</span><span>${controllerGlyph('favorite')}${igdbText('Yêu thích','Favorite')}</span><span>${controllerGlyph('search')}${igdbText('Tìm','Search')}</span><span><b class="controllerGlyph shoulder">${ps?'L1 / R1':'LB / RB'}</b>${igdbText('Chuyển mục','Tabs')}</span><span><b>☰</b>Menu</span>`;
 const nav=document.getElementById('categories');if(nav && bigPictureActive && !nav.querySelector('.controllerShoulderHints')){const hint=document.createElement('span');hint.className='controllerShoulderHints';hint.innerHTML=`<b class="controllerGlyph shoulder">${ps?'L1':'LB'}</b><b class="controllerGlyph shoulder">${ps?'R1':'RB'}</b>`;nav.insertBefore(hint,nav.firstChild);}
}
function switchBigPictureTab(direction){
 if(bigPictureModal())return;
 if(!document.getElementById('library').classList.contains('show'))showLibrary(false);
 const tabs=[...document.querySelectorAll('#categories > button[data-category]')];if(!tabs.length)return;
 const index=Math.max(0,tabs.findIndex(tab=>tab.dataset.category===currentCategory)),target=tabs[(index+direction+tabs.length)%tabs.length];target.click();
 focusBigPicture(document.querySelector('#categories > .cat.active') || bigPictureCandidates()[0]);
 document.getElementById('libraryBody').scrollTop=0;
}
function favoriteBigPictureCard(){if(bigPictureModal())return;const card=document.activeElement?.closest('.card'),face=card?.querySelector('.cardFace'),match=/selectGame\('([^']+)'\)/.exec(face?.getAttribute('onclick') || '');if(match)toggleFavorite(match[1]);else if(!document.getElementById('library').classList.contains('show') && selectedGame)toggleFavorite(selectedGame.Id);restoreBigPictureFocus();}
function toggleBigPicture(){post({type:'setBigPicture',enabled:!bigPictureActive});}
function setBigPictureUi(enabled){
 bigPictureActive=!!enabled;document.body.classList.toggle('bigPicture',bigPictureActive);
 bigPictureHeld.clear();bigPictureNativePad={connected:false};
 const footer=document.getElementById('bigPictureFooter');footer.hidden=!bigPictureActive;
 if(bigPictureActive){currentCategory='All';showLibrary(false);document.getElementById('libraryBody').scrollTop=0;bigPictureFocusKey='';requestAnimationFrame(()=>{focusBigPictureHome();document.getElementById('libraryBody').scrollTop=0;});startBigPicturePolling();}
 else {cancelAnimationFrame(bigPictureFrame);bigPictureFrame=0;closeBigPictureKeyboard();const menu=document.getElementById('bigPictureMenu');if(menu)menu.classList.remove('show');render();}
 document.getElementById('bigPictureButton').title=igdbText(bigPictureActive?'Thoát Big Picture':'Big Picture · Toàn màn hình',bigPictureActive?'Exit Big Picture':'Big Picture · Fullscreen');
 renderControllerHints();
}
function handleBigPictureMessage(msg){if(msg.type==='bigPicture')setBigPictureUi(msg.enabled);else if(msg.type==='bigPicturePad')bigPictureNativePad=msg;}
function bigPictureModal(){return [...document.querySelectorAll('.modal.show,.appDialogLayer')].filter(el=>el.getClientRects().length).pop();}
function bigPictureScope(){return bigPictureModal() || (document.getElementById('library').classList.contains('show')?document.getElementById('library'):document.querySelector('.stage'));}
function bigPictureCandidates(){const root=bigPictureScope();return [...root.querySelectorAll('button,input,select,textarea,summary,[tabindex="0"]')].filter(el=>!el.disabled && el.type!=='hidden' && el.getClientRects().length && !el.closest('[hidden]'));}
function bigPictureKey(el){return el.id || el.getAttribute('onclick') || el.getAttribute('onchange') || el.getAttribute('aria-label') || el.textContent.trim();}
function focusBigPicture(el){if(!el)return;el.focus({preventScroll:true});el.scrollIntoView({block:'nearest',inline:'nearest',behavior:'auto'});bigPictureFocusKey=bigPictureKey(el);}
function focusBigPictureHome(){const scope=bigPictureScope(),el=scope.querySelector('.carouselHero .primary,.carouselTrack .cardFace,.exploreCard,#playBtn') || bigPictureCandidates()[0];focusBigPicture(el);}
function restoreBigPictureFocus(){if(!bigPictureActive)return;const list=bigPictureCandidates();if(list.includes(document.activeElement))return;focusBigPicture(list.find(el=>bigPictureKey(el)===bigPictureFocusKey) || list[0]);}
function chooseSpatialTarget(origin,candidates,direction){
 let result=null,best=Infinity;const ox=origin.left+origin.width/2,oy=origin.top+origin.height/2;
 for(const item of candidates){const r=item.rect,dx=r.left+r.width/2-ox,dy=r.top+r.height/2-oy,along=direction==='left'?-dx:direction==='right'?dx:direction==='up'?-dy:dy,across=direction==='left' || direction==='right'?Math.abs(dy):Math.abs(dx);if(along<8)continue;
  const score=along+across*3+(across>along?across*4:0);if(score<best){best=score;result=item.element;}
 }return result;
}
function moveBigPicture(direction){
 const current=document.activeElement,list=bigPictureCandidates();if(!list.includes(current)){focusBigPicture(list[0]);return;}
 if(current.type==='range' && (direction==='left' || direction==='right')){direction==='right'?current.stepUp():current.stepDown();current.dispatchEvent(new Event('input',{bubbles:true}));current.dispatchEvent(new Event('change',{bubbles:true}));return;}
 if(current.tagName==='SELECT' && (direction==='up' || direction==='down')){current.selectedIndex=Math.max(0,Math.min(current.options.length-1,current.selectedIndex+(direction==='down'?1:-1)));current.dispatchEvent(new Event('change',{bubbles:true}));return;}
 const target=chooseSpatialTarget(current.getBoundingClientRect(),list.filter(el=>el!==current).map(element=>({element,rect:element.getBoundingClientRect()})),direction);if(target)focusBigPicture(target);
}
function bigPictureBack(){
 if(document.getElementById('bigPictureKeyboard')?.classList.contains('show')){closeBigPictureKeyboard();return;}
 const modal=bigPictureModal();
 if(modal){
  const close={bigPictureMenu:()=>modal.classList.remove('show'),exploreDetail:closeExploreDetail,appSettingsModal:closeAppSettings,modal:closeEditor,pluginModal:closePluginEditor,pinModal:closePinModal,scanModal:closeScanReview,systemModal:closeSystemInfo,watchModal:closeWatchEditor,igdbSearchModal:closeIgdbSearch,igdbViewer:closeIgdbViewer};
  if(close[modal.id])close[modal.id]();else {const cancel=modal.querySelector('.appDialogCancel,button.ghost');if(cancel)cancel.click();else modal.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));}restoreBigPictureFocus();return;
 }
 if(!document.getElementById('library').classList.contains('show')){showLibrary();focusBigPictureHome();return;}
 if(currentCategory==='__explore' && exploreMode==='dealsBrowse'){closeSteamDeals();restoreBigPictureFocus();return;}
 if(currentCategory==='__explore' && exploreMode==='search'){selectExploreMode('home');restoreBigPictureFocus();return;}
 if(currentCategory!=='All'){selectCategory('All');restoreBigPictureFocus();return;}openBigPictureMenu();
}
function activateBigPicture(){
 const el=document.activeElement;if(!el || !bigPictureCandidates().includes(el)){restoreBigPictureFocus();return;}
 if(el.id==='bigPictureKeyboardValue'){focusBigPicture(document.querySelector('.bigPictureKeys button'));return;}
 if((el.tagName==='INPUT' && !['checkbox','radio','range','color','button','submit'].includes(el.type)) || el.tagName==='TEXTAREA'){openBigPictureKeyboard(el);return;}
 if(el.tagName==='SELECT'){el.selectedIndex=(el.selectedIndex+1)%el.options.length;el.dispatchEvent(new Event('change',{bubbles:true}));return;}el.click();requestAnimationFrame(restoreBigPictureFocus);
}
function openBigPictureMenu(){
 let modal=document.getElementById('bigPictureMenu');if(!modal){modal=document.createElement('div');modal.id='bigPictureMenu';modal.className='modal';modal.setAttribute('role','dialog');modal.setAttribute('aria-modal','true');document.body.appendChild(modal);}
 modal.innerHTML=`<div class="dialog bigPictureMenuDialog"><h2>HYRO · Big Picture</h2><button class="ghost" onclick="bigPictureGo('All')">${igdbText('Thư viện game','Game library')}</button>${state.Settings.SteamId?`<button class="ghost" onclick="bigPictureGo('__steam')">Steam</button>`:''}<button class="ghost" onclick="bigPictureGo('__explore')">Explore</button><button class="ghost" onclick="document.getElementById('bigPictureMenu').classList.remove('show');openAppSettings();restoreBigPictureFocus()">${igdbText('Cài đặt','Settings')}</button><button class="ghost" onclick="toggleBigPicture()">${igdbText('Thoát Big Picture','Exit Big Picture')}</button><button class="primary" onclick="document.getElementById('bigPictureMenu').classList.remove('show');restoreBigPictureFocus()">${igdbText('Tiếp tục','Resume')}</button></div>`;
 modal.classList.add('show');focusBigPicture(modal.querySelector('button'));
}
function bigPictureGo(category){document.getElementById('bigPictureMenu').classList.remove('show');selectCategory(category);showLibrary(false);focusBigPictureHome();}
function bigPictureSearch(){if(bigPictureModal())return;showLibrary(false);const input=currentCategory==='__explore'?document.getElementById(exploreMode==='dealsBrowse'?'steamDealTerm':'exploreSearch'):document.getElementById('search');if(input)openBigPictureKeyboard(input);}
function openBigPictureKeyboard(target){
 bigPictureKeyboardTarget=target;let modal=document.getElementById('bigPictureKeyboard');if(!modal){modal=document.createElement('div');modal.id='bigPictureKeyboard';modal.className='modal';modal.setAttribute('role','dialog');modal.setAttribute('aria-modal','true');document.body.appendChild(modal);}
 const secret=target.type==='password';modal.innerHTML=`<div class="dialog bigPictureKeyboardDialog"><h2>${igdbText('Bàn phím','Keyboard')}</h2><input id="bigPictureKeyboardValue" type="${secret?'password':'text'}" maxlength="${target.maxLength>0?target.maxLength:120}" value="${escapeHtml(target.value)}" aria-label="${igdbText('Nội dung','Text')}"><div class="bigPictureKeys">${'1234567890QWERTYUIOPASDFGHJKLZXCVBNM.-:/'.split('').map(char=>`<button onclick="bigPictureType('${char}')">${char}</button>`).join('')}</div><div class="bigPictureKeyboardActions"><button class="ghost" onclick="bigPictureType(' ')">${igdbText('Dấu cách','Space')}</button><button class="ghost" onclick="bigPictureType(null)">⌫</button><button class="ghost" onclick="closeBigPictureKeyboard()">${igdbText('Hủy','Cancel')}</button><button class="primary" onclick="saveBigPictureKeyboard()">${igdbText('Xong','Done')}</button></div></div>`;
 modal.classList.add('show');focusBigPicture(modal.querySelector('.bigPictureKeys button'));
}
function bigPictureType(char){const field=document.getElementById('bigPictureKeyboardValue');if(bigPictureKeyboardTarget?.type==='number' && char!==null && !/^[0-9.\-]$/.test(char))return;field.value=char===null?field.value.slice(0,-1):(field.value+char).slice(0,field.maxLength);}
function closeBigPictureKeyboard(){const modal=document.getElementById('bigPictureKeyboard');if(modal)modal.classList.remove('show');if(bigPictureKeyboardTarget?.isConnected)focusBigPicture(bigPictureKeyboardTarget);bigPictureKeyboardTarget=null;}
function saveBigPictureKeyboard(){const target=bigPictureKeyboardTarget;if(target){target.value=document.getElementById('bigPictureKeyboardValue').value;target.dispatchEvent(new Event('input',{bubbles:true}));target.dispatchEvent(new Event('change',{bubbles:true}));}closeBigPictureKeyboard();}
function repeatBigPictureAction(action,pressed,now,callback,repeat=true){
 const hold=bigPictureHeld.get(action);if(!pressed){bigPictureHeld.delete(action);return;}if(!hold){bigPictureHeld.set(action,{next:now+350});callback();}else if(repeat && now>=hold.next){hold.next=now+145;callback();}
}
function processBigPicturePad(pad,now){
 const buttons=pad.buttons || [],pressed=i=>!!buttons[i]?.pressed,x=pad.axes?.[0] || 0,y=pad.axes?.[1] || 0;
 for(const [name,on] of [['left',pressed(14)||x<-.55],['right',pressed(15)||x>.55],['up',pressed(12)||y<-.55],['down',pressed(13)||y>.55]])repeatBigPictureAction(name,on,now,()=>moveBigPicture(name));
 for(const [name,on,callback] of [['accept',pressed(0),activateBigPicture],['back',pressed(1),bigPictureBack],['search',pressed(3),bigPictureSearch],['menu',pressed(9),openBigPictureMenu]])repeatBigPictureAction(name,on,now,callback,false);
 for(const [name,on,callback] of [['tabLeft',pressed(4),()=>switchBigPictureTab(-1)],['tabRight',pressed(5),()=>switchBigPictureTab(1)],['favorite',pressed(2),favoriteBigPictureCard]])repeatBigPictureAction(name,on,now,callback,false);
}
function nativeToGamepad(pad){const mask=pad.buttons || 0,buttons=Array.from({length:16},()=>({pressed:false}));for(const [index,flag] of [[0,4096],[1,8192],[2,16384],[3,32768],[4,256],[5,512],[9,16],[12,1],[13,2],[14,4],[15,8]])buttons[index].pressed=!!(mask&flag);return {buttons,axes:[pad.x || 0,pad.y || 0]};}
function startBigPicturePolling(){
 if(bigPictureFrame || !bigPictureActive || document.visibilityState!=='visible' || !document.hasFocus())return;const tick=now=>{
  if(!bigPictureActive){bigPictureFrame=0;return;}
  if(document.visibilityState==='visible' && document.hasFocus()){
   let pad=null;try{pad=[...(navigator.getGamepads?.() || [])].find(p=>p && p.connected && p.mapping==='standard');}catch{}
   const connected=!!pad || !!bigPictureNativePad.connected;
   document.getElementById('bigPicturePadStatus').textContent=igdbText(connected?'Tay cầm đã kết nối':'Bàn phím / Chuột · F11 thoát',connected?'Controller connected':'Keyboard / Mouse · F11 exits');
   if(connected)processBigPicturePad(pad || nativeToGamepad(bigPictureNativePad),now);else bigPictureHeld.clear();
  }else bigPictureHeld.clear();bigPictureFrame=requestAnimationFrame(tick);
 };bigPictureFrame=requestAnimationFrame(tick);
}
document.addEventListener('keydown',event=>{
 if(event.key==='F11'){event.preventDefault();toggleBigPicture();return;}if(!bigPictureActive || event.ctrlKey || event.altKey || event.metaKey)return;
 if(event.key==='PageUp' || event.key==='PageDown'){event.preventDefault();switchBigPictureTab(event.key==='PageUp'?-1:1);return;}
 if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(event.key)){event.preventDefault();event.stopImmediatePropagation();moveBigPicture(event.key.slice(5).toLowerCase());}
 else if(event.key==='Escape'){event.preventDefault();event.stopImmediatePropagation();bigPictureBack();}
 else if(event.key==='Enter' && !['INPUT','TEXTAREA'].includes(document.activeElement?.tagName)){event.preventDefault();event.stopImmediatePropagation();activateBigPicture();}
},true);
document.addEventListener('focusin',event=>{if(bigPictureActive && event.target instanceof HTMLElement)bigPictureFocusKey=bigPictureKey(event.target);});
function pauseBigPicturePolling(){cancelAnimationFrame(bigPictureFrame);bigPictureFrame=0;bigPictureHeld.clear();bigPictureNativePad={connected:false};}
window.addEventListener('blur',pauseBigPicturePolling);
window.addEventListener('focus',startBigPicturePolling);
document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='visible')startBigPicturePolling();else pauseBigPicturePolling();});
