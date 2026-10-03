const fs=require('fs'),path=require('path');
const a=require('./assets.cjs'),g=require('./geometry.cjs'),t=require('./textures.cjs');
const parts=[];
function part(name,material,m,parent='Visual',position=[0,0,0]){parts.push({name,material,m,parent,position});return m;}
part('Receiver','Steel',g.receiver());
part('Stock','StockWood',g.loft([
 [-.472,-.068,.032,.070],[-.465,-.067,.035,.073],[-.445,-.061,.036,.071],[-.408,-.054,.035,.067],[-.362,-.041,.032,.058],[-.315,-.025,.029,.049],[-.277,-.013,.024,.036],[-.253,-.016,.021,.034],[-.237,-.024,.021,.038],[-.222,-.027,.021,.045],[-.207,-.021,.021,.041],[-.194,-.014,.020,.036],[-.180,-.008,.020,.030],[-.164,-.003,.020,.027],[-.145,.002,.020,.025]],80,2.45));
part('RecoilPad','Rubber',g.loft([[-.489,-.068,.030,.068],[-.486,-.068,.033,.071],[-.476,-.068,.035,.073],[-.472,-.068,.034,.072]],64,3));
part('StockSpacer','Steel',g.loft([[-.473,-.068,.033,.071],[-.471,-.068,.033,.071]],64,3));
part('Barrel','Steel',g.lathe([[.104,.017],[.119,.017],[.137,.0158],[.179,.0152],[.4,.0134],[.583,.0128],[.594,.0128],[.596,.0122],[.596,.0093],[.58,.0093],[.12,.0093]],.032,80,false));
part('BoreBack','Bore',g.lathe([[.121,.0092],[.122,.0092]],.032,48));
part('MagazineTube','Steel',g.lathe([[.103,.012],[.112,.013],[.48,.013],[.488,.0125]],-.010,64));
const cap=g.lathe([[.486,.013],[.488,.015],[.501,.015],[.505,.013]],-.010,64);
for(let i=0;i<4;i++)g.append(cap,g.lathe([[.49+i*.003,.015],[.491+i*.003,.0154]],-.010,64,false));
part('MagazineCap','Steel',cap);
// Continuous forend surface with shaped rib valleys; no overlapping rib shells.
const pumpRings=[[-.093,.021],[-.090,.023],[-.084,.0245],[-.077,.0245]];
for(let i=0;i<12;i++){const z=-.073+i*.0114;pumpRings.push([z,.0245],[z+.002,.0245],[z+.004,.0240],[z+.006,.0230],[z+.008,.0240],[z+.010,.0245]);}
pumpRings.push([.069,.0245],[.077,.0245],[.085,.023],[.088,.021]);
const forend=g.lathe(pumpRings,0,80);
forend.v=forend.v.map(([x,y,z])=>{
 const radius=Math.hypot(x,y);
 if(z<-.073||z>.063||radius<.001)return[x,y,z];
 // Reference has a smooth upper strip and flutes concentrated on the side/underside.
 const influence=Math.max(0,Math.min(1,(.55-y/radius)/.65));
 const target=.0245+(radius-.0245)*influence;
 return[x*target/radius,y*target/radius,z];
});
part('Forend','PumpWood',forend,'Pump',[0,-.010,.264]);
part('ActionBarLeft','Steel',g.box(-.025,-.021,.127,.005,.010,.144),'Pump');
part('ActionBarRight','Steel',g.box(.025,-.021,.127,.005,.010,.144),'Pump');
part('PortInterior','Bore',g.box(.015,.005,.042,.005,.037,.092));
part('BoltBody','BoltSteel',g.box(.024,.007,.026,.013,.028,.069),'Bolt');
part('BoltExtractor','Steel',g.box(.031,.005,.055,.003,.016,.009),'Bolt');
part('BoltSeam','Bore',g.box(.0311,.005,.046,.001,.025,.0015),'Bolt');
part('LoadingWell','Bore',g.box(0,-.027,.043,.044,.008,.090));
part('LoadingGate','BoltSteel',g.profile([[-.032,-.002],[-.031,.059],[-.035,.081],[-.037,.08],[-.035,.020],[-.036,-.002]],.026,.001));
const guardPoints=[];
for(let i=0;i<48;i++){const q=i/48*2*Math.PI;guardPoints.push([-.063+.027*Math.sin(q),-.065+.051*Math.cos(q)]);}
part('TriggerGuard','Steel',g.loop(guardPoints,.013,.006));
part('Trigger','BoltSteel',g.profile([[-.041,-.059],[-.049,-.060],[-.057,-.057],[-.064,-.053],[-.071,-.056],[-.075,-.063],[-.076,-.071],[-.073,-.074],[-.070,-.066],[-.065,-.064],[-.056,-.068],[-.044,-.069]],.008,.0009));
part('TriggerAssemblyPlate','Steel',g.box(0,-.039,-.064,.042,.008,.105));
const safety=g.lathe([[-.006,.004],[.006,.004]],0,32);
safety.v=safety.v.map(([x,y,z])=>[z+.008,y-.049,-x-.107]);
part('CrossBoltSafety','BoltSteel',safety);
for(const z of [-.091,-.019])for(const side of [-1,1]){
 const pin=g.lathe([[-.0006,.0031],[.0006,.0031]],0,32);
 pin.v=pin.v.map(([x,y,zz])=>[zz+side*.0344,y-.019,-x+z]);
 part((z<-.05?'Rear':'Front')+'ReceiverPin'+(side>0?'Right':'Left'),'BoltSteel',pin);
 part((z<-.05?'Rear':'Front')+'PinSlot'+(side>0?'Right':'Left'),'Bore',g.box(side*.0351,-.019,z,.0004,.00065,.0036));
}
part('BarrelLug','Steel',g.box(0,.008,.475,.016,.023,.016));
part('FrontSightRamp','Steel',g.profile([[.044,.569],[.049,.577],[.049,.585],[.044,.589]],.006,.0006));
part('FrontBead','Brass',g.lathe([[.581,.0008],[.582,.0018],[.5835,.002],[.585,.0018],[.586,.0008]],.050,32));
part('StockSlingStud','Steel',g.box(0,-.098,-.353,.009,.012,.009));
const slingPts=[];for(let i=0;i<32;i++){const q=i/32*2*Math.PI;slingPts.push([-.109+.006*Math.sin(q),-.353+.008*Math.cos(q)]);}
part('RearSlingLoop','Steel',g.loop(slingPts,.003,.002));
// Recoil pad surface ribs are actual geometry, making the back silhouette visible in close shots.
const padRibs=g.mesh();
for(let i=0;i<13;i++)g.append(padRibs,g.box(0,-.127+i*.0095,-.489,.049,.0018,.0015));
part('PadGripRibs','Rubber',padRibs);
// Keep hardware seated in the slimmer receiver, including the recessed bolt and pins.
for(const p of parts){
 if(['Receiver','PortInterior','BoltBody','BoltExtractor','BoltSeam','LoadingWell','LoadingGate'].includes(p.name)||/ReceiverPin|PinSlot/.test(p.name))
  p.m.v=p.m.v.map(([x,y,z])=>[x*.84,y*.95,z]);
}
g.validate(parts);
const {maps,pixelsByName}=t.all();
const mats={Steel:a.material('BluedSteel',maps.BluedSteel,.92,.75),BoltSteel:a.material('BoltSteel',maps.BluedSteel,.92,.86,[2.4,2.5,2.6]),StockWood:a.material('CheckeredWalnut',maps.StockWalnut,0,.68),PumpWood:a.material('PumpWalnut',maps.PumpWalnut,0,.68),Rubber:a.material('RecoilRubber',maps.RecoilRubber,0,.65),Bore:a.material('Interior',maps.BluedSteel,.3,.15,[.09,.09,.09]),Brass:a.material('FrontBeadBrass',maps.BluedSteel,.9,.9,[2.6,1.7,.65])};
a.prefab(parts,mats);
const triangles=parts.reduce((s,p)=>s+p.m.f.length,0);
const manifest={triangles,meshParts:parts.length,approximateLengthMeters:1.085,textures:{wood:2048,steel:1024,rubber:512},parts:parts.map(p=>({name:p.name,material:p.material,parent:p.parent,position:p.position,vertices:p.m.v.length,triangles:p.m.f.length}))};
fs.writeFileSync(path.join(__dirname,'manifest.json'),JSON.stringify(manifest,null,2));
fs.writeFileSync(path.join(__dirname,'preview-data.json'),JSON.stringify({parts:parts.map(p=>({...p,v:p.m.v.map(v=>v.map((q,i)=>q+p.position[i])),uv:p.m.uv,f:p.m.f,m:undefined})),triangles}));
// Source geometry preview uses the same UVs and texture bytes as the exported asset.
require('./preview.cjs').render(parts,pixelsByName);
console.log(`Created realistic M870: ${parts.length} mesh parts; ${triangles} triangles; geometry, UV and prefab-reference checks passed.`);
