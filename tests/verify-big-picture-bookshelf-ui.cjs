'use strict';
const runtime=require('./test-runtime.cjs');runtime.prepare();
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict');
const {chromium}=runtime.playwright(),out=path.join(runtime.verificationRoot,'big-picture-bookshelf');
const server=http.createServer((request,response)=>{
 const name=decodeURIComponent(request.url.split('?')[0]);
 if(!/^\/ui\/[\w.-]+$/.test(name)){response.writeHead(404);response.end();return;}
 const file=path.join(runtime.projectRoot,name);if(!fs.existsSync(file)){response.writeHead(404);response.end();return;}
 response.writeHead(200,{'Content-Type':file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':'text/html'});response.end(fs.readFileSync(file));
});
(async()=>{
 fs.mkdirSync(out,{recursive:true});await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;
 const check=(value,name)=>{assert(value,name);checks++;};
 try{
  const page=await browser.newPage({viewport:{width:1920,height:1080},deviceScaleFactor:1}),errors=[];
  page.on('pageerror',error=>errors.push(error.message));await page.route('**/*',route=>route.request().url().startsWith('http://127.0.0.1:')?route.continue():route.abort());
  await page.addInitScript(()=>{window.qa={messages:[],handlers:[],send(data){this.handlers.forEach(handler=>handler({data}));}};window.chrome=window.chrome||{};window.chrome.webview={addEventListener:(type,handler)=>{if(type==='message')qa.handlers.push(handler);},postMessage:message=>qa.messages.push(message)};});
  await page.goto('http://127.0.0.1:'+server.address().port+'/ui/index.html');
  await page.evaluate(()=>{
   const art=(name,i)=>'data:image/svg+xml,'+encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="600" height="840"><defs><linearGradient id="g" x2="1" y2="1"><stop stop-color="${['#7736ba','#286ab0','#269173','#ae475b'][i%4]}"/><stop offset="1" stop-color="#091022"/></linearGradient></defs><rect width="600" height="840" fill="url(#g)"/><circle cx="300" cy="300" r="160" fill="#c6d0f433"/><path d="M30 840 250 330 450 840" fill="#080d28"/><text x="300" y="150" text-anchor="middle" font-family="sans-serif" font-size="35" fill="#fff">${name}</text></svg>`);
   qa.art=art;qa.games=Array.from({length:36},(_,i)=>({Id:'bp-game-'+i,Name:'Shelf adventure '+String(i+1).padStart(2,'0'),IconPath:art('Adventure '+(i+1),i),BannerPath:art('Adventure '+(i+1),i),Path:'D:\\Games\\Fixture'+i+'.exe',Category:i>=28?'Tools':'Games',Favorite:i<6,Private:i===34,IgdbId:60000+i,IsUrl:false}));
   qa.settings={LibraryLayout:'bookshelf',AccentColor:'#ad61f4',Language:'en',ThemeMode:'dark',DisableBgm:true,DisableEffects:false,MaxGridColumns:5,CardStyle:'icon',SteamId:'76561198000000000',BookshelfPullDuration:300};
   qa.assets={logo:art('HYRO',0),banner:art('HYRO',0),svgPlayApp:'<svg viewBox="0 0 24 24"><path d="M7 4v16l14-8Z" fill="currentColor"/></svg>'};
   qa.send({type:'state',state:{Games:qa.games,Settings:qa.settings,Categories:['Games','Tools'],Plugins:[]},assets:qa.assets});
   qa.send({type:'steamLibrary',steamId:qa.settings.SteamId,status:'ready',games:[{AppId:123,Name:'Steam controller fixture',Image:art('Steam fixture',2),PlaytimeMinutes:123}],profile:{Name:'QA fixture'}});
   qa.send({type:'bigPicture',enabled:true});
  });
  await page.waitForSelector('.bigPicture .bookshelfLibrary .shelfCaseFace');await page.waitForTimeout(200);
  check(await page.evaluate(()=>bigPictureActive && isBookshelfLayout() && !isCarouselLayout() && state.Settings.LibraryLayout==='bookshelf'),'Big Picture preserves bookshelf setting and render dispatch');
  check(await page.locator('.carouselLayout,.carouselHero').count()===0,'Big Picture does not force a carousel');
  check(await page.locator('.shelfCase').count()===37,'Big Picture keeps all local, private and Steam cases');
  check(await page.evaluate(()=>document.activeElement.matches('.shelfCaseFace')),'initial controller focus lands on a case');
  const initial=await page.evaluate(()=>document.activeElement.dataset.bookId);
  await page.keyboard.press('ArrowRight');check(await page.evaluate(first=>document.activeElement.matches('.shelfCaseFace')&&document.activeElement.dataset.bookId!==first,initial),'keyboard Right moves between fixed shelf slots');
  const selected=await page.evaluate(()=>document.activeElement.dataset.bookId);
  await page.keyboard.press('Enter');await page.waitForTimeout(360);
  check(await page.locator('[data-book-case="'+selected+'"].selected').count()===1,'keyboard Enter pulls the focused case out');
  check(await page.locator('#bookshelfDetail').getAttribute('data-game-id')===selected,'selection updates the right information pane');
  await page.evaluate(id=>qa.send({type:'igdbInfo',id:60000+Number(id.split('-').at(-1)),gameId:id,info:{Id:60000+Number(id.split('-').at(-1)),Name:qa.games.find(g=>g.Id===id).Name,Summary:Array(55).fill('A long fixture description is contained in its own scrolling information panel.').join(' '),Rating:88.2,ReleaseDate:'2025-01-12',Genres:['Action','Adventure','Role-playing'],Screenshots:[qa.art('Scene fixture',1)]},error:null}),selected);
  check(await page.evaluate(id=>document.activeElement.dataset.bookId===id,selected),'late metadata refresh retains case focus');
  check((await page.locator('#bookshelfDetail').innerText()).includes('88.2/100'),'selected game metadata remains readable in Big Picture');
  await page.evaluate(()=>favoriteBigPictureCard());check(await page.evaluate(id=>qa.messages.some(m=>m.type==='saveGame'&&m.game.Id===id),selected),'controller Favorite uses shelf game ID');
  await page.evaluate(()=>qa.send({type:'state',state:{Games:qa.games,Settings:qa.settings,Categories:['Games','Tools'],Plugins:[]},assets:qa.assets}));
  check(await page.evaluate(id=>document.activeElement.dataset.bookId===id,selected),'reordered favorites retain stable case focus');
  await page.keyboard.press('Escape');await page.waitForTimeout(360);check(await page.locator('.shelfCase.selected').count()===0,'Back retracts a selected case before changing category');
  check(await page.evaluate(id=>document.activeElement.dataset.bookId===id,selected),'Back leaves focus on the returned case');
  await page.evaluate(()=>{processBigPicturePad(nativeToGamepad({buttons:4096}),1000);processBigPicturePad(nativeToGamepad({buttons:4096}),1400);processBigPicturePad(nativeToGamepad({buttons:0}),1410);});await page.waitForTimeout(360);
  check(await page.locator('[data-book-case="'+selected+'"].selected').count()===1,'controller A pulls out once while held');
  await page.locator('#bookshelfDetail [data-book-action="play"]').focus();await page.keyboard.press('Enter');
  check(await page.evaluate(id=>qa.messages.filter(m=>m.type==='launch'&&m.id===id).length===1,selected),'focused Play keeps native launch IPC (mocked)');
  await page.locator('#bookshelfDetail [data-book-action="details"]').focus();await page.keyboard.press('Enter');
  check(!await page.locator('#library').evaluate(el=>el.classList.contains('show')),'Details opens the existing game screen');
  await page.keyboard.press('Escape');await page.waitForTimeout(160);
  check(await page.locator('#library').evaluate(el=>el.classList.contains('show')),'Back restores the same shelf library');
  check(await page.evaluate(id=>document.activeElement.closest('#bookshelfDetail')?.dataset.gameId===id || document.activeElement.dataset.bookId===id,selected),'game-screen Back restores previous library control');
  await page.evaluate(()=>switchBigPictureTab(1));check(await page.evaluate(()=>currentCategory==='Favorite'),'controller shoulder changes shelf category');
  await page.evaluate(()=>switchBigPictureTab(-1));check(await page.evaluate(()=>currentCategory==='All'),'reverse shoulder returns All');
  const reports=[];
  for(const viewport of [{width:1920,height:1080},{width:1280,height:720},{width:900,height:650},{width:540,height:720}]){
   await page.setViewportSize(viewport);await page.waitForTimeout(250);
   await page.evaluate(id=>{selectCategory('All');bookshelfPreviewId=id;setBookshelfSelection(id);renderBookshelfDetail();document.getElementById('libraryBody').scrollTop=0;},selected);await page.waitForTimeout(360);
   const bounds=await page.evaluate(()=>{
    const rect=el=>{const r=el.getBoundingClientRect();return {x:r.x,y:r.y,right:r.right,bottom:r.bottom,width:r.width,height:r.height};};
    const footer=document.getElementById('bigPictureFooter'),body=document.getElementById('libraryBody'),panel=document.getElementById('bookshelfDetail'),play=panel.querySelector('.cabinetPlay'),hints=[...footer.querySelectorAll('.bigPictureHints>span')];
    return {footer:rect(footer),library:rect(document.getElementById('library')),body:rect(body),panel:rect(panel),play:rect(play),hints:hints.map(rect),bodyScroll:body.scrollHeight>body.clientHeight,documentWidth:document.documentElement.scrollWidth,viewportWidth:innerWidth};
   });
   check(bounds.library.bottom<=bounds.footer.y+1,'library reserves controller footer at '+viewport.width+'×'+viewport.height);
   check(bounds.play.bottom<=Math.min(bounds.body.bottom,bounds.footer.y)+1,'detail Play stays above footer at '+viewport.width+'×'+viewport.height);
   check(bounds.hints.every(r=>r.y>=bounds.footer.y-1&&r.bottom<=bounds.footer.bottom+1),'all controller hints fit footer at '+viewport.width+'×'+viewport.height);
   check(bounds.documentWidth<=bounds.viewportWidth,'no page horizontal overflow at '+viewport.width+'×'+viewport.height);
   check(bounds.bodyScroll,'multiple shelf rows preserve vertical scroll at '+viewport.width+'×'+viewport.height);
   reports.push({viewport,...bounds});await page.screenshot({path:path.join(out,'shelf-'+viewport.width+'x'+viewport.height+'.png')});
  }
  await page.setViewportSize({width:1280,height:720});await page.waitForTimeout(200);
  await page.evaluate(()=>{
   activeExploreSections().forEach(([mode])=>exploreCache.set(exploreKey(mode),{Games:Array.from({length:8},(_,i)=>({Id:70000+i,Name:mode+' discovery '+i,Source:mode==='deals'||mode==='wishlist'?'Steam':'IGDB',Image:qa.art('Discovery '+i,i),Url:'https://www.igdb.com/games/fixture',Price:mode==='deals'?'120.000₫':'',OriginalPrice:'240.000₫',Discount:mode==='deals'?50:0})),HasMore:false}));
   selectCategory('__explore');focusBigPictureHome();
  });
  check(await page.locator('#exploreLibrary .exploreCard').count()===32,'Explore preserves all ordinary cards with the library bookshelf setting');
  check(await page.locator('#exploreLibrary .shelfCase,#exploreLibrary.exploreBookshelf').count()===0,'Explore has no 3D shelf cases in Big Picture');
  check(await page.evaluate(()=>document.activeElement.matches('.exploreCard:not(.shelfCaseFace)')),'Explore home controller focus lands on an ordinary card');
  const exploreInitial=await page.evaluate(()=>bigPictureKey(document.activeElement));
  await page.keyboard.press('ArrowRight');
  check(await page.evaluate(key=>document.activeElement.matches('.exploreCard')&&bigPictureKey(document.activeElement)!==key,exploreInitial),'controller navigation moves between ordinary Explore cards');
  const exploreFocused=await page.evaluate(()=>bigPictureKey(document.activeElement));
  await page.keyboard.press('Enter');
  check(await page.locator('#exploreDetail.show').count()===1,'Explore first A opens the existing detail dialog directly');
  await page.keyboard.press('Escape');check(await page.locator('#exploreDetail.show').count()===0,'controller Back closes Explore dialog');
  check(await page.evaluate(key=>document.activeElement.matches('.exploreCard')&&bigPictureKey(document.activeElement)===key,exploreFocused),'Explore dialog Back restores the same ordinary card focus');
  await page.evaluate(()=>{
   requestExplore('new',true);const request=explorePending.get(exploreKey('new')),snapshot=exploreCache.get(exploreKey('new'));
   qa.send({type:'explore',requestId:request.requestId,snapshot:{...snapshot,Games:[...snapshot.Games].reverse()}});
  });
  check(await page.evaluate(key=>document.activeElement.matches('.exploreCard')&&bigPictureKey(document.activeElement)===key,exploreFocused),'late Explore API reordering preserves focus by source/id rather than list index');
  await page.evaluate(()=>{processBigPicturePad(nativeToGamepad({buttons:4096}),2200);processBigPicturePad(nativeToGamepad({buttons:4096}),2600);processBigPicturePad(nativeToGamepad({buttons:0}),2610);});
  check(await page.locator('#exploreDetail.show').count()===1,'controller A directly activates an ordinary Explore card once while held');
  await page.evaluate(()=>{processBigPicturePad(nativeToGamepad({buttons:8192}),2700);processBigPicturePad(nativeToGamepad({buttons:0}),2710);});
  check(await page.locator('#exploreDetail.show').count()===0&&await page.evaluate(key=>bigPictureKey(document.activeElement)===key,exploreFocused),'controller B closes ordinary Explore details and restores its card');
  await page.screenshot({path:path.join(out,'explore-cards-1280x720.png')});
  await page.evaluate(()=>bigPictureSearch());check(await page.locator('#bigPictureKeyboard.show').count()===1,'controller Search opens virtual keyboard');
  await page.keyboard.press('Escape');check(await page.locator('#bigPictureKeyboard.show').count()===0,'Back closes virtual keyboard');
  await page.keyboard.press('Escape');check(await page.evaluate(()=>currentCategory==='All'&&isBookshelfLayout()),'Explore home Back returns directly to the unchanged 3D library');
  await page.evaluate(()=>qa.send({type:'bigPicture',enabled:false}));check(await page.evaluate(()=>!bigPictureActive && isBookshelfLayout() && state.Settings.LibraryLayout==='bookshelf'),'exiting Big Picture retains shelf setting');
  await page.evaluate(()=>{qa.settings.LibraryLayout='default';qa.send({type:'state',state:{Games:qa.games,Settings:qa.settings,Categories:['Games','Tools'],Plugins:[]},assets:qa.assets});qa.send({type:'bigPicture',enabled:true});});
  check(await page.evaluate(()=>bigPictureActive && isCarouselLayout() && !isBookshelfLayout() && state.Settings.LibraryLayout==='default'),'other layouts keep the existing temporary Big Picture carousel');
  await page.evaluate(()=>qa.send({type:'bigPicture',enabled:false}));check(await page.evaluate(()=>!isCarouselLayout()&&state.Settings.LibraryLayout==='default'),'carousel override restores default on exit');
  check(errors.length===0,'no browser runtime errors: '+errors.join('; '));
  fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({checks,reports,errors},null,2));console.log('PASS: '+checks+' Big Picture bookshelf checks. Isolated mock native calls; no game or account modified.');
 }finally{await browser.close();server.close();}
})().catch(error=>{console.error(error);server.close();process.exitCode=1;});
