"""Versioned native cutouts/PSD. Local alpha extraction is user-authorized.
Generated RGB is never repainted here. Existing originals stay untouched.
"""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFilter,ImageFont
R=Path(__file__).resolve().parents[1];O=R/'art/chapter00-01/art-polish-v1';qa=[]
tree=ast.parse((R/'tools/build-ui-title-styles.py').read_text(encoding='utf8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
def rel(p):return p.relative_to(R).as_posix()
def read(p):return Image.open(R/p).convert('RGBA')
def dump(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
ORIGINAL={
 'suhyeok-care':'art/chapter01/completion-v1/layers/suhyeok-care-native.png',
 'suhyeok-retrieve':'art/chapter01/completion-v1/layers/suhyeok-retrieve-native.png',
 'suhyeok-seated':'art/chapter00-01/narrative-art-v1/layers/suhyeok-seated-native.png',
 'soi-seated':'art/chapter00-01/narrative-art-v1/layers/soi-seated-native.png',
 'suhyeok-drawing':'art/chapter00-01/narrative-art-v1/layers/suhyeok-drawing-native.png',
 'suhyeok-shoot':'art/chapter01/first-photo-v1/layers/suhyeok-shoot-native.png',
 'sejin-standing':'art/chapter01/sejin-v1/layers/sejin-standing-native.png',
 'sejin-repair':'art/chapter01/completion-v1/layers/sejin-repair-native.png',
 'sejin-dialogue':'art/chapter01/completion-v1/layers/sejin-dialogue-native.png',
 'exchange-npc':'art/chapter01/completion-v1/layers/exchange-npc-native.png',
 'van':'art/chapter01/sejin-v1/layers/van-native.png',
 'dog-alert':'art/chapter01/dog-v2/layers/alert-native.png',
 'dog-sit':'art/chapter01/dog-v2/layers/sit-native.png',
 'dog-rest':'art/chapter01/dog-v2/layers/rest-native.png',
 'suhyeok-delivery':'art/chapter01/first-drawing-v2/layers/suhyeok-place-pencils-native.png',
 'suhyeok-cook':'art/style-revision/soft-storybook-v1/sprites/suhyeok-cook.png',
 'suhyeok-repair':'art/style-revision/soft-storybook-v1/sprites/suhyeok-repair.png',
 'suhyeok-drawer':'art/style-revision/soft-storybook-v1/sprites/suhyeok-drawer.png',
 'suhyeok-investigate':'art/style-revision/soft-storybook-v1/sprites/suhyeok-investigate.png',
 'camera-handover':'art/chapter01/completion-v1/layers/camera-handover-native.png',
}
for f in ['sources','layers','psd','review']:(O/f).mkdir(parents=True,exist_ok=True)
assets=[]
def save_asset(ident,src,cut,previous,method):
    path=O/'layers'/f'{ident}-native.png';cut.save(path)
    trimmed=cut.crop(cut.getchannel('A').getbbox());trimmed.save(O/'layers'/f'{ident}-trim.png')
    psdp=O/'psd'/f'{ident}-native.psd'
    psd(psdp,[('source_hidden',src,(0,0),False),('isolated_subject',cut,(0,0),True)],cut)
    box=cut.getchannel('A').getbbox();check(ident+':alpha',cut.getchannel('A').getextrema()==(0,255))
    check(ident+':native_RGB_unchanged',np.array_equal(np.array(src)[:,:,:3],np.array(cut)[:,:,:3]))
    assets.append({'id':ident,'source':rel(O/'sources'/f'{ident}.png'),'previous':previous,'native':rel(path),'trimmed':rel(O/'layers'/f'{ident}-trim.png'),'psd':rel(psdp),'native_size':list(src.size),'alpha_bounds':box,'visible_size':[box[2]-box[0],box[3]-box[1]],'method':method,'upscaled':False,'source_sha256':hashlib.sha256((O/'sources'/f'{ident}.png').read_bytes()).hexdigest()})
for ident,previous in ORIGINAL.items():
    p=O/'sources'/f'{ident}.png'
    if not p.exists():continue
    src=Image.open(p).convert('RGBA');cut=src.copy();old=read(previous)
    if src.getchannel('A').getextrema()[0]==255:
        a=np.array(src);rgb=a[:,:,:3].astype('int16');neutral=(rgb.max(2)-rgb.min(2)<27)&(rgb.min(2)>90)
        mask=Image.fromarray(neutral.astype('uint8'))
        for x,y in [(0,0),(src.width-1,0),(0,src.height-1),(src.width-1,src.height-1)]:
            if mask.getpixel((x,y))==1:ImageDraw.floodfill(mask,(x,y),2)
        removed=np.array(mask)==2
        # Only neutral pixels in verified old negative space; never erase eyes/clothes by global color key.
        oldalpha=old.getchannel('A').resize(src.size,Image.Resampling.NEAREST)
        negative=np.array(oldalpha)<8
        loose_neutral=(rgb.max(2)-rgb.min(2)<70)&(rgb.min(2)>115)
        removed|=loose_neutral&negative
        a[:,:,3]=np.where(removed,0,255);cut=Image.fromarray(a)
        cut.putalpha(cut.getchannel('A').filter(ImageFilter.MinFilter(3)))
        # Pale paper fringe is confined to the existing silhouette edge; do not key the shirt interior.
        ca=np.array(cut.getchannel('A'));inner=np.array(cut.getchannel('A').filter(ImageFilter.MinFilter(15)))
        fringe=(ca>0)&(inner==0)&(rgb.min(2)>185)&(rgb.max(2)-rgb.min(2)<80)
        ca[fringe]=0;cut.putalpha(Image.fromarray(ca))
        method='image_gen repaint; exterior neutral extraction plus registered previous negative-space mask; RGB unchanged'
    else:
        # Near-invisible generated speckles must not expand the crop and squeeze the actual figure.
        ca=np.array(cut.getchannel('A'));ca[ca<16]=0;cut.putalpha(Image.fromarray(ca))
        method='image_gen repaint; alpha below 16 removed to clear isolated invisible speckles; RGB unchanged'
    save_asset(ident,src,cut,previous,method)

# Exact alpha-only correction of inspected enclosed pockets. Keep all painting pixels.
previous='art/chapter00/memory/dialogue-v1/suhyeok-response-soft-v3.png'
src=read(previous);src.save(O/'sources/suhyeok-dialogue.png');cut=src.copy();a=np.array(src);rgb=a[:,:,:3].astype('int16')
neutral=(rgb.max(2)-rgb.min(2)<25)&(rgb.min(2)>125)
regions=np.zeros(neutral.shape,bool)
for box in [(730,1020,820,1305),(202,1310,238,1357),(790,1370,825,1415)]:
    x0,y0,x1,y1=box;regions[y0:y1,x0:x1]=True
a[:,:,3][neutral&regions]=0;cut=Image.fromarray(a)
save_asset('suhyeok-dialogue',src,cut,previous,'local alpha-only enclosed-gap correction; original RGB preserved')
for pt in [(775,1100),(790,1190),(220,1340)]:check('dialogue_hole:'+str(pt),cut.getpixel(pt)[3]==0)
for pt in [(600,350),(550,900),(845,1130)]:check('dialogue_subject:'+str(pt),cut.getpixel(pt)[3]==src.getpixel(pt)[3])

font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18)
for offset in range(0,len(assets),8):
    group=assets[offset:offset+8];sheet=Image.new('RGB',(1600,450*((len(group)+3)//4)),(32,33,30));d=ImageDraw.Draw(sheet)
    for i,v in enumerate(group):
        x=i%4*400;y=i//4*450;im=read(v['native']);im.thumbnail((385,400),Image.Resampling.LANCZOS)
        bg=Image.new('RGBA',(390,405),(92,93,86,255));bg.alpha_composite(im,((390-im.width)//2,(405-im.height)//2));sheet.paste(bg.convert('RGB'),(x+5,y+35));d.text((x+8,y+7),v['id'],font=font,fill='white')
    sheet.save(O/'review'/f'native-alpha-{offset//8+1}.jpg',quality=94)
dump(O/'native-manifest.json',{'id':'art-polish-v1-natives','assets':assets,'limitations':['Native output dimensions recorded; 2048px request is not a guarantee.','Body parts are not rigged; held tools remain joint painted art.','Scene-specific shadows are separate composition layers.'],'checks':qa})
print(json.dumps({'assets':len(assets),'checks':len(qa),'passed':all(v['pass'] for v in qa)}))
