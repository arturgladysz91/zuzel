"""Standalone scientific figures from saved evidence; no simulation or tuning."""
import gzip,json,math,time
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/tyre-physics-corner-limits';P=D/'plots'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.name.endswith('.gz') else p.read_bytes())
def finish(fig,name):fig.tight_layout();fig.savefig(P/(name+'.png'),dpi=150);fig.savefig(P/(name+'.svg'));svg=P/(name+'.svg');svg.write_text('\n'.join(s.rstrip() for s in svg.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n');plt.close(fig)
def main():
 begin=time.process_time();ledger=read(D/'budget.json');assert all(x['Complete'] for x in ledger);assert not any(x['Name']=='saved-figures' for x in ledger);P.mkdir(exist_ok=True);a=read(D/'analysis.json');grid=read(D/'contact-grid.json.gz')
 fig,ax=plt.subplots(2,2,figsize=(11,7));colors={.45:'#d67b19',.9:'#19769d',1.8:'#8b48a5'}
 for model,style in [('A','-'),('B','--')]:
  for iw,color in colors.items():
   q=sorted([q for q in grid if q['Model']==model and q['Iw']==iw and q['LoadFraction']==1 and abs(q['U']-25)<1e-8 and q['AngleDegrees']==20],key=lambda q:q['SlipRatio'])
   for column,key in enumerate(['Fx','Fy']):ax[0,column].plot([x['SlipRatio'] for x in q],[x[key] for x in q],style,color=color,label=f'{model}, Iw={iw}')
   q=sorted([q for q in grid if q['Model']==model and q['Iw']==iw and q['LoadFraction']==1 and abs(q['U']-25)<1e-8 and q['SlipRatio']==.5],key=lambda q:q['AngleDegrees'])
   for column,key in enumerate(['Fx','Fy']):ax[1,column].plot([x['AngleDegrees'] for x in q],[x[key] for x in q],style,color=color,label=f'{model}, Iw={iw}')
 for j in range(2):ax[0,j].set(xlabel='Longitudinal slip ratio, lateral angle20°',ylabel=['Rear Fx N','Rear Fy N'][j]);ax[1,j].set(xlabel='Rear lateral slip angle °, longitudinal ratio0.5',ylabel=['Rear Fx N','Rear Fy N'][j])
 for v in ax.flat:v.grid(alpha=.25)
 ax[0,0].legend(fontsize=8);fig.suptitle('90km/h, dt=0, same nominal mu/Fz. B curves coincide across Iw.');finish(fig,'tyre-forces')
 paths={'A':D/'native/baseline149-final148.json.gz','B':D/'native/separated149-autonomous.json.gz','K1':R/'docs/experiments/standing-start-three-laps/native/K1.json.gz','F':R/'docs/experiments/standing-start-three-laps/native/F.json.gz'};runs={n:read(p) for n,p in paths.items()};starts=[s['startReferenceMeters'] for s in read(R/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json')['segments']]
 palette={'A':'#167aab','B':'#d47d1d','K1':'#8a8a8a','F':'#8d47a3'}
 fig,ax=plt.subplots(2,3,figsize=(12,7))
 for index,v in enumerate(ax.flat):
  lap=index//2;bend=index%2;lo=lap*runs['A']['LapMeters']+starts[1 if bend==0 else 5];hi=lap*runs['A']['LapMeters']+starts[4 if bend==0 else 8]
  for n,r in runs.items():
   ss=[s for s in r['Riders'][0]['Samples'] if lo<=s['Input']['Rider']['TotalReferenceMeters']<=hi];v.plot([s['Input']['Rider']['TotalReferenceMeters']-lo for s in ss],[s['Input']['Rider']['Speed']*3.6 for s in ss],label=n,color=palette[n],alpha=1 if n in ['A','B'] else .65)
  v.set(title=f'Lap{lap+1} bend{bend+1}',xlabel='Reference distance after entry m',ylabel='km/h');v.grid(alpha=.2)
 ax[0,0].legend();finish(fig,'six-corner-speeds')
 fig,ax=plt.subplots(4,2,figsize=(12,11))
 for col,(lap,bend) in enumerate([(1,1),(2,0)]):
  lo=lap*runs['A']['LapMeters']+starts[1 if bend==0 else 5]-25;hi=lap*runs['A']['LapMeters']+(starts[5] if bend==0 else runs['A']['LapMeters']+starts[1])
  for n in ['A','B']:
   ss=[s for s in runs[n]['Riders'][0]['Samples'] if lo<=s['Input']['Rider']['TotalReferenceMeters']<=hi];x=[s['Input']['Rider']['TotalReferenceMeters']-lo for s in ss]
   values=[[s['Input']['Active']['Drive'] for s in ss],[s['Input']['Rider']['SlideRadians']*180/math.pi for s in ss],[s['Input']['Rider']['Road']['OffsetMeters'] for s in ss],[s['Input']['Rider']['AngularVelocity'] for s in ss]]
   for row,y in enumerate(values):ax[row,col].plot(x,y,label=n,color=palette[n])
  for row in range(4):ax[row,col].set(xlabel='Reference distance from preparation m',ylabel=['Throttle','Beta °','Offset m','Yaw rad/s'][row]);ax[row,col].grid(alpha=.25)
  ax[0,col].set_title(f'Lap{lap+1} bend{bend+1} + following straight');ax[0,col].legend()
 finish(fig,'coast-line-beta-yaw')
 fig,ax=plt.subplots(figsize=(11,4));cs=[c for c in a['corners'] if c['run'] in ['baseline149-final148','separated149-autonomous']]
 for model,name,delta in [('A','baseline149-final148',-.18),('B','separated149-autonomous',.18)]:
  values=[]
  for c in [q for q in cs if q['run']==name]:
   r=next(q for q in a['regions'] if q['run']==name and q['lap']==c['lap'] and q['bend']==c['bend']) if not(c['lap']==1 and c['bend']==2) else None
   ss=runs[model]['Riders'][0]['Samples'];lo=(c['lap']-1)*runs[model]['LapMeters']+starts[1 if c['bend']==1 else 5];hi=(c['lap']-1)*runs[model]['LapMeters']+starts[4 if c['bend']==1 else 8]
   # Original native force held on [previous,current], split proportionally at geometric boundaries.
   total=0
   for u,v in zip(ss,ss[1:]):
    ru=u['Input']['Rider']['TotalReferenceMeters'];rv=v['Input']['Rider']['TotalReferenceMeters'];fraction=max(0,min(rv,hi)-max(ru,lo))/(rv-ru) if rv>ru else 0
    if fraction:
     f=v['Forces'];dt=(v['Input']['Time']-u['Input']['Time'])*fraction;vel={'X':(u['Input']['Rider']['Velocity']['X']+v['Input']['Rider']['Velocity']['X'])/2,'Y':(u['Input']['Rider']['Velocity']['Y']+v['Input']['Rider']['Velocity']['Y'])/2};spin=(u['Input']['Rider']['AngularVelocity']+v['Input']['Rider']['AngularVelocity'])/2
     for force,arm in [(f['FrontForce'],f['FrontArm']),(dict(X=f['BodyLeft']['X']*f['RearLateral'],Y=f['BodyLeft']['Y']*f['RearLateral']),f['RearArm'])]:total-=(force['X']*(vel['X']-spin*arm['Y'])+force['Y']*(vel['Y']+spin*arm['X']))*dt
   values.append(total/1000)
  ax.bar([i+delta for i in range(6)],values,width=.36,label=model,color=palette[model])
 ax.set(xticks=range(6),xticklabels=['1.1','1.2','2.1','2.2','3.1','3.2'],ylabel='Front + rear lateral slip loss kJ',xlabel='Lap.bend');ax.legend();finish(fig,'corner-energy')
 geometry=read(R/'docs/experiments/planner-speed-root-cause/geometry-audit.json');fig,ax=plt.subplots(figsize=(11,4));q=geometry['Profile'];ax.plot([v['ReferenceMeters'] for v in q],[v['Curvature'] for v in q],label='Frozen G0 progressive C2');ax.axhline(1/31,color='gray',ls='--',label='1/31 per metre');ax.set(xlabel='Reference m',ylabel='Curvature 1/m',title='R31 is a nominal oval parameter; local C2 curvature reaches1/24.479');ax.legend();ax.grid(alpha=.3);finish(fig,'curvature')
 ledger.append(dict(Name='saved-figures',Complete=True,NativeWorlds=0,CpuSeconds=time.process_time()-begin,Outcome='saved standard scientific plots; no simulation'));save=lambda p,x:p.write_text(json.dumps(x,indent=2)+'\n',encoding='utf-8');save(D/'budget.json',ledger);print('figures CPU',ledger[-1]['CpuSeconds'])
if __name__=='__main__':main()
