local root=[[E:/personalProject/Demo3/demo6/아트/정수리-질감수정-v10]]
local f=io.open(root..'/manifest.json','r');local parts=json.decode(f:read('*a'));f:close();local rows={}
for _,p in ipairs(parts)do local s=app.open(root..'/'..p.id..'-master-v010.aseprite');local im=Image(s.spec);im:drawSprite(s,1);local png=app.open(root..'/PNG/'..p.id..'.png');local expected=Image(png.spec);expected:drawSprite(png,1);local ok=im.bytes==expected.bytes and #s.layers==p.layers;assert(ok,p.id..' Aseprite/PNG mismatch');table.insert(rows,{id=p.id,asepriteLayers=#s.layers,asepriteCompositeExact=ok});png:close();s:close()end
f=io.open(root..'/source-reopen-validation.json','w');f:write(json.encode(rows));f:close();print(json.encode(rows))
