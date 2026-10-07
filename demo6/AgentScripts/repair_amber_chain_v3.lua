local root=[[E:/personalProject/Demo3/demo6]];local art=root..'/아트/장비-UI-v3';local id='amu_amber';local src=Image{fromFile=art..'/원본/amu_amber-generated-untrimmed.png'}
local size=1654;local s=Sprite(size,size,ColorMode.RGB);local chain=Image(s.spec);local pendant=Image(s.spec)
-- Preserve the native pendant pixels. The cut-off generated chain is replaced by a complete loop.
for it in src:pixels() do if it.y>=365 or (it.x>=550 and it.x<=716 and it.y>=200) then pendant:drawPixel(it.x+200,it.y+400,it()) end end
local cx,cy,rx,ry=827,345,390,300;local count=48
for i=0,count-1 do
 local a=i*math.pi*2/count;local x,y=cx+rx*math.cos(a),cy+ry*math.sin(a);local angle=math.atan(ry*math.cos(a),-rx*math.sin(a));local ca,sa=math.cos(angle),math.sin(angle);local long,short=35,(i%2==0 and 16 or 12)
 for py=math.floor(y-43),math.ceil(y+43) do for px=math.floor(x-43),math.ceil(x+43) do
  local dx,dy=px-x,py-y;local u,v=dx*ca+dy*sa,-dx*sa+dy*ca
  local outer=math.sqrt((u/long)^2+(v/short)^2);local inner=math.sqrt((u/(long-8))^2+(v/(short-6))^2)
  if outer<=1 and inner>=1 then local dome=math.sin(math.min(1,math.max(0,(1-outer)*3.5))*math.pi/2);local grain=math.sin(px*1.3+py*.79+i)*5;local edge=(outer>.87 or inner<1.14) and 40 or 0;local tone=math.floor(37+dome*45+edge+grain);chain:drawPixel(px,py,app.pixelColor.rgba(tone,tone,math.max(0,tone-5),255)) end
 end end
end
s.layers[1].name='complete forged chain (retouched)';s:newCel(s.layers[1],1,chain,Point(0,0));local layer=s:newLayer();layer.name='original amber pendant and bail';s:newCel(layer,1,pendant,Point(0,0))
s:saveAs(art..'/원본/amu_amber-master-v003.aseprite');local composite=Image(s.spec);composite:drawSprite(s,1);composite:saveAs(art..'/원본/amu_amber-master.png');chain:saveAs(art..'/분리-PNG/amu_amber-chain.png');pendant:saveAs(art..'/분리-PNG/amu_amber-pendant.png');s:resize(512,512);s:saveAs(root..'/Assets/Resources/UI/Items/amu_amber.png');s:close()
local f=io.open(art..'/source-manifest.json','r');local parts=json.decode(f:read('*a'));f:close();for _,p in ipairs(parts) do if p.id==id then p.width=size;p.height=size;p.layers={'retouched complete chain','native original pendant'};p.independentMaterialLayers=true;p.note='1654px canvas: original pendant pixels kept at native size; new full chain replaces cropped chain. Raw 1254px generated source preserved.' end end
local f=io.open(art..'/source-manifest.json','w');f:write(json.encode(parts));f:close()
local ids={'arm_leather','arm_chain','arm_plate','hlm_leather','hlm_chain','hlm_plate','glv_leather','glv_chain','glv_plate','bts_leather','bts_chain','bts_plate','rng_iron','rng_blood','rng_fang','amu_fang','amu_charm','amu_amber'};local sheet=Sprite(1200,600,ColorMode.RGB);local board=Image(sheet.spec);board:clear(app.pixelColor.rgba(27,26,24,255));for i,id in ipairs(ids) do local q=app.open(art..'/원본/'..id..'-master.png');q:resize(180,180);local im=Image(q.spec);im:drawSprite(q,1);board:drawImage(im,Point(((i-1)%6)*200+10,math.floor((i-1)/6)*200+10));q:close() end;sheet:newCel(sheet.layers[1],1,board,Point(0,0));sheet:saveAs(art..'/검수/equipment-contact.png');sheet:close()
