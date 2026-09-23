from pathlib import Path
from PIL import Image,ImageChops
from psd_layers import psd
import shutil,json
r=Path(__file__).resolve().parents[1];o=r/'아트/재료안내-v1'
for n in ['원본','PSD']:(o/n).mkdir(parents=True,exist_ok=True)
layers=[];canvas=Image.new('RGBA',(1320,524))
for name,file,x,y,w,h in [('panel','card-paper.png',0,0,660,262),('action','footer-paper.png',404,204,232,46)]:
 src=r/'Assets/Art/PartySelection'/file;shutil.copyfile(src,o/'원본'/file)
 im=Image.open(src).convert('RGBA').resize((w*2,h*2),Image.Resampling.LANCZOS)
 if name=='action':im=ImageChops.multiply(im,Image.new('RGBA',im.size,(194,219,173,255)))
 layers.append((name,im,(x*2,y*2),True));canvas.alpha_composite(im,(x*2,y*2))
psd(o/'PSD/material-guide.psd',layers,canvas)
(o/'README.md').write_text('기존 종이 자산을 재사용한 패널/버튼 2개 레이어 PSD. 원본 픽셀도 보존. 2배 배치 캔버스는 디테일 복원이 아니다. 동적 아이콘·글자와 위치는 CraftMaterialGuide.prefab에서 편집한다. 화면 전체 PSD가 아니다.\n',encoding='utf-8')
print('Material guide two-layer PSD verified.')
