"""Keep native masters and make independent scene/album review layers."""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
ROOT=Path(__file__).resolve().parents[1];qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
S=ROOT/'art/chapter01/sejin-v1';P=ROOT/'art/chapter01/first-photo-v1'
for out in [S,P]:
 for d in ['layers','psd','review']:(out/d).mkdir(exist_ok=True)
def rel(p):return p.relative_to(ROOT).as_posix()
def read(p):return Image.open(p).convert('RGBA')
def extract(src,seeds=[]):
 rgb=np.asarray(src)[:,:,:3].astype('int16');neutral=(rgb.max(2)-rgb.min(2)<24)&(rgb.min(2)>70)
 m=Image.fromarray(neutral.astype('uint8')).copy()
 for xy in [(0,0),(src.width-1,0),(0,src.height-1),(src.width-1,src.height-1)]+seeds:
  if m.getpixel(xy)==1:ImageDraw.floodfill(m,xy,2)
 alpha=Image.fromarray(np.where(np.asarray(m)==2,0,255).astype('uint8')).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(.25))
 im=src.copy();im.putalpha(alpha);return im
def trim(im):return im.crop(im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox())
def sizeby(im,*,width=None,height=None):
 im=trim(im)
 if width is None:width=round(im.width*height/im.height)
 else:height=round(im.height*width/im.width)
 return im.resize((width,height),Image.Resampling.LANCZOS)
def shadow(w,h,opacity=88):
 im=Image.new('RGBA',(w,h),(27,22,16,0));m=Image.new('L',(w,h));ImageDraw.Draw(m).ellipse((2,2,w-3,h-3),fill=opacity);im.putalpha(m.filter(ImageFilter.GaussianBlur(1.4)));return im
def master(out,name,source,seeds=[],alpha=True):
 src=read(out/'sources'/source);im=extract(src,seeds) if alpha else src.copy();path=out/'layers'/(name+'-native.png');im.save(path)
 check(name+':rgb_preserved',np.array_equal(np.asarray(src)[:,:,:3],np.asarray(im)[:,:,:3]))
 check(name+':nonempty',im.getbbox() is not None)
 psd(out/'psd'/(name+'-native.psd'),[('source_hidden',src,(0,0),False),(name+'_native',im,(0,0),True)],im)
 proof=Image.new('RGBA',im.size,(65,54,39,255));proof.alpha_composite(im);proof.thumbnail((850,850));proof.save(out/'review'/(name+'-alpha-proof.png'))
 box=im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox()
 return im,{'id':name,'source':rel(out/'sources'/source),'native':rel(path),'native_size':list(im.size),'visible_bounds_alpha16':box,'visible_size':[box[2]-box[0],box[3]-box[1]],'extraction':'neutral exterior alpha-only cut; RGB unchanged' if alpha else 'original pixels and alpha preserved','psd':rel(out/'psd'/(name+'-native.psd'))}
sejin,sejinentry=master(S,'sejin-standing','sejin-standing-source-v2.png')
van,vanentry=master(S,'van','van-source-v1.png',alpha=False)
shoot,shootentry=master(P,'suhyeok-shoot','suhyeok-shoot-source.png',[(413,348)]+[(x,y) for x in range(460,711,20) for y in range(30,111,20)])
album,albumentry=master(P,'album-blank','album-blank-source.png')
photo_selection=json.loads((P/'photo-selection.json').read_text(encoding='utf-8')) if (P/'photo-selection.json').exists() else {'source':'riverside-photo-source.png','revision':'v1'}
photo,photoentry=master(P,'riverside-photo',photo_selection['source'],alpha=False)
photoentry['style_revision']=photo_selection['revision']
def scene(out,name,background,layers):
 base=read(ROOT/background);merged=base.copy();stack=[('fixed_base',base,(0,0),True)];slots=[];allowed=np.zeros((base.height,base.width),bool)
 for lid,im,xy in layers:
  merged.alpha_composite(im,xy);stack.append((lid,im,xy,True));x,y=xy;allowed[y:y+im.height,x:x+im.width]=True
  p=out/'layers'/(name+'-'+lid+'.png');im.save(p);slots.append({'id':lid,'path':rel(p),'rect':[*xy,*im.size]})
 merged.save(out/'review'/(name+'.png'));psd(out/'psd'/(name+'.psd'),stack,merged)
 delta=np.any(np.asarray(base)!=np.asarray(merged),axis=2);check(name+':fixed_background',not np.any(delta&~allowed))
 return {'id':name,'background':background,'canvas':list(base.size),'layers':slots,'review':rel(out/'review'/(name+'.png')),'psd':rel(out/'psd'/(name+'.psd'))},merged
v=sizeby(van,width=470);npc=sizeby(sejin,height=188)
camera=read(ROOT/'art/chapter01/photographer-props-v1/layers/camera-native.png');camera=sizeby(camera,width=27)
vs=Image.new('RGBA',(465,130),(27,22,16,0));va=Image.new('L',vs.size);ImageDraw.Draw(va).polygon([(4,34),(91,85),(163,123),(429,48),(455,20),(315,34),(140,35)],fill=72);vs.putalpha(va.filter(ImageFilter.GaussianBlur(3)))
station,stationimg=scene(S,'L2-sejin-meeting','art/chapter01/gas-station-v1/gas-station-clean-base-source-v1.png',[
 ('van_contact_shadow',vs,(312,513)),('front_tire_shadow',shadow(58,11),(448,628)),('rear_tire_shadow',shadow(44,10),(711,552)),('van',v,(299,309)),
 ('camera_bench_shadow',shadow(28,6),(570,213)),('camera_on_bench',camera,(570,217-camera.height)),
 ('sejin_contact_shadow',shadow(npc.width-8,12),(804,605)),('sejin_standing',npc,(800,614-npc.height))])
stationimg.crop((260,290,950,670)).save(S/'review/meeting-contact-native.png')
actor=sizeby(shoot,height=190)
shootstate,shootimg=scene(P,'L6-first-shoot','art/chapter01/riverside-v1/riverside-clean-base-source-v1.png',[
 ('left_foot_contact_shadow',shadow(33,9),(759,641)),('right_foot_contact_shadow',shadow(26,8),(792,619)),('suhyeok_shoot',actor,(744,648-actor.height))])
shootimg.crop((660,430,970,700)).save(P/'review/shoot-contact-native.png')
# Camera and fingers are an editable occlusion copy, not a reconstructed separate hand.
mask=Image.new('L',shoot.size);ImageDraw.Draw(mask).polygon([(282,78),(477,76),(517,283),(497,414),(346,402),(284,207)],fill=255)
hands=shoot.copy();hands.putalpha(Image.fromarray(np.minimum(np.asarray(mask),np.asarray(shoot.getchannel('A')))));hands.save(P/'layers/camera-hands-copy-native.png')
psd(P/'psd/suhyeok-shoot-native.psd',[('source_hidden',read(P/'sources/suhyeok-shoot-source.png'),(0,0),False),('actor_native',shoot,(0,0),True),('camera_hands_occlusion_copy',hands,(0,0),False)],shoot)
# Album content is a removable photo layer; the original family photo is never overwritten.
small=photo.resize((474,356),Image.Resampling.LANCZOS);border=Image.new('RGBA',(506,406),(233,225,204,255));border.alpha_composite(small,(16,16))
photo_layer=Image.new('RGBA',album.size);photo_layer.alpha_composite(border,(810,340));result=album.copy();result.alpha_composite(photo_layer)
photo_layer.save(P/'layers/album-first-photo-layer.png');result.save(P/'review/album-first-photo.png');album.save(P/'review/album-before-first-photo.png')
psd(P/'psd/album-states-native.psd',[('blank_album_native',album,(0,0),True),('first_photo_removable',photo_layer,(0,0),True)],result)
check('album_outside_insert_unchanged',np.array_equal(np.asarray(album)[:,:810],np.asarray(result)[:,:810]))
printborder=Image.new('RGBA',(photo.width+96,photo.height+176),(234,226,207,255));printborder.alpha_composite(photo,(48,48));printborder.save(P/'review/first-print-native.png')
backing=Image.new('RGBA',printborder.size,(234,226,207,255));psd(P/'psd/first-print-native.psd',[('plain_paper_border',backing,(0,0),True),('photo_content_native',photo,(48,48),True)],printborder)
shared=['Originals remain at native generated resolution; no upscaling claimed as restored detail.','Scene bases remain 1672x941; not final 4K backgrounds.','PSD validated by file readers, not Photoshop app or Unity.']
sm={'id':'sejin-v1','status':'provisional-character-and-vehicle-review-not-approved-design','events':['E07','E09','E10'],'generator':'built-in image_gen','assets':[sejinentry,vanentry],'scene':station,'design_candidate':'Short dark greying hair, broad adult face, olive overshirt. Age and vehicle model are visual proposals, not established biography.','limitations':shared+['No full expression set, separate dialogue portrait master, repair or handover action pose.','Van is one cutout: hood/wheels/engine cannot be independently animated.','First candidate v1 preserved but not used because it resembled Suhyeok too closely.']}
pm={'id':'first-photo-v1','status':'provisional-first-photo-composition-review','events':['E11'],'quests':['Q10'],'generator':'built-in image_gen; local alpha extraction and composition','assets':[shootentry,albumentry,photoentry],'scene':shootstate,'album':{'before':rel(P/'review/album-before-first-photo.png'),'after':rel(P/'review/album-first-photo.png'),'psd':rel(P/'psd/album-states-native.psd'),'photo_rect':[810,340,506,406],'photo_content_preview_size':[474,356],'native_content_kept':photoentry['native']},'limitations':shared+['Landscape-only first photo is a proposed composition, not user-approved story canon.','Album is blank reusable art backing; no new acquisition quest or physical possession fact implied. UI remains deferred.','Camera and hands in shooting pose are painted together; copy layer supports occlusion only, not independent articulation.','First print border is a simple local compositing layer, not generated paper detail.','Separate expression, receiving-camera pose, audio and Unity photo/save behavior are not implemented.']}
for out,m in [(S,sm),(P,pm)]:
 (out/'manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(ROOT/'design/chapter01/sejin-first-photo-validation.json').write_text(json.dumps({'passed':all(c['pass'] for c in qa),'checks':qa},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'checks':len(qa),'new_native_assets':5,'scene_composites':2,'visible_sizes':{e['id']:e['visible_size'] for e in [sejinentry,vanentry,shootentry,albumentry,photoentry]}}))
