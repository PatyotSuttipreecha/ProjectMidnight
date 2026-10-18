using UnityEditor;
using UnityEngine;

public sealed class EnemyCombatSettingsWindow : EditorWindow
{
    private const string SettingsPath = "Assets/Data/Enemies/Resources/EnemyCombatSettings.asset";
    private static readonly string[] Tabs = { "Approach", "Pressure", "Attack" };
    private EnemyCombatSettings settings;
    private SerializedObject serialized;
    [SerializeField] private Vector2 scroll;
    [SerializeField] private int tab;
    [SerializeField] private bool showAsset;
    private GUIStyle titleStyle, subtitle, section, hint, card, metric, metricCaption, badge, tabStyle;
    private bool styledForDark;
    private Color Accent => new Color(.24f, .78f, .71f);
    private Color Muted => EditorGUIUtility.isProSkin ? new Color(.65f, .70f, .76f) : new Color(.36f, .40f, .46f);
    private Color CardColor => EditorGUIUtility.isProSkin ? new Color(.20f, .22f, .25f) : new Color(.94f, .95f, .97f);

    [MenuItem("System Modification/Enemy Lab/Shared Combat Settings")]
    public static void Open()
    {
        var window = GetWindow<EnemyCombatSettingsWindow>("Combat Settings");
        window.minSize = new Vector2(440, 520);
        window.Resolve();
        window.Show();
        window.Focus();
    }

    private void Resolve()
    {
        var asset = EnemyCombatSettings.Shared;
        if (asset == null) asset = AssetDatabase.LoadAssetAtPath<EnemyCombatSettings>(SettingsPath);
        if (asset == settings && serialized != null) return;
        settings = asset;
        serialized = settings != null ? new SerializedObject(settings) : null;
    }
    private void OnEnable()
    {
        minSize = new Vector2(440, 520);
        Resolve();
        Undo.undoRedoPerformed += Repaint;
    }
    private void OnDisable() => Undo.undoRedoPerformed -= Repaint;
    private void OnFocus() { Resolve(); Repaint(); }

    private void Styles()
    {
        if (titleStyle != null && styledForDark == EditorGUIUtility.isProSkin) return;
        styledForDark = EditorGUIUtility.isProSkin;
        titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 23, wordWrap = true };
        subtitle = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true };
        subtitle.normal.textColor = Muted;
        section = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, wordWrap = true };
        hint = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11, padding = new RectOffset(0, 0, 2, 5) };
        hint.normal.textColor = Muted;
        card = new GUIStyle { padding = new RectOffset(16, 16, 14, 14), margin = new RectOffset(0, 0, 0, 10) };
        card.normal.background = EditorGUIUtility.whiteTexture;
        metric = new GUIStyle(EditorStyles.boldLabel) { fontSize = 23, alignment = TextAnchor.MiddleLeft };
        metricCaption = new GUIStyle(subtitle) { fontSize = 10 };
        badge = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight };
        badge.normal.textColor = Accent;
        tabStyle = new GUIStyle(EditorStyles.toolbarButton) { fixedHeight = 30, fontSize = 12 };
    }

    private void OnGUI()
    {
        Styles();
        if (settings == null) Resolve();
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * .40f, 160, 230);
        try
        {
            GUILayout.BeginVertical();
            DrawHeader();
            if (settings == null || serialized == null)
            {
                GUILayout.Space(12);
                EditorGUILayout.HelpBox("Combat settings could not be loaded. Expected: " + SettingsPath, MessageType.Error);
                if (GUILayout.Button("Reimport settings asset", GUILayout.Height(30)))
                {
                    AssetDatabase.ImportAsset(SettingsPath, ImportAssetOptions.ForceUpdate);
                    Resolve();
                }
                GUILayout.EndVertical();
                return;
            }

            serialized.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(18, 18, 14, 6) });
            DrawSummary();
            tab = GUILayout.Toolbar(Mathf.Clamp(tab, 0, Tabs.Length - 1), Tabs, tabStyle);
            Rect tabsRect = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(new Rect(tabsRect.x + tabsRect.width / Tabs.Length * tab,
                tabsRect.yMax - 2, tabsRect.width / Tabs.Length, 2), Accent);
            GUILayout.Space(14);
            if (tab == 0) DrawApproach();
            else if (tab == 1) DrawPressure();
            else DrawAttack();
            DrawAsset();
            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            serialized.ApplyModifiedProperties();
            DrawFooter();
            GUILayout.EndVertical();
        }
        finally { EditorGUIUtility.labelWidth = previousLabelWidth; }
    }

    private void DrawHeader()
    {
        Rect stripe = GUILayoutUtility.GetRect(0, 4, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(stripe, Accent);
        GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(20, 20, 16, 14) });
        GUILayout.BeginHorizontal();
        GUILayout.Label("ENEMY LAB", subtitle);
        GUILayout.FlexibleSpace();
        GUILayout.Label(Application.isPlaying ? "PLAY MODE / ASSET EDIT" : "SHARED COMBAT", badge);
        GUILayout.EndHorizontal();
        GUILayout.Label("Combat director", titleStyle);
        GUILayout.Space(3);
        GUILayout.Label("Shape how the group approaches, applies pressure and hands off individual stalled pursuers.", subtitle);
        GUILayout.EndVertical();
    }

    private SerializedProperty P(string name) => serialized.FindProperty(name);
    private void BeginCard(string heading, string description)
    {
        Color saved = GUI.backgroundColor;
        GUI.backgroundColor = CardColor;
        GUILayout.BeginVertical(card);
        GUI.backgroundColor = saved;
        GUILayout.Label(heading, section);
        if (!string.IsNullOrEmpty(description)) GUILayout.Label(description, hint);
        GUILayout.Space(5);
    }
    private static void EndCard() => GUILayout.EndVertical();
    private void DrawSummary()
    {
        Color saved = GUI.backgroundColor;
        GUI.backgroundColor = CardColor;
        GUILayout.BeginHorizontal(card);
        GUI.backgroundColor = saved;
        SummaryMetric(Mathf.Max(1, P("maxRushers").intValue).ToString(), "SHARED CAP");
        SummaryMetric((Mathf.Clamp01(P("approachRunChance").floatValue) * 100).ToString("0") + "%", "WILLING TO RUN");
        SummaryMetric(Mathf.Max(1, P("pressureDistance").floatValue).ToString("0.#") + " m", "CLAIM DISTANCE");
        GUILayout.EndHorizontal();
        GUILayout.Label("Random runners  >  First arrivals  >  Pressure ownership", hint);
        GUILayout.Space(5);
    }
    private void SummaryMetric(string value, string caption)
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Label(value, metric);
        GUILayout.Label(caption, metricCaption);
        GUILayout.EndVertical();
    }
    private void Toggle(string path, string label, string description)
    {
        var property = P(path);
        property.boolValue = EditorGUILayout.Toggle(new GUIContent(label, property.tooltip), property.boolValue);
        GUILayout.Label(description, hint);
        GUILayout.Space(4);
    }
    private void Number(string path, string label, float minimum, string description)
    {
        var property = P(path);
        EditorGUI.BeginChangeCheck();
        var content = new GUIContent(label, property.tooltip);
        if (property.propertyType == SerializedPropertyType.Integer)
            property.intValue = EditorGUILayout.IntField(content, property.intValue);
        else property.floatValue = EditorGUILayout.FloatField(content, property.floatValue);
        if (EditorGUI.EndChangeCheck())
        {
            if (property.propertyType == SerializedPropertyType.Integer) property.intValue = Mathf.Max(Mathf.CeilToInt(minimum), property.intValue);
            else property.floatValue = Mathf.Max(minimum, property.floatValue);
        }
        GUILayout.Label(description, hint);
        GUILayout.Space(5);
    }
    private void DrawApproach()
    {
        BeginCard("01  /  Approach mode", "Applies to enemies targeting the same player across profiles and spawn groups.");
        Toggle("useRushSlots", "Shared coordination", "Use random incoming runners and first-arrival pressure ownership.");
        if (!P("useRushSlots").boolValue)
            EditorGUILayout.HelpBox("Legacy chase is active. Each Enemy Profile uses its saved Walk / Boost settings. Attack Start Interval still applies.", MessageType.Info);
        EndCard();
        using (new EditorGUI.DisabledScope(!P("useRushSlots").boolValue))
        {
            BeginCard("02  /  Who starts running?", "Running does not reserve pressure ownership. A walker can arrive first.");
            Number("maxRushers", "Owners + incoming runners", 1, "Shared total cap, including enemies already circling or attacking.");
            var chance = P("approachRunChance");
            EditorGUI.BeginChangeCheck();
            float percent = EditorGUILayout.Slider(new GUIContent("Approach run chance (%)", chance.tooltip), chance.floatValue * 100, 0, 100);
            if (EditorGUI.EndChangeCheck()) chance.floatValue = percent / 100;
            GUILayout.Label("Rolled once per enemy per round. 0% = everyone walks; 100% = everyone is willing, within the cap.", hint);
            GUILayout.Space(5);
            var delay = P("rushStartDelay");
            EditorGUILayout.LabelField(new GUIContent("Stagger between starts (s)", delay.tooltip), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 34;
            GUILayout.BeginHorizontal();
            float minimum = EditorGUILayout.FloatField("Min", delay.vector2Value.x);
            GUILayout.Space(10);
            float maximum = EditorGUILayout.FloatField("Max", delay.vector2Value.y);
            GUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = labelWidth;
            if (EditorGUI.EndChangeCheck()) delay.vector2Value = new Vector2(Mathf.Max(.05f, minimum), Mathf.Max(Mathf.Max(.05f, minimum), maximum));
            GUILayout.Label("One runner starts at a time. The same stagger applies on first detection and replacement.", hint);
            EndCard();
        }
    }
    private void DrawPressure()
    {
        BeginCard("Front interception", "Nearby enemies ahead of actual walking/sprint movement can claim an approach slot before melee range. Admission uses the shared replacement interval and cap.");
        Number("frontInterceptDistance", "Front opportunity range (m)", 1, "Requires sight and a complete NavMesh path. Replaces an uncommitted rear owner, without a group escape pause.");
        Number("frontInterceptAngle", "Movement cone angle", 10, "Full angle centred on actual player movement, not camera direction. Runtime clamps to 160 degrees.");
        EndCard();
        BeginCard("Pressure handoff", "Walking away transfers stale ownership instead of resetting the whole group. Close defenders get priority; committed attacks stay protected.");
        Number("pursuitHandoffDelay", "Growing-gap duration (s)", .1f, "Yield a distant uncommitted owner after this sustained pursuit window.");
        Number("pursuitHandoffGap", "Minimum gap growth (m)", .1f, "Distance must grow by at least this much; equal-speed pursuit keeps its slot.");
        Number("pressureRetreatBudget", "Pressure backstep budget (m)", 0, "Maximum deliberate outward movement per Pressure episode. Stops endless retreat when walked into.");
        EndCard();

        using (new EditorGUI.DisabledScope(!P("useRushSlots").boolValue))
        {
            BeginCard("01  /  First arrivals earn a place", "Owners keep their place through circling, preparation, attacks and recovery.");
            Number("pressureDistance", "Claim distance (m)", 1, "Enter this radius on a valid path to claim pressure ownership. Walkers and runners are equal.");
            Number("replacementInterval", "Replacement interval (s)", 0, "Wait between replacement admissions. Distant runners do not reserve a place.");
            EndCard();
            BeginCard("02  /  Release and retry", "An Action Break, death, pause or blocked pursuit frees only that enemy's place.");
            Number("rushReentryDelay", "Reentry cooldown (s)", 0, "Rest before an interrupted enemy can request another place.");
            Number("rushStallTimeout", "Pursuit stall timeout (s)", .5f, "Release distant pursuers that stop making progress. Deliberate circling and attacks are excluded.");
            EndCard();
        }
    }
    private void DrawAttack()
    {
        BeginCard("01  /  Space attack starts", "Attack timing is separate from pressure ownership and runner selection.");
        Number("attackStartInterval", "Attack start interval (s)", 0, "Minimum time between different enemies entering Preparation against the same player. 0 disables spacing.");
        EndCard();
        BeginCard("Charge / committed melee", "One preparing or running charge per player. Uses shared melee turns and start spacing. Stone users do not charge.");
        Toggle("enableCharge", "Enable charge", "Telegraph, snapshot destination, run, then melee or recover on a miss.");
        EditorGUILayout.HelpBox("Individual charge distances, speed, timing and animation states: Enemy Profile > Combat > Charge. Mid-range charge may request an attack turn without pressure ownership.", MessageType.Info);
        Number("chargePostHoldGrace", "After-lock grace (s)", 0, "Block group attack starts during restraint and this grace period. Event can call EnemyPlayerEffects.EndChargeLock().");
        Number("chargeHoldDuration", "Hit restraint (s)", 0, "Confirmed Lost combat hits only. Zero disables movement restraint.");
        Toggle("chargeLockMovement", "Lock movement on hit", "Freeze movement during confirmed contact. Camera look and attacks pause during the hold.");
        Number("chargeCameraFov", "Hold camera FOV", 15, "Camera field of view during confirmed charge contact; restored after release.");
        Number("chargeHoldSpeedMultiplier", "Held movement multiplier", .25f, "Used when Lock movement on hit is disabled. 0.25 means 25% movement speed; camera look still pauses during contact.");
        EndCard();
        BeginCard("02  /  Tune individual attackers", "Attack turn limits, damage and animation timing belong to each Enemy Profile / Weapon Module.");
        GUILayout.Label("Different windups can still produce hits at the same time. This interval controls when preparation starts.", hint);
        if (GUILayout.Button("Open Enemy Lab", GUILayout.Height(28)))
            EditorApplication.ExecuteMenuItem("System Modification/Enemy Lab/Enemy Settings");
        EndCard();
    }
    private void DrawAsset()
    {
        showAsset = EditorGUILayout.Foldout(showAsset, "Asset & persistence", true);
        if (!showAsset) return;
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Shared asset", settings, typeof(EnemyCombatSettings), false);
        GUILayout.Label(AssetDatabase.GetAssetPath(settings), hint);
        GUILayout.Label("Changes are recorded on this asset and support Undo / Redo.", hint);
    }
    private void DrawFooter()
    {
        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Editing a shared asset in Play Mode. Changes persist after stopping Play.", MessageType.Warning);
        GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));
        GUILayout.Label(EditorUtility.IsDirty(settings) ? "Unsaved asset changes" : "Shared asset ready", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Locate", EditorStyles.toolbarButton, GUILayout.Width(60))) EditorGUIUtility.PingObject(settings);
        if (GUILayout.Button("Save settings", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            AssetDatabase.SaveAssetIfDirty(settings);
            ShowNotification(new GUIContent("Combat settings saved"));
            Repaint();
        }
        GUILayout.EndHorizontal();
    }
}
