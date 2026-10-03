local root=[[E:/personalProject/Demo3/demo6/아트/UI-정리-v2]]
local ids={'panel-iron-leather','slot','wpn_longsword','wpn_greatsword','wpn_twinblades','potion','stone','gold','pickaxe','key','figure'}
local log=io.open(root..'/원본/source-validation.txt','w')
for _,id in ipairs(ids) do
 local original=Image{fromFile=root..'/원본/'..id..'-master.png'}
 local s=Sprite(original.width,original.height,ColorMode.RGB)
 local lower=s.layers[1]; lower.name='painted original'
 local high=s:newLayer(); high.name='retouch (empty)'
 local base=Image(original);local extra=Image(original.spec)
 if id=='panel-iron-leather' or id=='slot' then
  lower.name='leather lining';high.name='iron rim and corner plates'
  for it in base:pixels() do
   local x,y=it.x,it.y;local edge=math.min(x,y,1253-x,1253-y)
   local corner=(x<150 or x>1103) and (y<150 or y>1103)
   if edge<90 or (id=='panel-iron-leather' and corner) then extra:drawPixel(x,y,it());it(0) end
  end
 elseif id=='wpn_longsword' or id=='wpn_greatsword' or id=='wpn_twinblades' then
  lower.name='grip and guard area';high.name='blade area'
  local cutoff=id=='wpn_longsword' and 365 or id=='wpn_greatsword' and 460 or 555
  for it in base:pixels() do
   if (it.x-it.y+1254)/2>cutoff then extra:drawPixel(it.x,it.y,it());it(0) end
  end
 end
 s:newCel(lower,1,base,Point(0,0));s:newCel(high,1,extra,Point(0,0))
 s:saveAs(root..'/원본/'..id..'-master-v002.aseprite')
 base:saveAs(root..'/분리-PNG/'..id..'-base.png')
 extra:saveAs(root..'/분리-PNG/'..id..'-upper.png')
 local merged=Image(original.spec);merged:drawSprite(s,1)
 local mismatch=0
 for it in original:pixels() do if it()~=merged:getPixel(it.x,it.y) then mismatch=mismatch+1 end end
 log:write(id..' | '..original.width..'x'..original.height..' | layers: '..lower.name..' / '..high.name..' | composite pixel differences: '..mismatch..'\n')
 s:close()
end
log:close()
print('Packaged 11 Aseprite sources with lossless regional layer masks')
