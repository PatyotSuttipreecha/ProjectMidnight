const {texture}=require('./assets.cjs');
const clamp=(v,min=0,max=1)=>Math.max(min,Math.min(max,v));
function hash(x,y){let n=Math.imul(x,374761393)+Math.imul(y,668265263);n=Math.imul(n^(n>>>13),1274126177);return((n^(n>>>16))>>>0)/4294967295;}
function noise(u,v,n=16){let x=u*n,y=v*n,ix=Math.floor(x),iy=Math.floor(y),a=x-ix,b=y-iy;a=a*a*(3-2*a);b=b*b*(3-2*b);const h=(i,j)=>hash((i%n+n)%n,(j%n+n)%n);return(h(ix,iy)*(1-a)+h(ix+1,iy)*a)*(1-b)+(h(ix,iy+1)*(1-a)+h(ix+1,iy+1)*a)*b;}
const smooth=(a,b,x)=>{const t=clamp((x-a)/(b-a));return t*t*(3-2*t);};
const maps={},pixelsByName={};
function generate(name,size,kind){
 const base=Buffer.alloc(size*size*4),mask=Buffer.alloc(size*size*4),normal=Buffer.alloc(size*size*4),height=new Float32Array(size*size);
 for(let y=0;y<size;y++)for(let x=0;x<size;x++){
  const u=x/size,v=y/size,i=y*size+x,o=i*4,fine=hash(x,y)-.5,coarse=noise(u,v,12),mid=noise(u,v,64);
  let rgb,h,metal,smoothness;
  if(kind==='wood'||kind==='stock'){
   const warp=noise(u,v,4)*24+noise(u,v,16)*3+Math.sin(u*Math.PI*4)*.7;
   const grain=Math.sin(v*Math.PI*180+warp+Math.sin(u*6*Math.PI+v*14)*1.2);
   const thin=Math.sin(v*Math.PI*620+warp*2+u*9);
   const pores=Math.pow(Math.max(0,Math.sin(v*Math.PI*1300+warp*3+u*15)),18)*(.5+.5*mid);
   const tone=.85+coarse*.26+grain*.035+thin*.009-pores*.08+fine*.025;
   rgb=[98*tone,51*tone,27*tone];h=grain*.005+thin*.002-pores*.018+fine*.0015;metal=0;smoothness=.35+coarse*.12-pores*.08;
   if(kind==='stock'){
    const side=Math.min(Math.abs(v-.5),Math.min(v,1-v));
    const patch=smooth(.64,.66,u)*(1-smooth(.79,.815,u))*(1-smooth(.115,.14,side));
    const diamond=Math.min(Math.abs(Math.sin((u*92+v*72)*Math.PI)),Math.abs(Math.sin((u*92-v*72)*Math.PI)));
    const groove=(1-smooth(.06,.24,diamond))*patch;
    rgb=rgb.map(c=>c*(1-patch*.12-groove*.22));h-=groove*.10;smoothness-=patch*.20;
   }
  }else if(kind==='rubber'){
   const pore=fine*.08+Math.max(0,mid-.65)*.2;
   rgb=[20+fine*5,21+fine*5,22+fine*5];h=pore;metal=0;smoothness=.12+mid*.06;
  }else{
   const brushing=Math.sin(v*size*.7+noise(u,v,32)*.7)*.012;
   const variation=.87+coarse*.20+fine*.025;
   rgb=[47*variation,53*variation,60*variation];h=fine*.003+brushing*.002;metal=.92;smoothness=.66+coarse*.12+brushing;
   // Very restrained surface scratches; no random giant damage patches.
   const scratch=Math.abs(Math.sin((v+noise(u,v,8)*.004)*Math.PI*950));
   if(scratch<.018&&mid>.57){rgb=rgb.map(c=>c+8);smoothness-=.14;h-=.004;}
  }
  height[i]=h;
  for(let k=0;k<3;k++)base[o+k]=Math.round(clamp(rgb[k],0,255));base[o+3]=255;
  mask[o]=Math.round(clamp(metal)*255);mask[o+1]=0;mask[o+2]=0;mask[o+3]=Math.round(clamp(smoothness)*255);
 }
 for(let y=0;y<size;y++)for(let x=0;x<size;x++){
  const i=y*size+x,o=i*4,dx=(height[y*size+(x+1)%size]-height[y*size+(x+size-1)%size])*3;
  const dy=(height[((y+1)%size)*size+x]-height[((y+size-1)%size)*size+x])*3;
  const len=Math.hypot(dx,dy,1);normal[o]=Math.round((-dx/len*.5+.5)*255);normal[o+1]=Math.round((dy/len*.5+.5)*255);normal[o+2]=Math.round((1/len*.5+.5)*255);normal[o+3]=255;
 }
 maps[name]={base:texture(name+'_BaseColor',size,base,'base'),normal:texture(name+'_Normal',size,normal,'normal'),mask:texture(name+'_MetallicSmoothness',size,mask,'mask')};
 pixelsByName[name]={size,base,normal,mask};
 console.log('Generated '+name+' PBR textures ('+size+'px).');
}
function all(){generate('StockWalnut',2048,'stock');generate('PumpWalnut',2048,'wood');generate('BluedSteel',1024,'steel');generate('RecoilRubber',512,'rubber');return{maps,pixelsByName};}
module.exports={all,clamp};
