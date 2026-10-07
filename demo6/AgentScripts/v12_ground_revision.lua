local root=[[E:/personalProject/Demo3/demo6/아트/승인형상-대표샘플-v12]]
local source=[[E:/personalProject/Demo3/demo6/아트/정수리-시안적용-v9/generated/floor-original.png]]
local pc=app.pixelColor
local function clamp(v,a,b)return math.max(a,math.min(b,v))end
local function px(r,g,b,a)return pc.rgba(clamp(math.floor(r+.5),0,255),clamp(math.floor(g+.5),0,255),clamp(math.floor(b+.5),0,255),a or 255)end
local old=app.open(root..'/connected-bedrock-study-v012.aseprite');old:saveCopyAs(root..'/ground-line-layout-rejected.aseprite');old:close()
local s=app.open(source);s.layers[1].name='01-native-connected-rock';local raw=Image(s.spec);raw:drawSprite(s,1)
local soil=Image(s.spec);local flecks=Image(s.spec)
for y=0,s.height-1 do for x=0,s.width-1 do local p=raw:getPixel(x,y);local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p);local l=r*.299+g*.587+b*.114
 if r>g*1.08 and g>b*1.12 then
  local t=l*.96;soil:drawPixel(x,y,px(t*1.067,t*.997,t*.865))
 elseif l>52 and math.abs(r-g)<11 and math.abs(g-b)<13 then
  -- Repaint only neutral stone-face flecks into a restrained mineral value range.
  -- Deep cracks, earth, shape boundaries and the source resolution are untouched.
  local k=l-(62+(l-62)*.30);flecks:drawPixel(x,y,px(r-k,g-k,b-k))
 end
end end
local sl=s:newLayer();sl.name='02-local-earth-paint';s:newCel(sl,1,soil);local fl=s:newLayer();fl.name='03-mineral-fleck-paint';s:newCel(fl,1,flecks)
local dir=root..'/layers/connected-bedrock';local meta={width=s.width,height=s.height,layers={}}
for _,l in ipairs(s.layers)do local im=Image(s.spec);im:drawImage(l:cel(1).image,l:cel(1).position);im:saveAs(dir..'/'..l.name..'.png');local file=dir..'/'..l.name..'.rgba';local f=io.open(file,'wb');f:write(im.bytes);f:close();meta.layers[#meta.layers+1]={name=l.name,x=0,y=0,width=s.width,height=s.height,file=file,visible=true}end
local out=Image(s.spec);out:drawSprite(s,1);local f=io.open(dir..'/composite.rgba','wb');f:write(out.bytes);f:close();f=io.open(dir..'/psd-layers.json','w');f:write(json.encode(meta));f:close();out:saveAs(root..'/PNG/connected-bedrock.png');s:saveAs(root..'/connected-bedrock-study-v012.aseprite')
f=io.open(root..'/manifest.json','r');local m=json.decode(f:read('*a'));f:close();for _,v in ipairs(m)do if v.id=='connected-bedrock'then v.width=s.width;v.height=s.height;v.layers=3;v.notes='Reuses the existing v9 native floor-original painting at native resolution, with two local material correction layers. The new straight-line layout draft was rejected. No magnified reference crop and no v11 cobblestone draft used.' end end;f=io.open(root..'/manifest.json','w');f:write(json.encode(m));f:close();s:close()
print('Native connected rock painting selected; artificial straight-line draft retained as rejected')
