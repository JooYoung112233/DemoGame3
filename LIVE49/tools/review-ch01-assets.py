"""Read-only source checks and visual-review contact sheets; no art edits."""
from pathlib import Path
import json, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
out=ROOT/'art/chapter01/gallery-qa';out.mkdir(exist_ok=True)
records=json.loads((ROOT/'art/chapter01/revision-v3/manifest-v3.json').read_text(encoding='utf-8'))
report=[]
for r in records:
    if 'original' not in r:continue
    mask=np.array(Image.open(ROOT/f'art/chapter01/revision-v3/{r["scene"]}-preserved-region-v3.png'))==255
    old=np.array(Image.open(ROOT/r['original']).convert('RGB'))
    new=np.array(Image.open(ROOT/r['asset']).convert('RGB'))
    assert old.shape==new.shape==(941,1672,3)
    assert np.array_equal(old[mask],new[mask]),r['scene']
    report.append(dict(scene=r['scene'],protectedPixels=int(mask.sum()),exactMatch=True))

data=json.loads((ROOT/'design/chapter01/gallery-scenes.json').read_text(encoding='utf-8'))
scenes={s['id']:s for s in data['scenes']}
def slot(scene,id):return next(l for l in scenes[scene]['slots'] if l['id']==id)
poses=[slot('store','investigate'),slot('letter','investigate'),slot('letter','read-letter'),slot('byeolddongi','meet-dog')]
assert len({p['asset'] for p in poses})==4
assert scenes['store']['occluders']==[]
for s in ['kitchen','drawing','byeolddongi','evening']:assert scenes[s]['base']==scenes['kitchen']['base']
father=Image.open(ROOT/slot('byeolddongi','meet-dog')['asset'])
for x,y in [(310-82,615-378),(327-82,742-378)]:assert father.getpixel((x,y))[3]==0

font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
for width in [736,320]:
    for sc in data['scenes']:
        files=[out/f'audit-{width}-{sc["id"]}-{s["id"]}.png' for s in sc['presets']]
        if not all(f.exists() for f in files):continue
        sheet=Image.new('RGB',(1440,math.ceil(len(files)/3)*320),'#1b221e');draw=ImageDraw.Draw(sheet)
        for i,(f,s) in enumerate(zip(files,sc['presets'])):
            tile=Image.open(f).convert('RGB');tile.thumbnail((460,275),Image.Resampling.LANCZOS)
            x=(i%3)*480;y=(i//3)*320
            sheet.paste(tile,(x+10+(460-tile.width)//2,y+32))
            draw.text((x+12,y+5),sc['id']+' / '+s['id'],font=font,fill='#ece2cc')
        sheet.save(out/f'audit-{width}-{sc["id"]}-contact.png')

result=dict(protectedBackgrounds=report,distinctActionSprites=len(poses),sharedCamperBase=True,openFrontOccluders=True,inspectedAlphaHoles=True)
(out/'asset-review-report.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2))
