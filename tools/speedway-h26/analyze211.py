"""Read accepted H28-F evidence and summarize coupled-transition feasibility."""
import argparse
from bisect import bisect_right
import csv
import json
from pathlib import Path

from analyze210 import crossing, divergence, model_series, native_series, performance, read, write


def transition_rows(run, track):
    points, controls = model_series(run)
    times = [p['Time'] for p in controls]
    rows = []
    for i,bend in enumerate(track['Bends']):
        end = track['Bends'][i+1]['Entry'] if i < 5 else track['LapMeters']*3
        start = bend['Exit']
        distances = sorted(set([start + 5*n for n in range(14) if start + 5*n < end] + [end]))
        for s in distances:
            point = crossing(points,s)
            index = min(len(controls)-1,bisect_right(times,point['Time'])-1)
            step = run['Trace'][index]
            force, power, before = step['Forces'], step['Power'], step['Before']
            rows.append(dict(Visit=i+1, MetersAfterExit=s-start, **point, AcceptedCheckpoint=before,
                             Request=step['Command']['Selected'], Dominant=step['Command']['Dominant'],
                             Pending=before['Pending'], Active=before['Active'], Forces=force, Losses=power,
                             LongitudinalAcceleration=step['Acceleration'], Swept=step['After']['Sweep'],
                             Outer=step['After']['Outer'], Inner=step['After']['Inner'],
                             SteeringTravelLeftRadians=max(0., 1.5707963267948966*(step['After']['Time']-before['State']['Time'])
                                 - abs(force['SteeringAfter']-force['SteeringBefore']))))
    return rows


def entry_survey(raw):
    surveys = []
    targets = (95,100,105,110,115,120)
    for arm in raw['Surveys']:
        entered = []
        for trial in arm['Trials']:
            result = trial['Results'][0]
            mark = next((p for p in result['Marks'] if abs(p['GateS']-arm['EntryS'])<1e-8), None)
            if mark:
                marks = result['Marks']
                state = mark['State']
                entered.append(dict(Id=trial['Id'], Kph=state['State']['Rider']['Speed']*3.6,
                    State=state, ThroughNext=any(x['GateS']>=arm['StabilizationS'] for x in marks),
                    Finish=result['Race']['Finish'], Reserve=result['Race']['Sweep'], Stop=result['Race']['Stop'],
                    ReleaseMeters=next(l['ReleaseMeters'] for l in trial['Policy']['Links'] if l['Visit']==arm['Visit']-1)))
        bins = []
        for target in targets:
            matching = [e for e in entered if target-2.5 <= e['Kph'] < target+2.5]
            eligible = [e for e in matching if e['Finish'] is not None]
            bins.append(dict(NearKph=target, Entered=len(matching),
                Stabilized=sum(e['ThroughNext'] for e in matching), Full=len(eligible),
                Fastest=min((e['Finish'] for e in eligible),default=None)))
        surveys.append(dict(Visit=arm['Visit'], Attempted=len(arm['Trials']), EntryS=arm['EntryS'], Bins=bins,
            MaxEntered=max((e['Kph'] for e in entered),default=None),
            MaxStabilized=max((e['Kph'] for e in entered if e['ThroughNext']),default=None),
            MaxFull=max((e['Kph'] for e in entered if e['Finish'] is not None),default=None), Entries=entered))
    return surveys


def envelope(runs, track):
    groups = [[] for _ in track['Bends'][1:]]
    for run in runs:
        if not run['Complete']:
            continue
        points, _ = model_series(run)
        for bend, examples in zip(track['Bends'][1:], groups):
            row = next(r for r in run['Trace'] if r['Before']['State']['Rider']['TotalReferenceMeters'] >= bend['Entry'])
            state = row['Before']
            body = state['State']['Rider']
            p = crossing(points,bend['Entry'])
            examples.append(dict(Fixture=run['Fixture'], Offset=body['Road']['OffsetMeters'],
                                 Heading=body['Heading'], Course=p['Course'], Speed=p['Speed'],
                                 Beta=body['SlideRadians'], Yaw=body['AngularVelocity'], State=state,
                                 Swept=row['After']['Sweep'], Finish=run['Finish'],
                                 RateTravelPerReview=1.5707963267948966*.05,
                                 AngleTravelBeforeStop=.6108652949333191-abs(state['State']['Steer'])))
    envelope_rows = []
    keys=('Offset','Heading','Course','Speed','Beta','Yaw','Swept','AngleTravelBeforeStop')
    for i,(bend,examples) in enumerate(zip(track['Bends'][1:],groups),1):
        envelope_rows.append(dict(Incoming=i+1, EntryS=bend['Entry'], Cases=examples,
            Range={k:[min(e[k] for e in examples),max(e[k] for e in examples)] for k in keys} if examples else {},
            Interpretation='Ranges summarize complete trajectory witnesses. Crossed combinations of extrema are untested.'))
    return envelope_rows


def root_figures(data, fig_dir):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    p = read(data/'root/root-audit.json.gz')
    hi = p['PublishedBox']['Hi']['Ax']
    lo = p['PublishedBox']['Lo']['Ax']
    decimal = read(data/'root/decimal.json')
    center = (hi+lo)/2
    fig,(ax,local)=plt.subplots(1,2,figsize=(11.5,4.2),layout='constrained')
    ax.plot([v['Ax'] for v in p['Broad']],[v['Residual'] for v in p['Broad']],color='#17726C')
    ax.axhline(0,color='#555F6C',linestyle=':')
    ax.set(xlabel='Trial longitudinal acceleration (m/s²)', ylabel='Residual (m/s²)', title='Positive-load domain')
    local.plot([(v['Ax']-center)*1e6 for v in p['Local']], [v['Residual']*1e6 for v in p['Local']],
               label='Frozen evaluation',color='#17726C')
    local.scatter([(float(v['Ax'])-center)*1e6 for v in decimal['Local']], [float(v['residual'])*1e6 for v in decimal['Local']],
                  s=9,label='76-digit evaluation',color='#A14D25')
    local.axvspan((lo-center)*1e6,(hi-center)*1e6,color='#F3AC75',alpha=.9,label='Rejected box')
    local.axhline(0,color='#555F6C',linestyle=':')
    local.set(xlim=(-.75,.65),ylim=(-.24,5.5),xlabel='Acceleration relative to rejected-box center (µm/s²)',
              ylabel='Residual (µm/s²)',title='One crossing and a nearby steering-rate transition')
    local.legend(loc='upper left',frameon=False,fontsize=8)
    fig.suptitle('Study #211   •   Archived guard residual at 48.610 s',fontweight='bold')
    fig.savefig(fig_dir/'root-residual.png',dpi=155)
    plt.close(fig)


def race_figure(fig_dir, track, reference, selected):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig,axes=plt.subplots(3,1,figsize=(12,7.5),sharex=True,layout='constrained')
    for label, color, run in (('#210 frozen', '#6B758A',reference),('#211 coupled','#087F74',selected)):
        points, controls=model_series(run)
        axes[0].plot([x['S'] for x in points],[x['Speed'] for x in points],label=label,color=color)
        axes[1].plot([x['S'] for x in controls],[x['Active'] for x in controls],color=color)
        axes[2].plot([x['S'] for x in controls],[x['Sweep'] for x in controls],color=color)
    for axis in axes:
        axis.spines[['top','right']].set_visible(False)
        for b in track['Bends']:
            axis.axvspan(b['Entry'],b['Exit'],color='#D4E7E4',alpha=.4)
        axis.grid(axis='y',alpha=.2)
    axes[0].set_ylabel('Speed (km/h)')
    axes[0].legend(frameon=False,loc='lower right',ncol=2)
    axes[0].axhline(110,linestyle=':',color='#B65C35',linewidth=1)
    axes[1].set_ylabel('Active throttle')
    axes[2].set(ylabel='Swept clearance (m)',xlabel='Track reference distance (m)',ylim=(.33,2.3))
    axes[2].axhline(.50,linestyle=':',color='#B65C35',linewidth=1)
    axes[2].axhline(.55,linestyle=':',color='#597C92',linewidth=1)
    fig.suptitle('Study #211   •   Nominal complete MODEL race',fontweight='bold')
    fig.savefig(fig_dir/'race-distance.png',dpi=155)
    plt.close(fig)


def main():
    p=argparse.ArgumentParser()
    p.add_argument('data',type=Path)
    p.add_argument('--plots',action='store_true')
    args=p.parse_args()
    data=args.data
    track=read(data/'reference/209/frozen-reference.json')['Track']
    reference=read(data/'reference/nominal-210.json.gz')
    prior=performance(*model_series(reference), reference['Finish'], track)
    controls=transition_rows(reference,track)
    write(data/'reference/transition-5m.json',controls)
    with (data/'reference/transition-5m.csv').open('w',encoding='utf-8',newline='') as file:
        writer=csv.writer(file)
        writer.writerow(('transition','exit_relative_m','speed_kph','v_out_mps','heading_yaw_rad_per_s',
                         'beta_rad','requested','active','ax_mps2','swept_m'))
        for r in controls:
            writer.writerow((r['Visit'],r['MetersAfterExit'],r['Speed'],r['Outward'],r['Yaw'],r['Beta'],
                             r['Request']['Drive'],r['Active']['Drive'],r['LongitudinalAcceleration'],r['Swept']))
    official = {}
    result = {'Reference':prior,'MODEL':{},'Native':{}}
    for path in sorted((data/'official').glob('*.json.gz')):
        run=read(path)
        if run['Complete']:
            official[run['Fixture']] = path
            result['MODEL'][run['Fixture']]=performance(*model_series(run),run['Finish'],track)
    for path in sorted((data/'native').glob('H30-C-*.json.gz')):
        archive=read(path)
        r=archive['Result']
        row=dict(Result=r, TotalCpuSeconds=archive['Run']['TotalCpuSeconds'],
                 ControllerCpuSeconds=archive['Run']['ControllerCpuSeconds'],
                 NativeAndObservationCpuSeconds=archive['Run']['NativeAndObservationCpuSeconds'],
                 Timings=[d['Milliseconds'] for d in archive['Decisions']])
        if r['Finished']:
            row.update(performance(*native_series(archive),r['Finish'],track))
            model=read(official[r['Fixture']])
            row['Divergence']=divergence(model,archive)['Max']
        result['Native'][r['Fixture']]=row
    write(data/'race-metrics.json', result)
    write(data/'terminal-envelopes.json',envelope((read(p) for p in official.values()),track))
    probes=read(data/'entry/entry-search.json.gz')
    write(data/'entry/entry-envelopes.json',entry_survey(probes))
    if args.plots:
        fig=data/'figures'
        fig.mkdir(exist_ok=True)
        race_figure(fig,track,reference,read(official['C']))
        root_figures(data,fig)


if __name__=='__main__':
    main()
