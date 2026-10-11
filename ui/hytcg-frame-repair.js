'use strict';
window.HyTcgFrameRepair=(()=>{
  const queued=new Set(),attempted=new Set();let running=0;
  const observer=new IntersectionObserver(entries=>{
    if(tcg.view!=='collection'||document.hidden)return;
    for(const entry of entries){if(!entry.isIntersecting)continue;const index=Number(entry.target.dataset.detail),card=tcg.collection[index]?.Card;if(!card||attempted.has(card.CardId))continue;queued.add(card.CardId);observer.unobserve(entry.target);}
    pump();
  },{root:document.getElementById('tcgScroll'),rootMargin:'40px'});
  function observe(){
    observer.disconnect();queued.clear();
    if(tcg.view!=='collection')return;
    for(const node of document.querySelectorAll('#collectionGrid [data-collection="true"]')){
      const card=tcg.collection[Number(node.dataset.detail)]?.Card;
      const low=card&&['Common','Uncommon','Rare','SR'].includes(card.Rarity),marker=low?'window-v4':'full-v4';
      const reusable=low&&['window-v2','window-v3'].includes(card?.Metadata?.hytcg_card_layout);
      if(card&&card.Metadata?.hytcg_card_layout!==marker&&!reusable&&/^https:\/\//.test(card.SourceImage||'')&&!attempted.has(card.CardId))observer.observe(node);
    }
  }
  async function pump(){
    if(running||!queued.size||tcg.view!=='collection'||document.hidden)return;
    const ids=[...queued].slice(0,2),stamp=tcg.collectionRequest;
    for(const id of ids){queued.delete(id);attempted.add(id);}running++;
    try{
      const response=await call('hytcgRepairCards',{cardIds:ids});
      if(tcg.collectionRequest!==stamp)return;
      for(const repaired of response.items||[]){
        const index=tcg.collection.findIndex(item=>item.Card.CardId===repaired.cardId);if(index<0||!repaired.imageData)continue;
        tcg.collection[index].ImagePath=tcg.collection[index].Thumbnail=repaired.imageData;
        const image=document.querySelector(`#collectionGrid [data-collection="true"][data-detail="${index}"] img`);if(image)image.src=repaired.imageData;
      }
    }catch{}
    finally{running--;pump();}
  }
  return {observe};
})();
