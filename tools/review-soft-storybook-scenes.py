"""Art-only scene composites from existing placements; no UI layout changes."""
from pathlib import Path
import json,shutil
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
R=Path(__file__).resolve().parents[1];O=R/'art/style-revision/soft-storybook-v1';V=O/'review';SIZE=(1672,941)
def read(p):return json.loads((R/p).read_text(encoding='utf-8'))
def im(p):return Image.open(R/p).convert('RGBA')
def tile(frame,path,rect,tone=None):
    x,y,w,h=rect;t=im(path).resize((w,h),Image.Resampling.LANCZOS)
    if tone:
        a=np.array(t);a[:,:,:3]=np.clip(a[:,:,:3].astype(float)*tone,0,255).astype('uint8');t=Image.fromarray(a)
    frame.alpha_composite(t,(x,y))
c=read('design/chapter00/camper-assets-v1.json');seated=read('design/interaction/camper-seated-v1.json')
warm=im(c['base']);dark=im('art/chapter00/camper/layers/camper-outage-stable-v1.png')
old=O/'previous-composites';old.mkdir(exist_ok=True)
frames=[]
for s in c['states']:
    night=s['light']=='off';tone=(.39,.47,.66) if night else None
    frame=(dark if night else warm).copy()
    for a in seated['actors']:
        if 'drawing' not in a['id'] and a['part']=='body' and not (s['repair_pose'] and a['id'].startswith('suhyeok')):tile(frame,a['asset'],a['rect'],tone)
    frame.paste((dark if night else warm).crop((835,432,1092,633)),(835,432))
    if not s['book_packed']:tile(frame,'art/chapter01/layers/pages/sketchbook-blank-v1.png',[904,454,102,73],tone)
    tile(frame,'art/chapter01/layers/sprites/mug-source-v1.png',[1010,480,53,52],tone)
    for a in seated['actors']:
        if 'drawing' not in a['id'] and a['part']=='hands' and not (s['repair_pose'] and a['id'].startswith('suhyeok')):tile(frame,a['asset'],a['rect'],tone)
    props=['water-packed' if s['water_packed'] else 'water-before','blanket-packed' if s['blanket_packed'] else 'blanket-before','panel-fixed' if s['connection_secured'] else 'panel-loose']
    if not s['repair_pose']:props.append('flashlight-stored')
    if s['journal_open']:props.append('journal')
    for k in props:
        ident,rect=c['placements'][k];tile(frame,c['assets'][ident]['path'],rect,tone)
    if s['repair_pose']:
        if night:
            local=warm.copy();ident,rect=c['placements']['panel-fixed' if s['connection_secured'] else 'panel-loose'];tile(local,c['assets'][ident]['path'],rect)
            local.putalpha(Image.open(R/'art/chapter00/camper/layers/flashlight-local-reveal-mask-v1.png'))
            frame.alpha_composite(local);frame.alpha_composite(im('art/chapter00/camper/layers/flashlight-bounce-overlay-v1.png'))
        frame.alpha_composite(im('art/chapter00/camper/layers/repair-contact-shadow-v1.png'))
        ident,rect=c['placements']['repair'];tile(frame,c['assets'][ident]['path'],rect,(.57,.61,.73) if night else None)
    p=R/s['review'];backup=old/p.name
    if not backup.exists():shutil.copy2(p,backup)
    frame.save(p);frame.save(V/f"C0-{s['id']}.png");frames.append((s['id'],frame))
# Chapter1 reuse exact visibility, order, tone and contact-shadow data.
g=read('design/chapter01/gallery-scenes.json')
for sc in g['scenes']:
    layers=sc.get('slots',[])+sc.get('actors',[])+sc.get('pages',[])+([sc['foreground']] if sc.get('foreground') else [])
    for preset in sc['presets']:
        visible=set(preset['visible']);frame=im(sc['base'])
        for a in sorted(layers,key=lambda a:a.get('order',0)):
            if not(a.get('alwaysVisible') or a['id'] in visible):continue
            if a.get('contactShadow'):
                x,y,rx,ry,alpha=a['contactShadow'];shadow=Image.new('RGBA',SIZE);ImageDraw.Draw(shadow).ellipse((x-rx,y-ry,x+rx,y+ry),fill=(20,20,20,round(alpha*255)));frame.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(2)))
            tile(frame,a['asset'],a['rect'],a.get('tone'))
        frame.save(V/f"C1-{sc['id']}-{preset['id']}.png")
        if any(a['id'] in visible and 'soft-storybook' in a['asset'] for a in layers):frames.append((sc['id']+'-'+preset['id'],frame))
board=Image.new('RGB',(1672,((len(frames)+2)//3)*340),'#ddd2bc');d=ImageDraw.Draw(board)
for i,(name,frame) in enumerate(frames):
    x=(i%3)*557;y=(i//3)*340;d.text((x+8,y+8),name,fill='#302b24');board.paste(frame.convert('RGB').resize((557,313)),(x,y+25))
board.save(V/'scene-contact-sheet.jpg',quality=92)
print('Updated',len(c['states']),'C0 composites;',sum(len(s['presets']) for s in g['scenes']),'C1 art review states. Old C0 composites preserved.')
