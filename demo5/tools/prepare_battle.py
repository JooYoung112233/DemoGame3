"""Export existing UI art as a layered layout; retain original PNG pixels separately."""
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw
from psd_layers import psd
import json, shutil

R=Path(__file__).resolve().parents[1]
O=R/'아트/분리진형전투-v1'
for name in ['원본','개별-PNG','PSD']:(O/name).mkdir(parents=True,exist_ok=True)
layout=json.loads((O/'layout.json').read_text(encoding='utf-8-sig'))
sources={e['source'] for group in layout.values() for e in group if e['source']}
sources.update(['Assets/Art/Backgrounds/arcade-unlit-v1.png','Assets/Art/Settlement/standee-scout.png','Assets/Art/Settlement/standee-medic.png','Assets/Art/Settlement/base-white.png','Assets/Art/PartySelection/portrait-medic.png'])
manifest=[]
for source in sorted(sources):
 p=R/source;name=p.parent.name+'-'+p.name
 shutil.copyfile(p,O/'원본'/name);shutil.copyfile(p,O/'개별-PNG'/name)
 manifest.append({'source':source,'copy':name,'size':Image.open(p).size})

def project(side,col,row):
 ratio=1/(1-(1-1265/1547)*row/3)
 return (960+((455 if side==0 else 1060)+col*135-960)*ratio,-900+1265*ratio)

for group,entries in layout.items():
 canvas=Image.new('RGBA',(3840,2160));layers=[]
 def put(name,im,x,y):
  layers.append((name,im,(round(x*2),round(y*2)),True));canvas.alpha_composite(im,(round(x*2),round(y*2)))
 if group=='battle':
  put('background-existing-mall',Image.open(R/'Assets/Art/Backgrounds/arcade-unlit-v1.png').convert('RGBA').resize(canvas.size,Image.Resampling.LANCZOS),0,0)
  for side in [0,1]:
   board=Image.new('RGBA',canvas.size);draw=ImageDraw.Draw(board)
   def vertex(col,row):
    x,y=project(side,col,row);return (round(x*2),round(y*2))
   color=(214,219,194,215) if side==0 else (222,125,102,230)
   for i in range(4):
    draw.line([vertex(i,0),vertex(i,3)],fill=color,width=4)
    draw.line([vertex(0,i),vertex(3,i)],fill=color,width=4)
   put('ally-grid' if side==0 else 'enemy-grid',board,0,0)
  for i,(source,x,y,tint) in enumerate([
   ('Settlement/standee-scout.png',810,420,(255,199,97,255)),('Settlement/standee-medic.png',786,626,(163,201,189,255)),
   ('Tokens/infected-body.png',1110,420,(212,110,87,255)),('Tokens/infected-body.png',1134,626,(212,110,87,255))]):
   x,y=project(0 if i<2 else 1,2.5 if i<2 else .5,.8 if i%2==0 else 2.8)
   base=Image.open(R/'Assets/Art/Settlement/base-white.png').convert('RGBA').resize((204,56),Image.Resampling.LANCZOS)
   base=ImageChops.multiply(base,Image.new('RGBA',base.size,tint));put(f'pawn-{i}-base',base,x-51,y-14)
   body=Image.open(R/'Assets/Art'/source).convert('RGBA');body=body.crop(body.getbbox());body=body.resize((round(body.width/body.height*344),344),Image.Resampling.LANCZOS)
   put(f'pawn-{i}-body',body,x-body.width/4,y-172)
 for e in entries:
  color=tuple(round(c*255) for c in e['color']);w,h=round(e['w']*2),round(e['h']*2)
  if not w or not h or color[3]==0:continue
  if e['source']:
   im=Image.open(R/e['source']).convert('RGBA')
   if e['aspect']:
    im.thumbnail((w,h),Image.Resampling.LANCZOS);ratio=min(w/im.width,h/im.height)
    im=im.resize((round(im.width*ratio),round(im.height*ratio)),Image.Resampling.LANCZOS)
    box=Image.new('RGBA',(w,h));box.alpha_composite(im,((w-im.width)//2,(h-im.height)//2));im=box
   else:im=im.resize((w,h),Image.Resampling.LANCZOS)
   im=ImageChops.multiply(im,Image.new('RGBA',im.size,color))
  else:im=Image.new('RGBA',(w,h),color)
  put(e['name'],im,e['x'],e['y'])
 if group=='battle':
  shield=Image.new('RGBA',(112,114));draw=ImageDraw.Draw(shield)
  draw.polygon([(56,0),(105,19),(97,70),(81,96),(56,114),(31,96),(15,70),(7,19)],fill=(11,16,15,255))
  put('guard-vector-glyph',shield,1254,926)
  for i in range(4):
   paper=Image.open(R/'Assets/Art/PartySelection/card-paper.png').convert('RGBA').resize((192,216),Image.Resampling.LANCZOS)
   if i==0:paper=ImageChops.multiply(paper,Image.new('RGBA',paper.size,(255,199,97,255)))
   if i>=2:paper=ImageChops.multiply(paper,Image.new('RGBA',paper.size,(212,110,87,255)))
   put(f'turn-{i}-paper',paper,773+i*108,18)
 psd(O/'PSD'/f'{group}-layout.psd',layers,canvas)
 canvas.resize((1920,1080),Image.Resampling.LANCZOS).save(O/f'{group}-art-preview.png')
 print(group,len(layers),'layers verified')
(O/'sources.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(O/'README.md').write_text('''# 분리 진형 전투 UI — v1

UI10의 양 진형 3×3, 상단 차례, 하단 대원/대상/행동 구성을 기준으로 기존 Demo5 자산을 조합했다. 새로운 원화 생성은 없다.

- 원본/ 및 개별-PNG/: 기존 이미지의 바이트 동일 복사본. sources.json에 원본 경로와 실제 해상도를 기록한다.
- PSD/battle-layout.psd: 배경, 진형 선, 몸/발판, 패널·버튼·초상 등 분리 레이어. 결과와 철수 확인창도 별도 PSD로 제공한다.
- 3840×2160은 편집용 배치 캔버스이며 저해상도 캐릭터를 복원한 고해상도 원화가 아니다. 특히 기존 감염자 원본은 77×116의 임시 자산이다.
- 글자·수치·동작·차례 카드는 Unity 프리팹에서 편집한다. PSD는 UI 글자가 없는 이미지 조립 원본이다. 기본 초상/막대 상태와 실제 플레이의 선택 상태는 다를 수 있다.
- 방어 아이콘과 진형은 Unity 벡터 메시이며 코드와 Inspector가 편집 원본이다. PSD에는 분리된 미리보기 레이어로 포함했다.
- 실제 상태: Assets/Screenshots/Settlement/battle-v1.png. PNG 미리보기는 실제 게임 캡처와 구별한다.

PSD를 다시 읽어 레이어 이름, 좌표, 알파와 합성 결과를 검증했다.
''',encoding='utf-8')
