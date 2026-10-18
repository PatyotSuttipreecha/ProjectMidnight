$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testRoot=Join-Path $root 'Library/PlayerHUDValidation'
New-Item -ItemType Directory -Force -Path "$testRoot/Assets/Editor", "$testRoot/Assets/Prefab/UI/Resources", "$testRoot/Packages", "$testRoot/ProjectSettings" | Out-Null
Copy-Item "$root/ProjectSettings/ProjectVersion.txt" "$testRoot/ProjectSettings/ProjectVersion.txt" -Force
'{"dependencies":{"com.unity.modules.ui":"1.0.0","com.unity.ugui":"2.0.0"}}' | Set-Content "$testRoot/Packages/manifest.json" -Encoding UTF8
Copy-Item "$root/Assets/Script/PlayerHUD.cs*" "$testRoot/Assets" -Force
Copy-Item "$root/Assets/Editor/PlayerHUDBuilder.cs*" "$testRoot/Assets/Editor" -Force
Copy-Item "$root/Assets/Prefab/UI/Resources/PlayerHealthRing.png*" "$testRoot/Assets/Prefab/UI/Resources" -Force
if(Test-Path "$root/Assets/Prefab/UI/Resources/PlayerHUD.prefab.meta") { Copy-Item "$root/Assets/Prefab/UI/Resources/PlayerHUD.prefab.meta" "$testRoot/Assets/Prefab/UI/Resources" -Force }
@'
using UnityEngine;
public class PlayerController : MonoBehaviour { public static PlayerController instance; public float currentHealth=100,maxHealth=100; public bool IsGameplayInputBlocked; public Guns EquippedGun; }
public class ItemSO { public string itemName; public Sprite icon; }
public class InventoryItemData { public ItemSO itemSO; }
public class Guns : MonoBehaviour { public enum WeaponType {None,Knife,Pistol,Shotgun,Rifle} public struct WeaponStat {public WeaponType weaponName; public int currentAmmo,ammoReserve;} public WeaponStat weaponStat; public bool isReloading; public InventoryItemData BoundInventoryItem; }
public class AmmoHUD : MonoBehaviour {public void Retire(){enabled=false;}}
'@ | Set-Content "$testRoot/Assets/Dependencies.cs" -Encoding UTF8
Copy-Item "$PSScriptRoot/PlayerHUDChecks.cs" "$testRoot/Assets/Editor/PlayerHUDChecks.cs" -Force
Remove-Item "$testRoot/success.txt" -ErrorAction SilentlyContinue
& 'C:/Program Files/Unity/Hub/Editor/6000.2.4f1/Editor/Unity.exe' -batchmode -projectPath $testRoot -executeMethod PlayerHUDChecks.Run -logFile "$testRoot/checks.log" | Out-Null
if($LASTEXITCODE -ne 0){throw "HUD verification failed: $testRoot/checks.log"}
Copy-Item "$testRoot/Assets/Prefab/UI/Resources/PlayerHUD.prefab*" "$root/Assets/Prefab/UI/Resources" -Force
Copy-Item "$testRoot/Assets/Prefab/UI/Resources/PlayerHealthRing.png.meta" "$root/Assets/Prefab/UI/Resources" -Force
Get-Content "$testRoot/success.txt"
