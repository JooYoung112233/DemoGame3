-- Original RGB extraction only. No generated/repainted RGB and no resampling.
local project=[[E:/personalProject/Demo3/demo6/]]
local root=project..'아트/승인픽셀-정지추출-v13/'
local evidence=project..'검증/승인픽셀-정지추출-v13/'
local pc=app.pixelColor
local s=app.open(project..'승인.png');local full=Image(s.spec);full:drawSprite(s,1);s:close()
local ox,oy,w,h=772,338,160,192
local source=Image(full,Rectangle(ox,oy,w,h))
-- Coordinates are inspected on an 8x nearest-neighbour reference, then divided
-- by eight. The source pixels themselves are copied at native resolution.
local body={
 {505,650},{520,649},{539,665},{565,680},{586,704},{606,730},{622,752},{637,770},{649,779},
 {676,773},{701,779},{727,792},{750,810},{767,836},{776,862},{798,894},{818,923},{817,947},
 {805,972},{785,994},{767,1005},{741,1008},{716,1004},{700,1015},
 {709,1030},{719,1049},{731,1071},{740,1092},{750,1116},{765,1144},{780,1177},{790,1196},
 {789,1207},{774,1205},{763,1192},{750,1182},{744,1194},{729,1210},{711,1213},{698,1208},
 {684,1218},{676,1230},{679,1261},{685,1308},{692,1348},{689,1362},{676,1362},{666,1351},
 {647,1344},{639,1325},{627,1325},{615,1348},{608,1361},{595,1356},{585,1328},{574,1326},
 {562,1354},{545,1376},{531,1392},{516,1420},{505,1431},{495,1421},{484,1401},{473,1390},
 {455,1390},{444,1377},{427,1372},{418,1354},{407,1348},{396,1318},{386,1307},{380,1318},
 {367,1327},{354,1327},{344,1318},{331,1328},{317,1312},{311,1306},{293,1316},{279,1324},
 {263,1320},{253,1312},{248,1297},{257,1266},{268,1235},{279,1204},{290,1180},{300,1144},
 {285,1164},{264,1176},{246,1177},{237,1166},{224,1167},{210,1160},{207,1145},{195,1142},
 {188,1124},{176,1122},{171,1107},{152,1108},{137,1104},{129,1094},{136,1081},{151,1075},
 {171,1069},{194,1066},{214,1056},{237,1044},{252,1039},
 {239,1021},{230,1000},{225,975},{214,947},{211,924},{224,909},{230,886},{233,861},
 {246,831},{267,810},{293,795},{327,785},{357,782},{365,767},{375,750},{391,730},
 {408,710},{429,691},{453,675},{479,659}
}
local weapon={
 {1098,148},{1104,153},{1101,177},{1091,207},{1080,237},{1065,270},{1048,309},{1030,349},
 {1010,392},{990,436},{969,481},{947,530},{926,576},{905,619},{885,663},{868,701},{856,726},
 {861,744},{883,753},{904,751},{925,755},{944,765},{945,778},{935,793},{919,798},{906,796},
 {881,783},{862,775},{858,786},{857,807},{849,828},{832,843},{831,869},{821,891},{808,913},
 {789,907},{779,883},{778,847},{775,824},{770,810},{768,792},{782,776},{785,762},{789,745},
 {772,736},{755,730},{745,717},{741,707},{744,694},{752,679},{766,676},{775,683},{778,695},
 {792,702},{804,706},{813,685},{825,653},{838,620},{854,584},{869,551},{887,511},{906,474},
 {924,438},{941,403},{959,367},{977,329},{998,289},{1019,250},{1043,212},{1071,174},{1088,155}
}
local function inside(x,y,p)local c=false;local j=#p;for i=1,#p do local a,b=p[i],p[j];if(a[2]>y)~=(b[2]>y)and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1]then c=not c end;j=i end;return c end
local out=Image(w,h,ColorMode.RGB);local mask=Image(w,h,ColorMode.RGB)
local nativeRGBA={};local bits={};local count=0;local bounds={w,h,0,0}
for y=0,h-1 do bits[y]={};for x=0,w-1 do
 local keep=inside((x+.5)*8,(y+.5)*8,body)or inside((x+.5)*8,(y+.5)*8,weapon)
 bits[y][x]=keep;local p=source:getPixel(x,y);local a=keep and 255 or 0
 out:drawPixel(x,y,pc.rgba(pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p),a))
 mask:drawPixel(x,y,pc.rgba(a,a,a,255))
 if keep then count=count+1;bounds[1]=math.min(bounds[1],x);bounds[2]=math.min(bounds[2],y);bounds[3]=math.max(bounds[3],x);bounds[4]=math.max(bounds[4],y)end
end end
local boundary=Image(w,h,ColorMode.RGB);local review=Image(w,h,ColorMode.RGB);local boundaryCount=0
for y=0,h-1 do for x=0,w-1 do if bits[y][x]then
 local edge=false;for yy=math.max(0,y-1),math.min(h-1,y+1)do for xx=math.max(0,x-1),math.min(w-1,x+1)do if not bits[yy][xx]then edge=true end end end
 if edge then boundary:drawPixel(x,y,pc.rgba(245,184,68,255));boundaryCount=boundaryCount+1 end
 -- Amber: all one-pixel inner edges. Magenta: ambiguous ground/shadow contact
 -- around cloak hem and the low-contrast guard/hand junction, kept for review.
 if edge and (y>=145 or (x>=92 and x<=119 and y>=86 and y<=111))then review:drawPixel(x,y,pc.rgba(234,88,192,255))end
end end end
app.fs.makeAllDirectories(root..'layers')
source:saveAs(root..'source-native-crop.png');out:saveAs(root..'character-native.png');mask:saveAs(root..'alpha-mask.png');boundary:saveAs(root..'boundary-inner-1px.png');review:saveAs(root..'ambiguous-boundary-review.png')
local master=Sprite(w,h,ColorMode.RGB);master:deleteLayer(master.layers[1])
local function layer(n,im,visible)local l=master:newLayer();l.name=n;master:newCel(l,1,im,Point(0,0));l.isVisible=visible;im:saveAs(root..'layers/'..n..'.png');return l end
local reference=layer('01-reference-RGB-do-not-repaint',source,false);reference.isEditable=false
layer('02-extracted-native-RGB',out,true)
layer('03-alpha-mask-guide',mask,false)
layer('04-edge-review-only',boundary,false)
layer('05-ambiguous-boundary-only',review,false)
master:saveAs(root..'approved-character-native-v013.aseprite');master:close()

local function bg(kind)local im=Image(w,h,ColorMode.RGB);for y=0,h-1 do for x=0,w-1 do local c=kind=='light'and 238 or kind=='dark'and 24 or((math.floor(x/8)+math.floor(y/8))%2==0 and 170 or 112);im:drawPixel(x,y,pc.rgba(c,c,c,255))end end;return im end
local function scale(im,k)local large=Image(im.width*k,im.height*k,ColorMode.RGB);for y=0,large.height-1 do for x=0,large.width-1 do large:drawPixel(x,y,im:getPixel(math.floor(x/k),math.floor(y/k)))end end;return large end
local surfaces={source}
for _,kind in ipairs({'checker','light','dark'})do local im=bg(kind);im:drawImage(out);im:saveAs(evidence..'extracted-on-'..kind..'-native.png');scale(im,8):saveAs(evidence..'extracted-on-'..kind..'-8x.png');surfaces[#surfaces+1]=im end
local board=Image(w*4,h,ColorMode.RGB);for i,im in ipairs(surfaces)do board:drawImage(im,Point((i-1)*w,0))end;board:saveAs(evidence..'comparison-native-original-checker-light-dark.png');scale(board,4):saveAs(evidence..'comparison-4x-original-checker-light-dark.png')
local ed=Image(source);ed:drawImage(boundary);ed:drawImage(review);scale(ed,8):saveAs(evidence..'boundary-review-8x.png')
local rgbMismatch=0;local nonbinary=0
for y=0,h-1 do for x=0,w-1 do local a,b=out:getPixel(x,y),source:getPixel(x,y);if pc.rgbaA(a)>0 and (pc.rgbaR(a)~=pc.rgbaR(b)or pc.rgbaG(a)~=pc.rgbaG(b)or pc.rgbaB(a)~=pc.rgbaB(b))then rgbMismatch=rgbMismatch+1 end;if pc.rgbaA(a)~=0 and pc.rgbaA(a)~=255 then nonbinary=nonbinary+1 end end end
assert(rgbMismatch==0,'RGB altered')
local meta={source='승인.png',sourceCanvas={full.width,full.height},crop={x=ox,y=oy,width=w,height=h},foregroundPixels=count,alphaMode='binary hand-reviewed silhouette mask; no recovered subpixel coverage',alphaNonbinaryPixels=nonbinary,foregroundRGBMismatch=rgbMismatch,foregroundBounds={x=bounds[1],y=bounds[2],width=bounds[3]-bounds[1]+1,height=bounds[4]-bounds[2]+1},innerBoundaryPixels=boundaryCount,notes={'Original RGB copied at 1:1; mask only','No hidden anatomy reconstruction','Ground/contact shadow excluded by the manual silhouette; uncertain hem and guard borders separately marked','Review masks are hidden layers and not part of the character PNG','Enlarged images are nearest-neighbour inspection views, not high-resolution masters'}}
local f=io.open(root..'extraction-metadata.json','w');f:write(json.encode(meta));f:close();print(json.encode(meta))
