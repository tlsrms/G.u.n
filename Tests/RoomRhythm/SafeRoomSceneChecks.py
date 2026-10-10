"""Validate the shared safe room's serialized geometry and tutorial routes."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def blocks(text):
    matches = list(re.finditer(r"^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)", text, re.M | re.S))
    result = {int(m[2]): (int(m[1]), m[3]) for m in matches}
    assert len(result) == len(matches), "Duplicate scene file IDs"
    return result


def field(body, name):
    return re.search(r"^  " + name + r": (.*)$", body, re.M)[1].strip('"')


def ref(body, name):
    return int(re.search(r"fileID: (\d+)", field(body, name))[1])


def xy(body, name):
    return tuple(float(re.search(axis + r": ([^,}]+)", field(body, name))[1]) for axis in ("x", "y"))


scene_path = "Assets/Scenes/SafeRooms/SafeRoom.unity"
scene = read(scene_path)
objects = blocks(scene)
assert [p.name for p in (ROOT / "Assets/Scenes/SafeRooms").glob("*.unity")] == ["SafeRoom.unity"]
for ident, (_, body) in objects.items():
    for target in re.findall(r"\{fileID: (\d+)\}", body):
        assert int(target) == 0 or int(target) in objects, (ident, target)
    if "\n  m_Father:" in body:
        parent = ref(body, "m_Father")
        if parent:
            parent_body = objects[parent][1]
            children = re.search(r"^  m_Children:.*?(?=^  m_Father:)", parent_body, re.M | re.S)[0]
            assert f"{{fileID: {ident}}}" in children, (ident, "missing in parent's child list")

names = {field(body, "m_Name"): ident for ident, (kind, body) in objects.items() if kind == 1}


def transform(name):
    go = names[name]
    return next(body for kind, body in objects.values() if kind == 224 and "  m_GameObject:" in body and ref(body, "m_GameObject") == go)


for side, x in (("West", -300), ("East", 300)):
    for part, y in (("upper", 177), ("lower", -177)):
        wall = transform(f"{side} {part} wall")
        assert xy(wall, "m_AnchoredPosition") == (x, y)
        assert xy(wall, "m_SizeDelta") == (4, 246)
assert xy(transform("Entrance passage floor"), "m_AnchoredPosition") == (-400, 0)
assert xy(transform("Entrance arrival point"), "m_AnchoredPosition") == (-490, 0)
for part, y in (("upper", 27), ("lower", -27)):
    gate = transform(f"Entrance {part} gate")
    assert xy(gate, "m_AnchoredPosition") == (-300, y)
    assert xy(gate, "m_SizeDelta") == (8, 54)

controller_guid = re.search(r"^guid: (\w+)", read("Assets/Scripts/Stages/SafeRoomController.cs.meta"), re.M)[1]
controller = next(body for kind, body in objects.values() if kind == 114 and controller_guid in body)
assert field(controller, "useSharedRoute") == "1"
for key, name in (("entranceUpperGate", "Entrance upper gate"), ("entranceLowerGate", "Entrance lower gate"), ("entrancePoint", "Entrance arrival point")):
    assert objects[ref(controller, key)][1] == transform(name)
assert xy(objects[ref(controller, "player")][1], "m_AnchoredPosition") == (0, 0)
assert ref(transform("Entrance arrival point"), "m_Father") == ref(objects[ref(controller, "player")][1], "m_Father")

# Each newly authored UI surface needs the renderer that previously caused invisible UI.
for ident, (kind, body) in objects.items():
    if kind != 114 or "m_RaycastTarget:" not in body:
        continue
    owner = ref(body, "m_GameObject")
    assert any(k == 222 and ref(b, "m_GameObject") == owner for k, b in objects.values())

build = read("ProjectSettings/EditorBuildSettings.asset")
assert build.count("path: " + scene_path) == 1
safe_guid = re.search(r"^guid: (\w+)", read(scene_path + ".meta"), re.M)[1]
assert f"path: {scene_path}\n    guid: {safe_guid}" in build
for i in range(1, 6):
    tutorial = read(f"Assets/Scenes/Tutorials/Stage{i:02}.unity")
    assert field(tutorial, "nextSafeScene") == "SafeRoom"
    assert field(tutorial, "entrySafeScene") == ("AwakeningScene" if i == 1 else "SafeRoom")
    onward = f"Stage{i+1:02}" if i < 5 else "StageSelectScene"
    assert field(tutorial, "nextTutorialScene") == onward
    assert re.search(r"enabled: 1\n    path: .*?/" + onward + r"\.unity", build)
    assert not re.search(r"SafeRoom0[1-4]", tutorial)
assert not re.search(r"SafeRoom0[1-4]", build)
print("PASS: one shared safe room, authored left passage/doors, local references, five tutorial routes and build registration.")
