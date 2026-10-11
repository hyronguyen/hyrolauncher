const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require('./test-runtime.cjs').playwright();
(async()=>{
 const rates=[['Common',65,'#aebccc'],['Uncommon',22,'#6fd6b0'],['Rare',7.3,'#7ab9f9'],['SR',4,'#bf96ef'],['SSR',1.1,'#f5c879'],['UR',.6,'#f6c6ff']].map(([Name,Weight,Color])=>({Name,Weight,Color}));
 const config={DropRates:rates,Concurrency:2,MaxCharacterAttempts:15};
 const image=fs.existsSync('output/verification/hytcg-live/artwork-data.txt')?fs.readFileSync('output/verification/hytcg-live/artwork-data.txt','utf8'):'data:image/png;base64,'+fs.readFileSync('assets/default-logo.png').toString('base64');
 const out=path.resolve('output/verification/hytcg-ui-upgrade');fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:480,height:730}}),errors=[];let network=0;
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('https://**/*',route=>{network++;return route.abort();});
  await page.addInitScript(({config,image})=>{
   let listener,session,serial=0;const cards=new Map(),saved=[],tokens=new Map();
   const wallet={FreeCredits:5,PaidCredits:10,Gems:159,MaxFreeCredits:5,IntervalSeconds:1200,PackPrice:160,SalvageValues:{Common:1,Uncommon:3,Rare:8,SR:20,SSR:60,UR:160}};
   const snapshot=()=>({...wallet,ServerNow:new Date().toISOString(),NextFreeAt:new Date(Date.now()+1200000).toISOString()});
   const reply=(data)=>setTimeout(()=>listener({data:{session,...data,wallet:snapshot()}}),5);
   window.qa={messages:[],wallet,tokens,saved,cards,active:0,peak:0,opens:0,reserves:0,failRender:true,saveFailure:true};window.chrome=window.chrome||{};
   window.chrome.webview={addEventListener:(_,fn)=>listener=fn,postMessage:msg=>{
    qa.messages.push(msg);session=msg.session;
    if(msg.type==='ready')reply({type:'state',state:{Settings:{Language:'vi',ThemeMode:'dark',AccentColor:'#bc95ee'}}});
    if(msg.type==='hytcgInit')reply({type:'hytcgConfig',config,cardsDirectory:'cards',recoveries:[]});
    if(msg.type==='hytcgWallet')reply({type:'hytcgWallet',requestId:msg.requestId});
    if(msg.type==='hytcgReserve'){
     qa.reserves++;if(msg.payment==='gems'&&wallet.Gems<160*msg.count){reply({type:'hytcgReserved',requestId:msg.requestId,error:'InsufficientGems'});return;}if(msg.payment!=='gems'&&wallet.FreeCredits+wallet.PaidCredits<msg.count){reply({type:'hytcgReserved',requestId:msg.requestId,error:'NoCredits'});return;}
     const reserved=Array.from({length:msg.count},()=>{const token='token-'+(serial++);const source=msg.payment==='gems'?'Gems':wallet.FreeCredits>0?'FreeCredits':'PaidCredits';wallet[source]-=source==='Gems'?160:1;tokens.set(token,{source,issued:false,saved:false});return token;});reply({type:'hytcgReserved',requestId:msg.requestId,reservationId:'reservation-'+serial,tokens:reserved});
    }
    if(msg.type==='hytcgOpen'){
     qa.active++;qa.peak=Math.max(qa.peak,qa.active);qa.opens++;
     setTimeout(()=>{
      qa.active--;const token=tokens.get(msg.token),index=Number(msg.token.split('-').at(-1));
      if(index===2&&!token.failed){token.failed=true;wallet[token.source]++;reply({type:'hytcgCard',requestId:msg.requestId,error:'CharacterNotFound'});return;}
      let card=cards.get(msg.token);
      if(!card){const rate=config.DropRates[index%6];card={CharacterId:10000+index,Name:index===0?'Hinata Hyuuga':index===1?'A very long character name 日本語 テスト':'Character '+index,Series:'Naruto',Gender:'Female',Rarity:rate.Name,RarityColor:rate.Color,ImageSource:index===1?'acdb':'zerochan',ArtworkPage:index===1?'':'https://www.zerochan.net/3085577',CardId:'HT-20261008-'+String(index).padStart(12,'0'),Metadata:{desc:'Preview fixture card. These cards never enter your real collection.'}};cards.set(msg.token,card);}
      token.issued=true;reply({type:'hytcgCard',requestId:msg.requestId,card,imageData:index===4&&qa.failRender?'data:image/png;base64,INVALID':image});
     },130);
    }
    if(msg.type==='hytcgFallback')reply({type:'hytcgCard',requestId:msg.requestId,error:'ArtworkInvalid',recoverable:true});
    if(msg.type==='hytcgCancelReservation')reply({type:'hytcgWallet'});
    if(msg.type==='hytcgSave'){
     if(msg.cardId.endsWith('000000000005')&&qa.saveFailure){qa.saveFailure=false;reply({type:'hytcgSaved',cardId:msg.cardId,error:'SaveFailed'});return;}
     const card=[...cards.values()].find(c=>c.CardId===msg.cardId);if(!saved.some(item=>item.Card.CardId===msg.cardId))saved.push({Card:card,ImagePath:'',png:msg.png,CanDissolve:true,SalvageValue:wallet.SalvageValues[card.Rarity],IsLegacy:false});reply({type:'hytcgSaved',cardId:msg.cardId,path:'cards/'+msg.cardId+'.png'});
    }
    if(msg.type==='hytcgCollection'){
     const items=saved.filter(item=>(!msg.rarity||item.Card.Rarity===msg.rarity)&&(!msg.query||item.Card.Name.includes(msg.query)));reply({type:'hytcgCollection',requestId:msg.requestId,page:{Items:items.filter(i=>i.ImagePath),Total:items.length,HasMore:false}});
    }
    if(msg.type==='hytcgDissolveMany'){
     let gemsAwarded=0;for(const cardId of msg.cardIds){const index=saved.findIndex(item=>item.Card.CardId===cardId);if(index<0){reply({type:'hytcgDissolvedMany',requestId:msg.requestId,error:'CardAlreadyDissolved'});return;}gemsAwarded+=saved[index].SalvageValue;saved.splice(index,1);}wallet.Gems+=gemsAwarded;reply({type:'hytcgDissolvedMany',requestId:msg.requestId,cardIds:msg.cardIds,gemsAwarded});
    }
    if(msg.type==='hytcgBuyPull'){if(wallet.Gems<160){reply({type:'hytcgWallet',requestId:msg.requestId,error:'InsufficientGems'});return;}wallet.Gems-=160;wallet.PaidCredits++;reply({type:'hytcgWallet',requestId:msg.requestId});}
   }};
  },{config,image});
  await page.goto('file:///'+path.resolve('ui/hytcg.html').replaceAll('\\','/')+'?id=builtin.hytcg');await page.waitForFunction(()=>tcg.config&&tcg.wallet);
  assert.equal(await page.isDisabled('#buyPull'),true);await page.click('#settingsButton');assert.equal(await page.locator('#rateFields input').count(),0);assert.match(await page.textContent('#rateFields'),/65%/);await page.screenshot({path:path.join(out,'rules.png')});await page.click('[data-close="settingsDialog"]');
  await page.click('#openTen');await page.waitForFunction(()=>!tcg.busy,null,{timeout:20000});
  assert.equal(await page.evaluate(()=>qa.peak),2);assert.equal(await page.evaluate(()=>tcg.slots[4].issued),true);assert.equal(await page.evaluate(()=>tcg.slots[4].error),'ArtworkInvalid');assert.equal(await page.evaluate(()=>tcg.slots[5].saved),false);
  const before=await page.evaluate(()=>({reserves:qa.reserves,name:tcg.slots[4].cardNative.Name}));await page.evaluate(()=>qa.failRender=false);await page.click('[data-retry="4"]');await page.waitForFunction(()=>!tcg.busy);
  assert.equal(await page.evaluate(()=>qa.reserves),before.reserves);assert.equal(await page.evaluate(()=>tcg.slots[4].card.Name),before.name);await page.click('[data-save="5"]');await page.waitForFunction(()=>tcg.slots[5].saved);
  const visible=await page.textContent('#results');assert(!/ACDB|Zerochan|profile fallback/i.test(visible));
  await page.click('[data-detail="5"]');await page.waitForTimeout(450);
  assert.deepEqual(await page.evaluate(()=>[...document.querySelectorAll('#cardDetail [data-source]')].map(b=>b.textContent)),['ACDB ↗','ZeroChan ↗']);
  const box=await page.locator('.detailCard').boundingBox();await page.mouse.move(box.x+box.width*.8,box.y+box.height*.2);await page.waitForTimeout(100);
  const effect=await page.evaluate(()=>({transform:getComputedStyle(document.querySelector('.detailCard .cardSurface')).transform,shine:getComputedStyle(document.querySelector('.detailCard .cardShine')).opacity}));assert.match(effect.transform,/matrix3d/);assert(Number(effect.shine)>0);
  for(const [width,height,theme]of [[480,730,'dark'],[375,600,'dark'],[760,550,'dark'],[480,730,'light']]){
   await page.setViewportSize({width,height});await page.evaluate(theme=>bindTheme({ThemeMode:theme,Language:'vi',AccentColor:'#bc95ee'}),theme);await page.screenshot({path:path.join(out,`detail-${width}-${theme}.png`)});
   const overflow=await page.evaluate(()=>[document.documentElement.scrollWidth>innerWidth,document.getElementById('cardDialog').scrollWidth>document.getElementById('cardDialog').clientWidth]);assert.deepEqual(overflow,[false,false]);
  }
  await page.emulateMedia({reducedMotion:'reduce'});await page.mouse.move(0,0);const reducedBox=await page.locator('.detailCard').boundingBox();await page.mouse.move(reducedBox.x+reducedBox.width*.8,reducedBox.y+reducedBox.height*.2);await page.waitForTimeout(60);assert.match(await page.locator('.detailCard .cardSurface').evaluate(el=>getComputedStyle(el).transform),/matrix3d/);await page.click('#cardEffectsButton');assert.equal(await page.locator('.detailCard .cardSurface').evaluate(el=>getComputedStyle(el).transform),'none');await page.click('#cardEffectsButton');await page.emulateMedia({reducedMotion:'no-preference'});
  await page.click('#dissolveButton');assert.equal(await page.isVisible('#dissolveConfirm'),true);await page.click('#cancelDissolve');assert.equal(await page.isVisible('#dissolveConfirm'),false);await page.click('#dissolveButton');await page.click('#confirmDissolve');await page.waitForFunction(()=>!tcg.dissolving);
  assert.equal(await page.evaluate(()=>tcg.slots[5].status),'dissolved');assert.equal(await page.evaluate(()=>tcg.wallet.Gems),319);
  await page.click('[data-detail="1"]');assert.equal(await page.locator('#cardDetail .altLabel').count(),0);assert.equal(await page.locator('#cardDetail [data-source]').count(),1);await page.click('[data-close="cardDialog"]');
  await page.click('#buyPull');await page.waitForFunction(()=>!tcg.busy&&tcg.slots.length===1&&tcg.slots[0].saved);assert.equal(await page.evaluate(()=>tcg.wallet.Gems),159);assert.equal(await page.evaluate(()=>qa.messages.filter(msg=>msg.type==='hytcgReserve').at(-1).payment),'gems');assert.equal(await page.evaluate(()=>qa.messages.some(msg=>msg.type==='hytcgBuyPull')),false);
  for(const [width,height,theme]of [[480,730,'dark'],[375,600,'dark'],[760,550,'dark'],[480,730,'light']]){
   await page.setViewportSize({width,height});await page.evaluate(theme=>bindTheme({ThemeMode:theme,Language:'vi',AccentColor:'#bc95ee'}),theme);await page.screenshot({path:path.join(out,`booster-${width}-${theme}.png`)});assert.equal(await page.evaluate(()=>document.getElementById('tcgScroll').scrollWidth>document.getElementById('tcgScroll').clientWidth),false);
  }
  await page.evaluate(()=>{qa.wallet.FreeCredits=0;qa.wallet.PaidCredits=0;});await page.evaluate(()=>refreshWallet());await page.waitForFunction(()=>tcg.wallet.FreeCredits===0&&tcg.wallet.PaidCredits===0);assert.equal(await page.isDisabled('#openOne'),true);assert.equal(await page.isDisabled('#openTen'),true);
  fs.writeFileSync(path.join(out,'sample-alt.png'),Buffer.from((await page.evaluate(()=>tcg.slots[0].png)).split(',')[1],'base64'));
  const liveModel='output/verification/hytcg-live/live-card.json',liveImage='output/verification/hytcg-live/live-card-image.txt';
  if(fs.existsSync(liveModel)&&fs.existsSync(liveImage)){
   const card=JSON.parse(fs.readFileSync(liveModel,'utf8')),imageData=fs.readFileSync(liveImage,'utf8');
   const png=await page.evaluate(async({card,imageData})=>HyTcgRenderer.render(card,imageData),{card,imageData});
   fs.writeFileSync(path.join(out,'live-card.png'),Buffer.from(png.split(',')[1],'base64'));
   console.log(`PASS: ALT card renderer with real fetched character #${card.CharacterId}: ${card.Name}.`);
  }
  assert.equal(network,0);assert.deepEqual(errors,[]);console.log('PASS: native wallet authority, reserve/concurrency, authorized resume, retry save, buy/dissolve, ALT labels, 3D/holo, reduced motion, 375/480/760 responsive.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
