const fs=require('fs');
const root='아트/승인형상-대표샘플-v12';
const rows=[];
for(const p of JSON.parse(fs.readFileSync(root+'/manifest.json','utf8'))){
 const b=fs.readFileSync(root+'/'+p.id+'-study-v012.psd');let q=26;
 const skip=()=>{const n=b.readUInt32BE(q);q+=4+n;};skip();skip();skip();
 if(b.readUInt16BE(q)!==0)throw Error('Expected raw PSD composite');q+=2;
 const count=p.width*p.height;const rgba=Buffer.alloc(count*4);
 for(let c=0;c<4;c++)for(let i=0;i<count;i++)rgba[4*i+c]=b[q+c*count+i];
 const expected=fs.readFileSync(root+'/layers/'+p.id+'/composite.rgba');
 if(!rgba.equals(expected))throw Error('PSD composite pixels differ: '+p.id);
 rows.push({id:p.id,width:p.width,height:p.height,psdCompositeExact:true,layers:p.layers,reader:'Independent PSD section parser and planar RGBA composite decoder; source PNG exactness verified by Aseprite re-open. Photoshop application not run.'});
}
fs.writeFileSync(root+'/psd-composite-validation.json',JSON.stringify(rows,null,2));console.log(JSON.stringify(rows));
