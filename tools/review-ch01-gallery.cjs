// A bounded end-to-end review of the chapter-one art preview.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const html=fs.readFileSync(path.join(process.argv[2],'chapter-one.html'),'utf8');
const data=JSON.parse(fs.readFileSync('design/chapter01/gallery-scenes.json','utf8'));
const out='art/chapter01/gallery-qa';fs.mkdirSync(out,{recursive:true});
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const errors=[],issues=[],checks={states:0,hotspotClicks:0,navigation:0,zoomRoundTrips:0};
 const page=await browser.newPage();page.on('pageerror',e=>errors.push(e.message));
 const settle=(sc,id)=>page.waitForFunction(expected=>document.getElementById('live49-chapter-one').dataset.rendered===expected,sc+'/'+id);
 for(const width of [736,360,320]){
  await page.setViewportSize({width,height:900});await page.emulateMedia({reducedMotion:'reduce'});
  await page.setContent('<body style="margin:0;background:#171c1a">'+html+'</body>');
  for(let n=0;n<data.scenes.length;n++){
   const sc=data.scenes[n];await page.getByLabel('장면 선택',{exact:true}).selectOption(String(n));
   for(let k=0;k<sc.presets.length;k++){
    const s=sc.presets[k];await page.locator(`button[data-state="${s.id}"]`).click();await settle(sc.id,s.id);checks.states++;
    if(width===736||width===320)await page.locator('.photo').screenshot({path:`${out}/audit-${width}-${sc.id}-${s.id}.png`});
    const geometry=await page.locator('.viewport').evaluate(v=>{
     const r=v.getBoundingClientRect();return [...v.querySelectorAll('.hotspot')].map(b=>{
      const a=b.getBoundingClientRect(),l=b.querySelector('span').getBoundingClientRect();
      return {name:b.getAttribute('aria-label'),button:[a.left-r.left,a.top-r.top,a.right-r.left,a.bottom-r.top],label:[l.left-r.left,l.top-r.top,l.right-r.left,l.bottom-r.top],frame:[r.width,r.height]};
     });
    });
    for(const g of geometry)for(const kind of ['button','label']){
     const a=g[kind];if(a[0]<-1||a[1]<-1||a[2]>g.frame[0]+1||a[3]>g.frame[1]+1)issues.push({width,scene:sc.id,state:s.id,kind,name:g.name,bounds:a,frame:g.frame});
    }
    const names=await page.locator('.hotspot').evaluateAll(bs=>bs.map(b=>b.getAttribute('aria-label')));
    if(names.length!==(s.active||[]).length)issues.push({width,scene:sc.id,state:s.id,kind:'missing-active-target',expected:s.active,actual:names});
    for(const name of names){
     await page.locator(`button[data-state="${s.id}"]`).click();await settle(sc.id,s.id);
     await page.getByRole('button',{name,exact:true}).last().click();checks.hotspotClicks++;
     await page.waitForFunction(()=>{const r=document.getElementById('live49-chapter-one');return r.dataset.rendered===r.dataset.scene+'/'+r.dataset.state;});
    }
    await page.locator(`button[data-state="${s.id}"]`).click();await settle(sc.id,s.id);
    if(!(sc.id==='letter'&&s.id==='read')){
     const before=await page.locator('.world').getAttribute('style');
     await page.getByRole('button',{name:'전체 보기',exact:true}).click();await settle(sc.id,s.id);
     await page.getByRole('button',{name:'가까이 보기',exact:true}).click();await settle(sc.id,s.id);
     assert.equal(await page.locator('.world').getAttribute('style'),before);checks.zoomRoundTrips++;
    }
    if(k>0){
     await page.getByRole('button',{name:'이전',exact:true}).click();await settle(sc.id,sc.presets[k-1].id);
     await page.getByRole('button',{name:'다음',exact:true}).click();await settle(sc.id,s.id);checks.navigation++;
    }
    assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
   }
  }
 }
 // Rapid scene and pose changes must finish on the same pixels as an ordinary selection.
 await page.getByLabel('장면 선택',{exact:true}).selectOption('3');await page.locator('button[data-state="approach"]').click();await settle('byeolddongi','approach');
 const capture=()=>page.locator('.world canvas').evaluate(c=>c.toDataURL());const expected=await capture();
 await page.evaluate(()=>{const select=document.querySelector('#live49-chapter-one select');for(const i of [0,4,2,1,5,3]){select.value=String(i);select.dispatchEvent(new Event('change'));}document.querySelector('button[data-state="approach"]').click();});
 await settle('byeolddongi','approach');assert.equal(await capture(),expected);
 // Resize an existing scene, so label placement cannot rely on a page reload.
 await page.getByLabel('장면 선택',{exact:true}).selectOption('2');await settle('store','enter');
 for(const width of [736,320,736]){
  await page.setViewportSize({width,height:900});
  await page.waitForFunction(()=>{const v=document.querySelector('.viewport').getBoundingClientRect(),b=document.querySelector('.hotspot[aria-label="냉장고"]'),l=b.querySelector('span').getBoundingClientRect(),r=b.getBoundingClientRect();return l.top>=v.top&&l.bottom<=v.bottom&&(innerWidth>480?l.bottom<r.top:l.top>r.bottom);});
 }
 assert.deepEqual(errors,[]);
 const report={widths:[736,360,320],checks,rapidSceneSwitchPixels:'pass',resizeLabels:'pass',errors,issues};
 fs.writeFileSync(`${out}/review-report.json`,JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));
 assert.deepEqual(issues,[],'Review found clipped labels or missing click targets.');
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exitCode=1});
