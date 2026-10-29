"""Read-only, measured postprocessing of preserved149 evidence. No world/forecast or parameter fit."""
from pathlib import Path
import csv,gzip,hashlib,importlib.util,json,math,os,statistics,subprocess,time
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/tyre-physics-corner-limits'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.name.endswith('.gz') else p.read_bytes())
def save(p,x):p.write_text(json.dumps(x,indent=2,ensure_ascii=False,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def module(name,path):
 s=importlib.util.spec_from_file_location(name,R/path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def main():
 begin=time.process_time();lease=(D/'budget.json.lock').open('a+b');ledger=read(D/'budget.json');assert all(x['Complete'] for x in ledger);assert not any(x['Name']=='saved-evidence-analysis' for x in ledger)
 m=module('m145','tools/corner-speed-loss/analyze145.py');m146=module('m146','tools/autonomous-corner-planning/analyze146.py');m147=module('m147','tools/efficient-autonomous-planner/analyze147.py')
 g=read(R/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json');starts=[q['startReferenceMeters'] for q in g['segments']]
 corners=[];full=[];curves=[];indices=[];laps=[];audits={};regions=[];runs={};telemetry={}
 paths=[R/'docs/experiments/standing-start-three-laps/native/K1.json.gz',R/'docs/experiments/standing-start-three-laps/native/F.json.gz',D/'native/baseline149-final148.json.gz',D/'native/separated149-autonomous.json.gz']
 for path in paths:
  run=read(path);name=run['Name'];runs[name]=run;ss=run['Riders'][0]['Samples'];rows=[m.interval(a,b) for a,b in zip(ss,ss[1:])];telemetry[name]=rows
  c=m146.analyze(run,starts);corners+=c;phase=m.phase(rows,0,run['EndSeconds']);rr=run['Riders'][0]
  result=read(path.with_name(name+'-result.json')) if path.parent==D/'native' else None
  full.append(dict(name=name,model='B' if name=='separated149-autonomous' else 'A',outcome=run['Outcome'],finish_s=rr['FinishSeconds'],laps=rr['CompletedLaps'],maximum_kph=max(s['Input']['Rider']['Speed']*3.6 for s in ss),minimum_gap_m=min(min(s['Inner'],s['Outer']) for s in ss),minimum_sweep_m=min(s['Certificate'] for s in ss if s['Certificate'] is not None),contacts=len(run['Contacts']),controller_cpu_s=run['ControllerCpuSeconds'],total_cpu_s=run['TotalCpuSeconds'],cost_scope='new149 measurement' if result else 'historical144 measurement',energy=phase['energy'],stats=phase['stats'],maximum_system_energy_residual_J=max(abs(q['total_energy_residual_J']) for q in rows),runtime=result['Runtime'] if result else None,legal_completions=1 if run['Outcome']=='completed' else 0))
  launch_end=m.point(ss,starts[1]);regions.append(dict(run=name,lap=1,bend=0,entry=None,through_next_entry_complete=bool(launch_end),through_next_straight=m.phase(rows,0,launch_end['time_s']) if launch_end else None,next_bend_complete=False,through_next_bend=None))
  for lap,bend in [(1,1),(2,1),(2,2),(3,1),(3,2)]:
   lo=(lap-1)*run['LapMeters']+starts[1 if bend==1 else 5];hi=(lap-1)*run['LapMeters']+(starts[5] if bend==1 else run['LapMeters']+starts[1]);end=m.point(ss,hi);entry=m.point(ss,lo)
   if not entry:continue
   endtime=end['time_s'] if end else run['EndSeconds'];p=m.phase(rows,max(0,entry['time_s']-1.2),endtime)
   next_exit=(lap-1)*run['LapMeters']+(starts[8] if bend==1 else run['LapMeters']+starts[4]);nextend=m.point(ss,next_exit)
   regions.append(dict(run=name,lap=lap,bend=bend,entry=entry,through_next_entry_complete=bool(end),through_next_straight=p,next_bend_complete=bool(nextend),through_next_bend=m.phase(rows,max(0,entry['time_s']-1.2),nextend['time_s']) if nextend else None))
  curves.append(dict(name=name,points=[dict(t=s['Input']['Time'],reference=s['Input']['Rider']['TotalReferenceMeters'],x=s['Input']['Rider']['Position']['X'],y=s['Input']['Rider']['Position']['Y'],speed=s['Input']['Rider']['Speed']*3.6,beta=s['Input']['Rider']['SlideRadians']*180/math.pi,gas=s['Input']['Active']['Drive'],offset=s['Input']['Rider']['Road']['OffsetMeters']) for s in ss[::4]]))
  indices.append(dict(Id=name,File=Path(os.path.relpath(path,D)).as_posix(),Variant=run['Variant'],Riders=1,Outcome=run['Outcome'],Duration=run['EndSeconds'],Fingerprint=run['Fingerprint']));laps.append(dict(Run=name,RiderId=1,Laps=[dict(Lap=q['Lap'],LapSeconds=q['Time']-(run['Crossings'][i-1]['Time'] if i else 0)) for i,q in enumerate(run['Crossings'])]))
  if result:
   payload=m147.journal(D/'native'/f'{name}-decisions.jsonl');ds=[x for x in payload if x['Event']=='decision'];pr=[]
   for j,(d,n) in enumerate(zip(ds,rr['Decisions'])):
    assert d['Selected']==n['Choice']['Action'];assert d['Certificate']['Legal'] and d['Independent']['Legal']
    pr.append(dict(time_s=d['Time'],reason=d['Reason'],selected=d['PlannedActions'][:3],planned_goal_time_s=d['GoalTime'],planned_exit_time_s=d['CornerExitTime'],planned_exit_kph=d['CornerExitSpeed']*3.6 if d['CornerExitSpeed'] is not None else None,forecast_legal=True,actual_action=n['Choice']['Action'],actual_activation=next((a for a in rr['Activations'] if abs(a['ObservedAt']-d['Time'])<1e-8),None),next_change_reason=ds[j+1]['Reason'] if j+1<len(ds) else None,prediction_error=d['PredictionError'],predicted_trace=None))
   assert len(ds)==len(rr['Decisions']);errors=[d['PredictionError'] for d in ds if d['PredictionError']];costs=[d['PlanningCpuSeconds'] for d in ds]
   audits[name]=dict(reviews=len(ds),native_decisions=len(rr['Decisions']),coverage_start_s=0,coverage_end_s=ds[-1]['Time'],followed_expected=sum(d['FollowedExpected'] for d in ds if d['ExpectedAction'] is not None),expected_reviews=sum(d['ExpectedAction'] is not None for d in ds),queues_match=all(e['ActiveMatches'] and e['QueueMatches'] for e in errors),maximum_error={k:max(e[k] for e in errors) for k in ['PositionMeters','SpeedMps','BetaRadians','YawRate','WheelOmega','CrankOmega']},cpu_p50_s=statistics.median(costs),cpu_p95_s=sorted(costs)[int((len(costs)-1)*.95)],cpu_max_s=max(costs),rows=pr)
 for c in corners:
  k=next(x for x in corners if x['run']=='K1' and x['lap']==c['lap'] and x['bend']==c['bend']);c['delta_K1_s']=c['seconds']-k['seconds'] if c['complete'] else None
 fixture=[];causal=[]
 for p in sorted((D/'fixtures').glob('*-result.json')):
  result=read(p);name=p.name.removesuffix('-result.json');run=read(p.with_name(name+'.json.gz'));ss=run['Riders'][0]['Samples'];rows=[m.interval(a,b) for a,b in zip(ss,ss[1:])]
  def segment(lo,hi):
   a,b=m.point(ss,lo),m.point(ss,hi)
   if not a or not b:return None
   q=m.phase(rows,a['time_s'],b['time_s']);inside=[v for v in rows if lo<=v['reference_m']<=hi];q.update(entry_kph=a['speed_kph'],exit_kph=b['speed_kph'],minimum_kph=min(v['speed_kph'] for v in inside),maximum_kph=max(v['speed_kph'] for v in inside),path_m=b['distance_m']-a['distance_m'],trajectory_radius_min_m=min((v['trajectory_radius_m'] for v in inside if v['trajectory_radius_m'] is not None),default=None));return q
  indices.append(dict(Id=name,File='fixtures/'+name+'.json.gz',Variant=run['Variant'],Riders=1,Outcome=run['Outcome'],Duration=run['EndSeconds'],Fingerprint=run['Fingerprint']))
  laps.append(dict(Run=name,RiderId=1,Laps=[dict(Lap=q['Lap'],LapSeconds=q['Time']-(run['Crossings'][i-1]['Time'] if i else 0)) for i,q in enumerate(run['Crossings'])]))
  if name.startswith('causal'):
   result.update(name=name,PrefixSource=result.get('PrefixSource','A'),intervention_reached=run['EndSeconds']>=result['At'],completed_laps=run['Riders'][0]['CompletedLaps'],corners=m146.analyze(run,starts),through_following_bend=segment((run['LapMeters']+starts[5]) if result['At']==28.4 else (2*run['LapMeters']+starts[1]),(2*run['LapMeters']+starts[4]) if result['At']==28.4 else (2*run['LapMeters']+starts[8])));causal.append(result);continue
  first=segment(starts[1],starts[4]);straight=segment(starts[4],starts[5]);nextbend=segment(starts[5],starts[8]);window=[s for s in ss if s['Input']['Rider']['TotalReferenceMeters']<=starts[5]]
  certified=first is not None and straight is not None and all(s['Inner']>=0 and s['Outer']>=0 and (s['Certificate'] is None or s['Certificate']>=0) and abs(s['Input']['Rider']['SlideRadians'])<=math.pi/4 for s in window)
  result.update(name=name,initial_kph=ss[0]['Input']['Rider']['Speed']*3.6,initial_offset_m=ss[0]['Input']['Rider']['Road']['OffsetMeters'],bend=first,next_straight=straight,next_bend=nextbend,corner_and_next_straight_certified=certified,minimum_speed_over_whole_bend=first['minimum_kph'] if first else None,physical_crossing=any(s['Inner']<-.0016 or s['Outer']<-.0016 for s in ss),numeric_instability=run['Outcome'].startswith('technical invariant'),reverse=run['Outcome']=='reverse');fixture.append(result)
 for x in causal:
  control=next(y for y in causal if y['Model']==x['Model'] and y['At']==x['At'] and y['PrefixSource']==x['PrefixSource'] and y['Mode']=='control');x['delta_finish_vs_matched_s']=x['FinishSeconds']-control['FinishSeconds'] if x['FinishSeconds'] is not None and control['FinishSeconds'] is not None else None
 old=read(R/'docs/experiments/planner-speed-root-cause/native/autonomous148-prefix.json.gz');a=runs['baseline149-final148'];equivalence=dict(samples_equal=a['Riders'][0]['Samples']==old['Riders'][0]['Samples'],commands_equal=[d['Choice'] for d in a['Riders'][0]['Decisions']]==[d['Choice'] for d in old['Riders'][0]['Decisions']],fingerprint=a['Fingerprint'],samples=len(a['Riders'][0]['Samples']),commands=len(a['Riders'][0]['Decisions']))
 assert equivalence['samples_equal'] and equivalence['commands_equal']
 sensitivity=[]
 for compression in [0,.1,.2,.35]:
  def profile(t):
   z=min(t,1-t)/.05;ramp=1 if z>=1 else z**3*(10+z*(-15+6*z));return ramp*(1+compression*math.cos(2*math.pi*t))
  n=4096;integral=sum((profile(i/n)+4*profile((i+.5)/n)+profile((i+1)/n))/(6*n) for i in range(n));maximum=max(profile(i/(n*2)) for i in range(n*2+1));k=math.pi*maximum/integral/((a['LapMeters']-124)/2);radius=1/k
  sensitivity.append(dict(compression=compression,maximum_curvature=k,minimum_reference_radius_m=radius,minimum_curvature=0,constant_offset_upper_kph=[dict(offset_m=o,kph=3.6*math.sqrt(1.2845000267028808*9.81*(radius+o))) for o in [0,6,15.6]],scope='offline curvature-profile sensitivity at same bend arc length; width and dynamic path are not simulated'))
 repeated=read(D/'native/separated149-repeat.json.gz');br=runs['separated149-autonomous'];repeat=dict(samples_equal=repeated['Riders'][0]['Samples']==br['Riders'][0]['Samples'],commands_equal=[d['Choice'] for d in repeated['Riders'][0]['Decisions']]==[d['Choice'] for d in br['Riders'][0]['Decisions']],fingerprint_equal=repeated['Fingerprint']==br['Fingerprint'],finish_s=repeated['Riders'][0]['FinishSeconds'],controller_cpu_s=repeated['ControllerCpuSeconds'],outcome=repeated['Outcome']);assert repeat['samples_equal'] and repeat['commands_equal'] and repeat['fingerprint_equal']
 ja=m147.journal(D/'native/baseline149-final148-decisions.jsonl');jb=m147.journal(D/'native/separated149-autonomous-decisions.jsonl');da=[x for x in ja if x['Event']=='decision'];db=[x for x in jb if x['Event']=='decision'];first=next((dict(time=x['Time'],A=x,B=y) for x,y in zip(da,db) if x['Selected']!=y['Selected']),None)
 save(D/'first-decision-divergence.json',first)
 save(D/'analysis.json',dict(full_runs=full,corners=corners,plan_audits=audits,baseline_equivalence=equivalence,B_repeatability=repeat,fixtures=fixture,causal=causal,regions=regions,geometry_sensitivity=sensitivity,limits=['No certified global optimum or maximum legal steady speed. Entry-speed fixtures use causal K1 and may decelerate.','B material separation fixes inertia coupling but preserves nominal material curve; neither law has empirical calibration.','Beyond-beta45 observation cannot establish a race PASS.','No automatic cache approximation or algorithm change is mixed with tyre A/B.']))
 save(D/'index.json',dict(Title='#149 · Opony, granica prędkości i geometria',DefaultRun='separated149-autonomous',GeometryFile='../causal-yaw-throttle/progressive-b-viewer-geometry.json',Runs=indices,LapMeters=a['LapMeters'],RequiredLaps=3,StartReference=0,BendReferences=[starts[i] for i in [1,4,5,8]],Note='A/B zmienia wyłącznie tylne prawo kontaktu i jego solver. Nie jest to kalibracja rzeczywistej opony.'))
 save(D/'lap-metrics.json',laps);(D/'comparison.json.gz').write_bytes(gzip.compress(json.dumps(curves,separators=(',',':')).encode(),mtime=0))
 with (D/'corners.csv').open('w',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(corners[0]),lineterminator='\n');w.writeheader();w.writerows(corners)
 oldpaths=set(subprocess.check_output(['git','ls-tree','-r','--name-only','14cb6c2fb947d2d04b1a7979346c8947b131b4fd'],text=True,encoding='utf-8').splitlines());changed=subprocess.check_output(['git','diff','14cb6c2fb947d2d04b1a7979346c8947b131b4fd','--name-only'],text=True,encoding='utf-8').splitlines();changedold=sorted(set(changed)&oldpaths)
 allowed={'tests/CoreSim.Tests/CoreSim.Tests.csproj','tools/replay-viewer/viewer.mjs','tools/replay-viewer/replay.test.mjs'};assert set(changedold)<=allowed
 save(D/'source-proof.json',dict(Base='14cb6c2fb947d2d04b1a7979346c8947b131b4fd',OldTrackedPaths=len(oldpaths),ChangedOldPaths=changedold,UnchangedPhysicsAndHistoricalData=True,FrozenHashes={p:hashlib.sha256((R/p).read_bytes()).hexdigest() for p in ['tools/shared-world-speedway/RearWheelDynamics.cs','tools/shared-world-speedway/StandingStart144/Contact144.cs','tools/shared-world-speedway/StandingStart144/CrankClutch144.cs','tools/shared-world-speedway/ExperimentalTrackShape.cs','tools/planner-speed-root-cause/ExactRuntime148.cs','docs/experiments/planner-speed-root-cause/budget.json']}))
 entry=dict(Name='saved-evidence-analysis',Complete=True,NativeWorlds=0,CpuSeconds=time.process_time()-begin,Outcome='read-only complete A/B, fixtures and full-prefix diagnostics analysis');ledger.append(entry);save(D/'budget.json',ledger);lease.close();print('CPU',entry['CpuSeconds'],'total',sum(x['CpuSeconds'] for x in ledger),'worlds',sum(x['NativeWorlds'] for x in ledger));print([(x['name'],x['finish_s'],x['controller_cpu_s']) for x in full])
if __name__=='__main__':main()
