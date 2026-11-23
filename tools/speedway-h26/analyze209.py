"""Rebuild Study #209 metrics and static figures from accepted H28-F steps."""
import collections, gzip, json, math, pathlib, statistics, sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

root = pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.')
out = root/'data/experiments/speedway209'
fig = out/'figures'; fig.mkdir(exist_ok=True)
def read(path):
    return json.load(gzip.open(path,'rt') if str(path).endswith('.gz') else open(path,encoding='utf-8-sig'))
def save(name, value):
    (out/name).write_text(json.dumps(value,indent=2,allow_nan=False),encoding='utf-8')
def summarize(run):
    rows=run['Trace']; duration=sum(x['After']['Time']-x['Before']['State']['Time'] for x in rows)
    clamp=collections.defaultdict(lambda: [0.,0.,0]); bands=collections.defaultdict(list)
    requests={}; observed_reductions=[]; zero_requested=0; over100=[]; start100=None
    forces=collections.defaultdict(float); max_force_error=0
    for i,x in enumerate(rows):
        dt=x['After']['Time']-x['Before']['State']['Time']; command=x['Command']; s=x['Before']['State']['Rider']
        v=s['Speed']; a=x['Acceleration']; bands[int(v*3.6//10)*10].append(a)
        if command:
            gas=command['Selected']['Drive']; zero_requested+=dt*(gas<=1e-6)
            requests[x['RequestedAt']]=(gas,x)
            clamp[command['Dominant']][0]+=dt
            for stage in command['Stages']:
                clamp[stage['Name']][1]+=dt*stage['Reduction']
                clamp[stage['Name']][2]+=int(stage['Reduction']>1e-9 and abs(x['RequestedAt']-s.get('Time',x['Before']['State']['Time']))<1e-8)
        if a<0:
            for key in ['ResistanceAcceleration','FrontAcceleration','RearAcceleration']:
                forces[key]+=x[key]*dt
        max_force_error=max(max_force_error,abs(x['ForceAcceleration']-x['ResistanceAcceleration']-x['FrontAcceleration']-x['RearAcceleration']))
        is100=x['After']['Speed']*3.6>=100 and not s['Road']['IsTurn']
        if is100 and start100 is None: start100=i
        if start100 is not None and (not is100 or i==len(rows)-1):
            end=i if is100 else i-1; first=rows[start100]; last=rows[end]
            over100.append({'From':first['Before']['State']['Time'],'To':last['After']['Time'],
                'Seconds':last['After']['Time']-first['Before']['State']['Time'],
                'Meters':last['After']['S']-first['Before']['State']['Rider']['TotalReferenceMeters'],
                'MinimumKph':min(z['After']['Speed']*3.6 for z in rows[start100:end+1])})
            start100=None
    previous=None
    for t,(gas,row) in sorted(requests.items()):
        if previous and gas<previous[1]-.05:
            subsequent=[x for x in rows if t<=x['Before']['State']['Time']<t+.5]
            if subsequent:
                observed_reductions.append({'Time':t,'Drop':previous[1]-gas,
                    'Dominant':row['Command']['Dominant'],'MetersInNextHalfSecond':subsequent[-1]['After']['S']-row['Before']['State']['Rider']['TotalReferenceMeters'],
                    'SpeedChangeInNextHalfSecond':subsequent[-1]['After']['Speed']-row['Before']['State']['Rider']['Speed']})
        previous=t,gas
    # Actual activation is read from the accepted pending queue, never inferred from a throttle change.
    activations={}
    for x in rows:
        for p in x['Before']['Pending']:
            activations[(p['ObservedAt'],p['ActivationAt'])]=p['ActivationAt']-p['ObservedAt']
    delays=list(activations.values())
    return {'Finish':run['Finish'],'PeakKph':run['PeakKph'],'Sweep':run['Sweep'],'Duration':duration,
        'RequestedFullPct':100*run['RequestedFullSeconds']/duration,'ActiveFullPct':100*run['ActiveFullSeconds']/duration,
        'ActiveZeroSeconds':run['ZeroSeconds'],'RequestedZeroSeconds':zero_requested,
        'AcceleratingSeconds':run['AcceleratingSeconds'],'DeceleratingSeconds':run['DeceleratingSeconds'],
        'RequestDropsOverOnePct':run['Reductions'],'Clamps':dict(clamp),'ForceClosureError':max_force_error,
        'DecelerationImpulsePerMass':dict(forces),'AccelerationBy10Kph':{str(k):{'Maximum':max(v),'Minimum':min(v)} for k,v in bands.items()},
        'RequestDropsOverFivePct':observed_reductions,'StraightOver100':over100,
        'ActivationDelay':{'Count':len(delays),'Minimum':min(delays),'Maximum':max(delays),'Median':statistics.median(delays)}}

baseline=read(out/'baseline/C.json.gz'); final=read(out/'official/C.json.gz')
save('telemetry-summary.json',{'Baseline':summarize(baseline),'Final':summarize(final),
    'Attribution':'Clamp reductions are ordered command-space contributions. Observed speed changes after reductions are associations; only paired legal rollouts support causal time gains.',
    'BrakeCommand':False})
plt.rcParams.update({'font.size':10,'axes.grid':True,'grid.alpha':.18,'figure.facecolor':'white'})
f,axes=plt.subplots(4,1,figsize=(13,10),sharex=True)
for run,color,label in [(baseline,'#697888','#208 frozen'),(final,'#007f75','#209 frozen')]:
    rows=run['Trace']; t=[x['After']['Time'] for x in rows]
    axes[0].plot(t,[x['After']['Speed']*3.6 for x in rows],color=color,label=label,lw=1.2)
    axes[1].plot(t,[x['Command']['Selected']['Drive'] for x in rows],color=color,label=label+' requested',lw=.9)
    if run is final: axes[1].plot(t,[x['Before']['Active']['Drive'] for x in rows],color='#ca6b00',label='#209 active',lw=.6,alpha=.75)
    axes[2].plot(t,[x['Acceleration'] for x in rows],color=color,lw=.8)
    axes[3].plot(t,[x['After']['Sweep'] for x in rows],color=color,lw=.8)
axes[0].axhline(100,color='#b65746',ls=':',lw=.8); axes[3].axhline(.5,color='#b65746',ls='--',lw=.8)
for ax,y in zip(axes,['Speed [km/h]','Throttle [0–1]','Acceleration [m/s²]','Swept [m]']): ax.set_ylabel(y)
axes[3].set_ylim(0,8);axes[0].legend(loc='upper left');axes[1].legend(loc='lower left',ncols=3,fontsize=8)
for p in baseline['Passages']:
    if p['Mark']=='entry':
        axes[0].text(p['Time'],110,f"B{p['Visit']+1}",ha='center',fontsize=9)
        for ax in axes: ax.axvline(p['Time'],color='#aaa',lw=.5,ls=':')
axes[-1].set_xlabel('Elapsed physical time [s]');f.suptitle('Same H28-F physics: throttle, speed, acceleration and legal clearance')
f.tight_layout();f.savefig(fig/'race-timeline.png',dpi=160);plt.close(f)

f,axes=plt.subplots(3,1,figsize=(13,8),sharex=True)
for run,color,label in [(baseline,'#697888','#208 frozen'),(final,'#007f75','#209 frozen')]:
    rows=run['Trace']; s=[x['After']['S'] for x in rows]
    axes[0].plot(s,[x['After']['Speed']*3.6 for x in rows],color=color,label=label,lw=1.1)
    axes[1].plot(s,[x['Before']['Active']['Drive'] for x in rows],color=color,lw=.8)
    axes[2].plot(s,[x['Acceleration'] for x in rows],color=color,lw=.8)
for visit in range(6):
    entry=next(p['S'] for p in baseline['Passages'] if p['Visit']==visit and p['Mark']=='entry')
    exit=next(p['S'] for p in baseline['Passages'] if p['Visit']==visit and p['Mark']=='exit')
    for ax in axes: ax.axvspan(entry,exit,color='#dfe8ed',alpha=.4,zorder=0)
    axes[0].text((entry+exit)/2,112,f'Bend {visit+1}',ha='center',fontsize=9)
for ax,y in zip(axes,['Speed [km/h]','Active throttle [0–1]','Acceleration [m/s²]']): ax.set_ylabel(y)
axes[0].legend();axes[0].axhline(100,color='#b65746',ls=':',lw=.8)
axes[-1].set_xlabel('Track reference distance S [m]');f.suptitle('All six bends and connecting straights on a common distance axis')
f.tight_layout();f.savefig(fig/'six-bends.png',dpi=160);plt.close(f)

capability=read(out/'diagnostics/capability.json.gz')
full=[x for x in capability['Straights'] if x['Gas']==1]
f,ax=plt.subplots(figsize=(10,5))
for q in full:
    rows=q['Run']['Trace']; start=q['Initial']['State']['Rider']['TotalReferenceMeters']
    ax.plot([x['After']['S']-start for x in rows],[x['After']['Speed']*3.6 for x in rows],label=f"After bend {q['Visit']+1}")
ax.axhline(100,color='#b65746',ls=':');ax.set(xlabel='Distance on the original straight [m]',ylabel='Speed [km/h]',
    title='Controlled full-throttle capability; following-corner completion is not certified')
ax.legend(ncols=2);f.tight_layout();f.savefig(fig/'straight-capability.png',dpi=160);plt.close(f)

native=[]; pooled=[]; differences=[]
for fixture in ['C','C-hold-minus','C-hold-plus','C-alt','C-alt-plus']:
    a=read(out/f'native/H29-S-{fixture}.json.gz'); m=read(out/f'official/{fixture}.json.gz')
    r=a['Run']['Riders'][0]; times=[x['Milliseconds'] for x in a['Decisions']]; pooled+=times
    peak=max(x['Input']['Rider']['Speed']*3.6 for x in r['Samples'])
    native.append({**a['Result'],'PeakKph':peak})
    model={round(x['Before']['State']['Time'],7):x['Before']['State']['Rider'] for x in m['Trace']}
    xy=[]; speeds=[]
    for x in a['Decisions']:
        k=round(x['State']['State']['Time'],7)
        if k in model:
            v=x['State']['State']['Rider']; w=model[k]
            xy.append(math.hypot(v['Position']['X']-w['Position']['X'],v['Position']['Y']-w['Position']['Y']))
            speeds.append(abs(v['Speed']-w['Speed']))
    differences.append({'Fixture':fixture,'Matched':len(xy),'MaximumXYMeters':max(xy),'MaximumSpeedMps':max(speeds),
        'NativeMinusModelSeconds':a['Result']['Finish']-m['Finish']})
pooled.sort()
save('native-summary.json',{'Runs':native,'Timing':{'Count':len(pooled),'P50':pooled[int((len(pooled)-1)*.5)],
    'P95':pooled[int((len(pooled)-1)*.95)],'P99':pooled[int((len(pooled)-1)*.99)],'Maximum':max(pooled),
    'Over50':sum(x>50 for x in pooled)},'ModelDifferences':differences})
safe=[x for x in capability['Trials'] if x['Gas']=='1.00' and x['Continuation']['Complete']]
save('capability-summary.json',{'Cpu':capability['Cpu'],'Wall':capability['Wall'],
    'SafeQuarterSecondFullThrottleContinuations':[{'Anchor':x['Anchor'],'Finish':x['Continuation']['Finish'],
        'GainSeconds':baseline['Finish']-x['Continuation']['Finish']} for x in safe],
    'OriginalStraights':[{'Visit':x['Visit'],'Gas':x['Gas'],'Outcome':x['Run']['Stop'],'PeakKph':x['Run']['PeakKph'],
        'Distance':x['Run']['LastLegalS']-x['Initial']['State']['Rider']['TotalReferenceMeters'],
        'InitialKph':x['Initial']['State']['Rider']['Speed']*3.6,
        'MeanAcceleration':statistics.mean(z['Acceleration'] for z in x['Run']['Trace'])} for x in capability['Straights']]})
print('Wrote telemetry, native and capability summaries plus two figures.')
