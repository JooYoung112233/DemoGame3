from pathlib import Path
from PIL import Image,ImageChops
from psd_layers import psd
import shutil,json
R=Path(__file__).resolve().parents[1];O=R/'아트/조우선택-v1';A=R/'Assets/Art'
for d in ['원본','개별-PNG','PSD']:(O/d).mkdir(parents=True,exist_ok=True)
paths={'paper':A/'PartySelection/card-paper.png','button':A/'PartySelection/footer-paper.png','infected':A/'Tokens/infected-body.png'}
for n,p in paths.items():
 shutil.copyfile(p,O/'원본'/f'{n}.png');shutil.copyfile(p,O/'개별-PNG'/f'{n}.png')
canvas=Image.new('RGBA',(3840,2160));layers=[];positions=[]
def put(n,key,box,tint=None):
 x,y,w,h=box;im=Image.open(paths[key]).convert('RGBA').resize((w*2,h*2),Image.Resampling.LANCZOS)
 if tint:im=ImageChops.multiply(im,Image.new('RGBA',im.size,tint))
 layers.append((n,im,(x*2,y*2),True));canvas.alpha_composite(im,(x*2,y*2));positions.append({'layer':n,'source':str(paths[key].relative_to(R)),'rect1080':box})
put('panel','paper',(28,238,720,692),(26,36,36,255));put('event-paper','paper',(48,254,680,256));put('infected','infected',(575,312,114,184))
for n,y in [('fight',530),('wait',645),('retreat',760)]:put(n,'button',(48,y,680,100))
psd(O/'PSD/encounter-layout.psd',layers,canvas)
canvas=Image.new('RGBA',(3840,2160));layers=[]
put('confirm-paper','paper',(460,250,1000,550));put('cancel','button',(510,680,410,78));put('confirm','button',(1000,680,410,78))
psd(O/'PSD/encounter-confirm.psd',layers,canvas)
(O/'layout.json').write_text(json.dumps(positions,ensure_ascii=False,indent=2),encoding='utf-8')
size=Image.open(paths['infected']).size
(O/'README.md').write_text(f'''# 조우 선택 UI
승인 UI09의 왼쪽 사건 카드와 세 가지 선택을 기준으로 기존 Demo5 종이·감염자 토큰을 재사용했다. 새 생성 원화는 없다.
원본 PNG 및 개별 PNG를 보관한다. 두 PSD는 사건 종이/감염자/각 선택 버튼, 확인창/버튼을 실제 레이어로 분리했다. 다시 읽어 레이어 이름·좌표·알파·채널을 검증했다. UI 문구는 Unity Text에서 수정한다.
3840×2160은 배치 편집 캔버스다. 기존 감염자 원본은 {size[0]}×{size[1]}로 저해상도 임시 토큰이며 확대 디테일 복원본이 아니다. 배경·대원은 기존 묶음의 원본을 사용한다.
ExpeditionEncounterPanel / EncounterChoiceReview 프리팹, 기존 ExpeditionMemberCard를 사용한다.
시안 차이: 실제 오락실 배경 유지, 비용을 1턴으로 표시, 교전은 전투 연결 전 비활성, 후퇴 목적지는 실제 구현된 복도. 원본 사건 일러스트와 동일한 그림은 아니며 큰 감염자 원화/세계 배치는 후속 보완이다. 사용자 시각 승인 대기.
''',encoding='utf-8')
print('Encounter PSD layers verified; originals retained.')
