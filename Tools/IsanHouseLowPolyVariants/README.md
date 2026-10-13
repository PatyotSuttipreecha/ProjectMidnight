# Two additional low-poly Isan houses

These use the same old Isan timber-house language as the original GDD-based house. The original house and its GUIDs are unchanged.

- `Assets/Prefab/Environment/IsanHouseCottageLowPoly/Prefab_Environment_IsanHouse_Cottage_LowPoly.prefab`: smaller 8 × 6.8 m main footprint, 1.8 m floor height, compact front veranda, open interior, weathered brown roof. Entry width approximately 1.52 m. Front and rear stairs included. Narrower plan may require camera adjustment.
- `Assets/Prefab/Environment/IsanHouseSideWorkshopLowPoly/Prefab_Environment_IsanHouse_SideWorkshop_LowPoly.prefab`: 10 × 8 m main footprint, raised 3 × 6 m side workshop with lean-to roof and independent front stair, open living area and rear storage partition. Front and rear stairs included.

Root transforms have unit scale, +Y up and front toward -Z. Floor surface is 1.8 m above the origin. Place on suitable scene ground and test stairs with the actual CharacterController. The cottage plan scaling resides on section transforms; retain it unless intentionally resizing. Colliders are supplied, NavMesh must be baked after scene placement.

Both prefabs have separate roof, doors, furniture and placement-marker groups. Doors remain posed open without runtime interaction. Evidence markers remain empty transforms; they do not include loot or mission logic. Side workshop lean-to roof is under `10_SideWorkshop` and can be hidden separately for interior inspection.

Native URP materials and combined mesh assets, no runtime scripts or ProBuilder dependency. Generator sources remain in Tools. Each variant was generated in its own isolated Library project and includes exterior/interior render previews. Neither overwrites gameplay scenes.
