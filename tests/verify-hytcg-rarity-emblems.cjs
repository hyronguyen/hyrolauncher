'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const {pathToFileURL} = require('node:url');
const {chromium} = require('./test-runtime.cjs').playwright();
const root = testRuntime.projectRoot, out = path.join(testRuntime.verificationRoot, 'rarity-emblems');
const names = ['Common','Uncommon','Rare','SR','SSR','UR'];
const palette = ['#9baec8','#6ed4a5','#6cabff','#c093f4','#efc975','#ec91bf'];
(async () => {
  fs.mkdirSync(out, {recursive:true});
  fs.writeFileSync(path.join(out,'fixture.css'), 'body{background:#0c111c;color:#cbd7eb;font-family:Segoe UI,Arial;margin:20px}#emblems{display:flex;gap:8px;margin-bottom:18px}.rarityEmblem{width:180px;height:64px}canvas{background:#0c111c}');
  fs.writeFileSync(path.join(out,'fixture.html'), '<!doctype html><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src \'self\' file:; script-src \'self\'; style-src \'self\'; img-src \'self\' file: data:; connect-src \'none\'; frame-src \'none\'; object-src \'none\'; base-uri \'none\'"><title>Rarity emblem verification</title><link rel="stylesheet" href="fixture.css"><script src="'+pathToFileURL(path.join(root,'ui/hytcg-rarity.js')).href+'"></script><div id="emblems"></div><canvas id="canvas" width="1180" height="170"></canvas>');
  const browser = await chromium.launch({channel:'msedge',headless:true});
  let checks=0;
  const check = (value,label) => {assert(value,label);checks++;};
  try {
    const page = await browser.newPage({viewport:{width:1200,height:300}}), errors=[], external=[];
    page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))external.push(r.url());});
    await page.addInitScript(() => {const BaseImage=window.Image;window.__imageCount=0;window.Image=function(){window.__imageCount++;return new BaseImage();};window.fetch=function(){throw Error('Fetch must not be used');};});
    await page.goto(pathToFileURL(path.join(out,'fixture.html')).href);
    await page.evaluate(names=>{document.querySelector('#emblems').innerHTML=names.map(name=>HyTcgRarity.markup(name)).join('');},names);
    await page.waitForFunction(()=>[...document.querySelectorAll('.rarityEmblem')].every(image=>image.complete&&image.naturalWidth===180));
    for (let i=0;i<names.length;i++) {
      const svg=fs.readFileSync(path.join(root,'ui/rarities',names[i].toLowerCase()+'.svg'),'utf8');
      check(svg.includes('viewBox="0 0 180 64"'),'Consistent geometry: '+names[i]);
      check(svg.includes(palette[i]),'Independent fixed palette: '+names[i]);
      check(svg.includes('>'+names[i]+'</text>'),'Readable label: '+names[i]);
      check(fs.readFileSync(path.join(root,'ui/hytcg-rarity.js'),'utf8').includes(JSON.stringify(svg.replace(/\r?\n\s*/g,''))),'Canvas vector matches standalone SVG: '+names[i]);
      check(!/(https?:|currentColor|var\(|<script|<foreignObject|<image\b)/.test(svg.replace('http://www.w3.org/2000/svg','')),'Standalone safe vector: '+names[i]);
    }
    const result=await page.evaluate(async names=>{
      const ctx=document.querySelector('#canvas').getContext('2d');
      const values=await Promise.all(names.map((name,i)=>HyTcgRarity.draw(ctx,name,{x:10+i*194,y:12,w:180,h:64})));
      const firstCount=window.__imageCount;
      await Promise.all(names.map((name,i)=>HyTcgRarity.draw(ctx,name,{x:10+i*194,y:94,w:90,h:32})));
      return {values,firstCount,lastCount:window.__imageCount,exportable:document.querySelector('#canvas').toDataURL('image/png').startsWith('data:image/png'),unknown:HyTcgRarity.url('unknown'),caseUrl:HyTcgRarity.url(' ssr '),safeMarkup:HyTcgRarity.markup('UR','x" onerror="alert(1)')};
    },names);
    check(result.values.every(Boolean),'All actual SVG assets draw onto Canvas');
    check(result.firstCount===6&&result.lastCount===6,'Exactly one cached Image object per rarity');
    check(result.exportable,'Canvas remains exportable after local SVG draw');
    check(result.unknown.endsWith('/rarities/common.svg')&&result.caseUrl.endsWith('/rarities/ssr.svg'),'Canonical allowlist resolves safe asset URLs');
    check(!result.safeMarkup.includes('onerror="'),'Extra class is HTML-escaped');
    await page.locator('#canvas').screenshot({path:path.join(out,'six-emblems.png')});
    const fallbackPage=await browser.newPage();
    await fallbackPage.addInitScript(()=>{window.Image=function(){Object.defineProperty(this,'src',{set:()=>queueMicrotask(()=>this.onerror())});};});
    await fallbackPage.goto(pathToFileURL(path.join(out,'fixture.html')).href);
    const fallback=await fallbackPage.evaluate(async()=>{
      const canvas=document.querySelector('#canvas'),ctx=canvas.getContext('2d');
      const drawn=await HyTcgRarity.draw(ctx,'SSR',{x:10,y:10,w:180,h:64});
      const pixels=ctx.getImageData(10,10,180,64).data;let opaque=0;for(let i=3;i<pixels.length;i+=4)if(pixels[i])opaque++;
      return {drawn,opaque,invalid:await HyTcgRarity.draw(ctx,'UR',{x:0,y:0,w:-1,h:64})};
    });
    check(fallback.drawn===false&&fallback.opaque>9000,'Missing local asset paints readable fixed-color fallback');
    check(fallback.invalid===false,'Invalid Canvas bounds rejected');
    check(errors.length===0,'No browser script errors');check(external.length===0,'No external/API request');
    fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({passed:checks,errors,external,canvasExportable:result.exportable,cacheImageCount:result.lastCount,assets:names},null,2));
    console.log('PASS '+checks+' rarity SVG / Canvas checks');
  } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
