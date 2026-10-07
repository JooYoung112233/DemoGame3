const fs=require('fs'),path=require('path'),cp=require('child_process');
const output=path.resolve('검증/v052-feedback/compile');fs.mkdirSync(output,{recursive:true});
const candidates=path.resolve('아트/캐릭터-연결보정-v052/code-candidate');
const csc='C:/Program Files/dotnet/sdk/10.0.401/Roslyn/bincore/csc.dll';
function files(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):e.name.endsWith('.cs')?[path.join(dir,e.name)]:[]);}
let results=[];
for(const name of ['Core','Game']){
 const project=fs.readFileSync('Demo6.'+name+'.csproj','utf8');
 const refs=[...project.matchAll(/<HintPath>([^<]+)<\/HintPath>/g)].map(x=>path.resolve(x[1]));
 for(const x of project.matchAll(/<ProjectReference Include="([^"]+)"/g)){
  const n=path.basename(x[1],'.csproj');refs.push(n==='Demo6.Core'?path.join(output,n+'.dll'):path.resolve('Library/ScriptAssemblies/'+n+'.dll'));
 }
 const definitions=project.match(/<DefineConstants>([^<]+)<\/DefineConstants>/)[1];
 let sources=files('Assets/Scripts/'+name).map(s=>fs.existsSync(path.join(candidates,path.basename(s)))?path.join(candidates,path.basename(s)):path.resolve(s));
 if(name==='Game')sources.push(path.join(candidates,'WeaponEdgeV052.cs'));
 const args=['-nologo','-target:library','-langversion:9.0','-unsafe','-nostdlib+','-define:'+definitions,'-out:"'+path.join(output,'Demo6.'+name+'.dll')+'"',...refs.map(r=>'-r:"'+r+'"'),...sources.map(s=>'"'+s+'"')];
 const rsp=path.join(output,name+'.rsp');fs.writeFileSync(rsp,args.join('\n'));
 const r=cp.spawnSync('C:/Program Files/dotnet/dotnet.exe',[csc,'@'+rsp],{encoding:'utf8',windowsHide:true});
 fs.writeFileSync(path.join(output,name+'.log'),(r.stdout||'')+(r.stderr||'')+(r.error?.message||''));results.push({name,status:r.status,error:r.error?.message,output:r.stdout});
 if(r.status!==0)break;
}
console.log(JSON.stringify(results,null,2));process.exitCode=results.some(r=>r.status!==0)?1:0;
