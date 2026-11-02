using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Lighting/Lighting Bounding")]
public class LightingBounding : MonoBehaviour
{
    [Tooltip("Entering changes GLOBAL lighting permanently for this play session. Leaving does not restore the old look.")]
    public SceneLightingController controller;
    public LightingPreset preset;
    [Tooltip("Optional. Empty uses the object tagged Player, then Main Camera.")]
    public Transform target;
    public Vector3 center;
    public Vector3 size = new Vector3(15,8,15);
    public int priority;
    [Tooltip("Activate once per play session, so backtracking cannot reset the story lighting.")]
    public bool triggerOnce = true;
    public bool HasTriggered { get; private set; }
    internal bool IsTargetInside { get; private set; }
    [Min(0)] public float enterDuration = 2;
    [HideInInspector] public float exitDuration = 2; // Legacy serialized value; no exit transition.
    [HideInInspector]
    public float exitMargin = .25f;
    public Color gizmoColor = new Color(.3f,.7f,1,.15f);
    private Transform resolvedTarget;
    private static readonly List<LightingBounding> areas = new List<LightingBounding>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => areas.Clear();
    private void OnEnable() { if (!areas.Contains(this)) areas.Add(this); }
    private void OnDisable() { areas.Remove(this); IsTargetInside=false; }
    public void ResetActivation() { HasTriggered=false; IsTargetInside=false; }
    internal void MarkTriggered() => HasTriggered=true;
    public bool Contains(Vector3 position, float margin = 0)
    {
        Vector3 point = transform.InverseTransformPoint(position) - center;
        Vector3 half = new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z))*.5f;
        var local = new Vector3(Mathf.Clamp(point.x,-half.x,half.x),Mathf.Clamp(point.y,-half.y,half.y),Mathf.Clamp(point.z,-half.z,half.z));
        return Vector3.Distance(position,transform.TransformPoint(center+local)) <= Mathf.Max(0,margin)+.0001f;
    }
    internal static LightingBounding Select(SceneLightingController owner, LightingBounding current)
    {
        LightingBounding best=null;
        foreach(var area in areas)
        {
            if(!area || !area.isActiveAndEnabled || area.controller!=owner || !area.preset)continue;
            Transform subject=area.target;
            if(!subject)
            {
                if(!area.resolvedTarget)
                {
                    var player=GameObject.FindGameObjectWithTag("Player");
                    area.resolvedTarget=player ? player.transform : Camera.main ? Camera.main.transform : null;
                }
                subject=area.resolvedTarget;
            }
            bool inside=subject && area.Contains(subject.position,area.IsTargetInside ? area.exitMargin : 0);
            bool entered=inside && !area.IsTargetInside;
            area.IsTargetInside=inside;
            if(!entered || area.triggerOnce && area.HasTriggered)continue;
            if(!best || area.priority>best.priority || area.priority==best.priority && area.GetInstanceID()<best.GetInstanceID())best=area;
        }
        return best;
    }
    private void OnDrawGizmos()
    {
        var matrix=Gizmos.matrix;Gizmos.matrix=transform.localToWorldMatrix;
        Gizmos.color=gizmoColor;Gizmos.DrawCube(center,size);
        var color=gizmoColor;color.a=1;Gizmos.color=color;Gizmos.DrawWireCube(center,size);Gizmos.matrix=matrix;
    }
}
