local root=[[E:/personalProject/Demo3/demo6]]
local art=root..'/아트/장비-UI-v3'
local ids={'arm_leather','arm_chain','arm_plate','hlm_leather','hlm_chain','hlm_plate','glv_leather','glv_chain','glv_plate','bts_leather','bts_chain','bts_plate','rng_iron','rng_blood','rng_fang','amu_fang','amu_charm','amu_amber'}
local sheet=Sprite(1200,600,ColorMode.RGB);local board=Image(sheet.spec);board:clear(app.pixelColor.rgba(27,26,24,255))
local report={}
for i,id in ipairs(ids) do
 local original=Image{fromFile=art..'/원본/'..id..'-master.png'}
 local s=Sprite(original.width,original.height,ColorMode.RGB);s.layers[1].name='painted equipment (single image)';s:newCel(s.layers[1],1,original,Point(0,0))
 local l=s:newLayer();l.name='retouch (empty)';s:newCel(l,1,Image(s.spec),Point(0,0));s:saveAs(art..'/원본/'..id..'-master-v003.aseprite');s:close()
 original:saveAs(art..'/분리-PNG/'..id..'-painted.png')
 local r=app.open(art..'/원본/'..id..'-master.png');r:resize(512,512);r:saveAs(root..'/Assets/Resources/UI/Items/'..id..'.png');r:resize(180,180)
 local thumb=Image(r.spec);thumb:drawSprite(r,1);board:drawImage(thumb,Point(((i-1)%6)*200+10,math.floor((i-1)/6)*200+10));r:close()
 table.insert(report,{id=id,width=original.width,height=original.height,runtime=512,layers={'painted equipment','empty retouch'},independentMaterialLayers=false})
end
sheet:newCel(sheet.layers[1],1,board,Point(0,0));sheet:saveAs(art..'/검수/equipment-contact.png');sheet:close()
for _,id in ipairs({'weapon','armor','helm','gloves','boots','ring','amulet'}) do local s=app.open(art..'/빈슬롯/empty_'..id..'.png');s.layers[1].name='engraved symbol';s:saveAs(art..'/빈슬롯/empty_'..id..'-v003.aseprite');s:close() end
local f=io.open(art..'/source-manifest.json','w');f:write(json.encode(report));f:close()
print('18 equipment icons and 7 empty-slot Aseprite sources packaged')
