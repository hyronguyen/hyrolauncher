'use strict';
/* Atelier stays independent of collection paging and never hydrates the entire vault. */
(() => {
  const state = { view:'album', epoch:0, activation:0, data:null, pending:null, busy:false, album:null, studio:null, studioPanel:'style', quote:null, quoteKey:'', quotePending:null, quoteGeneration:0, quoteTimer:0, itemPicker:null, inventoryFilter:'all', inventoryQuery:'', paidOperation:null, lastDyeId:'', expeditionTeam:[], arenaTeam:[], routeId:'', datingId:'', inventoryTarget:'', result:null, picker:null, pickerTimer:0, extraCards:new Map(), extraProfiles:new Map(), metadataQueue:[], metadataPending:new Map(), metadataWorkers:0, metadataWanted:new Set(), metadataFailures:new Map(), timer:0, imageWorkers:0, imageQueue:[], imageCache:new Map(), imagePending:new Map(), imageVersions:new Map(), imageRevision:0, canvasData:'', dirty:false };
  const content = $('lifestyleContent');
  const observedImages=new Set();
  const safe = escapeText;
  const field = (obj,key,fallback='') => obj?.[key] ?? obj?.[key[0].toLowerCase()+key.slice(1)] ?? fallback;
  const creative = () => state.data?.creative || {};
  const gameplay = () => state.data?.gameplay || {};
  const roster = () => [...new Map([...(state.data?.roster||[]),...state.extraCards.values()].map(x=>[String(field(x,'CardId')),x])).values()];
  const profiles = () => [...new Map([...field(gameplay(),'Profiles',[]),...state.extraProfiles.values()].map(x=>[String(field(x,'CardId')),x])).values()];
  const card = id => roster().find(x=>String(field(x,'CardId'))===String(id));
  const profile = id => profiles().find(x=>String(field(x,'CardId'))===String(id));
  const characterBusy = id => Boolean(field(profile(id),'Busy',false));
  const cosmetic = id => field(creative(),'Cosmetics',[]).find(x=>String(field(x,'CardId'))===String(id));
  const albums = () => field(creative(),'Albums',[]);
  const inventory = () => field(gameplay(),'Inventory',[]);
  const routes = () => field(gameplay(),'Routes',[]);
  const routeName = route => ({meadow:tx('Moonlit meadow','Đồng cỏ ánh trăng'),observatory:tx('Starlight observatory','Đài thiên văn ánh sao'),ruins:tx('Ancient sky ruins','Di tích trên mây')})[field(route,'Id')]||field(route,'Name',tx('Expedition','Thám hiểm'));
  const itemName = item => ({training:tx('Training manual','Sách huấn luyện'),tea:tx('Tea set','Bộ trà'),gift:tx('Keepsake ribbon','Ruy băng kỷ niệm'),fiber:tx('Fiber','Sợi'),petal:tx('Sakura petals','Cánh hoa anh đào'),crystal:tx('Arcane crystal','Pha lê huyền thuật')})[field(item,'Id')]||field(item,'Name');
  const enemyName = fight => field(fight,'EnemyName')==='Sky guardian'?tx('Sky guardian','Hộ vệ bầu trời'):tx('Training sentinel','Lính gác huấn luyện');
  const num = value => Number(value)||0;
  const name = id => field(card(id),'Name',field(profile(id),'Name',tx('Unavailable card','Thẻ không còn tồn tại')));
  const icons = {
    album:'<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M8 4v16m4-11h5m-5 5h5"/>',
    studio:'<path d="m15 3 6 6-9 9-6-6zM6 12l-3 9 9-3M13 5l6 6"/>',
    expedition:'<path d="m3 5 6-2 6 2 6-2v16l-6 2-6-2-6 2zM9 3v16m6-14v16"/>',
    inventory:'<rect x="4" y="5" width="16" height="16" rx="2"/><path d="M8 5V3h8v2M4 11h16m-10 0v4h4v-4"/>',
    arena:'<path d="m4 3 8 8-3 3-8-8zM20 3l-8 8 3 3 8-8M7 13l-4 4m14-4 4 4M2 15l5 5m10 0 5-5"/>',
    dating:'<path d="M20.5 5.5a5 5 0 0 0-7.1 0L12 7l-1.4-1.5a5 5 0 0 0-7.1 7.1L12 21l8.5-8.4a5 5 0 0 0 0-7.1Z"/>'
  };
  const labels = () => ({album:tx('Album','Album'),studio:tx('Studio','Studio'),expedition:tx('Explore','Thám hiểm'),inventory:tx('Items','Túi đồ'),arena:tx('Arena','Đấu trường'),dating:tx('Bond','Gắn kết')});
  const svg = key => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icons[key]||icons.album}</svg>`;
  const btn = (text,action,extra='',type='') => `<button class="lifeButton ${type}" data-life-action="${action}" ${extra}>${text}</button>`;
  const sectionHeading = (title,sub='') => `<div class="lifeSectionHeading"><h2>${title}</h2>${sub?`<span>${sub}</span>`:''}</div>`;
  const empty = (title,key='album',sub='') => `<div class="lifeEmpty">${svg(key)}<strong>${title}</strong>${sub}</div>`;
  const backgrounds = () => ({midnight:tx('Midnight','Đêm sao'),sakura:tx('Sakura','Anh đào'),forest:tx('Forest','Rừng xanh'),aurora:tx('Aurora','Cực quang'),royal:tx('Royal','Hoàng gia'),paper:tx('Paper','Trang giấy')});
  const validColor = (value,fallback='#c49bf7') => /^#[0-9a-f]{6}$/i.test(value||'')?value:fallback;
  const rarityColor = id => validColor(field(card(id),'RarityColor'),validColor(tcg.config?.DropRates?.find(x=>x.Name===field(card(id),'Rarity'))?.Color));
  const dyes = () => field(creative(),'Dyes',[]);
  const dye = id => dyes().find(x=>String(field(x,'Id'))===String(id));
  const gems = () => num(field(tcg.wallet||state.data?.wallet,'Gems'));
  const dyePrice = () => num(field(creative(),'DyeBoosterPrice',50));
  const rarityFrameLabel = id => field(card(id),'Rarity',tx('Rarity frame','Khung theo độ hiếm'));
  const dyeLabel = item => (field(item,'Mystic',false)?'Mystic · ':'')+field(item,'Name',field(item,'Color'));
  const restorableLook = id => field(creative(),'RestorableLooks',[]).find(x=>String(field(x,'CardId'))===String(id));
  const artworkPosition = s => ({artworkScale:Math.min(4,Math.max(1,Number(s?.artworkScale)||1)),artworkX:Math.min(1,Math.max(-1,Number(s?.artworkX)||0)),artworkY:Math.min(1,Math.max(-1,Number(s?.artworkY)||0))});
  const artworkRegion = id => ['Common','Uncommon','Rare','SR'].includes(field(card(id),'Rarity'))?{kind:'window',x:.07,y:.035,width:.86,height:.59}:{kind:'full',x:34/600,y:34/840,width:532/600,height:772/840};
  const studioPayload = (s=state.studio) => ({cardId:s.cardId,frame:'classic',frameItemId:'',dyeId:s.dyeId||'',artworkId:s.artworkId||'',...artworkPosition(s)});
  const quoteMatches = () => state.studio&&state.quote&&state.quoteKey===JSON.stringify(studioPayload());
  function studioChanged() {
    const s=state.studio;if(!s)return false;const c=cosmetic(s.cardId);
    const original=artworkPosition({artworkScale:field(c,'ArtworkScale',1),artworkX:field(c,'ArtworkX',0),artworkY:field(c,'ArtworkY',0)}),position=artworkPosition(s);
    return s.dyeId!==field(c,'DyeId','')||s.artworkId!==field(c,'ArtworkId','')||JSON.stringify(position)!==JSON.stringify(original);
  }
  const bgClass = value => `lifeBackground-${Object.hasOwn(backgrounds(),value)?value:'midnight'}`;
  const options = (values,current) => Object.entries(values).map(([id,label])=>`<option value="${safe(id)}" ${id===String(current)?'selected':''}>${safe(label)}</option>`).join('');
  const duration = seconds => {
    const n=Math.max(0,Math.ceil(seconds));
    if(n>=3600)return Math.floor(n/3600)+'h '+Math.floor(n%3600/60)+'m';
    if(n>=60)return Math.floor(n/60)+'m '+String(n%60).padStart(2,'0')+'s';
    return n+'s';
  };
  function serverNow() { return state.data?.serverReceived ? state.data.serverStamp+Date.now()-state.data.serverReceived : Date.now(); }
  function timeLeft(expedition) { return Math.max(0,(Date.parse(field(expedition,'EndsAt'))-serverNow())/1000)||0; }
  function miniCard(id,extra='',c=null) {
    if(!id)return `<button class="lifeAlbumAdd lifeBlankSlot" ${extra}><b>+</b>${tx('Empty slot','Ô trống')}</button>`;
    const item=card(id),label=name(id);
    return `<div class="lifeMiniCard" data-life-card="${safe(id)}" ${extra}><img data-life-image="${safe(id)}" alt="${safe(label)}" loading="lazy" decoding="async"><div class="lifeImageFallback" aria-hidden="true">✧<small>${safe(label)}</small></div></div>`;
  }
  function nav() {
    const header=document.querySelector('.tcgTabs'),navigation=$('lifestyleNav');if(navigation.parentElement!==header)header.insertBefore(navigation,$('folderButton'));
    $('lifestyleTab').hidden=true;
    if($('lifestyleRefresh').parentElement!==header)header.insertBefore($('lifestyleRefresh'),$('folderButton'));
    $('lifestyleRefresh').hidden=tcg.view!=='lifestyle';
    queueMicrotask(()=>{for(const button of navigation.querySelectorAll('button')){const label=labels()[button.dataset.lifeView];button.title=label;button.setAttribute('aria-label',label);button.querySelector('span')?.remove();button.classList.toggle('active',tcg.view==='lifestyle'&&button.dataset.lifeView===state.view);}});
    $('lifestyleTab').textContent=tx('Atelier','Atelier');
    $('lifestyleTitle').textContent=tx('Your characters, your world','Thế giới của những nhân vật');
    $('lifestyleRefresh').title=tx('Refresh','Làm mới');
    $('lifestyleRefresh').setAttribute('aria-label',tx('Refresh','Làm mới'));
    $('lifestyleNav').innerHTML=Object.entries(labels()).map(([key,label])=>`<button data-life-view="${key}" class="${key===state.view?'active':''}" aria-selected="${key===state.view}">${svg(key)}<span>${label}</span></button>`).join('');
  }
  function status(message='',error=false) { $('lifestyleStatus').textContent=message; $('lifestyleStatus').classList.toggle('error',error); }
  const errorLabels = {
    AlbumInvalid:['Check the album name and cards.','Kiểm tra tên album và danh sách thẻ.'], AlbumNotFound:['This album is no longer available.','Album này không còn tồn tại.'], AlbumLimit:['Album limit reached.','Đã đạt giới hạn album.'], AlbumCardsLimit:['Buy a slot to add more cards. Maximum 120 slots.','Mua thêm ô để đặt thẻ. Tối đa 120 ô.'],
    CardNotFound:['This card is no longer available.','Thẻ này không còn tồn tại.'], CardBusy:['This character is on an expedition.','Nhân vật đang đi thám hiểm.'], ExpeditionBusy:['Choose characters who are available.','Chọn nhân vật đang sẵn sàng.'], TeamInvalid:['Choose 1–3 available characters.','Chọn 1–3 nhân vật sẵn sàng.'], EffortInsufficient:['Your team needs more effort.','Đội cần thêm điểm nỗ lực.'], ExpeditionEffort:['Your team needs more effort.','Đội cần thêm điểm nỗ lực.'], ExpeditionNotReady:['The expedition is still in progress.','Chuyến thám hiểm chưa hoàn tất.'], ExpeditionClaimed:['Rewards already claimed.','Đã nhận phần thưởng.'], ExpeditionLimit:['No expedition starts left today.','Hôm nay đã hết lượt thám hiểm.'],
    DailyLimit:['Daily limit reached.','Đã hết lượt hôm nay.'], FightLimit:['No arena attempts left today.','Hôm nay đã hết lượt đấu.'], DateLimit:['No dates left today.','Hôm nay đã hết lượt hẹn.'], DateDailyLimit:['No dates left today.','Hôm nay đã hết lượt hẹn.'], ItemInsufficient:['You do not have this item.','Bạn đã hết vật phẩm này.'], InventoryInsufficient:['You do not have this item.','Bạn đã hết vật phẩm này.'], ItemInvalid:['This item cannot be used here.','Không dùng được vật phẩm này tại đây.'],
    ArtworkInvalid:['Choose a valid PNG or JPG image.','Chọn ảnh PNG hoặc JPG hợp lệ.'], ArtworkUrlInvalid:['Enter a public HTTPS image URL.','Nhập liên kết HTTPS công khai của ảnh.'], ArtworkTooLarge:['The image is too large.','Ảnh vượt quá dung lượng cho phép.'], ArtworkBlocked:['This image address is unavailable.','Địa chỉ ảnh này không khả dụng.'], InvalidUrl:['Enter a public HTTPS image URL.','Nhập liên kết HTTPS công khai của ảnh.'], ArtworkCanceled:['No image selected.','Chưa chọn ảnh.'], CosmeticInvalid:['Check the cosmetic options.','Kiểm tra tùy chọn trang trí.'], RouteInvalid:['Choose an expedition route.','Chọn tuyến thám hiểm.'], StageInvalid:['This arena stage is not available.','Chặng đấu này chưa mở.']
  };
  Object.assign(errorLabels,{
    AlbumUnavailable:errorLabels.AlbumNotFound,CardUnavailable:errorLabels.CardNotFound,ArtworkUnavailable:['This artwork is unavailable.','Artwork này không còn khả dụng.'],ArtworkLimit:['Artwork library is full.','Kho artwork đã đầy.'],ExpeditionUnavailable:['This expedition is unavailable.','Chuyến thám hiểm không còn khả dụng.'],ExpeditionDailyLimit:errorLabels.ExpeditionLimit,ExpeditionActiveLimit:['Claim an expedition before starting another.','Nhận thưởng một chuyến trước khi bắt đầu chuyến mới.'],FightDailyLimit:errorLabels.FightLimit,FightStageLocked:errorLabels.StageInvalid,GameplayInvalid:['Choose a valid character or team.','Chọn nhân vật hoặc đội hợp lệ.'],DateInvalid:['Choose a dating activity.','Chọn hoạt động hẹn hò.'],LevelMaximum:['This character has reached the level limit.','Nhân vật đã đạt giới hạn cấp.'],BondMaximum:['This character has reached the bond limit.','Nhân vật đã đạt giới hạn gắn kết.']
  });
  errorLabels.CardLocked=['This character is busy.','Nhân vật đang bận.'];
  Object.assign(errorLabels,{AlbumPurchaseRequired:['Buy an album first.','Mua album trước.'],AlbumRestyleRequired:['Change the cover and pages with Restyle.','Đổi bìa và trang qua tùy chọn trang trí.'],BattleActive:['Finish the current battle.','Hoàn tất trận đang diễn ra.'],BattleTurnExpired:['This turn has already changed.','Lượt này đã thay đổi.'],BattleComplete:['This battle has ended.','Trận đấu đã kết thúc.'],SkillUnavailable:['This skill is not available.','Kỹ năng chưa khả dụng.'],SkillsInvalid:['Choose up to 4 unlocked skills.','Chọn tối đa 4 kỹ năng đã mở.'],BattleTargetInvalid:['Choose a living teammate.','Chọn đồng đội còn HP.']});
  Object.assign(errorLabels,{
    DyeUnavailable:['This dye is no longer in your inventory.','Màu nhuộm này không còn trong túi đồ.'],DyeInsufficient:['You have used all of this dye.','Bạn đã dùng hết màu nhuộm này.'],DyeInvalid:['Choose a dye from your inventory.','Chọn màu nhuộm trong túi đồ.'],DyeLimit:['Your dye inventory is full.','Kho màu nhuộm đã đầy.'],FrameRetired:['Frames follow the card rarity.','Khung theo độ hiếm của thẻ.'],StudioOperationInvalid:['Refresh the studio and try again.','Làm mới Studio rồi thử lại.'],StudioRequestExpired:['This action has expired. Refresh the studio.','Thao tác đã hết hạn. Hãy làm mới Studio.'],StudioReplayMismatch:['Choose the style again and retry.','Chọn lại trang trí rồi thử lại.'],StudioSequenceInvalid:['Refresh the studio and try again.','Làm mới Studio rồi thử lại.'],GemsInsufficient:['Not enough gems.','Không đủ Gems.']
  });
  function errorMessage(error) { return errorLabels[error?.message]?tx(...errorLabels[error.message]):typeof errors!=='undefined'&&errors[error?.message]?errorText(error.message):tx('Could not complete this action. Try again.','Chưa hoàn tất thao tác. Hãy thử lại.'); }
  function apply(data) {
    if(!data)return;
    if(data.creative){state.quote=null;state.quoteKey='';state.quoteGeneration++;}
    if(data.creative&&JSON.stringify(field(creative(),'Cosmetics',[]))!==JSON.stringify(field(data.creative,'Cosmetics',[]))){state.imageCache.clear();state.imageRevision++;}
    const previousCards=new Map((state.data?.roster||[]).map(x=>[String(field(x,'CardId')),x]));
    if(data.roster)for(const item of data.roster){const id=String(field(item,'CardId')),old=previousCards.get(id);if(old&&(field(old,'Rarity')!==field(item,'Rarity')||field(old,'Orientation')!==field(item,'Orientation')))invalidateImages(id);}
    if(data.creative){const previousLooks=new Map(field(creative(),'Cosmetics',[]).map(x=>[String(field(x,'CardId')),JSON.stringify(x)])),currentLooks=new Map(field(data.creative,'Cosmetics',[]).map(x=>[String(field(x,'CardId')),JSON.stringify(x)]));for(const id of new Set([...previousLooks.keys(),...currentLooks.keys()]))if(previousLooks.get(id)!==currentLooks.get(id))invalidateImages(id);}
    state.data={...state.data,...data};
    if(data.roster)for(const item of data.roster)state.extraCards.delete(String(field(item,'CardId')));
    if(data.gameplay)for(const item of field(data.gameplay,'Profiles',[]))state.extraProfiles.delete(String(field(item,'CardId')));
    if(data.wallet&&typeof applyWallet==='function')applyWallet(data.wallet);
    if(data.missions&&typeof applyDiscovery==='function')applyDiscovery(data);
    const now=data.serverNow||field(data.gameplay,'ServerNow');
    if(now){state.data.serverStamp=Date.parse(now)||Date.now();state.data.serverReceived=Date.now();}
    const available=new Set(roster().map(x=>String(field(x,'CardId'))));
    state.expeditionTeam=state.expeditionTeam.filter(id=>available.has(String(id)));
    state.arenaTeam=state.arenaTeam.filter(id=>available.has(String(id)));
    if(data.result)state.result=data.result;
  }
  async function refresh() {
    if(state.pending)return state.pending;
    const epoch=state.epoch;
    status(tx('Loading your world…','Đang tải thế giới của bạn…'));
    setBusy(true);
    const request=(async()=>{try{const data=await call('hytcgLifestyle',{action:'snapshot'});if(epoch!==state.epoch)return null;apply(data);status();render();return state.data;}catch(error){if(epoch!==state.epoch)return null;status(errorMessage(error),true);if(!state.data)content.innerHTML=empty(tx('Your world is unavailable','Chưa tải được thế giới'),'studio')+btn(tx('Try again','Thử lại'),'refresh');}finally{if(state.pending===request)state.pending=null;if(epoch===state.epoch&&!state.busy)setBusy(false);}})();
    state.pending=request;
    return state.pending;
  }
  async function run(action,payload={}) {
    if(state.busy)return null;
    const epoch=state.epoch;
    state.busy=true;status(tx('Working…','Đang xử lý…'));setBusy(true);
    try{if(state.pending)await state.pending;if(epoch!==state.epoch)return null;const response=await call('hytcgLifestyle',{action,...payload});if(epoch!==state.epoch)return null;apply(response);for(const id of new Set([payload.cardId,...(payload.cardIds||[]),...field(response.result,'CardIds',[])].filter(Boolean))){if(!field(gameplay(),'Profiles',[]).some(x=>String(field(x,'CardId'))===String(id)))await ensureCard(id);}if(epoch!==state.epoch)return null;status();return response;}catch(error){if(epoch===state.epoch)status(errorMessage(error),true);return null;}finally{if(epoch===state.epoch){state.busy=false;setBusy(false);}}
  }
  async function paidRun(action,payload={}) {
    const epoch=state.epoch;if(state.pending)await state.pending;if(epoch!==state.epoch)return null;
    const key=action+JSON.stringify(payload);
    if(state.paidOperation?.key!==key)state.paidOperation={key,operationId:'SC-'+num(field(creative(),'NextStudioSequence',1))+'-'+crypto.randomUUID().replaceAll('-','').toLowerCase()};
    const operation=state.paidOperation,response=await run(action,{...payload,operationId:operation.operationId});
    if(response&&state.paidOperation===operation)state.paidOperation=null;
    return response;
  }
  function setBusy(value) {
    $('lifestyleRefresh').disabled=value;
    content.setAttribute('aria-busy',String(value));
    renderBooster();
    for(const node of content.querySelectorAll('button')){if(value){if(!('lifeWasDisabled' in node.dataset))node.dataset.lifeWasDisabled=String(node.disabled);node.disabled=true;}else if('lifeWasDisabled' in node.dataset){node.disabled=node.dataset.lifeWasDisabled==='true';delete node.dataset.lifeWasDisabled;}}
  }
  function render() {
    nav();
    if(!state.data)return;
    const renderers={album:renderAlbum,studio:renderStudio,expedition:renderExpedition,inventory:renderInventory,arena:renderArena,dating:renderDating};
    const enhanced=window.HyTcgAdventure?.renderView(state.view);
    content.innerHTML=enhanced??renderers[state.view]();
    window.HyTcgAdventure?.afterRender(content);
    applyVisualStyles(content);
    watchImages(content);
    if(state.view==='studio')bindStudio();
    if(state.view==='album')bindAlbumDrag();
    if(state.view==='album'&&state.album)hydrateAlbumMetadata(state.album.cardIds.slice((state.albumPage||0)*12,(state.albumPage||0)*12+12));else{state.metadataWanted.clear();state.metadataQueue=[];}
    syncAvailability();
    if(state.busy||state.pending)setBusy(true);
    updateTimers();
    renderBooster();
  }
  function applyVisualStyles(root) {
    for(const node of root.querySelectorAll('[data-life-progress]'))node.style.setProperty('--life-progress',Math.max(0,Math.min(100,num(node.dataset.lifeProgress)))+'%');
    for(const node of root.querySelectorAll('[data-life-color]'))node.style.setProperty('--life-swatch',validColor(node.dataset.lifeColor));
    for(const node of root.querySelectorAll('[data-life-mystic-color]'))node.style.setProperty('--life-mystic-color',validColor(node.dataset.lifeMysticColor));
    for(const node of root.querySelectorAll('[data-life-card]'))decorate(node,node.dataset.lifeCard);
  }
  function syncAvailability() {
    const disable=(selector,condition)=>{if(condition)for(const node of content.querySelectorAll(selector))node.disabled=true;};
    disable('[data-life-action="fight"]',state.arenaTeam.some(characterBusy));
    disable('[data-life-action="expeditionStart"]',state.expeditionTeam.some(characterBusy)||field(gameplay(),'Expeditions',[]).filter(x=>!field(x,'Claimed',false)).length>=num(field(gameplay(),'ExpeditionActiveLimit',3)));
    disable('[data-life-action="inventoryUse"]',characterBusy(state.inventoryTarget));
    disable('[data-life-action="cosmeticSave"],[data-life-action="cosmeticReset"],[data-life-action="cosmeticRestore"]',state.studio&&characterBusy(state.studio.cardId));
    disable('[data-life-action="cosmeticSave"]',!studioChanged()||!quoteMatches()||num(field(state.quote,'TotalGems'))>gems());
    for(const node of content.querySelectorAll('.lifeTeamSlot')){const id=node.querySelector('[data-life-card]')?.dataset.lifeCard,label=node.querySelector('.lifeSlotActions>span');if(id&&label&&characterBusy(id))label.textContent+=' · '+tx('Busy','Bận');}
    for(const node of content.querySelectorAll('.lifeCharacterButton')){const id=node.querySelector('[data-life-card]')?.dataset.lifeCard,label=node.querySelector(':scope > div > small');if(id&&label&&characterBusy(id))label.textContent=tx('Busy · claim expedition or finish upgrade','Đang bận · nhận thưởng hoặc hoàn tất nâng cấp');}
  }
  function startTimers() { clearInterval(state.timer);if(!$('lifestyleView').hidden&&!document.hidden)state.timer=setInterval(updateTimers,1000); }
  function hide() { clearInterval(state.timer);state.timer=0;state.metadataWanted.clear();state.metadataQueue=[];nav(); }
  function reset() {
    window.HyTcgAdventure?.reset();
    hide();state.epoch++;state.activation++;state.pending=null;state.busy=false;state.data=null;state.album=null;state.studio=null;state.studioId='';state.studioPanel='style';state.quote=null;state.quoteKey='';state.quoteGeneration++;clearTimeout(state.quoteTimer);state.itemPicker=null;state.paidOperation=null;state.lastDyeId='';state.datingId='';state.inventoryTarget='';state.inventoryFilter='all';state.inventoryQuery='';state.expeditionTeam=[];state.arenaTeam=[];state.result=null;state.canvasData='';state.extraCards.clear();state.extraProfiles.clear();state.metadataFailures.clear();state.imageCache.clear();state.imageVersions.clear();state.imageRevision=0;state.imageQueue=[];state.picker=null;clearTimeout(state.pickerTimer);
    for(const id of ['lifestylePicker','lifestyleAlbumDialog','lifestyleItemPicker'])if($(id)?.open)$(id).close();
    setBusy(false);content.replaceChildren();status();
  }
  function updateTimers() {
    if($('lifestyleView').hidden||document.hidden){hide();return;}
    for(const node of content.querySelectorAll('[data-life-countdown]')){
      const expedition=field(gameplay(),'Expeditions',[]).find(x=>String(field(x,'Id'))===node.dataset.lifeCountdown);
      if(!expedition)continue;
      const left=timeLeft(expedition),ready=field(expedition,'Ready',false)||left<=0;
      node.textContent=ready?tx('Ready to claim','Sẵn sàng nhận'):duration(left);
      const button=content.querySelector(`[data-life-claim="${CSS.escape(node.dataset.lifeCountdown)}"]`);
      if(button)button.disabled=state.busy||Boolean(state.pending)||!ready;
    }
    for(const node of content.querySelectorAll('[data-life-reset]')){const left=Math.max(0,(Date.parse(field(gameplay(),'NextResetAt'))-serverNow())/1000);node.textContent=tx('Resets in ','Làm mới sau ')+duration(left);}
  }
  function open(view=state.view) {
    if(Object.hasOwn(labels(),view))state.view=view;
    $('lifestyleView').hidden=false;
    nav();startTimers();
    if(state.data)render();
    return state.busy?Promise.resolve(state.data):refresh();
  }
  async function activate(view,id) {
    const activation=++state.activation,epoch=state.epoch;
    if(id&&view==='studio'){state.studio=null;state.studioId=String(id);state.canvasData='';}
    if(id&&view==='dating')state.datingId=String(id);
    if(typeof showView==='function')showView('lifestyle');
    open(view);
    if(id){if(state.pending)await state.pending;if(!card(id)||!profile(id))await ensureCard(id);if(activation!==state.activation||epoch!==state.epoch)return;if(view==='studio'){state.studioId=String(id);state.studio=null;}if(view==='dating')state.datingId=String(id);if(state.view===view)render();}
  }
  function rememberCards(data) {
    for(const item of data?.roster||[]){const id=String(field(item,'CardId'));state.extraCards.delete(id);state.extraCards.set(id,item);}
    for(const item of data?.profiles||[]){const id=String(field(item,'CardId'));state.extraProfiles.delete(id);state.extraProfiles.set(id,item);}
    trimMetadata();
  }
  function trimMetadata() {
    const pinned=new Set([state.studioId,state.datingId,state.inventoryTarget,...state.arenaTeam,...state.expeditionTeam,...(state.album?.cardIds||[])].filter(Boolean).map(String));
    for(const collection of [state.extraCards,state.extraProfiles])for(const id of collection.keys()){if(collection.size<=200)break;if(!pinned.has(id))collection.delete(id);}
  }
  async function ensureCard(id,quiet=false) {
    const epoch=state.epoch;
    try{const data=await call('hytcgLifestyle',{action:'profile',cardId:String(id)});if(epoch!==state.epoch)return null;const p=data.profile||data.Profile,c=data.card||data.Card;if(c)state.extraCards.set(String(id),c);if(p){state.extraProfiles.set(String(id),p);if(!card(id))state.extraCards.set(String(id),{...p,Series:'',Orientation:'portrait'});}trimMetadata();return p||c||null;}catch(error){if(epoch===state.epoch&&!quiet)status(errorMessage(error),true);}
    return null;
  }
  function hydrateAlbumMetadata(ids) {
    state.metadataWanted=new Set(ids.filter(Boolean).map(String));
    state.metadataQueue=state.metadataQueue.filter(id=>state.metadataWanted.has(id));
    for(const id of state.metadataWanted)if(!card(id)&&!state.metadataPending.has(id)&&!state.metadataQueue.includes(id)&&Date.now()-(state.metadataFailures.get(id)||0)>60000)state.metadataQueue.push(id);
    pumpAlbumMetadata();
  }
  function pumpAlbumMetadata() {
    while(state.metadataWorkers<2&&state.metadataQueue.length){
      const id=state.metadataQueue.shift();if(!state.metadataWanted.has(id)||($('lifestyleView').hidden&&!$('lifestyleAlbumDialog').open))continue;
      const epoch=state.epoch;state.metadataWorkers++;
      const request=ensureCard(id,true);state.metadataPending.set(id,request);
      request.then(value=>{if(epoch!==state.epoch)return;if(!value)state.metadataFailures.set(id,Date.now());else for(const node of document.querySelectorAll(`[data-life-card="${CSS.escape(id)}"]`)){const image=node.querySelector('img');if(image)image.alt=name(id);const caption=node.querySelector('.lifeImageFallback small');if(caption)caption.textContent=name(id);}}).finally(()=>{if(state.metadataPending.get(id)===request)state.metadataPending.delete(id);state.metadataWorkers--;pumpAlbumMetadata();});
    }
  }
  function albumDraft(source=null) { return {id:field(source,'Id',''),name:field(source,'Name',tx('My album','Album của tôi')),background:field(source,'Background','midnight'),cardIds:[...field(source,'CardIds',[])]}; }
  function renderAlbum() {
    const list=albums();
    const tiles=list.map(a=>`<button class="lifeAlbumTile ${state.album?.id===field(a,'Id')?'active':''}" data-life-album="${safe(field(a,'Id'))}"><div class="lifeAlbumCover ${bgClass(field(a,'Background'))}">${field(a,'CardIds',[]).filter(Boolean).slice(0,3).map(id=>miniCard(id)).join('')||svg('album')}</div><div class="lifeAlbumCaption"><strong>${safe(field(a,'Name'))}</strong><span>${field(a,'CardIds',[]).length} ${tx('cards','thẻ')}</span></div></button>`).join('');
    return `<div class="lifeSection"><div class="lifeSectionHeading"><h2>${tx('Albums','Album')}</h2>${btn('+ '+tx('New album','Tạo album'),'albumNew')}</div>${tiles?`<div class="lifeAlbumList">${tiles}</div>`:empty(tx('A home for your favorites','Nơi lưu những thẻ yêu thích'),'album')}</div>${state.album?albumEditor():''}`;
  }
  function albumEditor() {
    const a=state.album,background=bgClass(a.background);
    return `<div class="lifeSection lifeHero">${sectionHeading(a.id?tx('Edit album','Chỉnh sửa album'):tx('New album','Album mới'),`${a.cardIds.length} / 54`)}<div class="lifeFormRow"><label class="lifeField">${tx('Name','Tên album')}<input id="lifeAlbumName" maxlength="60" value="${safe(a.name)}"></label><label class="lifeField">${tx('Background','Nền')}<select id="lifeAlbumBackground">${options(backgrounds(),a.background)}</select></label></div><div class="lifeAlbumGrid ${background}">${a.cardIds.map((id,i)=>`<div class="lifeAlbumSlot" draggable="true" data-life-slot="${i}">${miniCard(id,id?'':`data-life-fill="${i}"`)}<div class="lifeSlotActions"><button data-life-move="${i}" data-direction="-1" aria-label="${tx('Move left','Di chuyển trái')}" ${i===0?'disabled':''}>‹</button><span>${i+1}</span><button data-life-remove="${i}" aria-label="${tx('Remove card','Bỏ thẻ')}">×</button><button data-life-move="${i}" data-direction="1" aria-label="${tx('Move right','Di chuyển phải')}" ${i===a.cardIds.length-1?'disabled':''}>›</button></div></div>`).join('')}${a.cardIds.length<54?`<button class="lifeAlbumAdd" data-life-action="albumAdd"><b>+</b>${tx('Add card','Thêm thẻ')}</button>`:''}</div><div class="lifeActions">${btn(tx('Save album','Lưu album'),'albumSave','','primary')}${btn(tx('Full view','Xem album'),'albumView')}${a.cardIds.length<54?btn(tx('Blank slot','Thêm ô trống'),'albumBlank'):''}${btn(tx('Close','Đóng'),'albumClose')}${a.id?btn(tx('Delete','Xóa'),'albumDelete','','danger'):''}</div><p class="lifeHelp">${tx('Drag cards or use arrows to arrange.','Kéo thẻ hoặc dùng mũi tên để sắp xếp.')}</p></div>`;
  }
  function openAlbumView() {
    if(!state.album)return;
    const a=state.album;$('lifestyleAlbumDialogTitle').textContent=a.name;
    $('lifestyleAlbumDisplay').className='lifeAlbumDisplay '+bgClass(a.background);
    $('lifestyleAlbumDisplay').innerHTML=a.cardIds.length?a.cardIds.map(id=>miniCard(id)).join(''):empty(tx('No cards yet','Chưa có thẻ'));
    applyVisualStyles($('lifestyleAlbumDisplay'));
    $('lifestyleAlbumDialog').showModal();watchImages($('lifestyleAlbumDialog'));
    hydrateAlbumMetadata(a.cardIds);
  }
  function bindAlbumDrag() {
    let dragging=-1;
    for(const node of content.querySelectorAll('[data-life-slot]')){
      node.addEventListener('dragstart',event=>{dragging=num(node.dataset.lifeSlot);event.dataTransfer.effectAllowed='move';event.dataTransfer.setData('text/plain',String(dragging));});
      node.addEventListener('dragover',event=>{event.preventDefault();node.classList.add('dragOver');});
      node.addEventListener('dragleave',()=>node.classList.remove('dragOver'));
      node.addEventListener('dragend',()=>{dragging=-1;content.querySelectorAll('.dragOver').forEach(n=>n.classList.remove('dragOver'));});
      node.addEventListener('drop',event=>{event.preventDefault();if(dragging<0)return;const target=num(node.dataset.lifeSlot);const [moved]=state.album.cardIds.splice(dragging,1);state.album.cardIds.splice(target,0,moved);state.dirty=true;render();});
    }
  }
  function chooseCardButton(id,action='chooseStudio') {
    return `<button class="lifeCharacterButton" data-life-action="${action}">${id?miniCard(id):`<span class="lifeItemIcon">✧</span>`}<div><strong>${id?safe(name(id)):tx('Choose a character','Chọn nhân vật')}</strong><small>${id?safe(field(card(id),'Series')):tx('From your collection','Từ bộ sưu tập của bạn')}</small></div><b>⌄</b></button>`;
  }
  function getStudio() {
    const id=state.studioId&&card(state.studioId)?state.studioId:field(roster()[0],'CardId');
    if(!id)return null;
    if(!state.studio||state.studio.cardId!==String(id)){
      const c=cosmetic(id);state.studio={cardId:String(id),frame:'classic',frameItemId:'',dyeId:field(c,'DyeId',''),dye:validColor(field(c,'Dye'),rarityColor(id)),morph:validColor(field(c,'Morph'),'#e796b9'),morphMode:field(c,'MorphMode','none'),mystic:field(c,'Mystic',false),artworkId:field(c,'ArtworkId',''),artworkData:'',...artworkPosition({artworkScale:field(c,'ArtworkScale',1),artworkX:field(c,'ArtworkX',0),artworkY:field(c,'ArtworkY',0)})};state.studioId=String(id);state.quote=null;state.quoteKey='';state.quoteGeneration++;
    }
    return state.studio;
  }
  function renderStudio() {
    const s=getStudio();
    if(!s)return `<div class="lifeSection">${empty(tx('Your studio awaits','Studio đang đợi bạn'),'studio',tx('Open a booster to collect your first character.','Mở booster để nhận nhân vật đầu tiên.'))}</div>`;
    const selectedDye=dye(s.dyeId),current=cosmetic(s.cardId),frameName=rarityFrameLabel(s.cardId);
    const dyeName=selectedDye?dyeLabel(selectedDye):field(current,'Dye')?tx('Applied color','Màu đang dùng'):tx('Original color','Màu gốc');
    return `<div class="lifeTwoColumn"><div class="lifeSection lifeHero">${sectionHeading(tx('Cosmetic Studio','Studio trang trí'))}${chooseCardButton(s.cardId)}${studioPreview(s)}<div class="lifeStudioSelections"><div class="lifeRarityFrame" aria-label="${safe(tx('Current frame','Khung hiện tại'))}"><span class="lifeRarityFrameIcon" data-life-color="${rarityColor(s.cardId)}" aria-hidden="true">▱</span><span><small>${tx('Rarity frame','Khung theo độ hiếm')}</small><strong>${safe(frameName)}</strong></span></div><button class="lifeInventorySelect" data-life-action="chooseDye"><i class="lifeSelectedColor" data-life-color="${validColor(s.dye)}"></i><span><small>${tx('Dye','Màu nhuộm')}</small><strong>${safe(dyeName)}</strong></span><b>⌄</b></button></div><div class="lifeActions">${btn(tx('Inventory','Túi đồ'),'openInventory')}</div><div class="lifeStudioSave"><p id="lifeSaveSummary" class="lifeSaveSummary"></p><div class="lifeActions">${btn(tx('Apply look','Áp dụng'),'cosmeticSave','','primary')}${btn(tx('Reset','Khôi phục'),'cosmeticReset')}</div></div></div><div class="lifeSection">${sectionHeading(tx('Your artwork','Artwork của bạn'),tx('1 gem / 10,000 pixels','1 Gem / 10.000 pixel'))}<div class="lifeArtworkRow">${btn(tx('Choose image','Chọn ảnh'),'artworkPick')}${btn(tx('Original artwork','Artwork gốc'),'artworkOriginal')}</div><div class="lifeArtworkRow"><input id="lifeArtworkUrl" type="url" placeholder="https://…" maxlength="2048" aria-label="${tx('Image URL','Liên kết ảnh')}">${btn(tx('Load URL','Tải liên kết'),'artworkUrl')}</div><div class="lifeSectionHeading lifeDrawingHeading"><h2>${tx('Drawing canvas','Bảng vẽ')}</h2><span>600 × 840</span></div><div class="lifeCanvasWrap"><canvas id="lifeCanvas" width="600" height="840" aria-label="${tx('Draw custom artwork','Vẽ artwork riêng')}"></canvas></div><div class="lifeBrushTools"><input id="lifeBrushColor" type="color" value="#c49bf7" aria-label="${tx('Brush color','Màu cọ')}"><input id="lifeBrushSize" type="range" min="2" max="64" value="14" aria-label="${tx('Brush size','Cỡ cọ')}"><button id="lifeEraser" class="lifeButton" aria-pressed="false">${tx('Eraser','Tẩy')}</button><button id="lifeCanvasClear" class="lifeButton">${tx('Clear','Xóa')}</button></div><div class="lifeActions">${btn(tx('Use drawing','Dùng bản vẽ'),'artworkDraw','','primary')}</div><p class="lifeHelp">${tx('Only visible pixels count. The price appears before Apply.','Chỉ tính pixel có màu. Xem giá trước khi áp dụng.')}</p></div></div>`;
  }
  function studioPreview(s,id='lifeStudioPreview') {
    const p=artworkPosition(s);
    return `<div id="${id}" class="lifeStudioPreview ${s.mystic?'lifeMystic':''}" data-life-art-region="${artworkRegion(s.cardId).kind}" data-life-mystic-color="${validColor(s.dye)}" tabindex="0" aria-label="${safe(tx('Move artwork','Di chuyển artwork'))}"><img ${id==='lifeStudioPreview'?'id="lifeStudioBase"':''} data-life-image="${safe(s.cardId)}" data-life-preview-frame="classic" data-life-preview-dye="${safe(s.dyeId)}" data-life-preview-artwork="${safe(s.artworkId)}" data-life-artwork-scale="${p.artworkScale}" data-life-artwork-x="${p.artworkX}" data-life-artwork-y="${p.artworkY}" alt="${safe(name(s.cardId)+' · '+rarityFrameLabel(s.cardId))}" draggable="false" decoding="async"><i class="lifeArtworkGuide" aria-hidden="true"></i></div>${id==='lifeStudioPreview'?studioPositionControls(s):''}`;
  }
  function studioPositionControls(s) {
    const p=artworkPosition(s),range=(key,label,value,min,max)=>`<label class="lifePositionRow" for="lifeArtwork${key}"><span>${label}</span><input id="lifeArtwork${key}" data-life-art-position="${key.toLowerCase()}" type="range" min="${min}" max="${max}" step="1" value="${Math.round(value*100)}"><output id="lifeArtwork${key}Value" for="lifeArtwork${key}">${Math.round(value*100)}%</output></label>`;
    return `<details class="lifeArtworkPosition" id="lifeArtworkPosition" ${s.positionOpen?'open':''}><summary><span aria-hidden="true">⌖</span>${tx('Artwork position','Căn chỉnh artwork')}<b aria-hidden="true">⌄</b></summary><div class="lifePositionControls">${range('Scale',tx('Zoom','Thu phóng'),p.artworkScale,100,400)}${range('X',tx('Horizontal','Ngang'),p.artworkX,-100,100)}${range('Y',tx('Vertical','Dọc'),p.artworkY,-100,100)}${btn(tx('Center & reset','Căn giữa và đặt lại'),'artworkPositionReset')}</div></details>`;
  }
  function renderDyeBooster() {
    const last=dye(state.lastDyeId),palette=field(creative(),'DyePalette',[]);
    return `<div class="lifeDyeBooster compact"><div class="lifeBoosterGem" aria-hidden="true">◈</div><div><strong>${tx('Dye booster','Booster màu nhuộm')}</strong><span>${palette.length||64} ${tx('colors','màu')} · ${field(creative(),'MysticChancePercent',1)}% Mystic</span></div>${btn('◆ '+dyePrice()+' · '+tx('Open','Mở'),'dyeBoosterBuy',gems()<dyePrice()?'disabled':'','primary')}${last?`<div class="lifeDyeReveal ${field(last,'Mystic',false)?'lifeMysticSwatch':''}" data-life-color="${validColor(field(last,'Color'))}"><i></i><strong>${safe(dyeLabel(last))}</strong><span>${tx('Added to your inventory','Đã thêm vào túi đồ')}</span></div>`:''}</div>`;
  }
  function renderBooster() {
    const mount=$('dyeBoosterMount');if(!mount)return;
    const stamp=[tcg.language,gems(),state.busy,state.pending!==null,state.lastDyeId,tcg.busy].join(':');if(mount.dataset.rendered===stamp)return;mount.dataset.rendered=stamp;
    mount.innerHTML=renderDyeBooster();applyVisualStyles(mount);
    const button=mount.querySelector('button');if(button)button.disabled=state.busy||Boolean(state.pending)||tcg.busy||gems()<dyePrice();
  }
  function updateStudioQuote() {
    const summary=$('lifeSaveSummary'),save=content.querySelector('[data-life-action="cosmeticSave"]');if(!summary||!save)return;
    if(!studioChanged()){summary.textContent=tx('Look already applied','Trang trí đã áp dụng');save.textContent=tx('Apply look','Áp dụng');save.disabled=true;return;}
    if(!quoteMatches()){summary.textContent=state.quoteError||tx('Calculating price…','Đang tính giá…');save.disabled=true;return;}
    const q=state.quote,total=num(field(q,'TotalGems')),pixels=num(field(q,'ArtworkPixels')),parts=[total?'◆ '+total:tx('No gem charge','Không tốn Gems')];
    if(pixels&&num(field(q,'ArtworkGems')))parts.push(pixels.toLocaleString(tcg.language==='vi'?'vi-VN':'en-US')+' '+tx('pixels','pixel')+' × '+num(field(q,'ArtworkColors',1))+' '+tx('colors','màu'));
    if(field(q,'DyeConsumed',false))parts.push(tx('Uses 1 dye','Dùng 1 màu nhuộm'));
    summary.textContent=parts.join(' · ');save.textContent=tx('Apply look','Áp dụng')+(total?' · ◆ '+total:'');save.disabled=state.busy||state.pending||characterBusy(state.studio.cardId)||total>gems();
  }
  async function requestStudioQuote(force=false) {
    if(!state.studio)return null;const payload=studioPayload(),key=JSON.stringify(payload),epoch=state.epoch;
    if(!force&&quoteMatches()){updateStudioQuote();return state.quote;}
    if(state.quotePending?.key===key&&state.quotePending.generation===state.quoteGeneration)return state.quotePending.promise;
    const generation=++state.quoteGeneration;state.quote=null;state.quoteError='';updateStudioQuote();
    const promise=(async()=>{try{const reply=await call('hytcgLifestyle',{action:'quote',...payload});if(epoch!==state.epoch||generation!==state.quoteGeneration||JSON.stringify(studioPayload())!==key)return null;state.quote=reply.quote||reply.Quote||reply;state.quoteKey=key;updateStudioQuote();return state.quote;}catch(error){if(epoch===state.epoch&&generation===state.quoteGeneration){state.quoteError=errorMessage(error);updateStudioQuote();}return null;}finally{if(state.quotePending?.promise===promise)state.quotePending=null;}})();
    state.quotePending={key,promise,generation};return promise;
  }
  function updateStudioPreview() {
    const s=state.studio,node=$('lifeStudioPreview');if(!s||!node)return;
    const image=node.querySelector('img'),position=artworkPosition(s);image.dataset.lifePreviewFrame='classic';image.dataset.lifePreviewDye=s.dyeId;image.dataset.lifePreviewArtwork=s.artworkId;image.dataset.lifeArtworkScale=String(position.artworkScale);image.dataset.lifeArtworkX=String(position.artworkX);image.dataset.lifeArtworkY=String(position.artworkY);
    image.alt=name(s.cardId)+' · '+rarityFrameLabel(s.cardId);node.classList.toggle('lifeMystic',s.mystic);node.style.setProperty('--life-mystic-color',validColor(s.dye));delete image.dataset.lifeQueued;enqueueImage(image);
    clearTimeout(state.quoteTimer);updateStudioQuote();state.quoteTimer=setTimeout(()=>requestStudioQuote(),100);
  }
  function bindStudio() {
    updateStudioPreview();
    bindArtworkPosition();
    if(state.studio&&restorableLook(state.studio.cardId)&&content.querySelector('.lifeStudioSave .lifeActions'))content.querySelector('.lifeStudioSave .lifeActions').insertAdjacentHTML('beforeend',btn(tx('Restore previous · free','Khôi phục lần trước · miễn phí'),'cosmeticRestore'));
    const canvas=$('lifeCanvas');if(!canvas)return;const context=canvas.getContext('2d');if(!context)return;
    if(state.canvasData){const image=new Image();image.onload=()=>{if(canvas.isConnected)context.drawImage(image,0,0);};image.src=state.canvasData;}
    let drawing=false,last=null,erasing=false;
    const point=event=>{const r=canvas.getBoundingClientRect();return {x:(event.clientX-r.left)*canvas.width/r.width,y:(event.clientY-r.top)*canvas.height/r.height};};
    const stroke=(p,to)=>{context.save();context.globalCompositeOperation=erasing?'destination-out':'source-over';context.strokeStyle=context.fillStyle=$('lifeBrushColor').value;context.lineWidth=num($('lifeBrushSize').value);context.lineCap=context.lineJoin='round';context.beginPath();context.moveTo(p.x,p.y);context.lineTo(to.x,to.y);context.stroke();if(p.x===to.x&&p.y===to.y){context.beginPath();context.arc(p.x,p.y,context.lineWidth/2,0,Math.PI*2);context.fill();}context.restore();};
    canvas.addEventListener('pointerdown',event=>{if(state.busy||state.pending)return;event.preventDefault();canvas.setPointerCapture(event.pointerId);drawing=true;last=point(event);stroke(last,last);});
    canvas.addEventListener('pointermove',event=>{if(!drawing)return;const p=point(event);stroke(last,p);last=p;});
    const finish=()=>{if(!drawing)return;drawing=false;state.canvasData=canvas.toDataURL('image/png');};canvas.addEventListener('pointerup',finish);canvas.addEventListener('pointercancel',finish);canvas.addEventListener('lostpointercapture',finish);
    $('lifeEraser').addEventListener('click',()=>{erasing=!erasing;$('lifeEraser').classList.toggle('active',erasing);$('lifeEraser').setAttribute('aria-pressed',String(erasing));});
    $('lifeCanvasClear').addEventListener('click',()=>{context.clearRect(0,0,canvas.width,canvas.height);state.canvasData='';});
  }
  function bindArtworkPosition() {
    const node=$('lifeStudioPreview'),details=$('lifeArtworkPosition'),s=state.studio;if(!node||!s)return;
    let dragging=null,timer=0;
    const sync=()=>{for(const key of['Scale','X','Y']){const value=Math.round(artworkPosition(s)['artwork'+key]*100),input=$('lifeArtwork'+key),output=$('lifeArtwork'+key+'Value');if(input)input.value=String(value);if(output)output.textContent=value+'%';}updateStudioQuote();};
    const preview=flush=>{sync();if(flush){clearTimeout(timer);timer=0;updateStudioPreview();}else if(!timer)timer=setTimeout(()=>{timer=0;if(node.isConnected&&state.studio===s)updateStudioPreview();},70);};
    details?.addEventListener('toggle',()=>{s.positionOpen=details.open;});
    for(const input of content.querySelectorAll('[data-life-art-position]'))input.addEventListener('input',()=>{if(state.busy||state.pending)return;s[{scale:'artworkScale',x:'artworkX',y:'artworkY'}[input.dataset.lifeArtPosition]]=Number(input.value)/100;preview(false);});
    node.addEventListener('pointerdown',event=>{if(event.button!==0||state.busy||state.pending)return;const rect=node.getBoundingClientRect(),region=artworkRegion(s.cardId),x=(event.clientX-rect.left)/rect.width,y=(event.clientY-rect.top)/rect.height;if(x<region.x||x>region.x+region.width||y<region.y||y>region.y+region.height)return;event.preventDefault();node.focus({preventScroll:true});node.setPointerCapture(event.pointerId);dragging={id:event.pointerId,x:event.clientX,y:event.clientY,startX:s.artworkX,startY:s.artworkY,width:rect.width*region.width,height:rect.height*region.height};node.classList.add('positioning');if(details)details.open=true;});
    node.addEventListener('pointermove',event=>{if(!dragging||dragging.id!==event.pointerId)return;s.artworkX=Math.round(Math.max(-1,Math.min(1,dragging.startX+(event.clientX-dragging.x)*2/dragging.width))*100)/100;s.artworkY=Math.round(Math.max(-1,Math.min(1,dragging.startY+(event.clientY-dragging.y)*2/dragging.height))*100)/100;preview(false);});
    const finish=()=>{if(!dragging)return;dragging=null;node.classList.remove('positioning');preview(true);};
    node.addEventListener('pointerup',finish);node.addEventListener('pointercancel',finish);node.addEventListener('lostpointercapture',finish);
    node.addEventListener('keydown',event=>{if(state.busy||state.pending||!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Home'].includes(event.key))return;event.preventDefault();const delta=event.shiftKey ? .1 : .02;if(event.key==='Home'){s.artworkScale=1;s.artworkX=s.artworkY=0;}else{const axis=/Left|Right/.test(event.key)?'artworkX':'artworkY';s[axis]=Math.round(Math.max(-1,Math.min(1,s[axis]+(/Left|Up/.test(event.key)?-delta:delta)))*100)/100;}if(details)details.open=true;preview(false);});
  }
  function teamMarkup(kind) {
    const team=kind==='expedition'?state.expeditionTeam:state.arenaTeam;
    return `<div class="lifeTeam">${Array.from({length:3},(_,i)=>{const id=team[i];return `<div class="lifeTeamSlot">${id?`${miniCard(id)}<span class="lifeMuted">${safe(name(id))}</span><div class="lifeSlotActions"><span>Lv.${field(profile(id),'Level',1)}</span><button data-life-team-remove="${kind}" data-index="${i}" aria-label="${tx('Remove character','Bỏ nhân vật')}">×</button></div>`:`<button class="lifeAlbumAdd" data-life-team-add="${kind}"><b>+</b>${tx('Character','Nhân vật')}</button>`}</div>`;}).join('')}</div>`;
  }
  function effort(team) { return team.reduce((total,id)=>total+num(field(profile(id),'Effort')),0); }
  function renderExpedition() {
    const list=routes();if(!state.routeId||!list.some(r=>String(field(r,'Id'))===state.routeId))state.routeId=String(field(list[0],'Id'));
    const route=list.find(r=>String(field(r,'Id'))===state.routeId),points=effort(state.expeditionTeam),min=num(field(route,'MinEffort'));
    const active=field(gameplay(),'Expeditions',[]).filter(e=>!field(e,'Claimed',false));
    return `<div class="lifeTwoColumn"><div class="lifeSection lifeHero">${sectionHeading(tx('Worker expedition','Thám hiểm'),`${field(gameplay(),'ExpeditionStartsRemaining',0)} ${tx('starts left','lượt còn lại')}`)}<div class="lifeRouteList">${list.map(r=>`<button class="lifeRoute ${String(field(r,'Id'))===state.routeId?'active':''}" data-life-route="${safe(field(r,'Id'))}"><strong>${safe(routeName(r))}</strong><div><span>${duration(num(field(r,'DurationSeconds')))}</span><span>${tx('Effort','Nỗ lực')} ${num(field(r,'MinEffort'))}</span></div><small>◆ ${num(field(r,'RewardGems'))} · XP ${num(field(r,'RewardXp'))}${field(r,'RewardItems',[]).length?' · '+field(r,'RewardItems',[]).map(item=>safe(itemName(item))+' ×'+field(item,'Count',1)).join(', '):''}</small></button>`).join('')||empty(tx('No routes available','Chưa có tuyến thám hiểm'),'expedition')}</div>${teamMarkup('expedition')}<div class="lifeInlineMetric"><span>${tx('Team effort','Nỗ lực của đội')}</span><b>${points} / ${min}</b></div><div class="lifeMeter"><i data-life-progress="${Math.min(100,min?points/min*100:100)}"></i></div><div class="lifeActions">${btn(tx('Start expedition','Bắt đầu thám hiểm'),'expeditionStart',`${!route||!state.expeditionTeam.length||points<min||num(field(gameplay(),'ExpeditionStartsRemaining'))<1?'disabled':''}`,'primary')}</div><p class="lifeHelp">${tx('Working characters return when you claim their rewards.','Nhân vật trở về sau khi bạn nhận phần thưởng.')}</p></div><div class="lifeSection">${sectionHeading(tx('On the journey','Đang thám hiểm'),`${active.length} ${tx('active','chuyến')}`)}${active.map(e=>{const r=list.find(r=>String(field(r,'Id'))===String(field(e,'RouteId'))),ready=field(e,'Ready',false)||timeLeft(e)<=0;return `<div class="lifeExpedition"><div><strong>${safe(routeName(r))}</strong><small data-life-countdown="${safe(field(e,'Id'))}">${ready?tx('Ready to claim','Sẵn sàng nhận'):duration(timeLeft(e))}</small></div><p>${field(e,'CardIds',[]).map(id=>safe(name(id))).join(' · ')}</p><div class="lifeInlineMetric"><span>◆ ${num(field(e,'RewardGems'))}</span>${btn(tx('Claim','Nhận'),'expeditionClaim',`data-life-claim="${safe(field(e,'Id'))}" ${ready?'':'disabled'}`,'primary')}</div></div>`;}).join('')||empty(tx('Your next adventure awaits','Chuyến phiêu lưu đang đợi'),'expedition')}</div></div>${renderResult()}`;
  }
  function itemKind(item) { return (field(item,'Kind')+' '+field(item,'Id')).toLowerCase(); }
  function itemIcon(item) { const k=itemKind(item);return ({training:'📘',tea:'☕',gift:'🎁',fiber:'🧵',petal:'🌸',crystal:'💎'}[field(item,'Id')])||(/gift|bond|affinity/.test(k)?'🎁':/xp|exp|training/.test(k)?'📘':/effort|energy|worker/.test(k)?'⚡':'🎒'); }
  function itemDescription(item) { const k=itemKind(item);return field(item,'Kind')==='material'?tx('Expedition material','Nguyên liệu thám hiểm'):/gift|bond|affinity/.test(k)?tx('One gift date · affinity and XP.','Một lượt tặng quà · gắn kết và XP.'):/tea/.test(k)?tx('One tea date · affinity and XP.','Một lượt uống trà · gắn kết và XP.'):/xp|exp|training/.test(k)?tx('Gain 80 character XP.','Nhận 80 XP nhân vật.'):/effort|energy|worker/.test(k)?tx('Improve character effort.','Tăng nỗ lực của nhân vật.'):tx('Use on a character.','Sử dụng cho nhân vật.'); }
  function inventoryCategory(item) { const kind=String(field(item,'Kind')).toLowerCase();return kind==='dye'?'dye':kind==='material'?'material':'supply'; }
  function inventoryItems() {
    const native=field(creative(),'InventoryItems',state.data?.inventoryItems||null);if(native)return native.filter(x=>String(field(x,'Kind')).toLowerCase()!=='frame');
    return [...inventory(),...dyes().filter(x=>num(field(x,'Count'))>0).map(x=>({...x,Kind:'dye'}))].filter(x=>String(field(x,'Kind')).toLowerCase()!=='frame');
  }
  const inventoryLabels = () => ({all:tx('All','Tất cả'),supply:tx('Supplies','Vật phẩm'),material:tx('Materials','Nguyên liệu'),dye:tx('Dyes','Màu nhuộm')});
  function inventoryTile(item,picker=false) {
    const kind=inventoryCategory(item),id=String(field(item,'Id')),count=num(field(item,'Count')),title=kind==='dye'?dyeLabel(item):itemName(item),usable=count>0&&(picker||kind!=='supply'||!state.inventoryTarget||!characterBusy(state.inventoryTarget));
    const element=kind==='material'?'div':'button',action=kind==='material'?'':picker?'data-life-item-pick="'+safe(id)+'"':'data-life-item="'+safe(id)+'"';
    return `<${element} class="lifeInventoryTile ${kind==='dye'&&field(item,'Mystic',false)?'lifeMysticSwatch':''}" ${action} ${element==='button'&&!usable?'disabled':''} title="${safe(title)}">${kind==='dye'?`<i class="lifeInventoryColor" data-life-color="${validColor(field(item,'Color'))}"></i>`:`<span class="lifeItemIcon">${itemIcon(item)}</span>`}<span class="lifeItemTop"><small>${safe(inventoryLabels()[kind])}</small><b>×${count}</b></span><strong>${safe(title)}</strong><span class="lifeInventoryDetail">${kind==='dye'?tx('Apply in Studio','Dùng trong Studio'):itemDescription(item)}</span></${element}>`;
  }
  function filteredInventory(items,filter,query) {const q=query.trim().toLocaleLowerCase();return items.filter(x=>(filter==='all'||inventoryCategory(x)===filter)&&(!q||(itemName(x)+' '+field(x,'Color')+' '+field(x,'Kind')).toLocaleLowerCase().includes(q)));}
  function renderInventory() {
    const items=inventoryItems();
    if(state.inventoryTarget&&!card(state.inventoryTarget))state.inventoryTarget='';
    return `<div class="lifeSection">${sectionHeading(tx('Inventory','Túi đồ'),`${items.reduce((sum,x)=>sum+num(field(x,'Count')),0)} ${tx('items','vật phẩm')}`)}${renderDyeBooster()}<div class="lifeActions">${btn(tx('Studio','Studio'),'openStudio')}</div><div class="lifeInventoryFilters">${Object.entries(inventoryLabels()).map(([key,label])=>`<button data-life-inventory-filter="${key}" class="${state.inventoryFilter===key?'active':''}">${label}</button>`).join('')}</div><input id="lifeInventorySearch" type="search" maxlength="80" value="${safe(state.inventoryQuery)}" placeholder="${tx('Find an item','Tìm vật phẩm')}" aria-label="${tx('Search inventory','Tìm trong túi đồ')}">${chooseCardButton(state.inventoryTarget,'chooseInventory')}<div id="lifeInventoryGrid" class="lifeUnifiedInventory">${filteredInventory(items,state.inventoryFilter,state.inventoryQuery).map(item=>inventoryTile(item)).join('')||empty(tx('No matching items','Chưa có vật phẩm phù hợp'),'inventory')}</div></div>${renderResult()}`;
  }
  function chooseInventoryItem(kind) {
    if(kind!=='dye')return;getStudio();state.itemPicker={kind,query:''};$('lifestyleItemPickerTitle').textContent=tx('Choose from inventory','Chọn từ túi đồ')+' · '+inventoryLabels()[kind];$('lifestyleItemSearch').value='';$('lifestyleItemSearch').placeholder=tx('Find an item','Tìm vật phẩm');$('lifestyleItemInventory').textContent=tx('Open inventory','Mở túi đồ');renderItemPicker();$('lifestyleItemPicker').showModal();$('lifestyleItemSearch').focus();
  }
  function renderItemPicker() {
    const p=state.itemPicker;if(!p)return;const list=filteredInventory(inventoryItems(),p.kind,p.query).filter(x=>num(field(x,'Count'))>0);
    $('lifestyleItemPickerGrid').innerHTML=`<button class="lifeInventoryTile lifeKeepItem" data-life-keep-item="dye"><span class="lifeItemIcon">◈</span><strong>${tx('Keep applied','Giữ màu đang dùng')}</strong></button>${list.map(item=>inventoryTile(item,true)).join('')||empty(tx('No available items','Chưa có vật phẩm sẵn sàng'),'inventory')}`;applyVisualStyles($('lifestyleItemPickerGrid'));
  }
  function teamStats(team) { return { attack:team.reduce((n,id)=>n+num(field(profile(id),'Attack')),0), defense:team.reduce((n,id)=>n+num(field(profile(id),'Defense')),0),hp:team.reduce((n,id)=>n+num(field(profile(id),'Hp')),0) }; }
  function statBlocks(values) { return `<div class="lifeStats">${values.map(([label,value])=>`<div><strong>${safe(value)}</strong><span>${label}</span></div>`).join('')}</div>`; }
  function renderArena() {
    const fight=field(gameplay(),'Fight',{}),stage=Math.max(1,num(field(fight,'Stage',1))),max=Math.max(stage,num(field(fight,'MaxStage',stage))),stats=teamStats(state.arenaTeam);
    return `<div class="lifeTwoColumn">
      <div class="lifeSection lifeHero">
        <div class="lifeArenaStage">
          <div class="lifeArenaBadge">${svg('arena')}</div>
          <h2>${tx('Solo arena','Đấu trường đơn')}</h2>
          <p>${tx('Stage','Chặng')} ${stage} / ${max}</p>
          <p class="lifeEnemyStats">${safe(enemyName(fight))} · HP ${field(fight,'EnemyHp',0)} · ${tx('ATK','Công')} ${field(fight,'EnemyAttack',0)} · ${tx('DEF','Thủ')} ${field(fight,'EnemyDefense',0)}</p>
        </div>
        ${statBlocks([[tx('Attempts','Lượt đấu'),field(fight,'DailyRemaining',0)],[tx('Attack','Công'),stats.attack],[tx('Defense','Thủ'),stats.defense],['HP',stats.hp]])}
        ${teamMarkup('arena')}
        <div class="lifeActions">${btn(tx('Enter battle','Bắt đầu trận đấu'),'fight',`${state.arenaTeam.length<1||num(field(fight,'DailyRemaining'))<1?'disabled':''}`,'primary')}</div>
        <p class="lifeHelp" data-life-reset></p>
      </div>
      <div class="lifeSection">${sectionHeading(tx('Battle record','Kết quả trận đấu'))}${renderResult(state.result||field(fight,'LastResult'))||empty(tx('Choose your team','Chọn đội của bạn'),'arena',tx('Build a team of up to three characters.','Lập đội gồm tối đa ba nhân vật.'))}</div>
    </div>`;
  }
  function renderDating() {
    if(!profile(state.datingId))state.datingId=String(field(profiles()[0],'CardId',''));
    const id=state.datingId,p=profile(id);
    if(!id)return `<div class="lifeSection">${empty(tx('A new bond begins with a card','Gắn kết bắt đầu từ một thẻ'),'dating')}</div>`;
    const gifts=inventory().filter(x=>num(field(x,'Count'))>0&&/gift|bond|affinity/.test(itemKind(x)));
    const left=num(field(p,'DatesRemaining',field(gameplay(),'DateDailyRemaining',0))),global=num(field(gameplay(),'DateDailyRemaining',left)),busy=field(p,'Busy',false),disabled=left<1||global<1||busy;
    return `<div class="lifeTwoColumn"><div class="lifeSection lifeHero">${sectionHeading(tx('Time together','Thời gian bên nhau'))}${chooseCardButton(id,'chooseDating')}<div class="lifeBondArt">${miniCard(id)}<div class="lifeBondLevel">♡ ${tx('Bond','Gắn kết')} ${field(p,'BondLevel',0)} · ${field(p,'Bond',0)}</div></div>${statBlocks([[tx('Level','Cấp'),field(p,'Level',1)],[tx('Effort','Nỗ lực'),field(p,'Effort',0)],[tx('Dates','Lượt hẹn'),left],[tx('Today','Hôm nay'),global]])}<div class="lifeInlineMetric"><span>XP ${field(p,'Xp',0)} / ${field(p,'NextLevelXp',100)}</span><span>${busy?tx('Busy','Đang bận'):tx('Available','Sẵn sàng')}</span></div><div class="lifeMeter"><i data-life-progress="${Math.min(100,num(field(p,'Xp'))/Math.max(1,num(field(p,'NextLevelXp',100)))*100)}"></i></div></div><div class="lifeSection">${sectionHeading(tx('Make a memory','Tạo kỷ niệm'),`${global} ${tx('left today','lượt hôm nay')}`)}<div class="lifeDates"><button data-life-date="chat" ${disabled?'disabled':''}><span>☏</span>${tx('Chat','Trò chuyện')}<small>${tx('A little closer','Gần nhau hơn')}</small></button><button data-life-date="tea" ${disabled||!inventory().some(x=>field(x,'Id')==='tea'&&num(field(x,'Count'))>0)?'disabled':''}><span>☕</span>${tx('Tea time','Uống trà')}<small>${tx('1 tea set','1 bộ trà')}</small></button><button data-life-date="gift" ${disabled||!gifts.length?'disabled':''}><span>♧</span>${tx('Give a gift','Tặng quà')}<small>${tx('1 gift','1 món quà')}</small></button></div><label class="lifeField lifeDateGift">${tx('Gift','Món quà')}<select id="lifeGift" ${gifts.length?'':'disabled'}>${gifts.length?gifts.map(x=>`<option value="${safe(field(x,'Id'))}">${safe(itemName(x))} ×${num(field(x,'Count'))}</option>`).join(''):`<option>${tx('No gifts available','Chưa có quà')}</option>`}</select></label><p class="lifeHelp" data-life-reset></p>${renderResult()}</div></div>`;
  }
  function resultTitle(result) {
    const kind=field(result,'Kind');
    if(kind==='fight'||num(field(result,'Stage'))>0)return field(result,'Win')?tx('Victory','Chiến thắng'):tx('A stronger team awaits','Hẹn một đội mạnh hơn');
    const kinds={date:tx('A moment together','Một kỷ niệm bên nhau'),expeditionStart:tx('Expedition started','Đã bắt đầu thám hiểm'),expeditionClaim:tx('Journey complete','Hoàn tất thám hiểm'),inventoryUse:tx('Training complete','Hoàn tất huấn luyện')};
    if(kinds[kind])return kinds[kind];
    const title=field(result,'Title');
    const known={Date:tx('A memory made','Thêm một kỷ niệm'),Expedition:tx('Journey complete','Hoàn tất thám hiểm'),Inventory:tx('Item used','Đã dùng vật phẩm'),Bond:tx('A closer bond','Gắn kết sâu hơn')};
    return known[title]||safe(title)||tx('Progress saved','Đã lưu tiến trình');
  }
  function resultMessage(result) {
    const message=field(result,'Message');if(tcg.language!=='vi')return message;
    const kind=field(result,'Kind'),firstName=name(field(result,'CardIds',[])[0]);
    if(kind==='fight')return field(result,'Win')?`Đã vượt qua chặng ${field(result,'Stage')} sau ${field(result,'Rounds')} lượt.`:`Đội đã nhận ${field(result,'XpAwarded',0)} XP. Huấn luyện hoặc thêm nhân vật để thử lại.`;
    if(kind==='expeditionStart')return 'Đội đã lên đường. Nhận phần thưởng khi chuyến thám hiểm hoàn tất.';
    if(kind==='expeditionClaim')return 'Đội đã trở về cùng phần thưởng và kinh nghiệm.';
    if(kind==='inventoryUse')return firstName+' đã hoàn tất buổi huấn luyện.';
    if(kind==='date'){
      const moments={'shared a favorite memory':'cùng bạn chia sẻ một kỷ niệm yêu thích','talked about a future adventure':'cùng bạn trò chuyện về chuyến phiêu lưu sắp tới','enjoyed a thoughtful conversation':'cùng bạn trò chuyện thật ấm áp','enjoyed a peaceful afternoon tea':'cùng bạn tận hưởng buổi trà chiều yên bình','laughed over a warm cup of tea':'cùng bạn cười vui bên chén trà ấm','shared stories beneath the stars':'cùng bạn kể chuyện dưới bầu trời sao','treasured the keepsake you brought':'trân trọng món quà kỷ niệm bạn mang đến','smiled at your thoughtful gift':'mỉm cười trước món quà đầy tình cảm','promised to keep your ribbon safe':'hứa giữ gìn chiếc ruy băng bạn tặng'};
      return firstName+' '+(Object.entries(moments).find(([key])=>message.includes(key))?.[1]||'đã có thêm một kỷ niệm đẹp bên bạn')+'.';
    }
    return message;
  }
  function combatLine(entry) {
    let line=typeof entry==='string'?entry:field(entry,'Message',JSON.stringify(entry));
    if(tcg.language==='vi')line=line.replace(/^Turn /,'Lượt ').replace('Sky guardian','Hộ vệ bầu trời').replace('Training sentinel','Lính gác huấn luyện').replace(' dealt ',' gây ').replace(' critical',' chí mạng').replace(' damage. Enemy HP ',' sát thương. HP địch ').replace(' hit ',' đánh ').replace(' for ',' gây ');
    return line;
  }
  function renderResult(result=state.result) {
    if(!result)return '';
    const rewards=[];if(num(field(result,'GemsAwarded')))rewards.push('◆ +'+field(result,'GemsAwarded'));if(num(field(result,'XpAwarded')))rewards.push('XP +'+field(result,'XpAwarded'));if(num(field(result,'BondAwarded')))rewards.push('♡ +'+field(result,'BondAwarded'));
    const items=field(result,'Items',[]);if(Array.isArray(items))for(const item of items)rewards.push(typeof item==='string'?item:itemName(item)+' ×'+field(item,'Count',1));
    const log=field(result,'CombatLog',[]),message=resultMessage(result);
    return `<div class="lifeResult"><h3>${resultTitle(result)}</h3>${message?`<p>${safe(message)}</p>`:''}${rewards.length?`<div class="lifeRewards">${rewards.map(value=>`<span>${safe(value)}</span>`).join('')}</div>`:''}${Array.isArray(log)&&log.length?`<details class="lifeCombatLog"><summary>${tx('Battle log','Diễn biến trận đấu')}</summary><ol>${log.map(entry=>`<li>${safe(combatLine(entry))}</li>`).join('')}</ol></details>`:''}</div>`;
  }
  function choose(title,callback,settings={}) {
    const serverMode=num(state.data?.rosterTotal)>500||num(field(gameplay(),'ProfilesTotal'))>500||field(gameplay(),'ProfilesHasMore',false)||state.data?.hasMore===true;
    state.picker={callback,settings,query:'',limit:30,serverMode,items:[],profileLookup:new Map(),serverHasMore:false,nextOffset:0,generation:0};
    $('lifestylePickerTitle').textContent=title;
    $('lifestylePickerSearch').placeholder=tx('Character or series','Tên nhân vật hoặc series');
    $('lifestylePickerSearch').value='';
    $('lifestylePickerMore').textContent=tx('Load more','Xem thêm');
    renderPicker();$('lifestylePicker').showModal();watchImages($('lifestylePicker'));$('lifestylePickerSearch').focus();
    if(serverMode)searchPicker();
  }
  async function searchPicker(more=false) {
    const p=state.picker;if(!p)return;const generation=++p.generation,query=p.query;
    p.loading=true;renderPicker();
    try{
      const response=await call('hytcgLifestyle',{action:'roster',query:query.slice(0,80),offset:more?p.nextOffset:0,count:60});
      if(state.picker!==p||generation!==p.generation||query!==p.query)return;
      rememberCards(response);const returned=response.roster||[];
      if(!more)p.profileLookup.clear();for(const item of response.profiles||field(response.profilePage,'Items',[]))p.profileLookup.set(String(field(item,'CardId')),item);
      p.items=more?[...p.items,...returned]:returned;p.serverHasMore=response.hasMore===true;p.nextOffset=num(response.nextOffset);p.loading=false;renderPicker();
    }catch(error){if(state.picker===p&&generation===p.generation){p.loading=false;p.error=errorMessage(error);renderPicker();}}
  }
  function renderPicker() {
    const p=state.picker;if(!p)return;
    const query=p.query.toLocaleLowerCase(),excluded=new Set((p.settings.exclude||[]).map(String));
    const list=(p.serverMode?p.items:roster()).filter(x=>{const id=String(field(x,'CardId')),pr=p.profileLookup.get(id)||profile(id);return !excluded.has(id)&&(!p.settings.playable||pr)&&(!p.settings.available||(pr&&!field(pr,'Busy',false)))&&(!query||(field(x,'Name')+' '+field(x,'Series')).toLocaleLowerCase().includes(query));});
    $('lifestylePickerGrid').innerHTML=list.slice(0,p.limit).map(x=>{const id=String(field(x,'CardId')),pr=p.profileLookup.get(id)||profile(id);return `<button class="lifeCardChoice" data-life-pick="${safe(id)}">${miniCard(id)}<strong>${safe(field(x,'Name'))}</strong><span>${safe(field(x,'Rarity'))} · ${p.settings.available?tx('Effort ','Nỗ lực ')+field(pr,'Effort',0):safe(field(x,'Series'))}</span></button>`;}).join('')||empty(tx('No matching characters','Chưa có nhân vật phù hợp'));
    applyVisualStyles($('lifestylePickerGrid'));
    if(p.loading&&!list.length)$('lifestylePickerGrid').innerHTML=empty(tx('Loading characters…','Đang tải nhân vật…'));
    if(p.error)$('lifestylePickerGrid').innerHTML=empty(p.error);
    $('lifestylePickerMore').hidden=list.length<=p.limit&&(!p.serverMode||!p.serverHasMore);
    $('lifestylePickerMore').disabled=Boolean(p.loading);
    watchImages($('lifestylePicker'));
  }
  function watchImages(root) {
    for(const node of observedImages)if(!node.isConnected){observer?.unobserve(node);observedImages.delete(node);}
    for(const node of root.querySelectorAll('img[data-life-image]')){
      if(node.dataset.lifeObserved)continue;node.dataset.lifeObserved='true';
      const {key}=imageSpec(node);
      const cached=state.imageCache.get(key);if(cached){node.src=cached;continue;}
      if(observer){observer.observe(node);observedImages.add(node);}else enqueueImage(node);
    }
  }
  const observer=typeof IntersectionObserver==='function'?new IntersectionObserver(entries=>{for(const entry of entries)if(entry.isIntersecting){observer.unobserve(entry.target);observedImages.delete(entry.target);enqueueImage(entry.target);}},{rootMargin:'120px'}):null;
  function imageSpec(node) {
    const cardId=node.dataset.lifeImage,payload={cardId,thumbnail:true};
    if(node.dataset.lifePreviewFrame){payload.previewFrameId='classic';if(node.dataset.lifePreviewDye)payload.previewDyeId=node.dataset.lifePreviewDye;if(node.dataset.lifePreviewArtwork)payload.previewArtworkId=node.dataset.lifePreviewArtwork;else payload.previewOriginalArtwork=true;Object.assign(payload,artworkPosition({artworkScale:node.dataset.lifeArtworkScale,artworkX:node.dataset.lifeArtworkX,artworkY:node.dataset.lifeArtworkY}));}
    else if(node.dataset.lifeOriginal)payload.original=true;
    return {payload,key:imageKey(payload)};
  }
  const imageKey = payload => payload.cardId+':'+(state.imageVersions.get(payload.cardId)||0)+':'+state.imageRevision+':'+JSON.stringify(payload);
  function enqueueImage(node) { if(node.dataset.lifeQueued)return;node.dataset.lifeQueued='true';state.imageQueue.push(node);pumpImages(); }
  function pumpImages() {
    while(state.imageWorkers<2&&state.imageQueue.length){
      const node=state.imageQueue.shift();if(!node.isConnected||node.closest('[hidden]')||node.closest('dialog:not([open])')){delete node.dataset.lifeQueued;continue;}
      const epoch=state.epoch,{key,payload}=imageSpec(node),cached=state.imageCache.get(key);if(cached){node.src=cached;continue;}
      state.imageWorkers++;
      let promise=state.imagePending.get(key);
      if(!promise){promise=call('hytcgLifestyleImage',payload);state.imagePending.set(key,promise);}
      promise.then(response=>{if(epoch===state.epoch&&response.imageData&&imageKey(payload)===key){state.imageCache.delete(key);state.imageCache.set(key,response.imageData);while(state.imageCache.size>80)state.imageCache.delete(state.imageCache.keys().next().value);if(node.isConnected&&imageSpec(node).key===key)node.src=response.imageData;}}).catch(()=>{}).finally(()=>{state.imagePending.delete(key);state.imageWorkers--;pumpImages();});
    }
  }
  function invalidateImages(id) { id=String(id);state.imageVersions.set(id,(state.imageVersions.get(id)||0)+1);for(const key of state.imageCache.keys())if(key.startsWith(id+':'))state.imageCache.delete(key); }
  function decorate(node,id,metadata=cosmetic(id)) {
    if(!node)return;
    node.classList.remove('lifeCosmetic');
    node.classList.toggle('lifeMystic',Boolean(field(metadata,'Mystic',false)));
    if(field(metadata,'Mystic',false))node.style.setProperty('--life-mystic-color',validColor(field(metadata,'Dye')));else node.style.removeProperty('--life-mystic-color');
  }
  content.addEventListener('input',event=>{
    const node=event.target;
    if(node.id==='lifeAlbumName'&&state.album){state.album.name=node.value;state.dirty=true;}
    if(node.id==='lifeInventorySearch'){state.inventoryQuery=node.value;$('lifeInventoryGrid').innerHTML=filteredInventory(inventoryItems(),state.inventoryFilter,state.inventoryQuery).map(item=>inventoryTile(item)).join('')||empty(tx('No matching items','Chưa có vật phẩm phù hợp'),'inventory');applyVisualStyles($('lifeInventoryGrid'));}
  });
  content.addEventListener('change',event=>{const node=event.target;if(node.id==='lifeAlbumBackground'&&state.album){state.album.background=node.value;state.dirty=true;render();}});
  function selectDye(item,current=false) {
    const s=getStudio();if(!s)return;
    const saved=cosmetic(s.cardId);
    s.dyeId=current?field(saved,'DyeId',''):String(field(item,'Id'));
    s.dye=current?validColor(field(saved,'Dye'),rarityColor(s.cardId)):validColor(field(item,'Color'));
    s.mystic=current?field(saved,'Mystic',false):field(item,'Mystic',false);
    s.morph=current?validColor(field(saved,'Morph'),'#e796b9'):'#e796b9';s.morphMode=current?field(saved,'MorphMode','none'):'none';
    state.studioPanel='style';state.view='studio';render();
  }
  content.addEventListener('click',async event=>{
    const node=event.target.closest('button');if(!node||node.disabled||state.busy)return;
    if(node.dataset.lifeAlbum){state.album=albumDraft(albums().find(a=>String(field(a,'Id'))===node.dataset.lifeAlbum));state.dirty=false;render();return;}
    if(node.dataset.lifeRoute){state.routeId=node.dataset.lifeRoute;render();return;}
    if(node.dataset.lifeInventoryFilter){state.inventoryFilter=node.dataset.lifeInventoryFilter;render();return;}
    if(node.dataset.lifeCurrentDye){selectDye(null,true);return;}
    const dyeId=node.dataset.lifeDyeId||node.dataset.lifeInventoryDye;if(dyeId){const item=dye(dyeId);if(num(field(item,'Count'))>0)selectDye(item);return;}
    if(node.dataset.lifeFill!==undefined&&state.album){const index=num(node.dataset.lifeFill);choose(tx('Fill this slot','Thêm thẻ vào ô'),id=>{if(state.album){state.album.cardIds[index]=id;state.dirty=true;render();}},{exclude:state.album.cardIds.filter(Boolean)});return;}
    if(node.dataset.lifeMove!==undefined&&state.album){const from=num(node.dataset.lifeMove),to=from+num(node.dataset.direction);if(to>=0&&to<state.album.cardIds.length){[state.album.cardIds[from],state.album.cardIds[to]]=[state.album.cardIds[to],state.album.cardIds[from]];state.dirty=true;render();}return;}
    if(node.dataset.lifeRemove!==undefined&&state.album){state.album.cardIds.splice(num(node.dataset.lifeRemove),1);state.dirty=true;render();return;}
    if(node.dataset.lifeTeamRemove){state[node.dataset.lifeTeamRemove==='arena'?'arenaTeam':'expeditionTeam'].splice(num(node.dataset.index),1);render();return;}
    if(node.dataset.lifeTeamAdd){const key=node.dataset.lifeTeamAdd==='arena'?'arenaTeam':'expeditionTeam';choose(tx('Choose your team','Chọn đội của bạn'),id=>{if(state[key].length<3)state[key].push(id);render();},{exclude:state[key],available:true});return;}
    if(node.dataset.lifeDate){const activity=node.dataset.lifeDate,payload={cardId:state.datingId,activity};if(activity==='gift')payload.itemId=$('lifeGift').value;const response=await run('date',payload);if(response)render();return;}
    const action=node.dataset.lifeAction;if(!action)return;
    if(action==='refresh'){refresh();return;}
    if(action==='openStudio'){state.view='studio';state.studioPanel='style';render();return;}
    if(action==='openInventory'){state.view='inventory';render();return;}
    if(action==='chooseDye'){chooseInventoryItem('dye');return;}
    if(action==='dyeBoosterBuy'){
      if(gems()<dyePrice())return;const response=await paidRun('dyeBoosterBuy');if(response){state.lastDyeId=field(field(creative(),'LastStudioReceipt',response.result),'DyeId');render();toast(tx('Dye added to your inventory','Đã thêm màu nhuộm vào kho'));}return;
    }
    if(action==='albumNew'){state.album=albumDraft();state.dirty=false;render();return;}
    if(action==='albumClose'){state.album=null;state.dirty=false;render();return;}
    if(action==='albumAdd'){choose(tx('Add to album','Thêm vào album'),id=>{if(state.album&&state.album.cardIds.length<54){state.album.cardIds.push(id);state.dirty=true;render();}},{exclude:state.album?.cardIds});return;}
    if(action==='albumBlank'){if(state.album&&state.album.cardIds.length<54){state.album.cardIds.push('');state.dirty=true;render();}return;}
    if(action==='albumView'){openAlbumView();return;}
    if(action==='albumSave'){
      const a=state.album;if(!a)return;const response=await run('albumSave',{...(a.id?{albumId:a.id}:{}),name:a.name,background:a.background,cardIds:a.cardIds});
      if(response){const saved=albums().find(x=>field(x,'Id')===a.id)||albums().find(x=>field(x,'Name')===a.name);state.album=albumDraft(saved||{Id:a.id,Name:a.name,Background:a.background,CardIds:a.cardIds});state.dirty=false;render();toast(tx('Album saved','Đã lưu album'));}return;
    }
    if(action==='albumDelete'){
      const a=state.album;if(!a?.id)return;
      if(node.dataset.confirm!=='true'){node.dataset.confirm='true';node.textContent=tx('Delete album? Click again','Xóa album? Nhấn lần nữa');return;}
      const response=await run('albumDelete',{albumId:a.id});if(response){state.album=null;render();toast(tx('Album deleted','Đã xóa album'));}return;
    }
    if(action==='chooseStudio'){choose(tx('Choose a character','Chọn nhân vật'),id=>{state.studioId=id;state.studio=null;state.canvasData='';render();});return;}
    if(action==='chooseDating'){choose(tx('Spend time together','Dành thời gian bên nhau'),id=>{state.datingId=id;state.result=null;render();},{playable:true});return;}
    if(action==='chooseInventory'){choose(tx('Use items on','Dùng vật phẩm cho'),id=>{state.inventoryTarget=id;render();},{available:true});return;}
    if(action==='cosmeticSave'){
      const s=state.studio;if(!s||!studioChanged())return;const q=await requestStudioQuote(true);if(!q||state.studio!==s||num(field(q,'TotalGems'))>gems())return;const payload=studioPayload(s);
      const response=await paidRun('cosmeticSave',payload);if(response){invalidateImages(s.cardId);state.studio=null;render();document.dispatchEvent(new CustomEvent('hytcgCosmeticChanged',{detail:{cardId:s.cardId}}));toast(tx('Look saved','Đã lưu trang trí'));}return;
    }
    if(action==='cosmeticReset'){const id=state.studio.cardId;const response=await run('cosmeticReset',{cardId:id});if(response){invalidateImages(id);state.studio=null;state.paidOperation=null;render();document.dispatchEvent(new CustomEvent('hytcgCosmeticChanged',{detail:{cardId:id,reset:true}}));toast(tx('Original look restored','Đã khôi phục trang trí gốc'));}return;}
    if(action==='cosmeticRestore'){const id=state.studio.cardId;const response=await paidRun('cosmeticSave',{cardId:id,restorePrevious:true});if(response){invalidateImages(id);state.studio=null;render();document.dispatchEvent(new CustomEvent('hytcgCosmeticChanged',{detail:{cardId:id}}));toast(tx('Previous look restored','Đã khôi phục trang trí trước'));}return;}
    if(action==='artworkPositionReset'){state.studio.artworkScale=1;state.studio.artworkX=state.studio.artworkY=0;for(const key of['Scale','X','Y']){const value=key==='Scale'?100:0;const input=$('lifeArtwork'+key),output=$('lifeArtwork'+key+'Value');if(input)input.value=String(value);if(output)output.textContent=value+'%';}updateStudioPreview();return;}
    if(action==='artworkOriginal'){state.studio.artworkId='';state.studio.artworkData='';state.studio.useOriginal=true;updateStudioPreview();return;}
    if(['artworkPick','artworkUrl','artworkDraw'].includes(action)){
      const payload={};if(action==='artworkUrl')payload.url=$('lifeArtworkUrl').value.trim();if(action==='artworkDraw')payload.png=$('lifeCanvas').toDataURL('image/png');
      const current=state.studio,response=await run(action,payload);if(response&&state.studio===current){current.artworkId=response.artworkId||field(response,'ArtworkId');current.artworkData=response.imageData||'';current.useOriginal=false;updateStudioPreview();toast(tx('Artwork ready · save your look','Artwork sẵn sàng · lưu trang trí'));}return;
    }
    if(action==='expeditionStart'){const response=await run(action,{routeId:state.routeId,cardIds:state.expeditionTeam});if(response){state.expeditionTeam=[];render();}return;}
    if(action==='expeditionClaim'){const response=await run(action,{expeditionId:node.dataset.lifeClaim});if(response)render();return;}
    if(action==='inventoryUse'){const response=await run(action,{itemId:node.dataset.item,cardId:state.inventoryTarget});if(response)render();return;}
    if(action==='fight'){const response=await run(action,{stage:num(field(field(gameplay(),'Fight',{}),'Stage',1)),cardIds:state.arenaTeam});if(response){state.arenaSetup=false;render();}}
  });
  content.addEventListener('click',async event=>{
    const node=event.target.closest('[data-life-item]');if(!node||node.disabled||state.busy)return;const item=inventoryItems().find(x=>String(field(x,'Id'))===node.dataset.lifeItem);if(!item)return;
    const kind=inventoryCategory(item);if(kind==='dye'){selectDye(item);return;}if(kind==='material')return;
    const use=async id=>{state.inventoryTarget=id;const response=await run('inventoryUse',{itemId:String(field(item,'Id')),cardId:id});if(response)render();};
    if(state.inventoryTarget){if(!characterBusy(state.inventoryTarget))await use(state.inventoryTarget);}else choose(tx('Use items on','Dùng vật phẩm cho'),use,{available:true});
  });
  $('lifestyleNav').addEventListener('click',event=>{const node=event.target.closest('[data-life-view]');if(!node)return;state.result=null;status();activate(node.dataset.lifeView);});
  $('lifestyleTab').addEventListener('click',()=>activate(state.view));
  $('lifestyleRefresh').addEventListener('click',()=>refresh());
  $('lifestylePickerSearch').addEventListener('input',event=>{if(state.picker){const p=state.picker;p.query=event.target.value;p.limit=30;p.error='';if(p.serverMode){clearTimeout(state.pickerTimer);p.generation++;state.pickerTimer=setTimeout(()=>searchPicker(),250);}else renderPicker();}});
  $('lifestylePickerMore').addEventListener('click',()=>{if(state.picker){const p=state.picker;if(p.serverMode&&p.limit>=p.items.length&&p.serverHasMore){p.limit+=60;searchPicker(true);}else{p.limit+=30;renderPicker();}}});
  $('lifestylePickerGrid').addEventListener('click',event=>{const node=event.target.closest('[data-life-pick]');if(!node||state.busy)return;const p=state.picker,id=node.dataset.lifePick;if(p?.serverMode){const item=p.items.find(x=>String(field(x,'CardId'))===id),pr=p.profileLookup.get(id);rememberCards({roster:item?[item]:[],profiles:pr?[pr]:[]});}const callback=p?.callback;$('lifestylePicker').close();state.picker=null;callback?.(id);});
  document.querySelectorAll('[data-life-close]').forEach(node=>node.addEventListener('click',()=>$(node.dataset.lifeClose).close()));
  $('lifestylePicker').addEventListener('close',()=>{state.picker=null;clearTimeout(state.pickerTimer);});
  $('lifestyleItemSearch').addEventListener('input',event=>{if(state.itemPicker){state.itemPicker.query=event.target.value;renderItemPicker();}});
  $('lifestyleItemPickerGrid').addEventListener('click',event=>{
    const node=event.target.closest('button');if(!node||node.disabled||state.busy)return;const p=state.itemPicker;if(!p)return;
    if(node.dataset.lifeKeepItem){selectDye(null,true);}
    else if(node.dataset.lifeItemPick){const item=inventoryItems().find(x=>String(field(x,'Id'))===node.dataset.lifeItemPick);if(!item)return;if(inventoryCategory(item)!=='dye')return;selectDye(item);}
    else return;$('lifestyleItemPicker').close();state.itemPicker=null;
  });
  $('lifestyleItemInventory').addEventListener('click',()=>{$('lifestyleItemPicker').close();state.itemPicker=null;state.view='inventory';render();});
  $('lifestyleItemPicker').addEventListener('close',()=>{state.itemPicker=null;});
  new MutationObserver(()=>{$('lifestyleView').hidden?hide():startTimers();}).observe($('lifestyleView'),{attributes:true,attributeFilter:['hidden']});
  document.addEventListener('visibilitychange',()=>document.hidden?hide():startTimers());
  window.HyTcgLifestyle={open,refresh,hide,reset,translate:()=>{nav();if(!$('lifestyleView').hidden)render();},showStudio:cardId=>activate('studio',cardId),showDating:cardId=>activate('dating',cardId),decorate,applySnapshot:data=>{apply(data);if(!$('lifestyleView').hidden)render();},getCosmetic:cosmetic};
  window.HyTcgLifestyle.renderBooster=renderBooster;
  window.HyTcgLifestyle.showView=view=>activate(view);
  window.HyTcgAdventure?.install({state,content,$,tx,safe,field,creative,gameplay,albums,roster,profile,card,gems,miniCard,backgrounds,bgClass,options,btn,choose,run,paidRun,render,watchImages,hydrateAlbumMetadata,teamMarkup,statBlocks,renderResult,chooseCardButton,applyVisualStyles,toast,svg,getStudio});
  $('dyeBoosterMount')?.addEventListener('click',async event=>{
    const button=event.target.closest('[data-life-action="dyeBoosterBuy"]');if(!button||button.disabled||state.busy)return;
    button.disabled=true;if(!state.data)await refresh();const reply=await paidRun('dyeBoosterBuy');
    if(reply){state.lastDyeId=field(field(creative(),'LastStudioReceipt',reply.result),'DyeId');renderBooster();toast(tx('Dye added to inventory','Đã thêm màu nhuộm vào túi đồ'));}
    else renderBooster();
  });
  nav();renderBooster();
})();
