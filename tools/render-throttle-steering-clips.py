"""Render measured accepted Box2D states into six MP4 clips; no trajectory interpolation."""
import bisect
import gzip
import hashlib
import json
import math
from pathlib import Path
import subprocess
import sys
import time
import imageio_ffmpeg
from PIL import Image,ImageDraw,ImageFont

start_cpu=time.process_time();root=Path(__file__).resolve().parents[1];d=root/'docs/experiments/throttle-steering-decoupling'
out=d/'clips';out.mkdir(exist_ok=True)
def read(p):return json.loads(gzip.decompress(p.read_bytes())if p.suffix=='.gz'else p.read_bytes())
rows=read(d/'native-summaries.json');fast=min((r for r in rows if r['Completed']),key=lambda r:r['GateSeconds'])['Name']
clips=[('01-full-no-correction','D0-A',False,'Pelny gaz bez korekty'),('02-full-corrected','D0-B',False,'Pelny gaz + korekta'),
       ('03-partial-same-correction','D0-D',False,'31.82% + identyczna korekta B'),('04-fastest-legal',fast,False,'Najszybszy legalny zapis: retrospective sampled oracle'),
       ('05-failed-full','S0-B',False,'Nieudany duzy gaz'),('06-continuous-two-bends','D0-D',True,'Ciagle dwa luki i pelne nastepne 62m')]
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',19);small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16);big=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',26)
manifest=[]
for name,recording,whole,title in clips:
    data=read(d/'viewer'/(recording+'.json.gz'));fs=data['Frames'];times=[f['time']for f in fs];summary=next(r for r in rows if r['Name']==recording)
    geom=read(d/(data['Geometry']+'-geometry.json'));points=[p[e]for seg in geom['segments']for p in seg['sections']for e in ['inner','outer']]
    xmin,xmax=min(p['x']for p in points),max(p['x']for p in points);ymin,ymax=min(p['y']for p in points),max(p['y']for p in points)
    scale=min(760/(xmax-xmin),470/(ymax-ymin));cx,cy=(xmax+xmin)/2,(ymax+ymin)/2
    def project(x,y):return (420+(x-cx)*scale,365-(y-cy)*scale)
    first=0 if whole else max(0,summary['TriggerTime']-.3);last=fs[-1]['time'];fps=15;count=max(1,math.ceil((last-first)*fps));path=out/(name+'.mp4')
    cmd=[imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','error','-f','rawvideo','-vcodec','rawvideo','-pix_fmt','rgb24','-s','1200x720','-r',str(fps),'-i','-',
         '-an','-vcodec','libx264','-preset','fast','-crf','22','-pix_fmt','yuv420p','-movflags','+faststart',str(path)]
    proc=subprocess.Popen(cmd,stdin=subprocess.PIPE)
    sample_times=[]
    for j in range(count+1):
        t=min(last,first+j/fps);idx=max(0,bisect.bisect_right(times,t+1e-9)-1);f=fs[idx];sample_times.append(f['time'])
        im=Image.new('RGB',(1200,720),'#0b1520');draw=ImageDraw.Draw(im)
        draw.text((25,20),'#138  '+recording+'  |  '+title,font=font if len(title)>65 else big,fill='#e6eff5');draw.text((25,60),'NATIVE BOX2D  |  '+data['Geometry']+'  |  accepted samples, no interpolation',font=small,fill='#acbecb')
        outer=[project(p['outer']['x'],p['outer']['y'])for s in geom['segments']for p in s['sections']];inner=[project(p['inner']['x'],p['inner']['y'])for s in geom['segments']for p in s['sections']]
        draw.polygon(outer,fill='#76563b');draw.polygon(inner,fill='#102231');draw.line(outer+[outer[0]],fill='#c6d0d6',width=3);draw.line(inner+[inner[0]],fill='#e9d69d',width=2)
        trail=[project(s['x'],s['y'])for s in fs[:idx+1]]
        if len(trail)>1:draw.line(trail,fill='#72dcf7',width=3)
        px,py=project(f['x'],f['y']);dx,dy=math.cos(f['psi']),-math.sin(f['psi']);draw.line([(px-dx*9,py-dy*9),(px+dx*9,py+dy*9)],fill='#ffc07a',width=6)
        tx,ty=project(f['x']+f['vx']*.3,f['y']+f['vy']*.3);draw.line([(px,py),(tx,ty)],fill='#72dcf7',width=3)
        draw.text((25,640),f't = {f["time"]:.3f}s   requested gas {100*f["requestedGas"]:.1f}%   active {100*f["gas"]:.1f}%',font=font,fill='#e6eff5')
        draw.text((25,672),'Orange: bike orientation psi  |  Blue: actual velocity chi and native XY',font=small,fill='#acbecb')
        def val(k,n=2):return 'N/D'if f.get(k)is None else f'{f[k]:.{n}f}'
        labels=[('Speed',val('speed')+' km/h'),('Beta / yaw',val('beta')+' deg / '+val('yaw')+' rad/s'),('Steer / request',val('steering')+' / '+val('requestedSteering')+' deg'),
                ('Front Fx / Fy',val('frontFx',0)+' / '+val('frontFy',0)+' N'),('Rear Fx / Fy',val('rearFx',0)+' / '+val('rearFy',0)+' N'),
                ('Yaw moment F / R',val('frontTorque',0)+' / '+val('rearTorque',0)+' Nm'),('Rear use / slip',val('rearUse')+' / '+val('slip')),
                ('Wheel omega',val('omega')+' rad/s'),('Inner / outer gap',val('inner')+' / '+val('outer')+' m'),('Certificate',val('certificate')+' m')]
        for k,(label,value)in enumerate(labels):draw.text((835,115+k*46),label,font=small,fill='#acbecb');draw.text((835,134+k*46),value,font=font,fill='#e6eff5')
        status=data['Metrics']['Native']['Outcome'];draw.text((835,610),'DNF: '+status if status!='completed'and j==count else 'Outcome: '+status,font=font,fill='#ffab98'if status!='completed'else'#b6e3a2')
        draw.text((835,658),'Beta45: experimental envelope',font=small,fill='#acbecb')
        if j==count:im.save(out/(name+'.png'))
        proc.stdin.write(im.tobytes())
    proc.stdin.close();code=proc.wait()
    if code:raise RuntimeError('ffmpeg failed')
    manifest.append({'Clip':path.name,'NativeRecord':recording,'RetrospectiveSampledOracle':name.startswith('04'),
                     'NativeRawSha256':hashlib.sha256((d/'native'/(recording+'.json.gz')).read_bytes()).hexdigest().upper(),
                     'StartSeconds':first,'EndSeconds':last,'Fps':fps,'AcceptedNativeSampleTimes':sample_times,
                     'VideoSha256':hashlib.sha256(path.read_bytes()).hexdigest().upper()})
    print(path.name,recording,round(last-first,2),'s')
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
(d/'clip-render-cost.json').write_text(json.dumps({'PythonCpuSeconds':time.process_time()-start_cpu,'NativeWorlds':0,'ForceQueries':0,
    'ExternalEncoderCpuIncluded':False,'Note':'MP4 encoder is a separate reporting process; no episode/search or physical work.'},indent=2)+'\n')
