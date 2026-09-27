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
$rules = Join-Path $root 'Assets/Scripts/RoomRhythm/TimingRules.cs'
$model = Join-Path $root 'Assets/Scripts/RoomRhythm/RoomRun.cs'
$selection = Join-Path $root 'Assets/Scripts/RoomRhythm/TargetSelection.cs'
$test = Join-Path $PSScriptRoot 'RoomRunChecks.cs'
$authoredTest = Join-Path $PSScriptRoot 'AuthoredChartChecks.cs'
$beatSource = Join-Path $root 'Assets/Scripts/RoomRhythm/BeatChart.cs'
$beatTest = Join-Path $PSScriptRoot 'BeatChartChecks.cs'
$mapSource = Join-Path $root 'Assets/Scripts/RoomRhythm/MapChart.cs'
$timelineEditingSource = Join-Path $root 'Assets/Scripts/RoomRhythm/MapTimelineEditing.cs'
$mapTest = Join-Path $PSScriptRoot 'MapChartChecks.cs'
$offsetSource = Join-Path $root 'Assets/Scripts/RoomRhythm/OffsetCalibration.cs'
$offsetTest = Join-Path $PSScriptRoot 'OffsetCalibrationChecks.cs'
$testDll = Join-Path $output 'RoomRunChecks.dll'
$response = Join-Path $output 'checks.rsp'
@('/nologo','/target:exe','/nostdlib+','/langversion:9',("/out:`"$testDll`""),
    ("/reference:`"$standard`""),("`"$rules`""),("`"$model`""),("`"$selection`""),("`"$test`""),("`"$authoredTest`""),("`"$beatSource`""),("`"$beatTest`""),("`"$mapSource`""),("`"$timelineEditingSource`""),("`"$mapTest`""),("`"$offsetSource`""),("`"$offsetTest`"")) |
    Set-Content -LiteralPath $response
& $runtime $compiler "@$response"
if ($LASTEXITCODE -ne 0) { throw 'Rule checks did not compile.' }
'{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}' |
    Set-Content -LiteralPath (Join-Path $output 'RoomRunChecks.runtimeconfig.json')
& dotnet $testDll (Join-Path $root 'Assets/RoomRhythm/FirstMovement.asset')
if ($LASTEXITCODE -ne 0) { throw 'Rule checks failed.' }

$offsetDll = Join-Path $output 'SongOffsetChecks.dll'
& $runtime $compiler /nologo /target:exe /nostdlib+ /langversion:9 "/out:$offsetDll" "/reference:$standard" `
    (Join-Path $root 'Assets/Scripts/RoomRhythm/InputOffsetSettings.cs') (Join-Path $PSScriptRoot 'SongOffsetChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Song offset checks did not compile.' }
Copy-Item -LiteralPath (Join-Path $output 'RoomRunChecks.runtimeconfig.json') -Destination (Join-Path $output 'SongOffsetChecks.runtimeconfig.json')
& dotnet $offsetDll
if ($LASTEXITCODE -ne 0) { throw 'Song offset checks failed.' }

# Compile against the project's configured references; never launch the Unity editor.
$wanted = @('netstandard','UnityEngine.CoreModule','UnityEngine.AudioModule','UnityEngine.TextRenderingModule','UnityEngine.JSONSerializeModule','UnityEngine.IMGUIModule','Unity.InputSystem')
$lines = @('/nologo','/target:library','/nostdlib+','/langversion:9','/nowarn:0649',
    ('/out:"' + (Join-Path $output 'RoomRhythm.dll') + '"'))
foreach ($reference in $references | Where-Object { $_.Include -in $wanted }) {
    $path = [string]$reference.HintPath
    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $root $path }
    $lines += '/reference:"' + $path + '"'
}
$lines += Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Scripts/RoomRhythm') -Filter '*.cs' -File |
    ForEach-Object { '"' + $_.FullName + '"' }
$response = Join-Path $output 'compile.rsp'
$lines | Set-Content -LiteralPath $response
& $runtime $compiler "@$response"
if ($LASTEXITCODE -ne 0) { throw 'Gameplay compilation failed.' }
Write-Output 'PASS: gameplay compilation against Unity and Input System references.'

$editorLines = @('/nologo','/target:library','/nostdlib+','/langversion:9',
    ('/out:"' + (Join-Path $output 'RoomRhythm.Editor.dll') + '"'),
    ('/reference:"' + (Join-Path $output 'RoomRhythm.dll') + '"'))
$editorWanted = @('netstandard','UnityEngine.CoreModule','UnityEngine.AudioModule','UnityEngine.IMGUIModule','UnityEngine.JSONSerializeModule','UnityEngine.UIElementsModule','UnityEditor','UnityEditor.CoreModule')
foreach ($reference in $references | Where-Object { $_.Include -in $editorWanted }) {
    $editorLines += '/reference:"' + [string]$reference.HintPath + '"'
}
$editorLines += Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Scripts/RoomRhythm/Editor') -Filter '*.cs' -File |
    ForEach-Object { '"' + $_.FullName + '"' }
$editorResponse = Join-Path $output 'editor.rsp'
$editorLines | Set-Content -LiteralPath $editorResponse
& $runtime $compiler "@$editorResponse"
if ($LASTEXITCODE -ne 0) { throw 'Inspector compilation failed.' }
Write-Output 'PASS: editor inspector compilation.'
& (Join-Path $PSScriptRoot 'Check-Scene.ps1')
