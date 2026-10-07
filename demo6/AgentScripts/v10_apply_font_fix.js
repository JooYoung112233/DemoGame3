const fs=require('fs'),crypto=require('crypto');
const root='E:/personalProject/Demo3/demo6',clone='E:/personalProject/Demo3/topdown-v10-review-isolated',ev=root+'/검증/정수리-질감수정-v10';
const file='Assets/Scripts/Game/Dungeon/UI/DungeonUi.cs';const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const report=JSON.parse(fs.readFileSync(ev+'/applied-files.json','utf8'));const entry=report.files.find(x=>x.path===file);const before=fs.readFileSync(root+'/'+file),after=fs.readFileSync(clone+'/'+file);
if(hash(before)!==entry.after)throw Error('Concurrent original UI change preserved');
const expected=before.toString().replace('            var label = _skin.label;','            var label = _skin.label;\n            if (_bodyFont) label.font = _bodyFont;');
if(expected.replace(/\r/g,'')!==after.toString().replace(/\r/g,''))throw Error('Unexpected follow-up difference');
fs.writeFileSync(ev+'/font-fix-applied.json',JSON.stringify({path:file,before:hash(before),after:hash(after),reason:'Bind the font explicitly so IMGUI styles keep the same font across GUI contexts.'},null,2));
fs.copyFileSync(clone+'/'+file,root+'/'+file);entry.after=hash(after);entry.bytes=after.length;report.fontFixTime=new Date().toISOString();fs.writeFileSync(ev+'/applied-files.json',JSON.stringify(report,null,2));console.log('One-line font binding applied; original concurrent files untouched.');
