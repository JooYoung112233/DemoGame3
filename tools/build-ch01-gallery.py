from pathlib import Path
import json,base64,io,sys
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
dest=Path(sys.argv[1]);dest.mkdir(parents=True,exist_ok=True)
scenes=[json.loads((ROOT/f'design/chapter01/{id}.json').read_text(encoding='utf-8')) for id in ['kitchen','store','byeolddongi','letter','evening']]
drawing=json.loads((ROOT/'design/interaction/ch01-first-drawing-layers-v1.json').read_text(encoding='utf-8'))
story=json.loads((ROOT/'design/interaction/ch01-first-drawing-v1.json').read_text(encoding='utf-8'))
drawing['id']='drawing';drawing['title']='처음 그린 바다';drawing['presets']=[]
for i,b in enumerate(story['beats']):
    visible=['pencil-cup','sketchbook','mug','suhyeok-seated-body','suhyeok-seated-hands']
    visible+=['soi-'+b['pose']+'-body','soi-'+b['pose']+'-hands']
    if b['page']!='blank':visible+=['sea-'+b['page']]
    drawing['presets'].append(dict(id=b['id'],label=str(i+1),visible=visible,focus=[.575,.44,2.9] if b['camera']=='pair' else [.572,.525,5],line=b['text'],speaker=b['speaker'],active=[]))
scenes.insert(1,drawing)
profile=json.loads((ROOT/'design/chapter01/visual-integration-v2.json').read_text(encoding='utf-8'))
for sc in scenes:
    adjustments=profile['scenes'].get(sc['id'],{})
    for l in sc['slots']+sc.get('actors',[]):l.update(adjustments.get('layers',{}).get(l['id'],{}))
    for s in sc['presets']:s.update(adjustments.get('presets',{}).get(s['id'],{}))
    sc['occluders']=adjustments.get('occluders',[])
revision=json.loads((ROOT/'design/chapter01/visual-revision-v3.json').read_text(encoding='utf-8'))
for sc in scenes:
    adjustments=revision['scenes'].get(sc['id'],{})
    if 'base' in adjustments:sc['base']=adjustments['base']
    sc['slots'].extend(adjustments.get('addSlots',[]))
    for l in sc['slots']+sc.get('actors',[]):l.update(adjustments.get('layers',{}).get(l['id'],{}))
    for s in sc['presets']:s.update(adjustments.get('presets',{}).get(s['id'],{}))
    if 'occluders' in adjustments:sc['occluders']=adjustments['occluders']
stationery='art/chapter01/letter/pink-stationery-v1.png'
data=dict(scenes=scenes,letter=json.loads((ROOT/'design/chapter01/first-letter-text.json').read_text(encoding='utf-8')),stationery=stationery)
specs={stationery:(300,400)}
bases=set()
for sc in scenes:
    specs[sc['base']]=(1672,941);bases.add(sc['base'])
    for l in sc['slots']+sc.get('actors',[])+sc.get('pages',[])+([sc['foreground']] if sc.get('foreground') else []):
        specs[l['asset']]=(max(specs.get(l['asset'],(0,0))[0],min(360,l['rect'][2]*2)),max(specs.get(l['asset'],(0,0))[1],min(440,l['rect'][3]*2)))
assets={}
for file,size in specs.items():
    im=Image.open(ROOT/file);im.thumbnail(size,Image.Resampling.LANCZOS);buff=io.BytesIO()
    if file in bases:im.convert('RGB').save(buff,format='WEBP',quality=86,method=6);mime='webp'
    else:im.save(buff,format='WEBP',quality=88,method=6);mime='webp'
    assets[file]='data:image/'+mime+';base64,'+base64.b64encode(buff.getvalue()).decode('ascii')
html=(ROOT/'design/chapter01/gallery.template.html').read_text(encoding='utf-8').replace('__DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':'))).replace('__ASSETS__',json.dumps(assets,separators=(',',':')))
assert len(html.encode())<1000000,len(html.encode())
(dest/'chapter-one.html').write_text(html,encoding='utf-8')
(ROOT/'design/chapter01/gallery-scenes.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'{len(scenes)} scenes / {sum(len(s["presets"]) for s in scenes)} states / {len(assets)} embedded assets / {len(html.encode())} bytes')
