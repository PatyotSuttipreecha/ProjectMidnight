const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../..');
const source=fs.readFileSync(path.join(root,'Tools/IsanHouseLowPoly/IsanHouseBuilder.cs'),'utf8');
for(const variant of ['Cottage','SideWorkshop']){
 const folder=`Assets/Prefab/Environment/IsanHouse${variant}LowPoly/`;
 const name=`Prefab_Environment_IsanHouse_${variant}_LowPoly`;
 let code=source.replaceAll('IsanHouseBuilder',`IsanHouse${variant}Builder`).replaceAll('Assets/Prefab/Environment/IsanHouseLowPoly/',folder).replaceAll('Prefab_Environment_IsanAbandonedHouse_LowPoly',name).replaceAll('house-success.txt',`${variant}-success.txt`).replaceAll('IsanHouse-exterior.png',`${variant}-exterior.png`).replaceAll('IsanHouse-interior.png',`${variant}-interior.png`).replaceAll('IsanHouse_Preview.unity',`${variant}_Preview.unity`);
 const marker='            Physics.SyncTransforms();';
 const alterations=variant==='Cottage'?`
            UnityEngine.Object.DestroyImmediate(root.Find("04_InteriorPartitions").gameObject);
            // Smaller footprint, same full standing height. Bake the plan scale into section transforms.
            foreach(Transform section in root){var p=section.localPosition;section.localPosition=new Vector3(p.x*.8f,p.y,p.z*.85f);var s=section.localScale;section.localScale=new Vector3(s.x*.8f,s.y,s.z*.85f);}
            mats["RoofMetal"].SetColor("_BaseColor",new Color(.30f,.24f,.18f));
            mats["FadedWood"].SetColor("_BaseColor",new Color(.43f,.37f,.29f));
`:`
            // A separate raised side workshop with its own front stair and lean-to roof.
            var annex=Group(root,"10_SideWorkshop");
            Box(annex,"WorkshopDeck",new Vector3(-6.5f,1.7f,0),new Vector3(3,.2f,6),"FadedWood");
            foreach(float z in new[]{-2.8f,0,2.8f}){Box(annex,"AnnexStilt",new Vector3(-7.8f,.9f,z),new Vector3(.2f,1.8f,.2f),"DarkWood");Box(annex,"AnnexPost",new Vector3(-7.8f,2.9f,z),new Vector3(.18f,2.2f,.18f),"DarkWood");}
            Wall(annex,-3,3,-8,1.8f,3.9f,true);Wall(annex,-8,-5,3,1.8f,3.9f,false);
            var lean=Box(annex,"WorkshopLeanToRoof",new Vector3(-6.5f,4.35f,0),new Vector3(3.4f,.12f,6.6f),"Rust");lean.localRotation=Quaternion.Euler(0,0,17);
            for(int i=0;i<10;i++){float h=(10-i)*.18f;Box(annex,"WorkshopFrontStep",new Vector3(-6.5f,h/2,-3-(i+.5f)*.32f),new Vector3(1.8f,h,.32f),"FadedWood");}
            Table(annex,new Vector3(-6.5f,1.8f,1),"WorkshopBench");
            Box(annex,"WoodCrate",new Vector3(-6.7f,2.1f,-1.5f),new Vector3(.7f,.6f,.7f),"Timber");
            // Remove the old side-room partition and replace it with a rear storage room.
            UnityEngine.Object.DestroyImmediate(root.Find("04_InteriorPartitions").gameObject);
            var partitions=Group(root,"04_RearStoragePartition");Wall(partitions,-5,-1,1,1.8f,4.8f,false);Wall(partitions,1,5,1,1.8f,4.8f,false);Wall(partitions,-1,1,1,4.2f,4.8f,false);
`;
 code=code.replace(marker,alterations+marker);
 code=code.replace('new Vector3(.95f,1.8f,4.15f),105','new Vector3(.95f,1.8f,4.15f),-105');
 if(variant==='Cottage')code=code.replace('new[]{-5,-3,0,3}','new[]{-4,-2,0,2}');
 if(variant==='SideWorkshop')code=code.replace('c.orthographicSize=8.3f','c.orthographicSize=9.3f');
 // Get exact triangle count after removed partitions, rather than the creation counter.
 code=code.replace('            CombineSections(root);','            triangles=0;foreach(var meshFilter in root.GetComponentsInChildren<MeshFilter>())triangles+=meshFilter.sharedMesh.triangles.Length/3;\n            CombineSections(root);');
 const project=path.join(root,`Library/IsanHouse${variant}Validation`);
 for(const dir of ['Assets/Editor','Packages','ProjectSettings'])fs.mkdirSync(path.join(project,dir),{recursive:true});
 fs.writeFileSync(path.join(__dirname,`IsanHouse${variant}Builder.cs`),code);fs.writeFileSync(path.join(project,`Assets/Editor/IsanHouse${variant}Builder.cs`),code);
 fs.copyFileSync(path.join(root,'Library/KeyAndPadlockValidation/Packages/manifest.json'),path.join(project,'Packages/manifest.json'));fs.copyFileSync(path.join(root,'ProjectSettings/ProjectVersion.txt'),path.join(project,'ProjectSettings/ProjectVersion.txt'));
 console.log(name);
}
