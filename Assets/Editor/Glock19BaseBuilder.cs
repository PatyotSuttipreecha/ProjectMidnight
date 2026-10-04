using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class Glock19BaseBuilder
{
    private const string Folder = "Assets/Prefab/Weapon/Glock19Base/";
    private const string PrefabPath = Folder + "Glock19_Base.prefab";
    [Serializable] private class FlatSource { public string name; public int triangles; public FlatPart[] parts; }
    [Serializable] private class FlatPart { public string name, material, parent; public Vector3[] vertices; public Vector2[] uv; public int[] indices; }

    [MenuItem("Tools/Project Midnight/Rebuild Glock 19 Base")]
    public static void Build()
    {
        var data = JsonUtility.FromJson<FlatSource>(File.ReadAllText(Folder + "Glock19_Source.json"));
        EnsureFolder("Meshes"); EnsureFolder("Materials");
        var root = new GameObject("Glock19_Base");
        try
        {
            var visual = Child(root.transform, "Visual");
            var slide = Child(visual, "Slide");
            var magazine = Child(visual, "Magazine");
            var materials = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var name in new[] { "Slide", "Polymer", "Panel", "Dark", "White" })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Materials/" + name + ".mat");
                if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, Folder + "Materials/" + name + ".mat"); }
                mat.name = "Glock19_" + name;
                mat.SetColor("_BaseColor", name == "White" ? new Color(.8f,.8f,.75f) : name == "Slide" ? new Color(.18f,.20f,.22f) : name == "Polymer" ? new Color(.14f,.145f,.15f) : name == "Panel" ? new Color(.095f,.10f,.105f) : new Color(.008f,.009f,.011f));
                mat.SetFloat("_Metallic", name == "Slide" ? .5f : 0);
                mat.SetFloat("_Smoothness", name == "Slide" ? .28f : .17f);
                materials[name] = mat;
            }
            int triangles = 0;
            foreach (var part in data.parts)
            {
                var path = Folder + "Meshes/" + part.name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
                mesh.Clear(); mesh.name = part.name;
                if (part.name == "SlideBody")
                {
                    var vertices = new Vector3[part.indices.Length]; var uv = new Vector2[part.indices.Length]; var indices = new int[part.indices.Length];
                    for (int i = 0; i < indices.Length; i++) { indices[i] = i; vertices[i] = part.vertices[part.indices[i]]; uv[i] = part.uv[part.indices[i]]; }
                    mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices;
                }
                else { mesh.vertices = part.vertices; mesh.uv = part.uv; mesh.triangles = part.indices; }
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                triangles += part.indices.Length / 3;
                var node = Child(part.parent == "Slide" ? slide : part.parent == "Magazine" ? magazine : visual, part.name);
                node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = materials[part.material];
            }
            Child(root.transform, "FirePoint").localPosition = new Vector3(0,.021f,.103f);
            Child(root.transform, "GripPoint").localPosition = new Vector3(0,-.055f,-.065f);
            if (triangles != data.triangles) throw new Exception("Triangle count mismatch.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Glock 19 Base created: " + data.parts.Length + " meshes, " + triangles + " triangles.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void EnsureFolder(string name) { if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/') + "/" + name)) AssetDatabase.CreateFolder(Folder.TrimEnd('/'), name); }
    private static Transform Child(Transform parent, string name) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }

    // Batch validation entry point used only in the separate validation project.
    public static void ValidateBatch()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Build();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.z < .18f || bounds.size.z > .23f || bounds.size.y < .13f || bounds.size.y > .18f) throw new Exception("Incorrect base-model dimensions.");
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh.vertexCount == 0 || filter.sharedMesh.uv.Length != filter.sharedMesh.vertexCount) throw new Exception("Invalid imported mesh.");
            var rd = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(rd,"Assets/StudioRenderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(rd); AssetDatabase.CreateAsset(pipeline,"Assets/StudioPipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.5f,.55f,.6f);
            foreach (var angle in new[] { new Vector3(35,-30,0), new Vector3(20,140,0) }) { var light = new GameObject("StudioLight").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(angle); }
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = .097f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f,.105f,.12f);
            camera.transform.position = new Vector3(-.6f,.13f,.22f); camera.transform.LookAt(new Vector3(0,-.032f,0));
            camera.nearClipPlane = .01f; camera.farClipPlane = 4; camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(1500,1000,24); target.Create(); camera.targetTexture = target;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var old = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(1500,1000,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1500,1000),0,0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath,"../Glock19-preview.png"),texture.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"Glock19_Preview.unity");
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Application.dataPath,"../validation-success.txt"),"Glock 19 Base: native mesh assets, materials, prefab and URP render verified in " + Application.unityVersion);
            EditorApplication.Exit(0);
        }
        catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
