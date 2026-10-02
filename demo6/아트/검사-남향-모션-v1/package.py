"""Package existing rendered frames; never redraw or alter the generated source art."""
from pathlib import Path
from PIL import Image, ImageChops
import json, hashlib

root=Path(__file__).resolve().parent
def gif_from_rendered(folder, output, durations):
    files=sorted((root/folder).glob('*.png'))
    # Fixed palette prevents palette flicker between frames of the same painted rig.
    first=Image.open(files[0]).convert('RGB')
    palette=first.quantize(colors=256)
    frames=[Image.open(f).convert('RGB').quantize(palette=palette,dither=Image.Dither.NONE) for f in files]
    frames[0].save(root/output,save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False,disposal=2)
    for im in frames: im.close()
    with Image.open(root/output) as check:
        return {'file':output,'frames':check.n_frames,'size':check.size,'bytes':(root/output).stat().st_size}

report={'previews':[], 'clips':{}, 'sources':{}}
report['previews'].append(gif_from_rendered('review-grid','four-motions.gif',50))
report['previews'].append(gif_from_rendered('review-sequence','transitions.gif',[30,30,40]*104))
manifest=json.loads((root/'animation-manifest.json').read_text(encoding='utf-8-sig'))
(root/'sheets').mkdir(exist_ok=True)
for name,clip in manifest['clips'].items():
    files=[root/p for p in clip['files']]
    images=[Image.open(p).convert('RGBA') for p in files]
    w,h=images[0].size
    sheet=Image.new('RGBA',(w*4,h*((len(images)+3)//4)))
    for i,im in enumerate(images): sheet.paste(im,((i%4)*w,(i//4)*h))
    sheet.save(root/'sheets'/f'{name}.png')
    boxes=[im.getchannel('A').point(lambda a:255 if a>128 else 0).getbbox() for im in images]
    assert all(b and b[0]>0 and b[1]>0 and b[2]<w and b[3]<h for b in boxes),(name,boxes)
    assert all(im.getpixel((0,0))[3]==0 for im in images)
    hashes=[hashlib.sha256(p.read_bytes()).hexdigest() for p in files]
    report['clips'][name]={'count':len(images),'distinctFrames':len(set(hashes)),'size':[w,h], 'alphaBounds':boxes,'clipped':False,'transparentCorners':True,'sheet':f'sheets/{name}.png'}
    for im in images: im.close()
for p in (root/'sources').glob('*.png'):
    im=Image.open(p);report['sources'][p.name]={'size':im.size,'alphaRange':im.getchannel('A').getextrema(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
(root/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
