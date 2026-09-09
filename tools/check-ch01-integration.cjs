const fs=require('node:fs'),assert=require('node:assert/strict');
const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const html=fs.readFileSync(process.argv[2],'utf8');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const page=await browser.newPage({viewport:{width:736,height:900}});await page.emulateMedia({reducedMotion:'reduce'});await page.setContent(html);
 await page.getByLabel('장면 선택',{exact:true}).selectOption('2');await page.locator('[data-state="stationery"]').click();
 await page.waitForFunction(()=>document.getElementById('live49-chapter-one').dataset.rendered==='store/stationery');
 const beam=await page.locator('.world').evaluate(world=>{
   const actual=world.querySelector('canvas'),expected=document.createElement('canvas');expected.width=1672;expected.height=941;
   const ctx=expected.getContext('2d');ctx.drawImage(world.querySelector('.base'),0,0,1672,941);
   const a=actual.getContext('2d').getImageData(1032,610,240,20).data,b=ctx.getImageData(1032,610,240,20).data;
   return {size:[actual.width,actual.height],base:[world.querySelector('.base').naturalWidth,world.querySelector('.base').naturalHeight],foregroundVisible:a.some((v,i)=>v!==b[i])};
 });
 assert.deepEqual(beam.size,[1672,941]);assert.deepEqual(beam.base,[1672,941]);assert(beam.foregroundVisible,'Actor feet must remain visible through the removed foreground beam.');
 await page.getByLabel('장면 선택',{exact:true}).selectOption('3');await page.locator('[data-state="approach"]').click();
 await page.waitForFunction(()=>document.getElementById('live49-chapter-one').dataset.rendered==='byeolddongi/approach');
 const pixels=()=>page.locator('.world canvas').evaluate(c=>Array.from(c.getContext('2d').getImageData(718,901,1,1).data));
 const withFather=await pixels();await page.locator('[data-state="meet"]').click();
 await page.waitForFunction(()=>document.getElementById('live49-chapter-one').dataset.rendered==='byeolddongi/meet');
 assert.notDeepEqual(await pixels(),withFather,'Father and his contact shadow must disappear together.');
 console.log('Native base/canvas resolution, open-front actor visibility and state-bound contact shadow verified.');
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exitCode=1});
