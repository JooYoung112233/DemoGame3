const http=require('node:http');
const fs=require('node:fs');
const file=process.argv[2];
if(!file)throw Error('Provide a preview fragment path.');
http.createServer((req,res)=>{
  res.setHeader('Content-Type','text/html; charset=utf-8');
  res.end('<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1"></head><body style="margin:0;background:#171c1a">'+fs.readFileSync(file,'utf8')+'</body></html>');
}).listen(Number(process.argv[3]||8772),'127.0.0.1',()=>console.log('Layer preview ready.'));
