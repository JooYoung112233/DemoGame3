"""Individual dog originals, alpha-only prop extraction and fixed-base state proofs."""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFilter,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter01/dog-v2';qa=[]
for d in ['layers','psd','review']:(OUT/d).mkdir(exist_ok=True)
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
def rel(p):return p.relative_to(ROOT).as_posix()
def bezier(p0,p1,p2,p3):
 return [tuple((1-t)**3*p0[k]+3*(1-t)**2*t*p1[k]+3*(1-t)*t*t*p2[k]+t**3*p3[k] for k in [0,1]) for t in np.linspace(0,1,50)]
assets={};entries=[]
for name in ['alert','sit','rest','bowl','mat']:
 srcpath=OUT/'sources'/(name+'-source.png');src=Image.open(srcpath).convert('RGBA');im=src.copy();method='original generated alpha preserved'
 if name=='mat':
  rgb=np.asarray(src)[:,:,:3].astype('int16');seed=(rgb.max(2)-rgb.min(2))>24
  m=Image.fromarray((seed*255).astype('uint8')).copy();ImageDraw.floodfill(m,(0,0),128,thresh=0)
  m=Image.fromarray((np.asarray(m)!=128).astype('uint8')*255).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(.3))
  im.putalpha(m);method='baked checkerboard removed via chroma silhouette / exterior flood fill; RGB preserved'
 if name=='bowl':
  # Trim generated exterior glow without repainting bowl pixels.
  segments=[((71,419),(78,267),(345,139),(690,129)),((690,129),(1040,99),(1448,239),(1470,409)),((1470,409),(1495,483),(1447,553),(1354,599)),((1354,599),(1280,853),(1079,930),(797,947)),((797,947),(502,951),(281,866),(177,612)),((177,612),(90,561),(51,492),(71,419))]
  poly=[p for seg in segments for p in bezier(*seg)];m=Image.new('L',im.size);ImageDraw.Draw(m).polygon(poly,fill=255)
  m=m.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(.4));im.putalpha(Image.fromarray(np.minimum(np.asarray(m),np.asarray(src.getchannel('A')))))
  method='Bezier exterior alpha trim to remove glow; original RGB preserved'
 im.save(OUT/'layers'/(name+'-native.png'));assets[name]=im
 solid=im.getchannel('A').point(lambda x:255 if x>=16 else 0).getbbox()
 check(name+':rgb_unchanged',np.array_equal(np.asarray(im)[:,:,:3],np.asarray(src)[:,:,:3]))
 check(name+':transparent_alpha',im.getchannel('A').getextrema()[0]==0)
 entries.append({'id':name,'source':rel(srcpath),'native':rel(OUT/'layers'/(name+'-native.png')),'native_size':list(im.size),'solid_bounds_alpha16':solid,'visible_size':[solid[2]-solid[0],solid[3]-solid[1]],'alpha_method':method,'source_sha256':hashlib.sha256(srcpath.read_bytes()).hexdigest()})
 psd(OUT/'psd'/(name+'-native.psd'),[('source_hidden',src,(0,0),False),(name+'_native',im,(0,0),True)],im)
 proof=Image.new('RGBA',im.size,(64,53,38,255));proof.alpha_composite(im);proof.thumbnail((850,850));proof.save(OUT/'review'/(name+'-alpha-proof.png'))
def trim(im):return im.crop(im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox())
def sprite(name,w):
 im=trim(assets[name]);return im.resize((w,round(im.height*w/im.width)),Image.Resampling.LANCZOS)
def shadow(w,h,strength=90):
 im=Image.new('RGBA',(w,h),(28,22,15,0));m=Image.new('L',(w,h));ImageDraw.Draw(m).ellipse((2,2,w-3,h-3),fill=strength);im.putalpha(m.filter(ImageFilter.GaussianBlur(1.1)));return im
bases={k:Image.open(ROOT/'art/chapter01/revision-v3'/v).convert('RGBA') for k,v in [('L5','house-clean-base-v3.png'),('HUB','camper-clean-base-v3.png')]}
states=[]
def compose(sid,location,specs,crop):
 base=bases[location];layers=[('fixed_'+location+'_base',base,(0,0),True)];merged=base.copy();slots=[];allowed=np.zeros((base.height,base.width),bool)
 for name,im,xy in specs:
  layers.append((name,im,xy,True));merged.alpha_composite(im,xy);x,y=xy;allowed[y:y+im.height,x:x+im.width]=True
  path=OUT/'layers'/(sid+'-'+name+'.png');im.save(path);slots.append({'id':name,'path':rel(path),'rect':[*xy,*im.size]})
 merged.save(OUT/'review'/(sid+'.png'));merged.crop(crop).save(OUT/'review'/(sid+'-contact-native.png'));psd(OUT/'psd'/(sid+'.psd'),layers,merged)
 delta=np.any(np.asarray(merged)!=np.asarray(base),2);check(sid+':base_unchanged_outside_layers',not np.any(delta&~allowed))
 states.append({'id':sid,'location':location,'canvas':list(base.size),'layers':slots,'review':rel(OUT/'review'/(sid+'.png')),'psd':rel(OUT/'psd'/(sid+'.psd'))})
housecrop=(230,355,765,730)
# The doorway stays accessible; ground anchors remain consistent between poses.
dog=sprite('alert',76);dogxy=(385,551-dog.height)
compose('01-L5-wary','L5',[('dog_shadow',shadow(78,12),(382,543)),('dog_alert',dog,dogxy)],housecrop)
bowl=sprite('bowl',39)
compose('02-L5-water-placed','L5',[('dog_shadow',shadow(78,12),(382,543)),('dog_alert',dog,dogxy),('bowl_shadow',shadow(39,8),(519,578)),('water_bowl',bowl,(519,583-bowl.height))],housecrop)
sit=sprite('sit',64)
compose('03-L5-relaxed','L5',[('dog_shadow',shadow(63,11),(462,567)),('dog_sit',sit,(462,577-sit.height)),('bowl_shadow',shadow(39,8),(519,578)),('water_bowl',bowl,(519,583-bowl.height))],housecrop)
mat=sprite('mat',109);matxy=(678,640)
hubcrop=(600,545,847,718)
common=[('mat_shadow',shadow(108,14),(679,688)),('folded_mat',mat,matxy)]
compose('04-HUB-place-ready','HUB',common,hubcrop)
rest=sprite('rest',68)
compose('05-HUB-rest','HUB',common+[('dog_shadow',shadow(69,10),(697,672)),('dog_rest',rest,(697,680-rest.height))],hubcrop)
panels=[]
for sid in ['01-L5-wary','02-L5-water-placed','03-L5-relaxed','04-HUB-place-ready','05-HUB-rest']:
 im=Image.open(OUT/'review'/(sid+'.png')).convert('RGB');im.thumbnail((668,377));panels.append((sid,im))
contact=Image.new('RGB',(1336,1221),(43,37,29));draw=ImageDraw.Draw(contact)
for i,(sid,im) in enumerate(panels):
 x=(i%2)*668;y=(i//2)*407;contact.paste(im,(x,y+30));draw.text((x+10,y+8),sid,fill=(227,213,181))
contact.save(OUT/'review/story-states-contact.jpg',quality=93)
manifest={'id':'dog-v2','status':'new-individual-art-and-placement-review','generator':'built-in image_gen','events':['E12','E13','E14'],'quests':['Q11','Q12'],'identity_reference':'art/chapter01/dog/byeolddongi-poses-source-v1.png','assets':entries,'states':states,'limitations':['New dog originals are 1254x1254 or 1448x1086, actual visible size recorded; not 2048 target masters.','Independent sprite originals are new generations, not upscaled old sheet cutouts.','Five original PSDs contain one visible cutout and hidden source, not separately articulated body parts.','Scene PSDs contain separate objects and contact shadows on unchanged fixed bases.','No drinking/approach walk animation or new Suhyeok placing-hand pose; no Unity implementation.','Bowl water is baked inside the bowl sprite; an empty state requires later artwork.','Backgrounds are still 1672x941. Photoshop app not tested.']}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'validation.json').write_text(json.dumps({'passed':all(c['pass'] for c in qa),'checks':qa},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'checks':len(qa),'assets':len(entries),'states':len(states),'visible_sizes':{e['id']:e['visible_size'] for e in entries}}))
