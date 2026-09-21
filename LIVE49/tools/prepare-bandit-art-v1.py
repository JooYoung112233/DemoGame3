"""User-approved background-only extraction; original generated RGB stays untouched.
Native PNGs, native-size PSD layers and stage placement proof are separate outputs.
"""
from pathlib import Path
import ast, io, re, struct, json, hashlib, argparse
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

R=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--version',choices=['v1','v2'],default='v1')
args=parser.parse_args()
O=R/('art/chapter01/bandit-encounter-'+args.version)
qa=[]
tree=ast.parse((R/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
for folder in ['layers','psd','review']:(O/folder).mkdir(exist_ok=True)
assets=[]
for ident in ['bandit-lead','bandit-lookout']:
    path=O/'sources'/f'{ident}.png'
    src=Image.open(path).convert('RGBA');pixels=np.asarray(src).copy();rgb=pixels[:,:,:3].astype(np.int16)
    # The bright neutral background is connected to the outside; dark garment/hair outlines retain the figure.
    candidate=(rgb.max(2)-rgb.min(2)<32)&(rgb.min(2)>120)
    connected=Image.fromarray(candidate.astype('uint8')).copy()
    for point in [(x,y) for y in [0,src.height-1] for x in range(src.width)]+[(x,y) for x in [0,src.width-1] for y in range(src.height)]:
        if connected.getpixel(point)==1:ImageDraw.floodfill(connected,point,2)
    # Visually verified enclosed background gaps in the v2 clenched hand and left arm.
    if args.version=='v2':
        seeds={'bandit-lead':[(797,817),(791,838)],'bandit-lookout':[(376,751)]}[ident]
        for point in seeds:
            if connected.getpixel(point)==1:ImageDraw.floodfill(connected,point,2)
    outside=np.asarray(connected)==2
    alpha=Image.fromarray(np.where(outside,0,255).astype('uint8'))
    # Small enclosed checkerboard pockets around hair strands and arm gaps are also background.
    # Restrict cleanup to a narrow silhouette band; never key the neutral shirt interior or eye whites.
    interior=np.asarray(alpha.filter(ImageFilter.MinFilter(45)))
    remaining=(np.asarray(alpha)>0)&(interior==0)&(rgb.min(2)>175)&(rgb.max(2)-rgb.min(2)<32)
    alpha=Image.fromarray(np.where(remaining,0,np.asarray(alpha)).astype('uint8'))
    # One native pixel of edge decontamination removes antialiased checkerboard fringe.
    alpha=alpha.filter(ImageFilter.MinFilter(3))
    pixels[:,:,3]=np.asarray(alpha)
    cut=Image.fromarray(pixels);native=O/'layers'/f'{ident}-native.png';cut.save(native)
    box=alpha.getbbox();check(ident+':alpha',alpha.getextrema()==(0,255))
    check(ident+':rgb_preserved',np.array_equal(pixels[:,:,:3],np.asarray(src)[:,:,:3]))
    check(ident+':body_retained',box[3]-box[1]>1400 and (pixels[:,:,3]>0).sum()>300000)
    psd(O/'psd'/f'{ident}-native.psd', [('generated_source',src,(0,0),False),('isolated_character',cut,(0,0),True)],cut)
    assets.append({'id':ident,'source':str(path.relative_to(R)).replace('\\','/'),'native':str(native.relative_to(R)).replace('\\','/'),'size':list(cut.size),'alpha_bounds':list(box),'visible_size':[box[2]-box[0],box[3]-box[1]],'rgb_preserved':True,'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'faces':'left','upscaled':False})
    for name,color in [('dark',(31,35,34,255)),('warm',(187,165,121,255))]:
        proof=Image.new('RGBA',cut.size,color);proof.alpha_composite(cut);proof.save(O/'review'/f'{ident}-{name}.png')

background=Image.open(O/'sources/alley.png').convert('RGBA')
psd(O/'psd/alley-native.psd',[('fixed_background',background,(0,0),True)],background)
# Scene PSD keeps native pixels, not reduced delivery thumbnails. Native figures are initially hidden
# because these layers are editing masters; Unity consumes the separate placement manifest below.
master=Image.new('RGBA',(2048,2048))
master.alpha_composite(background,(0,0))
psd(O/'psd/encounter-editing-master.psd', [('fixed_background_native',background,(0,0),True),
    ('lead_native_hidden',Image.open(O/'layers/bandit-lead-native.png').convert('RGBA'),(0,0),False),
    ('lookout_native_hidden',Image.open(O/'layers/bandit-lookout-native.png').convert('RGBA'),(1024,0),False)],master)

# Placement proof uses separately retained sources. No repainting or deformation.
stage=background.copy();layers=[('fixed_background',background,(0,0),True)];placements=[]
items=[('lookout',O/'layers/bandit-lookout-native.png',1170,499,270),
       ('lead',O/'layers/bandit-lead-native.png',965,615,310)]
for ident,path,center,feet,height in items:
    src=Image.open(path).convert('RGBA');bbox=src.getchannel('A').getbbox();crop=src.crop(bbox)
    width=round(height*crop.width/crop.height);tile=crop.resize((width,height),Image.Resampling.LANCZOS)
    x=round(center-width/2);y=feet-height
    shadow=Image.new('RGBA',(width+38,46));d=ImageDraw.Draw(shadow);d.ellipse((16,12,width+20,33),fill=(30,26,23,75));shadow=shadow.filter(ImageFilter.GaussianBlur(5))
    shadow.save(O/'layers'/f'{ident}-contact-shadow.png')
    shadow_xy=(x-19,feet-23);stage.alpha_composite(shadow,shadow_xy);layers.append((ident+'_contact_shadow',shadow,shadow_xy,True))
    stage.alpha_composite(tile,(x,y));layers.append((ident+'_placement',tile,(x,y),True))
    placements.append({'id':ident,'source':str(path.relative_to(R)).replace('\\','/'),'source_alpha_bounds':list(bbox),'rect':[x,y,width,height],'shadow_rect':[*shadow_xy,*shadow.size],'source_faces':'right' if ident=='suhyeok' else 'left'})
stage.save(O/'review/first-encounter.png');psd(O/'psd/first-encounter-placement.psd',layers,stage)
(O/'manifest.json').write_text(json.dumps({'status':'art-first-pass-local-alpha-approved','background':{'source':'sources/alley.png','size':list(background.size),'layered_internals':False,'upscaled':False},'assets':assets,'stage':{'canvas':list(stage.size),'placements':placements},'limitations':['Native background is 1672x941, below 3840 target.','Character native canvases are 1024x1536; visible heights are recorded separately, below 2048 target.','Native PSDs separate character from source, not limbs/clothing.','Scene placement PSD has reduced placement layers; native PNG and editing master preserve full original resolution.']},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(O/'validation.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'assets':assets,'checks':len(qa),'pass':all(v['pass'] for v in qa)},ensure_ascii=True))
