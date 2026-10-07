-- A local material pass over the traced form study. Source photographs are not used.
local root=[[E:/personalProject/Demo3/demo6/아트/승인형상-대표샘플-v12]]
local pc=app.pixelColor
local function clamp(x,a,b)return math.max(a,math.min(b,x))end
local function pixel(r,g,b,a)return pc.rgba(clamp(math.floor(r+.5),0,255),clamp(math.floor(g+.5),0,255),clamp(math.floor(b+.5),0,255),a or 255)end
local function noise(x,y)return (math.sin(x*12.9898+y*78.233)*43758.5453)%1 end
local function read(p)local s=app.open(p);local im=Image(s.spec);im:drawSprite(s,1);s:close();return im end
local function merged(s)local im=Image(s.spec);im:drawSprite(s,1);return im end
local function add(s,n,im)local l=s:newLayer();l.name=n;s:newCel(l,1,im,Point(0,0));end
local function save(s,id)
 local dir=root..'/layers/'..id;local meta={width=s.width,height=s.height,layers={}}
 for _,l in ipairs(s.layers)do local im=Image(s.spec);im:drawImage(l:cel(1).image,l:cel(1).position);im:saveAs(dir..'/'..l.name..'.png');local name=dir..'/'..l.name..'.rgba';local f=io.open(name,'wb');f:write(im.bytes);f:close();meta.layers[#meta.layers+1]={name=l.name,x=0,y=0,width=s.width,height=s.height,file=name,visible=l.isVisible}end
 local im=merged(s);local f=io.open(dir..'/composite.rgba','wb');f:write(im.bytes);f:close();f=io.open(dir..'/psd-layers.json','w');f:write(json.encode(meta));f:close();im:saveAs(root..'/PNG/'..id..'.png');s:saveAs(root..'/'..id..'-study-v012.aseprite')
end
local texture=read([[E:/personalProject/Demo3/demo6/아트/정수리-질감수정-v10/generated/body-soft-native.png]])
local texfloor=read([[E:/personalProject/Demo3/demo6/아트/정수리-시안적용-v9/generated/floor-original.png]])
local s=app.open(root..'/hero-approved-shape-study-v012.aseprite')
-- Preserve a geometry-only version, then keep the final editable six-part topology.
s:saveCopyAs(root..'/hero-geometry-only.aseprite')
for _,l in ipairs(s.layers)do local im=l:cel(1).image
 for y=0,im.height-1 do for x=0,im.width-1 do local p=im:getPixel(x,y);if pc.rgbaA(p)==255 then
  local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
  local tx=570+(math.floor(x*.66+y*.08)%175);local ty=178+(math.floor(y*.48+x*.04)%122);local q=texture:getPixel(tx,ty)
  local material=(pc.rgbaR(q)+pc.rgbaG(q)+pc.rgbaB(q))/3-89
  -- Material marks are clipped inside each anatomical layer; no background filter.
  local facet=material*.24+(noise(math.floor(x/7),math.floor(y/9))-.5)*2.5
  if l.name=='01-asymmetric-worn-cape' then facet=facet*.32 end
  local edge=math.min(r,g,b)<22 and .08 or 1
  im:drawPixel(x,y,pixel(r+facet*edge,g+facet*edge,b+facet*edge))
 end end end
end
save(s,'hero-approved-shape');s:close()

s=app.open(root..'/connected-bedrock-study-v012.aseprite')
s:saveCopyAs(root..'/ground-geometry-only.aseprite')
local base=s.layers[1]:cel(1).image;local detail=Image(s.spec)
for y=0,s.height-1 do for x=0,s.width-1 do
 local q=texfloor:getPixel(math.floor(x/s.width*texfloor.width),math.floor(y/s.height*texfloor.height))
 local r,g,b=pc.rgbaR(q),pc.rgbaG(q),pc.rgbaB(q);local value=r*.299+g*.587+b*.114
 local p=base:getPixel(x,y)
 -- Transfer only restrained mineral brush marks, not the old cobblestone boundaries.
 local k=clamp((value-67)*.40,-5,10)
 local warm=clamp((r-b-7)*.16,-1,2)
 detail:drawPixel(x,y,pixel(pc.rgbaR(p)+k+warm,pc.rgbaG(p)+k,pc.rgbaB(p)+k-warm*.2))
end end
base:drawImage(detail)
-- Redraw true dark fissure cores once per output pixel. The initial overlapping
-- edge strokes are retained only in the geometry draft.
local lines={
 {{-20,230},{200,294},{404,265},{585,343},{712,323}},{{404,265},{434,421},{387,591},{459,729},{434,900},{502,1170}},
 {{712,-20},{669,163},{712,323},{682,475},{802,588}},{{712,323},{890,294},{1023,360},{1190,302},{1339,346},{1534,277},{1789,315},{2068,238}},
 {{1023,360},{994,511},{1071,662},{1017,817},{1074,985},{1020,1170}},{{1339,-20},{1296,151},{1339,346},{1390,473},{1341,651}},
 {{1789,315},{1745,486},{1832,668},{1785,880},{1879,1170}},{{-20,751},{198,683},{387,591},{611,649},{802,588},{1071,662},{1341,651},{1551,722},{1832,668},{2068,743}},
 {{198,683},{225,879},{175,1036},{209,1170}},{{802,588},{752,779},{825,955},{786,1170}},{{434,900},{622,966},{825,955},{1017,817}},
 {{1341,651},{1283,823},{1337,1033},{1311,1170}},{{1534,277},{1579,80},{1523,-20}},{{1551,722},{1576,943},{1488,1170}},{{1074,985},{1337,1033},{1576,943},{1785,880},{2068,948}}
}
local crack=Image(s.spec);local dust=Image(s.spec)
for k,path in ipairs(lines)do for i=1,#path-1 do local a,b=path[i],path[i+1];local dx,dy=b[1]-a[1],b[2]-a[2];local len=math.sqrt(dx*dx+dy*dy)
 for n=0,math.ceil(len*2)do local t=n/math.ceil(len*2);local off=math.sin(t*math.pi*7+k)*1.2*math.sin(t*math.pi);local cx,cy=a[1]+dx*t-dy/len*off,a[2]+dy*t+dx/len*off;local radius=1.25+.7*math.sin(t*13+k)^2
  for y=math.max(0,math.floor(cy-radius)),math.min(s.height-1,math.ceil(cy+radius))do for x=math.max(0,math.floor(cx-radius)),math.min(s.width-1,math.ceil(cx+radius))do if (x-cx)^2+(y-cy)^2<radius*radius then crack:drawPixel(x,y,pixel(25,26,23))end end end
  if (k+i)%4==0 then
   local rad=3+2*math.sin(t*8)^2
   for y=math.max(0,math.floor(cy-rad)),math.min(s.height-1,math.ceil(cy+rad))do for x=math.max(0,math.floor(cx-rad)),math.min(s.width-1,math.ceil(cx+rad))do if (x-cx)^2+(y-cy)^2<rad*rad then dust:drawPixel(x,y,pixel(69,61,48,110))end end end
  end
 end
end end
s.layers[2]:cel(1).image=crack
-- Dust sits between the bedrock and crack, leaving the fissure visible.
local dl=s:newLayer();dl.name='04-local-earth-in-fissures';s:newCel(dl,1,dust);dl.stackIndex=2
save(s,'connected-bedrock');s:close()
local f=io.open(root..'/manifest.json','r');local m=json.decode(f:read('*a'));f:close();for _,v in ipairs(m)do if v.id=='connected-bedrock'then v.layers=4 end end;f=io.open(root..'/manifest.json','w');f:write(json.encode(m));f:close()
print('Material pass and fissure drawing completed')
