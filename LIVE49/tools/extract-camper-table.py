"""Extract manually traced original pixels; never regenerate or repaint the art.

The user authorized local cropping/background removal on 2026-09-09.
Traces use coordinates in the 6x table QA crop (830,430)-(1085,550).
"""
from pathlib import Path
import json
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'art/chapter01/layers'
SPRITES = OUT / 'sprites'
QA = OUT / 'qa'
SPRITES.mkdir(exist_ok=True)
QA.mkdir(exist_ok=True)
SOURCE = Image.open(ROOT / 'art/scenes/01-camper/camper-mood-v1.png').convert('RGB')
BASE = Image.open(OUT / 'camper-clean-base-v1.png').convert('RGBA')
CONFIG_PATH = ROOT / 'design/interaction/camper-layers-v1.json'
config = json.loads(CONFIG_PATH.read_text(encoding='utf-8'))

# Polygons follow the dark outer object edges, excluding the tabletop shadow.
traces = {
    'pencil-cup': {
        'outline': [(145,158),(155,144),(171,141),(182,163),(186,108),(190,92),(199,86),(209,93),(214,130),(226,117),(239,114),(250,124),(261,142),(269,108),(274,93),(285,90),(295,98),(292,132),(305,118),(317,121),(325,139),(326,150),(339,151),(348,162),(347,180),(362,194),(365,210),(355,232),(348,243),(351,269),(351,389),(345,413),(331,430),(309,442),(279,450),(228,453),(195,446),(176,436),(157,416),(147,395),(146,271),(151,239),(152,214),(142,188)],
        'holes': [[(216,144),(224,161),(233,204),(231,154),(239,151),(252,179),(258,159),(254,144),(245,132),(235,127),(225,129)]]
    },
    'sketchbook': {
        'outline': [(458,223),(465,202),(483,186),(516,180),(552,176),(579,171),(605,168),(647,158),(696,155),(720,161),(742,172),(755,181),(777,171),(805,166),(831,160),(861,157),(888,161),(921,172),(959,177),(1001,184),(1027,204),(1037,230),(1038,494),(1048,521),(1048,542),(1035,553),(1004,555),(949,550),(896,546),(823,545),(790,546),(787,557),(773,562),(744,562),(728,556),(727,545),(698,547),(655,549),(599,546),(553,550),(507,555),(475,556),(458,550)],
        'holes': []
    },
    'mug': {
        'outline': [(1094,385),(1101,366),(1115,350),(1137,338),(1168,329),(1202,327),(1231,334),(1254,345),(1275,363),(1287,383),(1292,405),(1308,400),(1334,403),(1355,414),(1368,431),(1370,455),(1363,480),(1348,499),(1323,513),(1284,522),(1276,547),(1254,565),(1222,578),(1189,583),(1157,578),(1128,567),(1110,550),(1098,527),(1091,501)],
        'holes': [[(1292,432),(1306,424),(1322,425),(1334,435),(1336,450),(1330,468),(1317,482),(1290,490)]]
    }
}

def source_points(points):
    return [(830 + x / 6, 430 + y / 6) for x, y in points]

def mask_for(rect, outline, holes):
    x,y,w,h = rect
    scale=4
    mask=Image.new('L',(w*scale,h*scale),0)
    draw=ImageDraw.Draw(mask)
    def local(points): return [((px-x)*scale,(py-y)*scale) for px,py in points]
    draw.polygon(local(outline),fill=255)
    for hole in holes: draw.polygon(local(hole),fill=0)
    return mask.resize((w,h),Image.Resampling.LANCZOS)

report=[]
for slot in config['slots']:
    rect=slot['rect'];x,y,w,h=rect
    trace=traces[slot['id']]
    mask=mask_for(rect,source_points(trace['outline']),[source_points(p) for p in trace['holes']])
    crop=SOURCE.crop((x,y,x+w,y+h)).convert('RGBA')
    crop.putalpha(mask)
    filename=slot['id']+'-source-v1.png'
    dest=SPRITES/filename
    crop.save(dest)
    slot['asset']=dest.relative_to(ROOT).as_posix()
    slot['status']='manual-cutout-prototype'
    alpha=crop.getchannel('A')
    assert alpha.getextrema()==(0,255),slot['id']
    assert crop.convert('RGB').tobytes()==SOURCE.crop((x,y,x+w,y+h)).tobytes(), 'Source RGB changed'
    # Diagnostic backgrounds reveal wood remnants and lost light paper pixels.
    for bg,label in [('#f2e7d2','light'),('#283c48','dark')]:
        panel=Image.new('RGBA',crop.size,bg);panel.alpha_composite(crop)
        panel.resize((w*6,h*6),Image.Resampling.NEAREST).save(QA/(slot['id']+'-'+label+'.png'))
    report.append({'id':slot['id'],'file':slot['asset'],'size':[w,h],'alphaMin':0,'alphaMax':255,'sourceRgbPreserved':True})

# Same clean-base pixels form the tabletop occluder, without any props baked in.
table_rect=[835,432,257,201]
table_top=[(856,436),(1074,435),(1085,440),(1089,452),(1089,542),(1084,554),(849,557),(838,547),(837,448),(843,440)]
table_holes=[]
table_mask=mask_for(table_rect,table_top,table_holes)
tx,ty,tw,th=table_rect
draw=ImageDraw.Draw(table_mask)
for leg in [[(856,553),(870,553),(870,631),(858,631)],[(895,554),(909,554),(909,625),(896,625)],[(1004,554),(1017,554),(1017,628),(1005,628)],[(1060,553),(1077,553),(1077,629),(1062,629)]]:
    draw.polygon([(x-tx,y-ty) for x,y in leg],fill=255)
table=BASE.crop((tx,ty,tx+tw,ty+th));table.putalpha(table_mask)
table.save(SPRITES/'table-foreground-v1.png')
config['foreground']={'asset':'art/chapter01/layers/sprites/table-foreground-v1.png','rect':table_rect,'order':30,'status':'manual-cutout-prototype; character occlusion not yet tested'}
config['notes']=['사용자가 기존 원화의 로컬 분리를 허용한 뒤 수동 윤곽으로 RGB 픽셀을 보존해 추출했다.','검수용 상태이며 날짜별 사건이나 행동력 구현이 아니다.','슬롯 위치와 크기는 모든 상태에서 공유한다.','인물과 손 레이어는 아직 없다.']
CONFIG_PATH.write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

composite=BASE.copy()
for slot in config['slots']:
    sprite=Image.open(ROOT/slot['asset']).convert('RGBA')
    composite.alpha_composite(sprite,tuple(slot['rect'][:2]))
composite.save(QA/'camper-table-composite.png')
composite.crop((830,430,1095,555)).resize((1325,625),Image.Resampling.NEAREST).save(QA/'table-composite-5x.png')
BASE.convert('RGB').resize((1100,619),Image.Resampling.LANCZOS).save(OUT/'camper-clean-base-preview.jpg',quality=86)
(OUT/'extraction-report.json').write_text(json.dumps({'assets':report,'source':'art/scenes/01-camper/camper-mood-v1.png','method':'manual polygon alpha; unmodified source RGB','limitations':['Native source props are small; large Polaroid closeups require later detail work.','Pencil cup keeps existing colors, including blue; narrative state not finalized.']},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
