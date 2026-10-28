"""Export native comparison figure; no trajectory generation or image editing."""
import gzip,json,math
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
ROOT=Path(__file__).resolve().parents[2];DOC=ROOT/'docs/experiments/autonomous-corner-planning'
a=json.loads((DOC/'analysis.json').read_text());g=json.loads((ROOT/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json').read_text())
starts=[q['startReferenceMeters'] for q in g['segments']]
files={'K1':ROOT/'docs/experiments/standing-start-three-laps/native/K1.json.gz','F':ROOT/'docs/experiments/standing-start-three-laps/native/F.json.gz',
 'Ręczny benchmark':ROOT/'docs/experiments/corner-speed-loss/native/joint-grid-29.json.gz','Planner + kontynuacja':DOC/'native/autonomous146-viable.json.gz'}
colors=['#3b6b9a','#c66b21','#8c7a1c','#16836b'];fig,axes=plt.subplots(3,2,figsize=(13,12),layout='constrained')
for (label,path),color in zip(files.items(),colors):
 r=json.loads(gzip.decompress(path.read_bytes()));lo=r['LapMeters']+starts[5];hi=r['LapMeters']+starts[8];ss=[s for s in r['Riders'][0]['Samples'] if lo<=s['Input']['Rider']['TotalReferenceMeters']<=hi]
 pct=[100*(s['Input']['Rider']['TotalReferenceMeters']-lo)/(hi-lo) for s in ss];vs=[s['Input']['Rider'] for s in ss]
 for ax,y in [(axes[0,0],[v['Speed']*3.6 for v in vs]),(axes[1,0],[v['SlideRadians']*180/math.pi for v in vs]),(axes[1,1],[v['Road']['OffsetMeters'] for v in vs])]:ax.plot(pct,y,color=color,label=label,lw=1.7)
 axes[0,1].step(pct,[s['Input']['Active']['Drive'] for s in ss],where='post',color=color,label=label,lw=1.3)
 axes[2,0].plot([v['Position']['X'] for v in vs],[v['Position']['Y'] for v in vs],color=color,label=label,lw=1.7)
for ax,title,unit in [(axes[0,0],'Prędkość','km/h'),(axes[0,1],'Gaz aktywny','0–1'),(axes[1,0],'Poślizg β','°'),(axes[1,1],'Linia: offset od natywnej referencji','m')]:
 ax.set(title=title,xlabel='Postęp referencyjny badanego łuku [%]',ylabel=unit);ax.grid(alpha=.2)
axes[0,0].legend(fontsize=9,loc='lower left');axes[0,1].set_ylim(-.04,1.04)
axes[2,0].set(title='Natywne pozycje XY — badany łuk',xlabel='X [m]',ylabel='Y [m]');axes[2,0].axis('equal');axes[2,0].grid(alpha=.2)
ax=axes[2,1];xx=list(range(6));width=.24
for j,(name,color) in enumerate([('K1',colors[0]),('F',colors[1]),('autonomous146-viable',colors[3])]):
 cs=[c for c in a['corners'] if c['run']==name];valid=[(k,c) for k,c in enumerate(cs) if c['seconds'] is not None]
 ax.bar([k+(j-1)*width for k,c in valid],[c['seconds'] for k,c in valid],width=width,color=color,label=name if j<2 else 'Planner + kontynuacja')
ax.set(xticks=xx,xticklabels=['1/1','1/2','2/1','2/2','3/1','3/2'],ylabel='Czas łuku [s]',xlabel='Okrążenie / łuk',title='Wszystkie sześć łuków');ax.grid(axis='y',alpha=.2)
for k in [4,5]:ax.text(k+.24,.3,'N/D',rotation=90,ha='center',color=colors[3],fontsize=9)
ax.legend(fontsize=8,loc='upper left')
fig.suptitle('Badanie #146 — lepszy badany łuk, pełne trzy okrążenia: PARTIAL\nNowy planner: 5,295 s · wejście 81,71 · minimum 57,11 · wyjście 61,73 km/h',fontsize=15)
fig.savefig(DOC/'corner146.png',dpi=160);plt.close(fig)
