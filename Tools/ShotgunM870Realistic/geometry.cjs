function mesh(){return{v:[],uv:[],f:[]};}
function vertex(m,p,uv=[p[2]*2+.5,p[1]*5+.5]){m.uv.push(uv);return m.v.push(p)-1;}
function tri(m,a,b,c){m.f.push([a,b,c]);}
function quad(m,a,b,c,d){tri(m,a,b,c);tri(m,a,c,d);}
function append(m,b){const o=m.v.length;m.v.push(...b.v);m.uv.push(...b.uv);m.f.push(...b.f.map(f=>f.map(i=>i+o)));return m;}
function translate(m,x,y,z){m.v=m.v.map(p=>[p[0]+x,p[1]+y,p[2]+z]);return m;}
function lathe(rings,cy=0,n=64,caps=true){
 const m=mesh(),count=n+1,zmin=Math.min(...rings.map(r=>r[0])),span=Math.max(...rings.map(r=>r[0]))-zmin||1;
 for(const[z,r]of rings)for(let i=0;i<=n;i++){const a=i/n*2*Math.PI;vertex(m,[Math.cos(a)*r,cy+Math.sin(a)*r,z],[(z-zmin)/span,i/n]);}
 for(let k=0;k<rings.length-1;k++)for(let i=0;i<n;i++)quad(m,k*count+i,k*count+i+1,(k+1)*count+i+1,(k+1)*count+i);
 if(caps)for(const[k,flip]of [[0,true],[rings.length-1,false]]){const c=vertex(m,[0,cy,rings[k][0]],[.5,.5]);for(let i=0;i<n;i++)flip?tri(m,c,k*count+i+1,k*count+i):tri(m,c,k*count+i,k*count+i+1);}
 return m;
}
// Rounded superellipse rings along Z. V is circumference, U follows length/wood grain.
function loft(sections,n=64,power=2.5){
 const m=mesh(),count=n+1,min=sections[0][0],span=sections.at(-1)[0]-min;
 for(const[z,cy,w,h]of sections)for(let i=0;i<=n;i++){const a=i/n*2*Math.PI,c=Math.cos(a),s=Math.sin(a);vertex(m,[w*Math.sign(c)*Math.abs(c)**(2/power),cy+h*Math.sign(s)*Math.abs(s)**(2/power),z],[(z-min)/span,i/n]);}
 for(let k=0;k<sections.length-1;k++)for(let i=0;i<n;i++)quad(m,k*count+i,k*count+i+1,(k+1)*count+i+1,(k+1)*count+i);
 for(const[k,flip]of [[0,true],[sections.length-1,false]]){const c=vertex(m,[0,sections[k][1],sections[k][0]],[.5,.5]);for(let i=0;i<n;i++)flip?tri(m,c,k*count+i+1,k*count+i):tri(m,c,k*count+i,k*count+i+1);}
 return m;
}
function cross2(a,b,c){return(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);}
function triangulate(points){
 const result=[],remaining=points.map((_,i)=>i);let guard=0;
 while(remaining.length>3&&guard++<points.length*points.length){let clipped=false;for(let k=0;k<remaining.length;k++){
  const a=remaining[(k+remaining.length-1)%remaining.length],b=remaining[k],c=remaining[(k+1)%remaining.length];
  if(cross2(points[a],points[b],points[c])<=1e-12)continue;
  if(remaining.some(i=>i!==a&&i!==b&&i!==c&&cross2(points[a],points[b],points[i])>=-1e-12&&cross2(points[b],points[c],points[i])>=-1e-12&&cross2(points[c],points[a],points[i])>=-1e-12))continue;
  result.push([a,b,c]);remaining.splice(k,1);clipped=true;break;
 }if(!clipped)throw Error('Unable to triangulate profile');}
 if(remaining.length===3)result.push(remaining);return result;
}
function profile(points,width,bevel=.002){
 let area=0;for(let i=0;i<points.length;i++){const p=points[i],q=points[(i+1)%points.length];area+=p[0]*q[1]-q[0]*p[1];}if(area<0)points=[...points].reverse();
 const m=mesh(),n=points.length,cy=points.reduce((s,p)=>s+p[0],0)/n,cz=points.reduce((s,p)=>s+p[1],0)/n;
 for(const[x,scale]of [[-width/2,.96],[-width/2+bevel,1],[width/2-bevel,1],[width/2,.96]])for(const[y,z]of points)vertex(m,[x,cy+(y-cy)*scale,cz+(z-cz)*scale]);
 for(let k=0;k<3;k++)for(let i=0;i<n;i++){const j=(i+1)%n;quad(m,k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i);}
 for(const f of triangulate(points)){tri(m,...[...f].reverse());tri(m,...f.map(i=>i+3*n));}
 return m;
}
function box(x,y,z,sx,sy,sz){return translate(profile([[y-sy/2,z-sz/2],[y+sy/2,z-sz/2],[y+sy/2,z+sz/2],[y-sy/2,z+sz/2]],sx,Math.min(.001,sx/6)),x,0,0);}
function loop(points,width,thickness){
 const m=mesh(),n=points.length;
 for(let i=0;i<n;i++){const a=points[(i+n-1)%n],b=points[(i+1)%n],dy=b[0]-a[0],dz=b[1]-a[1],l=Math.hypot(dy,dz),p=points[i];for(const[x,s]of [[-width/2,-1],[-width/2,1],[width/2,1],[width/2,-1]])vertex(m,[x,p[0]-dz/l*thickness*s/2,p[1]+dy/l*thickness*s/2],[i/n,s*.5+.5]);}
 for(let i=0;i<n;i++){const j=(i+1)%n;for(let k=0;k<4;k++)quad(m,4*i+k,4*i+(k+1)%4,4*j+(k+1)%4,4*j+k);}return m;
}
function receiver(){
 const m=mesh();
 // Planar sides beneath an actual curved canopy, with rounded shoulders into the stock.
 const cross=[[.034,-.030],[.034,-.014],[.034,.026],[.034,.029]];
 for(let i=1;i<=20;i++){const a=i/20*Math.PI;cross.push([.034*Math.cos(a),.029+.020*Math.sin(a)]);}
 cross.push([-.034,-.030]);
 for(let i=1;i<=8;i++){const a=Math.PI+i/8*Math.PI/2;cross.push([-.026+.008*Math.cos(a),-.030+.010*Math.sin(a)]);}
 cross.push([.026,-.040]);
 for(let i=1;i<8;i++){const a=1.5*Math.PI+i/8*Math.PI/2;cross.push([.026+.008*Math.cos(a),-.030+.010*Math.sin(a)]);}
 const n=cross.length;
 // Z / half-width multiplier / roof height / underside height.
 const sections=[[-.145,.73,.028,-.026],[-.143,.79,.034,-.030],[-.140,.86,.039,-.033],[-.135,.94,.044,-.037],[-.128,.99,.048,-.039],[-.120,1,.050,-.040],[-.102,1,.052,-.040],[-.075,1,.052,-.040],[-.045,1,.051,-.040],[-.004,1,.050,-.040],[.040,1,.048,-.040],[.094,1,.046,-.040],[.104,.99,.044,-.039],[.112,.95,.042,-.037],[.118,.91,.040,-.035],[.120,.90,.039,-.034]];
 for(const[z,s,top,bottom]of sections)for(const[x,y]of cross){
  const yy=y>.026?.026+(y-.026)*(top-.026)/.023:y<-.014?-.014+(y+.014)*(bottom+.014)/(-.026):y;
  vertex(m,[x*s,yy,z],[(z+.145)/.265,(y+.04)/.089]);
 }
 for(let k=0;k<sections.length-1;k++)for(let i=0;i<n;i++){
  const next=cross[(i+1)%n];
  const bottomEdge=Math.abs(cross[i][1]+.040)<1e-8&&Math.abs(next[1]+.040)<1e-8;
  if(sections[k][0]>=-.004&&sections[k+1][0]<=.094&&(i===1||bottomEdge))continue;
  const j=(i+1)%n;quad(m,k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i);
 }
 for(const[k,flip]of [[0,true],[sections.length-1,false]]){const c=vertex(m,[0,0,sections[k][0]],[.5,.5]);for(let i=0;i<n;i++){const j=(i+1)%n;flip?tri(m,c,k*n+j,k*n+i):tri(m,c,k*n+i,k*n+j);}}
 // Inner bevel and walls around the right-side ejection aperture.
 const rim=[[.034,-.014,-.004],[.034,.026,-.004],[.034,.026,.094],[.034,-.014,.094]];
 const inset=rim.map(([x,y,z])=>[.030,y+(y>0?-.0015:.0015),z+(z>0?-.0015:.0015)]);
 const inner=inset.map(([x,y,z])=>[.020,y,z]);
 for(const ring of [rim,inset,inner])for(const p of ring)vertex(m,p);
 const o=m.v.length-12;
 for(let k=0;k<2;k++)for(let i=0;i<4;i++)quad(m,o+k*4+i,o+k*4+(i+1)%4,o+(k+1)*4+(i+1)%4,o+(k+1)*4+i);
 return m;
}
function validate(parts){
 for(const p of parts){const m=p.m;if(m.v.length!==m.uv.length)throw Error('UV mismatch '+p.name);for(const uv of m.uv)if(!uv.every(Number.isFinite))throw Error('Invalid UV');for(const f of m.f){const[a,b,c]=f.map(i=>m.v[i]);if(!a||!b||!c||![...a,...b,...c].every(Number.isFinite))throw Error('Invalid vertices');const u=b.map((x,i)=>x-a[i]),v=c.map((x,i)=>x-a[i]);if(Math.hypot(u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])<1e-12)throw Error('Degenerate triangle '+p.name);}}
}
module.exports={mesh,vertex,tri,quad,append,translate,lathe,loft,profile,box,loop,receiver,validate};
