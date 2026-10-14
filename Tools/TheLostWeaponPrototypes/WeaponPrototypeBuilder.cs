using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class WeaponPrototypeBuilder
{
    const string Folder="Assets/Prefab/Weapon/TheLostPrototypes/";
    public static void Run()
    {
        try
        {
            foreach(var p in new[]{"Assets/Prefab/Weapon",Folder.TrimEnd('/'),Folder+"Meshes",Folder+"Materials"})if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(Path.GetDirectoryName(p).Replace('\\','/'),Path.GetFileName(p));
            var mats=new Dictionary<Material,Material>();var report=new List<string>();var models=new List<GameObject>();
            foreach(string type in new[]{"Machete","WoodClub","HeavyAxe","Stone","Torch"})
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Character/TheLostLowPoly/Prefab_Character_TheLost_"+type+"_LowPoly.prefab");
                if(source==null)throw new Exception("Missing source "+type);
                var weapon=source.transform.Find("Visual/Weapon");if(weapon==null)throw new Exception("No weapon parts "+type);
                var root=new GameObject("Prefab_Weapon_TheLost_"+type+"_Prototype");var visual=new GameObject("Parts").transform;visual.SetParent(root.transform,false);int tris=0;
                foreach(var filter in weapon.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=type+"_"+filter.name;var points=mesh.vertices;
                    for(int i=0;i<points.Length;i++){points[i]-=new Vector3(.43f,.90f,.07f);if(type!="Torch"&&type!="Stone")points[i]=Quaternion.Euler(0,0,180)*points[i];}
                    mesh.vertices=points;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"Meshes/"+mesh.name+".asset");tris+=mesh.triangles.Length/3;
                    var node=new GameObject(filter.name);node.transform.SetParent(visual,false);node.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var old=filter.GetComponent<Renderer>().sharedMaterial;
                    if(!mats.ContainsKey(old)){var m=UnityEngine.Object.Instantiate(old);m.name=old.name;AssetDatabase.CreateAsset(m,Folder+"Materials/"+m.name+".mat");mats[old]=m;}
                    node.AddComponent<MeshRenderer>().sharedMaterial=mats[old];
                }
                new GameObject("GripPoint").transform.SetParent(root.transform,false);
                var all=root.GetComponentsInChildren<Renderer>();var bounds=all[0].bounds;foreach(var r in all)bounds.Encapsulate(r.bounds);
                var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
                // No physics/AI dependency; the same asset can be held or used as a ground prop.
                PrefabUtility.SaveAsPrefabAsset(root,Folder+root.name+".prefab");models.Add(root);report.Add(type+": "+all.Length+" separate mesh parts, "+tris+" triangles");
            }
            for(int i=0;i<models.Count;i++)models[i].transform.position=new Vector3((2-i)*.43f,0,0);
            var rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,Folder+"PreviewRenderer.asset");var pipeline=UniversalRenderPipelineAsset.Create(rd);AssetDatabase.CreateAsset(pipeline,Folder+"PreviewPipeline.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.62f,.65f);var l=new GameObject("PreviewLight").AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.6f;l.transform.rotation=Quaternion.Euler(30,160,0);
            var c=new GameObject("PreviewCamera").AddComponent<Camera>();c.orthographic=true;c.orthographicSize=.7f;c.nearClipPlane=.01f;c.farClipPlane=10;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.08f,.1f,.12f);c.gameObject.AddComponent<UniversalAdditionalCameraData>();c.transform.position=new Vector3(.05f,1.1f,3);c.transform.LookAt(new Vector3(0,.28f,0));
            var rt=new RenderTexture(1400,850,24);rt.Create();c.targetTexture=rt;RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var oldRT=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1400,850,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,850),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../TheLost-weapons.png"),tex.EncodeToPNG());RenderTexture.active=oldRT;c.targetTexture=null;rt.Release();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"TheLostWeapons_Preview.unity");AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(Application.dataPath,"../weapons-success.txt"),"PASS Unity "+Application.unityVersion+"\n"+string.Join("\n",report));EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
