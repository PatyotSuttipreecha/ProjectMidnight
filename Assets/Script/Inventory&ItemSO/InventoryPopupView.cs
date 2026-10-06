using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Serialized bindings let artists edit the layout in Prefab Mode.
public class InventoryPopupView : MonoBehaviour
{
    public RectTransform panel;
    public TMP_Text title, details, description, useLabel, dropLabel;
    public Button backdrop, use, examine, drop, close, reset;
    public RawImage modelView;
    public Image fallbackIcon;
    public TMP_Text fallbackHint;
    public ItemInspectionPreview preview;
}
