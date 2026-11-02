"""Read saved original predictions/native samples. No vehicle model or controller."""
from pathlib import Path
import csv,gzip,hashlib,importlib.util,json,math,statistics,zipfile
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/dynamic-slip-preview'
def read(p):
 p=Path(p);return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
def save(p,v):
 p.parent.mkdir(parents=True,exist_ok=True);b=(json.dumps(v,ensure_ascii=False,indent=2,allow_nan=False)+'\n').encode();p.write_bytes(gzip.compress(b,mtime=0) if p.suffix=='.gz' else b)
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def module(name,p):
 s=importlib.util.spec_from_file_location(name,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
energy=module('energy160',R/'tools/corner-speed-loss/analyze145.py')
def crossing(points,reference):
 for a,b in zip(points,points[1:]):
  sa,sb=a['Rider']['TotalReferenceMeters'],b['Rider']['TotalReferenceMeters']
  if sa<=reference<=sb and sb>sa:return a['Time']+(b['Time']-a['Time'])*(reference-sa)/(sb-sa)
 return None
def forecast(path):
 f=read(path);s=f['Summary'];p=f['Points'];entry=crossing(p,35);exit_=crossing(p,132.38937497138977)
 s.update(initial_time_s=f['Initial']['Input']['Time'],goal_reference_m=f['Goal'],scope=f['Scope'],first_bend_entry_s=entry,first_bend_exit_s=exit_,first_bend_duration_s=exit_-entry if entry and exit_ else None,source=str(path.relative_to(R)).replace('\\','/'),sha256=digest(path))
 return s
def native(path):
 run=read(path);r=run['Riders'][0];ss=r['Samples'];label=run['Name'];meta=read(D/'native'/f'{label}-result.json');journal=read(D/'journals'/f'{label}.json.gz');end=r['FinishSeconds'] if r['FinishSeconds'] is not None else run['EndSeconds'];rows=[]
 for a,b in zip(ss,ss[1:]):
  if a['Input']['Time']>=end:break
  q=energy.interval(a,b);v,w=a['Input']['Rider'],b['Input']['Rider'];dt=q['dt_s'];mx=(w['Velocity']['X']+v['Velocity']['X'])/2;my=(w['Velocity']['Y']+v['Velocity']['Y'])/2;ax=(w['Velocity']['X']-v['Velocity']['X'])/dt;ay=(w['Velocity']['Y']-v['Velocity']['Y'])/dt;speed=math.hypot(mx,my)
  q.update(theta_rad=w['Heading'],chi_rad=math.atan2(w['Velocity']['Y'],w['Velocity']['X']),actual_curvature_per_m=(mx*ay-my*ax)/speed**3 if speed>.1 else 0,swept_m=b['Certificate'],dt_weight_s=min(end,b['Input']['Time'])-a['Input']['Time'],active_gas_on_interval=a['Input']['Active']['Drive'],chi_dot_rads=(mx*ay-my*ax)/speed**2 if speed>.1 else 0,beta_dot_rads=(w['SlideRadians']-v['SlideRadians'])/dt,front_fx_world_N=b['Forces']['FrontForce']['X'],front_fy_world_N=b['Forces']['FrontForce']['Y'],rear_fx_world_N=b['Forces']['RearForce']['X'],rear_fy_world_N=b['Forces']['RearForce']['Y'],distance_xy_m=w['DistanceMeters'],wheel_omega=b['Input']['Omega']);rows.append(q)
 out=D/'telemetry'/f'{label}.csv.gz';out.parent.mkdir(exist_ok=True)
 if rows:
  with gzip.open(out,'wt',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
 bends=[];straights=[];lap=run['LapMeters'];starts=[0,35,67.46312499046326,99.92624998092651,132.38937497138977,194.38937497138977,226.85249996185303,259.3156249523163,291.77874994277954,318.77874994277954]
 for li in range(3):
  for bi,(a,b) in enumerate([(1,4),(5,8)]):
   lo,hi=li*lap+starts[a],li*lap+starts[b];e=energy.point(ss,lo);x=energy.point(ss,hi);part=[s for s in ss if lo<=s['Input']['Rider']['TotalReferenceMeters']<=hi]
   bends.append(dict(lap=li+1,bend=2*li+bi+1,status='complete' if e and x else 'incomplete' if e else 'unreached',entry=e,exit=x,minimum_kph=min((s['Input']['Rider']['Speed']*3.6 for s in part),default=None),duration_s=x['time_s']-e['time_s'] if e and x else None))
  for si,(lo,hi) in enumerate([(0,35),(132.38937497138977,194.38937497138977),(291.77874994277954,318.77874994277954)]):
   part=[q for q in rows if li*lap+lo<=q['reference_m']<=li*lap+hi];duration=sum(q['dt_weight_s'] for q in part)
   straights.append(dict(lap=li+1,section=si+1,observed_s=duration,mean_kph=sum(q['speed_kph']*q['dt_weight_s'] for q in part)/duration if duration else None,maximum_kph=max((q['speed_kph'] for q in part),default=None)))
 accepted=0;profiles={};rejected=[];ranks=[];failures=[]
 for p in sorted((D/'policies'/label).glob('*.json.gz')):
  q=read(p);review=q['Review']
  if q['Error'] is None:
   assert review and review['ConditionalCertificate'] and review['Independent'] and len(review['Tree'])==13 and len(review['Terminals'])==9
   event=journal[accepted];assert event['Action']==r['Decisions'][accepted]['Choice']['Action']==review['Tree'][0]['Action'];accepted+=1
  else:failures.append(dict(source=str(p.relative_to(R)).replace('\\','/'),time_s=q['Observed']['Input']['Time'],reason=q['Error'],outcomes=q.get('Outcomes'),rejections=q.get('Rejections')))
  if review:
   rejected.extend(dict(time_s=review['Time'],origin=x['Origin'],reason=x['Reason']) for x in review['Attempts'] if not x['Legal'])
   for phase in review['Profile']['Phases']:profiles[phase['Name']]=profiles.get(phase['Name'],0)+phase['CpuSeconds']
 assert accepted==meta['AcceptedDecisions']
 conditions=read(R/'docs/experiments/robust-three-lap/final-v6/scenarios-frozen.json')['Scenarios'];case_index=next(i for i,s in enumerate(conditions) if s['Id']==meta['Scenario']['Id'])
 base_path=R/'docs/experiments/robust-three-lap/final-v6/native'/f'validation-v6-new-{case_index}.json.gz'
 base=read(base_path);br=base['Riders'][0];assert r['Spec']==br['Spec'];be=energy.point(br['Samples'],35);bx=energy.point(br['Samples'],132.38937497138977)
 first=read(sorted((D/'policies'/label).glob('*.json.gz'))[0])['Observed'];base_first=read(base_path.with_name(base_path.name.replace('.json.gz','-result.json')))['Initial'];assert first==meta['Initial']==meta['ExpectedInitial']==base_first
 assert ss[0]['Input']['Rider']==br['Samples'][0]['Input']['Rider'];assert ss[0]['Input']==br['Samples'][0]['Input'];assert ss[0]['CrankOmega']==br['Samples'][0]['CrankOmega']
 # This is an exact initial-state pairing; a DNF has no finish-time delta.
 pair=dict(initial_state_exact=True,baseline_first_bend_s=bx['time_s']-be['time_s'],new_first_bend_s=bends[0]['duration_s'],baseline_source=str(base_path.relative_to(R)).replace('\\','/'),baseline_sha256=digest(base_path),baseline_finish_s=br['FinishSeconds'],new_finish_s=r['FinishSeconds'],delta_s=r['FinishSeconds']-br['FinishSeconds'] if meta['Pass'] else None)
 laps=[];prior=0
 for c in run['Crossings']:laps.append(c['Time']-prior);prior=c['Time']
 cpu=[q['CallbackCpuSeconds'] for q in journal];duration=sum(q['dt_weight_s'] for q in rows)
 averages={k:sum(q[k]*q['dt_weight_s'] for q in rows)/duration if duration else None for k in ['speed_kph','front_util','rear_util','active_gas','wheel_slip']}
 if not failures and not meta['Pass']:failures.append(dict(source=str(path.relative_to(R)).replace('\\','/'),time_s=run['EndSeconds'],reason=run['Outcome'],scope='Actual native technical DNF; failed callback threw before journal write. Separate terminal-cause-v5 probe preserves diagnostic candidate and stack.'))
 summary=dict(label=label,pass_=meta['Pass'],validation=meta['Validation'],scenario=meta['Scenario'],completed_laps=r['CompletedLaps'],finish_s=r['FinishSeconds'],observed_until_s=run['EndSeconds'],outcome=run['Outcome'],samples=len(ss),laps_s=laps,six_bends=bends,straights=straights,pair=pair,distance_xy_m=ss[-1]['Input']['Rider']['DistanceMeters']-ss[0]['Input']['Rider']['DistanceMeters'],minimum_pose_m=meta['MinimumPoseMeters'],minimum_swept_m=meta['MinimumSweepMeters'],maximum_beta_deg=meta['MaximumBetaDegrees'],maximum_yaw_rads=max(abs(s['Input']['Rider']['AngularVelocity']) for s in ss),contacts=meta['Contacts'],abstain=meta['Abstain'],accepted_decisions=accepted,rejected_plans=rejected,failures=failures,cpu_process_s=meta['ChargedCpuSeconds'],cpu_callbacks=dict(total=sum(cpu),median=statistics.median(cpu) if cpu else None,maximum=max(cpu,default=None)),cpu_phases=profiles,averages=averages,source=str(path.relative_to(R)).replace('\\','/'),sha256=digest(path))
 display=[]
 for i in sorted(set(range(0,len(ss),5))|{len(ss)-1}):
  s=ss[i];v=s['Input']['Rider'];display.append(dict(index=i,t=s['Input']['Time'],x=v['Position']['X'],y=v['Position']['Y'],s=v['TotalReferenceMeters'],heading=v['Heading'],speed=v['Speed']*3.6,beta=math.degrees(v['SlideRadians']),yaw=v['AngularVelocity'],gas=s['Input']['Active']['Drive'],steering=math.degrees(s['Input']['Steering']),wheel=s['Input']['Omega'],crank=s['CrankOmega'],gap=min(s['Inner'],s['Outer']),swept=s['Certificate']))
 replay=dict(name=label,scenario=meta['Scenario']['Id'],status='S3' if meta['Pass'] else 'DNF',source=summary['source'],sha256=digest(path),fingerprint=run['Fingerprint'],finish=r['FinishSeconds'],samples=display)
 return summary,replay
def main():
 forecasts=[forecast(p) for p in sorted((D/'forecasts').glob('*.json.gz'))];exact=read(D/'forecasts/exact-rest-speed12.json.gz');old=read(R/'docs/experiments/geometry-speed-audit/forecasts/rest-speed12.json.gz')
 assert exact['Initial']==old['Initial'] and exact['Points']==old['Points'] and exact['End']==old['End'];assert all(x['Action']==y['Action'] and x['State']==y['State'] for x,y in zip(exact['Actions'],old['Actions']))
 traces=[a['Trace'] for a in exact['Actions']];events=[q for q in traces if abs(q['DesiredAlpha'])>.1 and abs(q['Actual']['YawAcceleration']-q['Imagined']['YawAcceleration'])>.1];opposite=[q for q in events if q['Actual']['YawAcceleration']*q['DesiredAlpha']<0]
 certificates=[]
 for p in sorted((D/'certificates').glob('*.json.gz')):
  q=read(p);r=q['Review'];certificates.append(dict(label=p.stem,pass_=q['Error'] is None and r['ConditionalCertificate'] and r['Independent'] and len(r['Tree'])==13 and len(r['Terminals'])==9,cpu_s=q['CpuSeconds'],error=q['Error'],nodes=len(r['Tree']) if r else 0,reserves=len(r['Terminals']) if r else 0))
 native_rows=[];replays=[]
 for p in sorted((D/'native').glob('*.json.gz')):a,b=native(p);native_rows.append(a);replays.append(b)
 held=[q for q in native_rows if q['validation']];finish=[q['finish_s'] for q in held if q['pass_']];planned=read(R/'docs/experiments/robust-three-lap/final-v6/scenarios-frozen.json')['Scenarios'];expected=read(D/'controller-freeze.json')['HeldCount'] if (D/'controller-freeze.json').exists() else 5;held_exact=sorted(q['scenario']['Id'] for q in held)==sorted(q['Id'] for q in planned[:expected]);s_pass=held_exact and len(held)>=5 and len(finish)==len(held)
 data=dict(base='9fb071ba3129aae88a6ec8c401d51a784be2da2b',verdict='PASS' if s_pass and all(q['pair']['delta_s']<0 for q in held) else 'PARTIAL',exact_rest_speed12_reproduced=True,forecasts=forecasts,certificates=certificates,first_response_error=events[0],first_opposite_yaw_response=opposite[0],native=native_rows,new_native_worlds=len(native_rows),held_trials=len(held),held_set_exact=held_exact,series_closure=read(D/'series-closure.json') if (D/'series-closure.json').exists() else None,held_dnf=sum(not q['pass_'] for q in held),median_finish_s=statistics.median(finish) if finish else None,finish_range_s=[min(finish),max(finish)] if finish else None,budget=read(D/'budget-journal.json'))
 save(D/'analysis.json',data);geometry=read(R/'docs/experiments/geometry-speed-audit/geometry.json.gz')['Tracks'][0];save(D/'native-replay.json.gz',dict(scope='Actual native samples only, display decimation by original index, DNF freezes at final sample. No forecast portrayed as native.',geometry=geometry,runs=replays));print(json.dumps(dict(verdict=data['verdict'],native=len(native_rows),held=len(held),median=data['median_finish_s']),indent=2))
if __name__=='__main__':main()
