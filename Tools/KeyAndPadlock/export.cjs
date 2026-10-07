const fs=require('fs'),path=require('path'),root=path.resolve(__dirname,'../..');
const project=path.join(root,'Library/KeyAndPadlockValidation');
async function run(){const after=fs.statSync(path.join(__dirname,'manifest.json')).mtimeMs,report=path.join(project,'validation-success.txt'),end=Date.now()+55000;
 while(Date.now()<end){if(fs.existsSync(report)&&fs.statSync(report).mtimeMs>=after){
  const src=path.join(project,'Assets/Prefab/Props/KeyAndPadlock'),dst=path.join(root,'Assets/Prefab/Props/KeyAndPadlock');fs.mkdirSync(path.dirname(dst),{recursive:true});fs.cpSync(src,dst,{recursive:true});
  for(const name of ['Props','Props/KeyAndPadlock']){const file=path.join(root,'Assets/Prefab',name+'.meta');if(!fs.existsSync(file))fs.copyFileSync(path.join(project,'Assets/Prefab',name+'.meta'),file);}
  fs.copyFileSync(path.join(project,'KeyAndPadlock-preview.png'),path.join(__dirname,'KeyAndPadlock-preview.png'));fs.copyFileSync(report,path.join(__dirname,'validation-success.txt'));console.log(fs.readFileSync(report,'utf8'));return;}
  await new Promise(r=>setTimeout(r,1000));
 }throw Error('Unity validation has not finished; inspect unity-validation.log.');}
run().catch(e=>{console.error(e.message);process.exitCode=1;});
