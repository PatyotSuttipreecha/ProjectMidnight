using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Documents/Document", fileName = "NewDocument")]
public class DocumentSO : ScriptableObject
{
    [SerializeField] private string documentId;
    public string Id => documentId;
    public string title;
    public string author;
    [TextArea(2, 4)] public string summary;
    public Sprite icon;
    public GameObject inspectionPrefab;
    public Vector3 inspectionRotation = new Vector3(0, 25, 0);
    [TextArea(5, 20)] public string[] pages = new string[1];
    private void OnEnable() => EnsureId();
    private void OnValidate() => EnsureId();
    private void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(documentId)) documentId = Guid.NewGuid().ToString("N");
    }
}
