const fs=require('fs');const root='아트/정수리-질감수정-v10';
for(const ext of ['aseprite','psd'])fs.copyFileSync(root+'/entry-floor-master-v010.'+ext,root+'/entry-floor-mosaic-draft.'+ext);
const recipe=fs.readFileSync('AgentScripts/v10_source.lua','utf8');
fs.writeFileSync('AgentScripts/v10_floor_final.lua',recipe.slice(0,recipe.indexOf('-- Native source pixels'))+String.raw`
local im=open('floor-whole');local s=split(im,{'01-northwest','02-north','03-northeast','04-southwest','05-south','06-southeast'},function(x,y)return 1+math.min(2,math.floor(x/im.width*3))+(y>=im.height/2 and 3 or 0)end)
save(s,'entry-floor','Single continuous native painting, no quilt seams. Native source retained at 1672x941; runtime crops 1645x940 at 58.75 PPU to fit 28x16 world units exactly, without enlargement.');s:close()
local out=Image(im,Rectangle(13,0,1645,940));out:saveAs(root..'/runtime-PNG/entry_floor.png')
local f=io.open(root..'/manifest.json','r');local all=json.decode(f:read('*a'));f:close();local fresh={};for _,v in ipairs(all)do if v.id~='entry-floor' then table.insert(fresh,v)end end;table.insert(fresh,manifests[1]);f=io.open(root..'/manifest.json','w');f:write(json.encode(fresh));f:close()
f=io.open(root..'/runtime-manifest.json','r');all=json.decode(f:read('*a'));f:close();for _,v in ipairs(all)do if v.id=='entry_floor' then v.width=1645;v.height=940;v.ppu=58.75 end end;f=io.open(root..'/runtime-manifest.json','w');f:write(json.encode(all));f:close();print('Continuous floor native source saved; no upscaling')
`);
let imp=fs.readFileSync('AgentScripts/V10Import.cs','utf8').replace('(TextureImporter)AssetImporter.GetAtPath(file);var sp=', '(TextureImporter)AssetImporter.GetAtPath(file);if(id=="entry_floor"){imp.spritePixelsPerUnit=(float)p["ppu"];imp.SaveAndReimport();}var sp=');
imp=imp.replace('if(old.pivot!=sp.pivot)','if(id!="entry_floor"&&old.pivot!=sp.pivot)');fs.writeFileSync('AgentScripts/V10Import.cs',imp);
