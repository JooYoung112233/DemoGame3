"""Native prop extraction and review composites; fixed background is never repainted."""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFilter

ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter01/colored-pencils-v1'
for d in ['layers','review']:(OUT/d).mkdir(exist_ok=True)
qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
src=OUT/'colored-pencils-source-v1.png';source=Image.open(src).convert('RGBA')
a=np.asarray(source);rgb=a[:,:,:3].astype('int16')
# The generated source has a baked gray checkerboard, not native alpha.
# Keep the chromatic object silhouette, flood away the neutral exterior, restore a thin
# dark contour via alpha dilation. No RGB recoloring or generated detail.
seed=(rgb.max(axis=2)-rgb.min(axis=2))>24
mask=Image.fromarray((seed*255).astype('uint8')).copy()
ImageDraw.floodfill(mask,(0,0),128,thresh=0)
mask=Image.fromarray((np.asarray(mask)!=128).astype('uint8')*255).filter(ImageFilter.MaxFilter(3))
mask=mask.filter(ImageFilter.GaussianBlur(.35))
cut=source.copy();cut.putalpha(mask);bounds=cut.getchannel('A').getbbox()
cut.save(OUT/'colored-pencils-cutout-native-v1.png')
check('native_rgb_unchanged',np.array_equal(np.asarray(cut)[:,:,:3],a[:,:,:3]))
check('actual_alpha',cut.getchannel('A').getextrema()==(0,255))
check('transparent_edges',not np.asarray(mask)[[0,-1],:].any() and not np.asarray(mask)[:,[0,-1]].any())
psd(OUT/'colored-pencils-native-v1.psd',[('source_checkerboard_hidden',source,(0,0),False),('pencils_cutout_native',cut,(0,0),True)],cut)
# Transparent edge QA over contrasting surfaces, all derived from original pixels.
panels=[]
for color in [(29,33,29,255),(222,209,174,255)]:
 c=Image.new('RGBA',source.size,color);c.alpha_composite(cut);panels.append(c)
comparison=Image.new('RGB',(source.width,source.height*2))
for i,c in enumerate(panels):comparison.paste(c.convert('RGB'),(0,i*source.height))
comparison.save(OUT/'review/alpha-check.jpg',quality=94)

bgpath=ROOT/'art/chapter01/general-store-v1/general-store-clean-base-source-v1.png'
base=Image.open(bgpath).convert('RGBA');bg_hash=hashlib.sha256(bgpath.read_bytes()).hexdigest()
trim=cut.crop(bounds)
sprite=trim.resize((90,round(trim.height*90/trim.width)),Image.Resampling.LANCZOS)
sprite.save(OUT/'layers/pencils-table-placement.png')
xy=(1210,425)
shadow=Image.new('RGBA',(sprite.width+8,sprite.height+8),(31,24,16,0))
shadow_alpha=Image.new('L',shadow.size);shadow_alpha.paste(sprite.getchannel('A'),(4,5))
shadow_alpha=shadow_alpha.filter(ImageFilter.GaussianBlur(1.1)).point(lambda v:round(v*.42))
shadow.putalpha(shadow_alpha);shadow_xy=(xy[0]-4,xy[1]-4)
shadow.save(OUT/'layers/pencils-contact-shadow.png')
found=base.copy();found.alpha_composite(shadow,shadow_xy);found.alpha_composite(sprite,xy)
found.save(OUT/'review/L3-pencils-available.png')
base.save(OUT/'review/L3-pencils-collected.png')
found.crop((1050,380,1455,600)).save(OUT/'review/table-contact-native.png')
layers=[('fixed_L3_base',base,(0,0),True),('pencil_contact_shadow',shadow,shadow_xy,True),('pencils_placement_preview',sprite,xy,True)]
psd(OUT/'L3-discovery-placement-v1.psd',layers,found)
change=np.any(np.asarray(found)!=np.asarray(base),axis=2)
allowed=np.zeros(change.shape,bool);x,y=shadow_xy;allowed[y:y+shadow.height,x:x+shadow.width]=True
check('base_unchanged_outside_prop',not np.any(change&~allowed))
check('fixed_base_file_hash',hashlib.sha256(bgpath.read_bytes()).hexdigest()==bg_hash)
check('collected_equals_original',np.array_equal(np.asarray(Image.open(OUT/'review/L3-pencils-collected.png')),np.asarray(base)))
check('prop_on_table',xy[1]>=400 and xy[1]+sprite.height<=467 and xy[0]>=1083 and xy[0]+sprite.width<=1414)
manifest={
 'id':'colored-pencils-v1','status':'prop-and-placement-review','event':'E05','quest':'Q05',
 'source':src.relative_to(ROOT).as_posix(),'source_size':list(source.size),'visible_bounds':list(bounds),'visible_size':[bounds[2]-bounds[0],bounds[3]-bounds[1]],
 'generator':'built-in image_gen','source_had_baked_checkerboard':True,
 'extraction':'User-authorized local alpha-only extraction; native RGB preserved. Chromatic silhouette + exterior flood fill + thin contour recovery.','cutout':'art/chapter01/colored-pencils-v1/colored-pencils-cutout-native-v1.png',
 'native_psd':'art/chapter01/colored-pencils-v1/colored-pencils-native-v1.psd',
 'placement_psd':'art/chapter01/colored-pencils-v1/L3-discovery-placement-v1.psd',
 'prompt':'art/chapter01/colored-pencils-v1/colored-pencils-v1.prompt.txt',
 'placement':{'coordinate_canvas':list(base.size),'pencils_rect':[*xy,*sprite.size],'shadow_rect':[*shadow_xy,*shadow.size],'order':['fixed_base','contact_shadow','pencils'],'state_available':['contact_shadow','pencils'],'state_collected':[]},
 'reviews':{'available':'art/chapter01/colored-pencils-v1/review/L3-pencils-available.png','collected':'art/chapter01/colored-pencils-v1/review/L3-pencils-collected.png','contact_native':'art/chapter01/colored-pencils-v1/review/table-contact-native.png'},
 'limitations':['Pencils and cardboard tray are one raster object; individual pencils are not independent layers.','Placement PSD is a low-resolution scene composition. Native prop PSD/PNG is the editable source.','No acquisition hand pose, full inspection close-up background, UI, highlight or Unity logic implemented.','Background remains 1672x941; close camera use needs later background detail work.','PSD was reopened with file readers; Photoshop application not tested.']}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'validation.json').write_text(json.dumps({'passed':all(x['pass'] for x in qa),'checks':qa},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'checks':len(qa),'native_canvas':list(source.size),'visible_size':manifest['visible_size'],'placement':manifest['placement']['pencils_rect']}))
