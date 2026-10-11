'use strict';
/* Presentation uses native quotes, ownership, battle turns and purchase receipts. */
window.HyTcgAdventure = (() => {
  let E;
  const F=(o,k,d='')=>E.field(o,k,d), S=value=>E.safe(String(value??'')), T=(en,vi)=>E.tx(en,vi);
  const button=(action,label,extra='',kind='')=>'<button class="lifeButton '+kind+'" data-adventure-action="'+action+'" '+extra+'>'+label+'</button>';
  const draft=a=>({id:F(a,'Id'),name:F(a,'Name'),cover:F(a,'Cover',F(a,'Background','midnight')),background:F(a,'Background','midnight'),capacity:F(a,'Capacity',Math.max(4,F(a,'CardIds',[]).length)),cardIds:[...F(a,'CardIds',[])]});
  const scene=(id,title,subtitle='')=>'<div class="lifeScene scene-'+id+'"><div class="lifeSceneCaption"><span>HYTCG</span><h2>'+S(title)+'</h2>'+(subtitle?'<p>'+S(subtitle)+'</p>':'')+'</div></div>';
  function coverChoices(current,kind) {
    return '<div class="albumSkinChoices">'+Object.entries(E.backgrounds()).map(([id,name])=>'<button class="albumSkin '+E.bgClass(id)+(id===current?' selected':'')+'" data-album-skin="'+id+'" data-album-kind="'+kind+'" aria-pressed="'+(id===current)+'" title="'+S(name)+'"><span>'+S(name)+'</span></button>').join('')+'</div>';
  }
  function albumForm(restyle=false) {
    const a=restyle?E.state.album:E.state.albumPurchase;
    return '<section class="lifeSection albumPurchase"><header class="lifeSectionHeading"><h2>'+T(restyle?'Cover & pages':'New album',restyle?'Bìa và trang':'Album mới')+'</h2>'+button('albumFormClose','×','aria-label="'+T('Close','Đóng')+'"')+'</header>'+
      (!restyle?'<label class="lifeField">'+T('Name','Tên')+'<input id="adventureAlbumName" maxlength="64" value="'+S(a.name)+'"></label>':'')+
      '<div class="albumDesignColumns"><div><h3>'+T('Cover','Bìa')+'</h3>'+coverChoices(a.cover,'cover')+'</div><div><h3>'+T('Pages','Trang')+'</h3>'+coverChoices(a.background,'background')+'</div></div>'+
      '<div class="lifeActions">'+button(restyle?'albumRestyle':'albumPurchase',T(restyle?'Apply':'Buy album',restyle?'Áp dụng':'Mua album')+' · ◆ '+(restyle?200:2000),E.gems()<(restyle?200:2000)?'disabled':'','primary')+
      (!restyle?'<span class="lifeMuted">'+T('4 slots included','Tặng 4 ô')+'</span>':'')+'</div></section>';
  }
  function albumView() {
    const state=E.state,list=E.albums();
    if(state.albumPurchase)return albumForm();
    if(state.albumRestyling)return albumForm(true);
    if(!state.album) return '<section class="lifeSection albumLibrary"><header class="lifeSectionHeading"><h2>'+T('Albums','Album')+'</h2>'+button('albumNew','+ '+T('New album','Album mới')+' · ◆ 2,000',E.gems()<2000?'disabled':'')+'</header>'+
      '<div class="albumBookShelf">'+list.map(a=>'<button class="albumClosed '+E.bgClass(F(a,'Cover',F(a,'Background','midnight')))+'" data-adventure-album="'+S(F(a,'Id'))+'"><span class="albumEmboss">✧ HYTCG ✧</span><strong>'+S(F(a,'Name'))+'</strong><span class="albumCoverSeal">✦</span><small>'+F(a,'CardIds',[]).filter(Boolean).length+' / '+F(a,'Capacity',F(a,'CardIds',[]).length)+'</small></button>').join('')+
      (!list.length?'<div class="albumEmptyBook"><span>▤</span><h3>'+T('Your albums','Album của bạn')+'</h3></div>':'')+'</div></section>';
    const a=state.album,edit=state.albumEditing,total=Math.max(1,Math.ceil(a.capacity/12));
    state.albumPage=Math.min(total-1,Math.max(0,state.albumPage||0));const start=state.albumPage*12;
    const slot=i=>{
      const index=start+i,open=index<a.capacity,id=a.cardIds[index]||'';
      return '<div class="bookSlot '+(!open?'locked':'')+'" '+(edit&&open?'draggable="true" data-life-slot="'+index+'"':'')+'>'+
        (!open?'<span class="albumSlotLock">◇</span>'+(edit?button('albumSlot','+ ◆ 200','title="'+T('Buy a slot','Mua thêm ô')+'"'+(E.gems()<200?' disabled':'')):''):
        id?E.miniCard(id)+(edit?'<button class="albumRemove" data-adventure-remove="'+index+'" aria-label="'+T('Remove card','Bỏ thẻ')+'">×</button>':''):
        edit?'<button class="albumFill" data-life-fill="'+index+'" aria-label="'+T('Choose card','Chọn thẻ')+'">+</button>':'<span class="albumSlotEmpty">✧</span>')+
        '<small class="albumSlotNumber">'+(index+1)+'</small></div>';
    };
    const page=side=>'<div class="bookPage '+(side?'right':'left')+'">'+Array.from({length:6},(_,i)=>slot(side*6+i)).join('')+'<span class="bookPageNumber">'+(state.albumPage*2+side+1)+'</span></div>';
    return '<header class="albumReaderHeader">'+button('albumBack','‹','title="'+T('Albums','Album')+'"')+'<div><h2>'+S(a.name)+'</h2><span>'+a.cardIds.filter(Boolean).length+' / '+a.capacity+' '+T('slots','ô')+'</span></div>'+
      (edit?button('albumSave',T('Save','Lưu'),'','primary')+button('albumRestyleForm','▧','title="'+T('Cover & pages · 200 gems','Bìa và trang · 200 gems')+'"')+button('albumExitEdit','✓','title="'+T('Save & showcase','Lưu và xem album')+'" aria-label="'+T('Save & showcase','Lưu và xem album')+'"'):
      button('albumEdit','✎','title="'+T('Edit album','Chỉnh sửa album')+'" aria-label="'+T('Edit album','Chỉnh sửa album')+'"'))+'</header>'+
      (edit?'<div class="albumEditBar"><label class="lifeField">'+T('Name','Tên')+'<input id="lifeAlbumName" maxlength="64" value="'+S(a.name)+'"></label>'+button('albumSlot','+ '+T('Slot','Ô')+' · ◆ 200',(a.capacity>=120||E.gems()<200)?'disabled':'')+button('albumDelete',T('Delete','Xóa'),'','danger')+'</div>':'')+
      '<div class="albumBook '+E.bgClass(a.background)+(edit?' editing':' showcase')+'" data-cover="'+S(a.cover)+'">'+page(0)+page(1)+'<i class="bookBinding"></i></div>'+
      '<footer class="albumPageControls">'+button('albumPrevious','‹',(state.albumPage===0||state.albumFlipping)?'disabled':'')+'<span>'+(state.albumPage+1)+' / '+total+'</span>'+button('albumNext','›',(state.albumPage===total-1||state.albumFlipping)?'disabled':'')+'</footer>';
  }
  async function flip(direction) {
    const s=E.state,total=Math.max(1,Math.ceil(s.album.capacity/12)),next=s.albumPage+direction;if(s.albumFlipping||next<0||next>=total)return;
    s.albumFlipping=true;const book=E.content.querySelector('.albumBook'),page=book?.querySelector(direction>0?'.bookPage.right':'.bookPage.left');
    if(page&&!matchMedia('(prefers-reduced-motion: reduce)').matches) {
      const sheet=page.cloneNode(true);sheet.classList.add('albumTurningPage');book.append(sheet);
      try{await sheet.animate([{transform:'rotateY(0deg)',filter:'brightness(1)'},{transform:'rotateY('+(direction>0?-180:180)+'deg)',filter:'brightness(.7)'}],{duration:650,easing:'cubic-bezier(.22,.65,.3,1)',fill:'forwards'}).finished;}catch{}
    }
    s.albumPage=next;s.albumFlipping=false;E.render();
  }
  const skillName=id=>({strike:T('Strike','Đánh thường'),guard:T('Guard','Phòng thủ'),mend:T('Mend','Hồi phục'),burst:T('Power burst','Bộc phá'),focus:T('Focus','Tập trung'),pulse:T('Healing pulse','Sóng hồi phục'),nova:T('Star nova','Tinh vân')})[id]||id;
  const skillIcon=id=>({strike:'⚔️',guard:'🛡️',mend:'💚',burst:'💥',focus:'✨',pulse:'🌿',nova:'🌠'})[id]||'✦';
  const fallbackSkills=[['strike','attack',1,0],['guard','guard',1,0],['mend','heal',3,2],['burst','attack',5,2],['focus','focus',8,1],['pulse','teamHeal',12,3],['nova','attack',18,4]].map(([Id,Kind,Level,Energy])=>({Id,Kind,Level,Energy}));
  function skillEditor() {
    const s=E.state,id=s.skillCard||F(E.gameplay().Profiles?.[0],'CardId'),p=E.profile(id),level=F(p,'Level',1),skills=F(p,'Skills',fallbackSkills);
    if(!p)return '<section class="lifeSection">'+button('arenaBack','‹ '+T('Arena','Đấu trường'))+'</section>';
    if(!s.skillDraft||s.skillDraft.id!==id)s.skillDraft={id,ids:[...F(p,'EquippedSkills',['strike','guard'])]};
    return '<section class="lifeSection skillManager"><header class="lifeSectionHeading"><h2>'+T('Skills','Kỹ năng')+'</h2>'+button('arenaBack','×')+'</header>'+
      button('skillCharacter',S(F(p,'Name'))+' · Lv.'+level,'','lifeSkillCharacter')+
      '<div class="skillLearnGrid">'+skills.map(skill=>{
        const key=F(skill,'Id'),locked=level<F(skill,'Level',1);
        return '<label class="skillLearn '+(locked?'locked':'')+'"><span>'+skillIcon(key)+'</span><div><strong>'+S(skillName(key))+'</strong><small>Lv.'+F(skill,'Level')+' · ⚡ '+F(skill,'Energy')+'</small></div><input type="checkbox" data-adventure-skill="'+S(key)+'" '+(s.skillDraft.ids.includes(key)?'checked':'')+' '+(locked||key==='strike'?'disabled':'')+'></label>';
      }).join('')+'</div><div class="lifeActions">'+button('skillsSave',T('Equip skills','Trang bị kỹ năng')+' · '+s.skillDraft.ids.length+'/4',F(p,'Busy',false)?'disabled':'','primary')+'</div></section>';
  }
  function hpBar(id,hp,max,enemy=false) {
    return '<div class="arenaHealth"><div><strong data-arena-name="'+S(id)+'">'+(enemy?T('Enemy','Đối thủ'):T('HP','HP'))+'</strong><span data-arena-hp="'+S(id)+'">'+hp+' / '+max+'</span></div><div class="arenaHpTrack"><i data-arena-bar="'+S(id)+'" data-life-progress="'+(hp/max*100)+'"></i></div></div>';
  }
  function arenaView() {
    const s=E.state,fight=F(E.gameplay(),'Fight',{}),battle=F(E.gameplay(),'Battle',null);
    if(s.arenaSkills)return skillEditor();
    if(!battle||s.arenaSetup) return scene('arena',T('Starlight arena','Đấu trường ánh sao'),T('Stage ','Chặng ')+F(fight,'Stage',1))+
      '<div class="lifeTwoColumn"><section class="lifeSection"><header class="lifeSectionHeading"><h2>'+T('Your team','Đội của bạn')+'</h2>'+button('skillsOpen','✦ '+T('Skills','Kỹ năng'))+'</header>'+
      E.teamMarkup('arena')+'<div class="lifeActions">'+E.btn(T('Enter battle','Vào trận'),'fight',E.state.arenaTeam.length<1||F(fight,'DailyRemaining',0)<1?'disabled':'','primary')+
      '<span class="lifeMuted">'+F(fight,'DailyRemaining',0)+' / '+F(fight,'DailyLimit',3)+' '+T('attempts','lượt')+'</span></div></section><section class="lifeSection">'+E.renderResult(F(fight,'LastResult',null))+'</section></div>';
    const team=F(battle,'Team',[]),complete=F(battle,'Complete',false)&&!s.arenaAnimating,actor=team[F(battle,'Actor',0)],skills=F(E.profile(F(actor,'CardId')),'Skills',fallbackSkills);
    const visual=s.arenaVisual?.id===F(battle,'Id')?s.arenaVisual:null;
    const units=team.map(unit=>{
      const id=F(unit,'CardId'),old=visual?.team.find(u=>F(u,'CardId')===id),hp=old?F(old,'Hp'):F(unit,'Hp'),energy=F(unit,'Energy');
      return '<div class="arenaFighter '+(id===F(actor,'CardId')&&!complete?'acting':'')+(F(unit,'Hp')===0?' fainted':'')+'" data-arena-unit="'+S(id)+'"><div class="arenaFighterName">'+S(F(unit,'Name'))+'</div>'+E.miniCard(id)+hpBar(id,hp,F(unit,'MaxHp'))+'<span class="arenaEnergy">⚡ '+energy+'/6'+(F(unit,'Shield',false)?' · 🛡️':'')+(F(unit,'Focus',0)?' · ✨':'')+'</span></div>';
    }).join('');
    const enemySvg='<svg viewBox="0 0 180 180" aria-hidden="true"><defs><linearGradient id="sentinelBody" x2="0" y2="1"><stop stop-color="#d2d9ff"/><stop offset="1" stop-color="#6875b3"/></linearGradient></defs><ellipse cx="90" cy="157" rx="65" ry="12" fill="#101c4266"/><path d="M45 82 25 120 48 138 67 109M135 82l20 42-23 15-21-29" fill="#8b99d0" stroke="#d9ddff" stroke-width="3"/><path d="M58 69 90 49l34 20 11 53-45 28-45-28z" fill="url(#sentinelBody)" stroke="#e9edff" stroke-width="3"/><path d="m70 33 20-15 20 15 10 27-30 19-30-19z" fill="#242e59" stroke="#afc2ff" stroke-width="3"/><path d="m73 44 13 4m9 0 13-4" stroke="#60e8ed" stroke-width="6"/><path d="m90 92 10 12-10 12-10-12z" fill="#d7a3ff"/><path d="m53 144 12 14-30 4m89-18-12 14 32 4" stroke="#b7c3ef" stroke-width="10"/></svg>';
    return '<header class="arenaBattleHeader"><div><span>'+T('Stage ','Chặng ')+F(battle,'Stage')+'</span><h2>'+T('Turn ','Lượt ')+F(battle,'Turn')+'</h2></div>'+
      (!complete?button('battleForfeit',T('Forfeit','Rút lui'),s.arenaAnimating?'disabled':''):button('arenaNew',T('New battle','Trận mới')) )+'</header>'+
      '<div class="arenaBattlefield scene-arena"><div class="arenaOpponent" data-arena-unit="enemy"><strong>'+S(F(battle,'EnemyName'))+'</strong>'+hpBar('enemy',visual?visual.enemyHp:F(battle,'EnemyHp'),F(battle,'EnemyMaxHp'),true)+enemySvg+'</div><div class="arenaParty">'+units+'</div></div>'+
      (complete?'<section class="lifeSection arenaOutcome">'+E.renderResult(F(fight,'LastResult'))+'</section>':
      '<section class="arenaCommands"><header><strong>'+S(F(actor,'Name'))+'</strong><span>'+T('Choose a skill','Chọn kỹ năng')+'</span></header><div class="arenaSkillGrid">'+F(actor,'Skills',[]).map(id=>{
        const skill=skills.find(x=>F(x,'Id')===id)||{},disabled=s.arenaAnimating||F(actor,'Energy')<F(skill,'Energy');
        return button('battleSkill','<span>'+skillIcon(id)+'</span><b>'+S(skillName(id))+'</b><small>⚡ '+F(skill,'Energy',0)+'</small>','data-skill="'+S(id)+'" '+(disabled?'disabled':''),'arenaSkill');
      }).join('')+'</div>'+(s.arenaTargetSkill?'<div class="arenaTargetChooser"><span>'+T('Heal target','Nhân vật hồi phục')+'</span>'+team.filter(x=>F(x,'Hp')>0).map(u=>button('battleTarget',S(F(u,'Name')),'data-target="'+S(F(u,'CardId'))+'"')).join('')+button('battleTargetCancel','×')+'</div>':'')+'</section>');
  }
  async function turn(skill,target='') {
    const s=E.state,b=F(E.gameplay(),'Battle',null);if(!b||F(b,'Complete')||s.arenaAnimating)return;
    s.arenaVisual={id:F(b,'Id'),enemyHp:F(b,'EnemyHp'),team:structuredClone(F(b,'Team',[]))};s.arenaAnimating=true;s.arenaTargetSkill='';
    const reply=await E.run('battleTurn',{battleId:F(b,'Id'),turn:F(b,'Turn'),skillId:skill,targetId:target});
    if(!reply){s.arenaAnimating=false;s.arenaVisual=null;E.render();return;}
    E.render();const updated=F(E.gameplay(),'Battle'),events=F(updated,'Events',[]);
    for(const event of events) {
      if(s.view!=='arena'||document.hidden)break;
      const actor=E.content.querySelector('[data-arena-unit="'+CSS.escape(F(event,'ActorId'))+'"]'),victim=E.content.querySelector('[data-arena-unit="'+CSS.escape(F(event,'TargetId'))+'"]'),kind=F(event,'Kind');
      const reduced=matchMedia('(prefers-reduced-motion: reduce)').matches;
      if(actor&&!reduced)try{await actor.animate(kind==='attack'?[{transform:'translateX(0)'},{transform:'translateX('+(F(event,'ActorId')==='enemy'?-38:38)+'px)',offset:.5},{transform:'translateX(0)'}]:[{filter:'brightness(1)'},{filter:'brightness(1.5)'},{filter:'brightness(1)'}],{duration:430,easing:'ease-in-out'}).finished;}catch{}
      if(victim) {
        const text=document.createElement('b');text.className='arenaFloating '+(kind==='heal'?'healing':'');text.textContent=F(event,'Amount')?(kind==='heal'?'+':'−')+F(event,'Amount'):kind==='guard'?'🛡️':'✨';victim.append(text);
        const hp=E.content.querySelector('[data-arena-hp="'+CSS.escape(F(event,'TargetId'))+'"]'),bar=E.content.querySelector('[data-arena-bar="'+CSS.escape(F(event,'TargetId'))+'"]');
        const max=F(event,'TargetId')==='enemy'?F(updated,'EnemyMaxHp'):F(updated,'Team',[]).find(x=>F(x,'CardId')===F(event,'TargetId'))?.MaxHp;
        if(hp)hp.textContent=F(event,'HpAfter')+' / '+max;if(bar)bar.style.setProperty('--life-progress',(F(event,'HpAfter')/max*100)+'%');
        if(!reduced)try{await victim.animate(kind==='attack'?[{filter:'brightness(1)'},{filter:'brightness(1.8)'},{filter:'brightness(1)'}]:[{boxShadow:'0 0 0 0 #87eebc00'},{boxShadow:'0 0 25px 5px #87eebc99'},{boxShadow:'0 0 0 0 #87eebc00'}],{duration:330}).finished;}catch{}
        text.remove();
      }
    }
    s.arenaAnimating=false;s.arenaVisual=null;E.render();
  }
  function artworkPanel(section) {
    const mode=E.state.artMode||'original',labels={original:T('Original','Gốc'),file:T('Image','Ảnh'),url:'URL',sketch:T('Sketch','Vẽ')};
    section.innerHTML='<header class="lifeSectionHeading"><h2>'+T('Artwork','Artwork')+'</h2><span>64 '+T('colors','màu')+'</span></header><div class="artworkModes">'+Object.entries(labels).map(([id,label])=>button('artMode',label,'data-mode="'+id+'" aria-pressed="'+(id===mode)+'"',id===mode?'active':'')).join('')+'</div>'+
      (mode==='file'?'<div class="artworkModeBody">'+E.btn(T('Choose image','Chọn ảnh'),'artworkPick','','primary')+'</div>':
      mode==='url'?'<div class="lifeArtworkRow artworkModeBody"><input id="lifeArtworkUrl" type="url" placeholder="https://…" maxlength="2048" aria-label="'+T('Image URL','Liên kết ảnh')+'">'+E.btn(T('Load image','Tải ảnh'),'artworkUrl','','primary')+'</div>':
      mode==='sketch'?'<div class="lifeCanvasWrap"><canvas id="lifeCanvas" width="600" height="840" aria-label="'+T('Drawing','Bảng vẽ')+'"></canvas></div><div class="lifeBrushTools"><input id="lifeBrushColor" type="color" value="#aa55ff" aria-label="'+T('Brush color','Màu cọ')+'"><input id="lifeBrushSize" type="range" min="2" max="64" value="14" aria-label="'+T('Brush size','Cỡ cọ')+'"><button id="lifeEraser" class="lifeButton" aria-pressed="false">'+T('Eraser','Tẩy')+'</button><button id="lifeCanvasClear" class="lifeButton">'+T('Clear','Xóa')+'</button></div><div class="lifeActions">'+E.btn(T('Use drawing','Dùng bản vẽ'),'artworkDraw','','primary')+'</div>':
      '<div class="artworkOriginal"><span>✧</span>'+E.btn(T('Use original artwork','Dùng artwork gốc'),'artworkOriginal')+'</div>')+
      '<p class="artworkFormula">'+T('Price = visible pixels × colors ÷ 10,000','Giá = pixel có màu × số màu ÷ 10.000')+'</p>';
  }
  function afterRender(root) {
    const s=E.state;
    if(s.view==='studio'&&s.studioPanel==='style') {
      const right=root.querySelector('.lifeTwoColumn>.lifeSection:last-child');if(right&&E.getStudio())artworkPanel(right);
      root.querySelector('.lifeStudioSelections+.lifeActions')?.remove();
      const reset=root.querySelector('[data-life-action="cosmeticReset"]'),look=E.state.studio;if(reset&&!look?.artworkId&&!look?.dyeId&&(look?.artworkScale||1)===1&&!(look?.artworkX||look?.artworkY))reset.hidden=true;
    }
    if(s.view==='inventory') {
      root.querySelector('.lifeDyeBooster')?.remove();const target=root.querySelector('.lifeCharacterButton');if(target&&!s.inventoryTarget)target.remove();else target?.classList.add('compact');
      const emoji={training:'📘',tea:'☕',gift:'🎁',fiber:'🧵',petal:'🌸',crystal:'💎'};
      root.querySelectorAll('[data-life-item]').forEach(tile=>{const icon=tile.querySelector('.lifeItemIcon');if(icon)icon.textContent=emoji[tile.dataset.lifeItem]||'🖼️';});
    }
    if(s.view==='expedition') {
      const route=F(E.gameplay(),'Routes',[]).find(x=>F(x,'Id')===s.routeId),id=F(route,'Id','meadow');
      root.insertAdjacentHTML('afterbegin',scene(id,T(F(route,'Name','Expedition'),({meadow:'Đồng cỏ ánh trăng',observatory:'Đài thiên văn ánh sao',ruins:'Di tích trên mây'})[id]||'Thám hiểm')));
      root.querySelectorAll('[data-life-route]').forEach(node=>{node.classList.add('illustratedRoute','scene-'+node.dataset.lifeRoute);});
    }
    if(s.view==='dating')root.insertAdjacentHTML('afterbegin',scene('bond',T('A moment together','Một kỷ niệm bên nhau')));
    if(s.view==='album'&&s.album)E.hydrateAlbumMetadata(s.album.cardIds.slice((s.albumPage||0)*12,(s.albumPage||0)*12+12));
  }
  async function saveAlbumDraft(notify=false) {
    const s=E.state,a=s.album;
    if(!a||!s.dirty)return true;
    const r=await E.run('albumSave',{albumId:a.id,name:a.name,cardIds:[...a.cardIds]});
    if(!r)return false;
    if(s.album?.id===a.id){s.album=draft(E.albums().find(x=>F(x,'Id')===a.id));s.dirty=false;}
    if(notify)E.toast(T('Album saved','Đã lưu album'));
    return true;
  }
  async function click(event) {
    const node=event.target.closest('button');if(!node||node.disabled||E.state.busy)return;const s=E.state;
    if(node.dataset.adventureAlbum){s.album=draft(E.albums().find(a=>F(a,'Id')===node.dataset.adventureAlbum));s.albumEditing=false;s.dirty=false;s.albumPage=0;E.render();return;}
    if(node.dataset.albumSkin){const a=s.albumRestyling?s.album:s.albumPurchase;if(a){a[node.dataset.albumKind]=node.dataset.albumSkin;E.render();}return;}
    if(node.dataset.adventureRemove!==undefined){s.album.cardIds[Number(node.dataset.adventureRemove)]='';s.dirty=true;E.render();return;}
    const action=node.dataset.adventureAction;if(!action)return;
    if(action==='albumNew'){s.albumPurchase={name:T('My album','Album của tôi'),cover:'midnight',background:'paper'};E.render();}
    else if(action==='albumFormClose'){if(s.albumRestyling)s.album=draft(E.albums().find(a=>F(a,'Id')===s.album.id));s.albumRestyling=false;s.albumPurchase=null;E.render();}
    else if(action==='albumPurchase'){const a=s.albumPurchase,r=await E.paidRun('albumCreate',a);if(r){const id=F(F(E.creative(),'LastStudioReceipt'),'AlbumId');s.album=draft(E.albums().find(x=>F(x,'Id')===id));s.albumPurchase=null;s.albumEditing=false;s.albumPage=0;E.render();}}
    else if(action==='albumEdit'){s.albumEditing=true;s.dirty=false;E.render();}
    else if(action==='albumBack'){if(await saveAlbumDraft()){s.album=null;s.albumEditing=false;E.render();}}
    else if(action==='albumExitEdit'){if(await saveAlbumDraft()){s.albumEditing=false;E.render();}}
    else if(action==='albumNext'||action==='albumPrevious')await flip(action==='albumNext'?1:-1);
    else if(action==='albumSave'){if(await saveAlbumDraft(true))E.render();}
    else if(action==='albumSlot'){const a=s.album;if(s.dirty){E.toast(T('Save the album first','Lưu album trước khi mua ô'));return;}const r=await E.paidRun('albumSlots',{albumId:a.id,count:1});if(r){s.album=draft(E.albums().find(x=>F(x,'Id')===a.id));E.render();}}
    else if(action==='albumRestyleForm'){if(s.dirty){E.toast(T('Save the album first','Lưu album trước khi đổi bìa'));return;}s.albumRestyling=true;E.render();}
    else if(action==='albumRestyle'){const a=s.album,r=await E.paidRun('albumRestyle',{albumId:a.id,cover:a.cover,background:a.background});if(r){s.album=draft(E.albums().find(x=>F(x,'Id')===a.id));s.albumRestyling=false;E.render();}}
    else if(action==='albumDelete'){if(node.dataset.confirm!=='true'){node.dataset.confirm='true';node.textContent=T('Confirm delete','Xác nhận xóa');return;}const r=await E.run('albumDelete',{albumId:s.album.id});if(r){s.album=null;s.albumEditing=false;E.render();}}
    else if(action==='artMode'){s.artMode=node.dataset.mode;E.render();}
    else if(action==='arenaNew'){s.arenaSetup=true;s.result=null;E.render();}
    else if(action==='skillsOpen'){s.arenaSkills=true;s.skillCard=s.arenaTeam[0]||F(E.gameplay().Profiles?.[0],'CardId');s.skillDraft=null;E.render();}
    else if(action==='arenaBack'){s.arenaSkills=false;E.render();}
    else if(action==='skillCharacter'){E.choose(T('Choose a character','Chọn nhân vật'),id=>{s.skillCard=id;s.skillDraft=null;E.render();},{available:true});}
    else if(action==='skillsSave'){const r=await E.run('skillsSave',{cardId:s.skillDraft.id,skillIds:s.skillDraft.ids});if(r){s.arenaSkills=false;E.render();}}
    else if(action==='battleForfeit'){if(node.dataset.confirm!=='true'){node.dataset.confirm='true';node.textContent=T('Confirm','Xác nhận');return;}const r=await E.run('battleForfeit',{battleId:F(F(E.gameplay(),'Battle'),'Id')});if(r)E.render();}
    else if(action==='battleSkill'){const id=node.dataset.skill;if(id==='mend'){s.arenaTargetSkill=id;E.render();}else await turn(id);}
    else if(action==='battleTarget')await turn(s.arenaTargetSkill,node.dataset.target);
    else if(action==='battleTargetCancel'){s.arenaTargetSkill='';E.render();}
  }
  function install(env) {
    E=env;Object.assign(E.state,{albumEditing:false,albumPage:0,albumPurchase:null,albumRestyling:false,arenaSkills:false,arenaSetup:false,arenaAnimating:false,artMode:'original'});
    E.content.addEventListener('click',click);
    E.content.addEventListener('input',event=>{if(event.target.id==='adventureAlbumName'&&E.state.albumPurchase)E.state.albumPurchase.name=event.target.value;});
    E.content.addEventListener('change',event=>{
      const node=event.target;if(!node.dataset.adventureSkill)return;const ids=E.state.skillDraft?.ids;if(!ids)return;
      if(node.checked&&ids.length>=4){node.checked=false;E.toast(T('Choose up to 4 skills','Chọn tối đa 4 kỹ năng'));return;}
      E.state.skillDraft.ids=node.checked?[...ids,node.dataset.adventureSkill]:ids.filter(id=>id!==node.dataset.adventureSkill);E.render();
    });
    document.addEventListener('click',event=>{const node=event.target.closest('[data-mission-adventure]');if(node)window.HyTcgLifestyle.showView(node.dataset.missionAdventure);});
    renderMissions();
  }
  function renderMissions() {
    if(!E)return false;
    const map={movie:[T('Movie night','Xem phim'),'🎬','',T('Watch','Xem')],game:['','🎮','',T('Play','Chơi')],expedition:[T('Complete an expedition','Hoàn tất 1 chuyến thám hiểm'),'🗺️','expedition',T('Explore','Thám hiểm')],arena:[T('Win an arena battle','Thắng 1 trận đấu trường'),'⚔️','arena',T('Arena','Đấu trường')],gift:[T('Give a gift','Tặng quà cho nhân vật'),'🎁','dating',T('Bond','Gắn kết')]};
    E.$('missionDay').textContent=tcg.missionDay||'';
    E.$('missionSummary').textContent=tcg.missions.filter(m=>F(m,'Complete')).length+' / '+tcg.missions.length;
    E.$('missionCards').innerHTML=tcg.missions.map(m=>{
      const kind=F(m,'Kind'),[title,icon,view,cta]=map[kind]||map.game,done=F(m,'Complete',false),progress=Number(F(m,'Progress',0)),target=Number(F(m,'Target',1));
      const attrs=view?'data-mission-adventure="'+view+'"':kind==='movie'?'data-mission-movie':'data-mission-play="'+S(F(m,'GameId'))+'"';
      return '<article class="missionCard '+(done?'complete':'')+'"><div class="missionIcon">'+(done?'✓':icon)+'</div><div class="missionBody"><h3>'+S(title||F(m,'Title'))+'</h3><span>'+(done?T('Completed','Hoàn thành'):kind==='movie'?Math.floor(progress/60)+' / 20 '+T('min','phút'):progress+' / '+target)+'</span><div class="missionProgress"><i data-life-progress="'+(progress/target*100)+'"></i></div></div><div class="missionReward"><b>◆ '+F(m,'Reward')+'</b>'+(!done?'<button class="quietButton" '+attrs+'>'+cta+'</button>':'')+'</div></article>';
    }).join('');
    E.$('missionCards').querySelectorAll('[data-life-progress]').forEach(node=>node.style.width=node.dataset.lifeProgress+'%');return true;
  }
  function reset(){if(!E)return;Object.assign(E.state,{albumPurchase:null,albumRestyling:false,albumEditing:false,albumPage:0,arenaSetup:false,arenaSkills:false,arenaAnimating:false,arenaVisual:null,skillDraft:null,artMode:'original'});}
  return {install,afterRender,reset,renderMissions,renderView:view=>!E?null:view==='album'?albumView():view==='arena'?arenaView():null};
})();
