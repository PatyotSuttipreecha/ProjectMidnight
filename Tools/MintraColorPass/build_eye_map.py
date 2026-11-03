import bpy,numpy as np,os
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath='F:/Unity/ProjectMidnight/ArtSource/Mintra/Mintra_Combine_Lowpoly.fbx')
root='F:/Art/ProjectMidnight/Character/Mintra/Texture'
iris=bpy.data.images.load(root+'/Mintra_Iris_Brown_Source_v01.png')
w,h=iris.size; pixels=np.array(iris.pixels[:],dtype=np.float32).reshape(h,w,4)
N=2048
base=bpy.data.images.load(root+'/Mintra_Combine_Lowpoly_Mat_Character_Mintra_Head_AlbedoTransparency.png')
assert tuple(base.size)==(N,N)
combined=np.array(base.pixels[:],dtype=np.float32).reshape(N,N,4)
out=np.zeros((N,N,4),np.float32)
for o in bpy.data.objects:
 if o.type!='MESH' or 'Sphere' not in o.name:continue
 m=o.data;m.calc_loop_triangles();uv=m.uv_layers.active
 coords=np.array([tuple(v.co) for v in m.vertices]);coords[:,0]-=np.where(coords[:,0]<-.065,-.1314551979,0)
 for t in m.loop_triangles:
  p=np.array([tuple(uv.data[i].uv) for i in t.loops])*N-.5
  low=np.maximum(0,np.floor(p.min(0)).astype(int));high=np.minimum(N-1,np.ceil(p.max(0)).astype(int))
  x,y=np.meshgrid(np.arange(low[0],high[0]+1),np.arange(low[1],high[1]+1))
  den=(p[1,1]-p[2,1])*(p[0,0]-p[2,0])+(p[2,0]-p[1,0])*(p[0,1]-p[2,1])
  if abs(den)<1e-10:continue
  a=((p[1,1]-p[2,1])*(x-p[2,0])+(p[2,0]-p[1,0])*(y-p[2,1]))/den
  b=((p[2,1]-p[0,1])*(x-p[2,0])+(p[0,0]-p[2,0])*(y-p[2,1]))/den;c=1-a-b;mask=(a>=-1e-5)&(b>=-1e-5)&(c>=-1e-5)
  pos=a[:,:,None]*coords[t.vertices[0]]+b[:,:,None]*coords[t.vertices[1]]+c[:,:,None]*coords[t.vertices[2]]
  # The FBX transform maps local -Z toward the face front, not local Y.
  # Front pole UV is (0.79549044, 0.60882205), the center of the left eye disc.
  radius=.0115; rr=np.sqrt(pos[:,:,0]**2+pos[:,:,1]**2)/radius
  ix=np.clip(((pos[:,:,0]/radius*.5+.5)*(w-1)).astype(int),0,w-1)
  iy=np.clip(((-pos[:,:,1]/radius*.5+.5)*(h-1)).astype(int),0,h-1)
  color=np.empty(pos.shape,np.float32);color[:]=(.86,.83,.78)
  mix=np.clip((1-rr)*45,0,1)*(pos[:,:,2]<0)
  color=color*(1-mix[:,:,None])+pixels[iy,ix,:3]*mix[:,:,None]
  out[y[mask],x[mask],:3]=color[mask]
  out[y[mask],x[mask],3]=1
  combined[y[mask],x[mask],:3]=color[mask]
  combined[y[mask],x[mask],3]=1
for name,array in [('Mintra_Eyes_Brown_UV_v02_2048',out),('Mintra_Head_Albedo_BrownEyes_v01_2048',combined)]:
 image=bpy.data.images.new(name,width=N,height=N,alpha=True)
 image.colorspace_settings.name='sRGB';image.pixels.foreach_set(array.ravel());image.filepath_raw=root+'/'+name+'.png';image.file_format='PNG';image.save()
 print('EYE_MAP_SAVED',image.filepath_raw)
print('CHECK: unchanged non-eye linear RGBA pixels',np.array_equal(combined[out[:,:,3]==0],np.array(base.pixels[:],dtype=np.float32).reshape(N,N,4)[out[:,:,3]==0]))

