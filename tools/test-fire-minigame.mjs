import assert from 'node:assert/strict';
import{fresh,start,step}from'../design/minigames/fire-v1/core.mjs';
function run(control,easy=false){let s=start(easy);for(let i=0;i<1200&&s.phase==='playing';i++)s=step(s,control(s),.05);return s}
const idle=run(()=>false);assert.equal(idle.reason,'cold');
const constant=run(()=>true);assert.equal(constant.reason,'smoke');
const win=run(s=>s.heat<56);assert.equal(win.phase,'success');assert.ok(win.time>10&&win.time<30);
const easy=run(s=>s.heat<56,true);assert.equal(easy.phase,'success');
assert.equal(step(win,true,.1),win);assert.equal(step(fresh(),true,.1).phase,'ready');
let s=start();for(let i=0;i<30;i++)s=step(s,true,.05);const air=s.air;s=step(s,false,.05);assert.ok(s.air>0&&s.air<air);
assert.equal(step(s,true,NaN),s);assert.equal(step(s,true,-1),s);
for(const state of [idle,constant,win,easy])for(const key of ['air','heat','smoke','progress'])assert.ok(state[key]>=0&&state[key]<=100);
console.log(`PASS: no input → cold, held input → scatter, controlled input → fire (${win.time.toFixed(1)}s), easy mode, inertia, terminal guards, bounds`);
