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
    matches = list(re.finditer(r"^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)", read(path), re.M | re.S))
    objects = {int(m[2]): (int(m[1]), m[3]) for m in matches}
    assert len(objects) == len(matches), f"Duplicate file ID: {path}"
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


selection = load("Assets/Scenes/StageSelectScene.unity")
controller = scripts(selection, guid("Assets/Scripts/RoomRhythm/StageSelectScene.cs"))
assert len(controller) == 1
controller = controller[0]
assert len([1 for kind, _ in selection.values() if kind == 223]) == 1
assert len(scripts(selection, "76c392e42b5098c458856cdf6ecaaaa1")) == 1
buttons = scripts(selection, "4e29b1a8efbd4b44bb3f3716e73f07ff")
assert len(buttons) == 8
channel_buttons = [body for body in buttons if 'm_MethodName: "SelectStage"' in body]
assert len(channel_buttons) == 5
assert {int(re.search(r"m_IntArgument: (\d+)", body)[1]) for body in channel_buttons} == set(range(5))
for index in range(1, 6):
    assert f'sceneName: "Stage{index:02}"' in controller
    assert guid(f"Assets/RoomChart/Tutorial_{index}.asset") in controller
for source in ("StageSelectScene.cs", "StageSignal.cs", "StageSelection.cs", "StageSelectionReturn.cs", "StageSelectSurface.cs"):
    assert not re.search(r"\b(Instantiate|AddComponent|CreateInstance|CloneTree)\s*[<(]|new\s+GameObject", read("Assets/Scripts/RoomRhythm/" + source)), source
assert not any('m_Name: "Channel dial"' in body or 'm_Name: "Television cabinet"' in body for _, body in selection.values())
canvas = next(body for kind, body in selection.values() if kind == 223)
assert field(canvas, "m_RenderMode") == "1" and reference(canvas, "m_Camera") != 0
print("PASS: full-screen camera Canvas, eight buttons, five channel events; no cabinet or runtime object construction.")

session_guid = guid("Assets/Scripts/RoomRhythm/RoomSession.cs")
combat_guid = guid("Assets/Scripts/RoomRhythm/RoomCombat.cs")
return_guid = guid("Assets/Scripts/RoomRhythm/StageSelectionReturn.cs")
build = read("ProjectSettings/EditorBuildSettings.asset")
steps = ((0, 1), (-1, 0), (0, -1), (1, 0))
main = load("Assets/Scenes/MainScene.unity")
main_roots = references(field(main[9223372036854775807][1], "m_Roots"))
for index in range(1, 6):
    path = f"Assets/Scenes/Stages/Stage{index:02}.unity"
    objects = load(path)
    assert path in build and guid(path) in build
    sessions = scripts(objects, session_guid)
    assert len(sessions) == 1 and len(scripts(objects, return_guid)) == 1
    session = sessions[0]
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
