"""Separate native Suhyeok sprite; retain generated source and real PSD layers."""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageFilter
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter00/memory/dialogue-v1';qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
names={'check','packbits','channel_payload','unpackbits','psd'}
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'<PSD codec>','exec'))
path=OUT/'suhyeok-response-semi-source-v2.png';source=Image.open(path).convert('RGBA')
a=np.array(source);rgb=a[:,:,:3].astype(int);original_alpha=a[:,:,3].min()<255
if not original_alpha:
 gray=(rgb.max(2)-rgb.min(2)<18)&(rgb.mean(2)>110)
 a[:,:,3]=np.where(gray,0,255)
 cut=Image.fromarray(a);cut.putalpha(cut.getchannel('A').filter(ImageFilter.MinFilter(3)))
else:cut=source.copy()
cut.save(OUT/'suhyeok-response-semi-v2.png')
alpha=np.array(cut)[:,:,3];bounds=cut.getchannel('A').getbbox()
check('native-size-preserved',cut.size==source.size)
check('transparent-surround',np.mean(alpha==0)>.2)
for name,xy in [('face',(550,330)),('shirt',(520,740)),('trousers',(570,1250)),('left-hand',(185,1330)),('right-hand',(843,1400))]:
 check(name+'-opaque',cut.getpixel(xy)[3]==255)
psd(OUT/'suhyeok-response-semi-layers-v2.psd',[
 ('generated_source_hidden',source,(0,0),False),('suhyeok_transparent_paint',cut,(0,0),True)],cut)
review=Image.new('RGBA',(cut.width*2,cut.height),'#eee4cf');review.paste('#23302c',(cut.width,0,cut.width*2,cut.height));review.alpha_composite(cut);review.alpha_composite(cut,(cut.width,0))
review.convert('RGB').resize((1024,768)).save(OUT/'suhyeok-response-semi-alpha-review-v2.jpg',quality=95)
meta={'id':'suhyeok-dialogue-semi-v2','status':'in-game replacement draft; visual refinement pending feedback','native_size':list(source.size),'alpha_bounds':list(bounds),'body_pixel_height':bounds[3]-bounds[1],
 'generated_source':path.relative_to(ROOT).as_posix(),'runtime':'art/chapter00/memory/dialogue-v1/suhyeok-response-semi-v2.png','psd':'art/chapter00/memory/dialogue-v1/suhyeok-response-semi-layers-v2.psd',
 'previous':'art/chapter00/memory/dialogue-v1/suhyeok-response-v1.png','rect_1920':[100,50,720,1080],
 'style_reference':'art/style-reference/2026-09-10/mother-semi-realistic-approved-v1.png','face_study':'art/style-revision/2026-09-10/suhyeok-face-study-v1.png',
 'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'alpha_processing':'native alpha preserved' if original_alpha else 'neutral checker removal + 1px fringe trim under prior explicit user permission',
 'psd_scope':'2 actual RGBA layers: hidden full native source; visible isolated native character. Head, hands, clothing and expression remain one painted character layer.',
 'limitations':['Native 1024x1536; subject height below 1536-2048 target. No detail-restoring upscale.','Current Soi and scene backgrounds still old style; only one asset replaced.','No Photoshop application or Unity runtime test.'],
 'qa':{'passed':len(qa),'failed':0,'checks':qa}}
(OUT/'suhyeok-response-semi-v2.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'native_size':source.size,'alpha_bounds':bounds,'passed':len(qa),'failed':0}))
