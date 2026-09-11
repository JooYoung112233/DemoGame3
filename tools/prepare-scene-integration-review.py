"""Scene-bound paint patch review; explicitly not an isolated character master."""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageDraw
R=Path(__file__).resolve().parents[1];O=R/'art/chapter00-01/scene-integration-v1';qa=[]
tree=ast.parse((R/'tools/build-ui-title-styles.py').read_text(encoding='utf8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
def read(p):return Image.open(R/p).convert('RGBA')
def rel(p):return p.relative_to(R).as_posix()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
basepath='art/chapter00-01/background-harmony-v2/sources/house.png'
priorpath='art/chapter00-01/background-harmony-v2/review/E12-L5-place-water.png'
base=read(basepath);prior=read(priorpath);source=read(rel(O/'sources/house-context-paint.png'))
check('native_canvas',base.size==source.size==(1672,941))
rect=(330,340,675,635);x0,y0,x1,y1=rect;w=x1-x0;h=y1-y0
yy,xx=np.mgrid[0:h,0:w];dist=np.minimum.reduce([xx,yy,w-1-xx,h-1-yy]);a=(np.clip(dist/22,0,1)*255).astype('uint8')
patch=source.crop(rect);patch.putalpha(Image.fromarray(a));patch.save(O/'layers/house-context-region.png')
result=base.copy();result.alpha_composite(patch,(x0,y0));result.save(O/'review/house-integrated.png')
allowed=np.zeros((941,1672),bool);allowed[y0:y1,x0:x1]=True
delta=np.any(np.asarray(result)!=np.asarray(base),axis=2)
check('outside_context_region_exact_original_background',not np.any(delta&~allowed))
check('patch_is_feathered_region_not_character_alpha',patch.getchannel('A').getextrema()==(0,255))
check('opaque_composite',result.getchannel('A').getextrema()==(255,255))
layers=[('previous_scene_hidden',prior,(0,0),False),('fixed_empty_house',base,(0,0),True),('context_paint_includes_floor',patch,(x0,y0),True)]
psd(O/'psd/house-integration-review.psd',layers,result)
psd(O/'psd/house-context-generated-native.psd',[('generated_flat_native',source,(0,0),True)],source)
crop=(320,320,890,650)
sheet=Image.new('RGB',(1672,900),(35,34,30));d=ImageDraw.Draw(sheet)
for i,(label,im) in enumerate([('BEFORE',prior),('IN-CONTEXT STUDY',result)]):
    x=i*836;sheet.paste(im.convert('RGB').resize((836,471),Image.Resampling.LANCZOS),(x,24));d.text((x+12,7),label,fill='white')
    piece=im.crop(crop).convert('RGB');sheet.paste(piece,(x+(836-piece.width)//2,540));d.text((x+12,515),'1:1 native crop',fill='white')
sheet.save(O/'review/house-before-after.jpg',quality=95)
manifest={'id':'scene-integration-v1','status':'single-scene-review-not-approved-global-style','previous_scene':priorpath,'fixed_background':basepath,'generated_source':rel(O/'sources/house-context-paint.png'),'native_size':list(source.size),'native_sha256':hashlib.sha256((O/'sources/house-context-paint.png').read_bytes()).hexdigest(),'prompt':rel(O/'sources/house-context-paint.prompt.txt'),'generator':'built-in image_gen','layout':'art/chapter00-01/scene-integration-v1/layout.json','review':rel(O/'review/house-integrated.png'),'comparison':rel(O/'review/house-before-after.jpg'),'psd':rel(O/'psd/house-integration-review.psd'),'patch':{'path':rel(O/'layers/house-context-region.png'),'rect':[x0,y0,w,h],'feather_px':22,'includes_background':True},'limitations':['Not an isolated transparent character asset; patch includes floor, contact shadows and local background.','Native source1672x941 is below3840px target. Proportion layout scaled existing sprites; this is not high-resolution recovery.','Generated scene source is flat; native PSD is explicitly one layer. Review PSD separates original scene, fixed background and local repaint region only.','Single house scene study; no global replacement or approved new style.','No Unity or Photoshop application validation.']}
previous_manifest=json.loads((O/'manifest.json').read_text(encoding='utf-8-sig')) if (O/'manifest.json').exists() else {}
if previous_manifest.get('user_approval'):
    manifest['user_approval']=previous_manifest['user_approval']
    manifest['status']=previous_manifest['status']
    manifest['limitations'][3]='Approved visual integration reference for this house scene; other scenes are tracked separately.'
dump(O/'manifest.json',manifest)
dump(O/'validation.json',{'passed':all(x['pass'] for x in qa),'checks':qa})
print(json.dumps({'checks':len(qa),'passed':all(x['pass'] for x in qa)}))

