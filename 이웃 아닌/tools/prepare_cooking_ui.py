"""Reassemble existing UI image pieces into editable layout layers; no new artwork."""
from pathlib import Path
from PIL import Image, ImageChops
from psd_layers import psd
import json, shutil

root = Path(__file__).resolve().parents[1]
out = root / '아트/식량준비-v1'
for folder in ('원본', '개별-PNG', 'PSD'):
    (out / folder).mkdir(parents=True, exist_ok=True)
data = json.loads((out / 'layout.json').read_text(encoding='utf-8-sig'))
canvas = Image.new('RGBA', (1920, 1080))
layers = []
for index, item in enumerate(data['layers']):
    w, h = round(item['w']), round(item['h'])
    if w < 1 or h < 1:
        continue
    name = f"{index:02d}-{item['name']}"
    color = tuple(round(v * 255) for v in item['color'])
    if item['path']:
        src = root / item['path']
        original = Image.open(src).convert('RGBA')
        shutil.copyfile(src, out / '원본' / (name + '.png'))
        if item.get('aspect'):
            ratio = min(w / original.width, h / original.height)
            fitted = original.resize((max(1, round(original.width * ratio)), max(1, round(original.height * ratio))), Image.Resampling.LANCZOS)
            image = Image.new('RGBA', (w, h))
            image.alpha_composite(fitted, ((w - fitted.width) // 2, (h - fitted.height) // 2))
        else:
            image = original.resize((w, h), Image.Resampling.LANCZOS)
        image = ImageChops.multiply(image, Image.new('RGBA', image.size, color))
    else:
        image = Image.new('RGBA', (w, h), color)
    image.save(out / '개별-PNG' / (name + '.png'))
    xy = round(item['x']), round(item['y'])
    layers.append((name, image, xy, True))
    canvas.alpha_composite(image, xy)
psd(out / 'PSD/cooking-ui.psd', layers, canvas)
canvas.save(out / 'image-layers-preview.png')
(out / 'README.md').write_text('''# 식량 준비 UI v1

1920×1080 UI 이미지 배치 원본. 기존 승인 종이·물자·물·가방·인물 아이콘을 재사용한다.
PSD는 이미지 요소별 실제 분리 레이어이며 원본 PNG와 배치 크기 PNG를 각각 보관한다.
배경과 동적 문구는 PSD에 합치지 않았다. 텍스트·수량·탭·버튼은 CookingPanel.prefab에서 편집한다.
현재 음식 그림은 물자 상자, 휴대식은 가방 아이콘을 쓰는 임시 표기다. 음식 전용 원화 완성본이 아니다.
1920×1080은 배치 캔버스이며 확대된 기존 아이콘의 디테일 복원을 의미하지 않는다.
실제 합성 화면: Assets/Screenshots/Settlement/cooking-ui-v1.png.
''', encoding='utf-8')
print(f'Verified {len(layers)} editable PSD layers.')
