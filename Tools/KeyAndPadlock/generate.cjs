const fs=require('fs'),path=require('path'),g=require('../ShotgunM870Realistic/geometry.cjs');
const root=path.resolve(__dirname,'../..'),assets=[];
function item(name){const result={name,parts:[]};assets.push(result);return result;}
function add(asset,name,material,m,parent='Visual'){asset.parts.push({name,material,parent,vertices:m.v.map(([x,y,z])=>({x,y,z})),uv:m.uv.map(([x,y])=>({x,y})),indices:m.f.flat()});}
function faceProfile(points,depth,bevel){const m=g.profile(points.map(([x,y])=>[y,x]),depth,bevel);m.v=m.v.map(([x,y,z])=>[z,y,-x]);return m;}
function tube(points,radius,sides=20){
 const m=g.mesh();
 for(let k=0;k<points.length;k++){
  const a=points[Math.max(0,k-1)],b=points[Math.min(points.length-1,k+1)],t=[b[0]-a[0],b[1]-a[1]],len=Math.hypot(...t),n=[-t[1]/len,t[0]/len];
  for(let i=0;i<=sides;i++){const q=i/sides*2*Math.PI;g.vertex(m,[points[k][0]+n[0]*Math.cos(q)*radius,points[k][1]+n[1]*Math.cos(q)*radius,Math.sin(q)*radius],[i/sides,k/(points.length-1)]);}
 }
 const row=sides+1;for(let k=0;k<points.length-1;k++)for(let i=0;i<sides;i++)g.quad(m,k*row+i,k*row+i+1,(k+1)*row+i+1,(k+1)*row+i);
 for(const[k,flip]of [[0,true],[points.length-1,false]]){const c=g.vertex(m,[...points[k],0]);for(let i=0;i<sides;i++)flip?g.tri(m,c,k*row+i+1,k*row+i):g.tri(m,c,k*row+i,k*row+i+1);}
 return m;
}
const key=item('Key_Base');
const bow=[];for(let i=0;i<=48;i++){const q=i/48*2*Math.PI;bow.push([Math.cos(q)*.013,.031+Math.sin(q)*.013]);}
add(key,'BowRing','Brass',tube(bow,.0032,16));
add(key,'Shaft','Brass',g.box(0,-.008,0,.006,.052,.0038));
const teeth=g.mesh();for(const[y,w]of [[-.013,.008],[-.023,.011],[-.031,.008]])g.append(teeth,g.box(w/2+.001,-0+y,0,w,.005,.0038));
add(key,'Teeth','Brass',teeth);
add(key,'ShaftGroove','DarkBrass',g.box(-.0008,-.008,.00195,.001,.044,.00018));
const lock=item('Padlock_Base');
add(lock,'Body','Steel',faceProfile([[-.026,-.022],[.026,-.022],[.030,-.018],[.030,.017],[.025,.022],[-.025,.022],[-.030,.017],[-.030,-.018]],.020,.0014));
const shackle=[[-.018,.018],[-.018,.033],[-.018,.041]];
for(let i=1;i<=32;i++){const q=Math.PI-i/32*Math.PI;shackle.push([.018*Math.cos(q),.041+.018*Math.sin(q)]);}
shackle.push([.018,.033],[.018,.018]);
add(lock,'Shackle','Silver',tube(shackle,.0035,20),'Shackle');
add(lock,'KeyPlate','Brass',g.lathe([[.0102,.0105],[.0108,.011],[.0114,.0105]],-.001,40));
add(lock,'KeyholeRound','Dark',g.lathe([[.01145,.0025],[.0117,.0025]],.002,24));
add(lock,'KeyholeSlot','Dark',faceProfile([[-.001,-.0002],[.001,-.0002],[.0018,-.006],[-.0018,-.006]],.00025,.00003));
lock.parts.at(-1).vertices.forEach(v=>v.z+=.01155);
add(lock,'BodyFootBand','Silver',g.box(0,-.020,0,.052,.0015,.0205));
for(const asset of assets){g.validate(asset.parts.map(p=>({name:p.name,m:{v:p.vertices.map(v=>[v.x,v.y,v.z]),uv:p.uv.map(v=>[v.x,v.y]),f:Array.from({length:p.indices.length/3},(_,i)=>p.indices.slice(i*3,i*3+3))}})));asset.triangles=asset.parts.reduce((s,p)=>s+p.indices.length/3,0);}
const source={assets};fs.writeFileSync(path.join(__dirname,'source.json'),JSON.stringify(source));
const project=path.join(root,'Library/KeyAndPadlockValidation');
for(const dir of ['Assets/Editor','Packages','ProjectSettings'])fs.mkdirSync(path.join(project,dir),{recursive:true});
fs.copyFileSync(path.join(__dirname,'source.json'),path.join(project,'Assets/Source.json'));
fs.copyFileSync(path.join(__dirname,'KeyAndPadlockBuilder.cs'),path.join(project,'Assets/Editor/KeyAndPadlockBuilder.cs'));
fs.copyFileSync(path.join(root,'Library/Glock19BaseValidation/Packages/manifest.json'),path.join(project,'Packages/manifest.json'));
fs.copyFileSync(path.join(root,'ProjectSettings/ProjectVersion.txt'),path.join(project,'ProjectSettings/ProjectVersion.txt'));
fs.writeFileSync(path.join(__dirname,'manifest.json'),JSON.stringify(assets.map(a=>({name:a.name,parts:a.parts.length,triangles:a.triangles})),null,2));
console.log(assets.map(a=>`${a.name}: ${a.parts.length} mesh parts, ${a.triangles} triangles`).join('\n'));
