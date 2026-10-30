"""Standalone native telemetry figures. Read-only except the measured export ledger."""
from pathlib import Path
import csv,gzip,json,time
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
from analyze156 import D,R,read,write
COLORS={'A':'#087f8c','B':'#3861c8','C':'#d35936'}
NAMES={'first':'Po starcie · pierwszy łuk','second':'Po prostej · drugi łuk','historical':'Historyczny spadek 94→46 km/h'}
def rows(name):
 with gzip.open(D/'native'/f'{name}-intervals.csv.gz','rt',encoding='utf-8') as f:
  return [{k:float(v) if v else None for k,v in r.items()} for r in csv.DictReader(f)]
def main():
 began=time.process_time();a=read(D/'analysis.json');g=read(R/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json');out=D/'plots';out.mkdir(exist_ok=True)
 plt.rcParams.update({'font.family':'DejaVu Sans','font.size':9,'axes.spines.top':False,'axes.spines.right':False,'axes.grid':True,'grid.alpha':.16,'figure.facecolor':'#fafaf8','axes.facecolor':'#fafaf8','savefig.facecolor':'#fafaf8'})
 selected=a.get('matrix_results',a['results'])
 for fixture,title in NAMES.items():
  rs=[r for r in selected if r['fixture']==fixture and r['strategy'] in ('T0','T1') and r['status']=='PASS'];fig,ax=plt.subplots(3,2,figsize=(13,11),layout='constrained');fig.suptitle('Badanie #156  |  '+title,fontsize=17,fontweight='bold')
  for segment in g['segments']:
   for edge in ['inner','outer']:
    xy=[s[edge] for s in segment['sections']];ax[0,1].plot([q['x'] for q in xy],[q['y'] for q in xy],color='#939a9c',lw=1,zorder=0)
  for r in rs:
   q=rows(r['label']);x=np.cumsum([v['distance_m'] for v in q]);ref=np.array([v['reference_m'] for v in q]);ref-=ref[0];style='--' if r['strategy']=='T0' else '-';c=COLORS[r['family']];label=r['family']+' · '+r['strategy'];alpha=.7 if style=='--' else 1
   ax[0,0].plot(x,[v['speed_kph'] for v in q],style,color=c,lw=1.5,alpha=alpha,label=label)
   ax[0,1].plot([v['x_m'] for v in q],[v['y_m'] for v in q],style,color=c,lw=1.3,alpha=alpha)
   ax[1,0].step(ref,[v['active_gas'] for v in q],where='post',ls=style,color=c,lw=1.2,alpha=alpha)
   ax[2,1].plot(ref,[v['curvature_per_m'] for v in q],style,color=c,lw=1,alpha=alpha)
  best=min(rs,key=lambda r:r['time_s']);q=rows(best['label']);ref=np.array([v['reference_m'] for v in q]);ref-=ref[0]
  for key,label,c,ls in [('front_fx_body_N','przód Fx (osie korpusu)','#3861c8','--'),('front_fy_body_N','przód Fy (osie korpusu)','#3861c8','-'),('rear_fx_N','tył Fx','#d35936','--'),('rear_fy_N','tył Fy','#d35936','-')]:ax[1,1].plot(ref,[v[key] for v in q],label=label,color=c,ls=ls,lw=1)
  for key,label,c in [('combustion_J','spalanie','#1c7352'),('front_slip_loss_J','poślizg boczny przodu','#3861c8'),('rear_lateral_slip_loss_J','poślizg boczny tyłu','#d35936'),('rear_long_slip_loss_J','poślizg wzdłużny','#9d6bb3')]:ax[2,0].plot(ref,np.cumsum([v[key]*v['time_weight_s']/v['dt_s'] for v in q])/1000,label=label,color=c,lw=1.5)
  ax[0,0].set(xlabel='Rzeczywista droga od wspólnego startu [m]',ylabel='Prędkość [km/h]',title='Cały wspólny manewr');ax[0,0].legend(ncol=3,fontsize=8)
  ax[0,1].set(xlabel='X [m]',ylabel='Y [m]',title='Rzeczywiste XY · wspólne krawędzie G0/C2');ax[0,1].set_aspect('equal')
  ax[1,0].set(xlabel='Postęp referencyjny od startu [m]',ylabel='Aktywny gaz 0–1',ylim=(-.05,1.05),title='Gaz po pełnym opóźnieniu 0,24 s')
  ax[1,1].set(xlabel='Postęp referencyjny od startu [m]',ylabel='Siła [N]',title='Siły kontaktów · najszybszy legalny wariant');ax[1,1].legend(fontsize=8,ncol=2)
  ax[2,0].set(xlabel='Postęp referencyjny od startu [m]',ylabel='Praca / strata skumulowana [kJ]',title='Bilans napędu i poślizgu · ten sam wariant');ax[2,0].legend(fontsize=8,ncol=2)
  ax[2,1].set(xlabel='Postęp referencyjny od startu [m]',ylabel='Krzywizna rzeczywistego ruchu [1/m]',title='κ = (vx·ay − vy·ax) / |v|³')
  fig.savefig(out/f'{fixture}.png',dpi=165);plt.close(fig)
  fig,ax=plt.subplots(2,2,figsize=(12,7),layout='constrained');fig.suptitle(title+' · stan i budżet sił · '+best['family']+'/'+best['strategy'],fontsize=15,fontweight='bold')
  for key,label in [('front_util','przód'),('rear_util','tył')]:ax[0,0].plot(ref,[100*v[key] for v in q],label=label)
  ax[0,0].axhline(100,color='#444',lw=.6);ax[0,0].set(ylabel='Wykorzystanie pojemności [%]',xlabel='Postęp [m]');ax[0,0].legend()
  ax[0,1].plot(ref,[v['beta_deg'] for v in q],label='β');ax[0,1].set(ylabel='Uślizg korpusu β [°]',xlabel='Postęp [m]');ax[0,1].axhline(45,color='#d35936',ls='--',lw=.8)
  ax[1,0].plot(ref,np.degrees(np.unwrap([v['body_heading_rad'] for v in q])),label='orientacja korpusu ψ');ax[1,0].plot(ref,np.degrees(np.unwrap([v['travel_heading_rad'] for v in q])),label='kierunek prędkości χ');ax[1,0].set(ylabel='Kąt [°]',xlabel='Postęp [m]');ax[1,0].legend()
  ax[1,1].plot(ref,[v['yaw_rads'] for v in q],label='yaw');ax[1,1].set(ylabel='Prędkość yaw [rad/s]',xlabel='Postęp [m]')
  fig.savefig(out/f'{fixture}-state.png',dpi=165);plt.close(fig)
 ledger=read(D/'budget.json');ledger.append(dict(Label='figures-v1',CpuSeconds=time.process_time()-began,Closed=True,Reason='native speed/XY/forces/energy/throttle/actual-curvature figures'));write(D/'budget.json.next',ledger);(D/'budget.json.next').replace(D/'budget.json')
if __name__=='__main__':main()
