using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ProceduralRockPlacer")]
[AddComponentMenu("System Modification/TerrainTools/Procedural Decoration Placer")]
public class ProceduralDecorationPlacer : MonoBehaviour
{
    // =========================================================
    // TERRAIN
    // =========================================================

    [Header("Terrain")]
    public Terrain terrain;


    // =========================================================
    // ROCK PREFABS
    // =========================================================

    [Header("Decoration Prefabs")]
    public GameObject[] rockPrefabs;

    [HideInInspector]
    public float[] rockPrefabPercentages;

    public float GetRockPrefabWeight(int index)
    {
        if (rockPrefabs == null || index < 0 ||
            index >= rockPrefabs.Length || rockPrefabs[index] == null)
            return 0f;

        // Existing scenes and newly added elements retain equal chances.
        float weight = rockPrefabPercentages != null && index < rockPrefabPercentages.Length
            ? rockPrefabPercentages[index] : 100f;
        return float.IsNaN(weight) || float.IsInfinity(weight)
            ? 0f : Mathf.Clamp(weight, 0f, 100f);
    }

    private GameObject SelectRockPrefab(float totalWeight)
    {
        float roll = Random.value * totalWeight;
        GameObject lastEligible = null;
        for (int i = 0; i < rockPrefabs.Length; i++)
        {
            float weight = GetRockPrefabWeight(i);
            if (weight <= 0f)
                continue;

            lastEligible = rockPrefabs[i];
            if (roll < weight)
                return lastEligible;
            roll -= weight;
        }

        // Random.value can return 1, so handle the upper endpoint.
        return lastEligible;
    }


    // =========================================================
    // GENERATION AREA
    // =========================================================

    [Header("Generation Area")]

    [Tooltip("Center of the generation area relative to this GameObject.")]
    public Vector3 areaCenter = Vector3.zero;

    [Tooltip("Local size of the generation box. The complete decoration bounds after rotation and scaling must fit inside all three axes, including Y.")]
    public Vector3 areaSize = new Vector3(50f, 20f, 50f);


    // =========================================================
    // PLACEMENT
    // =========================================================

    [Header("Placement")]

    [Min(1)]
    public int amount = 500;

    [Tooltip("Minimum distance between generated decorations.")]
    [Min(0f)]
    public float minDistance = 2f;


    // =========================================================
    // ROCK SIZE
    // =========================================================

    [Header("Decoration Size")]

    public bool randomSize = true;

    [Tooltip("Keep X/Y/Z scale proportional.")]
    public bool uniformScale = true;

    [Min(0.01f)]
    public float minUniformScale = 0.5f;

    [Min(0.01f)]
    public float maxUniformScale = 1.5f;

    public Vector3 minScale = Vector3.one * 0.5f;

    public Vector3 maxScale = Vector3.one * 1.5f;


    // =========================================================
    // ROTATION
    // =========================================================

    [Header("Rotation")]

    public bool randomRotation = true;

    [Tooltip("Rotate the decoration to follow Terrain normal.")]
    public bool alignToTerrain = false;


    // =========================================================
    // SLOPE
    // =========================================================

    [Header("Slope")]

    [Range(0f, 90f)]
    public float maxSlope = 35f;


    // =========================================================
    // HEIGHT
    // =========================================================

    [Header("Height")]

    public bool useHeightLimit = false;

    public float minHeight = 0f;

    public float maxHeight = 1000f;


    // =========================================================
    // TERRAIN LAYER FILTER
    // =========================================================

    [Header("Terrain Layer Filter")]

    [Tooltip("Enable Terrain Painting Layer filtering.")]
    public bool useTerrainLayerFilter = true;

    [Range(0f, 1f)]
    [Tooltip(
        "If an excluded Terrain Layer has this weight or higher, " +
        "the decoration will NOT be placed."
    )]
    public float excludeLayerThreshold = 0.2f;

    [HideInInspector]
    public List<TerrainLayerRule> terrainLayerRules =
        new List<TerrainLayerRule>();


    // =========================================================
    // SCENE OBJECT DETECTION
    // =========================================================

    public enum SceneDetectionMode
    {
        ColliderOnly,
        RendererBoundsOnly,
        ColliderAndRendererBounds
    }

    [Header("Scene Object Detection")]

    [Tooltip(
        "If enabled, generated decorations will avoid existing GameObjects in the scene."
    )]
    public bool detectSceneObjects = true;

    [Tooltip(
        "Collider = objects with Collider.\n" +
        "Renderer Bounds = visible Mesh/Renderer objects.\n" +
        "Collider + Renderer = use both."
    )]
    public SceneDetectionMode sceneDetectionMode =
        SceneDetectionMode.ColliderAndRendererBounds;

    [Tooltip(
        "Only GameObjects on these Layers will block decoration generation."
    )]
    public LayerMask sceneObjectLayers = ~0;

    [Tooltip(
        "Radius around each candidate position used to detect existing objects."
    )]
    [Min(0.01f)]
    public float sceneObjectCheckRadius = 1f;

    [Tooltip(
        "Extra empty space between generated decorations and existing GameObjects."
    )]
    [Min(0f)]
    public float sceneObjectPadding = 0.25f;

    [Tooltip("Ignore Trigger Colliders.")]
    public bool ignoreTriggers = true;

    [Tooltip(
        "Ignore the Terrain Collider itself when checking for existing objects."
    )]
    public bool ignoreTerrain = true;


    // =========================================================
    // RANDOM
    // =========================================================

    [Header("Random")]

    public int seed = 12345;

    [Tooltip("Choose a new seed each time Generate runs. Disable to repeat the current seed.")]
    public bool randomizeSeedOnGenerate = true;

    public void RandomizeSeed()
    {
        int next = System.Guid.NewGuid().GetHashCode() & int.MaxValue;
        seed = next == seed ? (next == int.MaxValue ? 0 : next + 1) : next;
    }


    // =========================================================
    // CONTAINER
    // =========================================================

    [Header("Container")]

    public Transform rockContainer;


    // =========================================================
    // GIZMOS
    // =========================================================

    [Header("Gizmos")]

    public bool showGizmo = true;

    [Range(0.01f, 1f)]
    public float gizmoAlpha = 0.15f;

    public Color gizmoColor = Color.green;


    // =========================================================
    // INTERNAL
    // =========================================================

    private readonly List<Vector3> generatedPositions =
        new List<Vector3>();

    private readonly List<Bounds> cachedSceneRendererBounds =
        new List<Bounds>();


    // =========================================================
    // TERRAIN LAYER RULE
    // =========================================================

    [System.Serializable]
    public class TerrainLayerRule
    {
        public TerrainLayer terrainLayer;

        public bool excluded = false;
    }


    // =========================================================
    // GENERATE
    // =========================================================

    [Min(1), Tooltip("Maximum random trials per requested decoration. Increase when only a small part of the area passes the filters.")]
    public int attemptsPerDecoration = 100;
    [HideInInspector] public string lastGenerationReport;
    public void Generate()
    {
        var previousRandomState = Random.state;
        try { GenerateInternal(); }
        finally { Random.state = previousRandomState; }
    }
    private void GenerateInternal()
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError(
                "ProceduralDecorationPlacer: Terrain is not assigned."
            );

            return;
        }

        if (rockPrefabs == null ||
            rockPrefabs.Length == 0)
        {
            Debug.LogError(
                "ProceduralDecorationPlacer: No Decoration Prefabs assigned."
            );

            return;
        }

        if (!IsFinite(areaSize) || !IsFinite(areaCenter) || areaSize.x <= 0f ||
            areaSize.y <= 0f || areaSize.z <= 0f ||
            !IsFinite(transform.lossyScale) || Mathf.Abs(transform.localToWorldMatrix.determinant) < 0.000001f)
        {
            Debug.LogError(
                "ProceduralDecorationPlacer: Area Size X/Y/Z must be finite and greater than 0, with a valid center and non-zero transform scale."
            );

            return;
        }

        float totalPrefabWeight = 0f;
        for (int i = 0; i < rockPrefabs.Length; i++)
            totalPrefabWeight += GetRockPrefabWeight(i);

        if (totalPrefabWeight <= 0f)
        {
            Debug.LogWarning("ProceduralDecorationPlacer: Assign at least one prefab with a percentage greater than 0.", this);
            return;
        }

        if (amount <= 0 || (useHeightLimit && minHeight > maxHeight))
        {
            lastGenerationReport = "Generation stopped: Amount must be positive and Min Height must not exceed Max Height. Existing objects kept.";
            Debug.LogWarning(lastGenerationReport, this);
            return;
        }
        // Safety for scale values
        if (maxUniformScale < minUniformScale)
        {
            float temp = minUniformScale;
            minUniformScale = maxUniformScale;
            maxUniformScale = temp;
        }

        // =====================================================
        // CLEAR PREVIOUS ROCKS
        // =====================================================

        ClearRocks();


        // =====================================================
        // CREATE CONTAINER
        // =====================================================

        if (rockContainer == null)
        {
            GameObject container =
                new GameObject("Generated Decorations");

            container.transform.SetParent(
                transform,
                false
            );

#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(container, "Create Decoration Container");
#endif
            rockContainer =
                container.transform;
        }


        generatedPositions.Clear();


        // =====================================================
        // CACHE EXISTING SCENE OBJECTS
        // =====================================================

        Physics.SyncTransforms();
        CacheSceneObjects();


        // =====================================================
        // RANDOM
        // =====================================================

        if (randomizeSeedOnGenerate) RandomizeSeed();
        Random.InitState(seed);

        int placed = 0;
        int attempts = 0;

        int rejectedBySceneObjects = 0;
        int rejectedByAreaBounds = 0;
        int outsideTerrain = 0, heightRejected = 0, slopeRejected = 0, layerRejected = 0, distanceRejected = 0, prefabRejected = 0;

        int maxAttempts =
             (int)System.Math.Min(1000000L, System.Math.Max((long)amount * Mathf.Max(1, attemptsPerDecoration), 1000L));


        // =====================================================
        // GENERATION LOOP
        // =====================================================

        while (placed < amount &&
               attempts < maxAttempts)
        {
            attempts++;


            // -------------------------------------------------
            // RANDOM POSITION INSIDE GENERATION AREA
            // -------------------------------------------------

            float localX =
                Random.Range(
                    -areaSize.x * 0.5f,
                    areaSize.x * 0.5f
                );

            float localZ =
                Random.Range(
                    -areaSize.z * 0.5f,
                    areaSize.z * 0.5f
                );


            Vector3 localPosition =
                areaCenter +
                new Vector3(
                    localX,
                    0f,
                    localZ
                );


            Vector3 position =
                transform.TransformPoint(
                    localPosition
                );


            // -------------------------------------------------
            // TERRAIN COORDINATE
            // -------------------------------------------------

            Vector3 terrainPosition =
                terrain.transform.position;

            Vector3 terrainSize =
                terrain.terrainData.size;


            float normalizedX =
                (position.x - terrainPosition.x)
                / terrainSize.x;

            float normalizedZ =
                (position.z - terrainPosition.z)
                / terrainSize.z;


            // Outside Terrain
            if (normalizedX < 0f ||
                normalizedX > 1f ||
                normalizedZ < 0f ||
                normalizedZ > 1f)
            {
                outsideTerrain++;
                continue;
            }


            // -------------------------------------------------
            // TERRAIN HEIGHT
            // -------------------------------------------------

            float terrainHeight =
                terrain.terrainData.GetInterpolatedHeight(
                    normalizedX,
                    normalizedZ
                );


            float worldY =
                terrainPosition.y +
                terrainHeight;


            position.y =
                worldY;


            // -------------------------------------------------
            // HEIGHT FILTER
            // -------------------------------------------------

            if (useHeightLimit)
            {
                if (worldY < minHeight ||
                    worldY > maxHeight)
                {
                    heightRejected++;
                    continue;
                }
            }


            // -------------------------------------------------
            // SLOPE FILTER
            // -------------------------------------------------

            float slope =
                terrain.terrainData.GetSteepness(
                    normalizedX,
                    normalizedZ
                );


            if (slope > maxSlope) { slopeRejected++; continue; }


            // -------------------------------------------------
            // TERRAIN PAINTING LAYER FILTER
            // -------------------------------------------------

            if (!IsPositionAllowedByTerrainLayer(
                    normalizedX,
                    normalizedZ))
            {
                layerRejected++;
                continue;
            }


            // -------------------------------------------------
            // MINIMUM DISTANCE BETWEEN GENERATED ROCKS
            // -------------------------------------------------

            bool tooClose = false;


            for (int i = 0;
                 i < generatedPositions.Count;
                 i++)
            {
                // Horizontal distance is normally more useful
                // for objects placed on Terrain.

                Vector2 currentPosition =
                    new Vector2(
                        position.x,
                        position.z
                    );

                Vector2 generatedPosition =
                    new Vector2(
                        generatedPositions[i].x,
                        generatedPositions[i].z
                    );


                if (Vector2.Distance(
                        currentPosition,
                        generatedPosition
                    ) < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }


            if (tooClose) { distanceRejected++; continue; }


            // -------------------------------------------------
            // EXISTING SCENE OBJECT DETECTION
            // -------------------------------------------------

            if (IsBlockedBySceneObject(position))
            {
                rejectedBySceneObjects++;

                // IMPORTANT:
                //
                // We do NOT stop generation.
                //
                // The while loop simply tries another random
                // position inside the Generation Area.
                continue;
            }


            // -------------------------------------------------
            // RANDOM PREFAB
            // -------------------------------------------------

            GameObject prefab = SelectRockPrefab(totalPrefabWeight);


            if (prefab == null) { prefabRejected++; continue; }


            // -------------------------------------------------
            // CREATE ROCK
            // -------------------------------------------------

            GameObject rock;


#if UNITY_EDITOR

            rock =
                (GameObject)
                PrefabUtility.InstantiatePrefab(
                    prefab
                );

#else

            rock =
                Instantiate(prefab);

#endif


            if (rock == null) { prefabRejected++; continue; }


            // -------------------------------------------------
            // POSITION
            // -------------------------------------------------

            rock.transform.position =
                position;


            // -------------------------------------------------
            // ROTATION
            // -------------------------------------------------

            float randomY =
                Random.Range(
                    0f,
                    360f
                );


            if (alignToTerrain)
            {
                Vector3 normal =
                    terrain.terrainData.GetInterpolatedNormal(
                        normalizedX,
                        normalizedZ
                    );


                Quaternion terrainRotation =
                    Quaternion.FromToRotation(
                        Vector3.up,
                        normal
                    );


                if (randomRotation)
                {
                    rock.transform.rotation =
                        terrainRotation *
                        Quaternion.Euler(
                            0f,
                            randomY,
                            0f
                        );
                }
                else
                {
                    rock.transform.rotation =
                        terrainRotation;
                }
            }
            else if (randomRotation)
            {
                rock.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        randomY,
                        0f
                    );
            }


            // -------------------------------------------------
            // RANDOM SIZE
            // -------------------------------------------------

            ApplyRandomScale(rock);


            // -------------------------------------------------
            // PARENT
            // -------------------------------------------------

            rock.transform.SetParent(
                rockContainer,
                true
            );

            // Test final geometry, not just the pivot. Parenting can also affect bounds
            // under rotated/non-uniformly scaled ancestors, so validate after it.
            if (!IsRockInsideGenerationArea(rock))
            {
                rejectedByAreaBounds++;
                if (Application.isPlaying)
                {
                    rock.SetActive(false);
                    Destroy(rock);
                }
                else DestroyImmediate(rock);
                continue;
            }


            // -------------------------------------------------
            // SAVE GENERATED POSITION
            // -------------------------------------------------

            generatedPositions.Add(
                position
            );


#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(rock, "Generate Decoration");
#endif
            placed++;
        }


        // =====================================================
        // RESULT
        // =====================================================

        lastGenerationReport = $"Placed: {placed}/{amount} | Attempts: {attempts}/{maxAttempts}\n" +
            $"Outside Terrain: {outsideTerrain}\nHeight limit: {heightRejected}\nSlope: {slopeRejected}\n" +
            $"Excluded Terrain layers: {layerRejected}\nMin Distance: {distanceRejected}\n" +
            $"Scene objects: {rejectedBySceneObjects}\nArea bounds / missing geometry: {rejectedByAreaBounds}\nInvalid prefab: {prefabRejected}";
        if (placed < amount)
        {
            int[] counts = { outsideTerrain, heightRejected, slopeRejected, layerRejected, distanceRejected, rejectedBySceneObjects, rejectedByAreaBounds };
            string[] tips = {
                "Move/resize the box so more of it overlaps the assigned Terrain.",
                "Adjust Min/Max Height; these use world-space Terrain height.",
                "Increase Max Slope or choose flatter ground.",
                "Review excluded Terrain layers and Exclude Threshold.",
                "Reduce Min Distance, enlarge the area, or request fewer decorations.",
                "Review Scene Object Layers, Check Radius and Padding; large renderer bounds can block empty-looking space.",
                "The whole object must fit inside the box, including Y. Increase area height, move its center, or reduce Decoration Size."
            };
            int largest = 0;
            for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[largest]) largest = i;
            lastGenerationReport += "\nMost frequent rejection: " + tips[largest] + "\nIncreasing Attempts Per Decoration helps only when valid space remains.";
        }
        Debug.Log(lastGenerationReport, this);
        if (placed < amount)
        {
            Debug.LogWarning(
                "ProceduralDecorationPlacer could not place all decorations. " +
                "The Generation Area may be too crowded, " +
                "Min Distance may be too large, or too many " +
                "Terrain/Scene filters are blocking placement. " +
                "If Area Bounds rejects decorations, expand/reposition the box (including Y) or reduce Decoration Size."
            );
        }
    }


    // =========================================================
    // FINAL ROCK CONTAINMENT
    // =========================================================

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    public bool IsRockInsideGenerationArea(GameObject rock)
    {
        if (rock == null || !IsFinite(areaCenter) || !IsFinite(areaSize) ||
            areaSize.x <= 0 || areaSize.y <= 0 || areaSize.z <= 0 ||
            Mathf.Abs(transform.localToWorldMatrix.determinant) < 0.000001f) return false;

        Matrix4x4 worldToArea = transform.worldToLocalMatrix;
        bool foundBounds = false;
        // Include all LODs and inactive visual variants so changing LOD cannot reveal
        // a larger rock outside the box. Transform local bounds directly: a world AABB
        // is unnecessarily large for rotated meshes and rotated placement boxes.
        foreach (Renderer renderer in rock.GetComponentsInChildren<Renderer>(true))
        {
            foundBounds = true;
            if (!ContainsBounds(renderer.localBounds, worldToArea * renderer.transform.localToWorldMatrix)) return false;
        }
        if (foundBounds) return true;

        // Collider-only prefabs: world bounds conservatively contain their collision shape.
        Physics.SyncTransforms();
        foreach (Collider collider in rock.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled) continue;
            foundBounds = true;
            if (!ContainsBounds(collider.bounds, worldToArea)) return false;
        }
        // A prefab without measurable geometry cannot be certified as fitting.
        return foundBounds;
    }

    private bool ContainsBounds(Bounds bounds, Matrix4x4 toArea)
    {
        if (!IsFinite(bounds.center) || !IsFinite(bounds.extents)) return false;
        Vector3 min = areaCenter - areaSize * 0.5f, max = areaCenter + areaSize * 0.5f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 point = toArea.MultiplyPoint3x4(corner);
            if (!IsFinite(point) || point.x < min.x || point.x > max.x ||
                point.y < min.y || point.y > max.y || point.z < min.z || point.z > max.z) return false;
        }
        return true;
    }

    // =========================================================
    // RANDOM SCALE
    // =========================================================

    private void ApplyRandomScale(
        GameObject rock)
    {
        if (!randomSize)
            return;


        Vector3 originalScale =
            rock.transform.localScale;


        if (uniformScale)
        {
            float scale =
                Random.Range(
                    minUniformScale,
                    maxUniformScale
                );


            rock.transform.localScale =
                originalScale * scale;
        }
        else
        {
            float minimumX =
                Mathf.Min(
                    minScale.x,
                    maxScale.x
                );

            float maximumX =
                Mathf.Max(
                    minScale.x,
                    maxScale.x
                );


            float minimumY =
                Mathf.Min(
                    minScale.y,
                    maxScale.y
                );

            float maximumY =
                Mathf.Max(
                    minScale.y,
                    maxScale.y
                );


            float minimumZ =
                Mathf.Min(
                    minScale.z,
                    maxScale.z
                );

            float maximumZ =
                Mathf.Max(
                    minScale.z,
                    maxScale.z
                );


            float x =
                Random.Range(
                    minimumX,
                    maximumX
                );

            float y =
                Random.Range(
                    minimumY,
                    maximumY
                );

            float z =
                Random.Range(
                    minimumZ,
                    maximumZ
                );


            rock.transform.localScale =
                new Vector3(
                    originalScale.x * x,
                    originalScale.y * y,
                    originalScale.z * z
                );
        }
    }


    // =========================================================
    // CACHE EXISTING SCENE OBJECTS
    // =========================================================

    private void CacheSceneObjects()
    {
        cachedSceneRendererBounds.Clear();

        if (!detectSceneObjects)
            return;

        if (sceneDetectionMode ==
            SceneDetectionMode.ColliderOnly)
        {
            return;
        }

        Renderer[] renderers =
            Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            if (!renderer.enabled)
                continue;

            GameObject obj =
                renderer.gameObject;

            if (!obj.activeInHierarchy)
                continue;

            // Layer Mask
            if (!IsLayerInSceneObjectMask(obj.layer))
                continue;

            // Ignore Generator itself
            if (renderer.transform == transform)
                continue;

            // Ignore Generated Rocks
            if (rockContainer != null)
            {
                if (renderer.transform == rockContainer)
                    continue;

                if (renderer.transform.IsChildOf(rockContainer))
                    continue;
            }

            // Ignore Terrain related renderers
            if (ignoreTerrain)
            {
                Terrain parentTerrain =
                    renderer.GetComponentInParent<Terrain>();

                if (parentTerrain != null)
                    continue;
            }

            cachedSceneRendererBounds.Add(
                renderer.bounds
            );
        }
    }


    // =========================================================
    // SCENE OBJECT CHECK
    // =========================================================

    private bool IsBlockedBySceneObject(
        Vector3 position)
    {
        if (!detectSceneObjects)
            return false;


        // =====================================================
        // COLLIDER DETECTION
        // =====================================================

        if (sceneDetectionMode ==
                SceneDetectionMode.ColliderOnly ||
            sceneDetectionMode ==
                SceneDetectionMode.ColliderAndRendererBounds)
        {
            float radius =
                sceneObjectCheckRadius +
                sceneObjectPadding;


            QueryTriggerInteraction triggerMode =
                ignoreTriggers
                    ? QueryTriggerInteraction.Ignore
                    : QueryTriggerInteraction.Collide;


            Collider[] hits =
                Physics.OverlapSphere(
                    position,
                    radius,
                    sceneObjectLayers,
                    triggerMode
                );


            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                Collider hit =
                    hits[i];


                if (hit == null)
                    continue;


                // -----------------------------------------
                // IGNORE GENERATOR
                // -----------------------------------------

                if (hit.transform ==
                    transform)
                {
                    continue;
                }


                // -----------------------------------------
                // IGNORE GENERATED ROCKS
                // -----------------------------------------

                if (rockContainer != null)
                {
                    if (hit.transform ==
                        rockContainer)
                    {
                        continue;
                    }


                    if (hit.transform.IsChildOf(
                            rockContainer))
                    {
                        continue;
                    }
                }


                // -----------------------------------------
                // IGNORE TERRAIN
                // -----------------------------------------

                if (ignoreTerrain)
                {
                    if (hit is TerrainCollider)
                    {
                        continue;
                    }


                    Terrain parentTerrain =
                        hit.GetComponentInParent<Terrain>();


                    if (parentTerrain != null)
                    {
                        continue;
                    }
                }


                // Something is occupying this position.
                return true;
            }
        }


        // =====================================================
        // RENDERER BOUNDS DETECTION
        // =====================================================

        if (sceneDetectionMode ==
                SceneDetectionMode.RendererBoundsOnly ||
            sceneDetectionMode ==
                SceneDetectionMode.ColliderAndRendererBounds)
        {
            float diameter =
                sceneObjectCheckRadius * 2f;


            Bounds candidateBounds =
                new Bounds(
                    position,
                    new Vector3(
                        diameter,
                        diameter,
                        diameter
                    )
                );


            for (int i = 0;
                 i < cachedSceneRendererBounds.Count;
                 i++)
            {
                Bounds existingBounds =
                    cachedSceneRendererBounds[i];


                // Bounds.Expand changes the FULL size.
                // Multiply by 2 so Padding represents
                // extra distance on each side.

                existingBounds.Expand(
                    sceneObjectPadding * 2f
                );


                if (candidateBounds.Intersects(
                        existingBounds))
                {
                    return true;
                }
            }
        }


        return false;
    }


    // =========================================================
    // LAYER MASK CHECK
    // =========================================================

    private bool IsLayerInSceneObjectMask(
        int layer)
    {
        return
            (sceneObjectLayers.value &
             (1 << layer)) != 0;
    }


    // =========================================================
    // TERRAIN LAYER FILTER
    // =========================================================

    private bool IsPositionAllowedByTerrainLayer(
        float normalizedX,
        float normalizedZ)
    {
        if (!useTerrainLayerFilter)
            return true;


        if (terrain == null ||
            terrain.terrainData == null)
        {
            return true;
        }


        TerrainData data =
            terrain.terrainData;


        TerrainLayer[] terrainLayers =
            data.terrainLayers;


        if (terrainLayers == null ||
            terrainLayers.Length == 0)
        {
            return true;
        }


        int alphaWidth =
            data.alphamapWidth;

        int alphaHeight =
            data.alphamapHeight;


        int x =
            Mathf.Clamp(
                Mathf.RoundToInt(
                    normalizedX *
                    (alphaWidth - 1)
                ),
                0,
                alphaWidth - 1
            );


        int z =
            Mathf.Clamp(
                Mathf.RoundToInt(
                    normalizedZ *
                    (alphaHeight - 1)
                ),
                0,
                alphaHeight - 1
            );


        float[,,] alpha =
            data.GetAlphamaps(
                x,
                z,
                1,
                1
            );


        for (int i = 0;
             i < terrainLayers.Length;
             i++)
        {
            TerrainLayer layer =
                terrainLayers[i];


            TerrainLayerRule rule =
                GetRuleForLayer(layer);


            if (rule == null)
                continue;


            if (!rule.excluded)
                continue;


            float weight =
                alpha[0, 0, i];


            if (weight >=
                excludeLayerThreshold)
            {
                return false;
            }
        }


        return true;
    }


    // =========================================================
    // FIND TERRAIN LAYER RULE
    // =========================================================

    private TerrainLayerRule GetRuleForLayer(
        TerrainLayer layer)
    {
        for (int i = 0;
             i < terrainLayerRules.Count;
             i++)
        {
            if (terrainLayerRules[i].terrainLayer ==
                layer)
            {
                return terrainLayerRules[i];
            }
        }


        return null;
    }


    // =========================================================
    // SYNC TERRAIN LAYERS
    // =========================================================

    public void SyncTerrainLayers()
    {
        if (terrain == null ||
            terrain.terrainData == null)
        {
            return;
        }


        TerrainLayer[] layers =
            terrain.terrainData.terrainLayers;


        if (layers == null)
            return;


        List<TerrainLayerRule> newRules =
            new List<TerrainLayerRule>();


        for (int i = 0;
             i < layers.Length;
             i++)
        {
            TerrainLayer layer =
                layers[i];


            TerrainLayerRule oldRule =
                GetRuleForLayer(layer);


            TerrainLayerRule newRule =
                new TerrainLayerRule();


            newRule.terrainLayer =
                layer;


            if (oldRule != null)
            {
                newRule.excluded =
                    oldRule.excluded;
            }
            else
            {
                newRule.excluded =
                    false;
            }


            newRules.Add(
                newRule
            );
        }


        terrainLayerRules =
            newRules;
    }


    // =========================================================
    // CLEAR ROCKS
    // =========================================================

    public void ClearRocks()
    {
        generatedPositions.Clear();


        if (rockContainer == null)
            return;


#if UNITY_EDITOR

        while (rockContainer.childCount > 0)
        {
            GameObject child =
                rockContainer
                .GetChild(0)
                .gameObject;


            Undo.DestroyObjectImmediate(
                child
            );
        }

#else

        for (int i =
                rockContainer.childCount - 1;
             i >= 0;
             i--)
        {
            DestroyImmediate(
                rockContainer
                .GetChild(i)
                .gameObject
            );
        }

#endif
    }


    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmos()
    {
        if (!showGizmo)
            return;


        Matrix4x4 oldMatrix =
            Gizmos.matrix;


        Gizmos.matrix = transform.localToWorldMatrix;


        Color fillColor =
            gizmoColor;


        fillColor.a =
            gizmoAlpha;


        Gizmos.color =
            fillColor;


        Gizmos.DrawCube(
            areaCenter,
            areaSize
        );


        Gizmos.color =
            gizmoColor;


        Gizmos.DrawWireCube(
            areaCenter,
            areaSize
        );


        Gizmos.matrix =
            oldMatrix;
    }
}
