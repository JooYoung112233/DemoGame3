const fs=require('fs'),crypto=require('crypto');
const ev='검증/돌갑충-예고-v031',art='아트/돌갑충-예고-v031';
for(const d of [ev,ev+'/backup',ev+'/candidate',art])fs.mkdirSync(d,{recursive:true});
function walk(d){return fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(d+'/'+e.name):[d+'/'+e.name]);}
if(!fs.existsSync(ev+'/before.json')){
 const files=['Assets','Packages','ProjectSettings'].flatMap(walk).map(path=>({path,sha256:crypto.createHash('sha256').update(fs.readFileSync(path)).digest('hex')}));
 fs.writeFileSync(ev+'/before.json',JSON.stringify(files));
 for(const p of ['Assets/Scripts/Game/Art/TopDown/TopDownEnemyRig.cs','Assets/Scripts/Game/Enemies/BoarBrain.cs','Assets/Scripts/Game/Combat/Telegraph.cs'])fs.copyFileSync(p,ev+'/backup/'+p.split('/').pop());
}
let s=fs.readFileSync('AgentScripts/OgreV30State.cs','utf8').replaceAll('OgreV30State','BeetleV31State').replaceAll('오우거-패턴-v030','돌갑충-예고-v031');
fs.writeFileSync('AgentScripts/BeetleV31State.cs',s);
console.log('v031 original-project baseline and state helper ready');
