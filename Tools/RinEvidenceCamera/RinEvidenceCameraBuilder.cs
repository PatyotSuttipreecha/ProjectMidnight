using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Generator retained outside the main Assets folder; delivered prefabs have no generator dependency.
public static class RinEvidenceCameraBuilder
{
    private const string Folder = "Assets/Prefab/Props/RinEvidenceCamera/";
    [Serializable] private class Source { public Asset[] assets; }
    [Serializable] private class Asset { public string name; public int triangles; public Part[] parts; }
    [Serializable] private class Part { public string name, material, parent; public Vector3[] vertices; public Vector2[] uv; public int[] indices; }
    public static void Run()
    {
        try
        {
            AssetDatabase.DeleteAsset(Folder.TrimEnd('/')); AssetDatabase.DeleteAsset("Assets/StudioRenderer.asset"); AssetDatabase.DeleteAsset("Assets/StudioPipeline.asset"); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in new[] { "Assets/Prefab", "Assets/Prefab/Props", Folder.TrimEnd('/'), Folder+"Meshes", Folder+"Materials" })
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
            var mats = new Dictionary<string,Material>();
            foreach (var name in new[] { "Body", "Metal", "Silver", "Dark", "Rubber", "Glass", "Screen", "Flash", "Tag" })
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                mat.SetColor("_BaseColor",name=="Body"?new Color(.12f,.14f,.16f):name=="Metal"?new Color(.26f,.29f,.32f):name=="Silver"?new Color(.62f,.66f,.68f):name=="Glass"?new Color(.035f,.13f,.17f):name=="Screen"?new Color(.018f,.035f,.045f):name=="Flash"?new Color(.74f,.76f,.69f):name=="Tag"?new Color(.38f,.10f,.085f):new Color(.025f,.028f,.03f));
                mat.SetFloat("_Metallic",name=="Metal"||name=="Silver"?.7f:0);mat.SetFloat("_Smoothness",name=="Glass"||name=="Screen"?.85f:.3f); AssetDatabase.CreateAsset(mat,Folder+"Materials/"+name+".mat"); mats[name]=mat;
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
                Child(root.transform,"InteractionPoint").localPosition=Vector3.zero; Child(root.transform,"GripPoint").localPosition=new Vector3(.042f,0,0); var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,0,.01f);collider.size=new Vector3(.112f,.067f,.058f);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+asset.name+".prefab");UnityEngine.Object.DestroyImmediate(root);
                Debug.Log(asset.name+": "+triangles+" triangles; native mesh assets verified.");
            }
            AssetDatabase.SaveAssets();
            Studio();
            File.WriteAllText(Path.Combine(Application.dataPath,"../camera-success.txt"),"Rin camera prefab, native meshes and URP render verified in Unity "+Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static Transform Child(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    private static void Studio()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Rin_EvidenceCamera.prefab"));
        var rd=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(rd,"Assets/StudioRenderer.asset");var pipeline=UniversalRenderPipelineAsset.Create(rd);AssetDatabase.CreateAsset(pipeline,"Assets/StudioPipeline.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.6f,.65f);
        foreach(var angle in new[]{new Vector3(25,-150,0),new Vector3(15,140,0)}){var l=new GameObject("StudioLight").AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.6f;l.transform.rotation=Quaternion.Euler(angle);}
        var c=new GameObject("PreviewCamera").AddComponent<Camera>();c.orthographic=true;c.orthographicSize=.078f;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.08f,.095f,.11f);c.nearClipPlane=.01f;c.farClipPlane=4;c.gameObject.AddComponent<UniversalAdditionalCameraData>();
        c.transform.position=new Vector3(.15f,.105f,.3f);c.transform.LookAt(new Vector3(.008f,0,.006f));Render(c,"RinCamera-front.png");
        c.transform.position=new Vector3(-.13f,.09f,-.3f);c.transform.LookAt(new Vector3(.008f,0,0));Render(c,"RinCamera-back.png");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"RinCamera_Preview.unity");AssetDatabase.SaveAssets();
    }
    private static void Render(Camera c,string filename)
    {
        var rt=new RenderTexture(1300,950,24);rt.Create();c.targetTexture=rt;RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1300,950,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1300,950),0,0);t.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../"+filename),t.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(t);
    }

}
