"""Extract reusable legs from the authored player rigs; no runtime generation."""
import re
from BuildGeometricPlayer import PREFABS, HEADER, blocks, field, guid


def main():
    for ui in (False, True):
        source = PREFABS / ('GeometricPlayerUI.prefab' if ui else 'GeometricPlayer.prefab')
        objects = blocks(source)
        keep = {1000, 1001, 2010}

        def visit(transform):
            body = objects[transform][1]
            owner = int(re.search(r'm_GameObject: \{fileID: (\d+)\}', body)[1])
            keep.add(owner)
            keep.update(map(int, re.findall(r'component: \{fileID: (\d+)\}', objects[owner][1])))
            for child in map(int, re.findall(r'^  - \{fileID: (\d+)\}', body, re.M)):
                if child != 1141:  # Spine and upper body remain on the character.
                    visit(child)

        visit(1011)
        output = [HEADER]
        name = 'TopDownLegsUI' if ui else 'TopDownLegs'
        for id, (kind, body) in objects.items():
            if id not in keep:
                continue
            if id == 1000:
                body = field(body, 'm_Name', name)
                body = body.replace('  - component: {fileID: 1999}\n', '')
            if id == 1011:
                body = body.replace('  - {fileID: 1141}\n', '')
            output.append(f'--- !u!{kind} &{id}\n{body}')
        path = PREFABS / (name + '.prefab')
        path.write_text(''.join(output), encoding='utf-8')
        guid(path, 'PrefabImporter')
        print('Authored', name)


if __name__ == '__main__': main()
