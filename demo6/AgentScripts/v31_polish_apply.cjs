const fs=require('fs');const ev='검증/돌갑충-예고-v031';
for(const name of ['Telegraph.cs','BeetlePatternVfx.cs']){
 const p=name==='Telegraph.cs'?'Assets/Scripts/Game/Combat/'+name:'Assets/Scripts/Game/Art/TopDown/'+name;
 if(!fs.readFileSync(p).equals(fs.readFileSync(ev+'/candidate/'+name+'.before-polish')))throw Error('Concurrent change: '+p);
 fs.copyFileSync(ev+'/candidate/'+name,p);
}
console.log('Final cosmetics applied');
