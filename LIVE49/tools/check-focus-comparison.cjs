const fs=require('node:fs'),assert=require('node:assert/strict');
const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{const b=await chromium.launch({channel:'msedge',headless:true});try{
const p=await b.newPage(),errors=[];p.on('pageerror',e=>errors.push(e.message));
for(const width of [736,360]){
 await p.setViewportSize({width,height:700});await p.setContent(fs.readFileSync(process.argv[2],'utf8'));
 const img=p.locator('#live49-focus-review img');await img.evaluate(i=>i.decode());const after=await img.getAttribute('src');
 await p.getByRole('button',{name:'기존',exact:true}).click();await img.evaluate(i=>i.decode());assert.notEqual(await img.getAttribute('src'),after);
 await p.getByRole('button',{name:'초점·배치 보정',exact:true}).click();assert.equal(await img.getAttribute('src'),after);
 assert(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
}
assert.deepEqual(errors,[]);console.log('Comparison: 736/360px, both images decode, both controls change the image, no overflow/errors.');
}finally{await b.close()}})().catch(e=>{console.error(e);process.exitCode=1});
