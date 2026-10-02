import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';import {createRequire} from 'node:module';
const root=path.dirname(fileURLToPath(import.meta.url));const require=createRequire(import.meta.url);
const deps=process.env.CODEX_NODE_MODULES||'C:/Users/power/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules';
const sharp=require(path.join(deps,'sharp'));const {chromium}=require(path.join(deps,'playwright'));
const partial=process.argv.includes('--partial');
const clips=JSON.parse(fs.readFileSync(path.join(root,'clips.json'),'utf8'));
const files=fs.readdirSync(path.join(root,'sources')).filter(n=>n.endsWith('.png'));
const images={},anchors={},sourceReport={};
for(const name of files){const id=name.slice(0,-4),file=path.join(root,'sources',name);images[id]='data:image/png;base64,'+fs.readFileSync(file).toString('base64');
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});const w=info.width,h=info.height;
 // Use the crown region to remove generation-time canvas translation; no per-limb warping.
 let top=0;for(let y=0;y<Math.min(500,h);y++){let count=0;for(let x=Math.floor(w*.28);x<Math.floor(w*.68);x++)if(data[(y*w+x)*4+3]>180)count++;if(count>=20){top=y;break;}}
 let x0=w,x1=0;for(let y=top+40;y<Math.min(top+180,h);y++)for(let x=Math.floor(w*.24);x<Math.floor(w*.73);x++)if(data[(y*w+x)*4+3]>180){x0=Math.min(x0,x);x1=Math.max(x1,x)}
 let footBottom=0;for(let y=Math.floor(h*.65);y<h;y++)for(let x=Math.floor(w*.22);x<Math.floor(w*.70);x++)if(data[(y*w+x)*4+3]>180)footBottom=Math.max(footBottom,y);
 anchors[id]={x:(x0+x1)/2,y:top,scale:1,headSpan:x1-x0,footBottom};sourceReport[id]={width:w,height:h};
}
// Idle drawings register at the planted soles; breathing does not translate the feet.
const standingHeight=anchors.neutral.footBottom-anchors.neutral.y;
for(const[id,a]of Object.entries(anchors))if(id.startsWith('idle-'))a.y=a.footBottom-standingHeight;
const overridesFile=path.join(root,'anchor-overrides.json');if(fs.existsSync(overridesFile))for(const [id,override]of Object.entries(JSON.parse(fs.readFileSync(overridesFile,'utf8'))))anchors[id]={...anchors[id],...override};
fs.writeFileSync(path.join(root,'anchors.json'),JSON.stringify(anchors,null,2));
const ready=Object.fromEntries(Object.entries(clips).filter(([id,c])=>c.frames.every(f=>images[f])));
const missing=[...new Set(Object.values(clips).flatMap(c=>c.frames))].filter(id=>!images[id]);
if(!partial&&missing.length)throw new Error('Missing source frames: '+missing.join(', '));
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});const page=await browser.newPage();
await page.setContent('<canvas id="art" width="1536" height="1536"></canvas>');await page.addScriptTag({path:path.join(root,'player.js')});
await page.evaluate(async({images,anchors,clips})=>{window.art=document.querySelector('canvas');window.ctx=art.getContext('2d');window.imgs={};for(const[id,src]of Object.entries(images)){const im=new Image();im.src=src;await im.decode();imgs[id]=im;}window.anchors=anchors;window.clips=clips;},{images,anchors,clips});
async function save(file,pose,size=1536,background=null){const b64=await page.evaluate(({pose,size,background})=>{art.width=art.height=size;CelMotion.draw(ctx,imgs,anchors,pose,{width:size,height:size,background});return art.toDataURL().split(',')[1]},{pose,size,background});fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,Buffer.from(b64,'base64'));}
const manifest={technique:'individually redrawn full-body cel poses, no skeletal limb deformation',canvas:[1536,1536],sourceReport,clips:ready,missing};
for(const[name,c]of Object.entries(ready)){for(let i=0;i<c.frames.length;i++)await save(path.join(root,'frames',name,String(i).padStart(2,'0')+'.png'),{id:c.frames[i],bob:c.bob[i]});console.log('rendered',name,c.frames.length);}
fs.writeFileSync(path.join(root,'animation-manifest.json'),JSON.stringify(manifest,null,2));
if(!partial){
 await page.evaluate(()=>{art.width=art.height=640;window.player=new CelMotion.Player(clips)});
 const timeline=[[0,'idle'],[1.6,'walk'],[4.1,'run'],[6.3,'walk'],[8,'idle'],[9.7,'attack']];let next=0;const trace=[];
 for(let f=0;f<390;f++){const t=f/30;let request=null;if(next<timeline.length&&t>=timeline[next][0])request=timeline[next++][1];
  const r=await page.evaluate(({request,t})=>{if(request)player.request(request);const pose=player.step(1/30);CelMotion.draw(ctx,imgs,anchors,pose,{width:640,height:640,shadow:true});ctx.font='18px sans-serif';ctx.fillStyle='#eee';ctx.fillText(pose.state.toUpperCase(),22,30);return {pose,b64:art.toDataURL().split(',')[1]}},{request,t});
  const folder=path.join(root,'review-sequence');fs.mkdirSync(folder,{recursive:true});fs.writeFileSync(path.join(folder,String(f).padStart(4,'0')+'.png'),Buffer.from(r.b64,'base64'));trace.push({t,...r.pose});
 }
 fs.writeFileSync(path.join(root,'transition-trace.json'),JSON.stringify(trace));
 await page.evaluate(()=>{window.cell=document.createElement('canvas');cell.width=cell.height=400;window.cc=cell.getContext('2d');art.width=800;art.height=840;});
 for(let f=0;f<120;f++){const b64=await page.evaluate(f=>{ctx.fillStyle='#293139';ctx.fillRect(0,0,800,840);let j=0;for(const name of ['idle','walk','run','attack']){const c=clips[name],total=c.durations.reduce((a,b)=>a+b,0);let ms=(f*50)%(name==='attack'?1600:total),i=0;while(i<c.frames.length-1&&ms>=c.durations[i])ms-=c.durations[i++];const pose={id:c.frames[i],bob:c.bob[i]};CelMotion.draw(cc,imgs,anchors,pose,{width:400,height:400,shadow:true});const x=j%2*400,y=Math.floor(j/2)*420;ctx.drawImage(cell,x,y+20);ctx.fillStyle='#eee';ctx.font='17px sans-serif';ctx.fillText(name.toUpperCase()+' · '+c.frames.length+' frames',x+18,y+24);j++;}return art.toDataURL().split(',')[1]},f);
  const folder=path.join(root,'review-grid');fs.mkdirSync(folder,{recursive:true});fs.writeFileSync(path.join(folder,String(f).padStart(4,'0')+'.png'),Buffer.from(b64,'base64'));
 }
}
const sourceIds=Object.keys(images);
fs.writeFileSync(path.join(root,'preview.html'),`<!doctype html><meta charset="utf-8"><title>검사 남향 모션 v2</title><style>body{background:#293139;color:#eee;font:16px sans-serif;margin:16px}canvas{width:min(80vh,90vw);display:block}button{margin-right:8px}</style><p>1 대기 · 2 걷기 · 3 달리기 · 4 공격</p><p><button onclick="player.request('idle')">대기</button><button onclick="player.request('walk')">걷기</button><button onclick="player.request('run')">달리기</button><button onclick="player.request('attack')">공격</button></p><canvas id="art" width="900" height="900"></canvas><script src="player.js"></script><script>const clips=${JSON.stringify(clips)},anchors=${JSON.stringify(anchors)},imgs={};let player;Promise.all(${JSON.stringify(sourceIds)}.map(async id=>{const im=new Image();im.src='sources/'+id+'.png';await im.decode();imgs[id]=im})).then(()=>{player=new CelMotion.Player(clips);const ctx=art.getContext('2d');document.onkeydown=e=>{const s={1:'idle',2:'walk',3:'run',4:'attack'}[e.key];if(s)player.request(s)};let previous=performance.now();function loop(now){const dt=Math.min(.05,(now-previous)/1000);previous=now;CelMotion.draw(ctx,imgs,anchors,player.step(dt),{width:900,height:900,shadow:true});requestAnimationFrame(loop)}requestAnimationFrame(loop)})</script>`);
await browser.close();console.log('missing',missing);
