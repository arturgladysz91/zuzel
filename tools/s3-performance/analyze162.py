from audit162 import R,D,S,read,save,sha
from bundle162 import policies,read as bundle_read,verify
import collections,statistics

def analyze():
 ledger={q['Label']:q for q in read(D/'budget-journal.json')};old_ledger={q['Label']:q for q in read(S/'budget-journal.json')};old=read(D/'profile161.json');base={r['label']:r for r in old['runs']};rows=[]
 for p in sorted((D/'native').glob('*-result.json')) if (D/'native').exists() else []:
  meta=read(p);label=meta['Label'];z=p.with_name(label+'.zip');count=verify(z);ph=collections.Counter();cpu=[];equal_maneuvers=True;accepted=0;failures=[]
  for key,q,payload in policies(z):
   if q['Error'] is not None:
    failures.append(dict(key=key,error=q['Error']));continue
   accepted+=1;r=q['Review'];assert r['Independent'] and r['ConditionalCertificate'] and len(r['Tree'])==13 and len(r['Terminals'])==9
   original=read(S/'policies'/meta['BaselineLabel']/(key+'.json.gz'));assert r['PhysicalHash']==original['Review']['PhysicalHash']
   semantic=lambda outcomes:[{k:v for k,v in x.items() if k!='CpuSeconds'} for x in outcomes]
   equal_maneuvers &= semantic(q['Outcomes'])==semantic(original['Outcomes']);cpu.append(r['CpuSeconds'])
   for f in r['Profile']['Phases']:ph[f['Name']]+=f['CpuSeconds']
  assert accepted==meta['AcceptedDecisions'] and equal_maneuvers
  data=bundle_read(z,'native.json');assert data['Fingerprint']==meta['Fingerprint'];assert data['Riders'][0]['CompletedLaps']==3 if meta['Pass'] else True
  journal=bundle_read(z,'journal.json');b=base[meta['BaselineLabel']];bcpu=old_ledger[meta['BaselineLabel']]['CpuSeconds'];ncpu=ledger[label]['CpuSeconds'];callback=[j['CallbackCpuSeconds'] for j in journal]
  if meta['Validation']:
   frozen=read(D/'controller-freeze.json');assert bundle_read(z,'sources.json')['Files']==frozen['Sources']
  rows.append(dict(label=label,validation=meta['Validation'],scenario=meta['Scenario'],pass_=meta['Pass'],finish_s=meta['FinishSeconds'],baseline_finish_s=b['finish_s'],cpu_before_s=bcpu,cpu_after_s=ncpu,result_snapshot_cpu_s=meta['ChargedCpuSeconds'],cpu_scope='Complete budget-ledger elapsed CPU, including final native archive serialization; result snapshot occurs before bundle finalization',cpu_delta_s=ncpu-bcpu,cpu_reduction_percent=100*(bcpu-ncpu)/bcpu,planner_cpu_before_s=b['profile_cpu_s'],planner_cpu_after_s=sum(ph.values()),callback_before=b['callback_cpu_s'],callback_after=dict(total=sum(callback),median=statistics.median(callback),max=max(callback)),phases_before=b['phases_s'],phases_after=dict(ph),response_statistics=meta['ResponseStatistics'],exact_native_fingerprint=meta['NativeFingerprintExact'],exact_all_physical_reviews=meta['Exact161'],all_maneuvers_equal=equal_maneuvers,accepted_decisions=accepted,policy_records=count,failures=failures,minimum_swept_m=meta['MinimumSweepMeters'],source=p.relative_to(R).as_posix(),bundle=z.relative_to(R).as_posix(),bundle_sha256=sha(z),bundle_bytes=z.stat().st_size))
 held=[q for q in rows if q['validation']];complete=len(held)==5 and len({q['scenario']['Id'] for q in held})==5 and all(q['pass_'] for q in held);dev=next((q for q in rows if not q['validation']),None);perf=complete and dev is not None and dev['pass_'] and dev['cpu_after_s']<dev['cpu_before_s'] and all(sum(q[after] for q in held)<sum(q[before] for q in held) and statistics.median(q[after] for q in held)<statistics.median(q[before] for q in held) for before,after in [('cpu_before_s','cpu_after_s'),('planner_cpu_before_s','planner_cpu_after_s')])
 certs=[]
 for p in sorted((D/'certificates').glob('*.json.gz')):
  q=read(p);certs.append(dict(label=q['Label'],optimized=q['Optimized'],cpu=ledger[q['Label']]['CpuSeconds'],result_snapshot_cpu_s=q['CpuSeconds'],planner_cpu=q['Review']['CpuSeconds'],physical_hash=q['Review']['PhysicalHash'],statistics=q['ResponseStatistics']))
 out=dict(base='85a5ee3295c2dd983fbacb21859649f794cc5ba5',performance_verdict='PASS' if perf else 'PARTIAL',preserved_s3='PASS' if complete else 'PARTIAL',native=rows,held_count=len(held),held_dnf=sum(not q['pass_'] for q in held),median_finish_s=statistics.median(q['finish_s'] for q in held) if complete else None,certificates=certs,budget=read(D/'budget-journal.json'),historical_profile='profile161.json',tightest='tightest161.json',prepare_witness='prepare-witness.json',storage_benchmark='storage161.json')
 save(D/'analysis.json',out);print({'performance':out['performance_verdict'],'worlds':len(rows),'held':len(held)})
if __name__=='__main__':analyze()
