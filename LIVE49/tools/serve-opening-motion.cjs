const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..'),port=Number(process.argv[2]||8791);
const exact=new Set(['/art/chapter00/camper/review/01-current-conversation.png','/art/chapter01/revision-v3/camper-clean-base-v3.png','/design/unity-handoff/C0-opening-direction-v1.json','/art/chapter00/review/01-memory-family-v1.png','/art/chapter00/review/02-memory-hands-v1.png','/design/chapter01/day01-flow-v1.json','/docs/CH01-DAY01-NOON-STORYBOARD.ko.md','/docs/CH01-FIRST-WEEK-STORY.ko.md']);
const dirs=['design/ui/ch00-01','design/ui/day01-v1','design/chapter00/continuation-v1','art/title','art/chapter00/memory/dialogue-v1','art/chapter00/camper/review'].map(d=>path.resolve(root,d)+path.sep);
dirs.push(path.resolve(root,'art/chapter00/photo-care-v1')+path.sep);
dirs.push(path.resolve(root,'art/chapter00-01/narrative-art-v1')+path.sep);
const mime={'.md':'text/plain; charset=utf-8','.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.png':'image/png','.jpg':'image/jpeg','.svg':'image/svg+xml'};
http.createServer((req,res)=>{
 if(req.method!=='GET'&&req.method!=='HEAD'){res.writeHead(405);return res.end();}
 let pathname;try{pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname);}catch{res.writeHead(400);return res.end();}
 if(pathname==='/')pathname='/design/ui/ch00-01/opening-motion.html';
 const file=path.resolve(root,'.'+pathname);
 if(!(exact.has(pathname)||dirs.some(d=>file.startsWith(d)))){res.writeHead(403);return res.end();}
 fs.readFile(file,(error,data)=>{if(error){res.writeHead(404);return res.end('Not found');}res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');res.setHeader('Cache-Control','no-store');if(req.method==='HEAD')return res.end();res.end(data);});
}).listen(port,'127.0.0.1',()=>console.log('Opening motion preview: http://127.0.0.1:'+port));
