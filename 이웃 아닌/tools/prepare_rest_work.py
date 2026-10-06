from pathlib import Path
from PIL import Image
import numpy as np, json, shutil
from psd_layers import psd
R=Path(__file__).resolve().parents[1]; O=R/'아트/휴식담당자-v1'; A=R/'Assets/Art/RestWorkPanel'
for p in [O/'개별-PNG',O/'PSD',A]:p.mkdir(parents=True,exist_ok=True)
source=Image.open(R/'아트/UI전체시안-v1/05-생활작업지정-v2.png').convert('RGBA')
for name,box,light in [('icon-sleep',(378,240,421,280),False),('icon-clock',(57,692,101,738),True)]:
    im=source.crop(box);rgb=np.asarray(im)[:,:,:3];mask=rgb.min(2)>170 if light else rgb.max(2)<95
    im.putalpha(Image.fromarray(np.uint8(mask)*255));im.save(O/'개별-PNG'/f'{name}.png');shutil.copyfile(O/'개별-PNG'/f'{name}.png',A/f'{name}.png')
    psd(O/'PSD'/f'{name}.psd',[(name,im,(0,0),True)],im)
sources={'task-texture':R/'Assets/Art/PartySelection/card-paper.png','header-paper':R/'Assets/Art/PartySelection/count-paper.png','row-paper':R/'Assets/Art/PartySelection/footer-paper.png','detail-paper':R/'Assets/Art/PartySelection/card-paper.png','bed-icon':R/'Assets/Art/Settlement/icon-bed.png'}
for name,path in sources.items():shutil.copyfile(path,O/'개별-PNG'/f'{name}.png')
canvas=Image.new('RGBA',(3840,2160));layers=[]
def put(name,path,rect,tint=None):
    im=Image.open(path).convert('RGBA');x,y,w,h=rect;im=im.resize((w*2,h*2),Image.Resampling.LANCZOS)
    if tint:
        arr=np.asarray(im).copy().astype(float);arr*=np.array(tint);im=Image.fromarray(np.uint8(arr))
    xy=(x*2,y*2);layers.append((name,im,xy,True));canvas.alpha_composite(im,xy)
put('dark-task-panel',sources['task-texture'],(32,130,660,918),(.11,.14,.15,.98))
put('title-paper',sources['header-paper'],(52,146,350,70));put('bed-icon',sources['bed-icon'],(67,165,53,34))
for name,x in [('short-rest',54),('sleep',368)]:put(name+'-paper',sources['row-paper'],(x,285,300,70))
put('short-rest-icon',sources['bed-icon'],(72,305,42,30));put('sleep-icon',O/'개별-PNG/icon-sleep.png',(388,301,40,40))
for i in range(2):put('assignee-row-'+str(i),sources['row-paper'],(56,441+i*92,588,84))
put('clock-icon',O/'개별-PNG/icon-clock.png',(63,835,36,36))
put('confirm-paper',sources['row-paper'],(60,964,604,76),(.61,.83,.62,1))
put('detail-paper',sources['detail-paper'],(1460,617,420,390));put('detail-heading',sources['header-paper'],(1452,570,232,64))
psd(O/'PSD/rest-work-layout.psd',layers,canvas)
(O/'manifest.json').write_text(json.dumps({'reference':'../UI전체시안-v1/05-생활작업지정-v2.png','layout':[1920,1080],'psd':[3840,2160],'notes':['Existing approved paper assets reused; no new art generation.','Text, portraits and current health are live Unity UI, not baked into PSD.','Small icon crops remain native-resolution sources; enlarged layout is not detail restoration.','Existing shelter stays fixed, so its left bed is partly behind the task panel.']},ensure_ascii=False,indent=2),encoding='utf-8')
print('Rest panel PNG sources and real-layer PSD verified.')
