const fs=require('fs'),crypto=require('crypto');const ev='검증/맵-v034-기존조명',art='아트/맵-화풍정리/v034-dark-zone';
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
for(const p of JSON.parse(fs.readFileSync(art+'/protected-before.json')))if(sha(p.path)!==p.sha256)throw Error('Protected file changed: '+p.path);
function walk(d){return fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(d+'/'+e.name):[d+'/'+e.name]);}
fs.mkdirSync(ev,{recursive:true});if(!fs.existsSync(ev+'/before.json'))fs.writeFileSync(ev+'/before.json',JSON.stringify(['Assets','Packages','ProjectSettings'].flatMap(walk).map(path=>({path,sha256:sha(path)}))));
const bindings=JSON.parse(fs.readFileSync(art+'/preview-bindings.json'));const files=['floor','wall','damage'].map(n=>({part:n,path:bindings[n].file,sha256:sha(bindings[n].file)}));fs.writeFileSync(ev+'/candidate-inputs.json',JSON.stringify(files,null,2));
console.log('Preview baseline saved; three candidate PNGs outside Assets. No runtime file writes.');
