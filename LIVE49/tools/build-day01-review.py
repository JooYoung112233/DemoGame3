"""Day 1 storyboard resources. Native 2x UI; existing art used only in review composites."""
from pathlib import Path
from collections import deque
import ast, io, re, struct, json, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'design/ui/day01-v1'
ART=ROOT/'art/chapter01/day01-v1'
BOOK=ROOT/'art/chapter00/book-insert-v1'
for folder in [ART,BOOK,*[OUT/x for x in ['psd','layers','runtime','review']]]:folder.mkdir(parents=True,exist_ok=True)
qa=[]
tree=ast.parse((ROOT/'tools/build-ui-title-styles.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'check','packbits','channel_payload','unpackbits','psd'}],type_ignores=[]),'<PSD codec>','exec'))
FONT=ROOT/'art/title/fonts/Live49MenuSerif-Regular.ttf'
CREAM='#f2e4c4';MUTED='#c7b695';GOLD='#cc9e67'
W,H=3840,2160
def rel(p):return p.relative_to(ROOT).as_posix()
def dump(p,obj):p.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

# Preserve the image-generation output at its native dimensions and alpha.
notice=ART/'evacuation-notice-paper-source.png'
if not notice.exists():shutil.copyfile('C:/Users/admin/.codex/generated_images/01a084ef-08a3-7492-b066-9506a2da3dbc/exec-b7b6fc8d-6dfd-4dc1-bde0-3ea622f26403.png',notice)
paper_source=Image.open(notice).convert('RGBA')
check('notice-native-alpha',paper_source.getchannel('A').getextrema()==(0,255))
# Native output has colored matte fringe. Trim alpha only; retain untouched source.
pa=np.array(paper_source);edge=np.array(paper_source.getchannel('A').filter(ImageFilter.MinFilter(21)))<255
neon=((pa[:,:,0]>190)&(pa[:,:,1]>155)&(pa[:,:,2]<75))|((pa[:,:,0]>210)&(pa[:,:,1]<130)&(pa[:,:,2]<150))
pa[:,:,3]=np.array(paper_source.getchannel('A').filter(ImageFilter.MinFilter(7)))
pa[:,:,3][edge&neon]=0;paper=Image.fromarray(pa)
paper.save(ART/'evacuation-notice-paper-clean.png')
check('notice-rgb-preserved',np.array_equal(pa[:,:,:3],np.array(paper_source)[:,:,:3]))

# User authorized cutting existing art. Only alpha changes; RGB remains untouched.
source=Image.open(ROOT/'art/chapter01/layers/sketchbook-open-blank-v1.png').convert('RGBA')
a=np.array(source);rgb=a[:,:,:3].astype('int16');neutral=(rgb.max(2)-rgb.min(2)<23)&(rgb.mean(2)>75)
seen=np.zeros(neutral.shape,bool);q=deque();h,w=seen.shape
def push(y,x):
    if neutral[y,x] and not seen[y,x]:seen[y,x]=True;q.append((y,x))
for x in range(w):push(0,x);push(h-1,x)
for y in range(h):push(y,0);push(y,w-1)
while q:
    y,x=q.popleft()
    for yy,xx in [(y-1,x),(y+1,x),(y,x-1),(y,x+1)]:
        if 0<=yy<h and 0<=xx<w:push(yy,xx)
a[:,:,3]=np.where(seen,0,255);book=Image.fromarray(a);book.putalpha(book.getchannel('A').filter(ImageFilter.MinFilter(3)))
book.save(BOOK/'sketchbook-blank-native.png')
psd(BOOK/'sketchbook-blank-native.psd',[('original_hidden',source,(0,0),False),('book_cutout',book,(0,0),True)],book)
check('book-preserved-rgb',np.array_equal(np.asarray(book)[:,:,:3],np.asarray(source)[:,:,:3]))
check('book-border-transparent',np.array(book.getchannel('A'))[0].max()==0)
dump(BOOK/'manifest.json',{'source':'art/chapter01/layers/sketchbook-open-blank-v1.png','native_size':list(book.size),'alpha_bounds':book.getchannel('A').getbbox(),'sprite':rel(BOOK/'sketchbook-blank-native.png'),'psd':rel(BOOK/'sketchbook-blank-native.psd'),'scope':'Original hidden + alpha-only cutout. Pages are not separate. No new detail from scaling.','max_review_width':1300})

manifest={'id':'day01-v1','status':'storyboard-review-draft','canvas':[1920,1080],'ui_native_canvas':[W,H],'font':rel(FONT),'screens':[],'limitations':['Clickable storyboard, not a Unity game or inventory simulation.','Time/probability/loot counts and dialogue are review drafts.','PSD has actual separate raster layers; text is not Photoshop live text.','Backgrounds remain native 1672x941; preview enlargement adds no detail.','Paper native 1086x1448, book native 1536x1024; not 2x painting masters.','Map is a schematic UI, not approved geographical lore.','Audio cues are specified in storyboard; final audio files not supplied.']}
strings={};layers=[];entries=[];index=0
def add(name,im,xy,kind='image',text=None):
    global index
    index+=1;name=f'{index:02d}_{name}';xy=tuple(round(v*2) for v in xy)
    check(sid+':bounds:'+name,xy[0]>=0 and xy[1]>=0 and xy[0]+im.width<=W and xy[1]+im.height<=H)
    layers.append((name,im,xy,True));dest=OUT/'layers'/sid;dest.mkdir(exist_ok=True)
    im.save(dest/(name+'.png'))
    entry={'name':name,'path':rel(dest/(name+'.png')),'rect':[xy[0]//2,xy[1]//2,im.width/2,im.height/2],'kind':kind}
    if text is not None:strings[sid+'.'+name]=text;entry['string_id']=sid+'.'+name
    entries.append(entry)
def rect(name,box,fill=(35,30,24,237),outline=(190,155,104,135),radius=8):
    x,y,w,h=box;im=Image.new('RGBA',(w*2,h*2));d=ImageDraw.Draw(im)
    d.rounded_rectangle((1,1,w*2-2,h*2-2),radius=radius*2,fill=fill,outline=outline,width=2)
    add(name,im,(x,y))
def text(value,x,y,size=30,color=CREAM,name='text'):
    font=ImageFont.truetype(str(FONT),size*2);box=font.getbbox(value)
    im=Image.new('RGBA',(box[2]-box[0]+16,box[3]-box[1]+16));ImageDraw.Draw(im).text((8-box[0],8-box[1]),value,font=font,fill=color)
    add(name,im,(x-4,y-4),'runtime-text',value)
def button(label,box,focus=False):
    rect('button',box,(79,59,39,244) if focus else (39,33,26,245), (218,182,124,230) if focus else (167,139,98,150))
    text(label,box[0]+25,box[1]+22,28)
def erasure(name,box):
    x,y,w,h=box;im=Image.new('RGBA',(w*2,h*2));d=ImageDraw.Draw(im);rng=np.random.default_rng(w)
    for k in range(26):
        yy=8+k*(h*2-16)/26
        d.line([(int(rng.integers(0,18)),int(yy)),(w*2-int(rng.integers(0,23))-1,int(yy+rng.integers(-5,6)))],fill=(129,117,87,int(rng.integers(45,100))),width=int(rng.integers(2,6)))
    add(name,im,(x,y))
def title(label,subtitle):
    text(label,94,82,42);text(subtitle,96,148,24,MUTED)
def dialogue(name,line):
    rect('dialogue_panel',(60,790,1800,240));text(name,110,814,25,GOLD);text(line,140,885,36);text('다음  ›',1710,978,22,MUTED)
def sprite(path,box,name):
    im=Image.open(ROOT/path).convert('RGBA');scale=min(box[2]*2/im.width,box[3]*2/im.height)
    im=im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.LANCZOS)
    add(name,im,(box[0]+(box[2]-im.width/2)/2,box[1]+(box[3]-im.height/2)/2),'art-reference')
def start(ident,label,bg='camper',blur=True):
    global sid,layers,entries,index,background,screen
    sid=ident;layers=[];entries=[];index=0
    path='art/chapter00/camper/review/01-current-conversation.png' if bg=='camper' else 'art/chapter01/revision-v3/store-clean-base-v3.png'
    background=Image.open(ROOT/path).convert('RGBA').resize((1920,1080),Image.Resampling.LANCZOS)
    if blur:background=background.filter(ImageFilter.GaussianBlur(5))
    background=Image.alpha_composite(background,Image.new('RGBA',background.size,(16,18,16,72 if blur else 20)))
    screen={'id':sid,'label':label,'background':path,'blur_logical':5 if blur else 0,'review_only_background':True}
def finish(next_ids,note=''):
    # Art inserts stay native in separate art PSDs. UI-only PSD excludes art references.
    ui=Image.new('RGBA',(W,H));full=Image.new('RGBA',(W,H));ui_layers=[]
    for layer,e in zip(layers,entries):
        name,im,xy,v=layer;full.alpha_composite(im,xy)
        if e['kind']!='art-reference':ui.alpha_composite(im,xy);ui_layers.append(layer)
    psd(OUT/'psd'/f'{sid}.psd',ui_layers,ui)
    ui.resize((1920,1080),Image.Resampling.LANCZOS).save(OUT/'runtime'/f'{sid}-review-text.png')
    runtime=Image.new('RGBA',(W,H))
    for layer,e in zip(layers,entries):
        if e['kind']=='image':runtime.alpha_composite(layer[1],layer[2])
    runtime.resize((1920,1080),Image.Resampling.LANCZOS).save(OUT/'runtime'/f'{sid}-surfaces.png')
    frame=Image.alpha_composite(background,full.resize((1920,1080),Image.Resampling.LANCZOS))
    frame.convert('RGB').save(OUT/'review'/f'{sid}.jpg',quality=94)
    screen.update(layers=entries,psd=rel(OUT/'psd'/f'{sid}.psd'),preview=rel(OUT/'review'/f'{sid}.jpg'),next=next_ids,review_note=note)
    manifest['screens'].append(screen);print(sid,len(layers),'layers',flush=True)

start('C0-03c-choice','첫 응답 선택')
title('내일도 여기 있어?','소이의 질문 뒤, 수혁의 응답을 고른다')
button('다른 데 가 보고 싶어?',(660,395,600,88),True);button('어디로 가고 싶은데?',(660,510,600,88))
dialogue('소이','아빠, 우리 내일도 여기 있어?')
finish(['C0-03c-book'],'두 선택 모두 빈 페이지로 합류. 이 검토판에서는 분기 대사를 생략하며 상세 콘티에 보관한다.')
start('C0-03c-book','이미 펼쳐 둔 책')
sprite(rel(BOOK/'sketchbook-blank-native.png'),(310,40,1300,750),'native_book_reference')
dialogue('수혁','여기는 비워 뒀네.')
finish(['D1-01-need'],'소이: 응. 나중에 그릴 거야. 이후 출발 약속과 C0 준비·정전·취침을 거쳐 다음 날. 이 검토판은 그 사이를 건너뛴다.')
start('D1-01-need','1일 차 · 물을 구할 이유')
title('1일 차 · 아침','오늘 필요한 것부터')
dialogue('수혁','남은 물은 챙겨 뒀지만, 오늘 쓸 만큼은 더 있어야겠어.')
finish(['D1-02-map'],'비상분은 남아 있다. 어젯밤 챙긴 물을 없었던 것으로 만들지 않는다. 다음 대사: 지도에 편의점이 있었지.')
start('D1-02-map','지도 · 편의점 선택')
rect('map_paper',(180,210,1000,650),(210,192,151,249),(117,96,62,180))
im=Image.new('RGBA',(1840,1040));d=ImageDraw.Draw(im)
d.line([(100,900),(430,630),(1060,630),(1350,190),(1740,130)],fill=(118,106,79,160),width=30)
d.line([(1050,80),(1050,630),(1480,950)],fill=(145,128,92,110),width=16)
add('map_roads',im,(220,250))
rect('current_marker',(340,605,28,28),(75,92,75,255),None,14)
rect('store_marker',(885,365,32,32),(123,64,38,255),None,16)
text('현재 위치',285,660,27,'#4f4736');text('편의점',855,310,30,'#4f4736')
title('주변 지도','물을 구할 수 있을 만한 곳')
rect('destination_card',(1230,270,530,490))
text('편의점',1270,320,40);text('캠핑카 이동  20분',1270,405,30,GOLD)
text('문 앞에 주차할 수 있다.',1270,472,25);text('남은 물자를 확인해 보자.',1270,516,25,MUTED)
button('이곳으로 출발',(1270,630,450,86),True)
finish(['D1-03-arrival'],'실제 지명이 없는 개략 지도. 20분은 검토용. 출발 전 수혁: 편의점부터 가 보자. 차는 가게 앞에 세울게.')
start('D1-03-arrival','소리와 암전으로 이동')
background=Image.new('RGBA',(1920,1080),'#080a09')
text('엔진이 잦아들고, 주차 브레이크가 걸린다.',570,496,28,MUTED)
text('편의점 앞에 도착했다.',745,564,24,'#8f8775')
finish(['D1-04-store'],'위 문장은 소리 접근성 자막 시안. 최종 SFX 미납품. 검토판은 실제 음향을 재생하지 않는다. 화면 이동에 20분 반영, 복귀에는 다시 부과하지 않는다.')
start('D1-04-store','편의점 · 선택 가능한 탐색 지점','store',False)
rect('objective',(60,40,630,110));text('물과 먹을 것을 찾아보자',90,76,30)
rect('parking',(1280,40,580,88));text('캠핑카는 편의점 바로 앞',1310,67,25,MUTED)
# A single focused target; other colliders are invisible at runtime.
rect('fridge_focus',(876,147,219,234),(226,192,128,22),(242,218,161,225),3)
rect('focus_label',(880,398,210,60));text('냉장고',930,415,24)
finish(['D1-05-search','D1-08-note','D1-09-return'],'냉장고·식품 선반·생활용품 상자·문구대·계산대 쪽지·출입문. 이 화면은 냉장고에만 포커스한 예시. 충돌 영역은 flow.json에 별도 기록.')
start('D1-05-search','냉장고 · 획득 확률과 시간','store')
rect('search_panel',(350,180,1220,755));title('냉장고','한 번의 탐색으로 확인할 수 있는 물자')
text('물자',410,250,27,MUTED);text('빠른 탐색',995,250,27,MUTED);text('미니게임 성공 시',1230,250,27,MUTED)
for y,label,base,boost in [(338,'생수 2개 · 눈에 보이는 확보분','100%','100%'),(435,'추가 생수 1개','45%','65%'),(532,'밀봉 음료 1개','30%','50%')]:
    text(label,410,y,30);text(base,1050,y,32,GOLD);text(boost,1320,y,32,GOLD)
text('실패해도 기본 확률 유지 · 성공 보너스 +20%p',410,644,26,MUTED)
button('빠른 탐색 · 5분',(410,745,530,92));button('집중 탐색 · 총 15분',(970,745,530,92),True)
finish(['D1-07-result','D1-06-minigame'],'모든 숫자는 조정용. 품목별 독립 판정, 표시 수량은 상한. 집중 탐색 15분은 미니게임 포함 총시간.')
start('D1-06-minigame','집중 탐색 · 확률 상승 시안','store')
rect('minigame_panel',(320,280,1280,560));title('조심스럽게 꺼내기','흔들리는 선반 사이, 타이밍을 기다린다')
rect('track',(465,475,990,46),(90,79,56,230),None)
rect('success_zone',(865,475,180,46),(126,155,116,220),None,0)
rect('cursor',(951,450,7,95),(248,232,192,255),None,0)
text('표시가 구간 안에 들어오면 멈추기',645,583,32)
text('성공 +20%p  /  실패 기본 확률  /  소음 여부는 별도 판정',495,656,26,MUTED)
button('성공 결과 보기',(710,741,500,76),True)
finish(['D1-07-result'],'미니게임 화면 구도만 만든다. 실제 움직임·난이도·판정 규칙은 추후 상세 기획. 성공 사례를 보는 버튼이며 플레이 가능한 미니게임이 아니다.')
start('D1-07-result','탐색 결과','store')
rect('result_panel',(535,235,850,600));title('챙긴 물자','냉장고 탐색을 마쳤다')
text('생수',610,344,36);text('3개',1170,344,36,GOLD)
text('밀봉 음료',610,426,36);text('0개',1170,426,36,MUTED)
text('남은 재고를 다시 판정하지 않는다.',610,555,25,MUTED)
button('가방에 넣고 돌아가기',(610,674,690,90),True)
finish(['D1-04-store'],'정해진 예시 결과: 확정 2 + 추가 생수 1. 실제 RNG·가방·게임 시간 구현은 Unity 단계. 다른 탐색 지점은 같은 패널을 재사용한다.')
start('D1-08-note','계산대 · 대피 안내 쪽지','store')
sprite(rel(ART/'evacuation-notice-paper-clean.png'),(190,40,690,920),'notice_paper_reference')
text('대피 안내',360,214,40,'#534935')
text('감염자',327,325,30,'#665a43')
# Intentionally no recoverable hidden lore: erased bands contain no underlying words.
erasure('erasure_a',(465,316,245,44))
erasure('erasure_b',(326,389,376,40))
text('대피구역: 주민센터',327,503,28,'#534935')
text('안내를 따라 이동하십시오.',327,559,23,'#665a43')
rect('note_caption',(1050,280,670,390));text('누군가 남긴 안내',1100,336,35)
text('앞부분은 지워져 있다.',1100,428,28,MUTED)
text('날짜도 확인할 수 없다.',1100,478,28,MUTED)
dialogue('수혁','주민센터라… 아직 사람들이 있을까.')
finish(['D1-04-store'],'주민센터는 지명 검토용 가칭. 감염자에 관한 나머지 문장은 작성하지 않음. 읽은 위치 정보만 기록, 안전을 보장하지 않는다.')
start('D1-09-return','바로 앞 캠핑카로 복귀')
dialogue('수혁 · 독백','여기가 안전할까. 다른 곳으로 옮겨야 하나.')
finish(['D1-10-ration'],'문 클릭 → 0.25초 암전·문소리 → 같은 장소 캠핑카. 물/식량 부족 시 추가 탐색 안내, 종료/아이템 폐기 없음. 쪽지를 못 읽으면 주민센터 언급을 추가하지 않는다.')
start('D1-10-ration','물과 식량 · 분배 튜토리얼')
rect('ration_panel',(300,170,1320,760));title('오늘 낮에 먹을 것','가져온 것 중 지금 나눌 몫을 정한다')
text('가방에 있음',375,264,26,MUTED);text('생수 3개   ·   포장 식량 2개',650,264,30,GOLD)
for x,name in [(400,'수혁'),(1020,'소이')]:
    rect('person_card',(x,350,500,290),(51,43,31,245));text(name,x+40,388,38)
    text('생수        −    1    +',x+40,473,29);text('포장 식량   −    1    +',x+40,544,29)
text('남겨 둘 몫   생수 1개 · 포장 식량 0개',420,691,27,MUTED)
button('이렇게 나누기',(615,778,690,88),True)
finish(['D1-11-kitchen'],'가방 수량은 예시. 첫 안내는 두 자리 모두 물·식량 1씩 선택 후 확인. 최종 확정 시 한 번만 차감. 실제 영양/소비 단위와 자원 밸런스는 미확정.')
start('D1-11-kitchen','요리대 · 수리 필요 암시',blur=False)
rect('kitchen_focus',(525,326,430,123),(220,180,120,15),(224,185,129,200))
rect('repair_hint',(1100,225,700,280));text('요리대 · 수리 필요',1140,270,34)
text('쓸 만한 부품을 모으면',1140,351,28,MUTED);text('따뜻한 음식도 만들 수 있겠다.',1140,400,28,MUTED)
dialogue('수혁','오늘은 이걸로 먹자. 요리대는 손을 좀 봐야겠어.')
finish(['D1-12-soi'],'아직 수리 버튼·레시피·요리 미니게임을 열지 않는다. 재료와 수리 조건은 후속 기획. 물건을 두는 행동은 수혁 기준으로 연출.')
start('D1-12-soi','소이의 부탁 · 이벤트 알림',blur=False)
rect('soi_event',(1120,302,54,66),(40,35,27,230),(214,175,115,230));text('!',1138,313,36,GOLD)
finish(['D1-13-quest'],'실제 캐릭터 머리 위치에 앵커링. 소이를 클릭하면 기존 VN 구도: 아빠, 나 그림 그리고 싶어. → 어제 챙긴 책에 그릴까? → 응. 색연필도 있으면 좋겠어.')
start('D1-13-quest','1일 차 낮 마무리 · 색연필만 찾기')
rect('quest_card',(1040,200,760,420));text('소이의 작은 부탁',1090,253,38)
text('색연필 구하기',1090,343,40,GOLD);text('스케치북 · 이미 챙김',1090,424,27,MUTED)
text('책은 어제 챙겨 둔 것을 쓴다.',1090,486,27,MUTED)
dialogue('수혁','알았어. 색연필을 찾아볼게.')
finish([],'이 지점이 1일 차 낮 콘티의 끝. 이미 색연필이 있으면 찾기 대신 건네기 상태. 새 스케치북 획득 목표 없음. 그림 튜토리얼은 후속 구간.')

flow=json.loads((ROOT/'design/chapter01/day01-flow-v1.json').read_text(encoding='utf-8'))
for node in flow['search_nodes'][1:]:
    key=node['id'];start('D1-05-'+key,node['label']+' · 탐색 목록','store')
    rect('search_panel',(350,180,1220,755));title(node['label'],'한 번의 탐색으로 확인할 수 있는 물자')
    text('물자',410,250,27,MUTED);text('빠른 탐색',995,250,27,MUTED);text('미니게임 성공 시',1230,250,27,MUTED)
    for i,row in enumerate(node['loot']):
        y=338+i*97;text(row['label']+' '+str(row['quantity'])+'개',410,y,28);text(str(row['chance'])+'%',1050,y,32,GOLD);text(str(min(100,row['chance']+20))+'%',1320,y,32,GOLD)
    text('실패해도 기본 확률 유지 · 성공 보너스 +20%p',410,644,26,MUTED)
    button('빠른 탐색 · 5분',(410,745,530,92));button('집중 탐색 · 총 15분',(970,745,530,92),True)
    finish(['D1-07-'+key,'D1-06-minigame'],'품목·확률·수량은 지점별 검토안. 반복 재추첨 없음. '+('색연필 선확보를 인정하며 실패하면 후속 안내 경로를 둔다.' if key=='stationery' else ''))
    start('D1-07-'+key,node['label']+' · 결과 예시','store')
    rect('result_panel',(535,210,850,650));title('챙긴 물자',node['label']+' 탐색 결과 예시')
    counts={'food_shelf':[2,0],'supply_box':[1,1,0],'stationery':[0,1]}[key]
    for i,(row,count) in enumerate(zip(node['loot'],counts)):
        text(row['label'].split(' · ')[0],610,330+i*87,30);text(str(count)+'개',1190,330+i*87,32,GOLD if count else MUTED)
    text('이 지점의 탐색을 마쳤다.',610,626,25,MUTED)
    button('가방에 넣고 돌아가기',(610,718,690,90),True)
    finish(['D1-04-store'],'고정 결과 예시. 실제 난수 판정·인벤토리 없음.')

note_layers=[('source_hidden',paper_source,(0,0),False),('native_paper_clean',paper,(0,0),True)];note_merged=paper.copy()
for e in next(s for s in manifest['screens'] if s['id']=='D1-08-note')['layers']:
    x,y,w,h=e['rect']
    if e['kind']=='art-reference' or x<190 or x+w>880 or y+h>780:continue
    im=Image.open(ROOT/e['path']).convert('RGBA');scale=paper.width/690
    im=im.resize((round(w*scale),round(h*scale)),Image.Resampling.LANCZOS);xy=(round((x-190)*scale),round((y-40)*scale))
    note_layers.append((e['name'],im,xy,True));note_merged.alpha_composite(im,xy)
psd(ART/'evacuation-notice-native.psd',note_layers,note_merged)
note_merged.save(ART/'evacuation-notice-lettering-review.png')
dump(ART/'manifest.json',{'source':rel(notice),'clean_sprite':rel(ART/'evacuation-notice-paper-clean.png'),'native_size':list(paper.size),'psd':rel(ART/'evacuation-notice-native.psd'),'layers':[n for n,_,_,_ in note_layers],'contains_live_text':False,'paper_rgb_unchanged':True,'alpha_cleanup':'3px alpha erosion plus neon matte removal within edge only. Source hidden layer untouched.','note_text_status':'Provisional destination name; no authored text under erased regions','validation':'design/ui/day01-v1/validation.json'})
dump(OUT/'manifest.json',manifest);dump(OUT/'strings.ko.json',strings)
dump(OUT/'validation.json',{'checks':qa,'passed':all(c['pass'] for c in qa),'screens':len(manifest['screens']),'photoshop_app_tested':False,'unity_tested':False})
contact=Image.new('RGB',(1440,810),'#1b1a16')
for i,key in enumerate(['D1-02-map','D1-04-store','D1-05-search','D1-08-note','D1-10-ration','D1-13-quest']):
    im=Image.open(OUT/'review'/f'{key}.jpg').resize((480,270),Image.Resampling.LANCZOS);contact.paste(im,((i%3)*480,(i//3)*405))
    ImageDraw.Draw(contact).text(((i%3)*480+20,(i//3)*405+290),next(s['label'] for s in manifest['screens'] if s['id']==key),font=ImageFont.truetype(str(FONT),23),fill=CREAM)
contact.save(OUT/'review/contact-sheet.jpg',quality=94)
print('Finished',len(manifest['screens']),'screens;',len(qa),'checks')
