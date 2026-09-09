from pathlib import Path
import json
from PIL import Image
from asset_cutout import polygon_cut,gray_backdrop_cut,inspect_asset
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/chapter01/shared-props';QA=OUT/'qa'
OUT.mkdir(exist_ok=True)
sheet=Image.open(ROOT/'art/props/chapter01-props-v1.png')
specs=[
 ('food-can',[433,568,273,312],[(445,640),(458,611),(486,590),(525,576),(567,572),(608,577),(644,591),(673,613),(686,642),(692,788),(687,820),(670,842),(634,861),(590,874),(545,877),(499,868),(466,849),(448,824),(441,790)]),
 ('medicine-bottle',[102,572,248,311],[(139,595),(172,580),(224,574),(267,583),(294,600),(305,625),(301,651),(299,661),(319,681),(327,705),(334,805),(329,834),(309,854),(275,871),(226,880),(178,876),(139,862),(116,842),(109,818),(108,710),(115,684),(136,669),(130,641),(130,619)]),
 ('black-journal',[1301,189,354,303],[(1307,244),(1324,225),(1519,196),(1541,201),(1650,443),(1644,466),(1454,490),(1430,483),(1399,460)]),
 ('soup-bowl',[1224,569,424,285],[(1229,682),(1240,647),(1268,615),(1315,591),(1378,575),(1453,571),(1528,582),(1587,606),(1625,639),(1642,674),(1643,704),(1628,745),(1600,782),(1562,813),(1517,837),(1473,849),(1426,850),(1374,840),(1326,820),(1288,791),(1259,755),(1241,718)]),
 ('pencil-tin-closed',[28,182,440,303],[(34,278),(47,255),(348,184),(367,193),(469,400),(457,426),(133,484),(115,479),(39,299)]),
 ('pencil-tin-open',[449,55,377,441],[(455,110),(469,91),(703,58),(722,66),(770,222),(789,229),(826,433),(821,459),(579,494),(560,485),(487,303),(489,282),(462,136)])
]
records=[]
for name,rect,poly in specs:
    asset,actual=gray_backdrop_cut(sheet,rect)
    file=OUT/(name+'-v1.png');asset.save(file);inspect_asset(asset,QA,name)
    records.append({'id':name,'asset':file.relative_to(ROOT).as_posix(),'source':'art/props/chapter01-props-v1.png','sourceRect':actual,'size':list(asset.size),'method':'connected gray/checker exterior removal; source RGB preserved'})
pot=Image.open(ROOT/'art/chapter01/kitchen/soup-pot-source-v1.png')
asset,rect=gray_backdrop_cut(pot,[390,210,750,550],holes=[(470,430),(1061,431)])
file=ROOT/'art/chapter01/kitchen/soup-pot-v1.png';asset.save(file);inspect_asset(asset,QA,'soup-pot')
records.append({'id':'soup-pot','asset':file.relative_to(ROOT).as_posix(),'sourceRect':rect,'size':list(asset.size),'method':'gray exterior flood and handle holes; RGB preserved'})
dog=Image.open(ROOT/'art/chapter01/dog/byeolddongi-poses-source-v1.png')
for name,rect in [('sit',[180,200,370,490]),('alert',[680,200,415,500]),('rest',[1215,420,455,285])]:
    asset,actual=gray_backdrop_cut(dog,rect)
    file=ROOT/'art/chapter01/dog'/('byeolddongi-'+name+'-v1.png');asset.save(file);inspect_asset(asset,QA,'dog-'+name)
    records.append({'id':'dog-'+name,'asset':file.relative_to(ROOT).as_posix(),'sourceRect':actual,'size':list(asset.size),'method':'gray exterior flood; eye highlights retained'})
(OUT/'manifest-v1.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps([{'id':r['id'],'size':r['size']} for r in records]))
