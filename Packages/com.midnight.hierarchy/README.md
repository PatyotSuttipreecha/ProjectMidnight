# Midnight Hierarchy — preview 0.2.0

Original Editor extension targeting Unity **6000.2.4f1**. This is not vHierarchy
and does not include its code, assets, or license. Embedded UPM package: Unity
loads this folder automatically when the project opens.

## Start

Open **Tools > Midnight > Hierarchy**. Dock the tools window beside Hierarchy.
The Navigate tab contains scene selection, object bookmarks and Collapse all.
Unlike an injected toolbar, navigation is in this separate dockable window.

- **Alt-right-click a Hierarchy row**: apply a palette style, divider style,
  clear a style, or bookmark the object. If the row is selected, styling applies
  to all selected scene GameObjects.
- **Appearance**: individually disable features or disable the complete overlay.
- **Palette**: edit names, RGBA colors, built-in icon names or custom texture GUIDs.
  Assign a texture through the entry picker below the list. Import/export JSON.
  JSON references textures by GUID; copy textures with their `.meta` files when
  transferring a palette to another project.
- **Component minimap**: icons on the right, bounded by available row space.
  Alt-click opens the component's actual Inspector in a floating utility window.
  Custom inspectors are supported through `Editor.CreateCachedEditor`. No drag
  to pin behavior; utility windows already remain open.
- **Activation toggle**: right edge checkbox changes `activeSelf`, with Undo and
  prefab instance modification recording. Parent inactivity still affects
  `activeInHierarchy` normally.
- **Create folder/divider**: ordinary empty GameObjects under the selected
  Transform, or in the active scene if nothing is selected. These retain normal
  Transform/runtime behavior; there is no special build-time stripping.

## Hover shortcuts

Keep the pointer on a Hierarchy GameObject row (no selection required).
Shortcuts do not run while Unity reports that a text field is being edited.

| Key | Action |
| --- | --- |
| D | Set Unity's native Default Parent to hovered object |
| A | Toggle activeSelf, with Undo |
| F | Frame child renderer bounds, or the object's position |
| X | Delete hovered object and descendants, with confirmation and Undo |
| E | Toggle expansion of hovered object |
| Shift-E | Collapse other objects, reveal ancestors, expand hovered object |
| Ctrl-Shift-E | Collapse all GameObject rows; keep scene headers open |

Expansion and Default Parent call internal Unity 6000.2 APIs through guarded
reflection. Missing APIs produce one warning and leave the feature unavailable.
This requires revalidation after upgrading Unity. Other commands use public APIs.
Reference signatures were checked against Unity's 6000.2 reference source:
https://github.com/Unity-Technologies/UnityCsReference/blob/6000.2/Editor/Mono/SceneHierarchy.cs

## Data and prefab behavior

Decorations, bookmarks, toggles and palette are stored in
`ProjectSettings/MidnightHierarchy.asset`, created on the first change. Include
this file in version control to share the setup. It is one shared settings file,
so concurrent team edits can conflict. There is no scene-embedded storage option.

Scene decorations use GlobalObjectId. Save scenes before assigning styles or
bookmarks. Prefab Mode requires saving pending changes first; preview objects
are mapped to saved prefab asset objects. Prefab instances fall back to their
source object's style when they have no explicit instance style. Clearing an
instance style restores inheritance. Added/renamed/reordered prefab objects must
be saved before styling. Nested/variant prefabs require the validation below;
this preview does not promise complete inheritance across every variant chain.

Object bookmarks resolve only while their scenes are loaded. Missing/unloaded
bookmarks remain in the list and can be removed. Scene bookmarks use asset GUIDs
so moving a scene with its meta file preserves the bookmark. Switching scenes
prompts for unsaved edits. Scene switching is disabled in Play Mode and asks you
to leave Prefab Mode first. Persistent styling/bookmark edits are disabled in
Play Mode; ordinary component/Active edits follow Unity's normal play-mode rules.

## Performance and removal

Visible GameObject decorations/components are cached and invalidated on hierarchy,
project, play-mode and undo changes. No update-loop polling. No performance claim
has been measured yet. Appearance has a manual cache refresh if a custom tool
changes data without Unity's usual change events. All compiled code is restricted
to Editor through the assembly definition; no runtime component is added.

To remove: delete this package folder. Optionally remove
`ProjectSettings/MidnightHierarchy.asset`. Created empty groups remain normal
GameObjects; any intentional Active/component/deletion edits remain real edits.

## Validation status

Installed locally on 2026-10-31 in F:/Unity/ProjectMidnight for Unity 6000.2.4f1.
Source was recovered from the original cloud Tools chat, including its follow-up patches.
Unity's C# compiler passes, and the running project imported the package and produced
Library/ScriptAssemblies/Midnight.Hierarchy.Editor.dll successfully.
Existing project warnings remain. Interactive Undo/Redo, nested prefab inheritance,
keyboard navigation and performance checks below have not all been exercised.

Before relying on it in production, open a test scene in Unity 6000.2.4f1:

1. Wait for import; confirm Console has no compile errors and the Tools menu opens.
2. Style two selected saved objects, then Undo/Redo; reopen the project and verify.
3. Save a prefab, style a child in Prefab Mode, return to a scene and verify source
   inheritance. Test nested prefabs, variants and an instance style override.
4. Toggle Active with checkbox/A and Undo. Save/reload and verify prefab overrides.
5. Alt-click built-in and custom components, edit values and Undo; delete the
   component while its utility window is open and confirm graceful handling.
6. Bookmark objects/scenes; rename/move scenes with their meta files. Switch away
   from a dirty scene, cancel the save prompt and verify it remains open.
7. Verify E, Shift-E, Ctrl-Shift-E and D without changing the current selection.
   Rename an object and type A/D/E/F/X to confirm no shortcuts run while typing.
8. Export/import a palette and custom texture; import malformed JSON and confirm
   the current palette is retained. Undo a successful import.
9. Create groups in an additive scene and Prefab Mode; Undo creation. Check Play
   Mode entry/exit and the global overlay switch.
10. Profile Editor repaint/GC with 10,000 objects, scrolling and selection, with
    features enabled and disabled. Confirm a player build excludes this assembly.

## 0.2.0: visible styling

- Tools > Midnight > Hierarchy opens on Style: select objects and use color swatches,
  custom colors, built-in/custom icons, divider headers or saved palette entries.
- Alt-left-click a row opens the same controls; Alt-click component icons still opens
  their inspector. Alt-right-click retains the context menu.
- === section headers get automatic category colors without renaming or scene edits.
  Disable Auto Group Colors in Appearance if preferred. Ordinary objects are not
  automatically assigned random colors.
- Color Strength controls full-width row tint; an accent strip and redrawn label keep
  styled rows readable. Existing explicit colors take precedence over auto headers.
- Navigation remains in the dockable tools window, not an injected native toolbar.
  This is our own extension, not the vHierarchy 2 implementation.

## Recursive colors

Hold Alt while clicking a color/custom-color/palette button, or enable Include
Children (colors). Every descendant, including inactive objects, receives the
color. Overlapping selected roots are deduplicated. Existing per-object icons
and divider settings are preserved. One Undo restores the entire color operation.
Clear color (keep icons) follows the same recursive option. This writes explicit
colors to existing descendants; children created later do not automatically inherit.
Verified with 6 isolated Unity checks for descendants/dedup, inactive objects,
icon preservation, Undo, Redo and nonrecursive isolation.

## Gradient row colors

Appearance > Gradient Colors fades each row from left to right. Gradient End
Opacity controls the right edge; Child Color Strength softens descendants sharing
their parent's color. Native labels remain single-layer. The fade texture is cached
and rebuilt only when its end opacity changes. Disable Gradient Colors for flat tint.
