"""Author one flat top-down gunman sprite, without bones or animation."""
from pathlib import Path
import re
import time
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

if __name__=='__main__':main()
