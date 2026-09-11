"""Native cutouts and editable scene/UI layers; no generated artwork is upscaled as a master.
Background removal follows the user's earlier permission to cut and reuse existing images.
"""
from pathlib import Path
import ast, io, json, re, struct
from collections import deque
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'art/chapter00/memory/dialogue-v1'
OUT=ROOT/'design/ui/ch00-01'
SIZE=(3840,2160);S=2;qa=[]
FONT=ROOT/'art/title/fonts/Live49MenuSerif-Regular.ttf';PAPER=(242,228,196,255)
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
names={'blank','file','check','p','txt_layer','packbits','channel_payload','unpackbits','psd'}
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'<shared PSD codec>','exec'))

assets={}
for who,stem in [('seoyeon','seoyeon-speaking'),('suhyeok','suhyeok-response')]:
    source=Image.open(ART/(stem+'-source-v1.png')).convert('RGBA')
    rgb=np.asarray(source)[:,:,:3].astype(float)
    # Neutral/cool checkerboard is separable from warm painted skin, clothes and hair.
    gray=(np.abs(rgb[:,:,0]-rgb[:,:,1])<13)&(rgb[:,:,2]>=rgb[:,:,0]-8)&(rgb.mean(2)>65)
    if who=='seoyeon':
        h,w=gray.shape;remove=np.zeros_like(gray);q=deque()
        for y,x in [(0,x) for x in range(w)]+[(h-1,x) for x in range(w)]+[(y,0) for y in range(h)]+[(y,w-1) for y in range(h)]:
            if gray[y,x] and not remove[y,x]:remove[y,x]=True;q.append((y,x))
        while q:
            y,x=q.popleft()
            for yy,xx in [(y-1,x),(y+1,x),(y,x-1),(y,x+1)]:
                if 0<=yy<h and 0<=xx<w and gray[yy,xx] and not remove[yy,xx]:remove[yy,xx]=True;q.append((yy,xx))
        remove[:500]=gray[:500]
    else:
        remove=gray
    alpha=np.where(remove,0,255).astype('uint8')
    # Keep native pixels; do not invent detail or enlarge the silhouette.
    cut=source.copy();cut.putalpha(Image.fromarray(alpha))
    cut.save(ART/(stem+'-v1.png'))
    check(who+':transparent-corners',all(alpha[y,x]==0 for y,x in [(0,0),(0,-1),(-1,0),(-1,-1)]))
    check(who+':opaque-body',alpha[source.height//2,source.width//2]==255)
    psd(ART/(stem+'-layers-v1.psd'),[('source_with_baked_checker',source,(0,0),False),('character_cutout',cut,(0,0),True)],cut)
    assets[who]={'png':file(ART/(stem+'-v1.png')),'source':file(ART/(stem+'-source-v1.png')),'psd':file(ART/(stem+'-layers-v1.psd')),'native_size':list(cut.size),'native_alpha_bounds':list(cut.getchannel('A').getbbox()),'status':'single-pose-dialogue-draft; expression/identity approval pending','processing':'neutral checkerboard segmentation at native resolution, original RGB preserved'}
    # QA on contrasting backgrounds, for manual review.
    review=Image.new('RGB',(cut.width*2,cut.height),'#ebe4d5')
    for i,c in enumerate(['#ebe4d5','#202923']):
        bg=Image.new('RGBA',cut.size,c);bg.alpha_composite(cut);review.paste(bg.convert('RGB'),(i*cut.width,0))
    review.resize((1024,768)).save(ART/(stem+'-alpha-review.jpg'),quality=93)

path=Image.open(ROOT/'art/chapter00/memory/memory-path-base-v1.png').convert('RGBA')
feet=Image.open(ART/'feet-paused-v1.png').convert('RGBA')
# Ground-only framing; crop metadata preserves the source coordinate system.
crop=(300,360,1372,963)
crop=(300,338,1372,941)
ground=path.crop(crop).resize(feet.size,Image.Resampling.LANCZOS)
ground.save(ART/'path-closeup-review-v1.png')
scene=ground.copy();scene.alpha_composite(feet)
scene.save(ART/'feet-scene-review-v1.png')
psd(ART/'feet-scene-layers-v1.psd',[('original_path_native_hidden',path,(0,0),False),('ground_crop_preview',ground,(0,0),True),('feet_native',feet,(0,0),True)],scene)
assets['feet']={'png':file(ART/'feet-paused-v1.png'),'native_size':list(feet.size),'psd':file(ART/'feet-scene-layers-v1.psd'),'scene_review':file(ART/'feet-scene-review-v1.png'),'ground_source':'art/chapter00/memory/memory-path-base-v1.png','ground_source_crop':list(crop),'ground_review':file(ART/'path-closeup-review-v1.png'),'status':'new feet foreground, old path crop enlarged for composition only; final ground resolution below target'}

# UI is drawn at actual 2x master resolution; every piece is a separate layer.
folder=OUT/'layers/C0-02-dialogue';folder.mkdir(parents=True,exist_ok=True)
ui=[];entries=[]
def add(name,im,xy,visible=True,role='runtime image'):
    im.save(folder/(name+'.png'));ui.append((name,im,xy,visible))
    entries.append({'name':name,'png':file(folder/(name+'.png')),'rect_logical':[v/2 for v in (*xy,*im.size)],'visible':visible,'role':role})
panel=blank(p(1800,240));dr=ImageDraw.Draw(panel)
dr.rounded_rectangle((1,1,panel.width-2,panel.height-2),radius=24,fill=(32,25,21,224),outline=(239,222,186,235),width=3)
add('01_dialogue_panel',panel,p(60,790))
nameplate=blank(p(240,64));dr=ImageDraw.Draw(nameplate);dr.rounded_rectangle((1,1,nameplate.width-2,nameplate.height-2),radius=12,fill=(126,70,44,245),outline=(239,222,186,235),width=3)
add('02_nameplate',nameplate,p(110,752))
name,xy=txt_layer('서연',230,758,32);add('03_speaker_proof',name,xy,True,'raster proof only; Unity speaker string')
font=ImageFont.truetype(str(FONT),p(38)[0]);text=blank(p(1560,90));ImageDraw.Draw(text).text(p(0,0),'잠깐만. 신발에 돌 들어갔어.',font=font,fill=PAPER)
add('04_dialogue_proof',text,p(140,855),True,'raster proof only; Unity typewriter text')
cue=Image.open(OUT/'sprites/A/advance-cue.png').convert('RGBA');add('05_advance_cue',cue,p(1780,975))
merged=blank()
for name,im,xy,visible in ui:
    if visible:merged.alpha_composite(im,xy)
psd(OUT/'psd/C0-02-dialogue.psd',ui,merged)
for entry in entries:
    if 'proof' not in entry['name']:
        im=Image.open(ROOT/entry['png']);runtime=OUT/'runtime/1920x1080/C0-02';runtime.mkdir(parents=True,exist_ok=True)
        target=runtime/(entry['name']+'.png');im.resize((im.width//2,im.height//2),Image.Resampling.LANCZOS).save(target);entry['runtime_png']=file(target)

meta={'id':'C0-memory-dialogue-v1','flow_approved':True,'wording_and_portrait_approval':False,'resolution':[1920,1080],'ui_master':[3840,2160],'art':assets,'ui_layers':entries,'ui_psd':'design/ui/ch00-01/psd/C0-02-dialogue.psd','font':file(FONT),'limitations':['Generated feet 1672x941 and portraits 1024x1536 are below requested large-master targets; no restored-detail claim.','Native PSD layers preserve sources; body/hair/clothes are not internally split.','Ground crop is a preview from existing low-resolution background.','PSD text is raster proof; actual text, blur, fades and movement belong in Unity 6.6.','New portraits are single provisional poses, not a final expression library.'],'qa':{'passed':len(qa),'failed':0,'checks':qa,'photoshop_app_tested':False}}
reviews=[]
for label,speaker,words,people in [('narration','','몇 걸음 앞서던 발소리가 멎었다.',[]),('seoyeon','서연','잠깐만. 신발에 돌 들어갔어.',[]),('response','수혁','응.',['suhyeok'])]:
    frame=scene.resize((1920,1080),Image.Resampling.LANCZOS)
    if people:frame=frame.filter(ImageFilter.GaussianBlur(5))
    for who in people:
        rect=(1120,70,700,1050) if who=='seoyeon' else (100,50,720,1080)
        tile=Image.open(ROOT/assets[who]['png']).convert('RGBA').resize(rect[2:],Image.Resampling.LANCZOS)
        if label=='response' and who=='seoyeon':
            rgb=np.array(tile);rgb[:,:,:3]=(rgb[:,:,:3]*.8).astype('uint8');tile=Image.fromarray(rgb)
        frame.alpha_composite(tile,rect[:2])
    frame.alpha_composite(panel.resize((1800,240)),(60,790))
    if speaker:
        frame.alpha_composite(nameplate.resize((240,64)),(110,752))
        ImageDraw.Draw(frame).text((230,784),'수혁' if speaker=='수혁' else '?',font=ImageFont.truetype(str(FONT),32),fill=PAPER,anchor='mm')
    ImageDraw.Draw(frame).text((140,850),words,font=ImageFont.truetype(str(FONT),38),fill=PAPER)
    frame.alpha_composite(cue.resize((28,28)),(1780,975))
    dest=OUT/'review'/('C0-02-'+label+'.png');frame.save(dest);reviews.append(file(dest))
meta['reviews']=reviews
(OUT/'C0-02-assets.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':len(qa),'assets':{k:v['native_size'] for k,v in assets.items()}},ensure_ascii=False))
