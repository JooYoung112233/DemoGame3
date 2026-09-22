"""Preserve approved placement; extract existing pixels and package editable PSD layers.
Generated clean plate supplies ONLY hidden pixels beneath the two original characters.
No redraw or upscaling is presented as restored native detail.
"""
from pathlib import Path
import ast, io, json, re, struct
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'아트/시작화면-v2'
PNG=OUT/'개별-PNG'
PSD=OUT/'PSD'
qa=[]
def check(name,ok):
    qa.append({'name':name,'pass':bool(ok)})
    assert ok,name

# Reuse the project's independently checked PSD RLE serializer, without executing
# its LIVE49 asset authoring script. Copy it locally so packaging is self-contained.
HELPER=Path(__file__).with_name('psd_layers.py')
if not HELPER.exists():
    source=(ROOT.parent/'LIVE49/tools/build-ui-title-styles.py').read_text(encoding='utf-8')
    tree=ast.parse(source)
    names={'packbits','channel_payload','unpackbits','psd'}
    functions=[ast.get_source_segment(source,node) for node in tree.body if isinstance(node,ast.FunctionDef) and node.name in names]
    HELPER.write_text('import io, re, struct\nimport numpy as np\nfrom PIL import Image\n\ndef check(name,ok):\n    assert ok,name\n\n'+'\n\n'.join(functions)+'\n',encoding='utf-8')
from psd_layers import psd

def save(name,im):
    im.save(PNG/(name+'.png'))
    return im
def crop_alpha(im):
    box=im.getchannel('A').getbbox()
    return im.crop(box),box[:2]
def single_psd(name,im):
    psd(PSD/(name+'.psd'),[(name,im,(0,0),True)],im)

# Generated chroma background is not alpha. Remove it locally and preserve the
# paper pixels; this is an authorized cutout, not a claimed native RGBA output.
paper=Image.open(OUT/'원본/제목종이.png').convert('RGBA')
a=np.asarray(paper).astype(np.int16)
mask=~((a[:,:,0]-a[:,:,1]>45)&(a[:,:,2]-a[:,:,1]>30))
alpha=Image.fromarray(np.uint8(mask)*255)
paper.putalpha(alpha)
paper,_=crop_alpha(paper)
save('title-paper',paper)

button=Image.open(OUT/'원본/버튼종이.png').convert('RGBA')
a=np.asarray(button).astype(np.int16)
mask=~((a[:,:,0]-a[:,:,1]>45)&(a[:,:,2]-a[:,:,1]>30))
button.putalpha(Image.fromarray(np.uint8(mask)*255))
button,_=crop_alpha(button)
save('button-paper',button)
check('paper_alpha',paper.getchannel('A').getextrema()==(0,255))
check('button_alpha',button.getchannel('A').getextrema()==(0,255))

original=Image.open(ROOT/'아트/시작화면-v1/시작배경-원본.png').convert('RGBA')
clean=Image.open(OUT/'원본/인물제거-생성본.png').convert('RGBA')
check('clean_plate_size',clean.size==original.size)
# Tight manually traced silhouettes. The original raster pixels, including the
# pale cut-paper rim, remain intact; no new character identity is generated.
contours=[
 ('explorer-cap',[(294,613),(297,585),(293,562),(296,539),(305,514),(310,490),(320,475),(331,468),(341,457),(358,450),(371,451),(371,438),(378,425),(390,419),(390,411),(385,410),(387,402),(396,386),(410,374),(425,373),(438,378),(447,387),(449,391),(466,392),(476,398),(476,404),(456,409),(454,426),(447,438),(441,441),(444,449),(458,460),(470,482),(477,501),(479,521),(489,541),(496,552),(498,565),(491,575),(495,590),(497,609),(495,614)]),
 ('explorer-ponytail',[(518,614),(518,590),(524,572),(526,553),(538,533),(547,526),(557,520),(566,518),(566,508),(559,506),(554,499),(549,502),(539,503),(544,497),(548,487),(549,472),(554,458),(563,453),(575,454),(581,451),(586,447),(595,444),(607,441),(619,442),(630,446),(638,454),(639,464),(632,475),(631,486),(624,497),(613,508),(612,516),(625,519),(635,527),(641,543),(648,554),(653,571),(660,588),(664,602),(659,614)])
]
background=original.copy(); layers=[]; entries=[]
for name,points in contours:
    m=Image.new('L',original.size);ImageDraw.Draw(m).polygon(points,fill=255)
    # Include original antialiased edges and a minimal local fringe, preserving
    # the original assembled image exactly at its approved placement.
    m=m.filter(ImageFilter.MaxFilter(5))
    background.paste(clean,(0,0),m)
    tile=original.copy();tile.putalpha(m);tile,xy=crop_alpha(tile)
    save(name,tile);single_psd(name,tile)
    layers.append((name,tile,xy,True));entries.append({'name':name,'xy':list(xy),'size':list(tile.size)})
save('city-clean',background)
merged=background.copy()
for _,im,xy,_ in layers:merged.alpha_composite(im,xy)
check('assembled_background_pixel_identical',np.array_equal(np.asarray(merged),np.asarray(original)))
save('background-assembled',merged)
psd(PSD/'background-editable.psd',[('city-clean',background,(0,0),True),*layers],merged)

# Native-sized paper masters, with independently editable colour-state layers.
for name,im in [('title-paper',paper),('button-paper',button)]:
    arr=np.asarray(im).copy();arr[:,:,:3]=(arr[:,:,:3].astype(float)*[1,.92,.73]).clip(0,255).astype('uint8')
    gold=Image.fromarray(arr)
    psd(PSD/(name+'.psd'),[(name+'-ivory',im,(0,0),True),(name+'-gold-alternative',gold,(0,0),False)],im)

# 2x editable layout master. Background is explicitly a placement enlargement;
# all native PNGs and native-size PSDs above remain the true source masters.
master=Image.new('RGBA',(3840,2160));scene_layers=[]
sx,sy=3840/original.width,2160/original.height
for name,im,xy,visible in [('city-clean',background,(0,0),True),*layers]:
    scaled=im.resize((round(im.width*sx),round(im.height*sy)),Image.Resampling.LANCZOS)
    pos=(round(xy[0]*sx),round(xy[1]*sy));scene_layers.append((name,scaled,pos,True));master.alpha_composite(scaled,pos)
# The title panel's existing 1.3-degree rotation is retained by Unity; native PSD
# UI layers remain unrotated for straightforward editing, documented in layout.
placements=[('title-paper',paper,110,80,670,185),('new-game',button,1220,332,550,86),('continue',button,1220,434,550,86),('load',button,1220,536,550,86),('settings',button,1220,638,550,86),('exit',button,1220,740,550,74)]
for name,im,x,y,w,h in placements:
    tile=im.resize((w*2,h*2),Image.Resampling.LANCZOS)
    scene_layers.append((name,tile,(x*2,y*2),True));master.alpha_composite(tile,(x*2,y*2))
psd(PSD/'start-screen-art-layout.psd',scene_layers,master)
master.resize((1920,1080),Image.Resampling.LANCZOS).save(OUT/'검수/분리리소스-배치.png')
layout={'canvas':[1920,1080],'nativeBackground':list(original.size),'characters':entries,'ui': [{'name':n,'rect':[x,y,w,h]} for n,im,x,y,w,h in placements], 'titleRotationDegrees':1.3,'font':'Assets/Art/FrontEnd/Fonts/NanumPenScript-Regular.ttf','textBaked':False,'limits':['Background and characters retain original native resolution; 2x layout PSD is not new detail.','City environment is one layer, not every building individually.','PSD layout excludes runtime text and state tint and leaves title paper unrotated.','Character fringe needs recheck if moved far from original background.']}
(OUT/'layout.json').write_text(json.dumps(layout,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'검수/검증.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'paper':paper.size,'button':button.size,'background':original.size,'characters':entries,'checks':qa},ensure_ascii=False))
