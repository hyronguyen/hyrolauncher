'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const {chromium}=require('./test-runtime.cjs').playwright();
const root=testRuntime.projectRoot,verification=path.join(root,'output','verification'),out=path.join(verification,'hytcg-default-layout-ui-'+crypto.randomUUID().replaceAll('-',''));
const nativeRoot=process.argv[2]||fs.readdirSync(verification).filter(name=>name.startsWith('hytcg-cosmetic-renderer-')&&fs.existsSync(path.join(verification,name,'default-layouts.json'))).map(name=>({name,time:fs.statSync(path.join(verification,name)).mtimeMs})).sort((a,b)=>b.time-a.time)[0]?.name;
if(!nativeRoot)throw Error('Run verify-hytcg-cosmetic-renderer.ps1 first for the isolated native layout contract.');
const nativeLayouts=JSON.parse(fs.readFileSync(path.isAbsolute(nativeRoot)?path.join(nativeRoot,'default-layouts.json'):path.join(verification,nativeRoot,'default-layouts.json'),'utf8'));
(async()=>{
 fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0,network=0;
 try{
  const page=await browser.newPage({viewport:{width:1500,height:1120}}),errors=[];
  page.on('pageerror',error=>errors.push(error.message));await page.route(/^https?:\/\//,route=>{network++;return route.abort();});
  await page.setContent('<!doctype html><meta charset="utf-8"><body style="margin:20px;background:#202634;color:white;font-family:Segoe UI"><div id="cards" style="display:flex;flex-wrap:wrap;gap:16px;align-items:start"></div></body>');
  await page.addScriptTag({path:path.join(root,'ui','hytcg-renderer.js')});
  const results=await page.evaluate(async()=>{
   const imageCanvas=document.createElement('canvas');imageCanvas.width=300;imageCanvas.height=500;
   const imageContext=imageCanvas.getContext('2d');imageContext.fillStyle='#187889';imageContext.fillRect(0,0,300,500);const source=imageCanvas.toDataURL('image/png');
   const rates={Common:'#aebccc',Uncommon:'#6fd6b0',Rare:'#7ab9f9',SR:'#bf96ef',SSR:'#f5c879',UR:'#f6c6ff'},result=[];
   async function inspect(png,card){
    const image=new Image();await new Promise((resolve,reject)=>{image.onload=resolve;image.onerror=reject;image.src=png;});
    const canvas=document.createElement('canvas');canvas.width=image.naturalWidth;canvas.height=image.naturalHeight;const ctx=canvas.getContext('2d');ctx.drawImage(image,0,0);
    const width=canvas.width,height=canvas.height,layout=HyTcgRenderer.windowLayout(card),pixel=(x,y)=>Array.from(ctx.getImageData(Math.floor(x),Math.floor(y),1,1).data);
    const count=(name,test)=>{let total=0;const x=Math.floor(layout[name+'X']*width),y=Math.floor(layout[name+'Y']*height),w=Math.floor(layout[name+'Width']*width),h=Math.floor(layout[name+'Height']*height),pixels=ctx.getImageData(x,y,w,h).data;for(let i=0;i<pixels.length;i+=4)if(test(pixels[i],pixels[i+1],pixels[i+2],pixels[i+3]))total++;return total;};
    const color=card.RarityColor.slice(1).match(/../g).map(hex=>parseInt(hex,16));
    return {width,height,layout,art:pixel((layout.ArtworkX+layout.ArtworkWidth/2)*width,(layout.ArtworkY+layout.ArtworkHeight/2)*height),edge:pixel(35,height*.5),alpha:pixel(0,0)[3],
      title:count('Name',(r,g,b,a)=>a>200&&r>230&&g>230&&b>230),description:count('Description',(r,g,b,a)=>a>200&&r<100&&g<100&&b<100),footer:count('Number',(r,g,b,a)=>a>200&&r<100&&g<100&&b<100),rarity:count('Rarity',(r,g,b,a)=>a>200&&r===color[0]&&g===color[1]&&b===color[2])};
   }
   for(const orientation of['portrait','landscape'])for(const rarity of Object.keys(rates)){
    const card={Orientation:orientation,CharacterId:177,Name:'A companion with a long name 玉藻前 タマモノマエ',Series:'The Moonlit Garden Chronicles',Gender:'Female',Rarity:rarity,RarityColor:rates[rarity],CardId:'HT-20261010-ABCDEF123456',Revision:2,ImageSource:'acdb',Metadata:{desc:'<b>A companion</b> watches the moonlit garden &amp; keeps a promise. A protected character description appears below the artwork.'}},before=JSON.stringify(card);
    const png=await HyTcgRenderer.render(card,source),stats=await inspect(png,card),low=HyTcgRenderer.isWindowRarity(rarity);
    const changed=await HyTcgRenderer.render({...card,Metadata:{desc:'Another native character description.'}},source);
    result.push({orientation,rarity,low,png,stats,descriptionChanges:changed!==png,immutable:JSON.stringify(card)===before});
    const section=document.createElement('div');section.style.width=orientation==='landscape'?'330px':'225px';const label=document.createElement('p');label.textContent=rarity+' · '+orientation;label.style.margin='0 0 6px';section.append(label);const display=new Image();display.src=png;display.style.width='100%';section.append(display);document.querySelector('#cards').append(section);
   }
   let blocked=false;try{await HyTcgRenderer.render({Rarity:'Common'},'https://example.invalid/art.png');}catch(error){blocked=error.message==='ArtworkInvalid';}
   const sr={Orientation:'portrait',CharacterId:177,Name:'Upgrade companion',Series:'Fixture series',Rarity:'SR',RarityColor:rates.SR,CardId:'HT-20261010-ABCDEF123456'},ssr={...sr,Rarity:'SSR',RarityColor:rates.SSR,Revision:1};
   return {result,blocked,upgraded:{from:await inspect(await HyTcgRenderer.render(sr,source),sr),to:await inspect(await HyTcgRenderer.render(ssr,source),ssr)}};
  });
  for(const item of results.result){
   const {stats,orientation,rarity,low}=item;
   assert.equal(stats.width,orientation==='landscape'?1050:750);assert.equal(stats.height,orientation==='landscape'?750:1050);checks+=2;
   assert.equal(item.immutable,true);assert.equal(stats.alpha,0);checks+=2;
   if(low){
    assert.deepEqual(stats.layout,nativeLayouts[orientation]);
    if(rarity==='SR')assert.ok(stats.art.slice(0,3).every((channel,index)=>Math.abs(channel-[24,120,137][index])<=24)&&stats.art[3]===255,'SR foil stays subtle inside the artwork window');
    else assert.deepEqual(stats.art,[24,120,137,255]);checks+=2;
    assert.ok(stats.title>0&&stats.description>0&&stats.footer>0&&stats.rarity>0,'native header, description, footer and rarity glyphs');checks++;
    assert.deepEqual(stats.edge,[238,234,222,255]);assert.equal(item.descriptionChanges,true);checks+=2;
   }else{assert.notDeepEqual(stats.edge,[238,234,222,255]);assert.equal(item.descriptionChanges,false);checks+=2;}
   fs.writeFileSync(path.join(out,rarity.toLowerCase()+'-'+orientation+'.png'),Buffer.from(item.png.split(',')[1],'base64'));
  }
  assert.deepEqual(results.upgraded.from.edge,[238,234,222,255]);assert.notDeepEqual(results.upgraded.to.edge,[238,234,222,255]);checks+=2;
  assert.equal(results.blocked,true);assert.equal(network,0);assert.deepEqual(errors,[]);checks+=3;
  await page.screenshot({path:path.join(out,'all-rarities.png'),fullPage:true});
  console.log('HYTCG browser default layouts PASS: '+checks+' checks; both orientations, native geometry, descriptions, full-bleed SSR/UR, upgrade transition, immutable metadata and offline rendering.');
  console.log('Visual fixtures: '+out);
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
