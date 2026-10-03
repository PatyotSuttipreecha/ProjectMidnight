// Offline asset generator. Run: node Tools/ShotgunM870/generate.cjs
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'Assets/Prefab/Weapon/M870');
const guid = () => crypto.randomBytes(16).toString('hex');
fs.mkdirSync(out, {recursive:true});
function writeAsset(file, text, importer, extra='') {
  fs.writeFileSync(file, text);
  if (!fs.existsSync(file+'.meta')) fs.writeFileSync(file+'.meta', `fileFormatVersion: 2\nguid: ${guid()}\n${importer}:\n${extra}  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
  return fs.readFileSync(file+'.meta','utf8').match(/guid: (\w+)/)[1];
}
if (!fs.existsSync(out+'.meta')) fs.writeFileSync(out+'.meta',`fileFormatVersion: 2\nguid: ${guid()}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n`);
const colors = {Steel:[.085,.10,.12], Walnut:[.30,.13,.055], Rubber:[.025,.027,.03], Bore:[.008,.009,.01], Bolt:[.27,.30,.33]};
const materials = {};
for (const [name,color] of Object.entries(colors)) {
  const metallic = name==='Steel' || name==='Bolt' ? .82 : 0;
  const smoothness = name==='Bolt' ? .55 : name==='Steel' ? .38 : .22;
  materials[name] = writeAsset(path.join(out,name+'.mat'), `%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_Name: M870_${name}\n  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 1\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats:\n    - _Metallic: ${metallic}\n    - _Smoothness: ${smoothness}\n    - _Surface: 0\n    - _Cull: 2\n    - _ZWrite: 1\n    m_Colors:\n    - _BaseColor: {r: ${color[0]}, g: ${color[1]}, b: ${color[2]}, a: 1}\n`, 'NativeFormatImporter', '  mainObjectFileID: 2100000\n');
}
const parts=[];
function mesh(){return {v:[],f:[]};}
function triangle(m,a,b,c){m.f.push([a,b,c]);}
function quad(m,a,b,c,d){triangle(m,a,b,c);triangle(m,a,c,d);}
function append(a,b){const o=a.v.length;a.v.push(...b.v);a.f.push(...b.f.map(f=>f.map(i=>i+o)));}
// Convex side profiles, extruded across X with inset face rings for rounded edges.
function profile(points,width,bevel=.003){
  const m=mesh(), n=points.length;
  const cy=points.reduce((s,p)=>s+p[0],0)/n,cz=points.reduce((s,p)=>s+p[1],0)/n;
  const signed=points.reduce((s,p,i)=>s+p[0]*points[(i+1)%n][1]-points[(i+1)%n][0]*p[1],0);
  if(signed<0)points=[...points].reverse();
  for(const [x,shrink] of [[-width/2,.92],[-width/2+bevel,1],[width/2-bevel,1],[width/2,.92]])
    for(const [y,z] of points)m.v.push([x,cy+(y-cy)*shrink,cz+(z-cz)*shrink]);
  for(let k=0;k<3;k++)for(let i=0;i<n;i++){const j=(i+1)%n;quad(m,k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i);}
  const l=m.v.push([-width/2,cy,cz])-1,r=m.v.push([width/2,cy,cz])-1;
  for(let i=0;i<n;i++){const j=(i+1)%n;triangle(m,l,j,i);triangle(m,r,3*n+i,3*n+j);}
  return m;
}
// Revolved cross sections along Z; supports inner walls and open bores.
function lathe(rings,cy=0,segments=32,caps=true){
  const m=mesh();
  for(const [z,r] of rings)for(let i=0;i<segments;i++){const a=i*2*Math.PI/segments;m.v.push([Math.cos(a)*r,cy+Math.sin(a)*r,z]);}
  for(let k=0;k<rings.length-1;k++)for(let i=0;i<segments;i++){const j=(i+1)%segments;quad(m,k*segments+i,k*segments+j,(k+1)*segments+j,(k+1)*segments+i);}
  if(caps){const a=m.v.push([0,cy,rings[0][0]])-1,b=m.v.push([0,cy,rings.at(-1)[0]])-1;for(let i=0;i<segments;i++){const j=(i+1)%segments;triangle(m,a,j,i);triangle(m,b,(rings.length-1)*segments+i,(rings.length-1)*segments+j);}}
  return m;
}
function box(x,y,z,sx,sy,sz){const m=profile([[y-sy/2,z-sz/2],[y+sy/2,z-sz/2],[y+sy/2,z+sz/2],[y-sy/2,z+sz/2]],sx,Math.min(.002,sx/5));m.v.forEach(v=>v[0]+=x);return m;}
function part(name,material,m,parent='Visual',position=[0,0,0]){parts.push({name,material,m,parent,position});return m;}
part('Receiver','Steel',profile([[-.039,-.139],[.025,-.139],[.048,-.117],[.050,.095],[.038,.119],[-.034,.119]],.068,.004));
part('Stock','Walnut',profile([[-.124,-.465],[.017,-.460],[.011,-.373],[.004,-.255],[.018,-.160],[.014,-.132],[-.034,-.136],[-.054,-.201],[-.083,-.285],[-.111,-.372]],.063,.004));
part('RecoilPad','Rubber',profile([[-.127,-.485],[.019,-.480],[.017,-.461],[-.124,-.466]],.066,.003));
// Hollow barrel: outer surface, annular muzzle, inner bore and breech closure.
part('Barrel','Steel',lathe([[.101,.0175],[.16,.016],[.63,.0137],[.641,.0137],[.643,.0125],[.643,.0093],[.60,.0093],[.12,.0093]],.032,40,false));
part('BoreBack','Bore',lathe([[.13,.0091],[.131,.0091]],.032,32));
part('MagazineTube','Steel',lathe([[.107,.013],[.112,.014],[.504,.014],[.508,.013]],-.009,32));
part('MagazineCap','Steel',lathe([[.505,.014],[.507,.016],[.522,.016],[.524,.014]],-.009,32));
const pump=lathe([[-.095,.021],[-.083,.026],[.069,.026],[.082,.021]],0,32);
for(let i=0;i<11;i++){const z=-.068+i*.013;append(pump,lathe([[z,.026],[z+.002,.028],[z+.005,.028],[z+.007,.026]],0,32,false));}
part('Forend','Walnut',pump,'Pump',[0,-.009,.265]);
part('ActionBarLeft','Steel',box(-.027,-.019,.139,.006,.012,.13),'Pump');
part('ActionBarRight','Steel',box(.027,-.019,.139,.006,.012,.13),'Pump');
// The right side port is a black inset with a narrower metallic bolt face.
part('EjectionPortRecess','Bore',box(.0342,.010,.033,.0018,.036,.096));
part('BoltFace','Bolt',box(.0353,.007,.017,.0012,.025,.047));
part('PortUpperLip','Steel',box(.0355,.030,.033,.003,.004,.104));
part('PortLowerLip','Steel',box(.0355,-.010,.033,.003,.004,.104));
part('LoadingPort','Bore',box(0,-.038,.045,.038,.002,.077));
part('LoadingGate','Bolt',box(0,-.0395,.036,.028,.001,.057));
// A curved closed trigger guard swept around a side-view loop.
function loop(points,width,thickness){
 const m=mesh(),n=points.length;
 for(let i=0;i<n;i++){
  const a=points[(i+n-1)%n],b=points[(i+1)%n],dy=b[0]-a[0],dz=b[1]-a[1],l=Math.hypot(dy,dz),p=points[i];
  for(const [x,s] of [[-width/2,-1],[-width/2,1],[width/2,1],[width/2,-1]])m.v.push([x,p[0]-dz/l*thickness*s/2,p[1]+dy/l*thickness*s/2]);
 }
 for(let i=0;i<n;i++){const j=(i+1)%n;for(let k=0;k<4;k++)quad(m,4*i+k,4*i+(k+1)%4,4*j+(k+1)%4,4*j+k);}
 return m;
}
part('TriggerGuard','Steel',loop([[-.040,-.105],[-.061,-.113],[-.084,-.103],[-.091,-.082],[-.091,-.028],[-.083,-.008],[-.059,-.005],[-.041,-.016]],.012,.006));
part('Trigger','Steel',profile([[-.043,-.060],[-.067,-.052],[-.080,-.064],[-.081,-.071],[-.073,-.068],[-.057,-.069],[-.043,-.073]],.009,.001));
part('FrontBeadBase','Steel',box(0,.047,.614,.008,.005,.011));
part('FrontBead','Bolt',lathe([[.612,.0025],[.614,.003],[.616,.0025]],.052,16));
part('BarrelBand','Steel',box(0,.010,.490,.013,.023,.014));
for(const z of [-.091,-.016]){
 const pin=lathe([[-.0005,.0035],[.0005,.0035]],0,16);
 // Rotate the pin axis from Z to X.
 pin.v=pin.v.map(([x,y,zz])=>[zz+.035,y-.017,x+z]);
 part(z<-.05?'RearReceiverPin':'FrontReceiverPin','Bolt',pin);
}
const meshMap={};
const modelTemplate=fs.readFileSync(path.join(root,'Assets/Samples/AI Navigation/2.0.9/Build And Connect NavMesh Surfaces/Models/heightmesh.obj.meta'),'utf8');
for(const p of parts){
 const file=path.join(out,p.name+'.obj');
 let obj=`# M870 stylized mesh; meters; forward +Z\no ${p.name}\n`;
 // Unity's OBJ importer reflects X; pre-reflect export coordinates and winding.
 obj+=p.m.v.map(v=>`v ${-v[0]} ${v[1]} ${v[2]}`).join('\n')+'\n';
 obj+=p.m.v.map(v=>`vt ${v[2]*2+.5} ${v[1]*4+.5}`).join('\n')+'\n';
 obj+='s 1\n'+p.m.f.map(f=>'f '+[...f].reverse().map(i=>`${i+1}/${i+1}`).join(' ')).join('\n')+'\n';
 fs.writeFileSync(file,obj);
 let g=fs.existsSync(file+'.meta')?fs.readFileSync(file+'.meta','utf8').match(/guid: (\w+)/)[1]:guid();
 let meta=modelTemplate.replace(/guid: \w+/,`guid: ${g}`).replace('internalIDToNameTable: []',`internalIDToNameTable:\n  - first:\n      43: 4300000\n    second: ${p.name}`).replace('materialImportMode: 2','materialImportMode: 0').replace('fileIdsGeneration: 2','fileIdsGeneration: 1').replace('normalImportMode: 0','normalImportMode: 1').replace('normalSmoothAngle: 60','normalSmoothAngle: 35').replace('importAnimation: 1','importAnimation: 0');
 fs.writeFileSync(file+'.meta',meta);meshMap[p.name]=g;
}
const template=fs.readFileSync(path.join(root,'Assets/Prefab/Weapon/Shotgun_Simple.prefab'),'utf8');
const renderer=template.match(/MeshRenderer:\r?\n[\s\S]*?(?=\r?\n---|$)/)[0];
const nodes=[{name:'Shotgun_M870',parent:null},{name:'Visual',parent:'Shotgun_M870'},{name:'Pump',parent:'Visual'},...parts,{name:'FirePoint',parent:'Shotgun_M870',position:[0,.032,.649]},{name:'LeftHandGrip',parent:'Pump',position:[0,-.035,.265]},{name:'RightHandGrip',parent:'Shotgun_M870',position:[0,-.045,-.175]}];
nodes.forEach((p,i)=>{p.id=1000+i*10;p.t=p.id+1;});
const byName=Object.fromEntries(nodes.map(p=>[p.name,p]));
let prefab='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n';
for(const p of nodes){
 const pos=p.position||[0,0,0],children=nodes.filter(c=>c.parent===p.name);
 prefab+=`--- !u!1 &${p.id}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  - component: {fileID: ${p.t}}\n`;
 if(p.m)prefab+=`  - component: {fileID: ${p.id+2}}\n  - component: {fileID: ${p.id+3}}\n`;
 prefab+=`  m_Layer: 0\n  m_Name: ${p.name}\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n--- !u!4 &${p.t}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ${p.id}}\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: ${pos[0]}, y: ${pos[1]}, z: ${pos[2]}}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n`;
 prefab+=children.length?'  m_Children:\n'+children.map(c=>`  - {fileID: ${c.t}}\n`).join(''):'  m_Children: []\n';
 prefab+=`  m_Father: {fileID: ${p.parent?byName[p.parent].t:0}}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n`;
 if(p.m){
 prefab+=`--- !u!33 &${p.id+2}\nMeshFilter:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ${p.id}}\n  m_Mesh: {fileID: 4300000, guid: ${meshMap[p.name]}, type: 3}\n--- !u!23 &${p.id+3}\n`;
 prefab+=renderer.replace(/m_GameObject: \{fileID: \d+\}/,`m_GameObject: {fileID: ${p.id}}`).replace(/guid: \w+, type: 2/,`guid: ${materials[p.material]}, type: 2`).trimEnd()+'\n';
 }
}
writeAsset(path.join(out,'Shotgun_M870.prefab'),prefab,'PrefabImporter');
const triangles=parts.reduce((s,p)=>s+p.m.f.length,0);
const preview={parts:parts.map(p=>({name:p.name,color:colors[p.material],v:p.m.v.map(v=>v.map((x,i)=>x+p.position[i])),f:p.m.f})),triangles};
fs.writeFileSync(path.join(__dirname,'preview-data.json'),JSON.stringify(preview));
// Validate IDs, GUID dependencies, finite vertices and non-degenerate triangles.
const ids=[...prefab.matchAll(/^--- !u!\d+ &(\d+)/gm)].map(x=>x[1]);
if(new Set(ids).size!==ids.length)throw Error('Duplicate prefab IDs');
for(const ref of prefab.matchAll(/\{fileID: (\d+)\}/g))if(ref[1]!=='0'&&!ids.includes(ref[1]))throw Error('Unresolved local reference');
for(const p of parts)for(const f of p.m.f){
 const [a,b,c]=f.map(i=>p.m.v[i]);
 if(!a||!b||!c||![...a,...b,...c].every(Number.isFinite))throw Error('Invalid mesh data');
 const u=b.map((v,i)=>v-a[i]),v=c.map((v,i)=>v-a[i]);
 if(Math.hypot(u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])<1e-12)throw Error('Degenerate face');
}
console.log(`Created ${parts.length} mesh parts, ${triangles} triangles; prefab reference and geometry checks passed.`);
