const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root='아트/캐릭터-가드준비-수정-v050',target='Assets/Resources/CharacterMotionV045/clips.json';
const prior=fs.readFileSync(target),lib=JSON.parse(prior),stage=process.argv[2]||'priority';
const update=JSON.parse(fs.readFileSync(root+'/runtime-candidate-r3/Resources/CharacterMotionV045/clips-'+stage+'.json')).clips;
const source=JSON.parse(fs.readFileSync(root+'/motion-candidate.json')).clips;
const backup='검증/v049-final/backup';fs.mkdirSync(backup,{recursive:true});
if(!fs.existsSync(backup+'/clips-before-v050.json'))fs.writeFileSync(backup+'/clips-before-v050.json',prior,{flag:'wx'});
const template=fs.readFileSync('Assets/Resources/CharacterMotionV045/sword-first-slash/part-0000.png.meta','utf8');
for(const c of update){if(!c.id.startsWith('sword-'))throw Error('Sword only');const data=source.find(x=>x.id===c.id);
 c.frames.forEach((f,i)=>{for(const k of ['headOrigin','capL','capR','shoulderLeftAngle','shoulderRightAngle'])f[k]=data.frames[i][k];for(const part of f.parts){const from='Assets/Resources/'+part.resource+'.png';const exportFile=root+'/runtime-candidate-r3/Resources/'+part.resource+'.png';part.resource=part.resource.replace('CharacterMotionV045/','CharacterMotionV051/');const to='Assets/Resources/'+part.resource+'.png';fs.mkdirSync(path.dirname(to),{recursive:true});const b=fs.readFileSync(exportFile);if(!fs.existsSync(to))fs.writeFileSync(to,b,{flag:'wx'});else if(!fs.readFileSync(to).equals(b))throw Error('Unexpected changed generated asset '+to);if(!fs.existsSync(to+'.meta'))fs.writeFileSync(to+'.meta',template.replace(/^guid: .*$/m,'guid: '+crypto.randomUUID().replaceAll('-','')),{flag:'wx'});}});
 lib.clips[lib.clips.findIndex(x=>x.id===c.id)]=c;
}
if(!fs.readFileSync(target).equals(prior))throw Error('Concurrent manifest change');fs.writeFileSync(target,JSON.stringify(lib));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const record={atUtc:new Date().toISOString(),clips:update.map(c=>c.id),before:hash(prior),after:hash(fs.readFileSync(target)),originalPixelsPreserved:true};
fs.writeFileSync(root+'/INTEGRATION-'+stage+'.json',JSON.stringify(record,null,2));console.log(record);
