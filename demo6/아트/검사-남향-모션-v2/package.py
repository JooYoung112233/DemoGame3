"""Encode already-rendered drawings as contact sheets and animations. Sources are immutable."""
from pathlib import Path
from PIL import Image,ImageSequence
import json,hashlib,sys
root=Path(__file__).resolve().parent
manifest=json.loads((root/'animation-manifest.json').read_text(encoding='utf-8-sig'))
out=root/'review';out.mkdir(exist_ok=True)
def encode(files,destination,durations,size=None):
    rgb=[]
    for file in files:
        im=Image.open(file).convert('RGBA')
        if size:im.thumbnail(size,Image.Resampling.LANCZOS)
        bg=Image.new('RGBA',im.size,(41,49,57,255));bg.alpha_composite(im);rgb.append(bg.convert('RGB'))
    palette=rgb[0].quantize(colors=256)
    frames=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in rgb]
    frames[0].save(destination,save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False,disposal=2)
    return {'size':frames[0].size,'encodedFrames':Image.open(destination).n_frames,'durationMs':sum(f.info.get('duration',0) for f in ImageSequence.Iterator(Image.open(destination)))}
report={'sourceFiles':{},'clips':{},'missing':manifest['missing']}
for name,c in manifest['clips'].items():
    files=sorted((root/'frames'/name).glob('*.png'))
    boxes=[]
    sheet=Image.new('RGBA',(1536*4,1536*((len(files)+3)//4)))
    contact=Image.new('RGB',(1000,300*((len(files)+3)//4)),(41,49,57))
    for i,p in enumerate(files):
        im=Image.open(p).convert('RGBA');b=im.getchannel('A').point(lambda a:255 if a>128 else 0).getbbox();boxes.append(b)
        assert im.size==(1536,1536) and b and b[0]>0 and b[1]>0 and b[2]<1536 and b[3]<1536,(p,b)
        sheet.paste(im,((i%4)*1536,(i//4)*1536))
        thumb=im.copy();thumb.thumbnail((250,280),Image.Resampling.LANCZOS);contact.paste(thumb,((i%4)*250,(i//4)*300),thumb)
    (root/'sheets').mkdir(exist_ok=True);sheet.save(root/'sheets'/f'{name}.png');contact.save(out/f'{name}-contact.png')
    preview=encode(files,out/f'{name}.gif',c['durations'],(560,560))
    report['clips'][name]={'frames':len(files),'clipped':False,'bounds':boxes,'preview':preview}
for p in (root/'sources').glob('*.png'):
    im=Image.open(p);report['sourceFiles'][p.name]={'size':im.size,'mode':im.mode,'alphaRange':im.getchannel('A').getextrema(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
if '--partial' not in sys.argv:
    assert not manifest['missing']
    report['fourMotions']=encode(sorted((root/'review-grid').glob('*.png')),root/'four-motions.gif',50)
    report['transitions']=encode(sorted((root/'review-sequence').glob('*.png')),root/'transitions.gif',[30,30,40]*130)
(root/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({'clips':{k:v['frames'] for k,v in report['clips'].items()},'sources':len(report['sourceFiles']),'missing':manifest['missing']}))
