const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..'),prefix=path.join(root,'design/minigames')+path.sep,bg=path.join(root,'art/chapter00-01/art-polish-v1/review/L1-enter.png'),port=Number(process.argv[2]||8797);
const mime={'.html':'text/html; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.png':'image/png'};
http.createServer((req,res)=>{if(!['GET','HEAD'].includes(req.method)){res.writeHead(405);return res.end()}let p;try{p=decodeURIComponent(new URL(req.url,'http://localhost').pathname)}catch{res.writeHead(400);return res.end()}if(p==='/')p='/design/minigames/fire-v1/index.html';const file=path.resolve(root,'.'+p);if(!file.startsWith(prefix)&&file!==bg){res.writeHead(403);return res.end()}fs.readFile(file,(err,data)=>{if(err){res.writeHead(404);return res.end('Not found')}res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');res.setHeader('Cache-Control','no-store');res.end(req.method==='HEAD'?undefined:data)})}).listen(port,'127.0.0.1',()=>console.log('Fire minigame: http://127.0.0.1:'+port));


