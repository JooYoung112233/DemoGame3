"""Connect revised art without changing dialogue/UI/layout or frozen handoff."""
from pathlib import Path
import json, shutil, importlib.util
import numpy as np
from PIL import Image,ImageDraw,ImageChops
R=Path(__file__).resolve().parents[1];O=R/'art/style-revision/soft-storybook-v1'
jobs=json.loads((O/'jobs.json').read_text(encoding='utf-8'))
def write(p,data):p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def rel(p):return p.relative_to(R).as_posix()
mapping={};records=[]
for j in jobs:
    im=Image.open(O/'sprites'/f"{j['id']}.png").convert('RGBA')
    if j['category']=='dialogue':
        name={'suhyeok-dialogue':'suhyeok-response','soi-dialogue':'soi-answer','seoyeon-dialogue':'seoyeon-speaking'}[j['id']]
        path=R/'art/chapter00/memory/dialogue-v1'/f'{name}-soft-v3.png'
        shutil.copy2(O/'sprites'/f"{j['id']}.png",path)
    else:
        path=O/'sprites'/f"{j['id']}-trim.png"
        im.crop(im.getchannel('A').getbbox()).save(path)
    mapping[j['previous']]=rel(path)
    records.append({'id':j['id'],'previous':j['previous'],'replacement':rel(path),'native_master':rel(O/'sprites'/f"{j['id']}.png"),'psd':rel(O/'psd'/f"{j['id']}.psd"),'source':rel(O/j['source']),'role':j['category'],'status':j['status']})
# Investigation v1 is the same pose with older alpha; all editable consumers use current replacement.
mapping['art/chapter01/extra-poses/suhyeok-investigate-v1.png']=mapping['art/chapter01/extra-poses/suhyeok-investigate-v2.png']
# Disjoint native-resolution body/hands split at unchanged tabletop y=432.
spec=importlib.util.spec_from_file_location('softprep',R/'tools/prepare-soft-storybook.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
full=Image.open(O/'sprites/suhyeok-seated-trim.png').convert('RGBA');alpha=full.getchannel('A')
split=round(full.height*(432-289)/155)
back=alpha.copy();ImageDraw.Draw(back).rectangle((0,split,full.width,full.height),fill=0)
front=ImageChops.subtract(alpha,back)
body=full.copy();body.putalpha(back);hands=full.copy();hands.putalpha(front)
assert ImageChops.multiply(back,front).getbbox() is None
assert ImageChops.add(back,front).tobytes()==alpha.tobytes()
for part,im in [('body',body),('hands',hands)]:
    path=O/'sprites'/f'suhyeok-seated-{part}.png';im.save(path)
    mapping[f'art/chapter01/layers/characters/suhyeok-seated-{part}-v1.png']=rel(path)
mod.psd(O/'psd/suhyeok-seated-split.psd',[('body_behind_table',body,(0,0),True),('hands_above_table',hands,(0,0),True)],full)
def walk(v):
    if isinstance(v,dict):return {k:walk(x) for k,x in v.items()}
    if isinstance(v,list):return [walk(x) for x in v]
    if isinstance(v,str):return mapping.get(v,v)
    return v
changed=[]
for folder in ['design/chapter01','design/interaction']:
    for p in (R/folder).glob('*.json'):
        data=json.loads(p.read_text(encoding='utf-8'));new=walk(data)
        if new!=data:write(p,new);changed.append(rel(p))
dialog=R/'design/ui/ch00-01/memory-dialogue.json';data=json.loads(dialog.read_text(encoding='utf-8'))
for who in ['suhyeok','soi','seoyeon']:data['portraits'][who]['png']=mapping.get(data['portraits'][who]['png'],data['portraits'][who]['png'])
assert data['suppressed_portraits']==['seoyeon'];write(dialog,data)
assets=R/'design/ui/ch00-01/C0-02-assets.json';data=json.loads(assets.read_text(encoding='utf-8'))
for who in ['suhyeok','soi','seoyeon']:
    ident=who+'-dialogue';record=next(r for r in records if r['id']==ident)
    meta=json.loads((O/f'{ident}.json').read_text(encoding='utf-8'))
    data['art'][who]={'png':record['replacement'],'source':record['source'],'psd':record['psd'],'native_size':meta['native_size'],'native_alpha_bounds':meta['alpha_bounds'],'status':'soft-storybook-revision; suppressed in memory' if who=='seoyeon' else 'soft-storybook-revision','processing':meta['alpha_method']}
write(assets,data)
camper=R/'design/chapter00/camper-assets-v1.json';data=json.loads(camper.read_text(encoding='utf-8'))
repair=data['assets']['suhyeok-flashlight-repair-v2'];meta=json.loads((O/'suhyeok-repair.json').read_text(encoding='utf-8'))
repair.update(path=mapping.get(repair['path'],repair['path']),source_size=meta['native_size'],crop=meta['alpha_bounds'],size=list(Image.open(O/'sprites/suhyeok-repair-trim.png').size),method=meta['alpha_method'])
data['reused_assets']=[mapping.get(x,x) for x in data.get('reused_assets',[])];write(camper,data)
write(O/'replacement-map.json',{'style_reference':'art/style-revision/2026-09-10/suhyeok-storybook-face-study-source-v6.png','replacements':records,'path_map':mapping,'changed_manifests':changed,'seated_split_y_native':split,'seated_rect_unchanged':[840,289,109,155],'frozen_handoff_modified':False})
print('Connected',len(records),'art records;',len(changed),'editable manifests; seated split verified.')
