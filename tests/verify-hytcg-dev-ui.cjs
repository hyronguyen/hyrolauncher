'use strict';
const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require('./test-runtime.cjs').playwright();
const root=testRuntime.projectRoot,out=path.join(testRuntime.verificationRoot,'hytcg-dev-ui');
(async()=>{
 fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;
 try{
  const page=await browser.newPage({viewport:{width:500,height:840}}),errors=[],messages=[];let active=false,remaining=600;
  page.on('pageerror',error=>errors.push(error.message));await page.route(/^https?:\/\//,route=>route.abort());
  const wallet=()=>({Gems:active?100000000:42,FreeCredits:5,PaidCredits:active?100000:1,LimitedCredits:active?100000:2,PackPrice:160,LimitedPackPrice:180,MaxFreeCredits:5,ServerNow:new Date().toISOString(),NextFreeAt:new Date(Date.now()+1200000).toISOString()});
  async function deliver(value){await page.evaluate(value=>window.fixtureDeliver(value),value);}
  await page.exposeFunction('fixtureSend',async msg=>{
   messages.push(msg);const common={session:msg.session,requestId:msg.requestId,error:''};
   try{
    if(msg.type==='ready'){await deliver({type:'state',state:{Settings:{Language:'vi',AccentColor:'#44cbe1',ThemeMode:'dark'}},assets:{}});return;}
    if(msg.type==='hytcgInit'){await deliver({...common,type:'hytcgConfig',config:{DropRates:[{Name:'Common',Weight:65,Color:'#94a3b8'},{Name:'SSR',Weight:1.1,Color:'#fbbf24'}],Concurrency:3},wallet:wallet(),dev:{active,remainingSeconds:remaining},recoveries:[],upgrades:[],missions:[]});return;}
    if(msg.type==='hytcgDevToggle'){await deliver({...common,type:'hytcgDevTransition'});active=!active;await deliver({...common,type:'hytcgDevState',nextSession:'realm'+Date.now(),dev:{active,remainingSeconds:remaining}});return;}
    if(msg.type==='hytcgCollection'){await deliver({...common,type:msg.type,page:{Items:[],Total:0,Offset:0,NextOffset:0,HasMore:false}});return;}
    if(msg.type==='hytcgDiscoveryState'){await deliver({...common,type:msg.type,wallet:wallet(),missions:[],banners:[]});return;}
    if(msg.type==='hytcgWallet'){await deliver({...common,type:msg.type,wallet:wallet()});return;}
    if(msg.type==='hytcgLifestyle'){await deliver({...common,type:msg.type,wallet:wallet(),creative:{Albums:[],Cosmetics:[],FrameItems:[],FrameCatalog:[],Dyes:[],DyePalette:[],InventoryItems:[],NextStudioSequence:1},gameplay:{Profiles:[],Inventory:[],Routes:[],Expeditions:[],Fight:{Stage:1,DailyRemaining:3}},roster:[]});return;}
   }catch(error){if(!/Execution context|destroyed|Target.*closed/.test(error.message))throw error;}
  });
  await page.addInitScript(()=>{let listener;window.fixtureDeliver=data=>listener?.({data});window.chrome={webview:{postMessage:msg=>window.fixtureSend(msg),addEventListener:(type,fn)=>{if(type==='message')listener=fn;}}};});
  await page.goto('file:///'+path.join(root,'ui/hytcg.html').replaceAll('\\','/'));await page.waitForFunction(()=>document.getElementById('gemBalance').textContent==='42');
  const ok=(test,why)=>{assert.ok(test,why);checks++;};
  ok(await page.locator('#devModeBadge').isHidden(),'Normal account has no dev badge');
  for(let i=0;i<4;i++)await page.locator('#tcgDevIcon').click();
  ok(!messages.some(x=>x.type==='hytcgDevToggle'),'Four icon clicks do not enable dev');
  await page.locator('#tcgDevIcon').click();await page.waitForFunction(()=>document.body.classList.contains('tcgDevMode'));
  ok(messages.filter(x=>x.type==='hytcgDevToggle').length===1,'Fifth click emits one native request');
  ok(await page.locator('#devModeBadge').isVisible(),'Dev badge visible after native init');
  for(const id of ['gemBalance','freeBalance','paidBalance','limitedBalance'])ok(await page.locator('#'+id).textContent()==='∞',id+' shows infinity');
  ok(await page.locator('#devModeClock').textContent()==='10:00','Native deadline starts at ten minutes');
  await page.waitForFunction(()=>/^09:5[89]$/.test(document.getElementById('devModeClock').textContent));ok(/^09:5[89]$/.test(await page.locator('#devModeClock').textContent()),'Countdown decreases monotonically');
  ok(await page.locator('#openTen').isEnabled(),'Open ten unlocked by native dev wallet');
  await page.screenshot({path:path.join(out,'dev-mode.png')});
  await page.locator('#devModeBadge').click();await page.waitForFunction(()=>!document.body.classList.contains('tcgDevMode')&&document.getElementById('gemBalance').textContent==='42');
  ok(await page.locator('#devModeBadge').isHidden(),'Exit restores native real scope and hides badge');
  ok(await page.locator('#paidBalance').textContent()==='1','Real credits restored after fresh document');
  ok(await page.locator('#limitedBalance').textContent()==='2','Real limited credits restored');
  // A canceled native transition is visible, then dismissed without entering dev.
  const old=messages.findLast(x=>x.type==='hytcgInit').session;
  await deliver({type:'hytcgDevState',session:'stale-session',nextSession:'wrong',dev:{active:true}});ok(await page.locator('#devModeBadge').isHidden(),'Stale scope response ignored');
  await deliver({type:'hytcgDevTransition',session:old});ok(await page.locator('#devModeTransition').isVisible(),'Session switch loading state visible');
  ok(await page.locator('.tcgShell').evaluate(x=>x.inert),'Old controls inert during transition');
  await deliver({type:'hytcgDevState',session:old,nextSession:old,dev:{active:false},error:'Busy'});await page.waitForFunction(()=>!document.querySelector('.tcgShell').inert);
  ok(await page.locator('#devModeTransition').isHidden(),'Failed transition restores usable real controls');
  for(const width of [375,500,1120]){await page.setViewportSize({width,height:840});active=true;remaining=30;await deliver({type:'hytcgConfig',session:old,config:{DropRates:[{Name:'Common',Weight:65,Color:'#94a3b8'}]},wallet:wallet(),dev:{active,remainingSeconds:remaining}});ok(await page.locator('.tcgHeader').evaluate(x=>x.scrollWidth<=x.clientWidth+1),'Header compact at '+width);}
  ok(errors.length===0,'No JavaScript errors: '+errors.join(';'));
  fs.writeFileSync(path.join(testRuntime.verificationRoot,'hytcg-dev-ui-result.json'),JSON.stringify({Result:'PASS',Checks:checks,Errors:errors,Screenshots:out},null,2));console.log(JSON.stringify({Result:'PASS',Checks:checks,Errors:errors}));
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
