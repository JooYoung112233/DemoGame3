"""Export existing UI art as a layered layout; retain original PNG pixels separately."""
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw
from psd_layers import psd
import json, shutil

R=Path(__file__).resolve().parents[1]
O=R/'아트/시간진행-v1'
for name in ['원본','개별-PNG','PSD']:(O/name).mkdir(parents=True,exist_ok=True)
layout=json.loads((O/'layout.json').read_text(encoding='utf-8-sig'))
sources={e['source'] for group in layout.values() for e in group if e['source']}
manifest=[]
for source in sorted(sources):
 p=R/source;name=p.parent.name+'-'+p.name
 shutil.copyfile(p,O/'원본'/name);shutil.copyfile(p,O/'개별-PNG'/name)
 manifest.append({'source':source,'copy':name,'size':Image.open(p).size})

for group,entries in layout.items():
 canvas=Image.new('RGBA',(3840,2160));layers=[]
 def put(name,im,x,y):
  layers.append((name,im,(round(x*2),round(y*2)),True));canvas.alpha_composite(im,(round(x*2),round(y*2)))
 for e in entries:
  color=tuple(round(c*255) for c in e['color']);w,h=round(e['w']*2),round(e['h']*2)
  if not w or not h or color[3]==0:continue
  if e['source']:
   im=Image.open(R/e['source']).convert('RGBA')
   if e['aspect']:
    im.thumbnail((w,h),Image.Resampling.LANCZOS);ratio=min(w/im.width,h/im.height)
    im=im.resize((round(im.width*ratio),round(im.height*ratio)),Image.Resampling.LANCZOS)
    box=Image.new('RGBA',(w,h));box.alpha_composite(im,((w-im.width)//2,(h-im.height)//2));im=box
   else:im=im.resize((w,h),Image.Resampling.LANCZOS)
   im=ImageChops.multiply(im,Image.new('RGBA',im.size,color))
  else:im=Image.new('RGBA',(w,h),color)
  put(e['name'],im,e['x'],e['y'])
 psd(O/'PSD'/f'{group}-layout.psd',layers,canvas)
 canvas.resize((1920,1080),Image.Resampling.LANCZOS).save(O/f'{group}-art-preview.png')
 print(group,len(layers),'layers verified')
(O/'sources.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
