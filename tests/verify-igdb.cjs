const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('fs'),vm=require('vm'),assert=require('assert');
const nodes={};let libraryVisible=false;
const classes={contains:()=>libraryVisible,toggle(){},add(){},remove(){}};
for(const key of ['gameIntel','library','intelVisual','intelHead','intelBody','igdbLinkLabel','igdbQuery','fName','igdbSearchModal','igdbResults','igdbViewer','igdbViewerImage','igdbLongText','igdbViewerTitle','igdbViewerPrev','igdbViewerNext','igdbViewerClose']) nodes[key]={classList:classes,focus(){},value:'Genshin Impact'};
const sent=[];
const c={assets:{},state:{Settings:{Language:'en'}},selectedGame:{Id:'local',IgdbId:42},editing:{},document:{getElementById:id=>nodes[id],addEventListener(){}},isPrivateLocked:g=>!!g.Private,escapeHtml:s=>String(s||'').replace(/</g,'&lt;').replace(/"/g,'&quot;'),post:m=>sent.push(m),iconHtml:()=>'',id:()=>String(sent.length)};
c.img=p=>p;c.syncHeroLayoutPosition=()=>{};
vm.createContext(c);vm.runInContext(fs.readFileSync('ui/igdb.js','utf8'),c);
vm.runInContext('renderGameIntel();renderGameIntel()',c);assert.equal(sent.length,1);assert.equal(sent[0].gameId,'local');
c.msg={type:'igdbInfo',id:42,info:{Name:'Game',Summary:'<script>bad</script>',Genres:['RPG'],Rating:82,ReleaseDate:'2020-09-28',Developers:['Studio'],Publishers:[],Screenshots:['https://images.igdb.com/igdb/image/upload/t_screenshot_big/sc1.jpg']},error:''};
vm.runInContext('handleIgdbMessage(msg)',c);assert(nodes.intelBody.innerHTML.includes('&lt;script>'));assert(!nodes.intelBody.innerHTML.includes('<script>'));
vm.runInContext("igdbTab='shots';renderGameIntel();openIgdbViewer(0)",c);assert(nodes.intelBody.innerHTML.includes('loading="lazy"'));assert(nodes.intelBody.innerHTML.includes('t_screenshot_med'));assert(!nodes.igdbViewerImage.hidden);
vm.runInContext('openIgdbViewer(-1)',c);assert(nodes.igdbViewerImage.hidden);assert(nodes.igdbLongText.textContent.includes('Studio'));
c.selectedGame={Id:'other',IgdbId:99};vm.runInContext('handleIgdbMessage(msg)',c);assert(!nodes.intelBody.innerHTML.includes('RPG'));
libraryVisible=true;vm.runInContext('renderGameIntel()',c);assert(nodes.gameIntel.hidden);
libraryVisible=false;c.selectedGame.Private=true;vm.runInContext('renderGameIntel()',c);assert(nodes.gameIntel.hidden);
c.selectedGame={Id:'unlinked',IgdbId:0};vm.runInContext('renderGameIntel()',c);assert(nodes.intelBody.innerHTML.includes('Link IGDB'));
vm.runInContext('igdbSearchResults=[{Id:42}];selectIgdbResult(0)',c);assert.equal(c.editing.IgdbId,42);vm.runInContext('unlinkIgdb()',c);assert.equal(c.editing.IgdbId,0);
new vm.Script(fs.readFileSync('ui/index.html','utf8').match(/<script>([\s\S]*?)<\/script>/)[1]);
console.log('PASS: lazy metadata fetch, escaped descriptions, screenshot thumbnails/viewer, stale response isolation, hidden library/private panel, link/unlink, UI syntax.');
for(const ratio of [.3,1,2])for(const x of [100,400,1000])for(const y of [150,550,700]) {
 c.bounds={left:72,top:0,width:1208,height:760};c.panel={left:92,right:472,top:380,bottom:730};c.x=x;c.y=y;c.ratio=ratio;
 const p=vm.runInContext('safeHeroPlacement(bounds,panel,x,y,650,ratio)',c), h=p.width*ratio;
 assert(!(p.x+p.width/2>c.panel.left && p.x-p.width/2<c.panel.right && p.y+h/2>c.panel.top && p.y-h/2<c.panel.bottom));
 assert(p.width>0);assert(p.x-p.width/2>=84);assert(p.y-h/2>=48);
}
console.log('PASS: HERO avoids info panel at different positions and portrait/landscape sizes.');
