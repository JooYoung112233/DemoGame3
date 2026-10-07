const fs=require('fs');
const p='AgentScripts/v10_source.lua'; let s=fs.readFileSync(p,'utf8');
const seam=String.raw`
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
`;
s=s.replace('-- Native source pixels are preserved;',seam+'\n-- Native source pixels are preserved;');
const a=s.indexOf('local function wallout('),b=s.indexOf("wallout('wall_h29'",a);
s=s.slice(0,a)+String.raw`local function wallout(id,len,seed,vertical)
 local w,h=math.floor(len*128+.5),180;local canvas=sprite(w,h);local tileW=824;local overlap=192;local ox=0;local i=0
 while ox<w do
  local im=resample(wall,tileW,h,{110,100,1940,510},(i+seed)%2==0,false)
  if ox+tileW>w then im=Image(im,Rectangle(0,0,w-ox,h))end
  piece(canvas,im,ox,0,i>0 and math.min(overlap,im.width-1)or 0,0,'rock-run-'..i);ox=ox+tileW-overlap;i=i+1
 end
 local im=merged(canvas);canvas:close()
 if vertical then local v=Image(h,w,ColorMode.RGB);for y=0,h-1 do for x=0,w-1 do v:drawPixel(h-y-1,x,im:getPixel(x,y))end end;im=v end
 im:saveAs(root..'/runtime-PNG/'..id..'.png');table.insert(exports,{id=id,width=im.width,height=im.height,ppu=128,pivot={.5,.5}})
end
`+s.slice(b);
const f=s.indexOf('-- Four distinct native ground regions'),e=s.indexOf("local im=open('arch-original')",f);
s=s.slice(0,f)+String.raw`-- Nine reduced regions from three retained native source paintings. Smaller stones match the hero scale.
local floors={open('floor-clean'),open('floor-clean-1'),open('floor-clean-2')}
for i,im in ipairs(floors)do local n=split(im,{'01-west','02-middle','03-east'},function(x,y)return math.min(3,math.floor(x/im.width*3)+1)end);save(n,'ground-native-'..i,'Native generated painting retained at original resolution; three visible regional layers.');n:close()end
local fs=sprite(3136,1792)
local layout={{1,false,false},{2,true,false},{3,false,true},{3,true,false},{1,false,true},{2,false,false},{2,true,true},{3,false,false},{1,true,false}}
for i,v in ipairs(layout)do local ix=(i-1)%3;local iy=math.floor((i-1)/3);local src=floors[v[1]];local cropW=src.height*1200/696;local im=resample(src,1200,696,{(src.width-cropW)/2,0,cropW,src.height},v[2],v[3]);piece(fs,im,ix*968,iy*548,ix>0 and 232 or 0,iy>0 and 148 or 0,'0'..i..'-ground-region')end
local ground=save(fs,'entry-floor','3136x1792 assembled terrain, 112 PPU; nine reduced placements from three distinct full native paintings. One-pixel content-aware seams. Native source masters retained separately; no enlargement or blur.');ground:saveAs(root..'/runtime-PNG/entry_floor.png');table.insert(exports,{id='entry_floor',width=3136,height=1792,ppu=112,pivot={.5,.5}});fs:close()

`+s.slice(e);
s=s.replace("print('v9:","print('v10:");fs.writeFileSync(p,s);console.log('v10 source recipe refined');
