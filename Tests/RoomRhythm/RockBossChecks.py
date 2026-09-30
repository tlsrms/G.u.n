"""Validate the authored boss rig without launching Unity."""
from pathlib import Path
import re
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
path = ROOT / 'Assets/Prefabs/Characters/RockBoss.prefab'
text = path.read_text(encoding='utf-8-sig')
matches = re.findall(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)
objects = {int(i):(int(k),b) for k,i,b in matches}
assert len(objects) == len(matches)
def ref(body, key):
    return int(re.search(r'^  '+key+r': \{fileID: (\d+)', body, re.M)[1])
names = {}
for id,(kind,body) in objects.items():
    for target in re.findall(r'\{fileID: (\d+)\}',body):
        assert int(target)==0 or int(target) in objects, (id,target)
    if kind==1:
        name=re.search(r'^  m_Name: (.+)',body,re.M)[1];names[name]=id
        for component in re.findall(r'component: \{fileID: (\d+)\}',body):
            assert ref(objects[int(component)][1],'m_GameObject') == id
    if kind==4:
        parent=ref(body,'m_Father')
        if parent: assert f'  - {{fileID: {id}}}' in objects[parent][1]
        children=re.findall(r'^  - \{fileID: (\d+)\}',body,re.M)
        for child in children: assert ref(objects[int(child)][1],'m_Father') == id
        if children: assert 'm_LocalScale: {x: 1, y: 1, z: 1}' in body
assert {'RockBoss','Motion','Body','Neck','LeftShoulder','LeftElbow','LeftWrist',
        'RightShoulder','RightElbow','RightWrist','LeftSlamContact','RightSlamContact',
        'CoreTarget','GroundCenter','Forward','BackLeft','BackCenter','BackRight'} <= names.keys()
assert sum(k==212 for k,b in objects.values()) == 16
assert sum(k==95 for k,b in objects.values()) == 1
assert not any('Leg' in name or 'Foot' in name for name in names)
guids=set()
for meta in (ROOT/'Assets/Arts/RockBoss').glob('*.png.meta'):
    guids.add(re.search(r'^guid: (\w+)',meta.read_text(),re.M)[1])
    with Image.open(str(meta)[:-5]) as image:
        assert image.mode=='RGBA' and image.getextrema()[3] == (0,255)
for kind,body in objects.values():
    if kind==212:
        assert re.search(r'm_Sprite: \{fileID: 21300000, guid: (\w+)',body)[1] in guids
print('PASS: rock boss hierarchy, 16 separate parts, unit-scale joints, Animator, impact markers and eight transparent sprite references.')
