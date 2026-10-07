const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root='아트/구르기-모션-v057',out='검증/dodge-v057',hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const checkpoint=JSON.parse(fs.readFileSync(out+'/checkpoint.json','utf8'));
if(checkpoint.playing||checkpoint.compiling||checkpoint.scenes.some(s=>s.isDirty)||Date.now()-Date.parse(checkpoint.utc)>180000)throw Error('Fresh stopped clean Editor checkpoint required');
const baseline=JSON.parse(fs.readFileSync(root+'/baseline.json','utf8'));
for(const b of baseline.backup)if(hash(fs.readFileSync(b.path))!==b.sha256)throw Error('Concurrent source edit: '+b.path);
const target='Assets/Resources/CharacterMotionV045/clips.json',before=fs.readFileSync(target),lib=JSON.parse(before);
const updates=JSON.parse(fs.readFileSync(root+'/runtime-candidate-r3/Resources/DodgeMotionV057/clips-v057.json','utf8')).clips;
if(updates.length!==3||updates.some(c=>!['sword-dodge','great-dodge','twin-dodge'].includes(c.id)||c.duration!==.22||c.frames.length!==28||c.hits.length))throw Error('Unexpected clip scope');
fs.mkdirSync(out+'/backup',{recursive:true});
const town='Assets/Scripts/Game/Town/TownApprovedArtV057.cs';
const preserved=[town,'Assets/Scripts/Game/Player/PlayerController.cs','Assets/Scripts/Game/Art/TopDown/CharacterMotionV045.cs','Assets/Scripts/Game/Art/TopDown/ImportedFxV056.cs'];
const records=preserved.map(p=>({path:p,sha256:hash(fs.readFileSync(p))}));
for(const p of [target,...baseline.backup.map(b=>b.path).filter(p=>p.endsWith('.cs')),town])fs.copyFileSync(p,out+'/backup/'+path.basename(p),fs.constants.COPYFILE_EXCL);
const template=fs.readFileSync('Assets/Resources/CharacterMotionV045/sword-first-slash/part-0000.png.meta','utf8');let copied=0;
const unique=new Set(updates.flatMap(c=>c.frames.flatMap(f=>f.parts.map(p=>p.resource))));
for(const resource of unique){if(!resource.startsWith('DodgeMotionV057/'))throw Error('Invalid resource');const src=root+'/runtime-candidate-r3/Resources/'+resource+'.png',dst='Assets/Resources/'+resource+'.png';if(fs.existsSync(dst))throw Error('Unexpected existing generated target '+dst);fs.mkdirSync(path.dirname(dst),{recursive:true});fs.copyFileSync(src,dst);fs.writeFileSync(dst+'.meta',template.replace(/^guid: .*$/m,'guid: '+crypto.randomUUID().replaceAll('-','')),{flag:'wx'});copied++;}
for(const c of updates){const i=lib.clips.findIndex(x=>x.id===c.id);if(i<0||lib.clips[i].duration!==c.duration)throw Error('Clock changed');lib.clips[i]=c;}
if(!fs.readFileSync(target).equals(before))throw Error('Concurrent manifest edit');fs.writeFileSync(target,JSON.stringify(lib));
for(const f of ['FirstAttackArtV042.cs','EquipmentVisualV049.cs','TopDownCape.cs'])fs.copyFileSync(root+'/code-candidate/'+f,'Assets/Scripts/Game/Art/TopDown/'+f);
for(const r of records)if(hash(fs.readFileSync(r.path))!==r.sha256)throw Error('Preservation failed '+r.path);
const old=JSON.parse(before),unchanged=lib.clips.filter(c=>!updates.some(u=>u.id===c.id)).every(c=>JSON.stringify(c)===JSON.stringify(old.clips.find(o=>o.id===c.id)));
if(!unchanged)throw Error('Other clips changed');
const result={utc:new Date().toISOString(),clips:updates.map(c=>c.id),pngs:copied,otherClipsUnchanged:unchanged,manifestBefore:hash(before),manifestAfter:hash(fs.readFileSync(target)),preserved:records,gameplayUnchanged:true,backup:out+'/backup'};fs.writeFileSync(out+'/integration.json',JSON.stringify(result,null,2));console.log(result);
