const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),clone='E:/personalProject/Demo3/topdown-v10-review-isolated';
const evidence=path.join(root,'검증/정수리-질감수정-v10');
if(root.toLowerCase()!=='e:\\personalproject\\demo3\\demo6')throw Error('Original root mismatch');
if(fs.readFileSync(path.join(clone,'.v10-owned-review'),'utf8').trim()!=='Demo6 v10 texture review')throw Error('Clone ownership mismatch');
const baseline=JSON.parse(fs.readFileSync(path.join(evidence,'initial-files.json'),'utf8')).files;
const sync=JSON.parse(fs.readFileSync(path.join(evidence,'synced-concurrent-files.json'),'utf8'));
const expected=new Map([...baseline,...sync].map(x=>[x.path,x.sha256]));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const code=new Set(['Assets/Scripts/Game/Art/PainterlyMineArtV10.cs','Assets/Scripts/Game/Art/UI/UiSkinArt.cs','Assets/Scripts/Game/Dungeon/DungeonWorld.cs','Assets/Scripts/Game/Dungeon/Progression/SkillPanel.cs','Assets/Scripts/Game/Dungeon/UI/BigMap.cs','Assets/Scripts/Game/Dungeon/UI/DungeonHud.cs','Assets/Scripts/Game/Dungeon/UI/DungeonHudArt.cs','Assets/Scripts/Game/Dungeon/UI/DungeonMiniMap.cs','Assets/Scripts/Game/Dungeon/UI/DungeonUi.cs','Assets/Scripts/Game/Dungeon/UI/ExplorationLog.cs','Assets/Scripts/Game/Player/PlayerInputReader.cs']);
const differences=JSON.parse(fs.readFileSync(path.join(evidence,'clone-difference-review.json'),'utf8'));
const selected=differences.filter(({p})=>code.has(p)||code.has(p.replace(/\.meta$/,''))||/^Assets\/(Art\/TopDownPilotV9\/Hero|Resources\/TopDownPilotV9)\//.test(p)||/^Assets\/Resources\/(TopDownV10|UI\/V10)(\/|\.meta$)/.test(p)||p==='ProjectSettings/ProjectSettings.asset');
const plans=[];
for(const {p} of selected){const from=path.join(clone,p),to=path.join(root,p),bytes=fs.readFileSync(from);const before=fs.existsSync(to)?fs.readFileSync(to):null;
 if(before&&hash(before)!==expected.get(p))throw Error('Concurrent source change: '+p);
 if(!before&&expected.has(p))throw Error('Deleted source preserved: '+p);
 if(p==='ProjectSettings/ProjectSettings.asset'&&before.toString().replace('activeInputHandler: 1','activeInputHandler: 2')!==bytes.toString())throw Error('Unexpected settings differences');
 plans.push({path:p,before:before?hash(before):null,after:hash(bytes),bytes:bytes.length});
}
fs.writeFileSync(path.join(evidence,'apply-plan.json'),JSON.stringify({time:new Date().toISOString(),files:plans,excluded:differences.filter(x=>!selected.includes(x))},null,2));
if(!process.argv.includes('--apply')){console.log(JSON.stringify({reviewedFiles:plans.length,excludedFiles:differences.length-plans.length}));process.exit(0);}
const backup=path.join(evidence,'original-before-v10');if(fs.existsSync(backup))throw Error('Backup exists; will not overwrite prior apply');fs.mkdirSync(backup);
for(const p of plans){const from=path.join(clone,p.path),to=path.join(root,p.path);if(p.before){const out=path.join(backup,p.path);fs.mkdirSync(path.dirname(out),{recursive:true});fs.copyFileSync(to,out);}fs.mkdirSync(path.dirname(to),{recursive:true});fs.copyFileSync(from,to);if(hash(fs.readFileSync(to))!==p.after)throw Error('Copy check failed: '+p.path);}
fs.writeFileSync(path.join(evidence,'applied-files.json'),JSON.stringify({time:new Date().toISOString(),files:plans},null,2));
fs.copyFileSync(path.join(clone,'Temp/pipeline_test_status.json'),path.join(evidence,'editmode-final-isolated.json'));
console.log(JSON.stringify({appliedFiles:plans.length,backup,tests:'491/491'}));
