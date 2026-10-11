'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require('./test-runtime.cjs').playwright();
const root=testRuntime.projectRoot,out=path.join(testRuntime.verificationRoot,'default-card-layouts');
(async()=>{
 fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;
 try{
  const page=await browser.newPage({viewport:{width:1560,height:1100},deviceScaleFactor:1});const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.setContent('<!doctype html><meta charset="utf-8"><style>body{margin:0;background:#101623;color:#e6eaf4;font:14px Segoe UI}h1{font-size:18px;margin:20px}.cards{display:flex;gap:16px;margin:20px;align-items:start;flex-wrap:wrap}figure{margin:0}figcaption{padding:10px 0}img{display:block;width:235px;border-radius:12px}</style><h1>Native export renderer · isolated fixtures</h1><main class="cards"></main>');
  await page.addScriptTag({path:path.join(root,'ui/hytcg-renderer.js')});
  const results=await page.evaluate(async()=>{
   const artwork=document.createElement('canvas');artwork.width=640;artwork.height=960;const ctx=artwork.getContext('2d');
   const grad=ctx.createLinearGradient(0,0,640,960);grad.addColorStop(0,'#183341');grad.addColorStop(.5,'#687879');grad.addColorStop(1,'#bd7b7c');ctx.fillStyle=grad;ctx.fillRect(0,0,640,960);
   ctx.fillStyle='#c0adb2';ctx.beginPath();ctx.ellipse(320,320,105,140,0,0,Math.PI*2);ctx.fill();ctx.fillStyle='#252237';ctx.beginPath();ctx.ellipse(320,550,180,230,0,0,Math.PI*2);ctx.fill();
   const colors={Common:'#aebccc',Uncommon:'#74c8a8',Rare:'#68b7f0',SR:'#b49aef',SSR:'#e5c670',UR:'#ee9fc6'},results=[];
   for(const orientation of ['portrait','landscape'])for(const rarity of Object.keys(colors)){
    const card={CardId:'HT-20261011-ABCDEF123456',CharacterId:12345,Name:'Starlight Companion 玉藻前',Series:'Moonlit Garden',Gender:'Female',Rarity:rarity,RarityColor:colors[rarity],ImageSource:'zerochan',Revision:2,Orientation:orientation,Metadata:{desc:'<b>A curious companion</b> protects the moonlit garden &amp; welcomes every new adventure. Her story continues across countless nights beneath the stars.'}};
    const metadata=JSON.stringify(card),png=await HyTcgRenderer.render(card,artwork.toDataURL('image/png')),img=new Image();img.src=png;await img.decode();
    const sample=document.createElement('canvas');sample.width=img.naturalWidth;sample.height=img.naturalHeight;const sc=sample.getContext('2d');sc.drawImage(img,0,0);const pixel=(x,y)=>Array.from(sc.getImageData(x,y,1,1).data);
    const figure=document.createElement('figure');figure.innerHTML='<figcaption>'+rarity+' · '+orientation+'</figcaption>';const display=new Image();display.src=png;display.style.width=orientation==='landscape'?'320px':'235px';figure.append(display);document.querySelector('main').append(figure);
    results.push({rarity,orientation,png,width:img.naturalWidth,height:img.naturalHeight,metadataUnchanged:JSON.stringify(card)===metadata,footer:pixel(Math.floor(img.naturalWidth*.50),Math.floor(img.naturalHeight*.90)),art:pixel(Math.floor(img.naturalWidth*(orientation==='landscape'?.30:.50)),Math.floor(img.naturalHeight*(orientation==='landscape'?.40:.30))),layout:HyTcgRenderer.windowLayout(card)});
   }
   return results;
  });
  const low=new Set(['Common','Uncommon','Rare','SR']);for(const row of results){
   assert.equal(row.width,row.orientation==='portrait'?750:1050);checks++;assert.equal(row.height,row.orientation==='portrait'?1050:750);checks++;assert(row.metadataUnchanged);checks++;
   if(low.has(row.rarity)){assert.deepEqual(row.footer,[238,234,222,255]);checks++;}else{assert(row.footer[0]<40&&row.footer[1]<40&&row.footer[2]<50);checks++;}
   fs.writeFileSync(path.join(out,row.rarity.toLowerCase()+'-'+row.orientation+'.png'),Buffer.from(row.png.split(',')[1],'base64'));
  }
  const native=fs.readdirSync(testRuntime.verificationRoot).filter(n=>n.startsWith('hytcg-cosmetic-renderer-')&&fs.existsSync(path.join(testRuntime.verificationRoot,n,'default-layouts.json'))).map(n=>({n,time:fs.statSync(path.join(testRuntime.verificationRoot,n,'default-layouts.json')).mtimeMs})).sort((a,b)=>b.time-a.time)[0];
  assert(native,'Native layout fixture exists');checks++;const layouts=JSON.parse(fs.readFileSync(path.join(testRuntime.verificationRoot,native.n,'default-layouts.json'),'utf8'));
  for(const orientation of ['portrait','landscape']){assert.deepEqual(results.find(x=>x.orientation===orientation).layout,layouts[orientation]);checks++;}
  await page.screenshot({path:path.join(out,'all-rarities.png'),fullPage:true});assert.deepEqual(errors,[]);checks++;
  fs.writeFileSync(path.join(out,'result.json'),JSON.stringify({checks,errors,native:native.n},null,2));process.stdout.write('HYTCG default canvas layouts PASS: '+checks+' checks; '+out+'\n');
 }finally{await browser.close();}
})().catch(error=>{process.stderr.write(error.stack+'\n');process.exitCode=1;});
