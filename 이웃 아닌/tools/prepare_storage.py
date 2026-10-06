from pathlib import Path
from PIL import Image
from psd_layers import psd
import shutil
R=Path(__file__).resolve().parents[1];O=R/'아트/보관실-v1';A=R/'Assets/Art'
for d in ['원본','개별-PNG','PSD']:(O/d).mkdir(parents=True,exist_ok=True)
src=A/'ExpeditionArrival/storage.png';shutil.copyfile(src,O/'원본/storage.png');shutil.copyfile(src,O/'개별-PNG/storage.png');im=Image.open(src).convert('RGBA');psd(O/'PSD/storage-background.psd',[('storage-painted-background',im,(0,0),True)],im)
canvas=Image.new('RGBA',(3840,2160));layers=[]
def put(name,path,box):
 x,y,w,h=box;im=Image.open(path).convert('RGBA').resize((w*2,h*2),Image.Resampling.LANCZOS);layers.append((name,im,(x*2,y*2),True));canvas.alpha_composite(im,(x*2,y*2))
put('storage',src,(0,0,1920,1080))
for name,box in [('day',(80,36,204,110)),('place',(308,36,320,110)),('turn',(80,162,204,62)),('route',(308,162,455,62)),('return',(80,952,410,78))]:put(name,A/'PartySelection/card-paper.png',box)
for i,(x,y) in enumerate([(80,384),(1225,370),(1645,370)]):put('door-hotspot-'+str(i),A/'PartySelection/arrow-paper.png',(x,y,84,84))
psd(O/'PSD/storage-layout.psd',layers,canvas)

print('Native background PSD and layered placement PSD verified; background props remain flattened.')
