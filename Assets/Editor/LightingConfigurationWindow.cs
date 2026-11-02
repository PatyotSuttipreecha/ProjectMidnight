using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class LightingConfigurationWindow : EditorWindow
{
    public const string Folder = "Assets/Data/Lighting";
    [SerializeField] private SceneLightingController controller;
    [SerializeField] private LightingPreset preset;
    [SerializeField] private LightingPreset comparison;
    private bool showDiagnostics=true, showAreas=true;
    private SceneLightingController previewController;
    private Editor presetEditor;
    private Vector2 scroll;
    private bool livePreview;
    private float previewBlend = 1;
    private string lastPreview = "";
    private void OnEnable()
    {
        if (!controller) controller = Object.FindFirstObjectByType<SceneLightingController>();
        if (!preset && controller) preset = controller.initialPreset;
        EditorApplication.update += UpdatePreview;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneClosing += OnSceneClosing;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
    }
    private void OnDisable()
    {
        StopPreview();
        EditorApplication.update -= UpdatePreview;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneClosing -= OnSceneClosing;
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        if (presetEditor) DestroyImmediate(presetEditor);
    }
    private void OnPlayMode(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) StopPreview(); }
    private void OnSceneSaving(Scene scene, string path) { StopPreview(); }
    private void OnSceneClosing(Scene scene, bool removingScene) { StopPreview(); }
    private void StopPreview()
    {
        if (previewController) previewController.RestoreEditorPreview();
        previewController = null; livePreview = false; lastPreview = "";
        SceneView.RepaintAll(); Repaint();
    }
    private void UpdatePreview()
    {
        if (!livePreview) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || !controller || !controller.isActiveAndEnabled || !preset)
        { StopPreview(); return; }
        if (previewController && previewController != controller) StopPreview();
        if (!livePreview) return;
        string signature = EditorJsonUtility.ToJson(preset) + previewBlend + (comparison ? EditorJsonUtility.ToJson(comparison) : "");
        if (signature == lastPreview && previewController == controller) return;
        previewController = controller;
        controller.PreviewInEditor(preset, previewBlend, comparison);
        lastPreview = signature;
        SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate();
    }
    [MenuItem("System Modification/Scene Configuration/Lighting Configuration")]
    public static void Open() => GetWindow<LightingConfigurationWindow>("Lighting Configuration");
    public static void PreviewArea(SceneLightingController owner,LightingPreset look)
    {
        var window=GetWindow<LightingConfigurationWindow>("Lighting Configuration");
        window.StopPreview();window.controller=owner;window.preset=look;window.comparison=null;
        window.previewBlend=1;window.livePreview=true;window.UpdatePreview();window.Show();
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Story Lighting — no automatic clock", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUI.BeginChangeCheck();
        controller = (SceneLightingController)EditorGUILayout.ObjectField("Scene controller",controller,typeof(SceneLightingController),true);
        if (EditorGUI.EndChangeCheck()) StopPreview();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Install / Find in active scene")) controller = Install(SceneManager.GetActiveScene());
            if (GUILayout.Button("Create Local Box Volume at Scene View")) CreateLocalBox();
            if (GUILayout.Button("Create Lighting Bounding at Scene View")) LightingEditorTools.CreateBounding(controller,preset);
        }
        preset = (LightingPreset)EditorGUILayout.ObjectField("Preset",preset,typeof(LightingPreset),false);
        using(new EditorGUI.DisabledScope(!preset || Application.isPlaying))
        {
            if(GUILayout.Button("Duplicate / Save Preset As..."))
            {
                var copy=LightingEditorTools.Duplicate(preset);if(copy)preset=copy;
            }
        }
        using(new EditorGUI.DisabledScope(!controller || !preset || Application.isPlaying))
            if(GUILayout.Button("Apply as Initial Lighting + Save Scene"))
            {
                StopPreview();LightingEditorTools.ApplyInitialAndSave(controller,preset);
            }
        using (new EditorGUILayout.HorizontalScope())
            foreach (string name in new[] { "Evening", "Dusk", "Night", "Dawn" })
                if (GUILayout.Button(name)) preset = AssetDatabase.LoadAssetAtPath<LightingPreset>(Folder+"/Lighting_"+name+".asset");
        using (new EditorGUI.DisabledScope(Application.isPlaying || !controller || !preset))
        {
            bool requested = EditorGUILayout.ToggleLeft("Live Preview in Editor", livePreview);
            if (requested != livePreview)
            {
                if (!requested) StopPreview(); else { livePreview = true; lastPreview = ""; UpdatePreview(); }
            }
            previewBlend = EditorGUILayout.Slider(new GUIContent("Preview Blend", "Blend from the scene before preview to the selected preset. Does not change preset values."), previewBlend, 0, 1);
            comparison=(LightingPreset)EditorGUILayout.ObjectField(new GUIContent("Compare A (optional)","Empty uses the original scene before preview. B is the selected Preset."),comparison,typeof(LightingPreset),false);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("A — Before")){previewBlend=0;livePreview=true;lastPreview="";UpdatePreview();}
                if(GUILayout.Button("B — Selected Preset")){previewBlend=1;livePreview=true;lastPreview="";UpdatePreview();}
            }
            if (GUILayout.Button("Stop Preview / Restore Original Scene")) StopPreview();
        }
        if (GUILayout.Button("Enable Lighting / Fog / Post Effects in Scene View"))
            foreach (var item in SceneView.sceneViews)
                if (item is SceneView view)
                {
                    view.sceneLighting = true; view.sceneViewState.showFog = true;
                    view.sceneViewState.showSkybox = true; view.sceneViewState.showImageEffects = true; view.Repaint();
                }
        EditorGUILayout.HelpBox("Enable Live Preview, choose a look above, then edit its settings below. Preview restores the scene on Stop, closing this window, saving the scene or entering Play Mode. Preset edits are real asset edits (Undo supported). Scene View must have Lighting/Fog/Post Effects enabled. Baked lighting does not update in realtime.", MessageType.Info);
        if (preset)
        {
            EditorGUILayout.LabelField("Edit Preset — changes preview live", EditorStyles.boldLabel);
            Editor.CreateCachedEditor(preset, null, ref presetEditor);
            presetEditor.OnInspectorGUI();
        }
        using (new EditorGUI.DisabledScope(!controller || !preset || !Application.isPlaying))
        {
            if (GUILayout.Button("Blend to preset (Play Mode)")) controller.BlendTo(preset);
            if (GUILayout.Button("Apply instantly (Play Mode)")) controller.ApplyInstant(preset);
        }
        using (new EditorGUI.DisabledScope(!controller || Application.isPlaying))
            if (GUILayout.Button("Capture scene as NEW preset"))
            {
                string path = EditorUtility.SaveFilePanelInProject("Capture lighting","Lighting_Captured","asset","Save a new lighting preset.");
                if (!string.IsNullOrEmpty(path))
                {
                    var captured = CreateInstance<LightingPreset>(); controller.Capture(captured);
                    AssetDatabase.CreateAsset(captured,path); preset = captured; Selection.activeObject = captured;
                }
            }
        EditorGUILayout.HelpBox("WorldTrigger or Timeline Signal Receiver → LightingCue.Play. Presets change the global sun, ambient, sky and fog. A Local Box Volume only blends its enabled post effects around the camera; it does not change the sun or RenderSettings fog. Local volumes use priority 20; the story volume uses 10. Camera Post Processing and the volume layer mask must include these objects.",MessageType.Info);
        showDiagnostics=EditorGUILayout.Foldout(showDiagnostics,"Diagnostics",true);
        if(showDiagnostics)LightingEditorTools.Diagnostics(controller);
        showAreas=EditorGUILayout.Foldout(showAreas,"Area management",true);
        if(showAreas)LightingEditorTools.Areas(controller);
        if (controller && Application.isPlaying) EditorGUILayout.LabelField("Transition", controller.IsTransitioning ? controller.Progress.ToString("P0") : "Idle");
        EditorGUILayout.EndScrollView();
    }
    private static VolumeProfile BaseProfile(string name)
    {
        string path = Folder + "/" + name + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile) return profile;
        profile = CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile,path);
        var color = profile.Add<ColorAdjustments>();
        color.postExposure.overrideState = color.colorFilter.overrideState = true;
        AssetDatabase.AddObjectToAsset(color,profile);
        EditorUtility.SetDirty(profile);
        return profile;
    }
    public static SceneLightingController Install(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var existing = root.GetComponentInChildren<SceneLightingController>(true);
            if (existing) return existing;
        }
        Light sun = null; Transform parent = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name.Contains("Lighting")) parent = root.transform;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Directional && light.isActiveAndEnabled && (sun == null || light.intensity > sun.intensity)) sun = light;
        }
        if (!sun) { Debug.LogError("Lighting Configuration: scene needs an active Directional Light."); return null; }
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var obj = new GameObject("Scene Lighting System");
        SceneManager.MoveGameObjectToScene(obj,scene);
        Undo.RegisterCreatedObjectUndo(obj,"Install scene lighting");
        if (parent) Undo.SetTransformParent(obj.transform,parent,"Parent scene lighting");
        var controller = Undo.AddComponent<SceneLightingController>(obj); controller.mainLight = sun;
        var vol = Undo.AddComponent<Volume>(obj); vol.isGlobal = true; vol.priority = 10;
        vol.sharedProfile = BaseProfile("Lighting_GlobalBase"); controller.atmosphereVolume = vol;
        string currentPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Lighting_" + scene.name + "_Initial.asset");
        var current = CreateInstance<LightingPreset>(); controller.Capture(current);
        AssetDatabase.CreateAsset(current,currentPath); controller.initialPreset = current;
        CreateLook("Evening", new Color(1,.78f,.57f), .85f, new Vector3(18,-35,0), new Color(.25f,.3f,.38f),-.15f);
        CreateLook("Dusk",new Color(.68f,.74f,1), .35f,new Vector3(7,-35,0),new Color(.12f,.17f,.26f),-.35f);
        CreateLook("Night",new Color(.48f,.62f,.88f),.16f,new Vector3(35,130,0),new Color(.055f,.085f,.14f),-.45f);
        CreateLook("Dawn",new Color(1,.75f,.65f),.45f,new Vector3(10,100,0),new Color(.2f,.23f,.3f),-.1f);
        foreach (string name in new[] { "Evening", "Dusk", "Night", "Dawn" })
        {
            var cueObject = new GameObject("Lighting Cue - " + name);
            SceneManager.MoveGameObjectToScene(cueObject,scene);
            Undo.RegisterCreatedObjectUndo(cueObject,"Create lighting cue");
            Undo.SetTransformParent(cueObject.transform,obj.transform,"Parent lighting cue");
            var cue = Undo.AddComponent<LightingCue>(cueObject); cue.controller = controller;
            cue.preset = AssetDatabase.LoadAssetAtPath<LightingPreset>(Folder + "/Lighting_" + name + ".asset");
        }
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(vol);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = obj;
        return controller;
    }
    private static void CreateLook(string name,Color light,float intensity,Vector3 angles,Color ambient,float exposure)
    {
        string path=Folder+"/Lighting_"+name+".asset";
        if (AssetDatabase.LoadAssetAtPath<LightingPreset>(path)) return;
        var look=CreateInstance<LightingPreset>(); look.lightColor=light;look.lightIntensity=intensity;look.lightEuler=angles;
        look.ambientSky=ambient;look.ambientEquator=ambient*.6f;look.ambientEquator.a=1;
        look.ambientGround=ambient*.3f;look.ambientGround.a=1;
        look.skyTint=ambient;look.skyExposure=name=="Night"?.3f:.7f;
        look.postExposure=exposure;look.fogColor=ambient;look.fogEnabled=true;look.fogDensity=.008f;
        AssetDatabase.CreateAsset(look,path);
    }
    private static void CreateLocalBox()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var obj=new GameObject("Local Lighting Volume");
        Undo.RegisterCreatedObjectUndo(obj,"Create local lighting volume");
        if (SceneView.lastActiveSceneView) obj.transform.position=SceneView.lastActiveSceneView.pivot;
        var box=Undo.AddComponent<BoxCollider>(obj);box.isTrigger=true;box.size=new Vector3(10,5,10);
        var volume=Undo.AddComponent<Volume>(obj);volume.isGlobal=false;volume.priority=20;volume.blendDistance=3;
        // Unique profile per area avoids accidentally changing every room together.
        var profile=CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Lighting_LocalArea.asset"));
        var color=profile.Add<ColorAdjustments>();color.postExposure.Override(-.3f);
        AssetDatabase.AddObjectToAsset(color,profile);volume.sharedProfile=profile;
        EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();Selection.activeGameObject=obj;
        EditorSceneManager.MarkSceneDirty(obj.scene);
    }
}
