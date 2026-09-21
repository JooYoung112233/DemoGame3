"""Compose a 1920x1080 review only, using existing separate artwork/UI and runtime line data."""
from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont,ImageFilter
ROOT=Path(__file__).resolve().parents[1]
data=json.loads((ROOT/'design/ui/ch00-01/memory-dialogue.json').read_text(encoding='utf-8'))
line=next(l for l in data['lines'] if l['id']=='C0-02-D04')
frame=Image.open(ROOT/data['handoff_preview']).convert('RGBA').resize((1920,1080),Image.Resampling.LANCZOS).filter(ImageFilter.GaussianBlur(data['blur_logical_px']))
for who in line['portraits']:
    asset=data['portraits'][who];x,y,w,h=asset['rect']
    tile=Image.open(ROOT/asset['png']).convert('RGBA').resize((w,h),Image.Resampling.LANCZOS);frame.alpha_composite(tile,(x,y))
ui=ROOT/'design/ui/ch00-01/runtime/1920x1080/C0-02'
frame.alpha_composite(Image.open(ui/'01_dialogue_panel.png'),(60,790))
frame.alpha_composite(Image.open(ui/'02_nameplate.png'),(110,752))
font=ROOT/'art/title/fonts/Live49MenuSerif-Regular.ttf';draw=ImageDraw.Draw(frame)
draw.text((230,784),data.get('speaker_identity',{}).get('unknown_label','?'),font=ImageFont.truetype(str(font),32),fill='#f2e4c4',anchor='mm')
draw.text((140,850),line['text'],font=ImageFont.truetype(str(font),38),fill='#f2e4c4')
out=ROOT/'design/ui/ch00-01/review/C0-02-closing.png';frame.save(out)
print(out)
