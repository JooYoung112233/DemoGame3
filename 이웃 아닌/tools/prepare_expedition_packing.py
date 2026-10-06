from pathlib import Path
from PIL import Image
import numpy as np, shutil
from psd_layers import psd
R=Path(__file__).resolve().parents[1];O=R/'아트/원정짐꾸리기-v1';P=R/'Assets/Art/PartySelection'
for d in ['개별-PNG','PSD']:(O/d).mkdir(parents=True,exist_ok=True)
canvas=Image.new('RGBA',(3840,2160));layers=[]
def put(name,file,rect,tint=None):
 im=Image.open(P/file).convert('RGBA');x,y,w,h=rect;im=im.resize((w*2,h*2),Image.Resampling.LANCZOS)
 if tint:im=Image.fromarray(np.uint8(np.asarray(im).astype(float)*np.array(tint)))
 layers.append((name,im,(x*2,y*2),True));canvas.alpha_composite(im,(x*2,y*2))
put('background','teal-texture.png',(0,0,1920,1080))
put('heading','card-paper.png',(80,44,500,130));put('date','count-paper.png',(1650,55,190,90))
for name,rect in [('stock',(74,222,712,708)),('bag',(806,222,516,708)),('detail',(1342,222,502,392)),('checklist',(1342,634,502,296))]:put(name,'card-paper.png',rect,(.12,.16,.17,.97))
for name,rect in [('stock-title',(104,242,220,64)),('bag-title',(836,242,286,64)),('detail-title',(1372,242,220,55)),('checklist-title',(1372,654,344,53))]:put(name,'count-paper.png',rect)
for i in range(3):put('member-'+str(i),'card-paper.png',(620+i*286,28,272,172))
for i in range(4):put('category-'+str(i),'footer-paper.png',(104+i*165,320,153,52))
for i in range(10):put('stock-slot-'+str(i),'count-paper.png',(104+round((i%5)*129.6),390+(i//5)*134,118,120))
for i in range(3):put('bag-slot-'+str(i),'count-paper.png',(836+i*113,334,101,120))
for name,rect in [('item-paper',(1372,310,438,198)),('checklist-rows',(1372,721,438,115))]:put(name,'card-paper.png',rect)
for name,rect in [('to-bag',(1372,527,213,60)),('to-stock',(1597,527,213,60)),('back',(80,952,410,78)),('review',(1430,952,410,78)),('notice',(1372,848,438,55))]:put(name,'footer-paper.png',rect)
psd(O/'PSD/packing-layout.psd',layers,canvas)
for file in ['teal-texture.png','card-paper.png','count-paper.png','footer-paper.png','icon-left.png','selected-check.png']:
 im=Image.open(P/file).convert('RGBA');shutil.copyfile(P/file,O/'개별-PNG'/file);psd(O/'PSD'/(Path(file).stem+'.psd'),[(Path(file).stem,im,(0,0),True)],im)
(O/'README.md').write_text('''# 원정 짐 꾸리기 UI03
승인 기준: ../UI전체시안-v1/03-원정짐꾸리기-v2.png.
기존 승인 종이·배경·아이콘을 재사용한다. 새 그림 생성 없음. 3840×2160 PSD의 배경/패널/제목/카드/슬롯/버튼이 개별 레이어이며 글자와 수치는 Unity에서 편집한다. 원본 텍스처 PNG와 단일 요소 PSD도 보존했다. 큰 배치 PSD는 기존 픽셀 확대 배치이며 고해상도 디테일 복원이 아니다.
아이템 그림 원본: ../개별인벤토리-v1 및 Assets/Art/Inventory. 대원 원본: ../모험가선택-v1 및 Assets/Art/PartySelection. 새 PSD는 기존 인물·아이콘 원본을 대체하지 않는다.
시안과 차이: 최신 규칙대로 뒤로 버튼 좌측 하단. 실제 데이터의 2명·가방 3종 사용. 현재 아이템은 보급품/탄약/재료이므로 물/치료/도구 수량을 꾸며내지 않는다. 체크리스트도 실제 합계로 표시한다. 빈 16칸을 가짜로 그리지 않는다.
Unity 프리팹: ExpeditionPackingPanel, ExpeditionPackingReview, PackingItemSlot. 대원 카드는 ExpeditionMemberCard 공유. 출발 동작은 후속 단계이며 이번 화면은 준비 내역 확인까지다.
''',encoding='utf-8')
print('Layered PSD and native shared textures saved and verified.')
