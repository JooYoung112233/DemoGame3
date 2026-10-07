const fs=require('fs'),crypto=require('crypto');const ev='검증/돌갑충-예고-v031',art='아트/돌갑충-예고-v031';
const files=['Assets/Scripts/Game/Art/TopDown/TopDownEnemyRig.cs','Assets/Scripts/Game/Enemies/BoarBrain.cs','Assets/Scripts/Game/Combat/Telegraph.cs'];
for(const p of files)if(!fs.readFileSync(p).equals(fs.readFileSync(ev+'/backup/'+p.split('/').pop())))throw Error('Concurrent change: '+p);
for(const p of files)fs.copyFileSync(ev+'/candidate/'+p.split('/').pop(),p);
fs.copyFileSync(ev+'/candidate/BeetlePatternVfx.cs','Assets/Scripts/Game/Art/TopDown/BeetlePatternVfx.cs');
for(const d of ['Assets/Resources/BeetleV31','Assets/Resources/TelegraphV31'])fs.mkdirSync(d,{recursive:true});
for(const n of ['body','toe1','toe2','toe3','toe4','toe5','toe6'])fs.copyFileSync(art+'/'+n+'.png','Assets/Resources/BeetleV31/'+n+'.png');
for(const shape of ['circle','half','rect'])for(const kind of ['outline','fill','edge'])if(!(shape==='rect'&&kind==='edge'))fs.copyFileSync(art+'/'+shape+'-'+kind+'.png','Assets/Resources/TelegraphV31/'+shape+'-'+kind+'.png');
console.log('Applied three guarded cosmetic patches, one component and fifteen PNGs. Original art/AI data preserved.');
