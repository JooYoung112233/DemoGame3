-- Authored pixel clusters, painted and saved by official Aseprite Lua API.
-- This batch process owns only new sprites; no running GUI document is touched.
local root = app.params.out
assert(root and root ~= "", "Pass --script-param out=...")
local W,H=128,48
local s=Sprite(W,H,ColorMode.RGB)
local P={ink="28232C",deep="3C3034",leather0="513834",leather1="754A35",leather2="98633E",leather3="BA8350",steel0="424C60",steel1="64758A",steel2="96ADB8",steel3="CCDBD6",steel4="F0EBDD",brass0="82613A",brass1="BA873E",brass2="E7B953"}
local function col(key)
  local h=P[key] or key
  return app.pixelColor.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)
end
local im
local first=true
local function layer(name)
  local l
  if first then l=s.layers[1];first=false else l=s:newLayer() end
  l.name=name
  im=Image(W,H,ColorMode.RGB)
  s:newCel(l,1,im,Point(0,0))
  -- Paint directly into the cel's image, not an unbound copy.
  im=l:cel(1).image
end
local function dot(x,y,c) im:drawPixel(x,y,col(c)) end
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
  local ymin,ymax=H,0
  for _,v in ipairs(pts) do ymin=math.min(ymin,v[2]);ymax=math.max(ymax,v[2]) end
  for y=ymin,ymax do
    local xs={};local cy=y+0.5
    for i=1,#pts do
      local a,b=pts[i],pts[i%#pts+1]
      if (a[2]<=cy and b[2]>cy) or (b[2]<=cy and a[2]>cy) then
        table.insert(xs,a[1]+(cy-a[2])*(b[1]-a[1])/(b[2]-a[2]))
      end
    end
    table.sort(xs)
    for i=1,#xs,2 do
      for x=math.ceil(xs[i]-0.5),math.floor(xs[i+1]-0.5) do dot(x,y,c) end
    end
  end
  if outline then for i=1,#pts do local a,b=pts[i],pts[i%#pts+1];line(a[1],a[2],b[1],b[2],outline) end end
end

layer("01 Grip - leather wrap - hand anchor 25,24")
poly({{18,21},{33,21},{36,23},{36,26},{32,27},{18,27}},"leather0","ink")
rect(19,22,14,4,"leather1")
line(20,22,32,22,"leather3")
for x=21,30,3 do line(x,23,x+2,25,"deep");dot(x,23,"leather2") end
line(19,26,31,26,"leather0")

layer("02 Pommel - muted brass")
poly({{13,22},{15,20},{18,20},{20,22},{20,26},{18,28},{15,28},{13,26}},"brass0","ink")
poly({{14,22},{16,21},{18,21},{19,23},{18,25},{15,25},{14,24}},"brass1")
line(15,22,17,22,"brass2")
line(15,27,18,27,"deep")

layer("03 Blade - silhouette and lower facet")
poly({{36,19},{43,19},{89,20},{99,21},{113,24},{99,27},{89,28},{43,29},{36,28}},"steel1","ink")
poly({{38,25},{94,25},{107,24},{99,26},{88,27},{43,28},{38,27}},"steel0")
line(43,28,88,27,"steel2")
line(89,27,107,24,"steel1")

layer("04 Blade - upper bevel and edge")
poly({{38,20},{44,20},{88,21},{98,22},{110,24},{42,24},{38,23}},"steel2")
poly({{40,20},{44,20},{88,21},{98,22},{107,24},{89,23},{44,22},{40,22}},"steel3")
line(43,20,66,20,"steel4")
line(67,21,86,21,"steel4")
line(89,22,97,23,"steel3")
line(41,24,87,24,"steel1")
line(90,24,108,24,"steel2")
-- Short ricasso separates the hilt from the bright sharpened blade.
rect(38,21,3,6,"steel1")
line(39,21,39,26,"steel2")
line(41,21,41,27,"steel0")

layer("05 Guard - crosspiece and ferrule")
poly({{31,14},{34,14},{36,17},{36,21},{39,22},{39,26},{36,27},{36,30},{34,33},{31,33},{31,31},{33,28},{33,20},{31,17}},"brass0","ink")
poly({{32,15},{33,15},{35,18},{35,21},{34,23},{34,28},{32,31},{32,32},{33,32},{35,29},{35,26},{37,25},{37,23},{35,22},{34,18}},"brass1")
line(32,15,34,17,"brass2")
line(35,18,35,20,"brass2")
line(32,31,32,32,"brass1")
rect(32,21,6,7,"steel0")
rect(33,22,4,4,"steel1")
line(33,22,36,22,"steel3")
line(33,23,33,25,"steel2")
line(34,26,36,26,"ink")

s.frames[1].duration=0.1
local tag=s:newTag(1,1);tag.name="longsword_east_static"
local slice=s:newSlice(Rectangle(0,0,W,H));slice.name="hand_grip"
slice.pivot=Point(25,24)
s:saveAs(root.."/longsword-east-test-v1.aseprite")
print("CREATED "..s.filename.." 128x48 RGBA, 1 frame, "..#s.layers.." independent layers, hand grip (25,24)")
s:close()
