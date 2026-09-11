"""Produce a traceable storyboard/file map from existing assets and manifests."""
from pathlib import Path
import hashlib
import json
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'design/unity-handoff'
OUT.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8'))
camper=read('design/chapter00/camper-assets-v1.json')
opening=read('design/chapter00/opening-assets-v1.json')
seated=read('design/interaction/camper-seated-v1.json')
call=read('design/ui/ch00-01/C0-01.json')
title=read('design/ui/ch00-01/manifest.json')
assets={};scenes=[]
def asset(path,role):
    p=ROOT/path
    assert p.is_file(),f'Missing referenced file: {path}'
    if path not in assets:
        item={'path':path,'role':role,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
        if p.suffix.lower() in {'.png','.jpg','.psd'}:
            with Image.open(p) as im:
                item.update(native_size=list(im.size),mode=im.mode)
                if p.suffix.lower()=='.psd':item['layer_names']=[x[0] for x in im.layers]
        assets[path]=item
    return path
def scene(id,label,preview,parts,trigger,advance,next_id,ui='미제작',notes='',data=None):
    if preview:asset(preview,'합성 또는 장면 참고 이미지 / 분리 원본 여부 확인')
    for part in parts:asset(part['path'],part['role'])
    scenes.append({'id':id,'label':label,'preview':preview,'parts':parts,'entry':trigger,
        'advance':advance,'next':next_id,'ui_status':ui,'notes':notes,'state_data':data})
def part(path,role,rect=None):return {'path':path,'role':role,'rect':rect}

scene('C0-00','시작 화면 · A 채택','design/ui/ch00-01/review/C0-00-A-first-start.jpg',[
    part('art/title/title-background-v1.png','임시 배경 / 실제 손 화풍 교체 예정'),
    part('art/title/logo-v1.png','기존 투명 로고'),
    part('design/ui/ch00-01/psd/C0-00-A-first-start.psd','최초 시작 UI PSD'),
    part('design/ui/ch00-01/psd/C0-00-A-returning.psd','저장 있음 UI PSD'),
    part('design/ui/ch00-01/manifest.json','UI 좌표·클릭 영역·문자 참조'),
    part('design/ui/ch00-01/strings.ko.json','별도 런타임 문구'),
    part('art/title/fonts/Live49MenuSerif-Regular.ttf','메뉴 폰트 / OFL 고지 함께 사용')],
    '앱 시작 또는 메뉴 복귀','시작 선택 → 메뉴·로고 페이드 시작. 최초 시작 확인창 없음','C0-01',
    'A 화풍 채택 / 실행 구현 없음','저장이 있을 때만 이어하기 표시. 동일 시작 입력이 다음 대사를 넘기지 않게 소비한다.',title['screens'])
scene('C0-01','첫 부름 · 하단 딤 반영','design/ui/ch00-01/review/C0-01-call.jpg',[
    part('art/title/title-background-v1.png','시작과 공유하는 임시 배경'),
    part('design/ui/ch00-01/psd/C0-01-call.psd','UI 4레이어 PSD'),
    *[part(e['path'],e['layer'],[*e['position'],*e['size']]) for e in call['runtime_assets']],
    part('design/ui/ch00-01/C0-01.json','레이어 좌표·문구·해상도')],
    '시작 입력 직후 같은 배경에서 메뉴가 사라지고 자동으로 딤·수혁아. 표시',
    '읽을 수 있는 상태에서 새 클릭 또는 Space → 회상 전환','C0-02a','구성 채택 / 딤 요청 반영',
    '문구는 수혁아. 페이드 시간은 미정. 음성 미제작. PSD 글자는 래스터이며 인게임에서는 문자열 렌더링.',call)
memory_base='art/chapter00/memory/memory-path-base-v1.png'
scene('C0-02a','서연의 얼굴·모녀 회상','art/chapter00/review/01-memory-family-v1.png',[
    part(memory_base,'회상 배경'),part('art/chapter00/memory/seoyeon-soi-holding-v1.png','모녀 접촉 그룹 PNG',[650,70,429,835]),
    part('art/chapter00/memory/memory-ground-shadow-review-v1.png','접지 그림자 검수 레이어',[0,0,1672,941])],
    '첫 부름 읽기 완료','대사 확인 후 다음 컷','C0-02b','다음 제작·선택 대상',
    '기존 시안 재사용. 서연 외형·얼굴 확대·표정 보강은 미확정. 모녀는 하나의 PNG이며 개별 몸 레이어 아님.')
for sid,file_name,label,nextid in [('C0-02b','hands-handover','손을 건네기','C0-02c'),('C0-02c','hands-received','손을 받은 상태','C0-03a')]:
    idx='02' if sid.endswith('b') else '03'
    scene(sid,label,f'art/chapter00/review/{idx}-memory-hands-v1.png',[
        part(memory_base,'같은 회상 배경'),part(f'art/chapter00/memory/{file_name}-v1.png','손 상태 PNG',[0,0,1672,941]),
        part('art/chapter00/memory/memory-hands-layers-v1.psd','배경·손 전후 3레이어 / 손 상태 배타 표시')],
        '직전 대사 확인','손 상태 교체 또는 다음 대사 확인',nextid,'미제작',
        '손 두 상태를 동시에 켜지 않는다. 현재 책 위 손과 위치·손바닥 방향이 달라 정확한 매치 컷은 보강 필요.')
scene('C0-03a','현재 책 위 손으로 돌아오기','art/chapter00/present/hand-on-book-v1.png',[
    part('art/chapter00/present/hand-on-book-v1.png','손·책·식탁이 합쳐진 원화 / 분리 PSD 없음')],
    '회상 대사 종료','짧은 전환 후 현재 캠핑카','C0-03b','미제작',
    '전환 시간 미정. 손의 사실적 화풍 피드백과 매치 컷 보강을 함께 검토. 신규 고해상도 분리 원본이 아님.')

by_id={s['id']:s for s in camper['states']}
def camper_parts(state):
    parts=[part(camper['lighting']['stable_dark'] if state['light']=='off' else camper['base'],'고정 배경',[0,0,1672,941])]
    for a in seated['actors']:
        if 'drawing' in a['id'] or (state['repair_pose'] and a['id'].startswith('suhyeok')):continue
        parts.append(part(a['asset'],a['id'],a['rect']))
    if not state['book_packed']:parts.append(part('art/chapter01/layers/pages/sketchbook-blank-v1.png','식탁 위 스케치북',[904,454,102,73]))
    parts.append(part('art/chapter01/layers/sprites/mug-source-v1.png','컵',[1010,480,53,52]))
    keys=['water-packed' if state['water_packed'] else 'water-before',
          'blanket-packed' if state['blanket_packed'] else 'blanket-before',
          'panel-fixed' if state['connection_secured'] else 'panel-loose']
    keys+=['repair'] if state['repair_pose'] else ['flashlight-stored']
    if state['journal_open']:keys+=['journal']
    for key in keys:
        name,rect=camper['placements'][key];parts.append(part(camper['assets'][name]['path'],key,rect))
    if state['repair_pose']:
        for f in ['repair-contact-shadow-v1','flashlight-local-reveal-mask-v1','flashlight-bounce-overlay-v1']:
            parts.append(part('art/chapter00/camper/layers/'+f+'.png','그림자/국소광 보조 레이어',[0,0,1672,941]))
    return parts
states=[
 ('C0-03b','현재 소이와 대화','01-current-conversation','현재 컷 진입','대사·첫 응답 확인','C0-04a','좌우 대화 시안 채택; 실제 인물 원화·UI 조합 대기'),
 ('C0-04a','물·담요 준비 전','01-current-conversation','출발 준비 대화 종료','물 또는 여분 담요 챙기기','C0-04b 또는 C0-04c','미제작'),
 ('C0-04b','물만 준비','02a-water-only','물 먼저 챙김','담요 챙기기','C0-04d','미제작'),
 ('C0-04c','담요만 준비','02b-blanket-only','담요 먼저 챙김','물 챙기기','C0-04d','미제작'),
 ('C0-04d','준비 완료','02-preparation-complete','물·담요 두 행동 완료','준비 몽타주 후 소등 1회','C0-05','미제작'),
 ('C0-05','소등·소이에게 답하기','03-outage-answer-soi','두 준비 행동 완료 뒤 최초 소등','소이에게 답변 선택 → 수납함 조사','C0-06a','선택지·조사 UI 미제작'),
 ('C0-06a','손전등으로 조사','04-flashlight-investigation','손전등 확보','스위치와 연결부 모두 조사, 관계 대사 확인','C0-06b','조사 UI 미제작'),
 ('C0-06b','연결 고정','05-connection-secured','조사·대화 조건 완료','연결 고정 후 실내등 켜기','C0-07a','행동 UI 미제작'),
 ('C0-07a','조명 복구','06-light-restored','실내등 켜기','소이와 대화, 손전등 반환, 책 챙기기','C0-07b','대화·행동 UI 미제작'),
 ('C0-07b','책 정리·첫 기록','07-book-packed-journal-open','책 챙김','첫 문장 선택·저널 확인, 내일 약속','C0-08','기록 UI 미제작'),
 ('C0-08','스스로 불을 끄고 쉬기','08-voluntary-lights-off','약속 뒤 소등 입력','암전·작품명·1일 차로 연결','C1-01','날짜·소등 UI 미제작')]
for sid,label,state_id,entry,advance,nextid,ui in states:
    st=by_id[state_id]
    notes='순서·상태는 최신 콘티 기준이며 입력·저장 로직은 아직 없음. 소이는 물리 조작을 하지 않음.'
    if st['repair_pose']:notes+=' 손 포즈는 연결부를 실제로 잡지 못하므로 보강 필요. 밝은 배경 국소광을 인물 뒤에 합성.'
    if sid=='C0-04d':notes+=' 가방 속 담요와 정리된 짐의 확대 원화는 미제작, 작은 크롭은 대체 시안.'
    scene(sid,label,st['review'],camper_parts(st),entry,advance,nextid,ui,notes,st)

# Chapter 1 is an event draft, not a fixed daily timeline or completed UI.
chapter1=[
 ('C1-01','첫 출발·편의점·귀환', 'art/chapter01/revision-v3/store-clean-base-v3.png',[
   'art/chapter01/revision-v3/store-clean-base-v3.png','art/chapter01/extra-poses/suhyeok-investigate-v2.png','art/chapter01/store/drink-bottle-v1.png'],
   'C0 종료 후 첫 낮','물 확보·귀환·약속 확인','C1-02','지도·목표·회수·가방·상태 UI 미제작. 기존 파일은 배경/단품이며 완성 UI 합성본 아님.'),
 ('C1-02','색연필 전달·첫 그림', 'art/chapter01/01-camper-evening/camper-drawing-v2.png',[
   camper['base'],'art/chapter01/shared-props/pencil-tin-open-v1.png','art/chapter01/layers/pages/sketchbook-blank-v1.png',
   'art/chapter01/layers/pages/sea-started-overlay-v1.png','art/chapter01/layers/pages/sea-complete-overlay-v1.png'],
   '첫 귀환·소이의 부탁','도구 전달→함께 그리기→완성 확인','C1-03','첫 그림 필수 튜토리얼. 이미 가진 색연필 인정. 기존 바다 그림은 재사용 후보이며 최종 그림 내용과 대사집 대조 필요.'),
 ('C1-03','세진·공구·첫 사진',None,[], '첫 그림 후 길 정보 안내','공구 전달→카메라 확보→촬영·앨범 확인','C1-05 또는 선택 사건',
   '주유소·수리점·하천 쉼터 배경, 세진 대화용 원화, 카메라·공구 상세 자산과 촬영 UI는 이 인계에 연결된 납품 파일 없음. 연료 부족 필수 조건 아님.'),
 ('C1-04a','요리·생활 제작',camper['base'],[camper['base'],'art/chapter01/extra-poses/suhyeok-cook-v1.png',
   'art/chapter01/kitchen/soup-pot-v1.png','art/chapter01/shared-props/soup-bowl-v1.png'],
   '식재료 확보 등 조건','요리 또는 생활 제작 결과 확인','현재 장소로 복귀','UI·레시피 수치·미니게임 상세 미정. 후보 원화만 연결.'),
 ('C1-04b','낡은 집·별똥이', 'art/chapter01/revision-v3/house-clean-base-v3.png',[
   'art/chapter01/revision-v3/house-clean-base-v3.png','art/chapter01/dog/byeolddongi-sit-v1.png',
   'art/chapter01/dog/byeolddongi-rest-v1.png','art/chapter01/revision-v3/suhyeok-befriend-v3.png'],
   '낡은 집 조사와 동행 조건','거리를 두고 교감, 후속 재방문','현재 장소로 복귀',
   '별똥이의 집 앞 접지·포즈 배치는 미검증. 기존 캠핑카 배치 좌표를 집으로 자동 복사하지 않음. 첫 편지는 챕터 3으로 이관.'),
 ('C1-05','다음 지역 출발',None,[], '첫 그림·첫 사진과 길 정보 조건 완료','남은 일 안내→목적지·물자 확인→출발','다음 챕터',
   '지도·고갯길 UI/원화 연결 미제작. 정확한 날짜·비용·재방문 정책 미정.')]
for sid,label,preview,paths,entry,advance,nextid,notes in chapter1:
    scene(sid,label,preview,[part(p,'후속 장면 재사용 후보 / 개별 검수 필요') for p in paths],entry,advance,nextid,'미제작',notes)

# Provenance and reproduction files: include current masters as well as generated sources.
for a in opening['assets']:asset(a['path'],'회상 원본·분리 PNG / first draft')
for a in camper['assets'].values():asset(a['path'],'캠핑카 분리 원화')
for folder in ['art/chapter00/sources','art/chapter00/prompts','art/chapter00/camper/sources','art/chapter00/camper/prompts']:
    for p in sorted((ROOT/folder).glob('*')):
        if p.is_file():asset(p.relative_to(ROOT).as_posix(),'가공 전 생성 소스 또는 프롬프트 / 직접 배치하지 않음')
for p in ['art/chapter00/camper/camper-light-states-v1.psd','art/chapter00/camper/power-panel-states-v1.psd',
          'art/chapter00/camper/review/montage-water.png','art/chapter00/camper/review/montage-blanket.png',
          'art/chapter00/camper/review/prepared-supplies-closeup.png','design/dialogue/dialogue-layout-v1.png',
          'design/dialogue/README.ko.md','design/chapter00/camper-assets-v1.json','design/chapter00/opening-assets-v1.json',
          'design/interaction/camper-seated-v1.json','design/chapter01/gallery-scenes.json','design/chapter01/visual-revision-v3.json',
          'tools/prepare-ch00-camper.py','tools/prepare-ch00-memory.py','tools/build-ui-title-styles.py','tools/build-ui-opening-call.py',
          'art/title/fonts/NotoSerifKR-OFL.txt','art/title/fonts/CormorantGaramond-OFL.txt']:
    asset(p,'제작·조합·보완 참고; 파일별 상태 문서 우선')
data={'version':1,'base_resolution':[1920,1080],'art_coordinate_canvas':[1672,941],
      'ui_master_canvas':[3840,2160],'scenes':scenes,'assets':assets,
      'shared_composition':{'table_occlusion':{'method':'copy from current warm/dark base','rect':[835,432,257,201]},
      'order':'background → bodies → table occlusion → props → hands; repair local light behind actor',
      'night_actor_rgb_multiplier':[.39,.47,.66],'night_repair_rgb_multiplier':[.57,.61,.73],
      'placement_note':'art rectangles are x,y,width,height in 1672x941; UI rectangles state their 1920/3840 coordinate system'},
      'warnings':['Scene sub-IDs are documentation identifiers, not implemented Unity scene names.',
      'All chapter 1 previews are reference/candidate art, not completed matching UI.',
      'Older gallery JSON contains deferred letter and old dialogue branches; do not execute it as latest chapter 1 script.']}
(OUT/'storyboard-assets-v1.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

lines=['# 챕터 0~1 · 콘티와 실제 파일 연결표','',
       '2026-09-10. Unity 개발 시 장면과 파일을 함께 확인하는 인계 문서. `D:/Demo3`가 현재 프로젝트 루트이며 아래 링크는 저장소 상대 경로다. 다른 PC에서도 저장소 내부 구조를 유지하면 연결된다.',
       '', '## 사용 순서','',
       '1. [합의한 시작 흐름](UNITY-OPENING-HANDOFF.ko.md)을 읽고 장면 ID를 찾는다.',
       '2. 각 장면의 합성 이미지로 구도를 확인하고, 표의 배경·인물·소품·UI 파일을 개별로 연결한다.',
       '3. [기계 판독 파일 목록](../design/unity-handoff/storyboard-assets-v1.json)의 좌표·크기·SHA-256·상태를 확인한다.',
       '4. 임시/미제작/보완 필요 항목은 그대로 개발 완료로 처리하지 않는다. 후속 장면 UI는 사용자와 하나씩 검토한다.',
       '', '## 좌표·레이어 공통 규칙','',
       '- 게임 화면은 1920×1080. 기존 원화의 배치 좌표는 1672×941이며 `[x,y,w,h]` 순서다. UI는 각 JSON의 1920 논리 좌표와 3840 편집 좌표를 구분한다.',
       '- 원화는 비율을 유지하여 화면 안에 맞춘다: `s=min(1920/1672,1080/941)`, `ox=(1920-1672*s)/2`, `oy=(1080-941*s)/2`. `(x,y,w,h)`는 `(ox+x*s,oy+y*s,w*s,h*s)`로 변환한다. 독립 축 늘리기로 미세하게 뒤틀지 않는다.',
       '- 캠핑카 식탁 가림은 현재 밝음/소등 베이스의 `[835,432,257,201]` 영역을 같은 위치에 복사한다. 오래된 v1 식탁 픽셀을 v3에 새로 덮지 않는다.',
       '- 합성 순서: 배경→앉은 몸→식탁 가림→소품→손. 점검 때는 앉은 수혁을 숨기고 배경 국소광→접지 그림자→점검 인물 순서로 그린다. 구현 상세는 `tools/prepare-ch00-camper.py`와 JSON에 있다.',
       '- 일반 야간 인물 RGB 배율은 검수 스크립트에서 `[0.39,0.47,0.66]`, 점검 인물은 `[0.57,0.61,0.73]`이다. Unity 최종 조명 값으로 확정된 것은 아니다.',
       '- 동일한 손·연결부의 전후 레이어를 동시에 켜지 않는다. 물·담요 순서가 바뀌어도 준비 완료 상태는 동일하고 소등은 한 번만 발생한다.',
       '- PNG는 실제 크기·알파, PSD는 실제 레이어 범위만 보장한다. 합성 검수 이미지와 작은 크롭을 최종 분리 원본으로 사용하지 않는다.',
       '', '장면 세부 ID는 문서 연결용으로 부여했으며 Unity Scene 이름이나 실행 코드가 이미 존재한다는 뜻은 아니다. C0-00/A, C0-01 구성 외의 UI는 후속 검토 대상이다.']
for s in scenes:
    lines+=['',f"## {s['id']} · {s['label']}",'',f"**UI 상태:** {s['ui_status']}",'']
    if s['preview']:
        lines += [f"![{s['label']} 참고 화면](../{s['preview']})",'',
                  f"그림 파일: [{s['preview']}](../{s['preview']}). {'챕터 1 참고 원화이며 이 장면의 UI 합성 완료본은 아니다.' if s['id'].startswith('C1') else '현재 합성/원화 검수본이며 제작 상태는 아래 표를 따른다.'}",'']
    else:lines+=['대응하는 완성 이미지 없음. 후속 제작 대상.','']
    lines += [f"- 진입: {s['entry']}",f"- 입력·전환: {s['advance']}",f"- 다음: {s['next']}",f"- 주의: {s['notes']}",'']
    if s['parts']:
        lines+=['| 역할 | 실제 파일 | 원본 크기 | 배치 `[x,y,w,h]` |','| --- | --- | --- | --- |']
        for e in s['parts']:
            a=assets[e['path']];size='×'.join(map(str,a.get('native_size',[]))) or '데이터/폰트'
            rect=str(e['rect']) if e['rect'] else '해당 JSON 또는 원본 전체'
            lines.append(f"| {e['role']} | [{e['path']}](../{e['path']}) | {size} | {rect} |")
    if s['state_data'] and s['id'] not in ['C0-00','C0-01']:
        st={k:v for k,v in s['state_data'].items() if k not in ['review','id']}
        lines+=['','상태 기록: `'+json.dumps(st,ensure_ascii=False)+'`']
lines+=['','## 전체 관련 파일 인덱스','',
        '가공 전 소스·프롬프트·PSD·스크립트까지 포함한다. 소스 PNG의 체크무늬/색 배경은 런타임 투명 자산이 아니다. 목록의 존재 여부와 해시는 실제 파일에서 읽었다.',
        '', '| 분류 | 경로 | 원본 크기 / 형식 |','| --- | --- | --- |']
for path,a in sorted(assets.items()):
    dims='×'.join(map(str,a.get('native_size',[])))+' '+a.get('mode','')
    lines.append(f"| {a['role']} | [{path}](../{path}) | {dims.strip() or Path(path).suffix} |")
lines+=['','## 남은 제작과 검증','',
        '- 대사집: 최종 대사 ID·화자·감정·동작을 연결할 전체 대사집은 대기. 문서의 입력 설명만으로 대사를 새로 확정하지 않는다.',
        '- 대화용 원화: 좌우 인물 시안은 평면 그림. 인물별 큰 원본·표정·시선·행동의 실제 분리 파일은 대사집 이후 제작한다.',
        '- 소리: 호출 음성·환경음·물건·스위치 효과음이 이 인계에 연결된 실제 파일로 존재하지 않는다. 음소거에서도 문구로 진행 가능해야 한다.',
        '- 확대 원화: 기존 작은 앉기 인물·책·컵, 준비 완료 짐 크롭과 손 접촉 보강. [원화 검수 보고서](CHAPTER00-ART-REVIEW.ko.md) 참조.',
        '- 챕터 1: 7곳 장소 전부의 원화/UI가 완료된 것은 아니다. 주유소·잡화점·수리점·하천 쉼터·고갯길은 [최신 이벤트 계획](CHAPTER01-EVENT-QUEST-DRAFT.ko.md)의 제작 대상으로 관리한다.',
        '- 이전 `gallery-scenes.json`에는 첫 편지와 오래된 대사·선택 분기가 남아 있다. 자산 찾기 참고이며 최신 실행 콘티로 그대로 사용하지 않는다. 첫 편지는 챕터 3 무렵, 첫 그림·사진은 필수 안내, 초기 긴급 연료는 없음.',
        '- Unity 페이드·입력·저장·해상도 대응·실제 UI 동작, Photoshop 앱 직접 검수는 미실시. 파일 보관과 실행 검증을 구분한다.',
        '', '갱신: `tools/build-storyboard-asset-map.py`. 파일 경로·이미지 치수·해시를 다시 읽어 이 문서와 JSON을 갱신한다. 이야기 순서가 바뀌면 스크립트의 장면 정의도 함께 갱신한다.']
(ROOT/'docs/CH00-01-STORYBOARD-ASSET-MAP.ko.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps({'scenes':len(scenes),'verified_files':len(assets)},ensure_ascii=False))
