const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const root = path.resolve(__dirname, '../..');
const out = __dirname;
const walk = p => fs.readdirSync(p, {withFileTypes:true}).flatMap(e => {
  const q=path.join(p,e.name); if(e.isSymbolicLink())throw Error('Link: '+q);
  return e.isDirectory()?walk(q):[q];
});
const rel=p=>path.relative(root,p).replaceAll('\\','/');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const roots=['Assets','아트','AgentScripts','Packages','ProjectSettings','기획'];
const files=roots.flatMap(p=>walk(path.join(root,p))).concat(path.join(root,'README.md'));
const entries=files.map(p=>{const s=fs.statSync(p);return {path:rel(p),bytes:s.size,mtimeUtc:s.mtime.toISOString(),sha256:sha(p)};});
const candidateDirs=['아트/검사-남향-모션-v1/review-grid','아트/검사-남향-모션-v1/review-sequence','아트/검사-남향-모션-v2/review-grid','아트/검사-남향-모션-v2/review-sequence'];
const candidates=candidateDirs.map(p=>({path:p,reason:'README explicitly records user rejection; build.mjs generates these preview raster frames, package.py combines them into retained GIFs; browser preview reads retained sources instead.',files:entries.filter(e=>e.path.startsWith(p+'/'))}));
const textFiles=files.filter(p=>/\.(cs|json|js|mjs|py|lua|md|txt|html|asset|unity|prefab|meta|shader|mat|controller|anim)$/i.test(p));
const texts=textFiles.map(p=>[rel(p),fs.readFileSync(p,'utf8')]);
const guid='b118c02b77f3a3741895a0a0407d0d7d';
const oldSpriteReferences=texts.filter(([p,t])=>!p.endsWith('.meta')&&t.includes(guid)).map(([p])=>p);
const runtimeCandidateReferences=texts.filter(([p,t])=>p.startsWith('Assets/')&&/review-grid|review-sequence|검사-남향-모션-v[12]/.test(t)).map(([p])=>p);
const references=texts.filter(([p,t])=>/review-grid|review-sequence/.test(t)).map(([p,t])=>({path:p,lines:t.split('\n').flatMap((s,i)=>/review-grid|review-sequence/.test(s)?[{line:i+1,text:s.trim()}]:[])}));
const aseprite=files.filter(p=>/\.aseprite$/i.test(p)).map(p=>{const b=fs.readFileSync(p);return {path:rel(p),frames:b.readUInt16LE(6),width:b.readUInt16LE(8),height:b.readUInt16LE(10),bytes:b.length};});
const report={recordedUtc:new Date().toISOString(),root,entries,candidates,oldSpriteReferences,runtimeCandidateReferences,references,aseprite};
if(process.argv[2]==='after'){
 const before=JSON.parse(fs.readFileSync(path.join(out,'before.json'),'utf8'));
 const current=new Map(entries.map(e=>[e.path,e]));
 const expected=new Set(before.candidates.flatMap(c=>c.files.map(f=>f.path)));
 report.removed=before.entries.filter(e=>!current.has(e.path));
 report.unexpectedRemoved=report.removed.filter(e=>!expected.has(e.path));
 report.changed=before.entries.filter(e=>current.has(e.path)&&current.get(e.path).sha256!==e.sha256);
 report.unexpectedRemaining=[...expected].filter(p=>current.has(p));
 fs.writeFileSync(path.join(out,'after.json'),JSON.stringify(report,null,2));
 console.log(JSON.stringify({removed:report.removed.length,removedBytes:report.removed.reduce((n,e)=>n+e.bytes,0),unexpectedRemoved:report.unexpectedRemoved,changed:report.changed.map(e=>e.path),unexpectedRemainingCount:report.unexpectedRemaining.length,oldSpriteReferences,runtimeCandidateReferences}));
}else{
 const target=path.join(out,'before.json'); if(fs.existsSync(target))throw Error('Refusing to overwrite initial snapshot');
 fs.writeFileSync(target,JSON.stringify(report,null,2));
 console.log(JSON.stringify({files:entries.length,candidates:candidates.map(c=>({path:c.path,count:c.files.length,bytes:c.files.reduce((n,e)=>n+e.bytes,0)})),oldSpriteReferences,runtimeCandidateReferences,aseprite}));
}
