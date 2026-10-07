"""XL2540X+ reference prop with a separate screen for the interactive Unity OS.
Reference: https://zowie.benq.com/en-us/monitor/xl2540x-plus.html
"""
import bpy,bmesh,math,os
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/Art/ReferenceProps/Monitor');PREVIEW=os.path.join(ROOT,'ArtSource/Blender/Models')
os.makedirs(OUT,exist_ok=True);bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials):
 if m.users==0:bpy.data.materials.remove(m)
parts=[]
def mat(name,c,metal=0,rough=.4):
 m=bpy.data.materials.new(name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;return m
black=mat('Monitor graphite',(.024,.026,.029),.10,.40);bezel=mat('Monitor soft black bezel',(.012,.014,.017),.03,.48)
stand=mat('Monitor satin stand',(.034,.037,.042),.42,.36);red=mat('Stand red accent',(.38,.008,.014),.18,.38)
screen=mat('Dark display glass',(.003,.006,.011),.03,.27);mark=mat('Monitor etched markings',(.22,.23,.24),.15,.5)
def finish(o,ma,r=0):
 o.data.materials.append(ma);parts.append(o);bpy.context.view_layer.objects.active=o
 if r:
  m=o.modifiers.new('Manufactured edge radius','BEVEL');m.width=r;m.segments=3;bpy.ops.object.modifier_apply(modifier=m.name)
 for f in o.data.polygons:f.use_smooth=True
 m=o.modifiers.new('Face weighted normals','WEIGHTED_NORMAL');m.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=m.name);return o
def mesh(name,vs,fs,ma,r=0):
 m=bpy.data.meshes.new(name);m.from_pydata(vs,[],fs);m.update();bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
 o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o);return finish(o,ma,r)
def box(name,loc,size,ma,r=.001):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,ma,r)
def cyl(name,loc,r,d,ma,rot=(0,0,0)):
 bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=r,depth=d,location=loc,rotation=rot);o=bpy.context.object;o.name=name;return finish(o,ma,.0004)
def rounded(w,h,r):
 out=[]
 for x,z,a in [(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
  for i in range(11):
   t=math.radians(a+i*9);out.append((x+r*math.cos(t),z+r*math.sin(t)))
 return out
def rings(name,profiles,ma,cap=True):
 vs=[(x,y,z) for y,ring in profiles for x,z in ring];n=len(profiles[0][1]);fs=[]
 for j in range(len(profiles)-1):
  for i in range(n):fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 if cap:fs += [tuple(reversed(range(n))),tuple(range((len(profiles)-1)*n,len(profiles)*n))]
 return mesh(name,vs,fs,ma)
Z=.3000;W=.57615;H=.3418
outer=rounded(W,H,.0025);inner=rounded(.5375,.3002,.001)
profiles=[(-.012,[(x,z+Z) for x,z in outer]),(-.0108,[(x,z+Z+.0034) for x,z in inner]),(.013,[(x,z+Z+.0034) for x,z in inner]),(.014,[(x,z+Z) for x,z in outer]),(-.012,[(x,z+Z) for x,z in outer])]
# Close the continuous bezel by merging its duplicate seam before normal repair.
housing=rings('Monitor housing',profiles,bezel,False)
bm=bmesh.new();bm.from_mesh(housing.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(housing.data);bm.free()
for f in housing.data.polygons:f.use_smooth=False
profiles=[]
for w,h,r,y in [(W-.001,H-.001,.003,.012),(W-.003,H-.003,.004,.019),(.558,.323,.010,.029),(.510,.287,.030,.043),(.380,.243,.055,.056),(.215,.174,.068,.064),(.160,.130,.051,.066)]:
 profiles.append((y,[(x,z+Z) for x,z in rounded(w,h,r)]))
rings('Sculpted rear housing',profiles,black)
box('Screen glass',(0,-.0096,Z+.0034),(.53568,.001,.29808),screen,.0001)
for sign in [-1,1]:
 yz=[(-.010,Z+H/2),(-.109,Z+H/2),(-.117,Z+H/2-.010),(-.117,Z-H/2+.048),(-.112,Z-H/2+.030),(-.011,Z-H/2+.006)]
 vs=[(sign*(W/2+.002+(-y-.010)*.16)+d,y,z) for d in [-.0014,.0014] for y,z in yz];n=len(yz)
 fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh(('Left' if sign<0 else 'Right')+' removable light shield',vs,fs,black,.003)
 for z in [Z-H/2+.025,Z+H/2-.025]:box('Shield hinge',(sign*(W/2+.001),-.008,z),(.007,.014,.024),stand,.0014)
foot=[(-.091,-.066),(.091,-.066),(.117,-.046),(.116,.045),(.088,.082),(.039,.103),(-.039,.103),(-.088,.082),(-.116,.045),(-.117,-.046)]
vs=[(x,y,z) for z in [.002,.015] for x,y in foot];n=len(foot)
fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
mesh('Compact rounded swivel base',vs,fs,stand,.006);cyl('Base swivel disc',(0,.044,.0155),.041,.0014,black)
def column(name,z,length,w,d,ma):
 o=box(name,(0,.043+z*.135,z),(w,d,length),ma,min(w,d)*.38);o.rotation_euler.x=-math.atan(.135);return o
column('Inner height rail',.111,.195,.043,.027,stand);column('Upper height column',.294,.292,.060,.035,black)
column('Red column trim',.021,.002,.047,.030,red);column('Height lock collar',.157,.012,.061,.037,stand)
for i in range(16):
 z=.038+i*.008;box('Height graduation',(.022,.043+z*.135-.008,z),(.0004,.009 if i%5==0 else .004,.00065),mark,.0001)
box('Carry grip left',(-.020,.102,.445),(.019,.033,.019),black,.005);box('Carry grip right',(.020,.102,.445),(.019,.033,.019),black,.005)
box('Carry grip bridge',(0,.102,.457),(.059,.033,.009),black,.004)
cyl('Rear tilt mount',(0,.067,Z),.066,.005,black,(math.pi/2,0,0));cyl('Tilt pivot',(0,.073,Z),.028,.012,stand,(math.pi/2,0,0))
box('Hinge arm',(0,.085,Z),(.043,.041,.027),stand,.006)
box('Rear connector inset',(0,.049,Z-.112),(.268,.004,.025),bezel,.010)
for x in [.056,.079,.102]:box('HDMI socket',(x,.052,Z-.112),(.015,.002,.0055),mark,.001)
box('DisplayPort socket',(.129,.052,Z-.112),(.014,.002,.006),mark,.0006);box('Power inlet',(-.084,.052,Z-.112),(.022,.003,.013),bezel,.002)
for x in [-.228,.228]:
 for i in range(18):box('Edge ventilation',(x,.028,Z-.120+i*.0035),(.012,.001,.001),bezel,.00025)
for x in [-.211,-.191,-.171]:cyl('Rear control',(x,.031,Z-.151),.004,.004,bezel,(math.pi/2,0,0))
box('Power LED',(.250,-.0126,Z-H/2+.009),(.0022,.0005,.0011),mat('Power light',(.32,.52,.6)),.0002)
for i in range(-9,10):
 a=math.radians(i*5);o=box('Swivel graduation',(.050*math.sin(a),.044-.050*math.cos(a),.017),(.0006,.0025 if i%3 else .004,.0003),mark,.0001);o.rotation_euler.z=-a
cyl('S Switch body',(.157,-.019,.010),.029,.019,black);cyl('S Switch top',(.157,-.019,.020),.026,.001,bezel)
for x,y in [(.145,-.031),(.157,-.035),(.170,-.030)]:cyl('S Switch key',(x,y,.0215),.004,.002,stand)
cyl('S Switch red wheel',(.156,-.006,.022),.005,.008,red,(0,math.pi/2,0))
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Monitor_Clean.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,mesh_smooth_type='FACE',bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PREVIEW,'Monitor_Clean.blend'));print('MONITOR_CLEAN_EXPORTED')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True;scene.world.color=(.10,.10,.10)
box('Studio floor',(0,0,-.006),(20,20,.01),mat('Studio floor',(.025,.033,.045),0,.5))
for loc,power,size in [((-.5,-.6,1.1),22,.7),((.6,.1,.8),25,.6),((-.2,.6,.8),20,.5)]:
 d=bpy.data.lights.new('Softbox','AREA');d.energy=power;d.size=size;o=bpy.data.objects.new('Softbox',d);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.25))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',d);bpy.context.collection.objects.link(cam);d.type='ORTHO';d.ortho_scale=.76;scene.camera=cam
scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
for name,loc in [('monitor-clean',(.65,-1.4,.74)),('monitor-front',(0,-1.5,.32)),('monitor-rear',(.75,1.3,.70))]:
 cam.location=loc;cam.rotation_euler=(Vector((0,.01,.25))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=os.path.join(PREVIEW,name+'.png');bpy.ops.render.render(write_still=True)
