const fs=require('fs'),path=require('path'),crypto=require('crypto');const dir='검증/정수리-질감수정-v10';const clone='../topdown-v10-review-isolated';
const current=JSON.parse(fs.readFileSync(dir+'/concurrent-changes.json'));const records=[];
for(const p of [...current.changes.map(v=>v.path),...current.added]){
 if(!fs.existsSync(p))throw Error('Concurrent deletion: '+p);
 const dest=clone+'/'+p,backup=dir+'/isolated-before-sync/'+p;
 if(fs.existsSync(dest)){fs.mkdirSync(path.dirname(backup),{recursive:true});fs.copyFileSync(dest,backup)}
 fs.mkdirSync(path.dirname(dest),{recursive:true});fs.copyFileSync(p,dest);
 records.push({path:p,sha256:crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex')});
}
function edit(p,fn){p=clone+'/Assets/Scripts/Game/'+p;fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8')))}
for(const [script,target]of [['v10_ui_code.js','Player/PlayerInputReader.cs'],['v10_ui_finish.js','Dungeon/UI/ExplorationLog.cs']]){
 const s=fs.readFileSync('AgentScripts/'+script,'utf8');const a=s.indexOf("edit('"+target+"',");let b=s.indexOf('\nconsole.log',a);if(b<0)b=s.length;Function('edit',s.slice(a,b))(edit);
}
fs.writeFileSync(dir+'/synced-concurrent-files.json',JSON.stringify(records,null,2));
const root='아트/정수리-질감수정-v10';const manifests=[];for(const id of fs.readdirSync(root+'/layers')){const p=root+'/layers/'+id+'/psd-layers.json';if(!fs.existsSync(p))continue;const m=JSON.parse(fs.readFileSync(p));manifests.push({id,width:m.width,height:m.height,layers:m.layers.length,notes:'Native source and visible regional layers retained; runtime sizes are recorded separately.'})}
fs.writeFileSync(root+'/manifest.json',JSON.stringify(manifests,null,2));console.log({synced:records.length,masters:manifests.length});
