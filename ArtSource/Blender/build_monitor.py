"""Straight hard-surface monitor, XL2540X+ inspired; screen remains a separate plane."""
import bpy, math, os
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/Art/ReferenceProps/Monitor');PREVIEW=os.path.join(ROOT,'ArtSource/Blender/Models')
os.makedirs(OUT,exist_ok=True);os.makedirs(PREVIEW,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
parts=[]
def mat(name,color,metal=0,rough=.4):
 m=bpy.data.materials.new(name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
 p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;return m
black=mat('Monitor graphite',(.012,.015,.019),.18,.34)
bezel=mat('Monitor soft black bezel',(.006,.008,.011),.05,.43)
stand=mat('Monitor satin stand',(.025,.028,.034),.65,.28)
red=mat('Stand red accent',(.38,.006,.012),.32,.32)
screen=mat('Dark display glass',(.004,.008,.012),.2,.22)
def box(name,loc,size,material,r=.001):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.data.materials.append(material);m=o.modifiers.new('Small machined bevel','BEVEL');m.width=r;m.segments=3
 bpy.ops.object.modifier_apply(modifier=m.name)
 for p in o.data.polygons:p.use_smooth=True
 m=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');m.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=m.name)
 parts.append(o);return o
# Front faces negative Y; vertical axis Z in Blender.
box('Monitor housing',(0,.016,.337),(.574,.047,.340),black,.004)
box('Rear housing',(0,.047,.338),(.455,.036,.258),black,.010)
box('Bottom bezel',(0,-.010,.175),(.562,.013,.016),bezel,.0007)
for x in [-.281,.281]:box('Side bezel',(x,-.010,.342),(.007,.013,.319),bezel,.0006)
box('Top bezel',(0,-.010,.502),(.568,.013,.007),bezel,.0006)
box('Screen glass',(0,-.014,.340),(.548,.002,.30825),screen,.0002)
def cylinder(name,loc,radius,depth,material):
 bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=radius,depth=depth,location=loc)
 o=bpy.context.object;o.name=name;o.data.materials.append(material)
 for f in o.data.polygons:f.use_smooth=len(f.vertices)==4
 m=o.modifiers.new('Fine rim','BEVEL');m.width=.0008;m.segments=3;bpy.ops.object.modifier_apply(modifier=m.name)
 o.data.set_sharp_from_angle(angle=.6);parts.append(o);return o
cylinder('Height adjustment column',(0,.07,.201),.023,.371,stand)
box('Column slot',(0,.046,.236),(.008,.001,.240),bezel,.0004)
cylinder('Red column trim',(0,.070,.024),.024,.004,red)
box('Small flat base',(0,.035,.009),(.221,.179,.018),stand,.006)
cylinder('Base swivel disc',(0,.07,.0188),.041,.0015,stand)
box('Rear hinge',(0,.079,.34),(.072,.04,.068),stand,.005)
for i in range(15):box('Rear ventilation',(i*.015-.105,.066,.430),(.006,.001,.018),bezel,.0004)
box('Rear port recess',(.095,.066,.240),(.122,.001,.031),bezel,.002)
for x in [.048,.073,.098,.123]:box('Port',(x,.067,.240),(.016,.001,.007),stand,.0004)
box('Power LED',(.244,-.017,.175),(.002,.0004,.001),mat('Power light',(.45,.68,.8),0,.3),.0001)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Monitor_Clean.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,mesh_smooth_type='FACE',bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PREVIEW,'Monitor_Clean.blend'))
print('MONITOR_CLEAN_EXPORTED')

scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.world.color=(.12,.12,.12)
box('Studio floor',(0,0,-.008),(200,200,.01),mat('Studio floor',(.035,.045,.06),0,.5),.0001)
for loc,power,size in [((-.5,-.6,1.1),25,.7),((.6,.1,.8),30,.6),((-.2,.6,.8),15,.5)]:
 d=bpy.data.lights.new('Softbox','AREA');d.energy=power;d.size=size;o=bpy.data.objects.new('Softbox',d);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.27))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',d);bpy.context.collection.objects.link(cam);cam.location=(.7,-1.4,.8);cam.rotation_euler=(Vector((0,0,.26))-cam.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=.78;scene.camera=cam
scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.render.filepath=os.path.join(PREVIEW,'monitor-clean.png');bpy.ops.render.render(write_still=True)
