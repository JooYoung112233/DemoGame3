local project=[[E:/personalProject/Demo3/demo6/]]
local dir=project..'검증/화풍조화-교정-v11/diagnosis/'
app.fs.makeAllDirectories(dir)
local function read(p)local s=app.open(p);local im=Image(s.spec);im:drawSprite(s,1);s:close();return im end
local ref=read(project..'승인.png')
local current=read(project..'검증/정수리-질감수정-v10/original/connected-art/1080/E.png')
local function resize(im,w,h)local out=Image(w,h,ColorMode.RGB);for y=0,h-1 do for x=0,w-1 do out:drawPixel(x,y,im:getPixel(math.min(im.width-1,math.floor(x*im.width/w)),math.min(im.height-1,math.floor(y*im.height/h))))end end;return out end
local refhd=resize(ref,1920,1080)
local full=Image(2560,720,ColorMode.RGB);full:drawImage(resize(refhd,1280,720),Point(0,0));full:drawImage(resize(current,1280,720),Point(1280,0));full:saveAs(dir..'01-reference-left-current-right.png')
local panel=Image(1280,640,ColorMode.RGB)
local a=Image(refhd,Rectangle(800,365,320,320))
local b=Image(current,Rectangle(800,380,320,320))
panel:drawImage(resize(a,640,640),Point(0,0));panel:drawImage(resize(b,640,640),Point(640,0));panel:saveAs(dir..'02-character-ground-equal-scale.png')
local s=Sprite(1280,640,ColorMode.RGB);s.layers[1].name='comparison-only-not-an-art-source';s:newCel(s.layers[1],1,panel);s:saveAs(dir..'02-character-ground-equal-scale.aseprite');s:close()
local f=io.open(dir..'comparison-provenance.json','w');f:write(json.encode({left='승인.png',right='검증/정수리-질감수정-v10/original/connected-art/1080/E.png',normalization='Both full views normalized to 1920x1080. Full board reduced equally to 1280x720 per side. Detail crops are 320x320 at the same normalized screen scale, both enlarged 2x nearest for comparison only. No character-relative scaling.',referenceNative={1672,941},currentNative={1920,1080},detailReferenceRect={800,365,320,320},detailCurrentRect={800,380,320,320},notNewRuntimeCapture=true,v11NotApplied=true}));f:close()
print('Local comparison boards saved')
