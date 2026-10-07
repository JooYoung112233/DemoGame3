-- Local design study. Contours are traced by hand from the approved silhouette.
-- Original reference pixels are NOT enlarged into a claimed production master.
local root=[[E:/personalProject/Demo3/demo6/아트/승인형상-대표샘플-v12]]
local pc=app.pixelColor
local function clamp(x,a,b)return math.max(a,math.min(b,x))end
local function rgba(r,g,b,a)return pc.rgba(clamp(math.floor(r+.5),0,255),clamp(math.floor(g+.5),0,255),clamp(math.floor(b+.5),0,255),a or 255)end
local function sprite(w,h)local s=Sprite(w,h,ColorMode.RGB);s:deleteLayer(s.layers[1]);return s end
local function add(s,n,im)local l=s:newLayer();l.name=n;s:newCel(l,1,im,Point(0,0));return l end
local function merged(s)local im=Image(s.spec);im:drawSprite(s,1);return im end
local function inside(x,y,p)local c=false;local j=#p;for i=1,#p do local a,b=p[i],p[j];if (a[2]>y)~=(b[2]>y) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1]then c=not c end;j=i end;return c end
local function segment(x,y,a,b)local dx,dy=b[1]-a[1],b[2]-a[2];local k=clamp(((x-a[1])*dx+(y-a[2])*dy)/(dx*dx+dy*dy+.0001),0,1);return math.sqrt((x-a[1]-k*dx)^2+(y-a[2]-k*dy)^2)end
local function hash(x,y)return (math.sin(x*127.1+y*311.7)*43758.5453)%1 end
local function noise(x,y)local a,b=math.floor(x),math.floor(y);local u,v=x-a,y-b;u=u*u*(3-2*u);v=v*v*(3-2*v);return (hash(a,b)*(1-u)+hash(a+1,b)*u)*(1-v)+(hash(a,b+1)*(1-u)+hash(a+1,b+1)*u)*v end
local function brush(x,y)return (noise(x/7,y/7)-.5)*3.4+(noise(x/2.3,y/3.7)-.5)*1.6 end
local function smooth(p,steps)
 local out={};for i=1,#p do local a=p[(i-2)%#p+1];local b=p[i];local c=p[i%#p+1];local d=p[(i+1)%#p+1];for j=0,steps-1 do local t=j/steps;local t2,t3=t*t,t*t*t;out[#out+1]={.5*((2*b[1])+(-a[1]+c[1])*t+(2*a[1]-5*b[1]+4*c[1]-d[1])*t2+(-a[1]+3*b[1]-3*c[1]+d[1])*t3),.5*((2*b[2])+(-a[2]+c[2])*t+(2*a[2]-5*b[2]+4*c[2]-d[2])*t2+(-a[2]+3*b[2]-3*c[2]+d[2])*t3)}end end;return out
end
local W,H,S,OX,OY=1280,1536,4,202,84
local function paint(im,points,color,edge,curve)
 local p=curve and smooth(points,3)or points;local xmin,ymin,xmax,ymax=1e9,1e9,-1e9,-1e9
 for _,v in ipairs(p)do xmin=math.min(xmin,v[1]);ymin=math.min(ymin,v[2]);xmax=math.max(xmax,v[1]);ymax=math.max(ymax,v[2])end
 for py=math.max(0,math.floor((ymin-OY)*S)),math.min(H-1,math.ceil((ymax-OY)*S))do for px=math.max(0,math.floor((xmin-OX)*S)),math.min(W-1,math.ceil((xmax-OX)*S))do
  local x,y=OX+(px+.5)/S,OY+(py+.5)/S
  if inside(x,y,p)then
   local r,g,b;if type(color)=='function'then r,g,b=color(x,y)else r,g,b=table.unpack(color)end
   if edge and edge>0 then local dist=1e9;for i=1,#p do dist=math.min(dist,segment(x,y,p[i],p[i%#p+1]))end;if dist<edge then local t=clamp((dist-.35)/edge,0,1);r=17+(r-17)*t;g=17+(g-17)*t;b=15+(b-15)*t end end
   local n=brush(x,y);im:drawPixel(px,py,rgba(r+n,g+n,b+n,255))
  end
 end end
end
local function stroke(im,pts,width,col)
 for i=1,#pts-1 do local a,b=pts[i],pts[i+1];local dx,dy=b[1]-a[1],b[2]-a[2];local len=math.sqrt(dx*dx+dy*dy);for j=0,math.ceil(len*S)do local t=j/math.max(1,math.ceil(len*S));local cx,cy=a[1]+dx*t,a[2]+dy*t;for py=math.max(0,math.floor((cy-width-OY)*S)),math.min(H-1,math.ceil((cy+width-OY)*S))do for px=math.max(0,math.floor((cx-width-OX)*S)),math.min(W-1,math.ceil((cx+width-OX)*S))do local d=((OX+(px+.5)/S-cx)^2+(OY+(py+.5)/S-cy)^2)^.5;if d<width/2 then im:drawPixel(px,py,rgba(col[1],col[2],col[3]))end end end end end
end
local manifest={}
local function save(s,id,notes)
 local dir=root..'/layers/'..id;app.fs.makeAllDirectories(dir);app.fs.makeAllDirectories(root..'/PNG');local meta={width=s.width,height=s.height,layers={}}
 local comp=merged(s)
 for _,l in ipairs(s.layers)do local im=Image(s.spec);im:drawImage(l:cel(1).image,l:cel(1).position);im:saveAs(dir..'/'..l.name..'.png');local name=dir..'/'..l.name..'.rgba';local f=io.open(name,'wb');f:write(im.bytes);f:close();meta.layers[#meta.layers+1]={name=l.name,x=0,y=0,width=s.width,height=s.height,file=name,visible=l.isVisible}end
 local f=io.open(dir..'/composite.rgba','wb');f:write(comp.bytes);f:close();f=io.open(dir..'/psd-layers.json','w');f:write(json.encode(meta));f:close();comp:saveAs(root..'/PNG/'..id..'.png');s:saveAs(root..'/'..id..'-study-v012.aseprite');manifest[#manifest+1]={id=id,width=s.width,height=s.height,layers=#s.layers,notes=notes};return comp
end
local hero=sprite(W,H)
local cape=Image(W,H,ColorMode.RGB)
local capeContour={{277,305},{279,328},{269,342},{251,347},{235,352},{220,359},{214,367},{227,369},{234,379},{243,377},{250,383},{257,373},{261,384},{257,406},{255,419},{266,425},{274,417},{279,426},{287,422},{293,412},{299,421},{302,430},{311,426},{321,443},{326,453},{333,439},{344,434},{347,426},{355,435},{361,423},{370,430},{371,408},{371,389},{380,394},{385,387},{394,393},{400,391},{392,377},{388,364},{379,353},{369,331},{363,306}}
paint(cape,capeContour,function(x,y)local l=1+7*math.exp(-((x-304)/53)^2-((y-368)/85)^2);return 57+l,27+l*.3,28+l*.25 end,2.5,false)
paint(cape,{{284,315},{288,348},{276,378},{269,411},{279,412},{297,377},{307,345},{314,322}},{77,35,34},0,true)
paint(cape,{{294,318},{302,342},{321,354},{335,343},{349,319},{342,353},{326,371},{309,361},{299,348}},{34,22,23},0,true)
paint(cape,{{315,326},{313,360},{301,397},{312,417},{326,444},{338,421},{337,394},{346,351},{350,325}},function(x,y)return 69+brush(x,y)*.4,30,30 end,0,true)
paint(cape,{{353,318},{352,355},{349,382},{357,409},{365,421},{362,381},{362,350}},{33,21,22},0,true)
paint(cape,{{275,334},{258,352},{238,359},{236,366},{254,362},{269,352}},{73,34,34},0,true)
paint(cape,{{369,336},{380,367},{389,383},{380,380},{368,358}},{48,25,27},0,false)
stroke(cape,{{290,322},{293,349},{288,365},{280,385}},1.1,{86,41,38})
add(hero,'01-asymmetric-worn-cape',cape)

local arm=Image(W,H,ColorMode.RGB)
paint(arm,{{241,301},{240,319},{248,335},{264,339},{278,326},{275,310}}, {48,38,28},2,true)
paint(arm,{{382,295},{393,279},{401,270},{414,272},{418,283},{409,293},{404,312},{395,329},{381,333},{371,321}},{61,44,31},2.2,true)
stroke(arm,{{244,322},{255,328},{268,325}},2,{107,79,49});stroke(arm,{{386,322},{396,318},{404,301}},2,{106,77,48})
add(hero,'02-leather-arms-and-grip',arm)

local collar=Image(W,H,ColorMode.RGB)
paint(collar,{{280,276},{291,276},{301,298},{320,310},{339,303},{349,278},{362,279},{365,305},{354,326},{324,343},{301,332},{283,317}}, {39,36,28},2.8,true)
paint(collar,{{283,288},{292,309},{320,325},{348,310},{359,290},{355,313},{326,335},{318,334},{291,320}}, {100,76,44},1.1,true)
stroke(collar,{{288,307},{304,319},{323,328},{343,319},{354,307}},2.5,{139,109,65})
stroke(collar,{{291,317},{321,337},{346,323}},1.2,{167,143,100})
add(hero,'03-neck-collar',collar)

local function steel(cx,cy,rx,ry)
 return function(x,y)
  local broad=math.exp(-((x-cx)/rx)^2-((y-cy)/ry)^2)
  local hot=math.exp(-((x-(cx-1))/4.5)^2-((y-(cy-2))/8)^2)
  local l=34+45*broad+44*hot+(noise(x/5,y/6)-.5)*5
  return l*.99+2,l+1,l*.94
 end
end
local shoulders=Image(W,H,ColorMode.RGB)
paint(shoulders,{{282,265},{268,266},{252,272},{242,282},{236,292},{239,312},{247,322},{263,326},{279,320},{284,300},{286,281}},steel(261,288,14,21),2.5,true)
paint(shoulders,{{353,263},{369,267},{384,274},{397,282},{402,299},{397,315},{388,324},{368,326},{354,314},{349,293}},steel(370,288,16,19),2.5,true)
stroke(shoulders,{{246,278},{243,288},{245,307},{251,315},{266,317},{278,311}},1.7,{112,108,86})
stroke(shoulders,{{360,270},{373,272},{390,280}},1.7,{118,111,86})
stroke(shoulders,{{355,305},{366,318},{383,320},{393,312}},1.7,{106,102,83})
stroke(shoulders,{{250,321},{263,326},{276,320}},1.5,{108,77,46})
stroke(shoulders,{{368,325},{384,328},{393,321}},1.5,{104,73,43})
add(hero,'04-irregular-shoulder-plates',shoulders)

local helmet=Image(W,H,ColorMode.RGB)
local hc={{319,235},{307,241},{297,249},{290,261},{286,276},{287,294},{293,305},{305,313},{321,316},{336,309},{343,300},{346,286},{343,267},{337,250},{329,241}}
paint(helmet,hc,steel(315,270,17,33),2.6,true)
stroke(helmet,{{306,246},{299,255},{293,270},{292,288},{297,300},{307,307},{320,311},{333,304},{338,293},{337,272},{331,250}},1.6,{98,98,83})
stroke(helmet,{{318,238},{316,255},{316,275},{319,308}},3.6,{24,27,24})
stroke(helmet,{{320,239},{319,256},{320,277},{322,309}},1.4,{139,133,111})
stroke(helmet,{{305,243},{313,239}},2,{114,78,40})
add(hero,'05-elongated-ridged-helmet',helmet)

local sword=Image(W,H,ColorMode.RGB)
paint(sword,{{485,94},{481,117},{462,164},{443,208},{425,251},{413,247},{430,203},{458,139}}, {65,70,67},2,false)
paint(sword,{{483,100},{469,140},{445,197},{420,246},{416,244},{436,196},{462,138}}, {146,151,140},0,false)
paint(sword,{{483,99},{477,120},{452,178},{430,230},{423,248},{420,246},{444,191},{466,139}}, {87,95,91},0,false)
stroke(sword,{{481,107},{464,151},{441,204},{424,244}},1.4,{174,172,152})
paint(sword,{{408,249},{421,253},{415,276},{407,284},{401,278}}, {70,47,28},1.8,false)
paint(sword,{{395,243},{402,249},{430,259},{438,258},{442,262},{438,271},{428,267},{399,257},{390,254},{390,248}}, {112,82,43},1.6,false)
stroke(sword,{{399,251},{431,264},{437,264}},1.7,{161,127,73})
paint(sword,{{402,270},{408,268},{416,272},{415,280},{409,285},{399,281}}, {91,63,42},1.6,true)
stroke(sword,{{403,272},{412,276}},1.3,{126,87,56})
add(hero,'06-broad-sword-and-hand',sword)
save(hero,'hero-approved-shape','1280x1536 manually traced and repainted design study; six true layers. Coordinates trace approved 1920-normalized silhouette, not a magnified source crop. Static view only; not a rig-ready replacement.');hero:close()
print('Hero shape study saved')

-- Connected bedrock: a continuous painted mass divided by shared fractures.
local gw,gh=2048,1152;local ground=sprite(gw,gh);local base=Image(gw,gh,ColorMode.RGB)
for y=0,gh-1 do for x=0,gw-1 do
 local cloud=(noise(x/130,y/130)-.5)*15+(noise(x/34,y/34)-.5)*6+(noise(x/6,y/7)-.5)*2.5
 local warm=(noise(x/210+17,y/180+7)-.5)*7
 base:drawPixel(x,y,rgba(64+cloud+warm,64+cloud+warm*.55,56+cloud-warm*.1))
end end
add(ground,'01-continuous-bedrock',base)
local cracks=Image(gw,gh,ColorMode.RGB);local seams={
 {{-20,230},{200,294},{404,265},{585,343},{712,323}},
 {{404,265},{434,421},{387,591},{459,729},{434,900},{502,1170}},
 {{712,-20},{669,163},{712,323},{682,475},{802,588}},
 {{712,323},{890,294},{1023,360},{1190,302},{1339,346},{1534,277},{1789,315},{2068,238}},
 {{1023,360},{994,511},{1071,662},{1017,817},{1074,985},{1020,1170}},
 {{1339,-20},{1296,151},{1339,346},{1390,473},{1341,651}},
 {{1789,315},{1745,486},{1832,668},{1785,880},{1879,1170}},
 {{-20,751},{198,683},{387,591},{611,649},{802,588},{1071,662},{1341,651},{1551,722},{1832,668},{2068,743}},
 {{198,683},{225,879},{175,1036},{209,1170}},
 {{802,588},{752,779},{825,955},{786,1170}},
 {{434,900},{622,966},{825,955},{1017,817}},
 {{1341,651},{1283,823},{1337,1033},{1311,1170}},
 {{1534,277},{1579,80},{1523,-20}},
 {{1551,722},{1576,943},{1488,1170}},
 {{1074,985},{1337,1033},{1576,943},{1785,880},{2068,948}}
}
local gravel=Image(gw,gh,ColorMode.RGB)
local function pathseg(im,a,b,width,seed)
 local dx,dy=b[1]-a[1],b[2]-a[2];local len=(dx*dx+dy*dy)^.5;local nx,ny=-dy/len,dx/len
 for i=0,math.ceil(len*2)do local t=i/math.ceil(len*2);local off=(noise(t*13+seed,seed)-.5)*9*math.sin(t*math.pi);local cx,cy=a[1]+dx*t+nx*off,a[2]+dy*t+ny*off
  for yy=math.max(0,math.floor(cy-width*2)),math.min(gh-1,math.ceil(cy+width*2))do for xx=math.max(0,math.floor(cx-width*2)),math.min(gw-1,math.ceil(cx+width*2))do
   local d=((xx-cx)^2+(yy-cy)^2)^.5
   if d<width then im:drawPixel(xx,yy,rgba(28,28,23,255))elseif d<width+1.3 then im:drawPixel(xx,yy,rgba(83,78,63,110))end
  end end
 end
end
for k,p in ipairs(seams)do for i=1,#p-1 do pathseg(cracks,p[i],p[i+1],1.25+hash(k,i)*1.2,k*13+i)end end
-- Scattered grit belongs at selected junctions, never a continuous orange lane.
for k,p in ipairs(seams)do for i=2,#p-1 do if (k+i)%3==0 then local at=p[i]
 for n=1,20 do local a=hash(k*31+n,i)*math.pi*2;local rr=math.sqrt(hash(n*5,k))*24;local cx,cy=at[1]+math.cos(a)*rr,at[2]+math.sin(a)*rr;local r=1.5+hash(n,k+7)*4
  for y=math.max(0,math.floor(cy-r)),math.min(gh-1,math.ceil(cy+r))do for x=math.max(0,math.floor(cx-r)),math.min(gw-1,math.ceil(cx+r))do local d=((x-cx)^2+(y-cy)^2)^.5;if d<r then local v=44+hash(n,k)*20;if d>r-1 then v=v*.62 end;gravel:drawPixel(x,y,rgba(v+5,v+2,v-5))end end end
 end end end end
add(ground,'02-shared-fractures',cracks);add(ground,'03-junction-dust-and-grit',gravel)
save(ground,'connected-bedrock','2048x1152 locally painted continuous rock plane with shared fracture topology, three true layers. Representative terrain study, no independent cobblestone tiles.');ground:close()
local f=io.open(root..'/manifest.json','w');f:write(json.encode(manifest));f:close()
print('V12 representative study complete')
