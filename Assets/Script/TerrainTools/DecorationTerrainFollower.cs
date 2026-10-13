using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("System Modification/TerrainTools/Decoration Terrain Follower")]
public sealed class DecorationTerrainFollower : MonoBehaviour
{
    public bool autoFollow = true;
    public bool followSlope;
    [System.Serializable]
    public sealed class Binding
    {
        public Transform target;
        public Terrain terrain;
        public float heightOffset;
        public Quaternion relativeRotation;
    }
    [HideInInspector] public List<Binding> bindings = new List<Binding>();

    public static bool Sample(Terrain terrain, Vector3 position, out float height, out Quaternion slope)
    {
        height = 0; slope = Quaternion.identity;
        if (!terrain || !terrain.terrainData) return false;
        var local = position - terrain.transform.position;
        var size = terrain.terrainData.size;
        if (size.x <= 0 || size.z <= 0 || local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) return false;
        height = terrain.SampleHeight(position) + terrain.transform.position.y;
        slope = Quaternion.FromToRotation(Vector3.up, terrain.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z));
        return true;
    }

    public bool TryGetPlacement(Binding binding, out Vector3 position, out Quaternion rotation)
    {
        position = binding.target ? binding.target.position : Vector3.zero;
        rotation = binding.target ? binding.target.rotation : Quaternion.identity;
        if (!binding.target || !Sample(binding.terrain, position, out float height, out var slope)) return false;
        position.y = height + binding.heightOffset;
        if (followSlope) rotation = slope * binding.relativeRotation;
        return true;
    }
}
