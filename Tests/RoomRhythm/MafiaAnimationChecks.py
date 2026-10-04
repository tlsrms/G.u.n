"""Validate Mafia's authored clips and two-hand contacts without starting Unity.

Run from any directory with Python 3. Reads only the Mafia rig, its clips and
preview controller. Unity import, Animation-window editing and Play Mode are
still user checks. Clip keys are sampled using their serialized Hermite tangents.
"""
from pathlib import Path
import bisect
import math
import re
import zlib

ROOT = Path(__file__).resolve().parents[2]
ANIMATIONS = ROOT / 'Assets/Animations/Mafia'
BODY = 'Motion/Body'
LEFT = BODY + '/LeftShoulder/LeftElbow/LeftWrist'
RIGHT = BODY + '/RightShoulder/RightElbow/RightWrist'
GRIP = RIGHT + '/RifleGrip'


def blocks(text):
    rows = re.findall(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)
    result = {int(i): (int(k), body) for k, i, body in rows}
    assert len(result) == len(rows), 'Duplicate serialized object IDs'
    return result


def ref(body, field):
    return int(re.search(re.escape(field) + r': \{fileID: (\d+)', body)[1])


def vector(text):
    return tuple(float(x) for x in re.search(r'\{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', text).groups())


def field(body, name):
    return re.search(r'^  ' + name + r':(.*?)(?=^  \w|\Z)', body, re.M | re.S)[1]


def read_rig():
    objects = blocks((ROOT / 'Assets/Prefabs/Characters/MafiaBoss.prefab').read_text(encoding='utf-8-sig'))
    rig, paths = {}, {0: ''}
    pending = {i: body for i, (kind, body) in objects.items() if kind == 4}
    while pending:
        ready = [i for i, body in pending.items() if ref(body, 'm_Father') in paths]
        assert ready, 'Disconnected boss hierarchy'
        for i in ready:
            body = pending.pop(i)
            parent = ref(body, 'm_Father')
            name = re.search(r'm_Name: (.+)', objects[ref(body, 'm_GameObject')][1])[1]
            path = (paths[parent] + '/' + name).strip('/') if parent else ''
            paths[i] = path
            qz, qw = map(float, re.search(r'm_LocalRotation: \{x: 0, y: 0, z: ([^,]+), w: ([^}]+)', body).groups())
            rig[path] = {'parent': paths[parent] if parent else None,
                         'p': vector(field(body, 'm_LocalPosition')), 's': vector(field(body, 'm_LocalScale')),
                         'r': (0, 0, math.degrees(2 * math.atan2(qz, qw)))}
    return rig


def read_clip(path):
    text = path.read_text(encoding='utf-8-sig')
    curves = {}
    for prop, name in [('p', 'm_PositionCurves'), ('r', 'm_EulerCurves'), ('s', 'm_ScaleCurves')]:
        for body in re.findall(r'^  - curve:\n(.*?)(?=^  - curve:|\Z)', field(text, name), re.M | re.S):
            target = re.search(r'^    path: (.*)', body, re.M)[1]
            keys = []
            for key in re.findall(r'      - serializedVersion: \d+\n(.*?)(?=      - serializedVersion:|      m_PreInfinity:)', body, re.S):
                time = float(re.search(r'time: (\S+)', key)[1])
                keys.append((time, *(vector(re.search(n + r': ([^\n]+)', key)[1]) for n in ('value', 'inSlope', 'outSlope'))))
            assert len(keys) >= 2 and all(a[0] < b[0] for a, b in zip(keys, keys[1:])), (path.name, target, 'invalid keys')
            assert all(math.isfinite(x) for key in keys for v in key[1:] for x in v)
            curves[(target, prop)] = keys
    duration = float(re.search(r'm_StopTime: (\S+)', text)[1])
    loop = re.search(r'm_LoopTime: (\d+)', text)[1] == '1'
    return text, curves, duration, loop


def sample(keys, time):
    if time <= keys[0][0]:
        return keys[0][1]
    if time >= keys[-1][0]:
        return keys[-1][1]
    index = bisect.bisect_right([key[0] for key in keys], time)
    a, b = keys[index - 1], keys[index]
    dt = b[0] - a[0]
    t = (time - a[0]) / dt
    return tuple((2*t**3 - 3*t*t + 1)*a[1][i] + (t**3 - 2*t*t + t)*dt*a[3][i]
                 + (-2*t**3 + 3*t*t)*b[1][i] + (t**3 - t*t)*dt*b[2][i] for i in range(3))


def point(pose, offset):
    x, y, angle, sx, sy = pose
    c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    return x + offset[0]*sx*c - offset[1]*sy*s, y + offset[0]*sx*s + offset[1]*sy*c


def world_pose(rig, curves, time):
    result = {}
    for path, rest in rig.items():
        p, r, scale = (sample(curves[(path, prop)], time) if (path, prop) in curves else rest[prop] for prop in ('p', 'r', 's'))
        parent = result[rest['parent']] if rest['parent'] is not None else (0, 0, 0, 1, 1)
        x, y = point(parent, p)
        result[path] = (x, y, parent[2] + r[2], parent[3] * scale[0], parent[4] * scale[1])
    return result


def check():
    rig = read_rig()
    clips = sorted(ANIMATIONS.glob('Mafia_[0-9][0-9]_*.anim'))
    assert len(clips) == 15, 'Expected the 15 authored stage motions'
    controller = (ANIMATIONS / 'Mafia_MotionPractice.controller').read_text(encoding='utf-8-sig')
    objects = blocks(controller)
    for object_id, (_, body) in objects.items():
        for target in map(int, re.findall(r'\{fileID: (\d+)\}', body)):
            assert not target or target in objects, (object_id, target, 'missing controller object')
    maximum_contact = 0
    for path in clips:
        text, curves, duration, loop = read_clip(path)
        assert 'm_Legacy: 0' in text and 'm_Events: []' in text, path.name
        assert duration > 0 and curves
        guid = re.search(r'^guid: (\w+)', Path(str(path) + '.meta').read_text(), re.M)[1]
        assert controller.count('guid: ' + guid) == 1, (path.name, 'missing/duplicate preview state')
        for (target, prop), keys in curves.items():
            assert target in rig and target.startswith(BODY), (path.name, target, 'unbound or scene-routing curve')
            assert keys[0][0] == 0 and abs(keys[-1][0] - duration) < 1e-6
            if loop:
                assert math.dist(keys[0][1], keys[-1][1]) < 1e-5, (path.name, target, 'loop seam')
            attribute = {'p': 1, 'r': 4, 's': 3}[prop]
            assert f'path: {zlib.crc32(target.encode())}\n      attribute: {attribute}' in text, (path.name, target, 'binding hash')
        # Check editor curves too, so hand-editing and sampled playback see the same keys.
        for body in re.findall(r'^  - serializedVersion: 2\n(.*?)(?=^  - serializedVersion: 2|\Z)', field(text, 'm_EditorCurves'), re.M | re.S):
            target = re.search(r'^    path: (.*)', body, re.M)[1]
            attribute = re.search(r'^    attribute: (.*)', body, re.M)[1]
            if attribute == 'm_IsActive':
                # Unity's Animation window may add stepped GameObject visibility keys.
                assert target in rig and target.startswith(BODY), (path.name, target, 'unbound visibility key')
                keys = re.findall(r'        time: (\S+)\n        value: (\S+)', body)
                assert keys and all(float(value) in (0, 1) for _, value in keys)
                native = [curve for curve in re.findall(r'^  - serializedVersion: 2\n(.*?)(?=^  - serializedVersion: 2|\Z)',
                          field(text, 'm_FloatCurves'), re.M | re.S)
                          if re.search(r'^    path: (.*)', curve, re.M)[1] == target
                          and re.search(r'^    attribute: (.*)', curve, re.M)[1] == attribute]
                assert len(native) == 1 and keys == re.findall(r'        time: (\S+)\n        value: (\S+)', native[0])
                continue
            prefix, axis = attribute.rsplit('.', 1)
            prop = {'m_LocalPosition': 'p', 'localEulerAnglesRaw': 'r', 'm_LocalScale': 's'}[prefix]
            axis = 'xyz'.index(axis)
            keys = re.findall(r'        time: (\S+)\n        value: (\S+)', body)
            assert len(keys) == len(curves[(target, prop)])
            for (t, v), key in zip(keys, curves[(target, prop)]):
                assert abs(float(t)-key[0]) < 1e-6 and abs(float(v)-key[1][axis]) < 1e-5
        if 'Fire' in path.stem:
            body_y = [sample(curves[(BODY, 'p')], min(duration, i / 240))[1]
                      for i in range(math.ceil(duration * 240) + 1)]
            pulses = sum(y < body_y[i-1] and y <= body_y[i+1] and body_y[0] - y > .01
                         for i, y in enumerate(body_y[1:-1], 1))
            assert pulses == (3 if 'Burst3' in path.stem else 1), (path.name, 'incorrect recoil count', pulses)
        # Sample between keys, not just at the IK-authored poses.
        for frame in range(math.ceil(duration * 240) + 1):
            time = min(duration, frame / 240)
            pose = world_pose(rig, curves, time)
            if pose[GRIP + '/AK'][3] < .25:
                continue  # Gun is hidden during seated smoking and the initial draw.
            left_hand = point(pose[LEFT], (0, .075))
            foregrip = point(pose[GRIP], (0, .25))
            right_hand = point(pose[RIGHT], (0, .075))
            trigger_grip = point(pose[GRIP], (0, 0))
            shoulder = point(pose[BODY + '/RightShoulder'], (0, 0))
            stock = point(pose[GRIP], (0, -.3253125))
            contact = math.dist(left_hand, foregrip)
            maximum_contact = max(maximum_contact, contact)
            assert contact < .004, (path.name, time, 'left hand detached', contact)
            assert math.dist(right_hand, trigger_grip) < .0001
            assert math.dist(stock, shoulder) < .014, (path.name, time, 'stock detached')
    print(f'PASS: {len(clips)} Mafia clips; native/editor curves, bindings, loop seams, preview states and two-hand contact (max error {maximum_contact:.6f}).')


if __name__ == '__main__':
    check()
