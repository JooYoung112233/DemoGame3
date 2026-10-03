local root = [[E:/personalProject/Demo3/demo6/아트/정수리/굴쥐-v1]]
local s=Sprite(448,256,ColorMode.RGB)
local first=s.layers[1]
first.name='tail'
s:newCel(first,1,Image{fromFile=root..'/layers/td_rat_tail_v001.png'},Point(0,0))
for _,p in ipairs({'torso','head','dead'}) do
 local layer=s:newLayer();layer.name=p
 s:newCel(layer,1,Image{fromFile=root..'/layers/td_rat_'..p..'_v001.png'},Point(0,0))
 if p=='dead' then layer.isVisible=false end
end
local guide=s:newLayer();guide.name='GUIDE collider diameter 0.5 - pivot 224,128 - forward +X';guide.isVisible=false
local img=Image(448,256,ColorMode.RGB)
local color=app.pixelColor.rgba(0,200,255,180)
for x=92,356 do img:drawPixel(x,128,color) end
for y=0,255 do img:drawPixel(224,y,color) end
for t=0,359 do local rad=t*math.pi/180; local x=math.floor(224+128*math.cos(rad));local y=math.floor(128+128*math.sin(rad));if y>=0 and y<256 then img:drawPixel(x,y,color) end end
s:newCel(guide,1,img,Point(0,0))
s:saveAs(root..'/td_rat_master_v001.aseprite')
local ok,err=pcall(function()s:saveAs(root..'/td_rat_master_v001.psd')end)
local f=io.open(root..'/source-build-log.txt','w');f:write('Layers: '..#s.layers..'\nPSD save: '..tostring(ok)..' '..tostring(err));f:close()
s:close()
