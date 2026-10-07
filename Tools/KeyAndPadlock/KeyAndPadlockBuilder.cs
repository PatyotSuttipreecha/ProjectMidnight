using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Generator retained outside the main Assets folder; delivered prefabs have no generator dependency.
public static class KeyAndPadlockBuilder
{
    private const string Folder = "Assets/Prefab/Props/KeyAndPadlock/";
    [Serializable] private class Source { public Asset[] assets; }
    [Serializable] private class Asset { public string name; public int triangles; public Part[] parts; }
    [Serializable] private class Part { public string name, material, parent; public Vector3[] vertices; public Vector2[] uv; public int[] indices; }
    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in new[] { "Assets/Prefab", "Assets/Prefab/Props", Folder.TrimEnd('/'), Folder+"Meshes", Folder+"Materials" })
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
            var mats = new Dictionary<string,Material>();
            foreach (var name in new[] { "Brass", "Steel", "Silver", "Dark", "DarkBrass" })
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                mat.SetColor("_BaseColor",name=="Brass"?new Color(.62f,.40f,.14f):name=="Silver"?new Color(.55f,.60f,.65f):name=="Steel"?new Color(.16f,.21f,.26f):name=="DarkBrass"?new Color(.19f,.11f,.035f):new Color(.006f,.007f,.008f));
                mat.SetFloat("_Metallic",name=="Dark"?0:.65f);mat.SetFloat("_Smoothness",name=="Steel"?.32f:.45f);
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
                Child(root.transform,"InteractionPoint").localPosition=Vector3.zero;
                PrefabUtility.SaveAsPrefabAsset(root,Folder+asset.name+".prefab");UnityEngine.Object.DestroyImmediate(root);
                Debug.Log(asset.name+": "+triangles+" triangles; native mesh assets verified.");
            }
            AssetDatabase.SaveAssets();
            Studio();
            File.WriteAllText(Path.Combine(Application.dataPath,"../validation-success.txt"),"Key and padlock prefabs, native meshes and URP render verified in Unity "+Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static Transform Child(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    private static void Studio()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var key=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Key_Base.prefab"));key.transform.position=new Vector3(-.069f,.005f,0);
        var padlock=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Padlock_Base.prefab"));padlock.transform.position=new Vector3(.031f,0,0);
        var rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,"Assets/StudioRenderer.asset");var pipeline=UniversalRenderPipelineAsset.Create(rd);AssetDatabase.CreateAsset(pipeline,"Assets/StudioPipeline.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.60f,.65f);
        foreach(var angle in new[]{new Vector3(25,-150,0),new Vector3(15,140,0)}){var light=new GameObject("StudioLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(angle);}
        var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.074f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.095f,.11f);camera.transform.position=new Vector3(.16f,.09f,.48f);camera.transform.LookAt(new Vector3(-.01f,.015f,0));camera.nearClipPlane=.01f;camera.farClipPlane=4;camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var target=new RenderTexture(1500,950,24);target.Create();camera.targetTexture=target;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1500,950,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1500,950),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../KeyAndPadlock-preview.png"),texture.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"KeyAndPadlock_Preview.unity");AssetDatabase.SaveAssets();
    }
}
