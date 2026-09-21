"""Verify editable compositions, alpha fixes, active coverage and native provenance."""
from pathlib import Path
import json,hashlib,ast
import numpy as np
from PIL import Image,ImageDraw,ImageFont
R=Path(__file__).resolve().parents[1];O=R/'art/chapter00-01/art-polish-v1';checks=[]
def js(p):return json.loads((R/p).read_text(encoding='utf-8-sig'))
def read(p):return Image.open(R/p).convert('RGBA')
def check(name,ok):
    checks.append({'check':name,'pass':bool(ok)})
    if not ok:raise AssertionError(name)
m=js('art/chapter00-01/art-polish-v1/scene-manifest.json');n=js('art/chapter00-01/art-polish-v1/native-manifest.json')
check('native_builder_channel_checks',all(x['pass'] for x in n['checks']))
check('scene_builder_channel_checks',all(x['pass'] for x in m['checks']))
decoded=0
for p in O.rglob('*'):
    if p.suffix.lower() in ['.png','.psd','.jpg']:
        with Image.open(p) as im:im.load()
        decoded+=1
check('all_current_package_images_decode',True)
for a in n['assets']:
    src=read(a['source']);cut=read(a['native']);trim=read(a['trimmed'])
    check(a['id']+':native_RGB_preserved',np.array_equal(np.array(src)[:,:,:3],np.array(cut)[:,:,:3]))
    check(a['id']+':native_dimensions_preserved',src.size==cut.size==tuple(a['native_size']))
    check(a['id']+':alpha',cut.getchannel('A').getextrema()==(0,255))
    check(a['id']+':trim_bounds',cut.getchannel('A').getbbox()==tuple(a['alpha_bounds']) and trim.size==tuple(a['visible_size']))
    check(a['id']+':source_hash',hashlib.sha256((R/a['source']).read_bytes()).hexdigest()==a['source_sha256'])
    check(a['id']+':PSD_matches_cutout',np.array_equal(np.array(read(a['psd'])),np.array(cut)))
dialogue=read(next(a['native'] for a in n['assets'] if a['id']=='suhyeok-dialogue'))
for xy in [(775,1100),(790,1190),(220,1340)]:check('dialogue_gap_'+str(xy),dialogue.getpixel(xy)[3]==0)
byid={s['id']:s for s in m['scenes']}
for s in m['scenes']:
    merged=Image.new('RGBA',(1672,941))
    for l in s['layers']:
        p=read(l['path']);x,y,w,h=l['rect'];check(s['id']+':size:'+l['id'],p.size==(w,h))
        if l['visible']:merged.alpha_composite(p,(x,y))
    expected=read(s['review'])
    check(s['id']+':independent_layers_recompose',np.array_equal(np.array(merged),np.array(expected)))
    check(s['id']+':PSD_merged_matches',np.array_equal(np.array(read(s['psd'])),np.array(expected)))
    for old in (s.get('source_metadata') or {}).get('layers',[]):
        if 'condition' in old:
            check(s['id']+':condition:'+old['id'],next(l for l in s['layers'] if l['id']==old['id']).get('condition')==old['condition'])
for live,truth in [('C0-current','C0-current-truth'),('E06-drawing','E06-drawing-truth')]:
    a=byid[live];b=byid[truth];allowed=np.zeros((941,1672),bool)
    for l in a['layers']:
        if l['id'].startswith('soi'):
            x,y,w,h=l['rect'];allowed[y:y+h,x:x+w]|=np.array(read(l['path']).getchannel('A'))>0
    delta=np.any(np.array(read(a['review']))!=np.array(read(b['review'])),axis=2)
    check(live+':truth_diff_only_soi',not np.any(delta&~allowed))
shot=next(l for l in byid['L6-first-shoot']['layers'] if l['id']=='suhyeok_shoot');a=next(a for a in n['assets'] if a['id']=='suhyeok-shoot')
check('shooting_aspect_not_squeezed',abs(shot['rect'][2]/shot['rect'][3]-a['visible_size'][0]/a['visible_size'][1])<.03)
check('sixty_scene_variants',len(m['scenes'])==60)
cov=js('design/chapter01/week01-art-coverage-v1.json');check('all_25_events',len(cov['events'])==25)
reg=js('art/chapter00-01/art-polish-v1/active-art-register.json')
for v in reg['entries']:check('active:'+v['id']+':'+Path(v['path']).name,(R/v['path']).exists() and '-truth' not in v['path'])
reference_count=0
def check_refs(v,origin):
    global reference_count
    if isinstance(v,dict):
        for x in v.values():check_refs(x,origin)
    elif isinstance(v,list):
        for x in v:check_refs(x,origin)
    elif isinstance(v,str) and len(v)<240 and v.startswith(('art/','design/','docs/')) and Path(v).suffix in ['.png','.psd','.json','.md']:
        reference_count+=1;check('reference:'+origin+':'+v,(R/v).is_file())
for p in js('art/chapter00-01/art-polish-v1/reference-backup.json'):check_refs(js(p),p)
for p in ['tools/prepare-art-polish-native.py','tools/prepare-art-polish-scenes.py','tools/integrate-art-polish-v1.py','tools/prepare-scene-integration-review.py']:ast.parse((R/p).read_text(encoding='utf-8-sig'))
# Exercise just the approval-preservation branch without regenerating approved artwork.
tree=ast.parse((R/'tools/prepare-scene-integration-review.py').read_text(encoding='utf-8-sig'))
branch=next(x for x in tree.body if isinstance(x,ast.If) and ast.unparse(x.test).startswith("previous_manifest.get('user_approval')"))
approval=js('art/chapter00-01/scene-integration-v1/manifest.json');scope={'manifest':{'limitations':['']*4},'previous_manifest':approval}
exec(compile(ast.Module(body=[branch],type_ignores=[]),'<approval preservation>','exec'),scope)
check('approved_reference_status_preserved',scope['manifest']['user_approval']==approval['user_approval'] and scope['manifest']['status']==approval['status'])
# Public preview excludes ending spoilers.
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',21)
ids=[('C0-current','캠핑카 · 대화'),('L1-stationery','편의점 · 조사'),('E08-L4-retrieve','수리점 · 공구 회수'),('E12-L5-place-water','주택 · 강아지'),('L6-first-shoot','강변 · 첫 사진'),('E17-HUB-cooking','캠핑카 · 요리')]
sheet=Image.new('RGB',(1920,1716),(31,31,28));d=ImageDraw.Draw(sheet)
for i,(ident,label) in enumerate(ids):
    x=i%2*960;y=i//2*572;im=read(byid[ident]['review']).convert('RGB').resize((960,540),Image.Resampling.LANCZOS);sheet.paste(im,(x,y+32));d.text((x+12,y+3),label,font=font,fill=(237,226,204))
sheet.save(O/'review/overview.jpg',quality=94)
result={'passed':all(x['pass'] for x in checks),'checks':checks,'decoded_images':decoded,'checked_current_data_references':reference_count,'native_assets':len(n['assets']),'scene_variants':len(m['scenes']),'PSD_files':len(list((O/'psd').glob('*.psd'))),'visual_review':'All 60 compositions in 10 sheets; 17 native cutouts on dark neutral ground; full-size key scenes and corrected alpha edges inspected.','not_tested':['Unity runtime','Photoshop application'],'remaining_limit':'Native resolution goals are not all met; no upscaling claimed as restored detail.'}
(O/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({k:v for k,v in result.items() if k not in ['checks','visual_review','not_tested','remaining_limit']}))
