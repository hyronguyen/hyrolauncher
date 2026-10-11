const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs = require('fs'), vm = require('vm'), assert = require('assert');
let now = 0, nextId = 1, alive = true, modal = false, openedPlugins=0;
const timers = new Map();
const classes = (...initial) => { const set = new Set(initial); return { add(...s) { s.forEach(x => set.add(x)); }, remove(...s) { s.forEach(x => set.delete(x)); }, contains(s) { return set.has(s); } }; };
let captured=null;
const pet = { style:{setProperty(name,value){this[name]=value;}}, classList:classes(), onclick:null, getBoundingClientRect:()=>({left:parseInt(pet.style.left),top:parseInt(pet.style.top)||42}),setPointerCapture:id=>captured=id,hasPointerCapture:id=>captured===id,releasePointerCapture:()=>captured=null };
const layer = { clientWidth:1000,clientHeight:700,getBoundingClientRect:()=>({left:0,top:0}), classList:classes(), querySelector:() => pet };
const settings = { PetEnabled:true, PetSize:78, PetStaySeconds:60, Id:'pet' };
const context = {
  state:{Settings:{}}, petPlugin:() => settings, clampInt:(v,f,min,max) => Math.max(min,Math.min(max,parseInt(v,10) || f)),
  document:{hidden:false, getElementById:() => layer, querySelector:s => s.startsWith('#petLayer') ? pet : modal ? {} : null, body:{contains:() => alive}, addEventListener(){}},
  window:{addEventListener(){}}, applyPetVisuals(){}, petMarkup:() => '', openPlugin(){openedPlugins++;},
  performance:{now:()=>now},
  setTimeout(fn,ms) { const id = nextId++; timers.set(id,{at:now+ms,fn}); return id; }, clearTimeout:id => timers.delete(id),
  requestAnimationFrame() { throw Error('Idle pet must not use a continuous animation frame loop'); }
};
vm.createContext(context); vm.runInContext(fs.readFileSync('ui/pet-motion.js','utf8'),context);
const run = script => vm.runInContext(script,context);
function advance(ms) {
  const end = now + ms;
  for (let steps=0; steps < 100; steps++) {
    const entries = [...timers].filter(([,t]) => t.at <= end).sort((a,b)=>a[1].at-b[1].at);
    if (!entries.length) break;
    const [id,timer]=entries[0]; timers.delete(id); now=timer.at; timer.fn();
  }
  now=end;
}
run('renderPet()'); const first = pet.style.left;
pet.onclick({preventDefault(){},stopPropagation(){}});assert.equal(openedPlugins,0,'Direct click must not open PET settings');
assert.equal(first,'910px'); assert.equal(timers.size,1);
advance(59999); assert.equal(pet.style.left,first); assert(!pet.classList.contains('petFading'));
advance(1); assert(pet.classList.contains('petFading')); assert.equal(pet.style.left,first,'Fade out before moving');
advance(700); assert.notEqual(pet.style.left,first); assert(pet.classList.contains('petFading'),'Move while invisible');
advance(60); assert(!pet.classList.contains('petFading')); assert(pet.classList.contains('idle')); assert(!pet.classList.contains('walking'));
advance(700); pet.onmouseenter(); const second=pet.style.left;
advance(60000); assert.equal(pet.style.left,second); assert(!pet.classList.contains('petFading'),'Hover defers teleport');
pet.onmouseleave(); modal=true; advance(5000); assert(!pet.classList.contains('petFading'),'Dialogs defer teleport');
modal=false; advance(5000); assert(pet.classList.contains('petFading'));
context.document.hidden=true; advance(700); assert.equal(timers.size,0); assert(!pet.classList.contains('petFading'));
context.document.hidden=false; context.state.Settings.DisableEffects=true; run('renderPet()'); assert.equal(timers.size,0);
context.state.Settings.DisableEffects=false; settings.PetStaySeconds=120; run('renderPet()');
assert.equal([...timers.values()][0].at-now,120000,'Dwell setting applies');
settings.PetEnabled=false; run('renderPet()'); assert.equal(timers.size,0); assert(!layer.classList.contains('show'));
settings.PetEnabled=true; run('renderPet()');
const pointer=(x,y)=>({button:0,isPrimary:true,pointerId:7,clientX:x,clientY:y,preventDefault(){},stopPropagation(){}});
pet.onpointerdown(pointer(parseInt(pet.style.left)+10,parseInt(pet.style.top)+10));
assert.equal(timers.size,0,'Dragging pauses movement');
pet.onpointermove(pointer(-100,-100));
assert.equal(pet.style.left,'12px'); assert.equal(pet.style.top,'42px'); assert(pet.classList.contains('dragging'));
pet.onpointermove(pointer(2000,2000)); assert.equal(pet.style.left,'910px');assert.equal(pet.style.top,'610px');
pet.onpointerup(pointer(2000,2000)); assert(!pet.classList.contains('dragging')); assert.equal(timers.size,1);
assert(run('petMotion.suppressClick'),'Dragging must not open the panel on release');
pet.onclick(pointer(2000,2000));assert(!run('petMotion.suppressClick'));
assert.equal(openedPlugins,0,'Click after drag must not open PET settings');
assert.equal(run('nextPetPosition(12,12,12,()=>0)'),12);
assert(run('nextPetPosition(12,500,490,()=>.98)') < 300,'Random fallback stays away from the current position');
assert(!fs.readFileSync('ui/index.html','utf8').includes('<img class="petGif petWalkGif"'));
function releaseHigh() {
  pet.onpointerdown(pointer(parseInt(pet.style.left)+10,parseInt(pet.style.top)+10));
  pet.onpointermove(pointer(410,90));
  pet.onpointerup(pointer(410,90));
}
settings.PetSize=300; settings.PetStaySeconds=10; run('stopPetMotion();renderPet()');
assert.equal(pet.style['--pet-size'],'300px'); assert.equal(pet.style.top,'388px');
assert.equal([...timers.values()][0].at-now,10000);
releaseHigh(); const droppedX=pet.style.left;
assert.equal(run('petMotion.phase'),'fall'); assert(pet.classList.contains('dragging'));
advance(160); assert.equal(pet.style.left,droppedX); assert(parseInt(pet.style.top)>80 && parseInt(pet.style.top)<388);
assert(pet.classList.contains('dragging')); assert(!pet.classList.contains('petFading'));
advance(800); assert.equal(pet.style.left,droppedX); assert.equal(pet.style.top,'388px');
assert.equal(run('petMotion.phase'),'idle'); assert(!pet.classList.contains('dragging'));
assert([...timers.values()][0].at-now>9000,'Dwell must start after landing');
releaseHigh(); advance(100);
pet.onpointerdown(pointer(parseInt(pet.style.left)+10,parseInt(pet.style.top)+10));
assert.equal(timers.size,0,'Regrab cancels fall');
pet.onpointermove(pointer(610,100)); pet.onpointerup(pointer(610,100)); advance(900);
assert.equal(pet.style.left,'600px'); assert.equal(pet.style.top,'388px');
releaseHigh(); context.document.hidden=true; advance(16);
assert.equal(timers.size,0); assert.equal(pet.style.top,'388px'); assert(!pet.classList.contains('dragging'));
context.document.hidden=false; run('renderPet()'); releaseHigh();
context.state.Settings.DisableEffects=true; advance(16); assert.equal(timers.size,0); assert.equal(pet.style.top,'388px');
run('renderPet()'); releaseHigh(); assert.equal(pet.style.top,'388px'); assert.equal(timers.size,0);
context.state.Settings.DisableEffects=false; run('renderPet()'); releaseHigh();
settings.PetEnabled=false; run('renderPet()'); assert.equal(timers.size,0); assert.equal(run('petMotion.phase'),'idle');
settings.PetEnabled=true; layer.clientWidth=240; layer.clientHeight=200; run('renderPet()');
assert.equal(pet.style['--pet-size'],'146px'); assert.equal(pet.style.top,'42px');
assert(parseInt(pet.style.left)+146<=240,'Small window keeps the pet visible');
run('stopPetMotion()');
console.log('PASS: idle/fade lifecycle, bounded drag, 300px/10s settings, fixed-X gravity with drag animation, landing dwell, regrab, hidden/disabled cleanup, small window bounds.');
