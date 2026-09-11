import assert from'node:assert/strict';
import{readFileSync}from'node:fs';
import{lineEnd,lineFrame}from'../design/ui/ch00-01/memory-dialogue.mjs';
import{graphemes}from'../design/ui/ch00-01/opening-motion-core.mjs';
const d=JSON.parse(readFileSync(new URL('../design/ui/ch00-01/memory-dialogue.json',import.meta.url),'utf8'));
let n=0;const ok=x=>{assert.ok(x);n++;};
ok(d.lines[0].speaker===''&&d.lines[0].portraits.length===0);
ok(lineFrame(d,0,.59).panel===0&&lineFrame(d,0,.59).count===0);
ok(lineFrame(d,0,.85).count===1);
for(let i=0;i<d.lines.length;i++){
 const end=lineEnd(d,i),f=lineFrame(d,i,end);
 ok(f.count===graphemes(d.lines[i].text).length&&f.cue===0);
 ok(lineFrame(d,i,end+.15+1e-8).cue===1);
 ok(lineFrame(d,i,end+10).count===f.count);
}
ok(d.lines.every(l=>!l.portraits.includes('seoyeon'))&&d.suppressed_portraits.includes('seoyeon'));
ok(d.lines[3].speaker==='수혁'&&d.lines[3].portraits.includes('suhyeok'));
ok(d.lines[4].id==='C0-02-D04'&&d.lines[4].background==='handhold'&&!d.lines[4].end_preview);
ok(d.lines[4].portraits.length===0);
ok(lineFrame(d,4,0).panel===0&&lineFrame(d,4,0).portrait===0);
ok(lineFrame(d,4,.3).panel===1&&lineFrame(d,4,.3).count===1);
ok(d.timing.handhold_pause===.8);
ok(d.lines[5].speaker==='소이'&&d.lines[5].portraits.join()==='soi');
ok(d.lines[6].background==='handhold'&&!d.lines[6].end_preview);
console.log(JSON.stringify({passed:n,failed:0,scope:'narration hold, exact glyph boundaries, per-line waiting, portrait and speaker data'}));
