"""Offline polygon authoring for a top-down rock boss. Requires Pillow.

Rebuilds only this boss's textures, prefab and preview; preserves asset GUIDs.
Do not rerun after editing the prefab without saving those edits separately.
"""
from pathlib import Path
import math
import re
import time
from PIL import Image, ImageDraw
from BuildGeometricPlayer import ROOT, HEADER, guid, field

ART = ROOT / 'Assets/Arts/RockBoss'
PREFAB = ROOT / 'Assets/Prefabs/Characters/RockBoss.prefab'

# Large, flat rock facets: monochrome, no realistic texture or front-facing face.
SHAPES = {
    'Mantle': [([(.02,.53),(.14,.83),(.35,.98),(.71,.91),(.97,.65),(.91,.29),(.68,.03),(.25,.07)],100),
               ([(.14,.83),(.35,.98),(.71,.91),(.65,.64),(.37,.47)],178),
               ([(.02,.53),(.14,.83),(.37,.47),(.25,.07),(.08,.32)],132),
               ([(.37,.47),(.65,.64),(.97,.65),(.91,.29),(.68,.03),(.58,.36)],65),
               ([(.36,.47),(.4,.43),(.32,.22),(.4,.09),(.34,.1),(.26,.22)],28)],
    'Shoulder': [([(0,.36),(.1,.72),(.04,.85),(.37,1),(.51,.91),(.72,.98),(1,.62),(.92,.28),(.66,.04),(.25,0)],115),
                 ([(.1,.72),(.37,1),(.51,.91),(.72,.98),(.78,.57),(.46,.43)],205),
                 ([(0,.36),(.1,.72),(.46,.43),(.25,0)],155),
                 ([(.46,.43),(.78,.57),(1,.62),(.92,.28),(.66,.04),(.58,.31)], 70),
                 ([(.48,.88),(.53,.89),(.57,.68),(.47,.55),(.51,.39),(.47,.42),(.42,.57),(.52,.7)],30)],
    'Arm': [([(.16,.98),(.77,.94),(1,.66),(.81,.16),(.52,0),(.08,.19),(0,.6)],90),
            ([(.16,.98),(.77,.94),(.67,.59),(.14,.4),(0,.6)],155),
            ([(.14,.4),(.67,.59),(.81,.16),(.52,0),(.08,.19)],115)],
    'Forearm': [([(.19,1),(.82,.91),(1,.55),(.82,.13),(.57,0),(.13,.1),(0,.51)],110),
                ([(.19,1),(.82,.91),(.68,.61),(.29,.43),(0,.51)],185),
                ([(.29,.43),(.68,.61),(1,.55),(.82,.13),(.57,0),(.52,.33)],65),
                ([(.19,.84),(.26,.77),(.21,.55),(.3,.37),(.22,.44),(.15,.57)],35)],
    'Fist': [([(.06,.78),(.19,.98),(.4,.92),(.55,1),(.76,.92),(.96,.73),(1,.4),(.8,.08),(.34,0),(.04,.22)],125),
             ([(.06,.78),(.19,.98),(.4,.92),(.55,1),(.76,.92),(.96,.73),(.73,.48),(.27,.5)],205),
             ([(.04,.22),(.06,.78),(.27,.5),(.34,0)],160),
             ([(.73,.48),(.96,.73),(1,.4),(.8,.08),(.55,.14)],70),
             ([(.38,.88),(.42,.87),(.43,.65),(.36,.48),(.37,.64)],38),
             ([(.7,.86),(.74,.84),(.69,.62),(.61,.53),(.65,.68)],38)],
    'Crown': [([(.16,.73),(.39,1),(.7,.87),(.93,.54),(.8,.14),(.44,0),(.1,.26),(0,.53)],110),
              ([(.16,.73),(.39,1),(.7,.87),(.61,.56),(.37,.4),(0,.53)],220),
              ([(.61,.56),(.7,.87),(.93,.54),(.8,.14),(.53,.22)],65),
              ([(.37,.4),(.53,.22),(.44,0),(.1,.26),(0,.53)],145)],
    'Ridge': [([(.05,.22),(.14,.75),(.42,1),(.75,.82),(1,.29),(.62,0)],105),
              ([(.05,.22),(.14,.75),(.42,1),(.4,.4)],190),
              ([(.42,1),(.75,.82),(1,.29),(.4,.4)],145)],
    'Core': [([(.15,.75),(.5,1),(.88,.71),(1,.38),(.61,0),(.19,.08),(0,.39)],32),
             ([(.27,.66),(.5,.81),(.72,.6),(.73,.33),(.54,.18),(.29,.24),(.19,.42)],220),
             ([(.5,.81),(.72,.6),(.73,.33),(.54,.18),(.5,.46)],155)],
}

# Keep joint scale at one. Only leaf artwork has nonuniform scale.
NODES = [
    ('RockBoss',None,(0,0),0,None),
    ('Motion','RockBoss',(0,0),0,None),
    ('Body','Motion',(0,0),0,None),
    ('Mantle','Body',(0,-.10),0,('Mantle',2.0,1.65,20)),
    ('BackLeft','Body',(-.45,-.4),-28,None),
    ('BackLeftRock','BackLeft',(0,0),0,('Ridge',.59,.63,21)),
    ('BackCenter','Body',(0,-.5),0,None),
    ('BackCenterRock','BackCenter',(0,0),0,('Ridge',.57,.64,22)),
    ('BackRight','Body',(.45,-.38),24,None),
    ('BackRightRock','BackRight',(0,0),0,('Ridge',.59,.63,21)),
    ('CorePivot','Body',(0,-.24),0,None),
    ('Core','CorePivot',(0,0),0,('Core',.45,.5,25)),
    ('CoreTarget','CorePivot',(0,0),0,None),
    ('Neck','Body',(0,.48),0,None),
    ('Head','Neck',(0,.11),0,('Crown',.76,.83,31)),
    ('LeftShoulder','Body',(-.87,.12),24,None),
    ('LeftUpperArm','LeftShoulder',(0,.23),0,('Arm',.56,.77,16)),
    ('LeftElbow','LeftShoulder',(0,.58),-12,None),
    ('LeftForearm','LeftElbow',(0,.23),0,('Forearm',.66,.75,18)),
    ('LeftWrist','LeftElbow',(0,.53),0,None),
    ('LeftFist','LeftWrist',(0,.21),0,('Fist',.94,.85,26)),
    ('LeftSlamContact','LeftWrist',(0,.55),0,None),
    ('LeftShoulderRock','LeftShoulder',(0,.02),-10,('Shoulder',1.23,1.04,29)),
    ('LeftOuterShard','LeftShoulder',(-.39,-.10),-48,None),
    ('LeftOuterRock','LeftOuterShard',(0,0),0,('Ridge',.39,.65,30)),
    ('RightShoulder','Body',(.91,.09),-29,None),
    ('RightUpperArm','RightShoulder',(0,.25),0,('Arm',.6,.79,16)),
    ('RightElbow','RightShoulder',(0,.58),17,None),
    ('RightForearm','RightElbow',(0,.23),0,('Forearm',.68,.77,18)),
    ('RightWrist','RightElbow',(0,.53),0,None),
    ('RightFist','RightWrist',(0,.24),-5,('Fist',1.0,.91,26)),
    ('RightSlamContact','RightWrist',(0,.6),0,None),
    ('RightShoulderRock','RightShoulder',(0,.02),8,('Shoulder',1.3,1.12,29)),
    ('RightOuterShard','RightShoulder',(.41,-.12),45,None),
    ('RightOuterRock','RightOuterShard',(0,0),0,('Ridge',.44,.71,30)),
    ('GroundCenter','RockBoss',(0,0),0,None),
    ('Forward','RockBoss',(0,2.1),0,None),
]


def main():
    ART.mkdir(parents=True,exist_ok=True)
    template = (ROOT/'Assets/Arts/Solid.png.meta').read_text(encoding='utf-8-sig')
    sprites = {}
    for name, shapes in SHAPES.items():
        path = ART/(name+'.png'); sprites[name] = guid(path)
        im = Image.new('RGBA',(512,512)); draw = ImageDraw.Draw(im)
        for polygon,shade in shapes:
            draw.polygon([(16+x*480,496-y*480) for x,y in polygon],fill=(shade,shade,shade,255))
        meta = re.sub(r'^guid: \w+','guid: '+sprites[name],template,flags=re.M)
        for key,val in [('maxTextureSize','512'),('spritePixelsToUnits','512'),('filterMode','1'),('textureCompression','0'),('spriteGenerateFallbackPhysicsShape','0')]:
            meta = re.sub(r'(?m)^(\s*'+key+r':) .*$',r'\g<1> '+val,meta)
        for attempt in range(20):
            try:
                Path(str(path)+'.meta').write_text(meta)
                break
            except PermissionError:
                if attempt == 19: raise
                time.sleep(.1)
        im.save(path)
    renderer = (Path(__file__).parent / 'Templates/SpriteRenderer.template').read_text()
    ids = {n[0]:1000+i*10 for i,n in enumerate(NODES)}
    base = '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
    out=[]
    def emit(kind,id,body):out.append(f'--- !u!{kind} &{id}\n{body}')
    for name,parent,pos,angle,art in NODES:
        id=ids[name];components=[id+1]+([id+2] if art else [])+([1999] if parent is None else [])
        emit(1,id,'GameObject:\n'+base+'  serializedVersion: 6\n  m_Component:\n'+''.join(f'  - component: {{fileID: {c}}}\n' for c in components)+f'  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_IsActive: 1\n')
        size=art[1:3] if art else (1,1)
        children=[ids[n[0]]+1 for n in NODES if n[1]==name]
        body='Transform:\n'+base+f'  m_GameObject: {{fileID: {id}}}\n  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: {math.sin(math.radians(angle)/2)}, w: {math.cos(math.radians(angle)/2)}}}\n  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: 0}}\n  m_LocalScale: {{x: {size[0]}, y: {size[1]}, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children:'
        body+=('\n'+''.join(f'  - {{fileID: {c}}}\n' for c in children)) if children else ' []\n'
        body+=f'  m_Father: {{fileID: {ids[parent]+1 if parent else 0}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: {angle}}}\n'
        emit(4,id+1,body)
        if art:
            body=field(renderer,'m_GameObject',f'{{fileID: {id}}}')
            body=field(body,'m_Sprite',f'{{fileID: 21300000, guid: {sprites[art[0]]}, type: 3}}')
            body=field(body,'m_SortingOrder',str(art[3]));emit(212,id+2,body)
    emit(95,1999,'Animator:\n'+base+'  m_GameObject: {fileID: 1000}\n  m_Enabled: 1\n  m_Avatar: {fileID: 0}\n  m_Controller: {fileID: 0}\n  m_CullingMode: 0\n  m_UpdateMode: 0\n  m_ApplyRootMotion: 0\n  m_LinearVelocityBlending: 0\n  m_StabilizeFeet: 0\n  m_WarningMessage: \n  m_HasTransformHierarchy: 1\n  m_AllowConstantClipSamplingOptimization: 1\n  m_KeepAnimatorStateOnDisable: 0\n  m_WriteDefaultValuesOnDisable: 0\n')
    PREFAB.write_text(HEADER+''.join(out),encoding='utf-8');guid(PREFAB,'PrefabImporter')
    preview()
    print(f'Authored RockBoss: {len(SHAPES)} sprites, {sum(bool(n[4]) for n in NODES)} separate parts, editable joints and impact markers.')


def preview():
    poses={};parts=[]
    for name,parent,pos,angle,art in NODES:
        px,py,pa=poses[parent] if parent else (0,0,0);r=math.radians(pa)
        x=px+pos[0]*math.cos(r)-pos[1]*math.sin(r);y=py+pos[0]*math.sin(r)+pos[1]*math.cos(r)
        poses[name]=(x,y,pa+angle)
        if art:parts.append((art[3],x,y,pa+angle,art))
    im=Image.new('RGB',(1100,1000),(17,17,19));draw=ImageDraw.Draw(im)
    for _,x,y,a,art in sorted(parts):
        r=math.radians(a)
        for polygon,shade in SHAPES[art[0]]:
            points=[]
            for u,v in polygon:
                dx=(u-.5)*art[1]*.9375;dy=(v-.5)*art[2]*.9375
                points.append((550+(x+dx*math.cos(r)-dy*math.sin(r))*215,565-(y+dx*math.sin(r)+dy*math.cos(r))*215))
            draw.polygon(points,fill=(shade,shade,shade))
    destination = Path(__file__).parent / 'Previews/RockBossPreview.png'
    destination.parent.mkdir(exist_ok=True)
    im.save(destination)


if __name__=='__main__':main()

