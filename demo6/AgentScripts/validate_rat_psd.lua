local root=[[E:/personalProject/Demo3/demo6/아트/정수리/굴쥐-v1]]
local s=app.open(root..'/td_rat_master_v001.psd')
local data={width=s.width,height=s.height,layers={}}
for _,l in ipairs(s.layers) do table.insert(data.layers,{name=l.name,visible=l.isVisible}) end
local out=Image(s.spec);out:drawSprite(s,1)
local f=io.open(root..'/layers/psd-reopened.rgba','wb');f:write(out.bytes);f:close()
local j=io.open(root..'/psd-validation.json','w');j:write(json.encode(data));j:close()
s:close()
