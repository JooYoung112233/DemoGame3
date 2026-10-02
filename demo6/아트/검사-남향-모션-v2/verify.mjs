import fs from 'node:fs';import vm from 'node:vm';import path from 'node:path';import assert from 'node:assert/strict';import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));vm.runInThisContext(fs.readFileSync(path.join(root,'player.js'),'utf8'));const clips=JSON.parse(fs.readFileSync(path.join(root,'clips.json'),'utf8'));
const results=[];
for(const from of ['idle','walk','run'])for(const to of ['idle','walk','run','attack'])if(from!==to){
 for(let frame=0;frame<8;frame++){
  const p=new CelMotion.Player(clips);p.enter(from,frame);const before=p.current();p.request(to);const immediate=p.current();
  if(from!=='idle')assert.equal(before.id,immediate.id,'A gait request must not immediately replace the current drawing');
  let seen=false,last=before;const trace=[];
  for(let i=0;i<240;i++){const current=p.step(1/120);if(current.state!==last.state){trace.push({from:last.state,to:current.state,fromFrame:last.frame,toFrame:current.frame});if(['walk','run'].includes(last.state)&&['walk','run'].includes(current.state))assert.ok([2,6].includes(current.frame),'Change locomotion at matching passing phase');}if(current.state===to){seen=true;break}last=current;}
  assert.ok(seen,`${from}:${frame} -> ${to} was never reached`);results.push({from,fromFrame:frame,to,trace});
 }
}
const a=new CelMotion.Player(clips);a.request('attack');for(let i=0;i<120;i++)a.step(1/120);assert.equal(a.state,'idle');
assert.equal(clips.attack.frames[0],'neutral');assert.equal(clips.attack.frames.at(-1),'neutral');assert.equal(clips.idle.frames[0],'neutral');
for(const name of ['idle','walk','run','attack']){assert.ok(clips[name].frames.length>=8&&clips[name].frames.length<=15);for(const id of clips[name].frames)assert.ok(fs.existsSync(path.join(root,'sources',id+'.png')),id+' missing');}
fs.writeFileSync(path.join(root,'continuity-checks.json'),JSON.stringify({checkedRequests:results.length,attackReturnsIdle:true,sharedNeutral:true,locomotionPassingPhaseMaintained:true,results},null,2));console.log('Passed',results.length,'state/phase requests and attack recovery');
