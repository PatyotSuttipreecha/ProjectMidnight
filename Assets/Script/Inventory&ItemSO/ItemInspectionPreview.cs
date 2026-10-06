using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Renders mesh-only copies. The original item and its gameplay components are never activated.
public class ItemInspectionPreview : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private GameObject stage;
    private Transform pivot;
    private Camera previewCamera;
    private RenderTexture texture;
    private readonly List<Mesh> bakedMeshes = new List<Mesh>();
    private Quaternion initialRotation;
    private float distance = 4.5f;
    private const int PreviewLayer = 31;
    public float rotateSpeed = .35f;
    public float zoomSpeed = .3f;

    public bool Initialize(ItemSO item)
    {
        GameObject source = item.inspectionPrefab != null ? item.inspectionPrefab : item.weaponVisualPrefab;
        if (source == null && item.pickupPrefab != null) source = item.pickupPrefab.gameObject;
        return Initialize(source, item.inspectionRotation);
    }
    public bool Initialize(GameObject source, Vector3 rotation)
    {
        Dispose();
        distance = 4.5f;
        if (source == null) return false;
        stage = new GameObject("Item Inspection Stage");
        stage.layer = PreviewLayer;
        // Build and normalize at the origin before moving away from gameplay cameras.
        pivot = new GameObject("Rotation Pivot").transform; pivot.SetParent(stage.transform, false);
        var model = new GameObject("Visual Only").transform; model.SetParent(pivot, false);
        CopyMeshes(source.transform, model);
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Dispose(); return false; }
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float radius = Mathf.Max(.001f, bounds.extents.magnitude);
        model.localScale = Vector3.one / radius;
        model.localPosition = -bounds.center / radius;
        initialRotation = Quaternion.Euler(rotation);
        pivot.localRotation = initialRotation;
        stage.transform.position = new Vector3(100000, 100000, 100000);
        var cameraObject = new GameObject("Inspection Camera", typeof(Camera));
        cameraObject.layer = PreviewLayer; cameraObject.transform.SetParent(stage.transform, false);
        previewCamera = cameraObject.GetComponent<Camera>();
        // Render explicitly: this camera supplies a UI texture, independently of the gameplay camera stack.
        previewCamera.enabled = false;
        previewCamera.useOcclusionCulling = false;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = Color.clear;
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.fieldOfView = 35; previewCamera.nearClipPlane = .05f; previewCamera.farClipPlane = 20;
        previewCamera.allowHDR = false; previewCamera.allowMSAA = false;
        var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false; data.renderShadows = false;
        data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
        var rect = (RectTransform)transform;
        float aspect = Mathf.Clamp(rect.rect.width / Mathf.Max(1, rect.rect.height), .5f, 3f);
        texture = new RenderTexture(Mathf.Clamp(Mathf.RoundToInt(900 * aspect), 450, 2048), 900, 24, RenderTextureFormat.ARGB32);
        texture.name = "Item Inspection Preview"; texture.Create();
        previewCamera.targetTexture = texture;
        var image = GetComponent<RawImage>();
        // Repair old prefab/scene overrides whose Rect serialized without a version and loaded as 0×0.
        if (image.uvRect.width <= 0 || image.uvRect.height <= 0)
            image.uvRect = new Rect(0, 0, 1, 1);
        image.texture = texture;
        AddLight(new Vector3(-2, 3, -3), 18, new Color(1f, .93f, .82f));
        AddLight(new Vector3(3, 1, -2), 10, new Color(.75f, .85f, 1f));
        AddLight(new Vector3(0, 2, 3), 12, Color.white);
        SetDistance();
        RenderPreview();
        return true;
    }
    private void LateUpdate() => RenderPreview();
    private void RenderPreview()
    {
        if (previewCamera == null || texture == null || !texture.IsCreated()) return;
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            RenderPipeline.SubmitRenderRequest(previewCamera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
        else
            previewCamera.Render();
    }
    private void CopyMeshes(Transform source, Transform target)
    {
        target.gameObject.layer = PreviewLayer;
        Mesh mesh = null; Material[] materials = null;
        if (source.TryGetComponent<MeshFilter>(out var filter) && source.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled)
        { mesh = filter.sharedMesh; materials = renderer.sharedMaterials; }
        else if (source.TryGetComponent<SkinnedMeshRenderer>(out var skin) && skin.enabled && skin.sharedMesh != null)
        { mesh = new Mesh(); skin.BakeMesh(mesh); bakedMeshes.Add(mesh); materials = skin.sharedMaterials; }
        if (mesh != null)
        {
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var copy = target.gameObject.AddComponent<MeshRenderer>(); copy.sharedMaterials = materials;
            copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach (Transform child in source)
        {
            if (!child.gameObject.activeSelf || child.GetComponent<ParticleSystem>() != null) continue;
            var copy = new GameObject(child.name).transform; copy.SetParent(target, false);
            copy.localPosition = child.localPosition; copy.localRotation = child.localRotation; copy.localScale = child.localScale;
            CopyMeshes(child, copy);
        }
    }
    private void AddLight(Vector3 position, float intensity, Color color)
    {
        var obj = new GameObject("Inspection Light", typeof(Light));
        obj.layer = PreviewLayer; obj.transform.SetParent(stage.transform, false); obj.transform.localPosition = position;
        var light = obj.GetComponent<Light>(); light.type = LightType.Point; light.range = 14;
        light.intensity = intensity; light.color = color; light.cullingMask = 1 << PreviewLayer;
        light.shadows = LightShadows.None;
    }
    private void SetDistance()
    { if (previewCamera != null) previewCamera.transform.localPosition = new Vector3(0, 0, -distance); }
    public void OnPointerDown(PointerEventData eventData) { eventData.eligibleForClick = false; }
    // Consume click events here so releasing over the model cannot target the overlay's close button.
    public void OnPointerClick(PointerEventData eventData) { }
    public void OnBeginDrag(PointerEventData eventData) { eventData.eligibleForClick = false; }
    public void OnEndDrag(PointerEventData eventData) { eventData.eligibleForClick = false; }
    public void OnDrag(PointerEventData eventData)
    {
        if (pivot == null || eventData.button != PointerEventData.InputButton.Left) return;
        pivot.Rotate(Vector3.up, -eventData.delta.x * rotateSpeed, Space.World);
        pivot.Rotate(Vector3.right, eventData.delta.y * rotateSpeed, Space.World);
    }
    public void OnScroll(PointerEventData eventData)
    { distance = Mathf.Clamp(distance - eventData.scrollDelta.y * zoomSpeed, 1.8f, 8f); SetDistance(); }
    public void ResetView()
    { if (pivot != null) pivot.localRotation = initialRotation; distance = 4.5f; SetDistance(); }
    public void Dispose()
    {
        var image = GetComponent<RawImage>();
        if (image != null) image.texture = null;
        if (previewCamera != null) { previewCamera.enabled = false; previewCamera.targetTexture = null; }
        if (stage != null) { stage.SetActive(false); Destroy(stage); }
        if (texture != null) { texture.Release(); Destroy(texture); }
        foreach (var mesh in bakedMeshes) if (mesh != null) Destroy(mesh);
        bakedMeshes.Clear(); stage = null; texture = null; previewCamera = null; pivot = null;
    }
    private void OnDestroy() => Dispose();
}
