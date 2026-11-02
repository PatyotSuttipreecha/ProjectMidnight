import bpy,os,hashlib,json
from mathutils import Vector
ROOT='F:/Art/ProjectMidnight/Character/Mintra/'
OUT=ROOT+'ColorPass_v01/'
def load(name):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=ROOT+name)
def hashes():
 result={}
 for o in bpy.data.objects:
  if o.type!='MESH':continue
  m=o.data;m.calc_loop_triangles()
  for t in m.loop_triangles:
   name=m.materials[t.material_index].name
   points=tuple(sorted(tuple(round(v,6) for v in m.uv_layers.active.data[l].uv) for l in t.loops))
   result.setdefault(name,[]).append(points)
 return {k:hashlib.sha256(repr(sorted(v)).encode()).hexdigest() for k,v in result.items()}
load('Mintra_Bake_Lowpoly.fbx');a=hashes();load('Mintra_Combine_Lowpoly.fbx');b=hashes();assert a==b,'Bake and Painter source UVs differ'
for material in bpy.data.materials:
 k=next((s for s in ['Head','Body','Cloth'] if material.name.endswith('_'+s)),None)
 if not k:continue
 material.use_nodes=True;nodes=material.node_tree.nodes;shader=nodes.get('Principled BSDF')
 for suffix,input_name in [('BaseColor','Base Color'),('Roughness','Roughness')]:
  image=bpy.data.images.load(OUT+'Mintra_'+k+'_'+suffix+'.png',check_existing=True);image.colorspace_settings.name='sRGB' if suffix=='BaseColor' else 'Non-Color'
  tex=nodes.new('ShaderNodeTexImage');tex.image=image;material.node_tree.links.new(tex.outputs['Color'],shader.inputs[input_name])
# Preview only: center separated garment objects around the same body. No source file edits.
for o in bpy.data.objects:
 if o.type!='MESH' or o.name.startswith('Sphere'):continue
 pts=[o.matrix_world@v.co for v in o.data.vertices];cx=(min(p.x for p in pts)+max(p.x for p in pts))/2;cy=(min(p.y for p in pts)+max(p.y for p in pts))/2
 o.location.x-=cx;o.location.y-=cy
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1000;scene.render.resolution_y=1400;scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new('PreviewWorld');scene.world.color=(.25,.25,.25)
for name,loc,power,size in [('Key',(3,-4,5),650,4),('Fill',(-3,-2,3),400,4),('Rim',(0,3,4),550,3)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1.6))-o.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Preview');camera=bpy.data.objects.new('Preview',data);scene.collection.objects.link(camera);scene.camera=camera;camera.location=(3,-7,3.3);camera.rotation_euler=(Vector((0,0,1.6))-camera.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=3.45
scene.view_settings.view_transform='Standard';scene.render.filepath=OUT+'Mintra_BaseColor_Preview.png'
bpy.ops.wm.save_as_mainfile(filepath=OUT+'Mintra_BaseColor_Preview.blend');bpy.ops.render.render(write_still=True)
with open(OUT+'uv-match-validation.json','w')as f:json.dump({'uv_match':a==b,'materials':a,'preview_only_reassembly':True,'painter_layers_applied':False},f,indent=2)

