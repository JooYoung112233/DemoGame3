local project=[[E:/personalProject/Demo3/demo6]]
local root=project..'/아트/텍스처-구조-v8'
local pc=app.pixelColor
local function clamp(x,a,b)return math.max(a,math.min(b,x))end
local function rgba(r,g,b,a)return pc.rgba(clamp(math.floor(r+.5),0,255),clamp(math.floor(g+.5),0,255),clamp(math.floor(b+.5),0,255),clamp(math.floor(a+.5),0,255))end
local function smooth(a,b,x)local t=clamp((x-a)/(b-a),0,1);return t*t*(3-2*t)end
local function merged(s)local im=Image(s.spec);im:drawSprite(s,1);return im end
local function layer(s,name)local l=s:newLayer();l.name=name;return s:newCel(l,1,Image(s.spec),Point(0,0)).image end
local function distRect(x,y,cx,cy,hx,hy)local dx=math.max(math.abs(x-cx)-hx,0);local dy=math.max(math.abs(y-cy)-hy,0);return math.sqrt(dx*dx+dy*dy)end
local parts={}
for _,id in ipairs({'entry_floor','wall_tile'})do
 local source='아트/텍스처-시범-v7/'..id..'-master-v007.aseprite';local s=app.open(project..'/'..source);local before=merged(s)
 local face=layer(s,'05-large-value-planes');local top=layer(s,'06-top-and-side');local contact=layer(s,'07-ground-contact')
 for y=0,s.height-1 do for x=0,s.width-1 do
  local p=before:getPixel(x,y);local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p);local wx=(x+.5)/64;local wy=(s.height-y-.5)/64
  if id=='entry_floor' then
   -- Two broad, irregularly blended walkable planes; no new tiling or fine noise.
   local a=math.exp(-(((wx-7.2)/5.2)^4+((wy-4.65)/2.75)^4));local c=math.exp(-(((wx-18.4)/5.8)^4+((wy-10.2)/3.1)^4));local plane=math.max(a,c)
   if plane>.01 then face:drawPixel(x,y,rgba(r+18,g+18,b+15,plane*195))end
   -- A broad cool/warm shift in the existing ground, softly separated from the contact line.
   local dip=math.exp(-(((wx-11.8)/2.7)^4+((wy-7.2)/2.0)^4));if dip>.01 then top:drawPixel(x,y,rgba(r*.79,g*.81,b*.86,dip*110))end
   local distance=math.min(distRect(wx,wy,14,0,14.5,.5),distRect(wx,wy,14,16,14.5,.5),distRect(wx,wy,0,8,.5,8.5),distRect(wx,wy,28,2.75,.5,3.25),distRect(wx,wy,28,13.25,.5,3.25),distRect(wx,wy,4,3,.6,.6),distRect(wx,wy,24,13,.6,.6))
   local k=(1-smooth(.02,.58,distance));if k>0 then contact:drawPixel(x,y,rgba(14,14,13,k*128))end
  else
   local ly=(wy+.5)%1;local row=math.floor(wy+.5);local varied=.95+.065*math.sin(wx*1.13+row*1.73)
   -- Preserve existing mortar. The highlight is a broad upper face, never an emissive rim.
   local stone=smooth(37,90,(r+g+b)/3);if stone>.005 then
    local body=1-smooth(.42,.82,ly);face:drawPixel(x,y,rgba((r*.85+7)*varied,(g*.87+7)*varied,(b*.89+7)*varied,body*185*stone))
    local cap=smooth(.36,.87,ly)*(1-smooth(.975,1,ly));local broken=.83+.17*math.sin(wx*2.0+row*.8)^2
    if cap>0 then top:drawPixel(x,y,rgba((r*1.30+20)*varied,(g*1.30+21)*varied,(b*1.29+20)*varied,cap*broken*235*stone))end
    local toe=1-smooth(.07,.29,ly);if toe>0 then contact:drawPixel(x,y,rgba(r*.43,g*.45,b*.48,toe*208*stone))end
   end
  end
 end end
 local out=merged(s);local names={};local psd={width=s.width,height=s.height,layers={}}
 for _,l in ipairs(s.layers)do local cel=l:cel(1);if cel then local im=Image(s.spec);im:drawImage(cel.image,cel.position);im:saveAs(root..'/layers/'..id..'/'..l.name..'.png');local file=root..'/layers/'..id..'/'..l.name..'.rgba';local f=io.open(file,'wb');f:write(im.bytes);f:close();table.insert(names,l.name);table.insert(psd.layers,{name=l.name,visible=true,width=s.width,height=s.height,x=0,y=0,file=file})end end
 out:saveAs(root..'/PNG/'..id..'.png');s:saveAs(root..'/'..id..'-master-v008.aseprite');local f=io.open(root..'/layers/'..id..'/composite.rgba','wb');f:write(out.bytes);f:close();f=io.open(root..'/layers/'..id..'/psd-layers.json','w');f:write(json.encode(psd));f:close();table.insert(parts,{id=id,width=s.width,height=s.height,layers=names,source=source,pivot={.5,.5},masterPPU=64,runtimePPU=16,downsample=4,runtimePath='Assets/Resources/TextureStudyV7/'..id..'.png'});s:close()
end
local f=io.open(root..'/manifest.json','w');f:write(json.encode(parts));f:close();print('v8: 2 native environmental sources, 7 layers each, v7 retained')
