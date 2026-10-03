const fs=require('fs');
const path=require('path');
const data=JSON.parse(fs.readFileSync(path.join(__dirname,'preview-data.json'),'utf8'));
const yaw=.48,pitch=-.16;
function rotate([x,y,z]){
 const a=x*Math.cos(yaw)+z*Math.sin(yaw),c=-x*Math.sin(yaw)+z*Math.cos(yaw);
 return [c,a*Math.sin(pitch)+(y+.02)*Math.cos(pitch),a*Math.cos(pitch)-(y+.02)*Math.sin(pitch)];
}
const faces=[];
for(const p of data.parts)for(const f of p.f){
 const vs=f.map(i=>rotate(p.v[i])),[a,b,c]=vs;
 const u=b.map((v,i)=>v-a[i]),v=c.map((q,i)=>q-a[i]);
 let n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];
 const l=Math.hypot(...n);n=n.map(v=>v/l);
 // Reflection in projection flips handedness: outward triangles facing viewer have negative Z normals.
 if(n[2]>0)continue;
 const light=.40+.60*Math.max(0,n[0]*-.25+n[1]*.75+n[2]*-.60);
 const rgb=p.color.map(c=>Math.round(255*Math.pow(c,.4545)*light));
 faces.push({depth:vs.reduce((s,v)=>s+v[2],0)/3,points:vs.map(v=>[680+v[0]*1040,355-v[1]*1040]),rgb});
}
faces.sort((a,b)=>a.depth-b.depth);
fs.writeFileSync(path.join(__dirname,'preview-polygons.json'),JSON.stringify(faces));
console.log('Prepared '+faces.length+' visible preview triangles.');
// Depth-buffered offline rendering avoids painter-order errors on long intersecting surfaces.
const width=1360,height=650,pixels=Buffer.alloc(width*height*4),depth=new Float64Array(width*height);
depth.fill(-Infinity);
for(let i=0;i<width*height;i++){pixels[i*4]=22;pixels[i*4+1]=28;pixels[i*4+2]=36;pixels[i*4+3]=255;}
for(const p of data.parts)for(const f of p.f){
 const vs=f.map(i=>rotate(p.v[i])),[a,b,c]=vs;
 const u=b.map((v,i)=>v-a[i]),v=c.map((q,i)=>q-a[i]);
 const n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]],l=Math.hypot(...n);
 if(n[2]>0)continue;
 const light=.40+.60*Math.max(0,(n[0]*-.25+n[1]*.75+n[2]*-.60)/l);
 const rgb=p.color.map(c=>Math.round(255*Math.pow(c,.4545)*light));
 const q=vs.map(v=>[680+v[0]*1040,355-v[1]*1040,v[2]]);
 const [p0,p1,p2]=q,den=(p1[1]-p2[1])*(p0[0]-p2[0])+(p2[0]-p1[0])*(p0[1]-p2[1]);
 if(Math.abs(den)<1e-9)continue;
 const x0=Math.max(0,Math.floor(Math.min(...q.map(v=>v[0])))),x1=Math.min(width-1,Math.ceil(Math.max(...q.map(v=>v[0]))));
 const y0=Math.max(0,Math.floor(Math.min(...q.map(v=>v[1])))),y1=Math.min(height-1,Math.ceil(Math.max(...q.map(v=>v[1]))));
 for(let y=y0;y<=y1;y++)for(let x=x0;x<=x1;x++){
  const aa=((p1[1]-p2[1])*(x+.5-p2[0])+(p2[0]-p1[0])*(y+.5-p2[1]))/den;
  const bb=((p2[1]-p0[1])*(x+.5-p2[0])+(p0[0]-p2[0])*(y+.5-p2[1]))/den,cc=1-aa-bb;
  if(aa<-.00001||bb<-.00001||cc<-.00001)continue;
  const z=aa*p0[2]+bb*p1[2]+cc*p2[2],i=y*width+x;
  if(z<=depth[i])continue;
  depth[i]=z;pixels[i*4]=rgb[0];pixels[i*4+1]=rgb[1];pixels[i*4+2]=rgb[2];
 }
}
const zlib=require('zlib');
function crc(buf){let c=0xffffffff;for(const v of buf){c^=v;for(let k=0;k<8;k++)c=(c>>>1)^((c&1)?0xedb88320:0);}return (c^0xffffffff)>>>0;}
function chunk(type,data){const b=Buffer.alloc(data.length+12);b.writeUInt32BE(data.length,0);b.write(type,4);data.copy(b,8);b.writeUInt32BE(crc(b.subarray(4,-4)),b.length-4);return b;}
const header=Buffer.alloc(13);header.writeUInt32BE(width,0);header.writeUInt32BE(height,4);header[8]=8;header[9]=6;
const raw=Buffer.alloc(height*(width*4+1));
for(let y=0;y<height;y++)pixels.copy(raw,y*(width*4+1)+1,y*width*4,(y+1)*width*4);
fs.writeFileSync(path.join(__dirname,'M870-preview.png'),Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',header),chunk('IDAT',zlib.deflateSync(raw)),chunk('IEND',Buffer.alloc(0))]));
console.log('Depth-buffered source geometry preview saved.');
