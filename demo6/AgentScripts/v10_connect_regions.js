const fs=require('fs');const p='../topdown-v10-review-isolated/Assets/Scripts/Game/Dungeon/DungeonWorld.cs';let s=fs.readFileSync(p,'utf8');
if(!s.includes('PainterlyMineArtV10.ApplyFloor'))s=s.replace('TextureStudyV7.Attach(c, cellRoot, fs, Map.Floor);','TextureStudyV7.Attach(c, cellRoot, fs, Map.Floor);\n            PainterlyMineArtV10.ApplyFloor(c, fs, Map.Floor);');
if(!s.includes('PainterlyMineArtV10.Rock'))s=s.replace('owned = name == "Rock" ? ShapeSprites.RoughRock(rect) : ShapeSprites.StoneWall(rect);','owned = PainterlyMineArtV10.Rock(rect) ?? (name == "Rock" ? ShapeSprites.RoughRock(rect) : ShapeSprites.StoneWall(rect));');
fs.writeFileSync(p,s);console.log('Isolated mine art hooks written');
const recipe=fs.readFileSync('AgentScripts/v10_source.lua','utf8');const a=recipe.indexOf('-- Native source pixels');
fs.writeFileSync('AgentScripts/v10_rock_source.lua',recipe.slice(0,a)+String.raw`
local im=open('rock-field');local s=split(im,{'01-northwest','02-northeast','03-southwest','04-southeast'},function(x,y)return 1+(x>=im.width/2 and 1 or 0)+(y>=im.height/2 and 2 or 0)end)
save(s,'mine-field','Native 1254-square opaque painted wall mass. Four visible regions editable; seamless hidden-blocker world alignment.');s:close()
local f=io.open(root..'/manifest.json','r');local all=json.decode(f:read('*a'));f:close();local fresh={};for _,v in ipairs(all)do if v.id~='mine-field' then table.insert(fresh,v)end end;table.insert(fresh,manifests[1]);f=io.open(root..'/manifest.json','w');f:write(json.encode(fresh));f:close();print('Native rock master saved')
`);
