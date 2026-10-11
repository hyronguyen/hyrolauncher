/* Mystic light lives on the same surface as the card. A single throttled loop paints
   only visible cards; no animation or image work continues in hidden panels. */
(function () {
  'use strict';
  const records = new Map(), visible = new Set();
  const motion = matchMedia('(prefers-reduced-motion: reduce)');
  const selector = '.lifeMystic:is(.cardVisual,.detailCard,.lifeStudioPreview,.lifeMiniCard)';
  let frame = 0, lastPaint = 0, paints = 0;
  const enabled = () => !document.hidden && document.documentElement.dataset.cardEffects !== 'off';
  const color = node => {
    const value = node.style.getPropertyValue('--life-mystic-color').trim();
    return /^#[0-9a-f]{6}$/i.test(value) ? value : '#85ecff';
  };
  function rgba(rgb, opacity) { return 'rgba(' + rgb.join(',') + ',' + opacity + ')'; }
  function roundPath(ctx,x,y,w,h,r) {
    ctx.beginPath();ctx.moveTo(x+r,y);ctx.arcTo(x+w,y,x+w,y+h,r);ctx.arcTo(x+w,y+h,x,y+h,r);ctx.arcTo(x,y+h,x,y,r);ctx.arcTo(x,y,x+w,y,r);ctx.closePath();
  }
  // Constant-speed movement around a rounded rectangle, including its corners.
  function perimeter(record, fraction) {
    const w=record.width,h=record.height,r=record.radius,m=record.margin;
    const horizontal=w-2*r,vertical=h-2*r,arc=Math.PI*r/2,total=2*(horizontal+vertical)+4*arc;
    let d=((fraction%1+1)%1)*total;
    const line=(length,x,y,dx,dy,nx,ny)=>{if(d<=length)return {x:m+x+dx*d,y:m+y+dy*d,nx,ny};d-=length;return null;};
    const curve=(cx,cy,angle)=>{if(d<=arc){const a=angle+d/r;return{x:m+cx+Math.cos(a)*r,y:m+cy+Math.sin(a)*r,nx:Math.cos(a),ny:Math.sin(a)};}d-=arc;return null;};
    return line(horizontal,r,0,1,0,0,-1)||curve(w-r,r,-Math.PI/2)||line(vertical,w,r,0,1,1,0)||curve(w-r,h-r,0)||line(horizontal,w-r,h,-1,0,0,1)||curve(r,h-r,Math.PI/2)||line(vertical,0,h-r,0,-1,-1,0)||curve(r,r,Math.PI)||{x:m+r,y:m,nx:0,ny:-1};
  }
  function paint(record, milliseconds, still=false) {
    const ctx=record.context,{width:w,height:h,margin:m,radius:r,rgb,light}=record;
    if(!w||!h)return;
    ctx.clearRect(0,0,w+2*m,h+2*m);
    const t=milliseconds/1000,mini=record.node.classList.contains('lifeMiniCard'),strength=mini?.57:1;
    ctx.lineWidth=3;ctx.strokeStyle=rgba(rgb,.58*strength);ctx.shadowColor=rgba(rgb,.95);ctx.shadowBlur=12*strength;
    roundPath(ctx,m,m,w,h,r);ctx.stroke();ctx.shadowBlur=0;
    ctx.lineWidth=.85;ctx.strokeStyle=rgba(light,.78*strength);roundPath(ctx,m,m,w,h,r);ctx.stroke();
    if(still)return;
    // Thin translucent tongues and their bright cores float out from the actual rim.
    // Their bases stay on the card, rather than painting a rectangular panel aura.
    const count=mini?9:18,scale=Math.min(1.2,Math.max(.62,w/310));
    for(let i=0;i<count;i++){
      const phase=i*2.399963,point=perimeter(record,i/count+t*.016+Math.sin(t*.18+phase)*.002);
      const wave=.5+.5*Math.sin(t*(1.3+(i%5)*.17)+phase),length=(7+wave*19)*scale*strength;
      const nx=point.nx,ny=point.ny,tx=-ny,ty=nx,drift=Math.sin(t*1.7+phase)*5*scale;
      const tipX=point.x+nx*length+tx*drift,tipY=point.y+ny*length+ty*drift,base=(1.25+wave*1.1)*scale;
      const gradient=ctx.createLinearGradient(point.x,point.y,tipX,tipY);gradient.addColorStop(0,rgba(light,.6*strength));gradient.addColorStop(.32,rgba(rgb,.48*strength));gradient.addColorStop(1,rgba(rgb,0));
      ctx.fillStyle=gradient;ctx.beginPath();ctx.moveTo(point.x+tx*base,point.y+ty*base);
      ctx.bezierCurveTo(point.x+nx*length*.27+tx*(base+drift),point.y+ny*length*.27+ty*(base+drift),tipX+tx*2,tipY+ty*2,tipX,tipY);
      ctx.bezierCurveTo(point.x+nx*length*.45-tx*base,point.y+ny*length*.45-ty*base,point.x-tx*base,point.y-ty*base,point.x-tx*base,point.y-ty*base);ctx.closePath();ctx.fill();
      ctx.strokeStyle=rgba(light,(.33+wave*.3)*strength);ctx.lineWidth=.7;ctx.beginPath();ctx.moveTo(point.x,point.y);ctx.quadraticCurveTo(point.x+nx*length*.45+tx*drift,point.y+ny*length*.45+ty*drift,point.x+nx*length*.75+tx*drift*.5,point.y+ny*length*.75+ty*drift*.5);ctx.stroke();
    }
    // Two narrow highlights travel along the perimeter, leaving the artwork clear.
    for(let i=0;i<2;i++){
      ctx.beginPath();for(let j=0;j<=14;j++){const p=perimeter(record,t*.055+i*.47+j*.0019);if(j===0)ctx.moveTo(p.x,p.y);else ctx.lineTo(p.x,p.y);}
      ctx.strokeStyle=rgba(light,.78*strength);ctx.lineWidth=1.6;ctx.shadowBlur=7;ctx.shadowColor=rgba(rgb,.85);ctx.stroke();ctx.shadowBlur=0;
    }
  }
  function resize(record) {
    const width=record.node.clientWidth,height=record.node.clientHeight;if(!width||!height)return;
    const radius=Math.max(8,Math.min(20,parseFloat(getComputedStyle(record.node).borderRadius)||13));
    if(record.width===width&&record.height===height&&record.radius===radius)return;
    record.width=width;record.height=height;record.radius=radius;
    const ratio=Math.min(1.35,devicePixelRatio||1);record.canvas.width=Math.ceil((width+56)*ratio);record.canvas.height=Math.ceil((height+56)*ratio);record.context.setTransform(ratio,0,0,ratio,0,0);
    paint(record,performance.now());
  }
  function prune(node) {
    const record=records.get(node);if(!record)return;
    resizeObserver?.unobserve(node);intersection?.unobserve(node);visible.delete(record);record.canvas.remove();
    if(record.plane){const surface=record.plane.querySelector('.cardSurface');if(surface)node.insertBefore(surface,record.plane);record.plane.remove();}
    node.classList.remove('mysticAttached');records.delete(node);
  }
  function attach(node) {
    if(!node.matches(selector)||!node.isConnected){prune(node);return;}
    let record=records.get(node);
    if(!record){
      const canvas=document.createElement('canvas');canvas.className='mysticAura';canvas.setAttribute('aria-hidden','true');
      const surface=node.querySelector(':scope>.cardSurface');let plane=null;
      if(surface){plane=document.createElement('span');plane.className='mysticPlane';node.insertBefore(plane,surface);plane.append(canvas,surface);}else node.prepend(canvas);
      node.classList.add('mysticAttached');
      record={node,canvas,plane,context:canvas.getContext('2d',{alpha:true}),margin:28,width:0,height:0,radius:13,rgb:[133,236,255],light:[221,255,255],color:''};
      if(!record.context){canvas.remove();return;}records.set(node,record);resizeObserver?.observe(node);intersection?.observe(node);resize(record);
      if(!intersection&&node.getClientRects().length)visible.add(record);
    }
    const next=color(node);if(next!==record.color){record.color=next;record.rgb=next.slice(1).match(/../g).map(x=>parseInt(x,16));record.light=record.rgb.map(x=>Math.round(x+(255-x)*.72));paint(record,performance.now());}
    schedule();
  }
  function tick(time) {
    frame=0;if(!enabled())return;
    const active=activeRecords();if(!active.length)return;
    if(time-lastPaint>=1000/(motion.matches?16:24)){
      lastPaint=time;let count=0;
      active.sort((a,b)=>Number(b.node.classList.contains('detailCard'))-Number(a.node.classList.contains('detailCard')));
      for(const record of active){if(!record.node.isConnected){prune(record.node);continue;}if(count++<16){paint(record,time);paints++;}}
    }
    frame=requestAnimationFrame(tick);
  }
  function activeRecords() {
    const modal=document.querySelector('dialog[open]');
    return [...visible].filter(record=>record.node.isConnected&&(!modal||modal.contains(record.node)));
  }
  function schedule() {
    if(!enabled()){if(frame)cancelAnimationFrame(frame);frame=0;for(const record of visible)paint(record,performance.now(),true);return;}
    if(!frame&&activeRecords().length)frame=requestAnimationFrame(tick);
  }
  const resizeObserver=typeof ResizeObserver==='function'?new ResizeObserver(entries=>{for(const entry of entries){const record=records.get(entry.target);if(record)resize(record);}schedule();}):null;
  const intersection=typeof IntersectionObserver==='function'?new IntersectionObserver(entries=>{for(const entry of entries){const record=records.get(entry.target);if(record){if(entry.isIntersecting&&entry.intersectionRatio>0)visible.add(record);else visible.delete(record);}}schedule();},{threshold:.01}):null;
  const observer=new MutationObserver(entries=>{
    for(const entry of entries){
      if(entry.type==='childList'){for(const node of entry.addedNodes)if(node.nodeType===1){if(node.matches(selector))attach(node);for(const child of node.querySelectorAll(selector))attach(child);}if(entry.removedNodes.length)for(const [node]of records)if(!node.isConnected)prune(node);}
      else if(entry.target.matches?.(selector)||records.has(entry.target))attach(entry.target);
      else if(entry.target===document.documentElement||entry.attributeName==='hidden'||entry.attributeName==='open')schedule();
    }
  });
  function start(){observer.observe(document.documentElement,{childList:true,subtree:true,attributes:true,attributeFilter:['class','style','data-card-effects','hidden','open']});for(const node of document.querySelectorAll(selector))attach(node);}
  document.addEventListener('visibilitychange',schedule);motion.addEventListener('change',schedule);
  window.addEventListener('pagehide',()=>{if(frame)cancelAnimationFrame(frame);frame=0;observer.disconnect();intersection?.disconnect();resizeObserver?.disconnect();});
  window.HyTcgMystic=Object.freeze({refresh:attach,stats:()=>({attached:records.size,visible:visible.size,animating:Boolean(frame),paints,maxAnimated:16,fps:motion.matches?16:24})});
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
