using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DocumentLibraryView : MonoBehaviour
{
    public TMP_Text title, summary, status, documentCounter, pageCounter, pageText, emptyHint;
    public Button previousDocument, nextDocument, read, back, previousPage, nextPage, reset, close;
    public GameObject modelGroup, readingGroup;
    public ItemInspectionPreview preview;
    public RawImage modelView;
    public Image fallbackIcon;
}
