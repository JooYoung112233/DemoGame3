const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..'),port=Number(process.argv[2]||8792);
const dirs=['design/ui/day01-v1','art/chapter01/day01-v1','art/chapter00/book-insert-v1'].map(d=>path.resolve(root,d)+path.sep);
const exact=new Set(['design/chapter01/day01-flow-v1.json','docs/03-콘티/챕터1/CH01-DAY01-NOON-STORYBOARD.ko.md'].map(p=>path.resolve(root,p)));
const mime={'.html':'text/html; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.md':'text/plain; charset=utf-8','.png':'image/png','.jpg':'image/jpeg','.psd':'image/vnd.adobe.photoshop'};
http.createServer((req,res)=>{if(!['GET','HEAD'].includes(req.method)){res.writeHead(405);return res.end()}
let pathname;try{pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname)}catch{res.writeHead(400);return res.end()}
if(pathname==='/')pathname='/design/ui/day01-v1/index.html';const file=path.resolve(root,'.'+pathname);
if(!exact.has(file)&&!dirs.some(d=>file.startsWith(d))){res.writeHead(403);return res.end()}
fs.readFile(file,(err,data)=>{if(err){res.writeHead(404);return res.end('Not found')}res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');res.setHeader('Cache-Control','no-store');res.end(req.method==='HEAD'?undefined:data)})
}).listen(port,'127.0.0.1',()=>console.log('Day 1 storyboard: http://127.0.0.1:'+port));
