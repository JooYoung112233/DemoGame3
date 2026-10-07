from pathlib import Path
from PIL import Image
import json
root=Path('아트/정수리-질감수정-v10')
rows=[]
for part in json.loads((root/'manifest.json').read_text(encoding='utf-8')):
    psd=Image.open(root/(part['id']+'-master-v010.psd')).convert('RGBA')
    png=Image.open(root/'PNG'/(part['id']+'.png')).convert('RGBA')
    okay=psd.size==png.size and psd.tobytes()==png.tobytes()
    rows.append({'id':part['id'],'size':psd.size,'psdCompositeExact':okay,'layersInSource':part['layers']})
    assert okay,part['id']
(root/'psd-pillow-validation.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'files':len(rows),'allCompositePixelsExact':True}))
