"""Rebuild current Chapter 0/1 art compositions from separate native assets.
No UI/text changes; old art is preserved. Scene layers stay editable in PSD.
"""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFilter,ImageOps,ImageFont
R=Path(__file__).resolve().parents[1];O=R/'art/chapter00-01/art-polish-v1';qa=[]
tree=ast.parse((R/'tools/build-ui-title-styles.py').read_text(encoding='utf8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
def read(p):return Image.open(R/p).convert('RGBA')
def js(p):return json.loads((R/p).read_text(encoding='utf-8-sig'))
def rel(p):return p.relative_to(R).as_posix()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def trim(im):return im.crop(im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox())
def fit(im,h):im=trim(im);return im.resize((round(im.width*h/im.height),h),Image.Resampling.LANCZOS)
def tone(im,gain):
    a=np.array(im);a[:,:,:3]=np.clip(a[:,:,:3].astype(float)*gain,0,255).astype('uint8');return Image.fromarray(a)
def sh(w,h,alpha=65):
    im=Image.new('RGBA',(w,h),(39,29,21,0));a=Image.new('L',(w,h));ImageDraw.Draw(a).ellipse((1,1,w-2,h-2),fill=alpha);im.putalpha(a.filter(ImageFilter.GaussianBlur(1.5)));return im
def merge(layers):
    im=Image.new('RGBA',(1672,941))
    for _,p,xy,visible in layers:
        if visible:im.alpha_composite(p,xy)
    return im
baseline=O/'input-baseline.json'
if not baseline.exists():
    dump(baseline,{k:js(p) for k,p in {'camper':'design/chapter00/camper-assets-v1.json','story':'design/chapter00/continuation-v1/story.json','gallery':'design/chapter01/gallery-scenes.json'}.items()})
inputs=js(rel(baseline));cm=inputs['camper'];nm=js('art/chapter00-01/narrative-art-v1/manifest.json');completion=js('art/chapter01/completion-v1/manifest.json')
natives=js(rel(O/'native-manifest.json'))['assets'];native={a['id']:read(a['trimmed']) for a in natives}
bg={'HUB':'art/chapter00-01/background-harmony-v1/sources/camper-soft-base-v1.png','L1':'art/chapter00-01/background-harmony-v1/sources/store-soft-base-v2.png'}
for b in js('art/chapter00-01/background-harmony-v2/manifest.json')['backgrounds']:bg[b['id']]=b['source']
warm=read(bg['HUB']);dark=read('art/chapter00-01/background-harmony-v1/layers/camper-outage-derived.png')
scenes=[];mapping={};layer_mapping={}
def save(ident,layers,old_paths=(),context='subjective',note='',source_metadata=None):
    im=merge(layers);check(ident+':opaque',im.getchannel('A').getextrema()==(255,255))
    slots=[]
    for name,p,xy,visible in layers:
        check(ident+':bounds:'+name,xy[0]>=0 and xy[1]>=0 and xy[0]+p.width<=1672 and xy[1]+p.height<=941)
        path=O/'layers'/f'{ident}-{name}.png';p.save(path);slots.append({'id':name,'path':rel(path),'rect':[*xy,*p.size],'visible':visible})
    if source_metadata:
        for old_layer in source_metadata.get('layers',[]):
            for slot in slots:
                if slot['id']==old_layer['id'] and 'condition' in old_layer:slot['condition']=old_layer['condition']
    if ident.startswith('E'):
        for slot in slots:
            if slot['id'] in ['water-packed','blanket-packed']:slot['condition']=slot['id'].replace('-','_')+' == true; review assumes retained Chapter 0 supplies'
    path=O/'review'/f'{ident}.png';im.save(path);psdp=O/'psd'/f'{ident}.psd';psd(psdp,layers,im)
    scenes.append({'id':ident,'review':rel(path),'psd':rel(psdp),'context':context,'layers':slots,'note':note,'previous_reviews':list(old_paths),'source_metadata':source_metadata})
    for p in old_paths:mapping[p]=rel(path)
    print('Scene',ident,flush=True)
    return im
def placed(name,path,rect,gain=None):
    x,y,w,h=rect;p=read(path).resize((w,h),Image.Resampling.LANCZOS)
    return(name,tone(p,gain) if gain else p,(x,y),True)
def cmprop(key,night=False):
    ident,rect=cm['placements'][key];return placed(key,cm['assets'][ident]['path'],rect,[.39,.47,.66] if night else None)
def seated(night=False,father=True,soi=True):
    layers=[]
    for name,h,c,y,on in [('suhyeok-seated',155,894,289,father),('soi-seated',114,994,330,soi)]:
        if not on:continue
        p=fit(native[name],h)
        if night:p=tone(p,[.39,.47,.66])
        layers.append((name,p,(round(c-p.width/2),y),True))
    return layers
def hands(actors):
    out=[]
    for name,p,xy,v in actors:
        p=p.copy();a=np.array(p.getchannel('A'));a[:max(0,432-xy[1])]=0;p.putalpha(Image.fromarray(a));out.append((name+'_forearms',p,xy,v))
    return out
c0keys=['current','packed','outage','investigation','secured','restored','journal','night','water_only','blanket_only']
for key,s in zip(c0keys,cm['states']):
    night=s['light']=='off';base=dark if night else warm;people=seated(night,not s['repair_pose']);layers=[('fixed_background',base,(0,0),True),*people,('table_occlusion',base.crop((835,432,1092,633)),(835,432),True)]
    if not s['book_packed']:layers.append(placed('book','art/chapter01/layers/pages/sketchbook-blank-v1.png',[904,454,102,73],[.39,.47,.66] if night else None))
    layers.append(placed('mug','art/chapter01/layers/sprites/mug-source-v1.png',[1010,480,53,52],[.39,.47,.66] if night else None));layers+=hands(people)
    props=['water-packed' if s['water_packed'] else 'water-before','blanket-packed' if s['blanket_packed'] else 'blanket-before','panel-fixed' if s['connection_secured'] else 'panel-loose']
    if not s['repair_pose']:props.append('flashlight-stored')
    if s['journal_open']:props.append('journal')
    layers += [cmprop(k,night) for k in props]
    if s['repair_pose']:
        if night:
            local=merge([('bg',warm,(0,0),True),cmprop(props[2])]);local.putalpha(Image.open(R/'art/chapter00/camper/layers/flashlight-local-reveal-mask-v1.png'))
            layers += [('flashlight_local',local,(0,0),True),('flashlight_bounce',read('art/chapter00/camper/layers/flashlight-bounce-overlay-v1.png'),(0,0),True)]
        layers.append(('repair_shadow',read('art/chapter00/camper/layers/repair-contact-shadow-v1.png'),(0,0),True))
        aid,rect=cm['placements']['repair'];path=rel(O/'layers/suhyeok-repair-trim.png') if 'suhyeok-repair' in native else cm['assets'][aid]['path'];layers.append(placed('repair_actor',path,rect,[.57,.61,.73] if night else None))
    save('C0-'+key,layers,[inputs['story']['scenes'][key],s['review']],source_metadata=s)
    if key=='current':save('C0-current-truth',[l for l in layers if not l[0].startswith('soi-')],['art/chapter00-01/narrative-art-v1/review/C0-current-truth.png'],'ending-objective-private')

# Generic layer replacement keeps native masters separate from placement copies.
def improve_layer(l,base,sceneid,loc):
    name=l['id'];x,y,w,h=l['rect'];p=read(l['path']);native_id=None;mirror=False;scale=1.0
    match={'suhyeok_with_bag':'suhyeok-retrieve','suhyeok_release_bowl':'suhyeok-care','sejin_repair':'sejin-repair','sejin_standing':'sejin-standing','suhyeok_shoot':'suhyeok-shoot','unnamed_exchange_npc':'exchange-npc','van':'van','dog_wary':'dog-alert','dog_alert':'dog-alert','dog_sit':'dog-sit','dog_rest':'dog-rest','cook':'suhyeok-cook','existing_delivery_pose':'suhyeok-delivery','suhyeok_drawing':'suhyeok-drawing','suhyeok-seated':'suhyeok-seated','soi-seated':'soi-seated','investigate':'suhyeok-drawer'}
    match.update({'investigate':'suhyeok-investigate','resting_dog':'dog-rest'})
    native_id=match.get(name)
    if name in ['sejin_repair','suhyeok_release_bowl']:mirror=True
    if name=='suhyeok_with_bag':scale=1.25
    elif name=='suhyeok_shoot':scale=1.25
    elif name in ['sejin_standing','unnamed_exchange_npc']:scale=1.15
    # Approved house scale applies to this room only, all related dog/bowl states together.
    if loc=='L5' and (name.startswith('dog') or name.startswith('water_bowl') or name.startswith('food_bowl') or name in ['bowl','bowl_shadow','water','suhyeok_release_bowl','care_contact']):
        factor=1.2 if name.startswith('dog') else 1.35
        anchor=(423,551) if name.startswith('dog') else (619,587)
        x,y=round(anchor[0]+(x-anchor[0])*factor),round(anchor[1]+(y-anchor[1])*factor);w,h=round(w*factor),round(h*factor)
    elif name in ['left_foot_contact_shadow','right_foot_contact_shadow']:
        x,y=round(780+(x-780)*1.25),round(648+(y-648)*1.25);w,h=round(w*1.25),round(h*1.25)
    elif scale!=1:
        nw,nh=round(w*scale),round(h*scale);x,y=x+(w-nw)//2,y+h-nh;w,h=nw,nh
    if native_id in native:
        p=native[native_id]
        if mirror:p=ImageOps.mirror(p)
        p=p.resize((w,h),Image.Resampling.LANCZOS)
    elif name in ['suhyeok-seated-body','suhyeok-seated-hands','soi-seated-body','soi-seated-hands']:
        who='soi-seated' if name.startswith('soi') else 'suhyeok-seated';height=114 if who.startswith('soi') else 155;center=994 if who.startswith('soi') else 894;y=330 if who.startswith('soi') else 289
        p=fit(native[who],height);x=round(center-p.width/2)
        if name.endswith('hands'):
            a=np.array(p.getchannel('A'));a[:max(0,432-y)]=0;p=p.copy();p.putalpha(Image.fromarray(a))
    elif 'foreground_copy' in name and ('suhyeok' in name or 'soi' in name or 'delivery' in name):
        who='suhyeok-drawing' if 'drawing' in name else ('suhyeok-delivery' if 'delivery' in name else ('soi-seated' if 'soi' in name else 'suhyeok-seated'))
        if who in native:
            p=native[who].resize((w,h),Image.Resampling.LANCZOS);a=np.array(p.getchannel('A'));a[:max(0,432-y)]=0;p.putalpha(Image.fromarray(a))
    elif name in ['table-foreground','existing_table','matching_table_occlusion']:
        p=base.crop((x,y,x+w,y+h))
    elif name=='camera_handover_close_cut' and 'camera-handover' in native:
        p=native['camera-handover'].resize((w,h),Image.Resampling.LANCZOS)
    else:p=p.resize((w,h),Image.Resampling.LANCZOS)
    return(name,p,(x,y),l.get('visible',True))

for s in nm['states']:
    if not s['id'].startswith('E'):continue
    layers=[('fixed_background',warm,(0,0),True),cmprop('water-packed'),cmprop('blanket-packed'),cmprop('panel-fixed'),cmprop('flashlight-stored')]
    # E03 plate includes cups and supplies; rebuild below from completion counterpart instead.
    if s['id']=='E03-rations':continue
    for l in s['layers'][1:]:layers.append(improve_layer(l,warm,s['id'],'HUB'))
    save(s['id'],layers,[s['preview']],'ending-objective-private' if 'truth' in s['id'] else 'subjective',source_metadata=s)

allscenes=list(completion['scenes'])
for package in ['sejin-v1','first-photo-v1']:
    s=js('art/chapter01/'+package+'/manifest.json')['scene'];s['location']=s['id'][:2];allscenes.append(s)
dog=js('art/chapter01/dog-v2/manifest.json');allscenes+=dog['states']
for s in allscenes:
    ident=s['id'];loc=s['location'];base=read(bg[loc]);layers=[('fixed_background',base,(0,0),True)]
    for l in s['layers']:layers.append(improve_layer(l,base,ident,loc))
    # Enlarge only the contact area accompanying each resized person, not room geometry.
    for index,(name,p,xy,v) in enumerate(layers):
        if name=='feet_contact' and ident=='E08-L4-retrieve':layers[index]=(name,sh(125,16),(840,550),v)
        if name in ['shoot_contact_shadow','sejin_contact_shadow','trader_contact']:
            w=round(p.width*1.18);layers[index]=(name,sh(w,p.height),(xy[0]-(w-p.width)//2,xy[1]),v)
    old=[s['review']]
    if ident=='E03-HUB-rations-shared':old.append('art/chapter00-01/narrative-art-v1/review/E03-rations.png')
    save(ident,layers,old,note='Native actor repaint and matching background; item conditions retained in source_metadata; independent contact/occlusion layers.',source_metadata=s)

# Current convenience store states, same prop placements and visibility rules.
store=next(s for s in inputs['gallery']['scenes'] if s['id']=='store')
for preset in store['presets']:
    layers=[('fixed_background',read(bg['L1']),(0,0),True)]
    for a in sorted(store['slots'],key=lambda v:v.get('order',0)):
        if a['id'] not in preset['visible']:continue
        if a.get('contactShadow'):
            x,y,rx,ry,alpha=a['contactShadow'];layers.append((a['id']+'_contact',sh(rx*2,ry*2,round(alpha*255)),(x-rx,y-ry),True))
        l={'id':a['id'],'path':a['asset'],'rect':a['rect']};v=improve_layer(l,read(bg['L1']),'L1-'+preset['id'],'L1')
        if a.get('tone'):v=(v[0],tone(v[1],a['tone']),v[2],v[3])
        layers.append(v)
    save('L1-'+preset['id'],layers,['art/style-revision/soft-storybook-v1/review/C1-store-'+preset['id']+'.png'],source_metadata=preset)

pickup=js('art/chapter01/photographer-props-v1/manifest.json')['pickup']
for state in ['available','collected']:
    layers=[('fixed_background',read(bg['L4']),(0,0),True)]
    if state=='available':
        layers += [placed('bag_contact','art/chapter01/photographer-props-v1/layers/toolbag-shelf-shadow.png',pickup['shadow_rect']),placed('toolbag','art/chapter01/photographer-props-v1/layers/toolbag-shelf-placement.png',pickup['prop_rect'])]
    save('L4-toolbag-'+state,layers,[pickup[state]],source_metadata=pickup)

# Every adopted scene has a new review sheet; native size is never inflated for claims.
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18)
for offset in range(0,len(scenes),6):
    group=scenes[offset:offset+6];sheet=Image.new('RGB',(1672,495*((len(group)+1)//2)),(33,34,31));d=ImageDraw.Draw(sheet)
    for i,s in enumerate(group):
        x=i%2*836;y=i//2*495;im=read(s['review']).convert('RGB').resize((836,471),Image.Resampling.LANCZOS);sheet.paste(im,(x,y+24));d.text((x+8,y+1),s['id'],font=font,fill='white')
    sheet.save(O/'review'/f'all-scenes-{offset//6+1:02}.jpg',quality=93)
for a in natives:
    mapping[a['previous']]=a['native']
dump(O/'scene-manifest.json',{'id':'art-polish-v1-scenes','backgrounds':bg,'native_manifest':rel(O/'native-manifest.json'),'scenes':scenes,'replacement_map':mapping,'checks':qa,'limitations':['1672x941 scene canvas retained. Backgrounds remain below 3840px native goal.','Scene PSDs hold placement-sized layers; separate native PNG/PSD is authoritative.','Lighting-state multiplication reuses established scene gain, not new image detail.','Visual verification required before activating references; Unity/Photoshop application not tested.']})
print(json.dumps({'scenes':len(scenes),'checks':len(qa),'passed':all(x['pass'] for x in qa)}))
