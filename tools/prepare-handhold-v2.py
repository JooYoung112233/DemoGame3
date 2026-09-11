"""Correct the stacked-hands staging; retain original generated pixels and separate backdrop."""
from pathlib import Path
import ast,io,re,struct,json
import numpy as np
from PIL import Image,ImageFilter
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter00/memory/dialogue-v1';qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
names={'check','packbits','channel_payload','unpackbits','psd'}
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'<PSD codec>','exec'))
source=Image.open(OUT/'hands-held-source-v2.png').convert('RGBA')
a=np.asarray(source).copy();rgb=a[:,:,:3].astype(int)
gray=(np.max(rgb,2)-np.min(rgb,2)<18)&(rgb.mean(2)>85)
if a[:,:,3].min()==255:a[:,:,3]=np.where(gray,0,255)
cut=Image.fromarray(a)
# Remove the baked pale antialias fringe at the silhouette boundary only.
cut.putalpha(Image.fromarray(a[:,:,3]).filter(ImageFilter.MinFilter(3)))
a=np.asarray(cut)
cut.save(OUT/'hands-held-v2.png')
check('alpha-background',np.mean(a[:,:,3]==0)>.3)
check('central-grip-opaque',a[round(source.height*.60),round(source.width*.56),3]==255)
ground=Image.open(OUT/'path-closeup-review-v1.png').convert('RGBA')
assert ground.size==cut.size
merged=ground.copy();merged.alpha_composite(cut);merged.save(OUT/'hands-held-review-v2.png')
psd(OUT/'hands-held-layers-v2.psd',[
 ('generated_source_hidden',source,(0,0),False),
 ('existing_ground_preview',ground,(0,0),True),
 ('father_and_child_holding',cut,(0,0),True)],merged)
meta={'id':'C0-handhold-v2','reason':'User said three stacked hands looked like a team cheer. Show only father and daughter holding hands after mother releases.',
 'source':'art/chapter00/memory/dialogue-v1/hands-held-source-v2.png',
 'foreground':'art/chapter00/memory/dialogue-v1/hands-held-v2.png',
 'review':'art/chapter00/memory/dialogue-v1/hands-held-review-v2.png',
 'psd':'art/chapter00/memory/dialogue-v1/hands-held-layers-v2.psd',
 'native_size':list(cut.size),'alpha_bounds':list(cut.getchannel('A').getbbox()),
 'background':'art/chapter00/memory/dialogue-v1/path-closeup-review-v1.png',
 'implementation':'After Suhyeok responds, show the completed two-person handhold; mother handing over is implied, no three-hand intermediate frame.',
 'limitations':['Native 1672x941 below 3840 target; no detail-restoring upscaling.','Two contacting hands are one foreground group, not individually movable fingers.','Ground is the unchanged prior crop preview.','No Photoshop application or Unity testing.'],
 'processing':'Built-in imagegen, then neutral checkerboard removal under prior user permission; original retained.',
 'qa':{'checks':qa,'passed':len(qa),'failed':0},'status':'corrected pose draft awaiting visual feedback'}
(OUT/'hands-held-v2.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'native_size':list(cut.size),'passed':len(qa),'failed':0}))
