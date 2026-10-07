local root=[[E:/personalProject/Demo3/demo6/아트/정수리/검사-v3]]
local f=io.open(root..'/manifest.json','r');local data=json.decode(f:read('*a'));f:close()
local results={}
for _,part in ipairs(data) do
 local s=Sprite(part.width,part.height,ColorMode.RGB)
 local psd={width=s.width,height=s.height,layers={}}
 for i,name in ipairs(part.layers) do
  local layer=i==1 and s.layers[1] or s:newLayer();layer.name=name
  local image=Image{fromFile=root..'/layers/'..part.id..'/'..name..'.png'}
  s:newCel(layer,1,image,Point(0,0))
  local file=root..'/layers/'..part.id..'/'..name..'.rgba';local out=io.open(file,'wb');out:write(image.bytes);out:close()
  table.insert(psd.layers,{name=name,visible=true,width=image.width,height=image.height,x=0,y=0,file=file})
 end
 s:saveAs(root..'/'..part.id..'-master-v003.aseprite')
 local merged=Image(s.spec);merged:drawSprite(s,1)
 local expected=Image{fromFile=root..'/PNG/'..part.id..'.png'};local different,maxDelta=0,0
 for it in expected:pixels() do local a=it();local b=merged:getPixel(it.x,it.y);if a~=b then different=different+1 end end
 local out=io.open(root..'/layers/'..part.id..'/composite.rgba','wb');out:write(merged.bytes);out:close()
 local j=io.open(root..'/layers/'..part.id..'/psd-layers.json','w');j:write(json.encode(psd));j:close()
 table.insert(results,{id=part.id,width=s.width,height=s.height,layers=#s.layers,compositeDifferentPixels=different})
 s:close()
end
local f=io.open(root..'/aseprite-validation.json','w');f:write(json.encode(results));f:close()
print('Packaged '..#results..' native layered player source files')
