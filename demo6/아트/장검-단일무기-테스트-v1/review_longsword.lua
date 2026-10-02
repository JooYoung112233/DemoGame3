-- Reopen the saved Aseprite source; create only new review/export files.
local root=assert(app.params.out)
local ref=assert(app.params.reference)
local function flatten(sp)
  local image=Image(sp.width,sp.height,ColorMode.RGB)
  image:drawSprite(sp,1,Point(0,0))
  return image
end
local function save(image,path)
  local sp=Sprite(image.width,image.height,ColorMode.RGB)
  sp:newCel(sp.layers[1],1,image,Point(0,0))
  sp:saveAs(path);sp:close()
end
local function rgba(r,g,b) return app.pixelColor.rgba(r,g,b,255) end
local function backdrop(w,h)
  local out=Image(w,h,ColorMode.RGB)
  for y=0,h-1 do for x=0,w-1 do
    local v=((math.floor(x/8)+math.floor(y/8))%2==0) and 43 or 49
    out:drawPixel(x,y,rgba(v,v,v+5))
  end end
  return out
end
local function nearest(src,factor)
  local out=Image(src.width*factor,src.height*factor,ColorMode.RGB)
  for y=0,out.height-1 do for x=0,out.width-1 do
    out:drawPixel(x,y,src:getPixel(math.floor(x/factor),math.floor(y/factor)))
  end end
  return out
end

local sp=assert(app.open(root.."/longsword-east-test-v1.aseprite"))
assert(sp.width==128 and sp.height==48 and #sp.layers==5 and #sp.frames==1)
local weapon=flatten(sp)
local alphaZero,alphaFull,alphaPartial=0,0,0
local minx,miny,maxx,maxy=128,48,-1,-1
for y=0,47 do for x=0,127 do
  local a=app.pixelColor.rgbaA(weapon:getPixel(x,y))
  if a==0 then alphaZero=alphaZero+1 elseif a==255 then alphaFull=alphaFull+1 else alphaPartial=alphaPartial+1 end
  if a>0 then minx=math.min(minx,x);miny=math.min(miny,y);maxx=math.max(maxx,x);maxy=math.max(maxy,y) end
end end
assert(alphaZero>0 and alphaPartial==0 and minx>0 and miny>0 and maxx<127 and maxy<47)
print(string.format("REOPENED %dx%d layers=%d frames=%d visibleBounds=(%d,%d)-(%d,%d) alpha0=%d alpha255=%d alphaPartial=%d",sp.width,sp.height,#sp.layers,#sp.frames,minx,miny,maxx,maxy,alphaZero,alphaFull,alphaPartial))
for i,l in ipairs(sp.layers) do
  local cel=assert(l:cel(1));local n=0
  for it in cel.image:pixels() do if app.pixelColor.rgbaA(it())>0 then n=n+1 end end
  assert(n>0)
  print(string.format("LAYER %d %s pixels=%d",i,l.name,n))
  local out=Image(128,48,ColorMode.RGB)
  out:drawImage(cel.image,cel.position)
  save(out,root.."/layers/layer-0"..i..".png")
end
local slice=sp.slices[1]
assert(slice.name=="hand_grip" and slice.pivot.x==25 and slice.pivot.y==24)
save(weapon,root.."/review/reopened-native.png")
sp:close()

local one=backdrop(160,80);one:drawImage(weapon,Point(16,16))
save(one,root.."/review/longsword-1x.png")
save(nearest(one,8),root.."/review/longsword-8x.png")

-- Read existing character layers and compose only an isolated review image.
-- The original sprite and its PNGs are never saved or edited.
local files={"00-Contact-shadow.png","01-Left-leg-and-boot.png","02-Right-leg-and-boot.png","03-Leather-coat-and-undershirt.png","04-Sword---replaceable.png","05-Left-arm-and-glove.png","06-Right-arm-and-glove.png","07-Belt-buckle-and-pouch.png","08-Left-pauldron---replaceable.png","09-Cream-scarf.png","10-Face-and-ears.png","11-Brown-hair.png"}
local held=backdrop(160,224)
local angle=math.rad(72)
local co,si=math.cos(angle),math.sin(angle)
local anchorX,anchorY=16+95.6,12+107
for i,file in ipairs(files) do
  if i==5 then
    local rotated=Image(160,224,ColorMode.RGB)
    for y=0,223 do for x=0,159 do
      local dx,dy=x-anchorX,y-anchorY
      local sx=math.floor(co*dx+si*dy+25+0.5)
      local sy=math.floor(-si*dx+co*dy+24+0.5)
      if sx>=0 and sx<128 and sy>=0 and sy<48 then rotated:drawPixel(x,y,weapon:getPixel(sx,sy)) end
    end end
    held:drawImage(rotated,Point(0,0))
  else
    local source=assert(app.open(ref.."/layers-idle/"..file))
    held:drawImage(flatten(source),Point(16,12));source:close()
  end
end
save(held,root.."/review/hand-fit-1x.png")
save(nearest(held,4),root.."/review/hand-fit-4x.png")
print("REVIEW saved native 1:1, 8x nearest-neighbour, and reference hand-fit 1:1/4x. Original character unchanged.")
