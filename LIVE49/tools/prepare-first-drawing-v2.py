"""First drawing artwork, preserving native originals and fixed camper geometry."""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter01/first-drawing-v2'
for d in ['layers','psd','review']:(OUT/d).mkdir(exist_ok=True)
qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
def read(p):return Image.open(ROOT/p).convert('RGBA')
def rel(p):return p.relative_to(ROOT).as_posix()
src=read('art/chapter01/first-drawing-v2/sources/suhyeok-place-pencils-source.png')
rgb=np.asarray(src)[:,:,:3].astype('int16');neutral=(rgb.max(2)-rgb.min(2)<24)&(rgb.min(2)>70)
regions=Image.fromarray(neutral.astype('uint8')).copy()
for point in [(0,0),(src.width-1,0),(0,src.height-1),(src.width-1,src.height-1),(325,1120)]:
 if regions.getpixel(point)==1:ImageDraw.floodfill(regions,point,2)
actor=src.copy();actor.putalpha(Image.fromarray(np.where(np.asarray(regions)==2,0,255).astype('uint8')).filter(ImageFilter.MinFilter(3)))
actor.save(OUT/'layers/suhyeok-place-pencils-native.png')
check('actor_rgb_preserved',np.array_equal(np.asarray(actor)[:,:,:3],np.asarray(src)[:,:,:3]))
check('actor_alpha',actor.getchannel('A').getextrema()==(0,255))
# Keep a separately addressable foreground hand/forearm for table occlusion.
handmask=Image.new('L',actor.size);draw=ImageDraw.Draw(handmask)
draw.polygon([(38,944),(281,941),(316,1090),(305,1447),(0,1447),(0,1091)],fill=255)
draw.polygon([(390,1050),(788,1008),(1046,1000),(1085,1085),(1085,1250),(359,1250)],fill=255)
hands=actor.copy();hands.putalpha(Image.fromarray(np.minimum(np.asarray(actor.getchannel('A')),np.asarray(handmask))))
hands.save(OUT/'layers/suhyeok-place-pencils-hands-native.png')
psd(OUT/'psd/suhyeok-place-pencils-native.psd',[('source_hidden',src,(0,0),False),('actor_native',actor,(0,0),True),('hand_occlusion_copy',hands,(0,0),False)],actor)

ink=read('art/chapter01/first-drawing-v2/sources/first-sea-drawing-source.png')
check('ink_native_alpha',ink.getchannel('A').getextrema()[0]==0)
ink.save(OUT/'layers/sea-complete-native.png')
arr=np.array(ink);yy=np.arange(ink.height)[:,None]
# A partial first pass uses only the upper blue wave strokes from the same original.
blue=(arr[:,:,2].astype('int16')>arr[:,:,0].astype('int16')+15)&(arr[:,:,2].astype('int16')>arr[:,:,1].astype('int16')-30)
fade=np.clip((680-yy)/14,0,1)
arr[:,:,3]=(arr[:,:,3]*blue*fade).astype('uint8');started=Image.fromarray(arr)
started.save(OUT/'layers/sea-started-native.png')
psd(OUT/'psd/sea-drawing-states-native.psd',[('complete_ink',ink,(0,0),True),('started_ink_alternative',started,(0,0),False)],ink)
book=read('art/chapter00/book-insert-v1/sketchbook-blank-native.png')
bookbounds=book.getchannel('A').getbbox()
bookstates={'blank':book};page_layers={}
for name,drawing in [('started',started),('complete',ink)]:
 tile=drawing.resize((445,445),Image.Resampling.LANCZOS)
 layer=Image.new('RGBA',book.size);layer.alpha_composite(tile,(837,232));page_layers[name]=layer
 merged=book.copy();merged.alpha_composite(layer);bookstates[name]=merged
 merged.save(OUT/'layers'/('book-'+name+'-native.png'))
check('book_outside_right_page_unchanged',np.array_equal(np.asarray(bookstates['complete'])[:,:830],np.asarray(book)[:,:830]))
psd(OUT/'psd/book-page-states-native.psd',[('original_blank_book',book,(0,0),True),('complete_drawing',page_layers['complete'],(0,0),True),('started_alternative',page_layers['started'],(0,0),False)],bookstates['complete'])
for name,b in bookstates.items():
 proof=Image.new('RGBA',b.size,(76,59,39,255));proof.alpha_composite(b);proof.save(OUT/'review'/('book-'+name+'-native.png'))

base=read('art/chapter01/revision-v3/camper-clean-base-v3.png');prior=read('art/chapter00/camper/review/06-light-restored.png')
plate=prior.copy();patch=(815,280,1092,633);plate.paste(base.crop(patch),patch[:2]);size=base.size
def placed(im,rect):
 x,y,w,h=rect;return (im.resize((w,h),Image.Resampling.LANCZOS),(x,y))
actor_rect=(823,292,144,192)
act,actxy=placed(actor,actor_rect);acthands,_=placed(hands,actor_rect)
soi=read('art/chapter01/layers/characters/soi-seated-body-v1.png');soih=read('art/chapter01/layers/characters/soi-seated-hands-v1.png')
table=base.crop((835,432,1092,633))
pencil=read('art/chapter01/colored-pencils-v1/colored-pencils-cutout-native-v1.png');pencil=pencil.crop(pencil.getchannel('A').getbbox())
pencil,pxy=placed(pencil,(845,478,56,19))
shadow=Image.new('RGBA',(64,27),(35,24,15,0));sa=Image.new('L',shadow.size);sa.paste(pencil.getchannel('A'),(4,5));shadow.putalpha(sa.filter(ImageFilter.GaussianBlur(.8)).point(lambda v:round(v*.38)))
shadow.save(OUT/'layers/camper-pencil-contact-shadow.png')
states=[]
for name,page,hand_visible,drawing_pose in [('01-delivery','blank',True,False),('02-ready','blank',False,False),('03-started','started',False,True),('04-complete','complete',False,False)]:
 # At the quiet seated cuts reuse the established original actor, while delivery
 # uses the new reaching pose. Insert cuts use the native book, not this thumbnail.
 if hand_visible:body,xy=act,actxy;fatherhands=acthands
 else:
  body,xy=placed(read('art/style-revision/soft-storybook-v1/sprites/suhyeok-seated-body.png'),(840,289,109,155))
  fatherhands=read('art/style-revision/soft-storybook-v1/sprites/suhyeok-seated-hands.png').resize((109,155),Image.Resampling.LANCZOS)
 soibody,soixy=placed(read('art/chapter01/layers/characters/'+('soi-drawing-body-v1.png' if drawing_pose else 'soi-seated-body-v1.png')),(938,330,112,132))
 soihands=read('art/chapter01/layers/characters/'+('soi-drawing-hands-v1.png' if drawing_pose else 'soi-seated-hands-v1.png')).resize((112,132),Image.Resampling.LANCZOS)
 b=bookstates[page].crop(bookbounds);b,bxy=placed(b,(904,454,102,54))
 mug,mxy=placed(read('art/chapter01/layers/sprites/mug-source-v1.png'),(1010,480,53,52))
 layers=[('fixed_packed_camper',plate,(0,0),True),('father_body',body,xy,True),('soi_subjective_body',soibody,soixy,True),('fixed_table_occlusion_copy',table,(835,432),True),('book_'+page,b,bxy,True),('mug',mug,mxy,True),('pencils_shadow',shadow,(841,474),True),('pencils',pencil,pxy,True),('father_hands',fatherhands,xy,True),('soi_subjective_hands',soihands,soixy,True)]
 frame=plate.copy()
 for _,im,pos,visible in layers[1:]:frame.alpha_composite(im,pos)
 frame.save(OUT/'review'/(name+'.png'));frame.crop((810,280,1100,550)).save(OUT/'review'/(name+'-table-native.png'))
 psd(OUT/'psd'/(name+'-placement.psd'),layers,frame)
 changed=np.any(np.asarray(frame)!=np.asarray(prior),axis=2);allowed=np.zeros(changed.shape,bool);allowed[280:633,815:1092]=True
 check(name+':outside_scene_patch_unchanged',not np.any(changed&~allowed))
 states.append({'id':name,'page_state':page,'preview':rel(OUT/'review'/(name+'.png')),'psd':rel(OUT/'psd'/(name+'-placement.psd')),'new_father_pose':hand_visible,'soi_pose':'existing subjective drawing pose' if drawing_pose else 'existing seated pose'})
manifest={'id':'E05-E06-first-drawing-v2','status':'art-and-storyboard-review','events':['E05','E06'],'quests':['Q05','Q06','Q07'],'generator':'built-in image_gen','native_actor_size':list(actor.size),'native_ink_size':list(ink.size),'book_native_size':list(book.size),'scene_size':list(base.size),'actor_placement':actor_rect,'pencil_placement':[845,478,56,19],'book_placement':[904,454,102,54],'states':states,'resource_sources':['art/chapter01/first-drawing-v2/sources/suhyeok-place-pencils-source.png','art/chapter01/first-drawing-v2/sources/first-sea-drawing-source.png'],'limitations':['Actor native 1086x1448 is below requested 1536-2048px visible height.','Sea subject is a draft imagined destination; no visited seashore or family portrait is established.','Book pages use the same original blank book and overlaid marks; individual bound pages are not independent movable meshes.','Quiet seated poses are reused; only delivery father pose is newly painted.','Soi drawing pose is an existing subjective scene, not a third-party confirmation.','Placement PSDs contain downscaled sprites; large sources and native PSDs are preserved separately.','Camper remains 1672x941; no high-resolution background reconstruction.','No final dialogue, motion, sound, Unity or Photoshop application validation.']}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'validation.json').write_text(json.dumps({'passed':all(q['pass'] for q in qa),'checks':qa},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'checks':len(qa),'scene_states':len(states),'psd_count':len(list((OUT/'psd').glob('*.psd')))}))
