local root='E:/personalProject/Demo3/demo6/검증/v049-final/'
local s=Sprite(960,720,ColorMode.RGB)
for i=0,9 do
 if i>0 then s:newEmptyFrame() end
 local im=Image{fromFile=root..string.format('guard-sequence-%02d.png',i)}
 s:newCel(s.layers[1],i+1,im,Point(0,0))
 s.frames[i+1].duration=0.02
end
s:saveAs(root..'final-guard-10frames.gif')
s:close()
