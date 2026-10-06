"""Clean blue mouse with intersecting hexagonal ribs, not honeycomb holes.
The lattice is a geometric approximation of the supplied ATK reference.
"""
import bpy, math, os, json
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/Art/ReferenceProps/Mouse');PREVIEW=os.path.join(ROOT,'ArtSource/Blender/Models')
os.makedirs(OUT,exist_ok=True);os.makedirs(PREVIEW,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
parts=[]
def mat(name,color,metal=0,rough=.4):
 m=bpy.data.materials.new(name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
 p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 return m
blue=mat('Satin metallic blue',(.008,.21,.29),.48,.32)
rubber=mat('Scroll rubber',(.009,.012,.016),0,.63)
dark=mat('Internal chassis',(.006,.009,.012),.15,.5)
accent=mat('Recessed blue engraving',(.004,.038,.059),.05,.65)
def mesh(name,vs,fs,material):
 m=bpy.data.meshes.new(name);m.from_pydata(vs,[],fs);m.update();o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o)
 o.data.materials.append(material);parts.append(o);return o
def apply(o,mod):
 bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
def bevel(o,width=.00015):
 m=o.modifiers.new('Fine edge bevel','BEVEL');m.width=width;m.segments=2;m.limit_method='ANGLE';apply(o,m)
 for p in o.data.polygons:p.use_smooth=True
 m=o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL');m.keep_sharp=True;apply(o,m)
def box(name,loc,size,material):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(material);parts.append(o);bevel(o,.0005);return o
# Measured top-view outline and side-profile stations (metres).
stations=[(194,45,.020),(225,94,.021),(260,185,.0215),(300,275,.023),
 (340,299,.024),(430,297,.028),(550,288,.032),(690,281,.0355),
 (780,280,.0373),(875,285,.0368),(980,299,.0338),(1070,301,.029),
 (1140,277,.024),(1200,239,.017),(1260,180,.0105),(1300,120,.006),(1338,1,.0025)]
def lerp_station(v,index):
 for a,b in zip(stations,stations[1:]):
  if v<=b[0]:
   t=max(0,min(1,(v-a[0])/(b[0]-a[0])))
   return a[index]*(1-t)+b[index]*t
 return stations[-1][index]
def photo(x,v):return ((x-750)*.0625/610,.05985-(v-194)*.1197/1144)
def radius(y):return lerp_station(194+(.05985-y)*1144/.1197,1)*.0625/610
def height(x,y):
 v=194+(.05985-y)*1144/.1197;r=max(.0001,radius(y))
 centre=lerp_station(v,2)
 return .0025+(centre-.0025)*max(0,1-(x/r)**2)**.37

# Constrained planar triangulation preserves every aperture and panel seam.
# Subdivide before projecting: no intersecting 3D Boolean cutters are used.
import bmesh
surface=json.load(open(os.path.join(os.path.dirname(__file__),'mouse_surface.json')))
vs=[(*photo(x,y),0) for x,y in surface['vertices']]
shell=mesh('Continuous curved blue shell',vs,surface['faces'],blue)
bm=bmesh.new();bm.from_mesh(shell.data)
for iteration in range(6):
 edges=[e for e in bm.edges if e.calc_length()>.0016]
 if not edges:break
 bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
bmesh.ops.triangulate(bm,faces=list(bm.faces))
for v in bm.verts:v.co.z=height(v.co.x,v.co.y)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(shell.data);bm.free();shell.data.update()
# Ensure outward-facing top normals for disconnected click/palm panels.
for face in shell.data.polygons:
 if face.normal.z<0:face.flip()
shell.data.materials.append(mat('Blue aperture walls',(.005,.10,.15),.4,.36))
solid=shell.modifiers.new('Shell wall thickness','SOLIDIFY');solid.thickness=.00055;solid.offset=-1;solid.use_even_offset=False;solid.thickness_clamp=.3;solid.material_offset_rim=1;apply(shell,solid)
for f in shell.data.polygons:f.use_smooth=True
# Tiny bevel only on sharp hole boundaries, never across the curved surface.
m=shell.modifiers.new('Crisp aperture rims','BEVEL');m.width=.00007;m.segments=2;m.limit_method='ANGLE';m.angle_limit=.7;apply(shell,m)
shell.data.set_sharp_from_angle(angle=.55)
normals=[];eps=.000008
for f in shell.data.polygons:
 for li in f.loop_indices:
  v=shell.data.vertices[shell.data.loops[li].vertex_index].co
  if f.material_index==0 and f.normal.z>0 and abs(v.z-height(v.x,v.y))<.00012:
   dx=(height(v.x+eps,v.y)-height(v.x-eps,v.y))/(2*eps)
   dy=(height(v.x,v.y+eps)-height(v.x,v.y-eps))/(2*eps)
   normals.append(tuple(Vector((-dx,-dy,1)).normalized()))
  else:normals.append(tuple(f.normal))
shell.data.normals_split_custom_set(normals)

# Recessed bottom body, entirely inside the shell silhouette.
verts=[];faces=[];n=96
for z in [.001,.0027]:
 for i in range(n):
  a=2*math.pi*i/n;y=.0588*math.sin(a);x=radius(y)*math.cos(a)/max(.001,abs(math.cos(a))) if abs(math.cos(a))>.0001 else 0
  verts.append((x*.96,y,z))
faces.extend([tuple(reversed(range(n))),tuple(range(n,2*n))])
for i in range(n):faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
base=mesh('Low enclosed underside',verts,faces,dark);bevel(base,.00035)
# No top DPI button. Only a transverse scroll wheel and two left thumb buttons.
bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=.0092,depth=.0075,location=(0,.0358,.0235),rotation=(0,math.pi/2,0))
wheel=bpy.context.object;wheel.name='Single rubber scroll wheel';wheel.data.materials.append(rubber);parts.append(wheel);bevel(wheel,.0002)
for i in range(64):
 a=2*math.pi*i/64
 o=box('Scroll grip rib',(0,.0358+.00925*math.sin(a),.0235+.00925*math.cos(a)),(.0072,.00042,.0003),rubber)
 o.rotation_euler.x=-a
for y in [.001,.016]:
 o=box('Left thumb button',(-.0287,y,.013),(.0023,.0135,.0040),blue)

# Fine blue-on-blue panel lines, deliberately restrained rather than painted fake holes.
def line(name,points):
 # Dense surface projection prevents straight segments floating above the curved shell.
 samples=[]
 for a,b in zip(points,points[1:]):
  for i in range(12):
   t=i/12;y=a[1]*(1-t)+b[1]*t
   x=a[0]*(1-t)+b[0]*t;x=max(-radius(y)*.94,min(radius(y)*.94,x))
   samples.append((x,y))
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.bevel_depth=.000075;c.bevel_resolution=1;c.resolution_u=1
 s=c.splines.new('POLY');s.points.add(len(samples)-1)
 for p,(x,y) in zip(s.points,samples):p.co=(x,y,height(x,y)-.00002,1)
 o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);o.data.materials.append(accent)
 bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o.select_set(False);parts.append(o)
# Trace the bilateral dragonfly motif behind the wheel from the supplied top photo.
# These are thin recessed-color lines following the surface, never a raised button.
def photo_points(points,sign):
 return [(sign*(x-750)/610*.0625,.0599-(y-194)/1144*.1198) for x,y in points]
motif=[
 [(799,213),(806,305),(818,406),(837,446),(889,466),(879,500),(847,517),(824,543),(785,594),(772,587),(764,552),(751,531)],
 [(917,271),(909,347),(908,420),(912,471),(918,497),(960,517),(990,476),(1008,415),(1021,346)],
 [(934,279),(932,349),(935,428),(946,481),(985,512)],
 [(837,219),(841,306),(847,388),(860,426),(888,454),(900,302),(901,260)],
 [(825,546),(861,611),(826,641),(809,666),(796,775)],
 [(869,612),(844,639),(827,665),(814,702),(805,774)],
 [(759,573),(779,614),(766,645),(756,656)],
 [(795,638),(777,689),(769,774)],
 [(968,520),(950,558),(965,564),(982,526),(968,520)],
 [(880,463),(870,497),(884,498),(895,465),(880,463)],
 [(913,267),(912,297),(920,303),(923,271)],
 [(797,200),(798,246),(818,254),(817,216)]
]
for sign in [-1,1]:
 for i,points in enumerate(motif):line('Dragonfly engraving '+str(sign)+' '+str(i),photo_points(points,sign))

bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=shell
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Mouse_Clean.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,mesh_smooth_type='FACE',bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PREVIEW,'Mouse_Clean.blend'))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.world.color=(.12,.12,.12)
floor=mat('Studio floor',(.023,.031,.043),.1,.5)
box('Studio floor',(0,0,-.003),(2,2,.004),floor)
def area(loc,power,size,color):
 d=bpy.data.lights.new('Studio softbox','AREA');d.energy=power;d.shape='RECTANGLE';d.size=size;d.size_y=size*.4;d.color=color
 o=bpy.data.objects.new('Studio softbox',d);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.018))-o.location).to_track_quat('-Z','Y').to_euler()
area((-.12,.12,.20),1.0,.15,(.85,.94,1));area((.10,-.10,.14),1.6,.14,(.6,.83,1));area((.09,.10,.18),.7,.10,(1,.85,.68))
d=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',d);bpy.context.collection.objects.link(cam)
cam.location=(-.13,-.19,.20);cam.rotation_euler=(Vector((0,0,.018))-cam.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=.155;scene.camera=cam
scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
scene.render.filepath=os.path.join(PREVIEW,'mouse-clean.png');bpy.ops.render.render(write_still=True)
cam.location=(0,0,.3);cam.rotation_euler=(0,0,0);scene.render.filepath=os.path.join(PREVIEW,'mouse-top.png');bpy.ops.render.render(write_still=True)
print('MOUSE_CLEAN_EXPORTED')
