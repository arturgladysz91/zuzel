"""#124: read saved #122/#123, original h0 probe, and existing independent work ledger.

Never calls a native runner or a historical report's analyze()/save(). All new
files go to --output, which cannot be a historical capture directory.
"""
import argparse,csv,gzip,hashlib,importlib.util,json,math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
EXPERIMENTS=ROOT/'docs/experiments'
NEW=EXPERIMENTS/'speed-line-rider-root-cause'
BASE='c625de6faaeafb1d1ddb86f0598b61642c1452e5'
spec=importlib.util.spec_from_file_location('continuous',Path(__file__).with_name('continuous-corners-report.py'))
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
INPUTS={}

def load(directory,name):
    path=directory/name;INPUTS[path.relative_to(ROOT).as_posix()]=hashlib.sha256(path.read_bytes()).hexdigest()
    return json.loads(path.read_text(encoding='utf-8-sig'))

def read(directory,name):
    path=directory/(name+'.csv.gz');INPUTS[path.relative_to(ROOT).as_posix()]=hashlib.sha256(path.read_bytes()).hexdigest()
    return old.read(directory,name)

def intervals(rows,flags):
    result=[]
    for r,flag in zip(rows,flags):
        if not flag:continue
        end=r['t']+r['dt']
        if result and abs(result[-1][1]-r['t'])<1e-8:result[-1][1]=end
        else:result.append([r['t'],end])
    return result

def write_csv(path,records):
    with path.open('w',encoding='utf-8',newline='') as f:
        w=csv.DictWriter(f,fieldnames=list(records[0]));w.writeheader();w.writerows(records)

def analyze(output,plots):
    output=output.resolve()
    if output.parent==EXPERIMENTS and output!=NEW:raise ValueError('Historical output forbidden')
    output.mkdir(parents=True,exist_ok=True)
    d=EXPERIMENTS/'continuous-corner-planning';prior=EXPERIMENTS/'feasible-path-speed-planning'
    run=load(d,'continuous.json');rows=read(d,'continuous');paths=read(d,'continuous-plan')
    reviews=load(d,'reviews.json');analysis=load(d,'analysis.json');probe=load(output,'probe.json')
    baseline=read(prior,'C');baseline_reviews=load(prior,'C-reviews.json')
    assert rows[:len(baseline)]==baseline and reviews[:len(baseline_reviews)]==baseline_reviews
    assert probe['NewWorlds']==probe['BodySteps']==probe['WheelCommits']==0
    assert probe['MaximumSavedForceErrorNewtons']==0
    frozen=load(d,'protocol.json')['Sources']
    for name,digest in frozen.items():
        source=ROOT/'tools/shared-world-speedway'/name
        actual=hashlib.sha256(source.read_text(encoding='utf-8-sig').replace('\r\n','\n').encode()).hexdigest()
        assert actual.upper()==digest,(name,actual,digest)
        INPUTS[source.relative_to(ROOT).as_posix()]=actual
    final=run['Result']['Riders'][0]
    ends=rows[1:]+[dict(t=run['Result']['TimeSeconds'],speed=final['Speed'],vx=final['Velocity']['X'],vy=final['Velocity']['Y'],
        body_omega=final['AngularVelocity'],x=final['Position']['X'],y=final['Position']['Y'])]
    forces=[]
    for r,q in zip(rows,paths):
        required=147*r['speed']**2*q['path_k'];scale=147*r['speed']
        lo=q['min_chi_dot']*scale;hi=q['max_chi_dot']*scale
        forces.append(dict(t=r['t'],dt=r['dt'],s=r['s'],speed_kph=r['speed']*3.6,
            planned_normal=required,absolute_budget=q['front_capacity']+q['rear_capacity'],
            sampled_min_normal=lo,sampled_max_normal=hi,actual_normal=q['actual_normal'],
            controller_normal=q['desired_chi_dot']*scale,path_k=q['path_k'],actual_k=q['chi_dot']/r['speed'],
            rate_limited=q['rate_limited'],error=q['error'],normal_error=q['normal_error'],lateral_speed=q['lateral_speed'],
            absolute_exceeded=abs(required)>q['front_capacity']+q['rear_capacity']+1,
            sampled_exceeded=required<lo-1 or required>hi+1,
            controller_sampled_exceeded=q['unreachable']!=0))
    write_csv(output/'force-timeline.csv',forces)
    commands=run['Result']['Decisions'];observations=[x.copy() for x in probe['Reviews'] if x['Run']=='123']
    assert len(observations)==len(reviews)==200
    for obs,review in zip(observations,reviews):
        t=obs['Time'];assert t==review['Time']
        pending=next((c for c in commands if c['DecisionAt']<t-1e-8 and (c['ActualActivation'] is None or c['ActualActivation']>t+1e-8)),None)
        queued=next((c for c in commands if abs(c['DecisionAt']-t)<1e-8),None)
        upcoming=[q for q in review['Profile'] if q['S']<=review['Profile'][0]['S']+max(obs['Speed']*3,2)]
        limiting=min(upcoming,key=lambda q:q['SpeedLimit'])
        assert abs(limiting['SpeedLimit']-review['SpeedTarget'])<1e-8
        obs.update(SpeedTarget=review['SpeedTarget'],CoastNotCertified=review['NoCertifiedCoastSolution'],
            LimitingStation=limiting['S'],LimitingDistance=limiting['S']-review['Profile'][0]['S'],
            LimitingCurvature=limiting['Curvature'],LimitingResponse=limiting['ResponseCeiling'],
            HeldNormal=review['HeldSteeringNormal'],ImmediateNormal=review['ImmediateNormal'],
            ContactCeiling=review['ContactCeiling'],ResponseSeconds=review['ResponseSeconds'],
            PendingDrive=None if pending is None else pending['Drive'],PendingActivation=None if pending is None else pending['RequestedActivation'],
            Queued=queued is not None,QueuedDrive=None if queued is None else queued['Drive'],
            QueuedActivation=None if queued is None else queued['RequestedActivation'])
    write_csv(output/'decision-timeline.csv',observations)
    events=[]
    for command in commands:
        assert command['CancelledAt'] is None and abs(command['RequestedActivation']-command['DecisionAt']-.24)<1e-8
        if command['ActualActivation'] is None:continue
        t=command['ActualActivation'];assert abs(t-command['RequestedActivation'])<1e-8
        i=min(range(len(rows)),key=lambda i:abs(rows[i]['t']-t));r=rows[i];q=paths[i]
        assert abs(r['t']-t)<1e-8 and abs(r['drive']-command['Drive'])<1e-7
        recent=max((o for o in observations if o['Time']<=t+1e-8),key=lambda o:o['Time'])
        j=min(range(len(rows)),key=lambda j:abs(rows[j]['t']-(t+.24)))
        events.append(dict(Decision=command['DecisionAt'],Activation=t,Drive=command['Drive'],
            LatestReview=recent['Time'],LatestIntent=recent['RecordedDrive'],LatestSpinGate=recent['SpinGate'],LatestBetaGate=recent['BetaGate'],
            V=r['speed'],Wheelspin=r['wheelspin'],Beta=r['beta'],FiniteBetaDot=q['beta_dot'],Fx=r['rear_longitudinal'],
            DeltaVNext024=rows[j]['speed']-r['speed'],FollowupSeconds=rows[j]['t']-t))
    write_csv(output/'activation-timeline.csv',events)
    # Use already accepted endpoint work, never h0 power as the physical energy ledger.
    segments={}
    first=analysis['PerBend']['First'];second=analysis['PerBend']['Second']
    entry1=first['Landmarks']['Entry']['t'];middle1=first['Landmarks']['Middle']['t'];exit1=first['FullFootprintTime']
    entry2=second['Landmarks']['Entry']['t'];middle2=second['Landmarks']['Middle']['t'];exit2=second['FullFootprintTime']
    boundaries=[('Approach',0,entry1),('FirstEntryToMiddle',entry1,middle1),('FirstMiddleToExit',middle1,exit1),
        ('BetweenBends',exit1,entry2),('SecondEntryToMiddle',entry2,middle2),('SecondMiddleToExit',middle2,exit2)]
    for label,start,stop in boundaries:
        a=next(i for i,r in enumerate(rows) if r['t']>=start-1e-8)
        b=next((i for i,r in enumerate(rows) if r['t']>=stop-1e-8),len(rows))
        rr=rows[a:b];e=ends[b-1]
        import copy
        sub=copy.deepcopy(run);sub['Result']['Riders'][0]['Velocity']=dict(X=e['vx'],Y=e['vy']);sub['Result']['Riders'][0]['AngularVelocity']=e['body_omega']
        ledger=old.old.ledger.energy_from_new(rr,sub)
        impulse=old.old.physical.impulse_work(rr,sub)['MeasuredNativeImpulseErrorWorkJoules']
        assert abs(ledger['Residual']-impulse)<1e-6
        segments[label]=dict(Start=rr[0]['t'],End=e['t'],VStartKph=rr[0]['speed']*3.6,VEndKph=e['speed']*3.6,
            DistanceXY=sum(math.hypot(end['x']-r['x'],end['y']-r['y']) for r,end in zip(rr,ends[a:b])),
            MeanDrive=old.weighted(rr,[r['drive'] for r in rr]),ZeroDriveSeconds=sum(r['dt'] for r in rr if r['drive']==0),Energy=ledger)
    # Entire-run force windows are numerical sampled evidence, not a certified continuum bound.
    bounds={}
    for name,key in [('Absolute','absolute_exceeded'),('SampledPlan','sampled_exceeded'),('SampledController','controller_sampled_exceeded'),('SteeringRate','rate_limited')]:
        flags=[f[key] for f in forces]
        bounds[name]=dict(Seconds=sum(r['dt'] for r,flag in zip(rows,flags) if flag),Windows=intervals(rows,flags))
    sampled_points=[forces[min(range(len(rows)),key=lambda i:abs(rows[i]['t']-t))] for t in [1.6,2,2.4,3.4,4.3,5.4,6.42,9.9,13.2,14,16.6,19.9]]
    gate_stats={}
    for run_name in ['123','122A','122B']:
        oo=[o for o in probe['Reviews'] if o['Run']==run_name]
        gate_stats[run_name]=dict(Reviews=len(oo),Spin=sum(o['SpinGate'] for o in oo),Beta=sum(o['BetaGate'] for o in oo),
            Both=sum(o['SpinGate'] and o['BetaGate'] for o in oo),SpeedOnlyZero=sum(o['Request']==0 and not(o['SpinGate'] or o['BetaGate']) for o in oo),
            RampLimited=sum(o['RampLimited'] for o in oo))
    blocked=[o for o in observations if (o['SpinGate'] or o['BetaGate']) and o['PendingDrive'] is not None and o['PendingDrive']>0]
    stale=[e for e in events if e['Drive']>0 and (e['LatestSpinGate'] or e['LatestBetaGate'])]
    throttle=dict(Gates=gate_stats,Commands=len(commands),Activations=len(events),PendingReviews=sum(o['PendingDrive'] is not None for o in observations),
        BlockedSafetyReviews=blocked,PositiveActivationsWithLatestGate=stale,
        ActiveZeroSeconds=sum(r['dt'] for r in rows if r['drive']==0),ActiveMean=old.weighted(rows,[r['drive'] for r in rows]),
        ActivationReversals=old.flips([e['Drive'] for e in events]),
        MissingCertifiedCoast=sum(r['NoCertifiedCoastSolution'] for r in reviews))
    selected_reviews=[min(observations,key=lambda o:abs(o['Time']-t)) for t in [0,.1,.3,2,3.9,5.3,5.4,6.4,10.2,10.4,10.5,10.6,10.7,10.9,11,13.2,19.9]]
    result=dict(Base=BASE,NewWorlds=0,ContactQueries=probe['ContactQueries'],ForceReplayError=probe['MaximumSavedForceErrorNewtons'],
        PrefixRows=len(baseline),PrefixFingerprint=analysis['Prefix'],Bounds=bounds,SelectedStates=sampled_points,
        Throttle=throttle,SelectedReviews=selected_reviews,Segments=segments,Knots=probe['Knots'],Inputs=INPUTS,
        Interpretation='No causal speed counterfactual is identified. Sampled extrema are not certified continuous bounds. No empirical tyre calibration.')
    (output/'audit.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
    if plots:draw(output,rows,paths,forces,observations,segments,[entry1,exit1,entry2,exit2])
    print(json.dumps({k:result[k] for k in ['NewWorlds','ContactQueries','ForceReplayError','Bounds']},indent=2))
    print(json.dumps({k:v for k,v in throttle.items() if k not in ['BlockedSafetyReviews','PositiveActivationsWithLatestGate']},indent=2))

def draw(output,rows,paths,forces,observations,segments,landmarks):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    t=[r['t'] for r in rows];rt=[r['Time'] for r in observations]
    plt.rcParams.update({'font.size':10,'axes.grid':True,'grid.alpha':.22})
    fig,ax=plt.subplots(4,1,figsize=(12,11),sharex=True,layout='constrained')
    ax[0].plot(t,[r['speed']*3.6 for r in rows],label='V wykonana');ax[0].plot(rt,[min(100,r['SpeedTarget']*3.6) for r in observations],label='cel profilu (ucinany na 100)',alpha=.75);ax[0].set_ylabel('km/h');ax[0].legend(ncol=2)
    ax[1].step(t,[r['drive'] for r in rows],where='post',label='gaz aktywny');ax[1].step(rt,[r['RecordedDrive'] for r in observations],where='post',label='intencja co 0,1 s',alpha=.8)
    for key,color,label in [('SpinGate','tab:red','próg wheelspinu'),('BetaGate','tab:purple','próg ββdot')]:
        xs=[r['Time'] for r in observations if r[key]];ax[1].scatter(xs,[1.04 if key=='SpinGate' else 1.12]*len(xs),s=14,color=color,label=label)
    ax[1].set_ylabel('gaz / progi');ax[1].legend(ncol=2)
    ax[2].plot(t,[r['planned_normal'] for r in forces],label='mV²κ planu');ax[2].plot(t,[r['sampled_max_normal'] for r in forces],label='max skanu ±35°',alpha=.75)
    ax[2].plot(t,[r['actual_normal'] for r in forces],label='Fn wykonana',alpha=.75);ax[2].axhline(forces[0]['absolute_budget'],ls='--',c='black',label='absolutny budżet');ax[2].set_ylabel('N');ax[2].legend(ncol=2)
    ax[3].plot(t,[r['error'] for r in paths],label='błąd offsetu');ax[3].plot(t,[r['beta']*180/math.pi for r in rows],label='β [°]');ax[3].set_ylabel('m / °');ax[3].set_xlabel('czas [s]');ax[3].legend(ncol=2)
    for a in ax:
        for x in landmarks:a.axvline(x,c='grey',lw=.7,ls=':')
        a.axvspan(*landmarks[:2],color='tab:blue',alpha=.04);a.axvspan(*landmarks[2:],color='tab:orange',alpha=.05)
    fig.suptitle('#124 — istniejący przebieg #123; zero nowych światów');fig.savefig(output/'chronology.png',dpi=150);plt.close(fig)
    labels=['Dojazd','Łuk 1\nwejście–środek','Łuk 1\nśrodek–wyjście','Prosta','Łuk 2\nwejście–środek','Łuk 2\nśrodek–wyjście']
    values=list(segments.values());x=list(range(len(values)))
    fig,ax=plt.subplots(figsize=(12,5),layout='constrained');width=.16
    for j,(key,label) in enumerate([('Engine','praca silnika'),('FrontLoss','straty boczne przodu'),('RearLateralLoss','straty boczne tyłu'),('RearLongitudinalLoss','poślizg wzdłużny'),('ResistanceWork','opory (moduł)')]):
        ax.bar([i+(j-2)*width for i in x],[abs(s['Energy'][key])/1000 for s in values],width,label=label)
    ax.set_xticks(x,labels);ax.set_ylabel('kJ w zapisanym oknie');ax.set_title('Praca wykonanych sił — bez hipotetycznego doliczania niewykorzystanej mocy');ax.legend(ncol=3);fig.savefig(output/'energy.png',dpi=150);plt.close(fig)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,default=NEW);parser.add_argument('--plots',action='store_true');args=parser.parse_args();analyze(args.output,args.plots)
