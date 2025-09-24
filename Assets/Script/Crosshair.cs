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

    private void Update()
    {
        float distance = Mathf.Lerp(0, maxDistance, currentSpread);

        top.anchoredPosition = Vector2.Lerp(top.anchoredPosition, new Vector2(0, distance), Time.deltaTime * expandSpeed);
        bottom.anchoredPosition = Vector2.Lerp(bottom.anchoredPosition, new Vector2(0, -distance), Time.deltaTime * expandSpeed);
        left.anchoredPosition = Vector2.Lerp(left.anchoredPosition, new Vector2(-distance, 0), Time.deltaTime * expandSpeed);
        right.anchoredPosition = Vector2.Lerp(right.anchoredPosition, new Vector2(distance, 0), Time.deltaTime * expandSpeed);
    }

    public void SetSpread(float spread)
    {
        currentSpread = Mathf.Clamp01(spread); // 0 → 1
    }
}
