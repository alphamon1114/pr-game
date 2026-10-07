"""Prepare a valid 2D cut shell from traced contours (requires shapely 2.1+)."""
import json, math
from pathlib import Path
from shapely.geometry import Polygon, LineString, box
from shapely.ops import unary_union
from shapely import constrained_delaunay_triangles

root=Path(__file__).parent
from profile import sample
stations=[(v,sample(v,1)) for v in range(194,1339,2)]
outline=[(750-w,v) for v,w in stations]+[(750+w,v) for v,w in reversed(stations)]
body=Polygon(outline)
holes=[Polygon(p).buffer(0).buffer(-1.5).buffer(1.5,quad_segs=5).intersection(body.buffer(-8)) for p in json.loads((root/'mouse_apertures.json').read_text())]
seam=LineString([(455+i*590/120,775-88*((455+i*590/120-750)/295)**2) for i in range(121)]).buffer(2)
holes += [seam,box(746,190,754,775),box(701,185,799,524).buffer(3)]
shape=body.difference(unary_union(holes))
triangles=constrained_delaunay_triangles(shape)
vertices=[];faces=[];lookup={}
for tri in triangles.geoms:
 ids=[]
 for xy in list(tri.exterior.coords)[:3]:
  key=tuple(round(v,5) for v in xy)
  if key not in lookup:lookup[key]=len(vertices);vertices.append(key)
  ids.append(lookup[key])
 faces.append(ids)
(root/'mouse_surface.json').write_text(json.dumps({'vertices':vertices,'faces':faces},separators=(',',':')))
print(len(vertices),'vertices',len(faces),'constrained triangles')
