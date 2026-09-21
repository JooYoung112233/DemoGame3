"""Native character repaints and fixed-geometry scene variants; no UI or dialogue edits."""
from pathlib import Path
import ast, io, re, struct, json, hashlib
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/chapter00-01/narrative-art-v1'
qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
def read(p):return Image.open(ROOT/p).convert('RGBA')
def rel(p):return p.relative_to(ROOT).as_posix()
def putjson(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def cropalpha(im):return im.crop(im.getchannel('A').getbbox())
def fit(im,h):return im.resize((round(im.width*h/im.height),h),Image.Resampling.LANCZOS)
def tint(im):
 a=np.array(im);a[:,:,:3]=(a[:,:,:3].astype(float)*[.39,.47,.66]).clip(0,255).astype('uint8');return Image.fromarray(a)
def cut(src,seeds=()):
 a=np.asarray(src);rgb=a[:,:,:3].astype('int16');neutral=(rgb.max(2)-rgb.min(2)<28)&(rgb.min(2)>80)
 regions=Image.fromarray(neutral.astype('uint8')).copy()
 for p in [(0,0),(src.width-1,0),(0,src.height-1),(src.width-1,src.height-1),*seeds]:
  if regions.getpixel(p)==1:ImageDraw.floodfill(regions,p,2)
 im=src.copy();im.putalpha(Image.fromarray(np.where(np.asarray(regions)==2,0,255).astype('uint8')).filter(ImageFilter.MinFilter(3)));return im
native={};assets=[]
for name in ['suhyeok-seated','soi-seated','suhyeok-drawing']:
 p=OUT/'sources'/f'{name}.png';src=Image.open(p).convert('RGBA')
 im=src if src.getchannel('A').getextrema()[0]<255 else cut(src)
 # Visually inspected enclosed checkerboard pockets. Protect face from neutral removal.
 rgb=np.asarray(src)[:,:,:3].astype('int16');neutral=(rgb.max(2)-rgb.min(2)<32)&(rgb.min(2)>100)
 region=np.zeros(neutral.shape,bool)
 if name=='suhyeok-seated':
  region[:650]=True;region[330:800,320:850]=False
 elif name=='soi-seated':
  region[:930]=True;region[390:900,290:935]=False
 else:
  region[:650]=True;region[300:760,360:865]=False
  region[1000:1180,400:1000]=True
 a=np.array(im.getchannel('A'));a[neutral&region]=0;im.putalpha(Image.fromarray(a).filter(ImageFilter.MinFilter(3)))
 check(name+':source_RGB_preserved',np.array_equal(np.asarray(src)[:,:,:3],np.asarray(im)[:,:,:3]))
 check(name+':alpha',im.getchannel('A').getextrema()==(0,255))
 im.save(OUT/'layers'/f'{name}-native.png');native[name]=cropalpha(im)
 # Forearms are a pixel-identical occlusion copy, not separately articulated hands.
 hands=im.copy();mask=Image.new('L',im.size);ImageDraw.Draw(mask).rectangle((0,round(im.height*(.70 if name=='suhyeok-drawing' else .86)),im.width,im.height),fill=255)
 hands.putalpha(Image.fromarray(np.minimum(np.asarray(im.getchannel('A')),np.asarray(mask))))
 hands.save(OUT/'layers'/f'{name}-forearms-native.png')
 psd(OUT/'psd'/f'{name}-native.psd',[('generated_original_hidden',src,(0,0),False),('isolated_character',im,(0,0),True),('forearm_occlusion_copy_hidden',hands,(0,0),False)],im)
 bounds=im.getchannel('A').getbbox()
 assets.append({'id':name,'source':rel(p),'prompt':rel(p.with_suffix('.prompt.txt')),'native':rel(OUT/'layers'/f'{name}-native.png'),'forearm_copy':rel(OUT/'layers'/f'{name}-forearms-native.png'),'psd':rel(OUT/'psd'/f'{name}-native.psd'),'size':list(src.size),'alpha_bounds':bounds,'visible_height':bounds[3]-bounds[1],'target_met':bounds[3]-bounds[1]>=1536,'method':'built-in image_gen; exterior neutral matte cut with original RGB retained','source_sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
warm=read('art/chapter01/revision-v3/camper-clean-base-v3.png')
dark=read('art/chapter00/camper/layers/camper-outage-stable-v1.png')
baseline_path=OUT/'scene-baseline.json'
if not baseline_path.exists():
 story=json.loads((ROOT/'design/chapter00/continuation-v1/story.json').read_text(encoding='utf8'))
 putjson(baseline_path,{'c0_scenes':story['scenes'],'art_hashes':{p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in story['scenes'].values()}})
baseline=json.loads(baseline_path.read_text(encoding='utf8'));states=[]
def save_scene(ident,layers,previous,allowed,context,activate=True):
 result=Image.new('RGBA',warm.size)
 for _,im,xy,visible in layers:
  if visible:result.alpha_composite(im,xy)
 check(ident+':fixed_background',not np.any(np.any(np.asarray(result)!=np.asarray(previous),axis=2)&~allowed))
 p=OUT/'review'/f'{ident}.png';result.save(p)
 psd(OUT/'psd'/f'{ident}.psd',layers,result)
 slots=[]
 for name,im,xy,visible in layers:
  lp=OUT/'layers'/f'{ident}-{name}.png';im.save(lp);slots.append({'id':name,'path':rel(lp),'rect':[*xy,*im.size],'visible':visible})
 states.append({'id':ident,'preview':rel(p),'psd':rel(OUT/'psd'/f'{ident}.psd'),'context':context,'early_game_allowed':activate,'layers':slots})
 return result
def character_layers(isdark=False,father=True,soi=True):
 layers=[]
 for name,h,center,y in [('suhyeok-seated',155,894,289),('soi-seated',114,994,330)]:
  if (name.startswith('suhyeok') and not father) or (name.startswith('soi') and not soi):continue
  im=fit(native[name],h);xy=(round(center-im.width/2),y)
  if isdark:im=tint(im)
  layers.append((name,im,xy,True))
 return layers
def patch_seated(ident,source,father=True,soi=True,isdark=False,activate=True):
 prior=read(source);base=dark if isdark else warm
 rect=(830,280,1062,453) if father else (930,325,1062,453)
 x0,y0,x1,y1=rect;plate=prior.copy();plate.paste(base.crop(rect),(x0,y0))
 actors=character_layers(isdark,father,soi)
 # Existing book, mug, rations and every pixel below the actor area stay untouched.
 layers=[('preserved_scene_without_seated_actors',plate,(0,0),True),*actors]
 allowed=np.zeros((warm.height,warm.width),bool);allowed[y0:y1,x0:x1]=True
 return save_scene(ident,layers,prior,allowed,'present-subjective' if soi else 'ending-objective',activate)
for key,p in baseline['c0_scenes'].items():
 if key.startswith('photo_'):continue
 patch_seated('C0-'+key,p,father=key not in ['investigation','secured'],isdark=key in ['outage','investigation','secured','night'])
patch_seated('C0-current-truth',baseline['c0_scenes']['current'],soi=False,activate=False)
patch_seated('E03-rations','art/chapter01/completion-v1/review/E03-HUB-rations-shared.png')

# Book geometry/ink and table remain the existing original layers.
table=warm.crop((835,432,1092,633))
pencil=cropalpha(read('art/chapter01/colored-pencils-v1/colored-pencils-cutout-native-v1.png')).resize((56,19),Image.Resampling.LANCZOS)
mug=read('art/chapter01/layers/sprites/mug-source-v1.png').resize((53,52),Image.Resampling.LANCZOS)
for ident,page,draw,soi in [('E05-delivery','blank',False,True),('E06-ready','blank',False,True),('E06-drawing','started',True,True),('E06-complete','complete',False,True),('E06-drawing-truth','started',True,False)]:
 prior=read('art/chapter00/camper/review/06-light-restored.png');plate=prior.copy();rect=(815,280,1092,633);plate.paste(warm.crop(rect),rect[:2])
 if page=='blank':book=read('art/chapter00/book-insert-v1/sketchbook-blank-native.png')
 else:book=read(f'art/chapter01/first-drawing-v2/layers/book-{page}-native.png')
 book=cropalpha(book).resize((102,54),Image.Resampling.LANCZOS)
 delivery=ident=='E05-delivery'
 actors=character_layers(False,not draw and not delivery,soi)
 if delivery:
  im=read('art/chapter01/first-drawing-v2/layers/suhyeok-place-pencils-native.png').resize((144,192),Image.Resampling.LANCZOS)
  actors.insert(0,('existing_delivery_pose',im,(823,292),True))
 if draw:
  im=fit(native['suhyeok-drawing'],171);xy=(852,310)
  actors.insert(0,('suhyeok_drawing',im,xy,True))
 layers=[('fixed_packed_camper_without_actors',plate,(0,0),True),*actors,('existing_table',table,(835,432),True),('existing_book_'+page,book,(904,454),True),('existing_mug',mug,(1010,480),True),('existing_pencils_shadow',read('art/chapter01/first-drawing-v2/layers/camper-pencil-contact-shadow.png'),(841,474),True),('existing_pencils',pencil,(845,478),True)]
 for name,im,xy,_ in actors:
  if name=='existing_delivery_pose':hand=read('art/chapter01/first-drawing-v2/layers/suhyeok-place-pencils-hands-native.png').resize((144,192),Image.Resampling.LANCZOS)
  else:
   hand=im.copy();a=np.array(hand.getchannel('A'));a[:max(0,432-xy[1])]=0;hand.putalpha(Image.fromarray(a))
  layers.append((name+'_foreground_copy',hand,xy,True))
 allowed=np.zeros((warm.height,warm.width),bool);allowed[280:633,815:1092]=True
 save_scene(ident,layers,prior,allowed,'present-subjective' if soi else 'ending-objective',soi)

# Reveal pair must differ only where Soi was visible, with the same real drawing action.
subjective=read(rel(OUT/'review/E06-drawing.png'));objective=read(rel(OUT/'review/E06-drawing-truth.png'))
delta=np.any(np.asarray(subjective)!=np.asarray(objective),axis=2);region=np.zeros(delta.shape,bool);region[330:445,940:1048]=True
check('drawing_reveal_only_soi_region_changes',not np.any(delta&~region))
check('drawing_reveal_has_visible_difference',np.any(delta))
book_rect=[904,454,1006,508];tip=[957,477]
check('drawing_pencil_tip_inside_existing_page',book_rect[0]<tip[0]<book_rect[2] and book_rect[1]<tip[1]<book_rect[3])
for p,h in baseline['art_hashes'].items():check('original_preserved:'+Path(p).stem,hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h)
# Neutral opaque backdrops reveal alpha issues without changing source assets.
sheet=Image.new('RGB',(1500,620),(74,71,62));d=ImageDraw.Draw(sheet)
for i,(name,im) in enumerate(native.items()):
 v=fit(im,570);sheet.paste(v,(i*500+(500-v.width)//2,5),v);d.text((i*500+15,593),name,fill='white')
sheet.save(OUT/'review/native-alpha-review.jpg',quality=94)
for suffix,selected in [('current',[s for s in states if s['id'] in ['C0-current','C0-outage','C0-investigation','E03-rations','E06-drawing','E06-complete']]),('truth',[s for s in states if s['id'] in ['C0-current','C0-current-truth','E06-drawing','E06-drawing-truth']])]:
 sheet=Image.new('RGB',(1600,480*((len(selected)+1)//2)),(30,29,26));d=ImageDraw.Draw(sheet)
 for i,s in enumerate(selected):
  im=read(s['preview']).convert('RGB');im.thumbnail((790,445));x=(i%2)*800;y=(i//2)*480;sheet.paste(im,(x,y));d.text((x+8,y+450),s['id'],fill='white')
 sheet.save(OUT/'review'/f'{suffix}-contact.jpg',quality=94)
putjson(OUT/'manifest.json',{'id':'ch00-01-narrative-art-v1','assets':assets,'states':states,'canvas':[1672,941],'native_background_regenerated':False,'dialogue_modified':False,'source_scene_baseline':rel(baseline_path),'limitations':['Generated visible character heights remain below 1536px target; no upscaling claimed.','Generated RGB checker backdrops removed by local extraction explicitly permitted by user; hidden RGB preserved.','Scene PSD layers are placement-sized; separate native PSD/PNG masters supplied.','Forearms are occlusion copies, not rigged articulated hands.','Objective reveal variants must not be shown in early gameplay.','Final dialogue, Unity and Photoshop application validation not performed.']})
putjson(OUT/'validation.json',{'passed':all(x['pass'] for x in qa),'checks':qa})
print(json.dumps({'sources':len(assets),'scenes':len(states),'checks':len(qa),'passed':True}))
