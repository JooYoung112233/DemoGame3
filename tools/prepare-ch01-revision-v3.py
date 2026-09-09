"""Preserve fixed interior pixels; extract the three new action-specific poses."""
from pathlib import Path
import json, shutil
import numpy as np
from PIL import Image, ImageDraw
from asset_cutout import gray_backdrop_cut, inspect_asset

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/chapter01/revision-v3'
GEN=Path('C:/Users/admin/.codex/generated_images/01a084ef-08a3-7492-b066-9506a2da3dbc')
OUT.mkdir(exist_ok=True)
jobs=[
 ('store','exec-a7c6d4de-6728-4ba2-b460-30579b2c593b.png','art/chapter01/store/store-clean-base-v1.png',603),
 ('house','exec-830efbeb-3932-4e37-b6de-a35e4a25c1db.png','art/chapter01/letter/old-house-clean-base-v1.png',740),
 ('camper','exec-6730c00c-4357-4755-9a6c-917bae349737.png','art/chapter01/layers/camper-clean-base-v1.png',None)]
records=[]
for name,generated,original,edge in jobs:
    source=OUT/f'{name}-edit-source-v3.png'
    if not source.exists():shutil.copy2(GEN/generated,source)
    old=Image.open(ROOT/original).convert('RGB');new=Image.open(source).convert('RGB')
    assert old.size==new.size==(1672,941)
    mask=Image.new('L',old.size,0);d=ImageDraw.Draw(mask)
    if edge:
        d.rectangle((0,0,1671,edge-1),fill=255)
        for y in range(edge,edge+6):d.line((0,y,1671,y),fill=round(255*(edge+6-y)/7))
        if name=='house':d.rectangle((1601,545,1671,940),fill=0)
    else:
        # Cabin and the complete living interior, including all existing slot anchors.
        d.polygon([(297,165),(470,151),(1404,151),(1435,168),(1445,703),(1429,722),(464,722),(446,635),(145,628),(182,330),(258,233)],fill=255)
    result=Image.composite(old,new,mask)
    result.save(OUT/f'{name}-clean-base-v3.png')
    mask.save(OUT/f'{name}-preserved-region-v3.png')
    protected=np.array(mask)==255
    assert np.array_equal(np.array(old)[protected],np.array(result)[protected])
    records.append(dict(scene=name,original=original,generatedSource=source.relative_to(ROOT).as_posix(),asset=f'art/chapter01/revision-v3/{name}-clean-base-v3.png',protectedPixels=int(protected.sum()),protectedPixelsUnchanged=True))

source=OUT/'suhyeok-specific-poses-source-v3.png'
if not source.exists():shutil.copy2(GEN/'exec-ff0ea1b0-6019-4e9f-a398-d423f15a2be7.png',source)
sheet=Image.open(source)
for name,rect,holes in [
 ('suhyeok-befriend',[60,350,510,525],[(310,615),(327,742)]),
 ('suhyeok-drawer',[625,60,460,840],[]),
 ('suhyeok-read-letter',[1120,60,340,840],[])]:
    asset,source_rect=gray_backdrop_cut(sheet,rect,holes)
    for px,py in holes:assert asset.getpixel((px-source_rect[0],py-source_rect[1]))[3]==0
    asset.save(OUT/f'{name}-v3.png');inspect_asset(asset,OUT/'qa',name)
    records.append(dict(asset=f'art/chapter01/revision-v3/{name}-v3.png',generatedSource=source.relative_to(ROOT).as_posix(),sourceRect=source_rect,backgroundRemoval='neutral-grey exterior flood and inspected holes; foreground RGB preserved',alphaRange=list(asset.getchannel('A').getextrema())))
(OUT/'manifest-v3.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(records,ensure_ascii=False,indent=2))
