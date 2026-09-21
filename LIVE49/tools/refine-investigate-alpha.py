"""Remove inspected pale source-background gaps between the upper hair strands."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageDraw
from asset_cutout import inspect_asset
ROOT=Path(__file__).resolve().parents[1]
folder=ROOT/'art/chapter01/extra-poses'
source=Image.open(folder/'suhyeok-investigate-v1.png').convert('RGBA')
a=np.array(source);r,g,b=[a[:,:,i].astype(np.int16) for i in range(3)]
yy,xx=np.indices(a.shape[:2])
hair_gaps=(xx>=110)&(xx<=265)&(yy<75)&(r-b>22)&(r-b<68)&(r-g<36)&(g>160)&(b>140)&(a[:,:,3]>0)
before=a.copy();a[hair_gaps,3]=0
candidate=((r-b>22)&(r-b<68)&(r-g<36)&(g>130)&(b>112)).astype(np.uint8)
regions=Image.fromarray(candidate).copy()
seed=(158,216)
assert regions.getpixel(seed)==1,'Inspect the enclosed arm gap before changing the seed.'
ImageDraw.floodfill(regions,seed,2)
arm_gap=(np.array(regions)==2)&(a[:,:,3]>0)
a[arm_gap,3]=0
assert np.array_equal(a[:,:,:3],before[:,:,:3])
out=Image.fromarray(a);out.save(folder/'suhyeok-investigate-v2.png');inspect_asset(out,folder/'qa','suhyeok-investigate-v2')
report={'source':'art/chapter01/extra-poses/suhyeok-investigate-v1.png','asset':'art/chapter01/extra-poses/suhyeok-investigate-v2.png','operation':'alpha-only removal of inspected upper-hair gaps and enclosed arm gap','clearedPixels':int(hair_gaps.sum()+arm_gap.sum()),'hairPixels':int(hair_gaps.sum()),'armGapPixels':int(arm_gap.sum()),'rgbUnchanged':True,'size':list(out.size)}
(folder/'investigate-alpha-v2.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(report)
