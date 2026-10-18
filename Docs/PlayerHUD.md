# Player HUD

Prefab: `Assets/Prefab/UI/Resources/PlayerHUD.prefab`.

Open **System Modification > Player HUD > Open Prefab**, or **Player Configuration > Open Player HUD Prefab**. Move/resize the **Vitals and Equipment** RectTransform to change placement. Edit child images/text for appearance. Health/danger colours and threshold are configured on the root PlayerHUD component because health colours update at runtime.

- The circular Image uses a transparent ring sprite and Radial360 fill. Current/max health and the fill update directly from PlayerController.
- Equipment name/icon come from the equipped gun's bound ItemSO. Missing names fall back to weapon type; a missing icon is hidden.
- Ammunition displays magazine / reserve, with Reloading, reload prompt and no-ammo states. Knife/unarmed clear firearm ammunition.
- Inventory and cutscene gameplay input locks hide the HUD. It does not block clicks.
- PlayerController creates/binds it at startup. Repeated setup reuses a matching HUD. AmmoHUD remains for compatibility, but its old panel is retired while the combined HUD is active.
- The prefab builder creates a missing asset only; **Create or Rebuild Default Prefab** explicitly resets its layout. Opening it preserves edits.

Verification: runtime/editor compile passed. 15 isolated Unity checks used the production HUD and builder with player/gun data stubs, covering binding, live health, zero max health, equipment/ammo/reload/knife states and input-lock visibility. A Unity-rendered preview was visually inspected. Full gameplay integration still needs a scene playtest.
