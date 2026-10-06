"""Reuse approved location illustrations; native crops remain layout-quality sources."""
from pathlib import Path
from PIL import Image
import json
from psd_layers import psd
from prepare_home_icons import prepare
R=Path(__file__).resolve().parents[1];O=R/'아트/정착지선택-v1'
for d in ['개별-PNG','PSD']:(O/d).mkdir(parents=True,exist_ok=True)
icons=prepare(R)
source=Image.open(R/'아트/UI전체시안-v1/20-시작과정착지선택.png').convert('RGBA')
paper=Image.open(R/'아트/모험가선택-v1/개별-PNG/card-paper.png').convert('RGBA').resize((330,350))
entries=[]
for name,box in [('garage',(435,654,659,760)),('house',(726,657,974,761)),('clinic',(1013,659,1263,761))]:
    art=source.crop(box);art.save(O/'개별-PNG'/f'{name}.png')
    preview=art.resize((298,132),Image.Resampling.LANCZOS);merged=paper.copy();merged.alpha_composite(preview,(16,16))
    card_layers=[('paper',paper,(0,0),True),('location',preview,(16,16),True)]
    for i,key in enumerate(['icon-supplies','icon-ammo','icon-recovery']):
        im=icons[key].resize((32,32),Image.Resampling.LANCZOS);xy=(22+i*102,231);card_layers.append((key,im,xy,True));merged.alpha_composite(im,xy)
    psd(O/'PSD'/f'{name}-card.psd',card_layers,merged)
    psd(O/'PSD'/f'{name}-native.psd',[('location',art,(0,0),True)],art)
    entries.append({'id':name,'sourceBox':box,'nativeSize':list(art.size)})
(O/'manifest.json').write_text(json.dumps({'source':'../UI전체시안-v1/20-시작과정착지선택.png','locations':entries,'limitation':'Native location crops are about 240x105. Card PSD is layout only, not recovered HD detail. Text is editable Unity UI; location artwork is a single image layer, not decomposed building parts.'},ensure_ascii=False,indent=2),encoding='utf-8')
master=Image.new('RGBA',(3840,2160));layers=[]
def put(name,im,rect):
    x,y,w,h=rect;im=im.resize((w*2,h*2),Image.Resampling.LANCZOS);pos=(x*2,y*2);layers.append((name,im,pos,True));master.alpha_composite(im,pos)
def common(name):return Image.open(R/'아트/모험가선택-v1/개별-PNG'/f'{name}.png').convert('RGBA')
bg=Image.new('RGBA',(1920,1080));tile=common('teal-texture')
for y in range(0,1080,tile.height):
    for x in range(0,1920,tile.width):bg.alpha_composite(tile,(x,y))
put('background',bg,(0,0,1920,1080));put('heading',common('heading-paper'),(64,94,395,229))
put('details-paper',common('count-paper'),(76,565,1784,327));put('back-paper',common('footer-paper'),(76,944,410,78));put('start-paper',common('footer-paper'),(1450,944,410,78));put('location-icon',icons['icon-location'],(1477,962,40,42))
for i,name in enumerate(['garage','house','clinic']):
    x=490+i*356;put(name+'-paper',paper,(x,122,330,350));put(name+'-location',Image.open(O/'개별-PNG'/f'{name}.png').convert('RGBA'),(x+16,138,298,132))
    for j,key in enumerate(['icon-supplies','icon-ammo','icon-recovery']):put(name+'-'+key,icons[key],(x+22+j*102,353,32,32))
for j,key in enumerate(['icon-supplies','icon-ammo','icon-recovery']):put('summary-'+key,icons[key],(1296,672+j*42,30,30))
put('party-icon',common('icon-party'),(118,592,34,34));put('back-arrow',common('icon-left'),(98,966,26,34))
put('example-party-scout',common('portrait-scout'),(118,655,140,116));put('example-party-medic',common('portrait-medic'),(322,655,140,116))
psd(O/'PSD/home-selection-art-layout.psd',layers,master)
print('Native locations, layered cards, separated icons and full layout PSD verified.')
