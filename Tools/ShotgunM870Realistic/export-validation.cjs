const fs=require('fs'),path=require('path');
const {root,out}=require('./assets.cjs');
const project=path.join(root,'Library/M870AssetValidation'),reportFile=path.join(project,'validation-report.json');
async function run(){
 const generationTime=fs.statSync(path.join(__dirname,'manifest.json')).mtimeMs;
 const deadline=Date.now()+45000;
 while(Date.now()<deadline){
  if(fs.existsSync(reportFile)&&fs.statSync(reportFile).mtimeMs>=generationTime){
   const report=JSON.parse(fs.readFileSync(reportFile,'utf8'));
   if(!report.success||report.renderError)throw Error('Unity validation failed: '+report.renderError);
   const preview=path.join(project,'M870-unity-preview.png');
   if(fs.statSync(preview).mtimeMs<generationTime)throw Error('Preview is stale');
   fs.cpSync(path.join(project,'Assets/Prefab/Weapon/M870Realistic'),out,{recursive:true});
   fs.copyFileSync(reportFile,path.join(__dirname,'validation-report.json'));
   fs.copyFileSync(preview,path.join(__dirname,'M870-unity-preview.png'));
   const receiver=fs.readFileSync(path.join(out,'Meshes/Receiver.obj'),'utf8');
   if(!receiver.includes('\nvn ')||!receiver.includes('\ns off\n'))throw Error('Planar receiver normals missing');
   console.log('Exported Unity-validated revised prefab, scene and render. '+report.meshParts+' meshes / '+report.triangles+' triangles.');
   return;
  }
  await new Promise(r=>setTimeout(r,1000));
 }
 throw Error('No fresh Unity validation result within 45 seconds');
}
run().catch(e=>{console.error(e);process.exitCode=1;});
