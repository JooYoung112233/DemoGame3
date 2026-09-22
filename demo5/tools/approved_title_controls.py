"""Use approved source artwork for control papers and icons; no new icon design."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageFilter
from psd_layers import psd
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'아트/시작화면-승인원본-1대1/개별-PNG'
OUT=ROOT/'아트/시작화면-v2/개별-PNG'
P=ROOT/'아트/시작화면-v2/PSD'

def icon(source,box,name):
    im=Image.open(source).convert('RGBA').crop(box);a=np.asarray(im)
    # Source ink only, with transparent holes. Preserve the original dark RGB.
    alpha=np.where(a[:,:,:3].max(axis=2)<155,255,0).astype('uint8')
    if name=='icon-exit':
        alpha=np.where(a[:,:,:3].min(axis=2)>130,255,0).astype('uint8')
        im=Image.new('RGBA',im.size,(19,27,27,255))
    im.putalpha(Image.fromarray(alpha));im.save(OUT/(name+'.png'))
    psd(P/(name+'.psd'),[(name,im,(0,0),True)],im)
    return im
ref20=ROOT/'아트/UI전체시안-v1/20-시작과정착지선택.png'
ref19=ROOT/'아트/UI전체시안-v1/19-일시정지와설정.png'
icon(ref20,(1164,127,1209,176),'icon-new-game')
icon(ref20,(1165,53,1206,98),'icon-continue')
icon(ref20,(1161,201,1209,253),'icon-settings')
icon(ref19,(98,535,155,584),'icon-load')
icon(ref19,(1563,686,1603,724),'icon-exit')
icon(ref20,(1443,62,1470,89),'icon-arrow')
# Remove only dark source ink; the reference paper pixels remain on the paper layer.
def clean_paper(box):
    im=Image.open(ref20).convert('RGBA').crop(box);a=np.array(im)
    for px,py,pr,pb in [(60,7,117,65),(116,7,287,65),(343,7,381,65)]:
        crop=a[py:pb,px:pr]
        ink=Image.fromarray(np.uint8(crop[:,:,:3].max(axis=2)<190)*255).filter(ImageFilter.MaxFilter(11))
        # Blank strip from this very button; repeat its native texture behind
        # the removed lettering instead of leaving antialiased letter ghosts.
        sample=a[py:pb,289:336].copy()
        patch=np.tile(sample,(1,(pr-px)//sample.shape[1]+1,1))[:,:pr-px]
        crop[np.asarray(ink)>0]=patch[np.asarray(ink)>0]
    return Image.fromarray(a)
paper=clean_paper((1096,116,1493,187))
a=np.asarray(paper)
# Alpha-cut the corners and outer dark environment, keeping paper itself opaque.
mask=(a[:,:,0].astype('int16')-a[:,:,2]>15)&(a[:,:,0]>120)
paper.putalpha(Image.fromarray(np.uint8(mask)*255));paper.save(OUT/'button-paper-reference.png')
gold=clean_paper((1096,39,1493,113));a=np.asarray(gold)
mask=(a[:,:,0].astype('int16')-a[:,:,2]>25)&(a[:,:,0]>120)
gold.putalpha(Image.fromarray(np.uint8(mask)*255));gold.save(OUT/'button-gold-reference.png')
psd(P/'approved-button-paper.psd',[('ivory-paper',paper,(0,0),True),('gold-paper-alternative',gold,(0,0),False)],paper)

# Current runtime art layout, with every button/icon separate. Text remains live
# Unity text, not baked into these assets. Native originals stay alongside it.
master=Image.new('RGBA',(3840,2160));ls=[]
def place(name,im,rect):
    x,y,w,h=rect;tile=im.resize((round(w*2),round(h*2)),Image.Resampling.LANCZOS)
    xy=(round(x*2),round(y*2));ls.append((name,tile,xy,True));master.alpha_composite(tile,xy)
layout=json.loads((OUT.parent/'layout.json').read_text(encoding='utf-8'))
place('city-clean',Image.open(OUT/'city-clean.png'),(0,0,1920,1080))
for c in layout['characters']:
    x,y=c['xy'];w,h=c['size'];place(c['name'],Image.open(OUT/(c['name']+'.png')),(x/1672*1920,y/941*1080,w/1672*1920,h/941*1080))
heading=Image.open(OUT/'title-paper-stepped.png')
headingHeight=670*heading.height/heading.width
place('title-paper-stepped',heading,(110,80,670,headingHeight))
for name,y,h in [('new-game',332,86),('continue',434,86),('load',536,86),('settings',638,86),('exit',740,74)]:
    place(name+'-paper',gold if name=='new-game' else paper,(1220,y,550,h))
    im=Image.open(OUT/('icon-'+name+'.png'));ratio=min(64/im.width,64/im.height);w=im.width*ratio;ih=im.height*ratio
    place(name+'-icon',im,(1311+(64-w)/2,y+(h-ih)/2,w,ih))
    place(name+'-arrow',Image.open(OUT/'icon-arrow.png'),(1710,y+(h-20)/2,20,20))
psd(P/'start-screen-art-layout.psd',ls,master)
master.resize((1920,1080),Image.Resampling.LANCZOS).save(OUT.parent/'검수/분리리소스-배치.png')
layout['font']='Assets/Art/FrontEnd/Fonts/Gaegu/Gaegu-Bold.ttf'
layout['bodyFont']='Assets/Art/FrontEnd/Fonts/Gaegu/Gaegu-Regular.ttf'
layout['titlePaper']={'file':'title-paper-stepped.png','rect':[110,80,670,headingHeight],'projectRect':[64,30,542,30],'projectFontSize':24,'titleRect':[60,72,548,82],'titleFontSize':72,'subtitleRect':[64,168,280,98],'subtitleFontSize':34,'subtitleLineSpacing':1.12,'subtitle':'다시, 살아갈\n준비를 합니다.','alignment':'optical left alignment; title side bearing compensated by 4px; UpperLeft'}
for item in layout['ui']:
    if item['name']=='title-paper': item['rect']=[110,80,670,headingHeight]
layout['buttonInternal']={'iconLeft':91,'iconBox':[64,64],'labelLeft':166,'labelFontSize':42,'sourceButtonWidth':397,'runtimeButtonWidth':550}
layout['controlsSource']='approved mockups 20 and 19; source papers/ink separated, hidden ink areas repaired with source paper'
(OUT.parent/'layout.json').write_text(json.dumps(layout,ensure_ascii=False,indent=2),encoding='utf-8')
print('Approved controls: original paper and six original icons exported with PSDs.')
