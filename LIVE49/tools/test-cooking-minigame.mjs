import assert from 'node:assert/strict';import{fresh,start,select,step}from'../design/minigames/cooking-v1/core.mjs';
function run(control){let s=start(fresh());for(let i=0;i<1000&&s.phase!=='done';i++){s=select(s,control(s));s=step(s,.05)}return s}
const great=run(s=>s.phase==='warming'?(s.heat<78?3:1):(s.heat<68?2:1));assert.equal(great.quality,'great');
for(const l of [1,3]){const s=run(()=>l);assert.equal(s.quality,'normal');assert.equal(s.meals,1);assert.equal(s.gas,2);assert.equal(s.ingredients,2);assert.equal(s.minutes,25)}
assert.equal(start({...fresh(),gas:0}).phase,'ready');let s=start(fresh());assert.equal(start(s),s);const heat=s.heat;s=select(s,3);assert.equal(s.heat,heat);s=step(s,.05);assert.ok(s.heat>heat&&s.heat<120);assert.equal(step(great,.05),great);assert.equal(select(great,3),great);assert.equal(step(s,NaN),s);
console.log(`PASS: controlled cooking ${great.time.toFixed(1)}s / ${great.good.toFixed(1)}s in range, low/high normal meals, fixed costs, inertia, duplicate and terminal guards`);
