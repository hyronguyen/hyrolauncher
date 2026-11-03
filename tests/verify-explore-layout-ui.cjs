'use strict';
const runtime=require('./test-runtime.cjs');runtime.prepare();
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict');
const {chromium}=runtime.playwright(),output=path.join(runtime.verificationRoot,'explore-layout-ui');
const server=http.createServer((request,response)=>{
 const name=decodeURIComponent(request.url.split('?')[0]);
 if(!/^\/ui\/[\w.-]+$/.test(name)){response.writeHead(404);response.end();return;}
 const file=path.join(runtime.projectRoot,name);if(!fs.existsSync(file)){response.writeHead(404);response.end();return;}
 response.writeHead(200,{'Content-Type':file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':'text/html'});response.end(fs.readFileSync(file));
});
(async()=>{
 fs.mkdirSync(output,{recursive:true});await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const check=(value,label)=>{assert(value,label);checks++;};
 try{
  const page=await browser.newPage({viewport:{width:1440,height:920}}),errors=[];
  page.on('pageerror',error=>errors.push(error.message));await page.route('**/*',route=>route.request().url().startsWith('http://127.0.0.1:')?route.continue():route.abort());
  await page.addInitScript(()=>{window.qa={messages:[],handlers:[],send(data){this.handlers.forEach(handler=>handler({data}));}};window.chrome=window.chrome||{};window.chrome.webview={addEventListener:(type,handler)=>{if(type==='message')qa.handlers.push(handler);},postMessage:message=>qa.messages.push(message)};});
  await page.goto('http://127.0.0.1:'+server.address().port+'/ui/index.html');
  await page.evaluate(()=>{
   const image=(name,index)=>'data:image/svg+xml,'+encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="900" height="500"><defs><linearGradient id="g" x2="1" y2="1"><stop stop-color="hsl(${index*41} 50% 43%)"/><stop offset="1" stop-color="#091329"/></linearGradient></defs><rect width="900" height="500" fill="url(#g)"/><circle cx="600" cy="250" r="150" fill="#ccd6fa" opacity=".4"/><path d="M0 450L220 160 380 350 900 150V500H0" fill="#101a32"/><text x="450" y="100" fill="white" text-anchor="middle" font-size="40" font-family="sans-serif">${name}</text></svg>`);
   qa.settings={LibraryLayout:'bookshelf',Language:'en',ThemeMode:'dark',AccentColor:'#ad61f4',SteamId:'76561198000000001',DisableBgm:true,MaxGridColumns:5,BookshelfBacklightMode:'rgb-cycle',BookshelfRgbSpeed:400,BookshelfLightingIntensity:60};
   qa.send({type:'state',state:{Games:[{Id:'local-fixture',Name:'Local game',IconPath:image('LOCAL',2),BannerPath:image('LOCAL',2),Category:'Games',Path:'C:\\Games\\fixture.exe'}],Categories:['Games'],Plugins:[],Settings:qa.settings},assets:{logo:image('HYRO',1),banner:image('HYRO',2)}});
   selectCategory('__explore');
   for(const mode of ['new','deals','upcoming','wishlist']){
    const request=qa.messages.findLast(message=>message.mode===mode || mode==='wishlist' && message.type==='getSteamWishlist');
    const games=Array.from({length:mode==='deals'?14:8},(_,index)=>({Id:index+100,Name:mode.toUpperCase()+' Game '+index,Image:image(mode.toUpperCase(),index),Source:mode==='deals'||mode==='wishlist'?'Steam':'IGDB',Price:mode==='deals'||mode==='wishlist'?index===0?'Free':'100.000 ₫':'',OriginalPrice:'400.000 ₫',Discount:mode==='deals'?75:0,ReleaseDate:'2026-10-12',Url:mode==='deals'||mode==='wishlist'?'https://store.steampowered.com/app/'+(index+100)+'/':'https://www.igdb.com/games/'+index,Summary:'A fixture adventure.'}));
    if(request)qa.send({type:'explore',requestId:request.requestId,snapshot:{Games:games,TotalCount:games.length,HasMore:mode==='wishlist',NextOffset:50}});
   }
  });
  await page.waitForSelector('#exploreLibrary .exploreCard');
  check(await page.locator('#exploreLibrary .exploreCard').count()===38,'home keeps every loaded discovery/deal/wishlist game');
  check(await page.locator('#exploreLibrary :is(.cabinetCases,.shelfCase,.exploreShelfCase)').count()===0,'bookshelf preference never creates physical cases in Explore');
  check(!(await page.locator('#grid').isVisible()),'main library shelf stays hidden on Explore');
  check(await page.locator('#exploreLibrary .exploreShelf').count()===4,'original discovery sections remain separate');
  check(await page.locator('#exploreLibrary .exploreArt').first().evaluate(art=>{const r=art.getBoundingClientRect();return r.width/r.height>1.7&&r.width/r.height<1.85;}),'default cards retain wide cover artwork');
  check(await page.locator('#exploreLibrary .exploreCardInfo strong').first().innerText()==='NEW Game 0','game title visible under cover');
  check(await page.locator('#exploreLibrary .exploreShelfHeader [aria-label="Scroll left"]').count()===4&&await page.locator('#exploreLibrary .exploreShelfHeader [aria-label="Scroll right"]').count()===4,'original shelf row arrows remain available');
  check(await page.locator('#exploreRow-new').evaluate(row=>getComputedStyle(row).overflowX==='auto'&&row.scrollWidth>row.clientWidth),'discovery section retains horizontal scrolling');
  await page.locator('#exploreRow-new .exploreCard').first().click();check(await page.locator('#exploreDetail.show').count()===1,'one click opens details immediately');
  check(await page.locator('#exploreDetail h2').innerText()==='NEW Game 0','original detail modal shows selected metadata');
  await page.locator('#exploreSourceBtn').click();check(await page.evaluate(()=>qa.messages.some(message=>message.type==='openExternal'&&message.target==='https://www.igdb.com/games/0')),'detail source action retains IGDB external link');
  await page.keyboard.press('Escape');check(await page.locator('#exploreDetail.show').count()===0,'Escape closes details');
  const dealText=await page.locator('#exploreRow-deals').innerText();check(dealText.includes('−75%')&&dealText.includes('100.000 ₫')&&dealText.includes('400.000 ₫')&&dealText.includes('Free'),'original deal cards retain discount/current/original/free prices');
  await page.locator('#exploreRow-wishlist .exploreCard').first().click();check(await page.locator('#exploreDetail.show').count()===1,'wishlist single click opens details');
  await page.locator('#exploreSourceBtn').click();check(await page.evaluate(()=>qa.messages.some(message=>message.type==='openExternal'&&message.target==='https://store.steampowered.com/app/100/')),'wishlist source action retains Steam link');await page.keyboard.press('Escape');
  await page.evaluate(()=>{exploreQuery='Original search';searchExplore();const request=qa.messages.at(-1);qa.send({type:'explore',requestId:request.requestId,snapshot:{Games:Array.from({length:7},(_,index)=>({Id:500+index,Name:'Search '+index,Source:'IGDB',Image:assets.logo,Url:'https://www.igdb.com/games/search-'+index}))}});});
  check(await page.locator('#exploreRow-search .exploreCard').count()===7&&await page.locator('#exploreRow-search').evaluate(row=>getComputedStyle(row).display==='grid'),'search uses original card grid');
  await page.evaluate(()=>openSteamDeals());
  await page.evaluate(()=>{const request=qa.messages.findLast(message=>message.type==='getSteamDeals');qa.send({type:'explore',requestId:request.requestId,snapshot:{Games:Array.from({length:16},(_,index)=>({Id:700+index,Name:'Shop '+index,Source:'Steam',Image:assets.logo,Price:'160.000 ₫',OriginalPrice:'400.000 ₫',Discount:60,Url:'https://store.steampowered.com/app/'+(700+index)+'/'})),HasMore:true,NextOffset:50}});});
  check(await page.locator('.steamDealsFilters').count()===1&&await page.locator('.steamDealsResults .exploreCard').count()===16,'full Steam deals keeps sidebar and original cards');
  check(await page.locator('.steamDealsResults .exploreGrid').evaluate(grid=>getComputedStyle(grid).display==='grid'),'Steam deals uses original responsive grid');
  await page.locator('#steamDealTerm').fill('unfinished filter');await page.evaluate(()=>loadNextSteamDeals());
  check(await page.evaluate(()=>qa.messages.findLast(message=>message.type==='getSteamDeals').offset)===50,'deals pagination request remains unchanged');
  await page.evaluate(()=>{const request=qa.messages.findLast(message=>message.type==='getSteamDeals');qa.send({type:'explore',requestId:request.requestId,snapshot:{Games:[{Id:999,Name:'Next page',Source:'Steam',Price:'200.000 ₫',Image:assets.logo}],HasMore:false}});});
  check(await page.locator('.steamDealsResults .exploreCard').count()===17&&await page.locator('#steamDealTerm').inputValue()==='unfinished filter','pagination preserves loaded cards and unsubmitted filter input');
  for(const width of [1280,900,640,440]){
   await page.setViewportSize({width,height:850});await page.waitForTimeout(150);
   check(await page.locator('#libraryBody').evaluate(body=>body.scrollWidth<=body.clientWidth+2),'no horizontal page overflow at '+width);
   check(await page.locator('.steamDealsResults .exploreCard').first().evaluate(card=>getComputedStyle(card).display!=='none'&&card.querySelector('.exploreArt img').getBoundingClientRect().width>100),'original cards remain readable at '+width);
   check(await page.locator('#exploreLibrary .shelfCase').count()===0,'no physical shelf in phone/deals layout '+width);
  }
  await page.setViewportSize({width:1440,height:920});await page.evaluate(()=>closeSteamDeals());await page.waitForTimeout(100);
  await page.screenshot({path:path.join(output,'explore-original-bookshelf-setting.png')});
  for(const layout of ['default','carousel','bookshelf']){
   await page.evaluate(layout=>{state.Settings.LibraryLayout=layout;renderExplore();},layout);
   check(await page.locator('#exploreLibrary .exploreCard').count()===38&&await page.locator('#exploreLibrary .shelfCase').count()===0,'Explore remains original for '+layout+' preference');
  }
  await page.evaluate(()=>{qa.send({type:'bigPicture',enabled:true});selectCategory('__explore');});await page.waitForTimeout(150);
  check(await page.locator('body.bigPicture').count()===1&&await page.locator('#exploreLibrary .exploreCard').count()===38&&await page.locator('#exploreLibrary .shelfCase').count()===0,'Big Picture Explore retains original cover cards');
  await page.evaluate(()=>focusBigPicture(document.querySelector('#exploreRow-new .exploreCard')));await page.keyboard.press('Enter');
  check(await page.locator('#exploreDetail.show').count()===1,'Big Picture selects original card with a single controller accept');await page.keyboard.press('Escape');
  await page.evaluate(()=>selectCategory('All'));await page.waitForTimeout(150);
  check(await page.locator('#grid .shelfCase').count()===1&&await page.locator('#grid .shelfCaseSpine').count()===1,'main library remains 3D bookshelf in Big Picture');
  await page.evaluate(()=>selectCategory('__explore'));await page.waitForTimeout(100);
  check(!(await page.locator('#grid').isVisible())&&await page.locator('#exploreLibrary .exploreCard').count()===38,'returning to Explore switches back to original cards');
  check(errors.length===0,'no page errors: '+errors.join('; '));
  fs.writeFileSync(path.join(output,'report.json'),JSON.stringify({status:'PASS',checks,errors,productionDataAccessed:false},null,2));console.log('PASS: '+checks+' original Explore layout checks.');
 }finally{await browser.close();server.close();}
})().catch(error=>{console.error(error);server.close();process.exitCode=1;});
