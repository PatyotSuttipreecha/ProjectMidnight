using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class LightingEditorTools
{
    public static LightingPreset Duplicate(LightingPreset source)
    {
        if (!source) return null;
        string path=EditorUtility.SaveFilePanelInProject("Duplicate lighting preset",source.name+"_Copy","asset","Create an independent lighting look.");
        if(string.IsNullOrEmpty(path))return null;
        var copy=DuplicateTo(source,path);Selection.activeObject=copy;return copy;
    }
    public static LightingPreset DuplicateTo(LightingPreset source,string path)
    {
        var copy=Object.Instantiate(source);copy.name=System.IO.Path.GetFileNameWithoutExtension(path);copy.hideFlags=HideFlags.None;
        AssetDatabase.CreateAsset(copy,path);Undo.RegisterCreatedObjectUndo(copy,"Duplicate lighting preset");
        AssetDatabase.SaveAssets();Selection.activeObject=copy;return copy;
    }
    public static void ApplyInitialAndSave(SceneLightingController controller,LightingPreset preset,bool saveScene=true)
    {
        if(!controller || !controller.mainLight || !preset || Application.isPlaying)return;
        System.IO.Directory.CreateDirectory(LightingConfigurationWindow.Folder);AssetDatabase.Refresh();
        controller.RestoreEditorPreview();
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply scene initial lighting");
        Material sky=RenderSettings.skybox;
        if(sky)
        {
            sky=Object.Instantiate(sky);sky.hideFlags=HideFlags.None;
            sky.name="Lighting_"+controller.gameObject.scene.name+"_Sky";
            AssetDatabase.CreateAsset(sky,AssetDatabase.GenerateUniqueAssetPath(LightingConfigurationWindow.Folder+"/"+sky.name+".mat"));
        }
        VolumeProfile profile=null;
        if(controller.atmosphereVolume)
        {
            var source=controller.atmosphereVolume.sharedProfile;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile,AssetDatabase.GenerateUniqueAssetPath(LightingConfigurationWindow.Folder+"/Lighting_"+controller.gameObject.scene.name+"_Base.asset"));
            if(source)foreach(var component in source.components)
            {
                var copy=Object.Instantiate(component);copy.hideFlags=HideFlags.None;profile.components.Add(copy);AssetDatabase.AddObjectToAsset(copy,profile);
            }
            if(!profile.TryGet<ColorAdjustments>(out var color)) {color=profile.Add<ColorAdjustments>();AssetDatabase.AddObjectToAsset(color,profile);}
            color.postExposure.overrideState=color.colorFilter.overrideState=true;
        }
        // Asset imports flush pending Undo records. Record scene state only AFTER creating assets.
        // New independent asset files remain available after undoing their scene assignments.
        Undo.RecordObjects(new Object[]{controller,controller.mainLight,controller.mainLight.transform,Unsupported.GetRenderSettings()},"Apply scene initial lighting");
        if(controller.atmosphereVolume)Undo.RecordObject(controller.atmosphereVolume,"Apply scene atmosphere");
        controller.ApplySavedEditorPreset(preset,sky,profile);
        if(sky)EditorUtility.SetDirty(sky);
        if(profile){foreach(var component in profile.components)EditorUtility.SetDirty(component);EditorUtility.SetDirty(profile);}
        EditorUtility.SetDirty(controller);EditorUtility.SetDirty(controller.mainLight);EditorUtility.SetDirty(controller.mainLight.transform);
        if(PrefabUtility.IsPartOfPrefabInstance(controller))PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        if(PrefabUtility.IsPartOfPrefabInstance(controller.mainLight))PrefabUtility.RecordPrefabInstancePropertyModifications(controller.mainLight);
        if(PrefabUtility.IsPartOfPrefabInstance(controller.mainLight.transform))PrefabUtility.RecordPrefabInstancePropertyModifications(controller.mainLight.transform);
        Undo.CollapseUndoOperations(group);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        if(saveScene)EditorSceneManager.SaveScene(controller.gameObject.scene);SceneView.RepaintAll();
    }
    public static LightingBounding CreateBounding(SceneLightingController controller,LightingPreset preset)
    {
        var obj=new GameObject("Lighting Bounding");
        if(controller)SceneManager.MoveGameObjectToScene(obj,controller.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(obj,"Create lighting bounding");
        if(SceneView.lastActiveSceneView)obj.transform.position=SceneView.lastActiveSceneView.pivot;
        var area=Undo.AddComponent<LightingBounding>(obj);area.controller=controller;area.preset=preset;
        EditorSceneManager.MarkSceneDirty(obj.scene);Selection.activeGameObject=obj;return area;
    }
    public static void Diagnostics(SceneLightingController controller)
    {
        EditorGUILayout.LabelField("Lighting diagnostics",EditorStyles.boldLabel);
        if(!controller){EditorGUILayout.HelpBox("Assign a scene controller.",MessageType.Warning);return;}
        if(!controller.mainLight)EditorGUILayout.HelpBox("Missing main Directional Light.",MessageType.Error);
        else if(controller.mainLight.lightmapBakeType!=LightmapBakeType.Realtime)
            EditorGUILayout.HelpBox("Main light uses Baked/Mixed lighting. Presets do not rebake indirect light, lightmaps or probes.",MessageType.Warning);
        if(LightmapSettings.lightmaps.Length>0)EditorGUILayout.HelpBox("Scene has baked lightmaps. Bright baked surfaces may remain bright at night.",MessageType.Info);
        foreach(var item in SceneView.sceneViews)
            if(item is SceneView view && (!view.sceneLighting || !view.sceneViewState.showFog || !view.sceneViewState.showImageEffects || !view.sceneViewState.showSkybox))
                EditorGUILayout.HelpBox("Scene View \""+view.titleContent.text+"\" has lighting, fog or post effects disabled. Use the enable button above.",MessageType.Warning);
        var cameras=Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
        foreach(var camera in cameras)
        {
            if(camera.gameObject.scene!=controller.gameObject.scene || !camera.enabled)continue;
            if(!camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))continue;
            if(!data.renderPostProcessing)EditorGUILayout.HelpBox(camera.name+": Post Processing is disabled.",MessageType.Warning);
            if(controller.atmosphereVolume && (data.volumeLayerMask.value & (1<<controller.atmosphereVolume.gameObject.layer))==0)
                EditorGUILayout.HelpBox(camera.name+": Volume Layer Mask excludes the story lighting volume.",MessageType.Warning);
            foreach(var local in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(!local.isGlobal && local.enabled && local.gameObject.scene==controller.gameObject.scene && (data.volumeLayerMask.value & (1<<local.gameObject.layer))==0)
                    EditorGUILayout.HelpBox(camera.name+": Volume Layer Mask excludes local area "+local.name+".",MessageType.Warning);
        }
        foreach(var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(volume!=controller.atmosphereVolume && volume.gameObject.scene==controller.gameObject.scene && volume.isGlobal && volume.isActiveAndEnabled && volume.weight>0 && controller.atmosphereVolume && volume.priority>=controller.atmosphereVolume.priority)
                EditorGUILayout.HelpBox(volume.name+": another global volume has equal/higher priority; overlapping overrides may hide this preset.",MessageType.Warning);
    }
    public static void Areas(SceneLightingController controller)
    {
        EditorGUILayout.LabelField("Local Volumes & Lighting Bounds",EditorStyles.boldLabel);
        foreach(var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(volume.isGlobal || controller && volume.gameObject.scene!=controller.gameObject.scene)continue;
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(volume.name+" | Priority "+volume.priority))SelectAndFrame(volume.gameObject);
                using(new EditorGUI.DisabledScope(!volume.sharedProfile))if(GUILayout.Button("Profile",GUILayout.Width(55)))Selection.activeObject=volume.sharedProfile;
            }
            if(!volume.GetComponent<Collider>())EditorGUILayout.HelpBox(volume.name+": Local Volume needs a Collider.",MessageType.Warning);
            if(!volume.gameObject.activeInHierarchy || !volume.enabled)EditorGUILayout.LabelField("Inactive",EditorStyles.miniLabel);
        }
        foreach(var area in Object.FindObjectsByType<LightingBounding>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(!controller || area.controller==controller)
                if(GUILayout.Button(area.name+" | Priority "+area.priority+" | "+(area.preset ? area.preset.name : "No preset")))SelectAndFrame(area.gameObject);
    }
    private static void SelectAndFrame(GameObject go)
    {
        Selection.activeGameObject=go;EditorGUIUtility.PingObject(go);
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.Frame(new Bounds(go.transform.position,Vector3.one*15),false);
    }
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawVolume(Volume volume,GizmoType type)
    {
        if(volume.isGlobal || !volume.TryGetComponent<BoxCollider>(out var box))return;
        bool selected=(type & GizmoType.Selected)!=0;
        var matrix=Gizmos.matrix;Gizmos.matrix=box.transform.localToWorldMatrix;
        Gizmos.color=selected ? Color.cyan : new Color(0,.7f,1,.35f);Gizmos.DrawWireCube(box.center,box.size);Gizmos.matrix=matrix;
        if(!selected)return;
        Gizmos.color=new Color(0,.7f,1,.3f);
        Gizmos.DrawWireCube(box.bounds.center,box.bounds.size+Vector3.one*volume.blendDistance*2);
        Handles.Label(box.bounds.center+Vector3.up*(box.bounds.extents.y+.4f),volume.name+" | Priority "+volume.priority+" | Blend "+volume.blendDistance+"m (outer guide)");
    }
}

[CustomEditor(typeof(LightingBounding))]
public class LightingBoundingEditor : Editor
{
    private readonly UnityEditor.IMGUI.Controls.BoxBoundsHandle handle=new UnityEditor.IMGUI.Controls.BoxBoundsHandle();
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var area=(LightingBounding)target;
        using(new EditorGUI.DisabledScope(Application.isPlaying || !area.controller || !area.preset))
            if(GUILayout.Button("Preview this area's preset in Editor"))LightingConfigurationWindow.PreviewArea(area.controller,area.preset);
        EditorGUILayout.HelpBox("Entering changes GLOBAL lighting and keeps it after leaving. Trigger Once prevents backtracking from resetting the story. Priority chooses between simultaneous entries. No WorldTrigger or physics required. Persistence is for this play session; Save/Load integration is not included.",MessageType.Info);
        if(Application.isPlaying)EditorGUILayout.LabelField("Has Triggered",area.HasTriggered ? "Yes" : "No");
    }
    private void OnSceneGUI()
    {
        var area=(LightingBounding)target;
        using(new Handles.DrawingScope(area.transform.localToWorldMatrix))
        {
            handle.center=area.center;handle.size=area.size;handle.SetColor(Color.cyan);
            EditorGUI.BeginChangeCheck();handle.DrawHandle();
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(area,"Resize lighting bounding");area.center=handle.center;area.size=handle.size;
                if(PrefabUtility.IsPartOfPrefabInstance(area))PrefabUtility.RecordPrefabInstancePropertyModifications(area);
                EditorUtility.SetDirty(area);
            }
        }
    }
}
