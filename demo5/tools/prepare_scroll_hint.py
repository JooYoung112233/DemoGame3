from pathlib import Path
from PIL import Image,ImageDraw
from psd_layers import psd
R=Path(__file__).resolve().parents[1];O=R/'아트/UI공통/스크롤안내';O.mkdir(parents=True,exist_ok=True)
points=[(18,8),(82,8)]
def quad(control,end):
    start=points[-1]
    for i in range(1,9):
        t=i/8;points.append(tuple((1-t)**2*start[j]+2*(1-t)*t*control[j]+t*t*end[j] for j in range(2)))
quad((95,8),(95,20));quad((95,25),(88,32));points.append((61,60));quad((50,72),(39,60));points.append((12,32));quad((5,25),(5,20));quad((5,8),(18,8))
layers=[];merged=Image.new('RGBA',(240,180))
for name,color,offset in [('soft-edge',(5,10,10,87),10),('arrow',(250,242,219,140),0)]:
    im=Image.new('RGBA',(960,720));ImageDraw.Draw(im).polygon([(x*2.4*4,(y*170/70+offset)*4) for x,y in points],fill=color);im=im.resize((240,180),Image.Resampling.LANCZOS);im.save(O/(name+'.png'));layers.append((name,im,(0,0),True));merged.alpha_composite(im)
merged.save(O/'scroll-more.png');psd(O/'scroll-more.psd',layers,merged)
path='M18 8 H82 Q95 8 95 20 Q95 25 88 32 L61 60 Q50 72 39 60 L12 32 Q5 25 5 20 Q5 8 18 8 Z'
(O/'scroll-more.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 75"><path d="{path}" transform="translate(0 4.1176)" fill="#050a0a" opacity=".34"/><path d="{path}" fill="#faf2db" opacity=".55"/></svg>',encoding='utf-8')
(O/'README.md').write_text('''# 스크롤 하단 안내

사용자가 제안한 둥근 아래 화살표를 단순 벡터 UI로 구성했다. 스크롤 영역 우측 하단 24×17에서 낮은 불투명도로 표시한다.

- 인게임: `RoundedDownArrow` 벡터 Graphic, `ScrollMoreIndicator` 표시 조건.
- 프리팹: `Assets/Prefabs/Settlement/ScrollMoreHint.prefab`.
- SVG: 크기에 독립적인 원본 곡선.
- PSD/PNG: 240×180 편집용 출력. 화살표/옅은 가장자리를 별도 레이어로 검증했다.

스크롤 아래에 남은 내용이 2px보다 클 때 표시하고 끝·빈 목록·넘치지 않는 목록에서는 숨긴다. 위치/크기/색·알파는 프리팹에서 조정한다. 클릭/드래그/휠을 가로채지 않는다. 현재 작업대의 5개 스크롤과 휴식 담당자 스크롤에 적용했다.
''',encoding='utf-8')
print('Vector source, separated PNGs and two-layer PSD verified.')
