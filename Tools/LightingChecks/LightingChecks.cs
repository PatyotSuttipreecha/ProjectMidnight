using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class LightingChecks
{
    private const string Key="MidnightLightingChecks";
    private static int phase, checks;
    private static double since;
    private static SceneLightingController controller;
    private static LightingPreset a,b;
    private static float interrupted;
    private static LightingBounding low,high;
    private static Transform boundTarget;
    private static LightingPreset c;
    static LightingChecks()
    {
        if (SessionState.GetBool(Key,false)) EditorApplication.playModeStateChanged+=Changed;
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer,"Assets/Renderer.asset");
            var pipeline=UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline,"Assets/Pipeline.asset");
            GraphicsSettings.defaultRenderPipeline=pipeline;
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1;
            var go=new GameObject("Scene Lighting");controller=go.AddComponent<SceneLightingController>();controller.mainLight=sun;
            var volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile,"Assets/Base.asset");
            var color=profile.Add<ColorAdjustments>();color.postExposure.Override(0);AssetDatabase.AddObjectToAsset(color,profile);
            volume.sharedProfile=profile;controller.atmosphereVolume=volume;
            a=ScriptableObject.CreateInstance<LightingPreset>();a.lightIntensity=1;a.fogEnabled=false;
            b=ScriptableObject.CreateInstance<LightingPreset>();b.lightIntensity=.2f;b.fogEnabled=true;b.fogDensity=.02f;b.postExposure=-.4f;
            AssetDatabase.CreateAsset(a,"Assets/A.asset");AssetDatabase.CreateAsset(b,"Assets/B.asset");
            controller.initialPreset=a;
            controller.PreviewInEditor(b,.5f);
            Check(Mathf.Abs(sun.intensity-.6f)<.001f,"Edit Mode preview blend");
            Check(volume.sharedProfile!=profile,"Edit Mode profile isolated");
            b.lightIntensity=.4f;controller.PreviewInEditor(b,.5f);
            Check(Mathf.Abs(sun.intensity-.7f)<.001f,"Edited preset updates live");
            b.lightIntensity=.2f;controller.RestoreEditorPreview();
            Check(Mathf.Approximately(sun.intensity,1) && volume.sharedProfile==profile,"Stop editor preview restores scene");
            Check(Mathf.Approximately(color.postExposure.value,0),"Editor preview leaves profile asset unchanged");
            var copy=LightingEditorTools.DuplicateTo(b,"Assets/Duplicate.asset");
            copy.lightIntensity=.6f;
            Check(copy!=b && Mathf.Approximately(b.lightIntensity,.2f),"Duplicate independent of original");
            Undo.FlushUndoRecordObjects();
            LightingEditorTools.ApplyInitialAndSave(controller,b,false);
            Check(controller.initialPreset==b && Mathf.Abs(sun.intensity-.2f)<.001f,"Apply initial in Editor");
            Check(volume.sharedProfile!=profile && AssetDatabase.Contains(volume.sharedProfile),"Saved independent profile");
            Undo.FlushUndoRecordObjects();Undo.PerformUndo();
            Check(controller.initialPreset==a && Mathf.Approximately(sun.intensity,1) && volume.sharedProfile==profile,"Undo initial commit");
            var box=new GameObject("Local Box");box.transform.position=new Vector3(100,0,0);
            var collider=box.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=Vector3.one*10;
            var local=box.AddComponent<Volume>();local.isGlobal=false;local.priority=20;local.blendDistance=3;
            var lp=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(lp,"Assets/Local.asset");
            var lc=lp.Add<ColorAdjustments>();lc.postExposure.Override(-2);AssetDatabase.AddObjectToAsset(lc,lp);local.sharedProfile=lp;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(go.scene,"Assets/LightingCheck.unity");
            SessionState.SetInt(Key+".Checks",checks);
            SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=Changed;EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Fail(e);}
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode)return;
        checks=SessionState.GetInt(Key+".Checks",0);
        controller=UnityEngine.Object.FindFirstObjectByType<SceneLightingController>();
        a=AssetDatabase.LoadAssetAtPath<LightingPreset>("Assets/A.asset");b=AssetDatabase.LoadAssetAtPath<LightingPreset>("Assets/B.asset");
        phase=0;since=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    private static void Tick()
    {
        try
        {
            double elapsed=EditorApplication.timeSinceStartup-since;
            if(elapsed<.08)return;
            switch(phase)
            {
                case 0:
                    Check(Mathf.Approximately(controller.mainLight.intensity,1),"Initial preset");
                    Check(controller.atmosphereVolume.sharedProfile!=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Base.asset"),"Runtime profile clone");
                    controller.TransitionTo(b,.3f);phase=1;since=EditorApplication.timeSinceStartup;break;
                case 1:
                    if(elapsed<.5)return;
                    Check(!controller.IsTransitioning && controller.CurrentPreset==b,"Transition complete");
                    Check(Mathf.Abs(controller.mainLight.intensity-.2f)<.001f,"Target intensity");
                    Check(RenderSettings.fog && Mathf.Abs(RenderSettings.fogDensity-.02f)<.001f,"Fog target");
                    controller.TransitionTo(a,1);phase=2;since=EditorApplication.timeSinceStartup;break;
                case 2:
                    interrupted=controller.mainLight.intensity;
                    Check(interrupted>.2f && interrupted<1,"Intermediate intensity");
                    controller.TransitionTo(b,.2f);
                    Check(Mathf.Approximately(interrupted,controller.mainLight.intensity),"Retarget has no immediate jump");
                    Time.timeScale=0;phase=3;since=EditorApplication.timeSinceStartup;break;
                case 3:
                    if(elapsed<.4)return;
                    Check(!controller.IsTransitioning && Mathf.Abs(controller.mainLight.intensity-.2f)<.001f,"Unscaled fade at timescale zero");
                    Check(Mathf.Approximately(a.lightIntensity,1) && Mathf.Approximately(b.lightIntensity,.2f),"Preset assets unchanged");
                    var trigger=new GameObject("Volume trigger").transform;trigger.position=new Vector3(100,0,0);
                    Physics.SyncTransforms();
                    // Headless tests have no rendering camera to construct the pipeline/volume stack.
                    if (!VolumeManager.instance.isInitialized) VolumeManager.instance.Initialize();
                    VolumeManager.instance.Update(trigger,~0);
                    Check(Mathf.Abs(VolumeManager.instance.stack.GetComponent<ColorAdjustments>().postExposure.value+2)<.01f,"Local box overrides inside");
                    trigger.position=new Vector3(200,0,0);VolumeManager.instance.Update(trigger,~0);
                    Check(Mathf.Abs(VolumeManager.instance.stack.GetComponent<ColorAdjustments>().postExposure.value+.4f)<.01f,"Global returns outside box");
                    trigger.position=new Vector3(106.5f,0,0);VolumeManager.instance.Update(trigger,~0);
                    float edge=VolumeManager.instance.stack.GetComponent<ColorAdjustments>().postExposure.value;
                    Check(edge<-.4f && edge>-2,"Local blend distance");
                    controller.enabled=false;
                    Check(Mathf.Abs(controller.mainLight.intensity-1)<.001f,"Disable restores light");
                    Check(controller.atmosphereVolume.sharedProfile==AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Base.asset"),"Disable restores profile");
                    var baseProfile=controller.atmosphereVolume.sharedProfile;baseProfile.TryGet<ColorAdjustments>(out var baseColor);
                    Check(Mathf.Approximately(baseColor.postExposure.value,0),"Shared profile not mutated");
                    Time.timeScale=1;controller.enabled=true;controller.ApplyInstant(a);
                    boundTarget=new GameObject("Bounding Target").transform;boundTarget.position=new Vector3(30,0,0);
                    low=new GameObject("Low Area").AddComponent<LightingBounding>();low.transform.position=boundTarget.position;
                    low.controller=controller;low.target=boundTarget;low.preset=b;low.enterDuration=low.exitDuration=0;low.size=Vector3.one*10;
                    Check(low.Contains(boundTarget.position) && !low.Contains(boundTarget.position+Vector3.right*20),"Bounding containment");
                    low.transform.rotation=Quaternion.Euler(0,45,0);low.transform.localScale=new Vector3(2,1,1);
                    Check(low.Contains(low.transform.TransformPoint(Vector3.right*4)),"Rotated scaled bounding");
                    high=new GameObject("High Area").AddComponent<LightingBounding>();high.gameObject.SetActive(false);high.transform.position=boundTarget.position;
                    high.controller=controller;high.target=boundTarget;high.preset=a;high.priority=10;high.enterDuration=high.exitDuration=0;
                    phase=4;since=EditorApplication.timeSinceStartup;break;
                case 4:
                    Check(controller.ActiveBounding==low && Mathf.Abs(controller.mainLight.intensity-.2f)<.001f,"Bounding enters without WorldTrigger");
                    c=ScriptableObject.CreateInstance<LightingPreset>();c.lightIntensity=.65f;
                    controller.ApplyInstant(c);
                    Check(Mathf.Abs(controller.mainLight.intensity-.65f)<.001f,"Later story cue can change lighting inside consumed bounding");
                    high.gameObject.SetActive(true);phase=5;since=EditorApplication.timeSinceStartup;break;
                case 5:
                    Check(controller.ActiveBounding==high && Mathf.Approximately(controller.mainLight.intensity,1),"Bounding priority");
                    high.gameObject.SetActive(false);phase=6;since=EditorApplication.timeSinceStartup;break;
                case 6:
                    Check(Mathf.Approximately(controller.mainLight.intensity,1),"Disabling bounding keeps applied lighting");
                    boundTarget.position=Vector3.one*200;phase=7;since=EditorApplication.timeSinceStartup;break;
                case 7:
                    Check(!controller.ActiveBounding && Mathf.Approximately(controller.mainLight.intensity,1),"Leaving does not restore old lighting");
                    boundTarget.position=low.transform.position;phase=8;since=EditorApplication.timeSinceStartup;break;
                case 8:
                    Check(low.HasTriggered && Mathf.Approximately(controller.mainLight.intensity,1),"Backtracking into consumed bounding does not reset lighting");
                    low.triggerOnce=false;boundTarget.position=Vector3.one*200;phase=9;since=EditorApplication.timeSinceStartup;break;
                case 9:
                    boundTarget.position=low.transform.position;phase=10;since=EditorApplication.timeSinceStartup;break;
                case 10:
                    Check(Mathf.Abs(controller.mainLight.intensity-.2f)<.001f,"Optional repeat activates on new entry");
                    UnityEngine.Object.Destroy(low.gameObject);phase=11;since=EditorApplication.timeSinceStartup;break;
                case 11:
                    Check(Mathf.Abs(controller.mainLight.intensity-.2f)<.001f,"Destroying bounding keeps lighting");
                    SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
                    File.WriteAllText("success.txt",checks+" lighting checks passed (real production controller and URP Volume blending).");
                    EditorApplication.Exit(0);break;
            }
        }
        catch(Exception e){Fail(e);}
    }
    private static void Fail(Exception e){Time.timeScale=1;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Debug.LogException(e);EditorApplication.Exit(1);}
}
