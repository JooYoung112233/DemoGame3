const fs=require('fs'),crypto=require('crypto');const ev='검증/맵-화풍정리-v032',art='아트/맵-화풍정리';const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const map=JSON.parse(fs.readFileSync(art+'/apply-map.json'));for(const a of map){if(sha(a.source)!==a.sourceSha256||sha(a.target)!==a.expectedTargetSha256)throw Error('Concurrent or unexpected asset: '+a.target);}
fs.mkdirSync(ev+'/backup',{recursive:true});
function walk(d){return fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(d+'/'+e.name):[d+'/'+e.name]);}
if(!fs.existsSync(ev+'/before.json')){
 fs.writeFileSync(ev+'/before.json',JSON.stringify(['Assets','Packages','ProjectSettings'].flatMap(walk).map(path=>({path,sha256:sha(path)}))));
 for(const a of map){fs.copyFileSync(a.target,ev+'/backup/'+a.target.split('/').pop());fs.copyFileSync(a.target+'.meta',ev+'/backup/'+a.target.split('/').pop()+'.meta');}
}
console.log('Two guarded PNGs; full Assets/Packages/ProjectSettings baseline saved.');
