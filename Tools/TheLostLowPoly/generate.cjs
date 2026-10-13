const fs=require('fs'),path=require('path'),g=require('../ShotgunM870Realistic/geometry.cjs');
const root=path.resolve(__dirname,'../..'),project=path.join(root,'Library/TheLostLowPolyValidation'),assets=[];
function limb(a,b,r1,r2,n=8){const d=b.map((v,i)=>v-a[i]),len=Math.hypot(...d),w=d.map(v=>v/len);const ref=Math.abs(w[1])>.9?[1,0,0]:[0,1,0];const cross=(u,v)=>[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];let u=cross(ref,w);const l=Math.hypot(...u);u=u.map(v=>v/l);const v=cross(w,u);const m=g.lathe([[0,r1],[len,r2]],0,n);m.v=m.v.map(([x,y,z])=>a.map((t,i)=>t+u[i]*x+v[i]*y+w[i]*z));return m;}
function vertical(sections,n=8,p=2.5){const m=g.loft(sections,n,p);m.v=m.v.map(([x,y,z])=>[x,z,-y]);return m;}
for(const type of ['Unarmed','Machete','WoodClub','HeavyAxe','Stone','Torch']){
 const asset={name:'Prefab_Character_TheLost_'+type+'_LowPoly',parts:[]};assets.push(asset);
 function add(name,material,m,parent='Visual'){g.validate([{name,m}]);asset.parts.push({name,material,parent,vertices:m.v.map(([x,y,z])=>({x,y,z})),uv:m.uv.map(([x,y])=>({x,y})),indices:m.f.flat()});}
 add('Torso_Shirt','Cloth',vertical([[.93,0,.13,.085],[1.02,0,.17,.10],[1.25,-.02,.21,.105],[1.44,0,.22,.10],[1.48,0,.17,.09]],8));
 add('Hips_Trousers','Pants',vertical([[.84,0,.17,.11],[.98,0,.16,.1]],8));
 add('Neck','Skin',limb([0,1.44,0],[0,1.55,.015],.048,.055));
 const head=vertical([[1.51,-.025,.06,.055],[1.56,-.025,.09,.075],[1.69,0,.10,.085],[1.75,0,.075,.065]],10);add('Head','Skin',head);
 add('HairCap','Hair',vertical([[1.68,-.005,.102,.086],[1.74,0,.08,.07],[1.77,0,.055,.045]],10));
 for(const sign of [-1,1]){
  add('EyeSocket_'+sign,'Dark',g.box(sign*.036,1.647,.080,.026,.013,.008));
  add('UpperArm_'+sign,'Cloth',limb([sign*.20,1.40,0],[sign*.32,1.16,.015],.075,.055));
  add('Forearm_'+sign,'Skin',limb([sign*.32,1.16,.015],[sign*.40,.96,.065],.051,.032));
  add('Hand_'+sign,'Skin',g.box(sign*.41,.915,.068,.063,.105,.052));
  add('UpperLeg_'+sign,'Pants',limb([sign*.095,.9,0],[sign*.12,.49,.035],.086,.062));
  add('LowerLeg_'+sign,'Pants',limb([sign*.12,.49,.035],[sign*.13,.11,0],.062,.044));
  add('Foot_'+sign,'Boots',g.box(sign*.13,.065,.045,.115,.13,.23));
 }
 add('Nose','Skin',g.box(0,1.62,.087,.023,.035,.027));
 add('ShirtPatch','Patch',g.box(-.09,1.21,.103,.08,.07,.003));
 add('ShirtHemFray','Patch',g.box(.09,.967,.09,.075,.08,.003));
 const x=.43,y=.90,z=.07;
 if(type==='Machete'){
  add('MacheteGrip','Wood',g.box(x,y-.025,z,.035,.16,.035),'Weapon');
  const m=g.profile([[0,0],[.035,.015],[.043,.36],[.025,.42],[-.015,.36],[-.025,.03]],.008,.001);m.v=m.v.map(([a,b,c])=>[x+b,y-.1-c,z+a]);add('MacheteBlade','Metal',m,'Weapon');
 }else if(type==='WoodClub')add('WoodClub','Wood',limb([x,y+.06,z],[x+.05,y-.62,z],.028,.07,7),'Weapon');
 else if(type==='HeavyAxe'){
  add('AxeHandle','Wood',limb([x,y+.16,z],[x+.04,y-.73,z],.024,.029),'Weapon');
  add('AxeHead','Metal',g.box(x+.04,y-.61,z,.31,.14,.10),'Weapon');add('AxeCuttingEdge','Silver',g.box(x+.20,y-.61,z,.025,.15,.06),'Weapon');
 }else if(type==='Stone')add('HeldStone','Stone',g.translate(vertical([[-.065,0,.045,.03],[-.015,.005,.075,.05],[.05,0,.054,.04]],6),x,y,z),'Weapon');
 else if(type==='Torch'){
  add('TorchHandle','Wood',limb([x,y-.25,z],[x,y+.33,z],.018,.029),'Weapon');
  add('TorchWrap','Cloth',limb([x,y+.25,z],[x,y+.38,z],.043,.042),'Weapon');
  add('Flame_StaticPlaceholder','Flame',g.translate(vertical([[0,0,.038,.034],[.07,0,.055,.038],[.19,0,.002,.002]],6),x,y+.36,z),'Weapon');
 }
 asset.triangles=asset.parts.reduce((n,p)=>n+p.indices.length/3,0);
}
for(const dir of ['Assets/Editor','Packages','ProjectSettings'])fs.mkdirSync(path.join(project,dir),{recursive:true});
fs.writeFileSync(path.join(__dirname,'source.json'),JSON.stringify({assets}));fs.copyFileSync(path.join(__dirname,'source.json'),path.join(project,'Assets/Source.json'));
let code=fs.readFileSync(path.join(root,'Tools/KeyAndPadlock/KeyAndPadlockBuilder.cs'),'utf8').replaceAll('KeyAndPadlockBuilder','TheLostLowPolyBuilder').replaceAll('Assets/Prefab/Props/KeyAndPadlock/','Assets/Prefab/Character/TheLostLowPoly/').replace('"Assets/Prefab/Props"','"Assets/Prefab/Character"');
code=code.replace('"Brass", "Steel", "Silver", "Dark", "DarkBrass"','"Skin", "Cloth", "Pants", "Boots", "Hair", "Dark", "Patch", "Wood", "Metal", "Silver", "Stone", "Flame"');
code=code.replace(/mat.SetColor\("_BaseColor",.*?;/,'mat.SetColor("_BaseColor",name=="Skin"?new Color(.43f,.40f,.33f):name=="Cloth"?new Color(.29f,.30f,.22f):name=="Pants"?new Color(.19f,.22f,.22f):name=="Patch"?new Color(.38f,.32f,.22f):name=="Wood"?new Color(.27f,.17f,.09f):name=="Metal"?new Color(.34f,.36f,.36f):name=="Silver"?new Color(.56f,.59f,.60f):name=="Stone"?new Color(.39f,.38f,.33f):name=="Flame"?new Color(.95f,.40f,.07f):new Color(.06f,.055f,.045f));');
code=code.replace('name=="Dark"?0:.65f','name=="Metal"||name=="Silver"?.6f:0');
code=code.replace('Child(root.transform,"InteractionPoint").localPosition=Vector3.zero;','Child(root.transform,"WeaponGripPoint").localPosition=new Vector3(.43f,.90f,.07f);');
code=code.replaceAll('validation-success.txt','the-lost-success.txt').replace('Key and padlock prefabs, native meshes and URP render verified','Six The Lost visual prototype prefabs and URP render verified');
code=code.slice(0,code.indexOf('    private static void Studio()'))+fs.readFileSync(path.join(__dirname,'Studio.cs.txt'),'utf8')+'\n}\n';
fs.writeFileSync(path.join(__dirname,'TheLostLowPolyBuilder.cs'),code);fs.writeFileSync(path.join(project,'Assets/Editor/TheLostLowPolyBuilder.cs'),code);
fs.copyFileSync(path.join(root,'Library/KeyAndPadlockValidation/Packages/manifest.json'),path.join(project,'Packages/manifest.json'));fs.copyFileSync(path.join(root,'ProjectSettings/ProjectVersion.txt'),path.join(project,'ProjectSettings/ProjectVersion.txt'));
fs.writeFileSync(path.join(__dirname,'manifest.json'),JSON.stringify(assets.map(a=>({name:a.name,triangles:a.triangles,parts:a.parts.length})),null,2));
console.log(assets.map(a=>a.name+': '+a.triangles+' triangles').join('\n'));

