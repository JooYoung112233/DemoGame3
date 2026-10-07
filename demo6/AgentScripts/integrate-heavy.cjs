const fs=require('fs'),p='Assets/Scripts/Game/Art/TopDown/OgrePatternVfx.cs';
const s=fs.readFileSync(p,'utf8');
fs.writeFileSync('검증/v049-final/backup/OgrePatternVfx-before-heavy.cs',s,{flag:'wx'});
fs.mkdirSync('Assets/Resources/OgreSlamHeavyV1',{recursive:true});
fs.copyFileSync('아트/오우거-내려찍기-효과/candidate-v1/slam-heavy-runtime-512.png','Assets/Resources/OgreSlamHeavyV1/slam-heavy.png');
const n=s.replace('enum Kind { Slam, Dust, Sweep, Roar, Rock }','enum Kind { Slam, Dust, Sweep, Roar, Rock, HeavySlam }')
.replace('new KeyValuePair<Kind,string>(Kind.Slam,"slam")','new KeyValuePair<Kind,string>(Kind.HeavySlam,"slam-heavy"),new KeyValuePair<Kind,string>(Kind.Slam,"slam")')
.replace('"OgreVfxV30/"+pair.Value','(pair.Key==Kind.HeavySlam?"OgreSlamHeavyV1/":"OgreVfxV30/")+pair.Value')
.replace('Queue(Kind.Slam,at,4.1f,.32f)','Queue(Kind.HeavySlam,at,4.1f,.32f)');
if(n===s||!n.includes('pair.Key==Kind.HeavySlam'))throw Error('Patch failed');
if(fs.readFileSync(p,'utf8')!==s)throw Error('Concurrent edit');
fs.writeFileSync(p,n);console.log('Heavy slam resource copied; adapter updated only.');
