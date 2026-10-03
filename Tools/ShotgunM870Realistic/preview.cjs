const fs=require('fs'),path=require('path');
const {png}=require('./assets.cjs');
const dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0),sub=(a,b)=>a.map((v,i)=>v-b[i]);
const cross=(u,v)=>[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];
const norm=v=>{const l=Math.hypot(...v)||1;return v.map(q=>q/l);};
const mapNames={Steel:'BluedSteel',BoltSteel:'BluedSteel',StockWood:'StockWalnut',PumpWood:'PumpWalnut',Rubber:'RecoilRubber',Bore:'BluedSteel',Brass:'BluedSteel'};
const tint={BoltSteel:[2.4,2.5,2.6],Bore:[.09,.09,.09],Brass:[2.6,1.7,.65]};
function render(parts,textures){
 for(const setup of [{file:'M870-realistic-preview.png',yaw:.48,pitch:-.18,scale:1440,target:[0,-.015,.02],width:1800,height:850},{file:'M870-detail-preview.png',yaw:1.00,pitch:-.28,scale:3100,target:[0,.004,.18],width:1800,height:1050}]){
 const {width,height,yaw,pitch,scale,target}=setup,pixels=Buffer.alloc(width*height*4),depth=new Float64Array(width*height);depth.fill(-Infinity);
 function rotate([x,y,z]){const a=x*Math.cos(yaw)+z*Math.sin(yaw),c=-x*Math.sin(yaw)+z*Math.cos(yaw);return[c,a*Math.sin(pitch)+y*Math.cos(pitch),a*Math.cos(pitch)-y*Math.sin(pitch)];}
 for(let y=0;y<height;y++)for(let x=0;x<width;x++){const i=(y*width+x)*4,k=Math.max(0,1-Math.hypot((x-width*.5)/(width*.7),(y-height*.45)/(height*.75)));pixels[i]=20+k*14;pixels[i+1]=25+k*16;pixels[i+2]=32+k*20;pixels[i+3]=255;}
 const lights=[{l:norm([-.4,.65,1]),strength:1.2},{l:norm([.8,.1,.65]),strength:.6},{l:norm([-.3,-.6,.7]),strength:.25}];
 for(const p of parts){
  const m=p.m,world=m.v.map(v=>v.map((x,i)=>x+p.position[i])),vs=world.map(v=>rotate(sub(v,target))),vn=m.v.map(()=>[0,0,0]);
  for(const f of m.f){const [a,b,c]=f.map(i=>m.v[i]),n=cross(sub(b,a),sub(c,a));for(const i of f)for(let j=0;j<3;j++)vn[i][j]+=n[j];}
  const normals=vn.map(v=>rotate(norm(v))),tex=textures[mapNames[p.material]],colorTint=tint[p.material]||[1,1,1];
  const sample=(buffer,u,v)=>{const tx=Math.min(tex.size-1,Math.floor(((u%1+1)%1)*tex.size)),ty=Math.min(tex.size-1,Math.floor(((1-v)%1+1)%1*tex.size)),o=(ty*tex.size+tx)*4;return[buffer[o]/255,buffer[o+1]/255,buffer[o+2]/255,buffer[o+3]/255];};
  for(const f of m.f){
   const [a,b,c]=f.map(i=>vs[i]),face=norm(cross(sub(b,a),sub(c,a)));if(face[2]>=0)continue;
   const q=f.map(i=>[width*.5+vs[i][0]*scale,height*.52-vs[i][1]*scale,vs[i][2]]),[p0,p1,p2]=q;
   const den=(p1[1]-p2[1])*(p0[0]-p2[0])+(p2[0]-p1[0])*(p0[1]-p2[1]);if(Math.abs(den)<1e-9)continue;
   const x0=Math.max(0,Math.floor(Math.min(...q.map(v=>v[0])))),x1=Math.min(width-1,Math.ceil(Math.max(...q.map(v=>v[0])))),y0=Math.max(0,Math.floor(Math.min(...q.map(v=>v[1])))),y1=Math.min(height-1,Math.ceil(Math.max(...q.map(v=>v[1]))));
   const uvs=f.map(i=>m.uv[i]),[uv0,uv1,uv2]=uvs,duv1=sub(uv1,uv0),duv2=sub(uv2,uv0),ud=duv1[0]*duv2[1]-duv1[1]*duv2[0];
   const e1=sub(b,a),e2=sub(c,a),tangent=Math.abs(ud)>1e-8?norm(e1.map((x,i)=>(x*duv2[1]-e2[i]*duv1[1])/ud)):[1,0,0];
   for(let y=y0;y<=y1;y++)for(let x=x0;x<=x1;x++){
    const aa=((p1[1]-p2[1])*(x+.5-p2[0])+(p2[0]-p1[0])*(y+.5-p2[1]))/den,bb=((p2[1]-p0[1])*(x+.5-p2[0])+(p0[0]-p2[0])*(y+.5-p2[1]))/den,cc=1-aa-bb;
    if(aa<-.00001||bb<-.00001||cc<-.00001)continue;const z=aa*p0[2]+bb*p1[2]+cc*p2[2],i=y*width+x;if(z<=depth[i])continue;depth[i]=z;
    const u=aa*uv0[0]+bb*uv1[0]+cc*uv2[0],v=aa*uv0[1]+bb*uv1[1]+cc*uv2[1],base=sample(tex.base,u,v),mask=sample(tex.mask,u,v),bump=sample(tex.normal,u,v);
    let n=p.name==='Receiver'?face.map(v=>-v):norm([0,1,2].map(j=>aa*normals[f[0]][j]+bb*normals[f[1]][j]+cc*normals[f[2]][j]));
    const bitangent=norm(cross(n,tangent));n=norm(n.map((v,j)=>v+(bump[0]*2-1)*tangent[j]*.55+(bump[1]*2-1)*bitangent[j]*.55));
    const metal=p.material==='Bore'?.3:mask[0],smooth=mask[3]*(p.material==='Rubber'?.65:p.material.includes('Wood')?.68:.75),exponent=8+smooth*smooth*240;
    const linear=base.slice(0,3).map((v,j)=>Math.pow(Math.min(1,v*colorTint[j]),2.2));
    let rgb=linear.map(v=>v*(metal>.5?.26:.28));
    for(const light of lights){const ndl=Math.max(0,dot(n,light.l)),h=norm([light.l[0],light.l[1],light.l[2]+1]),spec=Math.pow(Math.max(0,dot(n,h)),exponent)*light.strength;rgb=rgb.map((v,j)=>v+linear[j]*ndl*light.strength*(1-metal*.76)+(metal*linear[j]*2.8+(1-metal)*.045)*spec*3.5);}
    for(let j=0;j<3;j++)pixels[i*4+j]=Math.round(Math.pow(Math.max(0,Math.min(1,rgb[j])),1/2.2)*255);
   }
  }
 }
 fs.writeFileSync(path.join(__dirname,setup.file),png(width,height,pixels));console.log('Rendered '+setup.file);
 }
}
module.exports={render};
