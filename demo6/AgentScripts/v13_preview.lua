local dir=[[E:/personalProject/Demo3/demo6/검증/승인픽셀-정지추출-v13/]]
local s=app.open(dir..'comparison-4x-original-checker-light-dark.png')
local im=Image(s.spec);im:drawSprite(s,1);local pair=Image(im,Rectangle(0,0,1280,768))
pair:saveAs(dir..'comparison-original-extracted-4x.png');s:close()
print('Paired preview saved: original left, extracted checker right, both 4x nearest')
