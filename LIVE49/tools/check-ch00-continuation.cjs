const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const out=path.resolve(__dirname,'../design/chapter00/continuation-v1/review');fs.mkdirSync(out,{recursive:true});
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage({viewport:{width:1920,height:1200}});const errors=[];page.on('pageerror',e=>errors.push(String(e)));page.on('response',r=>{if(r.status()>=400)errors.push(r.status()+' '+r.url())});
try{
 await page.goto('http://127.0.0.1:8793/design/ui/ch00-01/chapter0-continuation.html');await page.locator('#story-stage[data-ready="true"]').waitFor();
 await page.getByRole('button',{name:'다른 데 가 보고 싶어?',exact:true}).click();await page.locator('#story-stage[data-node="reply_a"][data-ready="true"]').waitFor();
 const before=await page.locator('#story-stage').getAttribute('data-line');await page.locator('#story-stage').press('Space');assert.equal(await page.locator('#text').textContent(),'다른 데 가 보고 싶어?');assert.equal(await page.locator('#story-stage').getAttribute('data-line'),before,'First press only completes text');
 await page.locator('#instant').check();await page.locator('#reduce').check();await page.locator('#story-stage[data-ready="true"]').waitFor();
 let count=0,reloaded=false;const captures=new Set();
 while(count++<150){
  await page.locator('#story-stage[data-ready="true"]').waitFor();const node=await page.locator('#story-stage').getAttribute('data-node'),line=await page.locator('#story-stage').getAttribute('data-line');
  if(['book','photo_observe','outage','small_light','restored','photo_care','photo_wish','journal_done','day1'].includes(node)&&!captures.has(node)){await page.locator('#story-stage').screenshot({path:path.join(out,node+'.png')});captures.add(node)}
  if(node==='small_light'&&!reloaded){const old=await page.evaluate(()=>localStorage.getItem('live49-ch00-continuation-v1'));await page.reload();await page.locator('#story-stage[data-ready="true"]').waitFor();await page.getByRole('button',{name:'이전 검토 지점 이어 보기'}).click();await page.locator('#story-stage[data-node="small_light"][data-ready="true"]').waitFor();assert.equal(await page.evaluate(()=>localStorage.getItem('live49-ch00-continuation-v1')),old);await page.locator('#instant').check();await page.locator('#reduce').check();reloaded=true;continue}
  if(await page.locator('#end').isVisible())break;
  const options=page.locator('#choices button');
  if(await options.count()){
   const labels=await options.allTextContents();let ix=labels.findIndex(t=>t==='잠깐 창밖 보기');if(ix<0)ix=labels.findIndex(t=>t==='벽의 가족사진 살펴보기');if(ix<0)ix=0;await options.nth(ix).click();
  }else await page.locator('#story-stage').press('Space');
  await page.waitForFunction(({node,line})=>{const s=document.getElementById('story-stage');return s.dataset.ready==='true'&&(s.dataset.node!==node||s.dataset.line!==line)},{node,line});
 }
 assert.ok(count<150);assert.ok(await page.locator('#end').isVisible());
 const saved=JSON.parse(await page.evaluate(()=>localStorage.getItem('live49-ch00-continuation-v1')));assert.ok(saved.flags.night_complete&&saved.flags.book_packed&&saved.flags.family_photo_secured&&saved.flags.photo_observed&&saved.flags.wants_new_photos);assert.equal(errors.length,0,errors.join('\n'));
 await page.getByRole('link',{name:'챕터 1 첫 외출로'}).click();await page.locator('#frame').waitFor();assert.ok(page.url().endsWith('/day01-v1/story.html'));
 await page.goto('http://127.0.0.1:8793/design/ui/ch00-01/opening-motion.html?review=present');await page.locator('#jump-present:not([disabled])').waitFor();await page.locator('#jump-present').click();await page.waitForFunction(()=>document.getElementById('stage').dataset.phase==='conversation');
 for(let i=0;i<10&&!page.url().includes('chapter0-continuation');i++){await page.waitForTimeout(400);if(!page.url().includes('chapter0-continuation'))await page.locator('#stage').press('Space')}
 await page.waitForURL('**/chapter0-continuation.html');await page.locator('#story-stage[data-ready="true"]').waitFor();assert.equal(errors.length,0,errors.join('\n'));
 const report={passed:true,continuation_clicks:count,full_continuation_path:true,mid_outage_reload:true,typewriter_first_press_only:true,opening_handoff:true,day1_story_link:true,screenshots:[...captures],browser_errors:errors,unity_tested:false};fs.writeFileSync(path.join(out,'browser-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exitCode=1});
