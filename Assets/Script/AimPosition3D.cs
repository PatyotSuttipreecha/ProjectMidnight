using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class AimPosition3D : MonoBehaviour
{
    [SerializeField] private Camera mainCam;
    [SerializeField] private LayerMask layerMask;
    [SerializeField, Min(0.01f)] private float maxDistance = 1000f;

    public Vector3 TargetPoint { get; private set; }
    public Vector2 ViewportOffset { get; set; }
    public Camera AimCamera => mainCam != null ? mainCam : Camera.main;
    public Vector3 AimViewportPoint => new Vector3(0.5f + ViewportOffset.x, 0.5f + ViewportOffset.y, 0f);

    private void Awake()
    {
        // Aimpoint is a visual/rig target, not an object rays or bullets should hit.
        foreach (Collider markerCollider in GetComponentsInChildren<Collider>(true))
            markerCollider.enabled = false;

        if (mainCam == null)
            mainCam = Camera.main;
        TargetPoint = transform.position;
    }

    private void Update()
    {
        RefreshTarget();
    }

    public void RefreshTarget()
    {
        if (mainCam == null)
            mainCam = Camera.main;
        if (mainCam == null) return;

        Ray ray = mainCam.ViewportPointToRay(AimViewportPoint);
        TargetPoint = Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask,
            QueryTriggerInteraction.Ignore) ? hit.point : ray.GetPoint(maxDistance);
        transform.position = TargetPoint;
    }
}
