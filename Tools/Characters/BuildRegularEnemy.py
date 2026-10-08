"""Author one flat top-down gunman sprite, without bones or animation."""
from pathlib import Path
import re
import time
import sys
from PIL import Image, ImageDraw
from BuildGeometricPlayer import ROOT, guid

OUTPUT = ROOT / 'Assets/Arts/RegularEnemy.png'
# Coordinates are sprite-local world units. Body center is the aiming/target pivot.
SHAPES = [
    ([(-.35,-.08),(-.28,.15),(-.15,.23),(.15,.23),(.29,.14),(.35,-.08),(.23,-.22),(-.22,-.22)],125),
    ([(-.31,-.08),(-.23,-.18),(.23,-.18),(.31,-.08),(.24,-.24),(-.23,-.24)],80),
    ([(.15,.10),(.29,.10),(.28,.36),(.16,.36)],145),
    ([(.16,.34),(.28,.34),(.27,.40),(.16,.40)],65),
    ([(.17,.39),(.27,.39),(.28,.46),(.24,.50),(.16,.46)],205),
    ([(.17,.46),(.26,.46),(.26,.65),(.17,.65)],55),
    ([(.185,.50),(.245,.50),(.245,.65),(.185,.65)],210),
    ([(-.13,-.08),(-.20,.04),(-.17,.18),(-.07,.26),(.08,.25),(.18,.16),(.18,.04),(.11,-.08)],190),
    ([(-.18,.10),(-.17,.18),(-.07,.26),(.08,.25),(.18,.16),(.17,.09),(.06,.15),(-.08,.13)],65),
    ([(-.13,.12),(-.06,.20),(.07,.20),(.13,.14),(.05,.16)],90),
]

# Face-up fallen silhouette. Head falls behind the standing aim axis (-Y),
# knees separate and the pistol arm loses its aiming pose. Same torso pivot.
FALLEN_SHAPES = [
    ([(-.26,-.21),(-.33,-.05),(-.27,.33),(-.11,.42),(.17,.37),(.27,.12),(.23,-.22)],125),
    ([(-.23,.06),(-.22,.34),(-.1,.43),(.17,.37),(.20,.12)],85),
    ([(-.21,.32),(-.07,.37),(-.13,.61),(-.32,.75),(-.42,.68),(-.27,.50)],85),
    ([(.02,.36),(.17,.34),(.30,.54),(.24,.80),(.10,.80),(.14,.58)],100),
    ([(-.43,.66),(-.30,.71),(-.31,.83),(-.52,.81),(-.55,.75)],45),
    ([(.1,.75),(.24,.75),(.32,.85),(.28,.91),(.07,.89)],45),
    ([(-.24,-.19),(-.34,-.17),(-.51,.02),(-.62,-.02),(-.67,.09),(-.48,.17),(-.29,.02)],140),
    ([(-.66,-.02),(-.75,.01),(-.73,.13),(-.64,.14),(-.6,.08)],195),
    ([(.19,-.19),(.33,-.15),(.41,.06),(.63,.13),(.60,.25),(.31,.19),(.22,-.01)],145),
    ([(.60,.12),(.72,.13),(.76,.20),(.67,.28),(.59,.23)],195),
    ([(.69,.18),(.81,.14),(.95,.24),(.91,.30),(.76,.25),(.72,.30),(.66,.26)],55),
    ([(.81,.16),(.95,.24),(.93,.27),(.79,.20)],190),
    ([(-.17,-.23),(-.20,-.39),(-.12,-.55),(.02,-.59),(.16,-.48),(.18,-.32),(.10,-.21)],190),
    ([(-.20,-.38),(-.12,-.55),(.02,-.59),(.16,-.48),(.14,-.40),(.02,-.46),(-.12,-.43)],65),
    ([(-.10,-.30),(.08,-.30),(.08,-.27),(-.10,-.27)],100),
]

def fallen():
    """Only rebuild the new fallen pose, preserving the user's standing sprite."""
    output = ROOT / 'Assets/Resources/RegularEnemyFallen.png'
    asset_guid = guid(output)
    size, extent = 768, 2.4
    im = Image.new('RGBA', (size, size))
    draw = ImageDraw.Draw(im)
    for points, shade in FALLEN_SHAPES:
        draw.polygon([((x / extent + .5) * size, (.5 - y / extent) * size) for x, y in points],
            fill=(shade, shade, shade, 255))
    meta = (ROOT / 'Assets/Arts/Solid.png.meta').read_text(encoding='utf-8-sig')
    meta = re.sub(r'^guid: \w+', 'guid: ' + asset_guid, meta, flags=re.M)
    for key, val in [('maxTextureSize','1024'), ('spritePixelsToUnits',str(size / extent)),
                     ('alignment','9'), ('spritePivot','{x: 0.5, y: 0.5}'), ('filterMode','1'),
                     ('textureCompression','0'), ('spriteGenerateFallbackPhysicsShape','0')]:
        meta = re.sub(r'(?m)^(\s*' + key + r':) .*$', r'\g<1> ' + val, meta)
    Path(str(output) + '.meta').write_text(meta)
    im.save(output)
    print('Created fallen enemy pose:', output)

def main():
    asset_guid = guid(OUTPUT)
    im=Image.new('RGBA',(512,512));draw=ImageDraw.Draw(im)
    for points,shade in SHAPES:
        draw.polygon([((x+.6)/1.2*512,(.8-y)/1.2*512) for x,y in points],fill=(shade,shade,shade,255))
    meta=(ROOT/'Assets/Arts/Solid.png.meta').read_text(encoding='utf-8-sig')
    meta=re.sub(r'^guid: \w+','guid: '+asset_guid,meta,flags=re.M)
    for key,val in [('maxTextureSize','512'),('spritePixelsToUnits',str(512/1.2)),('alignment','9'),('spritePivot','{x: 0.5, y: 0.3333333333}'),('filterMode','1'),('textureCompression','0'),('spriteGenerateFallbackPhysicsShape','0')]:
        meta=re.sub(r'(?m)^(\s*'+key+r':) .*$',r'\g<1> '+val,meta)
    for attempt in range(20):
        try:Path(str(OUTPUT)+'.meta').write_text(meta);break
        except PermissionError:
            if attempt==19:raise
            time.sleep(.1)
    im.save(OUTPUT)
    preview=Image.new('RGBA',im.size,(20,20,22,255));preview.alpha_composite(im)
    dest=Path(__file__).parent/'Previews/RegularEnemyPreview.png';dest.parent.mkdir(exist_ok=True);preview.convert('RGB').save(dest)
    print(asset_guid)

if __name__=='__main__':
    if '--fallen-only' in sys.argv: fallen()
    else: main()
