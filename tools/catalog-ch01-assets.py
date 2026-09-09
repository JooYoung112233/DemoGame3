from pathlib import Path
import json,hashlib
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
data=json.loads((ROOT/'design/chapter01/gallery-scenes.json').read_text(encoding='utf-8'))
refs={};bases=set()
for sc in data['scenes']:
    bases.add(sc['base']);refs.setdefault(sc['base'],set()).add(sc['id'])
    layers=sc['slots']+sc.get('actors',[])+sc.get('pages',[])+([sc['foreground']] if sc.get('foreground') else [])
    ids={l['id'] for l in layers}
    for st in sc['presets']:assert set(st['visible'])<=ids,(sc['id'],st['id'])
    for l in layers:
        refs.setdefault(l['asset'],set()).add(sc['id'])
        x,y,w,h=l['rect'];assert min(x,y)>=0 and w>0 and h>0 and x+w<=1672 and y+h<=941,(sc['id'],l['id'])
refs.setdefault(data['stationery'],set()).add('letter-reader')
provenance={}
for folder in ['shared-props','extra-poses','letter']:
    records=json.loads((ROOT/f'art/chapter01/{folder}/manifest-v1.json').read_text(encoding='utf-8'))
    if isinstance(records,dict):records=records.get('assets',[])
    for r in records:
        if 'asset' in r:provenance[r['asset']]=r
files=set(refs)|set(provenance)
for folder in ['kitchen','dog','shared-props','extra-poses','store','letter']:
    for file in (ROOT/f'art/chapter01/{folder}').glob('*-v1.png'):
        if Image.open(file).mode=='RGBA':files.add(file.relative_to(ROOT).as_posix())
records=[]
for asset in sorted(files):
    path=ROOT/asset;im=Image.open(path)
    record=dict(asset=asset,size=list(im.size),mode=im.mode,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),usedIn=sorted(refs.get(asset,[])),role='fixed-base' if asset in bases else 'sprite')
    if asset not in bases:
        assert im.mode=='RGBA',asset
        alpha=im.getchannel('A');assert alpha.getextrema()==(0,255),(asset,alpha.getextrema())
        record.update(alphaRange=list(alpha.getextrema()),alphaBounds=list(alpha.getbbox()),transparentPixels=alpha.histogram()[0])
    if asset in provenance:record['extraction']=provenance[asset]
    records.append(record)
out=dict(version=1,status='first-pass visual resource pack; not final game',canvas=dict(width=1672,height=941),sceneCount=len(data['scenes']),stateCount=sum(len(sc['presets']) for sc in data['scenes']),fixedBaseCount=len(bases),spriteCount=sum(r['role']=='sprite' for r in records),assets=records)
(ROOT/'design/chapter01/asset-catalog.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f"{out['fixedBaseCount']} fixed bases, {out['spriteCount']} RGBA sprites; all paths, bounds and nontrivial alpha checked.")
