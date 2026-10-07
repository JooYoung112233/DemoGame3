const fs=require('fs'),zlib=require('zlib'),crypto=require('crypto');
const root='아트/승인픽셀-정지추출-v13',evidence='검증/승인픽셀-정지추출-v13';
function png(file){
 const b=fs.readFileSync(file);if(b.toString('hex',0,8)!=='89504e470d0a1a0a')throw Error('PNG signature');
 let p=8,w,h,depth,type,parts=[];
 while(p<b.length){const n=b.readUInt32BE(p),tag=b.toString('ascii',p+4,p+8),v=b.subarray(p+8,p+8+n);p+=n+12;
  if(tag==='IHDR'){w=v.readUInt32BE(0);h=v.readUInt32BE(4);depth=v[8];type=v[9];if(v[12]!==0)throw Error('Interlace unsupported');}
  if(tag==='IDAT')parts.push(v);if(tag==='IEND')break;
 }
 if(depth!==8||![2,6].includes(type))throw Error('Unexpected PNG format '+file+' '+depth+' '+type);
 const channels=type===6?4:3,stride=w*channels,data=zlib.inflateSync(Buffer.concat(parts)),raw=Buffer.alloc(h*stride);
 const paeth=(a,b,c)=>{const p=a+b-c,pa=Math.abs(p-a),pb=Math.abs(p-b),pc=Math.abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;};
 for(let y=0;y<h;y++){const filter=data[y*(stride+1)];for(let x=0;x<stride;x++){
  const a=x>=channels?raw[y*stride+x-channels]:0,c=y>0&&x>=channels?raw[(y-1)*stride+x-channels]:0,up=y>0?raw[(y-1)*stride+x]:0;
  const v=data[y*(stride+1)+1+x]+(filter===0?0:filter===1?a:filter===2?up:filter===3?Math.floor((a+up)/2):filter===4?paeth(a,up,c):NaN);
  if(!Number.isFinite(v))throw Error('PNG filter');raw[y*stride+x]=v&255;
 }}
 const rgba=Buffer.alloc(w*h*4);for(let i=0;i<w*h;i++){for(let c=0;c<3;c++)rgba[4*i+c]=raw[channels*i+c];rgba[4*i+3]=channels===4?raw[channels*i+3]:255;}
 return{w,h,rgba};
}
const ref=png('승인.png'),out=png(root+'/character-native.png'),crop=png(root+'/source-native-crop.png');
if(out.w!==160||out.h!==192)throw Error('Native canvas changed');
let selected=0,rgbMismatch=0,cropMismatch=0,partial=0,opaque=0;
const bitmap=[];
for(let y=0;y<out.h;y++)for(let x=0;x<out.w;x++){
 const i=y*out.w+x,j=((y+338)*ref.w+x+772)*4,a=out.rgba[i*4+3];bitmap[i]=a!==0;
 if(a>0){selected++;for(let c=0;c<3;c++)if(out.rgba[i*4+c]!==ref.rgba[j+c])rgbMismatch++;}
 if(a===255)opaque++;else if(a!==0)partial++;
 for(let c=0;c<3;c++)if(crop.rgba[i*4+c]!==ref.rgba[j+c])cropMismatch++;
}
const seen=new Set(),components=[];
for(let i=0;i<bitmap.length;i++)if(bitmap[i]&&!seen.has(i)){
 let count=0;const q=[i];seen.add(i);while(q.length){const n=q.pop(),x=n%out.w,y=Math.floor(n/out.w);count++;
  for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){if(!dx&&!dy)continue;const xx=x+dx,yy=y+dy,j=yy*out.w+xx;if(xx>=0&&xx<out.w&&yy>=0&&yy<out.h&&bitmap[j]&&!seen.has(j)){seen.add(j);q.push(j);}}
 }components.push(count);
}
if(rgbMismatch||cropMismatch||partial)throw Error('Pixel preservation/alpha failed');
const initial=JSON.parse(fs.readFileSync(evidence+'/initial-files.json','utf8'));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const changed=initial.files.filter(x=>!fs.existsSync(x.path)||hash(x.path)!==x.sha256).map(x=>x.path);
const result={time:new Date().toISOString(),reader:'Independent Node PNG unfilter/deflate decoder',sourceDimensions:[ref.w,ref.h],sourceSha256:hash('승인.png'),outputDimensions:[out.w,out.h],cropOrigin:[772,338],selectedPixels:selected,opaquePixels:opaque,partialAlphaPixels:partial,transparentPixels:bitmap.length-selected,foregroundRGBChannelMismatches:rgbMismatch,cropRGBChannelMismatches:cropMismatch,foreground8ConnectedComponents:components.sort((a,b)=>b-a),originalTrackedFiles:initial.files.length,originalFilesChangedDuringTask:changed,gameAssetApplied:false};
fs.writeFileSync(evidence+'/independent-pixel-validation.json',JSON.stringify(result,null,2));console.log(JSON.stringify(result));
