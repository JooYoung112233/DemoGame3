"""Package unchanged generated RGBA originals as separate full-resolution PSD layers."""
from pathlib import Path
from PIL import Image
import struct
base=Path(__file__).resolve().parents[1]/'아트'/'정착지-시설-v4'
files=[base/'원본'/'research-desk-v4.png',base/'원본'/'storage-crates-v4.png']
images=[Image.open(p).convert('RGBA') for p in files]
assert all(Image.open(p).mode=='RGBA' for p in files)
be=lambda fmt,*v:struct.pack('>'+fmt,*v)
w=sum(i.width for i in images)+64;h=max(i.height for i in images)
records=b'';pixels=b'';offset=0
composite=Image.new('RGBA',(w,h))
for index,(im,p) in enumerate(zip(images,files)):
    x=offset;y=0;offset+=im.width+64
    composite.paste(im,(x,y))
    channels=im.split();ordered=[(-1,channels[3]),(0,channels[0]),(1,channels[1]),(2,channels[2])]
    record=be('iiiiH',y,x,y+im.height,x+im.width,4)
    for cid,band in ordered:record+=be('hI',cid,len(band.tobytes())+2)
    record+=b'8BIMnorm'+bytes([255,0,0,0])
    name=p.stem.encode('ascii');pname=bytes([len(name)])+name;pname+=b'\0'*((-len(pname))%4)
    extra=be('II',0,0)+pname
    record+=be('I',len(extra))+extra
    records+=record
    for cid,band in ordered:pixels+=be('H',0)+band.tobytes()
layer=be('h',-len(images))+records+pixels
layer+=b'\0'*(len(layer)%2)
section=be('I',len(layer))+layer+be('I',0)
header=b'8BPS'+be('H',1)+b'\0'*6+be('HIIHH',4,h,w,8,3)
output=header+be('II',0,0)+be('I',len(section))+section+be('H',0)+b''.join(b.tobytes() for b in composite.split())
path=base/'facilities-v4-native-layers.psd';path.write_bytes(output)
loaded=Image.open(path);loaded.load();assert loaded.size==(w,h)
assert loaded.convert('RGB').tobytes()==composite.convert('RGB').tobytes()
print('PSD composite verified',loaded.size,'layers',len(images),'bytes',len(output))
for p,im in zip(files,images):print(p.name,im.size,'alpha bounds',im.getchannel('A').getbbox())

