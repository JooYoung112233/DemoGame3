const fs=require('fs'),path=require('path');
const root='아트/정수리-시안적용-v9';
const u16=x=>{let b=Buffer.alloc(2);b.writeUInt16BE(x&65535);return b},u32=x=>{let b=Buffer.alloc(4);b.writeUInt32BE(x>>>0);return b},str=x=>Buffer.from(x,'ascii'),cat=xs=>Buffer.concat(xs);
const planes=(rgba,n)=>[0,1,2,3].map(c=>{let b=Buffer.alloc(n);for(let i=0;i<n;i++)b[i]=rgba[i*4+c];return b});
for(const part of JSON.parse(fs.readFileSync(root+'/manifest.json'))){
 const dir=root+'/layers/'+part.id,meta=JSON.parse(fs.readFileSync(dir+'/psd-layers.json'));let records=[],channels=[];
 for(const l of [...meta.layers].reverse()){let n=l.width*l.height,p=planes(fs.readFileSync(l.file),n),name=str(l.name.slice(0,255)),pas=cat([Buffer.from([name.length]),name]);let extra=cat([u32(0),u32(0),pas,Buffer.alloc((4-pas.length%4)%4)]);records.push(cat([u32(l.y),u32(l.x),u32(l.y+l.height),u32(l.x+l.width),u16(4),...[0,1,2,-1].flatMap(id=>[u16(id),u32(n+2)]),str('8BIMnorm'),Buffer.from([255,0,0,0]),u32(extra.length),extra]));channels.push(...p.map(b=>cat([u16(0),b])));}
 let info=cat([u16(meta.layers.length),...records,...channels]);if(info.length%2)info=cat([info,Buffer.alloc(1)]);let lm=cat([u32(info.length),info,u32(0)]),comp=fs.readFileSync(dir+'/composite.rgba');fs.writeFileSync(root+'/'+part.id+'-master-v009.psd',cat([str('8BPS'),u16(1),Buffer.alloc(6),u16(4),u32(meta.height),u32(meta.width),u16(8),u16(3),u32(0),u32(0),u32(lm.length),lm,u16(0),...planes(comp,meta.width*meta.height)]));
 // Retain channel streams as well as source PNG layers; no cleanup or deletion.
}
console.log('6 native layered PSD collections written.');
