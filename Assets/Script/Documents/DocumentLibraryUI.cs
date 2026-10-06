using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class DocumentLibraryUI : MonoBehaviour
{
    public Button openButton;
    public DocumentLibraryView viewPrefab;
    private DocumentLibraryView view;
    private DocumentCollection collection;
    private List<DocumentSO> documents = new List<DocumentSO>();
    private int documentIndex, pageIndex;
    private bool reading;
    private DocumentSO Current => documents.Count > 0 ? documents[documentIndex] : null;
    private void OnEnable()
    {
        if (openButton == null) openButton = GetComponent<Button>();
        openButton.onClick.AddListener(Open);
    }
    private void OnDisable()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        Close();
    }
    private void Update()
    {
        if (view != null && Input.GetKeyDown(KeyCode.Escape))
        { if (reading) ShowModel(); else Close(); }
    }
    public void Open()
    {
        if (view != null || viewPrefab == null) return;
        collection = DocumentCollection.Instance;
        var canvas = GetComponentInParent<Canvas>();
        if (collection == null || canvas == null) return;
        GetComponentInParent<InventoryUI>()?.CloseItemMenu();
        documents = collection.GetCollected(); documentIndex = pageIndex = 0;
        view = Instantiate(viewPrefab, canvas.rootCanvas.transform, false);
        view.gameObject.SetActive(true); view.transform.SetAsLastSibling();
        Bind(view.close, Close); Bind(view.read, ShowReading); Bind(view.back, ShowModel);
        Bind(view.previousDocument, () => SelectDocument(-1)); Bind(view.nextDocument, () => SelectDocument(1));
        Bind(view.previousPage, () => SelectPage(-1)); Bind(view.nextPage, () => SelectPage(1));
        Bind(view.reset, () => view.preview.ResetView());
        ShowModel();
    }
    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }
    private void SelectDocument(int step)
    {
        if (documents.Count == 0) return;
        documentIndex = Mathf.Clamp(documentIndex + step, 0, documents.Count - 1);
        pageIndex = 0; ShowModel();
    }
    private void ShowModel()
    {
        if (view == null) return;
        reading = false; view.readingGroup.SetActive(false); view.modelGroup.SetActive(true);
        view.title.text = Current != null ? Current.title : "Documents";
        view.summary.text = Current != null ? Current.summary : "Collect documents in the world to discover their stories.";
        view.documentCounter.text = documents.Count > 0 ? $"{documentIndex + 1} / {documents.Count}" : "0 / 0";
        view.previousDocument.interactable = documentIndex > 0;
        view.nextDocument.interactable = documentIndex + 1 < documents.Count;
        view.read.interactable = Current != null && Current.pages != null && Current.pages.Length > 0;
        view.preview.Dispose(); Canvas.ForceUpdateCanvases();
        bool model = Current != null && view.preview.Initialize(Current.inspectionPrefab, Current.inspectionRotation);
        view.modelView.enabled = model; view.reset.interactable = model;
        view.fallbackIcon.sprite = Current != null ? Current.icon : null;
        view.fallbackIcon.gameObject.SetActive(!model && view.fallbackIcon.sprite != null);
        view.emptyHint.gameObject.SetActive(!model);
        view.emptyHint.text = Current == null ? "No documents collected yet." : "No 3D model assigned. You can still read this document.";
        UpdateStatus();
    }
    private void ShowReading()
    {
        if (Current == null || Current.pages == null || Current.pages.Length == 0) return;
        reading = true; view.preview.Dispose(); view.modelGroup.SetActive(false); view.readingGroup.SetActive(true);
        collection.MarkRead(Current); UpdateStatus(); SelectPage(0);
    }
    private void UpdateStatus()
    {
        view.status.text = Current == null ? "" : (collection.HasRead(Current) ? "Read" : "Unread") +
            (string.IsNullOrWhiteSpace(Current.author) ? "" : "  |  " + Current.author);
    }
    private void SelectPage(int step)
    {
        pageIndex = Mathf.Clamp(pageIndex + step, 0, Current.pages.Length - 1);
        view.pageText.text = Current.pages[pageIndex] ?? "";
        view.pageCounter.text = $"Page {pageIndex + 1} / {Current.pages.Length}";
        view.previousPage.interactable = pageIndex > 0;
        view.nextPage.interactable = pageIndex + 1 < Current.pages.Length;
        // ScrollRect belongs to the editable prefab; every page starts at its top.
        var scroll = view.pageText.GetComponentInParent<ScrollRect>();
        if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1; }
    }
    public void Close()
    {
        if (view == null) return;
        view.preview.Dispose(); view.gameObject.SetActive(false); Destroy(view.gameObject); view = null;
    }
}
