"""Publish the closed #176 study from immutable native and archived histories."""
import argparse
import io
import json
import math
from pathlib import Path
import sys
import time
from zipfile import ZipFile, ZIP_STORED, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'docs/experiments/dynamic-axle-load'
OLD = ROOT / 'docs/experiments/three-lap-global-planning'
sys.path.insert(0, str(ROOT/'tools/track-grip-root-cause'))
sys.path.insert(0, str(ROOT/'tools/three-lap-global-planning'))
from analyze175 import native_trace, window
from analyze172 import queue_audit
import numpy as np


def read(path):
    return json.loads(path.read_bytes())


def write(path, value):
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')


def at_reference(trace, s):
    ref = trace['s']
    indices = np.flatnonzero(ref >= s)
    if not len(indices) or not indices[0]:
        return None
    i = indices[0]
    u = (s-ref[i-1])/(ref[i]-ref[i-1])
    return {key:float(trace[key][i-1]+u*(trace[key][i]-trace[key][i-1])) for key in ('t','speed','dist')}


def load_history(outer, name):
    with ZipFile(io.BytesIO(outer.read(name+'.zip'))) as z:
        return json.loads(z.read('native.json')), json.loads(z.read('decisions.json'))


def fz_history(samples):
    return np.array([[x['Forces']['TyreLoads'][key] for key in ('FrontNormalNewtons','RearNormalNewtons')]
                     for x in samples[1:]])


def diagnostic(trial, native, choices, archived, original, old_choices):
    q=native_trace(native)
    queue_audit(native,choices)
    case=trial['Case']; branch=case['BranchSeconds']; start=case['BranchReference']; bend=case['Bend']
    samples=native['Riders'][0]['Samples']; old=archived['Riders'][0]['Samples']
    fz=fz_history(samples); q['fz']=fz
    assert [p for p in samples if p['Input']['Time']<branch-1e-8] == [p for p in old if p['Input']['Time']<branch-1e-8]
    if trial['FixedCommands']:
        assert [x['Action'] for x in choices] == [x['Action'] for x in old_choices[:len(choices)]]
    k=int(np.searchsorted(q['t'],branch-1e-8))
    loaded=q['t'][1:]>branch+1e-8
    assert np.all(fz[loaded]>0)
    assert np.max(np.abs(fz.sum(axis=1)-147*9.81))<1e-7
    before=q['t'][-1]
    compare_s=min(float(q['s'][-1]),case['Goal'])
    b=at_reference(original,compare_s); n=at_reference(q,compare_s)
    comparison_end=at_reference(q,compare_s)
    assert b and n and comparison_end
    # Compare only a common actually traversed interval. The violating endpoint
    # remains visible and is never credited as a legal completion.
    when=at_reference(original,start)['t']
    base_local=window(original,when,b['t'])
    new_local=window(q,branch,comparison_end['t'])
    active_high=lambda x,lo,hi:float(np.sum(np.clip(np.minimum(x['t'][1:],hi)-np.maximum(x['t'][:-1],lo),0,np.diff(x['t']))*(x['gas'][1:]>=.95)))
    straight=(q['s']>=start)&(q['s']<=min(compare_s,bend['Entry']))
    base_straight=(original['s']>=start)&(original['s']<=min(compare_s,bend['Entry']))
    fall=next((c for c in choices if c['Time']>=branch and c['Tracking']['Gate']=='no admissible spatial line'),None)
    last=samples[-1]
    transition=choices[-1]
    cap=sum(last['Forces']['TyreLoads'][key] for key in ('RearCapacityNewtons','FrontCapacityNewtons'))
    last_point=dict(time=float(q['t'][-1]),reference=float(q['s'][-1]),pose=float(q['pose'][-1]),
                    swept=float(q['swept'][-1]),inner=last['Inner'],outer=last['Outer'],
                    offset=last['Input']['Rider']['Road']['OffsetMeters'],speed_kph=float(q['speed'][-1]*3.6),
                    yaw_rate=float(q['yaw'][-1]),beta_deg=float(q['beta'][-1]),Fx=float(q['fx'][-1]),
                    Fy_front=float(q['ff'][-1]),Fy_rear=float(q['fy'][-1]),Fz_front=float(fz[-1,0]),
                    Fz_rear=float(fz[-1,1]),kappa=float(q['kappa'][-1]),capacity=cap,
                    gas=float(q['gas'][-1]),steering_deg=float(q['steering'][-1]),
                    tracking=transition['Tracking']['Gate'],transition=transition['Transition'])
    entry=at_reference(q,bend['Entry']); exit=at_reference(q,bend['Exit'])
    select=(q['s']>=bend['Entry'])&(q['s']<=bend['Exit'])
    profile={
        'entry_kph':None if entry is None else entry['speed']*3.6,
        'observed_minimum_kph':float(q['speed'][select].min()*3.6) if np.any(select) else None,
        'exit_kph':None if exit is None else exit['speed']*3.6,
        'following_straight_vmax_kph':float(q['speed'][q['s']>=bend['Exit']].max()*3.6) if exit else None
    }
    timing=[c['DecisionMilliseconds'] for c in choices]
    # Native telemetry reports the force used on interval i-1 -> i. Its load
    # derives from interval i-2 -> i-1 and the heading at i-1.
    i=np.flatnonzero(loaded)+1
    previous=(q['t'][i-1]-q['t'][i-2]).astype(np.float32).astype(np.float64)
    vel=np.array([[p['Input']['Rider']['Velocity'][a] for a in ('X','Y')] for p in samples],
                 dtype=np.float32).astype(np.float64)
    h=np.array([p['Input']['Rider']['Heading'] for p in samples],dtype=np.float32).astype(np.float64)[i-1]
    along=np.sum((vel[i-1]-vel[i-2])*np.column_stack((np.cos(h),np.sin(h))),axis=1)/previous
    expected_front=147*9.81*.6/1.3-147*along*trial['Height']/1.3
    lag_error=float(np.max(np.abs(expected_front-fz[i-1,0])))
    assert lag_error < .15, (trial['Id'],lag_error)
    commit_wheel=max(abs(p['Input']['Omega']-p['Powertrain']['Wheel']['AfterOmega']) for p in samples[k+1:])
    commit_steering=max(abs(p['Input']['Steering']-p['Forces']['SteeringAfter']) for p in samples[k+1:])
    commit_shaft=max(abs(p['CrankOmega']-p['Powertrain']['AfterOmega']) for p in samples[k+1:])
    assert commit_wheel < 1e-8 and commit_steering == commit_shaft == 0
    assert abs(new_local['energy_J']['closure_residual']) < 3, (trial['Id'],new_local)
    summary=dict(trial=trial,profile=profile,branch_speed_kph=float(q['speed'][k]*3.6),
                 common_s=compare_s,base_speed_at_common_kph=b['speed']*3.6,new_speed_at_common_kph=n['speed']*3.6,
                 base_time_at_common=b['t'],new_time_at_common=n['t'],
                 matched_partial_gain_s=b['t']-n['t'],
                 base_straight_vmax_kph=float(original['speed'][base_straight].max()*3.6) if np.any(base_straight) else None,
                 new_straight_vmax_kph=float(q['speed'][straight].max()*3.6) if np.any(straight) else None,
                 base_ledger=base_local,new_ledger=new_local,
                 high_drive_s=dict(BASE=active_high(original,when,b['t']),NEW=active_high(q,branch,before)),
                 max_kappa_after_branch=float(np.max(q['kappa'][loaded])),
                 min_front_N=float(fz[loaded,0].min()),min_rear_N=float(fz[loaded,1].min()),
                 load_lag_recompute_max_N=lag_error,last=last_point,
                 committed_max_error=dict(wheel=commit_wheel,steering=commit_steering,crank=commit_shaft),
                 after_branch_seconds=before-branch,post_max_beta=float(abs(q['beta'][k:]).max()),
                 first_no_admissible=None if fall is None else dict(time=fall['Time'],reference=fall['Reference']),
                 decision_ms=dict(median=float(np.median(timing)),p95=float(np.percentile(timing,95)),
                                  p99=float(np.percentile(timing,99)),first=timing[0],maximum=max(timing)),
                 forecasts=native['Forecasts'],contact_queries=native['NativeContactQueries'])
    return summary,q


def geometry():
    with ZipFile(OLD/'172-protocol.zip') as z:
        return json.loads(z.read('geometry.json'))


def figures(rows, traces, original):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':9})
    colors={'BASE':'#475569','repair-T35':'#267aac','repair-T55':'#d07a40'}
    cases=sorted({x['trial']['Case']['Id'] for x in rows})
    fig,axes=plt.subplots(1,len(cases),figsize=(10,5),layout='constrained',squeeze=False)
    ax=axes[0]
    geom=geometry()
    for a,case in zip(ax,cases):
        for edge in ('Inner','Outer'):
            pts=[q[edge] for s in geom['Segments'] for q in s['Sections']]
            a.plot([q['X'] for q in pts],[q['Y'] for q in pts],color='.7',lw=.75)
        c=next(x['trial']['Case'] for x in rows if x['trial']['Case']['Id']==case)
        s0=c['BranchReference']; goal=c['Goal']
        b=original[case];m=(b['s']>=s0)&(b['s']<=goal)
        a.plot(*b['xy'][m].T,color=colors['BASE'],lw=1.5,label='BASE archiwum')
        for r in rows:
            t=r['trial']
            if t['Case']['Id']!=case:continue
            z=traces[t['Id']];m=z['t']>=c['BranchSeconds']-1e-8
            name=t['Stage']+'-'+t['Id'].split('-')[-1]
            a.plot(*z['xy'][m].T,color=colors[name],lw=1.25,label=name)
            a.plot(*z['xy'][-1],marker='x',color=colors[name])
        a.set_title(case);a.set_aspect('equal');a.set_xlabel('X [m]');a.set_ylabel('Y [m]')
    ax[0].legend(fontsize=7)
    fig.suptitle('Rzeczywiste trajektorie od włączenia transferu · × pierwszy stop swept')
    fig.savefig(DOC/'176-XY.png',dpi=160);plt.close(fig)
    fig,axes=plt.subplots(4,len(cases),figsize=(10,10),layout='constrained',squeeze=False)
    for i,case in enumerate(cases):
        c=next(x['trial']['Case'] for x in rows if x['trial']['Case']['Id']==case)
        records=[('BASE',original[case])]
        for r in rows:
            t=r['trial']
            if t['Case']['Id']==case:records.append((t['Stage']+'-'+t['Id'].split('-')[-1],traces[t['Id']]))
        for tag,z in records:
            d=z['s']-c['BranchReference'];m=(d>=0)&(z['s']<=c['Goal']);q=m[1:]
            def line(panel,x,y,ls='-',label=None):
                axes[panel,i].plot(x,y,color=colors[tag],lw=1.05,ls=ls,label=label)
            line(0,d[m],z['speed'][m]*3.6,label=tag)
            line(1,d[1:][q],z['fz'][q,0]);line(1,d[1:][q],z['fz'][q,1],'--')
            line(2,d[1:][q],z['fx'][q]);line(2,d[1:][q],z['fy'][q],'--');line(2,d[1:][q],z['ff'][q],':')
            line(3,d[m],z['pose'][m],'--');line(3,d[m],z['swept'][m])
        axes[0,i].set_title(case+' · BASE do celu, NEW do DNF');axes[0,i].legend(fontsize=7)
        for j,label in enumerate(('V [km/h]','Fz przód / tył [N]','Fx tył / Fy tył / Fy przód [N]','Swept / pose [m]')):
            axes[j,i].set_ylabel(label);axes[j,i].grid(alpha=.2)
        axes[3,i].axhline(.5,color='red',ls=':');axes[3,i].set_xlabel('Od rozwidlenia [m referencji]')
    fig.savefig(DOC/'176-sily-predkosc.png',dpi=150);plt.close(fig)
    fig,axes=plt.subplots(5,len(cases),figsize=(10,12),layout='constrained',squeeze=False)
    for i,case in enumerate(cases):
        c=next(x['trial']['Case'] for x in rows if x['trial']['Case']['Id']==case)
        selected=[('BASE',original[case])]+[(r['trial']['Stage']+'-'+r['trial']['Id'].split('-')[-1],
                   traces[r['trial']['Id']]) for r in rows if r['trial']['Case']['Id']==case]
        for tag,z in selected:
            dt=z['t']-c['BranchSeconds'];m=(dt>=-1e-8)&(z['s']<=c['Goal']);q=m[1:]
            def line(panel,y,mask=m,interval=False,ls='-',label=None):
                x=dt[1:] if interval else dt
                axes[panel,i].plot(x[mask],y[mask],color=colors[tag],lw=1.05,ls=ls,label=label)
            line(0,z['gas'],label=tag);line(0,z['requested'],ls='--')
            line(1,z['beta']);line(1,z['steering'],ls='--')
            line(2,z['yaw']);line(3,z['kappa'],q,True)
            for key,ls in [('wheel_slip','-'),('rear_lateral','--'),('front_lateral',':')]:
                y=np.cumsum(np.where(dt[1:]>=-1e-8,z['energy'][key],0))/1000
                line(4,y,q,True,ls)
        for j,label in enumerate(('Gaz aktywny / żądany','β / skręt [°]','Yaw rate [rad/s]','Wheelspin κ','Praca poślizgu [kJ]')):
            axes[j,i].set_ylabel(label);axes[j,i].grid(alpha=.2)
        axes[0,i].set_title(case);axes[0,i].legend(fontsize=7);axes[-1,i].set_xlabel('Czas od rozwidlenia [s]')
    fig.suptitle('Praca poślizgu: wzdłużna (ciągła), boczna tyłu (kreski), przodu (kropki)')
    fig.savefig(DOC/'176-sterowanie-energia.png',dpi=145);plt.close(fig)


def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('evidence',type=Path)
    ap.add_argument('archive_observations',type=Path)
    args=ap.parse_args(); e=args.evidence; started=time.process_time()
    DOC.mkdir(parents=True,exist_ok=True)
    closed=read(e/'closed.json')
    assert closed['Worlds']==14 and closed['S3']=='UNKNOWN'
    invalid=read(e/'invalid-integration.json')
    cases=read(e/'cases.json'); held=read(e/'held.json')
    rows=[];traces={};baseline={};old_runs={};bases=[]
    with ZipFile(OLD/'172-validation-K4.zip') as archive:
        for c in cases:
            run,dec=load_history(archive,c['Archive']); q=native_trace(run)
            queue_audit(run,dec)
            q['fz']=fz_history(run['Riders'][0]['Samples']); baseline[c['Id']]=q
            old_runs[c['Id']]=(run,dec)
            entry=at_reference(q,c['Bend']['Entry']);exit=at_reference(q,c['Bend']['Exit'])
            start=at_reference(q,c['BranchReference'])
            goal=at_reference(q,c['Goal'])
            inside=(q['s']>=c['Bend']['Entry'])&(q['s']<=c['Bend']['Exit'])
            cut=(q['t']>=c['BranchSeconds'])&(q['t']<=goal['t'])
            bases.append(dict(case=c,goal_seconds=goal['t'],manoeuvre_seconds=goal['t']-c['BranchSeconds'],
                              entry_kph=entry['speed']*3.6,exit_kph=exit['speed']*3.6,
                              minimum_bend_kph=float(q['speed'][inside].min()*3.6),
                              vmax_kph=float(q['speed'][cut].max()*3.6),
                              min_pose_m=float(q['pose'][cut].min()),min_swept_m=float(q['swept'][cut].min()),
                              max_beta_deg=float(abs(q['beta'][cut]).max()),
                              ledger=window(q,start['t'],goal['t'])))
    for path in sorted(e.glob('repair-*-result.json')):
        t=read(path); c=t['Case']['Id']
        with ZipFile(e/(t['Id']+'.zip')) as z:
            run=json.loads(z.read('native.json'));dec=json.loads(z.read('decisions.json'))
        old,old_choices=old_runs[c]
        row,q=diagnostic(t,run,dec,old,baseline[c],old_choices)
        traces[t['Id']]=q;rows.append(row)
    figures(rows,traces,baseline)
    (DOC/'stany-osi.csv').write_bytes((args.archive_observations/'stany-osi.csv').read_bytes())
    archive_b=read(args.archive_observations/'B-archiwum.json')
    # Store the native receipts in separate, bounded ZIPs. BASE remains in #172.
    for stage in ('tape','live','aware','repair'):
        with ZipFile(DOC/('176-'+stage+'.zip'),'w',compression=ZIP_STORED) as z:
            for p in sorted(e.glob(stage+'-*.zip')):
                if p.name.endswith('-source.zip'):continue
                z.write(p,p.name)
    with ZipFile(DOC/'176-protokol.zip','w',compression=ZIP_DEFLATED) as z:
        for p in sorted(e.iterdir()):
            if p.suffix=='.json' or p.name.endswith('-source.zip'):
                z.write(p,p.name)
        z.write(args.archive_observations/'B-archiwum.json','B-archiwum.json')
    cpu_receipts={p.name:read(p)['CpuSeconds'] for p in sorted(e.glob('*.done.json'))}
    cpu_receipts['B-archiwum.json']=archive_b['cpu_seconds']
    cpu_receipts['invalid-integration.json']=invalid['CpuSeconds']
    cpu_receipts['v0-analysis']=read(e/'v0-analysis-cost.json')['CpuSeconds']
    cpu_receipts['analysis']=time.process_time()-started
    summary=dict(verdict='PARTIAL',functional='NOT_ESTABLISHED',sport='NOT_TESTED',s3='UNKNOWN',
                 cpu_seconds=sum(cpu_receipts.values()),cpu_receipts=cpu_receipts,new_worlds=14,
                 diagnostics=closed,baselines=bases,native=rows,archive_deltas=archive_b['median_deltas'],
                 invalid=invalid,invalid_results=[read(p) for p in sorted(e.glob('*-result.json'))
                                                 if not p.name.startswith('repair-')],
                 held=[dict(**q,BASE='NOT_RUN',NEW='NOT_RUN') for q in held])
    write(DOC/'summary176.json',summary)
    lines=['# Wszystkie próby #176','','| Próba | s końca [m] | V wej. [km/h] | V min zaobserwowana | V wyj. | Swept min [m] | Fz przodu min [N] | Czas do celu [s] | Wynik |',
           '|---|---:|---:|---:|---:|---:|---:|---:|---|']
    def f(x,dp=3):return '—' if x is None else f'{x:.{dp}f}'
    for b in bases:
        lines.append(f"| BASE {b['case']['Id']} | {f(b['case']['Goal'])} | {f(b['entry_kph'])} | {f(b['minimum_bend_kph'])} | {f(b['exit_kph'])} | {f(b['min_swept_m'])} | 665.57 | {f(b['goal_seconds'],6)} | archiwum #172, legalny |")
    for r in rows:
        t=r['trial'];p=r['profile'];o=t['Outcome']
        lines.append(f"| {t['Id']} | {f(o['EndReference'])} | {f(p['entry_kph'])} | {f(p['observed_minimum_kph'])} | {f(p['exit_kph'])} | {f(o['MinSweep'])} | {f(r['min_front_N'],2)} | {f(t['GoalSeconds'],6)} | {o['Reason']} |")
    lines+=['','Czasy do celu są liczone od zatrzymanego startu. Minima prędkości NEW dotyczą tylko zaobserwowanej części łuku. Brak wartości oznacza nieosiągnięty przekrój.',
            '','## Nieważne technicznie próby przed naprawą','','| Próba | Maks. rozbieżność koła [rad/s] | Maks. rozbieżność skrętu [rad] | Status |',
            '|---|---:|---:|---|']
    lines.extend(f"| {q['Id']} | {q['MaxUncommittedWheelRadiansPerSecond']:.6f} | {q['MaxUncommittedSteeringRadians']:.6f} | INVALID_INTEGRATION |"
                 for q in invalid['Receipts'])
    lines+=['','## Odłożone pary pełnego S3','','| Stan | Pole | Start [m] | Offset [m] | BASE | NEW |',
            '|---|---|---:|---:|---|---|']
    lines.extend(f"| {q['Id']} | {q['Gate']} | {q['Start']} | {q['Offset']} | NOT_RUN | NOT_RUN |" for q in held)
    lines+=['','Etap pełnego S3 nie został uruchomiony, ponieważ żaden wariant nie przeszedł pełnego łuku. Mediany i porównanie sportowe pozostają UNKNOWN.']
    (DOC/'TABELE.md').write_text('\n'.join(lines)+'\n',encoding='utf-8',newline='\n')
    print(json.dumps(dict(worlds=14,valid_trials=len(rows),cpu=summary['cpu_seconds'],verdict='PARTIAL')))


if __name__=='__main__':
    main()
