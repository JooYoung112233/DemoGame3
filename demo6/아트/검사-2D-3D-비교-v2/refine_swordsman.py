"""Actual Blender mesh revision against the 2D design; retains the original source."""
import bpy, os, math, json
from mathutils import Vector

OUT=os.path.dirname(os.path.abspath(__file__))
SOURCE=os.path.join(os.path.dirname(OUT),'검사-2D-3D-비교-v1','swordsman-3d.blend')
bpy.ops.wm.open_mainfile(filepath=SOURCE)
scene=bpy.context.scene; scene.frame_set(1)
rig=bpy.data.objects['Swordsman_Rig']
mats={m.name:m for m in bpy.data.materials}
skin=mats['Skin | warm matte']; hair=mats['Hair | dark umber']; hairlit=mats['Hair | alternate planes']
leather=mats['Leather | dark brown']; leatherlit=mats['Leather | edge pieces']; cloth=mats['Tunic | charcoal brown']
linen=mats['Scarf | unbleached linen']; steel=mats['Armor | dark steel']; brass=mats['Fittings | dull brass']; eye=mats['Eyes']

# Remove the rough massing geometry, leaving the source rig and weapon intact.
prefixes=('Head shaped','Hair ','Ear','Eye','Brow','Mouth','Small wedge','Left steel pauldron',
          'Pauldron lower','Pauldron rivet','Leather breast panel','Linen collar','Scarf hanging',
          'L sleeve cap','R sleeve cap','L closed glove','R closed glove','L elbow','R elbow')
for ob in list(bpy.data.objects):
    if any(ob.name.startswith(p) for p in prefixes): bpy.data.objects.remove(ob,do_unlink=True)

def attach(ob,name,mat,bone,smooth=False):
    ob.name=name; ob.data.materials.append(mat)
    if smooth:
        for p in ob.data.polygons: p.use_smooth=True
    vg=ob.vertex_groups.new(name=bone); vg.add(list(range(len(ob.data.vertices))),1,'REPLACE')
    ar=ob.modifiers.new('Rigid attachment','ARMATURE'); ar.object=rig; ob.parent=rig
    return ob

def mesh(name,verts,faces,mat,bone,smooth=False):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    ob=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(ob)
    return attach(ob,name,mat,bone,smooth)

def uv(name,loc,scale,mat,bone,segments=20,rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1,location=loc)
    ob=bpy.context.object; ob.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return attach(ob,name,mat,bone,True)

def cube(name,loc,dim,mat,bone,bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); ob=bpy.context.object; ob.dimensions=dim
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    b=ob.modifiers.new('Soft edges','BEVEL'); b.width=bevel; b.segments=3
    bpy.ops.object.modifier_apply(modifier=b.name)
    return attach(ob,name,mat,bone,True)

# Face: shaped quad rings with a broad flat front, tapering jaw and a rounded chin.
N=24; verts=[]
rings=[(2.215,.07,.10),(2.25,.16,.17),(2.32,.235,.22),(2.43,.282,.245),
       (2.56,.29,.25),(2.68,.28,.23),(2.78,.225,.18),(2.84,.105,.085)]
for z,rx,ry in rings:
    for i in range(N):
        a=math.tau*i/N; c=math.cos(a); s=math.sin(a)
        # Slightly squarer than an ellipsoid, the cheeks do not triangulate visibly.
        x=math.copysign(abs(s)**.8,s)*rx
        y=-math.copysign(abs(c)**.46,c)*ry
        verts.append((x,y-.005,z))
faces=[tuple(reversed(range(N)))]
for j in range(len(rings)-1):
    for i in range(N): faces.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
faces.append(tuple(range((len(rings)-1)*N,len(rings)*N)))
head=mesh('V2 shaped continuous head',verts,faces,skin,'head',True)
for sign in [-1,1]:
    uv('V2 ear',(sign*.287,.01,2.48),(.052,.048,.083),skin,'head')
    uv('V2 simple dark eye',(sign*.108,-.253,2.525),(.026,.010,.043),eye,'head',16,8)
    # Low raised upper lid and asymmetrical brow, no spherical doll eyeball.
    brow=mesh('V2 brow',[(sign*.060,-.264,2.594),(sign*.157,-.243,2.604),
                       (sign*.159,-.246,2.582),(sign*.066,-.267,2.573)],[(0,1,2,3)],hair,'head')
mesh('V2 restrained nose',[(-.029,-.251,2.516),(.029,-.251,2.516),(0,-.285,2.455),
                         (-.024,-.247,2.445),(.024,-.247,2.445)],[(0,1,2),(0,2,3),(1,4,2),(3,2,4)],skin,'head',True)
mesh('V2 mouth',[(-.030,-.235,2.355),(.032,-.235,2.348),(.032,-.236,2.340),(-.030,-.236,2.346)],[(0,1,2,3)],leather,'head')

# A single fitted hair cap with tapered locks, replacing the stack of rock-like spheres.
verts=[]; HR=24
for level in range(5):
    for i in range(HR):
        a=math.tau*i/HR; c=math.cos(a); s=math.sin(a)
        if level==0:
            z=2.59 if c>.3 else 2.40
            rx,ry=.315,.267
        elif level==1: z=2.76; rx,ry=.343,.286
        elif level==2: z=2.885+.012*math.sin(a*3); rx,ry=.287,.248
        elif level==3: z=2.963+.009*math.cos(a*2); rx,ry=.16,.16
        else: z=2.989; rx,ry=.018,.018
        verts.append((s*rx,-c*ry+.025,z))
faces=[]
for j in range(4):
    for i in range(HR): faces.append((j*HR+i,j*HR+(i+1)%HR,(j+1)*HR+(i+1)%HR,(j+1)*HR+i))
faces.append(tuple(range(HR*4,HR*5)))
cap=mesh('V2 continuous hair cap',verts,faces,hair,'head',True)

def lock(name,root,tip,width,depth,mat):
    root,tip=Vector(root),Vector(tip); d=(tip-root).normalized(); side=Vector((d.z,0,-d.x)).normalized()*width
    mid=root.lerp(tip,.42)
    ridge=mid+Vector((0,-depth,0))
    v=[tuple(root-side*.70),tuple(root+side*.70),tuple(mid+side),tuple(tip),tuple(mid-side),tuple(ridge)]
    return mesh(name,v,[(0,1,5),(1,2,5),(2,3,5),(3,4,5),(4,0,5)],mat,'head',False)
lock('V2 fringe 01',(-.19,-.10,2.88),(-.29,-.205,2.60),.094,.055,hairlit)
lock('V2 fringe 02',(-.055,-.13,2.96),(-.16,-.267,2.64),.105,.062,hair)
lock('V2 fringe 03',(.075,-.15,2.91),(-.035,-.284,2.605),.100,.047,hairlit)
lock('V2 fringe 04',(.19,-.14,2.865),(.075,-.27,2.61),.098,.05,hair)
lock('V2 fringe 05',(.265,-.085,2.82),(.236,-.205,2.545),.065,.045,hairlit)
lock('V2 side lock',(.309,.045,2.69),(.304,-.025,2.405),.065,.045,hair)
lock('V2 crown swept tuft',(-.13,.09,2.91),(.16,-.06,3.012),.07,.025,hairlit)

# Breast panel sits only in front of the tunic; the old nested torso caused side intersections.
verts=[(-.27,-.262,1.49),(.27,-.262,1.49),(.315,-.271,1.94),(-.315,-.271,1.94),
       (-.27,-.245,1.49),(.27,-.245,1.49),(.315,-.253,1.94),(-.315,-.253,1.94)]
panel=mesh('V2 fitted chest leather',verts,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],leather,'spine')
border=cube('V2 chest panel upper welt',(0,-.277,1.95),(.64,.014,.018),leatherlit,'spine',.006)

# Rounded linen scarf loop, enough geometry to read as cloth instead of a stone collar.
verts=[]; cross=8; radial=32
for i in range(radial):
    a=math.tau*i/radial
    for j in range(cross):
        b=math.tau*j/cross
        verts.append(((.20+.055*math.cos(b))*math.sin(a),
                      (.15+.05*math.cos(b))*math.cos(a),
                      2.16+.075*math.sin(b)+.024*math.sin(a)))
faces=[]
for i in range(radial):
    for j in range(cross): faces.append((i*cross+j,i*cross+(j+1)%cross,((i+1)%radial)*cross+(j+1)%cross,((i+1)%radial)*cross+j))
mesh('V2 soft linen wrap',verts,faces,linen,'spine',True)
mesh('V2 scarf folded tail',[(.09,-.219,2.15),(.24,-.215,2.135),(.247,-.282,1.90),(.225,-.281,1.74),(.105,-.294,1.78),(.117,-.277,1.96),(.173,-.303,1.96)],
     [(0,1,6),(1,2,6),(2,3,6),(3,4,6),(4,5,6),(5,0,6)],linen,'spine',True)

# A shoulder plate with designed rings instead of a faceted ball.
verts=[]; PN=12
for z,rx,ry in [(2.01,.255,.232),(2.10,.275,.249),(2.205,.195,.183),(2.245,.07,.066)]:
    for i in range(PN):
        a=math.tau*i/PN; verts.append((-.49+rx*math.sin(a),-.015+ry*math.cos(a),z))
faces=[tuple(reversed(range(PN)))]
for j in range(3):
    for i in range(PN): faces.append((j*PN+i,j*PN+(i+1)%PN,(j+1)*PN+(i+1)%PN,(j+1)*PN+i))
faces.append(tuple(range(3*PN,4*PN)))
mesh('V2 shaped shoulder armor',verts,faces,steel,'upper_arm.L',False)
for x in [-.615,-.395]: uv('V2 armor rivet',(x,-.244,2.078),(.026,.016,.026),brass,'upper_arm.L',12,8)

for side,sign in [('L',-1),('R',1)]:
    uv('V2 sleeve '+side,(sign*.46,0,2),(.195,.205,.205),cloth,'upper_arm.'+side)
    uv('V2 elbow '+side,(sign*.62,-.018,1.69),(.121,.124,.13),skin,'forearm.'+side)
    uv('V2 glove '+side,(sign*.71,-.08,1.37),(.11,.105,.13),leather,'hand.'+side)
    uv('V2 thumb '+side,(sign*.66,-.16,1.4),(.057,.061,.074),leather,'hand.'+side)
    for j in range(3):
        cube('V2 finger fold '+side,(sign*.742,-.178,1.405-j*.042),(.10,.033,.029),leatherlit,'hand.'+side,.012)

# Smooth cylinder sides and retain crisp cap surfaces on existing clothing and bracers.
for ob in rig.children:
    if ob.type!='MESH': continue
    if any(k in ob.name for k in ('thigh','shin','high boot','boot cuff','upper arm','forearm','bracer')):
        for p in ob.data.polygons: p.use_smooth=(len(p.vertices)==4)

# Broad matte values closer to the illustration, no painted enhancement of render output.
for m in [skin,hair,hairlit,leather,leatherlit,cloth,linen,steel]:
    bs=m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Roughness'].default_value=.85
    bs.inputs['Specular IOR Level'].default_value=.22
    bs.inputs['Emission Color'].default_value=bs.inputs['Base Color'].default_value
    bs.inputs['Emission Strength'].default_value=.10
steel.node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value=.2

cam=scene.camera; cam.location=(2.8,-8,4.1)
cam.rotation_euler=(Vector((0,0,1.46))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=3.85
scene.render.engine='CYCLES'; scene.cycles.samples=48
scene.render.resolution_x=1800; scene.render.resolution_y=2048
scene.render.resolution_percentage=100
scene.render.film_transparent=False
scene.render.filepath=os.path.join(OUT,'swordsman-3d-v2.png')
bpy.ops.render.render(write_still=True)
floor=bpy.data.objects['Studio Ground (not exported)']; floor.hide_render=True
scene.render.film_transparent=True; scene.render.filepath=os.path.join(OUT,'swordsman-3d-v2-alpha.png')
bpy.ops.render.render(write_still=True)
floor.hide_render=False; scene.render.film_transparent=False
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'swordsman-3d-v2.blend'))
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
for ob in rig.children:
    if ob.type=='MESH': ob.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'swordsman-3d-v2.glb'),use_selection=True,export_format='GLB',export_animations=True)
objects=[o for o in rig.children if o.type=='MESH']
with open(os.path.join(OUT,'model-report.json'),'w',encoding='utf8') as f:
    json.dump({'vertices':sum(len(o.data.vertices) for o in objects),
               'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
               'bones':len(rig.data.bones),'note':'Actual mesh revision; original blockout animation retained, not polished'},f,indent=2)
print('V2_RENDER_COMPLETE')
