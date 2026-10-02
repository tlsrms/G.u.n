"""Static scene/prefab wiring checks. Does not import or launch Unity."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
def read(path): return (ROOT / path).read_text(encoding='utf-8-sig')
def blocks(text):
    rows = re.findall(r'^--- !u!(\d+) &(\d+)( stripped)?\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)
    result = {int(i): (int(k), stripped, body) for k, i, stripped, body in rows}
    assert len(result) == len(rows), 'Duplicate scene IDs'
    return result
def reference(body, key): return int(re.search(re.escape(key) + r': \{fileID: (\d+)', body)[1])
def guid(path): return re.search(r'^guid: (\w+)', read(path + '.meta'), re.M)[1]
def field(body, key, indent=2):
    return re.search(r'(?ms)^' + ' ' * indent + re.escape(key) + r':[^\n]*\n.*?(?=^' + ' ' * indent + r'\w|\Z)', body)[0]
def references(body, key): return list(map(int, re.findall(r'fileID: (\d+)', field(body, key))))
def scalar(body, key): return re.search(r'(?m)^\s*' + re.escape(key) + r': ([^\n]+)', body)[1].strip('"')
def rows(body, key, indent=2):
    return [' ' * (indent + 2) + row for row in re.findall(r'(?ms)^' + ' ' * indent + r'- (.*?)(?=^' + ' ' * indent + r'- |\Z)', field(body, key, indent))]

scene = 'Assets/Scenes/Stages/MafiaStage01.unity'
objects = blocks(read(scene))
prefabs = {guid('Assets/Prefabs/Characters/' + name + '.prefab'): blocks(read('Assets/Prefabs/Characters/' + name + '.prefab'))
           for name in ('GeometricPlayer', 'RegularEnemy', 'StagePropTarget', 'MafiaBoss')}
parents = {}
for id, (kind, stripped, body) in objects.items():
    for target in map(int, re.findall(r'\{fileID: (\d+)\}', body)):
        assert not target or target in objects, (id, 'dangling reference', target)
    if kind == 1001:
        pg = re.search(r'm_SourcePrefab: \{fileID: \d+, guid: (\w+)', body)[1]
        for sid in map(int, re.findall(r'- target: \{fileID: (\d+)', body)):
            assert sid in prefabs[pg], (id, 'missing prefab source', sid)
    if kind not in (4, 224): continue
    if stripped:
        inst = objects[reference(body, 'm_PrefabInstance')][2]
        parents[id] = reference(inst, 'm_TransformParent')
        sid, pg = re.search(r'm_CorrespondingSourceObject: \{fileID: (\d+), guid: (\w+)', body).groups()
        assert prefabs[pg][int(sid)][0] == kind
    else: parents[id] = reference(body, 'm_Father')
for id, parent in parents.items():
    if objects[id][1] and reference(objects[id][2], 'm_CorrespondingSourceObject') != 1001: continue
    if parent:
        children = re.search(r'(?ms)^  m_Children:.*?(?=^  \w)', objects[parent][2])[0]
        assert id in map(int, re.findall(r'fileID: (\d+)', children)), (id, 'missing child')
session = objects[820000545][2]
assert 'sections:' not in session and reference(session, 'stageDirector') in objects
chart_path = 'Assets/RoomChart/Stage1_Full.asset'
assert guid(chart_path) in field(session, 'chart')
chart = read(chart_path)
assert field(chart, 'mapDraft').replace('mapDraft:', 'appliedMap:', 1) == field(chart, 'appliedMap')
map_body = field(chart, 'appliedMap')
map_rooms = {scalar(row, 'id'): row for row in rows(map_body, 'rooms', 4)}
bindings = references(session, 'rooms')
room_ids = [scalar(objects[i][2], 'roomId') for i in bindings]
assert len(bindings) == len(set(bindings)) == len(map_rooms) == 43
assert set(room_ids) == set(map_rooms) and 'mafia0' not in room_ids
# All room ancestors are identity transforms, so saved local positions equal map positions.
for binding, room_id in zip(bindings, room_ids):
    body = objects[binding][2]
    go = objects[reference(body, 'm_GameObject')][2]
    transform = next(i for i in references(go, 'm_Component') if objects[i][0] == 4)
    position = field(objects[transform][2], 'm_LocalPosition')
    room = map_rooms[room_id]
    for axis, size_key in (('x', 'width'), ('y', 'height')):
        actual = float(re.search(axis + r': ([^,}]+)', position)[1])
        expected = float(scalar(map_body, 'origin' + axis.upper())) + float(scalar(room, axis)) * float(scalar(map_body, 'roomSize')) + float(scalar(room, 'offset' + axis.upper()))
        assert abs(actual - expected) < 1e-6, (room_id, 'map/scene position mismatch')
        dimension = float(re.search(axis + r': ([^,}]+)', field(body, 'dimensions'))[1])
        assert dimension == float(scalar(room, size_key)), (room_id, 'map/scene size mismatch')
    parent = parents[transform]
    while parent:
        ancestor = objects[parent][2]
        assert 'm_LocalPosition: {x: 0, y: 0, z: 0}' in ancestor
        assert 'm_LocalRotation: {x: 0, y: 0, z: 0, w: 1}' in ancestor
        assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in ancestor
        parent = parents[parent]

def prefab_value(component, key):
    body = objects[component][2]
    source, prefab = re.search(r'm_CorrespondingSourceObject: \{fileID: (\d+), guid: (\w+)', body).groups()
    instance = objects[reference(body, 'm_PrefabInstance')][2]
    pattern = r'- target: \{fileID: ' + source + r',[^\n]+\n      propertyPath: ' + re.escape(key) + r'\n      value: ([^\n]*)'
    override = re.search(pattern, instance)
    return override[1] if override else scalar(prefabs[prefab][int(source)][2], key)

combat_guid = guid('Assets/Scripts/RoomRhythm/RoomCombat.cs')
assert sum(kind == 114 and combat_guid in body for kind, _, body in objects.values()) == 1
combat = objects[reference(session, 'combat')][2]
enemy_ids = [prefab_value(i, 'enemyId') for i in references(combat, 'enemies')]
targets = [(int(prefab_value(i, 'role')), prefab_value(i, 'targetId')) for i in references(combat, 'stageTargets')]
assert len(enemy_ids) == len(set(enemy_ids)) == 48
assert len(targets) == len(set(targets)) == 90
shot_ids = {target for role, target in targets if role == 0}
door_ids = {target for role, target in targets if role == 1}
assert len(shot_ids) == 84 and len(door_ids) == 6
assert set(enemy_ids).isdisjoint(shot_ids)
assert set(enemy_ids) | shot_ids == {scalar(row, 'id') for row in rows(chart, 'enemies')}
assert door_ids <= {scalar(row, 'destinationId') for row in rows(chart, 'moves') if scalar(row, 'hasDoor') == '1'}
director = objects[reference(session, 'stageDirector')][2]
assert 'entranceRoomId:' not in director and 'entranceTimeline:' not in director and 'singleChart:' not in director
reset = objects[reference(session, 'resetState')][2]
assert reference(director, 'boss') in references(reset, 'roots'), 'Boss hierarchy must be restored on restart'
assert guid(chart_path) in read('Assets/Scenes/Hub/StageSelectScene.unity')
assert 'sceneName: "MafiaStage01"' in read('Assets/Scenes/Hub/StageSelectScene.unity')
assert scene in read('ProjectSettings/EditorBuildSettings.asset')
print('PASS: MafiaStage01 references, hierarchy, 43 map/scene rooms, 48 enemies, 90 boss targets, one combat and full-chart hub wiring.')
