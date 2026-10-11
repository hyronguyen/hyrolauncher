'use strict';
// New cards use one portrait canvas; native projects legacy landscape exports locally.
window.HyTcgRenderer = (() => {
  const width=750,height=1050;
  function dimensions(card){return {width,height};}
  const styles={Common:{lines:1},Uncommon:{lines:1},Rare:{lines:2},SR:{lines:2,foil:true},SSR:{lines:3,foil:true,gems:true},UR:{lines:3,foil:true,gems:true,spectrum:true}};
  function rounded(ctx,x,y,w,h,r) {
    ctx.beginPath();ctx.moveTo(x+r,y);ctx.arcTo(x+w,y,x+w,y+h,r);ctx.arcTo(x+w,y+h,x,y+h,r);ctx.arcTo(x,y+h,x,y,r);ctx.arcTo(x,y,x+w,y,r);ctx.closePath();
  }
  function star(ctx,x,y,size,color) {
    ctx.save();ctx.fillStyle=color;ctx.beginPath();ctx.moveTo(x,y-size);ctx.lineTo(x+size*.28,y-size*.28);ctx.lineTo(x+size,y);ctx.lineTo(x+size*.28,y+size*.28);ctx.lineTo(x,y+size);ctx.lineTo(x-size*.28,y+size*.28);ctx.lineTo(x-size,y);ctx.lineTo(x-size*.28,y-size*.28);ctx.closePath();ctx.fill();ctx.restore();
  }
  function lines(ctx,text,maxWidth,maxLines) {
    // Iterate graphemes/code points too: CJK names need not contain spaces.
    const result=[];let current='';
    for(const character of String(text||'')) {
      if(ctx.measureText(current+character).width>maxWidth&&current) {result.push(current.trim());current=character;if(result.length===maxLines)break;}
      else current+=character;
    }
    if(result.length<maxLines&&current)result.push(current.trim());
    if(result.join('').replaceAll(' ','').length<String(text||'').replaceAll(' ','').length&&result.length) {
      let last=result[result.length-1];while(last&&ctx.measureText(last+'…').width>maxWidth)last=last.slice(0,-1);result[result.length-1]=last+'…';
    }
    return result;
  }
  function loadImage(source) {
    return new Promise((resolve,reject)=>{
      if(!/^data:image\/(png|jpeg|webp|gif|avif);base64,/i.test(source||'')){reject(Error('ArtworkInvalid'));return;}
      const image=new Image(),timer=setTimeout(()=>{image.src='';reject(Error('ArtworkInvalid'));},15000);
      image.onload=()=>{clearTimeout(timer);if(!image.naturalWidth||image.naturalWidth*image.naturalHeight>50000000)reject(Error('ArtworkInvalid'));else resolve(image);};
      image.onerror=()=>{clearTimeout(timer);reject(Error('ArtworkInvalid'));};image.src=source;
    });
  }
  const windowRarities=new Set(['Common','Uncommon','Rare','SR']);
  function windowLayout(card) {
    return {
      ArtworkX:.07,ArtworkY:.035,ArtworkWidth:.86,ArtworkHeight:.59,
      NameX:.08,NameY:.65,NameWidth:.59,NameHeight:.065,
      SeriesX:.08,SeriesY:.733,SeriesWidth:.84,SeriesHeight:.044,
      RarityX:.765,RarityY:.663,RarityWidth:.16,RarityHeight:.035,
      NumberX:.08,NumberY:.942,NumberWidth:.84,NumberHeight:.021,
      DescriptionX:.08,DescriptionY:.817,DescriptionWidth:.84,DescriptionHeight:.103
    };
  }
  function windowBox(layout,name,width,height) {
    return {x:layout[name+'X']*width,y:layout[name+'Y']*height,w:layout[name+'Width']*width,h:layout[name+'Height']*height};
  }
  function windowLines(ctx,text,maxWidth,maxLines) {
    const result=[];let current='',truncated=false;
    const segments=typeof Intl.Segmenter==='function'?[...new Intl.Segmenter(undefined,{granularity:'grapheme'}).segment(String(text||''))].map(x=>x.segment):Array.from(String(text||''));
    for(const character of segments) {
      if(current&&ctx.measureText(current+character).width>maxWidth) {
        const split=current.lastIndexOf(' ');result.push((split>0?current.slice(0,split):current).trim());
        current=(split>0?current.slice(split+1):'')+character;
        if(result.length===maxLines){truncated=true;break;}
      } else current+=character;
    }
    if(result.length<maxLines&&current.trim())result.push(current.trim());
    if(truncated&&result.length){let last=Array.from(result.at(-1));while(last.length&&ctx.measureText(last.join('')+'…').width>maxWidth)last.pop();result[result.length-1]=last.join('')+'…';}
    return result;
  }
  function windowLabel(ctx,text,box,color,bold,preferred,maxLines=2) {
    let size=Math.max(8,Math.min(preferred,box.h/(maxLines*1.25)));
    while(size>8){ctx.font=`${bold?700:400} ${size}px "Segoe UI", sans-serif`;if(ctx.measureText(String(text||'')).width<=box.w*maxLines)break;size--;}
    ctx.font=`${bold?700:400} ${size}px "Segoe UI", sans-serif`;
    const wrapped=windowLines(ctx,text,box.w,maxLines),lineHeight=size*1.25;
    ctx.save();ctx.beginPath();ctx.rect(box.x,box.y,box.w,box.h);ctx.clip();ctx.fillStyle=color;ctx.textBaseline='top';ctx.textAlign='left';
    wrapped.forEach((line,index)=>ctx.fillText(line,box.x,box.y+Math.max(0,(box.h-wrapped.length*lineHeight)/2)+index*lineHeight));ctx.restore();
  }
  function description(card) {
    let text=String(card.Metadata?.desc||card.Metadata?.description||'').slice(0,4000).replace(/<[^>]*>/g,' ');
    const decoder=document.createElement('textarea');decoder.innerHTML=text;text=decoder.value.replace(/\s+/g,' ').trim();
    return Array.from(text|| (card.Series?'A companion from '+card.Series+'.':'A companion ready for a new adventure.')).slice(0,600).join('');
  }
  function mix(color,target,amount) {
    const rgb=value=>[1,3,5].map(offset=>parseInt(value.slice(offset,offset+2),16));
    return '#'+rgb(color).map((value,index)=>Math.round(value+(rgb(target)[index]-value)*amount).toString(16).padStart(2,'0')).join('');
  }
  function perimeter(ctx,width,height,rarity,ink) {
    const tier=Math.max(0,['Common','Uncommon','Rare','SR','SSR','UR'].indexOf(rarity)),light=mix(ink,'#ffffff',.55),shadow=mix(ink,'#000000',.64);
    ctx.save();rounded(ctx,2,2,width-4,height-4,35);
    // An even-odd ring protects the complete artwork and identity, so every
    // engraved accent remains a real piece of the frame rather than a label tint.
    const outer=new Path2D();outer.roundRect(2,2,width-4,height-4,35);outer.roundRect(31,31,width-62,height-62,15);ctx.clip(outer,'evenodd');
    const bevel=ctx.createLinearGradient(0,0,width,height);
    [[0,light],[.16,ink],[.39,mix(ink,'#ffffff',.15)],[.62,shadow],[.84,ink],[1,light]].forEach(([stop,color])=>bevel.addColorStop(stop,color));
    ctx.fillStyle=bevel;ctx.fillRect(0,0,width,height);
    ctx.strokeStyle=mix(ink,'#000000',.7);ctx.lineWidth=3;rounded(ctx,2,2,width-4,height-4,35);ctx.stroke();
    ctx.strokeStyle=light+'c8';ctx.lineWidth=2;rounded(ctx,7,7,width-14,height-14,31);ctx.stroke();
    ctx.strokeStyle=shadow+'dc';ctx.lineWidth=3;rounded(ctx,31,31,width-62,height-62,15);ctx.stroke();
    if(tier>=1){ctx.strokeStyle=light+'a5';ctx.lineWidth=1.5;rounded(ctx,23,23,width-46,height-46,21);ctx.stroke();}
    function line(points,color=light,size=2,closed=false){ctx.beginPath();ctx.moveTo(...points[0]);points.slice(1).forEach(point=>ctx.lineTo(...point));if(closed)ctx.closePath();ctx.strokeStyle=color;ctx.lineWidth=size;ctx.stroke();}
    for(let end=0;end<2;end++)for(let side=0;side<2;side++) {
      const x=side?width-17:17,y=end?height-17:17,dx=side?-1:1,dy=end?-1:1;
      if(tier===1){line([[x,y],[x+dx*48,y]]);line([[x,y],[x,y+dy*48]]);}
      else if(tier===2){line([[x,y-9],[x+9,y],[x,y+9],[x-9,y]],light,2,true);line([[x+dx*16,y],[x+dx*58,y]],shadow,3);}
      else if(tier===3){star(ctx,x,y,7,light);line([[x+dx*12,y],[x+dx*52,y],[x+dx*62,y+dy*7]]);line([[x,y+dy*12],[x,y+dy*52],[x+dx*7,y+dy*62]]);}
      else if(tier===4){star(ctx,x,y,8,light);for(let a=0;a<3;a++){ctx.beginPath();ctx.ellipse(x+(dx<0?-52:0)+dx*a*3+26,y+(dy<0?-52:0)+dy*a*3+26,26,26,0,(side===end?0:90)*Math.PI/180,(side===end?90:180)*Math.PI/180);ctx.strokeStyle=light;ctx.lineWidth=2;ctx.stroke();}}
      else if(tier===5){star(ctx,x,y,10,light);line([[x+dx*18,y],[x+dx*48,y],[x+dx*32,y+dy*8]],light,2,true);line([[x,y+dy*18],[x,y+dy*48],[x+dx*8,y+dy*32]],light,2,true);}
    }
    if(tier>=2)for(let side=0;side<2;side++)for(let i=0;i<tier-1;i++) {
      const x=side?width-16:16,y=height*(.33+i*.11);
      if(tier===2)line([[x,y-8],[x,y+8]]);
      else if(tier===3){ctx.beginPath();ctx.ellipse(x,y,3,3,0,0,Math.PI*2);ctx.strokeStyle=light;ctx.lineWidth=2;ctx.stroke();if(i)line([[x,y-height*.11+8],[x,y-8]]);}
      else {star(ctx,x,y,i%2===0?5:3,light);if(tier===5)line([[x,y-11],[x,y+11]],shadow,3);}
    }
    if(tier>=4){star(ctx,width/2,16,tier===5?9:6,light);line([[width/2-45,16],[width/2-15,16]]);line([[width/2+15,16],[width/2+45,16]]);}
    ctx.restore();
  }
  function drawWindow(ctx,card,image,width,height,color,style) {
    const landscape=width>height,layout=windowLayout(card),art=windowBox(layout,'Artwork',width,height);
    ctx.fillStyle='#eeeade';rounded(ctx,0,0,width,height,37);ctx.fill();
    ctx.save();rounded(ctx,art.x,art.y,art.w,art.h,Math.min(24,Math.min(art.w,art.h)/12));ctx.clip();
    // Keep the complete image: portrait art must never lose a head to cover cropping.
    const scale=Math.min(art.w/image.naturalWidth,art.h/image.naturalHeight),w=image.naturalWidth*scale,h=image.naturalHeight*scale;
    ctx.fillStyle='#f6f3e9';ctx.fillRect(art.x,art.y,art.w,art.h);ctx.drawImage(image,art.x+(art.w-w)/2,art.y+(art.h-h)/2,w,h);
    ctx.restore();perimeter(ctx,width,height,card.Rarity,color);
    windowLabel(ctx,card.Name,windowBox(layout,'Name',width,height),'#1c2b3b',true,40);
    windowLabel(ctx,card.Series||'Unknown series',windowBox(layout,'Series',width,height),'#1c2b3b',true,24);
    const desc=windowBox(layout,'Description',width,height);
    ctx.strokeStyle='#3a414a30';ctx.beginPath();ctx.moveTo(desc.x,height*(landscape?.495:.803));ctx.lineTo(desc.x+desc.w,height*(landscape?.495:.803));ctx.stroke();
    ctx.font='700 14px "Segoe UI", sans-serif';ctx.textBaseline='top';ctx.fillStyle='#3d464f';ctx.fillText('CHARACTER  /  '+card.CharacterId+(card.Gender?'  ·  '+String(card.Gender).slice(0,40):''),desc.x,height*(landscape?.455:.786));
    ctx.font=`400 ${landscape?20:22}px "Segoe UI", sans-serif`;ctx.fillStyle='#333941';const lineHeight=(landscape?20:22)*1.25;
    ctx.save();ctx.beginPath();ctx.rect(desc.x,desc.y,desc.w,desc.h);ctx.clip();windowLines(ctx,description(card),desc.w,landscape?6:4).forEach((line,index)=>ctx.fillText(line,desc.x,desc.y+index*lineHeight));ctx.restore();
    windowLabel(ctx,card.CardId+(Number(card.Revision)>0?' · R'+card.Revision:''),windowBox(layout,'Number',width,height),'#3a414a',false,14,1);
    ctx.font='700 12px "Segoe UI", sans-serif';ctx.fillStyle='#3a414a';ctx.fillText('HYTCG'+(card.ImageSource==='zerochan'?'  /  ALT':''),width*(landscape?.65:.80),height*(landscape?.94:.936));
  }
  async function render(card,imageData) {
    const image=await loadImage(imageData),canvas=document.createElement('canvas'),size=dimensions(card),width=size.width,height=size.height,landscape=false;canvas.width=width;canvas.height=height;
    const ctx=canvas.getContext('2d'),color=/^#[0-9a-f]{6}$/i.test(card.RarityColor)?card.RarityColor:'#aebccc',style=styles[card.Rarity]||styles.Common;
    if(windowRarities.has(card.Rarity)){drawWindow(ctx,card,image,width,height,color,style);await drawBadge(ctx,card,true);return canvas.toDataURL('image/png');}
    ctx.fillStyle='#111521';rounded(ctx,0,0,width,height,37);ctx.fill();
    ctx.save();rounded(ctx,34,34,width-68,height-68,15);ctx.clip();
    const art={x:34,y:34,w:width-68,h:height-68},scale=Math.max(art.w/image.naturalWidth,art.h/image.naturalHeight);
    const drawnWidth=image.naturalWidth*scale,drawnHeight=image.naturalHeight*scale;
    ctx.drawImage(image,art.x+(art.w-drawnWidth)/2,art.y+(art.h-drawnHeight)/2,drawnWidth,drawnHeight);
    const top=ctx.createLinearGradient(0,34,0,224);top.addColorStop(0,'#070b1791');top.addColorStop(1,'#070b1700');ctx.fillStyle=top;ctx.fillRect(34,34,width-68,190);
    const fadeY=landscape?height*.47:height*.6,bottom=ctx.createLinearGradient(0,fadeY,0,height);bottom.addColorStop(0,'#080d1800');bottom.addColorStop(.45,'#080d1891');bottom.addColorStop(.8,'#080d18eb');bottom.addColorStop(1,'#080d18');ctx.fillStyle=bottom;ctx.fillRect(34,fadeY,width-68,height-fadeY);
    ctx.restore();perimeter(ctx,width,height,card.Rarity,color);
    ctx.font='700 22px "Segoe UI", sans-serif';ctx.fillStyle='#f2f4fc';ctx.textBaseline='top';ctx.fillText('HYTCG',52,54);
    const characterY=landscape?height-232:807,nameY=landscape?height-181:858;
    ctx.font='600 16px "Segoe UI", sans-serif';ctx.fillStyle='#becddd';ctx.fillText('CHARACTER  /  '+card.CharacterId,52,characterY);
    let fontSize=44;while(fontSize>31){ctx.font=`750 ${fontSize}px "Segoe UI", sans-serif`;if(ctx.measureText(card.Name).width<width-92)break;fontSize--;}
    const nameLines=lines(ctx,card.Name,width-104,2);ctx.fillStyle='#fff';ctx.textBaseline='alphabetic';nameLines.forEach((line,index)=>ctx.fillText(line,52,nameY+index*(fontSize+7)));
    const seriesY=landscape?(nameLines.length>1?height-89:height-132):(nameLines.length>1?951:918);ctx.font='400 23px "Segoe UI", sans-serif';ctx.fillStyle='#c4cad9';lines(ctx,card.Series||'Unknown series',width-104,1).forEach(line=>ctx.fillText(line,52,seriesY));
    ctx.strokeStyle='#becddd4b';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(52,height-72);ctx.lineTo(width-52,height-72);ctx.stroke();
    ctx.font='500 13px "Segoe UI", sans-serif';ctx.fillStyle='#b4bed2';ctx.fillText(card.CardId+(Number(card.Revision)>0?' · R'+card.Revision:''),52,height-44);
    if(card.ImageSource==='zerochan'){ctx.textAlign='right';ctx.font='700 14px "Segoe UI", sans-serif';ctx.fillStyle='#b4bed2';ctx.fillText('ALT',width-52,height-44);}
    await drawBadge(ctx,card,false);return canvas.toDataURL('image/png');
  }
  async function drawBadge(ctx,card,windowCard) {
    const w=180,box={x:width-w-52,y:windowCard?height*.652:40,w,h:64};
    if(window.HyTcgRarity){await HyTcgRarity.draw(ctx,card.Rarity,box);return;}
    const index=Math.max(0,['Common','Uncommon','Rare','SR','SSR','UR'].indexOf(card.Rarity)),ink=['#9baec8','#6ed4a5','#6cabff','#c093f4','#efc975','#ec91bf'][index];
    ctx.save();rounded(ctx,box.x,box.y,box.w,box.h,9);ctx.fillStyle='#101927';ctx.fill();ctx.strokeStyle=ink;ctx.lineWidth=1.5;ctx.stroke();
    ctx.font='700 17px "Segoe UI",sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillStyle=ink;ctx.fillText(card.Rarity,box.x+box.w/2,box.y+box.h/2);ctx.restore();
  }
  return {render,width,height,dimensions,windowLayout,isWindowRarity:name=>windowRarities.has(name),isHighRarity:name=>!!styles[name]?.foil};
})();
