import fs from 'node:fs';import vm from 'node:vm';import path from 'node:path';import {fileURLToPath} from 'node:url';import assert from 'node:assert/strict';
const root=path.dirname(fileURLToPath(import.meta.url));vm.runInThisContext(fs.readFileSync(path.join(root,'rig.js'),'utf8'));
const {pose,Controller}=globalThis.SouthRig;
assert.deepEqual(pose('attack',0),pose('attack',1),'Attack must return to exactly the starting stance');
for(const clip of ['idle','walk','run'])for(const [key,value] of Object.entries(pose(clip,0)))assert.ok(Math.abs(value-pose(clip,1)[key])<1e-10,clip+' loop seam '+key);
const checks=[];
for(const [from,to] of [['idle','walk'],['walk','run'],['run','walk'],['walk','idle'],['run','idle'],['idle','attack'],['walk','attack'],['run','attack']]){
 const c=new Controller();c.set(from);for(let i=0;i<43;i++)c.step(1/60);
 const before={...c.current},phase=c.phase;c.set(to);assert.deepEqual(c.current,before,'State switch must preserve current pose');
 if(['walk','run'].includes(from)&&['walk','run'].includes(to))assert.equal(c.phase,phase,'Gait phase must survive walk/run transition');
 const next=c.step(1/120);const maxJump=Math.max(...Object.keys(before).map(k=>Math.abs(next[k]-before[k])));assert.ok(maxJump<3,from+' -> '+to+' discontinuity '+maxJump);
 checks.push({from,to,phasePreserved:['walk','run'].includes(from)&&['walk','run'].includes(to),largestFirstStep:maxJump});
}
const c=new Controller();c.set('attack');for(let i=0;i<61;i++)c.step(1/60);assert.equal(c.state,'idle','Attack must automatically recover');
const manifest=JSON.parse(fs.readFileSync(path.join(root,'animation-manifest.json'),'utf8'));for(const [clip,n] of Object.entries({idle:8,walk:12,run:12,attack:12})){assert.equal(manifest.clips[clip].files.length,n);for(const file of manifest.clips[clip].files)assert.ok(fs.existsSync(path.join(root,file)));}
const report={attackStartEqualsEnd:true,allLoopSeamsContinuous:true,attackAutoReturnsIdle:true,expectedFrameCounts:true,transitions:checks};fs.writeFileSync(path.join(root,'continuity-checks.json'),JSON.stringify(report,null,2));console.log(report);
