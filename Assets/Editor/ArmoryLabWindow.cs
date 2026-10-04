using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ArmoryLabWindow : EditorWindow
{
    private const string PendingKey = "ArmoryLab.PendingTuning";
    [SerializeField] private Guns weapon;
    [SerializeField] private bool followSelection = true;
    private Vector2 scroll;
    private SerializedObject serializedWeapon;
    private TuningSnapshot baseline;
    private string status;

    private static readonly string[] TuningPaths =
    {
        "weaponStat.damage", "weaponStat.bulletSpeed", "weaponStat.baseSpread",
        "weaponStat.pelletCount", "weaponStat.pelletSpreadAngle", "spreadPreviewDistance",
        "weaponStat.minSpread", "weaponStat.aimTime", "weaponStat.recoil",
        "weaponStat.recoilRecovery", "weaponStat.magazineSize", "weaponStat.reloadTime",
        "breathingAmplitude", "breathingFrequency", "swayBlendSpeed", "cameraBreathingAmplitude",
        "movingSpreadFraction", "movementThreshold", "movementBlendSpeed"
    };

    [Serializable]
    private class TuningValue
    {
        public string path;
        public float number;
        public int integer;
        public Vector2 vector;
        public SerializedPropertyType type;
    }

    [Serializable]
    private class TuningSnapshot
    {
        public string targetId;
        public string name;
        public List<TuningValue> values = new List<TuningValue>();
    }

    [MenuItem("System Modification/Armory Lab")]
    public static void Open()
    {
        ArmoryLabWindow window = GetWindow<ArmoryLabWindow>("Armory Lab");
        window.minSize = new Vector2(380f, 480f);
        window.Show();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        if (weapon != null) SetWeapon(weapon);
        else OnSelectionChanged();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnUndoRedo() => Repaint();

    private void OnSelectionChanged()
    {
        if (!followSelection) return;
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;
        Guns selectedWeapon = selected.GetComponent<Guns>();
        if (selectedWeapon == null) selectedWeapon = selected.GetComponentInParent<Guns>();
        if (selectedWeapon == null || selectedWeapon.weaponStat.weaponName == Guns.WeaponType.None ||
            selectedWeapon.weaponStat.weaponName == Guns.WeaponType.Knife)
        {
            Guns[] children = selected.GetComponentsInChildren<Guns>(true);
            foreach (Guns child in children)
                if (child.weaponStat.weaponName != Guns.WeaponType.None && child.weaponStat.weaponName != Guns.WeaponType.Knife)
                {
                    selectedWeapon = child;
                    break;
                }
        }
        if (selectedWeapon != null && selectedWeapon != weapon) SetWeapon(selectedWeapon);
    }

    private void SetWeapon(Guns selected)
    {
        weapon = selected;
        serializedWeapon = weapon != null ? new SerializedObject(weapon) : null;
        baseline = weapon != null ? Capture(weapon) : null;
        status = null;
        Repaint();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        serializedWeapon = null;
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            // Captured values are offered for explicit application, never written automatically.
            EditorApplication.delayCall += RestoreCapturedSelection;
        }
        Repaint();
    }

    private void RestoreCapturedSelection()
    {
        if (this == null) return;
        TuningSnapshot pending = ReadPending();
        Guns target = Resolve(pending);
        if (target != null) SetWeapon(target);
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Armory Lab", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Tune a weapon, try it, keep the settings you like.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        Guns selected = (Guns)EditorGUILayout.ObjectField("Weapon", weapon, typeof(Guns), true);
        if (EditorGUI.EndChangeCheck()) SetWeapon(selected);
        followSelection = EditorGUILayout.ToggleLeft("Follow selection in Hierarchy / Project", followSelection);
        DrawPending();
        if (weapon == null)
        {
            EditorGUILayout.HelpBox("Select a weapon such as Pistol, or drag a GameObject with a Guns component into Weapon. Prefabs and scene instances are supported.", MessageType.Info);
            return;
        }
        if (serializedWeapon == null || serializedWeapon.targetObject != weapon)
            serializedWeapon = new SerializedObject(weapon);
        EditorGUILayout.HelpBox(DescribeTarget(), EditorApplication.isPlaying ? MessageType.Warning : MessageType.Info);
        if (weapon.weaponStat.weaponName == Guns.WeaponType.None || weapon.weaponStat.weaponName == Guns.WeaponType.Knife)
            EditorGUILayout.HelpBox("This slot is unarmed or melee. Select the equipped firearm to tune aiming.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying && EditorUtility.IsPersistent(weapon)))
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            serializedWeapon.Update();
            EditorGUI.BeginChangeCheck();
            Section("Accuracy");
            Slider("weaponStat.baseSpread", "Base spread (degrees)", 0f, 30f, "Maximum random deviation when aiming has not settled.");
            Slider("weaponStat.minSpread", "Stationary spread (degrees)", 0f, 30f, "Minimum random deviation after holding aim still.");
            Slider("weaponStat.aimTime", "Time to settle (seconds)", 0f, 10f, "Time needed for the crosshair to close toward minimum spread.");
            PercentSlider("movingSpreadFraction", "Walking spread floor (%)", "Minimum spread while walking, as a percentage of base spread.");
            Slider("movementBlendSpeed", "Movement response", 0f, 30f, "How quickly the spread responds to starting and stopping.");
            Slider("movementThreshold", "Movement threshold (units/s)", 0f, 1f, "Movement below this speed is treated as stationary.");
            Section("Breathing");
            SerializedProperty amplitude = serializedWeapon.FindProperty("breathingAmplitude");
            Vector2 sway = amplitude.vector2Value;
            EditorGUI.BeginChangeCheck();
            sway.x = EditorGUILayout.Slider(new GUIContent("Horizontal sway (%)", "Percentage of camera viewport width."), sway.x * 100f, 0f, 2f) / 100f;
            sway.y = EditorGUILayout.Slider(new GUIContent("Vertical sway (%)", "Percentage of camera viewport height."), sway.y * 100f, 0f, 2f) / 100f;
            if (EditorGUI.EndChangeCheck()) amplitude.vector2Value = sway;
            Slider("breathingFrequency", "Breathing frequency (Hz)", 0f, 2f, "Vertical breathing cycles per second. 0.25 means one cycle every four seconds.");
            Slider("swayBlendSpeed", "Sway fade speed", 0f, 20f, "How quickly sway fades in while aiming and out when aim is released.");
            SerializedProperty cameraAmplitude = serializedWeapon.FindProperty("cameraBreathingAmplitude");
            Vector2 cameraSway = cameraAmplitude.vector2Value;
            EditorGUI.BeginChangeCheck();
            cameraSway.x = EditorGUILayout.Slider("Camera horizontal sway (degrees)", cameraSway.x, 0f, Mathf.Max(1f, cameraSway.x));
            cameraSway.y = EditorGUILayout.Slider("Camera vertical sway (degrees)", cameraSway.y, 0f, Mathf.Max(1f, cameraSway.y));
            if (EditorGUI.EndChangeCheck()) cameraAmplitude.vector2Value = cameraSway;
            Section("Recoil");
            Slider("weaponStat.recoil", "Recoil strength", 0f, 10f, "Strength of the existing Cinemachine recoil effect.");
            Slider("weaponStat.recoilRecovery", "Recoil recovery", 0f, 30f, "How quickly recoil returns toward neutral.");
            Section("Weapon");
            Slider("weaponStat.damage", "Damage", 0f, 200f, "Base damage before body-part multipliers. Zero keeps the legacy random hitbox damage.");
            Slider("weaponStat.bulletSpeed", "Bullet speed (units/s)", 0f, 300f, "Projectile travel speed.");
            if (weapon.weaponStat.weaponName == Guns.WeaponType.Shotgun)
            {
                Section("Shotgun Pellets");
                SerializedProperty pellets = serializedWeapon.FindProperty("weaponStat.pelletCount");
                pellets.intValue = EditorGUILayout.IntSlider("Pellets per shell", Mathf.Max(1, pellets.intValue), 1, 128);
                Slider("weaponStat.pelletSpreadAngle", "Pellet spread half-angle (degrees)", 0f, 45f, "Cone half-angle. Full cone width is twice this angle. Zero sends all pellets along the same direction.");
                EditorGUILayout.PropertyField(serializedWeapon.FindProperty("showSpreadGizmos"), new GUIContent("Show spread Gizmos when selected"));
                Slider("spreadPreviewDistance", "Preview distance (metres)", 0.1f, 100f, "Orange cone shows pellet spread; cyan includes aiming accuracy. Select the gun or its parent in Scene View.");
                EditorGUILayout.HelpBox("One click consumes one shell. Damage is per pellet, so multiple hits increase total damage. Aiming accuracy shifts the whole pellet cone; it does not remove the pellet spread.", MessageType.Info);
            }
            SerializedProperty magazine = serializedWeapon.FindProperty("weaponStat.magazineSize");
            magazine.intValue = EditorGUILayout.IntSlider("Magazine capacity", magazine.intValue,
                Mathf.Min(1, magazine.intValue), Mathf.Max(100, magazine.intValue));
            Slider("weaponStat.reloadTime", "Reload time (seconds)", 0f, 10f, "Duration of the reload.");
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                serializedWeapon.FindProperty("weaponStat.minSpread").floatValue = Mathf.Min(
                    serializedWeapon.FindProperty("weaponStat.minSpread").floatValue,
                    serializedWeapon.FindProperty("weaponStat.baseSpread").floatValue);
            }
            if (serializedWeapon.ApplyModifiedProperties()) MarkChanged(weapon);

            EditorGUILayout.Space();
            float floor = Mathf.Max(weapon.weaponStat.minSpread, weapon.weaponStat.baseSpread * serializedWeapon.FindProperty("movingSpreadFraction").floatValue);
            EditorGUILayout.LabelField("Settled walking spread", floor.ToString("0.###") + " degrees");
            EditorGUILayout.HelpBox("Damage 0 uses legacy damage. Fire rate is not listed because the current gun fires once per click. Captures exclude ammo consumed during play and object references.", MessageType.Info);
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(baseline == null))
                    if (GUILayout.Button("Restore opening values"))
                    {
                        Apply(weapon, baseline, "Restore weapon tuning");
                        status = "Opening values restored. Undo is available.";
                    }
                if (EditorApplication.isPlaying)
                {
                    using (new EditorGUI.DisabledScope(EditorUtility.IsPersistent(weapon)))
                        if (GUILayout.Button("Capture Play Mode tuning")) CaptureForLater();
                }
                else if (GUILayout.Button("Save Scene / Prefab")) SaveTarget();
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }

    private void Section(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void Slider(string path, string label, float min, float max, string tooltip)
    {
        SerializedProperty property = serializedWeapon.FindProperty(path);
        // Keep existing out-of-range values visible instead of silently clamping on open.
        property.floatValue = EditorGUILayout.Slider(new GUIContent(label, tooltip), property.floatValue,
            min, Mathf.Max(max, property.floatValue));
    }

    private void PercentSlider(string path, string label, string tooltip)
    {
        SerializedProperty property = serializedWeapon.FindProperty(path);
        EditorGUI.BeginChangeCheck();
        float percentage = EditorGUILayout.Slider(new GUIContent(label, tooltip), property.floatValue * 100f, 0f, 100f);
        if (EditorGUI.EndChangeCheck()) property.floatValue = percentage / 100f;
    }

    private string DescribeTarget()
    {
        if (EditorUtility.IsPersistent(weapon))
            return "Prefab asset: " + AssetDatabase.GetAssetPath(weapon) + (EditorApplication.isPlaying ? "\nAsset editing is disabled during Play Mode." : "\nChanges affect this prefab asset.");
        return (EditorApplication.isPlaying ? "PLAY MODE — temporary settings" : "Scene instance — changes stay on this instance") +
            "\n" + weapon.gameObject.scene.name + " / " + weapon.name;
    }

    private void CaptureForLater()
    {
        TuningSnapshot snapshot = Capture(weapon);
        if (!GlobalObjectId.TryParse(snapshot.targetId, out GlobalObjectId id) || id.targetObjectId == 0 ||
            string.IsNullOrEmpty(weapon.gameObject.scene.path))
        {
            status = "This weapon has no saved scene identity. Save the scene before Play Mode, then capture again.";
            return;
        }
        SessionState.SetString(PendingKey, JsonUtility.ToJson(snapshot));
        status = "Tuning captured for " + snapshot.name + ". Stop Play Mode, then choose Apply captured tuning.";
    }

    private void DrawPending()
    {
        TuningSnapshot pending = ReadPending();
        if (pending == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Captured tuning: " + pending.name + "\n" + (EditorApplication.isPlaying
            ? "Stop Play Mode to apply this capture." : "Applies only to the original weapon. Ammo and references are preserved."), MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || Resolve(pending) == null))
                if (GUILayout.Button("Apply captured tuning"))
                {
                    Guns target = Resolve(pending);
                    SetWeapon(target);
                    Apply(target, pending, "Apply captured weapon tuning");
                    SessionState.EraseString(PendingKey);
                    status = "Captured tuning applied. Save the scene or prefab to keep it on disk.";
                }
            if (GUILayout.Button("Discard capture")) SessionState.EraseString(PendingKey);
        }
        if (!EditorApplication.isPlaying && Resolve(pending) == null)
            EditorGUILayout.HelpBox("Open the original scene or prefab to resolve this capture. It will not be applied to another weapon.", MessageType.Warning);
    }

    private static TuningSnapshot ReadPending()
    {
        string json = SessionState.GetString(PendingKey, "");
        return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<TuningSnapshot>(json);
    }

    private static Guns Resolve(TuningSnapshot snapshot)
    {
        if (snapshot == null || !GlobalObjectId.TryParse(snapshot.targetId, out GlobalObjectId id)) return null;
        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Guns;
    }

    private static TuningSnapshot Capture(Guns target)
    {
        TuningSnapshot snapshot = new TuningSnapshot
        {
            targetId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString(), name = target.name
        };
        SerializedObject data = new SerializedObject(target);
        data.Update();
        foreach (string path in TuningPaths)
        {
            SerializedProperty property = data.FindProperty(path);
            TuningValue value = new TuningValue { path = path, type = property.propertyType };
            if (property.propertyType == SerializedPropertyType.Float) value.number = property.floatValue;
            else if (property.propertyType == SerializedPropertyType.Integer) value.integer = property.intValue;
            else if (property.propertyType == SerializedPropertyType.Vector2) value.vector = property.vector2Value;
            snapshot.values.Add(value);
        }
        return snapshot;
    }

    private static void Apply(Guns target, TuningSnapshot snapshot, string undoName)
    {
        if (target == null || snapshot == null) return;
        Undo.SetCurrentGroupName(undoName);
        SerializedObject data = new SerializedObject(target);
        data.Update();
        foreach (TuningValue value in snapshot.values)
        {
            SerializedProperty property = data.FindProperty(value.path);
            if (property == null || property.propertyType != value.type) continue;
            if (value.type == SerializedPropertyType.Float) property.floatValue = value.number;
            else if (value.type == SerializedPropertyType.Integer) property.intValue = value.integer;
            else if (value.type == SerializedPropertyType.Vector2) property.vector2Value = value.vector;
        }
        if (data.ApplyModifiedProperties()) MarkChanged(target);
    }

    private static void MarkChanged(Guns target)
    {
        if (EditorApplication.isPlaying) return;
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        if (!EditorUtility.IsPersistent(target) && target.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
    }

    private void SaveTarget()
    {
        if (EditorUtility.IsPersistent(weapon)) AssetDatabase.SaveAssetIfDirty(weapon);
        else if (PrefabStageUtility.GetCurrentPrefabStage() != null &&
            weapon.gameObject.scene == PrefabStageUtility.GetCurrentPrefabStage().scene)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
        }
        else if (!EditorSceneManager.SaveScene(weapon.gameObject.scene))
        {
            status = "Scene save was cancelled or failed. The tuning remains unsaved.";
            return;
        }
        status = "Saved " + weapon.name + ".";
    }
}
