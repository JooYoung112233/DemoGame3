local root=[[E:/personalProject/Demo3/demo6/아트/화풍조화-교정-v11]]
local pc=app.pixelColor
local function clamp(n,a,b)return math.max(a,math.min(b,n))end
local function pix(r,g,b,a)return pc.rgba(clamp(math.floor(r+.5),0,255),clamp(math.floor(g+.5),0,255),clamp(math.floor(b+.5),0,255),clamp(math.floor(a+.5),0,255))end
local function open(id)local s=app.open(root..'/generated/'..id..'.png');local im=Image(s.spec);im:drawSprite(s,1);s:close();return im end
local function sprite(w,h)local s=Sprite(w,h,ColorMode.RGB);s:deleteLayer(s.layers[1]);return s end
local function add(s,name,im,at)local l=s:newLayer();l.name=name;s:newCel(l,1,im,at or Point(0,0));return l end
local function merged(s)local im=Image(s.spec);im:drawSprite(s,1);return im end
local function poly(x,y,p)local c=false;local j=#p;for i=1,#p do local a,b=p[i],p[j];if (a[2]>y)~=(b[2]>y) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1] then c=not c end;j=i end;return c end
local function split(im,names,which)local s=sprite(im.width,im.height);local layers={};for _,n in ipairs(names)do local l=s:newLayer();l.name=n;layers[#layers+1]=s:newCel(l,1,Image(s.spec),Point(0,0)).image end;for y=0,im.height-1 do for x=0,im.width-1 do local p=im:getPixel(x,y);if pc.rgbaA(p)>0 then local i=which(x,y,p);if i then layers[i]:drawPixel(x,y,p)end end end end;return s end
local manifests={};local exports={}
local function save(s,id,notes)local out=merged(s);local dir=root..'/layers/'..id;app.fs.makeAllDirectories(dir);local meta={width=s.width,height=s.height,layers={}};for _,l in ipairs(s.layers)do local c=l:cel(1);if c then local im=Image(s.spec);im:drawImage(c.image,c.position);local file=dir..'/'..l.name..'.rgba';local f=io.open(file,'wb');f:write(im.bytes);f:close();im:saveAs(dir..'/'..l.name..'.png');table.insert(meta.layers,{name=l.name,x=0,y=0,width=s.width,height=s.height,file=file,visible=l.isVisible})end end;local f=io.open(dir..'/composite.rgba','wb');f:write(out.bytes);f:close();f=io.open(dir..'/psd-layers.json','w');f:write(json.encode(meta));f:close();out:saveAs(root..'/PNG/'..id..'.png');s:saveAs(root..'/'..id..'-master-v011.aseprite');table.insert(manifests,{id=id,width=s.width,height=s.height,layers=#s.layers,notes=notes});return out end
local function sample(im,x,y)local ix,iy=math.floor(x),math.floor(y);local fx,fy=x-ix,y-iy;local r,g,b,a=0,0,0,0;for j=0,1 do for i=0,1 do if ix+i>=0 and ix+i<im.width and iy+j>=0 and iy+j<im.height then local p=im:getPixel(ix+i,iy+j);local w=(i==0 and 1-fx or fx)*(j==0 and 1-fy or fy);local wa=w*pc.rgbaA(p);a=a+wa;r=r+pc.rgbaR(p)*wa;g=g+pc.rgbaG(p)*wa;b=b+pc.rgbaB(p)*wa end end end;if a<.1 then return 0 end;return pix(r/a,g/a,b/a,a)end
local function output(id,im,w,h,map,ppu,pivot)local out=Image(w,h,ColorMode.RGB);for y=0,h-1 do for x=0,w-1 do local sx,sy=map(x+.5,y+.5);out:drawPixel(x,y,sample(im,sx,sy))end end;out:saveAs(root..'/runtime-PNG/'..id..'.png');table.insert(exports,{id=id,width=w,height=h,ppu=ppu,pivot=pivot or {.5,.5}});return out end
local function cropout(id,im,rect,w,h,ppu,pivot)return output(id,im,w,h,function(x,y)return rect[1]+x/w*rect[3]-.5,rect[2]+y/h*rect[4]-.5 end,ppu,pivot)end
app.fs.makeAllDirectories(root..'/PNG');app.fs.makeAllDirectories(root..'/runtime-PNG')

-- Content-aware seam: no rectangular crop boundaries, no blended double rock outlines.
local function seam(old,im,ox,oy,vertical,width)
 local height=vertical and im.height or im.width;local back={};local prev={}
 for row=0,height-1 do local cur={};back[row]={};for col=4,width-5 do local x=vertical and col or row;local y=vertical and row or col;local a=old:getPixel(ox+x,oy+y);local b=im:getPixel(x,y);local cost=math.abs(pc.rgbaR(a)-pc.rgbaR(b))+math.abs(pc.rgbaG(a)-pc.rgbaG(b))+math.abs(pc.rgbaB(a)-pc.rgbaB(b))+math.abs(pc.rgbaA(a)-pc.rgbaA(b))*2;local best=0;local at=col;if row>0 then best=1e12;for j=math.max(4,col-2),math.min(width-5,col+2)do local v=prev[j]+math.abs(j-col)*1.5;if v<best then best=v;at=j end end end;cur[col]=cost+best;back[row][col]=at end;prev=cur end
 local at=4;for col=5,width-5 do if prev[col]<prev[at]then at=col end end;local out={};for row=height-1,0,-1 do out[row]=at;at=back[row][at]end;return out
end
local function piece(s,im,ox,oy,overx,overy,name)
 local old=merged(s);local sv=overx>0 and seam(old,im,ox,oy,true,overx)or nil;local sh=overy>0 and seam(old,im,ox,oy,false,overy)or nil;local m=Image(im)
 for y=0,m.height-1 do for x=0,m.width-1 do local a=1;if sv then a=a*clamp((x-sv[y]+.5),0,1)end;if sh then a=a*clamp((y-sh[x]+.5),0,1)end;if a<1 then local p=m:getPixel(x,y);m:drawPixel(x,y,pix(pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p),a*pc.rgbaA(p)))end end end;add(s,name,m,Point(ox,oy))
end
local function resample(im,w,h,rect,flipx,flipy)
 local out=Image(w,h,ColorMode.RGB);for y=0,h-1 do for x=0,w-1 do local u=flipx and w-x-.5 or x+.5;local v=flipy and h-y-.5 or y+.5;out:drawPixel(x,y,sample(im,rect[1]+u/w*rect[3]-.5,rect[2]+v/h*rect[4]-.5))end end;return out
end


local source=[[E:/personalProject/Demo3/demo6/아트/정수리-질감수정-v10/generated/]]
local function native(n)local s=app.open(source..n..'.png');local im=merged(s);s:close();return im end
local function value(p)return pc.rgbaR(p)*.299+pc.rgbaG(p)*.587+pc.rgbaB(p)*.114 end
-- Brush-sized irregular fields, not a blur kernel. Each material uses its own paint recipe.
local function grain(x,y,scale)
 return math.sin(x/scale+.8*math.sin(y/(scale*.71)))*math.cos(y/(scale*.89)+.7*math.sin(x/(scale*1.21)))
end
local function material(im,kind)
 local paint=Image(im.width,im.height,ColorMode.RGB)
 for y=0,im.height-1 do for x=0,im.width-1 do
  local p=im:getPixel(x,y);local r,g,b,a=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p),pc.rgbaA(p)
  if a==255 then
   local l=value(p);local n=grain(x,y,kind=='body' and 28 or 19);local rr,gg,bb
   if kind=='body' and l>25 then
    if r>g*1.16 and g>b*1.16 then
     -- Bronze trim: uneven worn midtones replace the continuous polished bright rim.
     local t=math.min(151,l*.83+8)+n*3.2;rr=t*1.16;gg=t*.98;bb=t*.76
    elseif math.abs(r-g)<20 and math.abs(g-b)<22 then
     -- Steel: neutral graphite, retained major curved forms, broken broad satin reflection.
     local t=l*.88+5+n*3.8;rr=t*.985;gg=t;bb=t*.975
    end
   elseif kind=='cape' and r>g*1.7 and l>12 then
    -- Cloth: earthy red middle planes; lift only the deepest fold troughs slightly.
    rr=r*.86+7+n*2.7;gg=g*.91+7+n*1.2;bb=b*.89+6+n*.9
   elseif kind=='ground' then
    if r>g*1.18 and g>b*1.12 then
     -- Ochre soil is repainted as dusty umber. Stone faces keep their original values.
     local t=l*.94+2+n*1.8;rr=t*1.105;gg=t*.99;bb=t*.845
    elseif l<40 and r>g*1.10 then
     -- Recess dust interrupts the heavy dark seam, without removing the structural crack.
     rr=r+4;gg=g+3;bb=b+2
    end
   elseif kind=='rock' and l>28 then
    -- Keep cool rock mass, mute copper-coloured chips only, match floor mineral accents.
    if r>g*1.12 and g>b*1.1 then
     local t=l+n*1.3;rr=t*1.055;gg=t;bb=t*.915
    end
   end
   if rr then paint:drawPixel(x,y,pix(rr,gg,bb,255))end
  end
 end end
 local out=Image(im);out:drawImage(paint);return out,paint
end
local helm={{617,459},{716,446},{845,478},{925,538},{993,617},{984,646},{923,698},{886,754},{779,797},{676,789},{606,747},{569,678},{567,571}}
local function bodypart(x,y)if poly(x,y,helm)then return 4 elseif y<449 then return 2 elseif y>797 then return 3 else return 1 end end
local raw=native('body-soft-native');local body,bodypaint=material(raw,'body');local bs=split(raw,{'01-underarmor','02-shoulder-left','03-shoulder-right','04-helmet'},bodypart)
local ps=split(bodypaint,{'05-underarmor-paint','06-left-matte-paint','07-right-matte-paint','08-helmet-matte-paint'},bodypart)
for _,l in ipairs(ps.layers)do add(bs,l.name,l:cel(1).image)end;ps:close()
save(bs,'hero-body','Native 1254px, four anatomical parts plus four material paint layers. Alpha, crop, pivot and rig scale retained.');bs:close()
local parts=split(body,{'torso','helmet'},function(x,y)return bodypart(x,y)==4 and 2 or 1 end)
parts.layers[2].isVisible=false;local torso=merged(parts);parts.layers[1].isVisible=false;parts.layers[2].isVisible=true;local head=merged(parts)
local function bodymap(x,y)return 665+(x-160)*1080/256,625+(y-160)*1080/256 end
output('body_leather',torso,320,320,bodymap,256);output('helm_leather',head,320,320,bodymap,256);parts:close()
raw=native('cape-original');local cape,cp=material(raw,'cape');local cs=split(raw,{'01-cloth-upper','02-cloth-middle','03-cloth-lower','04-collar'},function(x,y)if x>1025 then return 4 elseif y<400 then return 1 elseif y>625 then return 3 else return 2 end end)
add(cs,'05-earth-red-fold-paint',cp);save(cs,'hero-cape','Native 1502x1047; cloth material paint is separate. Exact previous alpha and runtime mapping.');cs:close()
cropout('short_cape',cape,{280,104,960,810},320,224,256,{.88,.5})

-- Four native placements at 1:1 produce smaller stones at the same world size.
-- No pixel enlargement, no blur, no different collider/camera/sprite footprint.
local grounds={native('floor-whole'),native('floor-whole-1'),native('floor-whole-2')}
for v=0,2 do
 local s=sprite(3000,1714)
 local layout={{1+v%3,false,false},{1+(v+1)%3,true,false},{1+(v+2)%3,false,true},{1+v%3,true,true}}
 for i,t in ipairs(layout)do
  local src=grounds[t[1]];local im=resample(src,1672,941,{0,0,1672,941},t[2],t[3]);local ox=(i-1)%2*1328;local oy=math.floor((i-1)/2)*773
  piece(s,im,ox,oy,ox>0 and 344 or 0,oy>0 and 168 or 0,'0'..i..'-native-stone-region')
 end
 local original=merged(s);local ground,paint=material(original,'ground');add(s,'05-soil-recess-paint',paint)
 local id=v==0 and 'entry-floor' or 'floor-variant-'..v
 save(s,id,'3000x1714 composition of four native paintings; stone size 55 percent of v10, painted soil and preserved stone faces. No enlarged detail.');s:close()
 cropout(v==0 and 'entry_floor' or 'floor_variant_'..v,ground,{0,0,3000,1714},1645,940,58.75)
 print('floor '..v..' done')
end

raw=native('rock-field');local field,fp=material(raw,'rock');local fs=split(raw,{'01-west','02-east'},function(x,y)return x<raw.width/2 and 1 or 2 end)
add(fs,'03-mineral-paint',fp);save(fs,'rock-field','Native 1254x1254; selective copper-chip repaint only. World sampling/opacity unchanged.');fs:close();field:saveAs(root..'/runtime-PNG/rock_field.png')
raw=native('wall-soft-native');local wall,wp=material(raw,'rock');local ws=split(raw,{'01-west','02-midwest','03-mideast','04-east'},function(x,y)return math.min(4,math.floor(x/raw.width*4)+1)end)
add(ws,'05-mineral-paint',wp);save(ws,'mine-rocks','2172x724 original silhouette and local mineral paint. Previous strip recipe retained.');ws:close()
local function wallout(id,len,seed,vertical)
 local w,h=math.floor(len*128+.5),180;local canvas=sprite(w,h);local tileW=824;local overlap=192;local ox=0;local i=0
 while ox<w do
  local im=resample(wall,tileW,h,{110,100,1940,510},(i+seed)%2==0,false)
  if ox+tileW>w then im=Image(im,Rectangle(0,0,w-ox,h))end
  piece(canvas,im,ox,0,i>0 and math.min(overlap,im.width-1)or 0,0,'rock-run-'..i);ox=ox+tileW-overlap;i=i+1
 end
 local im=merged(canvas);canvas:close()
 if vertical then local z=Image(h,w,ColorMode.RGB);for y=0,h-1 do for x=0,w-1 do z:drawPixel(h-y-1,x,im:getPixel(x,y))end end;im=z end
 im:saveAs(root..'/runtime-PNG/'..id..'.png');table.insert(exports,{id=id,width=im.width,height=im.height,ppu=128,pivot={.5,.5}})
end
wallout('wall_h29',29,1,false);wallout('wall_v17',17,2,true);wallout('wall_v6a',6.5,3,true);wallout('wall_v6b',6.5,4,true)
local f=io.open(root..'/manifest.json','w');f:write(json.encode(manifests));f:close();f=io.open(root..'/runtime-manifest.json','w');f:write(json.encode(exports));f:close()
print('V11 source done: '..#manifests..' layered masters')
