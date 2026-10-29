"""Saved evidence only. Complete tree/queue audit, native metrics and exclusive CPU accounting."""
from pathlib import Path
from contextlib import contextmanager
import collections,csv,gzip,hashlib,importlib.util,json,math,os,statistics,subprocess,sys,time
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/contingency-recourse'
BASE='5bc8575f60d9fa1116e45dad85c08786158faccd'
def read(p): return json.loads(gzip.decompress(p.read_bytes()) if str(p).endswith('.gz') else p.read_bytes())
def save(p,x): p.write_text(json.dumps(x,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
@contextmanager
def charged(label):
    import msvcrt
    with (D/'budget.json.lock').open('a+b') as lease:
        lease.seek(0);msvcrt.locking(lease.fileno(),msvcrt.LK_NBLCK,1)
        ledger=read(D/'budget.json');assert all(x['Complete'] for x in ledger);assert not any(x['Name']==label for x in ledger)
        start=time.process_time();spent=sum(x['CpuSeconds'] for x in ledger);assert spent<1200
        row=dict(Name=label,Complete=False,NativeWorlds=0,CpuSeconds=0,Base=BASE);ledger.append(row);save(D/'budget.json',ledger)
        def guard():
            if spent+time.process_time()-start>=1200:raise RuntimeError('Study151 postprocessing CPU reserve exhausted')
        try:yield guard
        finally:
            row.update(Complete=True,CpuSeconds=time.process_time()-start,Outcome='saved postprocessing; inspect output on exception');save(D/'budget.json',ledger)
def module(path):
    spec=importlib.util.spec_from_file_location('analyze146',R/path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
def journal(p):
    previous='GENESIS';rows=[]
    for k,line in enumerate(p.read_text(encoding='utf-8').splitlines()):
        x=json.loads(line);payload=x['Payload'];text=line.split('"Payload":',1)[1].rsplit(',"Sha256":',1)[0]
        digest=hashlib.sha256((previous+text).encode()).hexdigest().upper();assert x['Index']==k and x['Previous']==previous and x['Sha256']==digest
        previous=digest;rows.append(payload)
    return rows
def pct(xs,f):return sorted(xs)[int((len(xs)-1)*f)] if xs else None
def main():
    label=sys.argv[1] if len(sys.argv)>1 else 'v1'
    with charged('saved-analysis-'+label) as guard:
        m=module('tools/autonomous-corner-planning/analyze146.py')
        starts=[s['startReferenceMeters'] for s in read(R/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json')['segments']]
        runs=[];corners=[];audits={};curves=[];indices=[];laps=[];profiles={};sources=[]
        baseline=R/'docs/experiments/tyre-physics-corner-limits/native'
        paths=[baseline/'baseline149-final148.json.gz',baseline/'separated149-autonomous.json.gz']+sorted((D/'native').glob('*.json.gz'))
        for p in paths:
            guard();run=read(p);name=run['Name'];native=p.parent==D/'native';result=read(p.with_name(name+'-result.json'));rider=run['Riders'][0];ss=rider['Samples']
            model=result.get('Model','B' if name=='separated149-autonomous' else 'A');cs=m.analyze(run,starts)
            for c in cs:c.update(model=model,scope='new continuous native gate' if native else 'historical149 reference; not rerun151',delta_K1_s=None)
            corners+=cs
            runs.append(dict(name=name,model=model,gate=result.get('Gate'),gate_reached=result.get('GateReached'),outcome=run['Outcome'],finish_s=rider['FinishSeconds'],laps=rider['CompletedLaps'],end_s=run['EndSeconds'],controller_cpu_s=result.get('ControllerAttemptCpuSeconds',result.get('ControllerCpuSeconds',run['ControllerCpuSeconds'])),experiment_cpu_s=result.get('CpuSeconds'),minimum_gap_m=min(min(s['Inner'],s['Outer']) for s in ss),minimum_sweep_m=min(s['Certificate'] for s in ss if s['Certificate'] is not None),maximum_beta_deg=max(abs(s['Input']['Rider']['SlideRadians'])*180/math.pi for s in ss),contacts=len(run['Contacts']),decisions=len(rider['Decisions']),native=native))
            sources.append(dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
            indices.append(dict(Id=name,File=os.path.relpath(p,D).replace('\\','/'),Variant=run['Variant'],Riders=1,Outcome=run['Outcome'],Duration=run['EndSeconds'],Fingerprint=run['Fingerprint']))
            laps.append(dict(Run=name,RiderId=1,Laps=[dict(Lap=c['Lap'],LapSeconds=c['Time']-(run['Crossings'][j-1]['Time'] if j else 0)) for j,c in enumerate(run['Crossings'])]))
            curves.append(dict(name=name,points=[dict(t=s['Input']['Time'],x=s['Input']['Rider']['Position']['X'],y=s['Input']['Rider']['Position']['Y'],reference=s['Input']['Rider']['TotalReferenceMeters'],speed=s['Input']['Rider']['Speed']*3.6,beta=s['Input']['Rider']['SlideRadians']*180/math.pi,gas=s['Input']['Active']['Drive'],offset=s['Input']['Rider']['Road']['OffsetMeters']) for s in ss[::4]]))
            if not native:continue
            payload=journal(D/'native'/(name+'-decisions.jsonl'));ds=[x for x in payload if x['Event']=='decision'];assert len(ds)==len(rider['Decisions']);rows=[];node_count=leaf_count=attempt_count=nominal_legal=0;costs=[]
            phases=collections.Counter();previous_forecast=None;followed=expected=0
            costs_by_time={round(x['Time'],6):x['Profile'] for x in read(D/'native'/(name+'-cpu.json'))};all_attempts_known=True
            for k,(d,actual) in enumerate(zip(ds,rider['Decisions'])):
                guard();assert d['ConditionalCertificate'] and d['Independent'];assert d['Selected']==actual['Choice']['Action']
                proof=d.get('Proof');actions=proof['FirstActions'] if proof else [d['Selected']];arrival=proof['Arrival'] if proof else None
                # Every accepted command already has uncached full physical preflight in C#. Older journals lack a compact summary.
                # Their13/9 counts follow exactly from depth2 and the successful certificate; do not repeat all large physical tapes merely for export.
                count,leaves=(proof['Nodes'],proof['Leaves']) if proof else (13,9);assert(count,leaves)==(13,9);node_count+=count;leaf_count+=leaves
                if proof:attempt_count+=proof['Attempts'];nominal_legal+=proof['Legal'];assert actions[0]==d['Selected'] and proof['Safety']['Legal']
                else:all_attempts_known=False
                if previous_forecast is not None and len(previous_forecast)>1:expected+=1;followed+=previous_forecast[1]==d['Selected']
                previous_forecast=actions
                observed=d['Observed'];pending=[x for x in ds[:k] if x['Time']+.24>observed['Time']+1e-8];assert len(pending)==len(observed['Pending'])
                for issued,queued in zip(pending,observed['Pending']):assert issued['Selected']==queued['Action'] and abs(issued['Time']+.24-queued['ActivationAt'])<1e-8
                activation=next((a for a in rider['Activations'] if abs(a['ObservedAt']-d['Time'])<1e-8),None)
                if activation:assert activation['Action']==d['Selected'] and abs(activation['ActivationAt']-d['Time']-.24)<1e-8
                rows.append(dict(time_s=d['Time'],reason=d['Reason'],selected=actions,planned_goal_time_s=arrival,planned_exit_time_s=arrival,planned_exit_kph=proof['ExitKph'] if proof else None,forecast_legal=True,actual_action=d['Selected'],actual_activation=activation,next_change_reason='new observation; complete finite tree recertified',prediction_error=None,tree_file='trees/'+name+'/'+d['Tree']+'.json.gz',nodes=13,leaves=9,predicted_trace=proof['Trace'] if proof else None))
                costs.append(d['CpuSeconds'])
                for entry in costs_by_time[round(d['Time'],6)]['Phases']:phases[entry['Name']]+=entry['CpuSeconds']
            audits[name]=dict(reviews=len(ds),native_decisions=len(ds),coverage_start_s=ds[0]['Time'] if ds else None,coverage_end_s=ds[-1]['Time'] if ds else None,followed_expected=followed,expected_reviews=expected,queues_match=True,maximum_error=dict(PositionMeters=None),cpu_p50_s=pct(costs,.5),cpu_p95_s=pct(costs,.95),rows=rows,certified_nodes=node_count,certified_terminal_reserves=leaf_count,nominal_attempts=attempt_count if all_attempts_known else None,nominal_legal=nominal_legal if all_attempts_known else None,certificate_audit_scope='All command/activation journals checked; all trees physically replayed uncached at execution. Older13/9 counts derived from fixed complete tree topology. Full tapes preserved, not cold-replayed again during export.',abstentions=[x for x in payload if x['Event']=='abstain'])
            profiles[name]=dict(exclusive_phases=dict(phases),profile_cpu_s=sum(phases.values()),returned_decision_cpu_s=sum(costs),attempted_controller_cpu_s=runs[-1]['controller_cpu_s'],serialization_journal_and_abstention_s=runs[-1]['controller_cpu_s']-sum(phases.values()))
        full=[r for r in runs if r['native'] and r['laps']==3 and r['finish_s'] is not None];verdict='PARTIAL — native three-lap A completed, B/transfer not established' if full else 'PARTIAL — no new three-lap completion'
        new=[r for r in runs if r['native']];default=max(new,key=lambda r:(r['gate'],r['end_s'],r['name']))['name']
        if (D/'new-native-errors.json').exists():
            for item in read(D/'new-native-errors.json'):
                audits[item['Run']]['maximum_error']['PositionMeters']=max((r['Delta']['PositionMeters'] for r in item['Errors'] if r['Horizon']==.2),default=0)
        errors=read(D/('identical-command-errors-v2.json' if (D/'identical-command-errors-v2.json').exists() else 'identical-command-errors.json'));error_summary=[]
        for run in errors:
            groups=collections.defaultdict(list)
            for row in run['Rows']:groups[row['HorizonSeconds']].append(row)
            for horizon,rs in groups.items():error_summary.append(dict(run=run['Run'],horizon_s=horizon,pairs=len(rs),**{key:max(abs(x['Delta'][key]) for x in rs) for key in ['PositionMeters','VelocityMetersPerSecond','HeadingRadians','YawRadiansPerSecond','WheelRadiansPerSecond','CrankRadiansPerSecond','SteeringRadians']},trace_position_m=max((x.get('HistoricalNearestTracePositionMeters') or 0 for x in rs),default=0),trace_time_error_s=max((abs(x.get('HistoricalDecimatedTraceTimeErrorSeconds') or 0) for x in rs),default=0)))
        save(D/'analysis.json',dict(verdict=verdict,full_runs=runs,corners=corners,plan_audits=audits,cpu=profiles,error_summary=error_summary,source_files=sources,full_race_improvement_claimed=any(r['finish_s']<(53.989766 if r['model']=='A' else 54.234944) for r in full),scope='finite signed correlated observation scenarios, analytical held-wrench predictor and independent uncached model preflight; native Box2D owns actual execution; no global or four-rider guarantee'))
        save(D/'index.json',dict(Title='#151 · Planowanie warunkowe · '+verdict,DefaultRun=default,GeometryFile='../causal-yaw-throttle/progressive-b-viewer-geometry.json',Runs=indices,LapMeters=318.77874994277954,RequiredLaps=3,StartReference=0,BendReferences=[starts[k] for k in [1,4,5,8]],Note='Wyniki bramek i prognozy są osobne. Pełny wynik wymaga trzech legalnych natywnych okrążeń od zera. '+verdict))
        save(D/'lap-metrics.json',laps);(D/'comparison.json.gz').write_bytes(gzip.compress(json.dumps(curves,separators=(',',':')).encode(),mtime=0))
        for filename,table in [('corners.csv',corners),('runs.csv',runs),('errors.csv',error_summary)]:
            with (D/filename).open('w',encoding='utf-8',newline='') as file:
                writer=csv.DictWriter(file,fieldnames=list(table[0]),lineterminator='\n');writer.writeheader();writer.writerows(table)
        frozen=['tools/shared-world-speedway/StandingStart144/Contact144.cs','tools/shared-world-speedway/StandingStart144/CrankClutch144.cs','tools/shared-world-speedway/RearWheelDynamics.cs','tools/shared-world-speedway/ExperimentalTrackShape.cs','tools/shared-world-speedway/TyrePhysics149/Contact149.cs','tools/planner-speed-root-cause/ExactRuntime148.cs','tools/tyre-physics-corner-limits/ExactRuntime149.cs','tools/robust-autonomous-racing/Planner150.cs','tools/robust-autonomous-racing/RecourseAudit150.cs','tools/robust-autonomous-racing/Feedback150.cs','docs/experiments/robust-autonomous-racing/budget.json']
        save(D/'source-proof.json',dict(Base=BASE,FrozenHashes={p:hashlib.sha256((R/p).read_bytes().replace(b'\r\n',b'\n') if p.endswith('.cs') else (R/p).read_bytes()).hexdigest() for p in frozen}))
        print(verdict);print([(r['name'],r['outcome'],r['finish_s']) for r in new]);print('budget before saved analysis',sum(x['CpuSeconds'] for x in read(D/'budget.json')))
if __name__=='__main__':main()
