from pathlib import Path
from PIL import Image
import json
root=Path('아트/승인형상-대표샘플-v12')
rows=[]
for part in json.loads((root/'manifest.json').read_text(encoding='utf-8')):
    psd=Image.open(root/(part['id']+'-study-v012.psd')).convert('RGBA')
    png=Image.open(root/'PNG'/(part['id']+'.png')).convert('RGBA')
    okay=psd.size==png.size and psd.tobytes()==png.tobytes()
    rows.append({'id':part['id'],'size':psd.size,'psdCompositeExact':okay,'layersInSource':part['layers']})
    assert okay,part['id']
(root/'psd-pillow-validation.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'files':len(rows),'allCompositePixelsExact':True}))
