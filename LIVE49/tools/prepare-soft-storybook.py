"""Native source preservation, exterior-only neutral matte removal and layered PSD."""
from pathlib import Path
from collections import deque
import ast, io, re, struct, json, hashlib
import numpy as np
from PIL import Image, ImageFilter, ImageDraw
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/style-revision/soft-storybook-v1'
qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
def prepare(job):
    global qa
    qa=[]
    ident=job['id']; path=OUT/job['source']; source=Image.open(path).convert('RGBA')
    a=np.array(source); rgb=a[:,:,:3].astype(np.int16)
    if a[:,:,3].min()==255:
        neutral=(rgb.max(2)-rgb.min(2)<23)&(rgb.mean(2)>100)
        seen=np.zeros(neutral.shape,dtype=bool); h,w=neutral.shape
        queue=deque()
        def push(y,x):
            if neutral[y,x] and not seen[y,x]: seen[y,x]=True;queue.append((y,x))
        for x in range(w): push(0,x);push(h-1,x)
        for y in range(h): push(y,0);push(y,w-1)
        for x,y in job.get('holes',[]): push(y,x)
        while queue:
            y,x=queue.popleft()
            if y: push(y-1,x)
            if y+1<h: push(y+1,x)
            if x: push(y,x-1)
            if x+1<w: push(y,x+1)
        # Inspected enclosed hair gaps; protect face/eyes from neutral-color removal.
        if job.get('hair_cleanup'):
            region,face=job['hair_cleanup'];x0,y0,x1,y1=region;fx0,fy0,fx1,fy1=face
            holes=np.zeros_like(seen);holes[y0:y1,x0:x1]=True;holes[fy0:fy1,fx0:fx1]=False
            seen |= neutral & holes
        a[:,:,3]=np.where(seen,0,255)
        cut=Image.fromarray(a)
        cut.putalpha(cut.getchannel('A').filter(ImageFilter.MinFilter(3)))
        method='exterior-connected neutral backdrop cut, 1px fringe trim; RGB untouched'
    else: cut=source.copy();method='native alpha preserved'
    for folder in ['sprites','psd','review']: (OUT/folder).mkdir(parents=True,exist_ok=True)
    cut.save(OUT/'sprites'/f'{ident}.png')
    bounds=cut.getchannel('A').getbbox(); alpha=np.array(cut.getchannel('A'))
    check('native-size',cut.size==source.size)
    check('transparent-border',float(np.mean(alpha==0))>.1)
    check('visible-subject',bounds is not None and float(np.mean(alpha==255))>.05)
    check('foreground-RGB-preserved',np.array_equal(np.array(cut)[:,:,:3],np.array(source)[:,:,:3]))
    psd(OUT/'psd'/f'{ident}.psd', [('generated_source_hidden',source,(0,0),False),('isolated_character',cut,(0,0),True)],cut)
    review=Image.new('RGBA',(cut.width*2,cut.height),'#e8dfcc')
    review.paste('#263b3a',(cut.width,0,cut.width*2,cut.height));review.alpha_composite(cut);review.alpha_composite(cut,(cut.width,0))
    review.thumbnail((1100,900));review.convert('RGB').save(OUT/'review'/f'{ident}.jpg',quality=94)
    record={**job,'native_size':list(source.size),'alpha_bounds':list(bounds),'subject_height':bounds[3]-bounds[1], 'sprite':f'sprites/{ident}.png','psd':f'psd/{ident}.psd','alpha_method':method,'native_unscaled':True,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'qa':qa,'psd_scope':'2 actual layers: hidden native source + visible isolated character; body parts not independently layered','resolution_target_met':bounds[3]-bounds[1]>=1536,'unity_tested':False}
    (OUT/f'{ident}.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return record
if __name__=='__main__':
    import sys
    jobs=json.loads((OUT/'jobs.json').read_text(encoding='utf-8'))
    selected=set(sys.argv[1:]); records=[]
    for j in jobs:
        if selected and j['id'] not in selected: continue
        r=prepare(j);records.append(r);print(j['id'],r['native_size'],r['alpha_bounds'],len(r['qa']),'checks')
