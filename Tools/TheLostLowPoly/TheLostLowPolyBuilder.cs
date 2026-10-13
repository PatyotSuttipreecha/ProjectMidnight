using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Generator retained outside the main Assets folder; delivered prefabs have no generator dependency.
public static class TheLostLowPolyBuilder
{
    private const string Folder = "Assets/Prefab/Character/TheLostLowPoly/";
    [Serializable] private class Source { public Asset[] assets; }
    [Serializable] private class Asset { public string name; public int triangles; public Part[] parts; }
    [Serializable] private class Part { public string name, material, parent; public Vector3[] vertices; public Vector2[] uv; public int[] indices; }
    public static void Run()
    {
        try
        {
            AssetDatabase.DeleteAsset(Folder.TrimEnd('/')); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in new[] { "Assets/Prefab", "Assets/Prefab/Character", Folder.TrimEnd('/'), Folder+"Meshes", Folder+"Materials" })
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
            var mats = new Dictionary<string,Material>();
            foreach (var name in new[] { "Skin", "Cloth", "Pants", "Boots", "Hair", "Dark", "Patch", "Wood", "Metal", "Silver", "Stone", "Flame" })
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                mat.SetColor("_BaseColor",name=="Skin"?new Color(.43f,.40f,.33f):name=="Cloth"?new Color(.29f,.30f,.22f):name=="Pants"?new Color(.19f,.22f,.22f):name=="Patch"?new Color(.38f,.32f,.22f):name=="Wood"?new Color(.27f,.17f,.09f):name=="Metal"?new Color(.34f,.36f,.36f):name=="Silver"?new Color(.56f,.59f,.60f):name=="Stone"?new Color(.39f,.38f,.33f):name=="Flame"?new Color(.95f,.40f,.07f):new Color(.06f,.055f,.045f));
                mat.SetFloat("_Metallic",name=="Metal"||name=="Silver"?.6f:0);mat.SetFloat("_Smoothness",name=="Steel"?.32f:.45f);
                AssetDatabase.CreateAsset(mat,Folder+"Materials/"+name+".mat"); mats[name]=mat;
            }
            var source = JsonUtility.FromJson<Source>(File.ReadAllText("Assets/Source.json"));
            foreach (var asset in source.assets)
            {
                var root = new GameObject(asset.name);var visual = Child(root.transform,"Visual");var groups=new Dictionary<string,Transform>{{"Visual",visual}};
                int triangles = 0;
                foreach (var part in asset.parts)
                {
                    if (!groups.ContainsKey(part.parent)) groups[part.parent]=Child(visual,part.parent);
                    var mesh = new Mesh { name=asset.name+"_"+part.name };
                    mesh.vertices=part.vertices;mesh.uv=part.uv;mesh.triangles=part.indices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh,Folder+"Meshes/"+mesh.name+".asset");
                    var node=Child(groups[part.parent],part.name);node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mats[part.material];
                    if(mesh.vertexCount==0||mesh.uv.Length!=mesh.vertexCount)throw new Exception("Invalid mesh: "+mesh.name);
                    triangles+=part.indices.Length/3;
                }
                if(triangles!=asset.triangles)throw new Exception("Triangle count mismatch");
                Child(root.transform,"WeaponGripPoint").localPosition=new Vector3(.43f,.90f,.07f);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+asset.name+".prefab");UnityEngine.Object.DestroyImmediate(root);
                Debug.Log(asset.name+": "+triangles+" triangles; native mesh assets verified.");
            }
            AssetDatabase.SaveAssets();
            Studio();
            File.WriteAllText(Path.Combine(Application.dataPath,"../the-lost-success.txt"),"Six The Lost visual prototype prefabs and URP render verified in Unity "+Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static Transform Child(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    private static void Studio()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        int i=0;foreach(string type in new[]{"Unarmed","Machete","WoodClub","HeavyAxe","Stone","Torch"}){var actor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Prefab_Character_TheLost_"+type+"_LowPoly.prefab"));actor.transform.position=new Vector3((2.5f-i++)*1.1f,0,0);}
        var rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,Folder+"PreviewRenderer.asset");var pipeline=UniversalRenderPipelineAsset.Create(rd);AssetDatabase.CreateAsset(pipeline,Folder+"PreviewPipeline.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.58f,.62f);
        var light=new GameObject("PreviewLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(30,160,0);
        var c=new GameObject("PreviewCamera").AddComponent<Camera>();c.orthographic=true;c.orthographicSize=1.65f;c.nearClipPlane=.01f;c.farClipPlane=30;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.08f,.10f,.12f);c.gameObject.AddComponent<UniversalAdditionalCameraData>();c.transform.position=new Vector3(0,2.0f,10);c.transform.LookAt(new Vector3(0,.86f,0));
        var rt=new RenderTexture(1800,850,24);rt.Create();c.targetTexture=rt;RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1800,850,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1800,850),0,0);t.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../TheLost-lineup.png"),t.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(t);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"TheLost_Preview.unity");AssetDatabase.SaveAssets();
    }


}

