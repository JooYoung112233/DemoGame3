const fs=require('fs'),path=require('path'),crypto=require('crypto');
const workspace='E:/personalProject/Demo3',original=workspace+'/demo6',out=original+'/보존/별도검증작업-20261004';
const names=['topdown-neutral-light-review','topdown-rat-v21-review','topdown-v11-harmony-review','topdown-walk-v22-review'];
const excluded=new Set(['Library','Temp','obj','.vs','.git']);const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function walk(base,rel=''){let result=[];for(const e of fs.readdirSync(path.join(base,rel),{withFileTypes:true})){if(!rel&&excluded.has(e.name))continue;if(e.isSymbolicLink())throw Error('Unexpected link: '+path.join(base,rel,e.name));const r=rel?rel+'/'+e.name:e.name;if(e.isDirectory())result.push(...walk(base,r));else if(e.isFile()&&!/\.(csproj|sln|suo|user)$/.test(e.name))result.push(r);}return result;}
fs.mkdirSync(out,{recursive:true});const all=[];
for(const name of names){const src=path.resolve(workspace,name);if(path.dirname(src).toLowerCase()!==path.resolve(workspace).toLowerCase()||!fs.existsSync(src+'/ProjectSettings/ProjectVersion.txt'))throw Error('Unverified review target');const rows=[];
 for(const rel of walk(src)){const a=path.join(src,rel),sha=hash(a),current=path.join(original,rel);let dest;
  if(fs.existsSync(current)&&fs.statSync(current).isFile()&&hash(current)===sha){dest=current;}else{const mapped=rel.replace(/^(Assets|Packages|ProjectSettings)\//,'$1.snapshot/');dest=path.join(out,name,mapped);fs.mkdirSync(path.dirname(dest),{recursive:true});fs.copyFileSync(a,dest);if(hash(dest)!==sha)throw Error('Preservation mismatch: '+rel);}
  rows.push({source:rel,sha256:sha,preservedAt:dest.replace(/\\/g,'/')});
 }
 const entry={source:src.replace(/\\/g,'/'),files:rows,excludedGeneratedDirectories:[...excluded],preservedFileCount:rows.length};fs.mkdirSync(out+'/'+name,{recursive:true});fs.writeFileSync(out+'/'+name+'/preservation-manifest.json',JSON.stringify(entry,null,2));all.push({source:entry.source,files:rows.length,copiedUnique:rows.filter(r=>r.preservedAt.startsWith(out)).length,manifest:out+'/'+name+'/preservation-manifest.json'});
}
fs.writeFileSync(out+'/preservation-summary.json',JSON.stringify({time:new Date().toISOString(),projects:all},null,2));console.log(JSON.stringify(all));
