local root=[[E:/personalProject/Demo3/demo6/아트/정수리/검사-v4]]
local sheet=Sprite(620,260,ColorMode.RGB)
local bg=Image(sheet.spec)
local dark=app.pixelColor.rgba(31,29,26,255)
for p in bg:pixels() do p(dark) end
sheet:newCel(sheet.layers[1],1,bg)
sheet.layers[1].name='01-preview-background'
local art=sheet:newLayer();art.name='02-size-comparison';local canvas=Image(sheet.spec)
for i,name in ipairs({'leather','chain','plate'}) do
 for row,version in ipairs({'v3','v4'}) do
  local base=root:gsub('검사%-v4','검사-'..version)
  local body=Image{fromFile=base..'/PNG/body_'..name..'.png'}
  local helm=Image{fromFile=base..'/PNG/helm_'..name..'.png'}
  body:drawImage(helm,Point((body.width-helm.width)//2,(body.height-helm.height)//2))
  local x=20+(i-1)*200;local y=12+(row-1)*120
  local large=body:clone();large:resize(84,84);canvas:drawImage(large,Point(x,y))
  local small=body:clone();small:resize(56,56);canvas:drawImage(small,Point(x+104,y+14))
 end
end
sheet:newCel(art,1,canvas)
sheet:saveAs(root..'/body-helm-size-comparison.png');sheet:close()
local ring=Image{fromFile=[[E:/personalProject/Demo3/demo6/검증/비주얼-개선-v4/material-previews/after-loot-dust-ring.png]]}
local s=Sprite(128,128,ColorMode.RGB);local out=Image(s.spec);for p in out:pixels() do p(dark) end;out:drawImage(ring,Point(0,0));s:newCel(s.layers[1],1,out);s:saveAs([[E:/personalProject/Demo3/demo6/검증/비주얼-개선-v4/material-previews/after-loot-dust-ring-on-dark.png]]);s:close()
local f=io.open(root..'/manifest.json','r');local rows=json.decode(f:read('*a'));f:close();local result={}
for _,part in ipairs(rows) do
 local im=Image{fromFile=root..'/PNG/'..part.id..'.png'};local contact=0
 for x=0,im.width-1 do for _,y in ipairs({0,im.height-1}) do if app.pixelColor.rgbaA(im:getPixel(x,y))>0 then contact=contact+1 end end end
 for y=1,im.height-2 do for _,x in ipairs({0,im.width-1}) do if app.pixelColor.rgbaA(im:getPixel(x,y))>0 then contact=contact+1 end end end
 table.insert(result,{id=part.id,borderAlphaContacts=contact})
end
local f=io.open(root..'/alpha-border-validation.json','w');f:write(json.encode(result));f:close()
