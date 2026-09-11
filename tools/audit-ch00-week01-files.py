"""Read every PNG/PSD in chapter art trees; inventory native dimensions and references."""
from pathlib import Path
import json,struct,hashlib,time
from PIL import Image
R=Path(__file__).resolve().parents[1];O=R/'design/audit/ch00-week01';O.mkdir(parents=True,exist_ok=True)
rows=[];errors=[]
def psd_layers(p):
 with p.open('rb') as f:
  h=f.read(26)
  if h[:4]!=b'8BPS' or struct.unpack('>H',h[4:6])[0]!=1:raise ValueError('Unsupported PSD header')
  for _ in range(2):n=struct.unpack('>I',f.read(4))[0];f.seek(n,1)
  section=struct.unpack('>I',f.read(4))[0]
  if not section:return 0
  n=struct.unpack('>I',f.read(4))[0]
  return abs(struct.unpack('>h',f.read(2))[0]) if n else 0
for root in ['art/chapter00','art/chapter01','art/style-revision/soft-storybook-v1']:
 for p in sorted((R/root).rglob('*')):
  if p.suffix.lower() not in ['.png','.psd']:continue
  try:
   with Image.open(p) as im:
    im.load();row={'path':p.relative_to(R).as_posix(),'size':list(im.size),'mode':im.mode,'bytes':p.stat().st_size,'decode':'pass'}
    if p.suffix=='.psd':row['layers']=psd_layers(p)
    elif im.mode=='RGBA':row['alpha_range']=list(im.getchannel('A').getextrema())
   row['sha256']=hashlib.file_digest(p.open('rb'),'sha256').hexdigest();rows.append(row)
  except Exception as e:errors.append({'path':p.relative_to(R).as_posix(),'error':str(e)})
refs=[]
def walk(v,origin):
 if isinstance(v,dict):
  for x in v.values():walk(x,origin)
 elif isinstance(v,list):
  for x in v:walk(x,origin)
 elif isinstance(v,str) and v.startswith(('art/','docs/','design/')) and '\n' not in v and len(v)<240 and Path(v).suffix.lower() in ['.png','.psd','.json','.md','.jpg']:
  refs.append({'origin':origin,'target':v,'exists':(R/v).is_file()})
for root in ['art/chapter00','art/chapter01']:
 for p in (R/root).rglob('manifest.json'):
  try:walk(json.loads(p.read_text(encoding='utf8')),p.relative_to(R).as_posix())
  except Exception as e:errors.append({'path':str(p),'error':str(e)})
report={'scope':'All PNG/PSD files including historical versions under listed art roots. File decoding is not manual visual approval. Frozen handoff untouched.','inventory':rows,'decode_errors':errors,'references':refs,'missing_references':[v for v in refs if not v['exists']],'png_count':sum(r['path'].endswith('.png') for r in rows),'psd_count':sum(r['path'].endswith('.psd') for r in rows)}
(O/'file-inventory.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({k:len(v) if isinstance(v,list) else v for k,v in report.items() if k not in ['inventory','references','scope']}))
