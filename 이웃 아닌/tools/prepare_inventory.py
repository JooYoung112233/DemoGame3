from pathlib import Path
from PIL import Image
import numpy as np, shutil, json
from psd_layers import psd
R=Path(__file__).resolve().parents[1]; O=R/'아트/개별인벤토리-v1'; A=R/'Assets/Art/Inventory'
for p in [O/'개별-PNG',O/'PSD',A]:p.mkdir(parents=True,exist_ok=True)
src=Image.open(R/'아트/UI전체시안-v1/01-개별인벤토리.png').convert('RGBA')
boxes={'cloth':(135,610,215,681),'scrap':(255,612,339,680),'flashlight':(880,365,972,456),'bag':(760,252,810,307)}
for name,box in boxes.items():
 im=src.crop(box); rgb=np.asarray(im)[:,:,:3]; mask=rgb.mean(2)<170; seen=np.zeros(mask.shape,bool);parts=[]
 for y,x in zip(*np.where(mask)):
  if seen[y,x]:continue
  todo=[(y,x)];seen[y,x]=True;part=[]
  while todo:
   cy,cx=todo.pop();part.append((cy,cx))
   for ny,nx in [(cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)]:
    if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not seen[ny,nx]:seen[ny,nx]=True;todo.append((ny,nx))
  parts.append(part)
 mask[:]=False
 for y,x in max(parts,key=len):mask[y,x]=True
 im.putalpha(Image.fromarray(np.uint8(mask)*255));im=im.crop(im.getbbox());im.save(O/'개별-PNG'/f'{name}.png');shutil.copyfile(O/'개별-PNG'/f'{name}.png',A/f'{name}.png');psd(O/'PSD'/f'{name}.psd',[(name,im,(0,0),True)],im)
papers={n:R/f'Assets/Art/PartySelection/{n}.png' for n in ['card-paper','count-paper','footer-paper']}
for n,p in papers.items():shutil.copyfile(p,O/'개별-PNG'/f'{n}.png')
canvas=Image.new('RGBA',(3840,2160));layers=[]
def put(name,path,rect,tint=None):
 im=Image.open(path).convert('RGBA');x,y,w,h=rect;im=im.resize((w*2,h*2),Image.Resampling.LANCZOS)
 if tint:im=Image.fromarray(np.uint8(np.asarray(im).astype(float)*np.array(tint)))
 xy=(x*2,y*2);layers.append((name,im,xy,True));canvas.alpha_composite(im,xy)
dim=Image.new('RGBA',canvas.size,(0,0,0,158));layers.append(('background-dim-62-percent',dim,(0,0),True));canvas.alpha_composite(dim)
for name,rect in [('stock',(74,242,652,692)),('bag',(770,242,636,692)),('detail',(1416,242,428,692))]:put(name,papers['card-paper'],rect,(.11,.14,.15,.97))
for i in range(6):put('member-'+str(i),papers['card-paper'],(386+i*198,65,175,158))
for name,rect in [('clock',(80,65,235,102)),('location',(80,176,235,47)),('stock-title',(104,264,315,76)),('bag-title',(800,264,410,76)),('tool-title',(1444,264,372,76)),('notice',(776,952,1064,78))]:put(name,papers['count-paper'],rect)
for row in range(4):
 for col in range(4):
  # The fourth row is clipped by the Unity viewport. PSD layers retain its original pixels.
  put('stock-slot-%s-%s'%(row,col),papers['card-paper'],(104+col*147,432+row*132,137,120))
for row in range(3):
 for col in range(4):put('bag-slot-%s-%s'%(row,col),papers['card-paper'],(800+col*143,432+row*132,133,120))
for i in range(2):put('empty-tool-'+str(i),papers['card-paper'],(1444+i*194,432,178,142),(.32,.35,.34,.68))
put('detail-title',papers['count-paper'],(1444,637,372,58))
for name,rect in [('to-bag',(728,552,40,64)),('to-stock',(728,636,40,64)),('use',(1444,831,142,70)),('move',(1602,831,214,70)),('back',(80,952,410,78))]:put(name,papers['footer-paper'],rect)
put('back-icon',R/'Assets/Art/PartySelection/icon-left.png',(102,974,26,34))
psd(O/'PSD/inventory-layout.psd',layers,canvas)
(O/'README.md').write_text('''# 개인 가방·공용 창고

승인 기준: ../UI전체시안-v1/01-개별인벤토리.png (1672×941).
1920×1080 Unity UI, 3840×2160 레이어 배치 PSD. 패널·카드·슬롯·이동 버튼은 별도 레이어다. 텍스트·수량·실제 소유 데이터는 Unity UI에서 편집한다.
개별 PNG와 아이콘 PSD는 원본에서 분리한 실제 픽셀이다. 작은 시안 추출본이며 고해상도 최종 아이콘이 아니다. 확대가 새 디테일을 복원하지 않는다. 도구 배터리·사용 효과·장착은 후속 작업이며 현재 도구 칸은 비어 있음을 표시한다.
가방 용량과 인원은 실제 선택 데이터로 표시하며 시안 예시의 6명·12칸을 강제하지 않는다. 배경은 기존 정착지를 유지한다.
정렬 v4: 대원 리스트 위치는 유지한다. DAY 종이와 아래 패널은 텍스처의 투명 여백을 보정하여 보이는 왼쪽 테두리를 맞춘다. 가운데 가방 패널은 x=776·폭=624로 소폭 확장했다. 외곽 왼쪽 80px, 가운데 776px, 오른쪽 끝 1840px, 패널 안쪽 24px, 하단 버튼/안내 위쪽 952px을 공통 기준으로 삼는다. 세 패널의 제목/첫 아이템 줄 정렬, 좌상단 날짜/장소, 62% 배경 딤, 좌측 하단 410×78 돌아가기. PSD의 추가 목록 레이어는 편집용이고 실제 화면에서는 Unity 스크롤 마스크가 가린다.
''',encoding='utf-8')
(O/'manifest.json').write_text(json.dumps({'reference':'../UI전체시안-v1/01-개별인벤토리.png','nativeCrops':boxes,'psdSize':[3840,2160]},ensure_ascii=False,indent=2),encoding='utf-8')
print('Inventory source PNGs and layered PSD verified.')
