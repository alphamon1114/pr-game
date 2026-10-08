"""Bedroom layout from the user's drawing. Units are metres in Unity coordinates.
The export is rotated 180 degrees in Unity to cancel FBX handedness conversion.
Furniture is original procedural geometry; PBR maps are CC0 from Poly Haven.
"""
import bpy,bmesh,math,os,json,random
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/Art/Room');os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials):
 if not m.users:bpy.data.materials.remove(m)
parts=[];materials={};random.seed(21)
def U(p):return Vector((p[0],p[2],p[1]))
def mat(name,c,rough=.5,metal=0,tex=None,scale=1,emission=0):
 m=bpy.data.materials.new('Room '+name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
 p.inputs['Base Color'].default_value=(*c,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
 if emission:p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=emission
 if tex:
  for key,socket in [('albedo','Base Color'),('roughness','Roughness'),('normal','Normal')]:
   path=os.path.join(OUT,'Textures',tex,key+'.jpg');t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(path,check_existing=True)
   if key!='albedo':t.image.colorspace_settings.name='Non-Color'
   if key=='normal':
    n=m.node_tree.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.35;m.node_tree.links.new(t.outputs['Color'],n.inputs['Color']);m.node_tree.links.new(n.outputs['Normal'],p.inputs[socket])
   else:m.node_tree.links.new(t.outputs['Color'],p.inputs[socket])
 materials[m.name]={'color':list(c),'roughness':rough,'metallic':metal,'texture':tex,'emission':emission,'uvScale':scale};return m
wall=mat('Wall paint',(.42,.45,.47),.86,tex='white_plaster_02',scale=.7)
floor=mat('Oak floor',(.55,.49,.42),.65,tex='wood_floor',scale=1/1.7)
wood=mat('Desk walnut',(.42,.31,.22),.46,tex='wood_table_001',scale=.7)
ward=mat('Wardrobe paint',(.24,.28,.29),.62);black=mat('Graphite',(.035,.043,.052),.43,.15)
steel=mat('Brushed steel',(.23,.26,.29),.31,.76);rubber=mat('Rubber',(.012,.017,.022),.77)
linen=mat('Bed linen',(.21,.29,.34),.9,tex='fabric_pattern_07',scale=3.5)
cream=mat('Pillow fabric',(.59,.60,.55),.87,tex='fabric_pattern_07',scale=4)
curtain=mat('Curtain cloth',(.12,.17,.23),.89,tex='fabric_pattern_07',scale=3)
fabric=mat('Chair fabric',(.055,.062,.068),.82,tex='fabric_pattern_07',scale=5)
meshmat=mat('Chair mesh',(.035,.044,.053),.80)
matpad=mat('Desk mat',(.042,.075,.087),.90,tex='fabric_pattern_07',scale=4)
ivory=mat('Fridge enamel',(.59,.60,.55),.33,.17);paper=mat('Paper',(.61,.58,.48),.83)
blue=mat('Blue book',(.085,.23,.32),.63);ochre=mat('Ochre book',(.41,.24,.09),.64)
darkglass=mat('Night glass',(.025,.051,.082),.2,.4);amber=mat('Warm diffuser',(.9,.52,.20),.3,emission=1)
cold=mat('PC indicator',(.10,.34,.42),.3,emission=.7)
def uv(o,ma):
 if o.type!='MESH':return
 data=o.data;layer=data.uv_layers.active or data.uv_layers.new();scale=materials[ma.name]['uvScale']
 for f in data.polygons:
  axis=max(range(3),key=lambda k:abs(f.normal[k]));a,b=([1,2] if axis==0 else [0,2] if axis==1 else [0,1])
  for li in f.loop_indices:
   p=o.matrix_world@data.vertices[data.loops[li].vertex_index].co;layer.data[li].uv=(p[a]*scale,p[b]*scale)
def finish(o,name,ma,r=0):
 o.name=name;o.data.materials.append(ma);parts.append(o);bpy.context.view_layer.objects.active=o
 if r:
  m=o.modifiers.new('Soft manufactured edge','BEVEL');m.width=r;m.segments=3;bpy.ops.object.modifier_apply(modifier=m.name)
 for p in o.data.polygons:p.use_smooth=True
 m=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');m.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=m.name)
 uv(o,ma);return o
def box(name,p,s,ma,r=.007,yaw=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=U(p));o=bpy.context.object;o.dimensions=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.rotation_euler.z=math.radians(yaw);return finish(o,name,ma,min(r,min(s)*.45))
def mesh(name,vs,fs,ma,smooth=True):
 m=bpy.data.meshes.new(name);m.from_pydata([U(v) for v in vs],[],fs);m.update();o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o);m.materials.append(ma);parts.append(o)
 bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(m);bm.free()
 for f in m.polygons:f.use_smooth=smooth
 uv(o,ma);return o
def rod(name,a,b,r,ma):
 av=U(a);bv=U(b);d=bv-av;bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=r,depth=d.length,location=(av+bv)/2)
 o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return finish(o,name,ma,min(.002,r*.15))
def tube(name,points,r,ma,closed=False):
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.resolution_u=8;c.bevel_depth=r;c.bevel_resolution=2
 s=c.splines.new('BEZIER');s.bezier_points.add(len(points)-1);s.use_cyclic_u=closed
 for v,p in zip(s.bezier_points,points):v.co=U(p);v.handle_left_type=v.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o.select_set(False)
 o.data.materials.append(ma);parts.append(o);uv(o,ma);return o
def lathe(name,p,profile,ma,n=48):
 vs=[];fs=[]
 for r,y in profile:
  for i in range(n):
   a=2*math.pi*i/n;vs.append((p[0]+r*math.cos(a),p[1]+y,p[2]+r*math.sin(a)))
 for j in range(len(profile)-1):
  for i in range(n):fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 return mesh(name,vs,fs,ma)
def cloth(name,c,w,d,ma,drop=0):
 vs=[];fs=[];nx=56;nz=32
 for j in range(nz+1):
  v=j/nz
  for i in range(nx+1):
   u=i/nx;x=(u-.5)*w;z=(v-.5)*d
   edge=max(0,(abs(v-.5)-.39)/.11);y=c[1]-.045*edge*edge-drop*edge**3
   y+=.009*math.sin(u*24+v*6)*math.sin(v*10)+.004*math.sin(u*61+v*13)
   vs.append((c[0]+x,y,c[2]+z))
 for j in range(nz):
  for i in range(nx):
   k=j*(nx+1)+i;fs.append((k,k+1,k+nx+2,k+nx+1))
 o=mesh(name,vs,fs,ma);m=o.modifiers.new('Cloth thickness','SOLIDIFY');m.thickness=.004;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name);return o

# Architectural shell: 4.4 x 3.8 m, plan north is +Z. Real openings in the walls.
box('Architecture floor',(0,-.065,-.25),(4.52,.13,3.92),floor,.002)
box('Architecture ceiling',(0,2.63,-.25),(4.52,.12,3.92),wall,.002)
box('Architecture north wall',(0,1.3,1.71),(4.52,2.6,.12),wall,.002)
box('Architecture south wall',(0,1.3,-2.21),(4.52,2.6,.12),wall,.002)
box('Architecture west wall upper',(-2.26,1.3,.26),(.12,2.6,2.78),wall,.002)
box('Architecture west door lintel',(-2.26,2.32,-1.64),(.12,.56,1.02),wall,.002)
box('Architecture east sill wall',(2.26,.48,-.25),(.12,.96,3.92),wall,.002)
box('Architecture east lintel',(2.26,2.44,-.25),(.12,.38,3.92),wall,.002)
box('Architecture east north pier',(2.26,1.60,1.02),(.12,1.28,1.38),wall,.002)
box('Architecture east south pier',(2.26,1.60,-1.81),(.12,1.28,.80),wall,.002)
for x in [-2.185,2.185]:box('Skirting side',(x,.055,.30),(.026,.11,2.66),ward,.003)
for z in [1.635,-2.135]:box('Skirting cross',(0,.055,z),(4.38,.11,.026),ward,.003)
# Desk centered between fridge and wardrobe, as drawn.
box('Desk walnut top',(-.20,.753,1.27),(1.82,.045,.72),wood,.016)
box('Desk front apron',(-.20,.68,1.28),(1.77,.10,.045),black)
for x in [-1.01,.61]:
 for z in [1.02,1.52]:rod('Desk steel leg',(x,.03,z),(x,.73,z),.024,black)
 box('Desk foot rail',(x,.08,1.27),(.044,.045,.56),black)
box('Desk mat',(-.14,.781,1.13),(1.08,.007,.42),matpad,.009)
# Compact refrigerator in the northwest bay.
box('Fridge body',(-1.70,.51,1.23),(.76,1.00,.69),ivory,.025)
box('Fridge door gasket',(-1.70,.52,.876),(.745,.956,.018),rubber,.015)
box('Fridge door',(-1.70,.52,.858),(.742,.955,.035),ivory,.018)
rod('Fridge handle',(-1.405,.52,.821),(-1.405,.79,.821),.012,steel)
for x in [-1.98,-1.43]:box('Fridge foot',(x,.017,1.07),(.065,.034,.066),rubber)
box('Fridge note',(-1.87,.77,.837),(.12,.10,.0018),paper,.001,yaw=0)
rod('Note magnet',(-1.87,.80,.833),(-1.87,.80,.837),.008,blue)
# Northeast wardrobe, two tall doors and recessed handles.
box('Wardrobe carcass',(1.47,1.11,1.16),(1.40,2.18,.94),wood,.015)
for x in [1.119,1.821]:
 box('Wardrobe door',(x,1.14,.675),(.691,2.10,.035),ward,.011)
 rod('Wardrobe pull',(x+(.235 if x<1.5 else -.235),.94,.645),(x+(.235 if x<1.5 else -.235),1.30,.645),.006,steel)
box('Wardrobe plinth',(1.47,.075,1.16),(1.34,.13,.86),black)
# Single bed against east/south walls. The long axis follows the drawing.
box('Bed timber frame',(.97,.255,-1.55),(2.36,.33,1.10),wood,.025)
box('Bed mattress',(.96,.465,-1.55),(2.28,.21,1.055),cream,.070)
box('Bed headboard',(2.12,.64,-1.55),(.075,.90,1.13),wood,.028)
cloth('Duvet soft folds',(.69,.607,-1.55),1.75,1.19,linen,.14)
pillow=box('Pillow',(1.76,.619,-1.55),(.51,.17,.70),cream,.075,yaw=-3)
for x in [.10,1.89]:
 for z in [-1.96,-1.14]:box('Bed foot',(x,.10,z),(.065,.2,.065),black)
# East window, cool night view and partly gathered curtains.
box('Window night pane',(2.205,1.60,-.53),(.010,1.24,1.64),darkglass,.001)
for z in [-1.36,.30]:box('Window vertical frame',(2.18,1.60,z),(.06,1.32,.046),ivory)
for y in [.95,2.25]:box('Window horizontal frame',(2.18,y,-.53),(.065,.046,1.70),ivory)
box('Window center frame',(2.175,1.60,-.53),(.055,1.28,.034),ivory)
box('Window sill',(2.12,.924,-.53),(.21,.048,1.79),wood)
rod('Curtain pole',(2.03,2.36,-1.48),(2.03,2.36,.45),.013,steel)
for side,center in [('south',-1.30),('north',.26)]:
 vs=[];fs=[];rows=26;cols=36
 for j in range(rows+1):
  v=j/rows
  for i in range(cols+1):
   u=i/cols;z=center+(u-.5)*.38;x=2.04+.045*math.sin(u*10*math.pi)+.015*math.sin(v*5)
   vs.append((x,.31+2.00*v+.012*math.cos(u*20),z))
 for j in range(rows):
  for i in range(cols):k=j*(cols+1)+i;fs.append((k,k+1,k+cols+2,k+cols+1))
 o=mesh('Curtain '+side,vs,fs,curtain);m=o.modifiers.new('Curtain lining','SOLIDIFY');m.thickness=.002;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
# Southwest entry: slightly open inward; warm corridor remains mostly hidden.
for z in [-2.14,-1.12]:box('Door jamb',(-2.17,1.04,z),(.09,2.08,.055),ward)
box('Door lintel',(-2.17,2.095,-1.63),(.09,.055,1.075),ward)
door=box('Door leaf',(-2.06,1.025,-1.65),(.044,2.01,.97),ward,.009,yaw=11)
rod('Door handle',(-1.93,1.01,-1.28),(-1.88,1.01,-1.28),.013,steel)
box('Door corridor darkness',(-2.46,1.08,-1.62),(.04,2.13,1.02),rubber)
box('Door threshold light',(-2.29,.04,-1.62),(.025,.035,.91),amber,.002)
# Coat stand / pole beside the bed near the entry, as drawn.
lathe('Coat stand base',(-.53,.01,-1.82),[(0,0),(.23,0),(.24,.015),(.23,.031),(.06,.043),(0,.043)],black)
rod('Coat stand pole',(-.53,.04,-1.82),(-.53,1.69,-1.82),.018,steel)
for angle in [0,120,240]:
 a=math.radians(angle);tube('Coat hook',[(-.53,1.52,-1.82),(-.53+math.cos(a)*.17,1.60,-1.82+math.sin(a)*.17),(-.53+math.cos(a)*.19,1.67,-1.82+math.sin(a)*.19)],.010,black)
# A hanging jacket with rounded shoulders and naturally tapered sleeves.
box('Jacket torso',(-.56,1.10,-1.79),(.38,.64,.095),curtain,.045)
for x in [-.77,-.35]:
 o=box('Jacket sleeve',(x,1.06,-1.78),(.10,.60,.105),curtain,.04);o.rotation_euler.y=.16 if x<-.5 else -.16
# T50 inspired black frame, padded fabric seat, curved mesh back and five casters.
CX=-.20;CZ=.47
def C(x,y,z):return (CX+x,y,CZ+z)
rod('T50 gas lift',C(0,.11,0),C(0,.43,0),.022,steel)
for k in range(5):
 a=2*math.pi*k/5;ex=.30*math.sin(a);ez=.30*math.cos(a)
 tube('T50 star base',[C(0,.16,0),C(ex*.55,.12,ez*.55),C(ex,.073,ez)],.026,black)
 for delta in [-.017,.017]:rod('T50 caster',C(ex+delta-.008,.035,ez),C(ex+delta+.008,.035,ez),.034,rubber)
box('T50 underseat mechanism',C(0,.398,0),(.32,.065,.29),black,.021)
box('T50 contoured seat',C(0,.472,.033),(.495,.105,.465),fabric,.049)
for sign in [-1,1]:
 tube('T50 arm support',[C(sign*.20,.39,-.11),C(sign*.292,.48,-.10),C(sign*.292,.67,-.05)],.021,black)
 box('T50 arm pad',C(sign*.29,.708,.005),(.085,.04,.245),rubber,.018)
def back(u,v):
 w=.185+.045*math.sin(v*math.pi*.85);x=w*u;y=.535+v*.465
 z=-.208-.11*v+.052*math.sin(v*math.pi*1.5)+.050*u*u
 return C(x,y,z)
outline=[back(-1,i/20) for i in range(21)]+[back(-1+2*i/20,1) for i in range(1,21)]+[back(1,1-i/20) for i in range(1,21)]+[back(1-2*i/20,0) for i in range(1,20)]
tube('T50 S curve back frame',outline,.014,black,True)
# Fine real lattice strips transmit light through the chair instead of a solid slab.
for j in range(54):
 v=(j+.5)/54;tube('T50 horizontal mesh',[back(-.95+i*1.90/12,v) for i in range(13)],.00085,meshmat)
for i in range(46):
 u=-.95+(i+.5)*1.9/46;tube('T50 vertical mesh',[back(u,.03+j*.94/12) for j in range(13)],.00065,meshmat)
tube('T50 back spine',[C(0,.39,-.17),C(0,.54,-.29),C(0,.80,-.36)],.022,black)
box('T50 lumbar support',C(0,.681,-.266),(.29,.092,.047),black,.031)
rod('T50 headrest stem',C(0,.97,-.32),C(0,1.13,-.31),.012,black)
head=box('T50 headrest cushion',C(0,1.155,-.303),(.30,.145,.070),fabric,.032);head.rotation_euler.x=math.radians(-10)
rod('T50 tilt lever',C(.17,.392,-.09),C(.26,.407,-.09),.006,steel)
box('T50 lever grip',C(.255,.408,-.09),(.065,.022,.034),black)
# Personal desk details with uncluttered working space.
lathe('Task lamp base',(-.96,.778,1.43),[(0,0),(.080,0),(.083,.012),(.077,.021),(0,.021)],black)
tube('Task lamp arm',[(-.96,.80,1.43),(-.96,1.07,1.48),(-.87,1.26,1.40),(-.77,1.23,1.29)],.014,black)
lathe('Task lamp shade',(-.77,1.16,1.29),[(.095,0),(.099,.008),(.065,.092),(.039,.108),(.034,.098),(.058,.089),(.090,.010),(.095,0)],black)
lathe('Task lamp diffuser',(-.77,1.168,1.29),[(0,0),(.086,0),(.086,.004),(0,.004)],amber)
for x in [-.74,.40]:
 box('Speaker cabinet',(x,.858,1.45),(.13,.164,.13),black,.012)
 for y,r in [(.862,.040),(.919,.015)]:rod('Speaker cone',(x,y,1.378),(x,y,1.384),r,rubber)
box('PC chassis',(.49,.28,1.28),(.25,.48,.47),black,.014)
box('PC glass side',(.618,.30,1.27),(.003,.40,.38),darkglass,.002)
for y in [.17,.35]:
 pts=[(.49+.082*math.cos(i*2*math.pi/32),y+.082*math.sin(i*2*math.pi/32),1.039) for i in range(32)];tube('PC fan rim',pts,.002,cold,True)
lathe('Coffee mug',(-.80,.779,1.004),[(0,0),(.036,0),(.042,.006),(.045,.100),(.041,.104),(.038,.099),(.035,.015),(0,.015)],ivory)
tube('Mug handle',[(-.84,.86,1.004),(-.886,.85,1.004),(-.886,.811,1.004),(-.84,.804,1.004)],.007,ivory)
lathe('Coffee surface',(-.80,.870,1.004),[(0,0),(.037,0)],mat('Coffee',(.027,.014,.006),.22))
box('Notebook',(.51,.791,1.006),(.19,.023,.26),blue,.003,yaw=8)
box('Notebook pages',(.51,.800,1.006),(.181,.007,.25),paper,.002,yaw=8)
rod('Pen',(.49,.807,.921),(.55,.807,1.07),.003,steel)
box('Rug',(-.22,.010,.24),(1.31,.013,1.30),matpad,.005)
for x in [-.75,-.20,.35]:
 box('Music frame',(x,1.91,1.612),(.425,.51,.025),black,.005)
 box('Music print',(x,1.91,1.596),(.395,.48,.003),paper,.001)
 # Anonymous prints only; personal tastes are inferred from in-game records.
 rod('Music print disc',(x,1.96,1.590),(x,1.96,1.594),.126,blue if x<-.4 else black)
 rod('Music print label',(x,1.96,1.586),(x,1.96,1.590),.036,ochre)
tube('Monitor cable',[(-.20,.87,1.50),(-.20,.79,1.57),(.0,.73,1.57),(.48,.66,1.57),(.50,.47,1.46)],.003,rubber)
tube('Keyboard cable',[(-.31,.795,1.11),(-.50,.788,1.20),(-.51,.789,1.47),(-.20,.79,1.52)],.002,rubber)

# Export geometry only; lights and interaction stay authored in Unity.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'NightRoom.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,mesh_smooth_type='FACE',bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource/Blender/Models/NightRoom.blend'))
with open(os.path.join(OUT,'materials.json'),'w') as f:json.dump({'materials':[dict(name=k,**v) for k,v in materials.items()]},f,indent=2)
print('NIGHT_ROOM_EXPORTED',len(parts),'objects',sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts),'triangles')
