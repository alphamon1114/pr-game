"""Smooth mouse outline and crown, with an elliptical rounded rear end."""
import math
STATIONS=[(194,45,.0195),(225,94,.0200),(260,185,.0206),(300,275,.0213),
 (340,299,.0224),(430,297,.0250),(550,288,.0290),(690,281,.0337),
 (780,284,.0364),(875,292,.0373),(980,302,.0356),(1042,305,.0328),
 (1140,287,.0266),(1200,258,.0210),(1260,206,.0146),(1300,150,.0100),(1338,0,.0045)]
from functools import lru_cache
@lru_cache(maxsize=262144)
def sample(x,column):
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
 t=min(1,max(0,(v-1042)/296))
 rim=.0115-.007*(t*t*(3-2*t))
 u=abs(x)/max(.0000001,radius(y))
 if clamp:u=min(1,u)
 # Broad crown and rounded shoulders above a separate short grip skirt.
 return rim+(crown-rim)*(1-u**3)
