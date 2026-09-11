"""Six location repaints, native PSDs and existing event compositions. No UI changes."""
from pathlib import Path
import ast, io, re, struct, json, hashlib
import numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/chapter00-01/background-harmony-v2'
qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
def read(p):return Image.open(ROOT/p).convert('RGBA')
def js(p):return json.loads((ROOT/p).read_text(encoding='utf8'))
def rel(p):return p.relative_to(ROOT).as_posix()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
jobs=js(rel(OUT/'jobs.json')); backgrounds=[];scenes=[];protected={}
def protect(p):protected.setdefault(p,sha(p))
def merge(layers):
    im=Image.new('RGBA',(1672,941))
    for _,pic,xy,visible in layers:
        if visible:im.alpha_composite(pic,xy)
    return im
def comparison(name,before,after):
    sheet=Image.new('RGB',(1672,495),(35,34,30));d=ImageDraw.Draw(sheet)
    for i,(label,im) in enumerate([('BEFORE',before),('AFTER',after)]):
        sheet.paste(im.convert('RGB').resize((836,471),Image.Resampling.LANCZOS),(i*836,24));d.text((i*836+12,7),label,fill='white')
    sheet.save(OUT/'review'/f'{name}-comparison.jpg',quality=94)

for j in jobs:
    original=j['source'];protect(original);old=read(original);path=OUT/'sources'/f"{j['slug']}.png";base=read(rel(path))
    check(j['id']+':native_canvas',base.size==old.size==(1672,941))
    check(j['id']+':opaque_native',base.getchannel('A').getextrema()==(255,255))
    layers=[('previous_base_hidden',old,(0,0),False),('repainted_base',base,(0,0),True)];copies=[]
    if j['id']!='L5':
        lm=js(f"art/chapter01/{j['slug']}-v1/manifest.json")
        for s in lm.get('layers',lm.get('foreground_layers',[])):
            protect(s['path']);mask=read(s['path']).getchannel('A');x,y,w,h=s['rect']
            check(j['id']+':copy_mask_size:'+s['id'],mask.size==(w,h))
            pic=base.crop((x,y,x+w,y+h));pic.putalpha(mask);p=OUT/'layers'/f"{j['slug']}-{s['id']}.png";pic.save(p)
            # Hidden by default: enable only above characters when this occlusion is required.
            layers.append((s['id']+'_copy',pic,(x,y),False))
            copies.append({'id':s['id'],'path':rel(p),'rect':s['rect'],'visible':False,'alpha_source':s['path'],'scope':'same-position foreground copy; no background reconstruction'})
    merged=merge(layers);check(j['id']+':base_psd_preserves_native_pixels',np.array_equal(np.asarray(base),np.asarray(merged)))
    p=OUT/'psd'/f"{j['slug']}-native.psd";psd(p,layers,merged)
    comparison(j['slug'],old,base)
    backgrounds.append({'id':j['id'],'name':j['name'],'source':rel(path),'previous':original,'prompt':rel(path.with_suffix('.prompt.txt')),'generator':'built-in image_gen','sha256':sha(rel(path)),'native_size':list(base.size),'target_size':[3840,2160],'target_met':False,'coordinate_scale':[1,1],'psd':rel(p),'foreground_copies':copies})
    print('Native PSD:',j['id'],flush=True)

completion=js('art/chapter01/completion-v1/manifest.json')
inputscenes=[s for s in completion['scenes'] if s.get('location') in {j['id'] for j in jobs}]
for path in ['art/chapter01/sejin-v1/manifest.json','art/chapter01/first-photo-v1/manifest.json']:
    s=js(path)['scene'];s['location']=s['id'][:2];inputscenes.append(s)
for s in inputscenes:
    bg=next(b for b in backgrounds if b['id']==s['location']);base=read(bg['source']);old=read(s['background']);protect(s['review'])
    extras=[]
    for l in s['layers']:
        protect(l['path']);pic=read(l['path']);x,y,w,h=l['rect'];check(s['id']+':layer_size:'+l['id'],pic.size==(w,h))
        extras.append((l['id'],pic,(x,y),l.get('visible',True)))
    original=merge([('old',old,(0,0),True),*extras]);prior=read(s['review'])
    check(s['id']+':prior_reproduced_from_layers',np.array_equal(np.asarray(original),np.asarray(prior)))
    layers=[('soft_background',base,(0,0),True),*extras];result=merge(layers)
    check(s['id']+':opaque_scene',result.getchannel('A').getextrema()==(255,255))
    p=OUT/'review'/f"{s['id']}.png";result.save(p);psdp=OUT/'psd'/f"{s['id']}.psd";psd(psdp,layers,result)
    scenes.append({'id':s['id'],'location':s['location'],'background':bg['source'],'canvas':[1672,941],'review':rel(p),'psd':rel(psdp),'previous_review':s['review'],'layers':s['layers'],'note':'Original separate characters/props/shadows and placements retained; only background changed.'})
    comparison(s['id'],prior,result)
    print('Scene PSD:',s['id'],flush=True)

representatives=['E07-L2-sejin-repair','E19-L3-owned-goods','E08-L4-retrieve','E12-L5-place-water','L6-first-shoot','E25-L7-road-choice']
for name,items in [('backgrounds',[(b['id']+' '+b['name'],b['source']) for b in backgrounds]),('scenes',[(i,next(s['review'] for s in scenes if s['id']==i)) for i in representatives])]:
    sheet=Image.new('RGB',(1672,1485),(35,34,30));d=ImageDraw.Draw(sheet)
    try:font=__import__('PIL.ImageFont',fromlist=['truetype']).truetype('C:/Windows/Fonts/malgun.ttf',20)
    except OSError:font=None
    for i,(label,path) in enumerate(items):
        x=i%2*836;y=i//2*495;sheet.paste(read(path).convert('RGB').resize((836,471),Image.Resampling.LANCZOS),(x,y+24));d.text((x+12,y+1),label,fill='white',font=font)
    sheet.save(OUT/'review'/f'{name}-contact.jpg',quality=94)
for p,h in protected.items():check('original_preserved:'+p,sha(p)==h)
for start in range(0,len(scenes),6):
    group=scenes[start:start+6];sheet=Image.new('RGB',(1672,495*((len(group)+1)//2)),(35,34,30));d=ImageDraw.Draw(sheet)
    for i,s in enumerate(group):
        x=i%2*836;y=i//2*495;sheet.paste(read(s['review']).convert('RGB').resize((836,471),Image.Resampling.LANCZOS),(x,y+24));d.text((x+12,y+7),s['id'],fill='white')
    sheet.save(OUT/'review'/f'all-scenes-{start//6+1}.jpg',quality=94)
dump(OUT/'manifest.json',{'id':'background-harmony-v2','status':'remaining-six-locations-art-review','previous_batch':'art/chapter00-01/background-harmony-v1/manifest.json','backgrounds':backgrounds,'scenes':scenes,'limitations':['All six native outputs remain1672x941, below3840x2160 target; no upscale claimed.','Generated compositions visually preserve layout, not pixel-identical original contours.','Only listed foreground copies are isolated; background furniture remains baked in native painting.','Scene PSD characters and props are placement-size; native source masters remain in original bundles.','Active runtime/storyboard references are not globally replaced; this manifest records review-ready versions.','Unity and Photoshop application testing not performed.']})
dump(OUT/'validation.json',{'passed':all(q['pass'] for q in qa),'checks':qa})
print(json.dumps({'backgrounds':len(backgrounds),'scenes':len(scenes),'checks':len(qa),'passed':all(q['pass'] for q in qa)}))
