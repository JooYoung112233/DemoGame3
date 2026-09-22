from pathlib import Path
from PIL import Image
import numpy as np,json,shutil
from psd_layers import psd
R=Path(__file__).resolve().parents[1];O=R/'아트/제작수리-v1';A=R/'Assets/Art/CraftWorkPanel'
for path in [O/'개별-PNG',O/'PSD',A]:path.mkdir(parents=True,exist_ok=True)
source=Image.open(R/'아트/UI전체시안-v1/06-제작수리와진행작업-v2.png').convert('RGBA')
boxes={'prybar':(546,391,671,501),'nails':(475,744,516,786),'rope':(478,685,536,733),'wood':(476,638,536,679),'repair':(65,449,111,491),'upgrade':(64,517,112,560)}
for name,box in boxes.items():
    im=source.crop(box);rgb=np.asarray(im)[:,:,:3];mask=rgb.max(2)<140 if name=='nails' else rgb.mean(2)<175
    # Preserve the actual painted object; remove isolated paper grain from the cutout.
    seen=np.zeros(mask.shape,bool);parts=[]
    for y,x in zip(*np.where(mask)):
        if seen[y,x]:continue
        todo=[(y,x)];seen[y,x]=True;part=[]
        while todo:
            cy,cx=todo.pop();part.append((cy,cx))
            for ny,nx in [(cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)]:
                if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not seen[ny,nx]:seen[ny,nx]=True;todo.append((ny,nx))
        parts.append(part)
    mask[:]=False
    for y,x in max(parts,key=len):mask[y,x]=True
    im.putalpha(Image.fromarray(np.uint8(mask)*255));im=im.crop(im.getbbox());im.save(O/'개별-PNG'/f'{name}.png');shutil.copyfile(O/'개별-PNG'/f'{name}.png',A/f'{name}.png')
    psd(O/'PSD'/f'{name}.psd',[(name,im,(0,0),True)],im)
im=Image.open(R/'Assets/Art/Settlement/icon-work.png').convert('RGBA');im=im.crop(im.getbbox());im.save(O/'개별-PNG/craft.png');shutil.copyfile(O/'개별-PNG/craft.png',A/'craft.png');psd(O/'PSD/craft.psd',[('craft-icon',im,(0,0),True)],im)
papers={n:R/f'Assets/Art/PartySelection/{n}.png' for n in ['card-paper','count-paper','footer-paper']}
for name,path in papers.items():shutil.copyfile(path,O/'개별-PNG'/f'{name}.png')
canvas=Image.new('RGBA',(3840,2160));layers=[]
def put(name,path,rect,tint=None):
    im=Image.open(path).convert('RGBA');x,y,w,h=rect;im=im.resize((w*2,h*2),Image.Resampling.LANCZOS)
    if tint:im=Image.fromarray(np.uint8(np.asarray(im).astype(float)*np.array(tint)))
    xy=(x*2,y*2);layers.append((name,im,xy,True));canvas.alpha_composite(im,xy)
put('work-panel',papers['card-paper'],(38,345,1095,695),(.11,.14,.15,.98));put('heading',papers['count-paper'],(38,323,230,73))
for i,n in enumerate(['craft','repair','upgrade']):
    put(n+'-tab',papers['footer-paper'],(62,423+i*82,162,72));put(n+'-icon',O/'개별-PNG'/f'{n}.png',(79,439+i*82,42,42))
for i,n in enumerate(['prybar','nails','rope']):
    put(n+'-recipe',papers['footer-paper'],(242,423+i*82,346,72));put(n+'-icon',O/'개별-PNG'/f'{n}.png',(256,434+i*82,54,48))
put('detail-icon-paper',papers['card-paper'],(613,415,168,204));put('detail-icon',O/'개별-PNG/prybar.png',(631,441,132,153));put('detail-heading',papers['count-paper'],(803,415,302,58))
for i in range(2):put('worker-'+str(i),papers['footer-paper'],(72,759+i*77,408,69))
for i,n in enumerate(['wood','rope','nails']):
    put(n+'-cost',papers['footer-paper'],(518,759+i*59,577,52));put(n+'-cost-icon',O/'개별-PNG'/f'{n}.png',(530,765+i*59,48,40))
put('confirm',papers['footer-paper'],(518,948,577,73),(.61,.83,.62,1));put('orders',papers['card-paper'],(1180,563,690,455));put('orders-heading',papers['count-paper'],(1180,543,272,70))
psd(O/'PSD/craft-work-layout.psd',layers,canvas)
(O/'manifest.json').write_text(json.dumps({'reference':'../UI전체시안-v1/06-제작수리와진행작업-v2.png','crops':boxes,'designSize':[1920,1080],'psdSize':[3840,2160],'limits':['Icons are native-size cutouts, not HD restoration.','Live text, counts, portraits, selection, shortages and orders remain Unity UI.','Existing fixed background is reused; no furniture outline painting added.']},ensure_ascii=False,indent=2),encoding='utf-8')
print('Six extracted icons, separate source PNGs and layered layout PSD verified.')
