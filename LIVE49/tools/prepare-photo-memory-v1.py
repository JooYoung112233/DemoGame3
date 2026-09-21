"""Keep generated native pixels; deliver independent frame, face mask and portrait."""
from pathlib import Path
import ast,io,re,struct,json
import numpy as np
from PIL import Image,ImageFilter,ImageDraw
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter00/memory/dialogue-v1';qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
names={'check','packbits','channel_payload','unpackbits','psd'}
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'<PSD codec>','exec'))
photo=Image.open(OUT/'family-sunset-source-v1.png').convert('RGBA')
w,h=photo.size
# Mask is authored independently, never applied destructively to the source.
mask=Image.new('RGBA',photo.size);draw=ImageDraw.Draw(mask)
draw.polygon([(620,277),(665,266),(712,277),(701,316),(683,351),(648,359),(619,333)],fill='white')
mask=mask.filter(ImageFilter.GaussianBlur(5));mask.save(OUT/'seoyeon-face-mask-v1.png')
pad=56;bottom=152;size=(w+2*pad,h+pad+bottom)
frame=Image.new('RGBA',size,'#e9dfc7');ImageDraw.Draw(frame).rectangle((pad,pad,pad+w-1,pad+h-1),fill=(0,0,0,0))
frame.save(OUT/'polaroid-frame-v1.png')
merged=Image.new('RGBA',size);merged.alpha_composite(photo,(pad,pad));merged.alpha_composite(frame)
merged.save(OUT/'family-polaroid-clean-review-v1.png')
psd(OUT/'family-polaroid-layers-v1.psd',[
 ('clean_family_photo',photo,(pad,pad),True),('separate_paper_frame',frame,(0,0),True),
 ('face_effect_mask_hidden',mask,(pad,pad),False)],merged)
source=Image.open(OUT/'soi-answer-source-v1.png').convert('RGBA')
a=np.array(source);rgb=a[:,:,:3].astype(int)
gray=(rgb.max(2)-rgb.min(2)<16)&(rgb.mean(2)>110)
if a[:,:,3].min()==255:a[:,:,3]=np.where(gray,0,255)
cut=Image.fromarray(a);cut.putalpha(cut.getchannel('A').filter(ImageFilter.MinFilter(3)))
cut.save(OUT/'soi-answer-v1.png')
check('soi-background-transparent',np.mean(np.array(cut)[:,:,3]==0)>.3)
check('soi-shirt-opaque',cut.getpixel((512,620))[3]==255)
check('soi-face-opaque',cut.getpixel((500,350))[3]==255)
psd(OUT/'soi-answer-layers-v1.psd',[('generated_source_hidden',source,(0,0),False),('soi_cutout',cut,(0,0),True)],cut)
review=Image.new('RGBA',(source.width*2,source.height),'#e9dfc7');review.paste('#25332e',(source.width,0,source.width*2,source.height));review.alpha_composite(cut);review.alpha_composite(cut,(source.width,0));review.convert('RGB').resize((1024,768)).save(OUT/'soi-answer-alpha-review.jpg')
meta={'id':'C0-photo-memory-v1','photo_native':list(photo.size),'paper_canvas':list(size),'soi_native':list(source.size),
 'files':{'photo':'family-sunset-source-v1.png','frame':'polaroid-frame-v1.png','face_mask':'seoyeon-face-mask-v1.png','photo_psd':'family-polaroid-layers-v1.psd','soi':'soi-answer-v1.png','soi_source':'soi-answer-source-v1.png','soi_psd':'soi-answer-layers-v1.psd'},
 'photo_in_frame':[pad,pad,w,h],'face_rect_photo_pixels':[610,265,110,100],
 'status':'sequence approved; art and exact effect timing are review drafts',
 'limitations':['Photo native 1672x941; below requested large master target. No detail-restoring upscale.','Soi native 1024x1536. Body is one cutout, not expression/body-part layers.','Photo people and landscape remain a single painted layer; paper and face mask are independently editable.','PSD channels verified by decoder, not Photoshop or Unity.'],
 'qa':{'passed':len(qa),'failed':0,'checks':qa}}
(OUT/'photo-memory-assets-v1.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'photo_native':photo.size,'soi_native':source.size,'checks':len(qa)}))
