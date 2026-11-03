"""Read-only saved evidence analysis. Never invokes a simulator or planner."""
from pathlib import Path
import csv,gzip,hashlib,importlib.util,json,math,statistics,subprocess,sys,zipfile
from collections import Counter
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/feasible-speed-profile';OLD=R/'docs/experiments/trajectory-continuation'
sys.path.insert(0,str(R/'tools/s3-performance'))
from bundle162 import policies,read as bundle_read,verify
s=importlib.util.spec_from_file_location('energy164',R/'tools/corner-speed-loss/analyze145.py');energy=importlib.util.module_from_spec(s);s.loader.exec_module(energy)
BASE='75c208c28225005589b5ad84eb5825c1abcf3e1b'
def read(p):
 b=Path(p).read_bytes();return json.loads(gzip.decompress(b) if str(p).endswith('.gz') else b)
def save(p,data):
 Path(p).write_text(json.dumps(data,indent=2,ensure_ascii=False,allow_nan=False)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def bounds(samples,lap):
 bends=[];straights=[]
 for li in range(3):
  for bi,(lo,hi) in enumerate([(35,132.38937497138977),(194.38937497138977,291.77874994277954)]):
   lo+=li*lap;hi+=li*lap;e=energy.point(samples,lo);x=energy.point(samples,hi);part=[s for s in samples if lo<=s['Input']['Rider']['TotalReferenceMeters']<=hi]
   bends.append(dict(lap=li+1,bend=2*li+bi+1,status='complete' if e and x else 'incomplete' if e else 'unreached',entry=e,exit=x,minimum_kph=min((s['Input']['Rider']['Speed']*3.6 for s in part),default=None),duration_s=x['time_s']-e['time_s'] if e and x else None))
  for si,(lo,hi) in enumerate([(0,35),(132.38937497138977,194.38937497138977),(291.77874994277954,318.77874994277954)]):
   lo+=li*lap;hi+=li*lap;e=energy.point(samples,lo);x=energy.point(samples,hi);part=[s for s in samples if lo<=s['Input']['Rider']['TotalReferenceMeters']<=hi]
   duration=x['time_s']-e['time_s'] if e and x else None
   straights.append(dict(lap=li+1,section=si+1,status='complete' if e and x else 'incomplete' if e else 'unreached',duration_s=duration,mean_kph=(x['distance_m']-e['distance_m'])/duration*3.6 if duration and duration>0 else None,maximum_kph=max((s['Input']['Rider']['Speed']*3.6 for s in part),default=None)))
 return bends,straights
def analyze_native(path,base):
 assert verify(path)>0
 run=bundle_read(path,'native.json');meta=read(path.with_name(path.stem+'-result.json'));journal=bundle_read(path,'journal.json');ss=run['Riders'][0]['Samples'];finish=meta['FinishSeconds'];end=finish if finish is not None else run['EndSeconds'];rows=[]
 for a,b in zip(ss,ss[1:]):
  if a['Input']['Time']>=end:break
  q=energy.interval(a,b);v=b['Input']['Rider'];engine=b['Powertrain']['Engine'];q.update(dt_weight_s=min(end,b['Input']['Time'])-a['Input']['Time'],theta_rad=v['Heading'],chi_rad=math.atan2(v['Velocity']['Y'],v['Velocity']['X']),lateral_velocity_mps=sum(v['Velocity'][k]*v['Road']['Outward'][k] for k in ['X','Y']),swept_m=b['Certificate'],interval_active_gas=a['Input']['Active']['Drive'],pending=json.dumps(b['Input']['Pending'],separators=(',',':')),engine_available_wheel_torque_Nm=engine['AvailableWheelTorqueNewtonMeters'],engine_available_power_W=engine['CrankPowerWatts'],combustion_power_W=b['Powertrain']['CombustionWorkJoules']/q['dt_s'])
  available=q['engine_available_wheel_torque_Nm'];q['drive_torque_fraction']=q['drive_torque_Nm']/available if available else None;q['crank_torque_fraction']=b['Powertrain']['CrankTorque']/engine['CrankTorqueNewtonMeters'] if engine['CrankTorqueNewtonMeters'] else None;rows.append(q)
 if rows:
  folder=D/'telemetry';folder.mkdir(exist_ok=True)
  with gzip.open(folder/(path.stem+'.csv.gz'),'wt',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
 count=0;failures=[];rejected=[];origins={};phases={};reserves=[];hierarchy=Counter();final_safety_goal=None
 for key,q,raw in policies(path):
  review=q.get('Review');origin=q.get('SelectedOrigin') or 'none';origins[origin]=origins.get(origin,0)+1
  if q['Error'] is None:
   assert review and review['ConditionalCertificate'] and review['Independent'] and len(review['Tree'])==13 and len(review['Terminals'])==9
   assert review['Tree'][0]['Action']==journal[count]['Action']==run['Riders'][0]['Decisions'][count]['Choice']['Action'];count+=1
   final_safety_goal=review['SafetyGoal']
   for terminal in review['Terminals']:
    t=review['Tapes'][terminal['Tape']];assert t['Safety']['Legal'] and t['Safety']['SweepLowerBoundMeters']>=.5 and t['Safety']['MinimumGapMeters']>=.5
    reserves.append(t['SafetyGoal'])
  else:failures.append(dict(key=key,time=q['Observed']['Input']['Time'],reason=q['Error']))
  if review:
   hierarchy.update({k:review['Counts']['Hierarchy'][k] for k in ['PrimitiveCandidates','PhysicalSteps','FullPlans','GenerationBatches']})
   rejected.extend(dict(time=review['Time'],origin=a['Origin'],reason=a['Reason']) for a in review['Attempts'] if not a['Legal'])
   for phase in review['Profile']['Phases']:phases[phase['Name']]=phases.get(phase['Name'],0)+phase['CpuSeconds']
 assert count==meta['AcceptedDecisions'];assert meta['Initial']==read(OLD/'native'/(meta['BaselineLabel']+'-result.json'))['Initial']
 if meta['Pass']:assert final_safety_goal>=3*run['LapMeters']+32
 bends,straights=bounds(ss,run['LapMeters']);duration=sum(q['dt_weight_s'] for q in rows)
 mean=lambda key:sum(q[key]*q['dt_weight_s'] for q in rows)/duration if duration else None
 available_rows=[q for q in rows if q['drive_torque_fraction'] is not None];available_duration=sum(q['dt_weight_s'] for q in available_rows)
 stats=dict(mean_speed_kph=mean('speed_kph'),maximum_speed_kph=max((q['speed_kph'] for q in rows),default=None),mean_front_util=mean('front_util'),mean_rear_util=mean('rear_util'),mean_active_gas=mean('interval_active_gas'),mean_requested_gas=mean('requested_gas'),maximum_abs_yaw=max((abs(q['yaw_rads']) for q in rows),default=None),maximum_abs_wheel_slip=max((abs(q['wheel_slip']) for q in rows),default=None),mean_drive_torque_fraction=sum(q['drive_torque_fraction']*q['dt_weight_s'] for q in available_rows)/available_duration if available_duration else None)
 ledger=next(x for x in read(D/'budget-journal.json') if x['Label']==meta['Label']);baseline_cpu=base['cpu_process_s'];lap_times=[];prior=0
 def straight_mean(data):
  selected=[q for q in data if (q['reference_m']%run['LapMeters']<35 or 132.38937497138977<=q['reference_m']%run['LapMeters']<194.38937497138977 or q['reference_m']%run['LapMeters']>=291.77874994277954)]
  duration=sum(q['dt_weight_s'] for q in selected)
  return sum(q['speed_kph']*q['dt_weight_s'] for q in selected)/duration if duration else None
 with gzip.open(OLD/'telemetry'/(meta['BaselineLabel']+'.csv.gz'),'rt',encoding='utf-8') as f:base_rows=[{k:float(q[k]) for k in ['reference_m','speed_kph','dt_weight_s']} for q in csv.DictReader(f)]
 stats['straight_mean_kph']=straight_mean(rows);stats['baseline_straight_mean_kph']=straight_mean(base_rows)
 stats['mean_combustion_power_W']=mean('combustion_power_W');stats['mean_available_engine_power_W']=mean('engine_available_power_W');stats['baseline_mean_front_util']=base['mean_front_util'];stats['baseline_mean_rear_util']=base['mean_rear_util']
 baseline_journal=read(OLD/'journals'/(meta['BaselineLabel']+'.json.gz'));lo=2*run['LapMeters']+35;hi=2*run['LapMeters']+132.38937497138977
 fifth_bend_cpu=dict(reference_start_m=lo,reference_end_m=hi,new_callback_cpu_s=sum(q['CallbackCpuSeconds'] for q in journal if lo<=q['Reference']<hi),baseline_callback_cpu_s=sum(q['CallbackCpuSeconds'] for q in baseline_journal if lo<=q['Reference']<hi),scope='Same fifth-bend geometric reference interval, actual callback CPU including evidence writing; if DNF, new coverage can be incomplete.')
 for crossing in run['Crossings']:lap_times.append(crossing['Time']-prior);prior=crossing['Time']
 native=dict(label=meta['Label'],validation=meta['Validation'],pass_=meta['Pass'],faster_than161=meta['FasterThan161'],exact_initial=meta['Exact161Initial'],finish_s=finish,completed_laps=meta['CompletedLaps'],outcome=meta['Outcome'],observed_until_s=run['EndSeconds'],laps_s=lap_times,six_bends=bends,straights=straights,minimum_pose_m=meta['MinimumPoseMeters'],minimum_swept_m=meta['MinimumSweepMeters'],baseline_minimum_swept_m=base['minimum_swept_m'],swept_surplus_m=meta['MinimumSweepMeters']-.5 if meta['MinimumSweepMeters'] is not None else None,maximum_beta_deg=meta['MaximumBetaDegrees'],accepted_certificates=count,abstain=meta['Abstain'],contacts=meta['Contacts'],failures=failures,rejected_plans=rejected,selected_origins=origins,profile_cpu_s=phases,cpu_s=ledger['CpuSeconds'],charged_cpu_s=ledger['ChargedCpuSeconds'],earlier_result_cpu_snapshot_s=meta['ChargedCpuSeconds'],baseline_cpu_s=baseline_cpu,cpu_ratio=ledger['CpuSeconds']/baseline_cpu,baseline_finish_s=base['finish_s'],delta_s=finish-base['finish_s'] if meta['Pass'] else None,distance_xy_m=ss[-1]['Input']['Rider']['DistanceMeters']-ss[0]['Input']['Rider']['DistanceMeters'],baseline_distance_xy_m=base['distance_xy_m'],minimum_terminal_safety_goal_m=min(reserves,default=None),stats=stats,source=path.relative_to(R).as_posix(),sha256=sha(path),policy_records=count+len(failures))
 native.pop('rejected_plans');native['rejected_plans_count']=len(rejected);native['fifth_bend_cpu']=fifth_bend_cpu;native['rejected_reason_counts']=dict(Counter(q['reason'] for q in rejected));native['missing_failed_review']=any(q.get('Review') is None and q.get('Error') is not None for _,q,_ in policies(path))
 native['hierarchy_totals']=dict(hierarchy)
 native['final_safety_goal_m']=final_safety_goal;native['post_finish_reserve_m']=final_safety_goal-3*run['LapMeters'] if final_safety_goal is not None else None
 i=min(range(1,len(ss)),key=lambda i:ss[i]['Certificate']);tight=dict(time_s=ss[i]['Input']['Time'],reference_m=ss[i]['Input']['Rider']['TotalReferenceMeters'],swept_m=ss[i]['Certificate'],before=ss[i-1],after=ss[i],scope='Saved native interval only; no new physics query or recertification.')
 save(D/(path.stem+'-tightest.json'),tight);native['tightest_interval_s']=[ss[i-1]['Input']['Time'],ss[i]['Input']['Time']]
 return native
def main():
 baseline=read(OLD/'analysis.json');base={q['label']:q for q in baseline['native']};diagnostics=[]
 for p in sorted((D/'diagnostics').glob('*-result.json')):
  q=read(p);path=p.with_name(q['Label']+'.zip');assert verify(path)==1
  diagnostics.append(q|dict(source=path.relative_to(R).as_posix(),sha256=sha(path),improvement_s=q['Baseline161Arrival']-q['TrackArrival'] if q['TrackArrival'] is not None else None))
 native=[]
 for p in sorted((D/'native').glob('*.zip')):
  if p.with_name(p.stem+'-result.json').exists():native.append(analyze_native(p,base[p.stem]))
 held=[n for n in native if n['validation']];plan=read(D/'budget-plan.json');legal=len(held)==5 and all(n['pass_'] and n['exact_initial'] for n in held);sports=legal and all(n['faster_than161'] for n in held) and statistics.median(n['finish_s'] for n in held)<79.60283030281548
 cpu=legal and sum(n['cpu_s'] for n in held)<=1.25*sum(n['baseline_cpu_s'] for n in held) and statistics.median(n['cpu_s'] for n in held)<=1.25*statistics.median(n['baseline_cpu_s'] for n in held) and all(n['cpu_ratio']<=1.5 for n in held)
 speed=legal and all(n['stats']['straight_mean_kph']>n['stats']['baseline_straight_mean_kph'] for n in held)
 out=dict(base=BASE,controller_reference=plan['ControllerReference'],verdict='PASS' if sports and cpu and speed else 'PARTIAL',native_legal_gate=legal,sports_gate=sports,cpu_gate=cpu,speed_profile_gate=speed,diagnostics=diagnostics,certificates=[read(p) for p in sorted((D/'certificates').glob('*-result.json'))],native=native,held_count=len(held),held_dnf=sum(not q['pass_'] for q in held),median_finish_s=statistics.median(q['finish_s'] for q in held) if legal else None,budget_journal=read(D/'budget-journal.json'),baseline_summary_source='docs/experiments/trajectory-continuation/analysis.json',baseline_summary_sha256=sha(OLD/'analysis.json'),causal_scope='Only the speed-target proposal is changed. Coupled gas/steering and the resulting executed path can change together; no independent causal attribution to throttle, steering or line is claimed.')
 save(D/'analysis.json',out);print(json.dumps({k:v for k,v in out.items() if k in ['verdict','held_count','held_dnf','median_finish_s','native_legal_gate','sports_gate','cpu_gate']}))
if __name__=='__main__':main()
