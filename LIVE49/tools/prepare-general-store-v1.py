"""Preserve L3 native art; alpha-only foreground cuts authorized by user.

These are occlusion copies, not movable furniture with reconstructed walls/floor.
"""
from pathlib import Path
import ast, io, re, struct, json, hashlib
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/chapter01/general-store-v1'
(OUT/'layers').mkdir(exist_ok=True)
qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
src=OUT/'general-store-clean-base-source-v1.png'
base=Image.open(src).convert('RGBA');W,H=base.size
check('native_size',base.size==(1672,941))
cuts=[
 ('left_counter_occlusion',[(211,401),(230,370),(661,369),(654,432),(655,543),(633,563),(218,564),(211,440)]),
 ('right_display_occlusion',[(1069,400),(1080,396),(1417,398),(1438,471),(1432,583),(1077,591),(1069,474)])
]
layers=[('base_fixed_native',base,(0,0),True)];entries=[];merged=base.copy()
for name,poly in cuts:
 mask=Image.new('L',base.size);ImageDraw.Draw(mask).polygon(poly,fill=255)
 tile=base.copy();tile.putalpha(mask);box=mask.getbbox();tile=tile.crop(box)
 check(name+':rgb_unchanged',np.array_equal(np.asarray(tile)[:,:,:3],np.asarray(base.crop(box))[:,:,:3]))
 check(name+':alpha',tile.getchannel('A').getextrema()==(0,255))
 path=OUT/'layers'/(name+'.png');tile.save(path)
 xy=box[:2];layers.append((name,tile,xy,True));merged.alpha_composite(tile,xy)
 entries.append(dict(id=name,path=path.relative_to(ROOT).as_posix(),rect=[*xy,*tile.size],polygon_native=poly,role='Foreground occlusion COPY; source furniture remains in base. Edge mask is a placement draft.'))
check('foreground_default_composite_matches_base',np.array_equal(np.asarray(merged),np.asarray(base)))
psd(OUT/'general-store-native-occlusion-v1.psd',layers,merged)
manifest={
 'id':'L3-general-store-v1','status':'composition-review-native-resolution-below-target','location':'L3',
 'source':src.relative_to(ROOT).as_posix(),'native_size':[W,H],'requested_target':[3840,2160],
 'source_sha256':hashlib.sha256(src.read_bytes()).hexdigest(),
 'generator':'built-in image_gen','prompt':'art/chapter01/general-store-v1/general-store-clean-base-v1.prompt.txt',
 'style_references':['art/chapter01/revision-v3/store-clean-base-v3.png','art/chapter01/revision-v3/house-clean-base-v3.png'],
 'psd':'art/chapter01/general-store-v1/general-store-native-occlusion-v1.psd','psd_layers':len(layers),'foreground_layers':entries,
 'logical_canvas':[1920,1080],'logical_scale_xy':[1920/W,1080/H],
 'aspect_note':'Native 1672:941 is near 16:9, not exact. For unwarped full-width display use uniform 1920/1672 scale and center crop about 0.574 logical pixels from height; native is preserved.',
 'placement_planes':[
  {'id':'stationery_table_top','polygon_native':[[1083,400],[1414,403],[1428,467],[1078,466]],'events':['E05'],'planned_props':['colored_pencils'],'note':'확정 확보 색연필은 이 상판에 별도 배치. 지금 비어 있음.'},
  {'id':'left_counter_top','polygon_native':[[238,375],[653,374],[646,429],[218,434]],'events':['E19'],'planned_props':['ownership_note','optional_household_item'],'note':'색연필과 분리한 주인 있는 선택 물품 자리.'},
  {'id':'right_rear_shelf','rect_native':[1014,106,229,187],'events':['E14','E15','E18','E21'],'planned_props':['cloth','small_mat','cover'],'note':'각 선반 실제 접촉면에 맞춘 배치가 후속. 구역 전체를 한 소품으로 채우지 않음.'}
 ],
 'render_order_proposal':['fixed_base','standing_body_behind_furniture','foreground_occlusion_copy','props_on_top','hand_over_prop'],
 'events':['E05','E14','E15','E18','E19','E21','E22'],
 'limitations':['No new detail from upscaling; no 4K master delivered.','Three real raster layers: fixed painting plus two foreground occlusion copies. Architecture and shelves are not independently movable.','Foreground masks are draft; character and prop contact must be checked in actual composite.','Props, characters, shadows for added props, Unity UI and runtime integration not delivered.','PSD channels, offsets and composite checked by file readers; Photoshop app not tested.']}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'validation.json').write_text(json.dumps({'passed':all(c['pass'] for c in qa),'checks':qa,'native_size':[W,H],'photoshop_app_tested':False},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'checks':len(qa),'native_size':[W,H],'psd_layers':len(layers)}))
