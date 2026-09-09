const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const story=JSON.parse(fs.readFileSync('design/interaction/ch01-first-drawing-v1.json','utf8'));
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});
try{const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
for(const width of [736,360]){
 await page.setViewportSize({width,height:900});await page.emulateMedia({reducedMotion:'reduce'});await page.goto('http://127.0.0.1:8774');
 await page.locator('.world img').evaluateAll(imgs=>Promise.all(imgs.map(i=>i.decode())));
 const root=page.locator('#live49-first-drawing');
 const stable=()=>page.locator('.base,img[data-layer="sketchbook"],img[data-layer="mug"]').evaluateAll(imgs=>imgs.map(i=>[i.src,i.style.cssText]));
 const initial=await stable();
 await page.getByRole('button',{name:'소이의 스케치북',exact:true}).click();
 for(let i=0;i<story.beats.length;i++){
   const beat=story.beats[i];assert.equal(await root.getAttribute('data-beat'),beat.id);assert.equal(await root.getAttribute('data-page'),beat.page);
   assert.equal(await page.locator('.line').textContent(),beat.text);
   assert.equal(await page.locator('img[data-layer="sea-started"]').isVisible(),beat.page==='started');
   assert.equal(await page.locator('img[data-layer="sea-complete"]').isVisible(),beat.page==='complete');
   assert.deepEqual(await stable(),initial);
   if(i===0||i===4||i===8)await page.screenshot({path:path.resolve(`art/chapter01/layers/qa/story-${width}-${beat.id}.png`),fullPage:true});
   if(i===4){await page.getByRole('button',{name:'이전',exact:true}).click();assert.equal(await root.getAttribute('data-page'),'started');await page.getByRole('button',{name:'다음',exact:true}).click();}
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
   await page.getByRole('button',{name:i===story.beats.length-1?'그림 남기기':'다음',exact:true}).click();
 }
 assert.equal(await root.getAttribute('data-mode'),'hub');assert.equal(await root.getAttribute('data-completed'),'true');assert.equal(await root.getAttribute('data-page'),'complete');
 await page.getByRole('button',{name:'바다 그림',exact:true}).click();assert.equal(await root.getAttribute('data-beat'),'memory');
 await page.getByRole('button',{name:'처음부터',exact:true}).click();assert.equal(await root.getAttribute('data-beat'),'ask');assert.equal(await root.getAttribute('data-page'),'blank');
}
assert.deepEqual(errors,[]);console.log('736/360px: 9 beats, page/pose flow, back/replay, retained final drawing, fixed base/book, no overflow or script errors.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1});
