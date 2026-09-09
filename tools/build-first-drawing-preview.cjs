const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'..'),out=process.argv[2];
if(!out)throw Error('Provide task-owned preview directory.');
const config=JSON.parse(fs.readFileSync(path.join(root,'design/interaction/camper-seated-v1.json'),'utf8'));
const story=JSON.parse(fs.readFileSync(path.join(root,'design/interaction/ch01-first-drawing-v1.json'),'utf8'));
config.slots.find(s=>s.id==='sketchbook').asset='art/chapter01/layers/pages/sketchbook-blank-v1.png';
config.pages=[
  {id:'sea-started',pageState:'started',asset:'art/chapter01/layers/pages/sea-started-overlay-v1.png',rect:[904,454,102,73],order:45},
  {id:'sea-complete',pageState:'complete',asset:'art/chapter01/layers/pages/sea-complete-overlay-v1.png',rect:[904,454,102,73],order:45}
];
const assets={base:'data:image/jpeg;base64,'+fs.readFileSync(path.join(root,'art/chapter01/layers/camper-clean-base-preview.jpg')).toString('base64')};
for(const layer of [...config.slots,...config.actors,config.foreground,...config.pages]){
  const data=fs.readFileSync(path.join(root,layer.asset));
  if(![4,6].includes(data[25]))throw Error('Expected alpha PNG: '+layer.id);
  assets[layer.id]='data:image/png;base64,'+data.toString('base64');
}
let html=fs.readFileSync(path.join(root,'design/interaction/first-drawing-preview.template.html'),'utf8');
html=html.replace('__CONFIG__',JSON.stringify(config)).replace('__STORY__',JSON.stringify(story)).replace('__ASSETS__',JSON.stringify(assets));
for(const m of html.matchAll(/<script>([\s\S]*?)<\/script>/g))new vm.Script(m[1]);
if(Buffer.byteLength(html)>=1000000)throw Error('Preview exceeds 1 MB.');
fs.writeFileSync(path.join(root,'design/interaction/ch01-first-drawing-layers-v1.json'),JSON.stringify(config,null,2)+'\n');
fs.mkdirSync(out,{recursive:true});const dest=path.join(out,'first-drawing.html');fs.writeFileSync(dest,html);console.log(dest);
