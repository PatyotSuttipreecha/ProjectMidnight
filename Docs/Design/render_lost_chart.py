from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont
import math

root=Path(__file__).parent
cells=ET.parse(root/'TheLost-Behavior-OnePage.drawio').findall('.//mxCell')
nodes={c.get('id'):c for c in cells if c.get('vertex')=='1'}
scale=3
im=Image.new('RGB',(1400*scale,1050*scale),'white'); d=ImageDraw.Draw(im)
font=ImageFont.truetype('C:/Windows/Fonts/tahoma.ttf',16*scale)
small=ImageFont.truetype('C:/Windows/Fonts/tahoma.ttf',13*scale)
def box(id):
 g=nodes[id].find('mxGeometry'); return [float(g.get(k))*scale for k in ('x','y','width','height')]
routes={
'a':[(410,230),(410,260)],'b':[(410,325),(410,355)],'c':[(410,440),(410,475)],
'd':[(310,395),(150,395),(150,465)],'f':[(260,490),(290,490)],'g':[(150,555),(150,595)],
'h':[(40,625),(20,625),(20,200),(290,200)],'i':[(290,530),(260,530)],'j':[(410,545),(410,585)],
'k':[(525,630),(560,630)],'l':[(680,580),(680,510),(530,510)],'m':[(410,680),(410,720)],
'n':[(410,795),(410,830)],'o':[(410,895),(410,925)],'p':[(530,955),(825,955),(825,490),(530,490)]}
labels={'a':(485,245),'c':(440,455),'d':(180,380),'f':(272,468),'g':(205,575),'h':(150,185),'i':(270,555),'k':(545,610),'l':(695,545),'m':(445,700),'p':(715,945)}
for c in cells:
 if c.get('edge')!='1':continue
 pts=[(x*scale,y*scale) for x,y in routes[c.get('id')]]
 d.line(pts,fill='#52657c',width=2*scale)
 x,y=pts[-1]; px,py=pts[-2]; ang=math.atan2(y-py,x-px)
 d.polygon([(x,y),(x-12*scale*math.cos(ang-.45),y-12*scale*math.sin(ang-.45)),(x-12*scale*math.cos(ang+.45),y-12*scale*math.sin(ang+.45))],fill='#52657c')
 if c.get('value'):
  lx,ly=labels[c.get('id')]; b=d.textbbox((0,0),c.get('value'),font=small); w=b[2]-b[0];h=b[3]-b[1]
  d.rectangle((lx*scale-w/2-4*scale,ly*scale-h/2-3*scale,lx*scale+w/2+4*scale,ly*scale+h/2+3*scale),fill='white')
  d.text((lx*scale,ly*scale),c.get('value'),font=small,fill='#34465e',anchor='mm')
for id,c in nodes.items():
 x,y,w,h=box(id); style=dict(part.split('=',1) for part in c.get('style').split(';') if '=' in part)
 if id not in ('title','subtitle'):
  if 'rhombus' in c.get('style'):d.polygon([(x+w/2,y),(x+w,y+h/2),(x+w/2,y+h),(x,y+h/2)],fill=style['fillColor'],outline='#64748b',width=2*scale)
  else:d.rounded_rectangle((x,y,x+w,y+h),radius=10*scale,fill=style['fillColor'],outline='#64748b',width=2*scale)
 f=ImageFont.truetype('C:/Windows/Fonts/tahomabd.ttf',26*scale) if id=='title' else ImageFont.truetype('C:/Windows/Fonts/tahoma.ttf',13*scale) if id=='gate' else font
 lines=[]
 for line in c.get('value').split('\n'):
  current=''
  for char in line:
   if current and d.textlength(current+char,font=f)>w-24*scale:lines.append(current);current=char
   else:current+=char
  lines.append(current)
 spacing=(32 if id=='title' else 24)*scale
 top=y+h/2-(len(lines)-1)*spacing/2
 for j,line in enumerate(lines):d.text((x+w/2,top+j*spacing),line,font=f,fill='#142235',anchor='mm')
im.save(root/'TheLost-Behavior-OnePage.png')
print('Saved one-page chart PNG, 4200 x 3150')
