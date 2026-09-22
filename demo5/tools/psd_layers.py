import io, re, struct
import numpy as np
from PIL import Image

def check(name,ok):
    assert ok,name

def packbits(raw):
    out=bytearray();pos=0
    def literal(data):
        for start in range(0,len(data),128):
            b=data[start:start+128];out.append(len(b)-1);out.extend(b)
    for m in re.finditer(rb'(.)\1{2,}',raw,re.DOTALL):
        literal(raw[pos:m.start()]);length=m.end()-m.start()
        while length:
            n=min(128,length);out.extend((257-n if n>1 else 0,m.group()[0]));length-=n
        pos=m.end()
    literal(raw[pos:]);return bytes(out)

def channel_payload(band):
    a=np.asarray(band);rows=[packbits(row.tobytes()) for row in a]
    return b'\0\1'+struct.pack('>'+str(len(rows))+'H',*[len(r) for r in rows])+b''.join(rows)

def unpackbits(data):
    out=bytearray();i=0
    while i<len(data):
        n=data[i];i+=1
        if n<=127:out.extend(data[i:i+n+1]);i+=n+1
        elif n>=129:out.extend(bytes([data[i]])*(257-n));i+=1
    return bytes(out)

def psd(path,layers,merged):
    # Layers are bottom to top. Each carries native local pixels and placement.
    records=[];pixels=[];pk=struct.pack
    ordered=list(reversed(layers))
    for name,im,(x,y),visible in ordered:
        w,h=im.size
        rec=pk('>4iH',y,x,y+h,x+w,4)
        for cid,band in zip([-1,0,1,2],[im.getchannel('A'),*im.convert('RGB').split()]):
            payload=channel_payload(band);rec+=pk('>hI',cid,len(payload));pixels.append(payload)
        label=name.encode('ascii');pascal=bytes([len(label)])+label;pascal+=b'\0'*(-len(pascal)%4)
        extra=pk('>II',0,0)+pascal
        rec+=b'8BIMnorm'+bytes([255,0,0 if visible else 2,0])+pk('>I',len(extra))+extra
        records.append(rec)
    info=pk('>h',-len(layers))+b''.join(records)+b''.join(pixels)
    info+=b'\0'*(len(info)%2)
    section=pk('>I',len(info))+info+pk('>I',0)
    w,h=merged.size
    header=b'8BPS'+pk('>H',1)+b'\0'*6+pk('>HIIHH',4,h,w,8,3)
    payloads=[channel_payload(band) for band in merged.split()]
    composite=b'\0\1'+b''.join(c[2:2+h*2] for c in payloads)+b''.join(c[2+h*2:] for c in payloads)
    path.write_bytes(header+pk('>II',0,0)+pk('>I',len(section))+section+composite)
    with Image.open(path) as im:
        check(path.stem+':merged_rgba',np.array_equal(np.asarray(im.convert('RGBA')),np.asarray(merged)))
        check(path.stem+':layer_names',[x[0] for x in im.layers]==[x[0] for x in ordered])
    # Independent layer-channel reader verifies offsets, visibility, and hidden pixels.
    f=io.BytesIO(path.read_bytes());f.seek(26)
    def u(fmt):return struct.unpack('>'+fmt,f.read(struct.calcsize('>'+fmt)))
    for _ in range(2):f.read(u('I')[0])
    u('I');u('I');count=abs(u('h')[0]);recs=[]
    for _ in range(count):
        t,l,b,r,n=u('4iH');ch=[u('hI') for _ in range(n)];f.read(8)
        opacity,clip,flags,filler=u('4B');n=u('I')[0];end=f.tell()+n
        f.read(u('I')[0]);f.read(u('I')[0]);name=f.read(u('B')[0]).decode('ascii');f.seek(end)
        recs.append((name,(l,t),(r-l,b-t),ch,not bool(flags&2)))
    for (name,xy,size,channels,visible),expected in zip(recs,ordered):
        bands={}
        for cid,length in channels:
            data=f.read(length);height=size[1];assert data[:2]==b'\0\1'
            counts=struct.unpack('>'+str(height)+'H',data[2:2+2*height]);start=2+2*height;rows=[]
            for length in counts:rows.append(unpackbits(data[start:start+length]));start+=length
            bands[cid]=Image.frombytes('L',size,b''.join(rows))
        decoded=Image.merge('RGBA',[bands[c] for c in (0,1,2,-1)])
        check(path.stem+':layer:'+name,xy==expected[2] and visible==expected[3] and np.array_equal(np.asarray(decoded),np.asarray(expected[1])))
