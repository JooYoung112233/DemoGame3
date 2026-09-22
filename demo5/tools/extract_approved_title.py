"""Exact-pixel reference reconstruction, not a newly generated design or final HD art."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageDraw
from psd_layers import psd

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'아트/시작화면-승인원본-1대1'
for p in ['개별-PNG','PSD','검수']:(OUT/p).mkdir(parents=True,exist_ok=True)
original=Image.open(ROOT/'아트/UI전체시안-v1/20-시작과정착지선택.png').convert('RGBA').crop((0,0,1672,290))
original.save(OUT/'승인원본-첫화면.png')
base=original.copy();layers=[];manifest=[]

def add(name,im,xy,role):
    im.save(OUT/'개별-PNG'/f'{name}.png')
    layers.append((name,im,xy,True))
    manifest.append({'name':name,'file':f'개별-PNG/{name}.png','x':xy[0],'y':xy[1],'width':im.width,'height':im.height,'role':role})
def panel(name,box,parts):
    x,y,r,b=box;im=original.crop(box);a=np.array(im)
    # Occluded original background is unknown in this flattened mockup.
    # Mark only that hidden area as neutral underlay, never invent a new city.
    ImageDraw.Draw(base).rectangle((x,y,r-1,b-1),fill=(28,49,53,255))
    pieces=[]
    for part,rect in parts:
        px,py,pr,pb=rect
        tile=im.crop(rect);v=np.asarray(tile)
        mask=np.max(v[:,:,:3],axis=2)<170
        alpha=Image.fromarray(np.uint8(mask)*255)
        tile.putalpha(alpha)
        # Borrow blank paper from within this same original panel for hidden ink.
        # All original visible ink pixels remain on their own transparent layer.
        area=a[py:pb,px:pr]
        paper=a[max(4,min(im.height-5,py)),max(4,im.width-45),:3].copy()
        area[:,:,:3][mask]=paper
        pieces.append((name+'-'+part,tile,(x+px,y+py),'source-ink'))
    add(name+'-paper',Image.fromarray(a),(x,y),'source-paper')
    for args in pieces:add(*args)

panel('heading',(17,0,425,219),[
 ('number',(37,42,106,105)),('title',(124,46,393,120)),('subtitle',(76,128,261,198))])
for name,box in [('continue',(1096,39,1493,113)),('new-game',(1096,116,1493,187)),('settings',(1096,190,1493,262))]:
    panel(name,box,[('icon',(67,10,113,63)),('label',(120,10,284,62)),('arrow',(347,10,378,62))])
# Separate the quiet hand-written caption too, preserving exact lettering.
panel('caption',(1517,138,1629,211),[('lettering',(0,0,112,73))])
add('environment',base,(0,0),'source-environment')
layers=[layers[-1],*layers[:-1]]
manifest=[manifest[-1],*manifest[:-1]]
merged=Image.new('RGBA',original.size)
for _,im,xy,_ in layers:merged.alpha_composite(im,xy)
diff=np.abs(np.asarray(merged).astype('int16')-np.asarray(original).astype('int16'))
assert not diff.any(),int(diff.max())
merged.save(OUT/'검수/재조립.png')
psd(OUT/'PSD/approved-start-exact.psd',layers,merged)
for name,box in [('heading',(17,0,425,219)),('continue',(1096,39,1493,113)),('new-game',(1096,116,1493,187)),('settings',(1096,190,1493,262))]:
    selected=[(n,im,(xy[0]-box[0],xy[1]-box[1]),v) for n,im,xy,v in layers if n.startswith(name+'-')]
    psd(OUT/'PSD'/f'{name}.psd',selected,original.crop(box))
comparison=Image.new('RGBA',(1672,580));comparison.alpha_composite(original,(0,0));comparison.alpha_composite(merged,(0,290));comparison.save(OUT/'검수/원본-위-재조립-아래.png')
(OUT/'layers.json').write_text(json.dumps({'width':1672,'height':290,'layers':manifest,'comparison':{'differentPixels':int(np.count_nonzero(diff.max(axis=2))),'maxChannelDifference':int(diff.max())}},ensure_ascii=False,indent=2),encoding='utf-8')
print('Exact reference reconstructed: '+str(len(layers))+' layers; differing pixels: 0')
