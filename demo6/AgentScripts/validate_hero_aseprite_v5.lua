local root=[[E:/personalProject/Demo3/demo6/아트/정수리/검사-v5-망토와무기]]
local f=io.open(root..'/manifest.json','r');local parts=json.decode(f:read('*a'));f:close();local report={}
for _,part in ipairs(parts) do
 local s=app.open(root..'/'..part.id..'-master-v005.aseprite');local out=Image(s.spec);out:drawSprite(s,1)
 local original=Image{fromFile=root..'/PNG/'..part.id..'.png'};local maxRGB,maxA,different=0,0,0
 for it in original:pixels() do local a,b=it(),out:getPixel(it.x,it.y);if a~=b then different=different+1 end;maxA=math.max(maxA,math.abs(app.pixelColor.rgbaA(a)-app.pixelColor.rgbaA(b)));if app.pixelColor.rgbaA(a)>8 then for _,fn in ipairs({app.pixelColor.rgbaR,app.pixelColor.rgbaG,app.pixelColor.rgbaB}) do maxRGB=math.max(maxRGB,math.abs(fn(a)-fn(b))) end end end
 table.insert(report,{id=part.id,asepriteLayers=#s.layers,expectedLayers=#part.layers,width=s.width,height=s.height,differentPixels=different,maxRGBDelta=maxRGB,maxAlphaDelta=maxA});s:close()
end
local f=io.open(root..'/aseprite-composite-validation.json','w');f:write(json.encode(report));f:close()
