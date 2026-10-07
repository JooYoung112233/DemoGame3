const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root='아트/캐릭터-연결보정-v052',backup='검증/v052-feedback/backup';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const files=['FirstAttackArtV042','EquipmentVisualV049','SlashWaveV044','OgreArtRig','Telegraph'];
for(const name of files){const target='Assets/Scripts/Game/'+(name==='Telegraph'?'Combat/':'Art/TopDown/')+name+'.cs';if(!fs.readFileSync(target).equals(fs.readFileSync(backup+'/'+name+'.cs')))throw Error('Concurrent file change: '+target);}
const target='Assets/Resources/CharacterMotionV045/clips.json',prior=fs.readFileSync(target),lib=JSON.parse(prior);
const updates=JSON.parse(fs.readFileSync(root+'/runtime-candidate-r3/Resources/CharacterMotionV045/clips-v052.json')).clips;
const authored=JSON.parse(fs.readFileSync(root+'/motion-candidate.json')).clips;
const invariants=[];
for(const c of updates){const old=lib.clips.find(x=>x.id===c.id);if(!old)throw Error('Unexpected clip '+c.id);let sameTiming=c.duration===old.duration&&JSON.stringify(c.hits)===JSON.stringify(old.hits);if(!sameTiming)throw Error('Gameplay clock mismatch '+c.id);invariants.push({id:c.id,duration:c.duration,hits:c.hits,beforeFrames:old.frames.length,afterFrames:c.frames.length,sameTiming});}
if(!fs.existsSync(backup+'/clips-before-v052.json'))fs.writeFileSync(backup+'/clips-before-v052.json',prior,{flag:'wx'});
const template=fs.readFileSync('Assets/Resources/CharacterMotionV045/sword-first-slash/part-0000.png.meta','utf8');
for(const c of updates){const source=authored.find(x=>x.id===c.id);c.frames.forEach((f,i)=>{for(const k of ['headOrigin','capL','capR','shoulderLeftAngle','shoulderRightAngle'])f[k]=source.frames[i][k];for(const part of f.parts){const from=root+'/runtime-candidate-r3/Resources/'+part.resource+'.png';part.resource=part.resource.replace('CharacterMotionV045/','CharacterMotionV052/');const to='Assets/Resources/'+part.resource+'.png';fs.mkdirSync(path.dirname(to),{recursive:true});const bytes=fs.readFileSync(from);if(fs.existsSync(to)&&!fs.readFileSync(to).equals(bytes))throw Error('Unexpected generated asset change '+to);if(!fs.existsSync(to))fs.writeFileSync(to,bytes,{flag:'wx'});if(!fs.existsSync(to+'.meta'))fs.writeFileSync(to+'.meta',template.replace(/^guid: .*$/m,'guid: '+crypto.randomUUID().replaceAll('-','')),{flag:'wx'});}});lib.clips[lib.clips.findIndex(x=>x.id===c.id)]=c;}
if(!fs.readFileSync(target).equals(prior))throw Error('Concurrent manifest change');
const changes=[];
for(const name of [...files,'WeaponEdgeV052']){const target='Assets/Scripts/Game/'+(name==='Telegraph'?'Combat/':'Art/TopDown/')+name+'.cs';const bytes=fs.readFileSync(root+'/code-candidate/'+name+'.cs');changes.push({path:target,before:fs.existsSync(target)?hash(fs.readFileSync(target)):null,after:hash(bytes)});fs.writeFileSync(target,bytes);}
fs.mkdirSync('Assets/Resources/EquipmentV052',{recursive:true});fs.copyFileSync(root+'/weapon-edge-profiles.json','Assets/Resources/EquipmentV052/weapon-edge-profiles.json');
fs.writeFileSync(target,JSON.stringify(lib));
const result={atUtc:new Date().toISOString(),changes,invariants,manifestBefore:hash(prior),manifestAfter:hash(fs.readFileSync(target))};fs.writeFileSync(root+'/INTEGRATION-V052.json',JSON.stringify(result,null,2));console.log(result);
