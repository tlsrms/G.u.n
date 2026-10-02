"""Offline authoring for MafiaStage01. Rebuild overwrites this stage and its charts,
updates the matching hub record, and registers the scene in Build Settings.
Uses existing room geometry, linked character prefabs and authored projectile paths.
"""
from pathlib import Path
import re, uuid, math, json

ROOT = Path(__file__).resolve().parents[2]
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
BASE = 1000000000000

def read(p): return (ROOT / p).read_text(encoding='utf-8-sig')
def guid(p, importer='DefaultImporter'):
    path = ROOT / (str(p) + '.meta')
    if not path.exists():
        path.parent.mkdir(parents=True, exist_ok=True)
        extra = '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n' if importer == 'MonoImporter' else ('  mainObjectFileID: 11400000\n' if importer == 'NativeFormatImporter' else '')
        folder = 'folderAsset: yes\n' if (ROOT / p).is_dir() else ''
        path.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+folder+importer+':\n  externalObjects: {}\n'+extra+'  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
    return re.search(r'^guid: (\w+)', path.read_text(), re.M)[1]
def blocks(s): return {int(i): [int(k), st or '', b] for k,i,st,b in re.findall(r'^--- !u!(\d+) &(\d+)( stripped)?\n(.*?)(?=^--- !u!|\Z)', s, re.M|re.S)}
def field(s,k,v):
    s,n=re.subn(r'^  '+re.escape(k)+r':[^\n]*', '  '+k+': '+str(v),s,flags=re.M)
    assert n==1,(k,n)
    return s
def refs(s,k,ids):
    return re.sub(r'^  '+k+r':[^\n]*\n(?:  - [^\n]*\n)*', '  '+k+':'+ ('\n'+''.join('  - {fileID: '+str(i)+'}\n' for i in ids) if ids else ' []\n'),s,flags=re.M)
def ref(i): return '{fileID: '+str(i)+'}'
def vec(x=0,y=0,z=0): return '{x: '+str(x)+', y: '+str(y)+', z: '+str(z)+'}'
def emit(value, indent=2):
    pad=' '*indent
    if isinstance(value,dict):
        out=''
        for k,v in value.items():
            if isinstance(v,(dict,list)) and v: out+=pad+k+':\n'+emit(v,indent+2 if isinstance(v,dict) else indent)
            else: out+=pad+k+': '+ ('[]' if v==[] else scalar(v))+'\n'
        return out
    out=''
    for item in value:
        t=emit(item,indent+2)
        out+=pad+'- '+t[indent+2:]
    return out
def scalar(v):
    if isinstance(v,str): return json.dumps(v,ensure_ascii=False)
    return str(v)

source=blocks(read('Assets/Scenes/Tutorials/Stage05.unity'))
objects={i:v.copy() for i,v in source.items()}
removed={i for i in objects if 3000000000<=i<3000010000 or 9600000000<=i<9600000100}
for i,(k,st,b) in objects.items():
    if st and re.search(r'm_PrefabInstance: \{fileID: 9600000',b): removed.add(i)
for i in removed: objects.pop(i,None)
for i in objects:
    objects[i][2]=re.sub(r'^  - \{fileID: (\d+)\}\n',lambda m:'' if int(m[1]) in removed else m[0],objects[i][2],flags=re.M)
counter=BASE
def alloc():
    global counter
    counter+=1;return counter
def add(k,b,st=''):
    i=alloc();objects[i]=[k,st,b];return i
def children(t,ids): objects[t][2]=refs(objects[t][2],'m_Children',ids)
def group(name,parent=0,position=(0,0),active=1):
    g=alloc();t=alloc()
    objects[g]=[1,'',field(field(refs(source[840000820][2],'m_Component',[]),'m_Name',name),'m_IsActive',active).replace('  m_Component: []','  m_Component:\n  - component: '+ref(t))]
    b=source[820000541][2]
    b=field(field(field(b,'m_GameObject',ref(g)),'m_Father',ref(parent)),'m_LocalPosition',vec(*position))
    objects[t]=[4,'',refs(b,'m_Children',[])]
    if parent:
        existing=list(map(int,re.findall(r'^  - \{fileID: (\d+)\}',objects[parent][2],re.M)))
        children(parent,existing+[t])
    return g,t
def component(go,script,data):
    b=source[820000545][2].split('  chart:')[0]
    b=field(field(b,'m_GameObject',ref(go)),'m_Script','{fileID: 11500000, guid: '+guid('Assets/Scripts/RoomRhythm/'+script+'.cs','MonoImporter')+', type: 3}')
    i=add(114,b+data)
    objects[go][2]=objects[go][2].replace('  m_Layer:', '  - component: '+ref(i)+'\n  m_Layer:')
    return i

def instance(prefab,parent,name,position=(0,0),mods=(),wanted=(1001,1002)):
    pg=guid(prefab,'PrefabImporter'); pb=blocks(read(prefab)); inst=alloc();ids={}
    for sid in wanted:
        k,_,b=pb[sid]
        sb=b.split('\n')[0]+'\n  m_CorrespondingSourceObject: {fileID: '+str(sid)+', guid: '+pg+', type: 3}\n  m_PrefabInstance: '+ref(inst)+'\n  m_PrefabAsset: {fileID: 0}\n'
        if k==114: sb+='  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  '+re.search(r'm_Script:.*',b)[0]+'\n  m_Name: \n  m_EditorClassIdentifier: \n'
        ids[sid]=add(k,sb,' stripped')
    entries=[(1000,'m_Name',name,None),(1001,'m_LocalPosition.x',position[0],None),(1001,'m_LocalPosition.y',position[1],None),(1001,'m_LocalPosition.z',0,None)]+list(mods)
    b='PrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: '+ref(parent)+'\n    m_Modifications:\n'
    for sid,prop,value,obj in entries:
        b+='    - target: {fileID: '+str(sid)+', guid: '+pg+', type: 3}\n      propertyPath: '+prop+'\n      value: '+str(value)+'\n      objectReference: '+ref(obj or 0)+'\n'
    b+='    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {fileID: 100100000, guid: '+pg+', type: 3}\n'
    objects[inst]=[1001,'',b]
    existing=list(map(int,re.findall(r'^  - \{fileID: (\d+)\}',objects[parent][2],re.M)))
    children(parent,existing+[ids[1001]])
    return ids

normalGo,normalRoot=group('01 - Guards / authored rooms',820000021)
bossGo,bossRoot=group('02 - Mafia / authored rooms',820000021)
propGo,propRoot=group('Mafia targets and paths')
presentationGo,presentationRoot=group('Mafia presentation')
normal=[];boss=[]
def make_route(count,startbeat,origin,prefix):
    route=[];x,y=origin
    for i in range(count):
        w,h=(6,6) if prefix=='guard' else ((12,6) if i%6==2 else (6,12) if i%6==5 else (6,6))
        direction=3 if i%4!=0 else 0
        if i:
            prev=route[-1]
            if direction==3:x+=(prev['w']+w)/2
            else:y+=(prev['h']+h)/2
        route.append(dict(id=prefix+str(i),x=x,y=y,w=w,h=h,beat=startbeat+i*8,direction=direction,door=i>0 and i%4==0))
    return route
normal=make_route(16,0,(0,0),'guard')
boss=make_route(28,144,(normal[-1]['x'],normal[-1]['y']),'mafia')
def build_rooms(route,parent):
    for n,r in enumerate(route):
        mapping={i:alloc() for i in range(3000000000,3000000075)}
        for old,new in mapping.items():
            k,st,b=source[old]
            b=re.sub(r'\{fileID: (\d+)\}',lambda m:ref(mapping.get(int(m[1]),int(m[1]))),b)
            objects[new]=[k,st,b]
        r['binding']=mapping[3000000062];r['root']=mapping[3000000061]
        root=r['root']; b=objects[root][2]
        objects[root][2]=field(field(b,'m_Father',ref(parent)),'m_LocalPosition',vec(r['x'],r['y']))
        objects[mapping[3000000060]][2]=field(objects[mapping[3000000060]][2],'m_Name',r['id']+' - '+str(r['beat'])+' beat')
        bind=objects[r['binding']][2];bind=field(bind,'roomId',r['id'])+'  dimensions: '+vec(r['w'],r['h']).replace(', z: 0','')+'\n'
        objects[r['binding']][2]=bind
        floor=mapping[3000000001];objects[floor][2]=field(objects[floor][2],'m_LocalScale',vec(r['w'],r['h'],1))
        d=r['direction'];dx,dy=((1,0) if d==3 else (0,1));door=mapping[3000000009]
        objects[door][2]=field(field(objects[door][2],'m_LocalPosition',vec(-dx*r['w']/2,-dy*r['h']/2)),'m_LocalRotation','{x: 0, y: 0, z: '+str(math.sin(math.pi/4) if dx else 0)+', w: '+str(math.cos(math.pi/4) if dx else 1)+'}')
    children(parent,[r['root'] for r in route])
build_rooms(normal,normalRoot);build_rooms(boss,bossRoot)

def chart_data(route,prefix):
    moves=[];enemies=[];mr=[];me=[]
    for i,r in enumerate(route):
        duration=.28 if prefix=='guard' else (.65 if max(r['w'],r['h'])>6 else .32)
        mr.append(dict(id=r['id'],x=0,y=0,width=r['w'],height=r['h'],offsetX=r['x'],offsetY=r['y'],moveDuration=duration,moveEase=i%4,
            hitBeat=r['beat'],frameBeat=max(0,r['beat']-4),individualAppearance=1,appearBeat=max(0,r['beat']-4),door=int(r['door']),doorBeat=r['beat']-1 if i else 0,doorFrameBeat=max(0,r['beat']-3)))
        if i:
            moves.append(dict(destinationId=r['id'],direction=r['direction'],time=r['beat']*60/130,duration=duration,ease=i%4,
                customAppearance=1,appearanceTime=(r['beat']-4)*60/130,frameStartTime=(r['beat']-4)*60/130,doorFrameStartTime=(r['beat']-3)*60/130,
                hasDoor=int(r['door']),doorTime=(r['beat']-1)*60/130,moveDelay=60/130 if r['door'] else 0))
        for j,offset in enumerate([2,4,6]):
            hit=r['beat']+offset; eid=prefix+'_shot_'+str(i)+'_'+str(j); direction=[0,2,6][(i+j)%3]
            appear=hit-(3 if prefix=='guard' else 1.5)
            appear=max(0,appear)
            enemies.append(dict(id=eid,roomId=r['id'],direction=direction,time=hit*60/130,customAppearance=1,appearanceTime=appear*60/130,frameStartTime=appear*60/130))
            me.append(dict(id=eid,roomId=r['id'],direction=direction,hitBeat=hit,appearBeat=appear,frameBeat=appear))
    settings=dict(bpm=130,offsetSeconds=0,musicDelaySeconds=0,loopMusic=0,beatsPerBar=4,subdivision=2,roomLeadBeats=4,enemyLeadBeats=3,startingRoomId=route[0]['id'],notes=[])
    mp=dict(settings=settings,roomSize=6,originX=0,originY=0,cameraX=route[0]['x'],cameraY=route[0]['y'],cameraSize=7,cameraTrack=0,rooms=mr,enemies=me,groups=[],cameras=[],shakes=[])
    return moves,enemies,mp

def write_chart(route,prefix):
    moves,enemies,mp=chart_data(route,prefix)
    name='Stage1_'+('Guards' if prefix=='guard' else 'Mafia');path='Assets/RoomChart/'+name+'.asset'
    text=read('Assets/RoomChart/Tutorial_5.asset').split('  moves:')[0]
    text=field(field(field(field(field(text,'m_Name',name),'bpm',130),'inputOffsetMs',0),'startingRoomId',route[0]['id']),'music','{fileID: 8300000, guid: '+guid('Assets/Audio/Stage1_Bpm130.mp3')+', type: 3}')
    for k,v in [('moveDuration',.28),('roomFrameStartSize',20),('roomLeadTime',4*60/130),('enemyLeadTime',3*60/130)]:text=field(text,k,v)
    text+='  songTitle: Stage1_Bpm130\n'+emit(dict(moves=moves,enemies=enemies,mapDraft=mp,appliedMap=mp))
    text+='  mapDraftMusic: {fileID: 8300000, guid: '+guid('Assets/Audio/Stage1_Bpm130.mp3')+', type: 3}\n'
    (ROOT/path).write_text(text,encoding='utf-8');guid(path,'NativeFormatImporter')
    return path,moves,enemies
normalChart,normalMoves,normalNotes=write_chart(normal,'guard')
bossChart,bossMoves,bossNotes=write_chart(boss,'mafia')

enemyRefs=[]
for n in normalNotes:
    r=next(r for r in normal if r['id']==n['roomId']);a=(90-45*n['direction'])*math.pi/180
    ids=instance('Assets/Prefabs/Characters/RegularEnemy.prefab',r['root'],n['id'],(math.cos(a)*2.2,math.sin(a)*2.2),[(1002,'enemyId',n['id'],None)])
    enemyRefs.append(ids[1002])
objects[840000822][2]=refs(objects[840000822][2],'enemies',enemyRefs)
objects[840000820][2]=field(objects[840000820][2],'m_Name','Normal combat')
bossCombatGo,bossCombatRoot=group('Boss combat')
bossCombat=component(bossCombatGo,'RoomCombat','  enemies: []\n  selectedEnemyDot: {fileID: 840000841}\n  player: {fileID: 820000481}\n')

targets=[];shotCues=[]
for n in bossNotes:
    r=next(r for r in boss if r['id']==n['roomId'])
    _,start=group(n['id']+' / muzzle anchor',propRoot,(r['x'],r['y']+2.8))
    _,end=group(n['id']+' / impact anchor',propRoot,(r['x'],r['y']))
    ids=instance('Assets/Prefabs/Characters/StagePropTarget.prefab',propRoot,n['id'],(r['x'],r['y']+2.8),[
        (1002,'targetId',n['id'],None),(1002,'moving',1,None),(1002,'pathStart','',start),(1002,'pathEnd','',end),
        (1006,'m_LocalScale.x',.14,None),(1006,'m_LocalScale.y',.32,None)])
    targets.append(ids[1002]);shotCues.append(dict(time=n['appearanceTime'],target=vec(r['x'],r['y'])))
for i,r in enumerate(boss):
    if not i:continue
    prev=boss[i-1];shotCues.append(dict(time=r['beat']*60/130,target=vec(prev['x'],prev['y'])))
    if r['door']:
        pos=(r['x']-r['w']/2,r['y']) if r['direction']==3 else (r['x'],r['y']-r['h']/2)
        ids=instance('Assets/Prefabs/Characters/StagePropTarget.prefab',propRoot,'Barricade '+r['id'],pos,[(1002,'targetId',r['id'],None),(1002,'role',1,None),(1006,'m_LocalScale.x',.65,None),(1006,'m_LocalScale.y',.65,None)])
        targets.append(ids[1002])
objects[bossCombat][2]+='  stageTargets:\n'+''.join('  - '+ref(i)+'\n' for i in targets)

bossIds=instance('Assets/Prefabs/Characters/MafiaBoss.prefab',presentationRoot,'Mafia boss',(normal[-1]['x'],normal[-1]['y']+9),[(1000,'m_IsActive',0,None)],wanted=(1001,1021,1121,1321))
def beam(name,width,color):
    g,t=group(name,presentationRoot)
    lb=blocks(read('Assets/Prefabs/Characters/RegularEnemy.prefab'))[1010][2]
    lb=field(field(lb,'m_GameObject',ref(g)),'m_Enabled',0)
    lb=re.sub(r'^  m_Positions:\n(?:  - .*\n)*','  m_Positions:\n  - {x: 0, y: 0, z: 0}\n  - {x: 0, y: 1, z: 0}\n',lb,flags=re.M)
    lb=field(field(lb,'m_UseWorldSpace',1),'m_Loop',0)
    lb=re.sub(r'widthMultiplier: [^\n]+','widthMultiplier: '+str(width),lb)
    lb=re.sub(r'key[0-7]: \{[^\n]+\}',lambda m:m[0].split(':')[0]+': '+color,lb)
    line=add(120,lb);objects[g][2]=objects[g][2].replace('  m_Layer:','  - component: '+ref(line)+'\n  m_Layer:')
    return line
warning=beam('Attack warning',.018,'{r: 0.6, g: 0.6, b: 0.6, a: 0.35}')
shot=beam('AK muzzle trace',.025,'{r: 1, g: 1, b: 1, a: 1}')
directorData=''.join('  '+k+': '+ref(v)+'\n' for k,v in dict(boss=bossIds[1001],body=bossIds[1021],rifleArm=bossIds[1121],muzzle=bossIds[1321],player=820000481,warning=warning,shot=shot,feedback=860000022).items())
directorData+='  bpm: 130\n  entranceBeat: 128\n  battleBeat: 144\n  finishBeat: 384\n  shots:\n'
for cue in sorted(shotCues,key=lambda c:c['time']):directorData+='  - time: '+str(cue['time'])+'\n    target: '+cue['target']+'\n'
director=component(820000540,'MafiaStageDirector',directorData)
reset=component(820000540,'StageResetState','  roots:\n  - '+ref(bossIds[1001])+'\n')
ss=objects[820000545][2];ss=field(ss,'chart','{fileID: 11400000, guid: '+guid(normalChart)+', type: 2}')
ss=refs(ss,'rooms',[r['binding'] for r in normal])
ss+='  stageDirector: '+ref(director)+'\n  resetState: '+ref(reset)+'\n  sections:\n  - id: mafia\n    chart: {fileID: 11400000, guid: '+guid(bossChart)+', type: 2}\n    rooms:\n'
ss+=''.join('    - '+ref(r['binding'])+'\n' for r in boss)+'    combat: '+ref(bossCombat)+'\n'
objects[820000545][2]=ss
objects[820000481][2]=field(objects[820000481][2],'m_LocalPosition',vec())
objects[4100000013][2]=field(objects[4100000013][2],'entrySafeScene','"StageSelectScene"')
roots=9223372036854775807
objects[roots][2]+=''.join('  - '+ref(t)+'\n' for t in [propRoot,presentationRoot,bossCombatRoot])

scene='Assets/Scenes/Stages/MafiaStage01.unity'
(ROOT/scene).parent.mkdir(parents=True,exist_ok=True)
guid('Assets/Scenes/Stages')
(ROOT/scene).write_text(HEADER+''.join('--- !u!'+str(k)+' &'+str(i)+st+'\n'+b for i,(k,st,b) in objects.items()),encoding='utf-8')
guid(scene)
build=read('ProjectSettings/EditorBuildSettings.asset')
if scene not in build: build=build.replace('  m_configObjects:','  - enabled: 1\n    path: '+scene+'\n    guid: '+guid(scene)+'\n  m_configObjects:')
(ROOT/'ProjectSettings/EditorBuildSettings.asset').write_text(build,encoding='utf-8')
hub=read('Assets/Scenes/Hub/StageSelectScene.unity')
hub=re.sub(r'(  - chart: )[^\n]+\n    sceneName: "MafiaStage01"',r'\g<1>{fileID: 11400000, guid: '+guid(normalChart)+', type: 2}\n    sceneName: "MafiaStage01"',hub,count=1)
(ROOT/'Assets/Scenes/Hub/StageSelectScene.unity').write_text(hub,encoding='utf-8')
print('Authored',scene,'rooms',len(normal)+len(boss),'guards',len(normalNotes),'boss shots',len(bossNotes))
