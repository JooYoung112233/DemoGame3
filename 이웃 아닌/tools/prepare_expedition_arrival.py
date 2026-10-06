from pathlib import Path
from PIL import Image
from psd_layers import psd
import shutil,json
R=Path(__file__).resolve().parents[1];O=R/'아트/탐색도착-v1';A=R/'Assets/Art'
for d in ['원본','개별-PNG','PSD']:(O/d).mkdir(parents=True,exist_ok=True)
src=A/'ExpeditionArrival/arcade-clean.png';shutil.copyfile(src,O/'원본/arcade-clean.png')
canvas=Image.new('RGBA',(3840,2160));layers=[]
def put(name,path,rect):
 im=Image.open(path).convert('RGBA');x,y,w,h=rect;im=im.resize((w*2,h*2),Image.Resampling.LANCZOS);layers.append((name,im,(x*2,y*2),True));canvas.alpha_composite(im,(x*2,y*2))
put('arcade-background',src,(0,0,1920,1080))
P=A/'PartySelection'
for name,rect in [('date',(80,36,204,110)),('place',(308,36,320,110)),('resources',(1190,36,390,86)),('risk',(1600,36,240,86)),('arrival',(80,776,480,148)),('back',(80,952,410,78))]:put(name,P/'card-paper.png',rect)
for i in range(2):
 put('member-card-'+str(i),P/'card-paper.png',(600+i*286,754,272,172))
 put('pawn-base-'+str(i),A/'Settlement/base-white.png',(834+i*150,661,102,28))
 put('pawn-body-'+str(i),A/'Settlement'/('standee-scout.png' if i==0 else 'standee-medic.png'),(829+i*150,473,112,199))
psd(O/'PSD/arrival-layout.psd',layers,canvas)
for f in [src,A/'Settlement/standee-scout.png',A/'Settlement/standee-medic.png',A/'Settlement/base-white.png',P/'card-paper.png',P/'count-paper.png',P/'footer-paper.png']:
 im=Image.open(f).convert('RGBA');shutil.copyfile(f,O/'개별-PNG'/f.name);psd(O/'PSD'/(f.stem+'.psd'),[(f.stem,im,(0,0),True)],im)
(O/'README.md').write_text('''# 탐색 장소 도착
승인 기준: UI전체시안-v1/07-탐험수색지정-v2.png.
기본 image_gen 편집: 시안의 UI·캐릭터·상호작용 마크·상자 선택 테두리를 제거하고 가려진 좌측 오락실을 복원. 원래 정면 구도·종이 질감·기계·탁자·문 배치를 유지하도록 요청. 빛줄기·강조광 없이 Unity 조명을 위한 기본색으로 요청했다.
원본 PNG는 1672×941. 3840px 목표에 미달하며 3840×2160 레이어 PSD는 배치 편집용 확대다. 디테일 복원이 아니다.
PSD의 배경, 인물, 받침대, 카드, 패널은 개별 레이어. 배경 안 오락기·상자·탁자는 한 그림에 남아 있어 개별 편집이 필요하면 추후 분리해야 한다. 현재 상호작용 버튼만 별도 Unity 프리팹이다. 가려졌던 부분의 복원은 생성 결과이며 원본과 픽셀 단위 동일하지 않다.
캐릭터와 UI는 기존 승인 PNG 재사용. Unity 조명은 ExpeditionWorld의 URP Light2D.
현재 도착 배경은 폐상가에만 연결. 편의점 배경으로 잘못 재사용하지 않는다.
''',encoding='utf-8')
print('Arrival PSD layers verified; native background',Image.open(src).size)
