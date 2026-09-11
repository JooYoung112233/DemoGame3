"""Chapter 0 art assembly. Native generated sources are never overwritten.

Uses the user's authorized local cutout/compositing workflow. No Unity or UI.
"""
from pathlib import Path
import ast
import json
import struct
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'art/chapter00/camper'
SRC, LAY, REV = (ART / part for part in ('sources', 'layers', 'review'))
for folder in (LAY, REV):
    folder.mkdir(exist_ok=True, parents=True)
BASE = ROOT / 'art/chapter01/revision-v3/camper-clean-base-v3.png'
warm = Image.open(BASE).convert('RGBA')
W, H = warm.size
SIZE = warm.size
manifest = {'canvas': list(SIZE), 'base': BASE.relative_to(ROOT).as_posix(), 'assets': {}, 'states': []}

def cut(name, holes=()):
    source = Image.open(SRC / (name + '.png'))
    if source.mode == 'RGBA':
        out = source.copy()  # retain tool-generated alpha, including edge softness
        method = 'native-alpha-preserved'
    else:
        rgb = np.asarray(source.convert('RGB')).astype(np.int16)
        neutral = (rgb.max(2) - rgb.min(2) < 22) & (rgb.min(2) > 75)
        regions = Image.fromarray(neutral.astype(np.uint8)).copy()
        w, h = regions.size
        seeds = [(0,0), (w-1,0), (0,h-1), (w-1,h-1)] + list(holes)
        for point in seeds:
            if regions.getpixel(point) == 1:
                ImageDraw.floodfill(regions, point, 2)
        out = source.convert('RGBA')
        out.putalpha(Image.fromarray(np.where(np.asarray(regions) == 2, 0, 255).astype(np.uint8)))
        method = 'neutral-backdrop-flood-cut-no-resize'
    bbox = out.getchannel('A').getbbox()
    if name.startswith('power-panel'):
        # Identical trim rectangle for state replacement.
        bbox = (48, 110, 1325, 1020)
    out = out.crop(bbox)
    path = LAY / (name + '.png')
    out.save(path)
    alpha = np.asarray(out.getchannel('A'))
    assert int((alpha == 0).sum()) > 0, f'{name}: opaque backdrop remains'
    manifest['assets'][name] = {'path': path.relative_to(ROOT).as_posix(), 'source_size': list(source.size), 'crop': list(bbox), 'size': list(out.size), 'alpha_range': [int(alpha.min()), int(alpha.max())], 'transparent_pixels': int((alpha == 0).sum()), 'method': method}
    for color, suffix in [('#263943','dark'),('#eee5d4','light')]:
        proof = Image.new('RGBA', out.size, color)
        proof.alpha_composite(out)
        proof.thumbnail((650,650))
        proof.save(REV / (name + '-' + suffix + '.png'))
    return out

assets = {}
for name in ['water-jug-v1','water-jug-held-v1','folded-blanket-v1','flashlight-off-v1','power-panel-loose-v1','power-panel-fixed-v1','journal-open-v1','suhyeok-flashlight-repair-v2']:
    holes = {'journal-open-v1': [(1200,810)], 'suhyeok-flashlight-repair-v2': [(510,585), (560,1200)]}.get(name, [])
    assets[name] = cut(name, holes)

# Preserve every pixel outside the connector recess when changing panel state.
loose = assets['power-panel-loose-v1']
fixed_generated = assets['power-panel-fixed-v1']
fixed = loose.copy()
connector_region = (155, 415, 1150, 680)
fixed.paste(fixed_generated.crop(connector_region), connector_region)
fixed.save(LAY / 'power-panel-fixed-v1.png')
assets['power-panel-fixed-v1'] = fixed
manifest['assets']['power-panel-fixed-v1']['transparent_pixels'] = int((np.asarray(fixed.getchannel('A')) == 0).sum())
assert np.array_equal(np.asarray(loose)[:400], np.asarray(fixed)[:400])
manifest['assets']['power-panel-fixed-v1']['replacement_region'] = list(connector_region)
for color, suffix in [('#263943','dark'),('#eee5d4','light')]:
    proof = Image.new('RGBA', fixed.size, color)
    proof.alpha_composite(fixed)
    proof.thumbnail((650,650))
    proof.save(REV / ('power-panel-fixed-v1-' + suffix + '.png'))

# Transfer only low-frequency lighting from the generated study. All geometry,
# silhouettes and texture originate from the untouched v3 plate at same pixels.
study = Image.open(SRC / 'camper-outage-light-study-v1.png').convert('RGB')
assert study.size == SIZE
original = np.asarray(warm.convert('RGB')).astype(np.float32)
old_blur = np.asarray(warm.convert('RGB').filter(ImageFilter.GaussianBlur(5))).astype(np.float32)
new_blur = np.asarray(study.filter(ImageFilter.GaussianBlur(5))).astype(np.float32)
gain = np.clip((new_blur + 1) / (old_blur + 1), .06, 1.05)
dark = Image.fromarray(np.clip(original * gain, 0, 255).astype(np.uint8)).convert('RGBA')
dark.save(LAY / 'camper-outage-stable-v1.png')
# This image is a reusable color gain map, not a new high-resolution drawing.
Image.fromarray(np.clip(gain / 1.05 * 255,0,255).astype(np.uint8)).save(LAY / 'outage-color-gain-v1.png')

def full_layer(image, rect, tint=None):
    x,y,w,h = rect
    image = image.convert('RGBA').resize((w,h), Image.Resampling.LANCZOS)
    if tint:
        pix = np.array(image)
        pix[:,:,:3] = np.clip(pix[:,:,:3].astype(float) * tint, 0,255).astype('uint8')
        image = Image.fromarray(pix)
    full = Image.new('RGBA', SIZE)
    full.alpha_composite(image, (x,y))
    return full

def read_asset(relative):
    return Image.open(ROOT / relative).convert('RGBA')

seated = json.loads((ROOT / 'design/interaction/camper-seated-v1.json').read_text(encoding='utf-8'))
layers = {}
for item in seated['actors']:
    if 'drawing' not in item['id']:
        layers[item['id']] = full_layer(read_asset(item['asset']), item['rect'])
table = full_layer(read_asset(seated['foreground']['asset']), seated['foreground']['rect'])
book = full_layer(read_asset('art/chapter01/layers/pages/sketchbook-blank-v1.png'), (904,454,102,73))
mug = full_layer(read_asset('art/chapter01/layers/sprites/mug-source-v1.png'), (1010,480,53,52))

placement = {
    'water-before': ('water-jug-v1', [630,325,47,60]),
    'water-packed': ('water-jug-v1', [564,604,47,60]),
    'blanket-before': ('folded-blanket-v1', [1194,550,124,84]),
    'blanket-packed': ('folded-blanket-v1', [465,584,107,75]),
    'flashlight-stored': ('flashlight-off-v1', [504,586,55,37]),
    'panel-loose': ('power-panel-loose-v1', [604,426,86,61]),
    'panel-fixed': ('power-panel-fixed-v1', [604,426,86,61]),
    'repair': ('suhyeok-flashlight-repair-v2', [616,418,170,254]),
    'journal': ('journal-open-v1', [863,455,104,77]),
}
for key,(name,rect) in placement.items():
    layers[key] = full_layer(assets[name],rect)
manifest['placements'] = placement

def shaded(layer, tint=(.39,.47,.66)):
    pix=np.array(layer)
    pix[:,:,:3]=np.clip(pix[:,:,:3].astype(float)*tint,0,255).astype('uint8')
    return Image.fromarray(pix)

shadow=Image.new('RGBA',SIZE)
ImageDraw.Draw(shadow).ellipse((646,642,792,680),fill=(22,19,16,88))
shadow=shadow.filter(ImageFilter.GaussianBlur(4))
shadow.save(LAY/'repair-contact-shadow-v1.png')

# Separate illumination mask: flashlight lens (674,492) -> switch/connector.
yy,xx=np.mgrid[0:H,0:W]
spot=np.exp(-(((xx-646)/62)**2+((yy-458)/45)**2)*1.7)
mask=Image.fromarray((spot*160).astype('uint8'))
beam=Image.new('RGBA',SIZE)
beam.putalpha(mask)
beam_rgb=np.zeros((H,W,4),dtype='uint8');beam_rgb[:,:,:3]=[240,217,161];beam_rgb[:,:,3]=np.asarray(mask)*.23
beam=Image.fromarray(beam_rgb)
beam.save(LAY/'flashlight-bounce-overlay-v1.png')
mask.save(LAY/'flashlight-local-reveal-mask-v1.png')

def scene(state, packed=False, night=False, repair=False, secured=False, recorded=False, packed_book=False, water_packed=None, blanket_packed=None):
    water_packed = packed if water_packed is None else water_packed
    blanket_packed = packed if blanket_packed is None else blanket_packed
    frame=(dark if night else warm).copy()
    names=['soi-seated-body']
    if not repair:names.insert(0,'suhyeok-seated-body')
    for key in names:frame.alpha_composite(shaded(layers[key]) if night else layers[key])
    # Table front copied from the corresponding fixed base, never a new table.
    occ=Image.new('RGBA',SIZE)
    occ.paste((dark if night else warm).crop((835,432,1092,633)),(835,432))
    frame.alpha_composite(occ)
    if not packed_book:frame.alpha_composite(shaded(book) if night else book)
    frame.alpha_composite(shaded(mug) if night else mug)
    actor_hands=['soi-seated-hands'] + ([] if repair else ['suhyeok-seated-hands'])
    for key in actor_hands:frame.alpha_composite(shaded(layers[key]) if night else layers[key])
    props=['water-packed' if water_packed else 'water-before','blanket-packed' if blanket_packed else 'blanket-before','panel-fixed' if secured else 'panel-loose']
    if not repair:props.append('flashlight-stored')
    if recorded:props.append('journal')
    for key in props:frame.alpha_composite(shaded(layers[key]) if night else layers[key])
    if repair:
        if night:
            # Light the environment before drawing the actor so the revealed
            # background cannot erase the silhouette of his hand or head.
            local=warm.copy()
            local.alpha_composite(layers['panel-fixed' if secured else 'panel-loose'])
            local.putalpha(mask)
            frame.alpha_composite(local)
            frame.alpha_composite(beam)
        frame.alpha_composite(shadow)
        frame.alpha_composite(shaded(layers['repair'],(.57,.61,.73)) if night else layers['repair'])
    path=REV/(state+'.png')
    frame.save(path)
    manifest['states'].append({'id':state,'review':path.relative_to(ROOT).as_posix(),'packed':water_packed and blanket_packed,'water_packed':water_packed,'blanket_packed':blanket_packed,'light':'off' if night else 'warm','repair_pose':repair,'connection_secured':secured,'book_packed':packed_book,'journal_open':recorded})
    return frame

frames=[
    scene('01-current-conversation'),
    scene('02-preparation-complete',packed=True),
    scene('03-outage-answer-soi',packed=True,night=True),
    scene('04-flashlight-investigation',packed=True,night=True,repair=True),
    scene('05-connection-secured',packed=True,night=True,repair=True,secured=True),
    scene('06-light-restored',packed=True,secured=True),
    scene('07-book-packed-journal-open',packed=True,secured=True,recorded=True,packed_book=True),
    scene('08-voluntary-lights-off',packed=True,night=True,secured=True,packed_book=True),
]

# Alternate preparation orders are art proofs; they do not implement triggers.
partial_frames = [scene('02a-water-only', water_packed=True),
                  scene('02b-blanket-only', blanket_packed=True)]
manifest['reused_assets'] = [item['asset'] for item in seated['actors'] if 'drawing' not in item['id']] + [
    'art/chapter01/layers/pages/sketchbook-blank-v1.png',
    'art/chapter01/layers/sprites/mug-source-v1.png']

# Crops are placement proofs only. Large prop PNGs remain the enlargement sources.
for name,frame,box in [
    ('storage-flashlight-closeup',frames[2],(440,520,650,710)),
    ('soi-outage-closeup',frames[2],(835,285,1090,515)),
    ('soi-restored-closeup',frames[5],(835,285,1090,515)),
    ('prepared-supplies-closeup',frames[1],(440,535,640,710)),
]:
    frame.crop(box).save(REV/(name+'.png'))

# Large montage artwork uses native props and a neutral presentation ground;
# it is not a newly invented view of the camper room.
for name,prop in [('montage-water',assets['water-jug-held-v1']),('montage-blanket',assets['folded-blanket-v1']),('journal-detail',assets['journal-open-v1'])]:
    tile=Image.new('RGBA',SIZE,'#302c25')
    sample=prop.copy();sample.thumbnail((1300,850))
    tile.alpha_composite(sample,((W-sample.width)//2,(H-sample.height)//2))
    tile.save(REV/(name+'.png'))

sheet=Image.new('RGB',(1672,1884),'#302c25')
for i,frame in enumerate(frames):
    sheet.paste(frame.convert('RGB').resize((836,471),Image.Resampling.LANCZOS),((i%2)*836,(i//2)*471))
sheet.save(REV/'camper-state-contact-sheet-v1.jpg',quality=94)
overview=Image.new('RGB',(1672,942),'#302c25')
for i,index in enumerate([1,2,3,5]):
    overview.paste(frames[index].convert('RGB').resize((836,471),Image.Resampling.LANCZOS),((i%2)*836,(i//2)*471))
overview.save(REV/'preparation-outage-restoration-v1.jpg',quality=94)
prop_sheet=Image.new('RGB',(1672,700),'#313a3c')
for i,(name,prop) in enumerate(assets.items()):
    tile=Image.new('RGBA',(418,350),'#313a3c')
    sample=prop.copy();sample.thumbnail((380,315))
    tile.alpha_composite(sample,((418-sample.width)//2,(350-sample.height)//2))
    prop_sheet.paste(tile.convert('RGB'),((i%4)*418,(i//4)*350))
prop_sheet.save(REV/'new-assets-contact-sheet-v1.jpg',quality=94)

# Reuse only the reviewed PSD writer definition, without running its source file.
tree=ast.parse((ROOT/'tools/prepare-ch00-memory.py').read_text(encoding='utf-8'))
fn=next(node for node in tree.body if isinstance(node,ast.FunctionDef) and node.name=='write_psd')
exec(compile(ast.Module(body=[fn],type_ignores=[]),'<reviewed PSD writer>','exec'))
write_psd(ART/'camper-light-states-v1.psd',[
    ('03_flashlight_bounce_optional',beam,False),
    ('02_outage_same_geometry',dark,False),
    ('01_original_warm_v3',warm,True),
],warm)
panel_size=loose.size
write_psd(ART/'power-panel-states-v1.psd',[
    ('02_secured_HIDE_WHEN_01_VISIBLE',fixed,False),
    ('01_loose_connection',loose,True),
],loose)
manifest['lighting']={'study':'art/chapter00/camper/sources/camper-outage-light-study-v1.png','stable_dark':'art/chapter00/camper/layers/camper-outage-stable-v1.png','method':'per-channel low-frequency illumination transfer onto original v3 pixels; no geometry warp','warm_restored':BASE.relative_to(ROOT).as_posix(),'gain_map_scale':1.05}
manifest['notes']=['Art states only; no input, sound, time, saves or gameplay implemented.','Existing small seated actors/book/mug retained as placement drafts; new sources not upscaled.','Night completion is a fade after voluntary switch-off, not an empty-bed claim.','All physical actions are by Suhyeok; no dog or letter in chapter 0.','New panel position is a prop-placement proposal; furniture positions preserved.','Journal text remains external to artwork. Blank page is not a completed runtime journal.']
(ROOT/'design/chapter00/camper-assets-v1.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'assets':len(assets),'states':len(frames),'review':str(REV/'camper-state-contact-sheet-v1.jpg')},ensure_ascii=False))
