"""Static scientific plots from recorded accepted states only."""
import gzip,json,math
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
ROOT=Path(__file__).resolve().parents[2];D=ROOT/'docs/experiments/corner-speed-loss'
curves=json.loads(gzip.decompress((D/'comparison.json.gz').read_bytes()))
fig,axes=plt.subplots(3,2,figsize=(14,11));colors={'A':'#218caa','B':'#d27d1e','F':'#668f3f','K1':'#ae348a','joint-grid-29':'#3527c3','exit-full-338':'#cf442d'}
for run in curves:
    if run['name'] not in colors:continue
    q=[p for p in run['points'] if p['lap']==2 and p['bend']==2]
    if not q:continue
    color=colors[run['name']];x=[p['reference'] for p in q]
    for ax,key,label in [(axes[0,0],'speed','Speed [km/h]'),(axes[1,0],'gas','Active throttle [0..1]'),(axes[1,1],'steering','Actual steering [deg]'),(axes[2,0],'beta','Beta [deg]')]:
        ax.plot(x,[p[key] for p in q],label=run['name'],color=color,lw=1.2,drawstyle='steps-post' if key=='gas' else 'default');ax.set_ylabel(label);ax.set_xlabel('Unwrapped reference [m]')
    axes[0,1].plot([p['t'] for p in q],[p['speed'] for p in q],label=run['name'],color=color,lw=1.2)
    axes[2,1].plot([p['x'] for p in q],[p['y'] for p in q],color=color,lw=1.2)
    minimum=min(q,key=lambda p:p['speed']);axes[2,1].scatter([minimum['x']],[minimum['y']],color=color,s=18)
geometry=json.loads((ROOT/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json').read_bytes())
sections=[q for segment in geometry['segments'][5:8] for q in segment['sections']]
for key in ['inner','outer']:axes[2,1].plot([p[key]['x'] for p in sections],[p[key]['y'] for p in sections],color='#777',lw=.8,ls='--')
axes[0,1].set(xlabel='Native time [s]',ylabel='Speed [km/h]');axes[2,1].set(xlabel='X [m]',ylabel='Y [m]');axes[2,1].set_aspect('equal',adjustable='box')
axes[0,0].legend(ncol=3,fontsize=9)
for ax in axes.flat:ax.grid(alpha=.22)
fig.suptitle('#145: lap 2 / bend 2 — native data; manual interventions are diagnostic',fontsize=13)
fig.tight_layout();fig.savefig(D/'corner145.svg');fig.savefig(D/'corner145.png',dpi=140);plt.close(fig)
p=D/'corner145.svg';p.write_text('\n'.join(line.rstrip() for line in p.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
