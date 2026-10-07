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
import runpy
profile=runpy.run_path(os.path.join(os.path.dirname(__file__),'profile.py'))

def photo(x,v):return ((x-750)*.0625/610,.05985-(v-194)*.1197/1144)
radius=profile['radius']
height=profile['height']

# Constrained planar triangulation preserves every aperture and panel seam.
# Subdivide before projecting: no intersecting 3D Boolean cutters are used.
import bmesh
surface=json.load(open(os.path.join(os.path.dirname(__file__),'mouse_surface.json')))
vs=[(*photo(x,y),0) for x,y in surface['vertices']]
shell=mesh('Continuous curved blue shell',vs,surface['faces'],blue)
bm=bmesh.new();bm.from_mesh(shell.data)
for iteration in range(6):
 edges=[e for e in bm.edges if e.calc_length()>.0020]
 if not edges:break
 bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
bmesh.ops.triangulate(bm,faces=list(bm.faces))
for v in bm.verts:v.co.z=height(v.co.x,v.co.y)
# A clean planar surface is extruded explicitly; no rim Boolean or bevel can
# create overlapping faces around the thin, densely sampled apertures.
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00000005)
bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.00000001)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bmesh.ops.reverse_faces(bm,faces=[f for f in bm.faces if f.normal.z<0])
bm.verts.ensure_lookup_table();bm.verts.index_update()
vs=[tuple(v.co) for v in bm.verts];count=len(vs)
fs=[tuple(v.index for v in f.verts) for f in bm.faces];top_count=len(fs)
boundaries=[]
for edge in bm.edges:
 if len(edge.link_faces)==1:
  loop=edge.link_loops[0];boundaries.append((loop.vert.index,loop.link_loop_next.vert.index))
vs += [(x,y,z-.00055) for x,y,z in vs]
fs += [tuple(i+count for i in reversed(f)) for f in fs[:top_count]]
fs += [(b,a,a+count,b+count) for a,b in boundaries]
bm.free()
new=bpy.data.meshes.new('Watertight curved shell');new.from_pydata(vs,[],fs);new.update();shell.data=new
new.materials.append(blue);new.materials.append(mat('Blue aperture walls',(.005,.10,.15),.4,.36));new.materials.append(dark)
for i,f in enumerate(new.polygons):
 f.material_index=0 if i<top_count else (2 if i<top_count*2 else 1)
 f.use_smooth=i<top_count*2
normals=[];eps=.000001
for f in new.polygons:
 for li in f.loop_indices:
  v=new.vertices[new.loops[li].vertex_index].co
  if f.material_index==0:
   # Extend the analytic roof for differentiation. Clamping at the perimeter
   # halved its derivative and produced triangular highlight dents on the rim.
   dx=(height(v.x+eps,v.y,False)-height(v.x-eps,v.y,False))/(2*eps)
   dy=(height(v.x,v.y+eps,False)-height(v.x,v.y-eps,False))/(2*eps)
   normals.append(tuple(Vector((-dx,-dy,1)).normalized()))
  else:normals.append(tuple(f.normal))
new.normals_split_custom_set(normals)
check=bmesh.new();check.from_mesh(new)
assert all(e.is_manifold for e in check.edges),'Shell closure failed'
check.free()

# Recessed bottom body, entirely inside the shell silhouette.
verts=[];faces=[];n=384
for z in [.001,.0027]:
 for i in range(n):
  a=2*math.pi*i/n;y=.0588*math.sin(a);x=radius(y)*math.cos(a)/max(.001,abs(math.cos(a))) if abs(math.cos(a))>.0001 else 0
  verts.append((x*.96,y,z))
faces.extend([tuple(reversed(range(n))),tuple(range(n,2*n))])
for i in range(n):faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
base=mesh('Low enclosed underside',verts,faces,dark);bevel(base,.00035)
# The roof ends above a short grip skirt instead of descending to the desk.
outline=[photo(750-profile['sample'](v,1),v) for v in range(194,1338,3)]
outline += [photo(750,1338)]
outline += [photo(750+profile['sample'](v,1),v) for v in reversed(range(194,1338,3))]
vs=[];fs=[];n=len(outline)
for inward,bottom in [(False,False),(False,True),(True,True),(True,False)]:
 for x,y in outline:
  h=height(x,y)-.0004
  vs.append((x*(.975 if inward else .997),y*(.990 if inward else .999),.0028 if bottom else h))
for k in range(4):
 for i in range(n):
  j=(i+1)%n;kn=(k+1)%4
  fs.append((kn*n+i,kn*n+j,k*n+j,k*n+i))
skirt=mesh('Rounded lower grip skirt',vs,fs,blue)
for f in skirt.data.polygons:f.use_smooth=True
skirt.data.set_sharp_from_angle(angle=.6)
# No top DPI button. Only a transverse scroll wheel and two left thumb buttons.
# Dense rounded rubber barrel with geometric knurling instead of box-shaped ribs.
vs=[];fs=[];around=128;across=16
for i in range(across+1):
 x=-.00375+.0075*i/across
 edge=min(1,max(0,(.00375-abs(x))/.00045))
 for j in range(around):
  a=2*math.pi*j/around
  grip=.00017*max(0,math.cos(a*32))*max(0,math.cos(2*math.pi*i/4))
  r=.00885+.00035*math.sin(edge*math.pi/2)+grip
  vs.append((x,.0358+r*math.sin(a),.0238+r*math.cos(a)))
for i in range(across):
 for j in range(around):
  k=i*around+j;n=i*around+(j+1)%around
  fs.append((k,n,n+around,k+around))
fs.extend([tuple(reversed(range(around))),tuple(range(across*around,(across+1)*around))])
wheel=mesh('Single rubber scroll wheel',vs,fs,rubber)
for f in wheel.data.polygons:f.use_smooth=len(f.vertices)==4
wheel.data.set_sharp_from_angle(angle=.6)
for y in [.001,.016]:
 # Flush into the side wall; long bevelled surfaces instead of protruding blocks.
 o=box('Left thumb button',(-radius(y)*.988,y,.017),(.0020,.0135,.0038),blue)

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
 for p,(x,y) in zip(s.points,samples):
  hit,point,normal,index=shell.ray_cast(Vector((x,y,.1)),Vector((0,0,-1)))
  p.co=(x,y,(point.z if hit else height(x,y))+.00011,1)
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
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=64;scene.cycles.use_denoising=True;scene.world.color=(.12,.12,.12)
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
cam.location=(-.30,0,.043);cam.rotation_euler=(Vector((0,0,.020))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_y=750;d.ortho_scale=.15
scene.render.filepath=os.path.join(PREVIEW,'mouse-side.png');bpy.ops.render.render(write_still=True)
scene.render.resolution_y=1200;d.ortho_scale=.155
print('MOUSE_CLEAN_EXPORTED')

# An unperforated form study makes dents and silhouette discontinuities visible.
for o in parts:
 if o.name!='Studio floor':o.hide_render=True
vs=[];fs=[];rows=360;cols=160
for j in range(rows+1):
 y=-.05985+.1197*j/rows
 for i in range(cols+1):
  x=-radius(y)*math.cos(math.pi*i/cols)
  vs.append((x,y,height(x,y)))
for j in range(rows):
 for i in range(cols):
  k=j*(cols+1)+i;fs.append((k,k+1,k+cols+2,k+cols+1))
study=mesh('Unperforated form study',vs,fs,blue)
for f in study.data.polygons:f.use_smooth=True
cam.location=(-.13,-.19,.20);cam.rotation_euler=(Vector((0,0,.018))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=os.path.join(PREVIEW,'mouse-form-check.png');bpy.ops.render.render(write_still=True)
