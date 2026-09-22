"""Separate existing approved icon pixels. No newly generated illustration."""
from PIL import Image
import numpy as np
from psd_layers import psd
def prepare(root):
    out=root/'아트/정착지선택-v1';icons={}
    for name,source,box in [
        ('icon-supplies','01-개별인벤토리.png',(125,240,180,293)),
        ('icon-ammo','10-분리진형전투.png',(1297,666,1374,743)),
        ('icon-location','20-시작과정착지선택.png',(1335,705,1385,757))]:
        im=Image.open(root/'아트/UI전체시안-v1'/source).convert('RGBA').crop(box)
        mask=np.asarray(im)[:,:,:3].max(axis=2)<110
        im.putalpha(Image.fromarray(np.uint8(mask)*255));icons[name]=im
    icons['icon-recovery']=Image.open(root/'아트/모험가선택-v1/개별-PNG/icon-heart.png').convert('RGBA')
    merged=Image.new('RGBA',(384,96));layers=[]
    for i,(name,im) in enumerate(icons.items()):
        im.save(out/'개별-PNG'/f'{name}.png');xy=(i*96,0);layers.append((name,im,xy,True));merged.alpha_composite(im,xy)
    psd(out/'PSD/home-icons.psd',layers,merged)
    return icons
