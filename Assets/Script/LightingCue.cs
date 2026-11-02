using UnityEngine;

public class LightingCue : MonoBehaviour
{
    public SceneLightingController controller;
    public LightingPreset preset;
    [Tooltip("Negative uses the preset duration; zero applies instantly.")]
    public float durationOverride = -1;
    // Connect WorldTrigger.OnTriggered or a Timeline Signal Receiver to Play().
    public void Play()
    {
        if (!controller || !preset) return;
        controller.TransitionTo(preset, durationOverride < 0 ? preset.transitionDuration : durationOverride);
    }
}
