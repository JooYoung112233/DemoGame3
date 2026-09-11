"""A face-free close insert for the existing family photograph, with native sources."""
from pathlib import Path
import ast,io,re,struct,json
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
ROOT=Path(__file__).resolve().parents[1];O=ROOT/'art/chapter00/photo-care-v1';qa=[]
for d in ['layers','psd','review']:(O/d).mkdir(exist_ok=True)
for file,names in [('tools/build-ui-title-styles.py',{'check','packbits','channel_payload','unpackbits','psd'}),('tools/prepare-ch01-completion-v1.py',{'cut','trim','fit','shadow'})]:
 tree=ast.parse((ROOT/file).read_text(encoding='utf8'));exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'<helpers>','exec'))
def rel(p):return p.relative_to(ROOT).as_posix()
def read(p):return Image.open(ROOT/p).convert('RGBA')
assets={};inventory=[]
for p in sorted((O/'sources').glob('*.png')):
 src=Image.open(p).convert('RGBA');im=src if p.stem=='wall-close' or src.getchannel('A').getextrema()[0]<255 else cut(src)
 assets[p.stem]=im;im.save(O/'layers'/f'{p.stem}-native.png');psd(O/'psd'/f'{p.stem}-native.psd',[('source_hidden',src,(0,0),False),(p.stem,im,(0,0),True)],im)
 check(p.stem+':RGB_original',np.array_equal(np.asarray(src)[:,:,:3],np.asarray(im)[:,:,:3]))
 inventory.append({'id':p.stem,'source':rel(p),'native':rel(O/'layers'/f'{p.stem}-native.png'),'size':list(im.size),'visible_bounds':im.getchannel('A').getbbox(),'psd':rel(O/'psd'/f'{p.stem}-native.psd')})
# The source photograph is only cropped, never regenerated. All faces lie above y=700.
family=read('art/chapter00/memory/dialogue-v1/family-polaroid-clean-review-v1.png')
crop_native=family.crop((0,700,1784,1149));crop_native.save(O/'layers/original-photo-lower-crop.png');crop=crop_native.resize((1000,252),Image.Resampling.LANCZOS)
base=assets['wall-close'].crop((0,0,1448,815));check('canvas',base.size==(1448,815))
pressed=assets['tape-pressed'];lift=assets['tape-lifted'];check('tape_source_same_canvas',pressed.size==lift.size)
# Stable left anchor: only the final 24% uses the new curl source.
mask=Image.new('L',pressed.size);dr=ImageDraw.Draw(mask);dr.rectangle((int(pressed.width*.76),0,pressed.width,pressed.height),fill=255)
mask=mask.filter(ImageFilter.GaussianBlur(3));liftstate=pressed.copy();liftstate.paste(lift,(0,0),mask);liftstate.save(O/'layers/tape-lifted-stable-native.png')
check('left_anchor_pixels_stable',np.array_equal(np.asarray(liftstate)[:,:int(pressed.width*.74)],np.asarray(pressed)[:,:int(pressed.width*.74)]))
box=pressed.getchannel('A').getbbox();flat=pressed.crop(box).resize((280,55),Image.Resampling.LANCZOS);curled=liftstate.crop(box).resize((280,55),Image.Resampling.LANCZOS)
hand=fit(assets['pressing-hand'],w=880);hand=hand.crop((0,0,798,min(hand.height,595)))
ha=hand.getchannel('A').point(lambda v:round(v*.16)).filter(ImageFilter.GaussianBlur(5));hs=Image.new('RGBA',hand.size,(45,30,15,0));hs.putalpha(ha)
common=[('wall_insert',base,(0,0),True),('photo_edge_contact',shadow(1000,13,55),(100,246),True),('existing_photo_lower_crop',crop,(100,0),True)]
states=[]
for name,tape,showhand in [('01-lifted',curled,False),('02-pressing',flat,True),('03-secured',flat,False)]:
 layers=common+[('tape_contact',shadow(272,7,45),(425,273),True),('same_tape_corner',tape,(420,225),True)]
 if showhand:layers+=[('hand_contact',hs,(650,220),True),('suhyeok_pressing_hand',hand,(650,220),True)]
 result=Image.new('RGBA',base.size);slots=[]
 for lid,im,xy,visible in layers:
  result.alpha_composite(im,xy);p=O/'layers'/f'{name}-{lid}.png';im.save(p);slots.append({'id':lid,'path':rel(p),'rect':[*xy,*im.size]})
 result.save(O/'review'/f'{name}.png');psd(O/'psd'/f'{name}.psd',layers,result);states.append({'id':name,'review':rel(O/'review'/f'{name}.png'),'psd':rel(O/'psd'/f'{name}.psd'),'layers':slots})
 # Lower photo crop excludes all identity/face pixels; source offset is fixed for all cuts.
 check(name+':photo_crop_unchanged_above_hand',np.array_equal(np.asarray(result)[:200,100:1100],np.asarray(crop)[:200]))
sheet=Image.new('RGB',(1500,405),(32,31,27));d=ImageDraw.Draw(sheet)
for i,s in enumerate(states):
 im=read(s['review']).convert('RGB');im.thumbnail((490,365));sheet.paste(im,(i*500,0));d.text((i*500+10,375),s['id'],fill='white')
sheet.save(O/'review/photo-care-sequence.jpg',quality=93)
manifest={'id':'chapter00-photo-care-v1','status':'storyboard-art-proposal-not-Unity-runtime','assets':inventory,'canvas':[1448,815],'states':states,'original_photo':'art/chapter00/memory/dialogue-v1/family-polaroid-clean-review-v1.png','photo_source_crop':[0,700,1784,1149],'face_exclusion':'Only lower original photo is included; original face area is outside all three delivered close cuts. Do not pan or zoom beyond this crop.','world_anchor_1920':[1442,269,44,28.34],'story_nodes':['photo_observe','photo_care','photo_wish'],'limitations':['Close wall insert is a new matching paint study, not recovered detail from the tiny world placement. Full camper geometry is unchanged.','Native wall is 1448x1086; close composition canvas is 1448x815; not final 4K or 1920-wide sharp master. No upscale claims.','Hand is one static cutout, not articulated fingers. Tape left anchor retains original pixels; only curled end changes.','PSD binary/merged-image checked; Photoshop application and Unity not tested.','Dialogue, contact sound and transition timing remain implementation work.']}
(O/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8');(ROOT/'design/audit/ch00-week01/photo-care-validation.json').write_text(json.dumps({'passed':all(v['pass'] for v in qa),'checks':qa},indent=2)+'\n',encoding='utf8')
print(json.dumps({'sources':len(inventory),'states':len(states),'checks':len(qa),'passed':all(v['pass'] for v in qa)}))
