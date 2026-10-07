const fs=require('fs'),cp=require('child_process');
const exe='C:/Users/power/AppData/Local/Unity/bin/unity.exe';
const commands=[
 ['run_script','--file','AgentScripts/UiPointerVerification.cs','--entry','UiPointerVerification.Prepare'],
 ['simulate_key','--key','I','--action','down'],
 ['simulate_key','--key','I','--action','up'],
 ['run_script','--file','AgentScripts/UiPointerVerification.cs','--entry','UiPointerVerification.State','--args','["before close X"]'],
 ['run_script','--file','AgentScripts/UiRuntimeCheck.cs','--entry','UiRuntimeCheck.OpenBag'],
 ['run_script','--file','AgentScripts/UiPointerVerification.cs','--entry','UiPointerVerification.State','--args','["close test setup; not a pointer-open pass"]'],
 ['simulate_pointer','--x','3072','--y','1910.4','--action','move'],
 ['simulate_pointer','--x','3072','--y','1910.4','--action','down'],
 ['simulate_pointer','--x','3072','--y','1910.4','--action','up'],
 ['run_script','--file','AgentScripts/UiPointerVerification.cs','--entry','UiPointerVerification.State','--args','["after close X"]'],
 ['simulate_key','--key','Escape','--action','down'],
 ['simulate_key','--key','Escape','--action','up'],
 ['run_script','--file','AgentScripts/UiPointerVerification.cs','--entry','UiPointerVerification.State','--args','["after Escape cleanup"]'],
 ['run_script','--file','AgentScripts/UiPointerVerification.cs','--entry','UiPointerVerification.Restore']
];
const output=[];
for(const command of commands){const r=cp.spawnSync(exe,['command','--project-path','E:/personalProject/Demo3/demo6',...command],{encoding:'utf8',windowsHide:true});const result={command,status:r.status,stdout:r.stdout,stderr:r.stderr};output.push(result);fs.writeFileSync('아트/UI-정리-v2/검수/supported-pointer-commands.json',JSON.stringify(output,null,2));console.log(r.stdout.trim());if(r.status!==0||r.stdout.includes('"success":false')||r.stdout.includes('"Success":false')){console.log(r.stderr);break;}}
