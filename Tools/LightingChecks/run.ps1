$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$validationRoot=Join-Path $projectRoot 'Library/LightingValidation'
New-Item -ItemType Directory -Force -Path "$validationRoot/Assets/Editor", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$projectRoot/ProjectSettings/ProjectVersion.txt" -Destination "$validationRoot/ProjectSettings/ProjectVersion.txt" -Force
@'
{"dependencies":{"com.unity.render-pipelines.universal":"17.2.0","com.unity.modules.physics":"1.0.0"}}
'@ | Set-Content "$validationRoot/Packages/manifest.json" -Encoding UTF8
foreach($name in @('LightingPreset','SceneLightingController','LightingCue','LightingBounding')) {
    Copy-Item -LiteralPath "$projectRoot/Assets/Script/$name.cs" -Destination "$validationRoot/Assets/$name.cs" -Force
}
foreach($name in @('LightingConfigurationWindow','LightingEditorTools')) {
    Copy-Item -LiteralPath "$projectRoot/Assets/Editor/$name.cs" -Destination "$validationRoot/Assets/Editor/$name.cs" -Force
}
Copy-Item -LiteralPath "$PSScriptRoot/LightingChecks.cs" -Destination "$validationRoot/Assets/Editor/LightingChecks.cs" -Force
if(Test-Path "$validationRoot/success.txt") {Remove-Item -LiteralPath "$validationRoot/success.txt"}
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.2.4f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-nographics','-projectPath',"`"$validationRoot`"",'-executeMethod','LightingChecks.Run','-logFile',"`"$validationRoot/checks.log`"") -WindowStyle Hidden -PassThru
$process.WaitForExit()
if($process.ExitCode -ne 0 -or !(Test-Path "$validationRoot/success.txt")){throw "Lighting checks failed: $validationRoot/checks.log"}
Get-Content "$validationRoot/success.txt"
