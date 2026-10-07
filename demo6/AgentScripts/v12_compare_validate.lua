local project=[[E:/personalProject/Demo3/demo6/]]
local root=project..'아트/승인형상-대표샘플-v12'
local dir=project..'검증/승인형상-대표샘플-v12/comparison/'
app.fs.makeAllDirectories(dir)
local function read(p)local s=app.open(p);local im=Image(s.spec);im:drawSprite(s,1);s:close();return im end
local function resize(im,w,h)local out=Image(w,h,ColorMode.RGB);for y=0,h-1 do for x=0,w-1 do out:drawPixel(x,y,im:getPixel(math.min(im.width-1,math.floor(x*im.width/w)),math.min(im.height-1,math.floor(y*im.height/h))))end end;return out end
local ref=resize(read(project..'승인.png'),1920,1080)
local before=read(project..'검증/승인형상-대표샘플-v12/sample/1080/before-game.png')
local sample=read(project..'검증/승인형상-대표샘플-v12/sample/1080/sample-game.png')
local detail=Image(1920,640,ColorMode.RGB)
for i,record in ipairs({{ref,365},{before,397},{sample,402}})do local cut=Image(record[1],Rectangle(800,record[2],320,320));detail:drawImage(resize(cut,640,640),Point((i-1)*640,0))end
detail:saveAs(dir..'01-reference-current-sample-detail.png')
local all=Image(3840,720,ColorMode.RGB)
all:drawImage(resize(ref,1280,720),Point(0,0));all:drawImage(resize(before,1280,720),Point(1280,0));all:drawImage(resize(sample,1280,720),Point(2560,0));all:saveAs(dir..'02-reference-current-sample-full.png')
local pair=Image(2560,720,ColorMode.RGB);pair:drawImage(resize(before,1280,720),Point(0,0));pair:drawImage(resize(sample,1280,720),Point(1280,0));pair:saveAs(dir..'03-same-frame-before-sample.png')
local f=io.open(dir..'comparison-provenance.json','w');f:write(json.encode({columns={'approved reference','current original art in isolated copy','v12 static representative study'},referenceNative={1672,941},normalization='All full frames use a 1920x1080 screen basis. Detail crops are 320x320, enlarged equally 2x for inspection. Only vertical crop translation aligns helmet centers; characters are not individually scaled.',detailRects={{800,365,320,320},{800,397,320,320},{800,402,320,320}},productionArtApplied=false,staticStudyOnly=true}));f:close()

f=io.open(root..'/manifest.json','r');local parts=json.decode(f:read('*a'));f:close();local rows={}
for _,p in ipairs(parts)do
 local png=read(root..'/PNG/'..p.id..'.png')
 for _,ext in ipairs({'aseprite'})do
  local s=app.open(root..'/'..p.id..'-study-v012.'..ext);local im=Image(s.spec);im:drawSprite(s,1)
  local exact=im.bytes==png.bytes;local layers=#s.layers
  assert(exact,p.id..' '..ext..' composite mismatch');assert(layers==p.layers,p.id..' '..ext..' layer mismatch')
  rows[#rows+1]={id=p.id,format=ext,width=s.width,height=s.height,layers=layers,compositeExact=true};s:close()
 end
end
f=io.open(root..'/source-reopen-validation.json','w');f:write(json.encode(rows));f:close();print(json.encode(rows))
