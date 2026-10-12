using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CatalogUIUpgrade
{
    private static string Path => AssetDatabase.GUIDToAssetPath("bcc72ae0dcae4bf8ba014736f2fdd105");
    static CatalogUIUpgrade() => EditorApplication.delayCall += Upgrade;

    [MenuItem("System Modification/ItemLab/Upgrade Catalog UI")]
    public static void Upgrade()
    {
        if (string.IsNullOrEmpty(Path) || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == Path ||
            AssetDatabase.LoadAssetAtPath<GameObject>(Path) == null) return;
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var view = root.GetComponent<DocumentLibraryView>();
            if (view == null || view.read == null || view.readingGroup == null) return;
            bool changed = false;
            if (view.documentsTab == null) { view.documentsTab = CreateTab(view, "Documents", .08f); changed = true; }
            if (view.evidenceTab == null) { view.evidenceTab = CreateTab(view, "Evidence", .21f); changed = true; }
            if (view.catalogSelector == null)
            {
                var selector = TMPro.TMP_DefaultControls.CreateDropdown(new TMPro.TMP_DefaultControls.Resources());
                selector.name = "Catalog Record Selector"; selector.transform.SetParent(view.transform, false);
                var rect = (RectTransform)selector.transform;
                rect.anchorMin = new Vector2(.25f, .1f); rect.anchorMax = new Vector2(.75f, .15f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                view.catalogSelector = selector.GetComponent<TMPro.TMP_Dropdown>();
                foreach (var label in selector.GetComponentsInChildren<TMPro.TMP_Text>(true)) { label.font = view.title.font; label.color = Color.white; label.fontSize = 22; }
                selector.GetComponent<Image>().color = new Color(.15f, .2f, .28f, .98f);
                view.catalogSelector.template.GetComponent<Image>().color = new Color(.1f, .14f, .2f, 1);
                var arrow = selector.transform.Find("Arrow"); if (arrow != null) arrow.gameObject.SetActive(false);
                view.catalogSelector.template.pivot = new Vector2(.5f, 0);
                view.catalogSelector.template.anchorMin = new Vector2(0, 1);
                view.catalogSelector.template.anchorMax = new Vector2(1, 1);
                view.catalogSelector.template.anchoredPosition = new Vector2(0, 2);
                changed = true;
            }
            foreach (var button in new[] { view.previousDocument, view.nextDocument })
                if (button != null && button.gameObject.activeSelf) { button.gameObject.SetActive(false); changed = true; }
            if (view.documentCounter != null && view.documentCounter.gameObject.activeSelf)
            { view.documentCounter.gameObject.SetActive(false); changed = true; }
            if (view.detailImage == null)
            {
                var go = new GameObject("Evidence Detail Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(view.readingGroup.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(.53f, .2f); rect.anchorMax = new Vector2(.95f, .94f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                view.detailImage = go.GetComponent<Image>(); view.detailImage.preserveAspect = true; view.detailImage.raycastTarget = false;
                go.SetActive(false); changed = true;
            }
            if (view.catalogLayoutVersion < 3 && view.catalogSelector != null)
            {
                var rect = (RectTransform)view.catalogSelector.transform;
                rect.anchorMin = new Vector2(.53f, .8f); rect.anchorMax = new Vector2(.96f, .85f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var template = view.catalogSelector.template;
                template.pivot = new Vector2(.5f, 1); template.anchorMin = Vector2.zero; template.anchorMax = new Vector2(1, 0);
                template.anchoredPosition = new Vector2(0, -2);
                var arrow = new GameObject("Dropdown Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                arrow.transform.SetParent(view.catalogSelector.transform, false);
                var arrowRect = (RectTransform)arrow.transform; arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(1, .5f);
                arrowRect.sizeDelta = new Vector2(24,24); arrowRect.anchoredPosition = new Vector2(-16,0);
                var label = arrow.GetComponent<TMPro.TMP_Text>(); label.font = view.title.font; label.fontSize = 22; label.text = "v";
                label.alignment = TMPro.TextAlignmentOptions.Center; label.raycastTarget = false;
                view.catalogLayoutVersion = 3; changed = true;
            }
            if (view.catalogLayoutVersion < 4 || view.catalogList == null)
            {
                var panel = new GameObject("Catalog List", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                panel.transform.SetParent(view.transform, false);
                var rect = (RectTransform)panel.transform;
                rect.anchorMin = new Vector2(.53f, .2f); rect.anchorMax = new Vector2(.96f, .85f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                panel.GetComponent<Image>().color = new Color(.06f, .08f, .12f, .95f);
                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                viewport.transform.SetParent(panel.transform, false);
                var vr = (RectTransform)viewport.transform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
                vr.offsetMin = vr.offsetMax = Vector2.zero; viewport.GetComponent<Image>().color = Color.clear;
                var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
                var cr = (RectTransform)content.transform;
                cr.anchorMin = new Vector2(0,1); cr.anchorMax = Vector2.one; cr.pivot = new Vector2(.5f,1);
                cr.sizeDelta = Vector2.zero; cr.anchoredPosition = Vector2.zero;
                var layout = content.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(10,10,10,10); layout.spacing = 8;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                view.catalogList = panel.GetComponent<ScrollRect>(); view.catalogList.viewport = vr; view.catalogList.content = cr;
                view.catalogList.horizontal = false; view.catalogList.vertical = true;
                view.catalogList.movementType = ScrollRect.MovementType.Clamped; view.catalogList.scrollSensitivity = 30;
                var row = Object.Instantiate(view.read, cr, false); row.name = "Catalog Row Template";
                row.onClick = new Button.ButtonClickedEvent(); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
                var label = row.GetComponentInChildren<TMPro.TMP_Text>();
                label.text = "Record Title"; label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(14,0); label.rectTransform.offsetMax = new Vector2(-14,0); label.raycastTarget = false;
                view.catalogRowTemplate = row; row.gameObject.SetActive(false); panel.SetActive(false);
                view.catalogSelector.gameObject.SetActive(false); view.catalogLayoutVersion = 4; changed = true;
            }
            if (changed)
            {
                view.title.rectTransform.anchorMin = new Vector2(.35f, .9f);
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static Button CreateTab(DocumentLibraryView view, string name, float x)
    {
        var button = Object.Instantiate(view.read, view.transform, false);
        button.name = name + " Tab";
        button.onClick = new Button.ButtonClickedEvent();
        var rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(x, .91f); rect.anchorMax = new Vector2(x + .12f, .97f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = button.GetComponentInChildren<TMPro.TMP_Text>(); if (label != null) label.text = name;
        return button;
    }
}
