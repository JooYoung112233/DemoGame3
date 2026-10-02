"""Reproducible, primitive/custom-mesh Blender study. No downloaded models or textures."""
import bpy, math, json, os
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))
os.makedirs(os.path.join(OUT, 'frames'), exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color, metallic=0, rough=.75):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Metallic'].default_value = metallic
    bs.inputs['Roughness'].default_value = rough
    return m

skin = material('Skin | warm matte', (.55,.34,.22))
hair = material('Hair | dark umber', (.055,.027,.018))
hairlit = material('Hair | alternate planes', (.085,.041,.025))
leather = material('Leather | dark brown', (.15,.075,.043))
leatherlit = material('Leather | edge pieces', (.26,.135,.068))
cloth = material('Tunic | charcoal brown', (.105,.087,.072))
pants = material('Trousers | soot', (.067,.057,.052))
linen = material('Scarf | unbleached linen', (.61,.49,.31))
steel = material('Armor | dark steel', (.21,.24,.25), .65,.43)
edge = material('Blade | bright steel', (.49,.56,.57), .75,.3)
blade_dark = material('Blade | shaded ridge', (.28,.34,.35), .7,.35)
brass = material('Fittings | dull brass', (.39,.25,.10), .55,.5)
eye = material('Eyes', (.018,.014,.012))
ground = material('Studio ground', (.115,.105,.09))

parts = []
bindings = {}
def finish(ob, name, mat, bone=None, bevel=0):
    ob.name = name
    if mat: ob.data.materials.append(mat)
    if bevel:
        mod=ob.modifiers.new('Small manufactured edges','BEVEL'); mod.width=bevel; mod.segments=1
        bpy.context.view_layer.objects.active=ob
        bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(ob)
    if bone: bindings[ob.name]=bone
    return ob

def box(name, loc, scale, mat, bone=None, bevel=.03):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    ob=bpy.context.object; ob.dimensions=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(ob,name,mat,bone,bevel)

def sphere(name, loc, scale, mat, bone=None, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=loc)
    ob=bpy.context.object; ob.scale=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(ob,name,mat,bone)

def segment(name, a, b, r1, r2, mat, bone=None, vertices=8):
    a,b=Vector(a),Vector(b); delta=b-a
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2,
                                  depth=delta.length, location=(a+b)/2)
    ob=bpy.context.object; ob.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    return finish(ob,name,mat,bone)

def mesh(name, verts, faces, mat, bone=None):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    ob=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(ob)
    return finish(ob,name,mat,bone)

def torso(name, rings, mat, bone):
    verts=[]
    # Beveled rectangular cross-sections; front is negative Y.
    for z,wx,dy in rings:
        verts += [(x*wx,y*dy,z) for x,y in [(-.72,-1),(.72,-1),(1,-.65),(1,.65),(.72,1),(-.72,1),(-1,.65),(-1,-.65)]]
    faces=[tuple(reversed(range(8)))]
    for j in range(len(rings)-1):
        for i in range(8): faces.append((j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i))
    faces.append(tuple(range((len(rings)-1)*8,len(rings)*8)))
    return mesh(name,verts,faces,mat,bone)

torso('Tunic fitted torso',[(1.32,.31,.22),(1.6,.32,.235),(1.96,.43,.24),(2.12,.35,.19)],cloth,'spine')
torso('Leather breast panel',[(1.48,.30,.235),(1.92,.39,.25),(2.04,.30,.21)],leather,'spine')
torso('Short split tunic hem',[(1.09,.37,.25),(1.37,.32,.22)],leather,'root')
box('Hem center seam',(0,-.256,1.2),(.024,.018,.24),cloth,'root',.003)
torso('Waist belt',[(1.35,.337,.249),(1.46,.337,.249)],leatherlit,'root')
box('Belt buckle',(0,-.267,1.405),(.15,.055,.12),brass,'root',.016)
box('Buckle inset',(0,-.3,1.405),(.085,.012,.057),leather,'root',.004)
box('Hip pouch',(-.38,-.015,1.34),(.20,.23,.26),leatherlit,'root',.035)
box('Pouch flap',(-.38,-.137,1.40),(.20,.035,.13),leather,'root',.02)

for side, sign in [('L',-1),('R',1)]:
    x=sign*.205
    hip=(x,0,1.25); knee=(x,-.012,.76); ankle=(x,.02,.25)
    segment(side+' thigh',knee,hip,.14,.18,pants,'thigh.'+side)
    segment(side+' shin',ankle,knee,.11,.135,pants,'shin.'+side)
    segment(side+' high boot',(x,.02,.15),(x,0,.60),.14,.15,leather,'shin.'+side)
    box(side+' boot toe',(x,-.095,.135),(.30,.46,.23),leather,'shin.'+side,.06)
    box(side+' boot sole',(x,-.09,.046),(.31,.46,.073),pants,'shin.'+side,.022)
    segment(side+' boot cuff',(x,.01,.55),(x,.01,.61),.163,.163,leatherlit,'shin.'+side)
    box(side+' kneecap',(x,-.122,.76),(.23,.10,.22),leatherlit,'shin.'+side,.045)

shoulders={}
elbows={}
wrists={}
for side,sign in [('L',-1),('R',1)]:
    s=(sign*.46,0,2.00); e=(sign*.62,-.018,1.69); w=(sign*.71,-.08,1.37)
    shoulders[side]=s; elbows[side]=e; wrists[side]=w
    sphere(side+' sleeve cap',s,(.21,.235,.22),cloth,'upper_arm.'+side)
    segment(side+' upper arm',e,s,.125,.16,cloth,'upper_arm.'+side)
    sphere(side+' elbow',e,(.13,.135,.13),leather,'forearm.'+side)
    segment(side+' forearm',w,e,.105,.13,skin,'forearm.'+side)
    p=Vector(w).lerp(Vector(e),.7)
    segment(side+' bracer',w,p,.125,.15,leather,'forearm.'+side)
    segment(side+' bracer rim',p-Vector((0,0,.035)),p,.16,.16,leatherlit,'forearm.'+side)
    box(side+' closed glove',w,(.21,.20,.22),leather,'hand.'+side,.045)

# One substantial pauldron, broad planes and two rivets; no ornamental high-detail promises.
sphere('Left steel pauldron',(-.49,-.015,2.08),(.285,.275,.185),steel,'upper_arm.L',2)
box('Pauldron lower lip',(-.51,-.23,2.025),(.35,.045,.07),brass,'upper_arm.L',.017)
for x in [-.62,-.40]: sphere('Pauldron rivet',(x,-.262,2.06),(.027,.018,.027),brass,'upper_arm.L')

# Scarf, neck, face and a deliberately simple short-hair silhouette.
segment('Neck',(0,0,2.06),(0,0,2.35),.14,.145,skin,'head')
torso('Linen collar',[(2.07,.25,.22),(2.20,.23,.18)],linen,'spine')
mesh('Scarf hanging end',[(.10,-.24,2.12),(.27,-.24,2.12),(.22,-.27,1.75),(.09,-.27,1.85)],[(0,1,2,3)],linen,'spine')
sphere('Head shaped planes',(0,-.01,2.51),(.325,.275,.385),skin,'head',2)
for sign in [-1,1]: sphere('Ear',(sign*.315,0,2.50),(.065,.072,.106),skin,'head')
mesh('Small wedge nose',[(-.045,-.25,2.53),(.045,-.25,2.53),(0,-.32,2.44),(-.045,-.247,2.435),(.045,-.247,2.435)],[(0,1,2),(0,2,3),(1,4,2),(3,2,4)],skin,'head')
for sign in [-1,1]:
    box('Eye',(sign*.118,-.257,2.545),(.055,.019,.035),eye,'head',.008)
    brow=box('Brow',(sign*.12,-.263,2.597),(.085,.025,.024),hair,'head',.006)
    brow.rotation_euler.y=sign*.12
box('Mouth',(0,-.262,2.345),(.068,.013,.016),leather,'head',.003)
sphere('Hair back',(0,.055,2.68),(.34,.28,.27),hair,'head',2)
for i,(x,y,z,sx,sy,sz) in enumerate([
    (-.21,-.16,2.73,.15,.15,.19),(-.08,-.22,2.77,.15,.14,.20),(.07,-.23,2.79,.15,.13,.18),
    (.21,-.17,2.74,.13,.15,.20),(-.29,0,2.59,.065,.17,.21),(.29,0,2.60,.065,.17,.20),
    (-.13,.03,2.89,.20,.18,.11),(.12,.045,2.88,.19,.18,.11)]):
    lock=sphere('Hair lock %02d'%i,(x,y,z),(sx,sy,sz),hairlit if i%3==0 else hair,'head')
    lock.rotation_euler.y=-.22

# Sword geometry is independent and moves rigidly with the hand bone.
sx,sy,sz=wrists['R']; sy-=.095
segment('Sword grip',(sx,sy,sz-.16),(sx,sy,sz+.11),.049,.049,leatherlit,'hand.R',8)
for n in range(5):
    segment('Grip winding',(sx,sy,sz-.14+n*.047),(sx,sy,sz-.126+n*.047),.053,.053,pants,'hand.R',8)
sphere('Sword pommel',(sx,sy,sz-.2),(.07,.066,.08),brass,'hand.R')
box('Sword crossguard',(sx,sy,sz+.15),(.44,.095,.065),steel,'hand.R',.025)
for sign in [-1,1]: sphere('Guard cap',(sx+sign*.205,sy,sz+.17),(.07,.06,.06),brass,'hand.R')
z0=sz+.19; z1=z0+.88; z2=z0+1.11
verts=[(sx-.083,sy,z0),(sx,sy-.027,z0),(sx+.083,sy,z0),(sx,sy+.027,z0),
       (sx-.065,sy,z1),(sx,sy-.023,z1),(sx+.065,sy,z1),(sx,sy+.023,z1),(sx,sy,z2)]
faces=[(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,8),(5,6,8),(6,7,8),(7,4,8),(3,2,1,0)]
blade=mesh('Longsword diamond section blade',verts,faces,edge,'hand.R')
blade.data.materials.append(blade_dark)
for p in blade.data.polygons: p.material_index=p.index%2

# Rigid segment bone rig. All pieces are editable mesh objects with one bone group each.
bpy.ops.object.armature_add(enter_editmode=True, location=(0,0,0))
rig=bpy.context.object; rig.name='Swordsman_Rig'
arm=rig.data; arm.name='Swordsman_RigidSegmentRig'
arm.edit_bones.remove(arm.edit_bones[0])
def bone(name,head,tail,parent=None):
    b=arm.edit_bones.new(name); b.head=head; b.tail=tail
    if parent: b.parent=arm.edit_bones[parent]
bone('root',(0,0,1.15),(0,0,1.40))
bone('spine',(0,0,1.4),(0,0,2.15),'root')
bone('head',(0,0,2.15),(0,0,2.85),'spine')
for side,sign in [('L',-1),('R',1)]:
    bone('thigh.'+side,(sign*.205,0,1.25),(sign*.205,-.012,.76),'root')
    bone('shin.'+side,(sign*.205,-.012,.76),(sign*.205,.02,.15),'thigh.'+side)
    bone('upper_arm.'+side,shoulders[side],elbows[side],'spine')
    bone('forearm.'+side,elbows[side],wrists[side],'upper_arm.'+side)
    bone('hand.'+side,wrists[side],Vector(wrists[side])+Vector((0,0,-.18)),'forearm.'+side)
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front=True
for ob in parts:
    if ob.name not in bindings: continue
    group=ob.vertex_groups.new(name=bindings[ob.name]); group.add(list(range(len(ob.data.vertices))),1,'REPLACE')
    modifier=ob.modifiers.new('Rigid bone attachment','ARMATURE'); modifier.object=rig
    ob.parent=rig
for p in rig.pose.bones: p.rotation_mode='XYZ'

def pose(frame, values, bob=0):
    for p in rig.pose.bones:
        p.rotation_euler=(0,0,0); p.location=(0,0,0)
    for name,angles in values.items(): rig.pose.bones[name].rotation_euler=[math.radians(a) for a in angles]
    rig.pose.bones['root'].location.y=bob
    for p in rig.pose.bones:
        p.keyframe_insert('rotation_euler',frame=frame)
        p.keyframe_insert('location',frame=frame)

ready={'upper_arm.R':(0,-12,-12),'forearm.R':(10,0,0),'upper_arm.L':(8,0,8),'thigh.L':(-5,0,0),'thigh.R':(5,0,0)}
pose(1,ready)
pose(9,{**ready,'spine':(0,-15,-12),'upper_arm.R':(-70,-30,-35),'forearm.R':(-25,0,0),'hand.R':(0,0,-15),'upper_arm.L':(20,0,20)},-.035)
pose(14,{**ready,'spine':(6,20,15),'upper_arm.R':(75,20,35),'forearm.R':(20,0,15),'hand.R':(10,0,20),'upper_arm.L':(-20,0,10)},-.09)
pose(21,{**ready,'spine':(3,10,10),'upper_arm.R':(55,15,25),'forearm.R':(10,0,10)},-.06)
pose(32,ready)
if rig.animation_data and rig.animation_data.action: rig.animation_data.action.name='Attack_01_Blockout'

scene=bpy.context.scene; scene.frame_start=1; scene.frame_end=32; scene.render.fps=24
scene.frame_set(1)
scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.world.color=(.22,.22,.22)
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.20,.17,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.55
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.image_settings.color_mode='RGBA'

bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.002))
floor=bpy.context.object; floor.name='Studio Ground (not exported)'; floor.data.materials.append(ground)
def track(ob,point): ob.rotation_euler=(Vector(point)-ob.location).to_track_quat('-Z','Y').to_euler()
def area(name,loc,power,size,color):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size; data.color=color
    ob=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(ob); ob.location=loc; track(ob,(0,0,1.4))
area('Key softbox',(-3,-4,6),450,4,(1,.85,.68))
area('Fill softbox',(4,-1,3),230,3,(.73,.84,1))
area('Rim softbox',(1,4,5),500,3,(1,.89,.72))
bpy.ops.object.camera_add(location=(4,-7,4.1))
cam=bpy.context.object; cam.name='Portrait_Camera'; cam.data.type='ORTHO'; cam.data.ortho_scale=3.9
track(cam,(0,0,1.46)); scene.camera=cam
scene.render.resolution_x=1800; scene.render.resolution_y=2048; scene.render.resolution_percentage=100
scene.render.filepath=os.path.join(OUT,'swordsman-3d-portrait.png'); bpy.ops.render.render(write_still=True)

# Alpha render preserves the actual mesh appearance, no generated-image repainting.
floor.hide_render=True; scene.render.film_transparent=True
scene.render.filepath=os.path.join(OUT,'swordsman-3d-alpha.png'); bpy.ops.render.render(write_still=True)
floor.hide_render=False; scene.render.film_transparent=False
cam.location=(3,-6,7.5); track(cam,(0,0,1.3)); cam.data.ortho_scale=4.1
scene.render.resolution_x=1200; scene.render.resolution_y=1200
scene.render.filepath=os.path.join(OUT,'swordsman-3d-game-angle.png'); bpy.ops.render.render(write_still=True)

# Export only the character and skeletal clip, not the render studio.
bpy.ops.object.select_all(action='DESELECT')
for ob in parts: ob.select_set(True)
rig.select_set(True); bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'swordsman-3d.glb'),use_selection=True,export_format='GLB',export_animations=True)
report={'blender':bpy.app.version_string,'mesh_objects':len(parts),'vertices':sum(len(o.data.vertices) for o in parts),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts),'bones':len(rig.data.bones),'animation':'Attack_01_Blockout, 32 frames / 24 fps','rig':'One rigid bone weight per mesh segment; no smooth skin or facial rig','portrait_pixels':[1800,2048],'source':'Procedurally authored Blender meshes and materials; no generated render enhancement'}
with open(os.path.join(OUT,'model-report.json'),'w',encoding='utf8') as f: json.dump(report,f,indent=2)
scene.render.resolution_x=640; scene.render.resolution_y=640
scene.cycles.samples=12
scene.render.filepath=os.path.join(OUT,'frames','attack-')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'swordsman-3d.blend'))
bpy.ops.render.render(animation=True)
print('SWORDSMAN_COMPLETE '+json.dumps(report))
