# Procedural Decoration Placer - exported tools

Exported from Unity 6000.3.11f1. This package contains the tool scripts and documentation, with original script GUIDs preserved. It includes Box/Polygon areas, Preview, renderer grounding, groups, Terrain layer edges, reusable Placement Data SOs, the searchable Library and Decoration Terrain Follow.

Import with Assets > Import Package > Custom Package. Open System Modification > TerrainTools > Procedural Decoration Placer and create a scene tool. Assign a Terrain and add your own prefabs, or create Assets > Create > System Modification > TerrainTools > Decoration Placement Data, fill it, then assign Placement Data (SO).

Open System Modification > TerrainTools > Decorations Library, click New to create a catalog, then add your own Project folders/prefabs under Manage library. No PigNWolf art, scenes, Camp/Plant SOs or pre-populated Library asset are included. The export also excludes the broader THUNG Toolbox and unrelated Terrain tools; use the direct menu entries above.

For Polygon, choose Assets / Area > Area Shape > Polygon. Edit Boundary in Scene, Shift-click on the assigned Terrain to add pivots, drag handles, then Build Preview > Generate This Preview. TerrainCollider is required for click placement. Auto-follow is at System Modification > TerrainTools > Decoration Auto Follow Terrain.

Checks: the exact 14 exported C# files compiled as isolated runtime/editor assemblies against Unity 6000.3.11f1 built-in assemblies only, without Quantum/G3/project scripts. Archive contents and source/meta SHA-256 hashes were verified. A fresh Unity-project import and rendering on other Unity versions have not been tested.

If these scripts already exist in the destination project, review the import list: matching original GUIDs update those existing scripts. No existing Library or Placement Data assets are overwritten by this export.
