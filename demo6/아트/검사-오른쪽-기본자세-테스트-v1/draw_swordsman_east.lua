-- New right-facing single-pose character, authored as native Aseprite pixels.
-- No existing document is saved or changed. Run in official Aseprite --batch.
local root=assert(app.params.out)
local weaponPath=assert(app.params.weapon)
local W,H=208,192
local s=Sprite(W,H,ColorMode.RGB)
local P={ink="25232A",deep="342B2E",hair0="3E2E2C",hair1="574034",hair2="72523E",hair3="927057",hair4="AE8C69",skin0="805449",skin1="AD7860",skin2="D2A17D",skin3="E7C5A0",cloth0="2C2D36",cloth1="3C3D46",cloth2="57565B",cloth3="706C6A",leather0="40312D",leather1="5F4435",leather2="7B5941",leather3="997754",leather4="B59972",cream0="706752",cream1="98907A",cream2="BEB59A",cream3="DED3B5",steel0="383F4D",steel1="556271",steel2="839596",steel3="B6C3BD",steel4="DADCCD",brass0="6D5838",brass1="9D8050",brass2="C0A171"}
local function col(key,alpha)
  local v=P[key] or key
  return app.pixelColor.rgba(tonumber(v:sub(1,2),16),tonumber(v:sub(3,4),16),tonumber(v:sub(5,6),16),alpha or 255)
end
local im;local first=true
local function layer(name)
  local l
  if first then l=s.layers[1];first=false else l=s:newLayer() end
  l.name=name
  s:newCel(l,1,Image(W,H,ColorMode.RGB),Point(0,0))
  im=l:cel(1).image
end
local function dot(x,y,c,a)
  assert(x>=0 and x<W and y>=0 and y<H,"pixel out of canvas")
  im:drawPixel(x,y,col(c,a))
end
local function line(x0,y0,x1,y1,c)
  local dx,dy=math.abs(x1-x0),-math.abs(y1-y0)
  local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1
  local e=dx+dy
  while true do
    dot(x0,y0,c)
    if x0==x1 and y0==y1 then break end
    local e2=2*e
    if e2>=dy then e=e+dy;x0=x0+sx end
    if e2<=dx then e=e+dx;y0=y0+sy end
  end
end
local function rect(x,y,w,h,c)
  for yy=y,y+h-1 do for xx=x,x+w-1 do dot(xx,yy,c) end end
end
local function poly(pts,c,outline)
  local min,max=H,0
  for _,v in ipairs(pts) do min=math.min(min,v[2]);max=math.max(max,v[2]) end
  for y=min,max do
    local xs={};local cy=y+0.5
    for i=1,#pts do
      local a,b=pts[i],pts[i%#pts+1]
      if (a[2]<=cy and b[2]>cy) or (b[2]<=cy and a[2]>cy) then table.insert(xs,a[1]+(cy-a[2])*(b[1]-a[1])/(b[2]-a[2])) end
    end
    table.sort(xs)
    for i=1,#xs,2 do for x=math.ceil(xs[i]-0.5),math.floor(xs[i+1]-0.5) do dot(x,y,c) end end
  end
  if outline then for i=1,#pts do local a,b=pts[i],pts[i%#pts+1];line(a[1],a[2],b[1],b[2],outline) end end
end

layer("00 Ground shadow - separate")
for y=163,180 do for x=35,114 do
  local q=((x-75)/39)^2+((y-172)/8)^2
  if q<=1 then dot(x,y,"ink",q<0.55 and 80 or 42) end
end end

layer("01 Far leg and boot")
poly({{73,116},{95,120},{94,139},{98,153},{108,157},{112,162},{108,167},{81,167},{77,161},{77,143},{70,129}},"cloth0","ink")
poly({{78,121},{91,124},{89,140},{83,148},{78,142}},"cloth1")
line(80,130,87,132,"cloth2")
poly({{79,143},{92,141},{96,152},{106,156},{109,161},{106,164},{83,164},{80,158}},"leather0","ink")
poly({{82,145},{90,144},{93,154},{104,158},{106,161},{87,162},{83,157}},"leather1")
line(84,147,90,146,"leather2")
poly({{86,157},{93,154},{102,158},{104,160},{90,161}},"leather2")
line(83,165,107,165,"cloth2")
line(84,167,106,167,"ink")

layer("02 Near leg and heavy boot")
poly({{51,119},{77,119},{81,134},{75,149},{73,160},{82,164},{90,168},{90,173},{85,176},{56,176},{50,172},{52,153},{48,139}},"cloth0","ink")
poly({{54,122},{71,123},{75,134},{68,147},{57,148},{52,139}},"cloth1")
poly({{55,127},{63,128},{63,139},{58,144},{53,138}},"cloth2")
line(67,135,73,132,"cloth0")
poly({{54,146},{70,147},{73,154},{71,162},{80,164},{88,169},{87,172},{81,174},{57,173},{53,170}},"leather1","ink")
poly({{56,147},{65,149},{65,159},{69,164},{76,167},{76,171},{57,169}},"leather2")
poly({{67,162},{72,162},{82,166},{85,169},{81,171},{74,169}},"leather3")
line(57,150,67,152,"leather3")
line(56,156,68,157,"leather0")
line(56,158,68,159,"leather3")
rect(66,156,3,4,"steel0");dot(67,157,"steel2")
line(56,171,84,173,"leather0")
line(57,174,84,175,"cloth2")
line(60,176,84,176,"ink")

layer("03 Far arm and relaxed glove")
poly({{83,77},{97,80},{103,89},{101,100},{98,111},{94,117},{87,117},{83,111},{86,101},{86,94},{81,89}},"cloth0","ink")
poly({{89,82},{96,84},{99,91},{96,101},{88,101},{90,91}},"cloth1")
poly({{88,99},{96,100},{94,108},{91,111},{86,108}},"leather0","ink")
poly({{88,105},{94,104},{96,109},{93,116},{89,116},{86,112}},"leather1","ink")
line(88,107,92,107,"leather3")

layer("04 Leather coat - body and split skirt")
poly({{54,72},{72,69},{90,77},{95,89},{92,110},{94,124},{90,143},{80,146},{69,134},{62,145},{46,141},{43,132},{48,112},{46,89}},"leather0","ink")
poly({{58,77},{75,75},{87,80},{90,92},{85,110},{88,125},{80,139},{70,128},{59,139},{48,136},{53,114},{51,94}},"leather1")
poly({{57,80},{70,80},{70,101},{62,119},{53,125},{56,110},{53,93}},"leather2")
poly({{73,81},{86,83},{87,93},{80,104},{68,107},{73,94}},"leather2")
poly({{77,87},{85,86},{83,94},{77,98},{71,99}},"leather3")
line(56,93,60,110,"leather3")
line(73,107,84,103,"leather0")
line(75,109,82,107,"leather3")
poly({{51,120},{64,123},{61,137},{49,136}},"leather2")
line(49,138,59,141,"leather0")
poly({{75,121},{88,122},{85,140},{81,142},{73,130}},"leather1")
line(85,125,83,137,"leather3")
line(69,116,70,132,"ink")
line(51,90,55,79,"leather4")
-- Sparse seams and wear, not noise over the silhouette.
for y=97,113,5 do dot(55,y,"leather0") end
line(56,132,59,129,"leather3");line(81,135,83,131,"leather2")

layer("05 Belt, buckle, and rear pouch")
poly({{48,113},{63,117},{88,112},{92,116},{89,122},{64,125},{47,121}},"leather0","ink")
line(50,114,64,120,"leather3");line(65,120,88,116,"leather3")
poly({{79,116},{88,114},{89,121},{80,123}},"brass0","ink")
line(80,117,86,116,"brass2");line(87,117,87,120,"brass1")
line(81,122,86,121,"brass1");line(80,118,80,121,"brass1")
line(82,119,87,118,"steel0")
poly({{42,116},{51,113},{59,119},{57,134},{48,139},{40,133}},"leather0","ink")
poly({{43,120},{50,118},{55,122},{53,132},{47,135},{43,131}},"leather2")
poly({{42,117},{50,115},{57,120},{48,126},{42,124}},"leather3","leather0")
rect(48,125,3,4,"leather0");dot(49,126,"brass1")

layer("06 Left pauldron - steel plates")
poly({{49,70},{62,64},{74,67},{81,76},{78,90},{69,98},{52,94},{43,85},{44,76}},"steel0","ink")
poly({{49,73},{61,67},{71,70},{77,77},{74,84},{61,88},{47,83}},"steel1")
poly({{49,74},{61,69},{69,71},{73,76},{62,82},{47,80}},"steel2")
poly({{51,74},{61,70},{67,72},{59,76},{50,78}},"steel3")
line(47,78,49,74,"steel4")
poly({{47,85},{61,91},{75,86},{75,91},{68,96},{54,92}},"steel1","steel0")
line(50,85,61,89,"steel3");line(63,89,74,84,"steel2")
line(54,92,65,95,"steel2")
rect(49,84,3,3,"brass0");dot(50,84,"brass2")
rect(71,78,3,3,"brass0");dot(72,78,"brass2")
line(60,77,64,79,"steel0");line(63,78,67,77,"steel1")

layer("07 Scarf - collar and hanging cloth")
poly({{65,66},{83,65},{101,71},{101,81},{95,87},{82,90},{69,85},{62,77}},"cream0","ink")
poly({{67,68},{82,68},{97,73},{98,79},{93,83},{81,86},{71,81},{66,74}},"cream2")
poly({{68,69},{79,71},{93,73},{97,76},{91,79},{79,78},{70,75}},"cream3")
line(69,78,80,84,"cream1");line(82,84,93,81,"cream1")
poly({{79,83},{88,86},{87,96},{83,105},{85,114},{74,112},{70,107},{75,96}},"cream0","ink")
poly({{80,86},{85,88},{82,99},{81,107},{83,112},{76,109},{75,103}},"cream1")
poly({{79,87},{82,89},{79,99},{79,106},{76,105}},"cream2")
line(76,109,81,111,"cream2")
line(75,112,78,113,"deep")

layer("08 Face and ear - right three quarter")
poly({{72,38},{90,35},{105,43},{109,52},{109,58},{116,63},{110,67},{108,75},{99,82},{89,81},{79,74},{72,61}},"skin0","ink")
poly({{78,40},{91,40},{102,46},{105,54},{105,61},{112,64},{107,67},{105,74},{98,78},{89,76},{79,68}},"skin1")
poly({{85,44},{98,45},{102,52},{101,60},{109,64},{103,67},{101,73},{95,74},{86,68}},"skin2")
poly({{92,47},{99,49},{99,54},{96,57},{89,54}},"skin3")
line(105,61,111,63,"skin3")
line(110,66,107,67,"skin0")
line(101,73,106,71,"deep")
line(99,75,104,73,"skin3")
-- Brow, small visible eye, cheek plane; no oversized portrait eyes.
line(96,55,103,55,"hair0");line(103,55,105,57,"ink")
line(98,58,104,58,"deep");dot(103,58,"ink");dot(100,58,"cream3")
line(97,60,101,61,"skin0")
poly({{77,55},{84,54},{88,59},{85,66},{80,67},{76,62}},"skin1","hair0")
poly({{79,57},{83,57},{85,60},{82,63},{79,61}},"skin2")
line(80,59,83,60,"skin0")
-- Stubble/shadow is a broad dark cheek plane, not noisy dots.
poly({{86,69},{91,74},{99,78},{105,73},{102,78},{98,80},{90,78}},"skin0")

layer("09 Hair - crown and right fringe")
poly({{42,47},{48,36},{43,29},{56,29},{57,18},{67,22},{75,15},{80,23},{93,22},{101,30},{112,33},{107,40},{115,43},{107,51},{106,62},{98,68},{98,56},{91,61},{88,70},{82,66},{82,54},{74,59},{75,72},{66,76},{66,65},{58,69},{56,59},{47,62},{49,54}},"hair0","ink")
poly({{48,45},{55,37},{50,32},{61,32},{60,23},{67,27},{75,20},{77,29},{91,27},{99,34},{105,36},{101,42},{109,44},{102,49},{96,53},{87,49},{79,53},{73,50},{65,55},{58,50}},"hair1")
poly({{51,42},{61,36},{59,29},{68,34},{75,26},{82,33},{91,30},{100,37},{94,40},{97,45},{87,43},{81,47},{71,43},{64,48},{58,45}},"hair2")
poly({{58,38},{65,36},{67,28},{72,36},{78,30},{84,37},{79,40},{70,38},{64,42}},"hair3")
poly({{83,32},{91,32},{97,37},{91,37},{87,40},{81,39}},"hair3")
line(70,29,73,33,"hair4");line(86,34,92,35,"hair4")
poly({{49,46},{57,47},{61,54},{57,62},{54,54},{48,57}},"hair1")
poly({{61,49},{69,45},{73,51},{69,57},{68,68},{64,70},{64,60}},"hair2")
poly({{72,48},{81,43},{87,48},{82,55},{77,58},{78,53}},"hair1")
poly({{85,45},{94,43},{102,45},{100,52},{93,57},{93,52}},"hair2")
poly({{98,44},{107,44},{103,50},{102,59},{99,61},{100,51}},"hair1")
line(64,52,66,57,"hair3");line(89,47,94,48,"hair3")
line(75,40,80,44,"hair0")

layer("10 Near arm - sleeve and bracer")
poly({{79,82},{87,79},{96,85},{97,96},{102,105},{112,107},{117,115},{113,122},{103,123},{95,118},{88,111},{83,103},{77,95}},"cloth0","ink")
poly({{83,84},{87,82},{93,87},{94,95},{89,101},{84,98},{80,92}},"cloth1")
poly({{83,85},{88,84},{91,88},{91,93},{86,94},{82,91}},"cloth2")
line(81,93,88,99,"cloth3")
poly({{89,97},{94,95},{98,103},{103,108},{100,113},{94,110},{89,105}},"skin0","ink")
poly({{91,98},{94,99},{96,105},{100,109},{97,110},{92,105}},"skin1")
line(92,99,94,104,"skin2")
poly({{97,105},{104,104},{112,109},{111,118},{107,122},{99,118},{93,111}},"leather0","ink")
poly({{99,107},{104,106},{109,110},{108,118},{105,119},{100,115},{96,111}},"leather2")
line(98,109,106,115,"leather3")
line(97,112,105,118,"leather0")
line(103,106,110,111,"leather4")
rect(103,113,3,3,"steel0");dot(104,114,"steel2")

layer("11 Longsword - held - anchor 113,117")
local ws=assert(app.open(weaponPath))
local wi=Image(ws.width,ws.height,ColorMode.RGB);wi:drawSprite(ws,1,Point(0,0));ws:close()
local angle=math.rad(-28);local co,si=math.cos(angle),math.sin(angle)
for y=0,H-1 do for x=0,W-1 do
  local dx,dy=x-113,y-117
  local sx,sy=math.floor(co*dx+si*dy+25+0.5),math.floor(-si*dx+co*dy+24+0.5)
  if sx>=0 and sx<wi.width and sy>=0 and sy<wi.height then
    local v=wi:getPixel(sx,sy)
    if app.pixelColor.rgbaA(v)>0 then im:drawPixel(x,y,v) end
  end
end end

layer("12 Near glove - wraps over grip")
poly({{108,111},{113,110},{118,112},{120,116},{118,121},{113,124},{108,121},{107,116}},"leather0","ink")
poly({{110,112},{114,112},{117,114},{118,117},{115,121},{111,121},{109,118}},"leather2")
line(111,113,115,113,"leather4")
line(116,115,118,116,"leather0")
line(114,118,117,119,"leather0")
line(111,120,114,122,"leather1")
poly({{107,113},{110,112},{113,116},{112,119},{109,118},{107,116}},"leather3","leather1")

layer("13 Visible eye and brow - right profile")
line(107,56,109,57,"hair0")
line(106,59,109,59,"deep")
dot(107,59,"cream3");dot(108,59,"ink")
line(107,61,109,62,"skin1")

s.frames[1].duration=0.1
local tag=s:newTag(1,1);tag.name="idle_east_static_test"
local foot=s:newSlice(Rectangle(0,0,W,H));foot.name="feet_pivot";foot.pivot=Point(76,173)
local hand=s:newSlice(Rectangle(0,0,W,H));hand.name="right_hand_grip";hand.pivot=Point(113,117)
s:saveAs(root.."/swordsman-east-idle-test-v1.aseprite")
print("CREATED "..s.filename.." 208x192 RGBA 1 frame / "..#s.layers.." editable layers. Feet 76,173. Hand 113,117.")
s:close()
