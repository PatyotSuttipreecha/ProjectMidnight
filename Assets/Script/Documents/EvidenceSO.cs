using UnityEngine;

// Shares collection, IDs and preview data with documents; no second inventory or modal system.
[CreateAssetMenu(menuName = "Documents/Evidence", fileName = "NewEvidence")]
public class EvidenceSO : DocumentSO
{
    public override bool IsEvidence => true;
    [Tooltip("Optional image per details page. Index 0 matches Pages[0]. Images may also exist without text.")]
    public Sprite[] detailImages = new Sprite[0];
}
