"""Native location originals plus practical foreground copies, not reconstructed layers."""
from pathlib import Path
import ast,io,re,struct,json,hashlib
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[1];qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
jobs=[
 {'id':'L2','slug':'gas-station','name':'주유소 정비·주차 구역','events':['E02','E07','E09','E10','E19','E20','E23'],
  'copies':[('pump_fronts',[[[1125,194],[1203,195],[1215,358],[1122,358]],[[1348,197],[1426,198],[1437,358],[1332,357]]])],
  'slots':[{'id':'sejin_vehicle','rect':[245,365,525,300],'note':'세진 차량 배치 후보. 실제 차량을 얹어 정비소 출입구와 크기 확인 필요.'},{'id':'conversation_ground','rect':[785,405,260,290],'note':'세진·수혁 정지 자세 후보. 캐릭터 발 위치 검수 후 확정.'}],
  'remaining':'세진 기본 서기·차량은 art/chapter01/sejin-v1/manifest.json의 검토 시안 참조. 정비/전달 동작·정비 장비·교환처 인물은 후속. 펌프 호스와 기둥 전체가 분리된 것은 아님.'},
 {'id':'L4','slug':'repair-shop','name':'철물·전기 수리점','events':['E04','E08','E10','E15','E22'],
  'copies':[('workbench_front',[[[629,211],[1040,211],[1054,277],[620,277]],[[628,278],[654,278],[653,391],[631,391]],[[1018,278],[1044,278],[1041,390],[1018,390]]]),('side_table_front',[[[1337,374],[1434,373],[1498,567],[1389,569]],[[1386,570],[1400,570],[1401,695],[1387,695]],[[1478,570],[1496,570],[1492,696],[1478,696]]])],
  'slots':[{'id':'toolbag_shelf','rect':[742,306,130,49],'note':'작업대 아래 선반. 낮고 넓은 가방을 바닥에 붙여 배치. 회수 후 빈 선반 유지.'},{'id':'parts_drawers','rect':[476,129,111,222],'note':'부품 탐색 후보. 고정 서랍을 여는 상태는 별도 원화 후속.'}],
  'remaining':'공구 가방·회수 전후 합성은 art/chapter01/photographer-props-v1/manifest.json 참조. 추가 공구·수리 재료·서랍 상태 미제작. 가림 마스크는 원본 복사이며 가구 이동 후 바닥 복원 없음.'},
 {'id':'L6','slug':'riverside','name':'강변 쉼터','events':['E11','E16'],
  'copies':[('bench_front',[[[1062,409],[1421,406],[1439,449],[1061,453]],[[1068,454],[1088,454],[1088,508],[1069,508]],[[1410,451],[1430,449],[1431,504],[1411,506]]])],
  'slots':[{'id':'photo_standing_ground','rect':[681,497,250,245],'note':'촬영 자세 후보. 촬영할 실제 사진 구도는 별도 확정; 이 배경이 곧 앨범 사진은 아님.'},{'id':'drying_cloth','rect':[1090,409,279,35],'note':'벤치 상판의 천 말리기 후보. 같은 그림을 받침면에 맞춰 추가.'}],
  'remaining':'카메라·필름은 art/chapter01/photographer-props-v1/manifest.json 참조. 촬영 포즈·첫 사진/앨범 시안은 art/chapter01/first-photo-v1/manifest.json 참조. 말리는 천 미제작. 하천 물은 식수 보상으로 쓰지 않음.'},
 {'id':'L7','slug':'hill-road','name':'고갯길 입구','events':['E25'],
  'copies':[('blank_board_face',[[[1417,168],[1596,168],[1603,289],[1413,285]]])],
  'slots':[{'id':'temporary_closure','rect':[998,295,268,137],'note':'큰길의 임시 통제물 자리 후보. 차량 진입·우회 길 폭과 함께 검토.'},{'id':'roadboard_text','rect':[1426,182,158,90],'note':'표지 글자는 Unity에서 별도. 빈 안내판의 현재 픽셀 좌표.'}],
  'remaining':'임시 통제물·표지 글자·캠핑카 배치 미제작. 빈 안내판은 고정 구조물로 베이스에도 남아 있음. 실제 차량을 놓고 우회로 폭·시점 검수 필요.'}
]
for j in jobs:
 out=ROOT/'art/chapter01'/(j['slug']+'-v1');(out/'layers').mkdir(exist_ok=True)
 source=out/(j['slug']+'-clean-base-source-v1.png');base=Image.open(source).convert('RGBA');layers=[('fixed_base_native',base,(0,0),True)];entries=[];merged=base.copy()
 for name,polys in j['copies']:
  mask=Image.new('L',base.size);draw=ImageDraw.Draw(mask)
  for poly in polys:draw.polygon(poly,fill=255)
  box=mask.getbbox();im=base.copy();im.putalpha(mask);im=im.crop(box);path=out/'layers'/(name+'.png');im.save(path)
  layers.append((name,im,box[:2],True));merged.alpha_composite(im,box[:2]);entries.append({'id':name,'path':path.relative_to(ROOT).as_posix(),'rect':[*box[:2],*im.size],'polygons':polys,'type':'same-position occlusion/editing copy; native background remains underneath'})
 check(j['id']+':native_size',base.size==(1672,941));check(j['id']+':composite_unchanged',np.array_equal(np.asarray(base),np.asarray(merged)))
 psdpath=out/(j['slug']+'-native-layers-v1.psd');psd(psdpath,layers,merged)
 m={'id':j['id']+'-base-v1','status':'composition-review-below-resolution-target','source':source.relative_to(ROOT).as_posix(),'native_size':list(base.size),'target_size':[3840,2160],'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'generator':'built-in image_gen','prompt':(out/(j['slug']+'-v1.prompt.txt')).relative_to(ROOT).as_posix(),'psd':psdpath.relative_to(ROOT).as_posix(),'layers':entries,'slots':j['slots'],'events':j['events'],'remaining':j['remaining'],'limitations':['No 4K source was produced; no upscaling claimed as restored detail.','Painting is one fixed native base; only listed foreground copies are separately editable.','Character/prop contact, foreground edge masks and vehicle perspective require composition review when assets arrive.','No Photoshop app or Unity validation.']}
 (out/'manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 (out/'README.ko.md').write_text(f"# {j['id']} · {j['name']} 빈 베이스 v1\n\n내장 image_gen으로 제작한 신규 장소 구도 시안. [원본 PNG]({source.name})는 실제 1672×941이며 3840×2160 목표에 미달한다. 승인된 최종 고해상도 배경으로 취급하지 않는다.\n\n[사용 프롬프트]({j['slug']}-v1.prompt.txt) · [배치·원본 manifest](manifest.json) · [실제 {len(layers)}레이어 PSD]({psdpath.name})\n\nPSD는 고정 그림 1장과 가림/표면 편집을 위한 원본 복사 레이어다. 가구·구조물을 옮긴 뒤의 배경을 복원한 파일은 아니다. 채널·위치·합성은 파일 리더로 다시 확인했다.\n\n이벤트: {', '.join(j['events'])}.\n\n후속: {j['remaining']}\n",encoding='utf-8')
(ROOT/'design/chapter01/location-bases-validation.json').write_text(json.dumps({'passed':all(c['pass'] for c in qa),'checks':qa,'new_locations':[j['id'] for j in jobs]},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':True,'checks':len(qa),'new_locations':len(jobs)}))
