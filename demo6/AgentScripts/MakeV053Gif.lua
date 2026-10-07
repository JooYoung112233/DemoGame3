local root='E:/personalProject/Demo3/demo6/검증/v053-shield/'
local s=Sprite(1280,720,ColorMode.RGB)
for i=0,9 do
 if i>0 then s:newEmptyFrame() end
 local im=Image{fromFile=root..string.format('guard-continuous-%02d.png',i)}
 s:newCel(s.layers[1],i+1,im,Point(0,0))
 -- GIF quantizes delays to centiseconds; ten consecutive 60 Hz sampled poses remain in order.
 s.frames[i+1].duration=(i%3==0) and 0.01 or 0.02
end
s:saveAs(root..'guard-10frames.gif')
s:close()
