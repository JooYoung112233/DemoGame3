const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const path=require('node:path');
const assert=require('node:assert/strict');
(async()=>{
  const browser=await chromium.launch({channel:'msedge',headless:true});
  try{
    const page=await browser.newPage();
    const errors=[];page.on('pageerror',e=>errors.push(e.message));
    for(const width of [736,360]){
      await page.setViewportSize({width,height:800});
      await page.goto('http://127.0.0.1:8772');
      await page.locator('.base').evaluate(img=>img.decode());
      const loaded=await page.locator('.world img').evaluateAll(imgs=>Promise.all(imgs.map(img=>img.decode().then(()=>img.naturalWidth>0))));
      assert(loaded.every(Boolean));
      await page.getByRole('button',{name:'베이스만',exact:true}).click();
      assert.equal(await page.locator('img[data-slot]:visible').count(),0);
      await page.getByRole('button',{name:'소품 배치',exact:true}).click();
      assert.equal(await page.locator('img[data-slot]:visible').count(),3);
      assert.equal(await page.locator('.focus-box').isVisible(),false);
      await page.getByRole('button',{name:'스케치북 사건',exact:true}).click();
      assert.equal(await page.locator('.focus-box').isVisible(),true);
      await page.getByRole('button',{name:'식탁 확대',exact:true}).click();
      assert(await page.locator('.scene').evaluate(el=>el.classList.contains('zoom')));
      assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
      await page.screenshot({path:path.resolve('art/chapter01/layers/qa/preview-'+width+'.png'),fullPage:true});
    }
    assert.deepEqual(errors,[]);
    console.log('736/360px: images decode, 0/3 props, event highlight, zoom, no overflow or script errors.');
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
