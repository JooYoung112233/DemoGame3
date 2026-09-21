import fs from 'node:fs';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
const root=new URL('../',import.meta.url),read=p=>JSON.parse(fs.readFileSync(new URL(p,root),'utf8'));
const c0=read('design/chapter00/continuation-v1/story.json'),d1=read('design/ui/day01-v1/story.json'),baseline=read('design/story/ch00-01-revision-v1/dialogue-baseline.json');
for(const[id,lines]of Object.entries(baseline.c0_lines))assert.deepEqual(c0.nodes[id].lines,lines,`Dialogue changed: ${id}`);
for(const[id,labels]of Object.entries(baseline.c0_choice_labels))assert.deepEqual(c0.nodes[id].choices.map(c=>c.label),labels,`Dialogue choice changed: ${id}`);
assert.deepEqual(c0.journal_common,baseline.journal_common);
for(const[id,actions]of Object.entries(baseline.d1_actions))assert.deepEqual(d1.steps[id].actions??[],actions,`Day 1 branch changed: ${id}`);
for(const[p,hash]of Object.entries(baseline.protected_hashes))assert.equal(crypto.createHash('sha256').update(fs.readFileSync(new URL(p,root))).digest('hex'),hash,`Protected file changed: ${p}`);
assert.deepEqual(Object.keys(c0.nodes).filter(id=>!(id in baseline.c0_lines)),['camp_radio']);
assert.deepEqual(c0.nodes.camp_radio.lines,[]);
assert.equal(c0.narrative_revision.start_infection,'uninfected');assert.equal(d1.narrative_revision.start_infection,'uninfected');
assert.ok(!JSON.stringify(c0.nodes.camp_radio.effects).includes('infect'));
const cat=read('design/chapter01/event-quest-catalog-v2.json');assert.equal(cat.events.length,25);assert.equal(cat.quests.length,21);
let links=0;
const guide=new URL('docs/00-제작관리/CH00-01-TRUTH-REVISION-STORYBOARD.ko.md',root),text=fs.readFileSync(guide,'utf8');
for(const m of text.matchAll(/\]\(([^)]+)\)/g)){assert.ok(fs.existsSync(new URL(m[1],guide)),m[1]);links++}
for(const m of text.matchAll(/`((?:art|design|tools)\/[^`]+\.(?:png|json|mjs|html))`/g)){assert.ok(fs.existsSync(new URL(m[1],root)),m[1]);links++}
let browserResult=null;
if(process.argv.includes('--browser')){
 const require=createRequire(import.meta.url),{chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1100}}),errors=[];
  page.on('pageerror',e=>errors.push(String(e)));page.on('response',r=>{if(r.status()>=400)errors.push(r.status()+' '+r.url())});
  await page.goto('http://127.0.0.1:8793/design/ui/ch00-01/chapter0-continuation.html?review=camp-radio');
  await page.locator('#story-stage[data-ready="true"][data-node="camp_radio"]').waitFor();
  assert.equal(await page.locator('#panel').isVisible(),false);
  assert.match(await page.locator('#visual-note').textContent(),/대사집/);
  const out=new URL('design/story/ch00-01-revision-v1/radio-review.png',root);
  await page.screenshot({path:decodeURIComponent(out.pathname).replace(/^\/(\w:)/,'$1'),fullPage:true});
  await page.getByRole('button',{name:'방송 확인 후 출발 준비로',exact:true}).click();
  await page.locator('#story-stage[data-ready="true"][data-node="departure"]').waitFor();
  const saved=await page.evaluate(()=>JSON.parse(localStorage.getItem('live49-ch00-continuation-v1')));
  assert.equal(saved.flags.camp_broadcast_heard,true);assert.equal(saved.done.filter(x=>x==='camp_radio').length,1);
  await page.reload();await page.locator('#story-stage[data-ready="true"]').waitFor();await page.locator('#resume').click();
  await page.locator('#story-stage[data-ready="true"][data-node="departure"]').waitFor();
  assert.deepEqual(errors,[]);browserResult={radio_checkpoint:true,confirmation_and_resume:true,errors};
 }finally{await browser.close()}
}
const report={passed:true,existing_dialogue_nodes_preserved:Object.keys(baseline.c0_lines).length,protected_files_unchanged:Object.keys(baseline.protected_hashes).length,day01_action_branches_preserved:Object.keys(baseline.d1_actions).length,new_spoken_lines:0,guide_links_checked:links,browser:browserResult,unity_tested:false};
fs.writeFileSync(new URL('design/story/ch00-01-revision-v1/validation.json',root),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
