using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class DocumentConfigurationWindow : EditorWindow
{
    private DocumentSO document;
    private readonly List<DocumentSO> documents = new List<DocumentSO>();
    private Vector2 libraryScroll, scroll;
    private string search = "";
    private bool followSelection = true;
    private int tab, page;
    private Editor preview;
    private GameObject previewSource;
    private DocumentCollection collection;

    [MenuItem("System Modification/Document Configuration")]
    public static void Open() => GetWindow<DocumentConfigurationWindow>("Document Configuration");
    private void OnEnable() { minSize = new Vector2(650, 480); EditorApplication.projectChanged += RefreshLibrary; RefreshLibrary(); OnSelectionChange(); }
    private void OnDisable() { EditorApplication.projectChanged -= RefreshLibrary; ReleasePreview(); }
    private void RefreshLibrary()
    {
        documents.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:DocumentSO"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<DocumentSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) documents.Add(asset);
        }
        documents.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));
        Repaint();
    }
    private void OnSelectionChange() { if (followSelection && Selection.activeObject is DocumentSO selected) Select(selected); }
    private void Select(DocumentSO selected) { document = selected; page = 0; ReleasePreview(); Repaint(); }
    private void ReleasePreview() { if (preview != null) DestroyImmediate(preview); preview = null; previewSource = null; }
    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("สร้างเอกสาร", EditorStyles.toolbarButton)) CreateDocument(false);
            using (new EditorGUI.DisabledScope(document == null))
            {
                if (GUILayout.Button("ทำสำเนา", EditorStyles.toolbarButton)) CreateDocument(true);
                if (GUILayout.Button("บันทึก", EditorStyles.toolbarButton)) AssetDatabase.SaveAssetIfDirty(document);
                if (GUILayout.Button("แสดง Asset", EditorStyles.toolbarButton)) EditorGUIUtility.PingObject(document);
            }
            GUILayout.FlexibleSpace();
            followSelection = GUILayout.Toggle(followSelection, "ตาม Selection", EditorStyles.toolbarButton);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLibrary();
            using (new EditorGUILayout.VerticalScope())
            {
                var selected = (DocumentSO)EditorGUILayout.ObjectField("Document", document, typeof(DocumentSO), false);
                if (selected != document) Select(selected);
                if (document == null) { EditorGUILayout.HelpBox("เลือกเอกสารจากรายการ หรือสร้าง DocumentSO ใหม่ เอกสารไม่ใช้ช่อง Inventory", MessageType.Info); return; }
                tab = GUILayout.Toolbar(tab, new[] { "ข้อมูล", "หน้าเอกสาร", "โมเดล 3D", "ฉาก / UI" });
                scroll = EditorGUILayout.BeginScrollView(scroll);
                var data = new SerializedObject(document); data.Update();
                switch (tab)
                {
                    case 0:
                        foreach (var field in new[] { "title", "author", "summary", "icon" }) EditorGUILayout.PropertyField(data.FindProperty(field), true);
                        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(data.FindProperty("documentId"), new GUIContent("รหัสสำหรับ Save"));
                        foreach (var other in documents)
                            if (other != null && other != document && other.Id == document.Id)
                            {
                                EditorGUILayout.HelpBox("รหัสซ้ำกับ " + other.name + " ต้องเปลี่ยนรหัสให้สำเนาเพื่อเก็บแยกกัน", MessageType.Error);
                                if (GUILayout.Button("สร้างรหัสใหม่")) data.FindProperty("documentId").stringValue = Guid.NewGuid().ToString("N");
                                break;
                            }
                        break;
                    case 1: DrawPages(data.FindProperty("pages")); break;
                    case 2:
                        EditorGUILayout.PropertyField(data.FindProperty("inspectionPrefab"));
                        EditorGUILayout.PropertyField(data.FindProperty("inspectionRotation"));
                        EditorGUILayout.HelpBox("ลากในภาพเพื่อดูโมเดล พรีวิวนี้ใช้ตรวจ Asset; มุมเริ่มต้นในเกมกำหนดด้วย Inspection Rotation", MessageType.Info);
                        break;
                    case 3: DrawSceneTools(); break;
                }
                data.ApplyModifiedProperties();
                if (tab == 2) DrawPreview();
                EditorGUILayout.EndScrollView();
            }
        }
    }
    private void DrawLibrary()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(200)))
        {
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            EditorGUILayout.LabelField("เอกสารทั้งหมด: " + documents.Count, EditorStyles.miniLabel);
            libraryScroll = EditorGUILayout.BeginScrollView(libraryScroll);
            foreach (var asset in documents)
            {
                if (asset == null) continue;
                string label = string.IsNullOrWhiteSpace(asset.title) ? asset.name : asset.title;
                if (!string.IsNullOrWhiteSpace(search) && (label + " " + asset.name + " " + asset.author).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (GUILayout.Toggle(document == asset, label, "Button") && document != asset) Select(asset);
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("รีเฟรชรายการ")) RefreshLibrary();
        }
    }
    private void DrawPages(SerializedProperty pages)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("เพิ่มหน้า")) { page = pages.arraySize; pages.arraySize++; pages.GetArrayElementAtIndex(page).stringValue = ""; }
            using (new EditorGUI.DisabledScope(pages.arraySize == 0))
            {
                if (GUILayout.Button("สำเนาหน้า"))
                {
                    page = Mathf.Clamp(page, 0, pages.arraySize - 1);
                    string text = pages.GetArrayElementAtIndex(page).stringValue;
                    pages.InsertArrayElementAtIndex(page); page++; pages.GetArrayElementAtIndex(page).stringValue = text;
                }
                if (GUILayout.Button("ลบหน้า")) { pages.DeleteArrayElementAtIndex(Mathf.Clamp(page, 0, pages.arraySize - 1)); page = Mathf.Max(0, page - 1); }
            }
        }
        if (pages.arraySize == 0) { EditorGUILayout.HelpBox("ยังไม่มีหน้าเอกสาร กดเพิ่มหน้าเพื่อเขียนเนื้อหา", MessageType.Info); return; }
        page = Mathf.Clamp(page, 0, pages.arraySize - 1);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(page == 0)) if (GUILayout.Button("<", GUILayout.Width(30))) page--;
            page = Mathf.Clamp(EditorGUILayout.IntField("หน้า", page + 1) - 1, 0, pages.arraySize - 1);
            GUILayout.Label("/ " + pages.arraySize);
            using (new EditorGUI.DisabledScope(page == pages.arraySize - 1)) if (GUILayout.Button(">", GUILayout.Width(30))) page++;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(page == 0)) if (GUILayout.Button("เลื่อนหน้าขึ้น")) { pages.MoveArrayElement(page, page - 1); page--; }
            using (new EditorGUI.DisabledScope(page == pages.arraySize - 1)) if (GUILayout.Button("เลื่อนหน้าลง")) { pages.MoveArrayElement(page, page + 1); page++; }
        }
        var content = pages.GetArrayElementAtIndex(page);
        var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        content.stringValue = EditorGUILayout.TextArea(content.stringValue ?? "", style, GUILayout.MinHeight(260));
        EditorGUILayout.HelpBox("รองรับ Undo / Redo; ฟอนต์ใน UI ต้องมีตัวอักษรภาษาที่ใช้ในเอกสาร", MessageType.Info);
    }
    private void DrawPreview()
    {
        if (previewSource != document.inspectionPrefab) { ReleasePreview(); previewSource = document.inspectionPrefab; if (previewSource != null) preview = Editor.CreateEditor(previewSource); }
        if (preview == null) { EditorGUILayout.HelpBox("กำหนด Inspection Prefab เพื่อแสดงโมเดล 3D", MessageType.Info); return; }
        var rect = GUILayoutUtility.GetRect(100, 300, GUILayout.ExpandWidth(true));
        preview.OnInteractivePreviewGUI(rect, EditorStyles.helpBox);
    }
    private void CreateDocument(bool duplicate)
    {
        var path = EditorUtility.SaveFilePanelInProject("สร้าง DocumentSO", duplicate ? document.name + " Copy" : "NewDocument", "asset", "เลือกที่เก็บเอกสาร");
        if (string.IsNullOrEmpty(path)) return;
        var asset = duplicate ? Instantiate(document) : CreateInstance<DocumentSO>();
        if (!duplicate) asset.title = "New Document";
        var data = new SerializedObject(asset); data.FindProperty("documentId").stringValue = Guid.NewGuid().ToString("N"); data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(asset, path); Undo.RegisterCreatedObjectUndo(asset, "Create Document"); AssetDatabase.SaveAssetIfDirty(asset);
        RefreshLibrary(); Select(asset); Selection.activeObject = asset;
    }
    private void DrawSceneTools()
    {
        EditorGUILayout.HelpBox("Catalog ใช้เชื่อมรหัสเอกสารกับข้อมูลเมื่อโหลด Save ส่วน Pickup คือจุดเก็บในแมพ", MessageType.Info);
        collection = (DocumentCollection)EditorGUILayout.ObjectField("Document Collection", collection, typeof(DocumentCollection), true);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("ค้นหา Document Collection ในฉาก")) collection = FindFirstObjectByType<DocumentCollection>(FindObjectsInactive.Include);
            if (GUILayout.Button("เพิ่ม Document System ในฉาก"))
            {
                collection = FindFirstObjectByType<DocumentCollection>(FindObjectsInactive.Include);
                if (collection == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Documents/DocumentSystem.prefab");
                    if (prefab != null) { var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab); Undo.RegisterCreatedObjectUndo(instance, "Create Document System"); collection = instance.GetComponent<DocumentCollection>(); EditorSceneManager.MarkSceneDirty(instance.scene); }
                }
                if (collection != null) Selection.activeGameObject = collection.gameObject;
            }
            if (GUILayout.Button("เพิ่มเอกสารนี้เข้า Catalog")) RegisterInCatalog();
            if (GUILayout.Button("วาง Pickup ที่จุดกลาง Scene View"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Documents/DocumentPickup.prefab");
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab); Undo.RegisterCreatedObjectUndo(instance, "Place Document Pickup");
                    var pickup = instance.GetComponent<DocumentPickup>(); pickup.document = document;
                    if (SceneView.lastActiveSceneView != null) instance.transform.position = SceneView.lastActiveSceneView.pivot;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(pickup); PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    Selection.activeGameObject = instance; EditorSceneManager.MarkSceneDirty(instance.scene); RegisterInCatalog();
                }
            }
        }
        if (GUILayout.Button("เปิดหน้าต่างอ่าน / Examine UI Prefab")) OpenPrefab("Assets/Prefab/UI/DocumentLibrary.prefab");
        if (GUILayout.Button("เปิดปุ่ม Documents ใน Inventory")) OpenPrefab("Assets/Prefab/UI/DocumentsButton.prefab");
        if (GUILayout.Button("เปิด Pickup Prefab")) OpenPrefab("Assets/Prefab/Documents/DocumentPickup.prefab");
    }
    private static void OpenPrefab(string path) { var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (asset != null) AssetDatabase.OpenAsset(asset); }
    private void RegisterInCatalog()
    {
        if (collection == null) collection = FindFirstObjectByType<DocumentCollection>(FindObjectsInactive.Include);
        if (collection == null || EditorUtility.IsPersistent(collection)) { ShowNotification(new GUIContent("เลือก Document Collection ในฉากก่อน")); return; }
        var data = new SerializedObject(collection); data.Update(); var catalog = data.FindProperty("catalog");
        for (int i = 0; i < catalog.arraySize; i++) if (catalog.GetArrayElementAtIndex(i).objectReferenceValue == document) { ShowNotification(new GUIContent("เอกสารนี้อยู่ใน Catalog แล้ว")); return; }
        int index = catalog.arraySize++; catalog.GetArrayElementAtIndex(index).objectReferenceValue = document;
        data.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(collection); EditorSceneManager.MarkSceneDirty(collection.gameObject.scene);
        ShowNotification(new GUIContent("เพิ่มเอกสารเข้า Catalog แล้ว"));
    }
}
