using System;
using UnityEngine;

public static class GameplayNoise
{
    public enum Kind { Walk, Run, Gunshot }
    public static event Action<Vector3, float, Transform, Kind> Emitted;
    public static void DrawRadius(Vector3 position, float radius, Kind kind, string prefix = "")
    {
        if (radius <= 0) return;
        Gizmos.color = kind == Kind.Walk ? Color.cyan : kind == Kind.Run ? new Color(1f, .65f, .1f) : Color.magenta;
        Gizmos.DrawWireSphere(position, radius);
#if UNITY_EDITOR
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.DrawWireDisc(position, Vector3.up, radius);
        var style = new GUIStyle(UnityEditor.EditorStyles.helpBox)
        {
            fontSize = 5,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(5, 5, 3, 3),
            alignment = TextAnchor.MiddleLeft
        };
        style.normal.background = UnityEditor.EditorGUIUtility.whiteTexture;
        style.normal.textColor = Color.black;
        string name = kind == Kind.Walk ? "Walk noise" : kind == Kind.Run ? "Run noise" : "Gunshot noise";
        string heading = string.IsNullOrEmpty(prefix) ? "Noise range" : "Hearing range";
        Vector3 labelOffset = kind == Kind.Walk ? Vector3.forward : kind == Kind.Run ? Vector3.right : Vector3.back;
        UnityEditor.Handles.Label(position + labelOffset * radius + Vector3.up * .15f,
            heading + " | " + name + ": " + radius.ToString("0.#") + " m", style);
#endif
    }
    public static void Emit(Vector3 position, float radius, Transform source, Kind kind)
    {
        if (radius > 0 && source != null) Emitted?.Invoke(position, radius, source, kind);
    }
}
