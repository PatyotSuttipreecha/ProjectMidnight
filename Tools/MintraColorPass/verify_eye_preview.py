import bpy
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='F:/Unity/ProjectMidnight/ArtSource/Mintra/Mintra_Combine_Lowpoly.fbx')
for o in list(bpy.data.objects):
 if o.type!='MESH' or 'Sphere' not in o.name:bpy.data.objects.remove(o,do_unlink=True)
mat=bpy.data.materials.new('EyeUVCheck');mat.use_nodes=True
n=mat.node_tree.nodes;n.clear();tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load('F:/Art/ProjectMidnight/Character/Mintra/Texture/Mintra_Eyes_Brown_UV_v02_2048.png');em=n.new('ShaderNodeEmission');out=n.new('ShaderNodeOutputMaterial');mat.node_tree.links.new(tex.outputs['Color'],em.inputs[0]);mat.node_tree.links.new(em.outputs[0],out.inputs[0])
for o in bpy.data.objects:o.data.materials.clear();o.data.materials.append(mat)
bpy.ops.object.camera_add(location=(0,-1,2.846))
camera=bpy.context.object;camera.rotation_euler=(Vector((0,-.0835,2.846))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=.22
s=bpy.context.scene;s.camera=camera;s.render.engine='CYCLES';s.cycles.samples=1;s.render.resolution_x=800;s.render.resolution_y=300;s.render.resolution_percentage=100;s.view_settings.view_transform='Standard';s.render.filepath='F:/Art/ProjectMidnight/Character/Mintra/Texture/Mintra_Eyes_UV_Check.png';bpy.ops.render.render(write_still=True)
