"""Refresh review sheets and audit the delivered Chapter 1 resource links."""
from pathlib import Path
import json,re,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter01/completion-v1';V=OUT/'review'
m=json.loads((OUT/'manifest.json').read_text(encoding='utf8'));coverage=json.loads((ROOT/'design/chapter01/week01-art-coverage-v1.json').read_text(encoding='utf8'));checks=[]
def check(id,value):
 checks.append({'id':id,'pass':bool(value)})
 if not value:raise AssertionError(id)
def sheet(frames,name):
 out=Image.new('RGB',(1600,((len(frames)+1)//2)*480),(31,32,28));dr=ImageDraw.Draw(out)
 for i,(label,path,box) in enumerate(frames):
  im=Image.open(ROOT/path).convert('RGBA')
  if box:im=im.crop(box)
  bg=Image.new('RGBA',im.size,(69,63,50,255));bg.alpha_composite(im);bg.thumbnail((790,445));x=i%2*800;y=i//2*480
  out.paste(bg,(x+(790-bg.width)//2,y+(445-bg.height)//2));dr.text((x+8,y+450),label,fill=(237,231,215))
 out.save(V/name,quality=92)
for off in range(0,len(m['scenes']),6):sheet([(s['id'],s['review'],None) for s in m['scenes'][off:off+6]],f'scene-contact-{off//6+1:02}.jpg')
for off in range(0,len(coverage['events']),6):
 frames=[]
 for e in coverage['events'][off:off+6]:
  png=[v for v in e['resources'] if v.endswith('.png')];new=[v for v in png if '/completion-v1/review/' in v];path=new[0] if new else png[0];frames.append((e['event']+' | '+Path(path).stem,path,None))
 sheet(frames,f'event-review-{off//6+1:02}.jpg')
ids=['E07-L2-sejin-repair','E09-L2-camera-handover','E12-L5-place-water','E16-L6-drying','E20-L5-delivered','E24-HUB-memories-with-dog']
crops=[(200,300,950,680),None,(300,380,800,680),(990,350,1480,570),(930,540,1130,680),(460,270,1390,720)]
sheet([(name,'art/chapter01/completion-v1/review/'+name+'.png',crop) for name,crop in zip(ids,crops)],'week01-overview.jpg')
for s in m['scenes']:
 base=np.asarray(Image.open(ROOT/s['background']).convert('RGBA'));current=np.asarray(Image.open(ROOT/s['review']).convert('RGBA'));allowed=np.zeros(base.shape[:2],bool)
 for layer in s['layers']:
  p=ROOT/layer['path'];check(s['id']+':layer_exists:'+layer['id'],p.is_file());x,y,w,h=layer['rect'];allowed[y:y+h,x:x+w]=True
 check(s['id']+':fixed_base_outside_layers',not np.any(np.any(base!=current,axis=2)&~allowed))
 if s['location']=='HUB':
  names=[l['id'] for l in s['layers']];check(s['id']+':retained_blanket','conditional_c0_blanket-packed' in names)
  check(s['id']+':water_single_location',('conditional_c0_water-packed' in names)!=('shared_water' in names))
for a in m['assets']:
 src=Image.open(ROOT/a['source']).convert('RGBA');cut=Image.open(ROOT/a['native']).convert('RGBA');check(a['id']+':native_RGB_preserved',np.array_equal(np.asarray(src)[:,:,:3],np.asarray(cut)[:,:,:3]));check(a['id']+':native_size',list(src.size)==a['native_size'])
for name,xy in [('sejin-dialogue',(570,1430)),('camera-handover',(1079,616)),('suhyeok-retrieve',(250,806)),('wrench',(136,607)),('suhyeok-care',(690,1014))]:
 check(name+':gap_alpha',Image.open(OUT/'layers'/f'{name}-native.png').convert('RGBA').getpixel(xy)[3]==0)
for file in ['docs/05-원화/CH01-WEEK01-ART-REVIEW.ko.md','docs/03-콘티/챕터1/CH01-E15-25-LIVING-DEPARTURE-STORYBOARD.ko.md','art/chapter01/completion-v1/README.ko.md']:
 p=ROOT/file
 for target in re.findall(r'\]\(([^)]+)\)',p.read_text(encoding='utf8')):
  if not target.startswith(('http:','https:','#')):check(file+':link:'+target,(p.parent/target.split('#')[0]).resolve().is_file())
check('events_25',len(coverage['events'])==25);check('unlinked_events_zero',not coverage['unlinked_events'])
report={'passed':all(v['pass'] for v in checks),'checks':checks,'visual_review':'All current event representatives, scene composites and selected native/contact detail crops inspected; UI and runtime are not under test.','scene_count':len(m['scenes']),'psd_count':len(list((OUT/'psd').glob('*.psd'))),'source_count':len(m['assets'])}
(ROOT/'design/chapter01/completion-final-review-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({k:v for k,v in report.items() if k not in ['checks','visual_review']}|{'checks':len(checks)}))
