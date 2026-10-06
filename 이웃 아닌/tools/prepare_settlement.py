from pathlib import Path
from PIL import Image,ImageFilter
import numpy as np,json
from psd_layers import psd
R=Path(__file__).resolve().parents[1];O=R/'아트/정착지첫화면-v1'
for d in ['개별-PNG','PSD']:(O/d).mkdir(parents=True,exist_ok=True)
source=Image.open(R/'아트/UI전체시안-v1/04-거점기본화면.png').convert('RGBA');assets={};sources={}
def extract(name,box,body=False):
    im=source.crop(box);m=np.asarray(im)[:,:,:3].max(2)<110
    if body or name.startswith('icon-'):
        seen=np.zeros(m.shape,bool);parts=[]
        for y,x in zip(*np.where(m)):
            if seen[y,x]:continue
            stack=[(y,x)];seen[y,x]=True;part=[]
            while stack:
                cy,cx=stack.pop();part.append((cy,cx))
                for ny,nx in ((cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)):
                    if 0<=ny<m.shape[0] and 0<=nx<m.shape[1] and m[ny,nx] and not seen[ny,nx]:seen[ny,nx]=True;stack.append((ny,nx))
            parts.append(part)
        m[:]=False
        keep=[max(parts,key=len)] if body or name=='icon-work' else [p for p in parts if len(p)>=5]
        for part in keep:
            for y,x in part:m[y,x]=True
    alpha=Image.fromarray(np.uint8(m)*255)
    if body:alpha=alpha.filter(ImageFilter.MaxFilter(7))
    im.putalpha(alpha);assets[name]=im;sources[name]=box;im.save(O/'개별-PNG'/f'{name}.png')
for n,b in [('bed',(424,241,462,272)),('water',(681,250,708,286)),('work',(984,225,1026,269)),('storage',(1265,164,1306,203)),('exit',(1519,281,1558,316)),('advance',(237,40,275,76)),('journal',(1598,27,1635,66))]:extract('icon-'+n,b)
extract('standee-scout',(656,413,758,585),True);extract('standee-medic',(808,437,903,592),True)
bg=Image.open(R/'Assets/Art/Backgrounds/shelter-unlit-v2.png').convert('RGBA');bg.save(O/'개별-PNG/shelter-unlit.png')
base=Image.open(R/'Assets/Art/Tokens/base-white.png').convert('RGBA');base.save(O/'개별-PNG/base-white.png')
native=bg.copy();ls=[('background',bg,(0,0),True)]
for name,pos in [('standee-scout',(665,404)),('standee-medic',(810,424))]:
    im=assets[name];ls.append((name,im,pos,True));native.alpha_composite(im,pos)
psd(O/'PSD/shelter-native-layers.psd',ls,native)
sheet=Image.new('RGBA',(512,128));ls=[]
for i,(n,im) in enumerate((x for x in assets.items() if x[0].startswith('icon-'))):
    pos=(i*70,0);sheet.alpha_composite(im,pos);ls.append((n,im,pos,True))
psd(O/'PSD/interaction-icons.psd',ls,sheet)
for name in ['standee-scout','standee-medic']:psd(O/'PSD'/f'{name}.psd',[(name,assets[name],(0,0),True)],assets[name])
master=Image.new('RGBA',(3840,2160));ls=[]
def put(name,im,rect):
    x,y,w,h=rect;im=im.resize((round(w*2),round(h*2)),Image.Resampling.LANCZOS);xy=(round(x*2),round(y*2));ls.append((name,im,xy,True));master.alpha_composite(im,xy)
def common(name):return Image.open(R/'아트/모험가선택-v1/개별-PNG'/f'{name}.png').convert('RGBA')
put('background-unlit',bg,(0,0,1920,1080))
for i,n in enumerate(['standee-scout','standee-medic']):
    # The three white-source layers remain separately tintable in Unity/PSD.
    for suffix,rect in [('shadow',(757+i*175,652.5+i*18,116,36)),('edge',(763.5+i*175,651.2+i*18,103,30.6)),('base',(765+i*175,647+i*18,100,30))]:put(n+'-'+suffix,base,rect)
    im=assets[n];alpha=np.asarray(im.getchannel('A'));yy,xx=np.where(alpha>127);bottom=int(yy.max());fy,fx=np.where(alpha[max(0,bottom-13):bottom+1]>127)
    foot_x=(int(fx.min())+int(fx.max())+1)/2/im.width;foot_y=(bottom+1-3)/im.height
    put(n,im,(815+i*175-foot_x*112,662+i*18-foot_y*199,112,199))
for name,x,y,w,h in [('day',28,24,204,111),('resources',1410,24,365,78),('arrival',42,920,555,113)]:put(name+'-paper',common('count-paper'),(x,y,w,h))
put('advance-paper',common('footer-paper'),(253,33,220,62));put('advance-icon',assets['icon-advance'],(273,51,32,28))
put('journal-paper',common('footer-paper'),(1800,24,88,105));put('journal-icon',assets['icon-journal'],(1829,36,30,34))
for n,x,y in [('bed',483,221),('water',818,251),('work',1185,209),('storage',1458,137),('exit',1768,270)]:
    put(n+'-circle',common('arrow-paper'),(x,y,84,84))
    im=assets['icon-'+n];a,b,c,d=im.getchannel('A').point(lambda p:255 if p>64 else 0).getbbox();scale=42/max(c-a,d-b)
    put(n+'-icon',im,(x+42-(a+c)/2*scale,y+42-(b+d)/2*scale,im.width*scale,im.height*scale))
for i,n in enumerate(['scout','medic']):
    x=1448+i*208;put(n+'-card',common('card-paper'),(x,827,194,205));put(n+'-portrait',common('portrait-'+n),(x+12,889,90,80));put(n+'-bag',common('icon-bag'),(x+155,844,28,32))
psd(O/'PSD/settlement-art-layout.psd',ls,master)
(O/'manifest.json').write_text(json.dumps({'source':'../UI전체시안-v1/04-거점기본화면.png','crops':sources,'backgroundSource':'../../Assets/Art/Backgrounds/shelter-unlit-v2.png','limitations':['Existing clean shelter is reused, not a pixel-identical reconstruction of mockup 04.','Background is one painted layer; furniture is not separately editable.','Only two standee bodies; other roles temporarily share the scout body.','Native icons and standees are small source crops, not HD final artwork.','Lighting is applied by Unity URP Light2D, not exported into PNG.']},ensure_ascii=False,indent=2),encoding='utf-8')
print('Shelter, 2 standees and 7 icons prepared; layered PSDs verified.')
