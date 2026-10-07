"""Create a texture-free review copy; keep generated geometry unchanged.
Usage: blender -b --python clean_materials.py -- input.glb output.glb mouse|keyboard
"""
import bpy, os, sys
source, output, kind = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=os.path.abspath(source))
material = bpy.data.materials.new('Solid_Blue' if kind == 'mouse' else 'Unmarked_Black')
material.use_nodes = True
bsdf = material.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value = (0.012, 0.29, 0.43, 1) if kind == 'mouse' else (0.012, 0.015, 0.019, 1)
bsdf.inputs['Metallic'].default_value = 0.25 if kind == 'mouse' else 0.05
bsdf.inputs['Roughness'].default_value = 0.32
for obj in bpy.context.scene.objects:
    if obj.type == 'MESH':
        obj.data.materials.clear()
        obj.data.materials.append(material)
        for polygon in obj.data.polygons:
            polygon.material_index = 0
os.makedirs(os.path.dirname(os.path.abspath(output)), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=os.path.abspath(output), export_format='GLB')
print('SOLID_MATERIAL_COPY_SAVED', output)
