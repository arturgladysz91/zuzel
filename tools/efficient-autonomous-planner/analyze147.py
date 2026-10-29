"""Read-only comparison of saved native traces, durable decisions and instrumented CPU partitions."""
import argparse,bisect,csv,gzip,hashlib,importlib.util,json,math,statistics,subprocess
from collections import defaultdict,Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];DOC=ROOT/'docs/experiments/efficient-autonomous-planner'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.name.endswith('.gz') else p.read_bytes())
def save(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def journal(p):
    data=p.read_bytes();end=data.rfind(b'\n');previous='GENESIS';rows=[]
    for index,line in enumerate(data[:end].splitlines()):
        d=json.loads(line);start=line.index(b'"Payload":')+10;stop=line.rfind(b',"Sha256":');raw=line[start:stop]
        h=hashlib.sha256(previous.encode()+raw).hexdigest().upper()
        assert d['Index']==index and d['Previous']==previous and d['Sha256']==h,'journal chain differs'
        previous=h;rows.append(d['Payload'])
    return rows

def main():
    spec=importlib.util.spec_from_file_location('a146',ROOT/'tools/autonomous-corner-planning/analyze146.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
    starts=[s['startReferenceMeters'] for s in read(ROOT/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json')['segments']]
    paths=[ROOT/'docs/experiments/standing-start-three-laps/native/K1.json.gz',ROOT/'docs/experiments/standing-start-three-laps/native/F.json.gz',ROOT/'docs/experiments/autonomous-corner-planning/native/autonomous146-viable.json.gz',DOC/'native/autonomous147-shortlist.json.gz']
    corners=[];runs=[];index=[];curves=[];lapmetrics=[];rawruns={}
    for path in paths:
        r=read(path);rawruns[r['Name']]=r;corners+=m.analyze(r,starts);rr=r['Riders'][0];costs=[d['CpuSeconds'] for d in rr['Decisions']]
        runs.append(dict(name=r['Name'],outcome=r['Outcome'],laps=rr['CompletedLaps'],finish_s=rr['FinishSeconds'],stop_s=r['EndSeconds'],fingerprint=r['Fingerprint'],controller_cpu_s=r['ControllerCpuSeconds'],native_and_observation_cpu_s=r['NativeAndObservationCpuSeconds'],total_world_cpu_s=r['TotalCpuSeconds'],forecasts=r['Forecasts'],forecast_contacts=r['ForecastContactQueries'],native_steps=r['NativeSteps'],native_contact_queries=r['NativeContactQueries'],native_clutch_wheel_queries=r['NativeClutchWheelQueries'],decision_cpu_median_s=statistics.median(costs),decision_cpu_p95_s=sorted(costs)[int((len(costs)-1)*.95)],decision_cpu_max_s=max(costs),decision_cpu_sum_s=sum(costs)))
        import os
        index.append(dict(Id=r['Name'],File=Path(os.path.relpath(path,DOC)).as_posix(),Variant=r['Variant'],Riders=1,Outcome=r['Outcome'],Duration=r['EndSeconds'],Fingerprint=r['Fingerprint']))
        lapmetrics.append(dict(Run=r['Name'],RiderId=rr['Spec']['Profile']['Id'],Laps=[dict(Lap=c['Lap'],LapSeconds=c['Time']-(r['Crossings'][k-1]['Time'] if k else 0)) for k,c in enumerate(r['Crossings'])]))
        curves.append(dict(name=r['Name'],points=[dict(t=s['Input']['Time'],reference=s['Input']['Rider']['TotalReferenceMeters'],x=s['Input']['Rider']['Position']['X'],y=s['Input']['Rider']['Position']['Y'],speed=s['Input']['Rider']['Speed']*3.6,beta=s['Input']['Rider']['SlideRadians']*180/math.pi,gas=s['Input']['Active']['Drive'],offset=s['Input']['Rider']['Road']['OffsetMeters']) for s in rr['Samples'][::4]]))
    baseline={(c['lap'],c['bend']):c for c in corners if c['run']=='K1'}
    for c in corners:c['delta_K1_s']=c['seconds']-baseline[(c['lap'],c['bend'])]['seconds'] if c['complete'] else None
    payload=journal(DOC/'native/autonomous147-shortlist-decisions.jsonl');ds=[x for x in payload if x['Event']=='decision'];new=rawruns['autonomous147-shortlist'];native=new['Riders'][0]['Decisions'];samples=new['Riders'][0]['Samples']
    assert len(ds)==len(native) and all(d['Selected']==n['Choice']['Action'] and abs(d['Time']-n['Choice']['Time'])<1e-8 for d,n in zip(ds,native))
    phases=defaultdict(float);operations=defaultdict(float)
    for d in ds:
        for x in d['Profile']['Phases']:phases[x['Name']]+=x['CpuSeconds']
        for x in d['Profile']['Operations']:operations[x['Name']]+=x['CpuSeconds']
    def table(values):
        total=sum(values.values());return [dict(name=k,cpu_s=v,percent=100*v/total if total else 0) for k,v in sorted(values.items(),key=lambda kv:-kv[1])]
    before=[read(DOC/'profiles/cold-profile.json')]+[json.loads(s) for s in (DOC/'profiles/replay-profiles.jsonl').read_text().splitlines()]
    oldph=defaultdict(float);oldop=defaultdict(float)
    for d in before:
        for x in d['Profile']['Phases']:oldph[x['Name']]+=x['CpuSeconds']
        for x in d['Profile']['Operations']:oldop[x['Name']]+=x['CpuSeconds']
    errors=[d['PredictionError'] for d in ds if d['PredictionError']]
    audit=dict(decisions=len(ds),native_decisions=len(native),complete=True,hash_chain=True,all_certified=all(d['Certificate']['Legal'] and d['Independent']['Legal'] for d in ds),
        active_queue_matches=all(d['ActiveMatches'] and d['QueueMatches'] for d in errors),
        maximum_prediction_errors={k:max(d[k] for d in errors) for k in ['PositionMeters','SpeedMps','BetaRadians','YawRate','WheelOmega','CrankOmega']},
        reasons=dict(Counter(d['Reason'] for d in ds)),dispositions=dict(Counter(d['Disposition'] for d in ds)),budget_fallbacks=sum(d['BudgetFallback'] is not None for d in ds),
        seed_requests=sum(d['SeedCalls'] for d in ds),prediction_cache_hits=sum(d['CacheHits'] for d in ds),planning_cpu_s=sum(d['PlanningCpuSeconds'] for d in ds),phases=table(phases),operations=table(operations))
    refs=[s['Input']['Rider']['TotalReferenceMeters'] for s in samples]
    def time_at(reference):
        k=bisect.bisect_left(refs,reference)
        if k==0 or k>=len(samples):return None
        a,b=samples[k-1]['Input'],samples[k]['Input'];return a['Time']+(b['Time']-a['Time'])*(reference-a['Rider']['TotalReferenceMeters'])/(b['Rider']['TotalReferenceMeters']-a['Rider']['TotalReferenceMeters'])
    followed=[]
    for d in ds:
        goal_time=d['GoalTime'];commands=[n for n in native if d['Time']-1e-8<=n['Choice']['Time'] and n['Choice']['Time']+.24<goal_time-1e-8]
        same=all((k:=round((n['Choice']['Time']-d['Time'])/.2))<len(d['PlannedActions']) and n['Choice']['Action']==d['PlannedActions'][k] for n in commands)
        actual=time_at(d['GoalMeters'])
        if same and actual is not None:followed.append(dict(time_s=d['Time'],predicted_goal_s=goal_time,native_goal_s=actual,error_s=actual-goal_time))
    audit['unmodified_executed_goal_forecasts']=followed;audit['maximum_unmodified_goal_error_s']=max((abs(x['error_s']) for x in followed),default=None)
    planrows=[]
    for d,n in zip(ds,native):
        activation=next((a for a in new['Riders'][0]['Activations'] if abs(a['ObservedAt']-d['Time'])<1e-8),None)
        planrows.append(dict(time_s=d['Time'],reason=d['Reason'],disposition=d['Disposition'],goal_m=d['GoalMeters'],candidate_count=None,cpu_s=d['PlanningCpuSeconds'],forecasts=d['ActualForecasts'],contacts=d['ActualContacts'],selected=d['PlannedActions'][:3],planned_goal_time_s=d['GoalTime'],planned_exit_time_s=d['CornerExitTime'],planned_exit_kph=d['CornerExitSpeed']*3.6 if d['CornerExitSpeed'] is not None else None,forecast_legal=d['Certificate']['Legal'],actual_action=n['Choice']['Action'],actual_activation=activation,next_change_reason=None,prediction_error=d['PredictionError'],predicted_trace=None))
    plan_audits={'autonomous147-shortlist':dict(reviews=len(ds),native_decisions=len(native),coverage_start_s=ds[0]['Time'],coverage_end_s=ds[-1]['Time'],followed_expected=sum(d['FollowedExpected'] for d in ds if d['ExpectedAction'] is not None),expected_reviews=sum(d['ExpectedAction'] is not None for d in ds),queues_match=audit['active_queue_matches'],maximum_error=audit['maximum_prediction_errors'],cpu_p50_s=statistics.median(d['PlanningCpuSeconds'] for d in ds),cpu_p95_s=sorted(d['PlanningCpuSeconds'] for d in ds)[int((len(ds)-1)*.95)],rows=planrows)}
    plan_audits['autonomous146-viable']=read(ROOT/'docs/experiments/autonomous-corner-planning/analysis.json')['plan_audits']['autonomous146-viable']
    comparisons=[read(f) for f in sorted((DOC/'comparison').glob('*.json'))];budget=read(DOC/'budget.json')
    save(DOC/'analysis.json',dict(native_worlds_created_by_analysis=0,runs=runs,corners=corners,journal_audit=audit,plan_audits=plan_audits,comparisons=comparisons,baseline_profile=dict(decisions=len(before),phases=table(oldph),operations=table(oldop)),budget=dict(total_cpu_s=sum(x['CpuSeconds'] for x in budget),native_worlds=sum(x['NativeWorlds'] for x in budget),attempts=budget),sources=[dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in paths],method=__doc__))
    with (DOC/'corners.csv').open('w',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(corners[0]));w.writeheader();w.writerows(corners)
    save(DOC/'index.json',dict(Title='#147 · Wydajny planner · pełne trzy okrążenia',DefaultRun='autonomous147-shortlist',GeometryFile='../causal-yaw-throttle/progressive-b-viewer-geometry.json',Runs=index,LapMeters=new['LapMeters'],RequiredLaps=3,StartReference=0,BendReferences=[starts[k] for k in [1,4,5,8]],Note='Cały natywny przejazd i wszystkie decyzje są zachowane. Techniczny PASS nie oznacza gotowości do ekonomicznej symulacji czterech zawodników.'))
    save(DOC/'lap-metrics.json',lapmetrics);(DOC/'comparison.json.gz').write_bytes(gzip.compress(json.dumps(curves,separators=(',',':')).encode(),mtime=0))
    print('finish',new['Riders'][0]['FinishSeconds'],'study CPU',sum(x['CpuSeconds'] for x in budget),'journal',len(ds),'forecast matched goals',len(followed),audit['maximum_unmodified_goal_error_s'])
    for c in corners:
        if c['run']=='autonomous147-shortlist':print(c['lap'],c['bend'],c['seconds'],c['minimum_kph'],c['exit_kph'],c['decision_cpu_s'])
if __name__=='__main__':main()
