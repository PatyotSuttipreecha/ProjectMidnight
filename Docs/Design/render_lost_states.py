from pathlib import Path
import json, math
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).parent
graph=json.loads((root/'the-lost-state-render.json').read_text(encoding='utf-8'))
s=3
im=Image.new('RGB',(1500*s,1200*s),'white'); d=ImageDraw.Draw(im)
font=ImageFont.truetype('C:/Windows/Fonts/tahoma.ttf',16*s)
small=ImageFont.truetype('C:/Windows/Fonts/tahoma.ttf',12*s)
for edge in graph['es']:
 pts=[(x*s,y*s) for x,y in edge['points']];d.line(pts,fill='#52657c',width=2*s)
 x,y=pts[-1];px,py=pts[-2];a=math.atan2(y-py,x-px)
 d.polygon([(x,y),(x-10*s*math.cos(a-.5),y-10*s*math.sin(a-.5)),(x-10*s*math.cos(a+.5),y-10*s*math.sin(a+.5))],fill='#52657c')
 if edge['value']:
  x,y=pts[len(pts)//2];x+=45*s;y-=12*s
  if edge['id']=='r':x,y=655*s,548*s
  if edge['id']=='h':x,y=680*s,485*s
  b=d.textbbox((0,0),edge['value'],font=small);w=b[2]-b[0];h=b[3]-b[1]
  d.rectangle((x-w/2-3*s,y-h/2-3*s,x+w/2+3*s,y+h/2+3*s),fill='white')
  d.text((x,y),edge['value'],font=small,fill='#34465e',anchor='mm')
for node in graph['ns']:
 x,y,w,h=[node[k]*s for k in ('x','y','w','h')]
 if node['id'] not in ('title','sub','foot'):
  if node['diamond']:d.polygon([(x+w/2,y),(x+w,y+h/2),(x+w/2,y+h),(x,y+h/2)],fill=node['fill'],outline='#64748b',width=2*s)
  else:d.rounded_rectangle((x,y,x+w,y+h),radius=10*s,fill=node['fill'],outline='#64748b',width=2*s)
 f=ImageFont.truetype('C:/Windows/Fonts/tahomabd.ttf',26*s) if node['id']=='title' else font
 lines=node['value'].split('\n');step=24*s
 for j,line in enumerate(lines):d.text((x+w/2,y+h/2+(j-(len(lines)-1)/2)*step),line,font=f,fill='#142235',anchor='mm')
im.save(root/'TheLost-Behavior-OnePage.png')
print('Rendered updated states')
