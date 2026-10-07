const fs=require('fs'),cp=require('child_process');const ev='검증/돌갑충-예고-v031',art='아트/돌갑충-예고-v031';
const p='Assets/Scripts/Game/Combat/Telegraph.cs';const final=fs.readFileSync(ev+'/candidate/Telegraph.cs');
cp.execFileSync(process.execPath,['AgentScripts/v31_candidates.cjs']);
if(!fs.readFileSync(p).equals(fs.readFileSync(ev+'/candidate/Telegraph.cs')))throw Error('Concurrent Telegraph change; preserve it');
fs.writeFileSync(ev+'/candidate/Telegraph.cs',final);fs.writeFileSync(p,final);
for(const shape of ['circle','half','rect'])for(const kind of ['outline','fill','edge','rim'])if(!(shape==='rect'&&kind==='edge'))fs.copyFileSync(art+'/'+shape+'-'+kind+'.png','Assets/Resources/TelegraphV31/'+shape+'-'+kind+'.png');
console.log('Final rim refinement applied with exact source guard');
