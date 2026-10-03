# Current mechanic diagrams

Created 2 October 2026 for section 7 of the online GDD. These are explanatory diagrams based on the current scripts, not gameplay screenshots or Play Mode validation.

- `movement.png`: camera-relative movement, sprint, aiming, and inventory control lock.
- `combat.png`: aim, click to fire, reload, spread, breathing sway, and hitbox damage.
- `enemy.png`: patrol, sight checks, pursuit, attack, and return to patrol.
- `inventory.png`: pickup, direct ammunition routing, inventory grid, and healing.

Regenerate with `create-diagrams.ps1` on Windows (System.Drawing and Tahoma). Numeric configuration may be overridden in Unity's Inspector.

Primary sources: `PlayerController.cs`, `Guns.cs`, `Bullet.cs`, `EnemyController.cs`, `HitBoxManager.cs`, and the inventory/pickup scripts under `Assets/Script/Inventory&ItemSO`.
