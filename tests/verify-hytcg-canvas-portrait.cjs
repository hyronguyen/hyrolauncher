'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {pathToFileURL}=require('node:url');
const {chromium}=require('./test-runtime.cjs').playwright();
const root=testRuntime.projectRoot,out=path.join(testRuntime.verificationRoot,'canvas-portrait');
(async()=>{
 fs.mkdirSync(out,{recursive:true});let checks=0;
 const check=(okay,label)=>{assert(okay,label);checks++;};
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1200,height:900},reducedMotion:'reduce'}),errors=[],external=[],csp=[];
  page.on('pageerror',error=>errors.push(error.message));page.on('request',request=>{if(/^https?:/.test(request.url()))external.push(request.url());});
  page.on('console',msg=>{if(msg.type()==='error'&&/Content Security Policy/.test(msg.text()))csp.push(msg.text());});
  await page.route(/^https?:\/\//,route=>route.abort());
  await page.addInitScript(()=>{
   const names=['Common','Uncommon','Rare','SR','SSR','UR'],colors=['#9baec8','#6ed4a5','#6cabff','#c093f4','#efc975','#ec91bf'];
   const source=(wide)=>{const canvas=document.createElement('canvas');canvas.width=wide?640:160;canvas.height=wide?240:360;const ctx=canvas.getContext('2d');ctx.fillStyle='#d9e7ff';ctx.fillRect(0,0,canvas.width,canvas.height);ctx.fillStyle='#ff0000';ctx.fillRect(0,0,canvas.width,canvas.height*.13);ctx.fillStyle='#00ff00';ctx.fillRect(0,canvas.height*.87,canvas.width,canvas.height*.13);ctx.fillStyle='#5684d5';ctx.fillRect(canvas.width*.35,canvas.height*.32,canvas.width*.3,canvas.height*.36);return canvas.toDataURL('image/png');};
   let listener,session,issued=0;const images={wide:source(true),tall:source(false)};
   const wallet={Gems:99999,FreeCredits:5,PaidCredits:0,LimitedCredits:0,PackPrice:160,LimitedPackPrice:180,MaxFreeCredits:5,PitySR:0,PitySSR:0,PityUR:0,ServerNow:new Date().toISOString(),NextFreeAt:new Date(Date.now()+1200000).toISOString()};
   const creative={Albums:[],Cosmetics:[],RestorableLooks:[],DyePalette:[],Dyes:[],FrameMode:'rarity',FrameCatalog:[],FrameItems:[],InventoryItems:[],OwnedFrameIds:['classic'],DyeBoosterPrice:50,NextStudioSequence:1};
   const gameplay={Day:'2026-10-11',Profiles:[],Routes:[],Expeditions:[],Inventory:[],Fight:{DailyRemaining:3},ServerNow:new Date().toISOString()};
   const qa=window.canvasQa={calls:[],saved:[],badges:[],images,fetchCalls:0,names,colors};
   window.fetch=()=>{qa.fetchCalls++;throw Error('No external fetch is permitted in Canvas fixture');};
   const reply=msg=>setTimeout(()=>listener({data:{session,...msg,wallet}}),3);
   window.chrome={webview:{addEventListener(type,fn){if(type==='message')listener=fn;},postMessage(msg){
    session=msg.session||session;qa.calls.push({...msg,...(msg.png?{png:'[local card PNG]'}:{})});
    if(msg.type==='hytcgInit'){reply({type:'state',state:{Settings:{Language:'en',ThemeMode:'dark',AccentColor:'#44d5d1'}}});reply({type:'hytcgConfig',config:{Concurrency:2,DropRates:names.map((Name,i)=>({Name,Color:colors[i],Weight:1}))}});}
    else if(msg.type==='hytcgCollection')reply({...msg,page:{Items:[],Total:qa.saved.length,HasMore:false,NextOffset:0}});
    else if(msg.type==='hytcgWallet'||msg.type==='hytcgDiscoveryState')reply({...msg,banners:[],missions:[]});
    else if(msg.type==='hytcgReserve')reply({...msg,type:'hytcgReserved',tokens:Array.from({length:msg.count},(_,i)=>'fixture-token-'+i),reservationId:'fixture-reservation'});
    else if(msg.type==='hytcgOpen'){
     const index=issued++,rarity=names[index%6],wide=index%2===0;
     const card={CardId:'HT-20261011-'+(index+1).toString(16).padStart(12,'0').toUpperCase(),CharacterId:99991+index,Name:rarity+' portrait companion',Series:'HYTCG local Canvas verification',Gender:'Female',Rarity:rarity,RarityColor:colors[index%6],Orientation:'portrait',ImageSource:'zerochan',ArtworkWidth:wide?640:160,ArtworkHeight:wide?240:360,Metadata:{hytcg_card_layout:index%6<4?'window-v4':'full-v4',description:'Complete artwork in a portrait card. Independent rarity emblem.'}};
     reply({...msg,type:'hytcgCard',card,imageData:wide?images.wide:images.tall});
    }else if(msg.type==='hytcgSave'){qa.saved.push({cardId:msg.cardId,png:msg.png});reply({...msg,type:'hytcgSaved',path:'fixture-cards/'+msg.cardId+'.hycard'});}
    else if(msg.type==='hytcgLifestyle')reply({...msg,creative,gameplay,roster:[],rosterTotal:0});
   }}};
  });
  await page.goto(pathToFileURL(path.join(root,'ui/hytcg.html')).href);
  await page.waitForFunction(()=>tcg.config&&window.HyTcgRenderer&&window.HyTcgRarity);
  check(await page.evaluate(()=>document.querySelector('meta[http-equiv="Content-Security-Policy"]').content.includes("connect-src 'none'")),'Actual HYTCG document retains strict offline renderer CSP');
  await page.evaluate(()=>{const actual=window.HyTcgRarity;window.HyTcgRarity={...actual,draw:async(ctx,name,box)=>{canvasQa.badges.push({name,...box});return actual.draw(ctx,name,box);}};});
  await page.evaluate(()=>openPack(10,'gems'));
  check(await page.evaluate(()=>tcg.slots.length===10&&tcg.slots.every(slot=>slot.status==='ready'&&slot.saved)),'Actual Open x10 renders and saves all portrait Canvas exports');
  check(await page.evaluate(()=>canvasQa.saved.length===10&&canvasQa.calls.filter(call=>call.type==='hytcgSave').length===10),'Actual save bridge receives one local PNG per issued card');
  const result=await page.evaluate(async()=>{
   const {names,colors}=canvasQa,items=[];
   const load=data=>new Promise((resolve,reject)=>{const image=new Image();image.onload=()=>resolve(image);image.onerror=reject;image.src=data;});
   const rgba=(ctx,x,y)=>Array.from(ctx.getImageData(x,y,1,1).data);
   for(let index=0;index<names.length;index++)for(const shape of['tall','wide']){
    const rarity=names[index],low=index<4;
    const card={CardId:'HT-20261011-'+(20+index).toString(16).padStart(12,'0').toUpperCase(),CharacterId:99991,Name:rarity+' complete portrait companion with a long name',Series:'HYTCG verification collection',Gender:'Female',Rarity:rarity,RarityColor:colors[index],Orientation:'landscape',ImageSource:'zerochan',Metadata:{hytcg_card_layout:low?'window-v4':'full-v4',desc:'Complete original artwork, clipped within the frame. <b>Extra text</b> &amp; detail.'}};
    const before=JSON.stringify(card),png=await HyTcgRenderer.render(card,canvasQa.images[shape]),image=await load(png);
    const canvas=document.createElement('canvas');canvas.width=image.width;canvas.height=image.height;const ctx=canvas.getContext('2d');ctx.drawImage(image,0,0);
    const alt=await HyTcgRenderer.render({...card,RarityColor:'#ff0044'},canvasQa.images[shape]),altImage=await load(alt),altCanvas=document.createElement('canvas');altCanvas.width=image.width;altCanvas.height=image.height;const altCtx=altCanvas.getContext('2d');altCtx.drawImage(altImage,0,0);
    const expected=document.createElement('canvas');expected.width=750;expected.height=1050;const expectedCtx=expected.getContext('2d'),box={x:518,y:low?1050*.652:40,w:180,h:64};await HyTcgRarity.draw(expectedCtx,rarity,box);
    const expectedPixels=expectedCtx.getImageData(518,Math.floor(box.y),180,65).data,actualPixels=ctx.getImageData(518,Math.floor(box.y),180,65).data,altPixels=altCtx.getImageData(518,Math.floor(box.y),180,65).data;
    // Compare the actual icon/text artwork. Omit the separate semi-transparent
    // divider at x60: its two cap pixels can rerasterize at fractional Y=684.6.
    // The complete opaque badge still has an exact recolor equality check.
    let opaque=0,mismatch=0,maxBadgeDelta=0,recolorMismatch=0;const mismatchSamples=[];for(let offset=0;offset<expectedPixels.length;offset+=4){const px=(offset/4)%180,py=Math.floor(offset/4/180);if(expectedPixels[offset+3]===255&&px>=8&&px<172&&py>=8&&py<56){for(let channel=0;channel<3;channel++)if(actualPixels[offset+channel]!==altPixels[offset+channel])recolorMismatch++;if(px>=54&&px<=66)continue;opaque++;for(let channel=0;channel<3;channel++){const delta=Math.abs(actualPixels[offset+channel]-expectedPixels[offset+channel]);maxBadgeDelta=Math.max(maxBadgeDelta,delta);if(delta>1){mismatch++;if(mismatchSamples.length<8)mismatchSamples.push({px,py,channel,actual:actualPixels[offset+channel],expected:expectedPixels[offset+channel]});}}}}
    const layout=HyTcgRenderer.windowLayout(card),art={x:layout.ArtworkX*750,y:layout.ArtworkY*1050,w:layout.ArtworkWidth*750,h:layout.ArtworkHeight*1050},source=await load(canvasQa.images[shape]),scale=Math.min(art.w/source.width,art.h/source.height),headY=Math.ceil(art.y+(art.h-source.height*scale)/2+3),feetY=Math.floor(art.y+(art.h+source.height*scale)/2-4);
    const head=low?rgba(ctx,375,headY):[],feet=low?rgba(ctx,375,feetY):[],center=low?rgba(ctx,375,Math.floor(art.y+art.h/2)):[];
    items.push({rarity,shape,png,width:image.width,height:image.height,corner:rgba(ctx,0,0),artAlpha:rgba(ctx,375,300)[3],opaqueBadge:opaque,badgeMismatch:mismatch,maxBadgeDelta,mismatchSamples,recolorMismatch,head,feet,center,unchanged:JSON.stringify(card)===before,marker:card.Metadata.hytcg_card_layout});
   }
   return {items,badges:canvasQa.badges,saved:tcg.slots.map(slot=>({cardId:slot.card.CardId,orientation:slot.card.Orientation,marker:slot.card.Metadata.hytcg_card_layout,rarity:slot.card.Rarity,png:slot.png})),fetchCalls:canvasQa.fetchCalls};
  });
  fs.writeFileSync(path.join(out,'observations.json'),JSON.stringify(result.items.map(({png,...item})=>item),null,2));
  check(result.badges.every(badge=>badge.w===180&&badge.h===64),'Every exported card uses exact 180×64 SVG rarity badge geometry');
  for(const item of result.items){
   const label=item.rarity+'/'+item.shape;
   check(item.width===750&&item.height===1050,'All source aspect ratios and legacy model hints export portrait: '+label);
   check(item.corner[3]===0&&item.artAlpha===255,'Rounded alpha corner and opaque artwork: '+label);
   check(item.opaqueBadge>7000&&item.badgeMismatch===0,'Actual exported opaque badge icon/text pixels equal the standalone SVG: '+label);
   check(item.recolorMismatch===0,'Rarity emblem remains fixed when card/rim color changes: '+label);
   check(item.unchanged,'Rendering does not mutate source card ownership metadata: '+label);
   if(['Common','Uncommon','Rare','SR'].includes(item.rarity)){
    check(item.head[0]===255&&item.head[1]===0&&item.head[2]===0,'Headerless art retains complete head: '+label);
    check(item.feet[0]===0&&item.feet[1]===255&&item.feet[2]===0,'Contain fit retains complete artwork feet: '+label);
    check(item.center[2]>item.center[0]&&item.center[3]===255,'Wide/tall complete artwork is vertically centered: '+label);
   }
   fs.writeFileSync(path.join(out,item.rarity+'-'+item.shape+'.png'),Buffer.from(item.png.slice(item.png.indexOf(',')+1),'base64'));
  }
  for(const saved of result.saved){
   check(saved.orientation==='portrait'&&saved.marker===(['SSR','UR'].includes(saved.rarity)?'full-v4':'window-v4'),'Actual Open/Save keeps current native portrait layout metadata: '+saved.rarity);
  }
  await page.screenshot({path:path.join(out,'actual-booster.png'),fullPage:false});
  await page.evaluate(items=>{
   document.querySelector('.tcgShell').hidden=true;
   const sheet=document.createElement('section');sheet.id='qaContactSheet';sheet.innerHTML='<h1>HYTCG portrait / SVG rarity exports</h1>'+items.map(item=>'<figure><img width="200" height="280" src="'+item.png+'"><figcaption>'+item.rarity+' · '+item.shape+'</figcaption></figure>').join('');document.body.append(sheet);
  },result.items);
  // Apply presentation to individual DOM properties for this isolated contact
  // sheet while leaving the actual document's strict script/style CSP intact.
  await page.evaluate(()=>{const sheet=document.querySelector('#qaContactSheet');Object.assign(sheet.style,{display:'grid',gridTemplateColumns:'repeat(6, 160px)',gap:'14px',padding:'18px',background:'#141b29'});Object.assign(sheet.querySelector('h1').style,{gridColumn:'1 / -1',font:'600 20px Segoe UI',color:'#dbe8f9',margin:'0 0 8px'});for(const figure of sheet.querySelectorAll('figure')){figure.style.margin='0';const image=figure.querySelector('img');image.width=160;image.height=224;image.style.display='block';Object.assign(figure.querySelector('figcaption').style,{padding:'6px',color:'#b9cce1',font:'12px Segoe UI'});}});
  await page.locator('#qaContactSheet').screenshot({path:path.join(out,'twelve-portrait-cards.png')});
  check(result.fetchCalls===0&&external.length===0,'All card render/save integration uses local images and no remote API');
  check(errors.length===0,'Actual HYTCG scripts report no JavaScript errors');
  const details=result.items.map(({png,...item})=>item);fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({Status:'PASS',Checks:checks,Items:details,BadgeSize:{width:180,height:64},RemoteRequests:external,FetchCalls:result.fetchCalls,Errors:errors,CspErrors:csp.filter(message=>!/inline style/.test(message))},null,2));
  console.log('PASS '+checks+' actual HYTCG Canvas portrait/SVG integration checks');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
