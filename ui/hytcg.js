'use strict';
const tcg={session:crypto.randomUUID().replaceAll('-',''),config:null,wallet:null,walletStamp:0,walletReceived:0,walletPending:false,language:'vi',slots:[],packError:'',busy:false,purchasing:false,dissolving:false,upgrading:false,collection:[],collectionTotal:0,hasMore:false,collectionRequest:'',pending:new Map(),cardsDirectory:'',request:0,view:'booster',detail:null,payment:'credits',favoritesOnly:false,selected:new Map(),favoriteBusy:new Set(),upgrade:null,upgradeRecoveries:[]};
Object.assign(tcg,{banners:[],missions:[],missionDay:'',discoveryPending:false,discoveryReceived:0,dex:{characters:[],series:[],query:'',mode:'character',offset:0,hasMore:false,pending:false,request:0,seriesId:0},dexCharacter:null,artworks:[],deleting:false,limitedBusy:false});
const $=id=>document.getElementById(id);
const reducedMotion=matchMedia('(prefers-reduced-motion: reduce)');
function tx(en,vi){return tcg.language==='vi'?vi:en;}
function escapeText(value){return String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));}
function send(type,extra={}){chrome.webview.postMessage({type,session:tcg.session,...extra});}
function nextRequest(){return 'tcg_'+(++tcg.request)+'_'+Date.now().toString(36);}
const errors={
  AcdbUnavailable:['Character data is unavailable. Try again later.','Dữ liệu nhân vật đang không khả dụng. Hãy thử lại sau.'],
  AcdbRateLimited:['The character service is busy. Please wait.','Dịch vụ nhân vật đang giới hạn lượt gọi. Hãy đợi một chút.'],
  AcdbBrowserCheck:['The character service requires a browser check. Try again later.','Dịch vụ nhân vật đang yêu cầu kiểm tra trình duyệt. Hãy thử lại sau.'],
  AcdbClientUnavailable:['Windows curl is unavailable. Check your Windows installation.','Không tìm thấy curl của Windows. Hãy kiểm tra cài đặt Windows.'],
  CharacterNotFound:['No valid character found after the retry limit.','Chưa tìm được nhân vật hợp lệ sau giới hạn thử lại.'],
  ProfileUnavailable:['The character image could not be loaded.','Không tải được ảnh nhân vật.'],
  ArtworkInvalid:['This artwork could not be decoded.','Không đọc được ảnh artwork.'],
  ConfigInvalid:['The local configuration is invalid.','Cấu hình trên máy không hợp lệ.'],
  UsernameInvalid:['Check the local artwork account configuration.','Hãy kiểm tra cấu hình tài khoản artwork trên máy.'],
  SaveFailed:['Could not save. Check the cards folder and try Save again.','Chưa lưu được. Kiểm tra thư mục cards rồi nhấn Lưu lại.'],
  CardInvalid:['The rendered card could not be saved.','Không lưu được ảnh card đã dựng.'],
  TimedOut:['The request timed out. Try this card again.','Đã quá thời gian chờ. Bạn có thể thử lại card này.'],
  Busy:['Wait for your current action to finish.','Hãy đợi thao tác hiện tại hoàn tất.'],
  NoCredits:['Not enough credits. Wait for a refill or open with gems.','Chưa đủ lượt. Hãy đợi hồi lượt hoặc mở bằng gems.'],
  InsufficientGems:['Not enough gems for this opening.','Chưa đủ gems để mở số card này.'],
  WalletInvalid:['Your local wallet could not be verified.','Không xác thực được ví trên máy của bạn.'],
  CardTampered:['This card could not be verified and cannot be dissolved.','Card không xác thực được nên không thể phân giải.'],
  CardAlreadyDissolved:['This card has already been dissolved.','Card này đã được phân giải.'],
  LegacyCard:['This earlier card is display-only and cannot be dissolved.','Card phiên bản trước chỉ để xem, không thể phân giải.'],
  TokenInvalid:['This opening credit is no longer valid. Please try again.','Lượt mở này không còn hợp lệ. Hãy thử lại.'],
  RecoveryPending:['Your card is ready to resume. No extra credit is required.','Tiếp tục nhận card đã mở. Không tốn thêm lượt.'],
  MaterialsInsufficient:['Choose 10 cards of the target rarity, or 1 of the same character.','Chọn 10 card đúng độ hiếm đích, hoặc 1 card cùng nhân vật.'],
  MaterialInvalid:['A selected material is no longer available. Refresh and choose again.','Nguyên liệu đã chọn không còn khả dụng. Hãy làm mới và chọn lại.'],
  UpgradeInvalid:['This upgrade is no longer valid.','Lần nâng cấp này không còn hợp lệ.'],
  UpgradeLocked:['This card belongs to an unfinished upgrade. Resume or cancel it first.','Card đang dùng cho một lần nâng cấp. Hãy tiếp tục hoặc hủy trước.'],
  UpgradeUnavailable:['This card cannot be upgraded.','Card này không thể nâng cấp.'],
  UpgradeMax:['UR is the highest rarity.','UR là độ hiếm cao nhất.'],
  UpgradePending:['Resume your unfinished upgrade first.','Hãy tiếp tục lần nâng cấp đang chờ.'],
  ConnectionFailed:['Connection failed. Try again.','Lỗi kết nối. Hãy thử lại.']
};
Object.assign(errors,{"SaveFailed":["Save failed","Lưu thất bại"],"Busy":["Working…","Đang xử lý…"],"NoCredits":["Not enough credits","Chưa đủ lượt"],"TimedOut":["Request timed out","Hết thời gian chờ"],"RecoveryPending":["Resume card","Tiếp tục nhận thẻ"],"UpgradeLocked":["Upgrade pending","Đang nâng cấp"],"UpgradePending":["Upgrade pending","Còn nâng cấp đang chờ"],"TokenInvalid":["Invalid opening","Lượt mở không hợp lệ"],"ConnectionFailed":["Connection failed","Lỗi kết nối"]});
errors.UpgradeMaterialsInvalid=errors.MaterialsInsufficient;errors.CardLocked=errors.UpgradeLocked;
Object.assign(errors,{SearchInvalid:['Invalid search','Từ khóa tìm kiếm không hợp lệ'],BannerExpired:['This banner has ended','Banner đã kết thúc'],SeriesNotFound:['Series not found','Chưa tìm được series'],ConfirmationRequired:['Type delete to confirm','Nhập đúng delete để xác nhận'],AniListUnavailable:['Banners unavailable','Chưa tải được banner'],LimitedCreditsInsufficient:['Not enough limited credits','Chưa đủ lượt giới hạn'],CreditsLimit:['Credit limit reached','Đã đạt giới hạn lượt'],GemsLimit:['Gem limit reached','Đã đạt giới hạn gems'],GameUnavailable:['Game unavailable','Không mở được game']});
function errorText(code){return tx(...(errors[code]||errors.ConnectionFailed));}
let toastTimer,searchTimer;
function toast(text){$('toast').textContent=text;$('toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').hidden=true,6500);}
function call(type,extra={},key){
  return new Promise((resolve,reject)=>{
    const requestId=key||nextRequest(),timeout=['hytcgDiscoveryState','hytcgDissolvePreview','hytcgDissolve','hytcgDissolveMany'].includes(type)?125000:type==='hytcgWallet'?12000:['hytcgSave','hytcgUpgradeSave','hytcgFavorite'].includes(type)?30000:70000;
    const timer=setTimeout(()=>{tcg.pending.delete(requestId);reject(Error('TimedOut'));},timeout);
    tcg.pending.set(requestId,{resolve,reject,timer});send(type,{requestId,...extra});
  });
}
function finish(key,msg){const pending=tcg.pending.get(key);if(!pending)return;clearTimeout(pending.timer);tcg.pending.delete(key);if(msg.error){const error=Error(msg.error);error.recoverable=msg.recoverable===true;error.payload=msg;pending.reject(error);}else pending.resolve(msg);}
function phaseText(slot){
  return '';
}
function bindTheme(settings={}){
  tcg.language=settings.Language||tcg.language;
  const accent=/^#[0-9a-f]{6}$/i.test(settings.AccentColor||'')?settings.AccentColor:'#c49bf7';document.documentElement.style.setProperty('--accent',accent);
  document.body.classList.toggle('theme-light',settings.ThemeMode==='light');document.documentElement.lang=tcg.language;translate();
}
function translate(){
  $('tagline').textContent='';
  $('dexTab').textContent='CharacterDex';$('missionsTab').textContent=tx('Daily','Hằng ngày');
  $('limitedTitle').textContent=tx('Limited banners','Banner giới hạn');$('limitedEyebrow').textContent=tx('DAILY SPOTLIGHT','SERIES HÔM NAY');$('buyLimited').title=tx('Buy 1 limited credit · 180 gems','Mua 1 lượt giới hạn · 180 gems');
  $('dexTitle').textContent=tx('Discover characters','Khám phá nhân vật');$('dexSearch').placeholder=tx('Character or series','Tên nhân vật hoặc series');$('dexMode').options[0].textContent=tx('Character','Nhân vật');$('dexSubmit').textContent=tx('Search','Tìm');$('dexMore').textContent=tx('Load more','Xem thêm');$('missionsTitle').textContent=tx('Daily missions','Nhiệm vụ hằng ngày');
  $('dexArtworkMore').textContent=tx('Load more','Xem thêm');
  $('accountLabel').textContent=tx('Account','Tài khoản');$('resetAccount').textContent=$('deleteTitle').textContent=$('confirmDelete').textContent=tx('Delete account','Xóa tài khoản');$('deleteWarning').textContent=tx('All cards and progress will be deleted. The new account starts with 1,800 gems.','Toàn bộ thẻ và tiến trình sẽ bị xóa. Tài khoản mới nhận 1.800 gems.');$('deleteLabel').textContent=tx('Type delete to confirm','Nhập delete để xác nhận');$('deleteCancel').textContent=tx('Cancel','Hủy');
  $('packTitle').textContent='Booster';
  $('packDescription').textContent='';$('packHint').textContent='';
  $('freeLabel').textContent=tx('Free','Miễn phí');$('paidLabel').textContent=tx('Stored','Đã tích');$('buyLabel').textContent=tx('Open x1','Mở x1');
  $('payCredits').textContent=tx('Credits','Lượt mở');$('payGems').textContent='◆ Gems';
  $('resultsHeading').textContent=tx('Your latest pulls','Card vừa mở');
  if($('firstTitle'))$('firstTitle').textContent=tx('No cards','Chưa có thẻ');
  if($('firstDescription'))$('firstDescription').textContent='';
  $('collectionTitle').textContent=tx('Collection','Bộ sưu tập');$('collectionEyebrow').textContent='';
  $('refreshCollection').querySelector('span').textContent=tx('Refresh','Làm mới');
  $('collectionSearch').placeholder=tx('Character or series','Nhân vật hoặc series');$('rarityFilter').options[0].textContent=tx('All rarities','Mọi độ hiếm');$('collectionMore').textContent=tx('Load more','Xem thêm');
  [...$('collectionSort').options].forEach((option,index)=>option.textContent=[tx('Newest','Mới nhất'),tx('Rarity','Độ hiếm'),tx('Name','Tên A–Z')][index]);$('expandPanel').title=tx('Expand / restore panel','Mở rộng / thu gọn panel');$('expandPanel').setAttribute('aria-label',$('expandPanel').title);$('resetFilters').title=tx('Clear filters','Xóa bộ lọc');
  $('favoritesFilter').textContent=tx('♡ Favorites','♡ Yêu thích');$('selectVisible').textContent=tx('Select visible','Chọn tất cả');$('clearSelection').textContent=tx('Clear','Bỏ chọn');$('dissolveSelected').textContent=tx('◆ Dissolve','◆ Phân giải');
  $('settingsTitle').textContent=tx('Pack rules','Quy tắc mở card');$('rarityHeading').textContent=tx('Rarity','Độ hiếm');$('ratesHeading').textContent=tx('Chance','Tỷ lệ');$('salvageHeading').textContent=tx('Dissolve','Phân giải');
  $('rulesDescription').textContent=tx('20 min · 5 free credits · 160 gems/card','20 phút · 5 lượt miễn phí · 160 gems/card');
  $('rulesHint').textContent='SR+ 50 · SSR+ 100 · UR 500';
  $('batchTitle').textContent=tx('Dissolve cards','Phân giải thẻ');$('batchWarning').textContent=tx('Permanently removes these cards.','Xóa vĩnh viễn các thẻ đã chọn.');$('batchCancel').textContent=tx('Cancel','Hủy');$('confirmBatchDissolve').textContent=tx('Dissolve','Phân giải');
  $('upgradeTitle').textContent=tx('Upgrade card','Nâng cấp card');$('materialSame').textContent=tx('1 matching character','1 card cùng nhân vật');$('materialTen').textContent=tx('10 cards','10 card');$('materialSearch').placeholder=tx('Find materials','Tìm nguyên liệu');$('changeArtworkLabel').textContent=tx('Try a new artwork','Thử artwork mới');$('cancelUpgrade').textContent=tx('Cancel','Hủy');$('resumeUpgrade').textContent=tx('Resume upgrade','Tiếp tục nâng cấp');
  $('settingsCancel').textContent=tx('Got it','Đã hiểu');$('settingsButton').title=tx('Pack rules','Quy tắc mở card');$('settingsButton').setAttribute('aria-label',$('settingsButton').title);$('folderButton').title=tx('Open cards folder','Mở thư mục cards');
  const tabIcons={boosterTab:['Booster','Mở thẻ','M6 3h12v18H6zM9 7h6m-3 4v6m-3-3h6'],collectionTab:['Collection','Bộ sưu tập','M4 4h6v7H4zM14 4h6v7h-6zM4 15h6v5H4zM14 15h6v5h-6z'],dexTab:['CharacterDex','CharacterDex','M4 4h7v16H4zM13 4h7v16h-7zM7 8h1m8 0h1M7 12h1m8 0h1'],missionsTab:['Daily','Hằng ngày','M5 5h14v16H5zM8 3v4m8-4v4M8 13l3 3 5-6']};
  for(const [id,[en,vi,path]] of Object.entries(tabIcons)){const tab=$(id);tab.title=tx(en,vi);tab.setAttribute('aria-label',tab.title);tab.classList.add('tcgIconTab');tab.innerHTML='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round" stroke-linecap="round" aria-hidden="true"><path d="'+path+'"/></svg>'+(id==='collectionTab'?'<span id="collectionCount">'+tcg.collectionTotal+'</span>':'');}
  syncCardEffects();renderWallet();renderSelection();renderUpgradeRecoveries();if(tcg.slots.length)renderSlots();window.HyTcgLifestyle?.translate();
}
function renderConfig(){
  const config=tcg.config;if(!config)return;$('rarityLegend').replaceChildren();
  for(const rate of config.DropRates){const span=document.createElement('span'),dot=document.createElement('i');dot.style.setProperty('--rarity',rate.Color);span.append(dot,document.createTextNode(`${rate.Name} ${rate.Weight}%`));$('rarityLegend').append(span);}
  const current=$('rarityFilter').value;$('rarityFilter').innerHTML=`<option value="">${tx('All rarities','Tất cả rarity')}</option>`+config.DropRates.map(rate=>`<option value="${escapeText(rate.Name)}">${escapeText(rate.Name)}</option>`).join('');$('rarityFilter').value=current;updateButtons();
}
function applyWallet(wallet){
  if(!wallet)return;const stamp=Date.parse(wallet.ServerNow)||Date.now();if(tcg.wallet&&stamp<tcg.walletStamp)return;
  tcg.wallet=wallet;tcg.walletStamp=stamp;tcg.walletReceived=performance.now();renderWallet();
}
function renderWallet(){
  const wallet=tcg.wallet;
  $('gemBalance').textContent=wallet?Number(wallet.Gems).toLocaleString(tcg.language==='vi'?'vi-VN':'en-US'):'—';
  $('freeBalance').textContent=wallet?`${wallet.FreeCredits}/${wallet.MaxFreeCredits||5}`:'—';$('paidBalance').textContent=wallet?wallet.PaidCredits:'—';
  $('limitedBalance').textContent=Number(wallet?.LimitedCredits||0).toLocaleString(tcg.language==='vi'?'vi-VN':'en-US');
  $('buyPull').title=tx('Open one card directly with 160 gems','Mở ngay 1 card bằng 160 gems');
  if(wallet){
    const full=wallet.FreeCredits>=(wallet.MaxFreeCredits||5),remaining=full?0:Math.max(0,Math.ceil((Date.parse(wallet.NextFreeAt)-(tcg.walletStamp+performance.now()-tcg.walletReceived))/1000));
    $('refillLabel').textContent=full?tx('Free credits','Lượt miễn phí'):tx('Next free card','Hồi lượt sau');
    $('refillCountdown').textContent=full?tx('Full','Đã đầy'):Number.isFinite(remaining)?`${String(Math.floor(remaining/60)).padStart(2,'0')}:${String(remaining%60).padStart(2,'0')}`:'—';
    if(!full&&remaining===0&&!document.hidden)refreshWallet();
  }renderPity();updateButtons();updateLimitedButtons();window.HyTcgLifestyle?.renderBooster();window.HyTcgDev?.wallet();
}
function renderPity(){
  const stamp=[tcg.language,tcg.wallet?.PitySR,tcg.wallet?.PitySSR,tcg.wallet?.PityUR].join(':');
  if(tcg.pityRendered===stamp)return;tcg.pityRendered=stamp;
  const tiers=[['SR',50,'PitySR'],['SSR',100,'PitySSR'],['UR',500,'PityUR']];
  $('pityTrack').innerHTML=tiers.map(([rarity,limit,key])=>{const misses=Math.max(0,Math.min(limit-1,Number(tcg.wallet?.[key])||0)),remaining=limit-misses;return `<div class="pityItem" data-pity="${rarity}" title="${escapeText(tx(`Guaranteed ${rarity}+ within ${remaining} pulls`,`${rarity}+ chắc chắn trong ${remaining} lượt`))}"><div><strong>${rarity}${rarity==='UR'?'':'+'}</strong><span>${misses}<small> / ${limit}</small></span></div><span class="pityLine"><i></i></span></div>`;}).join('');
  tiers.forEach(([,limit,key],index)=>$('pityTrack').children[index].querySelector('i').style.width=Math.max(0,Math.min(limit-1,Number(tcg.wallet?.[key])||0))/limit*100+'%');
}
function setPayment(payment){tcg.payment=payment==='gems'?'gems':'credits';updateButtons();}
async function refreshWallet(){
  if(tcg.walletPending||!tcg.config)return;tcg.walletPending=true;
  try{await call('hytcgWallet');}catch(error){if(error.message==='WalletInvalid')toast(errorText(error.message));}finally{tcg.walletPending=false;}
}
function updateButtons(){
  const credits=tcg.wallet?Number(tcg.wallet.FreeCredits)+Number(tcg.wallet.PaidCredits):0,gems=Number(tcg.wallet?.Gems)||0,gemMode=tcg.payment==='gems',blocked=tcg.busy||tcg.purchasing||tcg.dissolving||tcg.upgrading||tcg.deleting||tcg.limitedBusy||!tcg.config||!tcg.wallet;
  const price=tcg.wallet?.PackPrice||160;$('openOne').disabled=blocked||(gemMode?gems<price:credits<1);$('openTen').disabled=blocked||(gemMode?gems<price*10:credits<10);$('buyPull').disabled=blocked||gems<price;
  $('oneCost').textContent=gemMode?'◆ '+price:tx('1 credit','1 lượt');$('tenCost').textContent=gemMode?'◆ '+(price*10).toLocaleString(tcg.language==='vi'?'vi-VN':'en-US'):tx('10 credits','10 lượt');
  for(const [id,mode]of[['payCredits','credits'],['payGems','gems']]){$(id).classList.toggle('active',tcg.payment===mode);$(id).setAttribute('aria-pressed',String(tcg.payment===mode));$(id).disabled=blocked;}
  if($('dissolveButton'))$('dissolveButton').disabled=blocked||tcg.detail?.infoLoading===true;
  if($('confirmDissolve'))$('confirmDissolve').disabled=blocked;
  if($('cancelDissolve'))$('cancelDissolve').disabled=tcg.dissolving;
  $('openOne').title=(gemMode?gems<price:credits<1)?errorText(gemMode?'InsufficientGems':'NoCredits'):'';$('openTen').title=(gemMode?gems<price*10:credits<10)?tx(gemMode?'1,600 gems required':'10 credits required',gemMode?'Cần đủ 1.600 gems':'Cần đủ 10 lượt mở'):'';
  const completed=tcg.slots.filter(slot=>['ready','error','dissolved','compensated'].includes(slot.status)).length,successes=tcg.slots.filter(slot=>slot.status==='ready').length;
  $('packProgress').textContent=tcg.slots.length?(tcg.busy?`${completed}/${tcg.slots.length} · ${tx('Opening','Đang mở')}`:`${successes}/${tcg.slots.length} ${tx('cards','card')}`):'';renderSelection();updateLimitedButtons();
}
function localImage(path){if(/^data:image\/(png|webp|jpeg|gif);base64,/i.test(String(path||'')))return String(path);return new URL('file:///'+String(path||'').replaceAll('\\','/')).href;}
function itemImage(item){return localImage(item?.Thumbnail||item?.ImageData||item?.ImagePath||'');}
function legacyMask(card){return `<span class="legacySourceMask" aria-hidden="true">${card.ImageSource==='zerochan'?'ALT':''}</span>`;}
function cardMarkup(card,src,index,collection=false,legacy=false){
  const high=HyTcgRenderer.isHighRarity(card.Rarity);
  return `<button class="cardVisual revealed ${high?'isRare':''} " data-tilt data-rarity="${escapeText(card.Rarity)}" data-detail="${index}" data-collection="${collection}" aria-label="${escapeText(card.Name+' · '+card.Rarity)}"><span class="cardSurface"><img src="${escapeText(src)}" alt="${escapeText(card.Name+' · '+card.Series+' · '+card.Rarity)}" loading="lazy">${legacy?legacyMask(card):''}<span class="cardShine" aria-hidden="true"></span><span class="cardGlare" aria-hidden="true"></span></span></button>`;
}
function variantLabel(card,cosmetic){return card.ImageSource==='zerochan'&&!cosmetic?.ArtworkId?'<span class="altLabel">ALT</span>':'<span></span>';}
function decorateLifestyleCards(){
  const decorate=window.HyTcgLifestyle?.decorate;if(!decorate)return;
  for(const node of $('results').querySelectorAll('.cardVisual')){const slot=tcg.slots[Number(node.dataset.detail)];if(slot?.card)decorate(node,slot.card.CardId,slot.cosmetic);}
  for(const node of $('collectionGrid').querySelectorAll('.cardVisual')){const item=tcg.collection[Number(node.dataset.detail)];if(item?.Card)decorate(node,item.Card.CardId,item.Cosmetic);}
}
function renderSlots(){
  if(!tcg.slots.length)return;
  if($('results').children.length!==tcg.slots.length||$('firstPull'))$('results').replaceChildren(...tcg.slots.map(()=>{const node=document.createElement('article');node.className='cardTile';return node;}));
  tcg.slots.forEach((slot,index)=>{
    let markup;
    if(slot.status==='ready')markup=`${cardMarkup(slot.card,slot.png,index)}<div class="cardCaption">${variantLabel(slot.card,slot.cosmetic)}${slot.saved?`<span class="savedLabel">✓ ${tx('Saved','Đã lưu')}</span>`:slot.saving?`<span>${tx('Saving…','Đang lưu…')}</span>`:`<button class="saveCardButton" data-save="${index}">${tx('Save','Lưu')}</button>`}</div>${slot.saveError?`<p class="inlineError">${escapeText(errorText(slot.saveError))}</p>`:''}`;
    else if(slot.status==='dissolved')markup=`<div class="pendingCard dissolvedCard"><span class="cardBackStar">◆</span><span class="phase">${tx('Card dissolved','Đã phân giải card')}</span></div>`;
    else if(slot.status==='compensated')markup=`<div class="pendingCard compensatedCard"><span class="compensationGem">◆</span><strong>+${Number(slot.gemsAwarded)||160} gems</strong><span>${tx('No character found','Không tìm được nhân vật')}</span></div>`;
    else{const failed=slot.status==='error';markup=`<div class="pendingCard ${failed?'failedCard':slot.status==='loading'?'loading':''}" ${!failed?'role="status" aria-label="'+tx('Opening card','Đang mở thẻ')+'"':''}><span class="slotNumber">${String(index+1).padStart(2,'0')} / ${String(tcg.slots.length).padStart(2,'0')}</span>${failed?'<span class="cardBackStar">◇</span>':'<span class="openingSpinner" aria-hidden="true"></span>'}${slot.issued&&slot.cardNative?`<strong class="recoveryName">${escapeText(slot.cardNative.Name)}</strong><span class="recoveryRarity">${escapeText(slot.cardNative.Rarity)}</span>`:''}<span class="phase" id="phase-${index}">${escapeText(failed?errorText(slot.error):'')}</span>${failed?`<button class="retryButton" data-retry="${index}" ${tcg.busy?'disabled':''}>${slot.issued?tx('Resume card','Tiếp tục nhận card'):tx('Try again · 1 credit','Thử lại · 1 lượt')}</button>`:''}</div>`;}
    const node=$('results').children[index];if(node._markup!==markup){node.innerHTML=markup;node._markup=markup;}if(slot.card)node.style.setProperty('--rarity',slot.card.RarityColor);
  });decorateLifestyleCards();updateButtons();
}
async function saveSlot(index){
  const slot=tcg.slots[index];if(!slot?.png||slot.saving||slot.saved)return;slot.saving=true;slot.saveError='';renderSlots();
  try{const saved=await call('hytcgSave',{cardId:slot.card.CardId,png:slot.png},slot.card.CardId);slot.saved=true;slot.path=saved.path;tcg.collectionTotal++;$('collectionCount').textContent=tcg.collectionTotal;}
  catch(error){slot.saveError=error.message;}finally{slot.saving=false;renderSlots();}
}
async function openSlot(index,token){
  const slot=tcg.slots[index];slot.token=token;slot.status='loading';slot.phase='character';slot.attempt=0;slot.error='';slot.requestId=nextRequest();renderSlots();
  try{
    let response=await call('hytcgOpen',{token},slot.requestId);
    if(response.compensated){slot.status='compensated';slot.gemsAwarded=response.gemsAwarded;slot.saved=true;slot.issued=false;renderSlots();return;}
    slot.issued=true;slot.cardNative=response.card;slot.phase='render';let png;
    try{png=await HyTcgRenderer.render(response.card,response.imageData);}catch(error){
      if(response.card.ImageSource!=='zerochan')throw error;slot.phase='profile';slot.requestId=nextRequest();response=await call('hytcgFallback',{cardId:response.card.CardId},slot.requestId);png=await HyTcgRenderer.render(response.card,response.imageData);
    }
    slot.card=response.card;slot.png=png;slot.status='ready';slot.saved=false;renderSlots();await saveSlot(index);
  }catch(error){slot.status='error';slot.error=error.message;if(error.recoverable)slot.issued=true;if(['AcdbBrowserCheck','AcdbClientUnavailable','AcdbRateLimited'].includes(error.message))tcg.packError=error.message;renderSlots();}
}
async function openPack(count,payment=tcg.payment,bannerId=''){
  if(tcg.busy||tcg.purchasing||tcg.dissolving||tcg.upgrading||tcg.deleting||tcg.limitedBusy||!tcg.config)return;
  if(tcg.slots.some(slot=>slot.issued&&slot.status!=='dissolved'&&!slot.saved)){toast(tx('Unsaved card','Còn thẻ chưa lưu'));return;}
  tcg.busy=true;updateButtons();let reservation;
  try{
    reservation=await call('hytcgReserve',{count,payment,bannerId});if(!Array.isArray(reservation.tokens)||reservation.tokens.length!==count)throw Error('TokenInvalid');
    showView('booster');tcg.packError='';tcg.slots=Array.from({length:count},()=>({status:'queued',phase:'queue',payment,bannerId}));renderSlots();let next=0;
    const worker=async()=>{while(next<count){const index=next++;if(tcg.packError){tcg.slots[index].status='error';tcg.slots[index].error=tcg.packError;renderSlots();}else await openSlot(index,reservation.tokens[index]);}};
    await Promise.all(Array.from({length:Math.min(count,tcg.config.Concurrency||2)},worker));
    $('resultsHeading').scrollIntoView({block:'start',behavior:reducedMotion.matches?'instant':'smooth'});
  }catch(error){toast(errorText(error.message));}finally{
    if(reservation?.reservationId)send('hytcgCancelReservation',{reservationId:reservation.reservationId});tcg.busy=false;renderSlots();updateButtons();
  }
}
async function retrySlot(index){
  if(tcg.busy||tcg.purchasing||tcg.dissolving||tcg.upgrading)return;tcg.busy=true;tcg.packError='';updateButtons();let reservation;
  try{if(tcg.slots[index]?.issued&&tcg.slots[index]?.token)await openSlot(index,tcg.slots[index].token);else{reservation=await call('hytcgReserve',{count:1,payment:tcg.slots[index]?.payment||tcg.payment,bannerId:tcg.slots[index]?.bannerId||''});if(!reservation.tokens?.[0])throw Error('TokenInvalid');await openSlot(index,reservation.tokens[0]);}}
  catch(error){toast(errorText(error.message));}finally{if(reservation?.reservationId)send('hytcgCancelReservation',{reservationId:reservation.reservationId});tcg.busy=false;renderSlots();}
}
async function buyPull(){
  await openPack(1,'gems');
}
function showView(view){tcg.view=view;document.querySelector('.tcgShell').classList.toggle('collectionMode',view!=='booster');document.querySelector('.walletBar').hidden=view!=='booster';for(const id of ['booster','collection','dex','missions','lifestyle']){if(!$(id+'View')||!$(id+'Tab'))continue;$(id+'View').hidden=view!==id;$(id+'Tab').classList.toggle('active',view===id);$(id+'Tab').setAttribute('aria-selected',String(view===id));}if(view!=='lifestyle')window.HyTcgLifestyle?.hide();if(view==='collection')loadCollection(false);if(view==='missions')refreshDiscovery();}
function loadCollection(more=false){
  const request=nextRequest();tcg.collectionRequest=request;$('collectionMore').disabled=true;$('collectionStatus').textContent=tx('Loading…','Đang tải…');
  if(!more){tcg.collection=[];$('collectionGrid').replaceChildren();}
  $('resetFilters').hidden=!($('rarityFilter').value||$('collectionSearch').value||tcg.favoritesOnly||$('collectionSort').value!=='newest');
  send('hytcgCollection',{requestId:request,offset:more?(tcg.collectionNextOffset??tcg.collection.length):0,rarity:$('rarityFilter').value,query:$('collectionSearch').value,favoritesOnly:tcg.favoritesOnly,sort:$('collectionSort').value});
}
function renderCollection(){
  $('collectionGrid').innerHTML=tcg.collection.map((item,index)=>`<article class="cardTile ${tcg.selected.has(item.Card.CardId)?'selected':''}">${cardMarkup(item.Card,itemImage(item),index,true,item.IsLegacy===true)}<button class="cardSelect" data-select="${index}" aria-pressed="${tcg.selected.has(item.Card.CardId)}" aria-label="${escapeText(tx('Select '+item.Card.Name,'Chọn '+item.Card.Name))}" ${!item.CanDissolve||item.UpgradeLocked?'disabled':''}>${tcg.selected.has(item.Card.CardId)?'✓':''}</button><button class="cardFavorite ${item.Favorite?'active':''}" data-favorite="${index}" aria-pressed="${item.Favorite===true}" aria-label="${escapeText(tx('Favorite '+item.Card.Name,'Yêu thích '+item.Card.Name))}" ${tcg.favoriteBusy.has(item.Card.CardId)?'disabled':''}>${item.Favorite?'♥':'♡'}</button><div class="cardCaption">${variantLabel(item.Card,item.Cosmetic)}<span class="savedLabel">${item.ExpeditionLocked?tx('On expedition','Đang thám hiểm'):item.UpgradeLocked?tx('Upgrade pending','Đang nâng cấp'):item.IsLegacy?tx('Display only','Chỉ để xem'):''}</span></div></article>`).join('');
  [...$('collectionGrid').querySelectorAll('.cardTile')].forEach((el,index)=>el.style.setProperty('--rarity',tcg.collection[index].Card.RarityColor));
  decorateLifestyleCards();window.HyTcgFrameRepair?.observe();
  $('collectionStatus').textContent=tcg.collection.length?`${tcg.collection.length} / ${tcg.filteredTotal}`:tx('No cards','Không có thẻ');$('collectionMore').hidden=!tcg.hasMore;$('collectionMore').disabled=false;renderSelection();
}
function renderSelection(){
  const count=tcg.selected.size,blocked=tcg.busy||tcg.dissolving||tcg.upgrading;
  $('selectionCount').textContent=count+' '+tx('selected','đã chọn');$('clearSelection').disabled=blocked||!count;$('dissolveSelected').disabled=blocked||!count;$('selectVisible').disabled=blocked||!tcg.collection.some(item=>item.CanDissolve&&!item.UpgradeLocked);
  $('selectionCount').hidden=$('clearSelection').hidden=$('dissolveSelected').hidden=!count;
  if($('confirmBatchDissolve'))$('confirmBatchDissolve').disabled=tcg.dissolving||tcg.previewBusy||!count;
  if($('batchCancel'))$('batchCancel').disabled=tcg.dissolving;
}
function toggleSelection(index){
  const item=tcg.collection[index];if(!item?.CanDissolve||item.UpgradeLocked||tcg.dissolving||tcg.upgrading)return;
  if(tcg.selected.has(item.Card.CardId))tcg.selected.delete(item.Card.CardId);else if(tcg.selected.size<100)tcg.selected.set(item.Card.CardId,item);else toast(tx('Select up to 100 cards at once.','Có thể chọn tối đa 100 card một lần.'));
  renderCollection();
}
function selectVisible(){for(const item of tcg.collection)if(item.CanDissolve&&!item.UpgradeLocked&&tcg.selected.size<100)tcg.selected.set(item.Card.CardId,item);renderCollection();}
function clearSelection(){tcg.selected.clear();renderCollection();}
async function confirmBatch(){
  if(!tcg.selected.size||tcg.dissolving)return;const generation=tcg.batchPreviewGeneration=(tcg.batchPreviewGeneration||0)+1,items=[...tcg.selected.values()],gems=items.reduce((sum,item)=>sum+(Number(item.SalvageValue)||0),0),favorites=items.filter(item=>item.Favorite).length;
  $('batchCardCount').textContent=items.length+' '+tx('cards','card');$('batchGemValue').textContent='◆ +'+gems.toLocaleString(tcg.language==='vi'?'vi-VN':'en-US');$('batchError').textContent='';
  $('batchFavoriteWarning').hidden=!favorites;$('batchFavoriteWarning').textContent=tx(`Includes ${favorites} favorite cards.`,`Có ${favorites} card yêu thích trong danh sách.`);$('batchDialog').showModal();
  tcg.previewBusy=true;renderSelection();$('batchGemValue').innerHTML='<span class="openingSpinner compactSpinner"></span>';
  try{
    const response=await call('hytcgDissolvePreview',{cardIds:items.map(item=>item.Card.CardId)});
    if(generation!==tcg.batchPreviewGeneration||!$('batchDialog').open)return;
    for(const item of items)if(Number.isInteger(response.values?.[item.Card.CardId]))item.SalvageValue=response.values[item.Card.CardId];
    if($('batchDialog').open)$('batchGemValue').textContent='◆ +'+items.reduce((sum,item)=>sum+(Number(item.SalvageValue)||0),0).toLocaleString(tcg.language==='vi'?'vi-VN':'en-US');
  }catch(error){if(generation===tcg.batchPreviewGeneration&&$('batchDialog').open){$('batchError').textContent=errorText(error.message);$('batchGemValue').textContent='◆ +'+gems.toLocaleString(tcg.language==='vi'?'vi-VN':'en-US');}}
  finally{if(generation===tcg.batchPreviewGeneration){tcg.previewBusy=false;renderSelection();}}
}
function removeCards(cardIds){
  const ids=new Set(cardIds);for(const id of ids)tcg.selected.delete(id);
  tcg.collection=tcg.collection.filter(item=>!ids.has(item.Card.CardId));for(const slot of tcg.slots)if(ids.has(slot.card?.CardId)){slot.status='dissolved';slot.png='';slot.card=null;}
  if(ids.has(tcg.detail?.card.CardId)){$('cardDialog').close();tcg.detail=null;}tcg.collectionTotal=Math.max(0,tcg.collectionTotal-ids.size);$('collectionCount').textContent=tcg.collectionTotal;renderSlots();renderCollection();
}
async function dissolveMany(){
  if(!tcg.selected.size||tcg.previewBusy||tcg.dissolving||tcg.upgrading||tcg.busy)return;tcg.dissolving=true;updateButtons();$('batchError').textContent='';
  try{const result=await call('hytcgDissolveMany',{cardIds:[...tcg.selected.keys()]});removeCards(result.cardIds||[]);$('batchDialog').close();loadCollection(false);toast(tx(`Cards dissolved · +${result.gemsAwarded} gems`,`Đã phân giải · +${result.gemsAwarded} gems`));}
  catch(error){$('batchError').textContent=errorText(error.message);}finally{tcg.dissolving=false;updateButtons();}
}
function favoriteState(cardId){const item=tcg.collection.find(item=>item.Card.CardId===cardId);return item?item.Favorite===true:tcg.slots.find(slot=>slot.card?.CardId===cardId)?.favorite===true;}
async function toggleFavorite(cardId){
  if(!cardId||tcg.favoriteBusy.has(cardId)||tcg.dissolving||tcg.upgrading)return;tcg.favoriteBusy.add(cardId);renderCollection();
  try{const response=await call('hytcgFavorite',{cardId,favorite:!favoriteState(cardId)}),favorite=response.favorite===true;
    for(const item of tcg.collection)if(item.Card.CardId===cardId){item.Favorite=favorite;if(tcg.selected.has(cardId))tcg.selected.set(cardId,item);}
    for(const slot of tcg.slots)if(slot.card?.CardId===cardId)slot.favorite=favorite;
    if(tcg.detail?.card.CardId===cardId){tcg.detail.favorite=favorite;const button=$('detailFavorite');if(button){button.classList.toggle('active',favorite);button.setAttribute('aria-pressed',String(favorite));button.textContent=favorite?tx('♥ Favorite','♥ Yêu thích'):tx('♡ Favorite','♡ Yêu thích');}}
    if(tcg.favoritesOnly&&!favorite){tcg.collection=tcg.collection.filter(item=>item.Card.CardId!==cardId);tcg.filteredTotal=Math.max(0,tcg.filteredTotal-1);}
  }catch(error){toast(errorText(error.message));}finally{tcg.favoriteBusy.delete(cardId);renderCollection();}
}
function openSettings(){
  if(!tcg.config)return;const values=tcg.wallet?.SalvageValues||{};
  $('rateFields').innerHTML=tcg.config.DropRates.map(rate=>`<div class="rateField"><span><i></i>${escapeText(rate.Name)}</span><b>${rate.Weight}%</b><span class="salvageRate">◆ ${values[rate.Name]??'—'}</span></div>`).join('');
  [...$('rateFields').querySelectorAll('i')].forEach((dot,index)=>dot.style.setProperty('--rarity',tcg.config.DropRates[index].Color));$('settingsDialog').showModal();
}
async function detail(index,isCollection){
  const item=isCollection?tcg.collection[index]:tcg.slots[index],card=isCollection?item?.Card:item?.card;if(!card)return;
  let source=isCollection?itemImage(item):item.png;const canDissolve=isCollection?item.CanDissolve===true:item.saved===true,value=isCollection?item.SalvageValue:tcg.wallet?.SalvageValues?.[card.Rarity];
  const favorite=isCollection?item.Favorite===true:item.favorite===true,canUpgrade=isCollection?item.CanUpgrade===true:canDissolve&&card.Rarity!=='UR';
  tcg.detail={card,canDissolve,value,favorite,canUpgrade};resetTilt();$('cardDialog').classList.remove('landscapeCardDialog');
  $('cardDetail').innerHTML=`<div class="detailLayout"><div class="detailStage"><div class="detailCard  ${HyTcgRenderer.isHighRarity(card.Rarity)?'isRare':''}" data-tilt data-rarity="${escapeText(card.Rarity)}" tabindex="0" aria-label="${escapeText(card.Name+' · '+card.Rarity)}"><span class="cardSurface"><img class="detailImage" src="${escapeText(source)}" alt="${escapeText(card.Name)}">${isCollection&&item.IsLegacy===true?legacyMask(card):''}<span class="cardShine" aria-hidden="true"></span><span class="cardGlare" aria-hidden="true"></span></span></div></div><div class="detailInfo"><div class="detailBadges">${window.HyTcgRarity?.markup(card.Rarity,'rarityBadge')||`<span class="rarityBadge">${escapeText(card.Rarity)}</span>`}${card.ImageSource==='zerochan'&&!(isCollection?item.Cosmetic:item.cosmetic)?.ArtworkId?'<span class="altLabel">ALT</span>':''}</div><h2>${escapeText(card.Name)}</h2><p class="detailSeries">${escapeText(card.Series||tx('Unknown series','Chưa có series'))}</p><div class="detailIdentity"><span>#${card.CharacterId}</span>${card.Gender?`<span>${escapeText(card.Gender)}</span>`:''}${Number(card.Revision)>0?`<span>↗ ${card.Revision}</span>`:''}</div><p class="cardNumber">${escapeText(card.CardId)}</p>${card.Metadata?.desc||card.Metadata?.description?`<p class="detailDescription">${escapeText(card.Metadata.desc||card.Metadata.description)}</p>`:''}<div class="detailActions"><button id="detailFavorite" class="quietButton ${favorite?'active':''}" aria-pressed="${favorite}" ${!canDissolve&&item?.saved===false?'disabled':''}>${favorite?tx('♥ Favorite','♥ Yêu thích'):tx('♡ Favorite','♡ Yêu thích')}</button>${canUpgrade?`<button id="startUpgrade" class="upgradeButton">↗ ${tx('Upgrade','Nâng cấp')}</button>`:''}</div><div class="detailLinks"><button class="quietButton" data-source="acdb">ACDB ↗</button>${card.ArtworkPage?'<button class="quietButton" data-source="zerochan">ZeroChan ↗</button>':''}</div><div class="dissolveArea">${canDissolve&&Number(value)>0?`<button id="dissolveButton" class="dissolveButton"><span>${tx('Dissolve card','Phân giải card')}</span><b>◆ ${value}</b></button><div id="dissolveConfirm" class="dissolveConfirm" hidden><p>${escapeText(tx('Permanently dissolve '+card.Name+' for '+value+' gems?','Phân giải vĩnh viễn '+card.Name+' để nhận '+value+' gems?'))}${favorite?`<br><span class="favoriteWarning">${tx('This is a favorite card.','Đây là card yêu thích.')}</span>`:''}</p><div><button id="cancelDissolve" class="quietButton">${tx('Keep card','Giữ card')}</button><button id="confirmDissolve" class="confirmDissolve">${tx('Dissolve','Phân giải')}</button></div></div>`:`<p class="fieldHint">${item?.saved===false?tx('Not saved','Chưa lưu'):item.ExpeditionLocked?tx('On expedition','Đang thám hiểm'):item.UpgradeLocked?tx('Upgrade pending','Đang nâng cấp'):tx('Display only','Chỉ xem')}</p>`}<p id="dissolveError" class="inlineError" role="alert"></p></div></div></div>`;
  syncCardEffects();$('cardDetail').style.setProperty('--rarity',card.RarityColor);$('cardDialog').dataset.character=String(card.CharacterId);$('cardDialog').dataset.artwork=/^https:\/\/www\.zerochan\.net\/\d+$/.test(card.ArtworkPage||'')?card.ArtworkPage:'';$('cardDialog').showModal();$('cardDialog').scrollTop=0;updateButtons();
  window.HyTcgLifestyle?.decorate($('cardDetail').querySelector('.detailCard'),card.CardId,isCollection?item.Cosmetic:item.cosmetic);
  loadCharacterDetail(tcg.detail);
  loadLifestyleDetail(tcg.detail,item?.saved!==false&&item?.IsLegacy!==true);
  if((isCollection&&(/\.hycard$/i.test(item.FileName||item.ImagePath||'')||item.Thumbnail||item.Encrypted))||(!isCollection&&item.saved===true)){const current=tcg.detail;try{const response=await call('hytcgReadCard',{cardId:card.CardId});if(tcg.detail===current&&$('cardDialog').open&&response.imageData){$('cardDetail').querySelector('.detailImage').src=response.imageData;if(response.cosmetic?.ArtworkId)$('cardDetail').querySelector('.detailBadges .altLabel')?.remove();window.HyTcgLifestyle?.decorate($('cardDetail').querySelector('.detailCard'),card.CardId,response.cosmetic||null);}}catch(error){if(tcg.detail===current)toast(errorText(error.message));}}
}
async function loadLifestyleDetail(current,enabled){
  if(!enabled||!window.HyTcgLifestyle)return;
  const actions=$('cardDetail').querySelector('.detailActions');
  for(const [id,label]of [['detailStudio',tx('✧ Studio','✧ Studio')],['detailDating',tx('♡ Bond','♡ Gắn kết')]]){const button=document.createElement('button');button.id=id;button.className='quietButton';button.textContent=label;actions.append(button);}
  const stats=document.createElement('section');stats.className='cardCompanionStats';
  $('cardDetail').querySelector('.detailInfo').insertBefore(stats,actions);
  try{
    const response=await call('hytcgLifestyle',{action:'profile',cardId:current.card.CardId});
    if(tcg.detail!==current||!$('cardDialog').open)return;
    const p=response.profile;if(!p){stats.remove();return;}
    stats.innerHTML=`<div><small>${tx('Level','Cấp')}</small><b>${Number(p.Level)||1}</b></div><div><small>${tx('Effort','Nỗ lực')}</small><b>${Number(p.Effort)||0}</b></div><div><small>${tx('Attack','Tấn công')}</small><b>${Number(p.Attack)||0}</b></div><div><small>${tx('Bond','Gắn kết')}</small><b>♥ ${Number(p.Bond)||0}</b></div>`;
    if(p.Busy){const busy=document.createElement('span');busy.className='companionBusy';busy.textContent=tx('Busy','Đang bận');stats.append(busy);}
  }catch{if(tcg.detail===current)stats.remove();}
}
async function loadCharacterDetail(current){
  const section=document.createElement('section');section.className='characterEnrichment';section.innerHTML='<span class="openingSpinner compactSpinner" aria-label="AniList"></span>';
  $('cardDetail').querySelector('.detailInfo').insertBefore(section,$('cardDetail').querySelector('.detailActions'));
  current.infoLoading=true;updateButtons();
  try{
    const response=await call('hytcgCharacterInfo',{cardId:current.card.CardId});
    if(tcg.detail!==current||!$('cardDialog').open)return;
    const info=response.info;
    if(Number.isInteger(response.salvageValue)&&response.salvageValue>0){
      current.value=response.salvageValue;
      for(const item of tcg.collection)if(item.Card.CardId===current.card.CardId)item.SalvageValue=current.value;
      const value=$('dissolveButton')?.querySelector('b');if(value)value.textContent='◆ '+current.value.toLocaleString(tcg.language==='vi'?'vi-VN':'en-US');
      const confirm=$('dissolveConfirm')?.querySelector('p');if(confirm)confirm.textContent=tx(`Permanently dissolve ${current.card.Name} for ${current.value} gems?`,`Phân giải vĩnh viễn ${current.card.Name} để nhận ${current.value} gems?`)+(current.favorite?' '+tx('Favorite card.','Thẻ yêu thích.'):'');
    }
    if(info?.Matched){
      section.innerHTML=`<div class="characterStats"><span class="characterFavourites">♥ ${Number(info.Favourites||0).toLocaleString(tcg.language==='vi'?'vi-VN':'en-US')}</span>${info.Age?`<span>${escapeText(info.Age)} ${tx('years','tuổi')}</span>`:''}${info.Gender&&info.Gender!==current.card.Gender?`<span>${escapeText(info.Gender)}</span>`:''}<button class="quietButton" data-anilist="${Number(info.AniListId)}">AniList ↗</button></div>${info.Description?`<p class="characterDescription">${escapeText(info.Description)}</p>`:''}`;
      if(!Number.isInteger(info.Favourites))section.querySelector('.characterFavourites')?.remove();
      if(info.DateOfBirth){const birthday=document.createElement('span');birthday.textContent='◷ '+info.DateOfBirth;birthday.title=tx('Birthday','Ngày sinh');section.querySelector('.characterStats').insertBefore(birthday,section.querySelector('[data-anilist]'));}
      if(info.Description)$('cardDetail').querySelector('.detailDescription')?.remove();
    }else section.remove();
  }catch{if(tcg.detail===current)section.remove();}
  finally{current.infoLoading=false;if(tcg.detail===current)updateButtons();}
}
async function dissolveCard(){
  const current=tcg.detail;if(!current?.canDissolve||tcg.busy||tcg.dissolving)return;tcg.dissolving=true;$('confirmDissolve').disabled=$('cancelDissolve').disabled=true;$('dissolveError').textContent='';updateButtons();
  try{
    const result=await call('hytcgDissolveMany',{cardIds:[current.card.CardId]});removeCards(result.cardIds||[current.card.CardId]);loadCollection(false);toast(tx('Card dissolved · +'+result.gemsAwarded+' gems','Đã phân giải card · +'+result.gemsAwarded+' gems'));
  }catch(error){if($('dissolveError'))$('dissolveError').textContent=errorText(error.message);if($('confirmDissolve'))$('confirmDissolve').disabled=$('cancelDissolve').disabled=false;}
  finally{tcg.dissolving=false;updateButtons();}
}
function renderUpgradeRecoveries(){
  $('pendingUpgradeBanner').hidden=!tcg.upgradeRecoveries.length;
  $('pendingUpgradeText').textContent=tx(`${tcg.upgradeRecoveries.length} unfinished upgrade`,`${tcg.upgradeRecoveries.length} lần nâng cấp đang chờ`);
}
function upgradeSource(card){return tcg.slots.find(slot=>slot.card?.CardId===card.CardId)?.png||itemImage(tcg.collection.find(item=>item.Card.CardId===card.CardId));}
async function startUpgrade(){
  if(!tcg.detail?.canUpgrade||tcg.busy||tcg.upgrading||tcg.dissolving)return;
  if(tcg.upgradeRecoveries.length){toast(errorText('UpgradePending'));return;}
  const card=tcg.detail.card;tcg.upgrade={baseCard:card,source:upgradeSource(card),targetRarity:'',mode:'ten',materials:[],selected:new Map(),hasMore:false,total:0,queryRequest:'',loading:false,token:'',card:null,png:'',error:''};
  $('cardDialog').close();$('materialSearch').value='';$('changeArtwork').checked=false;$('upgradePreview').replaceChildren();$('upgradePreview').hidden=true;$('upgradePicker').hidden=false;$('upgradeError').textContent='';renderUpgradeIdentity();renderUpgradeMaterials();$('upgradeDialog').showModal();await loadUpgradeMaterials();
}
function renderUpgradeIdentity(){
  const upgrade=tcg.upgrade;if(!upgrade)return;
  $('upgradeIdentity').innerHTML=`<div class="upgradeMini "><img src="${escapeText(upgrade.source)}" alt="${escapeText(upgrade.baseCard.Name)}"></div><div><strong>${escapeText(upgrade.baseCard.Name)}</strong><span>${escapeText(upgrade.baseCard.Rarity)} <b>→</b> <em>${escapeText(upgrade.targetRarity||'…')}</em></span><small>${escapeText(upgrade.baseCard.Series||'')}</small></div>`;
}
async function loadUpgradeMaterials(more=false){
  const upgrade=tcg.upgrade;if(!upgrade||upgrade.token)return;const request=nextRequest();upgrade.queryRequest=request;upgrade.loading=true;if(!more){upgrade.materials=[];upgrade.hasMore=false;}renderUpgradeMaterials();
  try{
    const response=await call('hytcgUpgradeOptions',{cardId:upgrade.baseCard.CardId,offset:more?upgrade.materials.length:0,query:$('materialSearch').value,sameCharacterOnly:upgrade.mode==='same'},request);
    if(tcg.upgrade!==upgrade||upgrade.queryRequest!==request)return;upgrade.baseCard=response.card||upgrade.baseCard;upgrade.targetRarity=response.targetRarity;upgrade.total=response.total??response.materials?.length??0;upgrade.hasMore=response.hasMore===true;
    upgrade.materials.push(...(response.materials||[]).filter(item=>item.Card?.Rarity===upgrade.targetRarity&&item.Card.CardId!==upgrade.baseCard.CardId&&!item.UpgradeLocked&&!item.IsLegacy&&(upgrade.mode!=='same'||item.Card.CharacterId===upgrade.baseCard.CharacterId)));
    renderUpgradeIdentity();$('upgradeError').textContent='';
  }catch(error){if(tcg.upgrade===upgrade&&upgrade.queryRequest===request)$('upgradeError').textContent=errorText(error.message);}
  finally{if(tcg.upgrade===upgrade&&upgrade.queryRequest===request){upgrade.loading=false;renderUpgradeMaterials();}}
}
function setMaterialMode(mode){
  const upgrade=tcg.upgrade;if(!upgrade||upgrade.token||tcg.upgrading)return;upgrade.mode=mode==='same'?'same':'ten';upgrade.selected.clear();loadUpgradeMaterials(false);
}
function renderUpgradeMaterials(){
  const upgrade=tcg.upgrade;if(!upgrade)return;
  for(const [id,mode]of[['materialSame','same'],['materialTen','ten']]){$(id).classList.toggle('active',upgrade.mode===mode);$(id).setAttribute('aria-pressed',String(upgrade.mode===mode));$(id).disabled=tcg.upgrading||!!upgrade.token;}
  $('materialRule').textContent=upgrade.mode==='same'?`1 × ${upgrade.targetRarity||'…'} · ${upgrade.baseCard.Name}`:`10 × ${upgrade.targetRarity||'…'}`;
  $('materialGrid').innerHTML=upgrade.materials.map((item,index)=>`<button class="materialCard ${upgrade.selected.has(item.Card.CardId)?'selected':''}" data-material="${index}" aria-pressed="${upgrade.selected.has(item.Card.CardId)}" ${tcg.upgrading||upgrade.loading?'disabled':''}><span class="materialImage "><img src="${escapeText(itemImage(item))}" alt="${escapeText(item.Card.Name)}"><i>${upgrade.selected.has(item.Card.CardId)?'✓':''}</i>${item.Favorite?'<b>♥</b>':''}</span><strong>${escapeText(item.Card.Name)}</strong><small>${escapeText(item.Card.Rarity)}</small></button>`).join('')||`<div class="materialEmpty">${upgrade.loading?'<span class="openingSpinner"></span>':tx('No materials','Không có nguyên liệu')}</div>`;
  $('materialLoadMore').hidden=!upgrade.hasMore;$('materialLoadMore').disabled=upgrade.loading||tcg.upgrading;$('materialLoadMore').textContent=tx('Load more materials','Xem thêm nguyên liệu');renderUpgradeFooter();
}
function selectMaterial(index){
  const upgrade=tcg.upgrade,item=upgrade?.materials[index];if(!item||tcg.upgrading||upgrade.token||upgrade.loading)return;
  if(upgrade.selected.has(item.Card.CardId))upgrade.selected.delete(item.Card.CardId);
  else if(upgrade.mode==='same'){upgrade.selected.clear();upgrade.selected.set(item.Card.CardId,item);}
  else if(upgrade.selected.size<10)upgrade.selected.set(item.Card.CardId,item);
  else toast(tx('Choose exactly 10 materials.','Chỉ cần chọn đúng 10 nguyên liệu.'));renderUpgradeMaterials();
}
function renderUpgradeFooter(){
  const upgrade=tcg.upgrade;if(!upgrade)return;const required=upgrade.mode==='same'?1:10,favorites=[...upgrade.selected.values()].filter(item=>item.Favorite).length;
  $('materialCount').textContent=upgrade.token?tx('Upgrade reserved','Đã giữ nguyên liệu'):upgrade.selected.size+' / '+required;
  $('upgradeFavoriteWarning').hidden=!favorites;$('upgradeFavoriteWarning').textContent=tx(`This uses ${favorites} favorite cards.`,`Thao tác này dùng ${favorites} card yêu thích làm nguyên liệu.`);
  $('confirmUpgrade').textContent=tcg.upgrading?tx('Working…','Đang xử lý…'):upgrade.token?(upgrade.png?tx('Retry save','Lưu lại'):tx('Resume upgrade','Tiếp tục nâng cấp')):tx('Upgrade','Nâng cấp');
  $('confirmUpgrade').disabled=tcg.upgrading||upgrade.loading||(!upgrade.token&&upgrade.selected.size!==required);$('cancelUpgrade').disabled=$('closeUpgrade').disabled=tcg.upgrading;$('changeArtwork').disabled=tcg.upgrading||!!upgrade.token;
}
function showUpgradePreview(){
  const upgrade=tcg.upgrade;if(!upgrade?.png)return;$('upgradePicker').hidden=true;$('upgradePreview').hidden=false;
  $('upgradePreview').innerHTML=`<div class="detailCard  ${HyTcgRenderer.isHighRarity(upgrade.card.Rarity)?'isRare':''}" data-tilt data-rarity="${escapeText(upgrade.card.Rarity)}"><span class="cardSurface"><img src="${escapeText(upgrade.png)}" alt="${escapeText(upgrade.card.Name)}"><span class="cardShine" aria-hidden="true"></span><span class="cardGlare" aria-hidden="true"></span></span></div>`;
  $('upgradePreview').style.setProperty('--rarity',upgrade.card.RarityColor);
}
async function finishUpgradeSave(){
  const upgrade=tcg.upgrade;if(!upgrade?.token||!upgrade.png)return;
  const response=await call('hytcgUpgradeSave',{upgradeToken:upgrade.token,png:upgrade.png});
  const consumed=response.consumedIds||[];removeCards(consumed);if(response.card?.CardId)tcg.selected.delete(response.card.CardId);for(const slot of tcg.slots)if(slot.card?.CardId===response.card?.CardId){slot.card=response.card;slot.png=upgrade.png;slot.saved=true;slot.path=response.path;}
  tcg.upgradeRecoveries=tcg.upgradeRecoveries.filter(item=>item.Token!==upgrade.token);tcg.upgrade=null;renderUpgradeRecoveries();$('upgradeDialog').close();$('upgradePreview').replaceChildren();loadCollection(false);renderSlots();toast(tx(`Upgraded to ${response.card.Rarity}`,`Đã nâng cấp lên ${response.card.Rarity}`));
}
async function performUpgrade(){
  const upgrade=tcg.upgrade;if(!upgrade||tcg.upgrading||upgrade.loading)return;tcg.upgrading=true;updateButtons();renderUpgradeFooter();$('upgradeError').textContent='';
  try{
    if(!upgrade.token){
      const response=await call('hytcgUpgradePrepare',{cardId:upgrade.baseCard.CardId,materialIds:[...upgrade.selected.keys()],changeArtwork:$('changeArtwork').checked});
      upgrade.token=response.upgradeToken;upgrade.card=response.card;upgrade.targetRarity=response.card.Rarity;upgrade.png=await HyTcgRenderer.render(response.card,response.imageData);showUpgradePreview();
    }else if(!upgrade.png){
      const response=await call('hytcgUpgradeResume',{upgradeToken:upgrade.token});upgrade.card=response.card;upgrade.targetRarity=response.card.Rarity;upgrade.png=await HyTcgRenderer.render(response.card,response.imageData);showUpgradePreview();
    }
    await finishUpgradeSave();
  }catch(error){if(tcg.upgrade===upgrade){if(error.payload?.upgradeToken)upgrade.token=error.payload.upgradeToken;$('upgradeError').textContent=errorText(error.message);}}
  finally{tcg.upgrading=false;updateButtons();renderUpgradeFooter();}
}
async function resumeUpgrade(){
  if(!tcg.upgradeRecoveries.length||tcg.upgrading||tcg.busy)return;const pending=tcg.upgradeRecoveries[0],existing=tcg.collection.find(item=>item.Card.CardId===(pending.Card?.CardId||pending.CardId))?.Card;
  const tiers=tcg.config?.DropRates.map(rate=>rate.Name)||[],target=pending.TargetRarity||pending.Card?.Rarity,card=existing||pending.SourceCard||(pending.Card?{...pending.Card,Rarity:tiers[Math.max(0,tiers.indexOf(target)-1)]||pending.Card.Rarity}:null);
  if(!card){toast(errorText('UpgradeInvalid'));return;}const selected=new Map(tcg.collection.filter(item=>(pending.MaterialIds||[]).includes(item.Card.CardId)).map(item=>[item.Card.CardId,item]));
  tcg.upgrade={baseCard:card,source:upgradeSource(card),targetRarity:pending.Card?.Rarity||pending.TargetRarity||'',mode:pending.MaterialIds?.length===1?'same':'ten',materials:[],selected,hasMore:false,total:0,queryRequest:'',loading:false,token:pending.Token,card:pending.Card,png:''};
  $('upgradePicker').hidden=true;$('upgradePreview').replaceChildren();$('upgradePreview').hidden=false;$('upgradeError').textContent='';renderUpgradeIdentity();renderUpgradeFooter();$('upgradeDialog').showModal();await performUpgrade();
}
async function cancelUpgrade(){
  const upgrade=tcg.upgrade;if(!upgrade||tcg.upgrading)return;
  if(!upgrade.token){tcg.upgrade=null;$('upgradeDialog').close();$('upgradePreview').replaceChildren();return;}
  tcg.upgrading=true;renderUpgradeFooter();updateButtons();
  try{await call('hytcgUpgradeCancel',{upgradeToken:upgrade.token});tcg.upgradeRecoveries=tcg.upgradeRecoveries.filter(item=>item.Token!==upgrade.token);tcg.upgrade=null;$('upgradeDialog').close();$('upgradePreview').replaceChildren();renderUpgradeRecoveries();loadCollection(false);}
  catch(error){$('upgradeError').textContent=errorText(error.message);}finally{tcg.upgrading=false;updateButtons();renderUpgradeFooter();}
}
function field(object,name){return object?.[name]??object?.[name[0].toLowerCase()+name.slice(1)];}
function spinner(){return '<span class="openingSpinner smallSpinner" aria-hidden="true"></span>';}
function applyDiscovery(message){
  if(message.banners){tcg.banners=message.banners;renderBanners();}
  if(message.missions){tcg.missions=message.missions;tcg.missionDay=message.missionDay||tcg.missionDay;renderMissions();}
}
async function refreshDiscovery(force=false){
  if(tcg.discoveryPending||(!force&&performance.now()-tcg.discoveryReceived<60000))return;
  tcg.discoveryPending=true;tcg.discoveryReceived=performance.now();if(!tcg.banners.length)$('limitedBanners').innerHTML=`<div class="bannerLoading" role="status" aria-label="${tx('Loading banners','Đang tải banner')}">${spinner()}</div>`;
  try{const response=await call('hytcgDiscoveryState');applyDiscovery(response);tcg.discoveryReceived=performance.now();}
  catch(error){if(!tcg.banners.length)$('limitedBanners').innerHTML=`<div class="bannerUnavailable"><span>${escapeText(tx('Banners unavailable','Chưa tải được banner'))}</span><button class="quietButton" data-refresh-banners>${tx('Retry','Thử lại')}</button></div>`;if(tcg.view==='missions'&&!tcg.missions.length)$('missionCards').innerHTML=`<div class="emptyView">${escapeText(errorText(error.message))}</div>`;}
  finally{tcg.discoveryPending=false;updateLimitedButtons();}
}
function renderBanners(){
  $('limitedBanners').innerHTML=tcg.banners.map((banner,index)=>{const image=field(banner,'ImageData'),title=field(banner,'Title')||field(banner,'Series')||'Series';return `<article class="limitedBanner">${image?`<img class="bannerArtwork" src="${escapeText(image)}" alt="" loading="lazy">`:''}<div class="bannerScrim"></div><div class="bannerBody"><div class="bannerTags"><span>LIMITED ${String(index+1).padStart(2,'0')}</span><span>30% SERIES</span></div><h3 title="${escapeText(title)}">${escapeText(title)}</h3><div class="bannerActions"><button class="bannerOpen" data-banner="${index}" data-count="1"><span>${tx('Open x1','Mở x1')}</span><b></b></button><button class="bannerOpen" data-banner="${index}" data-count="10"><span>${tx('Open x10','Mở x10')}</span><b></b></button></div></div></article>`;}).join('')||`<div class="bannerUnavailable"><span>${tx('No banners today','Chưa có banner hôm nay')}</span><button class="quietButton" data-refresh-banners>${tx('Retry','Thử lại')}</button></div>`;
  const expiry=field(tcg.banners[0],'ExpiresAt');$('bannerRefreshTime').textContent=expiry?tx('Refreshes ','Làm mới ')+new Date(expiry).toLocaleTimeString(tcg.language==='vi'?'vi-VN':'en-US',{hour:'2-digit',minute:'2-digit'}):'';updateLimitedButtons();
}
function updateLimitedButtons(){
  const blocked=tcg.busy||tcg.purchasing||tcg.dissolving||tcg.upgrading||tcg.deleting||tcg.limitedBusy||!tcg.wallet,price=Number(tcg.wallet?.LimitedPackPrice)||180,credits=Number(tcg.wallet?.LimitedCredits)||0,gems=Number(tcg.wallet?.Gems)||0,gemMode=tcg.limitedPayment!=='credits';
  for(const [id,mode]of[['limitedGems','gems'],['limitedCredits','credits']]){$(id).classList.toggle('active',(gemMode?'gems':'credits')===mode);$(id).setAttribute('aria-pressed',String((gemMode?'gems':'credits')===mode));$(id).disabled=blocked;}
  $('limitedCredits').textContent=tx('✦ Credits','✦ Lượt giới hạn');$('buyLimited').disabled=blocked||gems<price;$('buyLimited').querySelector('span').textContent='◆ '+price;
  for(const button of $('limitedBanners').querySelectorAll('[data-banner]')){const count=Number(button.dataset.count);button.disabled=blocked||(gemMode?gems<price*count:credits<count);button.querySelector('b').textContent=(gemMode?'◆ ':'✦ ')+(gemMode?price*count:count).toLocaleString(tcg.language==='vi'?'vi-VN':'en-US');}
  $('resetAccount').disabled=blocked;$('confirmDelete').disabled=blocked||$('deleteConfirmation').value!=='delete';
}
async function buyLimited(){if(tcg.limitedBusy)return;tcg.limitedBusy=true;updateLimitedButtons();try{await call('hytcgLimitedBuy',{count:1});}catch(error){toast(errorText(error.message));}finally{tcg.limitedBusy=false;updateLimitedButtons();}}
function renderMissions(){
  if(window.HyTcgAdventure?.renderMissions())return;
  const complete=tcg.missions.filter(m=>field(m,'Complete')).length,reward=tcg.missions.reduce((sum,m)=>sum+(field(m,'Complete')?Number(field(m,'Reward'))||0:0),0);
  $('missionDay').textContent=tcg.missionDay||'';$('missionSummary').textContent=complete+' / '+tcg.missions.length+' · ◆ '+reward;
  $('missionCards').innerHTML=tcg.missions.map(m=>{const movie=field(m,'Kind')==='movie',done=field(m,'Complete')===true,progress=Number(field(m,'Progress'))||0,target=Number(field(m,'Target'))||(movie?1200:1),title=movie?tx('Movie night','Xem phim'):field(m,'Title')||tx('Play a game','Chơi game'),id=field(m,'Id')||'';return `<article class="missionCard ${done?'complete':''}"><div class="missionIcon">${done?'✓':movie?'▷':'⌘'}</div><div class="missionBody"><h3>${escapeText(title)}</h3><span>${done?tx('Completed','Hoàn thành'):movie?Math.floor(progress/60)+' / '+Math.ceil(target/60)+' '+tx('min','phút'):tx('Play once','Chơi 1 lần')}</span><div class="missionProgress"><i></i></div></div><div class="missionReward"><b>◆ ${Number(field(m,'Reward'))||0}</b>${!done?`<button class="quietButton" ${movie?'data-mission-movie':'data-mission-play="'+escapeText(id.replace(/^game:/,''))+'"'}>▷ ${movie?tx('Watch','Xem'):tx('Play','Chơi')}</button>`:''}</div></article>`;}).join('')||`<div class="emptyView">${tx('No missions','Chưa có nhiệm vụ')}</div>`;
  [...$('missionCards').querySelectorAll('.missionProgress i')].forEach((line,index)=>{const m=tcg.missions[index];line.style.width=(field(m,'Complete')?100:Math.min(100,100*(Number(field(m,'Progress'))||0)/Math.max(1,Number(field(m,'Target'))||1)))+'%';});
}
async function playMission(button){button.disabled=true;try{const response=await call('hytcgMissionPlay',{gameId:button.dataset.missionPlay});applyDiscovery(response);await refreshWallet();}catch(error){toast(errorText(error.message));}finally{if(button.isConnected)button.disabled=false;}}
function dexCardCharacter(character,index){const name=field(character,'Name')||'',series=field(character,'Series')||'',image=field(character,'ImageData')||field(character,'ProfileData');return `<button class="dexCharacter" data-dex-character="${index}"><div class="dexPortrait">${image?`<img src="${escapeText(image)}" alt="" loading="lazy">`:`<span class="dexLazyProfile" data-character="${Number(field(character,'CharacterId'))||0}">${spinner()}</span>`}</div><strong>${escapeText(name)}</strong><span>${escapeText(series)}</span></button>`;}
function renderDex(){
  const dex=tcg.dex;$('dexResults').innerHTML=dex.series.length&&!dex.seriesId?dex.series.map(series=>`<button class="dexSeries" data-dex-series="${Number(field(series,'Id'))||0}"><span>▦</span><strong>${escapeText(field(series,'Name')||'')}</strong><b>→</b></button>`).join(''):dex.characters.map(dexCardCharacter).join('');
  $('dexStatus').innerHTML=dex.pending?spinner():dex.error?escapeText(errorText(dex.error)):(!dex.characters.length&&!dex.series.length&&dex.query?tx('No results','Không có kết quả'):'');$('dexMore').hidden=!dex.hasMore;$('dexMore').disabled=dex.pending;$('dexSubmit').disabled=dex.pending;observeDexImages();
}
async function searchDex(more=false,seriesId=0){
  const dex=tcg.dex;if(dex.pending)return;const query=$('dexSearch').value.trim();if(!query&&!seriesId)return;
  if(!more){dex.characters=[];dex.series=[];dex.offset=0;dex.seriesId=seriesId;dex.query=query;dex.mode=$('dexMode').value;}dex.error='';dex.pending=true;const stamp=++dex.request;renderDex();
  try{const response=await call(dex.seriesId?'hytcgDexSeries':'hytcgDexSearch',dex.seriesId?{seriesId:dex.seriesId,offset:dex.offset}:{query:dex.query,mode:dex.mode,offset:dex.offset});if(stamp!==dex.request)return;dex.characters.push(...(response.characters||[]));dex.series.push(...(response.series||[]));dex.offset=(Number(response.offset)||dex.offset)+20;dex.hasMore=response.hasMore===true;}
  catch(error){if(stamp===dex.request)dex.error=error.message;}finally{if(stamp===dex.request){dex.pending=false;renderDex();}}
}
async function viewDexCharacter(index){
  const character=tcg.dex.characters[index];if(!character)return;tcg.dexCharacter=character;tcg.artworks=[];tcg.artworkPage=0;$('dexArtworkMore').hidden=true;$('dexCharacterName').textContent=field(character,'Name')||'';$('dexCharacterSeries').textContent=field(character,'Series')||'';$('dexMetadata').innerHTML=[field(character,'Gender'),field(character,'CharacterId')?'#'+field(character,'CharacterId'):null].filter(Boolean).map(value=>`<span>${escapeText(value)}</span>`).join('');$('dexArtworks').replaceChildren();$('dexArtworkStatus').innerHTML=spinner();$('dexDialog').showModal();
  try{const response=await call('hytcgDexArtwork',{characterId:field(character,'CharacterId')});if(tcg.dexCharacter!==character)return;tcg.artworks=response.artworks||[];tcg.artworkPage=response.page||1;tcg.artworkHasMore=response.hasMore===true;renderDexArtworks();
  }catch(error){if(tcg.dexCharacter===character)$('dexArtworkStatus').textContent=errorText(error.message);}
}
function renderDexArtworks(){const character=tcg.dexCharacter;if(!character)return;$('dexArtworkStatus').textContent=tcg.artworks.length?tcg.artworks.length+' '+tx('artworks','artwork'):tx('No variants found','Chưa có biến thể');$('dexArtworks').innerHTML=tcg.artworks.map((art,index)=>`<button class="dexArtwork" data-dex-artwork="${index}"><span class="dexLazyArtwork" data-character="${Number(field(character,'CharacterId'))||0}" data-artwork="${Number(field(art,'Id'))||0}">${spinner()}</span><span>ALT <b>↗</b></span></button>`).join('');$('dexArtworkMore').hidden=!tcg.artworkHasMore;observeDexImages();}
async function moreDexArtworks(){const character=tcg.dexCharacter;if(!character||tcg.artworkPending)return;tcg.artworkPending=true;$('dexArtworkMore').disabled=true;try{const response=await call('hytcgDexArtwork',{characterId:field(character,'CharacterId'),page:(tcg.artworkPage||1)+1});if(tcg.dexCharacter!==character)return;tcg.artworks.push(...(response.artworks||[]));tcg.artworkPage=response.page||(tcg.artworkPage||1)+1;tcg.artworkHasMore=response.hasMore===true;renderDexArtworks();}catch(error){$('dexArtworkStatus').textContent=errorText(error.message);}finally{tcg.artworkPending=false;$('dexArtworkMore').disabled=false;}}
const dexImageCache=new Map(),dexImageQueue=[];let dexImageWorkers=0;
const dexObserver=new IntersectionObserver(entries=>{for(const entry of entries)if(entry.isIntersecting){dexObserver.unobserve(entry.target);queueDexImage(entry.target);}}, {rootMargin:'60px'});
function observeDexImages(){for(const node of document.querySelectorAll('.dexLazyProfile,.dexLazyArtwork'))dexObserver.observe(node);}
function queueDexImage(node){const key=node.dataset.character+':'+(node.dataset.artwork||'profile');if(dexImageCache.has(key)){setDexImage(node,dexImageCache.get(key));return;}dexImageQueue.push({node,key});pumpDexImages();}
function setDexImage(node,image){if(!node.isConnected)return;node.replaceChildren();if(!image){node.textContent='◇';return;}const img=document.createElement('img');img.src=image;img.alt='';img.loading='lazy';node.append(img);}
function pumpDexImages(){
  while(dexImageWorkers<2&&dexImageQueue.length){const {node,key}=dexImageQueue.shift();if(!node.isConnected||node.closest('[hidden]')||node.closest('dialog:not([open])'))continue;dexImageWorkers++;(async()=>{try{const response=await call(node.dataset.artwork?'hytcgDexImage':'hytcgDexProfile',{characterId:Number(node.dataset.character),artworkId:Number(node.dataset.artwork)||0,thumbnail:true});if(response.imageData){dexImageCache.set(key,response.imageData);while(dexImageCache.size>120)dexImageCache.delete(dexImageCache.keys().next().value);}setDexImage(node,response.imageData);}catch{setDexImage(node,'');}finally{dexImageWorkers--;pumpDexImages();}})();}
}
async function viewDexArtwork(index){
  const art=tcg.artworks[index],character=tcg.dexCharacter;if(!art||!character)return;let preview=$('artworkPreview');if(!preview){preview=document.createElement('dialog');preview.id='artworkPreview';preview.className='artworkPreview';preview.innerHTML='<button class="iconButton" data-close="artworkPreview" aria-label="Close">×</button><div class="artworkPreviewBody"></div><button class="quietButton" id="artworkSource">ZeroChan ↗</button>';document.body.append(preview);}
  const body=preview.querySelector('.artworkPreviewBody'),stamp=nextRequest();preview.dataset.request=stamp;body.innerHTML=spinner();preview.showModal();$('artworkSource').onclick=()=>send('openExternal',{target:field(art,'PageUrl')});
  try{const response=await call('hytcgDexImage',{characterId:field(character,'CharacterId'),artworkId:field(art,'Id'),thumbnail:false});if(preview.open&&preview.dataset.request===stamp){body.replaceChildren();const image=document.createElement('img');image.src=response.imageData;image.alt=field(character,'Name');body.append(image);}}catch(error){if(preview.open&&preview.dataset.request===stamp)body.textContent=errorText(error.message);}
}
function showAccountDelete(){if(tcg.busy||tcg.upgrading||tcg.dissolving||tcg.deleting)return;$('settingsDialog').close();$('deleteConfirmation').value='';$('deleteError').textContent='';$('confirmDelete').disabled=true;$('accountDialog').showModal();$('deleteConfirmation').focus();}
async function deleteAccount(){
  if(tcg.deleting||$('deleteConfirmation').value!=='delete'||tcg.busy||tcg.upgrading||tcg.dissolving)return;tcg.deleting=true;$('confirmDelete').disabled=$('deleteCancel').disabled=true;$('deleteError').textContent='';
  try{const response=await call('hytcgAccountDelete',{confirmation:'delete'});tcg.wallet=null;tcg.walletStamp=0;applyWallet(response.wallet);tcg.slots=[];tcg.collection=[];tcg.collectionTotal=0;tcg.selected.clear();tcg.upgrade=null;tcg.upgradeRecoveries=[];tcg.detail=null;renderUpgradeRecoveries();$('collectionCount').textContent='0';$('results').innerHTML=`<div id="firstPull" class="firstPull"><span>✧</span><strong>${tx('No cards','Chưa có thẻ')}</strong></div>`;$('cardDialog').close();$('upgradeDialog').close();$('batchDialog').close();$('accountDialog').close();applyDiscovery(response);window.HyTcgLifestyle?.reset();showView('booster');loadCollection(false);toast(tx('New account · 1,800 gems','Tài khoản mới · 1.800 gems'));}
  catch(error){$('deleteError').textContent=errorText(error.message);}finally{tcg.deleting=false;$('deleteCancel').disabled=false;updateButtons();updateLimitedButtons();}
}
// Effects are a user-controlled preference. Windows' reduced-motion setting only removes easing/autoplay.
let cardEffectsEnabled=true;
try{cardEffectsEnabled=localStorage.getItem('hytcg-card-effects')!=='off';}catch{}
function syncCardEffects(){
  document.documentElement.dataset.cardEffects=cardEffectsEnabled?'on':'off';
  $('cardEffectsButton').setAttribute('aria-pressed',String(cardEffectsEnabled));
  $('cardEffectsButton').title=cardEffectsEnabled?tx('Turn off 3D card effects','Tắt hiệu ứng card 3D'):tx('Turn on 3D card effects','Bật hiệu ứng card 3D');
  $('cardEffectsButton').setAttribute('aria-label',$('cardEffectsButton').title);
  const hint=$('cardDetail').querySelector('.detailHoverHint');if(hint)hint.textContent=cardEffectsEnabled?tx('Move over the card · 3D','Di chuột trên card · 3D'):tx('3D effects are off','Đã tắt hiệu ứng 3D');
}
$('cardEffectsButton').addEventListener('click',()=>{cardEffectsEnabled=!cardEffectsEnabled;resetTilt();syncCardEffects();try{localStorage.setItem('hytcg-card-effects',cardEffectsEnabled?'on':'off');}catch{}});
// One pointer-driven animation frame is shared by the visible card. No idle loop or listeners per card.
let tiltElement=null,tiltBounds=null,tiltFrame=0,tiltPoint=null,tiltCurrent={x:.5,y:.5},tiltTime=0;
function resetTilt(){
  if(tiltFrame)cancelAnimationFrame(tiltFrame);tiltFrame=0;tiltPoint=null;
  if(tiltElement){tiltElement.classList.remove('isInteracting');for(const name of ['--rotate-x','--rotate-y','--shine-x','--shine-y','--foil-x','--foil-y'])tiltElement.style.removeProperty(name);}
  tiltElement=tiltBounds=null;tiltCurrent={x:.5,y:.5};tiltTime=0;
}
function activateTilt(element){if(tiltElement===element&&element?.isConnected)return;resetTilt();if(!element||!cardEffectsEnabled)return;tiltElement=element;tiltBounds=element.getBoundingClientRect();element.classList.add('isInteracting');}
function queueTilt(point){
  tiltPoint=point;if(tiltFrame)return;
  const animate=time=>{
    tiltFrame=0;if(!tiltElement?.isConnected||!tiltBounds||!tiltPoint||!cardEffectsEnabled){resetTilt();return;}
    const targetX=Math.min(1,Math.max(0,(tiltPoint.x-tiltBounds.left)/tiltBounds.width)),targetY=Math.min(1,Math.max(0,(tiltPoint.y-tiltBounds.top)/tiltBounds.height)),elapsed=tiltTime?Math.min(48,time-tiltTime):16,blend=reducedMotion.matches?1:1-Math.exp(-elapsed/55);tiltTime=time;
    tiltCurrent.x+=(targetX-tiltCurrent.x)*blend;tiltCurrent.y+=(targetY-tiltCurrent.y)*blend;const {x,y}=tiltCurrent;
    tiltElement.style.setProperty('--rotate-x',((.5-y)*24).toFixed(2)+'deg');tiltElement.style.setProperty('--rotate-y',((x-.5)*32).toFixed(2)+'deg');
    tiltElement.style.setProperty('--shine-x',(x*100).toFixed(1)+'%');tiltElement.style.setProperty('--shine-y',(y*100).toFixed(1)+'%');
    tiltElement.style.setProperty('--foil-x',(35+x*30).toFixed(1)+'%');tiltElement.style.setProperty('--foil-y',(32+y*36).toFixed(1)+'%');
    tiltElement.style.setProperty('--pointer-distance',Math.min(1,Math.hypot(x-.5,y-.5)*1.6).toFixed(3));
    if(Math.abs(targetX-x)+Math.abs(targetY-y)>.001)tiltFrame=requestAnimationFrame(animate);else tiltTime=0;
  };tiltFrame=requestAnimationFrame(animate);
}
document.addEventListener('pointerover',event=>{if(event.pointerType!=='touch')activateTilt(event.target.closest('[data-tilt]'));});
document.addEventListener('pointermove',event=>{
  if(event.pointerType==='touch')return;activateTilt(event.target.closest('[data-tilt]'));if(tiltElement)queueTilt({x:event.clientX,y:event.clientY});
});
document.addEventListener('pointerout',event=>{if(tiltElement&&!tiltElement.contains(event.relatedTarget))resetTilt();});
document.addEventListener('focusin',event=>{const element=event.target.closest('[data-tilt]');if(element&&element.matches(':focus-visible')){activateTilt(element);if(tiltBounds)queueTilt({x:tiltBounds.left+tiltBounds.width*.7,y:tiltBounds.top+tiltBounds.height*.3});}});
document.addEventListener('focusout',event=>{if(tiltElement&&!tiltElement.contains(event.relatedTarget))resetTilt();});
window.addEventListener('blur',resetTilt);window.addEventListener('resize',resetTilt);document.addEventListener('scroll',resetTilt,true);reducedMotion.addEventListener('change',resetTilt);
for(const dialog of document.querySelectorAll('dialog'))dialog.addEventListener('close',resetTilt);
chrome.webview.addEventListener('message',event=>{
  const msg=event.data;if(msg.type==='state'){uiEffects.configure(msg.state?.Settings,msg.assets);bindTheme(msg.state?.Settings);return;}if(msg.session!==tcg.session)return;
  if(window.HyTcgDev?.message(msg))return;
  if(msg.dev)window.HyTcgDev?.apply(msg.dev);
  if(msg.wallet)applyWallet(msg.wallet);
  applyDiscovery(msg);
  if(msg.type==='hytcgCharacterInfo'||msg.type==='hytcgDissolvePreview'||msg.type==='hytcgLifestyle'||msg.type==='hytcgLifestyleImage'){finish(msg.requestId,msg);return;}
  if(msg.type==='hytcgConfig'){
    if(msg.error){toast(errorText(msg.error));return;}tcg.config=msg.config;tcg.cardsDirectory=msg.cardsDirectory;renderConfig();loadCollection(false);if(!tcg.wallet)refreshWallet();
    if(msg.recoveries?.length&&!tcg.slots.length){tcg.slots=msg.recoveries.map(item=>({status:'error',error:'RecoveryPending',issued:true,token:item.Token,cardNative:item.Card}));renderSlots();}
    tcg.upgradeRecoveries=(msg.upgrades||[]).filter(item=>item.Stage!=='complete');renderUpgradeRecoveries();refreshDiscovery(true);
  }else if(msg.type==='hytcgProgress'){
    const index=tcg.slots.findIndex(slot=>slot.requestId===msg.requestId),slot=tcg.slots[index];if(!slot||slot.status!=='loading')return;slot.phase=msg.phase;slot.attempt=msg.attempt;const el=$('phase-'+index);if(el)el.textContent=phaseText(slot);
  }else if(['hytcgCard','hytcgReserved','hytcgWallet','hytcgDissolved','hytcgDissolvedMany','hytcgFavorite','hytcgUpgradeOptions','hytcgUpgradeReady','hytcgUpgradeSaved','hytcgUpgradeCancelled','hytcgDiscoveryState','hytcgDexSearch','hytcgDexSeries','hytcgDexArtwork','hytcgDexImage','hytcgDexProfile','hytcgLimitedBuy','hytcgAccountDelete','hytcgReadCard','hytcgRepairCards','hytcgMissionPlay','hytcgMissionMovie'].includes(msg.type))finish(msg.requestId,msg);
  else if(msg.type==='hytcgSaved')finish(msg.cardId,msg);
  else if(msg.type==='hytcgCollection'){
    if(msg.requestId!==tcg.collectionRequest)return;if(msg.error){$('collectionStatus').textContent=errorText(msg.error);$('collectionMore').disabled=false;return;}
    tcg.collection.push(...msg.page.Items);for(const item of msg.page.Items)if(tcg.selected.has(item.Card.CardId)){if(item.CanDissolve&&!item.UpgradeLocked)tcg.selected.set(item.Card.CardId,item);else tcg.selected.delete(item.Card.CardId);}tcg.filteredTotal=msg.page.Total;tcg.hasMore=msg.page.HasMore;
    tcg.collectionNextOffset=msg.page.NextOffset??tcg.collection.length;
    if(!$('rarityFilter').value&&!$('collectionSearch').value&&!tcg.favoritesOnly){tcg.collectionTotal=msg.page.Total;$('collectionCount').textContent=tcg.collectionTotal;}renderCollection();
  }
});
$('openOne').addEventListener('click',()=>openPack(1));$('openTen').addEventListener('click',()=>openPack(10));$('buyPull').addEventListener('click',buyPull);
$('payCredits').addEventListener('click',()=>setPayment('credits'));$('payGems').addEventListener('click',()=>setPayment('gems'));
$('boosterTab').addEventListener('click',()=>showView('booster'));$('collectionTab').addEventListener('click',()=>showView('collection'));$('settingsButton').addEventListener('click',openSettings);
$('dexTab').addEventListener('click',()=>showView('dex'));$('missionsTab').addEventListener('click',()=>showView('missions'));$('buyLimited').addEventListener('click',buyLimited);
$('limitedGems').addEventListener('click',()=>{tcg.limitedPayment='gems';updateLimitedButtons();});$('limitedCredits').addEventListener('click',()=>{tcg.limitedPayment='credits';updateLimitedButtons();});
$('dexForm').addEventListener('submit',event=>{event.preventDefault();searchDex();});$('dexMore').addEventListener('click',()=>searchDex(true));$('dexMode').addEventListener('change',()=>{$('dexSearch').placeholder=$('dexMode').value==='series'?tx('Series name','Tên series'):tx('Character name','Tên nhân vật');});
$('dexArtworkMore').addEventListener('click',moreDexArtworks);
$('resetAccount').addEventListener('click',showAccountDelete);$('deleteConfirmation').addEventListener('input',updateLimitedButtons);$('confirmDelete').addEventListener('click',deleteAccount);$('accountDialog').addEventListener('cancel',event=>{if(tcg.deleting)event.preventDefault();});
$('folderButton').addEventListener('click',()=>send('hytcgFolder'));$('refreshCollection').addEventListener('click',()=>loadCollection());$('collectionMore').addEventListener('click',()=>loadCollection(true));$('rarityFilter').addEventListener('change',()=>{tcg.selected.clear();loadCollection();renderSelection();});
$('expandPanel').addEventListener('click',()=>send('hytcgPanelSize'));
$('collectionSort').addEventListener('change',()=>{tcg.selected.clear();loadCollection();renderSelection();});
$('resetFilters').addEventListener('click',()=>{$('collectionSearch').value='';$('rarityFilter').value='';$('collectionSort').value='newest';tcg.favoritesOnly=false;tcg.selected.clear();$('favoritesFilter').classList.remove('active');$('favoritesFilter').setAttribute('aria-pressed','false');loadCollection();renderSelection();});
$('collectionSearch').addEventListener('input',()=>{clearTimeout(searchTimer);searchTimer=setTimeout(()=>{tcg.selected.clear();loadCollection();renderSelection();},300);});
$('favoritesFilter').addEventListener('click',()=>{tcg.favoritesOnly=!tcg.favoritesOnly;tcg.selected.clear();$('favoritesFilter').classList.toggle('active',tcg.favoritesOnly);$('favoritesFilter').setAttribute('aria-pressed',String(tcg.favoritesOnly));loadCollection();renderSelection();});
$('selectVisible').addEventListener('click',selectVisible);$('clearSelection').addEventListener('click',clearSelection);$('dissolveSelected').addEventListener('click',confirmBatch);$('confirmBatchDissolve').addEventListener('click',dissolveMany);
$('materialSame').addEventListener('click',()=>setMaterialMode('same'));$('materialTen').addEventListener('click',()=>setMaterialMode('ten'));$('materialLoadMore').addEventListener('click',()=>loadUpgradeMaterials(true));$('confirmUpgrade').addEventListener('click',performUpgrade);$('cancelUpgrade').addEventListener('click',cancelUpgrade);$('closeUpgrade').addEventListener('click',cancelUpgrade);$('resumeUpgrade').addEventListener('click',resumeUpgrade);
let materialSearchTimer;$('materialSearch').addEventListener('input',()=>{clearTimeout(materialSearchTimer);materialSearchTimer=setTimeout(()=>loadUpgradeMaterials(false),300);});
$('upgradeDialog').addEventListener('cancel',event=>{event.preventDefault();cancelUpgrade();});$('batchDialog').addEventListener('cancel',event=>{if(tcg.dissolving)event.preventDefault();});
$('batchDialog').addEventListener('close',()=>{tcg.batchPreviewGeneration=(tcg.batchPreviewGeneration||0)+1;tcg.previewBusy=false;renderSelection();});
document.addEventListener('click',event=>{
  const button=event.target.closest('button');if(!button)return;
  if(button.dataset.banner!==undefined){const banner=tcg.banners[Number(button.dataset.banner)];if(banner)openPack(Number(button.dataset.count),tcg.limitedPayment==='credits'?'limited':'limitedGems',field(banner,'Id'));}
  if(button.hasAttribute('data-refresh-banners'))refreshDiscovery(true);if(button.dataset.dexSeries!==undefined)searchDex(false,Number(button.dataset.dexSeries));if(button.dataset.dexCharacter!==undefined)viewDexCharacter(Number(button.dataset.dexCharacter));if(button.dataset.dexArtwork!==undefined)viewDexArtwork(Number(button.dataset.dexArtwork));if(button.dataset.missionPlay)playMission(button);
  if(button.hasAttribute('data-mission-movie'))call('hytcgMissionMovie').catch(error=>toast(errorText(error.message)));
  if(button.dataset.close)$(button.dataset.close).close();if(button.dataset.retry!==undefined)retrySlot(Number(button.dataset.retry));if(button.dataset.save!==undefined)saveSlot(Number(button.dataset.save));if(button.dataset.detail!==undefined)detail(Number(button.dataset.detail),button.dataset.collection==='true');
  if(button.dataset.select!==undefined)toggleSelection(Number(button.dataset.select));if(button.dataset.favorite!==undefined)toggleFavorite(tcg.collection[Number(button.dataset.favorite)]?.Card.CardId);if(button.dataset.material!==undefined)selectMaterial(Number(button.dataset.material));
  if(button.id==='detailFavorite')toggleFavorite(tcg.detail?.card.CardId);if(button.id==='startUpgrade')startUpgrade();
  if(button.id==='detailStudio'&&tcg.detail){const id=tcg.detail.card.CardId;$('cardDialog').close();window.HyTcgLifestyle?.showStudio(id);}
  if(button.id==='detailDating'&&tcg.detail){const id=tcg.detail.card.CardId;$('cardDialog').close();window.HyTcgLifestyle?.showDating(id);}
  if(button.id==='dissolveButton'){$('dissolveButton').hidden=true;$('dissolveConfirm').hidden=false;$('confirmDissolve').focus();}
  if(button.id==='cancelDissolve'){$('dissolveConfirm').hidden=true;$('dissolveButton').hidden=false;$('dissolveButton').focus();}
  if(button.id==='confirmDissolve')dissolveCard();
  if(button.dataset.source){const target=button.dataset.source==='acdb'?'https://www.animecharactersdatabase.com/characters.php?id='+$('cardDialog').dataset.character:$('cardDialog').dataset.artwork;if(/^https:\/\//.test(target))chrome.webview.postMessage({type:'openExternal',target});}
  if(button.dataset.anilist&&/^\d+$/.test(button.dataset.anilist))chrome.webview.postMessage({type:'openExternal',target:'https://anilist.co/character/'+button.dataset.anilist});
});
const cosmeticRefreshVersion=new Map();
document.addEventListener('hytcgCosmeticChanged',async event=>{
  const id=event.detail?.cardId;if(!id)return;
  const slots=tcg.slots.filter(slot=>slot.saved&&slot.card?.CardId===id),current=tcg.detail;
  if(!slots.length&&current?.card.CardId!==id)return;
  const version=(cosmeticRefreshVersion.get(id)||0)+1;cosmeticRefreshVersion.set(id,version);
  try{
    const response=await call('hytcgReadCard',{cardId:id});
    if(cosmeticRefreshVersion.get(id)!==version||!response.imageData)return;
    for(const slot of slots)if(tcg.slots.includes(slot)&&slot.card?.CardId===id){slot.png=response.imageData;slot.cosmetic=response.cosmetic||null;}
    if(slots.length)renderSlots();
    if(tcg.detail===current&&current?.card.CardId===id&&$('cardDialog').open){$('cardDetail').querySelector('.detailImage').src=response.imageData;window.HyTcgLifestyle?.decorate($('cardDetail').querySelector('.detailCard'),id,response.cosmetic||null);const badges=$('cardDetail').querySelector('.detailBadges');badges.querySelector('.altLabel')?.remove();if(current.card.ImageSource==='zerochan'&&!response.cosmetic?.ArtworkId)badges.insertAdjacentHTML('beforeend','<span class="altLabel">ALT</span>');}
  }catch{}
});
document.addEventListener('visibilitychange',()=>{resetTilt();if(!document.hidden){refreshWallet();refreshDiscovery();}});
setInterval(()=>{if(!document.hidden)renderWallet();},1000);setInterval(()=>{if(!document.hidden){refreshWallet();const expiry=Date.parse(field(tcg.banners[0],'ExpiresAt'));if(Number.isFinite(expiry)&&Date.now()>=expiry)refreshDiscovery();}},25000);
updateButtons();translate();send('ready');send('hytcgInit');
