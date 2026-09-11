import{graphemes,smoothstep}from'./opening-motion-core.mjs';
import{speakerLabel}from'./speaker-identity.mjs';
export function lineEnd(data,index){const line=data.lines[index];return (index===0?data.timing.arrival_hold+data.timing.panel_fade:data.timing.portrait_fade)+(graphemes(line.text).length-1)*data.timing.seconds_per_grapheme;}
export function lineFrame(data,index,time){
 const t=data.timing,line=data.lines[index],start=index===0?t.arrival_hold+t.panel_fade:t.portrait_fade;
 return {panel:index===0?smoothstep((time-t.arrival_hold)/t.panel_fade):line.reenter_panel?smoothstep(time/t.panel_fade):1,count:time<start?0:Math.min(graphemes(line.text).length,1+Math.floor((time-start+1e-9)/t.seconds_per_grapheme)),cue:smoothstep((time-lineEnd(data,index))/t.cue_fade),portrait:smoothstep(time/t.portrait_fade)};
}
export async function setupDialogue(){
 const r=await fetch('./memory-dialogue.json');if(!r.ok)throw Error('Dialogue data missing');
 const data=await r.json(),el=id=>document.getElementById(id);let current=-1;
 const images=[];
 for(const[id,src]of[['memory-ground',data.background],['memory-feet',data.feet],['memory-held',data.handoff_preview],['memory-present',data.present_background],['portrait-suhyeok',data.portraits.suhyeok.png],['portrait-soi',data.portraits.soi.png],['handoff',data.handoff_preview]]){el(id).src='/'+src;images.push(el(id).decode());}
 for(const[who,asset]of Object.entries(data.portraits)){const[x,y,w,h]=asset.rect;Object.assign(el('portrait-'+who).style,{left:x/19.2+'%',top:y/10.8+'%',width:w/19.2+'%',height:h/10.8+'%'});}
 await Promise.all(images);
 return {data,
  duration:index=>lineEnd(data,index)+data.timing.cue_fade,
  fullTextTime:index=>lineEnd(data,index),
  render(index,time,visible,handoff=false){
   const group=el('memory-dialogue');group.hidden=!visible;
   el('memory').hidden=!visible;
   el('handoff').hidden=!handoff;
   if(!visible){el('memory').style.filter='none';return;}
   if(handoff){group.hidden=true;return;}
   const line=data.lines[index],f=lineFrame(data,index,time);
   const held=line.background==='handhold',present=line.background==='present';
   el('memory-ground').hidden=held||present;el('memory-feet').hidden=held||present;el('memory-held').hidden=!held;el('memory-present').hidden=!present;
   if(current!==index){current=index;el('dialogue-text').replaceChildren(...graphemes(line.text).map(char=>{const s=document.createElement('span');s.textContent=char;return s;}));el('dialogue-name').textContent=speakerLabel(data,index);}
   group.dataset.lineId=line.id;group.dataset.visibleGraphemes=f.count;
   el('dialogue-panel').style.opacity=f.panel;
   el('dialogue-nameplate').hidden=!line.speaker;
   Array.from(el('dialogue-text').children).forEach((s,i)=>s.style.visibility=i<f.count?'visible':'hidden');
   el('dialogue-cue').style.opacity=line.end_preview?0:f.cue;
   const scale=el('stage').clientWidth/1920;
   el('memory').style.filter=`blur(${index===0?0:data.blur_logical_px*scale*(index===1||line.reenter_panel?f.portrait:1)}px)`;
   for(const who of Object.keys(data.portraits)){
    const im=el('portrait-'+who),show=line.portraits.includes(who)&&!data.suppressed_portraits.includes(who);
    im.hidden=!show;
    im.style.opacity=show?((index===1||who==='suhyeok'||line.reenter_panel)?f.portrait:1):0;
    im.style.filter=who==='seoyeon'&&index===3?'brightness(.8)':'none';
   }
   const text=f.cue===1?line.text:'';if(el('dialogue-accessible').textContent!==text)el('dialogue-accessible').textContent=text;
  }
 };
}
