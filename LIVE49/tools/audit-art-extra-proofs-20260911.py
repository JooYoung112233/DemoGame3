"""Fresh visual proofs from current assets; writes audit output only."""
from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont
import numpy as np
R=Path(__file__).resolve().parents[1];O=R/'design/audit/art-2026-09-11'
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
def read(p):return json.loads((R/p).read_text(encoding='utf-8-sig'))
def sheet(items,name):
    out=Image.new('RGB',(1600,440*((len(items)+3)//4)),(35,36,34));d=ImageDraw.Draw(out)
    for i,(label,path) in enumerate(items):
        x=i%4*400;y=i//4*440;im=Image.open(R/path).convert('RGBA');im.thumbnail((390,397),Image.Resampling.LANCZOS)
        bg=Image.new('RGBA',(390,397),(98,96,89,255));bg.alpha_composite(im,((390-im.width)//2,(397-im.height)//2))
        out.paste(bg.convert('RGB'),(x+5,y+35));d.text((x+6,y+6),label[:42],font=font,fill='white')
    out.save(O/name,quality=94)
assets=read('art/chapter01/completion-v1/manifest.json')['assets']
sheet([(a['id'],a['native']) for a in assets],'current-native-alpha.jpg')
pts={'sejin-dialogue':(570,1430),'camera-handover':(1079,616),'suhyeok-retrieve':(250,806),'wrench':(136,607),'suhyeok-care':(690,1014)}
checks=[]
for a in assets:
    if a['id'] in pts:
        im=Image.open(R/a['native']).convert('RGBA');pt=pts[a['id']];checks.append({'path':a['native'],'point':pt,'alpha':im.getpixel(pt)[3]})
(O/'alpha-sample-checks.json').write_text(json.dumps(checks,indent=2)+'\n',encoding='utf8')
m=read('design/ui/ch00-01/memory-dialogue.json')
items=[('memory ground',m['background']),('feet paused',m['feet']),('handhold current v2',m['handoff_preview'])]
items += [(k,v['png']) for k,v in m['portraits'].items()]
sheet(items,'current-opening-assets.jpg')
# Check enclosed negative space, not just exterior transparency.
p=m['portraits']['suhyeok']['png'];im=Image.open(R/p).convert('RGBA')
pts2=[(775,1100),(790,1190),(265,1100),(240,1180),(220,1340),(100,100)]
(O/'dialogue-alpha-samples.json').write_text(json.dumps({'path':p,'samples':[{'point':pt,'rgba':im.getpixel(pt)} for pt in pts2]},indent=2)+'\n',encoding='utf8')
out=Image.new('RGB',(800,430),(30,32,35));d=ImageDraw.Draw(out)
for i,color in enumerate([(40,80,100,255),(200,180,145,255)]):
    crop=im.crop((720,1000,860,1320));bg=Image.new('RGBA',crop.size,color);bg.alpha_composite(crop);out.paste(bg.convert('RGB'),(30+i*400,65))
    d.text((20+i*400,15),'팔과 몸 사이 / 원본 1:1',font=font,fill='white')
d.text((190,140),'체크무늬\nalpha 255',font=font,fill='white')
out.save(O/'suhyeok-dialogue-alpha-defect.png')
paths=['art/chapter01/dog-v2/review/01-L5-wary.png','art/chapter01/dog-v2/review/02-L5-water-placed.png','art/chapter01/completion-v1/review/E12-L5-place-water.png','art/chapter01/dog-v2/review/03-L5-relaxed.png']
fixed_rect=(550,80,1100,380);ref=np.array(Image.open(R/paths[0]).convert('RGB').crop(fixed_rect));fixed=[]
out=Image.new('RGB',(1100,860),(30,32,35));d=ImageDraw.Draw(out)
for i,path in enumerate(paths):
    crop=Image.open(R/path).convert('RGB').crop(fixed_rect);delta=int(np.any(np.array(crop)!=ref,axis=2).sum());fixed.append({'path':path,'rect_xyxy':fixed_rect,'changed_pixels_vs_first':delta})
    x=i%2*550;y=i//2*430;out.paste(crop,(x,y+30));d.text((x+5,y+3),Path(path).stem,font=font,fill='white')
out.save(O/'house-fixed-furniture-comparison.png')
(O/'house-fixed-region-checks.json').write_text(json.dumps(fixed,indent=2)+'\n',encoding='utf8')
print(json.dumps(checks))
