const fs=require('fs'),path=require('path'),g=require('../ShotgunM870Realistic/geometry.cjs');
const root=path.resolve(__dirname,'../..'),project=path.join(root,'Library/RinEvidenceCameraValidation');
const asset={name:'Rin_EvidenceCamera',parts:[]};
function add(name,material,m,parent='Visual'){g.validate([{name,m}]);asset.parts.push({name,material,parent,vertices:m.v.map(([x,y,z])=>({x,y,z})),uv:m.uv.map(([x,y])=>({x,y})),indices:m.f.flat()});}
function rounded(x,y,z,w,h,d,r=.005){const pts=[];for(const[cx,cy,start]of [[w/2-r,h/2-r,0],[-w/2+r,h/2-r,90],[-w/2+r,-h/2+r,180],[w/2-r,-h/2+r,270]])for(let i=0;i<=5;i++){const a=(start+i*18)*Math.PI/180;pts.push([cy+r*Math.sin(a),cx+r*Math.cos(a)]);}const m=g.profile(pts,d,Math.min(.001,d/4));m.v=m.v.map(([a,b,c])=>[c+x,b+y,-a+z]);return m;}
add('RoundedBody','Body',rounded(0,0,0,.112,.067,.028));
add('FrontShell','Metal',rounded(0,0,.014,.108,.063,.003));
add('RightHandGrip','Rubber',rounded(.043,-.002,.018,.022,.054,.006,.006));
add('LensBase','Dark',g.translate(g.lathe([[.015,.026],[.018,.027],[.021,.026]],0,48),-.011,0,0),'Lens');
add('LensBarrel','Metal',g.translate(g.lathe([[.021,.022],[.024,.023],[.035,.023],[.038,.022]],0,48,false),-.011,0,0),'Lens');
add('LensRim','Dark',g.translate(g.lathe([[.037,.022],[.039,.022],[.039,.018],[.037,.018]],0,48,false),-.011,0,0),'Lens');
add('LensGlass','Glass',g.translate(g.lathe([[.0374,.0178],[.038,.0178]],0,48),-.011,0,0),'Lens');
add('LensHighlight','Silver',g.translate(g.lathe([[.035,.0232],[.0358,.0232]],0,48),-.011,0,0),'Lens');
add('FlashHousing','Dark',rounded(-.031,.023,.017,.029,.009,.002,.001));
add('FlashGlass','Flash',rounded(-.031,.023,.0185,.025,.006,.001,.001));
for(let i=0;i<7;i++)add('FlashRib_'+i,'Silver',g.box(-.042+i*.0035,.023,.0192,.0004,.005,.0003));
add('FocusAssistLamp','Glass',g.translate(g.lathe([[.0165,.002],[.018,.002]],.023,16),.009,0,0));
add('RearScreenFrame','Dark',rounded(-.014,0,-.016,.075,.048,.004,.004),'Screen');
add('RearScreenGlass','Screen',rounded(-.014,0,-.0183,.068,.041,.001,.003),'Screen');
add('ScreenReflection','Glass',rounded(-.033,.014,-.019,.022,.001,.0002,.0002),'Screen');
function topDisk(name,x,z,r,h,mat){const m=g.lathe([[0,r],[h,r]],0,24);m.v=m.v.map(([a,b,c])=>[a+x,c+.034,b+z]);add(name,mat,m,'Controls');}
topDisk('ShutterButton',.032,.001,.006,.003,'Silver');topDisk('PowerButton',.013,-.001,.0028,.0015,'Dark');topDisk('ModeDial',-.031,0,.009,.002,'Dark');
add('RearNavigationRing','Metal',g.translate(g.lathe([[-.016,.008],[-.019,.008],[-.019,.004],[-.016,.004]],0,32,false),.036,0,0),'Controls');
add('RearOKButton','Dark',g.translate(g.lathe([[-.018,.0038],[-.0195,.0038]],0,24),.036,0,0),'Controls');
for(const y of [-.021,.02])add(y>0?'PlaybackButton':'MenuButton','Dark',rounded(.037,y,-.017,.012,.006,.003,.002),'Controls');
// A small muted red tag identifies a personal camera without inventing written evidence.
add('PersonalStrapTab','Tag',rounded(.056,-.022,0,.006,.014,.005,.002),'Strap');
const strap=g.mesh();for(let i=0;i<32;i++){const a=i/32*2*Math.PI,b=(i+1)/32*2*Math.PI;const p=[.072+.015*Math.cos(a),-.019+.023*Math.sin(a),-.002],q=[.072+.015*Math.cos(b),-.019+.023*Math.sin(b),-.002];const o=strap.v.length;const dx=q[0]-p[0],dy=q[1]-p[1],l=Math.hypot(dx,dy),nx=-dy/l*.0015,ny=dx/l*.0015;for(const[v,s]of [[p,-1],[p,1],[q,1],[q,-1]])g.vertex(strap,[v[0]+nx*s,v[1]+ny*s,v[2]]);g.quad(strap,o,o+1,o+2,o+3);g.quad(strap,o+3,o+2,o+1,o);}
add('WristStrap','Rubber',strap,'Strap');
asset.triangles=asset.parts.reduce((n,p)=>n+p.indices.length/3,0);
for(const dir of ['Assets/Editor','Packages','ProjectSettings'])fs.mkdirSync(path.join(project,dir),{recursive:true});
fs.writeFileSync(path.join(__dirname,'source.json'),JSON.stringify({assets:[asset]}));fs.copyFileSync(path.join(__dirname,'source.json'),path.join(project,'Assets/Source.json'));
let builder=fs.readFileSync(path.join(root,'Tools/KeyAndPadlock/KeyAndPadlockBuilder.cs'),'utf8').replaceAll('KeyAndPadlockBuilder','RinEvidenceCameraBuilder').replaceAll('Assets/Prefab/Props/KeyAndPadlock/','Assets/Prefab/Props/RinEvidenceCamera/');
builder=builder.replace('"Brass", "Steel", "Silver", "Dark", "DarkBrass"','"Body", "Metal", "Silver", "Dark", "Rubber", "Glass", "Screen", "Flash", "Tag"');
builder=builder.replace(/mat.SetColor\("_BaseColor",.*?;/,'mat.SetColor("_BaseColor",name=="Body"?new Color(.12f,.14f,.16f):name=="Metal"?new Color(.26f,.29f,.32f):name=="Silver"?new Color(.62f,.66f,.68f):name=="Glass"?new Color(.035f,.13f,.17f):name=="Screen"?new Color(.018f,.035f,.045f):name=="Flash"?new Color(.74f,.76f,.69f):name=="Tag"?new Color(.38f,.10f,.085f):new Color(.025f,.028f,.03f));');
builder=builder.replace(/mat.SetFloat\("_Metallic".*?mats\[name\]=mat;/s,'mat.SetFloat("_Metallic",name=="Metal"||name=="Silver"?.7f:0);mat.SetFloat("_Smoothness",name=="Glass"||name=="Screen"?.85f:.3f); AssetDatabase.CreateAsset(mat,Folder+"Materials/"+name+".mat"); mats[name]=mat;');
builder=builder.replace('Child(root.transform,"InteractionPoint").localPosition=Vector3.zero;','Child(root.transform,"InteractionPoint").localPosition=Vector3.zero; Child(root.transform,"GripPoint").localPosition=new Vector3(.042f,0,0); var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,0,.01f);collider.size=new Vector3(.112f,.067f,.058f);');
builder=builder.replace('AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);','AssetDatabase.DeleteAsset(Folder.TrimEnd(\'/\')); AssetDatabase.DeleteAsset("Assets/StudioRenderer.asset"); AssetDatabase.DeleteAsset("Assets/StudioPipeline.asset"); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);');
builder=builder.replaceAll('validation-success.txt','camera-success.txt').replace('Key and padlock prefabs, native meshes and URP render verified','Rin camera prefab, native meshes and URP render verified');
builder=builder.slice(0,builder.indexOf('    private static void Studio()'))+fs.readFileSync(path.join(__dirname,'Studio.cs.txt'),'utf8')+'\n}\n';
fs.writeFileSync(path.join(__dirname,'RinEvidenceCameraBuilder.cs'),builder);fs.writeFileSync(path.join(project,'Assets/Editor/RinEvidenceCameraBuilder.cs'),builder);
fs.copyFileSync(path.join(root,'Library/KeyAndPadlockValidation/Packages/manifest.json'),path.join(project,'Packages/manifest.json'));fs.copyFileSync(path.join(root,'ProjectSettings/ProjectVersion.txt'),path.join(project,'ProjectSettings/ProjectVersion.txt'));
console.log(`${asset.parts.length} parts, ${asset.triangles} triangles`);
