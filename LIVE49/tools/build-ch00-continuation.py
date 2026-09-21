"""Single source for Chapter 0 continuation dialogue, playable review and written storyboard."""
from pathlib import Path
import json
R=Path(__file__).resolve().parents[1]
O=R/'design/chapter00/continuation-v1';O.mkdir(parents=True,exist_ok=True)
nodes={}
def choice(label,target,effects=None,requires=None,unless=None):
    return {'label':label,'target':target,'effects':effects or {},'requires':requires or [],'unless':unless or []}
def node(id,title,beat,lines=(),next=None,scene='current',effects=None,choices=None,camera=None,cue=None):
    nodes[id]={'id':id,'title':title,'purpose':beat,'scene':scene,'lines':[{'speaker':a,'text':b} for a,b in lines], 'next':next,'effects':effects or {},'choices':choices or [],'camera':camera or [1,.5,.5],'cue':cue,'draft':True}
node('reply','첫 응답','소이의 질문을 플레이어가 받아 여행의 이유를 듣는다.',choices=[choice('다른 데 가 보고 싶어?','reply_a',{'opening_reply':'curiosity'}),choice('어디로 가고 싶은데?','reply_b',{'opening_reply':'destination'})])
node('reply_a','다른 풍경','이동 욕구를 위험 회피만으로 설명하지 않는다.',[('수혁','다른 데 가 보고 싶어?'),('소이','응. 다른 것도 보고 싶어.'),('소이','아침에도 저 표지판이 보였잖아.')],next='book_look')
node('reply_b','정하지 않은 목적지','정확한 목적지가 아직 없는 이유를 아이의 호기심으로 받는다.',[('수혁','어디로 가고 싶은데?'),('소이','아직 몰라. 가면서 생각할래.'),('소이','여기서는 창밖이 계속 똑같아.')],next='book_look')
node('book_look','식탁의 책','말한 풍경이 앞으로 채울 빈 페이지와 이어진다.',[('수혁','그럼 새로운 걸 보면, 여기 그려 둘까?')],choices=[choice('식탁의 스케치북 살펴보기','book')],camera=[1.08,.58,.53])
node('book','남아 있는 페이지','새 책이 아니라 지금 쓰는 책에 의미를 쌓는다.',[('수혁','여기는 비워 뒀네.'),('소이','응. 나중에 그릴 거야.'),('수혁','모서리가 많이 닳았네.'),('소이','그래도 여기는 깨끗해.'),('수혁','아직 그릴 데가 많네.')],next='departure',effects={'blank_page_seen':True},camera=[1.08,.58,.53])
node('departure','내일 떠나자','목적지를 정하기보다 함께 보는 일을 약속한다.',[('수혁','그럼 내일 떠나자.'),('소이','멀리?'),('수혁','네가 보고 싶은 게 있을 때까지.'),('소이','아침 되면?'),('수혁','응. 오늘은 준비만 해 두고.')],next='prepare_intro',effects={'departure_decided':True})
node('prepare_intro','차 안에서 챙기는 이유','같은 캠핑카 안에서 물건을 옮기는 조작에 이유를 준다.',[('수혁','차가 움직이면 굴러다니겠다. 꺼내 둔 것부터 넣어 두자.'),('소이','책도?'),('수혁','책은 마지막에. 아직 보고 있어도 돼.')],next='prepare')
node('prepare','떠날 준비','두 필수 물건만 순서 자유. 창밖과 기존 사진은 선택 관찰.',choices=[choice('물통 챙기기','water',unless=['water_packed']),choice('여분 담요 챙기기','blanket',unless=['blanket_packed']),choice('잠깐 창밖 보기','window',unless=['window_seen']),choice('벽의 가족사진 살펴보기','photo_observe',unless=['photo_observed'])],scene='preparation')
node('photo_observe','처음 보았던 사진','도입과 동일한 사진이다. 사진을 붙인 오래된 테이프 끝이 들떠 있다는 생활 속 문제만 발견한다. 새 회상·날짜 추리·얼굴 복원은 없다.',scene='preparation',choices=[choice('준비 계속하기','prepare',{'photo_observed':True})],camera=[1.08,.76,.27])
node('water','남은 물','C0에서 새 물을 획득하는 것이 아니라 남은 물을 고정한다.',[('','물통을 기울이자 바닥 가까이에서 물소리가 났다.'),('수혁','아예 없는 건 아닌데… 내일 쓸 물은 더 찾아야겠어.'),('소이','내일 가는 데도 물이 있어?'),('수혁','가까운 가게부터 찾아보자.')],next='prepare',scene='preparation',effects={'water_packed':True},cue='sfx_water_shift')
node('blanket','둘이 쓰는 담요','생존 준비 중에도 서로를 살피는 반응을 남긴다.',[('소이','밖에서 잘 거야?'),('수혁','차 안에서도 추울 수 있으니까.'),('소이','그럼 아빠 것도.'),('수혁','같이 쓰면 돼.'),('','접은 담요를 손이 닿기 쉬운 곳에 넣었다.')],next='prepare',scene='preparation',effects={'blanket_packed':True},cue='sfx_fabric_fold')
node('window','오늘은 쉬는 이유','한밤중에 바로 출발하지 않는 이유. 필수 진행에 잠그지 않는다.',[('','유리 너머로 길의 가장자리만 희미하게 보였다.'),('수혁','지금 나가면 표지판도 놓치겠어. 밝아지면 움직이자.')],next='prepare',scene='preparation',effects={'window_seen':True},camera=[1.04,.38,.3])
node('prepared','정리된 실내','두 가지 준비 완료를 한 번 확인한 뒤 소등. 물건 이동이 고장 원인이라고 단정하지 않는다.',[('수혁','이렇게 두면 아침엔 바로 움직일 수 있겠다.')],next='outage',scene='packed')
node('outage','예고 없이 꺼진 불','밝기가 한 번 약해진 뒤 소등. 반복 깜빡임·폭음 없음.',[('','실내등이 힘을 잃듯 어두워졌다.'),('소이','아빠?'),('수혁','여기 있어.')],next='reassure',scene='outage',effects={'outage_triggered':True},cue='sfx_light_click')
node('reassure','먼저 대답하기','수리를 시작하기 전에 소이의 부름을 받는다. 정답 없는 말투 선택.',scene='outage',choices=[choice('움직이지 마. 내가 불 켤게.','reassure_a',{'reassurance':'guide'}),choice('놀랐지? 잠깐만 기다려.','reassure_b',{'reassurance':'comfort'})])
node('reassure_a','목소리를 따라','짧게 공간의 위치를 확인한다.',[('수혁','움직이지 마. 내가 불 켤게.'),('소이','응. 여기 있을게.')],next='flashlight_find',scene='outage')
node('reassure_b','놀람을 받아 주기','어둠에 놀랐다는 말만으로 충분하다.',[('수혁','놀랐지? 잠깐만 기다려.'),('소이','갑자기 안 보여서.')],next='flashlight_find',scene='outage')
node('flashlight_find','작은 빛부터','어둠 속 픽셀 찾기를 요구하지 않고 기존 수납 위치를 안내한다.',[('수혁','손전등을 수납칸에 뒀었지.')],scene='outage',choices=[choice('수납칸에서 손전등 꺼내 켜기','flashlight_on')])
node('flashlight_on','손전등','찾기와 켜기를 한 행동으로 묶어 클릭 수를 억지로 늘리지 않는다.',[('수혁','있다.'),('소이','켜졌어?'),('수혁','응. 이제 안쪽을 볼 수 있겠어.')],next='inspect',scene='investigation',effects={'flashlight_on':True},cue='sfx_flashlight_click')
node('inspect','어디가 문제일까','스위치와 연결부의 관찰 결과를 모은다. 조사 순서는 자유.',scene='investigation',choices=[choice('실내등 스위치 확인','switch_check',unless=['switch_checked']),choice('조명 전원부 확인','panel_check',unless=['panel_checked'])])
node('switch_check','스위치 확인','누르기만 하면 해결되는 사건이 아님을 관찰한다.',[('수혁','스위치만 다시 눌러서는 안 켜지네.')],next='inspect',scene='investigation',effects={'switch_checked':True})
node('panel_check','헐거운 연결','관찰 결과만 말하고 실제 배선 수리 절차는 묘사하지 않는다.',[('수혁','이쪽 연결이 헐거워졌네.')],next='inspect',scene='investigation',effects={'panel_checked':True})
node('small_light','서연을 떠올릴 계기','손전등의 작은 빛이 평소 잠자리의 불로 이어져 갑작스러운 가족 설명을 피한다.',[('소이','그 작은 불은 켜 두면 안 돼?'),('수혁','등이 켜지면 이건 아껴 두자.'),('소이','엄마도 어두운 거 싫어했어?'),('수혁','잘 때도 작은 불은 켜 뒀어.'),('소이','무서워서?'),('수혁','네가 깨면 얼굴 보려고.'),('소이','그럼 아빠도 보였겠다.'),('수혁','응. 나도.')],next='secure',scene='investigation')
node('secure','연결을 확인하기','기존 정지 포즈와 고정 전후 상태를 사용. 미니게임·부품 소모 없음.',[('','실내등 전원을 끈 상태에서 연결을 확인했다.')],scene='investigation',choices=[choice('연결 고정하기','secured')])
node('secured','고정된 연결','연결 상태와 스위치 상태를 구분한다.',[('수혁','됐어. 이제 불을 켜 보자.')],scene='secured',effects={'connection_secured':True},choices=[choice('실내등 켜기','restored')])
node('restored','돌아온 공간','같은 방이 다시 보이는 것으로 사건의 보상을 준다.',[('소이','이제 보여.'),('수혁','뭐가?'),('소이','아빠 얼굴.'),('수혁','아까도 여기 있었어.'),('소이','알아.')],next='put_flashlight_back',scene='restored',effects={'light_restored':True},cue='sfx_light_click')
node('put_flashlight_back','제자리의 손전등','다음에 필요할 물건의 위치를 남기되 반복 수리 퀘스트를 만들지 않는다.',[('수혁','손전등은 같은 데 둘게.'),('소이','다음엔 바로 찾을 수 있겠다.'),('수혁','아침에 등도 한 번 더 봐야겠어.')],next='photo_care',scene='restored',effects={'flashlight_stored':True})
node('photo_care','사진을 제자리에','불을 고친 뒤 작은 생활 정리로 돌아온다. 수혁이 같은 사진의 테이프 모서리를 눌러 고정한다. 사진 위치·내용·얼굴 가림은 바뀌지 않는다.',scene='restored',choices=[choice('들뜬 테이프를 눌러 사진 고정하기','photo_wish',{'family_photo_secured':True})],camera=[1.08,.76,.27])
node('photo_wish','다음 여행도 사진으로','소이는 기존 사진처럼 앞으로 가는 곳도 사진으로 남기고 싶어 한다. 수혁도 이번 여행을 새 사진으로 남기고 싶다는 마음을 받아 둔다. 당장 촬영 수단을 지급하지 않고, 챕터 1에서 사진기사 세진의 카메라에 관심을 보일 이유로 남긴다. 실제 문장은 대사집 단계에서 작성한다.',scene='restored',effects={'wants_new_photos':True},choices=[choice('내일 준비 이어가기','book_pack')],camera=[1.08,.76,.27])
node('book_pack','아직 남아 있던 책','준비 전에 마지막에 챙기기로 한 약속을 되받는다.',[('소이','책도 꼭 가져가.'),('수혁','참, 마지막에 챙기기로 했지.'),('소이','맨 밑에는 넣지 마.'),('수혁','꺼내기 쉬운 데 둘게.')],scene='restored',choices=[choice('기존 스케치북 챙기기','journal_intro',{'book_packed':True})])
node('journal_intro','오늘의 기록','정리 행동을 짧은 회고와 다음 목표로 바꾼다.',[('','책을 넣고, 늘 쓰던 저널을 펼쳤다.'),('소이','뭐 써?'),('수혁','내일 잊어버리면 안 되는 거.'),('소이','나한테 물어보면 되는데.'),('수혁','그래도 써 두려고.')],next='journal_choice',scene='journal')
node('journal_choice','첫 문장 고르기','정답·엔딩 점수 없이 플레이어가 오늘의 중심을 고른다.',scene='journal',choices=[choice('소이가 다른 곳에 가고 싶다고 했다.','journal_done',{'journal_first_line':'소이가 다른 곳에 가고 싶다고 했다.'}),choice('내일은 이곳을 떠나기로 했다.','journal_done',{'journal_first_line':'내일은 이곳을 떠나기로 했다.'})])
node('journal_done','준비와 남은 일','물을 더 구할 필요를 확인하되 소이 부탁보다 생존 설명만 남기지 않는다.',[('','{journal_first_line}'),('','실내등 연결을 확인했다. 물과 담요, 스케치북을 챙겼다.'),('','내일은 물부터 더 구하자.')],next='morning_promise',scene='journal',effects={'journal_written':True})
node('morning_promise','같이 보는 아침','‘혼자 먼저 보지 말고’의 대상을 창밖 풍경으로 명확히 한다.',[('소이','내일은 창밖도 달라질까?'),('수혁','조금은 달라지겠지.'),('소이','아빠가 먼저 일어나면 깨워줘.'),('수혁','알았어.'),('소이','혼자 먼저 보지 말고.'),('수혁','같이 보자.')],next='sleep',scene='journal')
node('sleep','이번에는 스스로','같은 소등을 고장과 휴식으로 구분한다.',[('수혁','이제 불 끌게.'),('소이','응. 잘 자, 아빠.')],scene='journal',choices=[choice('불을 끄고 쉬기','night')])
node('night','밤의 끝','준비물과 고정한 연결을 유지한 채 따뜻한 어둠으로 끝낸다.',[('','아직, 같이 가고 싶은 곳이 있다.')],next='day1',scene='night',effects={'night_complete':True},cue='sfx_light_click')
node('day1','1일 차 아침','어제의 약속과 물 필요가 동시에 이어진다. 정리된 물·담요와 책 보유 상태를 유지한 기존 합성본을 사용하며, 아침 전용 조명 원화는 미제작이다.',[('','1일 차. 창문 가장자리로 빛이 들어왔다.'),('소이','오늘 가는 거지?'),('수혁','응. 먼저 물을 구하러 가자.')],scene='journal')
scene_files={'current':'01-current-conversation','packed':'02-preparation-complete','outage':'03-outage-answer-soi','investigation':'04-flashlight-investigation','secured':'05-connection-secured','restored':'06-light-restored','journal':'07-book-packed-journal-open','night':'08-voluntary-lights-off','water_only':'02a-water-only','blanket_only':'02b-blanket-only'}
nodes['photo_observe']['visual_note']='연출 초안 · 준비물을 정리하던 시선이 도입의 가족사진으로 잠깐 향한다. 사진을 붙인 테이프 끝이 들떠 있다. 지금은 살펴보고 준비를 계속한다. 모서리 확대 원화는 미제작.'
nodes['photo_care']['visual_note']='연출 초안 · 불이 돌아온 뒤 같은 사진의 모서리를 수혁이 눌러 고정한다. 회상이나 잡음을 반복하지 않는다. 손과 테이프 전후 원화는 미제작이며, 현재는 선택 후 상태만 기록한다.'
nodes['photo_care']['visual_note_if_seen']='연출 초안 · 준비 중 눈에 걸렸던 사진의 모서리를, 불이 돌아온 지금 정리한다. 수혁이 기존 테이프를 눌러 사진을 고정한다. 손과 테이프 전후 원화는 미제작.'
nodes['photo_care']['visual_note_condition']='photo_observed'
nodes['photo_wish']['visual_note']='서사 초안 · 소이가 사진을 보며 앞으로 가는 곳도 이렇게 남기고 싶다는 바람을 꺼낸다. 수혁도 그 마음을 받아 두지만 지금 바로 찍을 수는 없다. 촬영할 기회가 생기면 새 사진을 남기기로 마음에 둔다. 정확한 대사는 대사집에서 작성한다.'
nodes['photo_wish']['dialogue_intents']=[{'speaker':'소이','intent':'앞으로 보는 풍경도 기존 사진처럼 남기고 싶다는 구체적인 바람'},{'speaker':'수혁','intent':'바람에 공감하고 촬영할 방법이 생기면 시도하겠다는 뜻. 내일 당장 찍는다고 약속하지 않음'}]
nodes['night']['visual_note']='연출 초안 · 마지막 소등에도 같은 사진은 벽의 원래 자리에 남는다. 사진만 다시 확대하거나 얼굴을 드러내지 않고, 캠핑카 전체가 천천히 어두워진다.'
data={'id':'C0-continuation-v1','status':'new-dialogue-and-flow-review-draft','entry':'reply','canvas':[1920,1080], 'nodes':nodes,'scenes':{k:f'art/chapter00/camper/review/{v}.png' for k,v in scene_files.items()},'names_known':['수혁','소이','서연'],'timing':{'seconds_per_character':.055,'panel_fade_seconds':.2,'scene_fade_seconds':.35,'outage_fade_seconds':.55,'night_fade_seconds':.8},'journal_common':['실내등 연결을 확인했다. 물과 담요, 스케치북을 챙겼다.','내일은 물부터 더 구하자.'],'art_layout_source':'design/chapter00/camper-assets-v1.json','limits':['Preview scene composites reuse existing art; no new high-resolution paintings.','Provisional facial expressions reused; dedicated emotions pending final script.','Cue IDs specify future sounds. This continuation plays no audio.','Browser local save is a review bookmark, not Unity save data.','Not a measured 15-minute game.']}
data['photo_story_extension']={'status':'story-beat-draft-art-pending','item':'existing_family_photo','attachment':'existing tape edge, provisional set dressing; no inventory item or consumption','new_spoken_lines':0,'dialogue_revision':'deferred to dialogue script','source_photo':'art/chapter00/memory/dialogue-v1/family-sunset-source-v1.png','face_mask':'art/chapter00/memory/dialogue-v1/seoyeon-face-mask-v1.png','frame':'art/chapter00/memory/dialogue-v1/polaroid-frame-v1.png','wall_rect_logical':[1442,269,44,28.34],'requires_new_art':['tape corner lifted/pressed at same mounting point','Suhyeok hand pressing corner; no face in crop'],'no_new_letter_or_camera_unlock':True}
data['photo_story_extension']['chapter01_payoff']={'seed_flag':'wants_new_photos','npc':'세진','role':'사진기사 / 기존 기획의 사진작가','encounter':'첫 그림 이후 지역 밖 길을 알아보러 주유소에 들러 만남. 카메라를 보고 수혁이 촬영 방법에 관심을 보임.','continuation':'기존 공구 가방 부탁·전달 → 여분 카메라와 필름 → 쉼터 첫 촬영·앨범 확인','not_required_for_progress':'Seed is narrative continuity, not a hard prerequisite; older saves cannot lose the first photo tutorial.','no_early_unlock':['camera','film','album','mandatory photo quest']}
revision_path=R/'design/story/ch00-01-revision-v1/manifest.json'
if revision_path.exists():
    revision=json.loads(revision_path.read_text(encoding='utf-8'))
    for key,patch in revision['c0_patch'].items():
        nodes.setdefault(key,{}).update(patch)
    data['narrative_revision']={'id':revision['id'],'guide':revision['guide'],'scope':'storyboard-flow-not-Unity-runtime'}
(O/'story.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
md=['# 챕터 0 · 첫 응답부터 1일 차 아침까지 연결 콘티','', '2026-09-10 사용자 요청: 처음부터 이어 보며 개연성과 볼륨을 보강한다. **기존 회상·이름 공개·사진 지지직은 유지하고, 현재 대화 이후를 신규 검토 초안으로 연결했다.** 이 문서의 추가 대사는 사용자 최종 확정본이 아니다.','',
'## 처음부터 볼 때의 점검','',
'최신 보강: 대사 퇴고는 대사집 단계로 미룬다. 기존 대사 문장은 그대로 두고 **준비 중 사진 살펴보기(선택) → 복구 뒤 사진 고정 → 앞으로의 여행도 사진으로 남기고 싶은 바람 → 기존 책 챙기기**를 추가했다. 이 바람은 챕터 1 사진기사 세진과 카메라·필름·첫 촬영으로 이어진다. [사진 생활 사건 상세](CH00-PHOTO-CARE-STORY.ko.md). 사진 확대·손·테이프 전후 그림은 미제작이며 현재 재생은 기존 배경과 연출 메모로 흐름을 검토한다.','',
'| 구간 | 유지할 것 | 연결 보강과 확인 |','| --- | --- | --- |',
'| 시작·사진 진입 | 일기장 손 → 흐린 가족사진 → “수혁아.” | 사진을 보고 떠올린 기억이라는 계기 유지. 새 재난 설명·추가 가족사진을 넣지 않음 |',
'| 걷기·발 멈춤 | 발 아래 나레이션 → 같은 배경의 대화 → 신발 속 돌 | 서연이 멈춘 이유가 돌이라는 연결 유지. “조금만 더 걷자”는 복원하지 않음 |',
'| 손·사진 복귀 | 돌을 뺀 뒤 다시 손 잡을지 묻기 → 소이 대답 → 사진 | 세 사람이 손을 포개지 않음. 서연 초상 없음. 사진 속 얼굴만 불분명·짧은 잡음 |',
'| 현재 소이 | “아빠?” → 수혁 응답·서연 내면 → 내일도 여기 있는지 질문 | 이 질문 뒤 첫 선택으로 연결. 수혁·소이 이름은 이미 드러났으므로 물음표로 되돌리지 않음 |',
'| 출발 결정 | 목적지 미정인 여행 약속 | 매일 같은 창밖 → 다른 풍경을 빈 책에 그릴 수 있음 → 내일 출발. 밤이라 지금 떠나지 않는 선택 관찰 추가 |',
'| 준비·정전 | 물·담요 순서 자유, 두 물건 뒤 한 번 소등 | 차가 움직일 때 물건이 굴러다니지 않도록 정리. 책은 마지막이라는 말을 복구 뒤 회수. 정전의 원인을 물건 이동 탓으로 단정하지 않음 |',
'| 어둠 속 대화 | 작은 빛·서연에 관한 기존 짧은 대화 | 손전등을 켜 두자는 부탁에서 엄마의 작은 불 이야기로 이어짐. 새 회상 컷·서연 얼굴 없이 현재에 머묾 |',
'| 기록·취침·다음 날 | 저널 첫 문장 선택, 자발적 소등 | “같이 보자”의 대상은 달라질 창밖. 저널에는 남은 물 문제를 기록하고 아침 첫 외출 목적과 합류 |','',
'## 입력·연출·상태','',
'- [처음부터 검토](../design/ui/ch00-01/opening-motion.html) → 마지막 질문을 읽고 새 입력 → [후속 검토](../design/ui/ch00-01/chapter0-continuation.html). 두 페이지의 연결은 검토용이며 Unity의 연속 Scene 전환을 구현한 것은 아니다.',
'- 한 글자씩 표시, 출력 중 입력은 문장 완성만. 다음 새 클릭/Space로 다음 문장. 선택지에는 제한 시간·정답·엔딩 점수가 없다.',
'- 물/담요, 스위치/전원부는 각각 순서 자유다. 이미 확인한 필수 선택을 제거하고 두 조건 완료 시 합류한다. 선택 관찰인 창밖은 생략해도 정상 진행한다.',
'- 각 노드 마지막 문장을 확인하고 행동을 선택할 때 완료 효과를 기록한다. 저장·재개는 노드/문장/준비/조사/선택을 복원하는 로컬 검토 북마크이며 Unity 저장 구현과 구분한다.',
'- 텍스트는 프레임별 이미지에 굽지 않는다. 소이 물리 조작·제삼자의 목격·객관적 생존 증거를 추가하지 않는다. 저널은 수혁이 기록한 체험이다.',
'- 장면 전환 0.35초, 갑작스러운 소등은 0.55초 밝기 감소, 마지막 자발적 소등은 0.8초. 반복 점멸 없음. 어두운 장면의 대화 초상은 명도를 낮추며 표정은 기존 기본 원화라 후속 보강 대상.',
'- 소품 접지는 이번 연속 검토에서 같은 배경의 테이블·가림을 유지해 확인한다. 빈 책은 현재 식탁을 1.08배 보는 방식으로 연결하며, 배경 위에 큰 잘라낸 책을 띄우는 D1 패키지의 인서트 시안은 이 재생 경로에서 쓰지 않는다. 그 인서트 파일은 보존한다. 실제 소품 재작화·접촉 그림자 수정이 끝났다는 뜻은 아니다.',
'- 준비 뒤 정전과 마지막 소등은 같은 빛 감소라도 원인이 다르다. 복구/저널/밤 상태의 물·담요·책을 되돌리지 않는다. 1일 차는 책 보유를 이어받고 색연필만 새로 구한다.',
'- 실제 SFX·음성은 미제작이다. 아래 cue는 제작 지시 ID이며 이 후속 검토는 무음이다. 기존 도입의 잡음 시안과 구분한다.','',
'## 연결된 대사와 화면','']
for n in nodes.values():
    scene=n['scene'];path=data['scenes'].get(scene,'준비 플래그에 따라 current / water_only / blanket_only / packed')
    md += [f"### {n['id']} · {n['title']}",'',n['purpose'],'',f"원화: `{path}`. 카메라 `[배율, 중심X, 중심Y]`: `{n['camera']}`. 소리 cue: `{n['cue'] or '환경음 유지 / 별도 효과 없음'}`.",'']
    for line in n['lines']:md.append(f"- {line['speaker'] or '수혁 시점 나레이션'}: {line['text']}")
    if n.get('visual_note'):md+=['',n['visual_note']]
    if n['choices']:
        md += ['','선택·행동:']
        for c in n['choices']:md.append(f"- {c['label']} → `{c['target']}`"+(' · 완료 후 숨김: '+', '.join(c['unless']) if c['unless'] else ''))
    md += ['',f"완료 효과: `{json.dumps(n['effects'],ensure_ascii=False)}`. 다음: `{n['next'] or ('선택 후 이동' if n['choices'] else '1일 차 지도 구간으로')}`.",'']
md+=['## 볼륨 검토 기준','', '새 분량은 서로 다른 풍경에 대한 바람, 출발 전 정리, 어둠 속 작은 불, 마지막 책 챙기기, 다음 아침의 약속을 연결하는 데 쓴다. 같은 뜻의 관찰 문장을 반복하거나 물건 하나에 여러 확인 버튼을 붙이지 않는다. 15분 달성은 실측 전이며 보장하지 않는다. 검토판에서 활성 체류 시간·확인 문장·선택 기록을 내려받을 수 있다. 빠른 재생으로 통과한 시간은 첫 독자의 플레이타임으로 취급하지 않는다.','', '미제작/미확정: 표정별 인물, 확대 책의 받침면·접지 보강, 준비 소품의 미세 가림·그림자, 사운드·음성, 저널 최종 UI/PSD, Unity 입력·저장. 이번에 새 UI 디자인/PSD를 추가한 것으로 보고하지 않는다. 기존 대화창과 원화로 콘티 연결을 확인하는 단계다.']
(R/'docs/03-콘티/챕터0/CH00-CONTINUITY-AND-VOLUME.ko.md').write_text('\n'.join(md)+'\n',encoding='utf-8')
print(json.dumps({'nodes':len(nodes),'dialogue_lines':sum(len(n['lines']) for n in nodes.values()),'status':'draft'},ensure_ascii=False))
