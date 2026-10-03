local root=[[E:/personalProject/Demo3/demo6]]
local source=root..'/아트/UI-정리-v2/원본/'
local ids={'wpn_longsword','wpn_greatsword','wpn_twinblades','potion','stone','gold','pickaxe','key','figure'}
for _,id in ipairs(ids) do
 local s=app.open(source..id..'-master.png')
 s:resize(512,512)
 s:saveAs(root..'/Assets/Resources/UI/Items/'..id..'.png')
 s:close()
end
for _,entry in ipairs({{'panel-iron-leather','panel',1024},{'slot','slot',512},{'panel-iron-leather','button',128}}) do
 local s=app.open(source..entry[1]..'-master.png')
 s:resize(entry[3],entry[3])
 s:saveAs(root..'/Assets/Resources/UI/Skin/'..entry[2]..'.png')
 s:close()
end
print('Exported 9 item textures and panel/socket/button textures')
