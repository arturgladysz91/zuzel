"""Audit ONLY saved native worlds; forecasts never count as a finish.
All failed worlds and zero-step ABSTAINs remain in the tables.
"""
from pathlib import Path
import csv,gzip,hashlib,importlib.util,json,math,statistics,sys,time,zipfile
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/robust-three-lap'
def read(p):
 p=Path(p);return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
def write(p,x):
 p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(x,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def csvwrite(p,rows,fields=None):
 p.parent.mkdir(parents=True,exist_ok=True)
 with p.open('w',encoding='utf-8',newline='') as f:
  w=csv.DictWriter(f,fieldnames=fields or list(rows[0]));w.writeheader();w.writerows(rows)
def module(name,p):
 s=importlib.util.spec_from_file_location(name,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
old=module('analysis156',R/'tools/line-throttle-physics/analyze156.py')
energy=module('energy145',R/'tools/corner-speed-loss/analyze145.py')

def policy_read(folder,label,name):
 archive=folder/'policies'/f'{label}.zip'
 if archive.exists():
  with zipfile.ZipFile(archive) as z:return json.loads(gzip.decompress(z.read(name)))
 return read(folder/'policies'/label/name)

def policy_items(folder,label):
 archive=folder/'policies'/f'{label}.zip'
 if archive.exists():
  with zipfile.ZipFile(archive) as z:
   for name in sorted(z.namelist()):yield name,json.loads(gzip.decompress(z.read(name)))
 else:
  for p in sorted((folder/'policies'/label).glob('*.json.gz')):yield p.name,read(p)
def source_case(folder,label,run):
 p=folder/'native'/f'{label}-result.json'
 if p.exists():return read(p)
 # v5 collector crashed AFTER preserving native + journal when there was no
 # swept interval. Reconstruct a DNF, never a certificate, directly from raw.
 journal=read(folder/'journals'/f'{label}.json.gz');r=run['Riders'][0];index=int(label.rsplit('-',1)[1]);condition=read(folder/'scenarios-frozen.json')['Scenarios'][index]
 return dict(Label=label,Baseline='-base-' in label,Validation=True,Scenario=condition,RequiredLaps=run['Options']['RequiredLaps'],CompletedLaps=r['CompletedLaps'],FinishSeconds=r['FinishSeconds'],Pass=False,Complete=False,MinimumPoseMeters=min(min(s['Inner'],s['Outer']) for s in r['Samples']),MinimumSweepMeters=None,MaximumBetaDegrees=max(abs(s['Input']['Rider']['SlideRadians'])*180/math.pi for s in r['Samples']),Contacts=len(run['Contacts']),AcceptedDecisions=sum(x['Accepted'] for x in journal),Abstain=sum(not x['Accepted'] for x in journal),Outcome=run['Outcome'],Fingerprint=run['Fingerprint'],CollectorRecovery='Empty swept list after actual time-zero ABSTAIN; raw native+journal preserved. DNF explicitly retained.')
def audit(folder,p):
 run=read(p);name=run['Name'];meta=source_case(folder,name,run);r=run['Riders'][0];ss=r['Samples'];journal=read(folder/'journals'/f'{name}.json.gz');finish=r['FinishSeconds'];end=finish if finish is not None else run['EndSeconds'];lap=run['LapMeters']
 points=[]
 for a,b in zip(ss,ss[1:]):
  dt=max(0,min(b['Input']['Time'],end)-a['Input']['Time'])
  if dt<=0:continue
  q=energy.interval(a,b);q['dt_weight_s']=dt;q['actual_curvature_per_m']=old.curvature(a,b);q['sweep_m']=b['Certificate'];points.append(q)
 def duty(lo,hi):
  zero=partial=full=0
  for a,b in zip(ss,ss[1:]):
   dt=max(0,min(b['Input']['Time'],hi)-max(a['Input']['Time'],lo));gas=a['Input']['Active']['Drive']
   if gas<=1e-9:zero+=dt
   elif gas>=1-1e-9:full+=dt
   else:partial+=dt
  return dict(zero_s=zero,partial_s=partial,full_s=full,zero_share=zero/(hi-lo) if hi>lo else None,partial_share=partial/(hi-lo) if hi>lo else None,full_share=full/(hi-lo) if hi>lo else None)
 # Existing unchanged G0 section starts; crossings are from actual native data.
 bend_start=35.;bend_end=132.38937497138977;half=lap/2
 bends=[];straights=[]
 for lap_index in range(3):
  for bend in range(2):
   entry=lap_index*lap+bend_start+bend*half;exit_=lap_index*lap+bend_end+bend*half;e=old.interpolate(ss,entry);x=old.interpolate(ss,exit_);rows=[q for q in points if entry<=q['reference_m']<=exit_]
   bends.append(dict(trial=name,lap=lap_index+1,bend=2*lap_index+bend+1,status='complete' if e and x else 'incomplete' if e else 'not reached',entry_kph=e['speed'] if e else None,minimum_kph=min([q['speed_kph'] for q in rows]+([e['speed'],x['speed']] if e and x else []),default=None),exit_kph=x['speed'] if x else None,time_s=x['time']-e['time'] if e and x else None,gas=duty(e['time'],x['time']) if e and x else None,max_beta_deg=max((abs(q['beta_deg']) for q in rows),default=None),max_yaw_rads=max((abs(q['yaw_rads']) for q in rows),default=None),max_wheelspin=max((abs(q['wheel_slip']) for q in rows),default=None),minimum_sweep_m=min((q['sweep_m'] for q in rows),default=None)))
  for straight,(lo,hi) in enumerate([(0,bend_start),(bend_end,bend_start+half),(bend_end+half,lap)]):
   lo+=lap_index*lap;hi+=lap_index*lap;rows=[q for q in points if lo<=q['reference_m']<=hi];straights.append(dict(trial=name,lap=lap_index+1,straight=straight+1,maximum_kph=max((q['speed_kph'] for q in rows),default=None),mean_kph=sum(q['speed_kph']*q['dt_weight_s'] for q in rows)/sum(q['dt_weight_s'] for q in rows) if rows else None,status='complete' if old.interpolate(ss,hi) else 'incomplete' if rows else 'not reached'))
 pose=min(min(s['Inner'],s['Outer']) for s in ss);sweeps=[s['Certificate'] for s in ss[1:] if s['Certificate'] is not None];sweep=min(sweeps,default=None);beta=max(abs(s['Input']['Rider']['SlideRadians'])*180/math.pi for s in ss);full=r['CompletedLaps']==3 and finish is not None and len(run['Crossings'])==3
 try:first_policy=policy_read(folder,name,'decision-0000.json.gz')
 except (FileNotFoundError,KeyError):first_policy=policy_read(folder,name,'abstain.json.gz')
 initial=first_policy['Observed'];iv=initial['Input'];stopped=iv['Time']==0 and iv['Rider']['Speed']==0 and not iv['Pending'] and iv['Active']==dict(SteeringRadians=0,Drive=0)
 initial_hash=hashlib.sha256(json.dumps(initial,sort_keys=True,separators=(',',':')).encode()).hexdigest()
 assert iv['Rider']==ss[0]['Input']['Rider'] and initial['CrankOmega']==ss[0]['CrankOmega']
 if 'ExpectedInitial' in meta:assert initial==meta['Initial']==meta['ExpectedInitial']
 if 'ActualMassKg' in meta:assert meta['ActualMassKg']==147 and abs(meta['ActualInertiaKgMetersSquared']-52.72097)<1e-5
 assert len(r['Decisions'])==sum(q['Accepted'] for q in journal)
 for command in r['Activations']:assert abs(command['ActivationAt']-command['ObservedAt']-.24)<1e-9
 legal=stopped and full and run['Options']['RequiredLaps']==3 and run['Outcome']=='completed' and not run['Contacts'] and pose>=.5 and len(sweeps)==len(ss)-1 and sweep is not None and sweep>=.5 and beta<=45 and not any(not q['Accepted'] for q in journal)
 lap_times=[None,None,None];previous=0
 for c in run['Crossings']:
  if 1<=c['Lap']<=3:lap_times[c['Lap']-1]=c['Time']-previous;previous=c['Time']
 callback=[q['CallbackCpuSeconds'] for q in journal];planner=[q['PlannerCpuSeconds'] for q in journal if q['PlannerCpuSeconds'] is not None];distance=old.interpolate(ss,3*lap)['distance']-ss[0]['Input']['Rider']['DistanceMeters'] if full else ss[-1]['Input']['Rider']['DistanceMeters']-ss[0]['Input']['Rider']['DistanceMeters'];rejected=repaired=independent=0;profiles={};certified=0
 full_k1=prefix_hits=joint_hits=joint_misses=0
 for policy,q in policy_items(folder,name):
  review=q.get('Review')
  counters=q.get('Counters') or {};full_k1=max(full_k1,counters.get('FullK1',0));prefix_hits=max(prefix_hits,q.get('PrefixCacheHits',0));joint_hits=max(joint_hits,q.get('JointMemoHits',0));joint_misses=max(joint_misses,q.get('JointMemoMisses',0))
  if not review:continue
  rejected+=sum(not x['Legal'] for x in review['Attempts'])+len(review['RejectedTrees']);repaired+=bool(review['ConditionalCertificate'] and any(not x['Legal'] for x in review['Attempts']));independent=max(independent,q['Counters']['IndependentComparisons'])
  if q['Error'] is None:
   assert review['ConditionalCertificate'] and review['Independent'] and len(review['Tree'])==13 and len(review['Terminals'])==9
   index=int(policy.split('-')[1].split('.')[0]);native=r['Decisions'][index]['Choice'];event=journal[index]
   assert native['Time']==event['Time']==q['Observed']['Input']['Time']
   # The inherited planner starts the forecast review epoch at the actual callback
   # time. Only NextReview is canonicalized; every body/pending/activation bit stays.
   canonical={**q['Observed'],'Input':{**q['Observed']['Input'],'NextReview':q['Observed']['Input']['Time']}}
   assert native['Action']==event['Action']==review['Tree'][0]['Action'] and canonical==review['Tree'][0]['Observed']
   assert all(n['Execution']['Legal'] and n['Execution']['SweepLowerBoundMeters']>=.5 and n['Execution']['MaximumBetaDegrees']<=45 for n in review['Tree'])
   if meta['Pass'] and review['Goal']>=3*lap-.001:assert review['SafetyGoal']>=3*lap+32-1e-6
   certified+=1
  for phase in review['Profile']['Phases']:profiles[phase['Name']]=profiles.get(phase['Name'],0)+phase['CpuSeconds']
 cpu=lambda xs:dict(mean=statistics.mean(xs) if xs else None,maximum=max(xs,default=None),median=statistics.median(xs) if xs else None)
 summary=dict(label=name,phase=folder.name,baseline=meta['Baseline'],validation=meta['Validation'],scenario=meta.get('Scenario',{}),pass_=legal,declared_pass=meta['Pass'],complete=full,completed_laps=r['CompletedLaps'],lap_times_s=lap_times,finish_s=finish if full else None,observed_until_s=run['EndSeconds'],outcome=run['Outcome'],distance_m=distance,minimum_pose_m=pose,minimum_sweep_m=sweep,max_beta_deg=beta,max_yaw_rads=max(abs(s['Input']['Rider']['AngularVelocity']) for s in ss),gas=duty(0,end),abstain=sum(not q['Accepted'] for q in journal),accepted_decisions=meta['AcceptedDecisions'],certified_decisions=certified,rejected_plans=rejected,repaired_plans=repaired,independent_comparisons=independent,planner_cpu=cpu(planner),callback_cpu=cpu(callback),callback_cpu_total=sum(callback),process_cpu_s=run['TotalCpuSeconds'],native_and_other_cpu_s=max(0,run['TotalCpuSeconds']-sum(callback)),profiles=profiles,collector_recovery=meta.get('CollectorRecovery'),fingerprint=run['Fingerprint'],source=str(p.relative_to(R)).replace('\\','/'))
 assert certified==meta['AcceptedDecisions']==sum(q['Accepted'] for q in journal)
 freeze_unchanged=None
 if meta['Validation']:
  freeze=read(folder/'controller-freeze.json')
  assert hashlib.sha256((folder/'scenarios-frozen.json').read_bytes()).hexdigest().upper()==freeze['ScenarioHash']
  with zipfile.ZipFile(folder/'sources'/f'{name}.zip') as archive:
   for source,expected in freeze['Sources'].items():assert hashlib.sha256(archive.read(source)).hexdigest().upper()==expected,(name,source)
  freeze_unchanged=True
 ledger=read(folder/'budget.json');charge=next(r['CpuSeconds'] for r in ledger if r['Name']==name)
 budget_row=next(r for r in ledger if r['Name']==name);budget_exhausted=charge-1>=budget_row['OperationCpuCap'];legal=legal and not budget_exhausted
 summary.update(pass_=legal,budget_exhausted=budget_exhausted)
 summary.update(stopped_start=stopped,initial_hash=initial_hash,frozen_sources_verified=freeze_unchanged,charged_cpu_s=charge,contacts=len(run['Contacts']),safety_violation=pose<.5 or (sweep is not None and sweep<.5) or beta>45 or bool(run['Contacts']),full_k1=full_k1,prefix_cache_hits=prefix_hits,joint_memo_hits=joint_hits,joint_memo_misses=joint_misses,max_wheelspin=max((abs(q['wheel_slip']) for q in points),default=None),forces_peak_N={key:max((abs(q[key]) for q in points),default=None) for key in ['front_fx_body_N','front_fy_body_N','rear_fx_N','rear_fy_N']})
 assert summary['declared_pass']==legal
 display=[];indices=[];next_time=0
 for i,s in enumerate(ss):
  if i not in (0,len(ss)-1) and s['Input']['Time']<next_time:continue
  next_time=s['Input']['Time']+.05;v=s['Input']['Rider'];indices.append(i);display.append(dict(t=s['Input']['Time'],x=v['Position']['X'],y=v['Position']['Y'],s=v['TotalReferenceMeters'],heading=v['Heading'],speed=v['Speed']*3.6,beta=v['SlideRadians']*180/math.pi,drive=s['Input']['Active']['Drive'],gap=min(s['Inner'],s['Outer'])))
 replay=dict(name=name,phase=folder.name,baseline=meta['Baseline'],validation=meta['Validation'],scenario=meta.get('Scenario',{}),status='PASS S3' if legal else 'DNF',outcome=run['Outcome'],finish_s=finish,start=0,samples=display,source=summary['source'],source_indices=indices,display_only_decimation=True)
 return summary,bends,straights,points,replay

def series(rows,manifest):
 expected=manifest['Scenarios'];ids=[r['scenario']['Id'] for r in rows];planned=[s['Id'] for s in expected];times=[r['finish_s'] for r in rows if r['pass_']]
 independent=[r for r in rows if r['scenario'].get('Role','independent')=='independent'];required_independent=manifest.get('MinimumIndependentTrials',5)
 complete_set=len(ids)==len(set(ids))==len(planned) and sorted(ids)==sorted(planned) and len({(s['Gate'],s['OffsetMeters']) for s in expected})==len(planned)
 held=[r for r in rows if r['scenario'].get('Held',False)];safety_failed=any(r.get('safety_violation',False) for r in rows);all_pass=not safety_failed and complete_set and len(independent)>=required_independent and len(rows)>=5 and all(r['pass_'] for r in rows)
 return dict(attempts=len(rows),planned_trials=len(planned),complete_set=complete_set,completed=sum(r['pass_'] for r in rows),dnf=sum(not r['pass_'] for r in rows),median_completed_s=statistics.median(times) if times else None,minimum_completed_s=min(times,default=None),maximum_completed_s=max(times,default=None),range_completed_s=max(times)-min(times) if times else None,independent_attempts=len(independent),independent_completed=sum(r['pass_'] for r in independent),held_attempts=len(held),held_completed=sum(r['pass_'] for r in held),all_pass=all_pass,status='FAIL' if safety_failed else 'PASS' if all_pass else 'PARTIAL')
def main():
 tag=sys.argv[1] if len(sys.argv)>1 else 'analysis-v1';entered=time.process_time();trials=[];bends=[];straights=[];replays=[];folders=[D]+[p for p in D.iterdir() if p.is_dir() and (p/'native').exists()]
 for folder in folders:
  for p in sorted((folder/'native').glob('*.json.gz')):
   row,bs,st,intervals,replay=audit(folder,p);trials.append(row);bends+=bs;straights+=st;replays.append(replay)
   if intervals:
    out=folder/'intervals'/f"{row['label']}.csv.gz";out.parent.mkdir(exist_ok=True)
    with gzip.open(out,'wt',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(intervals[0]));w.writeheader();w.writerows(intervals)
  # All versions, including raw recovered time-zero DNFs, remain visible.
 groups=[];pairs=[]
 for phase in sorted({r['phase'] for r in trials}):
  folder=next(p for p in folders if p.name==phase);manifest=read(folder/'scenarios-frozen.json')
  for baseline in [False,True]:
   rows=[r for r in trials if r['phase']==phase and r['baseline']==baseline and r['validation']];groups.append(dict(phase=phase,baseline=baseline,**series(rows,manifest)))
  for condition in manifest['Scenarios']:
   a=next((r for r in trials if r['phase']==phase and r['validation'] and r['baseline'] and r['scenario']['Id']==condition['Id']),None);b=next((r for r in trials if r['phase']==phase and r['validation'] and not r['baseline'] and r['scenario']['Id']==condition['Id']),None)
   if not a or not b:continue
   assert a['initial_hash']==b['initial_hash'],('Unpaired initial states',phase,condition['Id'])
   pairs.append(dict(phase=phase,scenario=condition['Id'],identical_initial=True,base_pass=a['pass_'],new_pass=b['pass_'],base_time_s=a['finish_s'],new_time_s=b['finish_s'],time_gain_s=a['finish_s']-b['finish_s'] if a['pass_'] and b['pass_'] else None,base_cpu_s=a['process_cpu_s'],new_cpu_s=b['process_cpu_s']))
 write(D/'analysis.json',dict(trials=trials,bends=bends,straights=straights,groups=groups,pairs=pairs,scope='All actual native attempts retained. Medians describe completed races only and are always accompanied by the DNF count; no time gain when baseline has no S completion.'))
 csvwrite(D/'trials.csv',[{k:(json.dumps(v,ensure_ascii=False) if isinstance(v,(list,dict)) else v) for k,v in r.items() if k!='profiles'} for r in trials]);csvwrite(D/'six-bends.csv',[{**{k:v for k,v in r.items() if k!='gas'},'gas':json.dumps(r['gas'])} for r in bends]);csvwrite(D/'straights.csv',straights);write(D/'replay.json',dict(runs=replays,scope='Display-decimated ACTUAL native positions, each source index retained. Full100Hz/event logs own safety and timing. No future forecasts shown as native.'))
 write(D/'analysis-costs'/f'{tag}.json',dict(Name=tag,CpuSeconds=time.process_time()-entered+1,NativeWorlds=0,Complete=True));print('Audited',len(trials),'actual worlds; analysis CPU',time.process_time()-entered)
if __name__=='__main__':
 tag=sys.argv[1] if len(sys.argv)>1 else 'analysis-v1';cost=D/'analysis-costs'/f'{tag}.json';entered=time.process_time()
 if cost.exists():raise FileExistsError('Unique analysis label required: '+tag)
 try:main()
 except Exception as error:
  write(cost,dict(Name=tag,CpuSeconds=time.process_time()-entered+1,NativeWorlds=0,Complete=True,Outcome='FAILED analysis retained: '+repr(error)));raise
