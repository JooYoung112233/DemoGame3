import assert from'node:assert/strict';
import{readFileSync,existsSync,writeFileSync}from'node:fs';
import{initialState,resolve,step,availableChoices,sceneFor,restore}from'../design/ui/ch00-01/chapter0-story-core.mjs';
const base=new URL('../',import.meta.url),data=JSON.parse(readFileSync(new URL('design/chapter00/continuation-v1/story.json',base),'utf8'));
let paths=0,steps=0,bookGrants=0,minLines=Infinity,maxLines=0,photoSeenPaths=0,photoSkippedPaths=0;const assets=new Set(Object.values(data.scenes));
const baseline=JSON.parse(readFileSync(new URL('design/chapter00/continuation-v1/dialogue-baseline-before-photo-beat.json',base),'utf8'));
for(const[id,lines]of Object.entries(baseline))assert.deepEqual(data.nodes[id].lines,lines,'Dialogue preserved: '+id);
assert.equal(data.nodes.photo_observe.lines.length+data.nodes.photo_care.lines.length+data.nodes.photo_wish.lines.length,0,'No new dialogue before script pass');
for(const p of assets)assert.ok(existsSync(new URL(p,base)),p);
for(const n of Object.values(data.nodes)){if(n.next)assert.ok(data.nodes[n.next]);for(const c of n.choices)assert.ok(data.nodes[c.target]);}
function run(s,visited=[],confirmed=0){
 assert.ok(visited.length<140,'No unbounded loop');steps++;
 assert.equal(sceneFor(data,s),sceneFor(data,restore(data,JSON.stringify(s))),'Exact bookmark restore');
 if(s.node==='outage')assert.ok(s.flags.water_packed&&s.flags.blanket_packed);
 if(s.node==='departure')assert.ok(s.flags.camp_broadcast_heard&&s.flags.travel_goal_known,'Both reply paths hear the camp broadcast before departure');
 if(s.node==='small_light')assert.ok(s.flags.switch_checked&&s.flags.panel_checked);
 if(s.node==='restored')assert.ok(s.flags.connection_secured);
 if(s.node==='photo_wish')assert.equal(s.flags.family_photo_secured,true);
 if(s.node==='book_pack')assert.equal(s.flags.wants_new_photos,true);
 if(s.node==='day1')assert.equal(sceneFor(data,s),data.scenes.journal,'Morning preserves packed supplies and book state');
 const n=data.nodes[s.node];
 if(s.line<n.lines.length){run(step(data,s),[...visited,s.node],confirmed+1);return}
 const choices=availableChoices(data,s);
 if(choices.length){for(let i=0;i<choices.length;i++){const next=step(data,s,i);if(!s.flags.book_packed&&next.flags.book_packed)bookGrants++;if(s.node==='prepare'){assert.ok(!s.flags.water_packed||choices.every(c=>c.target!=='water'));assert.ok(!s.flags.blanket_packed||choices.every(c=>c.target!=='blanket'))}run(next,[...visited,s.node],confirmed)}return}
 assert.equal(s.node,'day1');for(const f of['departure_decided','water_packed','blanket_packed','outage_triggered','flashlight_on','switch_checked','panel_checked','connection_secured','light_restored','book_packed','journal_written','night_complete'])assert.equal(s.flags[f],true,f);
 assert.equal(s.done.filter(x=>x==='camp_radio').length,1);assert.equal(s.flags.camp_broadcast_heard,true);assert.equal(s.flags.travel_goal_known,true);
 assert.equal(s.done.filter(x=>x==='outage').length,1);assert.equal(s.done.filter(x=>x==='book_pack').length,1);assert.equal(s.done.filter(x=>x==='photo_care').length,1);assert.equal(s.flags.family_photo_secured,true);if(s.flags.photo_observed)photoSeenPaths++;else photoSkippedPaths++;assert.ok(s.flags.journal_first_line);assert.ok(!('colored_pencils' in s.flags));paths++;minLines=Math.min(minLines,confirmed);maxLines=Math.max(maxLines,confirmed);
}
run(resolve(data,initialState()));
assert.throws(()=>restore(data,'{}'));assert.throws(()=>step(data,initialState(),999));
assert.ok(photoSeenPaths>0&&photoSkippedPaths>0);
const result={paths_passed:paths,state_steps_checked:steps,book_grant_transitions:bookGrants,spoken_lines_per_path:[minLines,maxLines],photo_observed_paths:photoSeenPaths,photo_skipped_paths:photoSkippedPaths,original_dialogue_unchanged:true,new_dialogue_lines:0,verified_scene_files:assets.size,scope:'Both initial replies, preparation orders and optional window/photo timing, both reassurances, both inspection orders, both journal choices, photo-care gate and exact bookmark restoration. No browser or Unity claims.'};
writeFileSync(new URL('design/chapter00/continuation-v1/validation.json',base),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result));
