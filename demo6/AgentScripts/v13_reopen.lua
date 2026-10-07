local root=[[E:/personalProject/Demo3/demo6/아트/승인픽셀-정지추출-v13/]]
local pc=app.pixelColor
local master=app.open(root..'approved-character-native-v013.aseprite')
local png=app.open(root..'character-native.png')
local a,b=Image(master.spec),Image(png.spec);a:drawSprite(master,1);b:drawSprite(png,1)
local mismatch=0;local visible={}
for _,l in ipairs(master.layers)do if l.isVisible then visible[#visible+1]=l.name end end
for y=0,a.height-1 do for x=0,a.width-1 do local p,q=a:getPixel(x,y),b:getPixel(x,y)
 if pc.rgbaA(p)~=pc.rgbaA(q) or (pc.rgbaA(q)>0 and (pc.rgbaR(p)~=pc.rgbaR(q)or pc.rgbaG(p)~=pc.rgbaG(q)or pc.rgbaB(p)~=pc.rgbaB(q)))then mismatch=mismatch+1 end
end end
assert(mismatch==0,'Aseprite/PNG mismatch');assert(#visible==1,'Review layer accidentally visible')
local result={width=a.width,height=a.height,layers=#master.layers,visibleLayers=visible,pixelMismatches=mismatch,referenceLayerLocked=not master.layers[1].isEditable,transparentPNGMatches=true}
local f=io.open(root..'reopen-validation.json','w');f:write(json.encode(result));f:close();print(json.encode(result));master:close();png:close()
