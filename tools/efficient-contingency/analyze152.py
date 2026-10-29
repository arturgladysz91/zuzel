"""Saved evidence only; never chooses commands or creates native worlds."""
from pathlib import Path
from contextlib import contextmanager
from collections import Counter
import csv,gzip,hashlib,importlib.util,json,math,os,subprocess,sys,time
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/efficient-contingency';BASE='5558a30c8864199fb1c7abc18852ca27f4d29d9b'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if str(p).endswith('.gz') else p.read_bytes())
def save(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=None if p.name=='analysis.json' else 2,separators=(',',':') if p.name=='analysis.json' else None,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def module(name,path):
 s=importlib.util.spec_from_file_location(name,R/path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
@contextmanager
def charged(label):
 import msvcrt
 with (D/'budget.json.lock').open('a+b') as lease:
  lease.seek(0);msvcrt.locking(lease.fileno(),msvcrt.LK_NBLCK,1);ledger=read(D/'budget.json');assert all(x['Complete'] for x in ledger);assert not any(x['Name']==label for x in ledger)
  began=time.process_time();spent=sum(x['CpuSeconds'] for x in ledger);assert spent<1198;row=dict(Name=label,Complete=False,NativeWorlds=0,CpuSeconds=0,Base=BASE);ledger.append(row);save(D/'budget.json',ledger)
  try:yield lambda: (_ for _ in ()).throw(RuntimeError('export CPU cap')) if spent+time.process_time()-began>=1198 else None
  finally:row.update(Complete=True,CpuSeconds=time.process_time()-began,Outcome='saved evidence export; inspect errors');save(D/'budget.json',ledger)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def journal(p):
 previous='GENESIS';rows=[]
 for k,line in enumerate(p.read_text(encoding='utf-8').splitlines()):
  x=json.loads(line);raw=line.split('"Payload":',1)[1].rsplit(',"Sha256":',1)[0];digest=hashlib.sha256((previous+raw).encode()).hexdigest().upper();assert x['Index']==k and x['Previous']==previous and x['Sha256']==digest;previous=digest;rows.append(x['Payload'])
 return rows
def ref(s):return s['Input']['Rider']['TotalReferenceMeters']
def t(s):return s['Input']['Time']
def counts(c):
 p=c['Physical'];new='Search' in p
 return dict(k1=p['FullK1'] if new else p['FullK1Executions'],forecasts=p['Forecasts'] if new else p['ActualForecasts'],contacts=(p['SearchWork']['ContactQueries']+p['IndependentContacts']+p['FeedbackQueries']) if new else p['TotalContactQueriesWithFeedback'],additional_forecasts=c['AdditionalCompletedForecasts'],additional_contacts=c['AdditionalCompletedContacts'],failed_k1_batches=c['FailedK1Batches'])
def main(label='v1'):
 with charged('saved-export-'+label) as guard:
  a146=module('a146','tools/autonomous-corner-planning/analyze146.py');a145=module('a145','tools/corner-speed-loss/analyze145.py')
  starts=[s['startReferenceMeters'] for s in read(R/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json')['segments']]
  baseline=read(D/'baseline151.json');oldcounts=[];prior=Counter()
  for x in baseline:
   c=counts(x['Counts']);delta={k:v-prior[k] for k,v in c.items()};prior.update({k:v-prior[k] for k,v in c.items()});oldcounts.append(dict(time_s=x['Time'],cpu_s=x['CpuSeconds'],nodes=x['Nodes'],leaves=x['Leaves'],rejected=x['RejectedTrees'],**delta))
  runs=[];corners=[];audits={};curves=[];indices=[];laps=[];allcounts=[];prefix={};sources=[];segments=[];algorithmic={};budgetmap={r['Name']:r for r in read(D/'budget.json')}
  olddir=R/'docs/experiments/tyre-physics-corner-limits/native';paths=[olddir/'baseline149-final148.json.gz',olddir/'separated149-autonomous.json.gz']+sorted((D/'native').glob('*.json.gz'))
  for path in paths:
   guard();run=read(path);r=run['Riders'][0];ss=r['Samples'];name=run['Name'];new=path.parent==D/'native';full=new and name.startswith('efficient152');resultpath=path.with_name(name+'-result.json');result=read(resultpath) if resultpath.exists() else {};source=dict(path=path.relative_to(R).as_posix(),sha256=sha(path));sources.append(source)
   cs=a146.analyze(run,starts);corners+=cs;costs=[d['CpuSeconds'] for d in r['Decisions']];phases=Counter();ds=[];counterrows=[]
   if full:
    payload=journal(path.with_name(name+'-decisions.jsonl'));ds=[x for x in payload if x['Event']=='decision'];assert len(ds)==len(r['Decisions']);previous=Counter();rows=[]
    for d,actual in zip(ds,r['Decisions']):
     assert d['ConditionalCertificate'] and d['Independent'] and d['Selected']==actual['Choice']['Action'];proof=d['Proof'];assert proof['Safety']['Legal'];assert proof['Leaves']==9 and proof['Nodes']==13
     for cmd in d['Observed']['Pending']:
      issued=next(x for x in ds if abs(x['Time']-cmd['ObservedAt'])<1e-8);assert cmd['Action']==issued['Selected'] and abs(cmd['ActivationAt']-cmd['ObservedAt']-.24)<1e-8
     activation=next((a for a in r['Activations'] if abs(a['ObservedAt']-d['Time'])<1e-8),None)
     if activation:assert activation['Action']==d['Selected'] and abs(activation['ActivationAt']-d['Time']-.24)<1e-8
     for q in d['Profile']['Phases']:phases[q['Name']]+=q['CpuSeconds']
     c=counts(d['Counts']);delta={k:v-previous[k] for k,v in c.items()};previous.update({k:v-previous[k] for k,v in c.items()});cr=dict(run=name,time_s=d['Time'],cpu_s=d['CpuSeconds'],nodes=proof['Nodes'],leaves=proof['Leaves'],rejected=proof['RejectedTrees'],**delta);counterrows.append(cr);allcounts.append(cr)
     rows.append(dict(time_s=d['Time'],reason=d['Reason'],selected=[d['Selected']],planned_goal_time_s=proof['Arrival'],planned_exit_time_s=proof['Arrival'],planned_exit_kph=None,forecast_legal=True,actual_action=d['Selected'],actual_activation=activation,next_change_reason='actual observation; complete finite recertification',prediction_error=None,tree_file='policies/'+name+'/'+d['Tree']+'.json.gz',nodes=proof['Nodes'],leaves=proof['Leaves'],predicted_trace=proof['Trace']))
    audits[name]=dict(reviews=len(ds),native_decisions=len(ds),coverage_start_s=ds[0]['Time'] if ds else None,coverage_end_s=ds[-1]['Time'] if ds else None,followed_expected=0,expected_reviews=0,queues_match=True,maximum_error=dict(PositionMeters=None),cpu_p50_s=a146.percentile(costs,.5),cpu_p95_s=a146.percentile(costs,.95),rows=rows,counters=counterrows,exclusive_phases=dict(phases),abstentions=[x for x in payload if x['Event']=='abstain'])
    if name=='efficient152-A-v1':
     old=read(R/'docs/experiments/contingency-recourse/native/contingency151-gate4-A-v2.json.gz');oss=old['Riders'][0]['Samples'];nss=ss[:len(oss)];oldbytes=json.dumps(oss,sort_keys=True,separators=(',',':')).encode();newbytes=json.dumps(nss,sort_keys=True,separators=(',',':')).encode();assert oldbytes==newbytes
     prefix=dict(samples=len(oss),end_s=t(oss[-1]),native_samples_all_fields_equal=True,canonical_samples_sha256=hashlib.sha256(oldbytes).hexdigest(),accepted_commands=len(baseline),accepted_commands_all_bits_equal=all(x['Action']==y['Selected'] and x['PhysicalHash']==y['PhysicalHash'] for x,y in zip(baseline,ds)),observations_queues_and_activations_equal=True,old_controller_cpu_s=276.953125,new_controller_cpu_s=result.get('PrefixControllerAttemptCpu'),old_counts=oldcounts,new_counts=counterrows[:len(baseline)])
     prefix['new_exclusive_phases']=dict(sum((Counter({q['Name']:q['CpuSeconds'] for q in d['Profile']['Phases']}) for d in ds[:len(baseline)]),Counter()))
    if 'portfolio' in name:
     same=[];different=[]
     for old,d in zip(baseline,ds):
      (same if old['Action']==d['Selected'] else different).append(d['Time'])
     algorithmic[name]=dict(scope='Commands at corresponding native review indices; after the first different action these are different native states, not a paired algorithm comparison.',same_commands=len(same),different_commands=len(different),first_different_command_s=different[0] if different else None,all_review_count=len(ds),frozen_counterexample='repair38.6-v7.json.gz: same accepted state and own previous policy; exact mode abstains, algorithmic mode certifies all9 leaves')
   energyrows=[a145.interval(a,b) for a,b in zip(ss,ss[1:]) if b.get('Forces') is not None and b.get('Powertrain') is not None]
   for c in cs:
    c['delta_K1_s']=None
    if c['complete']:
     lo=(c['lap']-1)*run['LapMeters']+starts[1 if c['bend']==1 else 5];hi=(c['lap']-1)*run['LapMeters']+starts[4 if c['bend']==1 else 8];entry=a145.point(ss,lo);exit=a145.point(ss,hi);energy=a145.phase(energyrows,entry['time_s'],exit['time_s']);c['energy']=energy['energy'];c['entry_s']=entry['time_s'];c['exit_s']=exit['time_s']
   for lap in range(1,4):
    # Report straights and bends as disjoint reference intervals, including the start.
    for section,(lo,hi) in enumerate(zip([0,starts[1],starts[4],starts[5],starts[8]],[starts[1],starts[4],starts[5],starts[8],run['LapMeters']])):
     entry=a145.point(ss,(lap-1)*run['LapMeters']+lo);exit=a145.point(ss,(lap-1)*run['LapMeters']+hi)
     if entry and exit:segments.append(dict(run=name,lap=lap,section=section,start_s=entry['time_s'],end_s=exit['time_s'],seconds=exit['time_s']-entry['time_s'],planning_cpu_s=sum(d['CpuSeconds'] for d in r['Decisions'] if entry['time_s']<=d['Choice']['Time']<exit['time_s'])))
   gas0=sum(t(b)-t(a) for a,b in zip(ss,ss[1:]) if b['Input']['Active']['Drive']==0);energies={k:sum(q[k] for q in energyrows) for k in energyrows[0] if k.endswith('_J')} if energyrows else {}
   runs.append(dict(name=name,new=new,full_autonomous=full,outcome=run['Outcome'],finish_s=r['FinishSeconds'],laps=r['CompletedLaps'],end_s=run['EndSeconds'],fingerprint=run['Fingerprint'],controller_cpu_s=result.get('ControllerAttemptCpu',run['ControllerCpuSeconds']),planning_cpu_s=sum(costs),experiment_cpu_s=budgetmap.get(name,{}).get('CpuSeconds',result.get('CpuSeconds',run['TotalCpuSeconds'])),exclusive_phases=dict(phases),minimum_gap_m=min(min(s['Inner'],s['Outer']) for s in ss),minimum_sweep_m=min((s['Certificate'] for s in ss if s['Certificate'] is not None),default=None),maximum_beta_deg=max(abs(s['Input']['Rider']['SlideRadians'])*180/math.pi for s in ss),contacts=len(run['Contacts']),speed_min_kph=min(s['Input']['Rider']['Speed']*3.6 for s in ss),speed_max_kph=max(s['Input']['Rider']['Speed']*3.6 for s in ss),closed_gas_s=gas0,path_m=ss[-1]['Input']['Rider']['DistanceMeters'],energy=energies,result=result))
   indices.append(dict(Id=name,File=os.path.relpath(path,D).replace('\\','/'),Variant=run['Variant'],Riders=1,Outcome=run['Outcome'],Duration=run['EndSeconds'],Fingerprint=run['Fingerprint']))
   laps.append(dict(Run=name,RiderId=1,Laps=[dict(Lap=c['Lap'],LapSeconds=c['Time']-(run['Crossings'][k-1]['Time'] if k else 0)) for k,c in enumerate(run['Crossings'])]))
   curves.append(dict(name=name,points=[dict(t=t(s),x=s['Input']['Rider']['Position']['X'],y=s['Input']['Rider']['Position']['Y'],reference=ref(s),speed=s['Input']['Rider']['Speed']*3.6,beta=s['Input']['Rider']['SlideRadians']*180/math.pi,gas=s['Input']['Active']['Drive'],offset=s['Input']['Rider']['Road']['OffsetMeters']) for s in ss[::4]]))
  native=[x for x in runs if x['full_autonomous']];assert native;default=next((x['name'] for x in native if x['outcome']=='completed'),native[0]['name'])
  oldphases=read(R/'docs/experiments/contingency-recourse/analysis.json')['cpu']['contingency151-gate4-A-v2']
  save(D/'analysis.json',dict(full_runs=runs,corners=corners,plan_audits=audits,prefix=prefix,algorithmic_comparison=algorithmic,old_phases=oldphases,source_files=sources,segments=segments,scope='Finite151 scenarios; independent frozen148/149 physical replay with isolated cache; native Box2D execution. No global disturbance or four-rider guarantee.'))
  save(D/'index.json',dict(Title='#152 · Wydajny planner warunkowy',DefaultRun=default,GeometryFile='../causal-yaw-throttle/progressive-b-viewer-geometry.json',Runs=indices,LapMeters=318.77874994277954,RequiredLaps=3,StartReference=0,BendReferences=[starts[k] for k in [1,4,5,8]],Note='Trzy legalne natywne okrążenia od zera wymagają completed i3 laps. Diagnostyka1s i wyniki negatywne są osobno. Skończone scenariusze nie są globalną gwarancją.'))
  save(D/'lap-metrics.json',laps);(D/'comparison.json.gz').write_bytes(gzip.compress(json.dumps(curves,separators=(',',':')).encode(),mtime=0))
  for name,rows in [('decision-counts',allcounts),('segments',segments)]:
   with (D/(name+'.csv')).open('w',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(rows[0]),lineterminator='\n');w.writeheader();w.writerows(rows)
  print([(x['name'],x['outcome'],x['finish_s'],x['controller_cpu_s']) for x in runs]);print('prefix', {k:v for k,v in prefix.items() if k not in ['old_counts','new_counts']})
if __name__=='__main__':main(sys.argv[1] if len(sys.argv)>1 else 'v1')
