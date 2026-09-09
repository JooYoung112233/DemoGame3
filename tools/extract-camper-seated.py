"""Cut existing seated poses into rear/body and tabletop/front alpha layers.
No new pixels, hidden anatomy, or background variants are generated.
"""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageChops

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/chapter01/layers'
DEST=OUT/'characters'
QA=OUT/'qa'
DEST.mkdir(exist_ok=True)
SOURCE=ROOT/'art/chapter01/01-camper-evening'

poses=[
 {'id':'suhyeok-seated','name':'수혁 · 앉기','source':'camper-before-pencils-v1.png','origin':[833,283],'rect':[840,289,109,155],
  'outline':[(42,479),(57,436),(78,412),(120,383),(161,362),(173,352),(151,330),(124,310),(115,282),(110,259),(120,235),(101,226),(94,215),(129,192),(141,174),(110,176),(105,162),(142,144),(164,123),(175,108),(165,106),(174,92),(204,81),(240,74),(252,58),(283,60),(316,60),(333,53),(332,42),(346,34),(366,40),(387,54),(393,71),(399,73),(401,56),(394,43),(407,41),(424,55),(429,72),(430,95),(450,95),(476,100),(491,111),(489,120),(474,120),(481,137),(501,155),(514,180),(529,195),(526,209),(534,222),(526,239),(529,252),(516,258),(512,282),(501,298),(486,301),(477,289),(469,329),(459,347),(475,355),(506,374),(530,402),(543,430),(548,472),(554,514),(556,594),(559,635),(560,652),(571,672),(570,696),(561,721),(560,738),(540,749),(525,754),(496,760),(429,760),(364,759),(322,763),(232,765),(216,782),(197,796),(176,798),(156,794),(142,785),(120,783),(100,780),(82,767),(69,745),(64,719),(53,697),(50,678),(44,652),(47,624),(46,594),(39,565)],'holes':[]},
 {'id':'soi-seated','name':'소이 · 앉기','source':'camper-before-pencils-v1.png','origin':[833,283],'rect':[938,330,112,132],
  'outline':[(606,481),(617,450),(631,423),(647,393),(655,366),(672,342),(692,315),(717,291),(743,274),(770,257),(802,248),(832,247),(854,247),(875,248),(897,253),(920,262),(944,272),(967,289),(988,312),(1003,340),(1016,374),(1026,414),(1030,447),(1051,471),(1062,487),(1068,514),(1062,537),(1048,550),(1041,571),(1023,587),(999,596),(982,601),(995,617),(1008,644),(1021,667),(1028,696),(1031,726),(1027,751),(1014,773),(989,787),(964,796),(927,803),(892,801),(871,805),(845,805),(825,807),(812,801),(800,792),(790,780),(788,765),(744,764),(706,765),(686,779),(663,786),(640,787),(618,781),(603,766),(595,750),(593,728),(595,705),(605,682),(611,661),(621,637),(634,608),(649,586),(657,574),(638,563),(626,547),(615,540),(607,527),(605,508)],'holes':[]},
 {'id':'soi-drawing','name':'소이 · 그리기','source':'camper-drawing-v2.png','origin':[938,326],'rect':[938,330,112,132],
  'outline':[(103,220),(117,181),(141,147),(163,122),(192,99),(224,74),(254,57),(281,44),(313,38),(346,38),(376,48),(407,53),(435,68),(461,87),(483,111),(500,140),(514,171),(525,205),(531,240),(541,261),(550,281),(554,304),(548,325),(534,347),(518,364),(495,375),(471,382),(460,386),(471,402),(494,426),(515,457),(529,486),(538,515),(540,539),(531,558),(516,572),(490,582),(458,586),(414,587),(393,590),(374,588),(355,595),(330,596),(307,592),(286,583),(275,567),(270,550),(274,534),(246,540),(211,539),(181,539),(173,512),(161,529),(140,540),(143,558),(152,570),(157,590),(151,608),(151,632),(157,648),(154,667),(145,671),(137,657),(133,640),(114,639),(98,646),(78,643),(61,634),(44,620),(32,600),(24,582),(15,559),(8,538),(6,515),(10,485),(21,460),(34,441),(56,424),(74,406),(99,390),(117,378),(107,361),(95,341),(88,319),(82,299),(83,276),(92,251)],
  'holes':[[(141,510),(155,506),(167,497),(164,526),(177,535),(143,538)]]}
]

actors=[]
reports=[]
for pose in poses:
    image=Image.open(SOURCE/pose['source']).convert('RGB')
    x,y,w,h=pose['rect'];ox,oy=pose['origin']
    mask=Image.new('L',(w*4,h*4),0);draw=ImageDraw.Draw(mask)
    def points(poly):return [((ox+px/5-x)*4,(oy+py/5-y)*4) for px,py in poly]
    draw.polygon(points(pose['outline']),fill=255)
    for hole in pose['holes']:draw.polygon(points(hole),fill=0)
    mask=mask.resize((w,h),Image.Resampling.LANCZOS)
    crop=image.crop((x,y,x+w,y+h)).convert('RGBA');crop.putalpha(mask)
    # Separate at the back tabletop edge. Disjoint masks avoid double alpha seams.
    back_mask=mask.copy();ImageDraw.Draw(back_mask).rectangle((0,432-y,w,h),fill=0)
    front_mask=ImageChops.subtract(mask,back_mask)
    assert ImageChops.add(back_mask,front_mask).tobytes()==mask.tobytes()
    assert ImageChops.multiply(back_mask,front_mask).getbbox() is None
    for part,alpha,order in [('body',back_mask,20),('hands',front_mask,50)]:
        layer=crop.copy();layer.putalpha(alpha)
        assert layer.getchannel('A').getextrema()==(0,255)
        assert layer.convert('RGB').tobytes()==image.crop((x,y,x+w,y+h)).tobytes()
        file=DEST/(pose['id']+'-'+part+'-v1.png');layer.save(file)
        actors.append({'id':pose['id']+'-'+part,'name':pose['name'],'asset':file.relative_to(ROOT).as_posix(),'rect':pose['rect'],'order':order,'interactive':False,'pose':pose['id'],'part':part})
    for bg,label in [('#f2e7d2','light'),('#283c48','dark')]:
        panel=Image.new('RGBA',crop.size,bg);panel.alpha_composite(crop)
        panel.resize((w*4,h*4),Image.Resampling.NEAREST).save(QA/(pose['id']+'-'+label+'.png'))
    reports.append({'id':pose['id'],'rect':pose['rect'],'source':pose['source'],'sourceRgbPreserved':True,'splitMasksDisjoint':True,'transparentExterior':True})

config=json.loads((ROOT/'design/interaction/camper-layers-v1.json').read_text(encoding='utf-8'))
config['version']=2
config['zoom']={'scale':2.9,'originX':57.5,'originY':44}
config['actors']=actors
config['foreground']['id']='table-foreground'
config['foreground']['alwaysVisible']=True
config['foreground']['status']='seated-pose composite inspected; other poses untested'
props=[s['id'] for s in config['slots']]
father=['suhyeok-seated-body','suhyeok-seated-hands']
idle=['soi-seated-body','soi-seated-hands']
drawing=['soi-drawing-body','soi-drawing-hands']
config['presets']=[
 {'id':'empty','label':'인물 숨기기','visible':props,'active':[],'description':'배경과 소품만 남긴 상태'},
 {'id':'seated','label':'함께 앉기','visible':props+father+idle,'active':[],'description':'같은 벤치에 앉은 수혁과 소이'},
 {'id':'drawing','label':'그리기 자세','visible':props+father+drawing,'active':[],'description':'소이의 자세만 교체 · 페이지 그림은 기존 상태'}
]
config['notes']=['원화의 보이는 인물 부분만 추출했다. 가려진 하체가 있는 전신 스프라이트가 아니다.','몸 → 식탁 전경 → 소품 → 팔·손 순서로 합성한다.','확대 선명도 보정은 사용자 요청에 따라 후속 작업으로 둔다.','페이지 그림 변화와 실제 사건·대화는 아직 연결하지 않았다.']
(ROOT/'design/interaction/camper-seated-v1.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'seated-extraction-report.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
all_layers=config['slots']+actors+[config['foreground']]
for preset in config['presets']:
    composed=Image.open(OUT/'camper-clean-base-v1.png').convert('RGBA')
    for layer in sorted(all_layers,key=lambda l:l['order']):
        if layer.get('alwaysVisible') or layer['id'] in preset['visible']:
            composed.alpha_composite(Image.open(ROOT/layer['asset']).convert('RGBA'),tuple(layer['rect'][:2]))
    composed.save(QA/('camper-'+preset['id']+'-composite.png'))
    composed.crop((830,280,1095,555)).resize((795,825),Image.Resampling.NEAREST).save(QA/(preset['id']+'-composite-3x.png'))
print(json.dumps(reports,ensure_ascii=False))
