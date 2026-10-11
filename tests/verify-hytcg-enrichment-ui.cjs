'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require('./test-runtime.cjs').playwright();
const root=testRuntime.projectRoot,out=path.join(root,'output','verification','hytcg-enrichment-ui');
const image='data:image/png;base64,'+fs.readFileSync(path.join(root,'assets','default-logo.png')).toString('base64');
const rates=[['Common',65,'#aebccc'],['Uncommon',22,'#6fd6b0'],['Rare',7.3,'#7ab9f9'],['SR',4,'#bf96ef'],['SSR',1.1,'#f5c879'],['UR',.6,'#f6c6ff']].map(([Name,Weight,Color])=>({Name,Weight,Color}));
const url='file:///'+path.join(root,'ui','hytcg.html').replaceAll('\\','/');
(async()=>{
 fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;
 try{
  const page=await browser.newPage({viewport:{width:1100,height:900}}),errors=[],network=[];
  page.on('pageerror',error=>errors.push(error.message));await page.route(/^https?:\/\//,route=>{network.push(route.request().url());return route.abort();});
  await page.addInitScript(({image,rates})=>{
   let listener,session,serial=200,nextToken=0;const values={Common:1,Uncommon:3,Rare:8,SR:20,SSR:60,UR:160};
   const wallet={Gems:9000,FreeCredits:5,PaidCredits:20,LimitedCredits:0,LimitedPackPrice:180,PackPrice:160,MaxFreeCredits:5,SalvageValues:values};
   const card=(index,rarity,name)=>({CharacterId:99991+index,Name:name,Series:'Magia Record',Gender:'Female',Rarity:rarity,RarityColor:rates.find(r=>r.Name===rarity).Color,CardId:'HT-20261010-'+String(index).padStart(12,'0'),CreatedAt:'2026-10-10T00:00:00Z',ImageSource:'zerochan',ArtworkPage:'https://www.zerochan.net/3085577',Orientation:'portrait',Metadata:{desc:'ACDB fixture description'}});
   const cards=new Map();for(const [index,rarity,name]of[[1,'SR','Alina Gray'],[2,'Rare','Unmatched character'],[3,'Common','Second page character'],[4,'Common','Third page character']]){const Card=card(index,rarity,name);cards.set(Card.CardId,{Card,Thumbnail:image,Encrypted:true,FileName:Card.CardId+'.hycard',CanDissolve:true,CanUpgrade:false,SalvageValue:values[rarity],Favorite:index===1});}
   const snapshot=()=>({...wallet,ServerNow:new Date().toISOString(),NextFreeAt:new Date(Date.now()+1200000).toISOString()});
   const reply=(msg,delay=5)=>setTimeout(()=>listener({data:structuredClone({session,...msg,wallet:snapshot()})}),delay);
   window.qa={messages:[],reply,cards,wallet,holdInfo:true,infoRequests:[],holdPreview:true,previewRequests:[],paged:false,tokens:new Map(),active:0,peak:0};
   qa.infoReply=(index,info,salvageValue)=>{const msg=qa.infoRequests.splice(index,1)[0];if(!msg)throw Error('missing info request');reply({...msg,info,salvageValue});};
   qa.previewReply=values=>{const msg=qa.previewRequests.shift();if(!msg)throw Error('missing preview request');reply({...msg,values});};
   qa.previewError=error=>{const msg=qa.previewRequests.shift();if(!msg)throw Error('missing preview request');reply({...msg,error});};
   window.chrome=window.chrome||{};chrome.webview={addEventListener(type,handler){if(type==='message')listener=handler;},postMessage(msg){
    qa.messages.push(msg);session=msg.session||session;
    if(msg.type==='hytcgInit'){reply({type:'state',state:{Settings:{Language:'en',ThemeMode:'dark',AccentColor:'#bc95ee'}}});reply({type:'hytcgConfig',config:{DropRates:rates,Concurrency:2}});}
    if(msg.type==='hytcgWallet')reply(msg);
    if(msg.type==='hytcgDiscoveryState')reply({...msg,banners:[],missions:[],missionDay:'2026-10-10'});
    if(msg.type==='hytcgCollection'){
     const all=[...cards.values()];let Items,NextOffset,Total,HasMore;
     if(qa.paged){Items=msg.offset===0?all.slice(0,2):msg.offset===40?all.slice(2,3):all.slice(3,4);NextOffset=msg.offset===0?40:msg.offset===40?80:82;Total=82;HasMore=msg.offset<80;}
     else{Items=all.slice(0,2);NextOffset=Items.length;Total=Items.length;HasMore=false;}
     reply({...msg,page:{Items,NextOffset,Total,HasMore}});
    }
    if(msg.type==='hytcgReadCard')reply({...msg,imageData:image},35);
    if(msg.type==='hytcgCharacterInfo'){if(qa.holdInfo)qa.infoRequests.push(msg);else reply({...msg,info:{Matched:false},salvageValue:cards.get(msg.cardId)?.SalvageValue||0});}
    if(msg.type==='hytcgDissolvePreview'){if(qa.holdPreview)qa.previewRequests.push(msg);else reply({...msg,values:Object.fromEntries(msg.cardIds.map(id=>[id,cards.get(id)?.SalvageValue||0]))});}
    if(msg.type==='hytcgDissolveMany'){const gems=msg.cardIds.reduce((sum,id)=>sum+(qa.nativeValues?.[id]??cards.get(id)?.SalvageValue??0),0);for(const id of msg.cardIds)cards.delete(id);wallet.Gems+=gems;reply({type:'hytcgDissolvedMany',requestId:msg.requestId,cardIds:msg.cardIds,gemsAwarded:gems});}
    if(msg.type==='hytcgReserve'){const tokens=Array.from({length:msg.count},()=>{const token='fixture-token-'+nextToken++;qa.tokens.set(token,null);return token;});if(msg.payment==='gems')wallet.Gems-=160*msg.count;else for(let i=0;i<msg.count;i++){if(wallet.FreeCredits)wallet.FreeCredits--;else wallet.PaidCredits--;}reply({type:'hytcgReserved',requestId:msg.requestId,tokens,reservationId:'fixture-reservation'});}
    if(msg.type==='hytcgOpen'){
     qa.active++;qa.peak=Math.max(qa.peak,qa.active);const index=Number(msg.token.split('-').at(-1));
     setTimeout(()=>{qa.active--;if(index===0){wallet.Gems+=10;reply({type:'hytcgCard',requestId:msg.requestId,compensated:true,gemsAwarded:10});}
      else{const opened=card(++serial,'Common','Opened '+serial);qa.tokens.set(msg.token,opened);reply({type:'hytcgCard',requestId:msg.requestId,card:opened,imageData:image});}},55);
    }
    if(msg.type==='hytcgSave')reply({type:'hytcgSaved',cardId:msg.cardId,path:'fixture.hycard'});
   }};
  },{image,rates});
  await page.goto(url);await page.waitForFunction(()=>tcg.config&&tcg.wallet&&tcg.collection.length===2);await page.click('#collectionTab');await page.waitForFunction(()=>tcg.collection.length===2);
  await page.click('#collectionGrid [data-detail="0"]');await page.waitForFunction(()=>qa.infoRequests.length===1);
  assert.equal(await page.isDisabled('#dissolveButton'),true);assert.equal(await page.locator('.characterEnrichment .openingSpinner').count(),1);checks+=2;
  const injected='<img src=x onerror="window.injectionExecuted=true"> & <script>bad()</script>';
  await page.evaluate(injected=>qa.infoReply(0,{Matched:true,AniListId:12345,Favourites:12345,Age:'17 < 18',Gender:'Female',Description:injected},143),injected);
  await page.waitForFunction(()=>tcg.detail?.infoLoading===false&&tcg.detail.value===143);
  assert.equal(await page.textContent('.characterDescription'),injected);assert.equal(await page.locator('.characterEnrichment img,.characterEnrichment script').count(),0);assert.equal(await page.evaluate(()=>window.injectionExecuted),undefined);checks+=3;
  assert.equal(await page.textContent('.characterFavourites'),'♥ 12,345');assert.equal(await page.locator('.detailDescription').count(),0);assert.equal(await page.isDisabled('#dissolveButton'),false);assert.equal(await page.textContent('#dissolveButton b'),'◆ 143');assert.equal(await page.evaluate(()=>tcg.collection[0].SalvageValue),143);checks+=5;
  await page.click('[data-anilist="12345"]');assert.equal(await page.evaluate(()=>qa.messages.find(m=>m.type==='openExternal')?.target),'https://anilist.co/character/12345');checks++;
  await page.click('#dissolveButton');assert.match(await page.textContent('#dissolveConfirm'),/143 gems/);await page.click('#cancelDissolve');checks++;
  await page.screenshot({path:path.join(out,'character-detail-enriched.png')});
  await page.click('[data-close="cardDialog"]');await page.click('#collectionGrid [data-detail="0"]');await page.waitForFunction(()=>qa.infoRequests.length===1);await page.click('[data-close="cardDialog"]');await page.click('#collectionGrid [data-detail="1"]');await page.waitForFunction(()=>qa.infoRequests.length===2);
  await page.evaluate(()=>qa.infoReply(1,{Matched:false},8));await page.waitForFunction(()=>tcg.detail?.card.Name==='Unmatched character'&&!tcg.detail.infoLoading);
  await page.evaluate(()=>qa.infoReply(0,{Matched:true,AniListId:12345,Favourites:900000,Description:'STALE CHARACTER'},9020));await page.waitForTimeout(50);
  assert.equal(await page.textContent('.detailInfo h2'),'Unmatched character');assert.equal(await page.locator('.characterEnrichment').count(),0);assert.equal(await page.textContent('#dissolveButton b'),'◆ 8');assert.equal(await page.evaluate(()=>tcg.detail.value),8);checks+=4;
  assert.equal(await page.textContent('.detailDescription'),'ACDB fixture description');checks++;
  await page.click('[data-close="cardDialog"]');await page.evaluate(()=>{qa.paged=true;loadCollection(false);});await page.waitForFunction(()=>tcg.collection.length===2&&tcg.collectionNextOffset===40);
  await page.click('#collectionMore');await page.waitForFunction(()=>tcg.collection.length===3&&tcg.collectionNextOffset===80);assert.equal(await page.evaluate(()=>qa.messages.filter(m=>m.type==='hytcgCollection').at(-1).offset),40);checks++;
  await page.click('#collectionMore');await page.waitForFunction(()=>tcg.collection.length===4&&!tcg.hasMore);assert.equal(await page.evaluate(()=>qa.messages.filter(m=>m.type==='hytcgCollection').at(-1).offset),80);assert.equal(await page.isVisible('#collectionMore'),false);checks+=2;
  await page.evaluate(()=>{qa.paged=false;loadCollection(false);});await page.waitForFunction(()=>tcg.collection.length===2&&!tcg.hasMore);
  const previewBaseline=await page.evaluate(()=>tcg.collection[0].SalvageValue);
  for(const outcome of['success','error']){
   await page.click('#collectionGrid [data-select="0"]');await page.click('#dissolveSelected');await page.waitForFunction(()=>qa.previewRequests.length===1);
   await page.click('#batchCancel');await page.waitForFunction(()=>!tcg.previewBusy&&!document.getElementById('batchDialog').open);assert.equal(await page.evaluate(()=>tcg.previewBusy),false);checks++;
   await page.click('#clearSelection');await page.click('#collectionGrid [data-select="1"]');await page.click('#dissolveSelected');await page.waitForFunction(()=>qa.previewRequests.length===2);
   await page.evaluate(outcome=>{if(outcome==='success')qa.previewReply({[tcg.collection[0].Card.CardId]:99999});else qa.previewError('CardTampered');},outcome);
   await page.waitForFunction(()=>qa.previewRequests.length===1);await page.waitForTimeout(35);
   assert.equal(await page.evaluate(()=>tcg.previewBusy),true);assert.equal(await page.isDisabled('#confirmBatchDissolve'),true);assert.equal(await page.locator('#batchGemValue .openingSpinner').count(),1);assert.equal(await page.textContent('#batchError'),'');assert.equal(await page.evaluate(()=>tcg.collection[0].SalvageValue),previewBaseline);checks+=5;
   await page.evaluate(()=>qa.previewReply({[tcg.collection[1].Card.CardId]:21}));await page.waitForFunction(()=>!tcg.previewBusy);
   assert.equal(await page.textContent('#batchGemValue'),'◆ +21');assert.equal(await page.isDisabled('#confirmBatchDissolve'),false);checks+=2;
   await page.click('#batchCancel');await page.waitForFunction(()=>!document.getElementById('batchDialog').open);await page.click('#clearSelection');
  }
  await page.click('#selectVisible');await page.click('#dissolveSelected');await page.waitForFunction(()=>qa.previewRequests.length===1);
  assert.equal(await page.isDisabled('#confirmBatchDissolve'),true);assert.equal(await page.locator('#batchGemValue .openingSpinner').count(),1);checks+=2;
  await page.evaluate(()=>dissolveMany());assert.equal(await page.evaluate(()=>qa.messages.filter(m=>m.type==='hytcgDissolveMany').length),0);checks++;
  await page.evaluate(()=>{qa.nativeValues=Object.fromEntries([...tcg.selected.keys()].map((id,index)=>[id,index?21:385]));qa.previewReply(qa.nativeValues);});await page.waitForFunction(()=>!tcg.previewBusy);
  assert.equal(await page.textContent('#batchGemValue'),'◆ +406');assert.equal(await page.isDisabled('#confirmBatchDissolve'),false);assert.equal(await page.evaluate(()=>[...tcg.selected.values()].map(item=>item.SalvageValue).join(',')),'385,21');checks+=3;
  await page.click('#confirmBatchDissolve');await page.waitForFunction(()=>!tcg.dissolving&&tcg.selected.size===0);assert.equal(await page.evaluate(()=>tcg.wallet.Gems),9406);assert.match(await page.textContent('#toast'),/\+406 gems/);checks+=2;
  await page.click('#boosterTab');await page.click('#payGems');await page.click('#openTen');await page.waitForFunction(()=>!tcg.busy&&tcg.slots.length===10&&tcg.slots.every(slot=>slot.saved),null,{timeout:30000});
  assert.equal(await page.evaluate(()=>tcg.slots[0].status),'compensated');assert.equal(await page.evaluate(()=>tcg.slots[0].issued),false);assert.equal(await page.locator('#results .compensatedCard').count(),1);assert.match(await page.textContent('#results .compensatedCard'),/\+10 gems/);checks+=4;
  assert.equal(await page.locator('#results .compensatedCard [data-retry],#results .compensatedCard [data-save]').count(),0);assert.equal(await page.evaluate(()=>qa.messages.filter(m=>m.type==='hytcgSave').length),9);assert.equal(await page.evaluate(()=>qa.messages.filter(m=>m.type==='hytcgOpen').length),10);assert.equal(await page.evaluate(()=>qa.peak),2);assert.equal(await page.evaluate(()=>tcg.wallet.Gems),7816);checks+=5;
  await page.screenshot({path:path.join(out,'pack-compensation.png')});
  for(const width of[375,520,1100]){await page.setViewportSize({width,height:760});assert.equal(await page.locator('#tcgScroll').evaluate(el=>el.scrollWidth>el.clientWidth),false);checks++;}
  assert.deepEqual(errors,[]);assert.deepEqual(network,[]);checks+=2;
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({status:'PASS',checks},null,2));console.log(`PASS: ${checks} enrichment UI checks · native reward preview, exact info values, escaped descriptions, stale detail isolation, loading guards, metadata offsets and compensated card slots with x10 continuation.`);
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
