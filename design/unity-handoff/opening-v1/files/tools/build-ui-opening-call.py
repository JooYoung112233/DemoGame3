"""C0-01 static UI proof using accepted style A; no new character artwork."""
from pathlib import Path
import ast
import io
import json
import re
import struct
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'design/ui/ch00-01'
SIZE=(3840,2160);S=2
FONT=ROOT/'art/title/fonts/Live49MenuSerif-Regular.ttf'
PAPER=(242,228,196,255)
qa=[]
# Share the already verified PSD codec without regenerating unrelated title files.
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
names={'blank','file','check','p','txt_layer','packbits','channel_payload','unpackbits','psd'}
nodes=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names]
exec(compile(ast.Module(body=nodes,type_ignores=[]),'<shared UI PSD functions>','exec'))

folder=OUT/'layers/C0-01-call';folder.mkdir(parents=True,exist_ok=True)
runtime_folder=OUT/'runtime/1920x1080/C0-01';runtime_folder.mkdir(parents=True,exist_ok=True)
layers=[];entries=[]
def add(name,im,xy=(0,0),visible=True,runtime='Image'):
    layers.append((name,im,xy,visible));path=folder/(name+'.png');im.save(path)
    entries.append({'name':name,'png':file(path),'rect_px':[*xy,*im.size],
                    'rect_logical':[v/S for v in (*xy,*im.size)],'visible':visible,'runtime':runtime})

# Draw transparency ramp directly; the original background is never modified.
a=np.zeros((SIZE[1],SIZE[0],4),dtype='uint8')
y=np.linspace(0,1,SIZE[1])
t=np.clip((y-.48)/.40,0,1)
# Smoothly reach a stronger dim behind the text, with no hard panel edge.
a[:,:,3]=((t*t*(3-2*t))*205).astype('uint8')[:,None]
add('01_bottom_readability_gradient',Image.fromarray(a))
caption,xy=txt_layer('수혁아.',960,805,38)
add('02_call_text',caption,xy,runtime='Text: opening.call; raster proof only')
cue=blank(p(28,28));d=ImageDraw.Draw(cue)
d.polygon([p(7,10),p(21,10),p(14,17)],fill=(221,204,172,230))
add('03_advance_cue',cue,p(1766,979),runtime='Image; show only when advancing is available')
hint,xy=txt_layer('클릭 또는 Space로 계속',960,976,16,(190,171,143,255))
add('04_first_input_hint',hint,xy,runtime='Device-dependent Text; first input only')

merged=blank()
for name,im,xy,visible in layers:
    if visible:merged.alpha_composite(im,xy)
merged.save(OUT/'review/C0-01-call-ui.png')
psd_path=OUT/'psd/C0-01-call.psd';psd(psd_path,layers,merged)
bg=Image.open(ROOT/'art/title/title-background-v1.png').convert('RGBA').resize(SIZE,Image.Resampling.LANCZOS)
bg.alpha_composite(merged);bg.save(OUT/'review/C0-01-call-composite.png')
bg.convert('RGB').resize((1920,1080),Image.Resampling.LANCZOS).save(OUT/'review/C0-01-call.jpg',quality=95)
add_path=OUT/'sprites/A/advance-cue.png';cue.save(add_path)
runtime_assets=[]
for name,im,xy,visible in layers:
    if name not in {'01_bottom_readability_gradient','03_advance_cue'}:continue
    size=(im.width//S,im.height//S)
    out=runtime_folder/(name+'.png')
    im.resize(size,Image.Resampling.LANCZOS).save(out)
    runtime_assets.append({'path':file(out),'size':list(size),'position':[v/S for v in xy],
                           'contains_text':False,'layer':name})
meta={'id':'C0-01','status':'layout-accepted-requested-bottom-dim-applied','style':'A','canvas':list(SIZE),
      'logical_canvas':[1920,1080],'game_resolution':[1920,1080],'master_scale':S,
      'runtime_assets':runtime_assets,'psd':file(psd_path),'layers':entries,
      'review':'design/ui/ch00-01/review/C0-01-call.jpg',
      'advance_sprite':{'path':file(add_path),'size':list(cue.size),'pivot':[.5,.5]},
      'strings':{'opening.call':'수혁아.','opening.input_hint':'클릭 또는 Space로 계속'},
      'font':file(FONT),'text_sizes_logical':{'caption':38,'hint':16},
      'bottom_dim':{'start_y_normalized':.48,'full_y_normalized':.88,'max_alpha':205,'curve':'smoothstep'},
      'flow':['Title menu hides','Call appears over the same book/hand shot','Wait for user advance','Move to family memory shot'],
      'provenance':'docs/DIRECTION-TOOLKIT.ko.md section 6: sound before the memory cut',
      'notes':['User accepted the composition and requested stronger bottom dim; applied.','Current realistic hand background is temporary and should be replaced later; not approved as final art.','Speaker label omitted in this proposal; disclosure timing remains script-dependent.',
               'No voice audio or actual playback/input implemented.','PSD text is a raster proof; runtime renders strings separately.',
               'Background enlargement is preview-only, not recovered native detail.']}
(OUT/'C0-01.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
check('C0-01:transparent_ui',np.asarray(merged.getchannel('A')).min()==0)
rebuild=blank()
for e in entries:rebuild.alpha_composite(Image.open(ROOT/e['png']).convert('RGBA'),tuple(e['rect_px'][:2]))
check('C0-01:exported_png_reassembly',np.array_equal(np.asarray(rebuild),np.asarray(merged)))
(OUT/'C0-01-qa.json').write_text(json.dumps({'checks':qa,'passed':len(qa),'failed':0,'unity_tested':False,'photoshop_app_tested':False},indent=2)+'\n',encoding='utf-8')
print('C0-01 PASS:',len(qa),'checks')
