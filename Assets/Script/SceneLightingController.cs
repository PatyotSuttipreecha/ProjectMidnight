using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class SceneLightingController : MonoBehaviour
{
    public Light mainLight;
    public Volume atmosphereVolume;
    public LightingPreset initialPreset;
    public bool applyOnStart = true;
    public bool useUnscaledTime = true;
    public bool restoreOnDisable = true;
    public UnityEvent onTransitionStarted = new UnityEvent();
    public UnityEvent onTransitionCompleted = new UnityEvent();
    public LightingPreset CurrentPreset { get; private set; }
    public bool IsTransitioning { get; private set; }
    public float Progress => IsTransitioning ? Mathf.Clamp01(elapsed / duration) : 1;
    private LightingPreset original, from, destination;
    private LightingPreset target;
    private Material originalSky, runtimeSky;
    private Light originalSun;
    private VolumeProfile runtimeProfile, previousProfile;
    private ColorAdjustments adjustments;
    private float elapsed, duration, previousWeight;
    private bool ready, started;
    public LightingBounding ActiveBounding { get; private set; }

    private void Start() { started = true; Initialize(); if (applyOnStart && initialPreset && !target) ApplyInstant(initialPreset); }
    private void OnEnable() { if (started) { Initialize(); if (applyOnStart && initialPreset) ApplyInstant(initialPreset); } }
    private void Initialize()
    {
        if (ready) return;
        if (!mainLight) { Debug.LogWarning("Scene Lighting: assign the main Directional Light.", this); return; }
        originalSky = RenderSettings.skybox;
        originalSun = RenderSettings.sun;
        if (originalSky)
        {
            runtimeSky = new Material(originalSky) { name = "Scene Lighting Sky (Runtime)", hideFlags = HideFlags.HideAndDontSave };
            RenderSettings.skybox = runtimeSky;
        }
        if (atmosphereVolume)
        {
            previousProfile = atmosphereVolume.sharedProfile;
            previousWeight = atmosphereVolume.weight;
            runtimeProfile = previousProfile ? Instantiate(previousProfile) : ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            // VolumeProfile components must also be cloned; never edit a shared asset.
            if (previousProfile)
            {
                runtimeProfile.components.Clear();
                foreach (var component in previousProfile.components)
                {
                    var copy = Instantiate(component); copy.hideFlags = HideFlags.HideAndDontSave;
                    runtimeProfile.components.Add(copy);
                }
            }
            if (!runtimeProfile.TryGet(out adjustments)) adjustments = runtimeProfile.Add<ColorAdjustments>();
            adjustments.postExposure.overrideState = adjustments.colorFilter.overrideState = true;
            atmosphereVolume.sharedProfile = runtimeProfile;
            atmosphereVolume.weight = 1;
        }
        original = ScriptableObject.CreateInstance<LightingPreset>();
        from = ScriptableObject.CreateInstance<LightingPreset>();
        original.hideFlags = from.hideFlags = HideFlags.HideAndDontSave;
        Capture(original);
        ready = true;
    }
    public void Capture(LightingPreset preset)
    {
        if (!preset || !mainLight) return;
        preset.lightColor = mainLight.color; preset.lightIntensity = mainLight.intensity;
        preset.lightEuler = mainLight.transform.eulerAngles;
        preset.ambientMode = RenderSettings.ambientMode;
        preset.ambientSky = RenderSettings.ambientSkyColor; preset.ambientEquator = RenderSettings.ambientEquatorColor;
        preset.ambientGround = RenderSettings.ambientGroundColor; preset.ambientIntensity = RenderSettings.ambientIntensity;
        preset.fogEnabled = RenderSettings.fog; preset.fogMode = RenderSettings.fogMode;
        preset.fogColor = RenderSettings.fogColor; preset.fogDensity = RenderSettings.fogDensity;
        preset.fogStart = RenderSettings.fogStartDistance; preset.fogEnd = RenderSettings.fogEndDistance;
        Material sky = RenderSettings.skybox;
        if (sky && sky.HasProperty("_SkyTint")) preset.skyTint = sky.GetColor("_SkyTint");
        else if (sky && sky.HasProperty("_Tint")) preset.skyTint = sky.GetColor("_Tint");
        if (sky && sky.HasProperty("_Exposure")) preset.skyExposure = sky.GetFloat("_Exposure");
        if (adjustments != null) { preset.postExposure = adjustments.postExposure.value; preset.colorFilter = adjustments.colorFilter.value; }
    }
    public void ApplyInstant(LightingPreset preset) => TransitionTo(preset, 0);
    public void BlendTo(LightingPreset preset) { if (preset) TransitionTo(preset, preset.transitionDuration); }
    public void TransitionTo(LightingPreset preset, float seconds)
    {
        if (!preset) return;
        BeginTransition(preset, seconds);
    }
    private void BeginTransition(LightingPreset preset, float seconds)
    {
        if (!preset || !isActiveAndEnabled) return;
        Initialize(); if (!ready) return;
        Capture(from);
        if (destination) Release(destination);
        destination = Instantiate(preset); // Snapshot prevents asset edits changing a running transition.
        destination.hideFlags = HideFlags.HideAndDontSave;
        target = preset; elapsed = 0; duration = Mathf.Max(0, seconds);
        IsTransitioning = duration > 0;
        onTransitionStarted.Invoke();
        if (!IsTransitioning) { Apply(from, destination, 1); Complete(); }
    }
    private void Update()
    {
        var bounding = mainLight ? LightingBounding.Select(this, ActiveBounding) : null;
        if (bounding)
        {
            bounding.MarkTriggered();ActiveBounding=bounding;
            BeginTransition(bounding.preset,bounding.enterDuration);
        }
        else if (ActiveBounding && (!ActiveBounding.IsTargetInside || !ActiveBounding.isActiveAndEnabled)) ActiveBounding=null;
        if (!IsTransitioning) return;
        elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Apply(from, destination, t * t * (3 - 2 * t));
        if (t >= 1) Complete();
    }
    private void Complete() { IsTransitioning = false; CurrentPreset = target; onTransitionCompleted.Invoke(); }
    private void Apply(LightingPreset a, LightingPreset b, float t)
    {
        mainLight.color = Color.Lerp(a.lightColor, b.lightColor, t);
        mainLight.intensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t);
        mainLight.transform.rotation = Quaternion.Slerp(Quaternion.Euler(a.lightEuler), Quaternion.Euler(b.lightEuler), t);
        RenderSettings.sun = mainLight;
        RenderSettings.ambientMode = t >= 1 ? b.ambientMode : a.ambientMode;
        RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky,b.ambientSky,t);
        RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator,b.ambientEquator,t);
        RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround,b.ambientGround,t);
        RenderSettings.ambientIntensity = Mathf.Lerp(a.ambientIntensity,b.ambientIntensity,t);
        // Fog mode is discrete. Keep the current mode until the final frame if modes differ.
        RenderSettings.fogMode = t >= 1 ? b.fogMode : a.fogMode;
        RenderSettings.fog = t >= 1 ? b.fogEnabled : a.fogEnabled || b.fogEnabled;
        RenderSettings.fogColor = Color.Lerp(a.fogColor,b.fogColor,t);
        RenderSettings.fogDensity = Mathf.Lerp(a.fogEnabled ? a.fogDensity : 0,b.fogEnabled ? b.fogDensity : 0,t);
        RenderSettings.fogStartDistance = t >= 1 ? b.fogStart : Mathf.Lerp(a.fogEnabled ? a.fogStart : 10000,b.fogEnabled ? b.fogStart : 10000,t);
        RenderSettings.fogEndDistance = t >= 1 ? b.fogEnd : Mathf.Lerp(a.fogEnabled ? a.fogEnd : 20000,b.fogEnabled ? b.fogEnd : 20000,t);
        if (runtimeSky)
        {
            if (runtimeSky.HasProperty("_SkyTint")) runtimeSky.SetColor("_SkyTint",Color.Lerp(a.skyTint,b.skyTint,t));
            else if (runtimeSky.HasProperty("_Tint")) runtimeSky.SetColor("_Tint",Color.Lerp(a.skyTint,b.skyTint,t));
            if (runtimeSky.HasProperty("_Exposure")) runtimeSky.SetFloat("_Exposure",Mathf.Lerp(a.skyExposure,b.skyExposure,t));
        }
        if (adjustments != null)
        {
            adjustments.postExposure.value = Mathf.Lerp(a.postExposure,b.postExposure,t);
            adjustments.colorFilter.value = Color.Lerp(a.colorFilter,b.colorFilter,t);
        }
    }
    private static void Release(Object value)
    {
        if (!value) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
#if UNITY_EDITOR
    // Called only by the editor preview owner. Does not invoke gameplay events or modify presets.
    public void PreviewInEditor(LightingPreset preset, float blend, LightingPreset comparison = null)
    {
        if (Application.isPlaying || !preset) return;
        Initialize(); if (!ready) return;
        Apply(comparison ? comparison : original, preset, Mathf.Clamp01(blend));
    }
    public void RestoreEditorPreview()
    {
        if (!Application.isPlaying) Cleanup(true);
    }
    public void ApplySavedEditorPreset(LightingPreset preset, Material sky, VolumeProfile profile)
    {
        if (Application.isPlaying || !preset || !mainLight) return;
        RestoreEditorPreview();
        runtimeSky=sky;
        if(profile)profile.TryGet(out adjustments);
        Apply(preset,preset,1);
        RenderSettings.skybox=sky;
        if(atmosphereVolume) {atmosphereVolume.sharedProfile=profile;atmosphereVolume.weight=1;}
        initialPreset=preset;applyOnStart=true;
        runtimeSky=null;adjustments=null;
    }
#endif
    private void OnDisable() => Cleanup(restoreOnDisable);
    private void Cleanup(bool restore)
    {
        IsTransitioning = false;
        ActiveBounding=null;
        if (!ready) return;
        if (restore && mainLight) Apply(original,original,1);
        if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = originalSky;
        if (restore) RenderSettings.sun = originalSun;
        if (atmosphereVolume) { atmosphereVolume.sharedProfile = previousProfile; atmosphereVolume.weight = previousWeight; }
        if (runtimeProfile)
        {
            foreach (var component in runtimeProfile.components) Release(component);
            Release(runtimeProfile);
        }
        Release(runtimeSky);
        Release(original); Release(from); Release(destination);
        adjustments = null; ready = false; CurrentPreset = null;
    }
}
