// Idle uses boundary timers; only a released, airborne pet needs frame updates.
let petMotionTimer = 0;
const petMotion = { x:NaN, y:NaN, hover:false, staySeconds:60, phase:'idle', drag:null, suppressClick:false };
function petBounds(layer, size) {
  const left=Math.min(12,Math.max(0,layer.clientWidth-size));
  const bottom=Math.max(0,(layer.clientHeight || size+54)-size-12);
  return { left, right:Math.max(left,layer.clientWidth-size-12), top:Math.min(42,bottom), bottom };
}
function petDisplaySize(layer, requested) {
  return Math.max(1,Math.min(clampInt(requested,78,44,300),Math.max(1,layer.clientWidth-24),Math.max(1,(layer.clientHeight || 354)-54)));
}
function stopPetMotion() {
  clearTimeout(petMotionTimer); petMotionTimer = 0; petMotion.phase = 'idle';
  const pet = document.querySelector('#petLayer .hyroPet');
  if (pet) {
    pet.classList.remove('petFading', 'dragging');
    const drag=petMotion.drag; petMotion.drag=null;
    if(drag && pet.hasPointerCapture?.(drag.id)) pet.releasePointerCapture(drag.id);
  }
  petMotion.drag=null;
}
function renderPet() {
  const plugin = petPlugin(), layer = document.getElementById('petLayer');
  if (!layer) return;
  if (!plugin || !plugin.PetEnabled) { stopPetMotion(); petMotion.hover = false; layer.classList.remove('show'); layer.innerHTML = ''; return; }
  const size = petDisplaySize(layer,plugin.PetSize);
  const stay = clampInt(plugin.PetStaySeconds, 60, 10, 180);
  layer.classList.add('show');
  if (!layer.querySelector('.hyroPet')) layer.innerHTML = petMarkup('idle');
  const pet = layer.querySelector('.hyroPet');
  applyPetVisuals(pet, plugin, size);
  // Extremely small windows may need a display size below the normal setting range.
  pet.style.setProperty('--pet-size',`${size}px`);
  pet.onclick = event => { event?.preventDefault(); event?.stopPropagation(); petMotion.suppressClick=false; };
  bindPetDrag(layer, pet, size, stay);
  if (petMotion.drag) return;
  pet.onmouseenter = () => {
    petMotion.hover = true;
    // Do not vanish while the user is trying to grab the pet.
    if (petMotion.phase === 'out') { stopPetMotion(); startPetMotion(layer, pet, size, stay); }
  };
  pet.onmouseleave = () => { petMotion.hover = false; };
  if (document.hidden || (typeof launcherActive !== 'undefined' && !launcherActive) || state.Settings.DisableEffects) {
    stopPetMotion(); const bounds=petBounds(layer,size);
    petMotion.x = Math.max(bounds.left, Math.min(bounds.right, Number.isFinite(petMotion.x) ? petMotion.x : bounds.right));
    petMotion.y = bounds.bottom;
    applyPetMotionFrame(pet, petMotion.x);
  } else if(petMotion.phase!=='fall') startPetMotion(layer, pet, size, stay);
}
function nextPetPosition(min, max, current, random = Math.random) {
  const distance = Math.min(140, (max - min) * .35);
  for (let attempt = 0; attempt < 6; attempt++) {
    const next = min + random() * (max - min);
    if (Math.abs(next - current) >= distance) return next;
  }
  return current > (min + max) / 2 ? min : max;
}
function startPetMotion(layer, pet, size, stay) {
  if(petMotion.phase==='fall') return;
  const bounds = petBounds(layer,size);
  if (!Number.isFinite(petMotion.x)) petMotion.x = bounds.right;
  petMotion.x = Math.max(bounds.left, Math.min(bounds.right, petMotion.x));
  petMotion.y = bounds.bottom;
  applyPetMotionFrame(pet, petMotion.x);
  if (petMotion.staySeconds !== stay) stopPetMotion();
  petMotion.staySeconds = stay;
  if (petMotionTimer) return;
  const alive = () => !document.hidden && (typeof launcherActive === 'undefined' || launcherActive) && document.body.contains(pet) && layer.classList.contains('show') && !state.Settings.DisableEffects;
  const rest = (delay = petMotion.staySeconds * 1000) => {
    petMotion.phase = 'idle';
    petMotionTimer = setTimeout(() => {
      if (!alive()) { stopPetMotion(); return; }
      if (petMotion.hover || petMotion.drag || document.querySelector('.appDialogLayer, .modal.show') || layer.clientWidth <= size + 24) { rest(5000); return; }
      petMotion.phase = 'out'; pet.classList.add('petFading');
      petMotionTimer = setTimeout(() => {
        if (!alive()) { stopPetMotion(); return; }
        const currentSize = petDisplaySize(layer,petPlugin()?.PetSize);
        const bounds = petBounds(layer,currentSize);
        petMotion.x = nextPetPosition(bounds.left,bounds.right,petMotion.x);
        petMotion.y = bounds.bottom;
        applyPetMotionFrame(pet, petMotion.x);
        // Move while fully transparent, then allow the new position to paint before fading in.
        petMotion.phase = 'in';
        petMotionTimer = setTimeout(() => {
          if (!alive()) { stopPetMotion(); return; }
          pet.classList.remove('petFading');
          petMotionTimer = setTimeout(() => { if (alive()) rest(); else stopPetMotion(); }, 700);
        }, 60);
      }, 700);
    }, delay);
  };
  rest();
}
function dropPetToGround(layer,pet,size,stay) {
  const bounds=petBounds(layer,size);
  petMotion.x=Math.max(bounds.left,Math.min(bounds.right,petMotion.x));
  if(document.hidden || state.Settings.DisableEffects || !Number.isFinite(petMotion.y) || petMotion.y>=bounds.bottom-1) {
    petMotion.y=bounds.bottom; pet.classList.remove('dragging');
    applyPetMotionFrame(pet,petMotion.x);
    if(!document.hidden && !state.Settings.DisableEffects) startPetMotion(layer,pet,size,stay);
    return;
  }
  petMotion.phase='fall'; pet.classList.add('dragging');
  let velocity=0, previous=performance.now();
  const fall=()=>{
    petMotionTimer=0;
    if(document.hidden || state.Settings.DisableEffects || !document.body.contains(pet) || !layer.classList.contains('show') || !petPlugin()?.PetEnabled) {
      stopPetMotion();
      petMotion.y=petBounds(layer,petDisplaySize(layer,petPlugin()?.PetSize)).bottom;
      applyPetMotionFrame(pet,petMotion.x);
      return;
    }
    const current=performance.now(), seconds=Math.min(.05,Math.max(.001,(current-previous)/1000)); previous=current;
    const currentSize=petDisplaySize(layer,petPlugin()?.PetSize), ground=petBounds(layer,currentSize);
    // X remains fixed throughout the fall, except when a resized window would clip it.
    petMotion.x=Math.max(ground.left,Math.min(ground.right,petMotion.x));
    velocity+=2200*seconds;
    petMotion.y=Math.min(ground.bottom,petMotion.y+velocity*seconds);
    applyPetMotionFrame(pet,petMotion.x);
    if(petMotion.y>=ground.bottom) {
      petMotion.phase='idle'; pet.classList.remove('dragging');
      startPetMotion(layer,pet,currentSize,clampInt(petPlugin()?.PetStaySeconds,stay,10,180));
    } else petMotionTimer=setTimeout(fall,16);
  };
  petMotionTimer=setTimeout(fall,16);
}
function applyPetMotionFrame(pet, x) {
  pet.style.left = `${Math.round(x)}px`;
  if (Number.isFinite(petMotion.y)) { pet.style.top=`${Math.round(petMotion.y)}px`; pet.style.bottom='auto'; }
  pet.classList.add('idle'); pet.classList.remove('walking', 'facingRight');
}
function bindPetDrag(layer, pet, size, stay) {
  pet.onpointerdown = event => {
    if (event.button !== 0 || !event.isPrimary) return;
    const rect=pet.getBoundingClientRect();
    stopPetMotion(); petMotion.suppressClick=false;
    petMotion.drag={id:event.pointerId,startX:event.clientX,startY:event.clientY,offsetX:event.clientX-rect.left,offsetY:event.clientY-rect.top,moved:false};
    pet.setPointerCapture(event.pointerId); event.preventDefault();
  };
  pet.onpointermove = event => {
    const drag=petMotion.drag; if (!drag || drag.id !== event.pointerId) return;
    if (!drag.moved && Math.hypot(event.clientX-drag.startX,event.clientY-drag.startY) < 5) return;
    drag.moved=true; pet.classList.add('dragging');
    const rect=layer.getBoundingClientRect(), bounds=petBounds(layer,size);
    petMotion.x=Math.max(bounds.left,Math.min(bounds.right,event.clientX-rect.left-drag.offsetX));
    petMotion.y=Math.max(bounds.top,Math.min(bounds.bottom,event.clientY-rect.top-drag.offsetY));
    applyPetMotionFrame(pet,petMotion.x); event.preventDefault();
  };
  const finish = event => {
    const drag=petMotion.drag; if (!drag || drag.id !== event.pointerId) return;
    petMotion.suppressClick=drag.moved; petMotion.drag=null; pet.classList.remove('dragging');
    if (pet.hasPointerCapture?.(event.pointerId)) pet.releasePointerCapture(event.pointerId);
    dropPetToGround(layer,pet,size,stay);
  };
  pet.onpointerup=finish; pet.onpointercancel=finish; pet.onlostpointercapture=finish;
}
document.addEventListener('visibilitychange', () => { if (document.hidden) stopPetMotion(); else renderPet(); });
window.addEventListener('resize', () => { if (typeof state !== 'undefined') renderPet(); });
