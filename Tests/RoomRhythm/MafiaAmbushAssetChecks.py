"""Check the ambush prefab's local/nested references. Does not launch Unity."""
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[2]


def guid(path):
    return re.search(r'^guid: (\w+)', (ROOT / (path + '.meta')).read_text(), re.M)[1]


text = (ROOT / 'Assets/Prefabs/Characters/MafiaShieldAmbush.prefab').read_text()
rows = re.findall(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)
objects = {int(i): (int(kind), body) for kind, i, body in rows}
assert len(rows) == len(objects), 'Duplicate local file ID'
for target in re.findall(r'\{fileID: (\d+)\}', text):
    assert int(target) == 0 or int(target) in objects, 'Missing local reference: ' + target

script = guid('Assets/Scripts/Stages/Mafia/MafiaShieldAmbush.cs')
components = [body for kind, body in objects.values() if kind == 114]
assert len(components) == 1 and script in components[0], 'Only ambush logic belongs in the prefab'
assert '  session: {fileID: 0}' in components[0], 'Prefab must bind within its scene'
assert re.search(r'^  escapeRoomId: *$', components[0], re.M), 'Each copy chooses its own note'
assert '  boss: {fileID: 501}' in components[0]

boss = guid('Assets/Prefabs/Characters/MafiaBoss.prefab')
assert f'm_SourcePrefab: {{fileID: 100100000, guid: {boss}, type: 3}}' in objects[500][1]
assert 'm_TransformParent: {fileID: 151}' in objects[500][1]
assert f'm_CorrespondingSourceObject: {{fileID: 1001, guid: {boss}, type: 3}}' in objects[501][1]
assert 'propertyPath: m_Enabled\n      value: 0' in objects[500][1], 'Nested Animator must not fight sampling'
assert 'Muzzle' in (ROOT / 'Assets/Prefabs/Characters/MafiaBoss.prefab').read_text()

for field, clip in [('enter', '06_ShieldEnter'), ('idle', '07_ShieldIdle'),
                    ('burst', '08_ShieldFireBurst3'), ('withdraw', '09_ShieldWithdraw')]:
    clip_guid = guid('Assets/Animations/Mafia/Mafia_' + clip + '.anim')
    assert f'{field}: {{fileID: 7400000, guid: {clip_guid}, type: 2}}' in components[0]
assert guid('Assets/Audio/MafiaAKShot.wav') in components[0]

for i in (162, 172, 182, 192):
    assert objects[i][0] == 212 and 'm_Enabled: 0' in objects[i][1], 'No flash/tracers at rest'
assert objects[222][0] == 212, 'Shield subordinate is visual only'
assert 'm_Children:\n  - {fileID: 501}\n  - {fileID: 221}' in objects[151][1]
assert 'm_LocalRotation: {x: 0, y: 0, z: 1, w: 0}' in objects[111][1], 'Default upper wall faces into room'

solid_size = struct.unpack('>II', (ROOT / 'Assets/Arts/Solid.png').read_bytes()[16:24])
ppu = float(re.search(r'spritePixelsToUnits: ([\d.]+)', (ROOT / 'Assets/Arts/Solid.png.meta').read_text())[1])
assert solid_size == (ppu, ppu), 'Authored door/effect dimensions assume a unit square'
print('PASS: ambush prefab references, shared boss, four clips, cosmetic shield, effects, orientation and dimensions.')
