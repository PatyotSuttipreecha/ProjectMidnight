using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
[FilePath("ProjectSettings/PointNoteColors.asset", FilePathAttribute.Location.ProjectFolder)]
public class PointNotePalette : ScriptableSingleton<PointNotePalette>
{
    [Serializable] private class Entry { public PointNote.PointType type; public PointNote.NoteColors colors; }
    [SerializeField] private List<Entry> entries = new List<Entry>();
    static PointNotePalette()
    {
        PointNote.PaletteResolver = type => instance.GetColors(type);
        Undo.undoRedoPerformed += () => { instance.Save(true); SceneView.RepaintAll(); };
    }
    private PointNote.NoteColors GetColors(PointNote.PointType type)
    {
        foreach (var entry in entries) if (entry.type == type) return entry.colors;
        return PointNote.DefaultColors(type);
    }
    public static void DrawSettings()
    {
        EditorGUILayout.HelpBox("Shared project colors. Marker, label background and label text can be set per point type. Individual notes can override these colors.", MessageType.Info);
        foreach (PointNote.PointType type in Enum.GetValues(typeof(PointNote.PointType)))
        {
            var colors = instance.GetColors(type);
            EditorGUILayout.LabelField(type.ToString(), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            Color marker = EditorGUILayout.ColorField("Marker", colors.marker);
            Color background = EditorGUILayout.ColorField("Label background", colors.background);
            Color text = EditorGUILayout.ColorField("Label text", colors.text);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(instance, "Change Point Note Colors");
                var entry = instance.entries.Find(value => value.type == type);
                if (entry == null) { entry = new Entry { type = type }; instance.entries.Add(entry); }
                entry.colors = new PointNote.NoteColors { marker = marker, background = background, text = text };
                instance.Save(true); SceneView.RepaintAll();
            }
            if (GUILayout.Button("Reset " + type + " colors", EditorStyles.miniButton))
            {
                Undo.RecordObject(instance, "Reset Point Note Colors");
                instance.entries.RemoveAll(entry => entry.type == type); instance.Save(true); SceneView.RepaintAll();
            }
        }
    }
}
