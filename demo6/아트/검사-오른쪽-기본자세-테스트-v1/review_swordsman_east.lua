-- Reopen and inspect only the newly made character. Review composites are not game captures.
local root=assert(app.params.out)
local function rgba(r,g,b,a) return app.pixelColor.rgba(r,g,b,a or 255) end
local function flatten(s)
  local i=Image(s.width,s.height,ColorMode.RGB);i:drawSprite(s,1,Point(0,0));return i
end
local function save(i,file)
  local s=Sprite(i.width,i.height,ColorMode.RGB);s:newCel(s.layers[1],1,i,Point(0,0));s:saveAs(root.."/"..file);s:close()
end
local function scale(i,factor)
  local w,h=math.floor(i.width*factor+0.5),math.floor(i.height*factor+0.5)
  local o=Image(w,h,ColorMode.RGB)
  for y=0,h-1 do for x=0,w-1 do
    local sx=math.min(i.width-1,math.floor(x/factor))
    local sy=math.min(i.height-1,math.floor(y/factor))
    o:drawPixel(x,y,i:getPixel(sx,sy))
  end end
  return o
end
local function backdrop(w,h,checker)
  local o=Image(w,h,ColorMode.RGB)
  for y=0,h-1 do for x=0,w-1 do
    if checker then
      local v=((math.floor(x/8)+math.floor(y/8))%2==0) and 43 or 49
      o:drawPixel(x,y,rgba(v,v,v+4))
    else
      local dx,dy=(x-w/2)/(w*.60),(y-h*.60)/(h*.75)
      local light=math.max(0,1-math.sqrt(dx*dx+dy*dy))
      local tile=((math.floor(x/48)+math.floor(y/32))%2==0) and 2 or 0
      local seam=(x%48==0 or y%32==0) and -4 or 0
      local v=math.floor(15+light*15+tile+seam)
      o:drawPixel(x,y,rgba(v+3,v+1,v))
    end
  end end
  return o
end
local s=assert(app.open(root.."/swordsman-east-idle-test-v1.aseprite"))
assert(s.width==208 and s.height==192 and #s.layers==14 and #s.frames==1)
local body=flatten(s)
local loX,loY,hiX,hiY=208,192,-1,-1
local clear,full,partial=0,0,0
for y=0,191 do for x=0,207 do
  local a=app.pixelColor.rgbaA(body:getPixel(x,y))
  if a==0 then clear=clear+1 elseif a==255 then full=full+1 else partial=partial+1 end
  if a>0 then loX=math.min(loX,x);loY=math.min(loY,y);hiX=math.max(hiX,x);hiY=math.max(hiY,y) end
end end
assert(clear>0 and loX>0 and loY>0 and hiX<207 and hiY<191)
print(string.format("REOPENED 208x192 RGBA / %d layers / %d frame; bounds %d,%d..%d,%d; alpha0=%d alpha255=%d shadowAlpha=%d",#s.layers,#s.frames,loX,loY,hiX,hiY,clear,full,partial))
for i,l in ipairs(s.layers) do
  local c=assert(l:cel(1));local n=0
  for it in c.image:pixels() do if app.pixelColor.rgbaA(it())>0 then n=n+1 end end
  assert(n>0)
  print(string.format("LAYER %02d %s visible_pixels=%d",i,l.name,n))
  local out=Image(208,192,ColorMode.RGB);out:drawImage(c.image,c.position)
  save(out,string.format("layers/layer-%02d.png",i))
end
local foot,hand
for _,slice in ipairs(s.slices) do
  print("PIVOT "..slice.name.." "..slice.pivot.x..","..slice.pivot.y)
  if slice.name=="feet_pivot" then foot=slice.pivot end
  if slice.name=="right_hand_grip" then hand=slice.pivot end
end
assert(foot and foot.x==76 and foot.y==173 and hand and hand.x==113 and hand.y==117)
local glove=s.layers[13]:cel(1)
assert(app.pixelColor.rgbaA(glove.image:getPixel(113-glove.position.x,117-glove.position.y))>0,"Glove must cover hand anchor")
-- Verify the hand anchor sits on the weapon, beneath the opaque glove.
local sword=s.layers[12]:cel(1)
assert(app.pixelColor.rgbaA(sword.image:getPixel(113-sword.position.x,117-sword.position.y))>0,"Weapon grip must occupy anchor")
save(body,"review/reopened-native.png")
s:close()
local native=backdrop(240,224,true);native:drawImage(body,Point(16,16))
save(native,"review/swordsman-east-1x.png")
save(scale(native,4),"review/swordsman-east-4x.png")

local mirrored=Image(body.width,body.height,ColorMode.RGB)
for y=0,body.height-1 do for x=0,body.width-1 do mirrored:drawPixel(body.width-1-x,y,body:getPixel(x,y)) end end
local directions=backdrop(480,240,false)
directions:drawImage(body,Point(8,28));directions:drawImage(mirrored,Point(264,28))
save(directions,"review/direction-flip-1x.png")
save(scale(directions,2),"review/direction-flip-2x.png")

for _,ppu in ipairs({64,96}) do
  -- The new dungeon camera is 8.0. A 1080px-high viewport displays 67.5px/unit.
  -- These are 384x320 CROPS of that pixel-density assumption, not full Unity screenshots.
  local f=67.5/ppu
  local display=scale(body,f)
  local out=backdrop(384,320,false)
  local x=192-math.floor(76*f+0.5);local y=252-math.floor(173*f+0.5)
  out:drawImage(display,Point(x,y))
  save(out,"review/game-scale-"..ppu.."ppu.png")
  print(string.format("DISPLAY 1080p ortho8 scale1 PPU%d: factor %.6f; canvas %dx%d screenpx; body height approx %.1fpx",ppu,f,display.width,display.height,162*f))
end
print("CHECKS PASS: native layers, clear border, feet and hand pivots, grip/glove overlap. Readability review only; Unity untested.")
