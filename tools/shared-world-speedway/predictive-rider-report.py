"""Analyze only the new saved captures. Never creates a world or regenerates historical reports."""
import copy,csv,gzip,hashlib,importlib.util,json,math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/predictive-rider-control'
spec=importlib.util.spec_from_file_location('continuous',Path(__file__).with_name('continuous-corners-report.py'))
continuous=importlib.util.module_from_spec(spec);spec.loader.exec_module(continuous)
def load(path):
    if not path.exists() and path.with_suffix(path.suffix+'.gz').exists():path=path.with_suffix(path.suffix+'.gz')
    if path.suffix=='.gz':
        with gzip.open(path,'rt',encoding='utf-8') as f:return json.load(f)
    return json.loads(path.read_text(encoding='utf-8-sig'))
def rows(path):
    opener=gzip.open if path.with_suffix(path.suffix+'.gz').exists() else open
    if opener is gzip.open:path=path.with_suffix(path.suffix+'.gz')
    with opener(path,'rt',encoding='utf-8-sig',newline='') as f:return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(f)]
def wrap(a):return math.atan2(math.sin(a),math.cos(a))
def save(path,value):path.write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def weighted(rr,values):return sum(r['dt']*v for r,v in zip(rr,values))/sum(r['dt'] for r in rr)

geometry=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
starts=[s['startReferenceMeters'] for s in geometry['segments'] if s['type']=='TurnEntry'][:2]
exits=[geometry['segments'][i]['startReferenceMeters'] for i in [4,8]]
def plane_clear(r,s):
    segment=next(x for x in geometry['segments'] if abs(x['startReferenceMeters']-s)<1e-7)
    section=segment['sections'][0];ref=section['reference'];tx,ty=-section['outward']['y'],section['outward']['x'];c,z=math.cos(r['heading']),math.sin(r['heading'])
    def projection(point):
        x=r['x']+c*point['x']-z*point['y'];y=r['y']+z*point['x']+c*point['y']
        return (x-ref['x'])*tx+(y-ref['y'])*ty
    return min(min(projection(p['start']),projection(p['end']))-p['radiusMeters'] for p in geometry['capsules'])

def analyze_run(directory,name):
    run=load(directory/(name+'.json'));rr=rows(directory/(name+'.csv'));pp=rows(directory/(name+'-plan.csv'))
    final=run['Result']['Riders'][0];duration=run['Result']['TimeSeconds']
    end=dict(t=duration,s=final['TotalReferenceMeters'],x=final['Position']['X'],y=final['Position']['Y'],vx=final['Velocity']['X'],vy=final['Velocity']['Y'],
        heading=final['Heading'],speed=final['Speed'],body_omega=final['AngularVelocity'],steer_before=rr[-1]['steer_after'])
    ends=rr[1:]+[end];distances=[0]
    for r,e in zip(rr,ends):distances.append(distances[-1]+math.hypot(e['x']-r['x'],e['y']-r['y']))
    def landmark(s):
        i=next(i for i,(r,e) in enumerate(zip(rr,ends)) if r['s']<=s<=e['s']);r,e=rr[i],ends[i];u=(s-r['s'])/(e['s']-r['s'])
        return dict(Time=r['t']+u*(e['t']-r['t']),SpeedKph=3.6*(r['speed']+u*(e['speed']-r['speed'])),DistanceXY=distances[i]+u*(distances[i+1]-distances[i]))
    def conservative(key,aa,bb):return min(r[key]-(max(r['speed'],e['speed'])+1.05*max(abs(r['body_omega']),abs(e['body_omega'])))*r['dt']/2-.012 for r,e in zip(rr[aa:bb],ends[aa:bb]))
    bends={}
    for label,start,exit in zip(['First','Second'],starts,exits):
        full=next(e['t'] for e in ends if e['s']>=exit and plane_clear(e,exit)>0)
        a=next(i for i,r in enumerate(rr) if r['s']>=start);b=next((i for i,r in enumerate(rr) if r['t']>=full-1e-8),len(rr))
        subset=rr[a:b];plan=pp[a:b]
        bends[label]=dict(Entry=landmark(start),Middle=landmark((start+exit)/2),Exit=landmark(exit),FullFootprintTime=full,
            MinimumSpeedKph=min(r['speed'] for r in subset)*3.6,MaximumError=max(abs(p['error']) for p in plan),RmsError=math.sqrt(weighted(subset,[p['error']**2 for p in plan])),
            MaximumBetaDegrees=max(abs(r['beta']) for r in subset)*180/math.pi,ConservativeInner=conservative('inner',a,b),ConservativeOuter=conservative('outer',a,b))
    ledger=continuous.old.ledger.energy_from_new(rr,run);impulse=continuous.old.physical.impulse_work(rr,run)
    unexplained=ledger['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules'];assert abs(unexplained)<1e-6
    commands=run['Result']['Decisions'];reviews=load(directory/(name+'-reviews.json'))
    for c in commands:
        assert abs(c['RequestedActivation']-c['ObservedAt']-.24)<1e-8 and c['CancelledAt'] is None
    late=sum(c['ActualActivation'] is not None and c['ActualActivation']>c['RequestedActivation']+1e-8 for c in commands)
    stale=sum(c['Drive']>0 and c['ActualActivation'] is not None and any(c['DecisionAt']<q['Time']<c['ActualActivation']-1e-8 and q['Drive']==0 for q in reviews) for c in commands)
    for r,p in zip(rr,pp):
        assert abs(r['steer_after']-r['steer_before'])<=math.pi/2*r['dt']+1e-6
        assert abs(r['front_lateral'])<=p['front_capacity']+1e-3
        assert math.hypot(r['rear_lateral'],r['rear_longitudinal'])<=p['rear_capacity']+1e-3
    result=dict(Time=duration,Steps=len(rr),Fingerprint=run['Result']['Fingerprint'],Initial=run['Initial'],PerBend=bends,DistanceXY=distances[-1],
        ReferenceProgress=final['TotalReferenceMeters']-run['Initial']['Riders'][0]['TotalReferenceMeters'],MaximumError=max(abs(p['error']) for p in pp),
        RmsError=math.sqrt(weighted(rr,[p['error']**2 for p in pp])),MaximumBetaDegrees=max(abs(r['beta']) for r in rr)*180/math.pi,
        MeanDrive=weighted(rr,[r['drive'] for r in rr]),ZeroDriveSeconds=sum(r['dt'] for r in rr if r['drive']==0),
        ThrottleCommands=len(commands),PendingCommands=sum(c['ActualActivation'] is None for c in commands),LateCommands=late,PositiveAfterNewerZero=stale,
        SteeringReversals=continuous.flips([r['steer_after'] for r in rr]),ConservativeInner=conservative('inner',0,len(rr)),ConservativeOuter=conservative('outer',0,len(rr)),
        Energy=ledger,UnexplainedEnergyJoules=unexplained,NativeForceEvaluations=run['Result']['Work']['RearWheelForceEvaluations'],
        BoundaryForceEvaluations=run['Result']['Work']['RearWheelBoundaryEvaluations'],PreparedContactQueries=13*len(reviews),
        SolverMilliseconds=run['Result']['Work']['SolverElapsedMilliseconds'],PreparedMilliseconds=run['Result']['Work']['PreparedMilliseconds'],
        OriginalInversionContactQueries=51*sum(math.isfinite(p['min_chi_dot']) for p in pp))
    return result,rr,pp,run

def validate_selected_forecasts(decisions,rr):
    native={round(r['t'],6):r for r in rr};limits=load(DATA/'protocol.json')['PredictionTolerance'];records=[]
    for decision in decisions:
        if not decision['Candidates']:continue
        selected=next(c for c in decision['Candidates'] if c['CorrectionRadians']==decision['CorrectionRadians'])
        errors=dict(PositionMeters=0.,SpeedMetersPerSecond=0.,ChiDegrees=0.,BetaDegrees=0.,SteeringDegrees=0.,WheelOmegaRadPerSecond=0.)
        count=0
        for point in selected['Prediction']['Points']:
            r=native.get(round(point['Time'],6))
            if not r or abs(point['Time']-r['t'])>1e-7:continue
            v=point['Rider'];x,y=v['Position']['X'],v['Position']['Y'];vx,vy=v['Velocity']['X'],v['Velocity']['Y'];chi=math.atan2(vy,vx)
            errors['PositionMeters']=max(errors['PositionMeters'],math.hypot(x-r['x'],y-r['y']))
            errors['SpeedMetersPerSecond']=max(errors['SpeedMetersPerSecond'],abs(math.hypot(vx,vy)-r['speed']))
            errors['ChiDegrees']=max(errors['ChiDegrees'],abs(wrap(chi-math.atan2(r['vy'],r['vx'])))*180/math.pi)
            errors['BetaDegrees']=max(errors['BetaDegrees'],abs(wrap(v['Heading']-chi-r['beta']))*180/math.pi)
            errors['SteeringDegrees']=max(errors['SteeringDegrees'],abs(point['Steering']-r['steer_before'])*180/math.pi)
            errors['WheelOmegaRadPerSecond']=max(errors['WheelOmegaRadPerSecond'],abs(point['Omega']-r['omega_before']));count+=1
        records.append(dict(ObservedAt=decision['ObservedAt'],Queued=decision['Queued'],ComparedStates=count,Errors=errors,Passed=all(v<=limits[k] for k,v in errors.items())))
    maxima={k:max(r['Errors'][k] for r in records) for k in records[0]['Errors']}
    return dict(Passed=all(r['Passed'] for r in records),MaximumErrors=maxima,Records=records)

def main():
    baseline,br,bp,bj=analyze_run(DATA,'baseline');candidate,cr,cp,cj=analyze_run(DATA/'controlled','candidate')
    assert baseline['Initial']==candidate['Initial']
    original=load(DATA/'../continuous-corner-planning/continuous.json');assert original['Result']['Fingerprint']==baseline['Fingerprint']
    bc=load(DATA/'baseline-changes.json');cc=load(DATA/'controlled/candidate-changes.json')
    shared=all(a[k]==b[k] for a,b in zip(bc,cc) for k in ['Phase','Time','Activation','Splice','EndTransition','Offset','BendStart','BendExit'])
    assert shared
    decisions=load(DATA/'controlled/candidate-predictions.json.gz');activations=load(DATA/'controlled/candidate-steering-activations.json')
    selected=validate_selected_forecasts(decisions,cr)
    for activation in activations:
        assert any(d['Queued'] and abs(d['Activation']-activation['Time'])<1e-8 and d['CorrectionRadians']==activation['CorrectionRadians'] for d in decisions)
        assert all(abs(d['Activation']-d['ObservedAt']-.24)<1e-8 for d in decisions)
    forecast_queries=sum(c['Prediction']['ContactQueries'] for d in decisions for c in d['Candidates'])
    cpu=[d['Milliseconds'] for d in decisions if d['Candidates']]
    candidate.update(PredictiveDecisions=len(decisions),PredictiveEvaluations=len(cpu),PredictiveQueued=sum(d['Queued'] for d in decisions),PredictiveActivations=len(activations),
        PredictiveContactQueries=forecast_queries,PredictiveMilliseconds=sum(cpu),PredictiveMedianMilliseconds=sorted(cpu)[len(cpu)//2],PredictiveMaximumMilliseconds=max(cpu))
    safety=candidate['ConservativeInner']>0 and candidate['ConservativeOuter']>0 and candidate['MaximumBetaDegrees']<45 and cj['Energy']['MaximumExcessBeyondEngineJoules']==0
    grade='FAIL' if not safety or not selected['Passed'] else 'PASS' if candidate['Time']<baseline['Time'] and candidate['MaximumBetaDegrees']<=baseline['MaximumBetaDegrees'] and candidate['RmsError']<=baseline['RmsError'] else 'PARTIAL'
    outcome=dict(Grade=grade,SharedPreparedLine=shared,NativeSafetyPassed=safety,PredictionBaselineGate=load(DATA/'prediction-validation.json')['Passed'],
        SelectedPredictionAgainstRealCandidate=selected,Baseline=baseline,Candidate=candidate,ExcludedUncontrolledPlanRun=load(DATA/'candidate.json')['Result']['Fingerprint'],
        NativeWorlds=3,Interpretation='First ON run excluded because dynamic planning changed Q2. One fixed-input repeat, same controller/model/horizon/tolerances, no retuning.')
    save(DATA/'analysis.json',outcome)
    events=[]
    for d in decisions:
        if not d['Queued']:continue
        actual=next((a for a in activations if abs(a['Time']-d['Activation'])<1e-8),None)
        events.append(dict(ObservedAt=d['ObservedAt'],RequestedActivation=d['Activation'],ActualActivation=None if actual is None else actual['Time'],CorrectionRadians=d['CorrectionRadians'],
            TargetRadians=None if actual is None else actual['TargetRadians'],Reason=d['Reason']))
    save(DATA/'controlled/candidate-steering-events.json',events)
    print(json.dumps({k:outcome[k] for k in ['Grade','SharedPreparedLine','NativeSafetyPassed','NativeWorlds']},indent=2))
    for n,r in [('baseline',baseline),('candidate',candidate)]:print(n,json.dumps({k:r[k] for k in ['Time','DistanceXY','ReferenceProgress','MaximumError','RmsError','MaximumBetaDegrees','MeanDrive','ZeroDriveSeconds','ConservativeInner','ConservativeOuter']}))
    print('selected-prediction maxima',selected['MaximumErrors'])
    # Compact only newly generated files, with verified byte-for-byte round trips.
    for path in [*DATA.glob('*.csv'),*DATA.glob('*-reviews.json'),DATA/'prediction-trajectories.json',*(DATA/'controlled').glob('*.csv'),*(DATA/'controlled').glob('*-reviews.json')]:
        if not path.exists():continue
        content=path.read_bytes();compressed=gzip.compress(content,mtime=0);assert gzip.decompress(compressed)==content
        path.with_suffix(path.suffix+'.gz').write_bytes(compressed);path.unlink()

if __name__=='__main__':main()
