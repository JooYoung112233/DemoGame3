from pathlib import Path
from PIL import Image
from psd_layers import psd
import shutil
R=Path(__file__).resolve().parents[1];O=R/'아트/복도-v1';A=R/'Assets/Art'
for d in ['원본','개별-PNG','PSD']:(O/d).mkdir(parents=True,exist_ok=True)
src=A/'ExpeditionArrival/corridor.png';shutil.copyfile(src,O/'원본/corridor.png');shutil.copyfile(src,O/'개별-PNG/corridor.png');im=Image.open(src).convert('RGBA');psd(O/'PSD/corridor-background.psd',[('corridor-painted-background',im,(0,0),True)],im)
canvas=Image.new('RGBA',(3840,2160));layers=[]
def put(name,path,box):
 x,y,w,h=box;im=Image.open(path).convert('RGBA').resize((w*2,h*2),Image.Resampling.LANCZOS);layers.append((name,im,(x*2,y*2),True));canvas.alpha_composite(im,(x*2,y*2))
put('corridor',src,(0,0,1920,1080))
for name,box in [('day',(80,36,204,110)),('place',(308,36,320,110)),('turn',(80,162,204,62)),('route',(308,162,455,62)),('return',(80,952,410,78))]:put(name,A/'PartySelection/card-paper.png',box)
for i,(x,y) in enumerate([(80,384),(1140,355),(1655,400)]):put('door-hotspot-'+str(i),A/'PartySelection/arrow-paper.png',(x,y,84,84))
psd(O/'PSD/corridor-layout.psd',layers,canvas)
(O/'README.md').write_text('''# 오락실과 이어지는 복도
image_gen 기본 도구 생성. 참조: Assets/Art/ExpeditionArrival/arcade-clean.png.
요청: 같은 종이 질감·정면에서 조금 높은 시점·회색/붉은 체크 타일·청록 회색 벽. 왼쪽 열린 출입문, 뒤쪽 잠긴 철문, 오른쪽 닫힌 문. 하단 이동 공간 확보, 캐릭터/UI/텍스트/강조광 제외. 최대 네이티브 해상도 요청.
실제 원본: 1672×941. 목표 3840×2160 미달. 원본 PNG 보존. 레이어 배치 PSD 3840×2160은 확대 배치이며 디테일 복원이 아니다.
corridor-background.psd는 배경 단일 레이어다. 벽/바닥/문/소품 독립 분리는 미완료. corridor-layout.psd는 배경/날짜/장소/턴/연결 표시/문 버튼/귀환 버튼 분리. 텍스트와 상호작용은 Unity 프리팹 편집.
문 배치는 첫 연결 시연용. 잠긴 철문/닫힌 문은 설명만 열리고 내부 방은 아직 미제작이다.
''',encoding='utf-8')
print('Corridor sources and verified PSDs saved.')
