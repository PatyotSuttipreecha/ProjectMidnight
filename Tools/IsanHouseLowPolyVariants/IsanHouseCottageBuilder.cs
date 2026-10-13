using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class IsanHouseCottageBuilder
{
    const string Folder="Assets/Prefab/Environment/IsanHouseCottageLowPoly/";
    static Dictionary<string,Material> mats=new Dictionary<string,Material>();
    static Transform root;
    static int triangles;
    public static void Run()
    {
        try
        {
            AssetDatabase.DeleteAsset(Folder.TrimEnd('/')); // Isolated generation project only.
            foreach(string p in new[]{"Assets/Prefab","Assets/Prefab/Environment",Folder.TrimEnd('/'),Folder+"Materials",Folder+"Meshes"})
                if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(Path.GetDirectoryName(p).Replace('\\','/'),Path.GetFileName(p));
            Material("Timber",new Color(.26f,.20f,.145f));Material("FadedWood",new Color(.40f,.34f,.265f));Material("DarkWood",new Color(.17f,.145f,.12f));Material("RoofMetal",new Color(.27f,.29f,.27f));Material("Rust",new Color(.34f,.22f,.145f));Material("Stone",new Color(.34f,.34f,.29f));
            root=Group(null,"Prefab_Environment_IsanHouse_Cottage_LowPoly");
            var foundation=Group(root,"01_FoundationAndStilts");
            foreach(float x in new[]{-4.8f,0,4.8f})foreach(float z in new[]{-5.7f,-3.8f,0,3.8f})
            {Box(foundation,"StoneFoot",new Vector3(x,.12f,z),new Vector3(.42f,.24f,.42f),"Stone");Box(foundation,"TimberPost",new Vector3(x,.92f,z),new Vector3(.22f,1.6f,.22f),"DarkWood");}
            foreach(float z in new[]{-5.8f,-4,0,4})Box(foundation,"FloorBeam",new Vector3(0,1.55f,z),new Vector3(10,.25f,.2f),"DarkWood");
            var floor=Group(root,"02_Floors");
            for(int i=0;i<25;i++)Box(floor,"Floorboard",new Vector3(-4.8f+i*.4f,1.7f,0),new Vector3(.396f,.2f,8),i%4==0?"FadedWood":"Timber");
            for(int i=0;i<25;i++)Box(floor,"VerandaBoard",new Vector3(-4.8f+i*.4f,1.7f,-5),new Vector3(.396f,.2f,2),"FadedWood");
            var walls=Group(root,"03_ExteriorWalls");
            // Front: two windows and 1.9 m open doorway. Back: rear escape doorway.
            Wall(walls,-5,-3.8f,-4,1.8f,4.8f,false);Wall(walls,-2.2f,-.95f,-4,1.8f,4.8f,false);Wall(walls,.95f,2.2f,-4,1.8f,4.8f,false);Wall(walls,3.8f,5,-4,1.8f,4.8f,false);
            foreach(float x in new[]{-3,3}){Wall(walls,x-.8f,x+.8f,-4,1.8f,2.75f,false);Wall(walls,x-.8f,x+.8f,-4,4.15f,4.8f,false);Window(walls,new Vector3(x,3.45f,-4),false);}
            Wall(walls,-.95f,.95f,-4,4.2f,4.8f,false);
            Wall(walls,-5,-.95f,4,1.8f,4.8f,false);Wall(walls,.95f,5,4,1.8f,4.8f,false);Wall(walls,-.95f,.95f,4,4.2f,4.8f,false);
            foreach(float x in new[]{-5,5})
            {
                Wall(walls,-4,-2.8f,x,1.8f,4.8f,true);Wall(walls,-1.2f,1.2f,x,1.8f,4.8f,true);Wall(walls,2.8f,4,x,1.8f,4.8f,true);
                foreach(float z in new[]{-2,2}){Wall(walls,z-.8f,z+.8f,x,1.8f,2.75f,true);Wall(walls,z-.8f,z+.8f,x,4.15f,4.8f,true);Window(walls,new Vector3(x,3.45f,z),true);}
            }
            foreach(float x in new[]{-5,5})foreach(float z in new[]{-4,4})Box(walls,"CornerPost",new Vector3(x,3.3f,z),new Vector3(.22f,3,.22f),"DarkWood");
            var interiors=Group(root,"04_InteriorPartitions");
            // Left enclosed evidence room; right open living space. Clear central route x > -1.
            Wall(interiors,-4,-.9f,-1.1f,1.8f,4.8f,true);Wall(interiors,.9f,4,-1.1f,1.8f,4.8f,true);Wall(interiors,-.9f,.9f,-1.1f,4.2f,4.8f,true);
            Wall(interiors,-5,-1.1f,.5f,1.8f,4.8f,false);
            var roof=Group(root,"05_Roof_ToggleForInterior");float angle=Mathf.Atan2(2,5.6f)*Mathf.Rad2Deg;
            foreach(int side in new[]{-1,1})
            {
                var sheet=Box(roof,"RoofSlope",new Vector3(side*2.8f,5.8f,0),new Vector3(5.95f,.12f,9.2f),"RoofMetal");sheet.localRotation=Quaternion.Euler(0,0,-side*angle);
                for(int i=0;i<17;i++){var rib=Box(roof,"RaisedRoofSeam",new Vector3(side*2.8f,5.88f,-4.5f+i*.56f),new Vector3(5.95f,.065f,.045f),i%5==0?"Rust":"RoofMetal");rib.localRotation=sheet.localRotation;}
            }
            Box(roof,"RidgeCap",new Vector3(0,6.85f,0),new Vector3(.24f,.12f,9.3f),"RoofMetal");
            Gable(roof,-4.03f);Gable(roof,4.03f);
            var veranda=Group(root,"06_VerandaAndStairs");
            foreach(float x in new[]{-4.8f,0,4.8f})Box(veranda,"VerandaRoofPost",new Vector3(x,3.25f,-5.85f),new Vector3(.18f,2.9f,.18f),"DarkWood");
            var canopy=Box(veranda,"VerandaRoof",new Vector3(0,4.65f,-5),new Vector3(10.8f,.12f,2.8f),"RoofMetal");canopy.localRotation=Quaternion.Euler(-10,0,0);
            Rail(veranda,new Vector3(-4.8f,1.8f,-6),new Vector3(-1.15f,1.8f,-6));Rail(veranda,new Vector3(1.15f,1.8f,-6),new Vector3(4.8f,1.8f,-6));
            Rail(veranda,new Vector3(-4.9f,1.8f,-6),new Vector3(-4.9f,1.8f,-4));Rail(veranda,new Vector3(4.9f,1.8f,-6),new Vector3(4.9f,1.8f,-4));
            Stairs(veranda,-6,-1);Stairs(veranda,4,1);
            var props=Group(root,"07_InteriorProps");
            Table(props,new Vector3(-3.2f,1.8f,-2.4f),"EvidenceTable");Table(props,new Vector3(3,1.8f,-2.3f),"LivingTable");
            Box(props,"WoodenBedFrame",new Vector3(-3.15f,2.1f,2.1f),new Vector3(1.7f,.5f,2.5f),"DarkWood");
            foreach(float y in new[]{2,2.8f,3.6f})Box(props,"ShelfBoard",new Vector3(4.5f,y,2),new Vector3(.65f,.1f,2.6f),"Timber");
            foreach(float z in new[]{.8f,3.2f})Box(props,"ShelfSupport",new Vector3(4.5f,2.8f,z),new Vector3(.16f,2,.16f),"DarkWood");
            var doors=Group(root,"08_Doors_Open_NoLogic");
            Door(doors,"FrontDoorPivot",new Vector3(-.95f,1.8f,-4.15f),-105);Door(doors,"RearDoorPivot",new Vector3(.95f,1.8f,4.15f),-105);
            var markers=Group(root,"09_PlacementMarkers");Group(markers,"RinEvidenceCamera").localPosition=new Vector3(-3.2f,2.78f,-2.4f);Group(markers,"FrontEntry").localPosition=new Vector3(0,1.8f,-5);Group(markers,"RearEscape").localPosition=new Vector3(0,0,7.6f);

            UnityEngine.Object.DestroyImmediate(root.Find("04_InteriorPartitions").gameObject);
            // Smaller footprint, same full standing height. Bake the plan scale into section transforms.
            foreach(Transform section in root){var p=section.localPosition;section.localPosition=new Vector3(p.x*.8f,p.y,p.z*.85f);var s=section.localScale;section.localScale=new Vector3(s.x*.8f,s.y,s.z*.85f);}
            mats["RoofMetal"].SetColor("_BaseColor",new Color(.30f,.24f,.18f));
            mats["FadedWood"].SetColor("_BaseColor",new Color(.43f,.37f,.29f));
            Physics.SyncTransforms();
            foreach(float z in new[]{-4,-2,0,2})if(Physics.CheckCapsule(new Vector3(.2f,2.2f,z),new Vector3(.2f,3.4f,z),.3f))throw new Exception("Blocked house route "+z);
            triangles=0;foreach(var meshFilter in root.GetComponentsInChildren<MeshFilter>())triangles+=meshFilter.sharedMesh.triangles.Length/3;
            CombineSections(root);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+root.name+".prefab");
            Preview();AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(Application.dataPath,"../Cottage-success.txt"),"PASS Unity "+Application.unityVersion+": "+triangles+" triangles; accessible house corridor, native mesh/material prefab, no runtime scripts. Based on GDD old Isan timber house and local world-reference-v3-dark.");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Material(string n,Color c){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=n};m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(m,Folder+"Materials/"+n+".mat");mats[n]=m;}
    static Transform Group(Transform p,string n){var t=new GameObject(n).transform;t.SetParent(p,false);return t;}
    static int mergedId;
    static void CombineSections(Transform parent)
    {
        var children=new List<Transform>();foreach(Transform child in parent)children.Add(child);
        foreach(var child in children)CombineSections(child);
        var batches=new Dictionary<Material,List<MeshFilter>>();
        foreach(var child in children)
        {
            var filter=child.GetComponent<MeshFilter>();var renderer=child.GetComponent<MeshRenderer>();
            if(filter==null||renderer==null)continue;
            if(!batches.ContainsKey(renderer.sharedMaterial))batches[renderer.sharedMaterial]=new List<MeshFilter>();
            batches[renderer.sharedMaterial].Add(filter);
        }
        foreach(var batch in batches)
        {
            if(batch.Value.Count<2)continue;
            var combines=new List<CombineInstance>();foreach(var filter in batch.Value)combines.Add(new CombineInstance{mesh=filter.sharedMesh,transform=parent.worldToLocalMatrix*filter.transform.localToWorldMatrix});
            var mesh=new Mesh{name="Section_"+(mergedId++)+"_"+batch.Key.name};mesh.CombineMeshes(combines.ToArray(),true,true);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"Meshes/"+mesh.name+".asset");
            var node=Group(parent,mesh.name);node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
            foreach(var filter in batch.Value){UnityEngine.Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(filter);}
        }
    }
    static Transform Box(Transform p,string n,Vector3 v,Vector3 s,string m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=v;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=mats[m];triangles+=12;return g.transform;}
    static void Wall(Transform p,float a,float b,float axis,float bottom,float top,bool side)
    {
        int n=Mathf.CeilToInt((top-bottom)/.22f);float h=(top-bottom)/n;
        for(int i=0;i<n;i++){float y=bottom+(i+.5f)*h;Box(p,"WeatheredWallPlank",side?new Vector3(axis,y,(a+b)/2):new Vector3((a+b)/2,y,axis),side?new Vector3(.14f,h-.004f,b-a):new Vector3(b-a,h-.004f,.14f),i%4==0?"FadedWood":"Timber");}
    }
    static void Window(Transform p,Vector3 v,bool side)
    {
        var t=Group(p,"Window_OpenShutters");t.localPosition=v;if(side)t.localRotation=Quaternion.Euler(0,90,0);
        foreach(float x in new[]{-.84f,.84f})Box(t,"WindowFrame",new Vector3(x,0,0),new Vector3(.09f,1.5f,.22f),"DarkWood");
        foreach(float y in new[]{-.74f,.74f})Box(t,"WindowFrame",new Vector3(0,y,0),new Vector3(1.75f,.09f,.22f),"DarkWood");
        foreach(int sign in new[]{-1,1}){var shutter=Group(t,"ShutterPivot");shutter.localPosition=new Vector3(sign*.85f,0,-.08f);shutter.localRotation=Quaternion.Euler(0,sign*145,0);Box(shutter,"Shutter",new Vector3(-sign*.39f,0,0),new Vector3(.76f,1.34f,.06f),"FadedWood");}
    }
    static void Rail(Transform p,Vector3 a,Vector3 b){var d=b-a;int n=Mathf.CeilToInt(d.magnitude/.35f);for(int i=0;i<=n;i++)Box(p,"Baluster",Vector3.Lerp(a,b,(float)i/n)+Vector3.up*.48f,new Vector3(.065f,.95f,.065f),"Timber");var t=Box(p,"HandRail",(a+b)/2+Vector3.up*.97f,new Vector3(.12f,.12f,d.magnitude+.12f),"DarkWood");t.rotation=Quaternion.LookRotation(d);}
    static void Stairs(Transform p,float edge,int direction){for(int i=0;i<10;i++){float h=(10-i)*.18f;Box(p,"StairStep",new Vector3(0,h/2,edge+direction*(i+.5f)*.32f),new Vector3(2.1f,h,.32f),"FadedWood");}}
    static void Table(Transform p,Vector3 v,string n){var t=Group(p,n);t.localPosition=v;Box(t,"Top",new Vector3(0,.9f,0),new Vector3(1.8f,.1f,1),"FadedWood");foreach(float x in new[]{-.75f,.75f})foreach(float z in new[]{-.35f,.35f})Box(t,"Leg",new Vector3(x,.43f,z),new Vector3(.1f,.86f,.1f),"DarkWood");}
    static void Door(Transform p,string n,Vector3 v,float angle){var t=Group(p,n);t.localPosition=v;t.localRotation=Quaternion.Euler(0,angle,0);Box(t,"DoorLeaf",new Vector3(.9f,1.15f,0),new Vector3(1.8f,2.3f,.09f),"DarkWood");Box(t,"Brace",new Vector3(.9f,.55f,-.06f),new Vector3(1.65f,.14f,.06f),"Timber");}
    static void Gable(Transform p,float z)
    {
        var mesh=new Mesh{name="TimberGable_"+(z<0?"Front":"Rear")};mesh.vertices=new[]{new Vector3(-5,4.8f,z),new Vector3(5,4.8f,z),new Vector3(0,6.59f,z),new Vector3(-5,4.8f,z+.08f),new Vector3(5,4.8f,z+.08f),new Vector3(0,6.59f,z+.08f)};mesh.triangles=new[]{0,2,1,3,4,5,0,1,4,0,4,3,1,2,5,1,5,4,2,0,3,2,3,5};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.zero,Vector2.right,Vector2.up};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"Meshes/"+mesh.name+".asset");var t=Group(p,mesh.name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mats["FadedWood"];triangles+=8;
    }
    static void Preview()
    {
        var rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,"Assets/PreviewRenderer.asset");var pipeline=UniversalRenderPipelineAsset.Create(rd);AssetDatabase.CreateAsset(pipeline,"Assets/PreviewPipeline.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.52f,.56f,.60f);
        var sun=new GameObject("PreviewSun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;sun.transform.rotation=Quaternion.Euler(45,-40,0);
        var c=new GameObject("PreviewCamera").AddComponent<Camera>();c.orthographic=true;c.orthographicSize=8.3f;c.nearClipPlane=.1f;c.farClipPlane=100;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.09f,.115f,.13f);c.gameObject.AddComponent<UniversalAdditionalCameraData>();c.transform.position=new Vector3(15,10,-20);c.transform.LookAt(new Vector3(0,2.7f,-.5f));Render(c,"Cottage-exterior.png");
        root.Find("05_Roof_ToggleForInterior").gameObject.SetActive(false);c.transform.position=new Vector3(13,18,-17);c.transform.LookAt(new Vector3(0,2,0));Render(c,"Cottage-interior.png");root.Find("05_Roof_ToggleForInterior").gameObject.SetActive(true);
        // Keep preview scene references together with the delivered folder.
        AssetDatabase.MoveAsset("Assets/PreviewRenderer.asset",Folder+"PreviewRenderer.asset");AssetDatabase.MoveAsset("Assets/PreviewPipeline.asset",Folder+"PreviewPipeline.asset");EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"Cottage_Preview.unity");
    }
    static void Render(Camera c,string name){var rt=new RenderTexture(1400,1050,24);rt.Create();c.targetTexture=rt;RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1400,1050,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1050),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../"+name),tex.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
}
