'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path');
const {chromium}=require('./test-runtime.cjs').playwright();
const output=path.join(testRuntime.verificationRoot,'frame-header-dye-ui');fs.mkdirSync(output,{recursive:true});
(async()=>{
  const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1200,height:850}});
  await page.setContent('<!doctype html><html><body style="margin:0;padding:24px;background:#0b101b;display:flex;gap:24px;align-items:flex-start"></body></html>');
  await page.addScriptTag({content:fs.readFileSync(path.join(testRuntime.projectRoot,'ui/hytcg-renderer.js'),'utf8')});
  let checks=0;
  for(const rarity of ['Common','Uncommon','Rare','SR'])for(const orientation of ['portrait','landscape']){
    const result=await page.evaluate(async({rarity,orientation})=>{
      const artwork=document.createElement('canvas');artwork.width=orientation==='landscape'?900:300;artwork.height=orientation==='landscape'?300:900;
      let ctx=artwork.getContext('2d');ctx.fillStyle='blue';ctx.fillRect(0,0,artwork.width,artwork.height);ctx.fillStyle='red';ctx.fillRect(0,0,artwork.width,35);ctx.fillStyle='lime';ctx.fillRect(0,artwork.height-35,artwork.width,35);
      const card={Name:'Meredy',Series:'Tales of Eternia',CharacterId:99991,CardId:'HT-20261011-ABCDEF123456',Gender:'Female',Rarity:rarity,RarityColor:'#78ceae',Orientation:orientation,ImageSource:'zerochan',Metadata:{desc:'A travelling companion from Tales of Eternia.'}};
      const result=await HyTcgRenderer.render(card,artwork.toDataURL());
      const img=new Image();img.src=result;await img.decode();
      const canvas=document.createElement('canvas');canvas.width=img.naturalWidth;canvas.height=img.naturalHeight;ctx=canvas.getContext('2d');ctx.drawImage(img,0,0);
      const data=ctx.getImageData(0,0,canvas.width,canvas.height).data;
      let red=0,green=0;for(let n=0;n<data.length;n+=4){if(data[n]>235&&data[n+1]<45&&data[n+2]<45)red++;if(data[n]<45&&data[n+1]>235&&data[n+2]<45)green++;}
      const layout=HyTcgRenderer.windowLayout(card),header=ctx.getImageData(orientation==='landscape'?30:375,orientation==='landscape'?20:25,1,1).data;
      img.style.width=orientation==='landscape'?'520px':'285px';img.style.borderRadius='16px';img.alt=rarity+' '+orientation;document.body.replaceChildren(img);
      return {red,green,header:[...header],dimensions:[canvas.width,canvas.height],data:result,layout};
    },{rarity,orientation});
    const verify=(value,label)=>{if(!value)throw Error(label+' '+rarity+' '+orientation);checks++;};
    verify(result.red>200,'Complete source head');verify(result.green>200,'Complete source feet');verify(result.header[0]>200,'No dark header');
    verify(orientation==='landscape'?result.layout.NameX>result.layout.ArtworkX+result.layout.ArtworkWidth:result.layout.NameY>result.layout.ArtworkY+result.layout.ArtworkHeight,'Identity clear of artwork');
    verify(result.dimensions.join(',')===(orientation==='landscape'?'1050,750':'750,1050'),'Export ratio');
    fs.writeFileSync(path.join(output,rarity+'-'+orientation+'.png'),Buffer.from(result.data.split(',')[1],'base64'));
  }
  await browser.close();const report={Passed:true,Checks:checks};fs.writeFileSync(path.join(output,'report.json'),JSON.stringify(report,null,2));process.stdout.write(JSON.stringify(report));
})().catch(error=>{process.stderr.write(error.stack);process.exit(1);});
