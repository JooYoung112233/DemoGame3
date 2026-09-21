"""Activate reviewed art references, preserving historic revisions and frozen handoff."""
from pathlib import Path
import json,hashlib
R=Path(__file__).resolve().parents[1];O=R/'art/chapter00-01/art-polish-v1'
def read(p):return json.loads((R/p).read_text(encoding='utf-8-sig'))
def dump(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
m=read('art/chapter00-01/art-polish-v1/scene-manifest.json');n=read('art/chapter00-01/art-polish-v1/native-manifest.json')
for s in m['scenes']:
    if s['id'].startswith('E'):
        for l in s['layers']:
            if l['id'] in ['water-packed','blanket-packed']:l['condition']=l['id'].replace('-','_')+' == true; review assumes retained Chapter 0 supplies'
dump(O/'scene-manifest.json',m)
mapping=dict(m['replacement_map']);manifest='art/chapter00-01/art-polish-v1/scene-manifest.json'
for a in n['assets']:
    if a['previous'].endswith('-native.png'):
        p=a['previous'].replace('-native.png','-trim.png')
    else:p=a['previous'].replace('.png','-trim.png')
    if (R/p).is_file():mapping[p]=a['trimmed']
mapping['art/style-revision/soft-storybook-v1/sprites/suhyeok-dialogue.png']=next(a['native'] for a in n['assets'] if a['id']=='suhyeok-dialogue')
# Alternate dog review copies adopt the supply-aware first-week composition.
for old,new in [('04-HUB-place-ready','E14-HUB-place-ready'),('05-HUB-rest','E14-HUB-rest')]:
    mapping['art/chapter01/dog-v2/review/'+old+'.png']='art/chapter00-01/art-polish-v1/review/'+new+'.png'
files=['design/chapter00/continuation-v1/story.json','design/chapter01/event-quest-catalog-v2.json','design/chapter01/week01-art-coverage-v1.json','design/chapter01/day01-flow-v1.json','design/ui/day01-v1/story.json','design/ui/ch00-01/memory-dialogue.json','design/ui/ch00-01/C0-02-assets.json']
backup=O/'reference-backup.json'
if not backup.exists():dump(backup,{p:(R/p).read_text(encoding='utf8') for p in files})
changes=[]
def walk(v,origin,key=''):
    if key.startswith(('previous','original','historical')):return v
    if isinstance(v,dict):return {k:walk(x,origin,k) for k,x in v.items()}
    if isinstance(v,list):return [walk(x,origin,key) for x in v]
    if isinstance(v,str) and v in mapping:
        changes.append({'file':origin,'old':v,'new':mapping[v]});return mapping[v]
    return v
for p in files:
    v=walk(read(p),p)
    if p.endswith('C0-02-assets.json'):
        a=next(a for a in n['assets'] if a['id']=='suhyeok-dialogue');v['art']['suhyeok'].update({'source':a['source'],'psd':a['psd'],'native_alpha_bounds':a['alpha_bounds'],'status':'alpha-repaired-original-paint-preserved','processing':a['method']})
    if p.endswith('week01-art-coverage-v1.json'):
        v['art_polish_manifest']=manifest
        for e in v['events']:
            ids=[Path(p).stem for p in e['resources'] if p.startswith('art/chapter00-01/art-polish-v1/review/')]
            if ids:e.update({'composition_manifest':manifest,'revision_composition_ids':ids,'status':'art-polish-v1-reviewed-native-PSD-supplied-resolution-limit-recorded'})
    elif p.endswith('event-quest-catalog-v2.json'):
        for e in v['events']:
            if any(p.startswith('art/chapter00-01/art-polish-v1/') for p in e.get('art_resources',[])):
                e['art_revision_manifest']=manifest;e['art_status']='art-polish-v1-reviewed; native resolution limits recorded; Unity pending'
    elif 'story.json' in p or 'day01-flow' in p:
        v['art_polish']={'manifest':manifest,'native_manifest':'art/chapter00-01/art-polish-v1/native-manifest.json','guide':'docs/05-원화/ART-POLISH-2026-09-11.ko.md','objective_variants':'ending-only','note':'Clean art updated; baked UI screenshot studies remain historical.'}
    dump(R/p,v)
# Only replace the existing portrait file URL. No UI structure, layout or behavior changes.
p=R/'design/ui/ch00-01/chapter0-continuation.html';t=p.read_text(encoding='utf8');old='art/chapter00/memory/dialogue-v1/suhyeok-response-soft-v3.png'
new=mapping[old]
if old in t:
    b=O/'chapter0-continuation.before.html.txt'
    if not b.exists():b.write_text(t,encoding='utf8')
    p.write_text(t.replace(old,new),encoding='utf8');changes.append({'file':p.relative_to(R).as_posix(),'old':old,'new':new})
c0=read(files[0]);cov=read(files[2]);refs=[{'id':'C0-'+k,'path':v} for k,v in c0['scenes'].items()]
refs += [{'id':e['event'],'path':p} for e in cov['events'] for p in e['resources'] if p.endswith('.png')]
for v in refs:
    assert (R/v['path']).is_file(),v
    assert '-truth' not in v['path'],v
    v['decision']='revised-composition' if '/art-polish-v1/' in v['path'] else 'retain-reviewed-original'
dump(O/'active-art-register.json',{'scope':'Current C0 continuation and all 25 C1 events','entries':refs,'manifest':manifest})
dump(O/'reference-changes.json',{'changes':changes,'current_reference_count':len(refs),'early_truth_exposure':False,'frozen_handoff_modified':False})
print(json.dumps({'changed_references':len(changes),'active_entries':len(refs),'current_events':len(cov['events'])}))
