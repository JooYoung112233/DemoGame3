const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const path=require('node:path');
const assert=require('node:assert/strict');
(async()=>{
  const browser=await chromium.launch({channel:'msedge',headless:true});
  try{
    const page=await browser.newPage();const errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    for(const width of [736,360]){
      await page.setViewportSize({width,height:800});
      await page.goto('http://127.0.0.1:8773');
      await page.locator('.world img').evaluateAll(imgs=>Promise.all(imgs.map(img=>img.decode())));
      const stable=()=>page.locator('.base,img[data-slot="sketchbook"],img[data-slot="mug"],img[data-slot="pencil-cup"]').evaluateAll(imgs=>imgs.map(img=>({src:img.src,style:img.getAttribute('style')})));
      const initial=await stable();
      await page.getByRole('button',{name:'인물 숨기기',exact:true}).click();
      assert.equal(await page.locator('img[data-pose]:visible').count(),0);
      await page.getByRole('button',{name:'함께 앉기',exact:true}).click();
      assert.equal(await page.locator('img[data-pose="suhyeok-seated"]:visible').count(),2);
      assert.equal(await page.locator('img[data-pose="soi-seated"]:visible').count(),2);
      assert.equal(await page.locator('img[data-pose="soi-drawing"]:visible').count(),0);
      await page.getByRole('button',{name:'식탁 확대',exact:true}).click();
      await page.screenshot({path:path.resolve(`art/chapter01/layers/qa/seated-preview-${width}.png`),fullPage:true});
      await page.getByRole('button',{name:'그리기 자세',exact:true}).click();
      assert.equal(await page.locator('img[data-pose="soi-seated"]:visible').count(),0);
      assert.equal(await page.locator('img[data-pose="soi-drawing"]:visible').count(),2);
      assert.equal(await page.locator('img[data-pose="suhyeok-seated"]:visible').count(),2);
      assert.deepEqual(await stable(),initial,'Base and prop pixels/placements must not change with the pose.');
      assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
      const order=await page.locator('img[data-slot="soi-drawing-body"],img[data-slot="table-foreground"],img[data-slot="sketchbook"],img[data-slot="soi-drawing-hands"]').evaluateAll(imgs=>Object.fromEntries(imgs.map(img=>[img.dataset.slot,Number(img.style.zIndex)])));
      assert(order['soi-drawing-body']<order['table-foreground']);
      assert(order['table-foreground']<order.sketchbook);
      assert(order.sketchbook<order['soi-drawing-hands']);
      await page.screenshot({path:path.resolve(`art/chapter01/layers/qa/drawing-preview-${width}.png`),fullPage:true});
    }
    assert.deepEqual(errors,[]);
    console.log('736/360px: poses exclusive, father stable, base/props unchanged, layer order and zoom verified, no overflow/script errors.');
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
