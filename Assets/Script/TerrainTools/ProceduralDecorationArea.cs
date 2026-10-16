using System.Collections.Generic;
using UnityEngine;

public partial class ProceduralDecorationPlacer
{
    public enum GenerationAreaMode { BoundingBox, Polygon }
    public GenerationAreaMode areaMode;
    [Tooltip("Ordered boundary pivots in tool-local X/Z. Polygon mode follows Terrain without a box height limit.")]
    public List<Vector2> areaPoints = new List<Vector2>();

    public string ValidateGenerationArea()
    {
        if (!IsFinite(transform.lossyScale) || Mathf.Abs(transform.localToWorldMatrix.determinant) < .000001f)
            return "The tool transform must have finite, non-zero scale.";
        if (areaMode == GenerationAreaMode.BoundingBox)
            return !IsFinite(areaCenter) || !IsFinite(areaSize) || areaSize.x <= 0 || areaSize.y <= 0 || areaSize.z <= 0
                ? "Area size must be positive and finite." : null;
        // Terrain sampling is vertical; a tilted polygon plane cannot describe its X/Z footprint reliably.
        if (Mathf.Abs(Vector3.Dot(transform.up.normalized, Vector3.up)) < .9999f)
            return "Polygon mode supports Y rotation and scale. Remove X/Z tilt from the tool transform.";
        return DecorationPolygon.Validate(areaPoints);
    }
    public Vector3 SampleGenerationArea()
    {
        if (areaMode == GenerationAreaMode.BoundingBox)
            return transform.TransformPoint(areaCenter + new Vector3(Random.Range(-areaSize.x * .5f, areaSize.x * .5f), 0, Random.Range(-areaSize.z * .5f, areaSize.z * .5f)));
        var bounds = DecorationPolygon.Bounds(areaPoints);
        return transform.TransformPoint(new Vector3(Random.Range(bounds.xMin, bounds.xMax), 0, Random.Range(bounds.yMin, bounds.yMax)));
    }
    public bool ContainsAreaPoint(Vector3 world)
    {
        var local = transform.InverseTransformPoint(world);
        return DecorationPolygon.Contains(new Vector2(local.x, local.z), areaPoints);
    }
}

public static class DecorationPolygon
{
    const float Epsilon = .00001f;
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static float Distance(Vector2 p, Vector2 a, Vector2 b)
    {
        var d = b - a;
        return (p - (a + d * (d.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0))).magnitude;
    }
    static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float x = Cross(b-a,c-a), y = Cross(b-a,d-a), u = Cross(d-c,a-c), v = Cross(d-c,b-c);
        return ((x > Epsilon && y < -Epsilon) || (x < -Epsilon && y > Epsilon)) &&
            ((u > Epsilon && v < -Epsilon) || (u < -Epsilon && v > Epsilon));
    }
    public static string Validate(IList<Vector2> points)
    {
        if (points == null || points.Count < 3) return "Add at least three boundary pivots.";
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var a = points[i]; var b = points[(i+1)%points.Count]; var next = points[(i+2)%points.Count];
            if (float.IsNaN(a.x) || float.IsInfinity(a.x) || float.IsNaN(a.y) || float.IsInfinity(a.y)) return "Boundary points must be finite.";
            if ((b-a).sqrMagnitude < .000001f) return "Remove duplicate boundary pivots.";
            if (Mathf.Abs(Cross(b-a,next-b)) < Epsilon && Vector2.Dot(b-a,next-b) < 0) return "Boundary edges must not double back.";
            area += (double)a.x*b.y-(double)b.x*a.y;
            for (int j = i+1; j < points.Count; j++)
            {
                if (j == i+1 || (i == 0 && j == points.Count-1)) continue;
                var c = points[j]; var d = points[(j+1)%points.Count];
                if (Crosses(a,b,c,d) || Distance(a,c,d) < Epsilon || Distance(b,c,d) < Epsilon || Distance(c,a,b) < Epsilon || Distance(d,a,b) < Epsilon)
                    return "Boundary edges must not cross or touch.";
            }
        }
        return System.Math.Abs(area) < .0001 ? "Boundary must enclose an area." : null;
    }
    public static Rect Bounds(IList<Vector2> points)
    {
        Vector2 min = points[0], max = min;
        foreach (var point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    public static bool Contains(Vector2 p, IList<Vector2> points)
    {
        if (points == null || points.Count < 3) return false;
        bool inside = false;
        for (int i=0,j=points.Count-1; i<points.Count; j=i++)
        {
            var a=points[j]; var b=points[i];
            if (Distance(p,a,b) <= Epsilon) return true;
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside = !inside;
        }
        return inside;
    }
    // Check all projected box-corner pairs, not only vertices: an object must not bridge a concave cutout.
    public static bool ContainsFootprint(Vector2[] corners, IList<Vector2> points)
    {
        foreach (var p in corners) if (!Contains(p,points)) return false;
        for (int a=0; a<corners.Length; a++) for (int b=a+1; b<corners.Length; b++)
        {
            var start = corners[a]; var end = corners[b];
            if (!Contains((start+end)*.5f,points)) return false;
            for (int i=0; i<points.Count; i++)
            {
                var c=points[i]; var d=points[(i+1)%points.Count];
                if (Crosses(start,end,c,d)) return false;
                // Split at boundary vertices on the segment to handle a tangent concave notch.
                if (Distance(c,start,end) < Epsilon && (!Contains(Vector2.Lerp(start,c,.999f),points) || !Contains(Vector2.Lerp(end,c,.999f),points))) return false;
            }
        }
        return true;
    }
}
