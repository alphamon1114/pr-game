"""Broad rounded button fronts, smooth crown, and elliptical rear outline."""
import math
# Image row, half-width in reference pixels, crown height, side shoulder height.
# The front-view reference has a wide lip; the oblique top photograph's apparent
# tapered nose must not be interpreted as the physical plan-view footprint.
STATIONS=[(194,260,.0162,.0154),(242,300,.0183,.0152),(300,300,.0202,.0155),
 (340,299,.0224,.0160),(430,297,.0250,.0185),(550,288,.0290,.0210),(690,281,.0337,.0240),
 (780,284,.0364,.0250),(875,292,.0373,.0250),(980,302,.0356,.0220),(1042,305,.0328,.0180),
 (1140,287,.0266,.0100),(1200,258,.0210,.0062),(1260,206,.0146,.0048),(1300,150,.0100,.0045),(1338,0,.0045,.0045)]
from functools import lru_cache
@lru_cache(maxsize=262144)
def sample(x,column):
 if column==1 and x<242:
  t=min(1,max(0,(x-194)/48))
  return 260+40*math.sqrt(max(0,1-(1-t)**2))
 # Analytic round tail: finite tip curvature instead of a pointed wedge.
 if column==1 and x>=1042:
  t=min(1,max(0,(x-1042)/296))
  return 305*math.sqrt(max(0,1-t*t))
 p=STATIONS
 h=[b[0]-a[0] for a,b in zip(p,p[1:])]
 d=[(b[column]-a[column])/step for a,b,step in zip(p,p[1:],h)]
 m=[d[0]]
 for i in range(1,len(p)-1):
  if d[i-1]*d[i]<=0:m.append(0)
  else:
   a=2*h[i]+h[i-1];b=h[i]+2*h[i-1]
   m.append((a+b)/(a/d[i-1]+b/d[i]))
 m.append(d[-1])
 for i,(a,b) in enumerate(zip(p,p[1:])):
  if x<=b[0]:
   t=max(0,min(1,(x-a[0])/h[i]))
   return (2*t**3-3*t*t+1)*a[column]+(t**3-2*t*t+t)*h[i]*m[i]+(-2*t**3+3*t*t)*b[column]+(t**3-t*t)*h[i]*m[i+1]
 return p[-1][column]

def radius(y):
 return sample(194+(.05985-y)*1144/.1197,1)*.0625/610

def height(x,y,clamp=True):
 v=194+(.05985-y)*1144/.1197
 crown=sample(v,2)
 rim=sample(v,3)
 u=abs(x)/max(.0000001,radius(y))
 if clamp:u=min(1,u)
 # Broad crown and rounded shoulders above a separate short grip skirt.
 roof=rim+(crown-rim)*(1-u**3)
 # The two button fronts have a shallow finger dish and rounded outer ends.
 # Blend it out along the first 15 mm instead of a central triangular beak.
 t=min(1,max(0,(v-194)/146))
 front_weight=1-t*t*(3-2*t)
 # Keep finger-dish width fixed near the rounded plan-view corners. Using the
 # rapidly changing outline radius here produced rippled corner highlights.
 lip=.0162-.0012*math.sin(math.pi*abs(x)/.0305)**2
 return roof*(1-front_weight)+lip*front_weight
