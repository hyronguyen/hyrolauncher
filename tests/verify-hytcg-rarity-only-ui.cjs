'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require('./test-runtime.cjs').playwright();
const root=testRuntime.projectRoot,out=path.join(testRuntime.verificationRoot,'rarity-only-ui');
const image='data:image/png;base64,'+fs.readFileSync(path.join(root,'assets/default-logo.png')).toString('base64');
const reusable=fs.readFileSync(path.join(__dirname,'verify-hytcg-adventure-ui.cjs'),'utf8');
const start=reusable.indexOf('  await page.addInitScript('),end=reusable.indexOf('  await page.goto(',start);
assert(start>0&&end>start,'Existing native UI fixture found');
let fixture=reusable.slice(start,end).replace('creative.RestorableLooks=[];creative.FrameItems=[];',`creative.RestorableLooks=[];creative.FrameItems=[{Id:'FI-legacy-unused',Name:'Retired frame',FrameId:'sakura',Available:true,Count:1}];creative.Cosmetics=[{CardId:roster[0].CardId,Frame:'royal',FrameItemId:'FI-legacy-applied',DyeId:palette[1].Id,Dye:palette[1].Color,Mystic:false,ArtworkId:'legacy-art',Morph:'#ff0000',MorphMode:'multiply'}];`);
(async()=>{
 fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;
 try{
  const page=await browser.newPage({viewport:{width:500,height:850}}),errors=[],requests=[],csp=[];
  page.on('pageerror',error=>errors.push(error.message));page.on('request',request=>{if(/^https?:/.test(request.url()))requests.push(request.url());});
  page.on('console',msg=>{if(msg.type()==='error'&&/Content Security Policy/.test(msg.text()))csp.push(msg.text());});
  await page.route(/^https?:\/\//,route=>route.abort());
  await new Function('page','image','previewImages','return (async()=>{'+fixture+'})();')(page,image,{});
  await page.goto('file:///'+path.join(root,'ui/hytcg.html').replaceAll('\\','/'));
  await page.waitForFunction(()=>window.HyTcgLifestyle&&tcg.config);
  const check=(ok,label)=>{assert(ok,label);checks++;};
  await page.click('[data-life-view="studio"]');await page.waitForSelector('.lifeRarityFrame');
  check((await page.locator('.lifeRarityFrame strong').textContent())==='SSR','Rarity frame is displayed even when old cosmetic had royal frame');
  check(await page.locator('.lifeRarityFrame button').count()===0,'Rarity frame is passive');
  check(await page.locator('[data-life-action="chooseFrame"],[data-life-action="createFrame"],[data-life-action="frameCreate"],.lifeStudioTabs,#lifeFrameStage').count()===0,'Creation and attachment controls removed');
  check(await page.locator('[data-life-action="chooseDye"]').count()===1,'Dye picker remains');
  check(await page.locator('.lifeMorph,.lifeFrameOverlay').count()===0,'No legacy whole-card dye or morph overlays');
  await page.waitForFunction(()=>lifeQa.messages.some(x=>x.type==='hytcgLifestyleImage'&&x.previewFrameId));
  check(await page.evaluate(()=>lifeQa.messages.filter(x=>x.type==='hytcgLifestyleImage'&&x.previewFrameId).every(x=>x.previewFrameId==='classic'&&!x.previewFrameItemId&&!x.previewFrameDraft)),'Previews do not request retired frame IDs/items/drafts');
  check(await page.evaluate(()=>lifeQa.messages.some(x=>x.previewArtworkId==='legacy-art'&&x.previewDyeId==='dye-01')),'Existing artwork and dye preserved in preview');
  check(await page.locator('[data-life-action="cosmeticSave"]').isDisabled(),'Legacy frame alone does not create paid dirty look');
  await page.click('[data-life-action="chooseDye"]');await page.waitForSelector('#lifestyleItemPicker[open]');
  check(await page.locator('#lifestyleItemPickerGrid [data-life-item-pick="FI-legacy-unused"],[data-life-classic]').count()===0,'Picker contains no frame choices');
  check(await page.locator('#lifestyleItemPickerGrid [data-life-item-pick="dye-02-mystic"]').count()===1,'Mystic dye available');
  await page.click('#lifestyleItemPickerGrid [data-life-item-pick="dye-02-mystic"]');await page.waitForFunction(()=>!document.querySelector('[data-life-action="cosmeticSave"]').disabled);
  const dyeBefore=await page.evaluate(()=>lifeQa.getDyeCount('dye-02-mystic'));
  await page.click('[data-life-action="cosmeticSave"]');await page.waitForFunction(()=>lifeQa.messages.some(x=>x.action==='cosmeticSave'));
  await page.waitForFunction(()=>document.querySelector('#lifestyleContent[aria-busy="false"]'));
  check(await page.evaluate(()=>lifeQa.messages.filter(x=>x.action==='cosmeticSave').at(-1).frame==='classic'&&lifeQa.messages.filter(x=>x.action==='cosmeticSave').at(-1).frameItemId===''),'Apply sends only automatic rarity frame');
  check(await page.evaluate(()=>lifeQa.messages.filter(x=>x.action==='cosmeticSave').at(-1).artworkId==='legacy-art'),'Dye preserves applied artwork');
  check(await page.evaluate(()=>lifeQa.getDyeCount('dye-02-mystic'))===dyeBefore-1,'Apply consumes one selected dye');
  check(await page.locator('#lifeStudioPreview.lifeMystic').count()===1,'Mystic visual retained');
  await page.click('[data-life-view="inventory"]');await page.waitForSelector('.lifeUnifiedInventory');
  check(await page.locator('[data-life-inventory-filter="frame"],[data-life-item="FI-legacy-unused"],[data-life-action="createFrame"]').count()===0,'No retired frame inventory or creation entry');
  check(await page.locator('.lifeInventoryTile').filter({hasText:'Retired frame'}).count()===0,'Legacy frame item hidden');
  check(await page.locator('.lifeInventoryTile').filter({hasText:'Fiber'}).count()===1,'Material remains in inventory');
  check(await page.locator('.lifeInventoryTile').filter({hasText:'Fiber'}).evaluate(n=>n.tagName==='DIV'&&!n.hasAttribute('data-life-item')),'Material is passive rather than opening retired editor');
  check(await page.locator('[data-life-item="training"]').count()===1,'Gameplay item remains usable');
  await page.click('[data-life-action="openStudio"]');await page.waitForSelector('.artworkModes');
  await page.click('[data-adventure-action="artMode"][data-mode="url"]');await page.fill('#lifeArtworkUrl','https://example.invalid/art.png');await page.click('[data-life-action="artworkUrl"]');
  await page.waitForFunction(()=>lifeQa.messages.some(x=>x.action==='artworkUrl'));check(true,'URL import remains available');
  await page.waitForFunction(()=>!document.querySelector('[data-life-action="cosmeticSave"]').disabled);
  await page.click('[data-life-action="cosmeticSave"]');await page.waitForFunction(()=>lifeQa.messages.filter(x=>x.action==='cosmeticSave').length===2);
  await page.waitForFunction(()=>document.querySelector('#lifestyleContent[aria-busy="false"]'));
  check(await page.evaluate(()=>lifeQa.messages.filter(x=>x.action==='cosmeticSave').at(-1).artworkId==='art-fixture'&&lifeQa.messages.filter(x=>x.action==='cosmeticSave').at(-1).dyeId==='dye-02-mystic'),'Artwork apply keeps selected dye');
  await page.click('[data-adventure-action="artMode"][data-mode="sketch"]');const canvas=await page.locator('#lifeCanvas').boundingBox();
  await page.mouse.move(canvas.x+35,canvas.y+35);await page.mouse.down();await page.mouse.move(canvas.x+65,canvas.y+60,{steps:4});await page.mouse.up();
  check(await page.locator('#lifeCanvas').evaluate(c=>c.getContext('2d').getImageData(0,0,c.width,c.height).data.some((x,i)=>i%4===3&&x>0)),'Sketch paints alpha pixels');
  await page.click('[data-life-action="artworkDraw"]');await page.waitForFunction(()=>lifeQa.messages.some(x=>x.action==='artworkDraw'));check(true,'Drawing upload remains available');
  await page.evaluate(()=>HyTcgLifestyle.showStudio('HT-FIXTURE-99'));await page.waitForFunction(()=>document.querySelector('.lifeCharacterButton strong')?.textContent==='Character 099');
  check(await page.locator('#lifeStudioPreview.landscape').count()===0&&await page.locator('#lifeStudioPreview').evaluate(n=>getComputedStyle(n).aspectRatio==='5 / 7'),'Older horizontal card metadata uses portrait preview');
  check((await page.locator('.lifeRarityFrame strong').textContent())==='Rare','Frame label follows selected card rarity');
  for(const width of[375,500,1100])for(const language of['en','vi']){
   await page.setViewportSize({width,height:900});await page.evaluate(language=>{tcg.language=language;translate();},language);
   for(const view of['studio','inventory']){
    await page.click('[data-life-view="'+view+'"]');await page.waitForTimeout(40);
    check(await page.locator('#lifestyleContent').evaluate(n=>n.scrollWidth<=n.clientWidth+2),'No overflow '+view+'/'+width+'/'+language);
    check(await page.locator('[data-life-action="createFrame"],[data-life-action="chooseFrame"],[data-life-inventory-filter="frame"],#lifeFrameStage').count()===0,'Retired controls absent '+view+'/'+width+'/'+language);
   }
  }
  await page.setViewportSize({width:1100,height:900});await page.evaluate(()=>HyTcgLifestyle.showStudio('HT-FIXTURE-0'));await page.waitForSelector('.lifeRarityFrame');
  await page.screenshot({path:path.join(out,'studio-rarity-1100.png')});
  await page.click('[data-life-view="inventory"]');await page.screenshot({path:path.join(out,'inventory-1100.png')});
  check(await page.evaluate(()=>!lifeQa.messages.some(x=>x.action==='frameCreate'||x.action==='frameBuy'||x.frameItemId||x.previewFrameItemId||x.previewFrameDraft)),'No frame action or legacy attachment sent');
  check(errors.length===0,'No JavaScript errors: '+errors.join('; '));check(csp.length===0,'No CSP errors: '+csp.join('; '));check(requests.length===0,'No external network calls');
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({Result:'PASS',Checks:checks,Errors:errors,CspErrors:csp,NetworkRequests:requests,ProductionDataAccessed:false},null,2));
  console.log('PASS rarity-only frame UI '+checks+' checks');
 }finally{await browser.close();}
})().catch(error=>{console.error(error.stack);process.exitCode=1;});
