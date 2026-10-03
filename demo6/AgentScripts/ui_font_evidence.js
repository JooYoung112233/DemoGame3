const fs=require('fs');
function readFont(path){
 const b=fs.readFileSync(path);const offsets=b.toString('ascii',0,4)==='ttcf'?Array.from({length:b.readUInt32BE(8)},(_,i)=>b.readUInt32BE(12+4*i)):[0];
 return offsets.map(o=>{let tables={};for(let i=0;i<b.readUInt16BE(o+4);i++){let p=o+12+16*i;tables[b.toString('ascii',p,p+4)]={o:b.readUInt32BE(p+8),n:b.readUInt32BE(p+12)};}
  let names={};let n=tables.name.o;for(let i=0;i<b.readUInt16BE(n+2);i++){let p=n+6+12*i,platform=b.readUInt16BE(p),lang=b.readUInt16BE(p+4),id=b.readUInt16BE(p+6);if(![1,2,8,13,14].includes(id)||names[id]||!(platform===3&&lang===1033))continue;let buf=b.subarray(n+b.readUInt16BE(n+4)+b.readUInt16BE(p+10),n+b.readUInt16BE(n+4)+b.readUInt16BE(p+10)+b.readUInt16BE(p+8));let val='';for(let j=0;j<buf.length;j+=2)val+=String.fromCharCode(buf.readUInt16BE(j));names[id]=val;}
  return {path,names,embeddingFsType:tables['OS/2']?b.readUInt16BE(tables['OS/2'].o+8):null};
 });
}
const result=['C:/Windows/Fonts/batang.ttc','C:/Windows/Fonts/malgun.ttf','C:/Windows/Fonts/malgunbd.ttf'].flatMap(readFont);
fs.writeFileSync('아트/UI-정리-v2/검수/font-evidence.json',JSON.stringify(result,null,2));
console.log(result.map(x=>({family:x.names[1],style:x.names[2],vendor:x.names[8],license:x.names[13]?.slice(0,350),licenseUrl:x.names[14],fsType:x.embeddingFsType})));
