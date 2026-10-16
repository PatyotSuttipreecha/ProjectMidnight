# THUNG Decorations Library

## Reusable Placement Data (SO)

`PlacementData/Decoration_Camp.asset` and `PlacementData/Decoration_Plant.asset` are example names for reusable prefab sets; no populated sets are included in this update. Create more with **Assets > Create > System Modification > TerrainTools > Decoration Placement Data** or **New Placement Data** in the browser.

1. Select a Data asset, click **Browse / Add Assets**, then search/filter the catalog and **Add selected to Data Asset**. The **In Data** filter displays catalog entries already in the target set. **Remove selected from Data** removes references with Undo. Save Data after editing.
2. The Data Inspector shows previews, per-entry weights, categories/tags and a search field. Drop prefab/model assets directly into it, or edit individual slots. Duplicate imports are skipped.
3. Assign the SO to **Placement Data (SO)** in the Placer's **Assets / Area** tab, or use **Use Data Asset on Target Placer** in the browser. Build Preview and Generate use that SO directly. Changing the source does not regenerate or remove scene output until you run generation.
4. Area, grouping, Terrain filters, scale and rotation remain on the scene Placer. A shared Data asset supplies only the prefab set and weights. Changes to shared data affect every Placer using it and invalidate an existing Preview. Clear the SO field to resume the preserved local list. **Save Local List as Placement Data** creates a reusable SO from a local list.

An assigned empty set or a set with no positive weights reports an error; it does not silently use the old local list. Data assets contain direct prefab references and are available outside the Editor assembly. The searchable catalog remains an Editor-only discovery tool.

Open **System Modification > TerrainTools > Decorations Library**, click **Browse Decorations Library** in the Placer's Assets / Area tab. The default catalog path is `Editor/Libraries/THUNG Decorations Library.asset` under this tool folder; create a catalog with New before adding your own prefabs.

The starter catalog contains 363 prefab references from `AssetModel/Decorate_Asset` and `DesignAssetPool/Big/Model/Prefab`. Categories are initial suggestions derived from names; edit them to suit your workflow. The catalog stores GUIDs, so moving a prefab keeps its reference. Source prefabs are loaded on demand for visible thumbnails or selected actions rather than loading the full catalog's art at startup.

## Find and use assets

- Search matches name, custom display label, category, tags and current asset path. All search words must match (case insensitive).
- Filter by category or Favorites. Click a card, use its checkbox, or Ctrl-click for multiple selections. Select results selects all current matches.
- Double-click or **Ping** locates the source asset in Project. Drag a card/selection into the Scene for ordinary Unity placement, or into the Placer's existing prefab drop area.
- Choose **Target Placer**, then **Add selected to Placer**. This appends missing prefabs with Weight 100 to its assigned Data SO, or to its local list if no SO is assigned. Existing prefab order/weights and generated scene objects stay intact. Use the Placer's Build Preview/Generate workflow when ready. Undo restores the prefab-list change. Save the Data asset after edits to a shared set.

## Maintain the catalog

Select a card to edit its display label, category and tags; the star toggles Favorite. These fields belong to the Library Asset, not the prefab. Click **Save** to persist catalog metadata.

To change categories in bulk, check several cards (or Ctrl-click / Select results). The details panel becomes **Edit Category — N selected**. Enter a category or choose **Existing…**, then **Apply to Selected**. All checked entries are included, even if hidden by search/filters. The category filter returns to All after applying. One Undo restores the previous categories; labels, tags, favorites, source prefabs and Placement Data SO metadata are preserved. Save Library when finished.

Expand **Manage library / Add assets** to drag in prefab/model assets or Project folders, add the current Project selection, or configure Source Folders. **Scan Source Folders** adds newly found references and preserves existing custom metadata; duplicate GUIDs are skipped. Cancelling a scan leaves the catalog unchanged. **Refresh** updates current references and thumbnails; it does not rescan source folders or remove missing entries.

**Remove from Library** removes selected references only and supports Undo. It does not delete source prefabs. Missing assets remain visible so they can be identified/removed. **New** creates a separate Library Asset; you can also use Assets > Create > System Modification > TerrainTools > Decorations Library.

This is an Editor catalog of prefab/model assets. It does not convert assets to G3, bake Quantum data, or verify gameplay configuration. Prefabs containing gameplay/Quantum components still need their existing placement/bake workflow. Do not treat a thumbnail as proof that a prefab is configured for gameplay.

All Library code, starter data, documentation and validation output live inside `Assets/Script/TerrainTools`. No source art assets are copied or edited.
