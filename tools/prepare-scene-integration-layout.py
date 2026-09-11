from pathlib import Path
import json
from PIL import Image
R=Path(__file__).resolve().parents[1];O=R/'art/chapter00-01/scene-integration-v1'
m=json.loads((R/'art/chapter01/completion-v1/manifest.json').read_text(encoding='utf8'))
s=next(s for s in m['scenes'] if s['id']=='E12-L5-place-water')
im=Image.open(R/'art/chapter00-01/background-harmony-v2/sources/house.png').convert('RGBA');layers=[]
for l in s['layers']:
    x,y,w,h=l['rect'];dog=l['id'].startswith('dog');scale=1.2 if dog else 1.35;anchor=(423,551) if dog else (619,587)
    rect=[round(anchor[0]+(x-anchor[0])*scale),round(anchor[1]+(y-anchor[1])*scale),round(w*scale),round(h*scale)]
    piece=Image.open(R/l['path']).convert('RGBA').resize(tuple(rect[2:]),Image.Resampling.LANCZOS)
    im.alpha_composite(piece,tuple(rect[:2]));p=O/'layers'/f"layout-{l['id']}.png";piece.save(p)
    layers.append({'id':l['id'],'previous':l['path'],'path':p.relative_to(R).as_posix(),'previous_rect':l['rect'],'rect':rect,'scale':scale})
im.save(O/'sources/house-proportion-layout.png')
(O/'layout.json').write_text(json.dumps({'purpose':'proportion-only layout reference, not final repaint or restored resolution','background':'art/chapter00-01/background-harmony-v2/sources/house.png','layers':layers},ensure_ascii=False,indent=2),encoding='utf8')
