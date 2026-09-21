const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
const outputDir = process.argv[2];
if (!outputDir) throw Error('Provide the task-owned visualization directory.');
const configFile = process.argv[3] || 'design/interaction/camper-layers-v1.json';
const outputName = process.argv[4] || 'camper-layers.html';
const config = JSON.parse(fs.readFileSync(path.resolve(root, configFile), 'utf8'));
const renderLayers = [...config.slots, ...(config.actors || []), ...(config.actors && config.foreground ? [config.foreground] : [])];
const basePreview = path.join(root, 'art/chapter01/layers/camper-clean-base-preview.jpg');
if (!fs.existsSync(basePreview)) throw Error('Base display copy is not ready.');
const assets = {base: 'data:image/jpeg;base64,' + fs.readFileSync(basePreview).toString('base64')};
for (const slot of renderLayers) {
  if (!slot.asset) throw Error(`Unfinished transparent asset: ${slot.id}`);
  const [x,y,w,h] = slot.rect;
  if (x<0 || y<0 || w<=0 || h<=0 || x+w>config.canvas.width || y+h>config.canvas.height) throw Error(`Invalid placement: ${slot.id}`);
  const data = fs.readFileSync(path.join(root, slot.asset));
  if (data.toString('hex',0,8) !== '89504e470d0a1a0a' || ![4,6].includes(data[25])) throw Error(`Expected alpha PNG: ${slot.id}`);
  assets[slot.id] = 'data:image/png;base64,' + data.toString('base64');
}
const template = fs.readFileSync(path.join(root,'design/interaction/camper-layer-preview.template.html'),'utf8');
const fragment = template.replace('__CONFIG__',JSON.stringify(config)).replace('__ASSETS__',JSON.stringify(assets)).replaceAll('live49-layers', config.actors ? 'live49-seated' : 'live49-layers');
for (const match of fragment.matchAll(/<script>([\s\S]*?)<\/script>/g)) new vm.Script(match[1]);
if (Buffer.byteLength(fragment) >= 1000000) throw Error('Preview exceeds 1 MB.');
fs.mkdirSync(outputDir,{recursive:true});
const output = path.join(outputDir,outputName);
fs.writeFileSync(output,fragment);
console.log(output);
