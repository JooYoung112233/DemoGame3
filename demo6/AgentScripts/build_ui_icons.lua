local root=[[E:/personalProject/Demo3/demo6/아트/UI-정리-v1/아이콘-원본]]
local ids={"wpn_longsword","wpn_greatsword","wpn_twinblades","potion","stone","gold","pickaxe","key","figure"}
local s=Sprite(1024,1024,ColorMode.RGB)
local main=s.layers[1];main.name='material and silhouette'
local detail=s:newLayer();detail.name='edge light and fittings'
for i,id in ipairs(ids) do
 if i>1 then s:newEmptyFrame() end
 s:newCel(main,i,Image{fromFile=root..'/layers/'..id..'-main.png'},Point(0,0))
 s:newCel(detail,i,Image{fromFile=root..'/layers/'..id..'-detail.png'},Point(0,0))
 s:newTag(i,i).name=id
end
s:saveAs(root..'/ui-items-master-v001.aseprite')
local f=io.open(root..'/aseprite-validation.txt','w');f:write('Size: '..s.width..'x'..s.height..'\nLayers: '..#s.layers..'\nFrames (one per icon; not animation): '..#s.frames);f:close()
s:close()
