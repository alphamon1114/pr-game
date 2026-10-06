"""Render an exported VARCO GLB from multiple angles without altering its mesh.
blender --background --python render_model.py -- model.glb output_directory
"""
import bpy, os, sys
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
path,out=os.path.abspath(args[0]),os.path.abspath(args[1]);os.makedirs(out,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=os.path.abspath(path))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
points=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
size=hi-lo;center=(hi+lo)/2;scale=1/max(size)
root=bpy.data.objects.new('Preview normalization',None);bpy.context.collection.objects.link(root)
for o in list(bpy.context.scene.objects):
 if o!=root and o.parent is None:
  old=o.matrix_world.copy();o.parent=root;o.matrix_world=old
root.scale=(scale,)*3;root.location=(-center.x*scale,-center.y*scale,-lo.z*scale)
height=size.z*scale
print('VARCO_GEOMETRY',len(meshes),sum(len(o.data.vertices) for o in meshes),'vertices')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.25,.29,.35,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.4
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.008))
floor=bpy.context.object;m=bpy.data.materials.new('Preview floor');m.use_nodes=True
p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.045,.055,.068,1);p.inputs['Roughness'].default_value=.65;floor.data.materials.append(m)
target=Vector((0,0,height*.47))
def area(loc,power,size):
 d=bpy.data.lights.new('Studio softbox','AREA');d.energy=power;d.shape='DISK';d.size=size
 o=bpy.data.objects.new('Studio softbox',d);bpy.context.collection.objects.link(o);o.location=loc
 o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
area((-1.3,-1.7,2.4),110,2);area((1.6,.8,2),160,1.7);area((-.5,1.6,.9),55,1.3)
d=bpy.data.cameras.new('Preview camera');cam=bpy.data.objects.new('Preview camera',d);bpy.context.collection.objects.link(cam)
d.type='ORTHO';d.ortho_scale=1.38;scene.camera=cam
scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
for name,pos in [('front',(-1,-1.8,1.7)),('back',(1,1.8,1.7)),('top',(0,0,3))]:
 cam.location=target+Vector(pos);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
 scene.render.filepath=os.path.join(out,name+'.png');bpy.ops.render.render(write_still=True)
print('VARCO_RENDER_DONE')
