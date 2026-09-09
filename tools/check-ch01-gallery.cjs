const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),vm=require('node:vm');
const dir=process.argv[2],fragment=fs.readFileSync(path.join(dir,'chapter-one.html'),'utf8');
for(const m of fragment.matchAll(/<script>([\s\S]*?)<\/script>/g))new vm.Script(m[1]);
assert(Buffer.byteLength(fragment)<1000000);
const data=JSON.parse(fs.readFileSync('design/chapter01/gallery-scenes.json','utf8'));
const qa=path.resolve('art/chapter01/gallery-qa');fs.mkdirSync(qa,{recursive:true});
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
for(const width of [736,360]){
 await page.setViewportSize({width,height:900});await page.emulateMedia({reducedMotion:'reduce'});await page.setContent('<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1"></head><body style="margin:0;background:#171c1a">'+fragment+'</body></html>');
 const root=page.locator('#live49-chapter-one');
 for(let n=0;n<data.scenes.length;n++){
  const sc=data.scenes[n];await page.getByLabel('장면 선택',{exact:true}).selectOption(String(n));
  await page.locator('.world img').evaluateAll(imgs=>Promise.all(imgs.map(i=>i.decode())));
  const base=await page.locator('.base').getAttribute('src');
  for(const state of sc.presets){
   await page.locator(`button[data-state="${state.id}"]`).click();assert.equal(await root.getAttribute('data-state'),state.id);
   await page.waitForFunction(expected=>document.getElementById('live49-chapter-one').dataset.rendered===expected,sc.id+'/'+state.id);
   assert.equal(await page.locator('.base').getAttribute('src'),base);
   for(const l of [...sc.slots,...(sc.actors||[]),...(sc.pages||[]),...(sc.foreground?[sc.foreground]:[])]){
    assert.equal(await page.locator(`img[data-layer="${l.id}"]`).evaluate(i=>i.hidden),!(l.alwaysVisible||state.visible.includes(l.id)),sc.id+'/'+state.id+'/'+l.id);
   }
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),sc.id+'/'+state.id+' overflow');
   if(['cook','meal','approach','fridge','stationery','reach','read','promise','journal','next'].includes(state.id))await page.screenshot({path:path.join(qa,`${width}-${sc.id}-${state.id}.png`),fullPage:true});
  }
  if(sc.id==='letter'){
   let collected=[];
   for(let p=0;p<4;p++){
    assert.equal(await root.getAttribute('data-letter-page'),String(p));
    collected.push(...await page.locator('.letter-paper p').allTextContents());
    assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
    if(p<3)await page.getByRole('button',{name:'다음',exact:true}).click();
   }
   assert.deepEqual(collected,data.letter.paragraphs);assert(await page.getByRole('button',{name:'다음',exact:true}).isDisabled());
   await page.getByRole('button',{name:'이전',exact:true}).click();assert.equal(await root.getAttribute('data-letter-page'),'2');
  }
 }
 // Dynamic targets: no pickup before the refrigerator has been selected.
 await page.getByLabel('장면 선택',{exact:true}).selectOption('2');
 assert.equal(await page.locator('.hotspot').count(),2);await page.getByRole('button',{name:'냉장고',exact:true}).last().click();
 assert.equal(await root.getAttribute('data-state'),'fridge');assert.equal(await page.locator('.hotspot').count(),1);
 await page.getByRole('button',{name:'음료 병',exact:true}).first().click();assert.equal(await root.getAttribute('data-state'),'recovered');
 assert.equal(await page.locator('.hotspot').count(),0);
 await page.getByRole('button',{name:'전체 보기',exact:true}).click();assert.equal(await page.getByRole('button',{name:'가까이 보기',exact:true}).count(),1);
}
assert.deepEqual(errors,[]);fs.writeFileSync(path.join(qa,'report.json'),JSON.stringify({widths:[736,360],scenes:data.scenes.length,states:data.scenes.reduce((n,s)=>n+s.presets.length,0),checks:['all images decode','layer states','fixed base','original 8 letter paragraphs','dynamic hotspots','zoom','no horizontal overflow','no script errors'],errors},null,2));
console.log('736/360px: every scene/state, alpha-layer visibility, fixed base, 8 original letter paragraphs, dynamic hotspots, zoom, no overflow/script errors.');
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exitCode=1});
