"""Read-only artwork audit; writes reports/proof sheets only, never rebuilds art."""
from pathlib import Path
import json,struct,hashlib,collections
from PIL import Image,ImageDraw,ImageFont
R=Path(__file__).resolve().parents[1];O=R/'design/audit/art-2026-09-11';O.mkdir(parents=True,exist_ok=True)
def rel(p):return p.relative_to(R).as_posix()
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def js(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def psd_records(p):
    with p.open('rb') as f:
        h=f.read(26)
        if h[:4]!=b'8BPS' or struct.unpack('>H',h[4:6])[0]!=1:raise ValueError('Unsupported PSD')
        for _ in range(2):n=struct.unpack('>I',f.read(4))[0];f.seek(n,1)
        length=struct.unpack('>I',f.read(4))[0]
        if not length:return []
        layerlength=struct.unpack('>I',f.read(4))[0]
        if not layerlength:return []
        count=abs(struct.unpack('>h',f.read(2))[0]);rows=[]
        for _ in range(count):
            rect=list(struct.unpack('>4i',f.read(16)));n=struct.unpack('>H',f.read(2))[0]
            channels=[list(struct.unpack('>hI',f.read(6))) for i in range(n)]
            blend=f.read(8);flags=f.read(4);extra=struct.unpack('>I',f.read(4))[0];end=f.tell()+extra
            for _ in range(2):size=struct.unpack('>I',f.read(4))[0];f.seek(size,1)
            namelen=f.read(1)[0];name=f.read(namelen).decode('latin1');f.seek(end)
            rows.append({'name':name,'rect_tlbr':rect,'visible':not(flags[2]&2),'channels':channels})
        return rows
inventory=[];errors=[]
files=sorted(p for p in (R/'art').rglob('*') if p.suffix.lower() in ['.png','.psd','.jpg','.jpeg'])
for i,p in enumerate(files):
    try:
        with Image.open(p) as im:
            im.load();row={'path':rel(p),'size':list(im.size),'mode':im.mode,'bytes':p.stat().st_size,'format':p.suffix.lower(),'decode':'pass'}
            if p.suffix.lower()=='.psd':
                row['layer_records']=psd_records(p);row['layers']=len(row['layer_records'])
            elif 'A' in im.getbands():
                a=im.getchannel('A');row['alpha_range']=list(a.getextrema());row['alpha_bbox']=a.getbbox()
        inventory.append(row)
    except Exception as e:errors.append({'path':rel(p),'error':str(e)})
    if i%200==0:print('Decoded',i,'/',len(files),flush=True)

refs=[];jsonerrors=[];native=[]
def walk(v,origin,key=''):
    if isinstance(v,dict):
        if isinstance(v.get('native'),str):native.append({'manifest':rel(origin),'id':v.get('id'),'native':v['native'],'reported_native_size':v.get('native_size',v.get('size')),'reported_visible_size':v.get('visible_size'),'reported_visible_height':v.get('visible_height',v.get('subject_height'))})
        for k,x in v.items():
            # Historical validation records store a target relative to the checked file.
            base=R/v['file'] if k=='target' and isinstance(v.get('file'),str) and v.get('check') in ['link','markdown_link'] else origin
            walk(x,base,k)
    elif isinstance(v,list):
        for x in v:walk(x,origin,key)
    elif isinstance(v,str) and len(v)<260 and ':' not in v and '\n' not in v and Path(v).suffix.lower() in ['.png','.psd','.jpg','.jpeg','.json','.md']:
        if v.startswith(('art/','design/','docs/')):target=R/v
        elif v.startswith(('sources/','layers/','review/','sprites/','psd/','../')):target=origin.parent/v
        else:return
        refs.append({'origin':rel(origin),'key':key,'value':v,'resolved':str(target.resolve()),'exists':target.is_file()})
jsonfiles=list((R/'art').rglob('*.json'))
for folder in ['design/chapter00','design/chapter01','design/dialogue','design/story']:
    jsonfiles+=list((R/folder).rglob('*.json'))
jsonfiles+=[R/'design/ui/day01-v1/story.json']
jsonfiles+=list((R/'design/ui/ch00-01').glob('*.json'))
for p in jsonfiles:
    try:walk(js(p),p)
    except Exception as e:jsonerrors.append({'path':rel(p),'error':str(e)})
current=js(R/'design/chapter00/continuation-v1/story.json')['scenes']
coverage=js(R/'design/chapter01/week01-art-coverage-v1.json')['events']
active=[{'id':'C0-'+k,'path':v} for k,v in current.items()]
for e in coverage:
    for p in e['resources']:
        if p.endswith('.png'):active.append({'id':e['event']+' '+Path(p).stem,'path':p})
seen=set();unique=[]
for v in active:
    if v['path'] not in seen:unique.append(v);seen.add(v['path'])
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
proofs=[]
for offset in range(0,len(unique),6):
    group=unique[offset:offset+6];sheet=Image.new('RGB',(1672,495*((len(group)+1)//2)),(38,38,33));d=ImageDraw.Draw(sheet)
    for i,v in enumerate(group):
        x=i%2*836;y=i//2*495
        p=R/v['path']
        if p.exists():
            im=Image.open(p).convert('RGBA');im.thumbnail((836,465),Image.Resampling.LANCZOS);bg=Image.new('RGBA',im.size,(92,90,78,255));bg.alpha_composite(im);sheet.paste(bg.convert('RGB'),(x+(836-im.width)//2,y+28+(465-im.height)//2))
        d.text((x+10,y+4),v['id'][:80],font=font,fill='white')
    p=O/f'current-scenes-{offset//6+1:02}.jpg';sheet.save(p,quality=92);proofs.append({'path':rel(p),'items':group})
flat=[{'path':v['path'],'layers':v['layers']} for v in inventory if v['format']=='.psd' and v['layers']<=1]
missing=[v for v in refs if not v['exists']]
counts=dict(collections.Counter(v['format'] for v in inventory))
report={'scope':'All PNG/PSD/JPEG under art, including historical files. Decode checks do not constitute visual approval. PSD merged pixels decoded, layer record structure inspected; all layer payloads not independently decoded in this audit. UI and Unity excluded.','counts':counts,'decode_errors':errors,'json_errors':jsonerrors,'missing_references':missing,'flat_psds':flat,'active_unique_pngs':len(unique),'early_truth_reference_hits':[v for v in active if '-truth' in v['path']],'approved_scene_in_active_references':any('scene-integration-v1/' in v['path'] for v in active),'inventory':inventory,'references':refs,'native_entries':native,'visual_proof_sheets':proofs}
dump(O/'inventory.json',report)
dump(O/'summary.json',{k:v for k,v in report.items() if k not in ['inventory','references','native_entries','visual_proof_sheets','scope']})
print(json.dumps({'counts':counts,'errors':len(errors),'missing_references':len(missing),'flat_psds':len(flat),'active_unique_pngs':len(unique),'proofs':len(proofs)}))

