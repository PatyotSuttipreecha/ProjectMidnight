using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class DoorConfigurationWindow : EditorWindow
{
    private DoorController door;
    private Editor inspector;
    private Vector2 scroll;
    [MenuItem("System Modification/Door Configuration")]
    public static void Open() => GetWindow<DoorConfigurationWindow>("Door Configuration");
    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null) door = Selection.activeGameObject.GetComponentInParent<DoorController>();
        Repaint();
    }
    private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("เล็งประตูแล้วกด F จากระยะที่กำหนด กุญแจใช้ ItemSO ชนิด Key ใน Inventory", MessageType.Info);
        door = (DoorController)EditorGUILayout.ObjectField("Door", door, typeof(DoorController), true);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("สร้างประตูบานเดียว + กุญแจตัวอย่างในฉาก")) CreateExample(false);
            if (GUILayout.Button("สร้างประตูสองบาน + กุญแจตัวอย่างในฉาก")) CreateExample(true);
        }
        if (door == null) return;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Editor.CreateCachedEditor(door, null, ref inspector);
        inspector.OnInspectorGUI();
        if (door.startsLocked && (door.requiredKey == null || door.requiredKey.itemType != ItemType.Key))
            EditorGUILayout.HelpBox("ประตูล็อกต้องกำหนด Required Key เป็น ItemSO ชนิด Key", MessageType.Warning);
        EditorGUILayout.HelpBox("ประตูหลายบาน: เพิ่ม Leaves แล้วกำหนด Hinge และ Open Rotation ของแต่ละบาน เช่น Y 90 / -90 รายการ Leaves จะใช้แทน Hinge เดิม", MessageType.Info);
        if (!door.HasValidHinges) EditorGUILayout.HelpBox("กำหนด Hinge ให้ครบทุกบานและห้ามซ้ำ นำ Mesh และ Collider ไว้ใต้บานพับแต่ละตัว", MessageType.Error);
        if (door.requiredKey != null && GUILayout.Button("เปิดข้อมูลกุญแจ")) Selection.activeObject = door.requiredKey;
        if (GUILayout.Button("เลือกประตูในฉาก")) { Selection.activeGameObject = door.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
        EditorGUILayout.EndScrollView();
    }
    private void CreateExample(bool doubleDoor)
    {
        const string folder = "Assets/Prefab/Doors";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefab", "Doors");
        var key = CreateInstance<ItemSO>();
        key.itemName = "Demo Door Key"; key.itemType = ItemType.Key; key.width = key.height = 1;
        key.description = "Opens the demo locked door.";
        var keyPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/DemoDoorKey.asset");
        AssetDatabase.CreateAsset(key, keyPath);
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Key Pickup"; visual.transform.localScale = new Vector3(.15f, .05f, .3f);
        DestroyImmediate(visual.GetComponent<BoxCollider>());
        var trigger = visual.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = 5;
        var pickup = visual.AddComponent<PickupItem>(); pickup.itemData = key;
        var keyPrefabPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/DemoKeyPickup.prefab");
        var keyPrefab = PrefabUtility.SaveAsPrefabAsset(visual, keyPrefabPath);
        DestroyImmediate(visual);
        key.pickupPrefab = keyPrefab.GetComponent<PickupItem>(); key.inspectionPrefab = keyPrefab;
        EditorUtility.SetDirty(key); AssetDatabase.SaveAssetIfDirty(key);
        var root = new GameObject("Locked Door");
        var controller = root.AddComponent<DoorController>(); controller.startsLocked = true; controller.requiredKey = key;
        controller.audioSource = root.AddComponent<AudioSource>(); controller.audioSource.playOnAwake = false; controller.audioSource.spatialBlend = 1;
        var hinge = new GameObject("Hinge").transform; hinge.SetParent(root.transform, false); controller.hinge = hinge;
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube); panel.name = "Door Panel"; panel.transform.SetParent(hinge, false);
        panel.transform.localPosition = new Vector3(.6f, 1.1f, 0); panel.transform.localScale = new Vector3(1.2f, 2.2f, .12f);
        if (doubleDoor)
        {
            root.name = "Double Locked Door";
            hinge.name = "Left Hinge"; hinge.localPosition = new Vector3(-1.2f, 0, 0);
            var right = new GameObject("Right Hinge").transform; right.SetParent(root.transform, false); right.localPosition = new Vector3(1.2f, 0, 0);
            var rightPanel = GameObject.CreatePrimitive(PrimitiveType.Cube); rightPanel.name = "Right Door Panel"; rightPanel.transform.SetParent(right, false);
            rightPanel.transform.localPosition = new Vector3(-.6f, 1.1f, 0); rightPanel.transform.localScale = panel.transform.localScale;
            controller.leaves = new[] {
                new DoorController.DoorLeaf { hinge = hinge, openRotation = new Vector3(0, 90, 0) },
                new DoorController.DoorLeaf { hinge = right, openRotation = new Vector3(0, -90, 0) }
            };
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GenerateUniqueAssetPath(folder + (doubleDoor ? "/DemoDoubleLockedDoor.prefab" : "/DemoLockedDoor.prefab")));
        DestroyImmediate(root);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var player = FindFirstObjectByType<PlayerController>();
        instance.transform.position = player != null ? player.transform.position + player.transform.forward * 2 : SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        Undo.RegisterCreatedObjectUndo(instance, "Create Demo Door");
        var keyInstance = (GameObject)PrefabUtility.InstantiatePrefab(keyPrefab);
        keyInstance.transform.position = instance.transform.position - instance.transform.forward + Vector3.up * .3f;
        Undo.RegisterCreatedObjectUndo(keyInstance, "Create Demo Key");
        door = instance.GetComponent<DoorController>(); Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }
}
