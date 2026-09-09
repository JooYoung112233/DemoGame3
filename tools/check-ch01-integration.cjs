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
   return {size:[actual.width,actual.height],base:[world.querySelector('.base').naturalWidth,world.querySelector('.base').naturalHeight],equal:[1060,1120,1180].every(x=>[615,630,650].every(y=>String(actual.getContext('2d').getImageData(x,y,1,1).data)===String(ctx.getImageData(x,y,1,1).data)))};
 });
 assert.deepEqual(beam.size,[1672,941]);assert.deepEqual(beam.base,[1672,941]);assert(beam.equal,'Foreground beam must occlude the actor with unchanged base pixels.');
 await page.getByLabel('장면 선택',{exact:true}).selectOption('3');await page.locator('[data-state="approach"]').click();
 await page.waitForFunction(()=>document.getElementById('live49-chapter-one').dataset.rendered==='byeolddongi/approach');
 const pixels=()=>page.locator('.world canvas').evaluate(c=>Array.from(c.getContext('2d').getImageData(713,907,1,1).data));
 const withFather=await pixels();await page.locator('[data-state="meet"]').click();
 await page.waitForFunction(()=>document.getElementById('live49-chapter-one').dataset.rendered==='byeolddongi/meet');
 assert.notDeepEqual(await pixels(),withFather,'Father and his contact shadow must disappear together.');
 console.log('Native base/canvas resolution, foreground occlusion pixels and state-bound contact shadow verified.');
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exitCode=1});
