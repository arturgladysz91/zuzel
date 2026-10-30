"""Sealed study155 data export. Does not rerun search or a native world."""
from pathlib import Path
from contextlib import contextmanager
from collections import Counter
import csv,gzip,hashlib,json,msvcrt,subprocess,time
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/hierarchical-maneuvers'
BASE='405111a721d5bc5ba1477999f25b3837e8c62307'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if str(p).endswith('.gz') else p.read_bytes())
def save(name,x):
 p=D/name
 if p.exists():raise FileExistsError(p)
 p.write_text(json.dumps(x,ensure_ascii=False,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8',newline='\n')
@contextmanager
def charged(label):
 with (D/'budget.json.lock').open('a+b') as lock:
  lock.seek(0);msvcrt.locking(lock.fileno(),msvcrt.LK_NBLCK,1);rows=read(D/'budget.json')
  assert all(x['Complete'] for x in rows) and not any(x['Name']==label for x in rows)
  spent=sum(x['CpuSeconds'] for x in rows);assert spent<1198
  began=time.process_time();row=dict(Name=label,Complete=False,NativeWorlds=0,CpuSeconds=0,Base=BASE);rows.append(row)
  def flush():(D/'budget.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8',newline='\n')
  def guard():
   if spent+time.process_time()-began>=1198:raise RuntimeError('study155 CPU cap')
  flush()
  try:yield guard
  finally:row.update(Complete=True,CpuSeconds=time.process_time()-began,Outcome='sealed-data analysis/export; no search or native world');flush()
def main():
 with charged('sealed-analysis-v1') as guard:
  replay=read(D/'replay.json.gz');rows=[];phases=[];stability=[]
  for p in sorted(D.glob('[ABC]-*.json.gz')):
   guard();x=read(p);r=x['Review'];c=r['Counts']['Physical'];h=r['Counts'].get('Hierarchy',{});mode=x['Mode'];final=p.name.endswith('v1.json.gz') if mode=='A' else p.name.endswith('v4.json.gz')
   nominal=r['Tapes'].get(r['Nominal']) if r.get('Nominal') else None
   partial=x['Extra'].get('FurthestPartial') if mode!='A' else None
   points=next((s['Points'] for case in replay['Cases'] if case['Time']==x['Time'] for s in case['Series'] if s['Mode']==mode),None) if final else None
   row=dict(name=p.name.removesuffix('.json.gz'),mode=mode,time_s=x['Time'],final=final,passed=x['Passed'],error=x['Error'],process_cpu_s=x['CpuSeconds'],planner_cpu_s=r['CpuSeconds'],full_k1=c['FullK1'],physical_forecasts=c['Forecasts'],forecast_requests=c['TransitionRequests'],search_hits=c['SearchHits'],independent_forecasts=c['IndependentForecasts'],primitive_candidates=h.get('PrimitiveCandidates',0),local_k1_candidates=c['Search'].get('FullHorizonCandidates',0),complete_generator_rollouts=h.get('FullPlans',0),distinct_legal_generator_states=h.get('DistinctLegalStates',x['Extra'].get('DistinctLegalStates',0)),nodes=len(r['Tree']),leaves=len(r['Terminals']),full_policies=int(x['Passed']),goal_m=r['Goal'],safety_goal_m=r['SafetyGoal'],arrival_s=nominal['Arrival'] if nominal else None,minimum_gap_m=nominal['Safety']['MinimumGapMeters'] if nominal else None,sweep_m=nominal['Safety']['SweepLowerBoundMeters'] if nominal else None,furthest_partial_reference_m=partial['End']['Input']['Rider']['TotalReferenceMeters'] if partial else None,conditional_attempts=len(r['Attempts']),conditional_legal_attempts=sum(a['Legal'] for a in r['Attempts']),hierarchy=h,recorded_rejections=dict(Counter(z['Reason'] for z in x['Extra'].get('Rejections',[]))),rejections_truncated=len(x['Extra'].get('Rejections',[]))==300)
   row['path_to_goal_m']=None
   if points and row['arrival_s'] is not None:
    # Dense samples, linearly interpolate only the final goal interval.
    distance=0
    for a,b in zip(points,points[1:]):
     fraction=min(1,max(0,(row['arrival_s']-a['t'])/(b['t']-a['t'])))
     distance+=((b['x']-a['x'])**2+(b['y']-a['y'])**2)**.5*fraction
     if b['t']>=row['arrival_s']:break
    row['path_to_goal_m']=distance
   rows.append(row)
   for q in r['Profile']['Phases']:phases.append(dict(name=row['name'],phase=q['Name'],cpu_s=q['CpuSeconds'],percent=q['Percent']))
  for p in sorted(D.glob('stability*.json.gz')):
   x=read(p);r=x['Review'];c=r['Counts']['Physical'];stability.append(dict(name=p.name.removesuffix('.json.gz'),passed=x['Passed'],error=x['Error'],process_cpu_s=x['CpuSeconds'],planner_cpu_s=r['CpuSeconds'],position_delta_m=x['MeasuredPositionDeltaMeters'],velocity_delta=x['MeasuredVelocityDelta'],full_k1=c['FullK1'],physical_forecasts=c['Forecasts'],nodes=len(r['Tree']),leaves=len(r['Terminals']),prior_hash=x['PriorPhysicalHash'],scope=x['Scope']))
  raw=subprocess.check_output(['git','ls-tree','-r',BASE],cwd=R,text=True);blobs={s.split('\t',1)[1]:s.split('\t',1)[0].split()[2] for s in raw.splitlines()}
  frozen={p:h for p,h in blobs.items() if p.startswith(('src/','data/')) or p.startswith('tools/') and not p.startswith(('tools/replay-viewer/','tools/ci/'))}
  raw=subprocess.check_output(['git','ls-tree',BASE+':docs/experiments'],cwd=R,text=True);trees={'docs/experiments/'+s.split('\t',1)[1]:s.split()[2] for s in raw.splitlines() if s.split()[1]=='tree'}
  save('source-proof.json',dict(Base=BASE,FrozenGitBlobs=frozen,FrozenHistoricalGitTrees=trees))
  save('analysis.json',dict(Study=155,Verdict='PARTIAL',Rows=rows,Final=[x for x in rows if x['final']],Stability=stability,Phases=phases,NewNativeWorlds=0,NativeReference={k:v for k,v in replay['NativeReference'].items() if k!='Points'},Gates=dict(A='PARTIAL: C passes start but abstains in both later states',B='LOCAL PASS at own0.2s causal prefix, not a sequence or global box',C='NOT RUN: gate A insufficient',D='NOT RUN',E='NOT RUN'),Scope='Only complete independently checked13/9 policies are accepted. B/C fail cold saved6.6/13.8 states; A carries its own prior at those states. All plotted A/B/C paths are forecasts; old154 native data is reference only.'))
  for name,table in [('measurements155.csv',[{k:v for k,v in x.items() if not isinstance(v,(dict,list))} for x in rows]),('cpu-phases155.csv',phases)]:
   with (D/name).open('x',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(dict.fromkeys(k for x in table for k in x)));w.writeheader();w.writerows(table)
  print(json.dumps(dict(probes=len(rows),stability=len(stability),final_passed=sum(x['passed'] for x in rows if x['final']),frozen_blobs=len(frozen),historical_trees=len(trees))))
if __name__=='__main__':main()
