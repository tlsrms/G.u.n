"""Author geometric part textures and two editable rigs. Never invoked by the game.

Requires Pillow. Rebuilding preserves GUIDs but replaces prefab poses; scene installation
is intentionally separate so running this does not overwrite scene edits.
"""
from pathlib import Path
import math
import re
import uuid
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Arts/GeometricPlayer'
PREFABS = ROOT / 'Assets/Prefabs/Characters'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'


def guid(path, importer='DefaultImporter'):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
    value = uuid.uuid4().hex
    meta.write_text(f'fileFormatVersion: 2\nguid: {value}\n{importer}:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    return value


def field(body, key, value):
    body, count = re.subn(r'^  ' + key + r':[^\n]*', '  ' + key + ': ' + value, body, flags=re.M)
    assert count == 1, (key, count)
    return body


def blocks(path):
    return {int(i): (int(k), b) for k, i, b in re.findall(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', path.read_text(encoding='utf-8-sig'), re.M | re.S)}


# Polygons, not painted anatomy. Each polygon remains independently editable here.
POLYGONS = {
    'Torso': [([(.08,.68),(.2,.9),(.38,1),(.62,1),(.8,.9),(.92,.68),(1,.38),(.91,.12),(.7,0),(.3,0),(.09,.12),(0,.38)],185),
              ([(.1,.35),(.3,.12),(.7,.12),(.9,.35),(.85,.12),(.7,0),(.3,0),(.15,.12)],150),
              ([(.485,.05),(.515,.05),(.515,.45),(.485,.45)],130),
              ([(.28,.83),(.39,.95),(.61,.95),(.72,.83),(.61,.62),(.39,.62)],220)],
    # Fedora viewed from above: brim, crown, hat band and center crease, no face/visor.
    'Head': [([(.02,.35),(.08,.65),(.24,.88),(.42,1),(.64,.96),(.86,.77),(1,.47),(.92,.2),(.73,.04),(.44,0),(.18,.12)],80),
             ([(.2,.3),(.22,.65),(.36,.85),(.61,.87),(.78,.66),(.8,.3),(.62,.15),(.38,.15)],40),
             ([(.24,.4),(.25,.66),(.39,.85),(.59,.85),(.74,.65),(.76,.4),(.59,.25),(.4,.25)],135),
             ([(.4,.7),(.49,.78),(.58,.7),(.57,.38),(.49,.32),(.41,.38)],105)],
    'Shoulder': [([(.12,.7),(.3,.95),(.72,.95),(.92,.7),(.88,.2),(.7,.05),(.25,.05),(.08,.2)],185)],
    'UpperArm': [([(.1,.9),(.25,1),(.75,1),(.9,.9),(.92,.15),(.78,0),(.22,0),(.08,.15)],185),
                 ([(.78,.9),(.9,.9),(.92,.15),(.78,0)],155)],
    'Forearm': [([(.16,1),(.84,1),(.9,.15),(.78,0),(.22,0),(.1,.15)],185),
                ([(.12,.15),(.88,.15),(.78,0),(.22,0)],120)],
    'Hand': [([(.2,1),(.8,1),(1,.6),(.8,.12),(.3,0),(0,.3)],140)],
    'Thigh': [([(.09,1),(.91,1),(.84,.15),(.58,0),(.19,.12),(0,.68)],150),
              ([(.61,.92),(.83,.92),(.76,.2),(.59,.1)],95)],
    'Shin': [([(.21,1),(.83,.94),(1,.28),(.73,0),(.14,.09),(0,.34)],200),
             ([(.64,.9),(.83,.87),(.89,.31),(.71,.12)],105)],
    'Foot': [([(.18,1),(.79,1),(1,.65),(.89,0),(.11,0),(0,.65)],90),
             ([(.14,.65),(.86,.65),(.79,.16),(.2,.16)],185)],
    'Pistol': [([(.12,1),(.88,1),(.94,.33),(.7,.19),(.67,0),(.27,0),(.23,.18),(.06,.3)],70),
               ([(.24,.95),(.74,.95),(.78,.35),(.24,.35)],216),
               ([(.44,.95),(.59,.95),(.59,.46),(.44,.46)],110)]
}


def textures():
    template = (ROOT / 'Assets/Arts/Solid.png.meta').read_text(encoding='utf-8-sig')
    result = {}
    for name, shapes in POLYGONS.items():
        path = ART / (name + '.png')
        value = guid(path)
        image = Image.new('RGBA', (512, 512))
        draw = ImageDraw.Draw(image)
        for points, shade in shapes:
            draw.polygon([(16+x*480, 496-y*480) for x,y in points], fill=(shade,shade,shade,255))
        image.save(path)
        meta = re.sub(r'^guid: \w+', 'guid: ' + value, template, flags=re.M)
        for key, val in [('maxTextureSize','512'), ('spritePixelsToUnits','512'), ('filterMode','1'), ('textureCompression','0'), ('spriteGenerateFallbackPhysicsShape','0')]:
            meta = re.sub(r'(?m)^(\s*'+key+r':) .*$', r'\g<1> '+val, meta)
        Path(str(path)+'.meta').write_text(meta)
        result[name] = value
    return result


# Joint coordinates are local to their parent, in rig units; Y points forward/up.
# Art lives under joints, leaving clean transforms for animation tracks.
NODES = [
    ('Rig',None,(0,0),0,None),
    ('Hips','Rig',(0,-.09),0,None),
    ('LeftHip','Hips',(-.15,0),-7,None),
    ('LeftThigh','LeftHip',(0,-.04),0,('Thigh',.22,.18,10)),
    ('LeftKnee','LeftHip',(0,-.12),8,None),
    ('LeftShin','LeftKnee',(0,-.035),0,('Shin',.17,.13,11)),
    ('LeftAnkle','LeftKnee',(0,-.09),0,None),
    ('LeftFoot','LeftAnkle',(0,-.055),0,('Foot',.19,.18,12)),
    ('RightHip','Hips',(.15,0),7,None),
    ('RightThigh','RightHip',(0,-.04),0,('Thigh',.22,.18,10)),
    ('RightKnee','RightHip',(0,-.12),-8,None),
    ('RightShin','RightKnee',(0,-.035),0,('Shin',.17,.13,11)),
    ('RightAnkle','RightKnee',(0,-.09),0,None),
    ('RightFoot','RightAnkle',(0,-.05),0,('Foot',.19,.18,12)),
    ('Spine','Hips',(0,.15),0,None),
    ('Torso','Spine',(0,0),0,('Torso',.72,.38,20)),
    ('LeftShoulder','Spine',(-.33,.09),145,None),
    ('LeftUpperArm','LeftShoulder',(0,.065),0,('UpperArm',.20,.27,21)),
    ('LeftElbow','LeftShoulder',(0,.19),-25,None),
    ('LeftForearm','LeftElbow',(0,.065),0,('Forearm',.19,.24,22)),
    ('LeftWrist','LeftElbow',(0,.19),0,None),
    ('LeftHand','LeftWrist',(0,.045),0,('Hand',.14,.15,23)),
    ('LeftPauldron','LeftShoulder',(0,-.005),0,('Shoulder',.30,.25,24)),
    ('RightShoulder','Spine',(.27,.05),0,None),
    ('RightUpperArm','RightShoulder',(0,.045),0,('UpperArm',.17,.20,21)),
    ('RightElbow','RightShoulder',(0,.13),0,None),
    ('RightForearm','RightElbow',(0,.065),0,('Forearm',.15,.20,22)),
    ('RightWrist','RightElbow',(0,.15),0,None),
    ('RightHand','RightWrist',(0,.04),0,('Hand',.14,.15,25)),
    ('Grip','RightWrist',(0,.06),0,None),
    ('Pistol','Grip',(0,.13),0,('Pistol',.15,.36,24)),
    ('Muzzle','Grip',(0,.29875),0,None),
    ('RightPauldron','RightShoulder',(0,0),0,('Shoulder',.30,.25,24)),
    ('Neck','Spine',(0,.025),0,None),
    ('Head','Neck',(0,.035),0,('Head',.46,.43,30)),
]


# Preserve editing joints while excluding occluded limbs from the overhead silhouette.
HIDDEN_NODES = {"LeftHip", "RightHip", "LeftShoulder", "RightPauldron"}

def build_prefab(ui, sprite_guids, sprite_template, image_template, script_guid):
    out = []
    ids = {n[0]: 1000+i*10 for i,n in enumerate(NODES)}
    scale = 60 if ui else 1
    def emit(kind, id, body): out.append(f'--- !u!{kind} &{id}\n{body}')
    base = '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
    for name,parent,pos,angle,art in NODES:
        id = ids[name]
        components = [id+1] + ([id+2,id+3] if art and ui else [id+2] if art else [])
        if not ui and parent is None: components.append(1999)
        body = 'GameObject:\n'+base+'  serializedVersion: 6\n  m_Component:\n'+''.join(f'  - component: {{fileID: {c}}}\n' for c in components)
        body += f'  m_Layer: {5 if ui else 0}\n  m_Name: {"GeometricPlayerUI" if ui else "GeometricPlayer" if parent is None else name}\n'
        # Non-root names are shared between world and UI rigs for animation bindings.
        if parent is not None: body = re.sub(r'  m_Name: .*', '  m_Name: '+name, body)
        body += f'  m_TagString: Untagged\n  m_IsActive: {0 if name in HIDDEN_NODES else 1}\n';emit(1,id,body)
        size = (art[1],art[2]) if art else (1,1)
        z,w = math.sin(math.radians(angle)/2),math.cos(math.radians(angle)/2)
        body = ('RectTransform:\n' if ui else 'Transform:\n')+base+f'  m_GameObject: {{fileID: {id}}}\n'
        body += f'  m_LocalRotation: {{x: 0, y: 0, z: {z}, w: {w}}}\n  m_LocalPosition: {{x: {pos[0]*scale}, y: {pos[1]*scale}, z: 0}}\n'
        body += f'  m_LocalScale: {{x: {1 if ui else size[0]}, y: {1 if ui else size[1]}, z: 1}}\n  m_ConstrainProportionsScale: 0\n'
        children = [ids[n[0]]+1 for n in NODES if n[1]==name]
        body += '  m_Children:'+ ('\n'+''.join(f'  - {{fileID: {c}}}\n' for c in children) if children else ' []\n')
        body += f'  m_Father: {{fileID: {ids[parent]+1 if parent else 0}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: {angle}}}\n'
        if ui:
            body += f'  m_AnchorMin: {{x: 0.5, y: 0.5}}\n  m_AnchorMax: {{x: 0.5, y: 0.5}}\n  m_AnchoredPosition: {{x: {pos[0]*scale}, y: {pos[1]*scale}}}\n  m_SizeDelta: {{x: {size[0]*scale}, y: {size[1]*scale}}}\n  m_Pivot: {{x: 0.5, y: 0.5}}\n'
        emit(224 if ui else 4,id+1,body)
        if art:
            if ui:
                emit(222,id+2,'CanvasRenderer:\n'+base+f'  m_GameObject: {{fileID: {id}}}\n  m_CullTransparentMesh: 1\n')
            body = image_template if ui else sprite_template
            body = field(body,'m_GameObject',f'{{fileID: {id}}}')
            body = field(body,'m_Sprite',f'{{fileID: 21300000, guid: {sprite_guids[art[0]]}, type: 3}}')
            body = field(body,'m_Color','{r: 1, g: 1, b: 1, a: 1}')
            if ui: body = field(body,'m_PreserveAspect','0')
            else: body = field(body,'m_SortingOrder',str(art[3]))
            emit(114 if ui else 212,id+3 if ui else id+2,body)
    if not ui:
        body = 'MonoBehaviour:\n'+base+'  m_GameObject: {fileID: 1000}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'+f'  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n'
        body += f'  grip: {{fileID: {ids["Grip"]+1}}}\n  muzzle: {{fileID: {ids["Muzzle"]+1}}}\n  parts:\n'
        body += ''.join(f'  - {{fileID: {ids[n[0]]+2}}}\n' for n in NODES if n[4])
        emit(114,1999,body)
    path = PREFABS / ('GeometricPlayerUI.prefab' if ui else 'GeometricPlayer.prefab')
    path.write_text(HEADER+''.join(out),encoding='utf-8')
    guid(path,'PrefabImporter')
    return ids


def main():
    ART.mkdir(parents=True,exist_ok=True);PREFABS.mkdir(parents=True,exist_ok=True)
    sprite_guids = textures()
    script_guid = guid(ROOT/'Assets/Scripts/RoomRhythm/GeometricPlayerRig.cs','MonoImporter')
    # Component templates are versioned beside this script; no scene reads are needed.
    sprite = (Path(__file__).parent / 'Templates/SpriteRenderer.template').read_text()
    image = (Path(__file__).parent / 'Templates/Image.template').read_text()
    for ui_mode in (False,True): build_prefab(ui_mode,sprite_guids,sprite,image,script_guid)
    print('Authored ten polygon textures and world/UI rig prefabs.')


if __name__ == '__main__': main()
