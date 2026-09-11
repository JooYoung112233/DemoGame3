import assert from'node:assert/strict';
import{readFileSync}from'node:fs';
import{sampleFrame,durationOf,configured,graphemes,transitionFrame,transitionDuration}from'../design/ui/ch00-01/opening-motion-core.mjs';
const base=JSON.parse(readFileSync(new URL('../design/unity-handoff/C0-opening-direction-v1.json',import.meta.url),'utf8'));
let checks=0;function ok(test){assert.ok(test);checks++;}
for(const preset of['gentle','base','clear']){
 const config=configured(base,preset),end=durationOf(config);
 const first=sampleFrame(0,config),last=sampleFrame(end,config);
 ok(first.scale===1&&first.menuOpacity===1&&!first.complete&&first.pressed);
 ok(last.scale===config.camera.scale_to&&last.menuOpacity===0&&last.complete&&!last.pressed);
 let prior=first;
 for(let i=1;i<=100;i++){
  const f=sampleFrame(end*i/100,config);
  assert.ok(f.scale>=prior.scale&&f.menuOpacity<=prior.menuOpacity);
  assert.ok(f.x<=0&&f.y<=0&&f.x+1920*f.scale>=1920&&f.y+1080*f.scale>=1080);
  const[x,y]=config.camera.focus_normalized;
  assert.ok(Math.abs(f.x+1920*x*f.scale-1920*x)<1e-8);
  assert.ok(Math.abs(f.y+1080*y*f.scale-1080*y)<1e-8);
  prior=f;
 }
 checks+=3;
 ok(sampleFrame(-1,config).scale===1&&sampleFrame(20,config).scale===config.camera.scale_to);
}
ok(base.camera.duration_seconds===1.2&&base.camera.scale_to===1.035);
ok(sampleFrame(.15,base).scale===1&&sampleFrame(.48,base).menuOpacity===0);
ok(sampleFrame(1.35,base).dimOpacity===0&&sampleFrame(1.35,base).scale===1.035);
ok(sampleFrame(1.55,base).dimOpacity>0&&sampleFrame(1.55,base).dimOpacity<1);
ok(sampleFrame(1.749,base).visibleGraphemes===0);
for(const[t,count]of[[1.75,1],[1.859,1],[1.86,2],[1.97,3],[2.08,4]])ok(sampleFrame(t,base).visibleGraphemes===count);
ok(Math.abs(durationOf(base)-2.08)<1e-8&&sampleFrame(10,base).visibleGraphemes===4);
ok(graphemes('수혁아.').length===4);
ok(base.approval.camera_and_menu.approved&&base.approval.call_timing.approved&&!base.approval.memory_transition.approved);
ok(transitionDuration(base)===1);
ok(transitionFrame(0,base).blackOpacity===0&&!transitionFrame(0,base).showMemory);
ok(transitionFrame(.349,base).blackOpacity>.99&&!transitionFrame(.349,base).showMemory);
ok(transitionFrame(.35,base).blackOpacity===1&&transitionFrame(.35,base).showMemory);
ok(transitionFrame(.44,base).blackOpacity===1&&transitionFrame(.44,base).showMemory);
ok(transitionFrame(.7,base).blackOpacity>0&&transitionFrame(.7,base).blackOpacity<1);
ok(transitionFrame(1,base).blackOpacity===0&&transitionFrame(1,base).complete);
console.log(JSON.stringify({passed:checks,failed:0,scope:'D00-D08 camera, dim, graphemes, opaque-only scene swap, transition boundaries, approval separation'}));
