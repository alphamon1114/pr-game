"""Build a clean 67-key black keyboard inspired by the archon M3 600 MINI.
Run with Blender --background --python this_file.py. No generated mesh is reused.
Blank keycaps and separate RGB diffusers are exported together as FBX.
"""
import bpy, math, os, colorsys
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
OUT = os.path.join(ROOT, 'Assets/Art/ReferenceProps/Keyboard')
PREVIEW = os.path.join(ROOT, 'ArtSource/Blender/Models')
os.makedirs(OUT, exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color, metal=0, rough=.4):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Metallic'].default_value = metal
    p.inputs['Roughness'].default_value = rough
    return m

metal = material('Black anodized aluminum', (.022,.026,.033), .48,.31)
edge = material('Machined graphite edge', (.018,.021,.027), .38,.38)
plastic = material('Matte black PBT', (.013,.016,.021), 0,.40)
well = material('Recess and underside', (.007,.009,.013), .05,.55)
white = material('Warm white legends', (.68,.70,.72), 0,.6)
led = material('Indicator diffuser', (.50,.68,.72), .1,.28)
lp=led.node_tree.nodes.get('Principled BSDF')
lp.inputs['Emission Color'].default_value=(.16,.45,.50,1)
lp.inputs['Emission Strength'].default_value=.25

parts=[]
def finish(obj, mat, bevel=0):
    obj.data.materials.append(mat)
    if bevel:
        mod=obj.modifiers.new('Precision edge bevel','BEVEL')
        mod.width=bevel;mod.segments=3
        mod.limit_method='ANGLE'
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in obj.data.polygons:p.use_smooth=True
    mod=obj.modifiers.new('Weighted face normals','WEIGHTED_NORMAL')
    mod.keep_sharp=True;mod.weight=50
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(obj)
    return obj

def box(name,loc,size,mat,bevel=.0006):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object;o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,mat,bevel)

def loop(w,d,r,z):
    pts=[]
    for cx,cy,start in [(w/2-r,d/2-r,0),(-w/2+r,d/2-r,90),(-w/2+r,-d/2+r,180),(w/2-r,-d/2+r,270)]:
        for i in range(5):
            a=math.radians(start+i*90/4)
            pts.append((cx+r*math.cos(a),cy+r*math.sin(a),z))
    return pts

def frame():
    # Closed ring with a straight rim, actual recessed opening and narrow edge highlight.
    loops=[loop(.315,.120,.003, .005),loop(.315,.120,.003,.027),
           loop(.314,.119,.0028,.0277),loop(.304,.102,.002,.0277),
           loop(.303,.101,.0018,.0268),loop(.303,.101,.0018,.016)]
    n=len(loops[0]);vs=[v for ring in loops for v in ring];fs=[]
    for k in range(len(loops)-1):
        for i in range(n):fs.append((k*n+i,k*n+(i+1)%n,(k+1)*n+(i+1)%n,(k+1)*n+i))
    for i in range(n):fs.append((5*n+i,5*n+(i+1)%n,(i+1)%n,i))
    mesh=bpy.data.meshes.new('Continuous machined frame');mesh.from_pydata(vs,[],fs);mesh.update()
    obj=bpy.data.objects.new('Aluminum chassis',mesh);bpy.context.collection.objects.link(obj)
    finish(obj,metal,.00018)
    obj.data.materials.append(edge)
    # Top perimeter has a restrained bright machined edge.
    for p in obj.data.polygons:
        if .027 < p.center.z < .02765:p.material_index=1

frame()
box('Bottom shell',(0,0,.006),(.313,.118,.010),metal,.002)
box('Recessed switch plate',(0,0,.014),(.303,.101,.007),well,.001)
for x in [-.128,.128]:
    for y in [-.038,.038]:box('Rubber foot',(x,y,.001),(.033,.011,.002),well,.0008)
box('USB-C port recess',(-.107,.0557,.012),(.009,.001,.0032),well,.0007)
box('USB-C inner tongue',(-.107,.0562,.012),(.0058,.0005,.0008),edge,.0001)

pitch=.0187

def key(label,start,u,row):
    x=(start+u/2-8)*pitch;y=(2-row)*pitch
    width=u*pitch-.0014;depth=pitch-.0014
    z=.023;h=[.015,.012,.010,.0105,.0115][row]
    tilt=[-.09,-.065,-.02,.05,.09][row]
    tw=width-.0025;td=depth-.0025
    def topz(xx,yy):
        dish=.00065*(1-(yy/(td/2))**2)
        if label=='Space':dish=-.00045*(1-(yy/(td/2))**2)
        return z+h+tilt*yy-dish
    # Regular surface grid: long side edges also sample the cylindrical dish.
    # Concentric rectangular rings alone caused the former star-shaped dent.
    nx=max(8,int(u*6));ny=12;radius=.0012;vs=[];fs=[]
    for j in range(ny+1):
        yy=-td/2+td*j/ny
        dy=max(0,abs(yy)-(td/2-radius))
        half=tw/2-radius+math.sqrt(max(0,radius*radius-dy*dy))
        for i in range(nx+1):
            xx=(-1+2*i/nx)*half;vs.append((xx,yy,topz(xx,yy)))
    for j in range(ny):
        for i in range(nx):
            k=j*(nx+1)+i;fs.append((k,k+1,k+nx+2,k+nx+1))
    boundary=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,ny+1)]+[ny*(nx+1)+i for i in range(nx-1,-1,-1)]+[j*(nx+1) for j in range(ny-1,0,-1)]
    previous=boundary
    for fraction,dz in [(.03,-.0002),(.15,-.0006),(1,None)]:
        current=[]
        for idx in boundary:
            xx,yy,zz=vs[idx]
            current.append(len(vs))
            vs.append((xx*(1+fraction*(width/tw-1)),yy*(1+fraction*(depth/td-1)),z if dz is None else zz+dz))
        for k in range(len(boundary)):
            n=(k+1)%len(boundary);fs.append((previous[k],current[k],current[n],previous[n]))
        previous=current
    fs.append(tuple(reversed(previous)))
    mesh=bpy.data.meshes.new('Sculpted keycap '+label);mesh.from_pydata(vs,[],fs);mesh.update()
    o=bpy.data.objects.new('Keycap '+label,mesh);bpy.context.collection.objects.link(o);o.location=(x,y,0)
    o.data.materials.append(plastic);parts.append(o)
    for face in o.data.polygons:face.use_smooth=True
    o.data.set_sharp_from_angle(angle=1.1)
    # Discrete diffusers sit below opaque, unmarked caps. Their Unity material
    # is replaced by the time-driven RGB shader; studio renders show one phase.
    rgb=colorsys.hsv_to_rgb((start/16+.08)%1,.9,1)
    glow=material('RGB wave diffuser '+str(row)+' '+str(start),rgb,0,.4)
    gp=glow.node_tree.nodes.get('Principled BSDF')
    gp.inputs['Emission Color'].default_value=(*rgb,1)
    gp.inputs['Emission Strength'].default_value=2
    box('RGB underkey '+str(row)+' '+str(start),(x,y,z-.0005),(width+.0005,depth+.0005,.0014),glow,.0004)


rows=[
 [('Esc',1)]+[(c,1) for c in ['1 !','2 @','3 #','4 $','5 %','6 ^','7 &','8 *','9 (','0 )','- _','= +']]+[('Back',2),('Ins',1)],
 [('Tab',1.5)]+[(c,1) for c in list('QWERTYUIOP')+['[',']']]+[('\\',1.5),('Del',1)],
 [('Caps',1.75)]+[(c,1) for c in list('ASDFGHJKL')+[';',"'"]]+[('Enter',2.25),('PgUp',1)],
 [('Shift',2.25)]+[(c,1) for c in list('ZXCVBNM')+[',','.','/']]+[('Shift',1.75),('↑',1),('PgDn',1)],
 [('Ctrl',1.25),('Win',1.25),('Alt',1.25),('Space',6.25),('Alt',1.25),('Fn',1.25),('',.5),('←',1),('↓',1),('→',1)]
]
for row,keys in enumerate(rows):
    start=0
    for label,u in keys:
        if label:key(label,start,u,row)
        else:
            for i in range(4):box('Status indicator '+str(i),((start+u/2-8)*pitch,-2*pitch+(i-1.5)*.003,.028),(.0037,.001,.0003),led,.0002)
        start+=u
    assert abs(start-16)<.0001,(row,start)

# Export only the model; render helpers never enter the Unity asset.
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Keyboard_Clean.fbx'),use_selection=True,
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,mesh_smooth_type='FACE',bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PREVIEW,'Keyboard_Clean.blend'))

# Broad studio reflection cards reveal the straight edges and metal/PBT contrast.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=64
scene.cycles.use_denoising=True
scene.world.color=(.10,.10,.10)
ground=material('Studio floor',(.024,.031,.044),.15,.48)
box('Studio platform',(0,0,-.006),(200,200,.008),ground,0)
def area(name,loc,power,size,color):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='RECTANGLE';data.size=size;data.size_y=size*.3;data.color=color
    o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);o.location=loc
    o.rotation_euler=(Vector((0,0,.02))-o.location).to_track_quat('-Z','Y').to_euler()
area('Softbox left',(-.20,-.10,.30),1.5,.28,(.83,.90,1))
area('Long strip back',(.03,.18,.20),2.4,.35,(1,.90,.75))
area('Rim right',(.25,.0,.14),1.2,.18,(.63,.79,1))
camdata=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',camdata);bpy.context.collection.objects.link(cam)
cam.location=(.19,-.28,.30);cam.rotation_euler=(Vector((0,0,.012))-cam.location).to_track_quat('-Z','Y').to_euler()
camdata.type='ORTHO';camdata.ortho_scale=.39;scene.camera=cam
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.filepath=os.path.join(PREVIEW,'keyboard-clean.png');bpy.ops.render.render(write_still=True)
cam.location=(0,0,.5);cam.rotation_euler=(0,0,0)
cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_y=700;camdata.ortho_scale=.36
scene.render.filepath=os.path.join(PREVIEW,'keyboard-top.png');bpy.ops.render.render(write_still=True)
print('KEYBOARD_CLEAN_EXPORTED',len(parts))
