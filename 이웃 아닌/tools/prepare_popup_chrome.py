from pathlib import Path
from PIL import Image,ImageChops
from psd_layers import psd
import shutil,json
r=Path(__file__).resolve().parents[1];o=r/'아트/공통팝업프레임-v1'
for n in ['원본','개별-PNG','PSD']:(o/n).mkdir(parents=True,exist_ok=True)
src=r/'Assets/Art/PartySelection/footer-paper.png'
for n in ['원본','개별-PNG']:shutil.copyfile(src,o/n/'paper.png')
layouts={}
for name,w,right in [('search',410,1442),('bags',410,1442),('loot',410,1442),('time',320,1532),('return',320,1532),('inventory',410,1442),('craft',410,1442),('rest',410,1442)]:
 entries=[('heading',80,32,540,72),('back',80,974,w,76),('confirm',right,974,w,76)]
 if name=='inventory':entries=[entries[0],entries[1],('notice',776,974,1076,76)]
 if name=='return':entries.append(('inspect-bags',1178,974,320,76))
 canvas=Image.new('RGBA',(3840,2160));layers=[]
 for label,x,y,ww,hh in entries:
  im=Image.open(src).convert('RGBA').resize((ww*2,hh*2),Image.Resampling.LANCZOS)
  if label=='confirm' and name in ['search','bags','loot','craft','rest']:im=ImageChops.multiply(im,Image.new('RGBA',im.size,(194,219,173,255)))
  layers.append((label,im,(x*2,y*2),True));canvas.alpha_composite(im,(x*2,y*2))
 psd(o/'PSD'/f'{name}-chrome.psd',layers,canvas);layouts[name]=entries
(o/'layout.json').write_text(json.dumps(layouts,ensure_ascii=False,indent=2),encoding='utf-8')
print('Eight layered popup chrome PSDs verified; original paper preserved.')
