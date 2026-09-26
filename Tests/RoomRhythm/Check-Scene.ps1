$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$scene = Get-Content -LiteralPath (Join-Path $root 'Assets/Scenes/MainScene.unity') -Raw
if ($scene -match '\{fileID:\s*\}') { throw 'Empty scene reference.' }
$records = @{}
foreach ($match in [regex]::Matches($scene, '(?ms)^--- !u!(\d+) &(\d+)\r?\n(.*?)(?=^--- !u!|\z)')) {
    $id = [long]$match.Groups[2].Value
    if ($records.ContainsKey($id)) { throw "Duplicate scene ID: $id" }
    $records[$id] = [pscustomobject]@{ Type=[int]$match.Groups[1].Value; Body=$match.Groups[3].Value }
}
foreach ($match in [regex]::Matches($scene, '\{fileID: (\d+)\}')) {
    $id = [long]$match.Groups[1].Value
    if ($id -ne 0 -and !$records.ContainsKey($id)) { throw "Missing local reference: $id" }
}
$transforms = @($records.Keys | Where-Object { $records[$_].Type -eq 4 })
foreach ($id in $transforms) {
    $body = $records[$id].Body
    $parent = [long][regex]::Match($body, 'm_Father: \{fileID: (\d+)\}').Groups[1].Value
    $owner = if ($parent -eq 0) { $records[[long]9223372036854775807].Body } else { $records[$parent].Body }
    if ($owner -notmatch "(?m)^  - \{fileID: $id\}") { throw "Parent missing child: $id" }
    $children = [regex]::Match($body, '(?ms)m_Children:(.*?)(?=  m_Father:)').Groups[1].Value
    foreach ($child in [regex]::Matches($children, '\{fileID: (\d+)\}')) {
        if ($records[[long]$child.Groups[1].Value].Body -notmatch "m_Father: \{fileID: $id\}") { throw "Child parent mismatch: $id" }
    }
}
# GUID checks are deliberately restricted to the new feature's asset folders.
$metas = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Scripts/RoomRhythm') -Filter '*.meta' -File)
$metas += @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Scripts/RoomRhythm/Editor') -Filter '*.meta' -File)
$metas += @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/RoomRhythm') -Filter '*.meta' -File)
$guids = @{}
foreach ($meta in $metas) {
    $guid = [regex]::Match((Get-Content -LiteralPath $meta.FullName -Raw), '(?m)^guid: ([a-f0-9]+)').Groups[1].Value
    if (!$guid -or $guids.ContainsKey($guid)) { throw "Missing/duplicate GUID: $($meta.Name)" }
    $guids[$guid] = $meta.FullName
}
foreach ($match in [regex]::Matches($scene, 'guid: ([a-f0-9]{32})')) {
    $guid = $match.Groups[1].Value
    if (!$guid.StartsWith('0000000000000000') -and !$guids.ContainsKey($guid)) { throw "Unknown scene asset GUID: $guid" }
}
function Position([long]$id) {
    $m = [regex]::Match($records[$id].Body, 'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}')
    return @([double]::Parse($m.Groups[1].Value,[cultureinfo]::InvariantCulture),[double]::Parse($m.Groups[2].Value,[cultureinfo]::InvariantCulture))
}
$chart = Get-Content -LiteralPath (Join-Path $root 'Assets/RoomRhythm/FirstMovement.asset') -Raw
$radius = [double]::Parse([regex]::Match($chart,'aimRadius: ([\d.]+)').Groups[1].Value,[cultureinfo]::InvariantCulture)
$previous = @(0.0,0.0)
$roomCount = 1
foreach ($move in [regex]::Matches($chart,'(?ms)^  - destinationId:.*?(?=^  - destinationId:|^  enemyReadTime:|\z)')) {
    $name=[regex]::Match($move.Value,'destinationId: ([^\r\n]+)').Groups[1].Value
    $direction=[int][regex]::Match($move.Value,'direction: (\d+)').Groups[1].Value
    $bindings=@($records.Keys | Where-Object { $records[$_].Body -match "(?m)^  roomId: $name\r?$" })
    if($bindings.Count -ne 1) { throw "Room binding count: $name" }
    $binding=$records[$bindings[0]].Body
    if($records[[long]820000545].Body -notmatch "- \{fileID: $($bindings[0])\}") { throw "Unconnected room: $name" }
    $go=[long][regex]::Match($binding,'m_GameObject: \{fileID: (\d+)\}').Groups[1].Value
    $transform=@($transforms | Where-Object { $records[$_].Body -match "m_GameObject: \{fileID: $go\}" })[0]
    $position=Position $transform
    $step=@(@(0,6),@(-6,0),@(0,-6),@(6,0))[$direction]
    if([Math]::Abs($position[0]-$previous[0]-$step[0]) -gt .001 -or [Math]::Abs($position[1]-$previous[1]-$step[1]) -gt .001) { throw "Nonadjacent room: $name" }
    if($move.Value -match 'hasDoor: 1') {
        $door=[long][regex]::Match($binding,'door: \{fileID: (\d+)\}').Groups[1].Value
        $doorGo=[long][regex]::Match($records[$door].Body,'m_GameObject: \{fileID: (\d+)\}').Groups[1].Value
        $doorTransform=@($transforms | Where-Object { $records[$_].Body -match "m_GameObject: \{fileID: $doorGo\}" })[0]
        $doorPosition=Position $doorTransform
        if([Math]::Abs($doorPosition[0]+$step[0]/2) -gt .001 -or [Math]::Abs($doorPosition[1]+$step[1]/2) -gt .001) { throw "Door outside shared entrance: $name" }
    }
    $previous=$position; $roomCount++
}
$enemyCount = 0
foreach ($note in [regex]::Matches($chart,'(?ms)^  - id: ([^\r\n]+)\r?\n    roomId: ([^\r\n]+)\r?\n    direction: (\d+)')) {
    $name=$note.Groups[1].Value; $room=$note.Groups[2].Value; $direction=[int]$note.Groups[3].Value
    $binding=@($records.Keys | Where-Object { $records[$_].Body -match "(?m)^  enemyId: $name\r?$" })
    if($binding.Count -ne 1) { throw "Enemy binding count: $name" }
    $go=[long][regex]::Match($records[$binding[0]].Body,'m_GameObject: \{fileID: (\d+)\}').Groups[1].Value
    $transform=@($transforms | Where-Object { $records[$_].Body -match "m_GameObject: \{fileID: $go\}" })[0]
    $parent=[long][regex]::Match($records[$transform].Body,'m_Father: \{fileID: (\d+)\}').Groups[1].Value
    $parentGo=[long][regex]::Match($records[$parent].Body,'m_GameObject: \{fileID: (\d+)\}').Groups[1].Value
    $roomBinding=@($records.Keys | Where-Object { $records[$_].Body -match "(?m)^  roomId: $room\r?$" -and $records[$_].Body -match "m_GameObject: \{fileID: $parentGo\}" })
    if($roomBinding.Count -ne 1) { throw "Enemy in wrong room: $name" }
    $position=Position $transform; $angle=(90-45*$direction)*[Math]::PI/180
    if([Math]::Abs($position[0]-[Math]::Cos($angle)*$radius) -gt .001 -or [Math]::Abs($position[1]-[Math]::Sin($angle)*$radius) -gt .001) { throw "Enemy position mismatch: $name" }
    if ($records[[long]840000822].Body -notmatch "- \{fileID: $($binding[0])\}") { throw "Unconnected enemy: $name" }
    $enemyCount++
}
if ($records[[long]820000545].Body -notmatch 'feedback: \{fileID: 860000022\}') { throw 'Unconnected feedback.' }
if ($records[[long]860000040].Body -notmatch 'm_IsActive: 0') { throw 'Feedback should start hidden.' }
$audio = [IO.File]::ReadAllBytes((Join-Path $root 'Assets/RoomRhythm/MovementCountIn.wav'))
if ($audio.Length -ne (44+48000*24*2)) { throw 'Unexpected test audio length.' }
Write-Output "PASS: $($records.Count) scene records, hierarchy and asset references; $roomCount rooms/entrances; $enemyCount enemy bindings/positions; static feedback; 24-second audio."
