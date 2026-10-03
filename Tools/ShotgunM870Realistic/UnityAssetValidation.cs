using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Runs only in the isolated validation project, never in the gameplay scene.
public static class UnityAssetValidation
{
    [Serializable] public class Report
    {
        public bool success;
        public int meshParts, triangles;
        public string unityVersion, renderError;
        public Vector3 boundsSize, barrelCenter;
        public string[] materials;
    }

    public static void Run()
    {
        var result = new Report { unityVersion = Application.unityVersion };
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!M870RealisticImportValidator.Validate(true)) throw new Exception("Prefab validation failed.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(M870RealisticImportValidator.PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            var filters = instance.GetComponentsInChildren<MeshFilter>();
            result.meshParts = filters.Length;
            result.triangles = filters.Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3);
            var renderers = instance.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            result.boundsSize = bounds.size;
            result.barrelCenter = instance.transform.Find("Visual/Barrel").GetComponent<MeshFilter>().sharedMesh.bounds.center;
            if (bounds.size.z < 1.0f || bounds.size.z > 1.2f || bounds.size.x > .1f || bounds.size.y > .3f)
                throw new Exception("Imported dimensions/axis are incorrect.");
            if (result.barrelCenter.z < .2f) throw new Exception("Imported forward axis is incorrect.");
            result.materials = renderers.Select(r => r.sharedMaterial.name).Distinct().ToArray();
            foreach (var r in renderers)
            {
                var mat = r.sharedMaterial;
                if (mat.shader.name != "Universal Render Pipeline/Lit") throw new Exception("Incorrect shader.");
                var baseImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap")));
                var normalImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mat.GetTexture("_BumpMap")));
                var maskImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mat.GetTexture("_MetallicGlossMap")));
                if (!baseImporter.sRGBTexture || maskImporter.sRGBTexture || normalImporter.textureType != TextureImporterType.NormalMap)
                    throw new Exception("Incorrect texture color space or normal-map import mode.");
            }
            result.success = true;
            try { RenderStudio(instance); } catch (Exception e) { result.renderError = e.ToString(); }
            // Persist the importer-resolved prefab in this isolated project for export back to the workspace.
            AssetDatabase.SaveAssets();
        }
        catch (Exception e) { result.renderError = e.ToString(); Debug.LogException(e); }
        File.WriteAllText(Path.Combine(Application.dataPath, "../validation-report.json"), JsonUtility.ToJson(result, true));
        EditorApplication.Exit(result.success ? 0 : 1);
    }

    private static void RenderStudio(GameObject instance)
    {
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, "Assets/ValidationRenderer.asset");
        var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
        AssetDatabase.CreateAsset(pipeline, "Assets/ValidationPipeline.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.65f, .70f, .8f);
        RenderSettings.ambientEquatorColor = new Color(.35f, .4f, .45f);
        RenderSettings.ambientGroundColor = new Color(.18f, .2f, .23f);
        foreach (var spec in new[] { new Vector3(35, -30, 0), new Vector3(15, 140, 0), new Vector3(-15, 30, 0) })
        {
            var light = new GameObject("StudioLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = spec.x == 35 ? 2.0f : .8f;
            light.transform.rotation = Quaternion.Euler(spec);
        }
        var camera = new GameObject("StudioCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.055f,.065f,.08f);
        camera.orthographic = true;
        camera.orthographicSize = .32f;
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 10;
        camera.transform.position = new Vector3(1.5f, .5f, -.6f);
        camera.transform.LookAt(new Vector3(0,-.025f,.045f));
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var target = new RenderTexture(1800, 900, 24, RenderTextureFormat.ARGB32);
        target.Create();
        camera.targetTexture = target;
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        RenderPipeline.SubmitRenderRequest(camera, request);
        var old = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(1800,900,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,1800,900),0,0);
        texture.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../M870-unity-preview.png"),texture.EncodeToPNG());
        RenderTexture.active = old;
        UnityEngine.Object.DestroyImmediate(texture);
        target.Release();
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(target);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
            "Assets/Prefab/Weapon/M870Realistic/M870_Preview.unity");
    }
}
