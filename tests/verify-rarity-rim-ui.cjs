'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path');
const {chromium}=require('./test-runtime.cjs').playwright();
const root=testRuntime.projectRoot,output=path.join(testRuntime.verificationRoot,'rarity-rim-ui');fs.mkdirSync(output,{recursive:true});
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1824,height:960}});
 await page.setContent('<html><body style="margin:0;padding:20px;background:#0a101b;color:#c9d4e5;font:14px Segoe UI;display:grid;grid-template-columns:repeat(6,280px);gap:20px"></body></html>');
 await page.addScriptTag({content:fs.readFileSync(path.join(root,'ui/hytcg-renderer.js'),'utf8')});let checks=0;
 for(const rarity of ['Common','Uncommon','Rare','SR','SSR','UR'])for(const orientation of ['portrait','landscape']){
  const result=await page.evaluate(async({rarity,orientation})=>{
   const source=document.createElement('canvas');source.width=orientation==='portrait'?400:1200;source.height=orientation==='portrait'?1000:500;let c=source.getContext('2d');
   const sky=c.createLinearGradient(0,0,0,source.height);sky.addColorStop(0,'#243353');sky.addColorStop(.55,'#778ea8');sky.addColorStop(1,'#151e37');c.fillStyle=sky;c.fillRect(0,0,source.width,source.height);
   c.fillStyle='#eadaca';c.beginPath();c.ellipse(source.width/2,source.height*.24,source.width*.16,source.height*.12,0,0,Math.PI*2);c.fill();
   c.fillStyle='#ddd1de';c.fillRect(source.width*.31,source.height*.36,source.width*.38,source.height*.50);c.fillStyle='#272140';c.fillRect(source.width*.25,source.height*.11,source.width*.50,source.height*.08);
   c.fillStyle='#ff0000';c.fillRect(0,0,source.width,25);c.fillStyle='#00ff00';c.fillRect(0,source.height-25,source.width,25);
   const card={CardId:'HT-20261011-ABCDEF123456',CharacterId:99991,Name:'Meredy',Series:'Tales of Eternia',Gender:'Female',Rarity:rarity,RarityColor:'#76b8b7',Orientation:orientation,ImageSource:'zerochan',Metadata:{desc:'A travelling companion from Tales of Eternia.'}};
   const data=await HyTcgRenderer.render(card,source.toDataURL());const image=new Image();image.src=data;await image.decode();const pixels=document.createElement('canvas');pixels.width=image.naturalWidth;pixels.height=image.naturalHeight;c=pixels.getContext('2d');c.drawImage(image,0,0);
   const rgba=c.getImageData(0,0,pixels.width,pixels.height).data;let red=0,green=0;for(let i=0;i<rgba.length;i+=4){if(rgba[i]>235&&rgba[i+1]<30&&rgba[i+2]<30)red++;if(rgba[i]<30&&rgba[i+1]>235&&rgba[i+2]<30)green++;}
   const rim=[...c.getImageData(16,Math.round(pixels.height*.22),1,1).data],stock=[...c.getImageData(375,700,1,1).data];
   if(orientation==='portrait'){const figure=document.createElement('figure');figure.style.margin='0';image.style.width='280px';image.style.display='block';image.style.borderRadius='14px';const label=document.createElement('figcaption');label.textContent=rarity;label.style.cssText='padding:10px;text-align:center';figure.append(image,label);document.body.append(figure);}
   return {data,source:source.toDataURL(),red,green,rim,stock,width:pixels.width,height:pixels.height,low:HyTcgRenderer.isWindowRarity(rarity)};
  },{rarity,orientation});
  const check=(ok,name)=>{if(!ok)throw Error(name+' '+rarity+' '+orientation);checks++;};
  check(result.width===(orientation==='portrait'?750:1050)&&result.height===(orientation==='portrait'?1050:750),'native dimensions');
  check(result.rim[3]===255 && result.rim[0]<220 && result.rim[1]>55,'visible solid beveled rim');
  if(result.low){check(result.red>100,'complete head');check(result.green>100,'complete feet');if(orientation==='portrait')check(result.stock[0]===238&&result.stock[1]===234&&result.stock[2]===222,'neutral identity stock');}
  fs.writeFileSync(path.join(output,rarity+'-'+orientation+'.png'),Buffer.from(result.data.split(',')[1],'base64'));fs.writeFileSync(path.join(output,'source-'+orientation+'.png'),Buffer.from(result.source.split(',')[1],'base64'));
 }
 await page.screenshot({path:path.join(output,'six-rarity-preview.png'),fullPage:true});await browser.close();fs.writeFileSync(path.join(output,'report.json'),JSON.stringify({Passed:true,Checks:checks},null,2));console.log('PASS Canvas rarity rim: '+checks+' checks');
})().catch(error=>{console.error(error.stack);process.exit(1);});
