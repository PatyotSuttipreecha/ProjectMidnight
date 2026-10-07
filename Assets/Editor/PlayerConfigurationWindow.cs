using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PlayerConfigurationWindow : EditorWindow
{
    [SerializeField] private PlayerController player;
    [SerializeField] private InventoryManager inventory;
    [SerializeField] private bool followSelection = true;
    private Vector2 scroll;
    private int tab;
    private int requestedWidth = 4, requestedHeight = 4;
    private SerializedObject settings;

    [MenuItem("System Modification/Player Configuration")]
    public static void Open()
    {
        var window = GetWindow<PlayerConfigurationWindow>("Player Configuration");
        window.minSize = new Vector2(430, 440);
        window.Show();
    }
    private void OnEnable()
    {
        Selection.selectionChanged += FollowSelection;
        Undo.undoRedoPerformed += Repaint;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        FollowSelection();
    }
    private void OnDisable()
    {
        Selection.selectionChanged -= FollowSelection;
        Undo.undoRedoPerformed -= Repaint;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
    }
    private void PlayModeChanged(PlayModeStateChange state) { settings = null; Repaint(); }
    private void FollowSelection()
    {
        if (!followSelection || Selection.activeGameObject == null) return;
        GameObject selected = Selection.activeGameObject;
        PlayerController found = selected.GetComponentInParent<PlayerController>();
        if (found == null) found = selected.GetComponentInChildren<PlayerController>(true);
        if (found != null) { player = found; settings = null; }
        InventoryManager bag = selected.GetComponentInChildren<InventoryManager>(true);
        if (bag != null) inventory = bag;
        Repaint();
    }
    private void OnGUI()
    {
        followSelection = EditorGUILayout.ToggleLeft("ติดตามวัตถุที่เลือก", followSelection);
        EditorGUI.BeginChangeCheck();
        player = (PlayerController)EditorGUILayout.ObjectField("Player", player, typeof(PlayerController), true);
        if (EditorGUI.EndChangeCheck()) settings = null;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("เลือก Player ในฉาก"))
            { player = FindFirstObjectByType<PlayerController>(); settings = null; }
            if (GUILayout.Button("เปิดค่าของ Player Prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Character/Player.prefab");
                player = prefab != null ? prefab.GetComponentInChildren<PlayerController>(true) : null;
                settings = null;
            }
        }
        if (player == null)
        { EditorGUILayout.HelpBox("เลือก Player ใน Hierarchy หรือเปิด Player Prefab เพื่อปรับค่า", MessageType.Info); return; }
        if (EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("ค่าที่ปรับใน Play Mode เป็นค่าชั่วคราว อาวุธเริ่มต้นมีผลเมื่อเริ่มเกมใหม่", MessageType.Info);
        else if (!EditorUtility.IsPersistent(player) && PrefabUtility.IsPartOfPrefabInstance(player))
            EditorGUILayout.HelpBox("กำลังปรับ Player ในฉาก ค่าจะเป็น Prefab override หากต้องการใช้กับทุกฉาก ให้เปิดค่าของ Player Prefab", MessageType.Info);
        tab = GUILayout.Toolbar(tab, new[] { "ผู้เล่น", "อาวุธเริ่มต้น", "กระเป๋า", "References", "Interaction" });
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (settings == null || settings.targetObject != player) settings = new SerializedObject(player);
        settings.Update();
        switch (tab)
        {
            case 0: DrawPlayer(); break;
            case 1: DrawLoadout(); break;
            case 2: DrawInventory(); break;
            case 3: DrawReferences(); break;
            case 4:
                var interaction = player.GetComponent<PlayerInteraction>();
                if (interaction == null)
                {
                    EditorGUILayout.HelpBox("PlayerInteraction will be added on startup. Add it now to configure it.", MessageType.Info);
                    if (GUILayout.Button("Add Player Interaction")) Undo.AddComponent<PlayerInteraction>(player.gameObject);
                }
                else
                {
                    var interactionData = new SerializedObject(interaction); interactionData.Update();
                    foreach (var field in new[] { "interactKey", "maxDistance", "interactionCamera", "interactionMask", "promptText", "showGizmos" })
                        EditorGUILayout.PropertyField(interactionData.FindProperty(field));
                    if (interactionData.ApplyModifiedProperties()) MarkChanged(interaction);
                }
                break;
        }
        if (settings.ApplyModifiedProperties()) MarkChanged(player);
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("แสดง Player ใน Inspector")) { Selection.activeObject = player; EditorGUIUtility.PingObject(player); }
    }
    private void Field(string path, string label)
    {
        var property = settings.FindProperty(path);
        if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }
    private void NonNegative(string path, string label, float minimum = 0)
    {
        var property = settings.FindProperty(path);
        property.floatValue = Mathf.Max(minimum, EditorGUILayout.FloatField(label, property.floatValue));
    }
    private void DrawPlayer()
    {
        EditorGUILayout.LabelField("การเคลื่อนที่", EditorStyles.boldLabel);
        NonNegative("moveSpeed", "ความเร็วเดิน");
        NonNegative("sprintBonus", "ความเร็วเพิ่มตอนวิ่ง");
        EditorGUILayout.LabelField("เสียงที่ศัตรูได้ยิน", EditorStyles.boldLabel);
        NonNegative("walkNoiseRadius", "ระยะเสียงเดิน");
        NonNegative("runNoiseRadius", "ระยะเสียงวิ่ง");
        NonNegative("walkNoiseInterval", "ช่วงเสียงเดิน (วินาที)", .1f);
        NonNegative("runNoiseInterval", "ช่วงเสียงวิ่ง (วินาที)", .1f);
        Field("showNoiseGizmos", "แสดง Gizmos เสียง");
        EditorGUILayout.LabelField("ความเร็ววิ่งรวม", (settings.FindProperty("moveSpeed").floatValue + settings.FindProperty("sprintBonus").floatValue).ToString("0.##"));
        EditorGUILayout.Space();
        NonNegative("maxHealth", "พลังชีวิตสูงสุด", 1);
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.FloatField("พลังชีวิตปัจจุบัน", player.currentHealth);
        NonNegative("aimBlendDuration", "เวลา Blend ท่าเล็ง (วินาที)");
        EditorGUILayout.Space();
        Field("footstepWalkSO", "เสียงเดิน"); Field("footstepRunSO", "เสียงวิ่ง");
    }
    private void DrawLoadout()
    {
        EditorGUILayout.HelpBox("Starting Weapons คือปืนที่ได้รับตอนเริ่มเกม ส่วนรายการจับคู่ด้านล่างคือปืนที่ระบบรองรับ แม้ไม่ได้ให้ตอนเริ่มก็ยังเก็บจากฉากได้", MessageType.Info);
        var starting = settings.FindProperty("startingWeaponItems");
        Field("startingWeaponItems", "Starting Weapons");
        var definitions = settings.FindProperty("weaponInventoryItems");
        var models = settings.FindProperty("weapons");
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("เริ่มด้วยปืนพกเท่านั้น"))
            {
                ItemSO pistol = null;
                for (int i = 0; i < definitions.arraySize; i++)
                {
                    var item = definitions.GetArrayElementAtIndex(i).objectReferenceValue as ItemSO;
                    if (item != null && item.itemType == ItemType.Weapon && item.weaponType == Guns.WeaponType.Pistol)
                    { pistol = item; break; }
                }
                if (pistol != null) { starting.arraySize = 1; starting.GetArrayElementAtIndex(0).objectReferenceValue = pistol; }
                else ShowNotification(new GUIContent("ยังไม่ได้จับคู่ ItemSO ปืนพก"));
            }
            if (GUILayout.Button("เริ่มมือเปล่า")) starting.arraySize = 0;
        }
        var seen = new HashSet<ItemSO>();
        for (int i = 0; i < starting.arraySize; i++)
        {
            var item = starting.GetArrayElementAtIndex(i).objectReferenceValue as ItemSO;
            if (item == null || item.itemType != ItemType.Weapon)
            { EditorGUILayout.HelpBox($"Starting Weapons [{i}] ต้องเป็น ItemSO ประเภท Weapon", MessageType.Warning); continue; }
            if (!seen.Add(item)) EditorGUILayout.HelpBox($"{item.itemName} ซ้ำในรายการ ระบบให้หนึ่งกระบอกเท่านั้น", MessageType.Warning);
            bool mapped = false;
            for (int j = 0; j < definitions.arraySize && j < models.arraySize; j++)
                if (definitions.GetArrayElementAtIndex(j).objectReferenceValue == item && models.GetArrayElementAtIndex(j).objectReferenceValue != null) mapped = true;
            if (!mapped) EditorGUILayout.HelpBox($"{item.itemName} ยังไม่มีโมเดลและ ItemSO ที่จับคู่กัน จึงยังใช้ไม่ได้", MessageType.Warning);
            EditorGUILayout.LabelField(item.itemName, $"{item.width} × {item.height} ช่อง");
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("การจับคู่โมเดลและ ItemSO (index ต้องตรงกัน)", EditorStyles.boldLabel);
        Field("weapons", "Weapon Models"); Field("weaponInventoryItems", "Weapon Item Definitions");
        if (models.arraySize != definitions.arraySize)
            EditorGUILayout.HelpBox("จำนวนช่องของ Weapon Models และ Item Definitions ไม่เท่ากัน ตรวจให้ index ของโมเดลและ SO ตรงกัน", MessageType.Warning);
    }
    private void DrawInventory()
    {
        inventory = (InventoryManager)EditorGUILayout.ObjectField("Inventory Manager", inventory, typeof(InventoryManager), true);
        if (inventory == null && GUILayout.Button("ค้นหา Inventory ในฉาก")) inventory = FindFirstObjectByType<InventoryManager>();
        if (inventory == null) { EditorGUILayout.HelpBox("เลือก Inventory Manager ของฉากเพื่อปรับขนาดกระเป๋า", MessageType.Info); return; }
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("ขนาดปัจจุบัน", $"{inventory.gridWidth} × {inventory.gridHeight}");
            requestedWidth = Mathf.Max(1, EditorGUILayout.IntField("ความกว้างใหม่", requestedWidth));
            requestedHeight = Mathf.Max(1, EditorGUILayout.IntField("ความสูงใหม่", requestedHeight));
            if (GUILayout.Button("ปรับขนาดกระเป๋า")) inventory.ResizeGrid(requestedWidth, requestedHeight);
            EditorGUILayout.HelpBox("ถ้าไอเทมทั้งหมดใส่ไม่พอ ระบบจะยกเลิกการปรับขนาดและเก็บของเดิมไว้", MessageType.Info);
        }
        else
        {
            var bag = new SerializedObject(inventory); bag.Update();
            var width = bag.FindProperty("gridWidth"); var height = bag.FindProperty("gridHeight");
            width.intValue = Mathf.Max(1, EditorGUILayout.IntField("ความกว้าง (ช่อง)", width.intValue));
            height.intValue = Mathf.Max(1, EditorGUILayout.IntField("ความสูง (ช่อง)", height.intValue));
            if (bag.ApplyModifiedProperties()) MarkChanged(inventory);
            EditorGUILayout.LabelField("ช่องทั้งหมด", (width.intValue * height.intValue).ToString());
        }
    }
    private void DrawReferences()
    {
        Field("cineCamera", "กล้อง Cinemachine"); Field("cameraInputControllers", "Camera Look Inputs (optional)");
        Field("rigBuilder", "Aiming Rig"); Field("inventoryLayout", "Inventory Layout");
        EditorGUILayout.HelpBox("ค่าปืน เช่น Damage และ Spread ปรับที่ Armory Lab ส่วนขนาดไอเทมและจำนวนต่อกองปรับใน ItemSO", MessageType.Info);
    }
    private static void MarkChanged(Object target)
    {
        EditorUtility.SetDirty(target);
        if (EditorApplication.isPlaying || EditorUtility.IsPersistent(target)) return;
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        if (target is Component component && component.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }
}
