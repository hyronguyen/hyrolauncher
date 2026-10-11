const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
function fakeAudio(){
 const handlers={};let source='';
 const a={paused:true,currentTime:0,duration:200,volume:.35,plays:0,loads:0,
  addEventListener(name,fn){(handlers[name]||=[]).push(fn);},removeEventListener(name,fn){handlers[name]=(handlers[name]||[]).filter(f=>f!==fn);},
  emit(name){for(const fn of [...handlers[name]||[]])fn();},pause(){this.paused=true;this.emit('pause');},
  play(){this.paused=false;this.plays++;this.emit('play');return this.fail?Promise.reject(Error('bad file')):Promise.resolve();},
  load(){this.loads++;},removeAttribute(){source='';}};
 Object.defineProperty(a,'src',{get:()=>source,set:v=>{source=v;a.currentTime=0;a.paused=true;}});return a;
}
const classes=()=>({toggle(){}});
const nodes={};for(const id of ['musicTracks','musicSeek','musicElapsed','musicDuration','musicPlay','musicStatus','musicMute','musicVolumeValue','musicFeedback'])nodes[id]={style:{setProperty(){}},setAttribute(){}};
const p={Id:'builtin.mp3',Type:'mp3',Mp3Tracks:[{Id:'a',Title:'Song A',Path:'a.mp3'},{Id:'b',Title:'Song B',Path:'b.mp3'},{Id:'c',Title:'Song C',Path:'c.mp3'}],ActiveTrackId:'a',Mp3Volume:45};
const messages=[],saves=[];const audio=fakeAudio();let nextId=1;
const c={plugin:()=>p,pluginId:p.Id,state:{Settings:{BgmVolume:35}},audio,app:{innerHTML:''},document:{getElementById:id=>nodes[id],querySelector:()=>({classList:classes()}),addEventListener(){}},window:{addEventListener(){}},post:m=>messages.push(m),savePlugin:p=>saves.push(structuredClone(p)),id:()=>String(nextId++),fileNameNoExt:path=>path.split(/[\\/]/).pop().replace(/\.[^.]+$/,''),esc:s=>String(s??''),mediaUrl:s=>s,tr:en=>en,appConfirm:async()=>true};
vm.createContext(c);vm.runInContext(fs.readFileSync('ui/mp3-player.js','utf8'),c);
const run=s=>vm.runInContext(s,c);
function grant(){const request=messages.filter(m=>m.playing).at(-1);c.message={requestId:request.requestId};run('receiveMusicGrant(message)');}
(async()=>{
 run('renderMp3(plugin());playTrack("a")');assert.equal(audio.plays,0,'MP3 must wait for BGM pause acknowledgement');grant();assert.equal(audio.plays,1);
 audio.currentTime=42;run('pauseMusic();playTrack("a")');grant();assert.equal(audio.currentTime,42,'Pause/resume retains position');
 run('seekMusic(500)');assert.equal(audio.currentTime,100);assert.equal(nodes.musicElapsed.textContent,'1:40');
 run('setMusicVolume(25);toggleMusicMute()');assert.equal(audio.volume,0);run('toggleMusicMute()');assert.equal(audio.volume,.25);
 run('nextMusic()');grant();assert.equal(p.ActiveTrackId,'b');audio.currentTime=10;run('previousMusic()');assert.equal(audio.currentTime,0);assert.equal(p.ActiveTrackId,'b');run('previousMusic()');grant();assert.equal(p.ActiveTrackId,'a');
 run('cycleMusicRepeat()');assert.equal(p.Mp3RepeatMode,'all');run('playTrack("c",true)');grant();audio.emit('ended');grant();assert.equal(p.ActiveTrackId,'a','Repeat playlist wraps');
 run('cycleMusicRepeat()');assert.equal(p.Mp3RepeatMode,'one');audio.emit('ended');grant();assert.equal(p.ActiveTrackId,'a','Repeat one retains current track');
 run('cycleMusicRepeat();playTrack("c",true)');grant();audio.emit('ended');assert.equal(audio.paused,true,'Repeat off stops at last track');assert.equal(messages.at(-1).playing,false);
 run('playTrack("a",true);toggleMusicShuffle()');grant();const shuffled=new Set(['a']);
 for(let i=0;i<2;i++){audio.emit('ended');grant();assert(!shuffled.has(p.ActiveTrackId),'Shuffle must exhaust queue before repeating');shuffled.add(p.ActiveTrackId);}
 audio.emit('ended');assert.equal(audio.paused,true,'Shuffle off-repeat stops after the full queue');
 run('playTrack("a",true)');const stale=messages.at(-1).requestId;run('pauseMusic()');const plays=audio.plays;c.message={requestId:stale};run('receiveMusicGrant(message)');assert.equal(audio.plays,plays,'Canceled request must not start playback');
 const saveCount=saves.length;run('addTracks(["d.mp3","e.mp3","d.mp3"])');assert.equal(p.Mp3Tracks.length,5);assert.equal(saves.length,saveCount+1,'Multiple imports save once');
 run('updateMusicSearch("Song B")');assert(nodes.musicTracks.innerHTML.includes('Song B'));assert(!nodes.musicTracks.innerHTML.includes('Song A'));
 run('updateMusicSearch("");playTrack("a",true)');grant();audio.emit('loadedmetadata');assert(nodes.musicTracks.innerHTML.includes('3:20'));
 await run('deleteTrack("a")');assert.equal(audio.paused,true);assert(!p.Mp3Tracks.some(t=>t.Id==='a'));assert.equal(messages.at(-1).playing,false);
 audio.fail=true;run('playTrack("b",true)');grant();await new Promise(resolve=>setImmediate(resolve));assert.equal(messages.at(-1).playing,false,'Playback error releases BGM ownership');assert(nodes.musicFeedback.textContent.includes('Cannot play'));
 const main=fs.readFileSync('ui/index.html','utf8').match(/<script>([\s\S]*?)<\/script>/)[1];
 const bgm=fakeAudio(),legacy=fakeAudio(),sent=[];
 const b={state:{Settings:{}},assets:{defaultBgm:'default.mp3'},selectedGame:{Id:'detail',BgmPath:'detail.mp3'},document:{getElementById:id=>id==='bgm'?bgm:id==='pluginAudio'?legacy:{classList:{contains:()=>false}}},post:m=>sent.push(m),mediaUrl:x=>x};
 vm.createContext(b);vm.runInContext("let mp3PlaybackActive=false,bgmRequestId=0,currentBgmSrc='',currentBgmKind='',currentBgmGameKey='',defaultBgmPosition=0;",b);
 const focusStart=main.indexOf('    function applyMp3PlaybackState(');
 vm.runInContext(main.slice(focusStart,main.indexOf("    document.addEventListener('pointermove'",focusStart)),b);
 const br=s=>vm.runInContext(s,b);
 br('playBgm(null)');br('applyMp3PlaybackState(true,"request")');bgm.emit('loadedmetadata');assert.equal(bgm.plays,0,'Stale BGM load must not overlap MP3');assert.equal(sent.at(-1).type,'mp3BgmReady');
 br('playBgm(selectedGame)');bgm.emit('loadedmetadata');assert.equal(bgm.plays,0,'Game selection must not restart BGM during MP3');
 br('applyMp3PlaybackState(false)');bgm.emit('loadedmetadata');assert.equal(bgm.src,'detail.mp3');assert.equal(bgm.plays,1,'BGM resumes for current screen after MP3 stops');
 b.state.Settings.DisableBgm=true;br('applyMp3PlaybackState(true,"second");applyMp3PlaybackState(false)');bgm.emit('loadedmetadata');assert.equal(bgm.plays,1,'Respect Disable BGM setting on resume');
 for(const file of ['ui/index.html','ui/plugin.html'])new vm.Script(fs.readFileSync(file,'utf8').match(/<script>([\s\S]*?)<\/script>/)[1]);
 console.log('PASS: gated playback, seek/resume, volume/mute, previous/next, repeat all/one/off, shuffle without duplicates, cancellation/errors, multi-add, search/remove, BGM ownership across game changes and late loads.');
})().catch(e=>{console.error(e);process.exitCode=1;});
