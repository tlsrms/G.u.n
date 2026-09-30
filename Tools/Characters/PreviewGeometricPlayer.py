"""Render the authored polygon layout without starting Unity (Pillow required)."""
import math
from pathlib import Path
from PIL import Image, ImageDraw
from BuildGeometricPlayer import NODES, POLYGONS, HIDDEN_NODES

poses, shapes = {}, []
hidden = set(HIDDEN_NODES)
for name, parent, position, angle, art in NODES:
    px, py, pa = poses[parent] if parent else (0, 0, 0)
    r = math.radians(pa)
    x = px + position[0] * math.cos(r) - position[1] * math.sin(r)
    y = py + position[0] * math.sin(r) + position[1] * math.cos(r)
    poses[name] = (x, y, pa + angle)
    if parent in hidden: hidden.add(name)
    if art and name not in hidden:
        shapes.append((art[3], x, y, pa + angle, art))

image = Image.new('RGB', (900, 900), (18, 18, 20))
draw = ImageDraw.Draw(image)
for _, x, y, angle, art in sorted(shapes):
    r = math.radians(angle)
    for polygon, shade in POLYGONS[art[0]]:
        points = []
        for u, v in polygon:
            dx, dy = (u - .5) * art[1] * .9375, (v - .5) * art[2] * .9375
            points.append((450 + (x + dx * math.cos(r) - dy * math.sin(r)) * 460,
                           500 - (y + dx * math.sin(r) + dy * math.cos(r)) * 460))
        draw.polygon(points, fill=(shade, shade, shade))
destination = Path(__file__).parent / 'Previews/GeometricPlayerPreview.png'
destination.parent.mkdir(exist_ok=True)
image.save(destination)
