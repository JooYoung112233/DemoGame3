import fs from 'node:fs';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {initialStory,choicesFor,advanceStory} from '../design/ui/day01-v1/story-core.mjs';
const require=createRequire(import.meta.url);
const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=new URL('../',import.meta.url),data=JSON.parse(fs.readFileSync(new URL('design/ui/day01-v1/story.json',root))),manifest=JSON.parse(fs.readFileSync(new URL('design/ui/day01-v1/manifest.json',root)));
for(const n of Object.values(data.steps))if(n.screen){const s=manifest.screens.find(s=>s.id===n.screen);assert.ok(s,n.screen);assert.ok(fs.existsSync(new URL(s.preview,root)),s.preview)}
const key=s=>JSON.stringify([s.step,Object.entries(s.flags).sort()]),queue=[initialStory()],seen=new Set(),ends=new Set();
for(let i=0;i<queue.length;i++){
 const state=queue[i];if(seen.has(key(state)))continue;seen.add(key(state));const n=data.steps[state.step];
 if(['return','ration','kitchen','soi','quest_owned','quest_search'].includes(state.step))assert.ok(state.flags.water_checked&&state.flags.food_checked);
 if(state.step==='quest_owned'){assert.ok(state.flags.pencils_owned&&state.flags.request_heard&&state.flags.ration_seen);ends.add('owned')}
 if(state.step==='quest_search'){assert.ok(!state.flags.pencils_owned&&state.flags.request_heard&&state.flags.ration_seen);ends.add('search')}
 choicesFor(data,state).forEach((a,index)=>{const before=JSON.stringify(state),next=advanceStory(data,state,index);assert.equal(JSON.stringify(state),before);for(const [f,v]of Object.entries(state.flags))if(!(f in (a.set||{})))assert.equal(next.flags[f],v);queue.push(next)});
 assert.ok(seen.size<10000,'Bounded branch state space');
}
assert.deepEqual([...ends].sort(),['owned','search']);
const browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage({viewport:{width:1440,height:1100}}),errors=[];
page.on('pageerror',e=>errors.push(String(e)));page.on('response',r=>{if(r.status()>=400)errors.push(r.status()+' '+r.url())});
const url='http://127.0.0.1:8793/design/ui/day01-v1/story.html';
async function click(name,step){await page.getByRole('button',{name,exact:true}).click();if(step)await page.locator(`body[data-step="${step}"]`).waitFor()}
async function start(){await page.goto(url);await page.locator('body[data-step="morning"]').waitFor();await click('주변 지도를 연다','map');await click('편의점으로 출발','travel');await click('편의점 안을 살핀다','store')}
async function water(){await click('냉장고 · 물','fridge');await click('빠른 탐색 사례','water_result');await click('물 확보를 확인하고 돌아간다','store')}
async function food(){await click('식품 선반 · 바로 먹을 것','food');await click('식량 탐색 결과 사례','food_result');await click('식량 확보를 확인하고 돌아간다','store')}
async function finish(expected){await click('물과 식량을 나눈다','ration');await click('첫 분배를 확인한다','kitchen');await click('식사를 마무리한다','soi');await click('소이의 부탁을 듣는다',expected)}
try{
 await start();await water();await click('문을 열고 바로 앞 캠핑카로','early_return');await click('바로 앞 편의점에 다시 들어간다','store');assert.equal(await page.getByRole('button',{name:'냉장고 · 물',exact:true}).count(),0);await food();
 await click('계산대 쪽지 읽기','notice');await click('읽고 편의점으로','store');await click('문구대 · 먼저 발견할 수도 있는 색연필','stationery');await click('색연필을 챙긴 사례','store');await click('문을 열고 바로 앞 캠핑카로','return');assert.match(await page.locator('#intent').textContent(),/읽은 대피 안내/);await finish('quest_owned');
 const out=new URL('design/ui/day01-v1/review/',root);await page.locator('#frame').evaluate(img=>img.decode());await page.screenshot({path:new URL('story-owned-review.png',out).pathname.replace(/^\/(\w:)/,'$1'),fullPage:true});
 await start();await food();await water();await click('문을 열고 바로 앞 캠핑카로','return');assert.match(await page.locator('#intent').textContent(),/읽지 않은 쪽지나 주민센터는 언급하지 않는다/);await finish('quest_search');
 await click('이전 검토 장면','soi');await click('첫날 아침부터 다시','morning');
 assert.equal((await page.request.get('http://127.0.0.1:8793/docs/CH01-FIRST-WEEK-STORY.ko.md')).status(),200);
 assert.deepEqual(errors,[]);
 const report={passed:true,reachable_states:seen.size,endings:[...ends],early_return_preserves_supplies:true,optional_nodes_not_required:true,notice_conditional_intent:true,early_pencils_acknowledged:true,browser_errors:errors,actual_game_tested:false};fs.writeFileSync(new URL('story-validation.json',out),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
}finally{await browser.close()}
