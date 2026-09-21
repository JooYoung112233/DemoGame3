"""Package untouched generated backgrounds; PSD has one honest flat background layer."""
from pathlib import Path
import ast, io, re, struct, json, hashlib
import numpy as np
from PIL import Image

R=Path(__file__).resolve().parents[1]
O=R/'art/chapter01/region-sites-v1'
qa=[]
tree=ast.parse((R/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
assets=[]
for ident in ['L8','L9','L10']:
    path=O/f'{ident}-source.png'
    src=Image.open(path).convert('RGBA')
    psd(O/f'{ident}-native.psd',[('fixed_background',src,(0,0),True)],src)
    unity=R/f'Demo3/Assets/_Project/Resources/Live49/Stages/region-{ident}.png'
    check(ident+':unity_original_bytes',path.read_bytes()==unity.read_bytes())
    assets.append({'id':ident,'source':path.name,'native_size':list(src.size),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'upscaled':False,'psd_layers':['fixed_background'],'separate_objects':False,'unity_resource':f'Live49/Stages/region-{ident}'})
(O/'manifest.json').write_text(json.dumps({'assets':assets,'target_width':3840,'limitation':'Native 1672x941 is below the target; no upscaling or recovered-detail claim. PSD holds one flat background, not separated furnishings.'},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(O/'validation.json').write_text(json.dumps(qa,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'checks':len(qa),'pass':all(v['pass'] for v in qa)}))
