"""Construct clean, symmetric intersecting-hexagon openings in reference coordinates.
The photo controls placement; straight manufactured ribs replace noisy traced edges.
"""
import json, math
from pathlib import Path
from shapely.geometry import Polygon, box
from shapely.affinity import scale
root=Path(__file__).parent
openings=[]
def add(shape):
    shapes=list(shape.geoms) if hasattr(shape,'geoms') else [shape]
    for p in shapes:
        if p.geom_type=='Polygon' and p.area>80:
            openings.append(list(p.exterior.coords)[:-1])
def cubes(zone,centres,r=48):
    for cx,cy in centres:
        v=[(cx+r*math.cos(math.pi*i/3),cy+r*math.sin(math.pi*i/3)) for i in range(6)]
        for i in (0,2,4):
            # Three rhombi meet at a Y junction within every hexagonal outline.
            p=Polygon([(cx,cy),v[i],v[(i+1)%6],v[(i+2)%6]])
            p=p.buffer(-5.0,join_style=2).buffer(1.5,quad_segs=2).intersection(zone)
            add(p);add(scale(p,xfact=-1,yfact=1,origin=(750,0)))
front=Polygon([(482,553),(544,568),(616,605),(642,735),(552,712),(482,678)])
cubes(front,[(x,y) for x,shift in [(504,0),(576,42)] for y in range(548+shift,790,84)])
rear=Polygon([(539,753),(650,786),(677,850),(686,994),(683,1110),(704,1250),(697,1313),(560,1264),(483,1150),(498,974)])
cubes(rear,[(x,y) for x,shift in [(506,42),(578,0),(650,42)] for y in range(762+shift,1390,84)])
# Repeated clipped hexagons with TWO uninterrupted vertical ribs down the spine.
for cy,hh,ww in [(850,38,42),(938,40,42),(1027,41,43),(1116,40,43),(1205,40,43),(1290,31,29)]:
    p=Polygon([(750-ww,cy-hh*.55),(750-ww*.77,cy-hh),(750+ww*.77,cy-hh),(750+ww,cy-hh*.55),(750+ww*.65,cy+hh),(750-ww*.65,cy+hh)])
    for strip in [box(650,cy-50,732,cy+50),box(740,cy-50,755,cy+50),box(763,cy-50,850,cy+50)]:
        add(p.intersection(strip).buffer(-1).buffer(1,quad_segs=2))
(root/'mouse_apertures.json').write_text(json.dumps(openings,separators=(',',':')))
print('REGULAR_APERTURES',len(openings))
