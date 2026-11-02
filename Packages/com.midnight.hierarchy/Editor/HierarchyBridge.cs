using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Midnight.Hierarchy
{
    // Isolate version-dependent calls. No modification of Unity internals or global selection.
    internal static class HierarchyExpansion
    {
        internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static readonly Type HierarchyType = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchy");
        private static readonly Type WindowType = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
        private static readonly MethodInfo Expanded = HierarchyType?.GetMethod("GetExpandedIDs", Flags);
        private static readonly MethodInfo Expand = HierarchyType?.GetMethod("ExpandTreeViewItem", Flags);
        private static bool warned;

        internal static object Current()
        {
            var window = EditorWindow.focusedWindow;
            if (!window || window.GetType() != WindowType)
                window = WindowType?.GetProperty("lastInteractedHierarchyWindow", Flags)?.GetValue(null) as EditorWindow;
            return window ? WindowType?.GetProperty("sceneHierarchy", Flags)?.GetValue(window) : null;
        }

        internal static bool Handle(GameObject target, bool only, bool all)
        {
            try
            {
                var hierarchy = Current();
                if (hierarchy == null || Expanded == null || Expand == null) return Unavailable();
                var expanded = (int[])Expanded.Invoke(hierarchy, null);
                int id = target ? target.GetInstanceID() : 0;
                bool wasExpanded = Array.IndexOf(expanded, id) >= 0;
                // Mutating the tree while its rows are being enumerated is unsafe.
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        if (only || all)
                            foreach (int item in expanded)
                                if (EditorUtility.InstanceIDToObject(item) is GameObject)
                                    Expand.Invoke(hierarchy, new object[] { item, false });
                        if (!all && target)
                        {
                            for (var p = target.transform.parent; p; p = p.parent)
                                Expand.Invoke(hierarchy, new object[] { p.gameObject.GetInstanceID(), true });
                            Expand.Invoke(hierarchy, new object[] { id, only || !wasExpanded });
                        }
                        EditorApplication.RepaintHierarchyWindow();
                    }
                    catch (Exception exception) { Warn(exception.Message); }
                };
                return true;
            }
            catch (Exception exception) { Warn(exception.Message); return false; }
        }

        private static bool Unavailable() { Warn("Hierarchy navigation API is unavailable in this Unity version."); return false; }
        internal static void Warn(string message)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("Midnight Hierarchy: " + message);
        }
    }

    internal static class DefaultParent
    {
        internal static void Set(GameObject go)
        {
            if (!go || (PrefabStageUtility.GetPrefabStage(go)?.prefabContentsRoot == go)) return;
            try
            {
                var method = HierarchyExpansion.HierarchyType?.GetMethod("SetDefaultParentObject", HierarchyExpansion.Flags,
                    null, new[] { typeof(bool), typeof(GameObject) }, null);
                if (method == null) { HierarchyExpansion.Warn("Default Parent API is unavailable."); return; }
                method.Invoke(null, new object[] { false, go });
                EditorApplication.RepaintHierarchyWindow();
            }
            catch (Exception exception) { HierarchyExpansion.Warn(exception.Message); }
        }
    }
}
