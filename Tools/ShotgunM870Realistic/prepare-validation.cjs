// Build a small disposable Unity import project using installed/cached packages only.
const fs=require('fs'),path=require('path');
const {root,out}=require('./assets.cjs');
const project=path.join(root,'Library/M870AssetValidation');
fs.mkdirSync(path.join(project,'Assets/Editor'),{recursive:true});
fs.mkdirSync(path.join(project,'Packages'),{recursive:true});
fs.mkdirSync(path.join(project,'ProjectSettings'),{recursive:true});
fs.mkdirSync(path.join(project,'Assets/Prefab/Weapon'),{recursive:true});
fs.cpSync(out,path.join(project,'Assets/Prefab/Weapon/M870Realistic'),{recursive:true});
fs.copyFileSync(path.join(root,'Assets/Editor/M870RealisticImportValidator.cs'),path.join(project,'Assets/Editor/M870RealisticImportValidator.cs'));
fs.copyFileSync(path.join(__dirname,'UnityAssetValidation.cs'),path.join(project,'Assets/Editor/UnityAssetValidation.cs'));
fs.copyFileSync(path.join(root,'ProjectSettings/ProjectVersion.txt'),path.join(project,'ProjectSettings/ProjectVersion.txt'));
const cache=path.join(root,'Library/PackageCache'),packages={};
for(const dir of fs.readdirSync(cache)){const file=path.join(cache,dir,'package.json');if(fs.existsSync(file)){const p=JSON.parse(fs.readFileSync(file,'utf8'));packages[p.name]={path:path.join(cache,dir),data:p};}}
const deps={},pending=['com.unity.render-pipelines.universal'];
while(pending.length){const name=pending.pop();if(name in deps)continue;if(name.startsWith('com.unity.modules.')){deps[name]='1.0.0';continue;}const p=packages[name];if(!p)throw Error('Required package not cached: '+name);deps[name]='file:'+p.path.replaceAll('\\','/');pending.push(...Object.keys(p.data.dependencies||{}));}
for(const name of ['com.unity.modules.imageconversion','com.unity.modules.jsonserialize','com.unity.modules.animation','com.unity.modules.physics','com.unity.modules.ui'])deps[name]='1.0.0';
fs.writeFileSync(path.join(project,'Packages/manifest.json'),JSON.stringify({dependencies:deps},null,2));
console.log('Prepared isolated Unity project: '+project+' ('+Object.keys(deps).length+' cached/builtin dependencies)');
