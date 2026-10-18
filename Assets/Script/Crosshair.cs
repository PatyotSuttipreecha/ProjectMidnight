using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class Crosshair : MonoBehaviour
{
    public RectTransform top;
    public RectTransform bottom;
    public RectTransform left;
    public RectTransform right;

    [Tooltip("Legacy normalized-spread animation only. Guns uses immediate projected spread so UI does not lag real accuracy.")]
    public float expandSpeed = 5f;    // ความเร็วในการขยับ
    [Tooltip("Legacy normalized-spread limit only. Guns calculates its gap from actual scatter, camera FOV and target distance.")]
    public float maxDistance = 300f;   // ระยะที่กางออกสูงสุด
    public float currentSpread;       // จะรับค่าจาก Guns.cs
    private Vector2 screenOffset;
    private Canvas parentCanvas;
    private RectTransform crosshairRoot;
    private Vector2 restingPosition;
    private bool hasProjectedSpread;
    private Rect projectedBounds;
    private Vector2 projectedCenter;

    public void SetProjectedSpread(Vector2 center, Rect bounds)
    {
        hasProjectedSpread = true;
        projectedCenter = center;
        projectedBounds = bounds;
    }

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        crosshairRoot = transform as RectTransform;
        if (crosshairRoot != null) restingPosition = crosshairRoot.anchoredPosition;
    }

    private void LateUpdate()
    {
        if (hasProjectedSpread && crosshairRoot != null)
        {
            Camera uiCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? parentCanvas.worldCamera : null;
            var parent = crosshairRoot.parent as RectTransform;
            if (parent != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, projectedCenter, uiCamera, out Vector3 world))
                crosshairRoot.position = world;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(crosshairRoot, projectedBounds.min, uiCamera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(crosshairRoot, projectedBounds.max, uiCamera, out Vector2 max);
            // Inner edges mark the real envelope. No extra UI-only lag or max-gap clamp.
            PositionArm(left, new Vector2(min.x - (left != null ? left.rect.width * (1 - left.pivot.x) : 0), 0));
            PositionArm(right, new Vector2(max.x + (right != null ? right.rect.width * right.pivot.x : 0), 0));
            PositionArm(top, new Vector2(0, max.y + (top != null ? top.rect.height * top.pivot.y : 0)));
            PositionArm(bottom, new Vector2(0, min.y - (bottom != null ? bottom.rect.height * (1 - bottom.pivot.y) : 0)));
            return;
        }
        UpdateSwayPosition();
        float distance = Mathf.Lerp(0, maxDistance, currentSpread);
        displayedDistance = Mathf.Lerp(displayedDistance, distance, 1f - Mathf.Exp(-expandSpeed * Time.deltaTime));
        PositionArm(top, new Vector2(0f, displayedDistance));
        PositionArm(bottom, new Vector2(0f, -displayedDistance));
        PositionArm(left, new Vector2(-displayedDistance, 0f));
        PositionArm(right, new Vector2(displayedDistance, 0f));
    }

    private float displayedDistance;

    private void PositionArm(RectTransform arm, Vector2 spreadOffset)
    {
        if (arm == null) return;
        arm.anchoredPosition = spreadOffset;
    }

    private void UpdateSwayPosition()
    {
        if (crosshairRoot == null) return;
        Vector2 localOffset = Vector2.zero;
        RectTransform parent = crosshairRoot.parent as RectTransform;
        Camera uiCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera : null;
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (parent != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenCenter, uiCamera, out Vector2 center) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenCenter + screenOffset, uiCamera, out Vector2 swayed))
            localOffset = swayed - center;
        // Sway stays in sync with the aim ray; only the spread gap is smoothed.
        crosshairRoot.anchoredPosition = restingPosition + localOffset;
    }

    private void OnDisable()
    {
        hasProjectedSpread = false;
        screenOffset = Vector2.zero;
        if (crosshairRoot != null) crosshairRoot.anchoredPosition = restingPosition;
    }

    public void SetScreenOffset(Vector2 offset)
    {
        screenOffset = offset;
    }

    public void SetSpread(float spread)
    {
        hasProjectedSpread = false;
        currentSpread = Mathf.Clamp01(spread); // 0 → 1
    }
}
