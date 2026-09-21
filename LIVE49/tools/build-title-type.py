"""Build outlined title assets and an offline menu font; does not edit background art."""
from pathlib import Path
import sys, json
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'.tmp/title-build-deps'))
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools import subset
OUT=ROOT/'art/title'
def load(name,weight):
    font=TTFont(OUT/'fonts'/name)
    return instantiateVariableFont(font,{'wght':weight},inplace=True)
latin=load('CormorantGaramond.ttf',400)
korean=load('NotoSerifKR.ttf',400)
def line(font,text,size,tracking,y,width,color):
    gs=font.getGlyphSet(); cmap=font.getBestCmap(); scale=size/font['head'].unitsPerEm
    names=[cmap[ord(c)] for c in text]
    total=sum(gs[n].width*scale for n in names)+tracking*(len(names)-1)
    x=(width-total)/2; parts=[]
    for name in names:
        pen=SVGPathPen(gs);gs[name].draw(pen)
        parts.append(f'<path fill="{color}" transform="translate({x:.4f} {y}) scale({scale:.6f} {-scale:.6f})" d="{pen.getCommands()}"/>')
        x+=gs[name].width*scale+tracking
    return ''.join(parts)
svg='<svg xmlns="http://www.w3.org/2000/svg" width="640" height="244" viewBox="0 0 640 244" role="img" aria-label="Live49 마지막 여름">'
svg+=line(latin,'Live49',172,1.5,160,640,'#F1E6CA')
svg+=line(korean,'마지막 여름',26,10,219,640,'#CDBB95')+'</svg>'
(OUT/'logo-v1.svg').write_text(svg,encoding='utf-8')
(OUT/'logo-source.json').write_text(json.dumps({'canvas':[640,244],'wordmark':{'text':'Live49','font':'CormorantGaramond','weight':400,'size':172,'tracking':1.5,'baseline':160,'fill':'#F1E6CA'},'subtitle':{'text':'마지막 여름','font':'Noto Serif KR','weight':400,'size':26,'tracking':10,'baseline':219,'fill':'#CDBB95'}},ensure_ascii=False,indent=2),encoding='utf-8')
# Keep all modern Hangul syllables plus common punctuation for localizable Korean UI.
opts=subset.Options(); opts.layout_features=['*'];opts.name_IDs=['*'];opts.name_languages=['*']
sub=subset.Subsetter(options=opts);sub.populate(unicodes=list(range(0x20,0x100))+list(range(0x1100,0x1200))+list(range(0x3130,0x3190))+list(range(0xAC00,0xD7A4))+list(range(0x2000,0x2070))+[0x3000,0x3001,0x3002])
sub.subset(korean)
for nid,value in [(1,'Live49 Menu Serif'),(2,'Regular'),(3,'Live49MenuSerif-1'),(4,'Live49 Menu Serif'),(6,'Live49MenuSerif-Regular'),(16,'Live49 Menu Serif'),(17,'Regular')]:
    korean['name'].removeNames(nameID=nid)
    korean['name'].setName(value,nid,3,1,0x409)
korean.flavor=None;korean.save(OUT/'fonts/Live49MenuSerif-Regular.ttf')
korean.flavor='woff2';korean.save(OUT/'fonts/Live49MenuSerif-Regular.woff2')
print('Title SVG, editable specification, static TTF and WOFF2 written.')
