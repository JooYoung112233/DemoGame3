"""Read-only art checks plus reproducible QA evidence; no Unity or image generation."""
from pathlib import Path
import io
import json
import struct
from PIL import Image
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'art/chapter00'
QA = ART / 'qa'
QA.mkdir(exist_ok=True)
report = {'scope': 'Chapter 0 art files and static composition; no Unity/Photoshop application test', 'checks': [], 'images': [], 'psds': []}

def check(name, passed, detail=''):
    report['checks'].append({'name': name, 'pass': bool(passed), 'detail': detail})

def rgba(path):
    return Image.open(ROOT / path).convert('RGBA')

opening = json.loads((ROOT/'design/chapter00/opening-assets-v1.json').read_text(encoding='utf-8'))
camper = json.loads((ROOT/'design/chapter00/camper-assets-v1.json').read_text(encoding='utf-8'))
reused = [camper['base'], 'art/title/title-background-v1.png'] + camper['reused_assets']
paths = set(p for p in ART.rglob('*') if p.suffix.lower() in {'.png','.jpg'} and QA not in p.parents)
paths.update(ROOT/p for p in reused)
for p in sorted(paths):
    try:
        with Image.open(p) as im:
            im.load()
            row = {'path':p.relative_to(ROOT).as_posix(), 'size':list(im.size), 'mode':im.mode}
            if im.mode == 'RGBA':
                a = np.asarray(im.getchannel('A'))
                row.update(alpha_range=[int(a.min()),int(a.max())], transparent_pixels=int((a==0).sum()),
                           alpha_bbox=im.getchannel('A').getbbox(),
                           opaque_core_bbox=Image.fromarray((a>=200).astype('uint8')*255).getbbox())
            report['images'].append(row)
    except Exception as e:
        check('decode:'+str(p),False,str(e))
check('all_raster_files_decode',len(paths)==len(report['images']),str(len(paths)))
for a in opening['assets'] + list(camper['assets'].values()):
    check('manifest_size:'+a['path'], list(Image.open(ROOT/a['path']).size)==a['size'])
for name,a in camper['assets'].items():
    alpha=np.asarray(rgba(a['path']).getchannel('A'))
    check('cutout_alpha:'+name,(alpha==0).any() and (alpha>200).any())
    check('crop_size:'+name,[a['crop'][2]-a['crop'][0],a['crop'][3]-a['crop'][1]]==a['size'])

# Decode raw PSD channel records independently of the production writer and
# Pillow's merged preview. This checks hidden layers and alpha byte-for-byte.
specs = [
 ('art/chapter00/memory/memory-hands-layers-v1.psd', [
  ('03_received_HIDE_WHEN_02_VISIBLE','art/chapter00/memory/hands-received-v1.png',False),
  ('02_handover_HIDE_WHEN_03_VISIBLE','art/chapter00/memory/hands-handover-v1.png',True),
  ('01_memory_path_background','art/chapter00/memory/memory-path-base-v1.png',True)]),
 ('art/chapter00/camper/camper-light-states-v1.psd', [
  ('03_flashlight_bounce_optional','art/chapter00/camper/layers/flashlight-bounce-overlay-v1.png',False),
  ('02_outage_same_geometry','art/chapter00/camper/layers/camper-outage-stable-v1.png',False),
  ('01_original_warm_v3',camper['base'],True)]),
 ('art/chapter00/camper/power-panel-states-v1.psd', [
  ('02_secured_HIDE_WHEN_01_VISIBLE','art/chapter00/camper/layers/power-panel-fixed-v1.png',False),
  ('01_loose_connection','art/chapter00/camper/layers/power-panel-loose-v1.png',True)])]
for path,expected in specs:
    f=io.BytesIO((ROOT/path).read_bytes())
    def u(fmt): return struct.unpack('>'+fmt,f.read(struct.calcsize('>'+fmt)))
    assert f.read(4)==b'8BPS' and u('H')[0]==1
    f.read(6)
    channels,h,w,depth,mode=u('HIIHH')
    for _ in range(2): f.read(u('I')[0])
    section_len=u('I')[0]; section_end=f.tell()+section_len
    u('I'); count=abs(u('h')[0]); records=[]
    for _ in range(count):
        top,left,bottom,right,n=u('4iH'); channel_info=[u('hI') for _ in range(n)]
        blend=f.read(8); opacity,clipping,flags,_=u('4B')
        extra_len=u('I')[0]; extra_end=f.tell()+extra_len
        f.read(u('I')[0]);f.read(u('I')[0]); name=f.read(u('B')[0]).decode('ascii')
        f.seek(extra_end)
        records.append((name,(right-left,bottom-top),channel_info,not bool(flags&2)))
    check('psd_layer_count:'+path,count==len(expected))
    visible=[]
    for i,(name,size,channel_info,is_visible) in enumerate(records):
        bands={}
        for cid,length in channel_info:
            data=f.read(length);assert data[:2]==b'\0\0'
            bands[cid]=Image.frombytes('L',size,data[2:])
        layer=Image.merge('RGBA',[bands[c] for c in (0,1,2,-1)])
        expected_name,png,expected_visible=expected[i]
        equal=np.array_equal(np.asarray(layer),np.asarray(rgba(png)))
        check('psd_rgba:'+name,equal and name==expected_name and is_visible==expected_visible)
        if is_visible:visible.append(layer)
    merged=Image.new('RGBA',(w,h),(0,0,0,255))
    for layer in reversed(visible):merged.alpha_composite(layer)
    check('psd_default_composite:'+path,np.array_equal(np.asarray(Image.open(ROOT/path).convert('RGB')),np.asarray(merged.convert('RGB'))))
    report['psds'].append({'path':path,'layers':count,'canvas':[w,h],'rgba_bytes_checked':True,'photoshop_application_tested':False})

loose=np.asarray(rgba(camper['assets']['power-panel-loose-v1']['path']))
fixed=np.asarray(rgba(camper['assets']['power-panel-fixed-v1']['path']))
x0,y0,x1,y1=camper['assets']['power-panel-fixed-v1']['replacement_region']
outside=np.ones(loose.shape[:2],dtype=bool);outside[y0:y1,x0:x1]=False
check('panel_unchanged_outside_connector',np.array_equal(loose[outside],fixed[outside]))
check('connector_state_differs',not np.array_equal(loose,fixed))
states={s['id']:s for s in camper['states']}
def frame(name):return np.array(rgba(states[name]['review']))
initial=frame('01-current-conversation');water=frame('02a-water-only');blanket=frame('02b-blanket-only');both=frame('02-preparation-complete')
dw=np.any(initial!=water,axis=2);db=np.any(initial!=blanket,axis=2)
def allowed_region(prop):
    mask=np.zeros(initial.shape[:2],dtype=bool)
    for suffix in ['before','packed']:
        _,(x,y,w,h)=camper['placements'][prop+'-'+suffix]
        mask[y:y+h,x:x+w]=True
    return mask
wr,br=allowed_region('water'),allowed_region('blanket')
check('water_first_only_changes_water_regions',not dw[~wr].any())
check('blanket_first_only_changes_blanket_regions',not db[~br].any())
check('water_then_blanket_only_changes_blanket_regions',not np.any(water!=both,axis=2)[~br].any())
check('blanket_then_water_only_changes_water_regions',not np.any(blanket!=both,axis=2)[~wr].any())
check('partial_preparation_still_lit',all(states[k]['light']=='warm' and not states[k]['packed'] for k in ('02a-water-only','02b-blanket-only')))
check('book_retained_until_restoration',all(not states[k]['book_packed'] for k in states if k[:2] in ['01','02','03','04','05','06']))
check('secured_connection_retained',all(states[k]['connection_secured'] for k in states if k[:2] in ['05','06','07','08']))

# Opaque repair pixels must be the actor, never background revealed over him.
actor=rgba(camper['assets']['suhyeok-flashlight-repair-v2']['path']).resize((170,254),Image.Resampling.LANCZOS)
actor_px=np.asarray(actor);actual=frame('04-flashlight-investigation')[418:672,616:786]
tinted=np.clip(actor_px[:,:,:3].astype(float)*(.57,.61,.73),0,255).astype('uint8')
opaque=actor_px[:,:,3]==255
check('repair_opaque_silhouette_preserved',np.array_equal(actual[:,:,:3][opaque],tinted[opaque]),str(int(opaque.sum()))+' opaque actor pixels')

# Comparison magnifications are explicitly QA proofs, never enlarged masters.
before=Image.open(QA/'repair-before.png');after=rgba(states['04-flashlight-investigation']['review'])
proof=Image.new('RGB',(1000,680),'#302c25')
for i,im in enumerate([before,after]):
    proof.paste(im.crop((590,410,840,750)).resize((500,680),Image.Resampling.NEAREST),(i*500,0))
proof.save(QA/'repair-before-after-2x.png')
proof=Image.new('RGB',(1672,942),'#302c25')
for i,key in enumerate(['01-current-conversation','02a-water-only','02b-blanket-only','02-preparation-complete']):
    im=rgba(states[key]['review']);im.thumbnail((836,471))
    proof.paste(im.convert('RGB'),((i%2)*836,(i//2)*471))
proof.save(QA/'preparation-orders.jpg',quality=94)
report['summary']={'passed':sum(c['pass'] for c in report['checks']),'failed':sum(not c['pass'] for c in report['checks']),
                   'visual_verdict':'Draft sequence usable; final art polish/resolution and missing montage art remain. See human review.'}
(ROOT/'design/chapter00/qa-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report['summary']))
for c in report['checks']:
    if not c['pass']:print(c)
raise SystemExit(bool(report['summary']['failed']))
