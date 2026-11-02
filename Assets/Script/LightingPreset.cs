using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Midnight/Lighting Preset", fileName = "LightingPreset")]
public class LightingPreset : ScriptableObject
{
    [Min(0), Tooltip("Seconds to reach this look when called by a Trigger or Timeline signal.")]
    public float transitionDuration = 4;
    [Header("Main Directional Light")]
    public Color lightColor = Color.white;
    [Min(0)] public float lightIntensity = 1;
    public Vector3 lightEuler = new Vector3(50, -30, 0);
    [Header("Ambient (Realtime / Unbaked Surfaces)")]
    public AmbientMode ambientMode = AmbientMode.Trilight;
    public Color ambientSky = new Color(.21f,.23f,.26f);
    public Color ambientEquator = new Color(.11f,.12f,.13f);
    public Color ambientGround = new Color(.05f,.04f,.035f);
    [Min(0)] public float ambientIntensity = 1;
    [Header("Global Distance Fog")]
    public bool fogEnabled;
    public FogMode fogMode = FogMode.ExponentialSquared;
    public Color fogColor = new Color(.3f,.35f,.4f);
    [Min(0)] public float fogDensity = .01f;
    public float fogStart = 20;
    public float fogEnd = 120;
    [Header("Skybox (If Shader Supports These Properties)")]
    public Color skyTint = new Color(.5f,.5f,.5f);
    [Min(0)] public float skyExposure = 1;
    [Header("URP Color Adjustments")]
    public float postExposure;
    public Color colorFilter = Color.white;
}
