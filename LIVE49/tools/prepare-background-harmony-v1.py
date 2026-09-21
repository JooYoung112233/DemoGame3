"""Art-only background style candidates. Existing native assets remain untouched."""
from pathlib import Path
import ast, io, re, struct, json, hashlib
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'art/chapter00-01/background-harmony-v1'
qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
def read(p): return Image.open(ROOT/p).convert('RGBA')
def js(p): return json.loads((ROOT/p).read_text(encoding='utf8'))
def rel(p): return p.relative_to(ROOT).as_posix()
def dump(p,v): p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p): return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def tone(im,gain):
    a=np.array(im);a[:,:,:3]=np.clip(a[:,:,:3].astype(float)*gain,0,255).astype('uint8');return Image.fromarray(a)
def placed(name,path,rect,gain=None):
    x,y,w,h=rect;im=read(path).resize((w,h),Image.Resampling.LANCZOS)
    return (name,tone(im,gain) if gain else im,(x,y),True)
def compose(layers):
    im=Image.new('RGBA',(1672,941))
    for _,piece,xy,visible in layers:
        if visible:im.alpha_composite(piece,xy)
    return im
def save(ident,layers,context='art-review'):
    result=compose(layers);p=OUT/'review'/f'{ident}.png';result.save(p)
    slots=[]
    for name,piece,xy,visible in layers:
        lp=OUT/'layers'/f'{ident}-{name}.png';piece.save(lp)
        slots.append({'id':name,'path':rel(lp),'rect':[*xy,*piece.size],'visible':visible})
    psd(OUT/'psd'/f'{ident}.psd',layers,result)
    check(ident+':opaque_scene',result.getchannel('A').getextrema()==(255,255))
    states.append({'id':ident,'preview':rel(p),'psd':rel(OUT/'psd'/f'{ident}.psd'),'context':context,'layers':slots})
    return result

native=js('art/chapter00-01/narrative-art-v1/manifest.json')
cm=js('design/chapter00/camper-assets-v1.json')
store=next(s for s in js('design/chapter01/gallery-scenes.json')['scenes'] if s['id']=='store')
protected={p:sha(p) for p in {cm['base'],store['base'],*[a['native'] for a in native['assets']],*[a['path'] for a in cm['assets'].values()],*[s['asset'] for s in store['slots']]}}
states=[];backgrounds=[]
for name,original,version in [('camper',cm['base'],'v1'),('store',store['base'],'v2')]:
    p=OUT/'sources'/f'{name}-soft-base-{version}.png';im=read(rel(p));old=read(original)
    check(name+':native_canvas_matches_placement',im.size==old.size==(1672,941))
    psd(OUT/'psd'/f'{name}-soft-base-{version}.psd',[('previous_base_hidden',old,(0,0),False),('repainted_background',im,(0,0),True)],im)
    backgrounds.append({'id':name,'source':rel(p),'previous':original,'prompt':rel(p.with_suffix('.prompt.txt')),'psd':rel(OUT/'psd'/f'{name}-soft-base-{version}.psd'),'native_size':list(im.size),'placement_canvas':[1672,941],'coordinate_scale':[1,1],'target_long_edge':3840,'target_met':False,'sha256':sha(rel(p))})
warm=read(backgrounds[0]['source'])
oldwarm=read(cm['base']);olddark=read('art/chapter00/camper/layers/camper-outage-stable-v1.png')
# Reuse the previously authored light-state gain, without regenerating dark geometry.
gain=(np.asarray(olddark)[:,:,:3].astype(float)+1)/(np.asarray(oldwarm)[:,:,:3].astype(float)+1)
dark=tone(warm,np.clip(gain,0,1.05));dark.save(OUT/'layers/camper-outage-derived.png')

def actors(ident):
    st=next(s for s in native['states'] if s['id']==ident)
    return [(s['id'],read(s['path']),tuple(s['rect'][:2]),s['visible']) for s in st['layers'] if s['id'] in ['suhyeok-seated','soi-seated','suhyeok_drawing']]
def props(packed=False,night=False,fixed=False):
    result=[]
    for key in ['water-packed' if packed else 'water-before','blanket-packed' if packed else 'blanket-before','panel-fixed' if fixed else 'panel-loose','flashlight-stored']:
        name,rect=cm['placements'][key]
        result.append(placed(key,cm['assets'][name]['path'],rect,[.39,.47,.66] if night else None))
    return result
def foreground(layers):
    result=[]
    for name,im,xy,visible in layers:
        hand=im.copy();a=np.array(hand.getchannel('A'));a[:max(0,432-xy[1])]=0;hand.putalpha(Image.fromarray(a))
        result.append((name+'_forearms_copy',hand,xy,visible))
    return result

for ident,isnight in [('C0-current',False),('C0-outage',True)]:
    base=dark if isnight else warm;people=actors(ident)
    layers=[('soft_background',base,(0,0),True),*people,('matching_table_occlusion',base.crop((835,432,1092,633)),(835,432),True)]
    for name,path,rect in [('book','art/chapter01/layers/pages/sketchbook-blank-v1.png',[904,454,102,73]),('mug','art/chapter01/layers/sprites/mug-source-v1.png',[1010,480,53,52])]:
        layers.append(placed(name,path,rect,[.39,.47,.66] if isnight else None))
    layers+=foreground(people)+props(packed=isnight,night=isnight)
    save(ident,layers)

for ident in ['E06-drawing','E06-drawing-truth']:
    st=next(s for s in native['states'] if s['id']==ident)
    layers=[('soft_background',warm,(0,0),True),*props(packed=True,fixed=True)]
    for s in st['layers'][1:]:
        if s['id']=='existing_table':layers.append(('matching_table_occlusion',warm.crop((835,432,1092,633)),(835,432),True))
        else:layers.append((s['id'],read(s['path']),tuple(s['rect'][:2]),s['visible']))
    save(ident,layers,'ending-objective-private' if ident.endswith('truth') else 'art-review')

for presetid in ['stationery','pencils-taken']:
    preset=next(p for p in store['presets'] if p['id']==presetid)
    layers=[('soft_background',read(backgrounds[1]['source']),(0,0),True)]
    for s in sorted(store['slots'],key=lambda x:x.get('order',0)):
        if s['id'] not in preset['visible']:continue
        if s.get('contactShadow'):
            x,y,rx,ry,alpha=s['contactShadow'];sh=Image.new('RGBA',(1672,941));ImageDraw.Draw(sh).ellipse((x-rx,y-ry,x+rx,y+ry),fill=(20,20,20,round(alpha*255)))
            layers.append((s['id']+'_contact_shadow',sh.filter(ImageFilter.GaussianBlur(2)),(0,0),True))
        layers.append(placed(s['id'],s['asset'],s['rect'],s.get('tone')))
    save('L1-'+presetid,layers)

# Show original-size crops; do not present enlargement as recovered detail.
comparisons=[('C0-current','art/chapter00-01/narrative-art-v1/review/C0-current.png',(795,270,1105,560)),('E06-drawing','art/chapter00-01/narrative-art-v1/review/E06-drawing.png',(795,270,1105,560)),('L1-stationery','art/style-revision/soft-storybook-v1/review/C1-store-stationery.png',(1030,345,1430,650))]
for ident,prior,rect in comparisons:
    before=read(prior);after=read(rel(OUT/'review'/f'{ident}.png'))
    sheet=Image.new('RGB',(1672,982),(37,35,31));d=ImageDraw.Draw(sheet)
    for i,(label,frame) in enumerate([('BEFORE',before),('AFTER',after)]):
        sheet.paste(frame.convert('RGB').resize((836,471),Image.Resampling.LANCZOS),(i*836,24));d.text((i*836+12,7),label,fill='white')
        crop=frame.crop(rect).convert('RGB');sheet.paste(crop,(i*836+(836-crop.width)//2,535));d.text((i*836+12,510),'1:1 native detail',fill='white')
    sheet.save(OUT/'review'/f'{ident}-comparison.jpg',quality=94)
pairA=read(rel(OUT/'review/E06-drawing.png'));pairB=read(rel(OUT/'review/E06-drawing-truth.png'))
delta=np.any(np.asarray(pairA)!=np.asarray(pairB),axis=2);allowed=np.zeros(delta.shape,bool);allowed[330:445,940:1048]=True
check('truth_pair_only_soi_changes',bool(np.any(delta)) and not np.any(delta&~allowed))
for p,h in protected.items():check('source_preserved:'+p,sha(p)==h)
dump(OUT/'manifest.json',{'id':'background-harmony-v1','status':'review-candidate-not-global-replacement','backgrounds':backgrounds,'states':states,'native_character_manifest':'art/chapter00-01/narrative-art-v1/manifest.json','reused_prop_manifest':'design/chapter00/camper-assets-v1.json','reused_store_layers':'design/chapter01/gallery-scenes.json','limitations':['Both generated native backgrounds are 1672x941, below 3840px target. No upscale is claimed.','Background furniture is a flat painted layer. Actual PSD layers separate background, existing characters, props, tabletop occlusion and forearm copies.','Generated paintings preserve composition visually, not pixel-identical contours. Existing placements are unchanged.','Night base derives from the existing lighting gain; it is not a separately painted native source.','Store investigation pose and its existing color tint are reused unchanged.','Other locations and other scene states have not been migrated. Existing active storyboard references remain unchanged pending this visual review.','Unity and Photoshop application checks were not performed.']})
dump(OUT/'validation.json',{'passed':all(x['pass'] for x in qa),'checks':qa})
print(json.dumps({'backgrounds':2,'scene_psd':len(states),'background_psd':2,'checks':len(qa),'passed':all(x['pass'] for x in qa)}))
