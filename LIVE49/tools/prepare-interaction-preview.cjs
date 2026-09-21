const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const root=path.resolve(__dirname,'..');
const previewRoot=process.argv[2];
if(!previewRoot)throw Error('Provide task-owned preview directory');
const previewFile=path.join(previewRoot,'mart-interaction.html');
const config=JSON.parse(fs.readFileSync(path.join(root,'design/interaction/hotspots-v1.json'),'utf8'));
const source=fs.readFileSync(path.join(root,'design/interaction/interaction-model.js'),'utf8');
let fragment=fs.readFileSync(previewFile,'utf8');
fragment=fragment.replace('__MODEL__',source).replace('__HOTSPOTS__',JSON.stringify(config));
for(const [token,file]of [['__MART__','mart-preview.jpg'],['__CAMPER__','camper-preview.jpg']])fragment=fragment.replace(token,'data:image/jpeg;base64,'+fs.readFileSync(path.join(previewRoot,file)).toString('base64'));
if(/__(MODEL|HOTSPOTS|MART|CAMPER)__/.test(fragment))throw Error('Unresolved placeholder');
for(const m of fragment.matchAll(/<script>([\s\S]*?)<\/script>/g))new vm.Script(m[1]);
if(Buffer.byteLength(fragment)>1000000)throw Error('Preview > 1MB');
fs.writeFileSync(previewFile,fragment);
for(const [sceneId,scene]of Object.entries(config.scenes)){
  for(const h of scene.hotspots){
    for(const [x,y] of h.polygon)if(x<0||y<0||x>scene.width||y>scene.height)throw Error('Invalid polygon');
    const points=h.polygon.map(p=>p.join(',')).join(' ');
    const svg=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${scene.width} ${scene.height}" width="${scene.width}" height="${scene.height}"><title>${h.name} 조사 영역</title><polygon points="${points}" fill="#ffe1a0" fill-opacity="0.12" stroke="#f7d78b" stroke-width="3"/></svg>\n`;
    fs.writeFileSync(path.join(root,'art/ui/interaction',sceneId+'-'+h.id+'-highlight.svg'),svg);
  }
}
console.log('Embedded preview, syntax and polygon bounds valid; 6 transparent SVG overlays exported. Bytes: '+Buffer.byteLength(fragment));
