"""Checks serialized selection/gameplay scene wiring without opening Unity."""
from pathlib import Path
import math
import re

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def guid(path):
    return re.search(r"^guid: (\w+)", read(path + ".meta"), re.M)[1]


def load(path):
    matches = list(re.finditer(r"^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)", read(path), re.M | re.S))
    objects = {int(m[2]): (int(m[1]), m[3]) for m in matches}
    assert len(objects) == len(matches), f"Duplicate file ID: {path}"
    # Resolve the authored character/enemy prefabs, including scene overrides and stripped references.
    prefab_paths = ["Assets/Prefabs/Characters/GeometricPlayer.prefab", "Assets/Prefabs/Characters/GeometricPlayerUI.prefab", "Assets/Prefabs/Characters/RegularEnemy.prefab"]
    for instance, (kind, body) in list(objects.items()):
        if kind != 1001:
            continue
        prefab_path = next(p for p in prefab_paths if guid(p) in field(body, "m_SourcePrefab"))
        parts = {int(i): (int(k), b) for k, i, b in re.findall(r"^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)", read(prefab_path), re.M | re.S)}
        for source, prop, value in re.findall(r"    - target: \{fileID: (\d+),[^\n]+\n      propertyPath: ([^\n]+)\n      value: ([^\n]*)", body):
            source = int(source)
            part_kind, part = parts[source]
            if "." in prop:
                key, axis = prop.split(".")
                pattern = r"(^  " + key + r": \{[^\n]*?" + axis + r": )[^,}]+"
                part, count = re.subn(pattern, lambda m: m[1] + value, part, flags=re.M)
            else:
                part, count = re.subn(r"^  " + prop + r":.*", lambda m: "  " + prop + ": " + value, part, flags=re.M)
            assert count == 1, (prefab_path, prop)
            parts[source] = (part_kind, part)
        mapped = {id: instance * 10000 + id for id in parts}
        for id, (part_kind, part) in list(objects.items()):
            if f"m_PrefabInstance: {{fileID: {instance}}}" in part:
                source = reference(part, "m_CorrespondingSourceObject")
                assert source in parts and parts[source][0] == part_kind
                mapped[source] = id
                del objects[id]
        parent = int(re.search(r"m_TransformParent: \{fileID: (\d+)\}", body)[1])
        for id, (part_kind, part) in parts.items():
            part = re.sub(r"\{fileID: (\d+)\}", lambda m: "{fileID: " + str(mapped.get(int(m[1]), int(m[1]))) + "}", part)
            if part_kind in (4, 224) and reference(part, "m_Father") == 0:
                part = part.replace("m_Father: {fileID: 0}", f"m_Father: {{fileID: {parent}}}")
            objects[mapped[id]] = (part_kind, part)
    for object_id, (_, body) in objects.items():
        local = re.findall(r"\{fileID: (\d+)\}", body)
        local += re.findall(r"^\s*- fileID: (\d+)$", body, re.M)
        for target in map(int, local):
            assert not target or target in objects, f"Dangling reference: {path}/{object_id} -> {target}"
    transforms = {id: body for id, (kind, body) in objects.items() if kind in (4, 224)}
    for id, body in transforms.items():
        parent = reference(body, "m_Father")
        if parent:
            assert id in references(field(transforms[parent], "m_Children")), f"Missing child: {path}/{id}"
        for child in references(field(body, "m_Children")):
            assert reference(transforms[child], "m_Father") == id, f"Wrong parent: {path}/{child}"
    root_list = references(field(objects[9223372036854775807][1], "m_Roots"))
    assert set(root_list) == {id for id, body in transforms.items() if not reference(body, "m_Father")}
    return objects


def field(body, name):
    match = re.search(r"^  " + re.escape(name) + r":[^\n]*(?:\n  -[^\n]*)*", body, re.M)
    assert match, f"Missing field: {name}"
    return match[0].split(":", 1)[1].strip()


def references(value):
    return [int(id) for id in re.findall(r"fileID: (\d+)", value)]


def reference(body, name):
    return references(field(body, name))[0]


def vector(value):
    return tuple(float(re.search(axis + r": ([^,}]+)", value)[1]) for axis in ("x", "y", "z"))


def scripts(objects, script_guid):
    return [body for kind, body in objects.values() if kind == 114 and script_guid in body]


def close(actual, expected):
    assert all(abs(a - b) < .001 for a, b in zip(actual, expected)), (actual, expected)


def check_graphic_components(objects, path):
    count = 0
    for graphic_id, (kind, body) in objects.items():
        if kind != 114 or "  m_RaycastTarget:" not in body:
            continue
        game_object = reference(body, "m_GameObject")
        owner = objects[game_object][1]
        name = field(owner, "m_Name")
        components = references(field(owner, "m_Component"))
        assert graphic_id in components, f"Unattached UI Graphic: {path}/{name}"
        for required_kind, label in ((224, "RectTransform"), (222, "CanvasRenderer")):
            matches = [id for id in components if objects[id][0] == required_kind]
            assert len(matches) == 1, f"UI Graphic requires one {label}: {path}/{name}"
            assert reference(objects[matches[0]][1], "m_GameObject") == game_object, \
                f"{label} attached to wrong object: {path}/{name}"
        count += 1
    return count


selection = load("Assets/Scenes/Hub/StageSelectScene.unity")
graphic_count = check_graphic_components(selection, "StageSelectScene")
assert graphic_count > 0
print(f"PASS: all {graphic_count} hub UI Graphics have an attached RectTransform and CanvasRenderer.")
controller = scripts(selection, guid("Assets/Scripts/RoomRhythm/StageSelectScene.cs"))
assert len(controller) == 1
controller = controller[0]
assert "preview:" not in controller
record_objects = [id for id, (kind, body) in selection.items()
                  if kind == 1 and field(body, "m_Name").strip('"') == "Stage Records Area"]
assert len(record_objects) == 1
record_transform = next(body for kind, body in selection.values()
                        if kind == 224 and reference(body, "m_GameObject") == record_objects[0])
assert references(field(record_transform, "m_Children")), "Selected song details must be authored"
assert not any(re.search(r'm_Name: "Channel \d+ preview"', body) for _, body in selection.values())
assert len([1 for kind, _ in selection.values() if kind == 223]) == 1
assert len(scripts(selection, "76c392e42b5098c458856cdf6ecaaaa1")) == 1
buttons = scripts(selection, "4e29b1a8efbd4b44bb3f3716e73f07ff")
assert len(buttons) == 7
channel_buttons = [body for body in buttons if 'm_MethodName: "SelectStage"' in body]
assert len(channel_buttons) == 6
assert {int(re.search(r"m_IntArgument: (\d+)", body)[1]) for body in channel_buttons} == set(range(6))
start_buttons = [body for body in buttons if 'm_MethodName: "StartSelectedStage"' in body]
assert len(start_buttons) == 1
assert selection[reference(controller, "startButton")][1] == start_buttons[0]
assert "beatGlow:" not in controller
for label in ("details", "duration", "bestAccuracy", "caseDisplay"):
    assert reference(controller, label) != 0
for index in range(1, 6):
    assert f'sceneName: "Stage{index:02}"' in controller
    assert guid(f"Assets/RoomChart/Tutorial_{index}.asset") in controller
assert 'sceneName: "MafiaStage01"' in controller
assert guid("Assets/RoomChart/Stage1_Full.asset") in controller
for source in ("StageSelectScene.cs", "SafeRoomController.cs", "StageProgression.cs", "StageSelection.cs", "StageSelectSurface.cs"):
    assert not re.search(r"\b(Instantiate|AddComponent|CreateInstance|CloneTree)\s*[<(]|new\s+GameObject", read("Assets/Scripts/RoomRhythm/" + source)), source
assert not any('m_Name: "Channel dial"' in body or 'm_Name: "Television cabinet"' in body for _, body in selection.values())
canvas = next(body for kind, body in selection.values() if kind == 223)
assert field(canvas, "m_RenderMode") == "1" and reference(canvas, "m_Camera") != 0
assert len(scripts(selection, guid("Assets/Scripts/RoomRhythm/SafeRoomController.cs"))) == 1
assert len([1 for kind, _ in selection.values() if kind == 82]) == 1
assert field(scripts(selection, guid("Assets/Scripts/RoomRhythm/SafeRoomController.cs"))[0], "exitInitiallyOpen") == "0"
names = {id: field(body, "m_Name").strip('"') for id, (kind, body) in selection.items() if kind == 1}
def named_transform(name):
    go = next(id for id, value in names.items() if value == name)
    return next((id, body) for id, (kind, body) in selection.items()
                if kind == 224 and reference(body, "m_GameObject") == go)

def xy(value):
    return tuple(float(re.search(axis + r": ([^,}]+)", value)[1]) for axis in ("x", "y"))

player_id, player = named_transform("Player")
assert xy(field(player, "m_AnchoredPosition")) == (0, 0)
station_id, station = named_transform("Record Station")
assert 0 < xy(field(station, "m_AnchoredPosition"))[1] < 200
assert reference(controller, "station") == station_id
assert reference(controller, "recordTransport") == named_transform("Record Transport")[0]
assert reference(controller, "recordDock") == named_transform("Record dock")[0]
safe = scripts(selection, guid("Assets/Scripts/RoomRhythm/SafeRoomController.cs"))[0]
assert reference(safe, "turntableInteractionPoint") == named_transform("Turntable interaction point")[0]
assert len([name for name in names.values() if re.fullmatch(r"Record 0[1-6]", name)]) == 6
assert not any(name in names.values() for name in ("Selected record", "Playing record", "Loose record"))
record_refs = [int(id) for id in re.findall(r"^    record: \{fileID: (\d+)\}", controller, re.M)]
assert len(record_refs) == len(set(record_refs)) == 6
for index, id in enumerate(record_refs, 1):
    assert id == named_transform(f"Record {index:02}")[0]
    case_id, case_body = named_transform(f"Case {index:02}")
    assert reference(selection[id][1], "m_Father") == case_id
    assert reference(case_body, "m_Father") == named_transform(f"Slot {index:02}")[0]
    assert field(selection[reference(selection[id][1], "m_GameObject")][1], "m_IsActive") == "0"
    assert references(field(case_body, "m_Children"))[0] == id, "Record must slide behind the opaque case cover"
assert len(re.findall(r"^    sleeve: \{fileID: \d+\}", controller, re.M)) == 6
assert xy(field(controller, "turntableSelectionPosition"))[0] < 0
rack_position = xy(field(controller, "rackSelectionPosition"))
assert rack_position[0] > 0 and rack_position[1] > 250
assert float(field(controller, "turntableSelectionScale")) >= 1.5
assert "Beat light" not in names.values()
assert not any(name in controller for name in ("movingRecord:", "worldRecord:", "looseRecord:"))
print("PASS: six cases with hidden original discs, separate Start action, song information, large left deck and upper-right rack; no beat pulse or map preview.")

session_guid = guid("Assets/Scripts/RoomRhythm/RoomSession.cs")
combat_guid = guid("Assets/Scripts/RoomRhythm/RoomCombat.cs")
return_guid = guid("Assets/Scripts/RoomRhythm/StageProgression.cs")
build = read("ProjectSettings/EditorBuildSettings.asset")
steps = ((0, 1), (-1, 0), (0, -1), (1, 0))
main = load("Assets/Scenes/Development/MainScene.unity")
main_roots = references(field(main[9223372036854775807][1], "m_Roots"))
for index in range(1, 6):
    path = f"Assets/Scenes/Tutorials/Stage{index:02}.unity"
    objects = load(path)
    assert path in build and guid(path) in build
    sessions = scripts(objects, session_guid)
    assert len(sessions) == 1 and len(scripts(objects, return_guid)) == 1
    session = sessions[0]
    assert field(session, "restartOnClear") == "0"
    progression = scripts(objects, return_guid)[0]
    assert field(progression, "nextSafeScene").strip('"') == (f"SafeRoom{index:02}" if index < 5 else "StageSelectScene")
    assert field(progression, "entrySafeScene").strip('"') == ("AwakeningScene" if index == 1 else f"SafeRoom{index-1:02}")
    chart_path = f"Assets/RoomChart/Tutorial_{index}.asset"
    assert guid(chart_path) in field(session, "chart")
    chart = read(chart_path)
    transforms = {reference(body, "m_GameObject"): id for id, (kind, body) in objects.items() if kind in (4, 224)}

    def world(transform):
        body = objects[transform][1]
        position = vector(field(body, "m_LocalPosition"))
        parent = reference(body, "m_Father")
        if parent:
            position = tuple(a + b for a, b in zip(position, world(parent)))
        return position

    def position(component):
        return world(transforms[reference(objects[component][1], "m_GameObject")])

    moves = re.search(r"^  moves:\n(.*?)(?=^  \w|\Z)", chart, re.M | re.S)[1]
    destinations = re.findall(r"^  - destinationId: (.+)", moves, re.M)
    directions = [int(v) for v in re.findall(r"^    direction: (\d+)", moves, re.M)]
    rooms = references(field(session, "rooms"))
    assert references(field(objects[9223372036854775807][1], "m_Roots")) == main_roots
    room_transforms = [transforms[reference(objects[id][1], "m_GameObject")] for id in rooms]
    assert references(field(objects[820000021][1], "m_Children")) == [820000481] + room_transforms
    for order, id in enumerate(rooms, 1):
        go = reference(objects[id][1], "m_GameObject")
        assert re.fullmatch(str(order) + r"th/-?\d+(?:\.\d+)?beat", field(objects[go][1], "m_Name"))
    assert len(rooms) == len(destinations) + 1
    start = re.search(r"^  startingRoomId: (.+)", chart, re.M)[1]
    assert [field(objects[id][1], "roomId") for id in rooms] == [start] + destinations
    room_positions = {field(objects[id][1], "roomId"): position(id) for id in rooms}
    for move, current, previous in zip(directions, rooms[1:], rooms):
        side = float(field(objects[current][1], "sideLength"))
        dx, dy = steps[move]
        expected = tuple(a + b for a, b in zip(position(previous), (dx * side, dy * side, 0)))
        close(position(current), expected)
        door = reference(objects[current][1], "door")
        close(position(door), tuple(a - b for a, b in zip(expected, (dx * side / 2, dy * side / 2, 0))))
    enemies = references(field(scripts(objects, combat_guid)[0], "enemies"))
    authored = re.search(r"^  enemies:\n(.*?)(?=^  \w|\Z)", chart, re.M | re.S)
    notes = re.split(r"^  - id: ", authored[1], flags=re.M)[1:] if authored else []
    assert len(enemies) == len(notes)
    radius = float(re.search(r"^  aimRadius: (.+)", chart, re.M)[1])
    for component, note in zip(enemies, notes):
        assert field(objects[component][1], "enemyId") == note.splitlines()[0]
        room = re.search(r"    roomId: (.+)", note)[1]
        direction = int(re.search(r"    direction: (\d+)", note)[1])
        angle = math.radians(90 - direction * 45)
        close(position(component), tuple(a + b for a, b in zip(room_positions[room], (math.cos(angle) * radius, math.sin(angle) * radius, 0))))
    print(f"PASS: Stage{index:02} static scene wiring, {len(rooms)} rooms, {len(enemies)} enemies, door/enemy positions, return component.")

assert re.search(r"m_Scenes:\s*- enabled: 1\s*path: Assets/Scenes/Opening/AwakeningScene.unity", build)
for index in range(5):
    path = "Assets/Scenes/Opening/AwakeningScene.unity" if index == 0 else f"Assets/Scenes/SafeRooms/SafeRoom{index:02}.unity"
    objects = load(path)
    assert path in build and guid(path) in build
    safe = scripts(objects, guid("Assets/Scripts/RoomRhythm/SafeRoomController.cs"))
    assert len(safe) == 1
    assert field(safe[0], "nextScene").strip('"') == f"Stage{index+1:02}"
    assert field(safe[0], "exitInitiallyOpen") == "1"
    assert not any(kind == 82 for kind, _ in objects.values()), "Rest room must be silent"
    assert not scripts(objects, session_guid), "Safe room must not run timing judgments"
print("PASS: awakening -> five tutorials with four silent rest rooms -> turntable hub; clear and retreat routes resolve.")

# The same prefab assets drive all 12 player scenes; bones and muzzle must remain linked.
rig_guid = guid("Assets/Scripts/RoomRhythm/GeometricPlayerRig.cs")
for path in (["Assets/Scenes/Development/MainScene.unity"]
             + [f"Assets/Scenes/Tutorials/Stage{i:02}.unity" for i in range(1, 6)]):
    objects = load(path)
    rig = scripts(objects, rig_guid)
    assert len(rig) == 1, path
    assert reference(objects[830000442][1], "characterRig") == 9500000002
    assert reference(scripts(objects, session_guid)[0], "playerRig") == 9500000002
    assert len(references(field(rig[0], "parts"))) == 17
    assert reference(rig[0], "grip") != reference(rig[0], "muzzle")
    for renderer in (910000012, 910000022, 910000032):
        assert field(objects[renderer][1], "m_Enabled") == "0", "Duplicate pistol geometry"
for path in (["Assets/Scenes/Hub/StageSelectScene.unity", "Assets/Scenes/Opening/AwakeningScene.unity"]
             + [f"Assets/Scenes/SafeRooms/SafeRoom{i:02}.unity" for i in range(1, 5)]):
    objects = load(path)
    assert 4100000065 not in objects, "Old flattened player Image still attached"
    check_graphic_components(objects, path)
    bone_names = {field(b, "m_Name").strip('"') for k,b in objects.values() if k == 1}
    assert {"LeftShoulder", "LeftElbow", "LeftWrist", "RightShoulder", "RightElbow", "RightWrist",
            "LeftHip", "LeftKnee", "LeftAnkle", "RightHip", "RightKnee", "RightAnkle", "Spine", "Head", "Grip", "Muzzle"} <= bone_names
assert vector(field(station, "m_LocalScale")) == (.25, .25, 1)
assert "a: 0.995" in field(selection[4200000011][1], "m_Color")
print("PASS: linked geometric rigs in all 12 scenes, 17 separate parts, joints, muzzle, UI components and compact station.")

for prefab in ("Assets/Prefabs/Characters/GeometricPlayer.prefab", "Assets/Prefabs/Characters/GeometricPlayerUI.prefab"):
    authored = read(prefab)
    for body in re.findall(r"^--- !u!1 &\d+\n(.*?)(?=^--- !u!|\Z)", authored, re.M | re.S):
        if field(body, "m_Name") in ("LeftHip", "RightHip", "LeftShoulder", "RightPauldron"):
            assert field(body, "m_IsActive") == "0", "Occluded limbs must remain hidden"
print("PASS: overhead detective idle legs, free arm and armor shoulder plate are hidden.")
