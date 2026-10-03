// Pixel-preserving PSD packaging. All artwork is generated with ImageGen.
// Native sprite regions are copied losslessly; no resizing, recoloring or repainting.
const fs = require('fs'), path = require('path'), crypto = require('crypto'), assert = require('assert');
const {PNG} = require(process.env.EAST_TRAIN_PNGJS || 'C:/Users/power/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pngjs');
const project = path.resolve(__dirname, '..');
const dir = path.join(project, '아트/전체-리소스-v2');
const report = JSON.parse(fs.readFileSync(path.join(dir, 'import-report.json'), 'utf8'));
const cache = new Map();
const u16 = n => { const b=Buffer.alloc(2); b.writeUInt16BE(n & 65535); return b; };
const u32 = n => { const b=Buffer.alloc(4); b.writeUInt32BE(n >>> 0); return b; };
const cat = a => Buffer.concat(a);
function region(e) {
  let p=cache.get(e.source);
  if(!p){p=PNG.sync.read(fs.readFileSync(path.join(project,e.source)));cache.set(e.source,p);}
  const {x,y,width:w,height:h}=e.pixels, data=Buffer.alloc(w*h*4);
  const top=p.height-y-h;
  for(let row=0;row<h;row++) p.data.copy(data,row*w*4,((top+row)*p.width+x)*4,((top+row)*p.width+x+w)*4);
  return {name:e.name,w,h,data};
}
function pack(row){
  const out=[];let i=0;
  while(i<row.length){
    let n=1;while(i+n<row.length && row[i+n]===row[i] && n<128)n++;
    if(n>=3){out.push(257-n,row[i]);i+=n;continue;}
    const start=i;i+=n;
    while(i<row.length && i-start<128){
      let run=1;while(i+run<row.length && row[i+run]===row[i] && run<3)run++;
      if(run>=3)break;
      i+=Math.min(run,128-(i-start));
    }
    out.push(i-start-1,...row.subarray(start,i));
  }
  return Buffer.from(out);
}
function channel(data,w,h,index){
  const rows=[];
  for(let y=0;y<h;y++){const row=Buffer.alloc(w);for(let x=0;x<w;x++)row[x]=data[(y*w+x)*4+index];rows.push(pack(row));}
  return cat([u16(1),...rows.map(r=>u16(r.length)),...rows]);
}
function writePSD(name,layers){
  const w=Math.max(...layers.map(l=>l.w)),h=Math.max(...layers.map(l=>l.h));
  const records=[],payloads=[];
  for(let i=0;i<layers.length;i++){
    const l=layers[i], channels=[3,0,1,2].map(c=>channel(l.data,l.w,l.h,c));
    const label=Buffer.from(l.name,'ascii'), pascal=cat([Buffer.from([label.length]),label,Buffer.alloc((4-(label.length+1)%4)%4)]);
    const extra=cat([u32(0),u32(0),pascal]);
    records.push(cat([u32(0),u32(0),u32(l.h),u32(l.w),u16(4),
      ...channels.flatMap((c,j)=>[u16([-1,0,1,2][j]),u32(c.length)]),
      Buffer.from('8BIMnorm'),Buffer.from([255,0,i===0?0:2,0]),u32(extra.length),extra]));
    payloads.push(...channels);
  }
  let info=cat([u16(-layers.length),...records,...payloads]);if(info.length%2)info=cat([info,Buffer.alloc(1)]);
  const section=cat([u32(info.length),info,u32(0)]);
  const merged=Buffer.alloc(w*h*4),visible=layers[0];
  for(let y=0;y<visible.h;y++)visible.data.copy(merged,y*w*4,y*visible.w*4,(y+1)*visible.w*4);
  const composite=[0,1,2,3].map(c=>channel(merged,w,h,c));
  const header=cat([Buffer.from('8BPS'),u16(1),Buffer.alloc(6),u16(4),u32(h),u32(w),u16(8),u16(3)]);
  const bytes=cat([header,u32(0),u32(0),u32(section.length),section,u16(1),
    ...composite.map(c=>c.subarray(2,2+h*2)),...composite.map(c=>c.subarray(2+h*2))]);
  const target=path.join(dir,'PSD',name+'.psd');fs.writeFileSync(target,bytes);
  verifyPSD(target,layers,merged,w,h);
  return {file:'PSD/'+name+'.psd',width:w,height:h,layers:layers.map(l=>({name:l.name,width:l.w,height:l.h,visible:l===layers[0]})),allNativePixelsVerified:true};
}
function unpackRow(data){
  const out=[];let i=0;while(i<data.length){const n=data[i++];if(n<=127){out.push(...data.subarray(i,i+n+1));i+=n+1;}else if(n>=129){const v=data[i++];for(let k=0;k<257-n;k++)out.push(v);}}return Buffer.from(out);
}
function verifyPSD(file,expected,merged,w,h){
  const b=fs.readFileSync(file);let p=26;
  const r16=()=>{const n=b.readUInt16BE(p);p+=2;return n;},r32=()=>{const n=b.readUInt32BE(p);p+=4;return n;};
  for(let i=0;i<2;i++){const n=r32();p+=n;}
  const sectionSize=r32(),sectionEnd=p+sectionSize; r32();
  const rawCount=r16(),count=65536-rawCount;assert.equal(count,expected.length);
  const records=[];
  for(let i=0;i<count;i++){
    const top=r32(),left=r32(),bottom=r32(),right=r32(),n=r16(),chs=[];
    for(let c=0;c<n;c++){let id=r16();if(id===65535)id=-1;chs.push({id,len:r32()});}
    p+=8;const visible=(b[p+2]&2)===0;p+=4;const extra=r32(),end=p+extra;
    let len=r32();p+=len;len=r32();p+=len;len=b[p++];const name=b.toString('ascii',p,p+len);p=end;
    assert.equal(name,expected[i].name);assert.equal(visible,i===0);assert.equal(top,0);assert.equal(left,0);
    assert.equal(right,expected[i].w);assert.equal(bottom,expected[i].h);records.push({w:right,h:bottom,chs});
  }
  for(let i=0;i<count;i++)for(const c of records[i].chs){
    const rec=records[i],end=p+c.len;assert.equal(r16(),1);const lens=[];for(let y=0;y<rec.h;y++)lens.push(r16());
    const channelIndex=c.id<0?3:c.id;
    for(let y=0;y<rec.h;y++){
      const row=unpackRow(b.subarray(p,p+lens[y]));p+=lens[y];assert.equal(row.length,rec.w);
      for(let x=0;x<rec.w;x++)assert.equal(row[x],expected[i].data[(y*rec.w+x)*4+channelIndex]);
    }assert.equal(p,end);
  }
  p=sectionEnd;assert.equal(r16(),1);const sizes=[];for(let c=0;c<4;c++)for(let y=0;y<h;y++)sizes.push(r16());
  for(let c=0;c<4;c++)for(let y=0;y<h;y++){
    const n=sizes[c*h+y],row=unpackRow(b.subarray(p,p+n));p+=n;
    for(let x=0;x<w;x++)assert.equal(row[x],merged[(y*w+x)*4+c]);
  }
}
fs.mkdirSync(path.join(dir,'PSD'),{recursive:true});
const layers=report.entries.map(region);
const reusedOriginals=JSON.parse(fs.readFileSync(path.join(project,'아트/기관실-리소스-v1/manifest.json'),'utf8').replace(/^\uFEFF/,''));
for(const old of reusedOriginals){
  layers.push(region({name:old.file.replace('.png',''),source:'Assets/Art/FirstPass/'+old.file,
    pixels:{x:0,y:0,width:old.width,height:old.height}}));
}
const environments=new Set(['snow-horizon','snow-ground','rail-strip','station','snow-drift','rocks','marker']);
const groups=[
  ['world-native',layers.filter(l=>environments.has(l.name))],
  ['train-native',layers.filter(l=>!environments.has(l.name)&&!l.name.startsWith('traveller-'))],
  ['traveller-native',layers.filter(l=>l.name.startsWith('traveller-'))]
];
const results=groups.map(([name,list])=>writePSD(name,list));
const originals=fs.readdirSync(path.join(dir,'originals')).filter(n=>n.endsWith('.png')).map(name=>{
  const raw=fs.readFileSync(path.join(dir,'originals',name)),copy=fs.readFileSync(path.join(project,'Assets/Art/WorldPass',name));
  assert(raw.equals(copy),'Unity copy differs: '+name);
  const png=PNG.sync.read(raw);
  return {file:'originals/'+name,width:png.width,height:png.height,sha256:crypto.createHash('sha256').update(raw).digest('hex'),unityCopyMatches:true,upscaled:false};
});
fs.writeFileSync(path.join(dir,'native-manifest.json'),JSON.stringify({originals,reusedOriginals,psds:results},null,2));
console.log(JSON.stringify({originals:originals.length,psds:results.map(r=>({file:r.file,layers:r.layers.length,pixelsVerified:r.allNativePixelsVerified}))}));
