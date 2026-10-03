const fs=require('fs'),path=require('path'),crypto=require('crypto'),zlib=require('zlib');
const root=path.resolve(__dirname,'../..');
const out=path.join(root,'Assets/Prefab/Weapon/M870Realistic');
fs.mkdirSync(out,{recursive:true});
const newGuid=()=>crypto.randomBytes(16).toString('hex');
function folder(p){fs.mkdirSync(p,{recursive:true});if(!fs.existsSync(p+'.meta'))fs.writeFileSync(p+'.meta',`fileFormatVersion: 2\nguid: ${newGuid()}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n`);}
folder(out);folder(path.join(out,'Meshes'));folder(path.join(out,'Materials'));folder(path.join(out,'Textures'));
function save(file,body,importer,extra=''){
 fs.writeFileSync(file,body);
 if(!fs.existsSync(file+'.meta'))fs.writeFileSync(file+'.meta',`fileFormatVersion: 2\nguid: ${newGuid()}\n${importer}:\n${extra}  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
 return fs.readFileSync(file+'.meta','utf8').match(/guid: (\w+)/)[1];
}
function crc(b){let c=0xffffffff;for(const v of b){c^=v;for(let k=0;k<8;k++)c=(c>>>1)^((c&1)?0xedb88320:0);}return(c^0xffffffff)>>>0;}
function chunk(type,data){const b=Buffer.alloc(data.length+12);b.writeUInt32BE(data.length);b.write(type,4);data.copy(b,8);b.writeUInt32BE(crc(b.subarray(4,-4)),b.length-4);return b;}
function png(w,h,pixels){const head=Buffer.alloc(13);head.writeUInt32BE(w);head.writeUInt32BE(h,4);head[8]=8;head[9]=6;const raw=Buffer.alloc(h*(w*4+1));for(let y=0;y<h;y++)pixels.copy(raw,y*(w*4+1)+1,y*w*4,(y+1)*w*4);return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',head),chunk('IDAT',zlib.deflateSync(raw,{level:6})),chunk('IEND',Buffer.alloc(0))]);}
function texture(name,size,bytes,type){
 const file=path.join(out,'Textures',name+'.png');
 const g=save(file,png(size,size,bytes),'TextureImporter',`  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 1\n    sRGBTexture: ${type==='base'?1:0}\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 2\n    aniso: 8\n    mipBias: 0\n    wrapU: 0\n    wrapV: 0\n    wrapW: 0\n  maxTextureSize: ${size}\n  textureFormat: 1\n  textureType: ${type==='normal'?1:0}\n  convertToNormalMap: 0\n  alphaSource: 1\n  alphaIsTransparency: 0\n  isReadable: 0\n  textureShape: 1\n`);
 return g;
}
function material(name,maps,metallic,smoothness,color=[1,1,1]){
 const tex=(prop,g)=>`    - ${prop}:\n        m_Texture: {fileID: 2800000, guid: ${g}, type: 3}\n        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}\n`;
 return save(path.join(out,'Materials',name+'.mat'),`%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_Name: ${name}\n  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}\n  m_ValidKeywords:\n  - _NORMALMAP\n  - _METALLICSPECGLOSSMAP\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 1\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs:\n${tex('_BaseMap',maps.base)}${tex('_BumpMap',maps.normal)}${tex('_MetallicGlossMap',maps.mask)}    m_Ints: []\n    m_Floats:\n    - _Metallic: ${metallic}\n    - _Smoothness: ${smoothness}\n    - _BumpScale: 0.55\n    - _SmoothnessTextureChannel: 0\n    - _WorkflowMode: 1\n    - _Surface: 0\n    - _Cull: 2\n    - _ZWrite: 1\n    m_Colors:\n    - _BaseColor: {r: ${color[0]}, g: ${color[1]}, b: ${color[2]}, a: 1}\n`, 'NativeFormatImporter','  mainObjectFileID: 2100000\n');
}
const modelTemplate=fs.readFileSync(path.join(root,'Assets/Samples/AI Navigation/2.0.9/Build And Connect NavMesh Surfaces/Models/heightmesh.obj.meta'),'utf8');
function obj(p){
 let text=`# M870 FPS art asset, meters, +Z forward\no ${p.name}\n`;
 text+=p.m.v.map(v=>`v ${-v[0]} ${v[1]} ${v[2]}`).join('\n')+'\n';
 text+=p.m.uv.map(v=>`vt ${v[0]} ${v[1]}`).join('\n')+'\n';
 if(p.name==='Receiver'){
  // Imported explicit normals keep large side panels flat instead of smoothing them into a bulge.
  const faceNormals=p.m.f.map(([ia,ib,ic])=>{
   const a=p.m.v[ia],b=p.m.v[ib],c=p.m.v[ic],u=b.map((v,i)=>v-a[i]),v=c.map((q,i)=>q-a[i]);
   const n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]],l=Math.hypot(...n);
   return n.map(v=>v/l);
  });
  const averaged=p.m.v.map(()=>[0,0,0]);
  p.m.f.forEach((f,j)=>f.forEach(i=>faceNormals[j].forEach((v,k)=>averaged[i][k]+=v)));
  const normals=[];
  p.m.f.forEach((f,j)=>{
   const face=faceNormals[j],planar=Math.abs(face[0])>.9999||Math.abs(face[1])>.999999||Math.abs(face[2])>.9999;
   f.forEach(i=>{const n=planar?face:averaged[i],l=Math.hypot(...n);normals.push([-n[0]/l,n[1]/l,n[2]/l]);});
  });
  text+=normals.map(n=>'vn '+n.join(' ')).join('\n')+'\ns off\n';
  text+=p.m.f.map((f,j)=>'f '+f.map((i,k)=>`${i+1}/${i+1}/${j*3+k+1}`).reverse().join(' ')).join('\n')+'\n';
 }else text+='s 1\n'+p.m.f.map(f=>'f '+[...f].reverse().map(i=>`${i+1}/${i+1}`).join(' ')).join('\n')+'\n';
 const file=path.join(out,'Meshes',p.name+'.obj');
 const guid=save(file,text,'ModelImporter');
 const meta=modelTemplate.replace(/guid: \w+/,`guid: ${guid}`).replace('internalIDToNameTable: []',`internalIDToNameTable:\n  - first:\n      43: 4300000\n    second: ${p.name}`).replace('materialImportMode: 2','materialImportMode: 0').replace('fileIdsGeneration: 2','fileIdsGeneration: 1').replace('normalImportMode: 0','normalImportMode: 1').replace('normalSmoothAngle: 60','normalSmoothAngle: 38').replace('tangentImportMode: 3','tangentImportMode: 3').replace('importAnimation: 1','importAnimation: 0');
 fs.writeFileSync(file+'.meta',p.name==='Receiver'?meta.replace('normalImportMode: 1','normalImportMode: 0'):meta);return guid;
}
function prefab(parts,mats){
 const renderer=fs.readFileSync(path.join(root,'Assets/Prefab/Weapon/Shotgun_Simple.prefab'),'utf8').match(/MeshRenderer:\r?\n[\s\S]*?(?=\r?\n---|$)/)[0];
 const nodes=[{name:'Shotgun_M870_Realistic',parent:null},{name:'Visual',parent:'Shotgun_M870_Realistic'},{name:'Pump',parent:'Visual'},{name:'Bolt',parent:'Visual'},...parts,{name:'FirePoint',parent:'Shotgun_M870_Realistic',position:[0,.032,.601]},{name:'LeftHandGrip',parent:'Pump',position:[0,-.039,.266]},{name:'RightHandGrip',parent:'Shotgun_M870_Realistic',position:[0,-.066,-.209]}];
 nodes.forEach((p,i)=>{p.id=1000+i*10;p.t=p.id+1;});const byName=Object.fromEntries(nodes.map(p=>[p.name,p]));
 let text='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n';
 for(const p of nodes){
  const pos=p.position||[0,0,0],children=nodes.filter(c=>c.parent===p.name);
  text+=`--- !u!1 &${p.id}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: ${p.t}}\n`;
  if(p.m)text+=`  - component: {fileID: ${p.id+2}}\n  - component: {fileID: ${p.id+3}}\n`;
  text+=`  m_Layer: 0\n  m_Name: ${p.name}\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n--- !u!4 &${p.t}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ${p.id}}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: ${pos[0]}, y: ${pos[1]}, z: ${pos[2]}}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n`;
  text+=children.length?'  m_Children:\n'+children.map(c=>`  - {fileID: ${c.t}}\n`).join(''):'  m_Children: []\n';
  text+=`  m_Father: {fileID: ${p.parent?byName[p.parent].t:0}}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n`;
  if(p.m){const g=obj(p);text+=`--- !u!33 &${p.id+2}\nMeshFilter:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ${p.id}}\n  m_Mesh: {fileID: 4300000, guid: ${g}, type: 3}\n--- !u!23 &${p.id+3}\n`;text+=renderer.replace(/m_GameObject: \{fileID: \d+\}/,`m_GameObject: {fileID: ${p.id}}`).replace(/guid: \w+, type: 2/,`guid: ${mats[p.material]}, type: 2`).trimEnd()+'\n';}
 }
 const ids=[...text.matchAll(/^--- !u!\d+ &(\d+)/gm)].map(m=>m[1]);if(new Set(ids).size!==ids.length)throw Error('Duplicate IDs');
 for(const ref of text.matchAll(/\{fileID: (\d+)\}/g))if(ref[1]!=='0'&&!ids.includes(ref[1]))throw Error('Unresolved prefab reference');
 save(path.join(out,'Shotgun_M870_Realistic.prefab'),text,'PrefabImporter');
}
module.exports={root,out,save,png,texture,material,prefab};
