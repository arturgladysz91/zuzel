import gzip
import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

root=Path(__file__).resolve().parents[2]
out=root/'outputs/badanie201'


def font(size,bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/'+('segoeuib.ttf' if bold else 'segoeui.ttf'),size)


def figure():
    data=json.loads((out/'metryki.json').read_text(encoding='utf8'))
    im=Image.new('RGB',(1440,820),'#101e29')
    d=ImageDraw.Draw(im)
    d.text((52,30),'#201 · ten sam stan, trzy kontynuacje',font=font(37,True),fill='#edf8f7')
    d.text((55,85),'Rzeczywista prędkość Box2D względem wspólnego postępu na torze',font=font(23),fill='#a9c2cb')
    l,t,r,b=114,199,1360,645
    x0=451.16814398765564
    x1=630.5574998855591
    def xy(s,v):
        return l+(s-x0)/(x1-x0)*(r-l),b-(v-50)/80*(b-t)
    d.rounded_rectangle((57,151,1390,735),13,fill='#192e38')
    for v in range(50,131,10):
        _,y=xy(x0,v)
        d.line((l,y,r,y),fill='#36505f')
        d.text((l-12,y),str(v),anchor='rm',font=font(18),fill='#b4c8d0')
    for s in range(0,181,20):
        x,_=xy(x0+s,50)
        d.line((x,t,x,b),fill='#2d4553')
        d.text((x,b+10),str(s),anchor='mt',font=font(18),fill='#b4c8d0')
    for s,text in ((513.1681249141693,'Wejście'),(610.5574998855591,'Wyjście')):
        x,_=xy(s,50)
        d.line((x,t,x,b),fill='#c8bd91',width=2)
        d.text((x,t-24),text,anchor='ms',font=font(19,True),fill='#c8bd91')
    labels={'196':'#196 · 118,013 km/h · 8,770 s','200':'C #200 · 89,389 km/h · 8,213 s',
            '201-matched':'Uczenie #201 · 94,150 km/h · 7,872 s'}
    colors={'196':'#e8ad79','200':'#b4b2ee','201-matched':'#80e7c8'}
    for name,record in data['matched'].items():
        points=[xy(p['s'],p['v']) for p in record['rows']]
        d.line(points,fill=colors[name],width=3)
    d.text(((l+r)/2,b+51),'Postęp od stanu #196 (m)',anchor='mt',font=font(22),fill='#c9dce1')
    for index,(name,label) in enumerate(labels.items()):
        x=55+index*466
        d.ellipse((x,772,x+12,784),fill=colors[name])
        d.text((x+22,762),label,font=font(20,True),fill='#e6f2f3')
    im.save(out/'196-200-201-predkosc.png')


if __name__=='__main__':
    figure()
