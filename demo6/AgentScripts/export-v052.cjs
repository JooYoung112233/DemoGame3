const fs=require('fs'),cp=require('child_process');const root='아트/캐릭터-연결보정-v052';
const ids=JSON.parse(fs.readFileSync(root+'/selected.json'));
const r=cp.spawnSync(process.execPath,[root+'/scripts/export-and-audit.cjs','v052','--only='+ids.join(',')],{encoding:'utf8',windowsHide:true});
console.log(r.stdout);console.error(r.stderr);process.exitCode=r.status;
