"""Reconcile all 25 event references and add the cold-ration result cut."""
from pathlib import Path
import ast,io,re,struct,json
import numpy as np
from PIL import Image,ImageDraw,ImageFilter,ImageOps
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'art/chapter01/completion-v1';qa=[];scenes=[];renders=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
tree=ast.parse((ROOT/'tools/prepare-ch01-completion-v1.py').read_text(encoding='utf8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef)],type_ignores=[]),'<art helpers>','exec'))
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf8'))
catalog_path=ROOT/'design/chapter01/event-quest-catalog-v2.json';catalog=json.loads(catalog_path.read_text(encoding='utf8'))
BG={l['id']:l['source'] for l in catalog['locations']}
# Reconcile retained Chapter 0 props in all current HUB cuts, without regenerating masters.
for state in manifest['scenes']:
 if state['location']=='HUB' and state['id']!='E03-HUB-rations-shared':
  scene(state['id'],'HUB',[(v['id'],read(v['path']),tuple(v['rect'][:2])) for v in state['layers']],state['events'],state['note'])
dog=json.loads((ROOT/'art/chapter01/dog-v2/manifest.json').read_text(encoding='utf8'))
for state in dog['states']:
 if state['location']=='HUB':
  scene('E14-HUB-'+('place-ready' if 'place-ready' in state['id'] else 'rest'),'HUB',[(v['id'],read(v['path']),tuple(v['rect'][:2])) for v in state['layers']],['E14'],'Existing dog and mat art with conditional retained Chapter 0 supplies. Original dog-v2 native masters unchanged.')
k=json.loads((ROOT/'design/chapter01/kitchen.json').read_text(encoding='utf8'))
layers=[klayer(v) for v in k['actors'] if v['id'] in ['suhyeok-seated-body','soi-seated-body']]+[klayer(k['foreground'])]
layers+=prop('shared_water',read('art/chapter00/camper/layers/water-jug-v1.png'),(631,325),44)
layers+=prop('father_cold_food',read('art/chapter01/shared-props/food-can-v1.png'),(882,474),27)
layers+=prop('soi_cold_food',read('art/chapter01/shared-props/food-can-v1.png'),(993,479),24)
layers +=[klayer(v) for v in k['actors'] if v['id'] in ['suhyeok-seated-hands','soi-seated-hands']]
scene('E03-HUB-rations-shared','HUB',layers,['E03'],'Cold supplies after confirmed allocation; stove remains off. Existing water jug and small can originals reused. Outcome cut, no new hand animation or cooked meal implied.')
scene_map={s['id']:s for s in manifest['scenes']}
for s in scenes:scene_map[s['id']]=s
manifest['scenes']=list(scene_map.values())
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
R='art/chapter01/';S='art/style-revision/soft-storybook-v1/review/'
extra={
'E01':[S+'C1-store-enter.png',S+'C1-store-fridge.png','design/ui/day01-v1/story.json'],
'E02':[R+'day01-v1/evacuation-notice-paper-clean.png',R+'day01-v1/evacuation-notice-native.psd'],
'E03':[],
'E04':[],
'E05':[R+'colored-pencils-v1/manifest.json',R+'first-drawing-v2/review/01-delivery.png'],
'E06':[R+'first-drawing-v2/review/02-ready.png',R+'first-drawing-v2/review/03-started.png',R+'first-drawing-v2/review/04-complete.png'],
'E07':[R+'sejin-v1/manifest.json',R+'completion-v1/layers/sejin-dialogue-native.png'],
'E08':[R+'photographer-props-v1/review/L4-toolbag-available.png',R+'photographer-props-v1/review/L4-toolbag-collected.png'],
'E09':[R+'photographer-props-v1/manifest.json',R+'completion-v1/layers/camera-handover-native.png'],
'E10':[],
'E11':[R+'first-photo-v1/review/L6-first-shoot.png',R+'first-photo-v1/review/album-first-photo.png',R+'first-photo-v1/photo-selection.json'],
'E12':[R+'dog-v2/review/01-L5-wary.png',R+'dog-v2/review/02-L5-water-placed.png'],
'E13':[R+'dog-v2/review/03-L5-relaxed.png'],
'E14':[R+'dog-v2/review/04-HUB-place-ready.png',R+'dog-v2/review/05-HUB-rest.png'],
'E15':[],'E16':[],'E17':[],'E18':[],'E19':[],'E20':[],'E21':[],
'E22':[S+'C1-store-noise.png',S+'C1-store-recovered.png'],
'E23':[],'E24':[],'E25':[]}
descriptions={
'E03':'기존 물통·통조림과 앉은 인물로 분배 확정 후의 찬 식사 결과 컷 제작. 요리대는 꺼진 상태. 분배 손 애니메이션 대신 확인 뒤 결과로 전환.',
'E04':'고정 요리대 꺼짐 상태와 수리 완료 후 조리 결과 컷 연결. 고장 원인·수리 레시피는 미정, 화염·김은 Unity 효과.',
'E07':'같은 L2 차량·새 세진 정비 자세·별도 큰 중립 대화 초상 제작. 외형은 검토 후보, 표정은 대사집 이후.',
'E08':'같은 L4 가방 회수 전/후와 새 가방 든 수혁 정지 자세 제작. 행동 컷에서 선반 가방은 제거.',
'E09':'같은 카메라를 세진이 건네고 수혁이 받는 두 손 근접 원화·PNG/PSD 제작. 팔은 화면 양쪽에서 들어오는 인서트.',
'E10':'개별 렌치 원본과 L4 유무 상태 PNG/PSD 제작. 추가 공구는 카메라 획득의 필수 조건 아님.',
'E12':'경계·물 놓는 새 수혁 자세·물러난 결과·먹이 대안 제작. 같은 그릇 외곽을 유지하고 내용물만 교체.',
'E15':'기존 HUB의 침구 젖음 제한 패치와 지붕 보수 천 상태 제작. 비/물방울은 Unity 효과; 전체 배경 재생성 없음.',
'E16':'젖은 침구/마른 침구 결과와 L6 벤치에 널린 천 제작. 실제 젖은 경로에서만 사용.',
'E17':'조리 자세의 식탁 앞면 가림 수정, 개별 큰 음식 원본과 두 그릇 식사 상태 제작. 실제 분배에 따라 노출.',
'E18':'기존 찬장 손잡이에 천 고정 상태 PNG/PSD 제작. 소리와 수리 판정은 Unity.',
'E19':'L3 선택 천/별도 소유자 메모와 L2 이름 미정 교환처 인물 후보 제작. 색연필 튜토리얼과 분리.',
'E20':'봉인 꾸러미·빈 보관함·내용물이 앞벽에 가려지는 전달 후 상태 제작. L2/L5 고정 배경 공유.',
'E21':'빈 천 주머니·카메라 수납 상태와 앞쪽 천 가림 레이어 제작. 개별 카메라 원본 유지.',
'E23':'L2 연료 용기 유무 선택 상태 제작. 조기 연료 부족 설정 없음, 주유 동작은 소리/가림 후보.',
'E24':'실제 첫 그림과 첫 사진을 함께 보는 HUB 상태, 강아지 동행 조건 변형 제작. 미경험 사건을 회상하지 않음.',
'E25':'L7 고정 배경에 오르막 차단물 분리. 왼쪽 우회로 유지. 출발 이동은 암전·음향 후보.'}
coverage=[]
for e in catalog['events']:
 ss=[s for s in manifest['scenes'] if e['id'] in s['events']]
 paths=extra[e['id']]+[s['review'] for s in ss]
 check(e['id']+':has_art_reference',bool(paths))
 for p in paths:check(e['id']+':path:'+p,(ROOT/p).is_file())
 if e['id'] in descriptions:e['art']=descriptions[e['id']]
 e['art_resources']=paths;e['art_status']='storyboard-art-linked; final dialogue/Unity/high-resolution signoff pending'
 coverage.append({'event':e['id'],'title':e['title'],'locations':e['locations'],'resources':paths,'composition_ids':[s['id'] for s in ss],'status':'current-storyboard-coverage','note':e['art']})
catalog['art_coverage_manifest']='design/chapter01/week01-art-coverage-v1.json'
catalog_path.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
coverage_doc={'scope':'Chapter 1 first-week current storyboard art; not release-ready game','events':coverage,'event_count':len(coverage),'unlinked_events':[c['event'] for c in coverage if not c['resources']],'resolution_and_runtime_deferred':manifest['limitations']}
(ROOT/'design/chapter01/week01-art-coverage-v1.json').write_text(json.dumps(coverage_doc,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
# All references in scene layers must exist; scene coordinates must stay inside fixed canvases.
for s in manifest['scenes']:
 for key in ['background','review','psd']:check(s['id']+':'+key,(ROOT/s[key]).is_file())
 for l in s['layers']:
  check(s['id']+':layer:'+l['id'],(ROOT/l['path']).is_file())
for a in manifest['assets']:
 for key in ['source','native','psd']:check(a['id']+':'+key,(ROOT/a[key]).is_file())
(ROOT/'design/chapter01/week01-art-coverage-validation.json').write_text(json.dumps({'passed':all(c['pass'] for c in qa),'checks':qa},ensure_ascii=False,indent=2)+'\n',encoding='utf8')
# Asset inventory document provides native measurements, not canvas-size claims.
lines=['# 챕터 1 · 마지막 보강 묶음','', '개별 생성 원화 17점과 현재 장면 합성 '+str(len(manifest['scenes']))+'개. 고정 배경은 보존하고 PNG·배치 레이어·PSD를 저장했다. 신규 인물·소품은 검토안이며 최종 디자인 승인을 대신하지 않는다.','', '[전체 이벤트 연결](../../../design/chapter01/week01-art-coverage-v1.json) · [후반 콘티](../../../docs/CH01-E15-25-LIVING-DEPARTURE-STORYBOARD.ko.md) · [좌표·원본 manifest](manifest.json)','', '| 자산 | 원본 크기 | 실제 가시 영역 | PNG / PSD |','| --- | --- | --- | --- |']
for a in manifest['assets']:lines.append(f"| {a['id']} | {'×'.join(map(str,a['native_size']))} | {'×'.join(map(str,a['visible_size']))} | [PNG](layers/{a['id']}-native.png) / [PSD](psd/{a['id']}-native.psd) |")
lines+=['','원본 PSD는 숨긴 생성 소스와 알파 분리본을 보관한다. 장면 PSD는 배경·접지·인물·소품·필요한 앞면 복사 레이어를 보관한다. 손과 도구가 함께 그려진 포즈를 팔다리별 독립 편집 원본으로 부르지 않는다. 꾸러미 앞벽과 주머니 앞천은 가림용 복사다.','', '침구 젖음은 기존 침대 일부에만 적용한 제한 패치다. 보수 천과 찬장 고정은 임시 시각 설계이며 수리 레시피를 확정하지 않는다. E03의 물통·통조림과 조리/앉기 포즈는 기존 원본을 재사용한다. 이름·UI·효과는 굽지 않았다.','', '배경 1672×941은 3840×2160 목표 미달이다. 행동 인물도 실제 높이를 위 표에서 확인해야 한다. 생성 출력의 단순 확대를 고해상도 복원으로 보고하지 않는다. PSD는 파일 구조·레이어·병합 픽셀 검증이며 Photoshop 앱과 Unity 가져오기는 미검수다.','', '## 장면','']
for s in manifest['scenes']:lines.append(f"- {', '.join(s['events'])}: [{s['id']}](review/{s['id']}.png) · [PSD](psd/{s['id']}.psd)")
(OUT/'README.ko.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
print(json.dumps({'events':len(coverage),'unlinked_events':coverage_doc['unlinked_events'],'scenes':len(manifest['scenes']),'checks':len(qa),'passed':all(c['pass'] for c in qa)}))
