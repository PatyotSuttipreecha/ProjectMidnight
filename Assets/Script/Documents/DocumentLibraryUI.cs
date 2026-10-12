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
    private DocumentSO pendingDocument;
    private bool pendingRead;
    private PlayerController modalPlayer;
    private System.Action pickupComplete;
    private bool temporaryHost;
    private bool evidenceCategory;
    private readonly List<Button> catalogRows = new List<Button>();
    private RectTransform pageScrollRect;
    private Vector2 originalScrollMax;
    private RectLayout scrollLayout;
    private DocumentSO previewedRecord;
    private RectLayout modelLayout, readingLayout;
    private struct RectLayout
    {
        public Vector2 min, max, offsetMin, offsetMax;
        public RectLayout(RectTransform rect) { min = rect.anchorMin; max = rect.anchorMax; offsetMin = rect.offsetMin; offsetMax = rect.offsetMax; }
        public void Restore(RectTransform rect) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
    }
    private int PageCount => Current == null ? 0 : Mathf.Max(Current.pages != null ? Current.pages.Length : 0,
        Current is EvidenceSO evidence && evidence.detailImages != null ? evidence.detailImages.Length : 0);
    private DocumentSO Current => documents.Count > 0 ? documents[documentIndex] : null;
    private void OnEnable()
    {
        if (openButton == null) openButton = GetComponent<Button>();
        openButton.onClick.AddListener(Open);
    }
    private void OnDisable()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        CloseInternal(false);
    }
    private void Update()
    {
        if (view != null && Input.GetKeyDown(KeyCode.Escape))
        {
            if (view.catalogSelector != null && view.catalogSelector.IsExpanded) { view.catalogSelector.Hide(); return; }
            if (reading) ShowModel(); else Close();
        }
    }
    public void Open()
    {
        if (view != null || viewPrefab == null) return;
        collection = DocumentCollection.Instance;
        var canvas = GetComponentInParent<Canvas>();
        if (collection == null || canvas == null) return;
        GetComponentInParent<InventoryUI>()?.CloseItemMenu();
        documents = collection.GetCollected(); documentIndex = pageIndex = 0;
        documents.RemoveAll(entry => entry.IsEvidence != evidenceCategory);
        CreateView(canvas.rootCanvas);
    }
    public static bool OpenPickup(DocumentSO document, PlayerController player, DocumentLibraryView prefab, System.Action completed)
    {
        if (document == null || player == null || player.IsGameplayInputBlocked || DocumentCollection.Instance == null) return false;
        Canvas canvas = null;
        foreach (var library in FindObjectsByType<DocumentLibraryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!library.gameObject.scene.IsValid()) continue;
            var parent = library.GetComponentInParent<Canvas>();
            if (parent == null || !parent.rootCanvas.gameObject.activeInHierarchy) continue;
            if (prefab == null) prefab = library.viewPrefab;
            if (prefab != null) { canvas = parent.rootCanvas; break; }
        }
        if (canvas == null && prefab != null)
            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (candidate.isRootCanvas) { canvas = candidate; break; }
        if (prefab == null || canvas == null) return false;
        // A separate active host is needed because the Inventory Documents button is inactive during gameplay.
        var host = new GameObject("Document Pickup Preview", typeof(RectTransform), typeof(Button));
        host.transform.SetParent(canvas.transform, false);
        var ui = host.AddComponent<DocumentLibraryUI>();
        ui.viewPrefab = prefab; ui.collection = DocumentCollection.Instance;
        ui.documents = new List<DocumentSO> { document }; ui.documentIndex = ui.pageIndex = 0;
        ui.pendingDocument = document; ui.modalPlayer = player; ui.pickupComplete = completed; ui.temporaryHost = true;
        player.BeginDocumentModal();
        ui.CreateView(canvas);
        return true;
    }
    private void CreateView(Canvas canvas)
    {
        view = Instantiate(viewPrefab, canvas.rootCanvas.transform, false);
        view.gameObject.SetActive(true); view.transform.SetAsLastSibling();
        Bind(view.close, Close); Bind(view.read, ShowReading); Bind(view.back, ShowModel);
        if (view.previousDocument != null) view.previousDocument.gameObject.SetActive(false);
        if (view.nextDocument != null) view.nextDocument.gameObject.SetActive(false);
        if (view.documentCounter != null) view.documentCounter.gameObject.SetActive(false);
        Bind(view.previousPage, () => SelectPage(-1)); Bind(view.nextPage, () => SelectPage(1));
        Bind(view.reset, () => view.preview.ResetView());
        if (view.documentsTab != null) Bind(view.documentsTab, () => SelectCategory(false));
        if (view.evidenceTab != null) Bind(view.evidenceTab, () => SelectCategory(true));
        if (view.documentsTab != null) view.documentsTab.gameObject.SetActive(pendingDocument == null);
        if (view.evidenceTab != null) view.evidenceTab.gameObject.SetActive(pendingDocument == null);
        var pageScroll = view.pageText.GetComponentInParent<ScrollRect>(true);
        if (pageScroll != null) { pageScrollRect = (RectTransform)pageScroll.transform; originalScrollMax = pageScrollRect.anchorMax; scrollLayout = new RectLayout(pageScrollRect); }
        modelLayout = new RectLayout((RectTransform)view.modelGroup.transform);
        readingLayout = new RectLayout((RectTransform)view.readingGroup.transform);
        RefreshCatalogList();
        ShowModel();
    }
    private void SelectCategory(bool evidence)
    {
        if (pendingDocument != null || collection == null) return;
        evidenceCategory = evidence;
        documents = collection.GetCollected();
        documents.RemoveAll(entry => entry.IsEvidence != evidence);
        documentIndex = pageIndex = 0;
        RefreshCatalogList();
        ShowModel();
    }
    private void RefreshCatalogList()
    {
        foreach (var row in catalogRows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        catalogRows.Clear();
        if (view.catalogSelector != null) view.catalogSelector.gameObject.SetActive(false);
        if (view.catalogList == null || view.catalogRowTemplate == null) return;
        view.catalogRowTemplate.gameObject.SetActive(false);
        for (int i = 0; i < documents.Count; i++)
        {
            int index = i;
            var row = Instantiate(view.catalogRowTemplate, view.catalogList.content, false);
            row.name = "Catalog Entry " + i;
            row.GetComponentInChildren<TMPro.TMP_Text>(true).text = string.IsNullOrWhiteSpace(documents[i].title) ? documents[i].name : documents[i].title;
            Bind(row, () => { documentIndex = index; pageIndex = 0; ShowModel(); });
            row.gameObject.SetActive(true); catalogRows.Add(row);
        }
        Canvas.ForceUpdateCanvases(); view.catalogList.verticalNormalizedPosition = 1;
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
        if (view.catalogSelector != null) view.catalogSelector.gameObject.SetActive(false);
        bool keepPreview = reading && Current != null && Current.IsEvidence && Current == previewedRecord && view.modelView.texture != null;
        reading = false; view.readingGroup.SetActive(false); view.modelGroup.SetActive(true);
        modelLayout.Restore((RectTransform)view.modelGroup.transform);
        readingLayout.Restore((RectTransform)view.readingGroup.transform);
        view.read.gameObject.SetActive(true);
        view.title.text = Current != null ? Current.title : evidenceCategory ? "Evidence" : "Documents";
        view.summary.text = Current != null ? Current.summary : "Collect records in the world to view them here.";
        if (view.documentsTab != null) view.documentsTab.interactable = evidenceCategory;
        if (view.evidenceTab != null) view.evidenceTab.interactable = !evidenceCategory;
        var readLabel = view.read.GetComponentInChildren<TMPro.TMP_Text>();
        if (readLabel != null) readLabel.text = Current != null && Current.IsEvidence ? "View Details" : "Read";
        view.read.interactable = Current != null && (Current.IsEvidence || PageCount > 0);
        if (!keepPreview) view.preview.Dispose();
        Canvas.ForceUpdateCanvases();
        bool model = keepPreview || Current != null && view.preview.Initialize(Current.inspectionPrefab, Current.inspectionRotation);
        if (keepPreview) view.preview.RefreshSize();
        previewedRecord = Current;
        view.modelView.enabled = model; view.reset.interactable = model;
        view.fallbackIcon.sprite = Current != null ? Current.icon : null;
        view.fallbackIcon.gameObject.SetActive(!model && view.fallbackIcon.sprite != null);
        view.emptyHint.gameObject.SetActive(!model);
        view.emptyHint.text = Current == null ? "Nothing collected in this category yet." : "No 3D model assigned. You can still view the details.";
        bool catalog = pendingDocument == null;
        view.summary.gameObject.SetActive(!catalog);
        if (view.catalogList != null) view.catalogList.gameObject.SetActive(catalog);
        for (int i = 0; i < catalogRows.Count; i++) catalogRows[i].interactable = i != documentIndex;
        if (catalog)
        {
            SetPanel((RectTransform)view.modelGroup.transform, new Vector2(.04f, .2f), new Vector2(.49f, .84f));
            Canvas.ForceUpdateCanvases(); view.preview.RefreshSize();
        }
        SetPageControls(false); view.back.gameObject.SetActive(false);
        UpdateStatus();
    }
    private void ShowReading()
    {
        if (Current == null || !Current.IsEvidence && PageCount == 0) return;
        reading = true;
        bool evidence = Current.IsEvidence;
        if (!evidence) view.preview.Dispose();
        view.modelGroup.SetActive(evidence); view.readingGroup.SetActive(true);
        view.read.gameObject.SetActive(!evidence);
        view.back.gameObject.SetActive(true);
        if (view.catalogList != null) view.catalogList.gameObject.SetActive(false);
        SetPageControls(PageCount > 1);
        if (evidence)
        {
            ApplySplitLayout();
        }
        else readingLayout.Restore((RectTransform)view.readingGroup.transform);
        if (!evidence && view.catalogSelector != null) view.catalogSelector.gameObject.SetActive(false);
        if (pendingDocument != null) pendingRead = true;
        else collection.MarkRead(Current);
        UpdateStatus();
        if (PageCount > 0) SelectPage(0);
        else view.pageText.text = string.IsNullOrWhiteSpace(Current.summary) ? "No details provided for this evidence." : Current.summary;
    }
    private void ApplySplitLayout()
    {
        view.modelGroup.SetActive(true); view.readingGroup.SetActive(true);
        SetPanel((RectTransform)view.modelGroup.transform, new Vector2(.04f, .2f), new Vector2(.49f, .84f));
        SetPanel((RectTransform)view.readingGroup.transform, new Vector2(.53f, .2f), new Vector2(.96f, .84f));
        if (pageScrollRect != null) scrollLayout.Restore(pageScrollRect);
        Canvas.ForceUpdateCanvases(); view.preview.RefreshSize();
    }
    private void SetPageControls(bool visible)
    {
        view.previousPage.gameObject.SetActive(visible); view.nextPage.gameObject.SetActive(visible);
        view.pageCounter.gameObject.SetActive(visible);
    }
    private static void SetPanel(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private void UpdateStatus()
    {
        bool evidence = Current != null && Current.IsEvidence;
        string viewed = evidence ? "Reviewed" : "Read";
        view.status.text = Current == null ? "" : (pendingDocument != null ? (pendingRead ? viewed + " | Close to collect" : "Close to collect") : collection.HasRead(Current) ? viewed : evidence ? "Not reviewed" : "Unread") +
            (string.IsNullOrWhiteSpace(Current.author) ? "" : "  |  " + Current.author);
    }
    private void SelectPage(int step)
    {
        if (PageCount == 0) return;
        pageIndex = Mathf.Clamp(pageIndex + step, 0, PageCount - 1);
        view.pageText.text = Current.pages != null && pageIndex < Current.pages.Length ? Current.pages[pageIndex] ?? "" : "";
        if (Current.IsEvidence && pageIndex == 0 && string.IsNullOrWhiteSpace(view.pageText.text))
            view.pageText.text = string.IsNullOrWhiteSpace(Current.summary) ? "No details provided for this evidence." : Current.summary;
        Sprite detail = Current is EvidenceSO evidence && evidence.detailImages != null && pageIndex < evidence.detailImages.Length ? evidence.detailImages[pageIndex] : null;
        if (view.detailImage != null)
        {
            view.detailImage.sprite = detail;
            view.detailImage.gameObject.SetActive(detail != null);
            if (pageScrollRect != null) scrollLayout.Restore(pageScrollRect);
            if (Current.IsEvidence)
            {
                // Images supplement the text in the right panel; the left panel stays a live 3D preview.
                var imageRect = view.detailImage.rectTransform;
                SetPanel(imageRect, new Vector2(.05f, .58f), new Vector2(.95f, .94f));
                if (pageScrollRect != null)
                {
                    pageScrollRect.anchorMin = new Vector2(.05f, .17f);
                    pageScrollRect.anchorMax = new Vector2(.95f, detail != null ? .55f : originalScrollMax.y);
                }
            }
        }
        view.pageCounter.text = $"Page {pageIndex + 1} / {PageCount}";
        view.previousPage.interactable = pageIndex > 0;
        view.nextPage.interactable = pageIndex + 1 < PageCount;
        // ScrollRect belongs to the editable prefab; every page starts at its top.
        var scroll = view.pageText.GetComponentInParent<ScrollRect>();
        if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1; }
    }
    public void Close()
    {
        CloseInternal(true);
    }
    private void CloseInternal(bool collect)
    {
        if (view != null)
        {
            view.preview.Dispose(); view.gameObject.SetActive(false); Destroy(view.gameObject); view = null;
        }
        var document = pendingDocument; pendingDocument = null;
        var callback = pickupComplete; pickupComplete = null;
        // Release the preview lock before completion events can start a cutscene.
        if (modalPlayer != null) { modalPlayer.EndDocumentModal(); modalPlayer = null; }
        if (collect && document != null && collection != null)
        {
            collection.Collect(document);
            if (pendingRead) collection.MarkRead(document);
            if (collection.Contains(document)) callback?.Invoke();
        }
        if (temporaryHost) { temporaryHost = false; Destroy(gameObject); }
    }
}
