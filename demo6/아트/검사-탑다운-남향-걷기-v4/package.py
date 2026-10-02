"""Package original hand-redrawn cels. No anatomy editing or per-limb transforms."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageSequence
import json, hashlib

root=Path(__file__).resolve().parent
durations=[110,130,110,150,110,130,110,150]
files=[root/'sources'/f'walk-{i:02d}.png' for i in range(8)]
assert all(p.exists() for p in files), [p.name for p in files if not p.exists()]
sources=[Image.open(p).convert('RGBA') for p in files]
assert all(im.size==(1024,1536) for im in sources)
report={'technique':'individual full-body redraws, unwarped original canvases','sources':{},'durationsMs':durations,'totalMs':sum(durations),'notes':'File checks do not certify visual quality or user approval.'}
sheet=Image.new('RGBA',(4096,3072))
contact=Image.new('RGB',(800,640),(43,50,55))
draw=ImageDraw.Draw(contact)
review=[]
for i,(p,im) in enumerate(zip(files,sources)):
    alpha=im.getchannel('A');box=alpha.point(lambda v:255 if v>128 else 0).getbbox()
    assert box and 0<box[0]<box[2]<im.width and 0<box[1]<box[3]<im.height,(p.name,box)
    report['sources'][p.name]={'size':im.size,'alpha':alpha.getextrema(),'bounds':box,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
    sheet.paste(im,((i%4)*1024,(i//4)*1536))
    thumb=im.resize((200,300),Image.Resampling.LANCZOS)
    contact.paste(thumb,((i%4)*200,(i//4)*320+20),thumb)
    draw.text(((i%4)*200+8,(i//4)*320+4),f'{i:02d}',fill='#ffffff')
    small=im.resize((360,540),Image.Resampling.LANCZOS)
    bg=Image.new('RGBA',small.size,(43,50,55,255));bg.alpha_composite(small);review.append(bg.convert('RGB'))
sheet.save(root/'walk-sheet.png');contact.save(root/'review'/'walk-contact.png')
palette=review[0].quantize(colors=256)
indexed=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in review]
for name,multiplier in [('walk.gif',1),('walk-slow.gif',2)]:
    indexed[0].save(root/name,save_all=True,append_images=indexed[1:],duration=[x*multiplier for x in durations],loop=0,disposal=2,optimize=False)
    check=Image.open(root/name)
    report[name]={'frames':check.n_frames,'durationMs':sum(f.info.get('duration',0) for f in ImageSequence.Iterator(check))}
(root/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(root/'clips.json').write_text(json.dumps({'walk':{'loop':True,'frames':[p.name for p in files],'durationsMs':durations,'size':[1024,1536]}},indent=2),encoding='utf-8')
print(json.dumps({k:report[k] for k in ['totalMs','walk.gif','walk-slow.gif']}))
