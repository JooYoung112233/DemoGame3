"""Reuse existing page texture and sea illustration inside a fixed book silhouette."""
from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageChops,ImageStat
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/chapter01/layers/pages';OUT.mkdir(exist_ok=True)
QA=ROOT/'art/chapter01/layers/qa'
book=Image.open(ROOT/'art/chapter01/layers/sprites/sketchbook-source-v1.png').convert('RGBA')
# Only the interior of the paper can change. Cover and silhouette remain original.
points=[(8,13),(13,10),(28,8),(39,6),(47,9),(51,12),(57,9),(65,6),(77,9),(94,13),(94,60),(78,59),(63,59),(52,63),(46,60),(29,60),(8,63)]
mask=Image.new('L',(102*4,73*4),0);ImageDraw.Draw(mask).polygon([(x*4,y*4) for x,y in points],fill=255)
mask=mask.resize(book.size,Image.Resampling.LANCZOS)
texture=Image.open(ROOT/'art/chapter01/layers/sketchbook-open-blank-v1.png').convert('RGB').crop((280,240,650,600)).resize(book.size,Image.Resampling.LANCZOS)
# Match the existing paper warmth; this is local page cleanup, not a new book.
texture=texture.point(lambda p:p*.91)
blank=book.copy();blank.paste(texture,(0,0),mask)
blank.putalpha(book.getchannel('A'))
blank.save(OUT/'sketchbook-blank-v1.png')
sea_source=Image.open(ROOT/'art/chapter01/01-camper-evening/camper-drawing-v2.png').convert('RGB')
paper=sea_source.crop((914,460,1016,515)).resize((90,58),Image.Resampling.LANCZOS)
sea=Image.new('RGBA',book.size);sea.paste(paper,(6,6));sea.putalpha(mask)
sea.save(OUT/'sea-complete-overlay-v1.png')
# Partial state contains the first sea strokes, leaving figures/sun for completion.
partial_mask=mask.copy();window=Image.new('L',book.size,0)
ImageDraw.Draw(window).rectangle((8,23,47,39),fill=255)
partial_mask=ImageChops.multiply(partial_mask,window)
partial=sea.copy();partial.putalpha(partial_mask);partial.save(OUT/'sea-started-overlay-v1.png')
assert blank.getchannel('A').tobytes()==book.getchannel('A').tobytes()
outside=mask.point(lambda p:255 if p==0 else 0)
assert ImageChops.multiply(ImageChops.difference(blank,book).convert('RGB').convert('L'),outside).getbbox() is None
for name,overlay in [('blank',None),('started',partial),('complete',sea)]:
    b=blank.copy()
    if overlay:b.alpha_composite(overlay)
    panel=Image.new('RGBA',book.size,'#29392f');panel.alpha_composite(b)
    panel.resize((612,438),Image.Resampling.NEAREST).save(QA/('page-'+name+'-6x.png'))
(OUT/'page-validation.json').write_text(json.dumps({'bookSize':[102,73],'bookAlphaUnchanged':True,'outsidePagePixelsUnchanged':True,'sourceArt':'art/chapter01/01-camper-evening/camper-drawing-v2.png','paperTexture':'art/chapter01/layers/sketchbook-open-blank-v1.png','method':'crop, resize, local page masking; no whole-scene generation','states':['blank','started','complete']},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('3 page assets; book alpha and pixels outside page mask unchanged.')
