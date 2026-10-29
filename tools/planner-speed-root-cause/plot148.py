"""Create scientific figures from saved accepted states only."""
import json,gzip
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
D=Path(__file__).resolve().parents[2]/'docs/experiments/planner-speed-root-cause';a=json.loads((D/'analysis.json').read_text(encoding='utf-8'));p=json.loads((D/'physical-analysis.json').read_text(encoding='utf-8'));curves=json.loads(gzip.decompress((D/'comparison.json.gz').read_bytes()))
fig,axs=plt.subplots(2,2,figsize=(14,10),layout='constrained');fig.suptitle('Study #148 — exact optimization, unchanged riding trajectory',fontsize=17)
colors={'K1':'#6e7b91','autonomous147-shortlist':'#ce8b3c','autonomous148-prefix':'#087e8b'}
for c in curves:
 if c['name'] not in colors:continue
 label={'autonomous147-shortlist':'#147','autonomous148-prefix':'#148 exact'}.get(c['name'],c['name']);ps=c['points'];kw=dict(color=colors[c['name']],label=label,lw=1.5,linestyle='--' if 'prefix' in c['name'] else '-')
 axs[0,0].plot([q['reference'] for q in ps],[q['speed'] for q in ps],**kw)
 lo=318.77875+194.389375;hi=318.77875+291.77875;qs=[q for q in ps if lo<=q['reference']<=hi];axs[0,1].plot([q['reference']-lo for q in qs],[q['speed'] for q in qs],**kw)
axs[0,0].set(title='Three laps: speed against native reference distance',xlabel='Reference distance [m]',ylabel='Speed [km/h]');axs[0,0].legend()
axs[0,1].set(title='Lap 2 / corner 2: overlap is the exact-equivalence result',xlabel='Reference distance after entry [m]',ylabel='Speed [km/h]')
ss=[x for x in p['segments'] if x['run']=='#147'];labels=[str(x['lap'])+'/'+{'start/home-before-bend':'S0','bend1':'B1','back-straight':'S1','bend2':'B2','home-after-bend':'S2'}[x['segment']] for x in ss];bottom=[0]*len(ss)
for keys,label,color in [(['front_slip_loss_J','rear_lateral_slip_loss_J'],'Lateral tyre dissipation','#087e8b'),(['rear_long_slip_loss_J'],'Longitudinal tyre slip','#cb8240'),(['clutch_heat_J','transmission_loss_J','bearing_loss_J'],'Drivetrain / bearing losses','#8f6daa'),(['resistance_J'],'Resistance','#768194')]:
 vals=[sum(-x['energy'][k] if k=='resistance_J' else x['energy'][k] for k in keys)/1000 for x in ss];axs[1,0].bar(range(len(ss)),vals,bottom=bottom,label=label,color=color);bottom=[x+y for x,y in zip(bottom,vals)]
axs[1,0].set(title='Saved #147 energy ledger (same physical trace for #148)',ylabel='Dissipated energy [kJ]',xticks=range(len(ss)),xticklabels=labels);axs[1,0].tick_params(axis='x',rotation=60);axs[1,0].legend(fontsize=8)
names=['K1','autonomous147-shortlist','autonomous148-exact','autonomous148-hoisted','autonomous148-prefix'];runs={x['name']:x for x in a['full_runs']};cpu=[runs[n]['controller_cpu_s'] for n in names];bars=axs[1,1].bar(['K1','#147','A: clearance','A: coefficients','A: prefix'],cpu,color=['#768194','#cb8240','#49a6ab','#188e98','#087e8b']);axs[1,1].axhspan(40,60,color='#70bd92',alpha=.18,label='Orientational 40–60 CPU-s goal');axs[1,1].bar_label(bars,fmt='%.2f',padding=3);axs[1,1].set(title='Measured controller CPU: equal native trajectories',ylabel='CPU seconds');axs[1,1].tick_params(axis='x',rotation=15);axs[1,1].legend(fontsize=9)
for ax in axs.flat:ax.grid(alpha=.16)
fig.savefig(D/'speed148.png',dpi=150);plt.close(fig)
fig,axs=plt.subplots(1,2,figsize=(13,5),layout='constrained');fig.suptitle('Diagnostic controls — distinct from autonomous G0 evidence',fontsize=16)
for k in [0,40,70,90]:
 r=json.loads(gzip.decompress((D/f'diagnostics/long-straight-{k}.json.gz').read_bytes()));ps=r['Riders'][0]['Samples'];qs=[q for q in ps if q['Input']['Rider']['DistanceMeters']<=100];axs[0].plot([q['Input']['Rider']['DistanceMeters'] for q in qs],[q['Input']['Rider']['Speed']*3.6 for q in qs],label=f'Initial {k} km/h')
axs[0].axvline(62,color='#333',ls=':',label='62 m');axs[0].set(xlabel='Physical distance on straight [m]',ylabel='Speed [km/h]',title='Same engine, tyres and full throttle');axs[0].legend(fontsize=8)
g=[x for x in a['geometry'] if x['name']!='G0'];x=list(range(len(g)));axs[1].plot(x,[q['back_straight']['exit_kph'] for q in g],'o-',label='Next corner entry');axs[1].plot(x,[q['bend2']['minimum_kph'] for q in g],'s-',label='Corner minimum');axs[1].set(xticks=x,xticklabels=['G0c\n62/31','G1\n75/31','G2\n90/31','G3\n62/36','G4\n62/42'],ylabel='Speed [km/h]',title='Circular-family K1 controls: entry states also change');axs[1].legend()
for ax in axs:ax.grid(alpha=.18)
fig.savefig(D/'diagnostics148.png',dpi=150);plt.close(fig)
print('Figures saved; no simulation performed.')
