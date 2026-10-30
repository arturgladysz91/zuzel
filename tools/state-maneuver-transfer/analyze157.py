"""Recorded-data forensics, CPU accounting and replay export. No physical stepping."""
from pathlib import Path
import csv,gzip,hashlib,json,math,sys,time,shutil
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/state-maneuver-transfer';O=R/'docs/experiments/line-throttle-physics'
def read(p):
 p=Path(p);return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
def write(p,x):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(x,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def csvwrite(p,rows):
 p.parent.mkdir(parents=True,exist_ok=True)
 with p.open('w',encoding='utf-8',newline='') as f:
  w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 tag=sys.argv[1] if len(sys.argv)>1 else 'forensics-export-v1';ledger=read(D/'budget.json');assert all(r['Complete'] for r in ledger);assert not any(r['Name']==tag for r in ledger);assert sum(r['CpuSeconds'] for r in ledger)<900
 ledger.append(dict(Name=tag,Complete=False,NativeWorlds=0,CpuSeconds=0));write(D/'budget.json',ledger)
 try:
  a=read(O/'analysis.json');selected=[r for r in a['matrix_results'] if r['strategy']=='T1' and r['status']=='PASS']+[next(r for r in a['results'] if r['label']=='held-first-B-refined-final')];witnesses=[];replays=[]
  def replay(p,start,kind,status,fixture):
   raw=read(p);ss=raw['Riders'][0]['Samples'];s=[q for q in ss if q['Input']['Time']>=start-1e-8];return dict(name=raw['Name'],fixture=fixture,kind=kind,status=status,start=start,source=str(p.relative_to(R)).replace('\\','/'),samples=[dict(t=q['Input']['Time']-start,x=q['Input']['Rider']['Position']['X'],y=q['Input']['Rider']['Position']['Y'],heading=q['Input']['Rider']['Heading'],s=q['Input']['Rider']['TotalReferenceMeters'],speed=q['Input']['Rider']['Speed']*3.6,beta=q['Input']['Rider']['SlideRadians']*180/math.pi,drive=q['Input']['Active']['Drive'],gap=min(q['Inner'],q['Outer']),yaw=q['Input']['Rider']['AngularVelocity'],pending=[dict(delay=c['ActivationAt']-q['Input']['Time'],steer=c['Action']['SteeringRadians']*180/math.pi,drive=c['Action']['Drive']) for c in q['Input']['Pending']]) for q in s])
  for row in selected:
   label=row['label'];result=read(R/row['source']);f=result['Fixture'];raw=read(O/'native'/f'{label}.json.gz')['Riders'][0];samples=raw['Samples'];dec=[q for q in raw['Decisions'] if q['Choice']['Time']>=f['Time']-1e-8];intervals=list(csv.DictReader(gzip.open(O/'native'/f'{label}-intervals.csv.gz','rt',encoding='utf-8')));events=[];last=None
   baseline=next((r for r in a['matrix_results'] if r['fixture']==row['fixture'] and r['family']==row['family'] and r['strategy']=='T0'),None)
   base_dec=read(O/'native'/f"{baseline['label']}.json.gz")['Riders'][0]['Decisions'] if baseline else []
   for d in dec:
    t=d['Choice']['Time'];action=d['Choice']['Action'];s=min(samples,key=lambda q:abs(q['Input']['Time']-t));v=s['Input']['Rider'];q=min(intervals,key=lambda q:abs(float(q['time_s'])-t));t0=next((x['Choice']['Action'] for x in base_dec if abs(x['Choice']['Time']-t)<1e-7),None)
    ev=dict(observed_s=t,relative_s=t-f['Time'],activation_s=t+.24,steer_deg=math.degrees(action['SteeringRadians']),drive=action['Drive'],changed=action!=last,different_T0=t0 is not None and t0!=action,reference_m=v['TotalReferenceMeters'],offset_m=v['Road']['OffsetMeters'],x_m=v['Position']['X'],y_m=v['Position']['Y'],speed_kph=v['Speed']*3.6,beta_deg=math.degrees(v['SlideRadians']),yaw_rads=v['AngularVelocity'],wheelspin=float(q['wheel_slip']),actual_curvature_per_m=float(q['curvature_per_m']),front_fx_N=float(q['front_fx_body_N']),front_fy_N=float(q['front_fy_body_N']),rear_fx_N=float(q['rear_fx_N']),rear_fy_N=float(q['rear_fy_N']),pending=json.dumps(s['Input']['Pending'],separators=(',',':')))
    events.append(ev);last=action
   csvwrite(D/'commands'/f'{label}.csv',events);shutil.copyfile(O/'native'/f'{label}-intervals.csv.gz',D/'commands'/f'{label}-intervals.csv.gz')
   witnesses.append(dict(**row,fixture_start=f['Time'],command_changes=sum(x['changed'] for x in events),first_T0_difference=next((x['observed_s'] for x in events if x['different_T0']),None),command_log=f'commands/{label}.csv'))
   replays.append(replay(O/'native'/f'{label}.json.gz',f['Time'],'#156 zapisany świadek',row['status'],f['Name']))
   if baseline and baseline['status']=='PASS':replays.append(replay(O/'native'/f"{baseline['label']}.json.gz",f['Time'],'#156 T0 rodzina',baseline['status'],f['Name']))
  for name in ['held-first-A-T1-final','held-first-B-T0-final','held-second-B-T1-final','held-second-C-refined-final']:
   row=next(r for r in a['results'] if r['label']==name);f=read(R/row['source'])['Fixture'];replays.append(replay(O/'native'/f'{name}.json.gz',f['Time'],'#156 odłożony transfer',row['status'],f['Name']))
  # Deduplicate the repeated original baselines, preserving actual native samples.
  replays=list({r['name']:r for r in replays}.values());reviews=[]
  for p in sorted(D.rglob('*.json.gz')):
   if p.parent.name in ('native','commands'):continue
   x=read(p)
   if not isinstance(x,dict) or 'Review' not in x:continue
   q=x['Review'];c=x['Counters'];reviews.append(dict(label=str(p.relative_to(D)).replace('\\','/').replace('.json.gz',''),pass_=x['Pass'],time=x['Observed']['Input']['Time'],reference=x['Observed']['Input']['Rider']['TotalReferenceMeters'],reason=x['Error'] or q['Reason'],cpu_s=q['CpuSeconds'] if q else None,forecasts=c['Forecasts'],contacts=c['Contacts'],independent_forecasts=c['IndependentForecasts'],independent_comparisons=c['IndependentComparisons'],search_hits=c['SearchHits'],rollout_hits=c['RolloutHits'],K1=x['K1Calls'],nodes=len(q['Tree']) if q else 0,reserves=len(q['Terminals']) if q else 0,rejected=sum(not g['Legal'] for g in x['Outcomes']),goal=q['Goal'] if q else None,safety_goal=q['SafetyGoal'] if q else None,profile=q['Profile'] if q else None))
  generation=[]
  for p in sorted(D.glob('generation-*.json.gz')):
   x=read(p);f=x['Fixture']
   for plan in x['Plans']:
    fam=plan['Origin'].split('/')[-2];w=next(r for r in selected if r['fixture']==f['Name'] and r['family']==fam);arrival=plan['Arrival']-f['Time'];generation.append(dict(fixture=f['Name'],family=fam,forecast_time_s=arrival,witness_native_time_s=w['time_s'],delta_s=arrival-w['time_s'],minimum_gap_m=plan['Safety']['MinimumGapMeters'],sweep_m=plan['Safety']['SweepLowerBoundMeters'],max_beta_deg=plan['Safety']['MaximumBetaDegrees'],status='FORECAST_ONLY_NO_13_9',origin=plan['Origin']))
  native=[]
  for p in sorted((D/'native').glob('*-result.json')):
   x=read(p);raw=read(p.with_name(p.name.replace('-result.json','.json.gz')));f=x['Fixture'];window=[s for s in raw['Riders'][0]['Samples'] if s['Input']['Time']>=f['Time']-1e-8];native.append(dict(label=raw['Name'],fixture=f['Name'],exact=x['ExactInitialKey'],complete=x['Complete'],pass_=x['Pass'],end_s=x['EndSeconds'],duration_s=x['EndSeconds']-f['Time'],certified_decisions=sum(d['Pass'] for d in x['Decisions']),reason=x['Outcome'],min_gap=min(min(s['Inner'],s['Outer']) for s in window),sweep=min(s['Certificate'] for s in window),max_beta=max(abs(math.degrees(s['Input']['Rider']['SlideRadians'])) for s in window)))
   replays.append(replay(p.with_name(p.name.replace('-result.json','.json.gz')),f['Time'],'#157 autonomiczne obserwacje','PREFIX / ABSTAIN' if not x['Complete'] else 'COMPLETE',f['Name']))
  c0=next(r for r in a['matrix_results'] if r['fixture']=='historical' and r['family']=='C' and r['strategy']=='T0');b1=next(r for r in selected if r['fixture']=='historical' and r['family']=='B');decomp=dict(gain_pct=(1-b1['time_s']/c0['time_s'])*100,distance_change_pct=(b1['distance_m']/c0['distance_m']-1)*100,mean_speed_change_pct=(b1['mean_speed_kph']/c0['mean_speed_kph']-1)*100,log_distance=math.log(b1['distance_m']/c0['distance_m']),negative_log_speed=-math.log(b1['mean_speed_kph']/c0['mean_speed_kph']),log_time=math.log(b1['time_s']/c0['time_s']),scope='Exact kinematic identity, not causal attribution of throttle versus yaw')
  causal=[]
  for x in read(D/'causal-v1.json.gz'):
   o=x['ObservedQueueRollout'];t=x['TeacherQueueCounterfactual'];causal.append(dict(fixture=x['Fixture'],teacher=x['Teacher'],same_initial_body=x['StartBodiesIdentical'],new_action_irrelevant_next_point2=x['NewActionsCannotChangeNextObservation'],next_error=x['QueueOnlyEffectAtNextObservation'],observed_queue=x['ObservedPending'],teacher_queue=x['TeacherPending'],first_forecast_failure_s=o['End']['Input']['Time'],reason=o['Reason'],counterfactual_complete=t['Complete'],counterfactual_safe=t['Safety']['Legal']))
  freeze=read(D/'algorithm-freeze.json');assert all(sha(R/'tools/state-maneuver-transfer'/f)==h for f,h in freeze['Hashes'].items())
  write(D/'analysis.json',dict(witnesses=witnesses,generation=generation,reviews=reviews,native=native,causal=causal,decomposition=decomp,freeze_unchanged=True,gates=dict(A='PARTIAL:7 local common-section reproductions; only historical certified;15 actual reviews then ABSTAIN',B='PARTIAL:6/6 small perturbations certified; held-first/held-second ABSTAIN',C='NOT_RUN: earlier transfer/stability gates fail',D='NOT_RUN:C gate closed'),success=False))
  csvwrite(D/'decisions.csv',[{k:v for k,v in r.items() if k!='profile'} for r in reviews]);csvwrite(D/'generation.csv',generation);csvwrite(D/'native.csv',native);write(D/'replay.json',dict(runs=replays,scope='Only original accepted native samples. Old156 witnesses and new157 prefixes are explicitly distinct. No forecast shown as native or L as S.'))
  from report157 import render
  render()
  ledger[-1].update(Complete=True,CpuSeconds=time.process_time()+1,Outcome='recorded command/force/energy/pending analysis, plots and comparative replay export');write(D/'budget.json',ledger)
  report=D/'REPORT.md';report.write_text(report.read_text(encoding='utf-8').replace('{{CPU}}',f"{sum(r['CpuSeconds'] for r in ledger):.6f}").replace('{{WORLDS}}',str(sum(r['NativeWorlds'] for r in ledger))),encoding='utf-8')
 except Exception as e:
  ledger[-1].update(Complete=True,CpuSeconds=time.process_time()+1,Outcome='export failed; preserved: '+str(e));write(D/'budget.json',ledger);raise
 print('Study157 charged CPU-s',sum(r['CpuSeconds'] for r in ledger),'native worlds',sum(r['NativeWorlds'] for r in ledger))
if __name__=='__main__':main()
