import{smoothstep,graphemes}from'./opening-motion-core.mjs';
export const PHOTO={entryEnd:4.2,returnEnd:3.6,glitchStart:1.42,glitchEnd:1.58,callStart:2.4,callStep:.11};
export function photoFrame(phase,t){
 const entry=phase==='photo-entry';
 const text=entry?'수혁아.':'아빠?';
 const start=entry?PHOTO.callStart:3.1;
 return {entry,opacity:entry?smoothstep(t/.45):1,zoom:entry?smoothstep((t-.45)/1.25):1,
  blur:entry?13:13*(1-smoothstep((t-.55)/.85)),
  face:entry?1:1,glitch:!entry&&t>=PHOTO.glitchStart&&t<PHOTO.glitchEnd,
  black:entry?0:1-smoothstep(t/.5),dim:smoothstep((t-(entry?2:2.7))/.4),
  text:graphemes(text).slice(0,t<start?0:1+Math.floor((t-start+1e-9)/PHOTO.callStep)).join(''),
  complete:t>=(entry?PHOTO.entryEnd:PHOTO.returnEnd)};
}
export async function setupPhoto(){
 const el=id=>document.getElementById(id),canvas=el('photo-canvas'),ctx=canvas.getContext('2d');
 const load=async src=>{const im=new Image();im.src='/'+src;await im.decode();return im;};
 const root='art/chapter00/memory/dialogue-v1/';
 const [photo,mask,frame,camper]=await Promise.all([
  load(root+'family-sunset-source-v1.png'),load(root+'seoyeon-face-mask-v1.png'),
  load(root+'polaroid-frame-v1.png'),load('art/chapter01/revision-v3/camper-clean-base-v3.png')]);
 const mosaic=document.createElement('canvas');mosaic.width=photo.width;mosaic.height=photo.height;
 const mc=mosaic.getContext('2d'),tiny=document.createElement('canvas');tiny.width=76;tiny.height=43;
 tiny.getContext('2d').drawImage(photo,0,0,76,43);mc.imageSmoothingEnabled=false;mc.drawImage(tiny,0,0,photo.width,photo.height);
 mc.globalCompositeOperation='destination-in';mc.drawImage(mask,0,0);
 const picture=document.createElement('canvas');picture.width=photo.width;picture.height=photo.height;const pc=picture.getContext('2d');
 pc.drawImage(photo,0,0);pc.drawImage(mosaic,0,0);
 const copy=document.createElement('canvas');copy.width=1920;copy.height=1080;
 let audioContext,lastReturn=-1,played=false,lastCaption='';
 function unlock(){try{audioContext??=new AudioContext();void audioContext.resume().catch(()=>{});}catch{}}
 function crackle(){if(!audioContext||audioContext.state!=='running'||!el('sound-preview').checked)return;
  const n=Math.floor(audioContext.sampleRate*.16),buffer=audioContext.createBuffer(1,n,audioContext.sampleRate),a=buffer.getChannelData(0);
  for(let i=0;i<n;i++)a[i]=(Math.random()*2-1)*Math.sin(Math.PI*i/n)*.055;
  const source=audioContext.createBufferSource();source.buffer=buffer;const filter=audioContext.createBiquadFilter();filter.type='highpass';filter.frequency.value=1100;
  source.connect(filter).connect(audioContext.destination);source.start();
 }
 return {unlock,reset(){lastReturn=-1;played=false;},render(phase,t,animate=false){
  const visible=phase==='photo-entry'||phase==='photo-return';el('photo-scene').hidden=!visible;if(!visible)return;
  const f=photoFrame(phase,t);el('photo-scene').style.opacity=f.opacity;ctx.clearRect(0,0,1920,1080);
  ctx.save();ctx.filter=`blur(${f.zoom*4}px)`;ctx.drawImage(camper,0,0,1920,1080);ctx.restore();
  ctx.fillStyle=`rgba(13,18,15,${f.zoom*.46})`;ctx.fillRect(0,0,1920,1080);
  // Inspect the wall photo as an insert. The base painting itself stays unchanged.
  const q=f.zoom,w=44+(1320-44)*q,h=w*frame.height/frame.width,x=1442+(300-1442)*q,y=269+(70-269)*q;
  const s=w/frame.width,px=x+56*s,py=y+56*s;
  ctx.save();ctx.beginPath();ctx.rect(px,py,photo.width*s,photo.height*s);ctx.clip();ctx.filter=`blur(${f.blur*s}px)`;
  ctx.drawImage(picture,px,py,photo.width*s,photo.height*s);ctx.restore();ctx.drawImage(frame,x,y,w,h);
  if(f.glitch){copy.getContext('2d').drawImage(canvas,0,0);const k=Math.floor((t-PHOTO.glitchStart)*60);
   for(let i=0;i<7;i++){const yy=(i*149+k*23)%1080;ctx.drawImage(copy,0,yy,1920,13+(i%3)*9,(i%2?1:-1)*(8+k%4*5),yy,1920,13+(i%3)*9);}
   ctx.fillStyle='rgba(205,216,201,.1)';for(let yy=k%5;yy<1080;yy+=7)ctx.fillRect(0,yy,1920,1);
  }
  if(phase==='photo-return'){
   if(animate&&!played&&lastReturn<PHOTO.glitchEnd&&t>=PHOTO.glitchStart&&t<PHOTO.glitchEnd+.1){played=true;crackle();}
   if(animate)lastReturn=t;
  }
  const caption=f.entry?'수혁아.':'아빠?';
  if(caption!==lastCaption){lastCaption=caption;el('photo-call').replaceChildren(...graphemes(caption).map(char=>{const span=document.createElement('span');span.textContent=char;return span;}));}
  [...el('photo-call').children].forEach((span,i)=>span.style.visibility=i<graphemes(f.text).length?'visible':'hidden');
  el('photo-dim').style.opacity=f.dim;el('photo-black').style.opacity=f.black;
  el('photo-scene').dataset.glitch=String(f.glitch);el('photo-scene').dataset.face='unresolved';el('photo-scene').dataset.blur=f.blur.toFixed(3);
  el('photo-cue').hidden=!f.complete;
 }};
}
