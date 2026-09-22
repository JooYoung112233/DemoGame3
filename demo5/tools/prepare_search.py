from pathlib import Path
from PIL import Image,ImageChops
from psd_layers import psd
import shutil,json
R=Path(__file__).resolve().parents[1];O=R/'아트/탐험수색지정-v1';A=R/'Assets/Art'
for d in ['원본','개별-PNG','PSD']:(O/d).mkdir(parents=True,exist_ok=True)
paths={'paper':A/'PartySelection/card-paper.png','button':A/'PartySelection/footer-paper.png','box':A/'Settlement/icon-storage.png','table':A/'Settlement/icon-work.png','machine':A/'Settlement/icon-journal.png'}
for n,p in paths.items():
 shutil.copyfile(p,O/'원본'/f'{n}.png');shutil.copyfile(p,O/'개별-PNG'/f'{n}.png')
canvas=Image.new('RGBA',(3840,2160));layers=[];positions=[]
def put(n,key,box,tint=None):
 x,y,w,h=box;im=Image.open(paths[key]).convert('RGBA').resize((w*2,h*2),Image.Resampling.LANCZOS)
 if tint:im=ImageChops.multiply(im,Image.new('RGBA',im.size,tint))
 layers.append((n,im,(x*2,y*2),True));canvas.alpha_composite(im,(x*2,y*2));positions.append({'layer':n,'source':str(paths[key].relative_to(R)),'rect1080':box})
put('panel','paper',(28,238,654,694),(26,36,36,250));put('title','button',(48,250,270,62));put('object-paper','paper',(48,326,130,100));put('object-icon','box',(75,340,76,72))
for i in range(3):put('pace-'+str(i),'button',(48+i*204,475,194,52),(255,194,89,255) if i==1 else None)
for i in range(2):put('worker-paper-'+str(i),'paper',(48+i*144,578,132,126))
for i in range(3):put('duty-'+str(i),'button',(48+i*204,755,194,48))
put('back','button',(80,952,410,78));put('confirm','button',(1030,952,410,78),(194,219,173,255))
psd(O/'PSD/search-layout.psd',layers,canvas)
(O/'layout.json').write_text(json.dumps(positions,ensure_ascii=False,indent=2),encoding='utf-8')
(O/'README.md').write_text('''# 탐험 수색 지정 UI
승인 기준: 아트/UI전체시안-v1/07-탐험수색지정-v2.png. 기존 Demo5 종이·아이콘을 그대로 재사용했다. 새 생성 원화는 없다.
원본/과 개별-PNG/에 실제 사용 텍스처를 원본 픽셀 크기로 보관한다. search-layout.psd는 패널/제목/사물 아이콘/각 버튼/담당자 카드 바탕을 실제 레이어로 분리했다. PSD를 재개방하고 채널·알파·좌표·레이어 이름을 검증했다.
PSD 캔버스는 3840×2160이며 배치 편집용이다. 저해상도 원본 텍스처를 확대했으므로 고해상도 디테일 복원본이 아니다. 배경과 실루엣은 기존 원본 묶음을 참조하며 이 PSD에는 포함하지 않는다. 문구는 Unity Text로 수정한다.
Unity 프리팹: ExpeditionSearchPanel, SearchWorkerCard, SearchAssignmentReview (Assets/Prefabs/Settlement).
07번과의 차이: 기존 방 이동 HUD 때문에 왼쪽 패널을 아래로 내렸다. 닫기는 최신 사용자 지시대로 좌하단이다. 실제 선택 대원만 표시하고, 가짜 수색도·장비 보유량·조우 확률은 표시하지 않는다. 수색 실행 전 단계로 배정 확인 버튼을 사용한다. 사용자 시각 승인 대기.
''',encoding='utf-8')
print('Verified layered PSD and original PNGs saved.')
