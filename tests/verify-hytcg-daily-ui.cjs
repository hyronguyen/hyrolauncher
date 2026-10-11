'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require('./test-runtime.cjs').playwright();
const root=testRuntime.projectRoot,out=path.join(root,'output','verification','hytcg-daily-ui');
const image='data:image/png;base64,'+fs.readFileSync(path.join(root,'assets','default-logo.png')).toString('base64');
const rates=[['Common',65,'#aebccc'],['Uncommon',22,'#6fd6b0'],['Rare',7.3,'#7ab9f9'],['SR',4,'#bf96ef'],['SSR',1.1,'#f5c879'],['UR',.6,'#f6c6ff']].map(([Name,Weight,Color])=>({Name,Weight,Color}));
const card={CharacterId:99991,Name:'Alina Gray',Series:'Magia Record',Gender:'Female',Rarity:'UR',RarityColor:'#d8b6ff',CardId:'HT-20261009-DAILYFIXTURE001',ImageSource:'zerochan',ArtworkPage:'https://www.zerochan.net/3085577',Orientation:'portrait',Metadata:{desc:'Local verification fixture'}};
(async()=>{
 fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;
 try{
  const page=await browser.newPage({viewport:{width:1100,height:900}}),errors=[],remote=[];page.on('pageerror',error=>errors.push(error.message));await page.route(/^https?:\/\//,route=>{remote.push(route.request().url());return route.abort();});
  await page.addInitScript(({image,rates,card})=>{
   let listener,session,cards=[{Card:card,FileName:'UR_Alina.hycard',ImagePath:image,CanDissolve:true,CanUpgrade:false,SalvageValue:160,Favorite:false}];
   const wallet={Gems:9000,FreeCredits:5,PaidCredits:0,LimitedCredits:2,LimitedPackPrice:180,PackPrice:160,MaxFreeCredits:5,SalvageValues:{Common:1,Uncommon:3,Rare:8,SR:20,SSR:60,UR:160}};
   const missions=[...['Genshin Impact','Terraria','Cyberpunk 2077'].map((Title,index)=>({Id:'game:test'+index,Title,Kind:'game',Progress:0,Target:1,Reward:60,Complete:false})),{Id:'movie20',Title:'Movie night',Kind:'movie',Progress:420,Target:1200,Reward:30,Complete:false}];
   const banners=[{Id:'20261009-1',Title:'Magia Record',Series:'Magia Record',ImageData:image,ExpiresAt:'2026-10-10T00:00:00+07:00'},{Id:'20261009-2',Title:'Naruto Shippuden',Series:'Naruto',ImageData:image,ExpiresAt:'2026-10-10T00:00:00+07:00'}];
   const snapshot=()=>({...wallet,ServerNow:new Date().toISOString(),NextFreeAt:new Date(Date.now()+1200000).toISOString()});
   const reply=(msg,delay=5)=>setTimeout(()=>listener({data:structuredClone({session,...msg,wallet:snapshot()})}),delay);
   window.dailyQa={messages:[],reply,profileActive:0,profilePeak:0};window.chrome=window.chrome||{};
   chrome.webview={addEventListener(type,handler){if(type==='message')listener=handler;},postMessage(msg){
    dailyQa.messages.push(msg);session=msg.session||session;
    if(msg.type==='hytcgInit'){reply({type:'state',state:{Settings:{Language:'en',ThemeMode:'dark',AccentColor:'#b995ef'}}});reply({type:'hytcgConfig',config:{Concurrency:2,DropRates:rates},missions,missionDay:'2026-10-09'});}
    if(msg.type==='hytcgDiscoveryState')reply({...msg,banners,missions,missionDay:'2026-10-09'});
    if(msg.type==='hytcgCollection')reply({...msg,page:{Items:cards,Total:cards.length,HasMore:false}});
    if(msg.type==='hytcgWallet')reply(msg);
    if(msg.type==='hytcgLimitedBuy'){wallet.Gems-=180*msg.count;wallet.LimitedCredits+=msg.count;reply(msg);}
    if(msg.type==='hytcgReserve'){if(msg.payment==='limited')wallet.LimitedCredits-=msg.count;else if(msg.payment==='limitedGems')wallet.Gems-=180*msg.count;else wallet.FreeCredits-=msg.count;reply({type:'hytcgReserved',requestId:msg.requestId,tokens:Array.from({length:msg.count},(_,i)=>'card-'+i),reservationId:'fixture'});}
    if(msg.type==='hytcgOpen')reply({type:'hytcgCard',requestId:msg.requestId,card,imageData:image},800);
    if(msg.type==='hytcgSave')reply({type:'hytcgSaved',cardId:msg.cardId,path:'fixture.hycard'});
    if(msg.type==='hytcgReadCard')reply({...msg,imageData:image},40);
    if(msg.type==='hytcgCharacterInfo'){const value=cards.find(item=>item.Card.CardId===msg.cardId);reply({...msg,info:{Matched:false},salvageValue:value?.SalvageValue||0});}
    if(msg.type==='hytcgDissolvePreview')reply({...msg,values:Object.fromEntries(msg.cardIds.map(id=>[id,cards.find(item=>item.Card.CardId===id)?.SalvageValue||0]))});
    if(msg.type==='hytcgDexSearch')reply({...msg,characters:msg.mode==='series'?[]:[card,{...card,CharacterId:2,Name:'Hinata Hyuuga',Series:'Naruto'},{...card,CharacterId:3,Name:'Naruto Uzumaki',Series:'Naruto'}],series:msg.mode==='series'?[{Id:2,Name:'Naruto'}]:[],hasMore:false});
    if(msg.type==='hytcgDexSeries')reply({...msg,characters:[{...card,CharacterId:2,Name:'Hinata Hyuuga',Series:'Naruto'}],series:[],hasMore:false});
    if(msg.type==='hytcgDexArtwork')reply({...msg,character:card,artworks:Array.from({length:4},(_,i)=>({Id:100+(msg.page||1)*10+i,PageUrl:'https://www.zerochan.net/'+(100+i)})),page:msg.page||1,hasMore:!msg.page||msg.page===1});
    if(msg.type==='hytcgDexProfile'||msg.type==='hytcgDexImage'){dailyQa.profileActive++;dailyQa.profilePeak=Math.max(dailyQa.profilePeak,dailyQa.profileActive);setTimeout(()=>{dailyQa.profileActive--;reply({...msg,imageData:image});},60);}
    if(msg.type==='hytcgMissionPlay'){const mission=missions.find(m=>m.Id==='game:'+msg.gameId);mission.Complete=true;mission.Progress=1;wallet.Gems+=60;reply({...msg,missions,missionDay:'2026-10-09'});}
    if(msg.type==='hytcgMissionMovie')reply(msg);
    if(msg.type==='hytcgAccountDelete'){wallet.Gems=1800;wallet.FreeCredits=5;wallet.LimitedCredits=0;cards=[];reply({...msg,missions,missionDay:'2026-10-09'});}
   }};
  },{image,rates,card});
  await page.goto('file:///'+path.join(root,'ui','hytcg.html').replaceAll('\\','/'));await page.waitForFunction(()=>tcg.banners.length===2&&tcg.wallet&&tcg.collection.length===1);
  assert.equal(await page.locator('.limitedBanner').count(),2);checks++;
  for(const [width,height,theme]of[[1100,900,'dark'],[520,760,'dark'],[375,650,'dark'],[760,760,'light']]){
   await page.setViewportSize({width,height});await page.evaluate(theme=>bindTheme({ThemeMode:theme,Language:'en',AccentColor:'#b995ef'}),theme);
   await page.screenshot({path:path.join(out,`booster-${width}-${theme}.png`)});assert.equal(await page.locator('#tcgScroll').evaluate(el=>el.scrollWidth>el.clientWidth),false);assert.equal(await page.locator('.tcgTabs').evaluate(el=>el.scrollWidth>el.clientWidth),false);checks+=2;
  }
  await page.setViewportSize({width:1100,height:900});await page.evaluate(()=>bindTheme({ThemeMode:'dark',Language:'en',AccentColor:'#b995ef'}));
  await page.click('#buyLimited');await page.waitForFunction(()=>tcg.wallet.LimitedCredits===3);assert.equal(await page.textContent('#gemBalance'),'8,820');checks++;
  await page.click('#limitedCredits');await page.click('[data-banner="0"][data-count="1"]');await page.waitForFunction(()=>tcg.slots[0]?.status==='loading');assert.equal(await page.locator('.pendingCard .openingSpinner').count(),1);assert.equal((await page.locator('.pendingCard .phase').allTextContents()).join(''),'');assert.equal(await page.isDisabled('[data-banner="1"][data-count="1"]'),true);checks+=3;
  await page.waitForFunction(()=>!tcg.busy&&tcg.slots[0]?.saved);const limited=await page.evaluate(()=>dailyQa.messages.find(m=>m.type==='hytcgReserve'));assert.equal(limited.bannerId,'20261009-1');assert.equal(limited.payment,'limited');assert.equal(await page.textContent('#limitedBalance'),'2');checks+=3;
  await page.click('#dexTab');await page.fill('#dexSearch','Alina');await page.click('#dexSubmit');await page.waitForFunction(()=>tcg.dex.characters.length===3&&!tcg.dex.pending);await page.waitForFunction(()=>document.querySelectorAll('.dexPortrait img').length===3);assert.equal(await page.evaluate(()=>dailyQa.profilePeak),2);checks++;
  await page.click('[data-dex-character="0"]');await page.waitForFunction(()=>tcg.artworks.length===4);await page.waitForFunction(()=>document.querySelectorAll('.dexArtwork img').length===4);await page.click('#dexArtworkMore');await page.waitForFunction(()=>tcg.artworks.length===8);assert.equal(await page.isVisible('#dexArtworkMore'),false);checks++;
  await page.click('[data-dex-artwork="0"]');await page.waitForFunction(()=>document.querySelector('#artworkPreview img'));assert.equal(await page.evaluate(()=>dailyQa.messages.some(m=>m.type==='hytcgDexImage'&&m.thumbnail===false)),true);checks++;
  await page.screenshot({path:path.join(out,'dex-artwork-detail.png')});await page.click('[data-close="artworkPreview"]');await page.click('[data-close="dexDialog"]');
  await page.selectOption('#dexMode','series');await page.fill('#dexSearch','Naruto');await page.click('#dexSubmit');await page.waitForFunction(()=>tcg.dex.series.length===1);await page.click('[data-dex-series="2"]');await page.waitForFunction(()=>tcg.dex.characters.length===1&&tcg.dex.seriesId===2);assert.equal(await page.textContent('.dexCharacter strong'),'Hinata Hyuuga');checks++;
  await page.click('#missionsTab');await page.click('[data-mission-play="test0"]');await page.waitForFunction(()=>tcg.missions[0].Complete);assert.match(await page.textContent('#missionSummary'),/1 \/ 4/);assert.equal(await page.locator('[data-mission-play="test0"]').count(),0);checks+=2;
  await page.click('[data-mission-movie]');await page.waitForFunction(()=>dailyQa.messages.some(m=>m.type==='hytcgMissionMovie'));checks++;
  for(const width of[375,520,1100]){await page.setViewportSize({width,height:760});await page.screenshot({path:path.join(out,`missions-${width}.png`)});assert.equal(await page.locator('#tcgScroll').evaluate(el=>el.scrollWidth>el.clientWidth),false);checks++;}
  await page.setViewportSize({width:1100,height:900});await page.click('#collectionTab');await page.waitForFunction(()=>tcg.collection.length===1);await page.click('#collectionGrid [data-detail="0"]');await page.waitForFunction(()=>dailyQa.messages.some(m=>m.type==='hytcgReadCard'));checks++;
  await page.click('[data-close="cardDialog"]');await page.click('#settingsButton');await page.click('#resetAccount');assert.equal(await page.isDisabled('#confirmDelete'),true);await page.fill('#deleteConfirmation','Delete');assert.equal(await page.isDisabled('#confirmDelete'),true);assert.equal(await page.evaluate(()=>dailyQa.messages.filter(m=>m.type==='hytcgAccountDelete').length),0);checks+=3;
  await page.fill('#deleteConfirmation','delete');assert.equal(await page.isDisabled('#confirmDelete'),false);await page.click('#confirmDelete');await page.waitForFunction(()=>!tcg.deleting&&tcg.wallet.Gems===1800&&tcg.collection.length===0);assert.equal(await page.textContent('#collectionCount'),'0');assert.equal(await page.locator('#results .cardVisual').count(),0);assert.equal(await page.evaluate(()=>dailyQa.messages.find(m=>m.type==='hytcgAccountDelete').confirmation),'delete');checks+=4;
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);checks+=2;fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({status:'PASS',checks},null,2));console.log(`PASS: ${checks} daily UI checks · banners, limited credits, spinner, Dex/gallery/native images, missions, encrypted detail and exact delete confirmation.`);
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exit(1);});
