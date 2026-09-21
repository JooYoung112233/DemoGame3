"""Separate authorized local cutouts; retain generated source pixels and dimensions."""
from pathlib import Path
import json
import struct
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from asset_cutout import gray_backdrop_cut, inspect_asset

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'art/chapter00'
OUT = ART / 'memory'
REVIEW = ART / 'review'
REVIEW.mkdir(exist_ok=True)

people_source = Image.open(ART / 'sources/seoyeon-soi-holding-source-v1.png')
people, source_rect = gray_backdrop_cut(people_source, (0, 0, *people_source.size), holes=[(565, 650), (635, 950)])
people.save(OUT / 'seoyeon-soi-holding-v1.png')
inspect_asset(people, REVIEW, 'seoyeon-soi')

def cyan_cut(source, destination):
    im = Image.open(source).convert('RGB')
    rgb = np.asarray(im).astype(np.int16)
    # Cyan is absent from all three characters. Feather only the keyed edge.
    strength = np.minimum(rgb[:, :, 1] - rgb[:, :, 0], rgb[:, :, 2] - rgb[:, :, 0])
    alpha = (255 * (1 - np.clip((strength - 6) / 24, 0, 1))).astype(np.uint8)
    rgba = im.convert('RGBA')
    rgba.putalpha(Image.fromarray(alpha))
    rgba.save(destination)
    inspect_asset(rgba, REVIEW, destination.stem)
    return rgba

handover = cyan_cut(ART / 'sources/hands-handover-source-v1.png', OUT / 'hands-handover-v1.png')
received = cyan_cut(ART / 'sources/hands-received-source-v1.png', OUT / 'hands-received-v1.png')
bg = Image.open(OUT / 'memory-path-base-v1.png').convert('RGBA')
present = Image.open(ART / 'present/hand-on-book-v1.png').convert('RGBA')

# Review-only placements. Master cutouts above remain at their native size.
person_h = 835
person_w = round(people.width * person_h / people.height)
position = (650, 70)
shadow = Image.new('RGBA', bg.size)
d = ImageDraw.Draw(shadow)
d.ellipse((690, 869, 955, 917), fill=(52, 45, 32, 65))
d.ellipse((958, 861, 1097, 910), fill=(52, 45, 32, 55))
shadow = shadow.filter(ImageFilter.GaussianBlur(10))
shadow.save(OUT / 'memory-ground-shadow-review-v1.png')
wide = bg.copy()
wide.alpha_composite(shadow)
wide.alpha_composite(people.resize((person_w, person_h), Image.Resampling.LANCZOS), position)
wide.save(REVIEW / '01-memory-family-v1.png')
frames = [wide]
for index, layer in [(2, handover), (3, received)]:
    frame = bg.copy()
    frame.alpha_composite(layer)
    frame.save(REVIEW / f'0{index}-memory-hands-v1.png')
    frames.append(frame)
frames.append(present)

def write_psd(path, layers, merged):
    """PSD v1, uncompressed 8-bit RGBA layers; names and visibility preserved."""
    pack = struct.pack
    w, h = merged.size
    records, channel_data = [], []
    for name, layer, visible in layers:  # Photoshop order: top to bottom.
        assert layer.size == (w, h)
        channels = layer.convert('RGBA').split()
        record = pack('>4iH', 0, 0, h, w, 4)
        for channel_id, band in [(-1, channels[3]), (0, channels[0]), (1, channels[1]), (2, channels[2])]:
            payload = b'\x00\x00' + band.tobytes()
            record += pack('>hI', channel_id, len(payload))
            channel_data.append(payload)
        encoded = name.encode('ascii')[:255]
        pascal = bytes([len(encoded)]) + encoded
        pascal += b'\x00' * (-len(pascal) % 4)
        extra = pack('>II', 0, 0) + pascal
        record += b'8BIMnorm' + bytes([255, 0, 0 if visible else 2, 0]) + pack('>I', len(extra)) + extra
        records.append(record)
    layer_info = pack('>h', len(layers)) + b''.join(records) + b''.join(channel_data)
    layer_info += b'\x00' * (len(layer_info) % 2)
    layer_mask = pack('>I', len(layer_info)) + layer_info + pack('>I', 0)
    header = b'8BPS' + pack('>H', 1) + b'\x00' * 6 + pack('>HIIHH', 3, h, w, 8, 3)
    # RGB merged previews need an explicit matte; hidden RGB under transparent
    # cutout pixels must not appear as a leftover generated backdrop.
    preview = Image.new('RGBA', merged.size, (0, 0, 0, 255))
    preview.alpha_composite(merged.convert('RGBA'))
    preview = preview.convert('RGB')
    composite = b'\x00\x00' + b''.join(band.tobytes() for band in preview.split())
    path.write_bytes(header + pack('>II', 0, 0) + pack('>I', len(layer_mask)) + layer_mask + composite)
    # Independent Pillow PSD decoder checks layer records and merged pixels.
    with Image.open(path) as opened:
        assert opened.size == (w, h)
        assert len(opened.layers) == len(layers)
        assert [item[0] for item in opened.layers] == [item[0] for item in layers]
        assert np.array_equal(np.asarray(opened.convert('RGB')), np.asarray(preview))
        print('PSD verified:', path.name, [item[0] for item in opened.layers])

write_psd(OUT / 'memory-hands-layers-v1.psd', [
    ('03_received_HIDE_WHEN_02_VISIBLE', received, False),
    ('02_handover_HIDE_WHEN_03_VISIBLE', handover, True),
    ('01_memory_path_background', bg, True),
], frames[1])
sheet = Image.new('RGB', (1672, 942), '#302f29')
for index, frame in enumerate(frames):
    sheet.paste(frame.convert('RGB').resize((836, 471), Image.Resampling.LANCZOS), ((index % 2) * 836, (index // 2) * 471))
sheet.save(REVIEW / 'opening-sequence-contact-sheet-v1.jpg', quality=94)

assets = []
for path in sorted(ART.rglob('*.png')):
    if path.relative_to(ART).parts[0] not in {'memory', 'sources', 'present'}:
        continue
    im = Image.open(path)
    entry = {'path': path.relative_to(ROOT).as_posix(), 'size': list(im.size), 'mode': im.mode}
    if im.mode == 'RGBA':
        alpha = np.asarray(im.getchannel('A'))
        entry.update(alpha_bbox=list(im.getchannel('A').getbbox()), transparent_pixels=int((alpha == 0).sum()), partial_alpha_pixels=int(((alpha > 0) & (alpha < 255)).sum()))
    assets.append(entry)
manifest = {
    'status': 'opening-memory-first-draft-not-final-resolution',
    'generator': 'built-in image_gen; native outputs preserved; local authorized background removal',
    'coordinate_canvas': list(bg.size),
    'people_source_crop': source_rect,
    'review_people_placement': {'position': list(position), 'size': [person_w, person_h]},
    'hands': 'same 1672x941 canvas, mutually exclusive layers; retain native pixels',
    'psd': {'path': 'art/chapter00/memory/memory-hands-layers-v1.psd', 'native_canvas': [1672,941], 'layers': 3, 'default_visible': 'handover + background', 'verification': 'Pillow PSD parser: layer names/count/canvas and pixel-exact merged image; Photoshop UI not tested'},
    'limitations': ['Backgrounds 1672x941, below 3840px target; no upscaling passed off as detail.', 'Character height below 1536px target; Seoyeon design is an unapproved first proposal.', 'Mother and child are one contact group; bodies are not individually layered.', 'Book/hand/table present frame is flattened; only the two hand states and memory background are PSD layers.', 'Hand matching to present frame is approximate; final camera alignment and transition remain to be implemented.'],
    'assets': assets,
}
(ROOT / 'design/chapter00/opening-assets-v1.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'people_source_rect': source_rect, 'assets': len(assets), 'review': str(REVIEW / 'opening-sequence-contact-sheet-v1.jpg')}, ensure_ascii=False))
