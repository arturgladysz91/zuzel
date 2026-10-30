"""Read-only export of sealed153 experiments and historical148/151/152 evidence."""
from pathlib import Path
from contextlib import contextmanager
from collections import Counter
import csv,gzip,hashlib,importlib.util,json,math,msvcrt,os,subprocess,sys,time
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/safe-fast-contingency';BASE='a9024b5bb141e8158fc7c441705768a7b7354d35'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if str(p).endswith('.gz') else p.read_bytes())
def save(p,x):
 if p.exists():raise FileExistsError(p)
 p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(x,ensure_ascii=False,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def module(name,path):
 s=importlib.util.spec_from_file_location(name,R/path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
@contextmanager
def charged(label):
 with (D/'budget.json.lock').open('a+b') as lock:
  lock.seek(0);msvcrt.locking(lock.fileno(),msvcrt.LK_NBLCK,1);ledger=read(D/'budget.json');assert all(r['Complete'] for r in ledger) and not any(r['Name']==label for r in ledger)
  began=time.process_time();spent=sum(r['CpuSeconds'] for r in ledger);assert spent<1498
  row=dict(Name=label,Complete=False,NativeWorlds=0,CpuSeconds=0,Base=BASE);ledger.append(row)
  def flush():(D/'budget.json').write_text(json.dumps(ledger,indent=2)+'\n',encoding='utf-8',newline='\n')
  flush()
  try:yield lambda: (_ for _ in ()).throw(RuntimeError('export CPU cap')) if spent+time.process_time()-began>=1498 else None
  finally:row.update(Complete=True,CpuSeconds=time.process_time()-began,Outcome='saved-data export; inspect validation');flush()
def journal(path):
 previous='GENESIS';rows=[]
 for k,line in enumerate(path.read_text(encoding='utf-8').splitlines()):
  x=json.loads(line);raw=line.split('"Payload":',1)[1].rsplit(',"Sha256":',1)[0];digest=hashlib.sha256((previous+raw).encode()).hexdigest().upper();assert x['Index']==k and x['Previous']==previous and x['Sha256']==digest;previous=digest;rows.append(x['Payload'])
 return rows
def percentile(a,q):return sorted(a)[min(len(a)-1,int((len(a)-1)*q))] if a else None
def t(s):return s['Input']['Time']
def ref(s):return s['Input']['Rider']['TotalReferenceMeters']
def summary(run,result=None):
 result=result or {};r=run['Riders'][0];ss=r['Samples'];costs=[d['CpuSeconds'] for d in r['Decisions']];sweeps=[s['Certificate'] for s in ss if isinstance(s.get('Certificate'),(float,int))]
 return dict(name=run['Name'],regime=result.get('Regime','Legacy'),outcome=run['Outcome'],laps=r['CompletedLaps'],finish_s=r['FinishSeconds'],end_s=run['EndSeconds'],reference_m=ref(ss[-1]),fingerprint=run['Fingerprint'],controller_cpu_s=result.get('ControllerAttemptCpu',run['ControllerCpuSeconds']),planning_cpu_s=sum(costs),experiment_cpu_s=result.get('CpuSeconds',run['TotalCpuSeconds']),minimum_gap_m=min(min(s['Inner'],s['Outer']) for s in ss),minimum_sweep_m=min(sweeps,default=None),maximum_beta_deg=max(abs(s['Input']['Rider']['SlideRadians'])*180/math.pi for s in ss),contacts=len(run['Contacts']),half_meter_pose_violations=sum(min(s['Inner'],s['Outer'])<.5 for s in ss),samples=len(ss),decisions=len(costs),cpu_p50_s=percentile(costs,.5),cpu_p95_s=percentile(costs,.95),cpu_max_s=max(costs,default=None),cpu_quantile_scope='planner function; complete controller/archive total is separate',path_m=ss[-1]['Input']['Rider']['DistanceMeters'],closed_gas_s=sum(t(b)-t(a) for a,b in zip(ss,ss[1:]) if b['Input']['Active']['Drive']==0),laps_s=[c['Time']-(run['Crossings'][j-1]['Time'] if j else 0) for j,c in enumerate(run['Crossings'])],result=result)
def main():
 with charged('saved-analysis-final-v1') as guard:
  a146=module('a146','tools/autonomous-corner-planning/analyze146.py');a152=module('a152','tools/efficient-contingency/analyze152.py')
  old=read(R/'docs/experiments/efficient-contingency/analysis.json');starts=[s['startReferenceMeters'] for s in read(R/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json')['segments']]
  paths=[R/'docs/experiments/tyre-physics-corner-limits/native/baseline149-final148.json.gz',R/'docs/experiments/contingency-recourse/native/contingency151-gate4-A-v2.json.gz',R/'docs/experiments/efficient-contingency/native/efficient152-A-portfolio-v1.json.gz']+sorted((D/'native').glob('*.json.gz'))
  runs=[];corners=[];audits={};curves=[];indices=[];laps=[];counterrows=[];sources=[];rankings=[]
  for path in paths:
   guard();run=read(path);name=run['Name'];r=run['Riders'][0];ss=r['Samples'];new=path.parent==D/'native';rp=path.with_name(name+'-result.json');result=read(rp) if rp.exists() else {};s=summary(run,result);s['new']=new;s['rejected_plans']=None;s['rejected_trees']=None;s['counts']=dict(k1=None,forecasts=run['Forecasts'],contacts=run['ForecastContactQueries']);s['exclusive_phases']={}
   sources.append(dict(path=path.relative_to(R).as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
   cs=a146.analyze(run,starts)
   for c in cs:c['delta_K1_s']=None
   corners+=cs
   if name=='baseline149-final148':
    s['counts']['k1']=result['Runtime']['FullK1Executions'];profile=Counter();operations=Counter()
    for q in journal(path.with_name(name+'-decisions.jsonl')):
     if q['Event']!='decision':continue
     for z in q['Profile']['Phases']:profile[z['Name']]+=z['CpuSeconds']
     for z in q['Profile']['Operations']:operations[z['Name']]+=z['CpuSeconds']
    s['exclusive_phases']=dict(full_k1=profile['seed_k1'],independent_preflight=operations['independent_preflight']);s['phase_scope']='148 seed phase and independent operation are disjoint here; no conditional151 tree existed'
   if not new and name in old['plan_audits']:
    p=old['plan_audits'][name];s['exclusive_phases']=p['exclusive_phases'];s['counts']={k:sum(x[k] for x in p['counters']) for k in ['k1','forecasts','contacts']};s['rejected_trees']=sum(x['rejected'] for x in p['counters'])
   if name=='contingency151-gate4-A-v2':
    s['controller_cpu_s']=276.953125;s['exclusive_phases']=old['old_phases']['exclusive_phases'];last=read(R/'docs/experiments/efficient-contingency/baseline151.json')[-1];s['counts']=a152.counts(last['Counts'])
   if new:
    js=journal(path.with_name(name+'-decisions.jsonl'));ds=[x for x in js if x['Event']=='decision'];assert len(ds)==len(r['Decisions']);phases=Counter();prev=Counter();rows=[];rejected=0
    for d,actual in zip(ds,r['Decisions']):
     assert d['Independent'] and d['ConditionalCertificate'] and d['Selected']==actual['Choice']['Action'];assert (d['Proof']['Nodes'],d['Proof']['Leaves'])==(13,9)
     if result['Regime']=='SafetyHalfMeter':assert d['Proof']['Safety']['MinimumGapMeters']>=.5 and d['Proof']['Safety']['SweepLowerBoundMeters']>=.5
     for cmd in d['Observed']['Pending']:
      issued=next(q for q in ds if abs(q['Time']-cmd['ObservedAt'])<1e-8);assert issued['Selected']==cmd['Action'] and abs(cmd['ActivationAt']-cmd['ObservedAt']-.24)<1e-8
     activation=next((a for a in r['Activations'] if abs(a['ObservedAt']-d['Time'])<1e-8),None)
     if activation:assert activation['Action']==d['Selected'] and abs(activation['ActivationAt']-d['Time']-.24)<1e-8
     for p in d['Profile']['Phases']:phases[p['Name']]+=p['CpuSeconds']
     c=a152.counts(d['Counts']);delta={k:v-prev[k] for k,v in c.items()};prev=Counter(c);counterrows.append(dict(run=name,time_s=d['Time'],cpu_s=d['CpuSeconds'],**delta))
     policy=read(D/'policies'/name/(d['Tree']+'.json.gz'));assert policy['PhysicalHash']==d['PhysicalHash'];rejected+=sum(not a['Legal'] for a in policy['Attempts'])
     rows.append(dict(time_s=d['Time'],reason=d['Reason'],selected=[d['Selected']],planned_goal_time_s=d['Proof']['Arrival'],planned_exit_time_s=None,planned_exit_kph=None,forecast_legal=True,actual_action=d['Selected'],actual_activation=activation,next_change_reason='fresh observation and full finite certification',prediction_error=None,tree_file='policies/'+name+'/'+d['Tree']+'.json.gz',nodes=13,leaves=9,predicted_trace=d['Proof']['Trace']))
     if len(d.get('Ranked',[]))>1:
      selected=d.get('SelectedRank') or min(d['Ranked'],key=lambda p:p['ArrivalSeconds'])
      rankings.append(dict(run=name,time_s=d['Time'],policies=d['Ranked'],selected=selected,gain_s=d['Ranked'][0]['ArrivalSeconds']-selected['ArrivalSeconds'],worst_gain_s=d['Ranked'][0]['WorstReserveArrivalSeconds']-selected['WorstReserveArrivalSeconds']))
    ap=D/'policies'/name/'abstain.json.gz'
    if ap.exists():
     ab=read(ap);rv=ab.get('Review');rejected+=sum(not a['Legal'] for a in rv['Attempts']) if rv else 0
     if rv:
      for p in rv['Profile']['Phases']:phases[p['Name']]+=p['CpuSeconds']
      final_counts=Counter(a152.counts(rv['Counts']));delta={k:v-prev[k] for k,v in final_counts.items()};counterrows.append(dict(run=name,time_s=rv['Time'],cpu_s=rv['CpuSeconds'],**delta));prev=final_counts
      s['failed_review']=dict(time_s=rv['Time'],reason=rv['Reason'],nodes=len(rv['Tree']),leaves=len(rv['Terminals']),cpu_s=rv['CpuSeconds'],reasons=dict(Counter(x['Reason'] for x in rv['Attempts'])),attempts=len(rv['Attempts']),first_failures=[x['FirstFailure'] for x in rv['Attempts'] if x.get('FirstFailure') is not None][:8])
    s['exclusive_phases']=dict(phases);s['counts']=dict(prev);s['rejected_plans']=rejected;s['rejected_trees']=sum(d['Proof']['RejectedTrees'] for d in ds)
    timing=D/'timings'/(name+'.json')
    if timing.exists():
     measured=read(timing);costs=[x['CpuSeconds'] for x in measured];s.update(cpu_p50_s=percentile(costs,.5),cpu_p95_s=percentile(costs,.95),cpu_max_s=max(costs,default=None),cpu_quantile_scope='complete controller callback attempts; failed attempts included')
    baseline=[x for x in journal(R/'docs/experiments/efficient-contingency/native/efficient152-A-portfolio-v1-decisions.jsonl') if x['Event']=='decision'];common=min(len(ds),len(baseline));different=[ds[k]['Time'] for k in range(common) if ds[k]['Selected']!=baseline[k]['Selected']]
    s['comparison152']=dict(common_decisions=common,same_commands=sum(ds[k]['Selected']==baseline[k]['Selected'] for k in range(common)),same_full_certificate_digests=sum(ds[k]['PhysicalHash']==baseline[k]['PhysicalHash'] for k in range(common)),first_different_command_s=different[0] if different else None,scope='Corresponding review indices; trajectories diverge after the first changed control. L full-time comparison is valid only for complete runs.')
    audits[name]=dict(reviews=len(ds),native_decisions=len(ds),coverage_start_s=ds[0]['Time'] if ds else None,coverage_end_s=ds[-1]['Time'] if ds else None,followed_expected=0,expected_reviews=0,queues_match=True,maximum_error=dict(PositionMeters=None),cpu_p50_s=s['cpu_p50_s'],cpu_p95_s=s['cpu_p95_s'],rows=rows,exclusive_phases=dict(phases),abstentions=[x for x in js if x['Event']=='abstain'])
   runs.append(s);indices.append(dict(Id=name,File=os.path.relpath(path,D).replace('\\','/'),Variant=run['Variant'],Regime=s['regime'],Riders=1,Outcome=run['Outcome'],Duration=run['EndSeconds'],Fingerprint=run['Fingerprint']))
   laps.append(dict(Run=name,RiderId=r['Spec']['Profile']['Id'],Laps=[dict(Lap=j+1,LapSeconds=v) for j,v in enumerate(s['laps_s'])]))
   curves.append(dict(name=name,points=[dict(t=t(p),x=p['Input']['Rider']['Position']['X'],y=p['Input']['Rider']['Position']['Y'],reference=ref(p),speed=p['Input']['Rider']['Speed']*3.6,beta=p['Input']['Rider']['SlideRadians']*180/math.pi,gas=p['Input']['Active']['Drive'],offset=p['Input']['Rider']['Road']['OffsetMeters']) for p in ss[::4]]))
  historical=[];violations=[]
  for row in read(D/'geometry-audit.json')['Runs']:
   x=read(D/'history'/(row['Run']+'-geometry.json.gz'));history=dict(row);history['LegalPoseSamples']=row['Poses']-row['PoseViolations'];history['LegalSweptIntervals']=row['Intervals']-row['UncertifiedHalfMeterIntervals'];historical.append(history)
   group=[]
   def close():
    if group:violations.append(dict(run=row['Run'],start_s=group[0]['Time'],end_s=group[-1]['Time'],start_reference_m=group[0]['Reference'],end_reference_m=group[-1]['Reference'],samples=len(group),minimum_gap_m=min(min(p['Inner'],p['Outer']) for p in group)))
   for p in x['Samples']:
    if not p['MeetsPoseHalfMeter']:group.append(p)
    elif group:close();group=[]
   close()
  saved_forecasts=[];old_rejected=0
  for path in sorted((R/'docs/experiments/efficient-contingency/policies/efficient152-A-portfolio-v1').glob('decision-*.json.gz')):
   guard();x=read(path);old_rejected+=sum(not a['Legal'] for a in x['Attempts']);safeties=[n['Execution'] for n in x['Tree']]+[x['Tapes'][x['Nominal']]['Safety']]+[x['Tapes'][t['Tape']]['Safety'] for t in x['Terminals']];saved_forecasts.append(dict(time_s=x['Time'],all_saved_finite_forecasts_meet_half_meter=all(s['Legal'] and s['MinimumGapMeters']>=.5 and s['SweepLowerBoundMeters']>=.5 for s in safeties),minimum_gap_m=min(s['MinimumGapMeters'] for s in safeties),minimum_sweep_m=min(s['SweepLowerBoundMeters'] for s in safeties)))
  next(r for r in runs if r['name']=='efficient152-A-portfolio-v1')['rejected_plans']=old_rejected
  matched=None;cp=D/'native/cold-L-prefix-v1.json.gz'
  if cp.exists():
   control=read(cp);optimized=read(D/'native/exact-L-v3.json.gz');archived_run=read(R/'docs/experiments/efficient-contingency/native/efficient152-A-portfolio-v1.json.gz');n=len(control['Riders'][0]['Decisions']);ns=len(control['Riders'][0]['Samples']);pack=lambda x:json.dumps(x,sort_keys=True,separators=(',',':'),allow_nan=False).encode();before=sum(x['CpuSeconds'] for x in control['Riders'][0]['Decisions']);after=sum(x['CpuSeconds'] for x in optimized['Riders'][0]['Decisions'][:n]);historic=sum(x['CpuSeconds'] for x in archived_run['Riders'][0]['Decisions'][:n]);data=pack(control['Riders'][0]['Samples']);equal=data==pack(optimized['Riders'][0]['Samples'][:ns])==pack(archived_run['Riders'][0]['Samples'][:ns]);assert equal
   matched=dict(StartSeconds=0,EndSeconds=control['EndSeconds'],Decisions=n,NativeSamples=ns,AllNativeSampleFieldsEqual=equal,CanonicalNativeSamplesSha256=hashlib.sha256(data).hexdigest(),Cold152PlannerCpu=before,Exact153PlannerCpu=after,Archived152SamePrefixPlannerCpu=historic,ReductionPercent=100*(1-after/before),Scope='Same-session original152 and exact153 on identical continuous native prefix. Compare planner function CPU, not a partial controller total to a full-race total. Archive timing is a separate measurement.')
   save(D/'same-session-cpu.json',matched)
  proof=read(R/'docs/experiments/efficient-contingency/source-proof.json');trees=proof['FrozenHistoricalGitTrees'].copy();trees['docs/experiments/efficient-contingency']=subprocess.check_output(['git','rev-parse',BASE+':docs/experiments/efficient-contingency'],cwd=R,text=True).strip()
  blobs=read(D/'checkout-audit.json')['FrozenGitBlobs'].copy();raw=subprocess.check_output(['git','ls-tree','-r',BASE,'--','tools/efficient-contingency','src'],cwd=R,text=True)
  for line in raw.splitlines():meta,path=line.split('\t',1);blobs[path]=meta.split()[2]
  current={line.split('\t',1)[1]:line.split('\t',1)[0].split()[2] for line in subprocess.check_output(['git','ls-tree','-r','HEAD'],cwd=R,text=True).splitlines()}
  assert all(current[p]==h for p,h in blobs.items());assert all(subprocess.check_output(['git','rev-parse','HEAD:'+p],cwd=R,text=True).strip()==h for p,h in trees.items())
  save(D/'source-proof.json',dict(Base=BASE,FrozenGitBlobs=blobs,FrozenHistoricalGitTrees=trees,Scope='Exact157 frozen sources, all152 tool blobs, all production src blobs, and150 historical evidence trees. Shared project friend declaration is the only shared build-access change.'))
  save(D/'analysis.json',dict(full_runs=runs,corners=corners,plan_audits=audits,rankings=rankings,history=historical,same_session_cpu=matched,historical152_forecasts=saved_forecasts,historical_violations=violations,source_files=sources,counts=counterrows,scope='Finite13-node/9-reserve model certificates, continuous native Box2D execution. S is swept whole-footprint>=0.5m. No global-disturbance or four-rider guarantee.'))
  default=next((r['name'] for r in runs if r['new'] and r['regime']=='SafetyHalfMeter' and r['outcome']=='completed'),next((r['name'] for r in reversed(runs) if r['new'] and r['outcome']=='completed'),runs[-1]['name']))
  save(D/'index.json',dict(Title='#153 · Legacy i obrys0,5m',DefaultRun=default,GeometryFile='../causal-yaw-throttle/progressive-b-viewer-geometry.json',Runs=indices,LapMeters=318.77874994277954,RequiredLaps=3,StartReference=0,BendReferences=[starts[k] for k in [1,4,5,8]],Note='Reżimy są oddzielne: L zachowuje reguły152; S wymaga>=0,5m dla całego swept obrysu. Historyczne148/152 naruszają S. PARTIAL pozostaje przerwanym zapisem. Skończone scenariusze nie są globalną gwarancją.'))
  save(D/'lap-metrics.json',laps);(D/'comparison.json.gz').write_bytes(gzip.compress(json.dumps(curves,separators=(',',':')).encode(),mtime=0))
  for name,rows in [('corners',corners),('decision-counts',counterrows),('historical-violations',violations)]:
   if rows:
    with (D/(name+'.csv')).open('x',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(rows[0]),lineterminator='\n');w.writeheader();w.writerows(rows)
  print([(r['name'],r['outcome'],r['finish_s'],r['controller_cpu_s']) for r in runs]);print('saved153 analysis and replay; no new worlds')
if __name__=='__main__':main()
