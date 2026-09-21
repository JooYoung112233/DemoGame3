"""Native photographer props and fixed-workshop pickup before/after proof."""
from pathlib import Path
import ast,io,re,struct,json
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter01/photographer-props-v1'
for d in ['layers','review','psd']:(OUT/d).mkdir(exist_ok=True)
qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
props={};entries=[]
for name in ['toolbag','camera','film']:
 src=Image.open(OUT/'sources'/(name+'-source.png')).convert('RGBA');im=src.copy()
 if name=='film':
  # User-authorized cut removes generated background glow; source RGB unchanged.
  mask=Image.new('L',src.size);ImageDraw.Draw(mask).polygon([(44,594),(298,232),(327,226),(399,191),(1013,289),(1269,326),(1394,376),(1454,425),(1501,443),(1494,469),(1320,838),(1262,854),(1172,916),(736,815),(171,699),(94,621)],fill=255)
  mask=mask.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(.3))
  im.putalpha(Image.fromarray(np.minimum(np.asarray(im.getchannel('A')),np.asarray(mask))))
  check('film_rgb_preserved',np.array_equal(np.asarray(im)[:,:,:3],np.asarray(src)[:,:,:3]))
 path=OUT/'layers'/(name+'-native.png');im.save(path);props[name]=im
 check(name+':true_alpha',im.getchannel('A').getextrema()[0]==0)
 proof=Image.new('RGBA',im.size,(76,62,41,255));proof.alpha_composite(im);proof.thumbnail((850,850));proof.save(OUT/'review'/(name+'-alpha-proof.png'))
 entries.append({'id':name,'source':(OUT/'sources'/(name+'-source.png')).relative_to(ROOT).as_posix(),'native':path.relative_to(ROOT).as_posix(),'native_size':list(im.size),'visible_bounds':im.getchannel('A').getbbox(),'alpha_method':'original alpha preserved' if name!='film' else 'polygon alpha trim to remove background glow; RGB preserved'})
canvas=Image.new('RGBA',(2790,2141));positions={'toolbag':(0,0),'camera':(0,887),'film':(1254,1000)}
layers=[]
for name,im in props.items():layers.append((name+'_native',im,positions[name],True));canvas.alpha_composite(im,positions[name])
psd(OUT/'psd/photographer-props-native.psd',layers,canvas)
base=Image.open(ROOT/'art/chapter01/repair-shop-v1/repair-shop-clean-base-source-v1.png').convert('RGBA')
bag=props['toolbag'];bag=bag.crop(bag.getchannel('A').getbbox());bag=bag.resize((130,49),Image.Resampling.LANCZOS)
shadow=Image.new('RGBA',(140,14),(28,24,16,0));m=Image.new('L',shadow.size);ImageDraw.Draw(m).ellipse((3,3,137,10),fill=92);shadow.putalpha(m.filter(ImageFilter.GaussianBlur(1.5)))
bag.save(OUT/'layers/toolbag-shelf-placement.png');shadow.save(OUT/'layers/toolbag-shelf-shadow.png')
found=base.copy();found.alpha_composite(shadow,(737,348));found.alpha_composite(bag,(742,306))
found.save(OUT/'review/L4-toolbag-available.png');base.save(OUT/'review/L4-toolbag-collected.png');found.crop((612,203,1060,401)).save(OUT/'review/toolbag-contact-native.png')
psd(OUT/'psd/L4-toolbag-placement.psd',[('fixed_L4_base',base,(0,0),True),('bag_contact_shadow',shadow,(737,348),True),('bag_preview',bag,(742,306),True)],found)
diff=np.any(np.asarray(found)!=np.asarray(base),axis=2);allowed=np.zeros(diff.shape,bool);allowed[306:363,737:878]=True
check('only_bag_area_changed',not np.any(diff&~allowed))
m={'id':'photographer-props-v1','status':'art-review-not-runtime','generator':'built-in image_gen','events':['E08','E09','E11'],'quests':['Q08','Q10'],'assets':entries,'native_psd':'art/chapter01/photographer-props-v1/psd/photographer-props-native.psd','native_psd_note':'Each prop retains its full native canvas/pixels as an independent raster layer; no downscaled sheet used as master. Internal parts are not separate.','pickup':{'background':'art/chapter01/repair-shop-v1/repair-shop-clean-base-source-v1.png','prop_rect':[742,306,130,49],'shadow_rect':[737,348,140,14],'available':'art/chapter01/photographer-props-v1/review/L4-toolbag-available.png','collected':'art/chapter01/photographer-props-v1/review/L4-toolbag-collected.png'},'limitations':['Film is visually blank packaging; labeling remains Unity text, not baked art.','No Sejin portrait/vehicle, handover animation, camera-held pose, album picture, audio or Unity runtime yet.','Placement PSD is a scene-size review, native prop originals preserved separately.','File-reader PSD checks done; Photoshop application not tested.']}
(OUT/'manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'validation.json').write_text(json.dumps({'passed':all(x['pass'] for x in qa),'checks':qa},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'checks':len(qa),'props':len(props)}))
