"""Local pixel-preserving cutout helpers; user authorized this workflow."""
from PIL import Image,ImageDraw
import numpy as np

def polygon_cut(image,rect,outline,holes=()):
    x,y,w,h=rect
    mask=Image.new('L',(w*4,h*4),0);d=ImageDraw.Draw(mask)
    local=lambda pts:[((px-x)*4,(py-y)*4) for px,py in pts]
    d.polygon(local(outline),fill=255)
    for hole in holes:d.polygon(local(hole),fill=0)
    mask=mask.resize((w,h),Image.Resampling.LANCZOS)
    out=image.convert('RGB').crop((x,y,x+w,y+h)).convert('RGBA');out.putalpha(mask)
    return out

def gray_backdrop_cut(image,rect,holes=()):
    x,y,w,h=rect
    crop=image.convert('RGB').crop((x,y,x+w,y+h));a=np.array(crop).astype(np.int16)
    lo=a.min(axis=2);hi=a.max(axis=2);chroma=hi-lo
    candidate=(chroma<16)&(lo>130)
    regions=Image.fromarray(candidate.astype(np.uint8)).copy()
    # Flood only the exterior and explicitly inspected holes. Eye highlights stay.
    for px,py in [(0,0),(w-1,0),(0,h-1),(w-1,h-1)]+[(int(px-x),int(py-y)) for px,py in holes]:
        if 0<=px<w and 0<=py<h and regions.getpixel((px,py))==1:
            ImageDraw.floodfill(regions,(px,py),2)
    remove=np.array(regions)==2
    alpha=np.where(remove,0,255).astype(np.uint8)
    component=Image.fromarray((alpha>0).astype(np.uint8)).copy()
    yy,xx=np.where(alpha>0)
    if len(xx)==0:raise ValueError('No foreground found')
    nearest=np.argmin((xx-w/2)**2+(yy-h/2)**2)
    ImageDraw.floodfill(component,(int(xx[nearest]),int(yy[nearest])),2)
    alpha=np.where(np.array(component)==2,alpha,0).astype(np.uint8)
    out=crop.convert('RGBA');out.putalpha(Image.fromarray(alpha))
    bbox=out.getchannel('A').getbbox()
    if not bbox:raise ValueError('No foreground found')
    out=out.crop(bbox)
    return out,[x+bbox[0],y+bbox[1],out.width,out.height]

def inspect_asset(asset,output,name):
    assert asset.mode=='RGBA' and asset.getchannel('A').getextrema()==(0,255),name
    output.mkdir(exist_ok=True,parents=True)
    for color,suffix in [('#263943','dark'),('#eee5d4','light')]:
        panel=Image.new('RGBA',asset.size,color);panel.alpha_composite(asset)
        panel.thumbnail((680,550));panel.save(output/(name+'-'+suffix+'.png'))

def trim_warm_backdrop(asset):
    a=np.array(asset).astype(np.int16);r,g,b=a[:,:,0],a[:,:,1],a[:,:,2]
    candidate=(a[:,:,3]==0)|((r-b>22)&(r-b<68)&(r-g<36)&(g>130)&(b>112))
    region=Image.fromarray(candidate.astype(np.uint8)).copy();w,h=asset.size
    for seed in [(0,0),(w-1,0),(0,h-1),(w-1,h-1)]:
        if region.getpixel(seed)==1:ImageDraw.floodfill(region,seed,2)
    alpha=np.where(np.array(region)==2,0,a[:,:,3]).astype(np.uint8)
    out=asset.copy();out.putalpha(Image.fromarray(alpha));return out
