local project=[[E:/personalProject/Demo3/demo6]]
local root=project..'/아트/텍스처-시범-v7'
local pc=app.pixelColor
local manifest={}
local function clamp(x,a,b)return math.max(a,math.min(b,x))end
local function rgba(r,g,b,a)return pc.rgba(clamp(math.floor(r+.5),0,255),clamp(math.floor(g+.5),0,255),clamp(math.floor(b+.5),0,255),clamp(math.floor(a+.5),0,255))end
local function gauss(x,w)return math.exp(-(x/w)^2)end
local function seg(x,y,ax,ay,bx,by)local dx,dy=bx-ax,by-ay;local t=clamp(((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy),0,1);return math.sqrt((x-ax-t*dx)^2+(y-ay-t*dy)^2)end
local function composite(s)local im=Image(s.spec);im:drawSprite(s,1);return im end
local function layerImage(s,name)local l=s:newLayer();l.name=name;local im=Image(s.spec);local cel=s:newCel(l,1,im,Point(0,0));return cel.image,l end
local function export(s,id,pivot,ppu,down,source)
 local merged=composite(s);local names={};local psd={width=s.width,height=s.height,layers={}}
 for _,l in ipairs(s.layers)do local cel=l:cel(1);if cel then local im=Image(s.spec);im:drawImage(cel.image,cel.position);im:saveAs(root..'/layers/'..id..'/'..l.name..'.png');local file=root..'/layers/'..id..'/'..l.name..'.rgba';local f=io.open(file,'wb');f:write(im.bytes);f:close();table.insert(names,l.name);table.insert(psd.layers,{name=l.name,visible=true,width=s.width,height=s.height,x=0,y=0,file=file})end end
 merged:saveAs(root..'/PNG/'..id..'.png');s:saveAs(root..'/'..id..'-master-v007.aseprite');local f=io.open(root..'/layers/'..id..'/composite.rgba','wb');f:write(merged.bytes);f:close();f=io.open(root..'/layers/'..id..'/psd-layers.json','w');f:write(json.encode(psd));f:close();table.insert(manifest,{id=id,width=s.width,height=s.height,layers=names,pivot=pivot,masterPPU=ppu*down,runtimePPU=ppu,downsample=down,source=source});s:close()
end
local function hero(id,source,pivot)
 local s=app.open(project..'/'..source);local before=composite(s);local masks={};for _,l in ipairs(s.layers)do if l:cel(1)then local im=Image(s.spec);im:drawImage(l:cel(1).image,l:cel(1).position);masks[l.name]=im end end
 local im,l=layerImage(s,'07-material-planes')
 for y=0,s.height-1 do for x=0,s.width-1 do local base=before:getPixel(x,y);if pc.rgbaA(base)==255 then local wx=(x+.5-s.width*pivot[1])/512;local wy=(s.height*pivot[2]-y-.5)/512;local r,g,b=pc.rgbaR(base),pc.rgbaG(base),pc.rgbaB(base);local k,alpha,blue=1,0,1
  if id=='body_leather' then
   local plate=masks['04-angular-shoulder-plates']:getPixel(x,y);local head=masks['06-head']:getPixel(x,y);local strap=masks['05-straps-and-collar']:getPixel(x,y)
   if pc.rgbaA(plate)>240 and pc.rgbaA(head)<10 and pc.rgbaA(strap)<10 then
    if wy>0 then local fold=wy-(.30+.17*wx);k=.94+.11*wx/.16-.22*gauss(fold,.026)+.13*gauss(fold-.045,.045);alpha=220
    else k=.87+.26*clamp((wy+.39)/.15,0,1)+.14*gauss(wx+.07,.024);blue=1.055;alpha=205 end
   elseif wx<-.20 and pc.rgbaA(masks['02-tapered-cloak']:getPixel(x,y))>240 and pc.rgbaA(head)<10 and pc.rgbaA(strap)<10 then k=.96-.20*gauss(wy-.35*(wx+.14),.036)+.12*gauss(wy-.35*(wx+.14)-.058,.04);alpha=165 end
  elseif id=='sleeve_leather' then k=.90+.17*gauss(wy-.016,.025)-.19*gauss(wx+.055,.018)+.12*gauss(wx+.028,.017);alpha=190
  elseif id=='short_cape' then local spread=.13-.13*wx;local q=wy/spread;k=.95+.16*gauss(q+.52,.19)-.24*gauss(q+.28,.17)+.12*gauss(q-.03,.23)-.18*gauss(q-.32,.18)+.13*gauss(q-.63,.20);alpha=190
  elseif id=='longsword' then
   if wx>.115 and wx<.965 and math.abs(wy)<.032 then k=wy>0 and (.88+.19*gauss(wx-.36,.21)) or (.80+.18*gauss(wx-.72,.21));blue=1.065;alpha=220
   elseif wx>.15 and wy>.029 and wy<.044 then k=.76+.23*gauss(wx-.26,.12)+.19*gauss(wx-.74,.09);blue=1.035;alpha=235 end
  end
  if alpha>0 then im:drawPixel(x,y,rgba(r*k,g*k,b*k*blue,alpha))end
 end end end
 -- Three broad scuffed areas on the existing cape hem; no silhouette or alpha change.
 if id=='short_cape' then local hem=masks['03-worn-hem'];local wear=layerImage(s,'08-muted-hem-wear');for y=0,s.height-1 do for x=0,s.width-1 do local p=hem:getPixel(x,y);if pc.rgbaA(p)>180 and pc.rgbaA(before:getPixel(x,y))==255 then local k=.60+.26*gauss(x-s.width*.24,s.width*.065)+.20*gauss(y-s.height*.73,s.height*.08);wear:drawPixel(x,y,rgba(pc.rgbaR(p)*k,pc.rgbaG(p)*k,pc.rgbaB(p)*k,190))end end end end
 local after=composite(s);local different=0;for it in before:pixels()do if pc.rgbaA(it())~=pc.rgbaA(after:getPixel(it.x,it.y))then different=different+1 end end
 if different>0 then error('Silhouette alpha changed: '..id..' '..different)end
 export(s,id,pivot,256,2,source)
end
hero('body_leather','아트/정수리/검사-v4/body_leather-master-v004.aseprite',{.5,.5})
hero('sleeve_leather','아트/정수리/검사-v4/sleeve_leather-master-v004.aseprite',{.5,.5})
hero('short_cape','아트/정수리/검사-v5-망토와무기/short_cape-master-v005.aseprite',{.88,.5})
hero('longsword','아트/정수리/검사-v4/longsword-master-v004.aseprite',{84/608,.5})
local function polygon(x,y,p)local inside=false;local j=#p;for i=1,#p do local a,b=p[i],p[j];if (a[2]>y)~=(b[2]>y) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1]then inside=not inside end;j=i end;return inside end
local function environment(id,w,h)
 local s=Sprite(w,h,ColorMode.RGB);s.layers[1].name='01-preserved-base';local base=Image{fromFile=project..'/검증/텍스처-시범-v7/baseline/'..id..'.png'};base:resize(w,h);s:newCel(s.layers[1],1,base,Point(0,0))
 local planes=layerImage(s,'02-broad-stone-planes');local cuts=layerImage(s,'03-few-cracks');local contact=layerImage(s,'04-contact-and-edges')
 local patches={{{3.7,2.35},{4.1,2.06},{5.5,2.16},{6.15,2.43},{6.5,2.8},{6.28,3.25},{5.53,3.58},{4.4,3.48},{3.92,3.12}},{{6.9,4.3},{7.43,3.97},{8.6,4.08},{9.8,4.54},{10.03,5.06},{9.72,5.57},{8.88,5.75},{7.62,5.36},{7.33,4.87}},{{8.67,1.45},{9.21,1.2},{10.37,1.39},{11.47,1.82},{11.63,2.28},{11.11,2.75},{9.82,2.66},{9.13,2.29}},{{15.76,7.71},{16.58,7.18},{17.77,7.36},{18.27,7.57},{19.27,7.87},{19.55,8.44},{18.89,9.22},{17.36,9.26},{16.15,8.76}},{{21.31,11.07},{22.04,10.65},{23.51,10.98},{24.41,11.44},{24.62,12.1},{23.86,12.97},{22.83,12.88},{21.86,12.28}}}
 local lines=id=='entry_floor' and {{4.68,2.42,5.12,2.67},{5.12,2.67,5.44,2.70},{5.44,2.70,5.38,2.99},{5.38,2.99,5.55,3.30},{5.12,2.67,5.08,3.05},{8.03,4.53,8.47,4.75},{8.47,4.75,8.83,4.71},{8.83,4.71,9.04,5.00},{9.04,5.00,8.96,5.48},{8.83,4.71,9.24,4.65},{16.78,7.70,17.31,8.07},{17.31,8.07,17.80,8.13},{17.80,8.13,18.11,8.49},{18.11,8.49,18.04,8.89},{17.80,8.13,17.71,8.55}} or {{6.1,.18,6.4,.42},{6.4,.42,6.25,.76},{3.1,.28,3.38,.54},{3.38,.54,3.6,.7},{1.2,3.3,1.4,3.7},{4.8,5.2,5.15,5.65}}
 for y=0,h-1 do for x=0,w-1 do local wx=(x+.5)/64;local wy=(h-y-.5)/64;local p=base:getPixel(x,y);local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
  if id=='entry_floor' then for i,poly in ipairs(patches)do if polygon(wx,wy,poly)then local edge=10;for j=1,#poly do local a,b=poly[j],poly[j%#poly+1];edge=math.min(edge,seg(wx,wy,a[1],a[2],b[1],b[2]))end;local buried=clamp(edge/.25,0,1);local k=clamp(.90+(wy-poly[1][2])*.12,.86,1.15);local a=buried*(68+12*math.sin(wx*1.4+wy*1.2));planes:drawPixel(x,y,rgba(73*k,72*k,64*k,a));break end end
   local edge=math.min(wx,28-wx,wy,16-wy);if edge>.5 and edge<1.05 then contact:drawPixel(x,y,rgba(20,19,17,(1.05-edge)*34))end
  elseif math.max(r,g,b)>58 then local ly=wy%1;local k=.90+.20*ly;planes:drawPixel(x,y,rgba(r*k,g*k,b*k*1.02,170));if ly<.17 then contact:drawPixel(x,y,rgba(22,22,21,(.17-ly)*700))elseif ly>.87 then contact:drawPixel(x,y,rgba(131,133,126,(ly-.87)*300))end end
  local dist=100;for _,line in ipairs(lines)do dist=math.min(dist,seg(wx,wy,line[1],line[2],line[3],line[4]))end
  local width=id=='entry_floor' and (.032+.009*math.sin(wx*6.7+wy*4.1)) or (.043+.012*math.sin(wx*6.7+wy*4.1));if dist<width then cuts:drawPixel(x,y,rgba(24,23,21,clamp((width-dist)/.018,0,1)*(id=='entry_floor' and 126 or 165)))elseif dist<width+.022 then cuts:drawPixel(x,y,rgba(117,116,104,(width+.022-dist)/.022*(id=='entry_floor' and 35 or 46)))end
 end end
 export(s,id,{.5,.5},16,4,'baseline procedural texture + new native material layers')
end
environment('entry_floor',1792,1024)
environment('wall_tile',512,512)
local f=io.open(root..'/manifest.json','w');f:write(json.encode(manifest));f:close();print('Texture pilot: '..#manifest..' layered sources; hero alpha silhouettes preserved')
