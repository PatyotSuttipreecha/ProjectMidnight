const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../..'),project=path.join(root,'Library/Glock19BaseValidation');
async function run(){
 const sourceTime=fs.statSync(path.join(__dirname,'manifest.json')).mtimeMs,success=path.join(project,'validation-success.txt');
 const deadline=Date.now()+55000;
 while(Date.now()<deadline){
  if(fs.existsSync(success)&&fs.statSync(success).mtimeMs>=sourceTime){
   const out=path.join(root,'Assets/Prefab/Weapon/Glock19Base'),source=path.join(project,'Assets/Prefab/Weapon/Glock19Base');
   fs.cpSync(source,out,{recursive:true});
   if(!fs.existsSync(out+'.meta'))fs.copyFileSync(source+'.meta',out+'.meta');
   fs.copyFileSync(path.join(project,'Glock19-preview.png'),path.join(__dirname,'Glock19-preview.png'));
   fs.copyFileSync(success,path.join(__dirname,'validation-success.txt'));
   const editor=path.join(root,'Assets/Editor/Glock19BaseBuilder.cs');
   if(!fs.existsSync(editor+'.meta'))fs.copyFileSync(path.join(project,'Assets/Editor/Glock19BaseBuilder.cs.meta'),editor+'.meta');
   console.log(fs.readFileSync(success,'utf8'));return;
  }
  await new Promise(r=>setTimeout(r,1000));
 }
 throw Error('Unity has not completed validation yet; inspect unity-validation.log.');
}
run().catch(e=>{console.error(e.message);process.exitCode=1;});
