using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PointNoteVisibility
{
    private const string MenuPath = "System Modification/Scene Configuration/Show Point Notes";
    private static string PreferenceKey => "PointNote.Visible." + Application.dataPath;
    public static bool Visible => EditorPrefs.GetBool(PreferenceKey, true);

    static PointNoteVisibility()
    {
        PointNote.VisibilityResolver = () => Visible;
    }

    [MenuItem(MenuPath)]
    public static void Toggle()
    {
        EditorPrefs.SetBool(PreferenceKey, !Visible);
        SceneView.RepaintAll();
        foreach (var window in Resources.FindObjectsOfTypeAll<SceneNotesWindow>()) window.Repaint();
    }

    [MenuItem(MenuPath, true)]
    private static bool Validate()
    {
        Menu.SetChecked(MenuPath, Visible);
        return true;
    }
}
