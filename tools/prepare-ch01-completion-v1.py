"""Native art, explicit occlusion copies and fixed-background Chapter 1 states."""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFilter,ImageOps
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter01/completion-v1';qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
for d in ['layers','psd','review']:(OUT/d).mkdir(exist_ok=True)
def rel(p):return p.relative_to(ROOT).as_posix()
def read(p):return Image.open(ROOT/p).convert('RGBA')
def trim(im):return im.crop(im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox())
def fit(im,w=None,h=None):
 im=trim(im)
 if w is None:w=round(im.width*h/im.height)
 else:h=round(im.height*w/im.width)
 return im.resize((w,h),Image.Resampling.LANCZOS)
def shadow(w,h,a=75):
 im=Image.new('RGBA',(w,h),(29,24,18,0));m=Image.new('L',(w,h));ImageDraw.Draw(m).ellipse((1,1,w-2,h-2),fill=a);im.putalpha(m.filter(ImageFilter.GaussianBlur(1.2)));return im
def cut(src,seeds=[]):
 rgb=np.asarray(src)[:,:,:3].astype('int16');neutral=(rgb.max(2)-rgb.min(2)<24)&(rgb.min(2)>70)
 m=Image.fromarray(neutral.astype('uint8')).copy()
 for xy in [(0,0),(src.width-1,0),(0,src.height-1),(src.width-1,src.height-1)]+seeds:
  if m.getpixel(xy)==1:ImageDraw.floodfill(m,xy,2)
 alpha=Image.fromarray(np.where(np.asarray(m)==2,0,255).astype('uint8')).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(.25))
 im=src.copy();im.putalpha(alpha);return im
def masked(im,poly):
 m=Image.new('L',im.size);ImageDraw.Draw(m).polygon(poly,fill=255);v=im.copy();v.putalpha(Image.fromarray(np.minimum(np.asarray(m),np.asarray(im.getchannel('A')))));return v
assets={};entries=[];scenes=[];renders=[]
seeds={'wrench':[(136,607)],'suhyeok-retrieve':[(435,627),(250,806)]+[(x,y) for x in range(180,730,12) for y in range(20,280,12)],'suhyeok-care':[(675,640),(690,1014),(750,1235)]+[(x,y) for x in range(380,930,12) for y in range(20,280,12)],'exchange-npc':[(x,y) for x in range(350,690,10) for y in range(0,135,10)],'sejin-dialogue':[(570,1430)],'camera-handover':[(1079,616)]}
for p in sorted((OUT/'sources').glob('*.png')):
 if 'crop' in p.stem:continue
 src=Image.open(p).convert('RGBA');original_alpha=src.getchannel('A').getextrema()[0]<255
 im=src.copy() if original_alpha or p.stem=='bed-wet' else cut(src,seeds.get(p.stem,[]))
 assets[p.stem]=im;dest=OUT/'layers'/f'{p.stem}-native.png';im.save(dest)
 box=im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox()
 check(p.stem+':source_RGB_preserved',np.array_equal(np.asarray(src)[:,:,:3],np.asarray(im)[:,:,:3]))
 check(p.stem+':visible',box is not None)
 if p.stem!='bed-wet':check(p.stem+':transparent_corner',im.getpixel((0,0))[3]==0)
 psd(OUT/'psd'/f'{p.stem}-native.psd',[('source_hidden',src,(0,0),False),(p.stem,im,(0,0),True)],im)
 proof=Image.new('RGBA',im.size,(74,65,51,255));proof.alpha_composite(im);proof.thumbnail((600,600));proof.save(OUT/'review'/f'{p.stem}-alpha-proof.png')
 entries.append({'id':p.stem,'source':rel(p),'native':rel(dest),'psd':rel(OUT/'psd'/f'{p.stem}-native.psd'),'native_size':list(im.size),'visible_bounds_alpha16':box,'visible_size':[box[2]-box[0],box[3]-box[1]],'extraction':'original alpha preserved' if original_alpha else ('opaque state source' if p.stem=='bed-wet' else 'neutral connected exterior cut; RGB unchanged')})
BG={'HUB':'art/chapter01/revision-v3/camper-clean-base-v3.png','L1':'art/chapter01/revision-v3/store-clean-base-v3.png','L5':'art/chapter01/revision-v3/house-clean-base-v3.png'}
for loc,folder in [('L2','gas-station'),('L3','general-store'),('L4','repair-shop'),('L6','riverside'),('L7','hill-road')]:BG[loc]=f'art/chapter01/{folder}-v1/{folder}-clean-base-source-v1.png'
def scene(name,loc,layers,events,note=''):
 if loc=='HUB':
  c0=json.loads((ROOT/'design/chapter00/camper-assets-v1.json').read_text(encoding='utf8'));retained=[];ids={x[0] for x in layers}
  for key in ['blanket-packed','water-packed']:
   lid='conditional_c0_'+key
   if lid in ids or (key=='water-packed' and 'shared_water' in ids):continue
   aid,rect=c0['placements'][key];x,y,w,h=rect;retained.append((lid,read(c0['assets'][aid]['path']).resize((w,h),Image.Resampling.LANCZOS),(x,y)))
  layers=retained+layers
 base=read(BG[loc]);merged=base.copy();stack=[('fixed_base',base,(0,0),True)];slots=[];allowed=np.zeros((base.height,base.width),bool)
 for lid,im,xy in layers:
  x,y=xy;check(name+':'+lid+':in_canvas',x>=0 and y>=0 and x+im.width<=base.width and y+im.height<=base.height)
  merged.alpha_composite(im,xy);stack.append((lid,im,xy,True));allowed[y:y+im.height,x:x+im.width]=True
  p=OUT/'layers'/f'{name}-{lid}.png';im.save(p);slot={'id':lid,'path':rel(p),'rect':[*xy,*im.size]}
  if lid.startswith('conditional_c0_'):slot['condition']=lid.removeprefix('conditional_c0_').replace('-','_')+' == true; review assumes retained Chapter 0 supplies'
  slots.append(slot)
 merged.save(OUT/'review'/f'{name}.png');psd(OUT/'psd'/f'{name}.psd',stack,merged)
 delta=np.any(np.asarray(base)!=np.asarray(merged),axis=2);check(name+':base_outside_layers_unchanged',not np.any(delta&~allowed))
 scenes.append({'id':name,'events':events,'location':loc,'background':BG[loc],'canvas':list(base.size),'layers':slots,'review':rel(OUT/'review'/f'{name}.png'),'psd':rel(OUT/'psd'/f'{name}.psd'),'note':note});renders.append((name,merged));return merged
def prop(name,im,xy,w):
 v=fit(im,w=w);return [(name+'_contact',shadow(max(4,w-6),max(5,round(w*.1))), (xy[0]+3,xy[1]+v.height-max(4,round(w*.07)))),(name,v,xy)]
# E08 bag is held in the new joint hand/bag pose; never duplicate the old shelf bag.
actor=fit(assets['suhyeok-retrieve'],h=167)
scene('E08-L4-retrieve','L4',[('feet_contact',shadow(100,13),(855,551)),('suhyeok_with_bag',actor,(775,398))],['E08'],'Joint hands and bag action; shelf bag absent. Pose is the moment after lifting, not an animated reach.')
scene('E10-L4-tool-available','L4',prop('wrench',assets['wrench'],(745,333),85),['E10'])
scene('E10-L4-tool-collected','L4',[],['E10'],'Same fixed counter after optional tool collection.')
# Camera exchange is a dedicated close action cut without embedding UI or text.
handover=fit(assets['camera-handover'],w=1672)
handover=handover.crop((0,max(0,(handover.height-941)//2),1672,min(handover.height,max(0,(handover.height-941)//2)+941)))
scene('E09-L2-camera-handover','L2',[('camera_handover_close_cut',handover,(0,941-handover.height))],['E09'],'Intentional foreground close cut with sleeves entering from screen edges. Same location, not giant in-world hands; native master retained. Unity handles focus and transition.')
sejin_m=json.loads((ROOT/'art/chapter01/sejin-v1/manifest.json').read_text(encoding='utf8'))
van_layers=[(v['id'],read(v['path']),tuple(v['rect'][:2])) for v in sejin_m['scene']['layers'] if v['id'] not in ['sejin_contact_shadow','sejin_standing','camera_bench_shadow','camera_on_bench']]
repair=ImageOps.mirror(fit(assets['sejin-repair'],h=210))
scene('E07-L2-sejin-repair','L2',van_layers+[('repair_feet_contact',shadow(62,11),(281,619)),('sejin_repair',repair,(264,420))],['E07'],'Mirrored pose faces the open engine; vehicle remains the same native master. Tool/hand are joint painted action art.')
# E12 left dog, right caregiver: camera-side silhouette avoids overlap with the frightened dog.
dog=read('art/chapter01/dog-v2/layers/alert-native.png');bowl=read('art/chapter01/dog-v2/layers/bowl-native.png')
dg=fit(dog,w=76);bw=fit(bowl,w=39);care=ImageOps.mirror(fit(assets['suhyeok-care'],h=159))
doglayers=[('dog_contact',shadow(67,9),(390,544)),('dog_wary',dg,(385,551-dg.height))]
waterlayers=prop('water_bowl',bowl,(519,583-bw.height),39)
scene('E12-L5-place-water','L5',doglayers+waterlayers+[('care_contact',shadow(100,12),(523,574)),('suhyeok_release_bowl',care,(502,428))],['E12'],'Reaching hand releases the bowl; next cut removes caregiver before trust response. No forced touch.')
# Keep the old bowl outer contour exactly: new food is clipped to the old inner opening.
old=trim(bowl);new=trim(assets['dog-food']).resize(old.size,Image.Resampling.LANCZOS)
mask=Image.new('L',old.size);ImageDraw.Draw(mask).ellipse((int(old.width*.095),int(old.height*.10),int(old.width*.91),int(old.height*.64)),fill=255)
food=old.copy();food.paste(new,(0,0),mask);food.putalpha(old.getchannel('A'));food.save(OUT/'layers/dog-food-stable-bowl.png')
psd(OUT/'psd/dog-food-stable-bowl.psd',[('original_water_bowl',old,(0,0),True),('food_interior',masked(new,[(int(old.width*.1),int(old.height*.2)),(int(old.width*.9),int(old.height*.2)),(int(old.width*.9),int(old.height*.6)),(int(old.width*.1),int(old.height*.6))]),(0,0),False),('food_state',food,(0,0),True)],food)
scene('E12-L5-food-alternative','L5',doglayers+prop('food_bowl',food,(519,583-bw.height),39),['E12'],'Alternative to water, not a second compulsory payment. Outer bowl alpha preserved.')
# Local bed patch only. Do not replace pillows, lantern, bed frame or the lower blanket.
wet=assets['bed-wet'].resize((285,322),Image.Resampling.LANCZOS)
wet=masked(wet,[(2,36),(65,35),(120,48),(154,72),(155,100),(2,105)])
wet.putalpha(wet.getchannel('A').filter(ImageFilter.GaussianBlur(2)))
patch=fit(assets['repair-tape'],w=110)
scene('E15-HUB-wet-bed','HUB',[('wet_blanket_patch',wet,(1110,328))],['E15','E16'],'Only rendered if bedding actually became wet.')
scene('E15-HUB-temporary-repair','HUB',[('wet_blanket_patch',wet,(1110,328)),('roof_cloth_patch',patch.resize((110,16)),(1112,162))],['E15'],'Temporary roof cloth cover candidate; exact repair recipe remains undecided. Does not instantly dry bedding.')
scene('E16-HUB-dry-bed','HUB',[('roof_cloth_patch',patch.resize((110,16)),(1112,162))],['E16'],'Original dry blanket restored only after drying resolves; do not reset repaired roof.')
cloth=fit(assets['drying-cloth'].rotate(9,Image.Resampling.BICUBIC,expand=True),w=275)
scene('E16-L6-drying','L6',[('drying_cloth',cloth,(1100,405))],['E16'],'Cloth rotated 9 degrees to follow the existing bench surface; drying duration/weather logic deferred.')
# Use established kitchen actors and table occlusion, changing only food source.
k=json.loads((ROOT/'design/chapter01/kitchen.json').read_text(encoding='utf8'))
def klayer(v):
 x,y,w,h=v['rect'];return(v['id'],read(v['asset']).resize((w,h),Image.Resampling.LANCZOS),(x,y))
scene('E04-HUB-stove-off','HUB',[],['E04'],'Unlit existing stove. Fault identity, controls and repair recipe are not invented by the image.')
cook=[klayer(v) for v in k['slots'] if v['id'] in ['can','pot','cook']]
scene('E17-HUB-cooking','HUB',cook+[klayer(k['foreground'])],['E04','E17'],'Existing cooking pose and pot are unlocked after repair. Table foreground hides the far foot; steam/light are Unity effects.')
meal=[klayer(v) for v in k['actors'] if v['id'] in ['suhyeok-seated-body','soi-seated-body']]+[klayer(k['foreground'])]
meal+=prop('father_meal',assets['warm-meal'],(872,469),58)+prop('soi_meal',assets['warm-meal'],(988,469),50)
meal +=[klayer(v) for v in k['actors'] if v['id'] in ['suhyeok-seated-hands','soi-seated-hands']]
scene('E17-HUB-warm-meal','HUB',meal,['E17'],'Two portions only after actual allocation; Soi scene is Suhyeok perspective.')
scene('E18-HUB-cabinet-secured','HUB',[('cloth_door_strap',patch.resize((130,12)),(897,239))],['E18'],'Optional cloth strap across existing cabinet handles; neither contents nor doors reconstructed.')
# E19 owner note shares existing large blank paper; writing is Unity content, not baked pixels.
note=read('art/chapter01/day01-v1/evacuation-notice-paper-clean.png')
scene('E19-L3-owned-goods','L3',prop('owned_cloth',assets['repair-tape'],(1110,409),91)+[('note_contact',shadow(44,7),(1157,444)),('owner_note_blank',trim(note).resize((44,25),Image.Resampling.LANCZOS),(1157,422))],['E19'],'Ownership note is next to optional cloth goods, never blocks first pencils. Paper flattened for counter perspective; native sheet retained. Text is a future Unity layer.')
trader=fit(assets['exchange-npc'],h=187)
scene('E20-L2-exchange','L2',[('trader_contact',shadow(48,9),(928,558)),('unnamed_exchange_npc',trader,(916,380))]+prop('parcel',assets['parcel'],(840,540),67),['E19','E20'],'Provisional unnamed role, separate from Sejin. Parcel is set on the ground for collection; no invented biography.')
# Crate front-wall copy is above parcel. Original master remains untouched.
crate=fit(assets['delivery-crate'],w=116);cw,ch=crate.size
front=masked(crate,[(0,int(ch*.38)),(int(cw*.3),int(ch*.60)),(cw,int(ch*.27)),(cw,ch),(0,ch)])
front.save(OUT/'layers/crate-front-occlusion-copy.png')
cratelayers=prop('crate',assets['delivery-crate'],(973,564),116)
scene('E20-L5-empty-crate','L5',cratelayers,['E20'])
parcel=fit(assets['parcel'],w=66)
scene('E20-L5-delivered','L5',cratelayers+[('parcel_in_crate',parcel,(1004,589)),('crate_front_wall_copy',front,(973,564))],['E20'],'Delivery marker distinct from dog encounter; front wall hides lower parcel. Never gates dog discovery.')
# Pouch content on a native prop canvas, with front cloth occluding lower camera.
pouch=trim(assets['camera-pouch']);cam=fit(read('art/chapter01/photographer-props-v1/layers/camera-native.png'),w=round(pouch.width*.55));px=round(pouch.width*.24);py=round(pouch.height*.07)
frontp=masked(pouch,[(0,int(pouch.height*.42)),(int(pouch.width*.48),int(pouch.height*.49)),(pouch.width,int(pouch.height*.38)),(pouch.width,pouch.height),(0,pouch.height)])
occupied=pouch.copy();occupied.alpha_composite(cam,(px,py));occupied.alpha_composite(frontp)
occupied.save(OUT/'layers/camera-pouch-occupied-native.png');frontp.save(OUT/'layers/pouch-front-occlusion-copy-native.png')
psd(OUT/'psd/camera-pouch-occupied-native.psd',[('pouch_native',pouch,(0,0),True),('camera',cam,(px,py),True),('front_cloth_copy',frontp,(0,0),True)],occupied)
scene('E21-HUB-pouch-empty','HUB',prop('camera_pouch',pouch,(1006,476),59),['E21'])
scene('E21-HUB-camera-protected','HUB',prop('camera_pouch',occupied,(1006,476),59),['E21'])
scene('E23-L2-fuel-option','L2',prop('fuel_can',assets['fuel-can'],(1219,325),45),['E23'],'Optional fuel stock; no early fuel shortage is implied. Availability is checked before cost is committed.')
scene('E23-L2-fuel-collected','L2',[],['E23'],'Removed only if stock was actually collected; pump/fuel transfer behavior deferred.')
scene('E25-L7-road-choice','L7',prop('road_barrier',assets['road-barrier'],(1010,274),260),['E25'],'Main uphill road barrier; left route remains open. Exit unlock needs road info, first drawing and first photo, not all optional quests.')
# E24 only displays memories actually acquired. Mandatory memories and optional companion are separate states.
book=read('art/chapter01/first-drawing-v2/layers/book-complete-native.png')
photo=read('art/chapter01/first-photo-v1/review/first-print-native.png')
reflect=[klayer(v) for v in k['actors'] if v['id'] in ['suhyeok-seated-body','soi-seated-body']]+[klayer(k['foreground'])]
reflect+=prop('completed_book',book,(894,459),100)+[('photo_contact',shadow(43,6),(1009,503)),('first_photo',trim(photo).resize((43,27),Image.Resampling.LANCZOS),(1009,481))]
reflect+=[klayer(v) for v in k['actors'] if v['id'] in ['suhyeok-seated-hands','soi-seated-hands']]
scene('E24-HUB-memories','HUB',reflect,['E24'],'Requires both first drawing and first photo. If either is absent, select only an owned memory instead of fabricating experience.')
mat=read('art/chapter01/dog-v2/layers/mat-native.png');rest=read('art/chapter01/dog-v2/layers/rest-native.png')
scene('E24-HUB-memories-with-dog','HUB',reflect+prop('dog_mat',mat,(678,640),109)+prop('resting_dog',rest,(697,645),68),['E24'],'Optional variant only if dog joined and its place exists. No quest generated by reflection.')
# Contact sheets: legible labels are review-only, never in delivered game layers.
for offset in range(0,len(renders),6):
 group=renders[offset:offset+6];sheet=Image.new('RGB',(1600,3*480),(32,33,29));d=ImageDraw.Draw(sheet)
 for i,(name,im) in enumerate(group):
  small=im.convert('RGB').resize((790,445),Image.Resampling.LANCZOS);x=(i%2)*800;y=(i//2)*480;sheet.paste(small,(x,y));d.text((x+8,y+450),name,fill=(235,229,210))
 sheet.save(OUT/'review'/f'scene-contact-{offset//6+1:02}.jpg',quality=91)
manifest={'id':'chapter01-completion-v1','status':'current-storyboard-art-proposals-reviewed-separately-from-final-resolution','assets':entries,'scenes':scenes,'limitations':['Background masters are 1672x941, below the 3840x2160 target. No upscale claimed as restored detail.','Action masters are individual native generations; consult actual visible sizes, some below 1536px height target.','PSD layers validated with binary reader and merged-pixel comparison; Photoshop application and Unity import not tested.','Hand-tool joint actions and occlusion copies are not articulated body parts or reconstructed hidden surfaces.','Exchange NPC, repair details and optional props are design candidates, not user-approved canon.','Expression sets, final dialogue, UI, audio, effects, minigame rules, quest persistence and Unity runtime remain later work.']}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
(ROOT/'design/chapter01/completion-v1-validation.json').write_text(json.dumps({'passed':all(c['pass'] for c in qa),'checks':qa},ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'assets':len(entries),'scenes':len(scenes),'checks':len(qa),'passed':all(c['pass'] for c in qa)}))
