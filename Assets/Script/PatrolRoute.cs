using UnityEngine;

public class PatrolRoute : MonoBehaviour
{
    [System.Serializable]
    public class PointWait
    {
        public Transform point;
        public bool overrideWaitTime;
        [Min(0f)] public float waitTime = 2f;
    }
    public EnemyController.PatrolMode mode = EnemyController.PatrolMode.Loop;
    public Transform[] points = new Transform[0];
    [HideInInspector] public System.Collections.Generic.List<PointWait> pointWaits = new System.Collections.Generic.List<PointWait>();
    public float GetWaitTime(Transform point, float fallback)
    {
        if (pointWaits != null)
            foreach (PointWait entry in pointWaits)
                if (entry != null && entry.point == point) return entry.overrideWaitTime ? Mathf.Max(0f, entry.waitTime) : fallback;
        // Compatibility for existing routes until their settings are imported in the editor.
        PatrolPoint legacy = point != null ? point.GetComponent<PatrolPoint>() : null;
        return legacy != null && legacy.overrideWaitTime ? legacy.waitTime : fallback;
    }
    [Min(0.1f)] public float markerSize = 0.4f;
    public Color pointColor = new Color(1f, 0.1f, 0.8f);
    public Color routeColor = Color.cyan;
    public bool showGizmos = true;
    public int agentTypeId;
    public int areaMask = -1;

    private void OnDrawGizmos()
    {
        if (!showGizmos || points == null) return;
        Transform previous = null;
        Transform first = null;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            Vector3 position = points[i].position + Vector3.up * markerSize;
            Gizmos.color = pointColor;
            Gizmos.DrawSphere(position, markerSize);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position + Vector3.up * (markerSize + 0.2f), name + " P" + (i + 1));
#endif
            if (mode == EnemyController.PatrolMode.Random)
                for (int j = 0; j < i; j++) { if (points[j] != null) DrawLeg(points[j].position, points[i].position, false); }
            else if (previous != null) DrawLeg(previous.position, points[i].position, true);
            if (first == null) first = points[i];
            previous = points[i];
        }
        if (mode == EnemyController.PatrolMode.Loop && previous != null && first != previous)
            DrawLeg(previous.position, first.position, true);
    }
    private void DrawLeg(Vector3 from, Vector3 to, bool arrow)
    {
        Gizmos.color = routeColor;
        from += Vector3.up * 0.15f; to += Vector3.up * 0.15f;
        Gizmos.DrawLine(from, to);
        if (!arrow || (to - from).sqrMagnitude < 0.01f) return;
        Vector3 direction = (to - from).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized * 0.25f;
        Vector3 tip = Vector3.Lerp(from, to, 0.65f);
        Gizmos.DrawLine(tip, tip - direction * 0.5f + side);
        Gizmos.DrawLine(tip, tip - direction * 0.5f - side);
        if (mode == EnemyController.PatrolMode.PingPong)
        {
            tip = Vector3.Lerp(from, to, 0.35f);
            Gizmos.DrawLine(tip, tip + direction * 0.5f + side);
            Gizmos.DrawLine(tip, tip + direction * 0.5f - side);
        }
    }
}
