"""Extract the user-selected existing paper; do not redraw it."""
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
from psd_layers import psd
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'아트/시작화면-v2'
source=OUT/'원본/제목종이-계단형-미사용.png'
im=Image.open(source).convert('RGBA')
a=np.asarray(im).astype('int16')
mask=Image.fromarray(np.uint8((a[:,:,0]-a[:,:,2]>22)&(a[:,:,1]-a[:,:,2]>9))*255)
mask=mask.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))
ImageDraw.floodfill(mask,(0,0),128,thresh=0)
mask=Image.fromarray(np.where(np.asarray(mask)==128,0,255).astype('uint8'))
im.putalpha(mask);im=im.crop(mask.getbbox())
im.save(OUT/'개별-PNG/title-paper-stepped.png')
alternative=np.asarray(im).copy();alternative[:,:,:3]=(alternative[:,:,:3].astype(float)*[1,.92,.73]).clip(0,255).astype('uint8')
psd(OUT/'PSD/title-paper-stepped.psd',[('stepped-ivory',im,(0,0),True),('gold-alternative',Image.fromarray(alternative),(0,0),False)],im)
check=Image.new('RGBA',im.size,(35,63,67,255));check.alpha_composite(im);check.save(OUT/'검수/선택한-제목종이-알파.png')
print('Selected existing stepped paper:',im.size,'alpha:',im.getchannel('A').getextrema())
