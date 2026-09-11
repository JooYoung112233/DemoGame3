"""One editable manuscript; extracts existing text verbatim, never invents dialogue."""
from pathlib import Path
import json,re
R=Path(__file__).resolve().parents[1];out=R/'docs/06-대사/DIALOGUE-BOOK-CH00-01.ko.md'
if out.exists():raise SystemExit('Existing editing book preserved. Do not overwrite user edits.')
def js(p):return json.loads((R/p).read_text(encoding='utf-8-sig'))
book=['# Live49 챕터 0~1 대사집 · 사용자 수정본','', '대사 수정은 이 파일 한 곳에서 한다. **현재 대사**는 제작 자료에서 그대로 옮긴 초안이다. **수정 대사 / 새 대사**에 최종 문장을 작성하고, 필요하면 화자·행을 추가한다. 삭제할 대사는 수정 칸에 `[삭제]`를 적는다. 빈 수정 칸은 미수정이며 삭제가 아니다. 아직 게임 데이터에 자동 반영되는 파일은 아니다.','', '기준: 2026-09-11 현재 콘티. 챕터 0은 분기별 실제 문장이 있으며, 챕터 1은 화면 대사와 대사 의도가 섞여 있다. 미작성 장면을 완성된 대사처럼 채우지 않았다. ID와 출처를 남겨 후속 반영 시 대조한다.','', '## 수정할 때 유지할 이야기 조건','', '- 이름표는 인물을 알기 전 `?`. 호명·소개·설명으로 연결된 뒤 이름 공개. 제작용 화자명과 구분한다. “수혁아.” 뒤 수혁, “소이 손 좀 잡아줘.” 뒤 소이. 공개 계기 문장을 삭제·변경하면 그 뒤 표시 시점도 함께 검토한다.','- 서연의 회상 얼굴은 숨긴다. 소이는 주관적 체험에 존재하며 NPC가 직접 확인하거나 독립적으로 물건을 움직이지 않는다. 그림은 수혁이 그린다.','- 시작 시 수혁은 비감염. 강제 물림은 중간 사건이며 날짜 미정. 평균 약 30일은 최초 증상 후 변형 시작까지의 작중 평균이다. 49일은 별도 서사 기조.','- 서연의 편지는 챕터 3. 그림 도구는 기존 책과 색연필. 튜토리얼은 진행 보장, 선택 사건 25개 전부를 의무 수행하지 않는다.','- 기존 문장이 최신 설정과 어긋나더라도 이 대사집에서 임의로 고치지 않았다. 특히 식량 분배·소이의 행동·NPC의 반응·여행 목적을 검토한다.','', '## 챕터 0 · 사진 진입과 회상','']
count=0
def line(ident,speaker,text,source,kind='대사'):
    global count
    count+=1;book.extend([f'#### {ident} · {speaker or "나레이션"} · {kind}',f'- 현재 대사: {text}','- 수정 대사: ','- 표정·말투·쉼 / 메모: ',f'- 출처: `{source}`',''])
line('C0-ENTRY-CALL','서연 · 표시 ?','수혁아.','design/ui/ch00-01/photo-memory.mjs / entry call')
mem=js('design/ui/ch00-01/memory-dialogue.json');inserted=False
for x in mem['lines']:
    if x['id'].startswith('C0-03') and not inserted:
        book.extend(['## 챕터 0 · 사진 복귀와 현재',''])
        line('C0-PRESENT-CALL','소이','아빠?','design/ui/ch00-01/photo-memory.mjs / return call');inserted=True
    line(x['id'],x.get('speaker',''),x['text'],'design/ui/ch00-01/memory-dialogue.json / '+x['id'],x.get('kind','대사'))
book.extend(['## 챕터 0 · 첫 응답부터 취침까지','', '아래는 콘티 등록 순서다. 선택 분기는 모두 연속해서 재생되는 대사가 아니다. 각 장면의 다음 연결과 선택 조건을 확인한다.',''])
c0=js('design/chapter00/continuation-v1/story.json')
for key,node in c0['nodes'].items():
    book.extend([f'### C0-{key} · {node["title"]}',f'- 장면 목적: {node.get("purpose","")}',f'- 배경 상태: `{node.get("scene","")}` · 다음 연결: `{node.get("next")}`',f'- 조건·결과: `{json.dumps({k:node[k] for k in ["requires","unless","effects"] if k in node},ensure_ascii=False)}`',''])
    for i,x in enumerate(node.get('lines',[]),1):line(f'C0-{key}-L{i:02}',x.get('speaker',''),x['text'],f'design/chapter00/continuation-v1/story.json / nodes/{key}/lines/{i-1}')
    for i,x in enumerate(node.get('choices',[]),1):
        line(f'C0-{key}-CHOICE{i:02}','선택지',x['label'],f'design/chapter00/continuation-v1/story.json / nodes/{key}/choices/{i-1}','선택 문구 · 반드시 발화하는 것은 아님')
        book.extend([f'- 선택 연결·조건: `{json.dumps(x,ensure_ascii=False)}`',''])
    if not node.get('lines'):book.extend(['- 추가 대사(필요할 때): ',''])
book.extend(['## 챕터 1 · 1일 차 아침~색연필 부탁','', '화면에 구워진 시안 대사와 흐름 데이터의 의도를 함께 적었다. 소요 시간·확률·버튼 전체는 대사에 포함하지 않는다.',''])
st=js('design/ui/day01-v1/strings.ko.json');day=js('design/ui/day01-v1/story.json')
speech={'D1-01-need':('05','수혁'),'D1-08-note':('14','수혁'),'D1-09-return':('03','수혁 · 독백'),'D1-11-kitchen':('08','수혁'),'D1-13-quest':('08','수혁')};seen=set()
for key,node in day['steps'].items():
    if 'title' not in node:
        book.extend([f'### D1-{key} · 연결 처리',f'- 데이터: `{json.dumps(node,ensure_ascii=False)}`','- 이 노드는 대사 화면이 아닌 연결·종료 처리다.','']);continue
    book.extend([f'### D1-{key} · {node["title"]}',f'- 사건: {", ".join(node.get("event_refs",[]))}',f'- 목적: {node.get("purpose","")}',f'- 연출·조건: {node.get("direction","")}',f'- 대사 의도: {node.get("dialogue_intent","구체 문장 미작성")}',f'- 출처: `design/ui/day01-v1/story.json / steps/{key}`',''])
    scr=node.get('screen','')
    if scr in speech and scr not in seen:
        num,who=speech[scr];sid=scr+'.'+num+'_text';line(sid,who,st[sid],'design/ui/day01-v1/strings.ko.json / '+sid,'화면 시안 대사');seen.add(scr)
    book.extend(['- 새 대사(화자: 문장): ','- 다음 대사: ','- 수정 메모: ',''])
    if node.get('actions'):book.extend(['- 분기 참고: '+ ' / '.join(f'{a["label"]} → {a.get("to","")}' for a in node['actions']),''])
book.extend(['## 챕터 1 · E01~E25 사건별 추가 대사','', 'E01~E05는 위 1일 차 상세 장면을 먼저 수정한다. 이 절은 추가 반응과 후속 사건을 쓰는 자리다. 기존 사건 설명은 발화 문장이 아니며, 정확한 문장을 새 대사 칸에 작성한다.',''])
for e in js('design/chapter01/event-quest-catalog-v2.json')['events']:
    book.extend([f'### {e["id"]} · {e["title"]}',f'- 장소: {", ".join(e.get("locations",[]))}',f'- 발생 조건: {e.get("trigger","")}',f'- 장면: {e.get("scene","")}',f'- 선택 분기: {e.get("branches","")}',f'- 완료·결과: {e.get("completion","")} / {e.get("result","")}',f'- 원화: '+ ' · '.join(f'`{p}`' for p in e.get('art_resources',[])),f'- 출처: `design/chapter01/event-quest-catalog-v2.json / {e["id"]}`','', '- 진입 대사(화자: 문장): ','- 주고받는 대사: ','- 선택지 / 거절·나중에 반응: ','- 성공·완료 반응: ','- 재방문 / 이미 보유한 경우: ','- 표정·말투·쉼 / 메모: ',''])
book.extend(['## 추가 집필 확인','', '- [ ] 회상·현재 이름 공개 시점','- [ ] 라디오의 캠프 안내와 여행 목적','- [ ] 사진을 더 찍고 싶은 동기 → 사진기사와의 만남','- [ ] 가방을 부탁하는 사정과 카메라를 건네는 이유','- [ ] 소이를 소개받은 NPC의 과하지 않은 망설임','- [ ] 물·색연필·쪽지 선발견 시 분기','- [ ] 강아지를 데려오지 않은 경우','- [ ] 첫 주 출발의 이유와 다음 목표','',f'기존 문장·선택 문구 {count}개를 원문 그대로 수록. 이후 문장을 추가해도 기존 ID는 유지한다.'])
out.write_text(re.sub(r'(?m)^(#{1,6} .+)\n(?=\S)',r'\1\n\n','\n'.join(book))+'\n',encoding='utf8');print(json.dumps({'path':str(out),'verbatim_entries':count,'c0_nodes':len(c0['nodes']),'day1_steps':len(day['steps']),'events':25},ensure_ascii=False))
