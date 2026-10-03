local root=[[E:/personalProject/Demo3/demo6/아트/정수리/굴쥐-v1]]
local s=app.open(root..'/td_rat_master_v001.aseprite')
local data={width=s.width,height=s.height,layers={}}
for i,l in ipairs(s.layers) do
 local cel=l:cel(1);local file=root..'/layers/psd-'..i..'.rgba'
 local f=io.open(file,'wb');f:write(cel.image.bytes);f:close()
 table.insert(data.layers,{name=l.name,visible=l.isVisible,width=cel.image.width,height=cel.image.height,x=cel.position.x,y=cel.position.y,file=file})
end
local out=Image(s.spec);out:drawSprite(s,1)
local f=io.open(root..'/layers/psd-composite.rgba','wb');f:write(out.bytes);f:close()
local j=io.open(root..'/layers/psd-layers.json','w');j:write(json.encode(data));j:close()
s:close()
