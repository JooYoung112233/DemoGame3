local root=[[E:/personalProject/Demo3/demo6/아트/정수리-질감수정-v10]]
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
local function save(s,id,notes)local out=merged(s);local dir=root..'/layers/'..id;app.fs.makeAllDirectories(dir);local meta={width=s.width,height=s.height,layers={}};for _,l in ipairs(s.layers)do local c=l:cel(1);if c then local im=Image(s.spec);im:drawImage(c.image,c.position);local file=dir..'/'..l.name..'.rgba';local f=io.open(file,'wb');f:write(im.bytes);f:close();im:saveAs(dir..'/'..l.name..'.png');table.insert(meta.layers,{name=l.name,x=0,y=0,width=s.width,height=s.height,file=file,visible=l.isVisible})end end;local f=io.open(dir..'/composite.rgba','wb');f:write(out.bytes);f:close();f=io.open(dir..'/psd-layers.json','w');f:write(json.encode(meta));f:close();out:saveAs(root..'/PNG/'..id..'.png');s:saveAs(root..'/'..id..'-master-v010.aseprite');table.insert(manifests,{id=id,width=s.width,height=s.height,layers=#s.layers,notes=notes});return out end
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


local im=open('ui-atlas');local s=sprite(im.width,im.height);local names={'attack','whirl','wave','dodge','potion','bag','map','skills','frame'};app.fs.makeAllDirectories(root..'/runtime-UI')
for i,name in ipairs(names)do local x=math.floor((i-1)%3*im.width/3);local y=math.floor(math.floor((i-1)/3)*im.height/3);local w=math.floor(im.width/3);local h=math.floor(im.height/3);local part=Image(im,Rectangle(x,y,w,h));add(s,string.format('%02d-',i)..name,part,Point(x,y));part:saveAs(root..'/runtime-UI/'..name..'.png')end
save(s,'ui-icons','Nine native 418px square UI tiles; 1254px native atlas preserved on nine separately editable layers. Runtime display 46-60 UI units, no enlargement.');s:close();local f=io.open(root..'/manifest.json','r');local all=json.decode(f:read('*a'));f:close();local fresh={};for _,v in ipairs(all)do if v.id~='ui-icons' then table.insert(fresh,v)end end;table.insert(fresh,manifests[1]);f=io.open(root..'/manifest.json','w');f:write(json.encode(fresh));f:close();print('Nine UI art tiles exported at native resolution')
