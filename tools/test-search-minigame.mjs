import assert from'node:assert/strict';
import{fresh,begin,finish,acknowledge,decide,restore,marker,hit,chance,SPOTS}from'../design/minigames/search-v1/core.mjs';
const rng=values=>{let i=0;return()=>values[i++]??.99};
let s=begin(fresh(),'fridge','focus',rng([.99,.55,.4,.99]));assert.equal(s.minutes,15);
let win=finish(s,true),loss=finish(s,false);assert.equal(win.inventory.water,3);assert.equal(win.inventory.drink,1);assert.equal(loss.inventory.water,2);assert.equal(loss.inventory.drink,undefined);
assert.deepEqual(finish(win,true),win);assert.throws(()=>begin(win,'food','quick'));s=acknowledge(win);assert.throws(()=>begin(s,'fridge','quick'));
const quick=finish(begin(fresh(),'fridge','quick',()=>.99),true);assert.equal(quick.minutes,5);assert.equal(quick.inventory.water,2);assert.equal(quick.last.success,false);
const noisy=finish(begin(fresh(),'box','focus',rng([.1,.1,.1,0])),true);assert.equal(noisy.last.noise,true);s=acknowledge(noisy);assert.equal(s.phase,'warning');assert.throws(()=>begin(s,'food','quick'));
const stay=decide(s,'stay');assert.equal(stay.caution,1);assert.equal(stay.phase,'explore');assert.deepEqual(stay.inventory,noisy.inventory);
const ret=decide(s,'return');assert.equal(ret.phase,'returned');assert.deepEqual(ret.inventory,noisy.inventory);assert.throws(()=>begin(ret,'food','quick'));
const restored=restore(JSON.stringify(begin(fresh(),'fridge','focus',rng([.99,.55,.4,0]))));assert.equal(restored.minutes,15);assert.equal(restored.inventory.water,2);assert.equal(restored.last.reason,'interrupted');assert.deepEqual(restore(JSON.stringify(restored)),restored);
assert.deepEqual(restore('bad'),fresh());assert.deepEqual(restore('null'),fresh());assert.equal(chance(.9,true),1);assert.equal(marker(0),0);assert.equal(marker(2.6),1);assert.equal(marker(5.2),0);assert.equal(hit(.5),true);assert.equal(hit(.3),false);assert.equal(hit(.35,true),true);
for(const spot of SPOTS){const a=begin(fresh(),spot.id,'focus',()=>.1);assert.equal(finish(a,true).last.noise,finish(a,false).last.noise)}
s=fresh();for(const x of SPOTS){s=acknowledge(finish(begin(s,x.id,'quick',()=>.99)));}assert.equal(s.searched.length,4);assert.equal(s.minutes,20);
console.log('PASS: reward thresholds, guarantees, separate noise, time, duplicate guards, interruption restore, decisions, timer and four-spot completion');
