local root=[[E:/personalProject/Demo3/demo6/아트/장비-UI-v3]]
local f=io.open(root..'/source-manifest.json','r');local parts=json.decode(f:read('*a'));f:close();local out={}
for _,part in ipairs(parts) do
 local img=Image{fromFile=root..'/원본/'..part.id..'-master.png'};local minx,miny,maxx,maxy,border,solid=img.width,img.height,-1,-1,0,0
 for it in img:pixels() do local a=app.pixelColor.rgbaA(it());if a>8 then minx=math.min(minx,it.x);miny=math.min(miny,it.y);maxx=math.max(maxx,it.x);maxy=math.max(maxy,it.y);solid=solid+1;if it.x==0 or it.y==0 or it.x==img.width-1 or it.y==img.height-1 then border=border+1 end end end
 table.insert(out,{id=part.id,width=img.width,height=img.height,bounds={minx,miny,maxx,maxy},borderPixels=border,visiblePixels=solid})
end
local f=io.open(root..'/검수/icon-alpha-validation.json','w');f:write(json.encode(out));f:close()
