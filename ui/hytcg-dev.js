'use strict';
window.HyTcgDev=(()=>{
  let active=false,received=0,remaining=0,clicks=0,lastClick=0,switching=false;
  const el=id=>document.getElementById(id);
  const label=(en,vi)=>typeof tx==='function'?tx(en,vi):vi;
  function wallet(){
    if(!active)return;
    for(const id of ['gemBalance','freeBalance','paidBalance','limitedBalance','refillCountdown'])if(el(id))el(id).textContent='∞';
  }
  function paint(){
    const button=el('devModeBadge');if(!button)return;button.hidden=!active;
    document.body.classList.toggle('tcgDevMode',active);
    const left=Math.max(0,remaining-Math.floor((performance.now()-received)/1000));
    el('devModeClock').textContent=String(Math.floor(left/60)).padStart(2,'0')+':'+String(left%60).padStart(2,'0');
    button.title=label('Exit dev mode · restore your account','Thoát dev mode · về tiến trình thật');
    button.setAttribute('aria-label',button.title);wallet();
  }
  function apply(state){
    active=state?.active===true;remaining=Math.max(0,Number(state?.remainingSeconds)||0);received=performance.now();paint();
  }
  function transition(value){
    switching=value;
    const shell=document.querySelector('.tcgShell');if(shell)shell.inert=value;
    if(el('devModeTransition')){el('devModeTransition').hidden=!value;el('devModeTransitionLabel').textContent=label('Switching session…','Đang chuyển phiên…');}
  }
  function toggle(){if(!switching)send('hytcgDevToggle');}
  function iconClick(){
    const now=performance.now();clicks=now-lastClick<=3000?clicks+1:1;lastClick=now;
    if(clicks>=5){clicks=0;toggle();}
  }
  function message(msg){
    if(msg.type==='hytcgDevTransition'){transition(true);return true;}
    if(msg.type!=='hytcgDevState')return false;
    // Reload discards in-flight promises, drafts and image caches from the old
    // account scope. Native init selects the already-mounted current service.
    if(msg.error){transition(false);tcg.session=msg.nextSession;toast(errorText(msg.error));send('hytcgInit');}
    else location.reload();
    return true;
  }
  document.addEventListener('DOMContentLoaded',()=>{
    el('tcgDevIcon')?.addEventListener('click',iconClick);
    el('devModeBadge')?.addEventListener('click',toggle);
    document.addEventListener('visibilitychange',paint);
    setInterval(()=>{if(active&&!document.hidden)paint();},1000);
  });
  return {apply,wallet,message,get active(){return active;}};
})();
