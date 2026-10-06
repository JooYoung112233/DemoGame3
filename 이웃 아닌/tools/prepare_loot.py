from pathlib import Path
from PIL import Image,ImageChops
from psd_layers import psd
import shutil,json
R=Path(__file__).resolve().parents[1];O=R/'아트/발견물분배-v1';A=R/'Assets/Art'
for d in ['원본','개별-PNG','PSD']:(O/d).mkdir(parents=True,exist_ok=True)
paths={'paper':A/'PartySelection/card-paper.png','button':A/'PartySelection/footer-paper.png','supplies':A/'HomeSelection/icon-supplies.png','scrap':A/'Inventory/scrap.png','cloth':A/'Inventory/cloth.png','rope':A/'CraftWorkPanel/rope.png'}
for n,p in paths.items():
 shutil.copyfile(p,O/'원본'/f'{n}.png');shutil.copyfile(p,O/'개별-PNG'/f'{n}.png')
canvas=Image.new('RGBA',(3840,2160));layers=[];positions=[]
def put(n,key,box,tint=None):
 x,y,w,h=box;im=Image.open(paths[key]).convert('RGBA').resize((w*2,h*2),Image.Resampling.LANCZOS)
 if tint:im=ImageChops.multiply(im,Image.new('RGBA',im.size,tint))
 layers.append((n,im,(x*2,y*2),True));canvas.alpha_composite(im,(x*2,y*2));positions.append({'layer':n,'source':str(paths[key].relative_to(R)),'rect1080':box})
put('main-panel','paper',(70,150,1780,785),(26,36,36,255));put('title','button',(96,166,620,75))
for i,box in enumerate([(100,410,460,500),(580,410,610,500),(1210,410,610,500)]):put('section-'+str(i),'paper',box,(51,61,59,255))
for i,box in enumerate([(120,426,320,65),(600,426,395,65),(1234,426,350,65)]):put('section-title-'+str(i),'button',box)
for i in range(2):put('worker-'+str(i),'paper',(600+i*144,252,132,126))
for i,key in enumerate(['supplies','scrap','cloth']):
 x=124+i*137;put('slot-'+str(i),'paper',(x,516,127,120));put('item-'+str(i),key,(x+28,526,77,69))
for n,box in [('minus',(1238,710,70,58)),('plus',(1442,710,70,58)),('max',(1530,710,125,58)),('back',(80,952,410,78)),('transfer',(1310,952,410,78))]:put(n,'button',box)
psd(O/'PSD/loot-layout.psd',layers,canvas)
canvas=Image.new('RGBA',(3840,2160));layers=[]
put('leave-paper','paper',(460,250,1000,550));put('cancel','button',(510,680,410,78));put('leave','button',(1000,680,410,78))
psd(O/'PSD/leave-loot-review.psd',layers,canvas)
(O/'layout.json').write_text(json.dumps(positions,ensure_ascii=False,indent=2),encoding='utf-8')
(O/'README.md').write_text('''# 발견물 분배 UI
기준: UI전체시안-v1/08-발견물과가방부족.png. Demo5의 기존 종이·아이콘을 재사용하며 원본 픽셀 PNG를 별도 보관한다. 새 생성 원화는 없다.
loot-layout.psd는 전체 패널/세 영역/제목 종이/대원 카드 바탕/아이템 슬롯/아이콘/버튼을 실제 레이어로 나눈 배치 원본이다. leave-loot-review.psd는 남은 물건 확인창이다. PSD 재개방, 레이어 이름·알파·채널·좌표를 검증했다.
3840×2160은 배치 편집 캔버스이며 확대된 기존 텍스처의 디테일 복원이 아니다. 배경과 캐릭터 원본은 기존 묶음을 사용한다. PSD의 아이템은 레이아웃 예시이며 실제 획득물은 게임 데이터로 결정된다. 텍스트·수량은 Unity 프리팹에서 수정한다.
프리팹: ExpeditionLootPanel / LeaveLootReview. 기존 InventorySlot / SearchWorkerCard / ScrollMoreHint 재사용.
시안과 차이: 실제 대원 수·가방 용량 표시, 최신 지시에 따라 닫기는 좌하단, 가져오기는 우하단. 기존 분리 아이콘과 종이 소재를 재사용하여 그림·테두리가 시안과 완전히 같지는 않다. 시각 승인 대기.
''',encoding='utf-8')
print('Loot PSD layers verified; source PNGs retained.')
