from pathlib import Path
from PIL import Image
import numpy as np, shutil, json
from psd_layers import psd
R=Path(__file__).resolve().parents[1];O=R/'아트/원정계획-v1';A=R/'Assets/Art/ExpeditionPlan'
for p in [O/'원본',O/'개별-PNG',O/'PSD',A]:p.mkdir(parents=True,exist_ok=True)
generated=Path('C:/Users/admin/.codex/generated_images/01a084ef-08a3-7492-b066-9506a2da3dbc/exec-4fad5c0e-f172-43e9-8381-48a330a5de2e.png')
for p in [O/'원본/map-clean.png',O/'개별-PNG/map-clean.png',A/'map-clean.png']:shutil.copyfile(generated,p)
src=Image.open(R/'아트/UI전체시안-v1/02-원정계획.png').convert('RGBA')
boxes={'pin-home':(188,216,242,258),'pin-mall':(477,165,526,212),'pin-store':(809,221,858,261),'pin-warehouse':(559,381,608,424),'clock':(1345,158,1390,201),'risk':(1343,208,1393,250),'water':(1394,312,1436,382),'parts':(1482,319,1554,377)}
for name,box in boxes.items():
 im=src.crop(box);rgb=np.asarray(im)[:,:,:3];mask=rgb.mean(2)<(100 if name=="pin-mall" else 175);seen=np.zeros(mask.shape,bool);parts=[]
 for y,x in zip(*np.where(mask)):
  if seen[y,x]:continue
  todo=[(y,x)];seen[y,x]=True;part=[]
  while todo:
   cy,cx=todo.pop();part.append((cy,cx))
   for ny,nx in [(cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)]:
    if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not seen[ny,nx]:seen[ny,nx]=True;todo.append((ny,nx))
  parts.append(part)
 mask[:]=False
 for part in parts:
  if len(part)>=max(8,len(max(parts,key=len))*.025):
   for y,x in part:mask[y,x]=True
 im.putalpha(Image.fromarray(np.uint8(mask)*255));im=im.crop(im.getbbox());im.save(O/'개별-PNG'/f'{name}.png');shutil.copyfile(O/'개별-PNG'/f'{name}.png',A/f'{name}.png');psd(O/'PSD'/f'{name}.psd',[(name,im,(0,0),True)],im)
im=src.crop((1104,213,1312,492));im.save(O/'개별-PNG/mall-photo.png');shutil.copyfile(O/'개별-PNG/mall-photo.png',A/'mall-photo.png');psd(O/'PSD/mall-photo.psd',[('approved-mall-photo',im,(0,0),True)],im)
canvas=Image.new('RGBA',(3840,2160));layers=[]
def put(name,path,rect,tint=None):
 im=Image.open(path).convert('RGBA');x,y,w,h=rect;im=im.resize((w*2,h*2),Image.Resampling.LANCZOS)
 if tint:im=Image.fromarray(np.uint8(np.asarray(im).astype(float)*np.array(tint)))
 xy=(x*2,y*2);layers.append((name,im,xy,True));canvas.alpha_composite(im,xy)
P=R/'Assets/Art/PartySelection'
put('background',P/'teal-texture.png',(0,0,1920,1080))
put('map',A/'map-clean.png',(80,166,1100,474))
put('heading',P/'card-paper.png',(80,44,360,116));put('date',P/'count-paper.png',(1650,55,190,90))
put('destination-paper',P/'card-paper.png',(1200,166,640,474));put('destination-title',P/'count-paper.png',(1220,177,236,70));put('photo',A/'mall-photo.png',(1227,260,220,300))
put('member-band',P/'card-paper.png',(80,660,1760,250),(.11,.14,.15,.97))
for i in range(6):put('member-'+str(i),P/'card-paper.png',(104+i*284,716,272,172))
put('back',P/'footer-paper.png',(80,952,410,78));put('pack',P/'footer-paper.png',(1430,952,410,78),(.76,.86,.68,1));put('selection',P/'card-paper.png',(514,952,892,78),(.11,.14,.15,.97))
psd(O/'PSD/expedition-plan-layout.psd',layers,canvas)
native=Image.open(A/'map-clean.png');psd(O/'PSD/map-clean.psd',[('clean-map-background',native.convert('RGBA'),(0,0),True)],native.convert('RGBA'))
(O/'manifest.json').write_text(json.dumps({'reference':'../UI전체시안-v1/02-원정계획.png','mapNativeSize':native.size,'iconNativeCrops':boxes,'layoutPsd':[3840,2160],'limitations':['Generative cleanup preserves reference style but cannot promise pixel-identical map roads.','Map is one painted background layer; pins, labels and UI are separate.','Icons/photo are native small reference crops, not high-resolution restoration.']},ensure_ascii=False,indent=2),encoding='utf-8')
(O/'README.md').write_text('''# 원정 계획 — UI02

승인 시안 배치: 지도·장소 상세·생존자 목록. 최신 사용자 규칙에 따라 돌아가기는 좌측 하단에 둔다.
지도는 기본 image_gen 도구로 이름표/핀을 제거한 바탕을 만들었다. 원본 네이티브 크기는 manifest.json에 기록했다. 지도 자체는 한 그림 레이어이며 건물/도로가 개별 레이어인 것은 아니다.
실제 PSD 레이어: 지도, 제목, 장소 종이, 사진, 대원 카드, 하단 버튼/요약. 각 아이콘 PNG/PSD는 승인 시안의 원본 픽셀 추출이며 작은 자산을 고해상도 복원으로 간주하지 않는다.
핀 외형은 Unity 벡터 Graphic, 내부 장소 아이콘·이름·선택색은 개별 UI다. 글자/수치/체력/선택 상태는 Unity에서 편집한다.
지도 정리 프롬프트: 승인 시안의 왼쪽 지도만 추출하고 4개 핀/글자/나침반을 제거하여 바탕 복원. 도로·강·건물·나무의 구성과 종이 질감을 최대한 유지. UI/인물 추가 금지. 넓은 약 2.3:1, 가능한 최대 네이티브 해상도. 원본 도구 출력은 원본/map-clean.png에 보존.
''',encoding='utf-8')
print('Expedition map copied; separate PNGs and layered PSDs verified. Native map:',native.size)
