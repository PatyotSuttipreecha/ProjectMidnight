import bpy, numpy as np, os, json
OUT='F:/Art/ProjectMidnight/Character/Mintra/ColorPass_v02'
os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='F:/Art/ProjectMidnight/Character/Mintra/Mintra_Bake_Lowpoly.fbx')
N=2048
maps={k:np.zeros((N,N,4),dtype=np.float32) for k in ['Head','Body','Cloth']}
rough={k:np.ones((N,N,4),dtype=np.float32) for k in maps}
covered={k:np.zeros((N,N),dtype=bool) for k in maps}
for k in maps:maps[k][:,:,:3]=(.58,.39,.28) if k!='Cloth' else (.10,.12,.075);maps[k][:,:,3]=1
for obj in bpy.data.objects:
 if obj.type!='MESH':continue
 mesh=obj.data;mesh.calc_loop_triangles();uv=mesh.uv_layers.active
 assert uv is not None,obj.name+' missing UV'
 coords=np.array([tuple(obj.matrix_world@v.co) for v in mesh.vertices])
 for tri in mesh.loop_triangles:
  name=mesh.materials[tri.material_index].name;k=next(s for s in maps if name.endswith('_'+s))
  p=np.array([tuple(uv.data[j].uv) for j in tri.loops])*N-.5
  low=np.maximum(0,np.floor(p.min(axis=0)).astype(int));high=np.minimum(N-1,np.ceil(p.max(axis=0)).astype(int))
  if np.any(high<low):continue
  x,y=np.meshgrid(np.arange(low[0],high[0]+1),np.arange(low[1],high[1]+1));den=(p[1,1]-p[2,1])*(p[0,0]-p[2,0])+(p[2,0]-p[1,0])*(p[0,1]-p[2,1])
  if abs(den)<1e-10:continue
  a=((p[1,1]-p[2,1])*(x-p[2,0])+(p[2,0]-p[1,0])*(y-p[2,1]))/den
  b=((p[2,1]-p[0,1])*(x-p[2,0])+(p[0,0]-p[2,0])*(y-p[2,1]))/den;c=1-a-b;mask=(a>=-1e-5)&(b>=-1e-5)&(c>=-1e-5)
  if not mask.any():continue
  positions=a[:,:,None]*coords[tri.vertices[0]]+b[:,:,None]*coords[tri.vertices[1]]+c[:,:,None]*coords[tri.vertices[2]]
  z=positions[:,:,2];noise=np.sin(x*12.9898+y*78.233)*43758.5453;noise=(noise-np.floor(noise)-.5)*.008
  if k=='Cloth':
   if 'PolishCloth' in obj.name:base=np.array([.24,.225,.18]);r=.78
   elif coords[:,2].min()<.5:
    base=np.where((z<.50)[:,:,None],np.array([.105,.061,.037]),np.array([.075,.10,.155]));r=np.where(z<.50,.56,.83)
   else:base=np.array([.032,.033,.040]);r=.85
  else:
   base=np.array([.76,.52,.39]);r=.55 if k=='Head' else .60
   if obj.name.startswith('Sphere'):
    base=np.array([.82,.80,.73]);r=.27
  # Subtle material-scale grain, avoiding painted-in directional lighting.
  grain=noise
  if k=='Cloth':
   weave=(np.sin((x+y)*2.1)+np.sin((x-y)*1.7))*.002
   grain=grain+weave
   if 'PolishCloth' not in obj.name and coords[:,2].min()<.5:
    denim=(np.sin(x*.45+y*.82)*.004+np.sin(x*.027+y*.013)*.003)
    grain=grain+np.where(z>=.50,denim,0)
  colors=np.clip(base+grain[:,:,None],0,1)
  yy=y[mask];xx=x[mask];maps[k][yy,xx,:3]=colors[mask];rough[k][yy,xx,:3]=np.broadcast_to(np.asarray(r)[...,None] if np.ndim(r) else r,colors.shape)[mask];covered[k][yy,xx]=True
# Dilate 12 pixels into UV padding without wrapping between opposite texture edges.
for k in maps:
 filled=covered[k].copy()
 for iteration in range(12):
  new=filled.copy()
  for dy,dx in [(1,0),(-1,0),(0,1),(0,-1)]:
   shifted=np.roll(filled,(dy,dx),(0,1));shifted[0 if dy==1 else -1,:]=False if dy else shifted[0 if dy==1 else -1,:];shifted[:,0 if dx==1 else -1]=False if dx else shifted[:,0 if dx==1 else -1]
   take=shifted&~new
   for image in [maps[k],rough[k]]:image[take]=np.roll(image,(dy,dx),(0,1))[take]
   new|=take
  filled=new
 for suffix,array in [('BaseColor',maps[k]),('Roughness',rough[k])]:
  image=bpy.data.images.new('Mintra_'+k+'_'+suffix,width=N,height=N,alpha=True)
  image.colorspace_settings.name='sRGB' if suffix=='BaseColor' else 'Non-Color'
  image.pixels.foreach_set(array.ravel());image.filepath_raw=os.path.join(OUT,image.name+'.png');image.file_format='PNG';image.save()
report={k:{'resolution':N,'covered_pixels':int(covered[k].sum())}for k in maps}
with open(os.path.join(OUT,'map-validation.json'),'w')as f:json.dump(report,f,indent=2)
print('MAPS_CREATED',json.dumps(report))
