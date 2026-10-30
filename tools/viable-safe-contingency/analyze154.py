"""Sealed154 export. No new native world or planner search."""
from pathlib import Path
from collections import Counter
import importlib.util,json,gzip,csv,hashlib,subprocess,math,copy
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/viable-safe-contingency';OLD=R/'docs/experiments/safe-fast-contingency'
spec=importlib.util.spec_from_file_location('a153',R/'tools/safe-fast-contingency/analyze153.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a);a.D=D;a.BASE='de500229a6fdb5c57da7cbeb90f5484c28d57130'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
def save(name,x):(D/name).write_text(json.dumps(x,ensure_ascii=False,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def main():
 with a.charged('sealed-analysis-final-v2'):
  g=read(R/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json');starts=[s['startReferenceMeters'] for s in g['segments']];a146=a.module('a146','tools/autonomous-corner-planning/analyze146.py');old=read(OLD/'analysis.json');runs=[];indices=[];audits={};corners=[];curves=[];laps=[];decision_rows=[];diagnostics=[]
  paths=[OLD/'native/ranked-S-v1.json.gz']+sorted((D/'native').glob('*.json.gz'))
  for path in paths:
   run=read(path);name=run['Name'];r=run['Riders'][0];ss=r['Samples'];is_old=path.parent==OLD/'native';res=read(path.with_name(name+'-result.json'));res['ControllerAttemptCpu']=res.get('ControllerCpu',res.get('ControllerAttemptCpu'));s=a.summary(run,res);s['new']=not is_old;s['mode']='S153' if is_old else 'S'+str(res['Mode']);s['regime']='SafetyHalfMeter';s['full_s_pass']=run['Outcome']=='completed' and s['laps']==3 and s['contacts']==0 and s['half_meter_pose_violations']==0 and s['minimum_sweep_m']>=.5;s['cpu_per_simulated_second']=s['controller_cpu_s']/s['end_s'] if s['end_s']>0 else None;s['cpu_per_accepted_decision']=s['controller_cpu_s']/s['decisions'] if s['decisions'] else None;s['gate_B_first_bend']=s['reference_m']>=starts[4] and s['contacts']==0 and s['half_meter_pose_violations']==0 and (s['minimum_sweep_m'] is None or s['minimum_sweep_m']>=.5);s['gate_C_first_lap']=s['laps']>=1;s['gate_D_two_laps']=s['laps']>=2;s['gate_E_three_laps']=s['full_s_pass']
   phases=Counter();rows=[];prev=Counter();timings=read(D/'timings'/(name+'.json')) if not is_old else read(OLD/'timings'/(name+'.json'));cpu=[x['CpuSeconds'] for x in timings];s.update(cpu_p50_s=a.percentile(cpu,.5),cpu_p95_s=a.percentile(cpu,.95),cpu_max_s=max(cpu,default=None),cpu_quantile_scope='complete callback, includes refusals and evidence/archive')
   js=a.journal(path.with_name(name+'-decisions.jsonl'));ds=[x for x in js if x['Event']=='decision'];pdir=(OLD if is_old else D)/'policies'/name
   for j,item in enumerate(ds):
    p=read(pdir/(item['Tree']+'.json.gz'));assert p['Independent'] and p['ConditionalCertificate'] and len(p['Tree'])==13 and len(p['Terminals'])==9
    for leaf in p['Terminals']:
     safe=p['Tapes'][leaf['Tape']]['Safety'];assert safe['Legal'] and safe['MinimumGapMeters']>=.5 and safe['SweepLowerBoundMeters']>=.5
    for n in p['Tree']:
     if n['Depth']<2:assert sorted(c['Sign'] for c in p['Tree'] if c['Parent']==n['Id'])==[-1,0,1]
    assert item['Selected']==r['Decisions'][j]['Choice']['Action']
    for z in p['Profile']['Phases']:phases[z['Name']]+=z['CpuSeconds']
    c=p['Counts']['Physical'];now=Counter(k1=c['FullK1'],forecasts=c['Forecasts'],contacts=c['Contacts']+c.get('FeedbackQueries',0),local_pose=c['Search'].get('LocalPoseRejected',c.get('LocalPoseRejected',0)),local_non_pose=c['Search'].get('LocalNonPoseRejected',c['Search'].get('LocalSweepOnlyRejected',c.get('LocalNonPoseRejected',c.get('LocalSweepOnlyRejected',0)))),local_legal=c['Search'].get('LocalLegalCandidates',c.get('LocalLegalCandidates',0)));diff=now-prev;prev=now
    reasons=Counter(x['Reason'] for x in p['Attempts'] if not x['Legal']);decision_rows.append(dict(run=name,time_s=p['Time'],accepted=True,cpu_s=p['CpuSeconds'],**diff,pose_rejects=reasons['footprint reserve'],sweep_rejects=reasons['swept physical footprint reserve0.5m'],certified_policies=1))
    prefix='../safe-fast-contingency/' if is_old else '';rows.append(dict(time_s=p['Time'],reason=p['Reason'],selected=[item['Selected']],planned_goal_time_s=p['Tapes'][p['Nominal']]['Arrival'],planned_exit_time_s=None,planned_exit_kph=None,forecast_legal=True,actual_action=item['Selected'],actual_activation=next((z['ActivationAt'] for z in r['Activations'] if abs(z['ObservedAt']-p['Time'])<1e-8),None),next_change_reason='fresh native observation; full13/9 and independent preflight',prediction_error=None,tree_file=prefix+'policies/'+name+'/'+item['Tree']+'.json.gz',nodes=13,leaves=9,predicted_trace=[]))
   abstain=pdir/'abstain.json.gz'
   if abstain.exists():
    ab=read(abstain);p=ab.get('Review')
    if p:
     for z in p['Profile']['Phases']:phases[z['Name']]+=z['CpuSeconds']
     c=p['Counts']['Physical'];now=Counter(k1=c['FullK1'],forecasts=c['Forecasts'],contacts=c['Contacts']+c.get('FeedbackQueries',0),local_pose=c['Search'].get('LocalPoseRejected',c.get('LocalPoseRejected',0)),local_non_pose=c['Search'].get('LocalNonPoseRejected',c['Search'].get('LocalSweepOnlyRejected',c.get('LocalNonPoseRejected',c.get('LocalSweepOnlyRejected',0)))),local_legal=c['Search'].get('LocalLegalCandidates',c.get('LocalLegalCandidates',0)));diff=now-prev;prev=now;reasons=Counter(x['Reason'] for x in p['Attempts'] if not x['Legal']);decision_rows.append(dict(run=name,time_s=p['Time'],accepted=False,cpu_s=p['CpuSeconds'],**diff,pose_rejects=reasons['footprint reserve'],sweep_rejects=reasons['swept physical footprint reserve0.5m'],certified_policies=0));s['failed_review']=dict(time_s=p['Time'],reason=p['Reason'],nodes=len(p['Tree']),leaves=len(p['Terminals']),attempts=len(p['Attempts']),reasons=dict(reasons),cpu_s=p['CpuSeconds'])
   s['counts']=dict(prev);s['exclusive_phases']=dict(phases);runs.append(s);audits[name]=dict(reviews=len(ds),native_decisions=len(ds),coverage_start_s=0,coverage_end_s=ds[-1]['Time'] if ds else 0,followed_expected=0,expected_reviews=0,queues_match=True,maximum_error=dict(PositionMeters=None),cpu_p50_s=s['cpu_p50_s'],cpu_p95_s=s['cpu_p95_s'],rows=rows,exclusive_phases=dict(phases));corners+=a146.analyze(run,starts)
   indices.append(dict(Id=name,File=('../safe-fast-contingency/native/' if is_old else 'native/')+path.name,Variant='K1',Mode=s['mode'],Regime='SafetyHalfMeter',Riders=1,Outcome=run['Outcome'],Duration=run['EndSeconds'],Fingerprint=run['Fingerprint']))
   laps.append(dict(Run=name,RiderId=r['Spec']['Profile']['Id'],Laps=[dict(Lap=j+1,LapSeconds=v) for j,v in enumerate(s['laps_s'])]))
   curves.append(dict(name=name,points=[dict(t=a.t(p),x=p['Input']['Rider']['Position']['X'],y=p['Input']['Rider']['Position']['Y'],reference=a.ref(p),speed=p['Input']['Rider']['Speed']*3.6,beta=p['Input']['Rider']['SlideRadians']*180/math.pi,gas=p['Input']['Active']['Drive'],offset=p['Input']['Rider']['Road']['OffsetMeters']) for p in ss[::4]]))
  probes=[]
  for p in sorted(D.glob('S*-A-*.json.gz')):
   q=read(p);v=q['Review'];probes.append(dict(name=p.stem.removesuffix('.json'),error=q['Error'],passed=q['Error'] is None,cpu_s=q['CpuSeconds'],planner_cpu_s=v['CpuSeconds'],k1=q['Counts']['FullK1'],attempts=len(v['Attempts']),nodes=len(v['Tree']),leaves=len(v['Terminals'])))
  for path in (D/'diagnostics').glob('*/*.json.gz'):
   doc=read(path);counts=Counter()
   for t in doc.get('RejectedRollouts') or []:
    points=t['LastPoints'];pose=any(min(p['Inner'],p['Outer'])<.5 for p in points)
    # This is a recorded last-window test only; do not call it a full continuous trajectory proof.
    counts['recorded_endpoint_breach' if pose else 'no_recorded_endpoint_breach']+=1
   diagnostics.append(dict(file=path.relative_to(D).as_posix(),**counts))
  save('analysis.json',dict(full_runs=runs,probes=probes,plan_audits=audits,corners=corners,diagnostics=diagnostics,counts=decision_rows,scientific_verdict='PARTIAL',scope='No complete S. Entire native traces retained,13/9 required; cheap ranking is never proof; S3 diagnostic is unpromoted.'))
  save('index.json',dict(Title='#154 · Pełny obrys0,5m i koszt planowania',DefaultRun='S0-full-baseline-v1',GeometryFile='../causal-yaw-throttle/progressive-b-viewer-geometry.json',BendReferences=[starts[i] for i in [1,4,5,8]],Runs=indices,Note='PARTIAL. Wszystkie nowe warianty wymagają fizycznego obrysu i sweep >=0,5 m. Nie ukończono trzech okrążeń. Zapisany prefiks i lokalny certyfikat nie są pełnym sukcesem.'))
  save('lap-metrics.json',laps);(D/'comparison.json.gz').write_bytes(gzip.compress(json.dumps(curves,separators=(',',':')).encode(),mtime=0))
  for name,table in [('decision-costs154.csv',decision_rows),('corners.csv',corners)]:
   fields=list(dict.fromkeys(k for r in table for k in r))
   with (D/name).open('w',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=fields);w.writeheader();w.writerows(table)
  raw=subprocess.check_output(['git','ls-tree','-r',a.BASE],cwd=R,text=True);blobs={s.split('\t',1)[1]:s.split('\t',1)[0].split()[2] for s in raw.splitlines()};frozen={p:h for p,h in blobs.items() if p.startswith(('src/','tools/safe-fast-contingency/','tools/efficient-contingency/'))};trees={p.as_posix():subprocess.check_output(['git','rev-parse',a.BASE+':'+p.as_posix()],cwd=R,text=True).strip() for p in Path('docs/experiments').iterdir() if p.is_dir() and p.name!='viable-safe-contingency'}
  save('source-proof.json',dict(Base=a.BASE,FrozenGitBlobs=frozen,FrozenHistoricalGitTrees=trees))
  print(json.dumps({'runs':len(runs),'probes':len(probes),'verdict':'PARTIAL','frozen_trees':len(trees)}))
if __name__=='__main__':main()
