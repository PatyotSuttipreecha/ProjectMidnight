$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput = Join-Path $taskRoot 'Library/InventoryGridValidation/CoreChecks'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskEditor = 'C:/Program Files/Unity/Hub/Editor/6000.2.4f1/Editor/Data'
$taskRuntime = Join-Path $taskEditor 'NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21'
$taskLines = @('-target:exe', '-nostdlib', ('-out:"' + $taskOutput + '/checks.dll"'))
foreach ($taskDll in @('System.Private.CoreLib.dll','System.Runtime.dll','System.Console.dll','System.Linq.dll','System.Collections.dll','netstandard.dll')) {
    $taskLines += '-r:"' + (Join-Path $taskRuntime $taskDll) + '"'
}
foreach ($taskFile in @('ItemSO.cs','InventoryItemData.cs','InventoryGrid.cs','InventoryManager.cs')) {
    $taskLines += '"' + (Join-Path $taskRoot ('Assets/Script/Inventory&ItemSO/' + $taskFile)) + '"'
}
$taskLines += '"' + (Join-Path $PSScriptRoot 'Checks.cs') + '"'
[IO.File]::WriteAllLines((Join-Path $taskOutput 'checks.rsp'), [string[]]$taskLines)
[IO.File]::WriteAllText((Join-Path $taskOutput 'checks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.21"}}}')
& (Join-Path $taskEditor 'NetCoreRuntime/dotnet.exe') (Join-Path $taskEditor 'DotNetSdkRoslyn/csc.dll') ('@' + $taskOutput + '/checks.rsp')
if ($LASTEXITCODE -ne 0) { throw 'Core check compilation failed' }
& (Join-Path $taskEditor 'NetCoreRuntime/dotnet.exe') (Join-Path $taskOutput 'checks.dll')
if ($LASTEXITCODE -ne 0) { throw 'Core checks failed' }
