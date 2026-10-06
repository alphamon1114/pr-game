"""Shape-preserving cubic interpolation for the reference mouse outline."""
STATIONS=[(194,45,.020),(225,94,.021),(260,185,.0215),(300,275,.023),
 (340,299,.024),(430,297,.028),(550,288,.032),(690,281,.0355),
 (780,280,.0373),(875,285,.0368),(980,299,.0338),(1070,301,.029),
 (1140,277,.024),(1200,239,.017),(1260,180,.0105),(1300,120,.006),(1338,1,.0025)]
from functools import lru_cache
@lru_cache(maxsize=262144)
def sample(x,column):
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
