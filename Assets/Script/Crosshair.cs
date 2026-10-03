using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    public RectTransform top;
    public RectTransform bottom;
    public RectTransform left;
    public RectTransform right;

    public float expandSpeed = 5f;    // ความเร็วในการขยับ
    public float maxDistance = 300f;   // ระยะที่กางออกสูงสุด
    public float currentSpread;       // จะรับค่าจาก Guns.cs
    private Vector2 screenOffset;
    private Canvas parentCanvas;
    private RectTransform crosshairRoot;
    private Vector2 restingPosition;

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        crosshairRoot = transform as RectTransform;
        if (crosshairRoot != null) restingPosition = crosshairRoot.anchoredPosition;
    }

    private void LateUpdate()
    {
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
        screenOffset = Vector2.zero;
        if (crosshairRoot != null) crosshairRoot.anchoredPosition = restingPosition;
    }

    public void SetScreenOffset(Vector2 offset)
    {
        screenOffset = offset;
    }

    public void SetSpread(float spread)
    {
        currentSpread = Mathf.Clamp01(spread); // 0 → 1
    }
}
