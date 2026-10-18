$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$validationRoot = Join-Path $projectRoot 'Library/WeaponValidation'
New-Item -ItemType Directory -Force -Path "$validationRoot/Assets/Editor", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$projectRoot/ProjectSettings/ProjectVersion.txt" -Destination "$validationRoot/ProjectSettings/ProjectVersion.txt" -Force
@'
{
  "dependencies": {
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.ugui": "2.0.0"
  }
}
'@ | Set-Content "$validationRoot/Packages/manifest.json" -Encoding UTF8
foreach ($sourceName in @('HitBoxManager', 'HitBox', 'Bullet', 'Crosshair')) {
    Copy-Item -LiteralPath "$projectRoot/Assets/Script/$sourceName.cs" -Destination "$validationRoot/Assets/$sourceName.cs" -Force
}
# Use the production geometry methods verbatim, without the unrelated inventory/audio/Cinemachine dependencies.
$gunSource = Get-Content "$projectRoot/Assets/Script/Guns.cs" -Raw
$accuracy = [regex]::Match($gunSource, 'public static Vector3 AccuracyDirection[\s\S]*?;').Value
$signature = 'public static Rect ProjectSpreadBounds'
$start = $gunSource.IndexOf($signature)
$brace = $gunSource.IndexOf('{', $start)
$depth = 1; $end = $brace + 1
while ($depth -gt 0) { if ($gunSource[$end] -eq '{') { $depth++ }; if ($gunSource[$end] -eq '}') { $depth-- }; $end++ }
$projection = $gunSource.Substring($start, $end - $start)
$stub = @'
using UnityEngine;
public class EnemyController : MonoBehaviour { public int alerts, impacts, deaths, lastShotId; public float lastImpactPower; public void AlertTo(Transform source){alerts++;} public void BeginDeath(){deaths++;} public void ReceiveBulletImpact(Collider c,Vector3 p,Vector3 d,int shotId=0,float power=0){impacts++;lastShotId=shotId;lastImpactPower=power;} }
public class Guns : MonoBehaviour { public struct WeaponStat { public float bulletSpeed; } public WeaponStat weaponStat;
'@
Set-Content "$validationRoot/Assets/Dependencies.cs" -Value ($stub + $accuracy + $projection + '}') -Encoding UTF8
Copy-Item -LiteralPath "$PSScriptRoot/WeaponChecks.cs" -Destination "$validationRoot/Assets/Editor/WeaponChecks.cs" -Force
Remove-Item -LiteralPath "$validationRoot/success.txt" -ErrorAction SilentlyContinue
& 'C:/Program Files/Unity/Hub/Editor/6000.2.4f1/Editor/Unity.exe' -batchmode -projectPath $validationRoot -executeMethod WeaponChecks.Run -logFile "$validationRoot/checks.log" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Weapon checks failed. See $validationRoot/checks.log" }
Get-Content "$validationRoot/success.txt"
