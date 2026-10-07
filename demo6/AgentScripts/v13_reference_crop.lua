local project=[[E:/personalProject/Demo3/demo6/]]
local dir=project..'검증/승인픽셀-정지추출-v13/'
local s=app.open(project..'승인.png');local src=Image(s.spec);src:drawSprite(s,1)
local crop=Image(src,Rectangle(772,338,160,192));crop:saveAs(dir..'reference-crop-160x192.png')
local large=Image(1280,1536,ColorMode.RGB)
for y=0,1535 do for x=0,1279 do large:drawPixel(x,y,crop:getPixel(math.floor(x/8),math.floor(y/8)))end end
large:saveAs(dir..'reference-crop-8x.png');s:close()
print('Reference only: native crop origin 772,338; no game asset modified')
