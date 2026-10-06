using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ItemConfigurationWindow : EditorWindow
{
    [SerializeField] private ItemSO item;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private bool followSelection = true;
    private Vector2 scroll;
    private int tab;
    private SerializedObject settings;
    private string filter = "";
    private ItemSO[] library = new ItemSO[0];
    private Editor modelPreview;
    private GameObject previewSource;

    [MenuItem("System Modification/Item Configuration")]
    public static void Open()
    {
        var window = GetWindow<ItemConfigurationWindow>("Item Configuration");
        window.minSize = new Vector2(440, 480); window.Show();
    }
    private void OnEnable()
    {
        Selection.selectionChanged += FollowSelection;
        Undo.undoRedoPerformed += Repaint;
        EditorApplication.projectChanged += RefreshLibrary;
        RefreshLibrary(); FollowSelection();
    }
    private void OnDisable()
    {
        Selection.selectionChanged -= FollowSelection;
        Undo.undoRedoPerformed -= Repaint;
        EditorApplication.projectChanged -= RefreshLibrary;
        if (modelPreview != null) DestroyImmediate(modelPreview);
    }
    private void RefreshLibrary()
    {
        string[] ids = AssetDatabase.FindAssets("t:ItemSO"); library = new ItemSO[ids.Length];
        for (int i = 0; i < ids.Length; i++) library[i] = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(ids[i]));
        Repaint();
    }
    private void FollowSelection()
    {
        if (!followSelection) return;
        if (Selection.activeObject is ItemSO selected) { item = selected; settings = null; tab = 0; }
        else if (Selection.activeGameObject != null)
        {
            var pickup = Selection.activeGameObject.GetComponent<PickupItem>();
            if (pickup != null && pickup.itemData != null) { item = pickup.itemData; settings = null; tab = 0; }
            var ui = Selection.activeGameObject.GetComponent<InventoryUI>();
            if (ui != null) { inventoryUI = ui; tab = 1; }
        }
        Repaint();
    }
    private void OnGUI()
    {
        tab = GUILayout.Toolbar(tab, new[] { "Item Settings", "Inventory / Examine UI" });
        followSelection = EditorGUILayout.ToggleLeft("ติดตามวัตถุที่เลือก", followSelection);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 0) DrawItem(); else DrawUI();
        EditorGUILayout.EndScrollView();
    }
    private void DrawItem()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            item = (ItemSO)EditorGUILayout.ObjectField("ItemSO", item, typeof(ItemSO), false);
            if (EditorGUI.EndChangeCheck()) settings = null;
            if (GUILayout.Button("สร้างใหม่", GUILayout.Width(85))) CreateItem();
        }
        filter = EditorGUILayout.TextField("ค้นหาไอเทม", filter);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            foreach (var entry in library)
            {
                if (entry == null || (!string.IsNullOrWhiteSpace(filter) &&
                    (entry.name + " " + entry.itemName).IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)) continue;
                if (GUILayout.Button($"{entry.itemName}  [{entry.itemType}]  {entry.width}×{entry.height}", EditorStyles.miniButton))
                { item = entry; settings = null; }
            }
        }
        if (item == null) { EditorGUILayout.HelpBox("เลือก ItemSO หรือสร้างไอเทมใหม่", MessageType.Info); return; }
        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("ItemSO เป็น asset ค่าที่แก้ใน Play Mode จะคงอยู่หลังหยุดเล่น", MessageType.Info);
        if (settings == null || settings.targetObject != item) settings = new SerializedObject(item);
        settings.Update();
        Section("ข้อมูลพื้นฐาน"); Field("itemName", "ชื่อไอเทม"); Field("description", "คำอธิบาย");
        Field("itemType", "ประเภท"); Field("icon", "รูปในกระเป๋า");
        Section("พื้นที่ในกระเป๋า");
        Integer("width", "ความกว้าง", 1, 4); Integer("height", "ความสูง", 1, 4);
        Field("allowRotation", "หมุนในกระเป๋าได้");
        bool weapon = settings.FindProperty("itemType").enumValueIndex == (int)ItemType.Weapon;
        using (new EditorGUI.DisabledScope(weapon)) Integer("maxStack", "จำนวนต่อกอง", 1, int.MaxValue);
        if (weapon) EditorGUILayout.HelpBox("ปืนไม่รวมกอง แม้ Max Stack ตั้งไว้มากกว่าหนึ่ง", MessageType.Info);
        Section("โมเดล 3D สำหรับ Examine");
        Field("inspectionPrefab", "Inspection Model"); Field("inspectionRotation", "มุมเริ่มต้น");
        EditorGUILayout.HelpBox("ไม่ใส่ Inspection Model จะใช้ Weapon Visual หรือ Pickup Prefab แทน ตรวจเฉพาะ Mesh/Material โดยไม่เปิดสคริปต์ของโมเดล", MessageType.Info);
        Field("pickupPrefab", "World Pickup Prefab");
        var type = (ItemType)settings.FindProperty("itemType").enumValueIndex;
        if (type == ItemType.Weapon)
        {
            Section("ข้อมูลปืน"); Field("weaponType", "ประเภทปืน"); Field("weaponVisualPrefab", "โมเดลปืนในฉาก");
            Integer("startingMagazineAmmo", "กระสุนเริ่มต้นในแม็ก", 0, int.MaxValue);
            Integer("startingReserveAmmo", "กระสุนสำรองเริ่มต้น", 0, int.MaxValue);
            EditorGUILayout.HelpBox("ค่านี้ใช้กับปืนใหม่ที่เก็บจากฉาก ปืนที่ทิ้งแล้วเก็บกลับใช้ค่าจริงของกระบอกนั้น", MessageType.Info);
        }
        else if (type == ItemType.Ammo)
        { Section("กระสุน"); Field("weaponType", "ใช้กับปืนประเภท"); Integer("ammoAmount", "จำนวนกระสุนต่อแพ็ก", 1, int.MaxValue); }
        else if (type == ItemType.Health)
        { Section("การรักษา"); Field("healType", "ประเภทของยา"); Integer("healAmount", "ค่ารักษาต่อชิ้น", 1, int.MaxValue); }
        if (settings.ApplyModifiedProperties()) EditorUtility.SetDirty(item);
        if (item.pickupPrefab == null) EditorGUILayout.HelpBox("ยังทิ้งลงพื้นไม่ได้ เพราะไม่มี Pickup Prefab", MessageType.Warning);
        else if (item.pickupPrefab.itemData != item) EditorGUILayout.HelpBox("Pickup Prefab อ้างถึง ItemSO คนละตัว ตรวจให้ตรงกันก่อนวางของในฉาก", MessageType.Warning);
        DrawModelPreview();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("แสดง asset")) { EditorGUIUtility.PingObject(item); Selection.activeObject = item; }
            if (GUILayout.Button("บันทึก ItemSO")) AssetDatabase.SaveAssetIfDirty(item);
        }
    }
    private void DrawModelPreview()
    {
        GameObject source = item.inspectionPrefab != null ? item.inspectionPrefab : item.weaponVisualPrefab;
        if (source == null && item.pickupPrefab != null) source = item.pickupPrefab.gameObject;
        if (source != previewSource)
        {
            if (modelPreview != null) DestroyImmediate(modelPreview);
            previewSource = source; modelPreview = source != null ? Editor.CreateEditor(source) : null;
        }
        if (modelPreview != null && modelPreview.HasPreviewGUI())
        {
            Section("ดูโมเดลต้นแบบ");
            modelPreview.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(200, 230, GUILayout.ExpandWidth(true)), GUIStyle.none);
        }
    }
    private void DrawUI()
    {
        inventoryUI = (InventoryUI)EditorGUILayout.ObjectField("Inventory UI", inventoryUI, typeof(InventoryUI), true);
        if (GUILayout.Button("ค้นหา UI ในฉาก (รวมวัตถุที่ซ่อน)"))
            foreach (var candidate in FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.enabled) { inventoryUI = candidate; break; }
        if (inventoryUI == null) { EditorGUILayout.HelpBox("เลือก InventoryUI ในฉากเพื่อปรับ Grid เมนู และ Examine", MessageType.Info); return; }
        EditorGUILayout.HelpBox("ปรับ Inventory Panel ใน Hierarchy หรือเปิด Prefab ด้านล่างเพื่อออกแบบเมนูและ Examine ใน Edit Mode ได้โดยตรง ระบบเติมข้อความและเชื่อมปุ่มตอนเล่น โดยใช้หน้าตาจาก Prefab", MessageType.Info);
        OpenUIPrefab("เปิด Inventory Panel Prefab", PrefabUtility.GetCorrespondingObjectFromSource(inventoryUI.gameObject));
        OpenUIPrefab("เปิด Context Menu Prefab", inventoryUI.contextMenuPrefab);
        OpenUIPrefab("เปิดยืนยัน Drop Prefab", inventoryUI.dropConfirmationPrefab);
        OpenUIPrefab("เปิด Examine Prefab", inventoryUI.inspectionPrefab);
        if (!EditorApplication.isPlaying && !EditorUtility.IsPersistent(inventoryUI) &&
            GUILayout.Button(inventoryUI.gameObject.activeSelf ? "ซ่อน Inventory Panel ใน Edit Mode" : "แสดง Inventory Panel ใน Edit Mode"))
        {
            Undo.RecordObject(inventoryUI.gameObject, "Preview Inventory Panel");
            inventoryUI.gameObject.SetActive(!inventoryUI.gameObject.activeSelf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(inventoryUI.gameObject);
            EditorSceneManager.MarkSceneDirty(inventoryUI.gameObject.scene);
            Selection.activeGameObject = inventoryUI.gameObject;
        }
        var ui = new SerializedObject(inventoryUI); ui.Update();
        var iterator = ui.GetIterator(); bool enter = true;
        while (iterator.NextVisible(enter))
        { enter = false; if (iterator.name != "m_Script") EditorGUILayout.PropertyField(iterator, true); }
        if (ui.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(inventoryUI);
            if (!EditorApplication.isPlaying && !EditorUtility.IsPersistent(inventoryUI))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(inventoryUI);
                EditorSceneManager.MarkSceneDirty(inventoryUI.gameObject.scene);
            }
        }
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("ค่า UI ใน Play Mode เป็นค่าชั่วคราว", MessageType.Info);
            if (GUILayout.Button("สร้าง Grid ใหม่เพื่อดูผล")) inventoryUI.RebuildAndRefresh();
        }
        if (GUILayout.Button("เลือก UI ใน Hierarchy")) { Selection.activeObject = inventoryUI; EditorGUIUtility.PingObject(inventoryUI); }
    }
    private static void OpenUIPrefab(string label, Object asset)
    {
        using (new EditorGUI.DisabledScope(asset == null))
            if (GUILayout.Button(label)) AssetDatabase.OpenAsset(asset is Component component ? component.gameObject : asset);
    }
    private void Field(string path, string title) => EditorGUILayout.PropertyField(settings.FindProperty(path), new GUIContent(title), true);
    private void Integer(string path, string title, int min, int max)
    { var property = settings.FindProperty(path); property.intValue = Mathf.Clamp(EditorGUILayout.IntField(title, property.intValue), min, max); }
    private static void Section(string title) { EditorGUILayout.Space(); EditorGUILayout.LabelField(title, EditorStyles.boldLabel); }
    private void CreateItem()
    {
        string path = EditorUtility.SaveFilePanelInProject("สร้าง ItemSO", "NewItem", "asset", "เลือกที่เก็บไอเทม");
        if (string.IsNullOrEmpty(path)) return;
        item = CreateInstance<ItemSO>(); item.itemName = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(item, path); Undo.RegisterCreatedObjectUndo(item, "Create ItemSO");
        AssetDatabase.SaveAssets(); settings = null; RefreshLibrary(); Selection.activeObject = item;
    }
}
