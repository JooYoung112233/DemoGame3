"""Package original generated pixels without retouching or resampling.

PSD layers are independent WHOLE creatures, not animation body parts.
The existing serializer reopens composites and checks every layer channel.
"""
from pathlib import Path
import hashlib, json, html
from PIL import Image
import numpy as np
from psd_layers import psd

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '아트/크리쳐-v1'
manifest = json.loads((OUT/'generation-manifest.json').read_text(encoding='utf-8'))
entries = manifest['entries']
layers = []
checks = []
(OUT/'PSD').mkdir(exist_ok=True)
for entry in entries:
    name = entry['id']
    path = OUT/'원본'/f'{name}.png'
    runtime = ROOT/'Assets/Art/Creatures/V1'/f'{name}.png'
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest == hashlib.sha256(Path(entry['source']).read_bytes()).hexdigest()
    assert digest == hashlib.sha256(runtime.read_bytes()).hexdigest()
    im = Image.open(path)
    assert im.mode == 'RGBA'
    a = np.asarray(im.getchannel('A'))
    assert a.min() == 0 and a.max() == 255
    substantial = np.argwhere(a >= 128)
    top,left = substantial.min(axis=0)
    bottom,right = substantial.max(axis=0)+1
    checks.append(dict(id=name,name=entry['name'],size=list(im.size),mode=im.mode,
        sha256=digest,transparent_fraction=float((a==0).mean()),
        visible_bounds_alpha128=[int(left),int(top),int(right),int(bottom)],
        touches_edge=bool(left==0 or top==0 or right==im.width or bottom==im.height),
        native_height_below_1536=im.height<1536))
    layers.append((name, im.copy(), (0,0), False))

for start in range(0,12,4):
    group=layers[start:start+4]
    group[0]=(group[0][0],group[0][1],group[0][2],True)
    target=OUT/'PSD'/f'creatures-{start+1:02d}-{start+4:02d}.psd'
    psd(target,group,group[0][1])
    print('Verified PSD:',target.name,'4 whole-creature layers; native pixels retained')

(OUT/'validation.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
cards=[]
for e in entries:
    src='원본/'+e['id']+'.png'
    cards.append(f'<figure><a href="{src}"><img src="{src}" alt="{html.escape(e["name"])}"></a><figcaption>{html.escape(e["name"])} · {e["id"][:2]}</figcaption></figure>')
page='''<!doctype html><meta charset="utf-8"><title>이웃 아닌 — 크리쳐 원화 12종</title>
<style>body{background:#233436;color:#e9dfcb;font:18px sans-serif;margin:32px}main{display:grid;grid-template-columns:repeat(4,1fr);gap:20px}figure{margin:0;background:#b0b4aa;padding:12px;color:#202827}img{width:100%;height:300px;object-fit:contain}figcaption{padding-top:12px}p{line-height:1.6}</style>
<h1>이웃 아닌 — 크리쳐 원화 12종</h1><p>외형 1차 검토 · 개별 투명 PNG 1086×1448 · 클릭하면 원본<br>색·형태 비교용이며 게임 화면이나 실제 크기 비율이 아닙니다. 가칭이며 전투 규칙은 미정입니다.</p><main>'''+''.join(cards)+'</main>'
(OUT/'index.html').write_text(page,encoding='utf-8')
print(json.dumps(checks,ensure_ascii=False,indent=2))
