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
assert 'id: mafia' in session and reference(session, 'stageDirector') in objects
assert len(re.findall(r'^  roomId: guard', read(scene), re.M)) == 16
assert len(re.findall(r'^  roomId: mafia', read(scene), re.M)) == 28
assert 'sceneName: "MafiaStage01"' in read('Assets/Scenes/Hub/StageSelectScene.unity')
assert scene in read('ProjectSettings/EditorBuildSettings.asset')
print('PASS: MafiaStage01 local references, prefab source IDs, hierarchy, 44 rooms and entry wiring.')
