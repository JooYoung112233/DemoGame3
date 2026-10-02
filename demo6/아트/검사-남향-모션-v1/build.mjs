import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createRequire} from 'node:module';
const here=path.dirname(fileURLToPath(import.meta.url));
const deps=process.env.CODEX_NODE_MODULES||'C:/Users/power/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules';
const require=createRequire(import.meta.url),sharp=require(path.join(deps,'sharp')), {chromium}=require(path.join(deps,'playwright'));
const mode=process.argv[2]||'stills';
const sources=['head','torso','arms','legs','sword'];
const regions={},data={},sourceReport={};
function bounds(raw,w,h,half){let x0=w,y0=h,x1=0,y1=0,solid=0;
 for(let y=0;y<h;y++)for(let x=half?.[0]??0;x<(half?.[1]??w);x++)if(raw[(y*w+x)*4+3]>100){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);solid++}
 x0=Math.max(half?.[0]??0,x0-3);x1=Math.min((half?.[1]??w)-1,x1+3);y0=Math.max(0,y0-3);y1=Math.min(h-1,y1+3);return [x0,y0,x1-x0+1,y1-y0+1];}
for(const name of sources){const file=path.join(here,'sources',name+'.png');const {data:raw,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 data[name]='data:image/png;base64,'+fs.readFileSync(file).toString('base64');sourceReport[name]={width:info.width,height:info.height};
 if(name==='arms'||name==='legs'){
  for(const [i,side] of ['left','right'].entries()){const id=side+(name==='arms'?'Arm':'Leg');regions[id]={source:name,rect:bounds(raw,info.width,info.height,[i*Math.floor(info.width/2),(i+1)*Math.floor(info.width/2)])};}
 }else regions[name]={source:name,rect:bounds(raw,info.width,info.height)};
}
fs.writeFileSync(path.join(here,'regions.json'),JSON.stringify({sources:sourceReport,regions},null,2));
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
const page=await browser.newPage({viewport:{width:1024,height:1024}});page.on('pageerror',e=>console.error(e));
await page.setContent('<canvas id="art" width="1536" height="1536"></canvas>');await page.addScriptTag({path:path.join(here,'rig.js')});
await page.evaluate(async({data,regions})=>{window.images={};for(const [id,url] of Object.entries(data)){const im=new Image();im.src=url;await im.decode();images[id]=im;}window.rig=new SouthRig.Rig(images,regions);window.canvas=document.getElementById('art');window.ctx=canvas.getContext('2d');}, {data,regions});
const out=path.join(here,'renders');fs.mkdirSync(out,{recursive:true});
async function saveFrame(file,clip,phase,size=1536,bg=null){const png=await page.evaluate(({clip,phase,size,bg})=>{canvas.width=canvas.height=size;rig.draw(ctx,SouthRig.pose(clip,phase),{size,background:bg});return canvas.toDataURL().split(',')[1];},{clip,phase,size,bg});fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,Buffer.from(png,'base64'));}
if(mode==='stills'){
 for(const [name,phase] of [['idle',0],['walk',.25],['walk',.75],['run',.25],['run',.75],['attack',.23],['attack',.43],['attack',.55]])await saveFrame(path.join(out,`${name}-${Math.round(phase*100)}.png`),name,phase,900,'#293139');
}else{
 const clips=await page.evaluate(()=>SouthRig.clips);const manifest={direction:'south',technique:'painted 2D mesh cutout animation',canvas:[1536,1536],pivotPixels:[763.2,1216.5],clips:{},sourceResolution:sourceReport,transitions:{idle_walk:.18,walk_run:.18,run_walk:.18,to_idle:.22,to_attack:.12,method:'pose blend; preserve walk/run normalized gait phase'}};
 for(const [name,clip] of Object.entries(clips)){
  manifest.clips[name]={...clip,sampleFps:clip.loop?clip.frames/clip.duration:(clip.frames-1)/clip.duration,files:[]};
  for(let i=0;i<clip.frames;i++){const rel=`frames/${name}/${String(i).padStart(2,'0')}.png`;await saveFrame(path.join(here,rel),name,i/(clip.loop?clip.frames:clip.frames-1));manifest.clips[name].files.push(rel);}
  console.log('exported',name,clip.frames);
 }
 fs.writeFileSync(path.join(here,'animation-manifest.json'),JSON.stringify(manifest,null,2));
 const fps=30,count=312;const schedule=[[0,'idle'],[1.6,'walk'],[3.6,'run'],[5.4,'walk'],[6.4,'idle'],[7.5,'attack']];
 await page.evaluate(()=>{window.ctrl=new SouthRig.Controller();canvas.width=canvas.height=640;});
 let event=0;const trace=[];
 for(let f=0;f<count;f++){
  const t=f/fps;let change=null;if(event<schedule.length&&t>=schedule[event][0]){change=schedule[event][1];event++;}
  const result=await page.evaluate(({change,dt,t})=>{if(change)ctrl.set(change);const p=ctrl.step(dt);rig.draw(ctx,p,{size:640,background:'#293139',shadow:true});ctx.fillStyle='#e9e7df';ctx.font='18px sans-serif';const names={idle:'IDLE',walk:'WALK',run:'RUN',attack:'ATTACK'};ctx.fillText(names[ctrl.state],24,34);return {png:canvas.toDataURL().split(',')[1],pose:p,state:ctrl.state,phase:ctrl.phase};},{change,dt:1/fps,t});
  const folder=path.join(here,'review-sequence');fs.mkdirSync(folder,{recursive:true});fs.writeFileSync(path.join(folder,String(f).padStart(4,'0')+'.png'),Buffer.from(result.png,'base64'));delete result.png;trace.push({frame:f,time:t,...result});
 }
 fs.writeFileSync(path.join(here,'transition-trace.json'),JSON.stringify(trace));
 // Four independent clips shown simultaneously at their actual playback speed.
 await page.evaluate(()=>{window.small=document.createElement('canvas');small.width=small.height=420;window.sc=small.getContext('2d');canvas.width=840;canvas.height=900;});
 for(let f=0;f<96;f++){
  const png=await page.evaluate(f=>{ctx.setTransform(1,0,0,1,0,0);ctx.fillStyle='#293139';ctx.fillRect(0,0,840,900);let i=0;for(const [name,clip] of Object.entries(SouthRig.clips)){const time=f/20,phase=name==='attack'?Math.min(1,(time%1.6)/.8):(time%clip.duration)/clip.duration;rig.draw(sc,SouthRig.samplePose(name,phase),{size:420,background:'#293139',shadow:true});const x=(i%2)*420,y=Math.floor(i/2)*450;ctx.drawImage(small,x,y+25);ctx.fillStyle='#e9e7df';ctx.font='18px sans-serif';ctx.fillText(name.toUpperCase()+' · '+clip.frames+' frames',x+24,y+28);i++;}return canvas.toDataURL().split(',')[1];},f);
  const folder=path.join(here,'review-grid');fs.mkdirSync(folder,{recursive:true});fs.writeFileSync(path.join(folder,String(f).padStart(4,'0')+'.png'),Buffer.from(png,'base64'));
 }
}
// Functional review only: keyboard state changes exercise the same shared pose controller.
fs.writeFileSync(path.join(here,'preview.html'),`<!doctype html><meta charset="utf-8"><title>검사 남향 모션 검수</title><style>body{background:#293139;color:#eee;font:16px sans-serif;margin:16px}canvas{width:min(80vh,90vw);display:block}button{margin-right:8px}</style><p>1 대기 · 2 걷기 · 3 달리기 · 4 공격 / 상태를 바꿔 연결 확인</p><p><button onclick="ctrl.set('idle')">대기</button><button onclick="ctrl.set('walk')">걷기</button><button onclick="ctrl.set('run')">달리기</button><button onclick="ctrl.set('attack')">공격</button></p><canvas id="art" width="900" height="900"></canvas><script src="rig.js"></script><script>const regions=${JSON.stringify(regions)};const imgs={};let ctrl;Promise.all(${JSON.stringify(sources)}.map(async n=>{const im=new Image();im.src='sources/'+n+'.png';await im.decode();imgs[n]=im})).then(()=>{const rig=new SouthRig.Rig(imgs,regions),ctx=art.getContext('2d');ctrl=new SouthRig.Controller();document.onkeydown=e=>{const states={1:'idle',2:'walk',3:'run',4:'attack'};if(states[e.key])ctrl.set(states[e.key])};let prev=performance.now();function draw(now){const dt=Math.min(.05,(now-prev)/1000);prev=now;rig.draw(ctx,ctrl.step(dt),{size:900,background:'#293139',shadow:true});requestAnimationFrame(draw)}requestAnimationFrame(draw)})</script>`);
await browser.close();console.log('Done:',mode);


