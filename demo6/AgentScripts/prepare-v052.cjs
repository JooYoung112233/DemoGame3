const fs=require('fs'),path=require('path');
const from='아트/캐릭터-가드준비-수정-v050',to='아트/캐릭터-연결보정-v052';
fs.mkdirSync(to,{recursive:true});for(const dir of ['scripts','source'])fs.cpSync(from+'/'+dir,to+'/'+dir,{recursive:true,errorOnExist:true,force:false});
fs.mkdirSync(to+'/preview',{recursive:true});
let p=to+'/scripts/build-motion.cjs',s=fs.readFileSync(p,'utf8');
s=s.replace('R(.60,.08,1.60,0,75)','R(.54,.46,1.60,0,75)').replace('R(.62,.12,1.61,6,75)','R(.56,.50,1.61,6,75)').replace('R(.62,.16,1.62,14,75)','R(.56,.53,1.62,14,75)').replace('R(.61,.10,1.64,-6,75)','R(.59,.45,1.64,-6,75)');
s=s.replace('const times=[...new Set','const dense=spec.id.includes("combo")||spec.id.includes("first-")||spec.id==="sword-parry-riposte";const fps=dense?60:20;const times=[...new Set').replace('Math.ceil(spec.duration*20)},(_,i)=>i/20)','Math.ceil(spec.duration*fps)},(_,i)=>i/fps)');
fs.writeFileSync(p,s);require('../'+p).buildAll();
let d=JSON.parse(fs.readFileSync(to+'/motion-candidate.json'));
const selected=d.clips.filter(c=>c.id.includes('combo')||c.id.includes('first-')||/^sword-(guard|parry)/.test(c.id)).map(c=>c.id);
fs.writeFileSync(to+'/selected.json',JSON.stringify(selected,null,2));
p=to+'/scripts/render-final.lua';s=fs.readFileSync(p,'utf8').replace(from,to).replace("if app.params['only'] then", "if stage=='v052' then local chosen={"+selected.map(x=>"['"+x+"']=true").join(',')+"};take=chosen[clip.id] end\n if app.params['only'] then");
fs.writeFileSync(p,s);console.log({selected,frames:d.clips.filter(c=>selected.includes(c.id)).reduce((n,c)=>n+c.frames.length,0)});
fs.mkdirSync('검증/v052-feedback/backup',{recursive:true});fs.mkdirSync('아트/캐릭터-연결보정-v052/code-candidate',{recursive:true});
for(const n of ['FirstAttackArtV042','EquipmentVisualV049','SlashWaveV044']){const file='Assets/Scripts/Game/Art/TopDown/'+n+'.cs';fs.copyFileSync(file,'검증/v052-feedback/backup/'+n+'.cs');fs.copyFileSync(file,to+'/code-candidate/'+n+'.cs');}
