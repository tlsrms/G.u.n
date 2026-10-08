$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $env:TEMP 'Gun-RoomRhythm-Checks'
New-Item -ItemType Directory -Path $output -Force | Out-Null
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'Assembly-CSharp.csproj') -Raw
$references = @($project.Project.ItemGroup.Reference)
$core = [string]($references | Where-Object Include -eq 'UnityEngine.CoreModule').HintPath
$unityData = Split-Path (Split-Path (Split-Path $core -Parent) -Parent) -Parent
$compiler = Join-Path $unityData 'DotNetSdkRoslyn/csc.dll'
$runtime = Join-Path $unityData 'NetCoreRuntime/dotnet.exe'
$standard = [string]($references | Where-Object Include -eq 'netstandard').HintPath
$rules = Join-Path $root 'Assets/Scripts/Gameplay/TimingRules.cs'
$model = Join-Path $root 'Assets/Scripts/Gameplay/RoomRun.cs'
$selection = Join-Path $root 'Assets/Scripts/Combat/TargetSelection.cs'
$test = Join-Path $PSScriptRoot 'RoomRunChecks.cs'
$stageTest = Join-Path $PSScriptRoot 'MafiaStageChecks.cs'
$beatSource = Join-Path $root 'Assets/Scripts/Charts/BeatChart.cs'
$beatTest = Join-Path $PSScriptRoot 'BeatChartChecks.cs'
$mapSource = Join-Path $root 'Assets/Scripts/Charts/MapChart.cs'
$timelineEditingSource = Join-Path $root 'Assets/Scripts/Charts/MapTimelineEditing.cs'
$mapTest = Join-Path $PSScriptRoot 'MapChartChecks.cs'
$offsetSource = Join-Path $root 'Assets/Scripts/Input/OffsetCalibration.cs'
$offsetTest = Join-Path $PSScriptRoot 'OffsetCalibrationChecks.cs'
$testDll = Join-Path $output 'RoomRunChecks.dll'
$response = Join-Path $output 'checks.rsp'
@('/nologo','/target:exe','/nostdlib+','/langversion:9',("/out:`"$testDll`""),
    ("/reference:`"$standard`""),("`"$rules`""),("`"$model`""),("`"$selection`""),("`"$test`""),("`"$beatSource`""),("`"$beatTest`""),("`"$mapSource`""),("`"$timelineEditingSource`""),("`"$mapTest`""),("`"$offsetSource`""),("`"$offsetTest`"")) |
    Set-Content -LiteralPath $response
('"' + $stageTest + '"') | Add-Content -LiteralPath $response
('"' + (Join-Path $root 'Assets/Scripts/Stages/Mafia/MafiaIntroTiming.cs') + '"') | Add-Content -LiteralPath $response
('"' + (Join-Path $root 'Assets/Scripts/Stages/Mafia/MafiaAmbushTiming.cs') + '"') | Add-Content -LiteralPath $response
('"' + (Join-Path $PSScriptRoot 'MafiaAmbushChecks.cs') + '"') | Add-Content -LiteralPath $response
& $runtime $compiler "@$response"
if ($LASTEXITCODE -ne 0) { throw 'Rule checks did not compile.' }
'{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}' |
    Set-Content -LiteralPath (Join-Path $output 'RoomRunChecks.runtimeconfig.json')
& dotnet $testDll $root
if ($LASTEXITCODE -ne 0) { throw 'Rule checks failed.' }

$offsetDll = Join-Path $output 'SongOffsetChecks.dll'
& $runtime $compiler /nologo /target:exe /nostdlib+ /langversion:9 "/out:$offsetDll" "/reference:$standard" `
    (Join-Path $root 'Assets/Scripts/Input/InputOffsetSettings.cs') (Join-Path $PSScriptRoot 'SongOffsetChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Song offset checks did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'RoomRunChecks.runtimeconfig.json') -Destination (Join-Path $output 'SongOffsetChecks.runtimeconfig.json')
& dotnet $offsetDll
if ($LASTEXITCODE -ne 0) { throw 'Song offset checks failed.' }

$recordsDll = Join-Path $output 'StageRecordStoreChecks.dll'
& $runtime $compiler /nologo /target:exe /nostdlib+ /langversion:9 "/out:$recordsDll" "/reference:$standard" `
    (Join-Path $root 'Assets/Scripts/Stages/StageRecordStore.cs') (Join-Path $PSScriptRoot 'StageRecordStoreChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Stage record checks did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'RoomRunChecks.runtimeconfig.json') -Destination (Join-Path $output 'StageRecordStoreChecks.runtimeconfig.json')
& dotnet $recordsDll
if ($LASTEXITCODE -ne 0) { throw 'Stage record checks failed.' }

$startupDll = Join-Path $output 'StageStartupChecks.dll'
& $runtime $compiler /nologo /target:exe /nostdlib+ /langversion:9 /nowarn:0649 "/out:$startupDll" "/reference:$standard" `
    $rules $model (Join-Path $root 'Assets/Scripts/Stages/StageProgression.cs') `
    (Join-Path $root 'Assets/Scripts/Input/RoomKeyboard.cs') (Join-Path $root 'Assets/Scripts/Audio/SongTimeline.cs') `
    (Join-Path $PSScriptRoot 'StageStartupChecks.cs') (Join-Path $PSScriptRoot 'StageStartupTestDoubles.cs')
if ($LASTEXITCODE -ne 0) { throw 'Stage startup checks did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'RoomRunChecks.runtimeconfig.json') -Destination (Join-Path $output 'StageStartupChecks.runtimeconfig.json')
& dotnet $startupDll
if ($LASTEXITCODE -ne 0) { throw 'Stage startup checks failed.' }

$enemyRestartDll = Join-Path $output 'EnemyRestartChecks.dll'
& $runtime $compiler /nologo /target:exe /nostdlib+ /langversion:9 /nowarn:0649 "/out:$enemyRestartDll" "/reference:$standard" `
    $rules $model $selection (Join-Path $root 'Assets/Scripts/Combat/RoomEnemy.cs') `
    (Join-Path $root 'Assets/Scripts/Presentation/ActionCueStyle.cs') `
    (Join-Path $root 'Assets/Scripts/Combat/RoomCombat.cs') (Join-Path $PSScriptRoot 'EnemyRestartChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Enemy restart checks did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'RoomRunChecks.runtimeconfig.json') -Destination (Join-Path $output 'EnemyRestartChecks.runtimeconfig.json')
& dotnet $enemyRestartDll
if ($LASTEXITCODE -ne 0) { throw 'Enemy restart checks failed.' }

# Compile against the project's configured references; never launch the Unity editor.
$wanted = @('netstandard','UnityEngine.CoreModule','UnityEngine.AnimationModule','UnityEngine.AudioModule','UnityEngine.TextRenderingModule','UnityEngine.JSONSerializeModule','UnityEngine.IMGUIModule','UnityEngine.UIModule','UnityEngine.UI','Unity.InputSystem','Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.Core.Runtime')
$lines = @('/nologo','/target:library','/nostdlib+','/langversion:9','/nowarn:0649',
    ('/out:"' + (Join-Path $output 'RoomRhythm.dll') + '"'))
foreach ($reference in $references | Where-Object { $_.Include -in $wanted }) {
    $path = [string]$reference.HintPath
    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $root $path }
    $lines += '/reference:"' + $path + '"'
}
# Explicit runtime roots keep Editor sources out of the gameplay assembly.
$runtimeFolders = @('Gameplay', 'Charts', 'Combat', 'World', 'Audio', 'Input', 'Presentation',
    'UI', 'Characters', 'Stages', 'Stages/Mafia', 'Settings')
$lines += foreach ($folder in $runtimeFolders) {
    Get-ChildItem -LiteralPath (Join-Path $root "Assets/Scripts/$folder") -Filter '*.cs' -File |
    ForEach-Object { '"' + $_.FullName + '"' }
}
$response = Join-Path $output 'compile.rsp'
$lines | Set-Content -LiteralPath $response
& $runtime $compiler "@$response"
if ($LASTEXITCODE -ne 0) { throw 'Gameplay compilation failed.' }
Write-Output 'PASS: gameplay compilation against Unity and Input System references.'

$editorLines = @('/nologo','/target:library','/nostdlib+','/langversion:9',
    ('/out:"' + (Join-Path $output 'RoomRhythm.Editor.dll') + '"'),
    ('/reference:"' + (Join-Path $output 'RoomRhythm.dll') + '"'))
$editorWanted = @('netstandard','UnityEngine.CoreModule','UnityEngine.AudioModule','UnityEngine.UnityWebRequestModule','UnityEngine.UnityWebRequestAudioModule','UnityEngine.IMGUIModule','UnityEngine.JSONSerializeModule','UnityEngine.UIElementsModule','UnityEditor','UnityEditor.CoreModule')
foreach ($reference in $references | Where-Object { $_.Include -in $editorWanted }) {
    $editorLines += '/reference:"' + [string]$reference.HintPath + '"'
}
$editorLines += Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Scripts/Editor') -Filter '*.cs' -File -Recurse |
    ForEach-Object { '"' + $_.FullName + '"' }
$editorResponse = Join-Path $output 'editor.rsp'
$editorLines | Set-Content -LiteralPath $editorResponse
& $runtime $compiler "@$editorResponse"
if ($LASTEXITCODE -ne 0) { throw 'Inspector compilation failed.' }
Write-Output 'PASS: editor inspector compilation.'
