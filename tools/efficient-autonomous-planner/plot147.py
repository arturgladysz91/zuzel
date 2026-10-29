"""Plot saved physical traces only; creates no world or prediction."""
import gzip,json
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
D=Path(__file__).resolve().parents[2]/'docs/experiments/efficient-autonomous-planner'
a=json.loads((D/'analysis.json').read_text(encoding='utf-8'));curves=json.loads(gzip.decompress((D/'comparison.json.gz').read_bytes()))
colors={'K1':'#6c7d95','F':'#ba7b37','autonomous146-viable':'#a764aa','autonomous147-shortlist':'#087e8b'}
labels={'K1':'K1','F':'F','autonomous146-viable':'#146 (partial)','autonomous147-shortlist':'#147'}
fig,axes=plt.subplots(3,2,figsize=(13.5,12),layout='constrained');fig.suptitle('Study #147 — saved native traces, one continuous three-lap trial',fontsize=17)
for c in curves:
 ps=c['points'];co=colors[c['name']];label=labels[c['name']]
 axes[0,0].plot([p['t'] for p in ps],[p['speed'] for p in ps],color=co,label=label,lw=1.1)
 # Common reference interval of corner 2, lap 2; no target line fed to chooser.
 # Locate curve's geometric second bend in lap 2 using same native segment starts as analyzer.
 geometry=json.loads((D/'../causal-yaw-throttle/progressive-b-viewer-geometry.json').resolve().read_text(encoding='utf-8'))
 starts=[s['startReferenceMeters'] for s in geometry['segments']];L=json.loads((D/'index.json').read_text(encoding='utf-8'))['LapMeters'];lo=L+starts[5];hi=L+starts[8]
 qs=[p for p in ps if lo<=p['reference']<=hi];x=[p['reference']-lo for p in qs]
 for ax,key in [(axes[0,1],'speed'),(axes[1,0],'gas'),(axes[1,1],'beta'),(axes[2,0],'offset')]:ax.plot(x,[p[key] for p in qs],color=co,label=label,lw=1.6)
axes[0,0].set(title='All saved speed history',xlabel='Native time [s]',ylabel='Speed [km/h]');axes[0,0].legend(ncol=2,fontsize=9)
axes[0,1].set(title='Lap 2 / corner 2: speed',ylabel='Speed [km/h]')
axes[1,0].set(title='Lap 2 / corner 2: actual active throttle',ylabel='Throttle [0..1]')
axes[1,1].set(title='Lap 2 / corner 2: controlled slip',ylabel='β [degrees]')
axes[2,0].set(title='Lap 2 / corner 2: line from native reference',ylabel='Offset [m]')
for ax in [axes[0,1],axes[1,0],axes[1,1],axes[2,0]]:ax.set_xlabel('Reference distance from corner entry [m]')
ax=axes[2,1]
for i,n in enumerate(['K1','F','autonomous146-viable','autonomous147-shortlist']):
 cs=[c for c in a['corners'] if c['run']==n and c['complete']];ax.bar([2*(c['lap']-1)+c['bend']+(i-1.5)*.19 for c in cs],[c['seconds'] for c in cs],width=.18,color=colors[n],label=labels[n])
ax.set(title='All six corners (#146 last two unmeasured)',xlabel='Corner in race order',ylabel='Time [s]',xticks=range(1,7));ax.legend(ncol=2,fontsize=9)
for ax in axes.flat:ax.grid(alpha=.18)
fig.savefig(D/'corner147.png',dpi=150);plt.close(fig)
print(D/'corner147.png')
