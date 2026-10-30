"""Saved153 cost attribution, including the unaccepted13.8s callback. No new search."""
from pathlib import Path
from collections import Counter
import importlib.util,csv,gzip,json
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/viable-safe-contingency';OLD=R/'docs/experiments/safe-fast-contingency'
spec=importlib.util.spec_from_file_location('audit153',R/'tools/safe-fast-contingency/analyze153.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a);a.D=D;a.BASE='de500229a6fdb5c57da7cbeb90f5484c28d57130'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
def main():
 with a.charged('saved153-all-decisions-audit-v2'):
  paths=sorted((OLD/'policies/ranked-S-v1').glob('decision-*.json.gz'))+[(OLD/'policies/ranked-S-v1/abstain.json.gz')]
  original_decisions={round(r['Time'],5):r for r in a.journal(OLD/'native/ranked-S-v1-decisions.jsonl') if r.get('Event')=='decision'}
  prior=Counter();rows=[];phases=Counter();origins=Counter();failures=[]
  for path in paths:
   obj=read(path);r=obj['Review'] if path.name=='abstain.json.gz' else obj;accepted=path.name!='abstain.json.gz';c=r['Counts']['Physical'];now=Counter({k:c[k] for k in ['FullK1','Forecasts','Contacts','IndependentForecasts','IndependentContacts','RolloutHits','SearchHits','IndependentHits']});diff=now-prior;prior=now
   types=Counter();aggregate=Counter();attempt_origins=Counter();earliest=None
   for attempt in r['Attempts']:
    attempt_origins[attempt['Origin']]+=1
    if attempt['Legal']:types['complete_legal_rollouts']+=1;continue
    aggregate[attempt['Reason']]+=1
    f=attempt.get('FirstFailure') or {};reason=f.get('Reason',attempt['Reason'])
    if 'footprint' in reason and 'swept' not in reason:kind='pose_below_half_meter'
    elif 'swept' in reason:kind='sweep_only_unproven'
    elif 'beta' in reason:kind='beta_envelope'
    elif 'terminal' in reason:kind='terminal_not_reached'
    else:kind='other_physical_or_incomplete'
    types[kind]+=1
    if f and (earliest is None or f.get('Time',1e99)<earliest.get('Time',1e99)):earliest=f
    failures.append(dict(decision_time=r['Time'],origin=attempt['Origin'],aggregate_reason=attempt['Reason'],first_failure=f,kind=kind))
   profile={p['Name']:p['CpuSeconds'] for p in r['Profile']['Phases']};phases.update(profile);origins.update(attempt_origins)
   roots=[t for t in r['RejectedTrees'] if t['Origin'].startswith('root-')];scenario=[t for t in r['RejectedTrees'] if not t['Origin'].startswith('root-')]
   rows.append(dict(time_s=r['Time'],accepted=accepted,cpu_s=r['CpuSeconds'],full_k1=diff['FullK1'],forecasts=diff['Forecasts'],contacts=diff['Contacts'],independent_forecasts=diff['IndependentForecasts'],independent_contacts=diff['IndependentContacts'],rollout_hits=diff['RolloutHits'],search_hits=diff['SearchHits'],independent_hits=diff['IndependentHits'],complete_certified_policies=len(original_decisions.get(round(r['Time'],5),{}).get('Ranked',[])) if accepted else 0,accepted_full_policy=int(accepted),full_rollout_pose_rejections=aggregate['footprint reserve'],full_rollout_sweep_rejections=aggregate['swept physical footprint reserve0.5m'],legal_rollouts=types['complete_legal_rollouts'],attempts=len(r['Attempts']),pose_below_half_meter=types['pose_below_half_meter'],sweep_only_unproven=types['sweep_only_unproven'],beta_envelope=types['beta_envelope'],terminal_not_reached=types['terminal_not_reached'],other_failures=types['other_physical_or_incomplete'],rejected_scenario_trees=len(scenario),rejected_root_trees=len(roots),nodes=len(r['Tree']),leaves=len(r['Terminals']),phases=profile,origins=dict(attempt_origins),first_failure=earliest))
  totals=Counter({k:sum(r[k] for r in rows) for k in rows[0] if isinstance(rows[0][k],(float,int)) and k not in ['time_s','nodes','leaves']})
  result=dict(base=a.BASE,rows=rows,totals=totals,exclusive_phases=phases,attempt_origins=origins,failures=failures,interpretation='69 accepted decisions plus unaccepted13.8s review. A legal rollout is not a complete13/9 policy. First-failure sweep-only describes that earliest interval only; later sampled poses in the same forecast may violate0.5m. Aggregate footprint reason counts the latter, separately. Neither is a count of demonstrated continuous physical crossings. K1 per-candidate physical rejection counters were not archived by153; they cannot be reconstructed honestly from aggregated selected seeds. Explicit new instrumentation is required.',local_k1_rejection_counts='NOT_RECORDED_BY_153')
  (D/'diagnosis153.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
  flat=[{k:v for k,v in r.items() if k not in ['phases','origins','first_failure']} for r in rows]
  with (D/'decisions153.csv').open('w',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(flat[0]));w.writeheader();w.writerows(flat)
  print(json.dumps({'decisions':len(rows),'k1':totals['full_k1'],'types':{k:totals[k] for k in ['pose_below_half_meter','sweep_only_unproven','terminal_not_reached','legal_rollouts']},'phases':phases,'last':rows[-1]},ensure_ascii=False))
if __name__=='__main__':main()
