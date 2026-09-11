"""Title UI style review: directly drawn 2x UI, independent PSD/PNG layers.

Existing artwork is used only for authorized review compositing. No art generation.
"""
from pathlib import Path
import hashlib
import io
import json
import math
import re
import struct
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'design/ui/ch00-01'
for sub in ['psd','sprites','layers','review']:(OUT/sub).mkdir(parents=True,exist_ok=True)
S=2
SIZE=(3840,2160)
FONT=ROOT/'art/title/fonts/Live49MenuSerif-Regular.ttf'
SUPPORT=Path('C:/Windows/Fonts/malgun.ttf')
BG=ROOT/'art/title/title-background-v1.png'
LOGO=ROOT/'art/title/logo-v1.png'
PAPER=(242,228,196,255)
MUTED=(199,182,149,255)
ACCENT=(204,158,103,255)
manifest={'id':'C0-00','status':'style-selection-pending','logical_canvas':[1920,1080],
          'native_ui_canvas':list(SIZE),'scale':S,'background':BG.relative_to(ROOT).as_posix(),
          'background_native_size':list(Image.open(BG).size),'styles':{},'sprites':{},'screens':{},
          'font':FONT.relative_to(ROOT).as_posix(),'strings':'strings.ko.json',
          'limitations':['UI geometry is drawn at 2x, not upscaled bitmap artwork.',
          'Background enlargement is review-only; native art is unchanged.',
          'PSD text layers are raster; runtime strings and font remain separate.',
          'No Unity/Photoshop application interaction test.']}
manifest['runtime_text']={'menu_font_size_logical':28,'font_weight':400,
    'state_colors':{'normal':'#C7B695','focus':'#F2E4C4','pressed':'#FFF0D2','disabled':'#867A64'},
    'note':'Normal/disabled A backgrounds are intentionally transparent; text tint and interactability distinguish them.'}
qa=[]

def blank(size=SIZE):return Image.new('RGBA',size)
def file(path):return path.relative_to(ROOT).as_posix()
def save(im,path):im.save(path);return file(path)
def check(name,ok):
    qa.append({'name':name,'pass':bool(ok)})
    assert ok,name
def p(*values):return tuple(round(v*S) for v in values)
def txt_layer(text,center,y,size,color=PAPER,fontpath=FONT):
    font=ImageFont.truetype(str(fontpath),round(size*S))
    box=font.getbbox(text);w=box[2]-box[0];h=box[3]-box[1]
    tile=blank((w+8*S,h+8*S));ImageDraw.Draw(tile).text((4*S-box[0],4*S-box[1]),text,font=font,fill=color)
    return tile,(round(center*S-tile.width/2),round(y*S))
def paste(frame,tile,xy):frame.alpha_composite(tile,xy)
def packbits(raw):
    out=bytearray();pos=0
    def literal(data):
        for start in range(0,len(data),128):
            b=data[start:start+128];out.append(len(b)-1);out.extend(b)
    for m in re.finditer(rb'(.)\1{2,}',raw,re.DOTALL):
        literal(raw[pos:m.start()]);length=m.end()-m.start()
        while length:
            n=min(128,length);out.extend((257-n if n>1 else 0,m.group()[0]));length-=n
        pos=m.end()
    literal(raw[pos:]);return bytes(out)
def channel_payload(band):
    a=np.asarray(band);rows=[packbits(row.tobytes()) for row in a]
    return b'\0\1'+struct.pack('>'+str(len(rows))+'H',*[len(r) for r in rows])+b''.join(rows)
def unpackbits(data):
    out=bytearray();i=0
    while i<len(data):
        n=data[i];i+=1
        if n<=127:out.extend(data[i:i+n+1]);i+=n+1
        elif n>=129:out.extend(bytes([data[i]])*(257-n));i+=1
    return bytes(out)

def psd(path,layers,merged):
    # Layers are bottom to top. Each carries native local pixels and placement.
    records=[];pixels=[];pk=struct.pack
    ordered=list(reversed(layers))
    for name,im,(x,y),visible in ordered:
        w,h=im.size
        rec=pk('>4iH',y,x,y+h,x+w,4)
        for cid,band in zip([-1,0,1,2],[im.getchannel('A'),*im.convert('RGB').split()]):
            payload=channel_payload(band);rec+=pk('>hI',cid,len(payload));pixels.append(payload)
        label=name.encode('ascii');pascal=bytes([len(label)])+label;pascal+=b'\0'*(-len(pascal)%4)
        extra=pk('>II',0,0)+pascal
        rec+=b'8BIMnorm'+bytes([255,0,0 if visible else 2,0])+pk('>I',len(extra))+extra
        records.append(rec)
    info=pk('>h',-len(layers))+b''.join(records)+b''.join(pixels)
    info+=b'\0'*(len(info)%2)
    section=pk('>I',len(info))+info+pk('>I',0)
    w,h=merged.size
    header=b'8BPS'+pk('>H',1)+b'\0'*6+pk('>HIIHH',4,h,w,8,3)
    payloads=[channel_payload(band) for band in merged.split()]
    composite=b'\0\1'+b''.join(c[2:2+h*2] for c in payloads)+b''.join(c[2+h*2:] for c in payloads)
    path.write_bytes(header+pk('>II',0,0)+pk('>I',len(section))+section+composite)
    with Image.open(path) as im:
        check(path.stem+':merged_rgba',np.array_equal(np.asarray(im.convert('RGBA')),np.asarray(merged)))
        check(path.stem+':layer_names',[x[0] for x in im.layers]==[x[0] for x in ordered])
    # Independent layer-channel reader verifies offsets, visibility, and hidden pixels.
    f=io.BytesIO(path.read_bytes());f.seek(26)
    def u(fmt):return struct.unpack('>'+fmt,f.read(struct.calcsize('>'+fmt)))
    for _ in range(2):f.read(u('I')[0])
    u('I');u('I');count=abs(u('h')[0]);recs=[]
    for _ in range(count):
        t,l,b,r,n=u('4iH');ch=[u('hI') for _ in range(n)];f.read(8)
        opacity,clip,flags,filler=u('4B');n=u('I')[0];end=f.tell()+n
        f.read(u('I')[0]);f.read(u('I')[0]);name=f.read(u('B')[0]).decode('ascii');f.seek(end)
        recs.append((name,(l,t),(r-l,b-t),ch,not bool(flags&2)))
    for (name,xy,size,channels,visible),expected in zip(recs,ordered):
        bands={}
        for cid,length in channels:
            data=f.read(length);height=size[1];assert data[:2]==b'\0\1'
            counts=struct.unpack('>'+str(height)+'H',data[2:2+2*height]);start=2+2*height;rows=[]
            for length in counts:rows.append(unpackbits(data[start:start+length]));start+=length
            bands[cid]=Image.frombytes('L',size,b''.join(rows))
        decoded=Image.merge('RGBA',[bands[c] for c in (0,1,2,-1)])
        check(path.stem+':layer:'+name,xy==expected[2] and visible==expected[3] and np.array_equal(np.asarray(decoded),np.asarray(expected[1])))

def button(style,state):
    # No text baked into button assets. Fixed 360x64 logical canvas across states.
    im=blank(p(360,64));d=ImageDraw.Draw(im)
    active=state in ['focus','pressed'];disabled=state=='disabled'
    if style=='A':
        if active:
            a=np.zeros((64*S,360*S,4),dtype='uint8');a[:,:,:3]=[179,132,78]
            xx=np.linspace(0,1,360*S);aa=(np.sin(xx*math.pi)**2*(38 if state=='focus' else 70)).astype('uint8')
            a[:,:,3]=aa[None,:];im=Image.fromarray(a);d=ImageDraw.Draw(im)
            d.line(p(54,56,306,56),fill=(214,181,127,200),width=S)
            d.polygon([p(28,32),p(33,27),p(38,32),p(33,37)],fill=PAPER)
    else:
        rng=np.random.default_rng(391)
        top=[(x,6+math.sin(x*.07)*1.0+float(rng.uniform(-.9,.9))) for x in range(11,350,9)]
        bottom=[(x,58+math.sin(x*.04)*1.1+float(rng.uniform(-1,1))) for x in reversed(range(11,350,9))]
        shape=Image.new('L',im.size);sd=ImageDraw.Draw(shape)
        sd.polygon([p(x,y) for x,y in top+bottom],fill=255)
        a=np.zeros((im.height,im.width,4),dtype='uint8')
        color=(125,71,45) if active else (54,43,31)
        noise=rng.normal(0,2.7,(im.height,im.width))
        for c in range(3):a[:,:,c]=np.clip(color[c]+noise,0,255)
        opacity=226 if state=='focus' else 240 if state=='pressed' else 80 if disabled else 148
        a[:,:,3]=(np.asarray(shape).astype(float)/255*opacity).astype('uint8')
        im=Image.fromarray(a);d=ImageDraw.Draw(im)
        linecolor=(221,184,129,175 if active else 62)
        for offset in [0,2]:
            d.line([p(x,y+offset) for x,y in top],fill=linecolor,width=1)
            d.line([p(x,y-offset) for x,y in bottom],fill=linecolor,width=1)
        if active:
            for dy in [-2,0,2]:d.line(p(30,32+dy,42,32+dy),fill=(240,211,156,150),width=S)
            d.line(p(36,26,43,32,36,38),fill=PAPER,width=2*S)
    return im

background=Image.open(BG).convert('RGBA').resize(SIZE,Image.Resampling.LANCZOS)
logo=Image.open(LOGO).convert('RGBA');logo=logo.crop(logo.getchannel('A').getbbox())
logo=logo.resize((1000,round(logo.height*1000/logo.width)),Image.Resampling.LANCZOS)
shade=blank();a=np.zeros((SIZE[1],SIZE[0],4),dtype='uint8')
a[:,:,:3]=[12,14,12];a[:,:,3]=np.clip(36*(1-np.arange(SIZE[0])/1900),0,36).astype('uint8')[None,:]
shade=Image.fromarray(a)
strings={'start':'시작','continue':'이어하기','settings':'설정','quit':'종료','input_hint':'↑ ↓ 선택   ·   Enter 확인'}
(OUT/'strings.ko.json').write_text(json.dumps(strings,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

for style in ['A','B']:
    style_dir=OUT/'sprites'/style;style_dir.mkdir(exist_ok=True)
    states={name:button(style,name) for name in ['normal','focus','pressed','disabled']}
    for name,im in states.items():
        path=style_dir/('title-button-'+name+'.png');save(im,path)
        manifest['sprites'][style+'-'+name]={'path':file(path),'size':list(im.size),'contains_text':False,
            'border_lbrt':[100,24,100,24],'pivot':[.5,.5],'pixels_per_unit':100,
            'notes':'Sliced image border in source pixels; keep native size initially. Small brush detail may stretch.'}
    kit_layers=[(name,im,(0,0),name=='focus') for name,im in states.items()]
    psd(OUT/'psd'/('C0-00-'+style+'-button-states.psd'),kit_layers,states['focus'])
    manifest['styles'][style]={'name':'활자와 얇은 선' if style=='A' else '붓 질감과 거친 선',
        'state_psd':file(OUT/'psd'/('C0-00-'+style+'-button-states.psd')),'selected':False}
    for has_save in [False,True]:
        screen_id='C0-00-'+style+('-returning' if has_save else '-first-start')
        folder=OUT/'layers'/screen_id;folder.mkdir(exist_ok=True)
        layers=[];entries=[]
        def layer(name,im,xy=(0,0),runtime='image',string_id=None):
            assert xy[0]>=0 and xy[1]>=0 and xy[0]+im.width<=SIZE[0] and xy[1]+im.height<=SIZE[1]
            layers.append((name,im,xy,True))
            path=folder/(name+'.png');im.save(path)
            entries.append({'name':name,'png':file(path),'rect_px':[*xy,*im.size],
                'rect_logical':[v/S for v in (*xy,*im.size)],'runtime':runtime,'string_id':string_id})
        layer('01_readability_shade',shade,runtime='optional overlay')
        layer('02_existing_logo',logo,p(167,222),runtime='existing logo image')
        menu=['continue','start','settings','quit'] if has_save else ['start','settings','quit']
        for i,key in enumerate(menu):
            y=492+i*82;state='focus' if i==0 else 'normal'
            # UI graphics and labels are independent native layers.
            layer(f'10_{i}_{key}_surface',states[state],p(235,y),runtime='button image')
            t,xy=txt_layer(strings[key],415,y+12,28,PAPER if i==0 else MUTED)
            layer(f'20_{i}_{key}_text',t,xy,runtime='replace with runtime text',string_id=key)
        t,xy=txt_layer(strings['input_hint'],415,964,15,(166,150,120,255),SUPPORT)
        layer('30_input_hint',t,xy,runtime='device-dependent runtime text',string_id='input_hint')
        ui=blank()
        for name,im,xy,visible in layers:paste(ui,im,xy)
        ui_path=OUT/'review'/(screen_id+'-ui.png');ui.save(ui_path)
        psd_path=OUT/'psd'/(screen_id+'.psd');psd(psd_path,layers,ui)
        composed=background.copy();composed.alpha_composite(ui)
        composed.save(OUT/'review'/(screen_id+'-composite.png'))
        composed.convert('RGB').resize((1920,1080),Image.Resampling.LANCZOS).save(OUT/'review'/(screen_id+'.jpg'),quality=95)
        manifest['screens'][screen_id]={'psd':file(psd_path),'has_save':has_save,'focus':menu[0],
            'review':file(OUT/'review'/(screen_id+'.jpg')),'ui_png':file(ui_path),'layers':entries,
            'menu_hit_rects_logical':{key:[235,492+i*82,360,64] for i,key in enumerate(menu)},
            'anchor':'top-left; scale uniformly inside centered 16:9 viewport','pivot':[0,1]}
        check(screen_id+':transparent_ui',np.asarray(ui.getchannel('A')).min()==0)
        # Reconstruct from delivered files and compare exactly, not in-memory layers only.
        reconstruct=blank()
        for e in entries:reconstruct.alpha_composite(Image.open(ROOT/e['png']).convert('RGBA'),tuple(e['rect_px'][:2]))
        check(screen_id+':exported_layer_reassembly',np.array_equal(np.asarray(reconstruct),np.asarray(ui)))
    print(style,'exported',flush=True)

# External comparison labels are not included in in-game exports.
sheet=Image.new('RGB',(1920,1190),'#191b18');d=ImageDraw.Draw(sheet)
font=ImageFont.truetype(str(SUPPORT),24)
for i,(style,name) in enumerate([('A','A · 활자와 얇은 선'),('B','B · 붓 질감과 거친 선')]):
    y=i*595
    d.text((30,y+12),name,font=font,fill='#eddfc3')
    shot=Image.open(OUT/'review'/f'C0-00-{style}-first-start.jpg').resize((960,540),Image.Resampling.LANCZOS)
    returning=Image.open(OUT/'review'/f'C0-00-{style}-returning.jpg').resize((960,540),Image.Resampling.LANCZOS)
    sheet.paste(shot,(0,y+55));sheet.paste(returning,(960,y+55))
sheet.save(OUT/'review/C0-00-style-and-save-states.jpg',quality=95)
swatch=Image.new('RGB',(1600,500),'#24261f');d=ImageDraw.Draw(swatch)
for row,style in enumerate(['A','B']):
    for col,state in enumerate(['normal','focus','pressed','disabled']):
        x=col*400;y=row*250;tile=Image.new('RGBA',(400,180),'#24261f')
        tile.alpha_composite(button(style,state).resize((360,64),Image.Resampling.LANCZOS),(20,55))
        swatch.paste(tile.convert('RGB'),(x,y+35));d.text((x+28,y+20),style+' / '+state,font=font,fill='#eddfc3')
swatch.save(OUT/'review/C0-00-button-states.jpg',quality=95)
manifest['source_hashes']={file(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in [BG,LOGO,FONT]}
selection_path=OUT/'selection.json'
if selection_path.exists():
    selection=json.loads(selection_path.read_text(encoding='utf-8'))
    manifest['status']='title-style-selected'
    manifest['selected_style']=selection['selected_style']
    for key,item in manifest['styles'].items():item['selected']=key==selection['selected_style']
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'qa.json').write_text(json.dumps({'checks':qa,'passed':len(qa),'failed':0,'photoshop_app_tested':False,'unity_tested':False},indent=2)+'\n',encoding='utf-8')
print('PASS',len(qa),'checks; PSD files:',len(list((OUT/'psd').glob('*.psd'))),flush=True)
