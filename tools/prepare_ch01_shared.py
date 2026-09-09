from pathlib import Path
import json
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
def layer(id,name,asset,rect,order=30):return dict(id=id,name=name,asset=asset,rect=rect,order=order)
def state(id,label,visible,focus,line,speaker='',active=None):return dict(id=id,label=label,visible=visible,focus=focus,line=line,speaker=speaker,active=active or [])
def save_pack(cfg):
    (ROOT/'design/chapter01'/f"{cfg['id']}.json").write_text(json.dumps(cfg,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    layers=cfg.get('slots',[])+cfg.get('actors',[])+([cfg['foreground']] if cfg.get('foreground') else [])
    for s in cfg['presets']:
        canvas=Image.open(ROOT/cfg['base']).convert('RGBA')
        for l in sorted(layers,key=lambda l:l['order']):
            if l.get('alwaysVisible') or l['id'] in s['visible']:
                im=Image.open(ROOT/l['asset']).convert('RGBA').resize(tuple(l['rect'][2:]),Image.Resampling.LANCZOS)
                canvas.alpha_composite(im,tuple(l['rect'][:2]))
        canvas.save(ROOT/'art/chapter01/scene-qa'/f"{cfg['id']}-{s['id']}.png")
