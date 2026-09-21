const {chromium}=require('C:/Users/admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('node:fs'),path=require('node:path');
(async()=>{const b=await chromium.launch({channel:'msedge',headless:true});try{
 const p=await b.newPage({viewport:{width:736,height:850}});await p.emulateMedia({reducedMotion:'reduce'});
 const out='art/chapter01/focus-review/qa';fs.mkdirSync(out,{recursive:true});
 for(const [id,file] of [['before','preview-output/chapter-one-before-focus.html'],['after','preview-output/chapter-one.html']]){
  await p.setContent('<!doctype html><html><body style="margin:0">'+fs.readFileSync(file,'utf8')+'</body></html>');
  await p.locator('button[data-state="cook"]').click();
  await p.locator('.world img').evaluateAll(imgs=>Promise.all(imgs.map(i=>i.decode())));
  if(id==='after')await p.waitForFunction(()=>document.getElementById('live49-chapter-one').dataset.rendered==='kitchen/cook');
  await p.locator('.viewport').screenshot({path:path.join(out,id+'.png')});
 }
}finally{await b.close()}})().catch(e=>{console.error(e);process.exitCode=1});
