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
soft_map_file=ROOT/'art/style-revision/soft-storybook-v1/replacement-map.json'
soft_map=json.loads(soft_map_file.read_text(encoding='utf-8')) if soft_map_file.exists() else {'path_map':{},'replacements':[]}
def asset(path,role):
    path=soft_map['path_map'].get(path,path)
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
def part(path,role,rect=None):return {'path':soft_map['path_map'].get(path,path),'role':role,'rect':rect}

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
photo_root='art/chapter00/memory/dialogue-v1/'
photo_parts=[part(photo_root+'family-sunset-source-v1.png','동일 사진 원본 / 1672×941'),part(photo_root+'polaroid-frame-v1.png','투명 구멍이 있는 별도 종이'),part(photo_root+'seoyeon-face-mask-v1.png','얼굴 효과 영역'),part(photo_root+'family-polaroid-layers-v1.psd','사진·종이·숨김 얼굴 마스크 3레이어'),part('art/chapter01/revision-v3/camper-clean-base-v3.png','고정 캠핑카'),part('docs/03-콘티/챕터0/C0-PHOTO-MEMORY-IMPLEMENTATION.ko.md','최신 초 단위 콘티'),part('design/ui/ch00-01/photo-memory.mjs','사진 효과·임시 잡음')]
scene('C0-01','흐린 벽 사진과 첫 부름',photo_root+'family-polaroid-clean-review-v1.png',photo_parts,
    '기존 책 시작 카메라 1.35초 후 사진 디졸브','사진 진입 2.40초 수혁아. / 새 입력으로 회상','C0-02a','흐름 채택 / 실행 시안',
    '첨부 PNG는 효과 없는 원본 합성이다. 실제 진입은 사진만 블러. 종이 [1442,269,44,28.34]→[300,70,1320,850.16].')
memory_base='art/chapter00/memory/memory-path-base-v1.png'
scene('C0-02a','멈춘 발 · 나레이션에서 인물 대화로','art/chapter00/memory/dialogue-v1/feet-scene-review-v1.png',[
    part(memory_base,'원래 길 배경'),
    part('art/chapter00/memory/dialogue-v1/feet-paused-v1.png','새 발 분리 원화'),
    part('art/chapter00/memory/dialogue-v1/feet-scene-layers-v1.psd','길 원본·근접 배치·발 3레이어'),
    part('art/chapter00/memory/dialogue-v1/seoyeon-speaking-v1.png','서연 초상 보관 / 현재 런타임 비표시',[1120,70,700,1050]),
    part('art/chapter00/memory/dialogue-v1/suhyeok-response-v1.png','수혁 기존 초상 복귀',[100,50,720,1080]),
    part('design/ui/ch00-01/psd/C0-02-dialogue.psd','공통 하단 대화창 5레이어 PSD'),
    part('design/ui/ch00-01/memory-dialogue.json','나레이션·대사·화자·1920 기준 배치·입력'),
    part('docs/03-콘티/챕터0/C0-MEMORY-DIALOGUE.ko.md','세부 콘티와 편집 범위')],
    '첫 부름 완료 뒤 새 입력 → 발 근접 회상 → 0.6초 여백',
    '같은 배경에서 나레이션 → 서연 2문장 → 수혁 응답. 출력 중 완성, 다음 입력으로 진행','C0-02b',
    '흐름 채택 / 브라우저·PNG·레이어 PSD 시안 제작',
    '모녀 전신 즉시 공개를 대체. 초상·문구·세부 UI는 검토 중. 좌표는 1920×1080 기준. 원본 해상도 목표 미달은 개별 문서 참조.')
for sid,label,nextid in [('C0-02b','두 손을 맞잡기 · 수정 v2','C0-02c'),('C0-02c','다시 엄마 손 잡을래 · 소이의 답','C0-03a')]:
    scene(sid,label,'art/chapter00/memory/dialogue-v1/hands-held-review-v2.png' if sid=='C0-02b' else 'design/ui/ch00-01/review/C0-02-closing.png',[
        part('art/chapter00/memory/dialogue-v1/path-closeup-review-v1.png','기존 길 근접 배경 시안'),
        part('art/chapter00/memory/dialogue-v1/hands-held-v2.png','수혁·소이 두 손 접촉 그룹',[0,0,1672,941]),
        part('art/chapter00/memory/dialogue-v1/hands-held-layers-v2.psd','원본 숨김·길·두 손 3레이어'),
        part('art/chapter00/memory/dialogue-v1/hands-held-v2.json','수정 이유·검증·원본 상태'),part(photo_root+'soi-answer-v1.png','소이 답변 초상 시안',[1100,180,700,1050]),part(photo_root+'soi-answer-layers-v1.psd','소이 원본·투명 초상 2레이어')],
        '수혁 응답 확인' if sid=='C0-02b' else '두 손 잡은 상태 유지',
        '두 손 컷 0.3초 등장+0.8초 여백 뒤 엄마 질문→소이 답→엄마 마무리. 각각 새 입력, 마지막 입력에 사진 복귀',nextid,'두 손 수정 시안 / 사용자 검토 대기',
        '세 손 포개기 구도는 사용자 지적으로 제외. 서연이 손을 놓는 중간 동작을 생략한다. C0-02c는 같은 그림을 유지하는 후속 상태이며 별도 새 손 컷이 아니다.')
scene('C0-03a','같은 사진 복귀 · 서연 얼굴 소실',photo_root+'family-polaroid-clean-review-v1.png',photo_parts,
    '서연 마지막 대사를 읽은 후 새 입력','암전 .4초→사진 등장 .5초→블러 해제→1.42~1.58초 지지직→3.10초 아빠?','C0-03b','실행 시안 / 소이 부름 후 입력으로 현재 대화',
    '첨부는 효과 없는 원본 합성. 얼굴은 마스크로 계속 불분명. 이후 현재 대화 3문장까지 연결. 선택지는 후속 제작. 원본·프레임·마스크는 분리.')

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
 ('C0-03b','현재 소이와 대화','01-current-conversation','현재 컷 진입','대사·첫 응답 확인','C0-04a','현재 대화 3문장 실행 시안; 수혁·소이 실명 표시; 선택지 다음 검토'),
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

# Current dialogue overrides the old room-only review with traceable runtime composition.
current=next(s for s in scenes if s['id']=='C0-03b')
current['preview']='design/ui/ch00-01/review/C0-03-response.png'
asset(current['preview'],'현재 대화 합성 검토 / 새 원화 원본 아님')
current['next']='C0-03c 상세 초안 → C0-04a; 실행 시안은 소이 질문에서 정지'
current['advance']='응. 잠깐 옛날 생각했어. → 수혁 내면 → 내일도 여기 있어?; 문장별 새 입력, 마지막 정지'
current['parts'] += [part('art/chapter00/memory/dialogue-v1/suhyeok-response-v1.png','수혁 최신 부드러운 동화풍 초상',[100,50,720,1080]),part('art/style-revision/soft-storybook-v1/psd/suhyeok-dialogue.psd','수혁 생성 원본·투명 초상 실제 2레이어')]
current['notes']='사진 .65초 디졸브+.35초 여백. 서연 초상 없음, 수혁·소이는 앞선 대사로 이름 공개, 서연 회상은 ?. 다음 선택지는 후속 검토. docs/03-콘티/챕터0/C0-PRESENT-DIALOGUE.ko.md 우선.'
for path in ['docs/05-원화/ART-STYLE-MIGRATION.ko.md','docs/05-원화/ART-STYLE-DIRECTION.ko.md','tools/prepare-suhyeok-semi-v2.py','docs/03-콘티/챕터0/C0-PRESENT-DIALOGUE.ko.md','docs/06-대사/SPEAKER-IDENTITY-RULES.ko.md','design/ui/ch00-01/speaker-identity.mjs','tools/render-dialogue-review.py','tools/test-speaker-identity.mjs','design/ui/ch00-01/review/C0-03-narration.png','design/ui/ch00-01/review/C0-03-soi-question.png']:
    asset(path,'현재 대화·화자 이름 공개 규칙·검토')

scene('C0-03c','첫 응답·빈 페이지·출발 결정 · 상세 초안',None,[
    part('docs/03-콘티/챕터0/C0-NEXT-DEPARTURE-STORYBOARD.ko.md','대사·카메라·입력·분기·저장 경계 신규 초안'),
    part('docs/00-제작관리/CH00-WEEK01-STORYBOARD-PROGRESS.ko.md','챕터 0~1 첫 주 상세 콘티 목표와 진행표'),
    part('art/chapter01/layers/sketchbook-open-blank-v1.png','1536×1024 RGB 큰 책 참고 원본; 체크 배경 분리·일치 검수 필요'),
    part('art/chapter00/memory/dialogue-v1/suhyeok-response-soft-v3.png','부드러운 동화풍 수혁 응답'),
    part('art/chapter00/memory/dialogue-v1/soi-answer-soft-v3.png','부드러운 동화풍 소이 응답')],
    'C0-03-D02 내일도 여기 있어? 읽기 완료 후 새 입력',
    '말투 선택 2갈래 → 빈 페이지 클릭·대화 → 내일 떠나자 → 준비 대상 열기','C0-04a',
    '상세 글 콘티 초안; 선택 UI·책 인서트·실행 연결 미제작',
    '기존 컷09~11의 대사를 연결한 신규 연출안. 시간과 세부 대사 채택 전. 두 분기 모두 합류하며 책은 복구 뒤 챙긴다.')

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
for folder in ['art/chapter00/memory/dialogue-v1','design/ui/ch00-01/layers/C0-02-dialogue','design/ui/ch00-01/runtime/1920x1080/C0-02']:
    for p in sorted((ROOT/folder).glob('*')):
        if p.is_file():asset(p.relative_to(ROOT).as_posix(),'회상 대화 제작 자산·원본·프롬프트·검수; manifest 상태 우선')
for p in ['docs/03-콘티/챕터0/C0-PHOTO-MEMORY-IMPLEMENTATION.ko.md','docs/03-콘티/챕터0/C0-PHOTO-MEMORY-REVISION.ko.md','design/ui/ch00-01/photo-memory.mjs','tools/prepare-photo-memory-v1.py','tools/test-photo-memory.mjs','docs/03-콘티/챕터0/C0-MEMORY-DIALOGUE.ko.md','design/ui/ch00-01/memory-dialogue.json','design/ui/ch00-01/memory-dialogue.mjs','design/ui/ch00-01/C0-02-assets.json','tools/build-memory-dialogue-assets.py','tools/test-memory-dialogue.mjs','tools/prepare-handhold-v2.py','tools/render-memory-closing-preview.py']:
    asset(p,'회상 대화 인계·재현·검사')
for p in read('design/ui/ch00-01/C0-02-assets.json')['reviews']:asset(p,'회상 대화 합성 미리보기 1920×1080')
for folder in ['art/chapter00/sources','art/chapter00/prompts','art/chapter00/camper/sources','art/chapter00/camper/prompts']:
    for p in sorted((ROOT/folder).glob('*')):
        if p.is_file():asset(p.relative_to(ROOT).as_posix(),'가공 전 생성 소스 또는 프롬프트 / 직접 배치하지 않음')
for p in ['art/chapter00/camper/camper-light-states-v1.psd','art/chapter00/camper/power-panel-states-v1.psd',
          'art/chapter00/camper/review/montage-water.png','art/chapter00/camper/review/montage-blanket.png',
          'art/chapter00/camper/review/prepared-supplies-closeup.png','design/dialogue/dialogue-layout-v1.png',
          'design/dialogue/README.ko.md','design/chapter00/camper-assets-v1.json','design/chapter00/opening-assets-v1.json',
          'design/interaction/camper-seated-v1.json','design/chapter01/gallery-scenes.json','design/chapter01/visual-revision-v3.json',
          'tools/prepare-ch00-camper.py','tools/prepare-ch00-memory.py','tools/build-ui-title-styles.py','tools/build-ui-opening-call.py',
          'docs/03-콘티/챕터0/C0-OPENING-DIRECTION.ko.md','design/unity-handoff/C0-opening-direction-v1.json',
          'design/ui/ch00-01/opening-motion.html','design/ui/ch00-01/opening-motion.css',
          'design/ui/ch00-01/opening-motion.mjs','design/ui/ch00-01/opening-motion-core.mjs',
          'tools/serve-opening-motion.cjs','tools/test-opening-motion.mjs','design/unity-handoff/opening-motion-review-qa.json',
          'art/title/fonts/NotoSerifKR-OFL.txt','art/title/fonts/CormorantGaramond-OFL.txt']:
    asset(p,'제작·조합·보완 참고; 파일별 상태 문서 우선')
if soft_map_file.exists():
    asset('docs/05-원화/ART-SOFT-STORYBOOK-PASS.ko.md','최신 동화풍 수정 범위·레이어·해상도 검수')
    asset(soft_map_file.relative_to(ROOT).as_posix(),'이전 원화와 현재 원본·투명 PNG·PSD 대응표')
    for item in soft_map['replacements']:
        for key in ['replacement','native_master','psd','source']:
            asset(item[key],'부드러운 동화풍 '+item['id']+' / '+key)
    for p in sorted((ROOT/'art/style-revision/soft-storybook-v1').glob('*.json')):
        asset(p.relative_to(ROOT).as_posix(),'개별 실제 크기·알파·PSD·배치 검수 메타데이터')
    asset('art/style-revision/soft-storybook-v1/psd/suhyeok-seated-split.psd','식탁 뒤 몸과 앞 팔의 실제 분리 레이어')

data={'version':1,'base_resolution':[1920,1080],'art_coordinate_canvas':[1672,941],
      'opening_direction':{'document':'docs/03-콘티/챕터0/C0-OPENING-DIRECTION.ko.md','data':'design/unity-handoff/C0-opening-direction-v1.json','status':'photo-memory-flow-approved-art-and-timing-review','latest_document':'docs/03-콘티/챕터0/C0-PHOTO-MEMORY-IMPLEMENTATION.ko.md'},
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
       '> 1일 차 낮 신규 UI·종이·빈 책과 파일 연결은 [최신 상세 콘티](CH01-DAY01-NOON-STORYBOARD.ko.md), `design/ui/day01-v1/manifest.json`, `design/chapter01/day01-flow-v1.json`에서 관리한다. 아래 기존 장면 인덱스의 1일 차 흐름보다 이 최신 자료가 우선한다.','',
       '2026-09-10. Unity 개발 시 장면과 파일을 함께 확인하는 인계 문서. `D:/Demo3`가 현재 프로젝트 루트이며 아래 링크는 저장소 상대 경로다. 다른 PC에서도 저장소 내부 구조를 유지하면 연결된다.',
       '', '## 사용 순서','',
       '세부 연출 검토: [시작 클릭·카메라·글자 출력·소리·페이드 타임라인](C0-OPENING-DIRECTION.ko.md). 아직 제안값이며 각 구간을 사용자와 함께 선택한다. 이후 장면도 같은 항목을 채운다.', '',
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
        '- 대화용 원화: 최신 동화풍 대화 3장과 수혁 행동 8장은 실제 투명 PNG·분리 인물 PSD를 제공한다. 표정별 추가 원화는 대사집 이후 제작한다. [최신 수정 범위와 한계](ART-SOFT-STORYBOOK-PASS.ko.md)를 확인한다.',
        '- 소리: 호출 음성·환경음·물건·스위치 효과음이 이 인계에 연결된 실제 파일로 존재하지 않는다. 음소거에서도 문구로 진행 가능해야 한다.',
        '- 확대 원화: 기존 작은 앉기 인물·책·컵, 준비 완료 짐 크롭과 손 접촉 보강. [원화 검수 보고서](CHAPTER00-ART-REVIEW.ko.md) 참조.',
        '- 챕터 1: 7곳 장소 전부의 원화/UI가 완료된 것은 아니다. 주유소·잡화점·수리점·하천 쉼터·고갯길은 [최신 이벤트 계획](CHAPTER01-EVENT-QUEST-DRAFT.ko.md)의 제작 대상으로 관리한다.',
        '- 이전 `gallery-scenes.json`에는 첫 편지와 오래된 대사·선택 분기가 남아 있다. 자산 찾기 참고이며 최신 실행 콘티로 그대로 사용하지 않는다. 첫 편지는 챕터 3 무렵, 첫 그림·사진은 필수 안내, 초기 긴급 연료는 없음.',
        '- Unity 페이드·입력·저장·해상도 대응·실제 UI 동작, Photoshop 앱 직접 검수는 미실시. 파일 보관과 실행 검증을 구분한다.',
        '', '갱신: `tools/build-storyboard-asset-map.py`. 파일 경로·이미지 치수·해시를 다시 읽어 이 문서와 JSON을 갱신한다. 이야기 순서가 바뀌면 스크립트의 장면 정의도 함께 갱신한다.']
(ROOT/'docs/00-제작관리/CH00-01-STORYBOARD-ASSET-MAP.ko.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps({'scenes':len(scenes),'verified_files':len(assets)},ensure_ascii=False))
