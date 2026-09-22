"""Approved-row source extraction and layered PSD packaging. No new art style."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageFilter,ImageOps
from psd_layers import psd
R=Path(__file__).resolve().parents[1];O=R/'아트/모험가선택-v1'
for p in ['개별-PNG','PSD','검수']:(O/p).mkdir(parents=True,exist_ok=True)
src=Image.open(R/'아트/UI전체시안-v1/20-시작과정착지선택.png').convert('RGBA')
manifest=[]
def save(name,im):
    im.save(O/'개별-PNG'/f'{name}.png');manifest.append({'name':name,'width':im.width,'height':im.height})
    return im
def dark(box,name):
    im=src.crop(box);a=np.asarray(im);mask=a[:,:,:3].max(axis=2)<110
    if name.startswith('portrait-'):
        # Only the connected person silhouette; discard the nearby source check badge.
        seen=np.zeros(mask.shape,dtype=bool);parts=[]
        for y,x in zip(*np.where(mask)):
            if seen[y,x]:continue
            stack=[(y,x)];seen[y,x]=True;component=[]
            while stack:
                cy,cx=stack.pop();component.append((cy,cx))
                for ny,nx in ((cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)):
                    if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not seen[ny,nx]:seen[ny,nx]=True;stack.append((ny,nx))
            parts.append(component)
        mask[:]=False
        for y,x in max(parts,key=len):mask[y,x]=True
    im.putalpha(Image.fromarray(np.uint8(mask)*255));return save(name,im)
portraits=[]
for name,box in [('scout',(510,346,650,452)),('mechanic',(692,346,820,452)),('medic',(860,345,1000,452)),('cook',(1040,345,1170,452)),('researcher',(1210,346,1346,452)),('guard',(1383,346,1518,452))]:
    portraits.append(dark(box,'portrait-'+name))
bag=dark((518,494,543,525),'icon-bag');heart=dark((589,495,620,523),'icon-heart')
party=dark((100,520,154,570),'icon-party')
left=src.crop((424,431,478,488));a=np.asarray(left);left.putalpha(Image.fromarray(np.uint8(a[:,:,:3].max(axis=2)>90)*255))
# Fill the arrow's ink hole with adjacent blank paper before adding the separate icon.
left.paste(src.crop((458,450,464,464)).resize((32,40)),(11,9));save('arrow-paper',left)
arrow=dark((438,442,463,477),'icon-left');save('icon-right',ImageOps.mirror(arrow))
check=src.crop((619,321,662,367));a=np.asarray(check);yy,xx=np.mgrid[:check.height,:check.width];check.putalpha(Image.fromarray(np.uint8(((xx-21)**2+(yy-23)**2)<21**2)*255));save('selected-check',check)
# Blank source paper area from a card: text/portraits remain independent assets.
patch=src.crop((716,331,815,345));save('paper-texture',patch)
paper=Image.new('RGBA',(166,260));tile=Image.open(R/'아트/시작화면-v2/개별-PNG/title-paper-stepped.png').convert('RGBA').crop((180,120,500,621)).resize((166,260),Image.Resampling.LANCZOS)
paper.alpha_composite(tile)
# Preserve native card outer shape from the unselected mechanic card.
original=src.crop((673,325,838,585));a=np.asarray(original);mask=(a[:,:,0].astype('int16')-a[:,:,2]>10)&(a[:,:,0]>90)
mask=Image.fromarray(np.uint8(mask)*255).filter(ImageFilter.MaxFilter(3))
# Outer silhouette from a rounded source card, holes from source ink filled.
from PIL import ImageDraw
mask=ImageOps.expand(mask,3,fill=0);ImageDraw.floodfill(mask,(0,0),128,thresh=0)
mask=Image.fromarray(np.where(np.asarray(mask)==128,0,255).astype('uint8')).crop((3,3,168,263)).resize(paper.size)
paper.putalpha(mask);save('card-paper',paper)
# Source-selected border only: preserve the ochre selection stroke, no portrait.
selected=src.crop((494,321,664,587));v=np.asarray(selected).astype('int16')
yy,xx=np.mgrid[:selected.height,:selected.width]
border=((xx<8)|(xx>161)|(yy<8)|(yy>257))&(v[:,:,0]>140)&(v[:,:,0]-v[:,:,2]>55)
selected.putalpha(Image.fromarray(np.uint8(border)*255));save('selected-border',selected)
tile=src.crop((1540,329,1620,413));background=Image.new('RGBA',(160,168))
background.paste(tile,(0,0));background.paste(ImageOps.mirror(tile),(80,0));background.paste(ImageOps.flip(tile),(0,84));background.paste(ImageOps.flip(ImageOps.mirror(tile)),(80,84));save('teal-texture',background)
# Approved already-selected stepped heading and paper button, not regenerated.
heading=Image.open(R/'아트/시작화면-v2/개별-PNG/title-paper-stepped.png').convert('RGBA');save('heading-paper',heading)
count=Image.open(R/'아트/시작화면-v2/개별-PNG/title-paper.png').convert('RGBA');save('count-paper',count)
footer=Image.open(R/'아트/시작화면-v2/개별-PNG/button-paper-reference.png').convert('RGBA');save('footer-paper',footer)
for i,name in enumerate(['scout','mechanic','medic','cook','researcher','guard']):
    p=portraits[i];layers=[('paper',paper,(0,0),True),('portrait',p,(round((166-p.width)/2),20),True),('bag-icon',bag,(19,168),True),('heart-icon',heart,(88,168),True)]
    merged=Image.new('RGBA',paper.size)
    for _,im,xy,_ in layers:merged.alpha_composite(im,xy)
    psd(O/'PSD'/f'candidate-{name}.psd',layers,merged)
    psd(O/'PSD'/f'portrait-{name}.psd',[('silhouette',p,(0,0),True)],p)
state=Image.new('RGBA',selected.size);state.alpha_composite(selected);state.alpha_composite(check,(125,0));psd(O/'PSD/selection-state.psd',[('border',selected,(0,0),True),('check',check,(125,0),True)],state)
# Full 2x art placement document. Raster resampling is layout, not recovered detail.
master=Image.new('RGBA',(3840,2160));ls=[]
def place(name,im,rect):
    x,y,w,h=rect;t=im.resize((round(w*2),round(h*2)),Image.Resampling.LANCZOS);pos=(round(x*2),round(y*2));ls.append((name,t,pos,True));master.alpha_composite(t,pos)
bg=Image.new('RGBA',(1920,1080))
for y in range(0,1080,168):
    for x in range(0,1920,160):bg.alpha_composite(background,(x,y))
place('background',bg,(0,0,1920,1080));place('heading',heading,(64,94,440,229));place('count-paper',count,(76,348,302,110))
place('party-icon',party,(100,375,52,48));place('back-paper',footer,(76,944,330,74));place('continue-paper',footer,(1450,944,330,74))
for i,p in enumerate(portraits):
    x=570+i*201;place('card-'+str(i),paper,(x,122,190,298));place('portrait-'+str(i),p,(x+15,144,160,125))
    place('bag-'+str(i),bag,(x+23,315,25,29));place('heart-'+str(i),heart,(x+109,315,29,28))
place('details-paper',count,(76,552,1704,340));place('details-portrait',portraits[0],(124,644,160,125))
# Native paper plus independently switchable native portrait layers; text is live Unity UI.
details=count.copy();details.alpha_composite(portraits[0],(54,150))
psd(O/'PSD/candidate-details.psd',[('paper',count,(0,0),True)]+[(name,p,(54,150),i==0) for i,(name,p) in enumerate(zip(['scout','mechanic','medic','cook','researcher','guard'],portraits))],details)
psd(O/'PSD/party-selection-art-layout.psd',ls,master)
master.resize((1920,1080)).save(O/'검수/리소스배치.png')
(O/'manifest.json').write_text(json.dumps({'source':'../UI전체시안-v1/20-시작과정착지선택.png','assets':manifest,'layoutCanvas':[3840,2160],'limitations':['Native source portraits approximately 130x106; not HD final art.','Card paper uses the approved larger title paper texture and source card silhouette; layout enlargement is not restored detail.','Runtime text stays in Unity; PSD contains independently editable artwork layers.']},ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared',len(manifest),'assets and layered PSDs from approved source.')
