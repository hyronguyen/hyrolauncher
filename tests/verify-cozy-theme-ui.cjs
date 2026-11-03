'use strict';
const runtime=require('./test-runtime.cjs');runtime.prepare();
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict');
const {chromium}=runtime.playwright(),output=path.join(runtime.verificationRoot,'cozy-theme-ui');
const server=http.createServer((request,response)=>{
 const name=decodeURIComponent(request.url.split('?')[0]);
 if(!/^\/ui\/[\w.-]+$/.test(name)){response.writeHead(404);response.end();return;}
 const file=path.join(runtime.projectRoot,name);if(!fs.existsSync(file)){response.writeHead(404);response.end();return;}
 response.writeHead(200,{'Content-Type':file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':'text/html'});response.end(fs.readFileSync(file));
});
(async()=>{
 fs.mkdirSync(output,{recursive:true});await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const browser=await chromium.launch({channel:'msedge',headless:true});let page,passed=false;const checks=[],reports=[],errors=[];
 const check=(value,label)=>{assert(value,label);checks.push(label);};
 try{
  page=await browser.newPage({viewport:{width:1280,height:800},deviceScaleFactor:1});
  page.on('pageerror',error=>errors.push(error.message));await page.route('**/*',route=>route.request().url().startsWith('http://127.0.0.1:')?route.continue():route.abort());
  await page.addInitScript(()=>{
   window.qa={messages:[],handlers:[],send(data){this.handlers.forEach(handler=>handler({data}));}};
   window.chrome=window.chrome||{};window.chrome.webview={addEventListener:(type,handler)=>{if(type==='message')qa.handlers.push(handler);},postMessage:message=>{const copy=JSON.parse(JSON.stringify(message));qa.messages.push(copy);if(copy.type==='saveSettings')qa.saved=copy.settings;}};
   qa.color=value=>{const canvas=document.createElement('canvas');canvas.width=canvas.height=1;const context=canvas.getContext('2d');context.fillStyle=value;context.fillRect(0,0,1,1);return [...context.getImageData(0,0,1,1).data];};
   qa.luminance=rgb=>rgb.slice(0,3).map(v=>{v/=255;return v<=.04045?v/12.92:Math.pow((v+.055)/1.055,2.4);}).reduce((sum,v,i)=>sum+v*[.2126,.7152,.0722][i],0);
   qa.contrast=(foreground,background)=>{const a=qa.luminance(qa.color(foreground)),b=qa.luminance(qa.color(background));return (Math.max(a,b)+.05)/(Math.min(a,b)+.05);};
  });
  const loadFixture=async settings=>page.evaluate(settings=>{
   const art=(name,index)=>'data:image/svg+xml,'+encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="600" height="840"><defs><linearGradient id="g" x2="1" y2="1"><stop stop-color="${['#356653','#9b6453','#78769a','#a78241'][index%4]}"/><stop offset="1" stop-color="#21303a"/></linearGradient></defs><rect width="600" height="840" fill="url(#g)"/><circle cx="300" cy="300" r="145" fill="#fff7da44"/><path d="M0 840 230 400 430 840" fill="#1b2c33"/><text x="300" y="120" text-anchor="middle" font-family="sans-serif" font-size="34" fill="#fff6e8">${name}</text></svg>`);
   qa.art=art;qa.games=Array.from({length:28},(_,i)=>({Id:'cozy-game-'+i,Name:['Moonlit Meadow','Tea & Tales','Quiet Skies','Golden Trails'][i%4]+' '+String(i+1).padStart(2,'0'),IconPath:art('Adventure '+(i+1),i),BannerPath:art('Adventure '+(i+1),i),Path:'D:\\Fixture\\Game'+i+'.exe',Category:i>=22?'Tools':'Games',Favorite:i<5,Private:false,IgdbId:80000+i,IsUrl:false}));
   qa.settings={LibraryLayout:'default',AccentColor:'#ad61f4',Language:'en',ThemeMode:'dark',DisableBgm:true,DisableEffects:false,MaxGridColumns:5,CardStyle:'2d',CarouselColumns:5,SteamId:'',BookshelfPullDuration:600,BookshelfBacklightMode:'accent',BookshelfRgbSpeed:400,BookshelfLightingIntensity:65,...settings};
   qa.assets={logo:art('HYRO',0),banner:art('HYRO',0),svgPlayApp:'<svg viewBox="0 0 24 24"><path d="M7 4v16l14-8Z" fill="currentColor"/></svg>'};
   qa.send({type:'state',state:{Games:qa.games,Settings:qa.settings,Categories:['Games','Tools'],Plugins:[]},assets:qa.assets});showLibrary(false);
  },settings||{});
  const sendSettings=async settings=>page.evaluate(settings=>{
   Object.assign(qa.settings,settings);qa.send({type:'state',state:{Games:qa.games,Settings:qa.settings,Categories:['Games','Tools'],Plugins:[]},assets:qa.assets});showLibrary(false);
  },settings);
  const surface=async selector=>page.locator(selector).first().evaluate(el=>{
   const style=getComputedStyle(el),background=style.backgroundColor,rgba=qa.color(background);
   return {selector:el.className,color:style.color,background,image:style.backgroundImage,rgba,warm:rgba[0]>rgba[2]+12,contrast:qa.contrast(style.color,background),animation:style.animationName,filter:style.filter};
  });
  const screenshot=async name=>{await page.waitForTimeout(180);await page.screenshot({path:path.join(output,name+'.png')});};
  await page.goto('http://127.0.0.1:'+server.address().port+'/ui/index.html');await loadFixture();await page.waitForSelector('#grid .cardFace');
  const darkBaseline=await surface('#library');await screenshot('default-dark-baseline');
  await page.evaluate(()=>openAppSettings());
  check(await page.locator('#sThemeMode option[value="cozy"]').count()===1,'Cozy is offered as a separate launcher theme');
  check(/cozy/i.test(await page.locator('#sThemeMode option[value="cozy"]').innerText()),'Cozy option has a readable English label');
  await page.locator('#sThemeMode').selectOption('cozy');await page.locator('#saveAppSettingsBtn').click();
  check(await page.evaluate(()=>qa.saved?.ThemeMode==='cozy'&&qa.messages.findLast(m=>m.type==='saveSettings').settings.ThemeMode==='cozy'),'theme selection saves cozy through native settings IPC');
  check(await page.evaluate(()=>state.Settings.ThemeMode==='cozy'&&document.body.classList.contains('theme-cozy')&&document.getElementById('app').classList.contains('theme-cozy')),'saving applies Cozy to body and app, including sibling dialogs');
  check(await page.locator('#appSettingsModal.show').count()===0,'saving Cozy closes settings normally');
  check(await page.evaluate(()=>qa.messages.findLast(m=>m.type==='setWindowVisuals')?.themeMode==='cozy'),'native window visuals receive the new mode');
  const saved=await page.evaluate(()=>qa.saved);await page.reload();await loadFixture(saved);await page.waitForSelector('#grid .cardFace');
  check(await page.evaluate(()=>state.Settings.ThemeMode==='cozy'&&document.body.classList.contains('theme-cozy')),'reloading renderer with persisted native settings retains Cozy');
  await page.evaluate(()=>openAppSettings());check(await page.locator('#sThemeMode').inputValue()==='cozy','settings reopen with saved Cozy selection');await page.evaluate(()=>closeAppSettings());
  for(const layout of ['default','carousel','bookshelf']){
   await page.setViewportSize({width:1280,height:800});await sendSettings({LibraryLayout:layout,ThemeMode:'cozy'});await page.waitForTimeout(220);
   const type=layout==='bookshelf'?'.shelfCaseFace':'.cardFace';await page.waitForSelector('#grid '+type);
   const library=await surface('#library');reports.push({layout,library});
   check(library.warm&&library.background!==darkBaseline.background,'Cozy warms the '+layout+' library surface');
   check(await page.evaluate(()=>state.Settings.ThemeMode==='cozy'&&!document.body.classList.contains('theme-light')&&!document.body.classList.contains('theme-translucent')),'Cozy remains independent of other modes in '+layout);
   check(await page.locator('#grid img').evaluateAll(images=>{const allowed=new Set(qa.games.flatMap(game=>[game.IconPath,game.BannerPath]).concat(assets.logo,assets.banner));return images.length>0&&images.every(image=>allowed.has(image.getAttribute('src')));}),layout+' preserves original artwork assets');
   check(layout==='bookshelf'?await page.locator('#grid .shelfCase').count()===28:await page.locator('#grid .shelfCase').count()===0,'theme preserves the '+layout+' rendering architecture');
   await screenshot(layout+'-cozy-wide');
   for(const width of [900,440]){
    await page.setViewportSize({width,height:800});await page.waitForTimeout(160);
    const bounds=await page.evaluate(()=>{const body=document.getElementById('libraryBody'),r=body.getBoundingClientRect();return {width:innerWidth,document:document.documentElement.scrollWidth,bodyWidth:body.clientWidth,bodyScroll:body.scrollWidth,top:r.top,bottom:r.bottom,height:innerHeight};});
    reports.push({layout,width,bounds});check(bounds.document<=width+2&&bounds.bodyScroll<=bounds.bodyWidth+2,'no Cozy horizontal overflow in '+layout+' at '+width);
    check(bounds.top>=0&&bounds.bottom<=bounds.height+1,'Cozy library viewport stays visible in '+layout+' at '+width);
    check(await page.locator('#grid '+type).first().isVisible(),'game controls remain visible in '+layout+' at '+width);
    await screenshot(layout+'-cozy-'+width);
   }
  }
  await page.setViewportSize({width:1280,height:800});await sendSettings({LibraryLayout:'bookshelf',ThemeMode:'cozy'});await page.waitForTimeout(180);
  const wood=await page.locator('.cabinetCases').first().evaluate(row=>{const s=getComputedStyle(row),edge=getComputedStyle(row,'::after');return {background:s.backgroundImage,border:qa.color(s.borderLeftColor),animation:s.animationName,filter:s.filter,edge:edge.backgroundImage};});
  reports.push({wood});check(wood.border[0]>wood.border[2]+12&&wood.background!=='none'&&wood.edge!=='none','wooden shelves have warm depth, grain and a physical front edge');
  check(wood.animation==='none'&&wood.filter==='none','wooden shelf surface stays static without whole-shelf color filters');
  const slot=async()=>page.locator('[data-book-case="cozy-game-5"]').evaluate(book=>{const face=book.querySelector('.shelfCaseFace'),r=book.getBoundingClientRect(),row=book.parentElement.getBoundingClientRect();return {transform:getComputedStyle(face).transform,x:r.x,y:r.y,width:r.width,rowTop:row.top,scroll:document.getElementById('libraryBody').scrollTop,src:face.querySelector('img').getAttribute('src'),neighbors:[...book.parentElement.children].map(el=>{const b=el.getBoundingClientRect();return {id:el.dataset.bookCase,x:b.x-row.x,y:b.y-row.y,width:b.width,height:b.height};})};});
  await page.mouse.move(10,10);await page.evaluate(()=>{document.activeElement.blur();document.getElementById('libraryBody').scrollTop=0;});const closed=await slot();
  await page.locator('[data-book-case="cozy-game-5"] .shelfCaseFace').click();await page.waitForTimeout(100);const moving=await slot();await page.waitForTimeout(610);const opened=await slot();
  check(closed.transform!==moving.transform&&moving.transform!==opened.transform&&closed.transform!==opened.transform,'Cozy preserves intermediate 3D pull-out animation before its end position');
  reports.push({pullOut:{closed,moving,opened}});
  check(JSON.stringify(closed.neighbors)===JSON.stringify(opened.neighbors),'pulling a Cozy case keeps all shelf slots and neighbors stable');
  check(closed.src===opened.src,'Cozy pull-out retains the original case artwork');
  check(await page.locator('#bookshelfDetail').getAttribute('data-game-id')==='cozy-game-5','Cozy case selection updates the existing information pane');
  await page.locator('#bookshelfDetail [data-book-action="play"]').click();check(await page.evaluate(()=>qa.messages.some(m=>m.type==='launch'&&m.id==='cozy-game-5')),'Cozy keeps native Play behavior (mocked only)');
  const detail=await surface('#bookshelfDetail');reports.push({detail});check(detail.warm,'Cozy warms the shelf information pane');
  await screenshot('bookshelf-cozy-selected');
  await page.evaluate(()=>{state.Settings.BookshelfBacklightMode='rgb-cycle';state.Settings.BookshelfLightingIntensity=65;applyBookshelfSettings();});
  const lamp=async()=>page.locator('.cabinetCases').first().evaluate(row=>{const base=getComputedStyle(row),led=getComputedStyle(row,'::before');return {wood:base.backgroundImage,woodFilter:base.filter,woodAnimation:base.animationName,lampFilter:led.filter,lampPosition:led.backgroundPosition,lampAnimation:led.animationName,opacity:Number(led.opacity)};});
  await page.waitForTimeout(100);const rgbA=await lamp();await page.waitForTimeout(250);const rgbB=await lamp();
  check(rgbA.lampAnimation!=='none'&&(rgbA.lampFilter!==rgbB.lampFilter||rgbA.lampPosition!==rgbB.lampPosition),'RGB lamp continues moving over Cozy shelves');
  check(rgbA.wood===rgbB.wood&&rgbB.woodFilter==='none'&&rgbB.woodAnimation==='none','RGB movement preserves static wooden grain and artwork colors');
  await page.evaluate(()=>{state.Settings.BookshelfLightingIntensity=0;applyBookshelfSettings();});const dim=await lamp();await page.evaluate(()=>{state.Settings.BookshelfLightingIntensity=100;applyBookshelfSettings();});const bright=await lamp();
  check(dim.opacity===0&&bright.opacity>dim.opacity+.4,'Cozy shelf lighting retains dim/off and bright controls');reports.push({rgbA,rgbB,dim,bright});
  await page.evaluate(()=>{
   activeExploreSections().forEach(([mode])=>exploreCache.set(exploreKey(mode),{Games:Array.from({length:6},(_,i)=>({Id:90000+i,Name:mode+' discovery '+i,Source:mode==='deals'?'Steam':'IGDB',Image:qa.art('Discovery '+i,i),Url:'https://www.igdb.com/games/fixture',Price:mode==='deals'?'120.000₫':'',OriginalPrice:'240.000₫',Discount:mode==='deals'?50:0})),HasMore:false}));selectCategory('__explore');
  });
  const explore=await surface('.exploreCard');reports.push({explore});check(explore.warm,'ordinary Explore cards receive Cozy colors');
  check(await page.locator('#exploreLibrary .exploreCard').count()===18&&await page.locator('#exploreLibrary .shelfCase').count()===0,'Cozy Explore remains ordinary cover cards');
  await screenshot('explore-cozy');await page.locator('#exploreRow-new .exploreCard').first().click();
  const exploreDialog=await surface('#exploreDetail .dialog');check(exploreDialog.warm&&exploreDialog.contrast>=4.5,'body-sibling Explore dialog uses warm readable Cozy colors');await screenshot('explore-dialog-cozy');await page.keyboard.press('Escape');
  await page.evaluate(()=>openSystemInfo());await page.evaluate(()=>qa.send({type:'systemInfo',info:{MachineName:'Cozy fixture',Cpu:'Fixture CPU',Memory:'32 GB',LauncherStorageSize:'2 GB',ReclaimableSize:'128 MB',CardsSize:'32 MB',CacheCategories:[{Id:'igdb',Files:1,Size:'128 MB'}]}}));
  const systemDialog=await surface('#systemModal .systemDialog'),cache=await surface('.systemCachePanel');reports.push({systemDialog,cache});
  check(systemDialog.warm&&cache.warm,'body-sibling System Info and cache panel use Cozy surfaces');
  check(systemDialog.contrast>=4.5&&cache.contrast>=4.5,'Cozy dialog and cache text retain readable contrast');await screenshot('system-dialog-cozy');await page.evaluate(()=>closeSystemInfo());
  await page.evaluate(()=>openAppSettings());const settingsDialog=await surface('#appSettingsModal .dialog');check(settingsDialog.warm&&settingsDialog.contrast>=4.5,'body-sibling Settings dialog receives warm readable Cozy colors');await screenshot('settings-dialog-cozy');await page.evaluate(()=>closeAppSettings());
  for(const layout of ['default','bookshelf']){
   await sendSettings({ThemeMode:'cozy',LibraryLayout:layout});await page.evaluate(()=>qa.send({type:'bigPicture',enabled:true}));await page.waitForTimeout(180);
   const library=await surface('#library'),footer=await surface('#bigPictureFooter');reports.push({bigPicture:layout,library,footer});
   check(library.warm&&footer.warm,'Big Picture retains Cozy library/footer colors for '+layout);
   check(await page.evaluate(layout=>layout==='bookshelf'?isBookshelfLayout()&&!isCarouselLayout():isCarouselLayout(),layout),'Cozy preserves Big Picture '+layout+' rendering dispatch');
   const footerBounds=await page.evaluate(()=>{const a=document.getElementById('library').getBoundingClientRect(),b=document.getElementById('bigPictureFooter').getBoundingClientRect();return {library:a.bottom,footer:b.top};});
   check(footerBounds.library<=footerBounds.footer+1,'Cozy Big Picture reserves its controller footer for '+layout);await screenshot('big-picture-'+layout+'-cozy');
   await page.evaluate(()=>{selectCategory('__explore');focusBigPictureHome();});check(await page.locator('#exploreLibrary .exploreCard').count()===18&&await page.locator('#exploreLibrary .shelfCase').count()===0,'Big Picture Cozy Explore preserves original cards for '+layout);
   await page.keyboard.press('Enter');check(await page.locator('#exploreDetail.show').count()===1,'Big Picture Cozy directly activates an Explore card for '+layout);await page.keyboard.press('Escape');await page.evaluate(()=>qa.send({type:'bigPicture',enabled:false}));
  }
  await sendSettings({ThemeMode:'dark',LibraryLayout:'default'});await page.waitForTimeout(180);const darkAgain=await surface('#library');
  check(!await page.evaluate(()=>document.body.classList.contains('theme-cozy')||document.getElementById('app').classList.contains('theme-cozy')),'switching away removes Cozy body/app classes');
  check(darkAgain.background===darkBaseline.background&&darkAgain.image===darkBaseline.image,'original Dark colors return unchanged after Cozy');
  await sendSettings({ThemeMode:'light'});check(await page.evaluate(()=>document.body.classList.contains('theme-light')&&!document.body.classList.contains('theme-cozy')),'Light remains a separate existing mode');
  await sendSettings({ThemeMode:'translucent'});check(await page.evaluate(()=>document.body.classList.contains('theme-translucent')&&!document.body.classList.contains('theme-cozy')),'Translucent remains a separate existing mode');
  check(errors.length===0,'no browser runtime errors: '+errors.join('; '));passed=true;
  fs.writeFileSync(path.join(output,'report.json'),JSON.stringify({status:'PASS',checks:checks.length,labels:checks,reports,errors,mockOnly:true},null,2));console.log('PASS: '+checks.length+' Cozy theme UI checks; all native IPC mocked.');
 }finally{
  if(!passed){if(page)await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});fs.writeFileSync(path.join(output,'report.json'),JSON.stringify({status:'FAIL',checks:checks.length,labels:checks,reports,errors,mockOnly:true},null,2));}
  await browser.close();server.close();
 }
})().catch(error=>{console.error(error);server.close();process.exitCode=1;});
