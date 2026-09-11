"""Render the current dialogue manifest as review images, not new art masters."""
from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont,ImageFilter
R=Path(__file__).resolve().parents[1];D=R/'design/ui/ch00-01'
d=json.loads((D/'memory-dialogue.json').read_text(encoding='utf-8'))
ui=D/'runtime/1920x1080/C0-02';font=R/'art/title/fonts/Live49MenuSerif-Regular.ttf'
names={0:'C0-02-narration',1:'C0-02-seoyeon',3:'C0-02-response',4:'C0-02-closing',7:'C0-03-response',8:'C0-03-narration',9:'C0-03-soi-question'}
for i,name in names.items():
 line=d['lines'][i];bg=d['present_background'] if line.get('background')=='present' else d['handoff_preview'] if line.get('background')=='handhold' else 'art/chapter00/memory/dialogue-v1/feet-scene-review-v1.png'
 frame=Image.open(R/bg).convert('RGBA').resize((1920,1080),Image.Resampling.LANCZOS)
 if i:frame=frame.filter(ImageFilter.GaussianBlur(d['blur_logical_px']))
 for who in line['portraits']:
  if who in d['suppressed_portraits']:continue
  a=d['portraits'][who];x,y,w,h=a['rect'];tile=Image.open(R/a['png']).convert('RGBA').resize((w,h),Image.Resampling.LANCZOS);frame.alpha_composite(tile,(x,y))
 frame.alpha_composite(Image.open(ui/'01_dialogue_panel.png'),(60,790))
 if line['speaker']:
  frame.alpha_composite(Image.open(ui/'02_nameplate.png'),(110,752))
  label=d.get('names_known_on_entry',{}).get(line['speaker_id'],d['speaker_identity']['unknown_label'])
  for event in d.get('name_reveals',[]):
   trigger=next((j for j,l in enumerate(d['lines']) if l['id']==event['after_line']),-1)
   if 0<=trigger<i and event['speaker_id']==line['speaker_id']:label=event['display_name']
  ImageDraw.Draw(frame).text((230,784),label,font=ImageFont.truetype(str(font),32),fill='#f2e4c4',anchor='mm')
 ImageDraw.Draw(frame).text((140,850),line['text'],font=ImageFont.truetype(str(font),38),fill='#f2e4c4')
 if not line.get('end_preview'):frame.alpha_composite(Image.open(ui/'05_advance_cue.png'),(1780,975))
 frame.save(D/'review'/f'{name}.png')
print(f'Rendered {len(names)} manifest-based dialogue review frames; no source art modified.')
