"""Saved-data audit only; no predictor, integrator or new world."""
from forensics161 import R,D,OLD,read,save
from pathlib import Path
import csv,gzip,hashlib,importlib.util,json,math,statistics
def module(name,p):
 s=importlib.util.spec_from_file_location(name,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
energy=module('energy161',R/'tools/corner-speed-loss/analyze145.py')
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def native(path):
 run=read(path);label=run['Name'];r=run['Riders'][0];ss=r['Samples'];meta=read(path.with_name(label+'-result.json'));journal=read(D/'journals'/f'{label}.json.gz');end=r['FinishSeconds'] if r['FinishSeconds'] is not None else run['EndSeconds'];rows=[]
 for a,b in zip(ss,ss[1:]):
  if a['Input']['Time']>=end:break
  q=energy.interval(a,b);v,w=a['Input']['Rider'],b['Input']['Rider'];dt=q['dt_s'];mx=(v['Velocity']['X']+w['Velocity']['X'])/2;my=(v['Velocity']['Y']+w['Velocity']['Y'])/2;ax=(w['Velocity']['X']-v['Velocity']['X'])/dt;ay=(w['Velocity']['Y']-v['Velocity']['Y'])/dt;speed=math.hypot(mx,my);road=w['Road']
  q.update(theta_rad=w['Heading'],chi_rad=math.atan2(w['Velocity']['Y'],w['Velocity']['X']),lateral_velocity_mps=w['Velocity']['X']*road['Outward']['X']+w['Velocity']['Y']*road['Outward']['Y'],actual_curvature_per_m=(mx*ay-my*ax)/speed**3 if speed>.1 else 0,dt_weight_s=min(end,b['Input']['Time'])-a['Input']['Time'],swept_m=b['Certificate'],pending=json.dumps(b['Input']['Pending'],separators=(',',':')),wheel_omega=b['Input']['Omega'],distance_xy_m=w['DistanceMeters']);rows.append(q)
 if rows:
  folder=D/'telemetry';folder.mkdir(exist_ok=True)
  with gzip.open(folder/f'{label}.csv.gz','wt',encoding='utf-8',newline='') as f:writer=csv.DictWriter(f,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
 lap=run['LapMeters'];bends=[];straights=[]
 for li in range(3):
  for bi,(lo,hi) in enumerate([(35,132.38937497138977),(194.38937497138977,291.77874994277954)]):
   lo+=li*lap;hi+=li*lap;e=energy.point(ss,lo);x=energy.point(ss,hi);part=[s for s in ss if lo<=s['Input']['Rider']['TotalReferenceMeters']<=hi]
   bends.append(dict(lap=li+1,bend=2*li+bi+1,status='complete' if e and x else 'incomplete' if e else 'unreached',entry=e,exit=x,minimum_kph=min((s['Input']['Rider']['Speed']*3.6 for s in part),default=None),duration_s=x['time_s']-e['time_s'] if e and x else None))
  for si,(lo,hi) in enumerate([(0,35),(132.38937497138977,194.38937497138977),(291.77874994277954,318.77874994277954)]):
   part=[q for q in rows if li*lap+lo<=q['reference_m']<=li*lap+hi];duration=sum(q['dt_weight_s'] for q in part);straights.append(dict(lap=li+1,section=si+1,observed_s=duration,mean_kph=sum(q['speed_kph']*q['dt_weight_s'] for q in part)/duration if duration else None,maximum_kph=max((q['speed_kph'] for q in part),default=None)))
 count=0;profiles={};rejections=[];generations=[];origins={};failures=[]
 for p in sorted((D/'policies'/label).glob('*.json.gz')):
  q=read(p);review=q['Review']
  if q['Error'] is None:
   assert review['ConditionalCertificate'] and review['Independent'] and len(review['Tree'])==13 and len(review['Terminals'])==9
   assert review['Tree'][0]['Action']==journal[count]['Action']==r['Decisions'][count]['Choice']['Action'];count+=1
  else:failures.append(dict(time=q['Observed']['Input']['Time'],reason=q['Error'],source=p.relative_to(R).as_posix()))
  origin=q.get('SelectedOrigin') or 'none';origins[origin]=origins.get(origin,0)+1
  if q.get('Outcomes'):generations.append(dict(time=q['Observed']['Input']['Time'],selected_origin=origin,outcomes=q['Outcomes']))
  if review:
   rejections.extend(dict(time=review['Time'],origin=x['Origin'],reason=x['Reason']) for x in review['Attempts'] if not x['Legal'])
   for phase in review['Profile']['Phases']:profiles[phase['Name']]=profiles.get(phase['Name'],0)+phase['CpuSeconds']
 assert count==meta['AcceptedDecisions'];conditions=read(R/'docs/experiments/robust-three-lap/final-v6/scenarios-frozen.json')['Scenarios'];index=next(i for i,s in enumerate(conditions) if s['Id']==meta['Scenario']['Id']);base_path=R/'docs/experiments/robust-three-lap/final-v6/native'/f'validation-v6-new-{index}-result.json';base=read(base_path)
 assert meta['Initial']==meta['ExpectedInitial']==base['Initial'];assert meta['Spec']==base['Spec'];assert meta['ActualMassKg']==base['ActualMassKg']==147;assert meta['ActualInertiaKgMetersSquared']==base['ActualInertiaKgMetersSquared'];assert ss[0]['Input']['Rider']==meta['Initial']['Input']['Rider'];assert ss[0]['Input']['Time']==meta['Initial']['Input']['Time']==0;assert ss[0]['Input']['Omega']==meta['Initial']['Input']['Omega']==0;assert ss[0]['Input']['Steering']==meta['Initial']['Input']['Steering']==0;assert ss[0]['CrankOmega']==meta['Initial']['CrankOmega']
 if meta['Validation']:
  freeze=read(D/'controller-freeze.json')
  import zipfile
  with zipfile.ZipFile(D/'sources'/f'{label}.zip') as z:
   for name,h in freeze['Sources'].items():assert hashlib.sha256(z.read(name)).hexdigest().upper()==h
 pair=dict(initial_state_exact=True,initial_comparison_phase='Full pre-first-decision observed state matches archived158 exactly. Sample0 is post-first-review (pending/NextReview updated); its body, wheel, actuator, crank and time remain equal to the stopped initial state.',baseline_finish_s=base['FinishSeconds'],new_finish_s=r['FinishSeconds'],delta_s=r['FinishSeconds']-base['FinishSeconds'] if meta['Pass'] else None,baseline_source=base_path.relative_to(R).as_posix(),baseline_sha256=digest(base_path));laps=[];prior=0
 for c in run['Crossings']:laps.append(c['Time']-prior);prior=c['Time']
 cpu=[q['CallbackCpuSeconds'] for q in journal];duration=sum(q['dt_weight_s'] for q in rows)
 summary=dict(label=label,pass_=meta['Pass'],validation=meta['Validation'],scenario=meta['Scenario'],completed_laps=r['CompletedLaps'],finish_s=r['FinishSeconds'],observed_until_s=run['EndSeconds'],outcome=run['Outcome'],samples=len(ss),laps_s=laps,six_bends=bends,straights=straights,pair=pair,distance_xy_m=ss[-1]['Input']['Rider']['DistanceMeters']-ss[0]['Input']['Rider']['DistanceMeters'],minimum_pose_m=meta['MinimumPoseMeters'],minimum_swept_m=meta['MinimumSweepMeters'],maximum_beta_deg=meta['MaximumBetaDegrees'],contacts=meta['Contacts'],abstain=meta['Abstain'],accepted_decisions=count,rejected_plans=rejections,failures=failures,generations=generations,selected_origins=origins,cpu_process_s=meta['ChargedCpuSeconds'],cpu_callbacks=dict(total=sum(cpu),median=statistics.median(cpu) if cpu else None,maximum=max(cpu,default=None)),cpu_phases=profiles,source=path.relative_to(R).as_posix(),sha256=digest(path),mean_front_util=sum(q['front_util']*q['dt_weight_s'] for q in rows)/duration if duration else None,mean_rear_util=sum(q['rear_util']*q['dt_weight_s'] for q in rows)/duration if duration else None)
 display=[]
 for i in sorted(set(range(0,len(ss),5))|{len(ss)-1}):
  s=ss[i];v=s['Input']['Rider'];road=v['Road'];display.append(dict(index=i,t=s['Input']['Time'],x=v['Position']['X'],y=v['Position']['Y'],s=v['TotalReferenceMeters'],heading=v['Heading'],speed=v['Speed']*3.6,beta=math.degrees(v['SlideRadians']),yaw=v['AngularVelocity'],gas=s['Input']['Active']['Drive'],steering=math.degrees(s['Input']['Steering']),wheel=s['Input']['Omega'],crank=s['CrankOmega'],gap=min(s['Inner'],s['Outer']),swept=s['Certificate'],offset=road['OffsetMeters'],lateral=v['Velocity']['X']*road['Outward']['X']+v['Velocity']['Y']*road['Outward']['Y']))
 return summary,dict(name=label,scenario=meta['Scenario']['Id'],status='S3' if meta['Pass'] else 'DNF',source=summary['source'],sha256=summary['sha256'],fingerprint=run['Fingerprint'],finish=r['FinishSeconds'],samples=display)
def main():
 diagnostics=[]
 for p in sorted((D/'diagnostics').glob('*.json.gz')):
  q=read(p);summary=q['Summary'];candidates=[]
  for c in q['Candidates']:
   pts=c['Points'];candidates.append(dict(origin=c['Origin'],legal=c['Safety']['Legal'],arrival=c['Arrival'],end_time=pts[-1]['Time'] if pts else None,end_reference=pts[-1]['Rider']['TotalReferenceMeters'] if pts else None,actions_sha256=hashlib.sha256(json.dumps(c['Actions'],sort_keys=True).encode()).hexdigest(),minimum_offset=min((p['Rider']['Road']['OffsetMeters'] for p in pts),default=None),maximum_offset=max((p['Rider']['Road']['OffsetMeters'] for p in pts),default=None),maximum_speed_kph=max((p['Rider']['Speed']*3.6 for p in pts),default=None)))
  summary.update(candidates=candidates,source=p.relative_to(R).as_posix());diagnostics.append(summary)
 natives=[];replays=[]
 for p in sorted((D/'native').glob('*.json.gz'),key=lambda p:(p.stem!='native-dev.json',p.name)):a,b=native(p);natives.append(a);replays.append(b)
 held=[n for n in natives if n['validation']];expected=read(R/'docs/experiments/robust-three-lap/final-v6/scenarios-frozen.json')['Scenarios'][:5];exact=sorted(n['scenario']['Id'] for n in held)==sorted(s['Id'] for s in expected);times=[n['finish_s'] for n in held if n['pass_']];passed=exact and len(times)==5 and all(n['pair']['delta_s']<0 for n in held)
 certificates=[]
 for p in sorted((D/'certificates').glob('*.json.gz')):
  q=read(p);r=q['Review'];certificates.append(dict(label=q['Label'],fixture=q['Fixture'],pass_=q['Error'] is None and r['ConditionalCertificate'] and r['Independent'] and len(r['Tree'])==13 and len(r['Terminals'])==9,cpu_s=q['CpuSeconds'],error=q['Error']))
 result=dict(base='e8866e91c4c3818b8ca836260883ca847ccc4a8a',verdict='PASS' if passed else 'PARTIAL',diagnostics=diagnostics,certificates=certificates,native=natives,new_native_worlds=len(natives),held_trials=len(held),held_set_exact=exact,held_dnf=sum(not n['pass_'] for n in held),median_finish_s=statistics.median(times) if times else None,finish_range_s=[min(times),max(times)] if times else None,budget=read(D/'budget-journal.json'),historical_forensics='historical-forensics.json.gz',latest_found_recoverable_time=67 if any(q['Legal'] and q['InitialTime']==67 for q in diagnostics) else None)
 save(D/'analysis.json',result);old_replay=read(OLD/'native-replay.json.gz');historical=old_replay['runs'][0];historical['name']='historical160-DNF';historical['scenario']='history160 nominal-B';save(D/'native-replay.json.gz',dict(scope='Actual native states only; original indices; historical160 stops67s and is never extrapolated.',geometry=old_replay['geometry'],runs=[historical]+replays));print(json.dumps(dict(verdict=result['verdict'],native=len(natives),held=len(held),median=result['median_finish_s'])))
if __name__=='__main__':main()
