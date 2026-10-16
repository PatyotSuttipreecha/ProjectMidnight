$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$validationRoot = Join-Path $projectRoot 'Library/EnemySpawningValidation'
# Reuse the isolated AI harness; never run tests against the user's open scene.
if (-not (Test-Path (Join-Path $validationRoot 'ProjectSettings'))) { throw 'Isolated EnemySpawningValidation project is required.' }
$runtimeSources = @('EnemyController.cs','EnemyAttackCoordinator.cs','EnemyCombatSettings.cs','EnemyRushCoordinator.cs','EnemyPlayerEffects.cs','EnemyStoneProjectile.cs','EnemyWeaponRuntime.cs','EnemySO/EnemyDataSO.cs','EnemySO/EnemyWeaponSO.cs','HitBox.cs','EnemyGroup.cs','EnemySpawnPoint.cs','EnemyStateDisplay.cs','EnemyLocomotionAnimator.cs')
foreach ($relativeSource in $runtimeSources) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "Assets/Script/$relativeSource") -Destination (Join-Path $validationRoot "Assets/$([IO.Path]::GetFileName($relativeSource))") -Force
    if (Test-Path (Join-Path $projectRoot "Assets/Script/$relativeSource.meta")) {
        Copy-Item -LiteralPath (Join-Path $projectRoot "Assets/Script/$relativeSource.meta") -Destination (Join-Path $validationRoot "Assets/$([IO.Path]::GetFileName($relativeSource)).meta") -Force
    }
}
$presetsDestination = Join-Path $validationRoot 'Assets/TheLostPresets'
$animationDestination = Join-Path $validationRoot 'Assets/TheLostAnimator'
New-Item -ItemType Directory -Path $animationDestination -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Art/Animation/TheLost') | Copy-Item -Destination $animationDestination -Recurse -Force
New-Item -ItemType Directory -Path $presetsDestination -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Data/Enemies/TheLost') | Copy-Item -Destination $presetsDestination -Recurse -Force
$stub = @'
using UnityEngine;
public class PlayerController : MonoBehaviour { public float currentHealth=100; public float walkNoiseRadius=3,runNoiseRadius=8; public Guns EquippedGun; public bool IsCutsceneInputLocked; public bool IsGameplayInputBlocked => IsCutsceneInputLocked; public bool IsSprinting; public Vector3 PlanarVelocity; public bool CheatInfiniteHealth; public string LastDamageResult; public void TakeDamage(float value){TryTakeDamage(value);} public bool TryTakeDamage(float value,string source="Damage"){ if(float.IsNaN(value)||float.IsInfinity(value)||value<=0||currentHealth<=0||!isActiveAndEnabled||IsCutsceneInputLocked||CheatInfiniteHealth){LastDamageResult="Blocked";return false;} float before=currentHealth;currentHealth=Mathf.Max(0,currentHealth-value);LastDamageResult="Hit";return currentHealth<before;} }
public class Guns : MonoBehaviour {public float gunshotNoiseRadius=25;}
public class HitBoxManager : MonoBehaviour {public float health;}
public class Bullet : MonoBehaviour {public void TryHit(Collider target){} }
'@
Set-Content -LiteralPath (Join-Path $validationRoot 'Assets/EnemyTestStubs.cs') -Value $stub -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LostModuleChecks.cs') -Destination (Join-Path $validationRoot 'Assets/Editor/LostModuleChecks.cs') -Force
foreach ($editorSource in @('EnemyProfileGUI.cs','EnemyLootEditor.cs','EnemyWeaponLabWindow.cs','EnemyCombatSettingsWindow.cs')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "Assets/Editor/$editorSource") -Destination (Join-Path $validationRoot "Assets/Editor/$editorSource") -Force
}
$unityExecutable = 'C:/Program Files/Unity/Hub/Editor/6000.2.4f1/Editor/Unity.exe'
& $unityExecutable -batchmode -projectPath $validationRoot -executeMethod LostModuleChecks.Begin -logFile (Join-Path $validationRoot 'lost-module.log') | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Unity validation failed ($LASTEXITCODE). See Library/EnemySpawningValidation/lost-module.log" }
Get-Content -LiteralPath (Join-Path $validationRoot 'lost-module-success.txt')
