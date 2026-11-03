import bpy,json
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='F:/Unity/ProjectMidnight/ArtSource/Mintra/Mintra_Combine_Lowpoly.fbx')
for o in bpy.data.objects:
 if o.type!='MESH':continue
 if 'Sphere' in o.name or 'eye' in o.name.lower():
  m=o.data; uv=m.uv_layers.active
  print('MATRIX',o.matrix_world)
  for axis in range(3):
   v=min(m.vertices,key=lambda v:v.co[axis])
   print('AXISMIN',axis,tuple(v.co),[tuple(uv.data[l.index].uv) for l in m.loops if l.vertex_index==v.index][:1])
   v=max(m.vertices,key=lambda v:v.co[axis])
   print('AXISMAX',axis,tuple(v.co),[tuple(uv.data[l.index].uv) for l in m.loops if l.vertex_index==v.index][:1])
  print('EYE',o.name, 'bounds',[(min(v.co[i] for v in m.vertices),max(v.co[i] for v in m.vertices)) for i in range(3)])
  print('UV',[(min(v.uv[i] for v in uv.data),max(v.uv[i] for v in uv.data)) for i in range(2)])
  for v in sorted(m.vertices,key=lambda v:v.co.y)[:3]:
   print('FRONT',tuple(v.co),[tuple(uv.data[l.index].uv) for l in m.loops if l.vertex_index==v.index][:5])

