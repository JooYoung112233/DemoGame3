import assert from'node:assert/strict';
import{readFileSync}from'node:fs';
import{speakerLabel,speakerNamesAt}from'../design/ui/ch00-01/speaker-identity.mjs';
const data=JSON.parse(readFileSync(new URL('../design/ui/ch00-01/memory-dialogue.json',import.meta.url),'utf8'));
let n=0;function ok(v){assert.ok(v);n++;}
ok(speakerLabel(data,0)==='');
ok(speakerLabel(data,1)==='?');
ok(speakerLabel(data,2)==='?');
ok(speakerNamesAt(data,2).soi===undefined);
ok(speakerNamesAt(data,3).soi==='소이');
ok(speakerLabel(data,3)==='수혁');
ok(speakerLabel(data,4)==='?');
ok(speakerLabel(data,5)==='소이');
ok(speakerLabel(data,6)==='?');
ok(speakerLabel(data,7)==='수혁');
ok(speakerLabel(data,8)==='');
ok(speakerLabel(data,9)==='소이');
ok(speakerNamesAt(data,8).seoyeon===undefined);
ok(speakerNamesAt(data,9).seoyeon==='서연');
const revealed=structuredClone(data);
revealed.names_known_on_entry={};
revealed.name_reveals=[{after_line:data.lines[3].id,speaker_id:'suhyeok',display_name:'수혁'}];
ok(speakerLabel(revealed,3)==='?'); // A name stays hidden throughout the introduction line.
ok(speakerLabel(revealed,7)==='수혁');
ok(speakerLabel(revealed,9)==='?'); // Another speaker is not disclosed by this event.
ok(speakerLabel(revealed,0)===''); // Narration has no nameplate.
revealed.name_reveals=[{after_line:'missing-line',speaker_id:'soi',display_name:'소이'}];
ok(speakerLabel(revealed,9)==='?');
console.log(JSON.stringify({passed:n,failed:0,scope:'names revealed by earlier address and dialogue; per-speaker timing, unknown identity, no retroactive reveal, narration remains nameless'}));
