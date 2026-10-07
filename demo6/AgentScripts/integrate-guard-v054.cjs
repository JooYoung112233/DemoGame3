const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root='아트/방패-전환보정-v054',target='Assets/Resources/CharacterMotionV045/clips.json',out='검증/v054-guard';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const before=fs.readFileSync(target);if(hash(before)!=='8c355cb0863528fadc43e6b9fd89f4d9ad4b66b9fd008c4106fe68340c3b5e7f')throw Error('Concurrent manifest change; review before applying');
const lib=JSON.parse(before),updates=JSON.parse(fs.readFileSync(root+'/runtime-candidate-r3/Resources/CharacterMotionV045/clips-v054.json')).clips.filter(c=>c.id!=='sword-parry-push'),authored=JSON.parse(fs.readFileSync(root+'/motion-candidate.json')).clips;
const qa=JSON.parse(fs.readFileSync(root+'/export-audit-v054.json')).qa;for(const c of updates)if(!qa.find(q=>q.id===c.id)?.passed)throw Error('Native QA failed '+c.id);
const template=fs.readFileSync('Assets/Resources/CharacterMotionV045/sword-first-slash/part-0000.png.meta','utf8');
const changes=[];fs.mkdirSync(out,{recursive:true});fs.writeFileSync(out+'/clips-before-v054.json',before,{flag:'wx'});
for(const c of updates){const idx=lib.clips.findIndex(x=>x.id===c.id),old=lib.clips[idx],source=authored.find(x=>x.id===c.id);if(c.duration!==old.duration||JSON.stringify(c.hits)!==JSON.stringify(old.hits))throw Error('Gameplay clock mismatch');
 c.frames.forEach((f,i)=>{for(const k of ['headOrigin','capL','capR','shoulderLeftAngle','shoulderRightAngle'])f[k]=source.frames[i][k];for(const part of f.parts){const from=root+'/runtime-candidate-r3/Resources/'+part.resource+'.png';part.resource=part.resource.replace('CharacterMotionV045/','GuardTransitionV054/');const to='Assets/Resources/'+part.resource+'.png';fs.mkdirSync(path.dirname(to),{recursive:true});const bytes=fs.readFileSync(from);if(fs.existsSync(to)&&!fs.readFileSync(to).equals(bytes))throw Error('Asset conflict');if(!fs.existsSync(to))fs.writeFileSync(to,bytes,{flag:'wx'});if(!fs.existsSync(to+'.meta'))fs.writeFileSync(to+'.meta',template.replace(/^guid: .*$/m,'guid: '+crypto.randomUUID().replaceAll('-','')),{flag:'wx'});}});
 changes.push({id:c.id,beforeFrames:old.frames.length,afterFrames:c.frames.length,duration:c.duration,hits:c.hits});lib.clips[idx]=c;
}
if(!fs.readFileSync(target).equals(before))throw Error('Concurrent manifest change');fs.writeFileSync(target,JSON.stringify(lib));
const result={utc:new Date().toISOString(),changes,before:hash(before),after:hash(fs.readFileSync(target)),gameplayCodeChanged:false,excluded:'sword-parry-push: offline candidate only; native overlap requires separate review'};fs.writeFileSync(root+'/INTEGRATION-V054.json',JSON.stringify(result,null,2));console.log(result);
