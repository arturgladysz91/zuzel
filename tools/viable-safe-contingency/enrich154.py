"""Final saved-data checks: native parity, exact/near terminal diversity and real-observation errors."""
import importlib.util,json,gzip,math,csv
from pathlib import Path
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/viable-safe-contingency';OLD=R/'docs/experiments/safe-fast-contingency'
spec=importlib.util.spec_from_file_location('a154',Path(__file__).with_name('analyze154.py'));a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
def read(p):return a.read(p)
def norm(v,w):return math.hypot(v['X']-w['X'],v['Y']-w['Y'])
def main():
 with a.a.charged('saved-native-parity-and-candidate-diversity-v2'):
  old=read(OLD/'native/ranked-S-v1.json.gz');new=read(D/'native/S0-full-baseline-v1.json.gz');oldp=OLD/'policies/ranked-S-v1';newp=D/'policies/S0-full-baseline-v1';pairs=[]
  for p in sorted(newp.glob('decision-*.json.gz')):
   x=read(p);y=read(oldp/p.name);pairs.append(dict(time_s=x['Time'],physical_hash_equal=x['PhysicalHash']==y['PhysicalHash'],full_k1_equal=x['Counts']['Physical']['FullK1']==y['Counts']['Physical']['FullK1'],attempts_equal=x['Attempts']==y['Attempts']))
  local_rows=[];examples=[];states=0;actions=0;groups=0;local_reasons={};observation_errors=[]
  for folder in (D/'diagnostics').iterdir():
   for path in folder.glob('*.json.gz'):
    data=read(path);batches=data.get('LocalCandidates') or [];bad=data.get('RejectedRollouts') or []
    cts=dict(total=0,pose_below=0,sweep_only=0,beta_only=0,other=0,legal=0)
    for batch in batches:
     for c in batch['Candidates']:
      q=c['Safety'];cts['total']+=1
      if q['MinimumGapMeters']<.5:cts['pose_below']+=1
      elif q['SweepLowerBoundMeters']<.5:cts['sweep_only']+=1
      elif q['MaximumBetaDegrees']>45:cts['beta_only']+=1
      elif q['Legal']:cts['legal']+=1
      else:cts['other']+=1
    local_rows.append(dict(run=folder.name,file=path.name,full_k1_batches=len(batches),available='LocalCandidates' in data,**cts))
    for batch in batches:
     states+=1;actions+=len(batch['Candidates']);keys=set()
     for c in batch['Candidates']:
      v=c['Terminal']['Rider'];keys.add((round(v['Position']['X'],1),round(v['Position']['Y'],1),round(v['Velocity']['X'],1),round(v['Velocity']['Y'],1),round(v['Heading'],2),round(v['AngularVelocity'],2),round(c['Terminal']['Omega'],1),round(c['Terminal']['Steering'],3)))
     groups+=len(keys)
     if len(examples)<4:
      for t in bad:
       if t['Start']==batch['Start'] and t['Actions']:
        chosen=next((c for c in batch['Candidates'] if c['Action']==t['Actions'][0] and c['Safety']['Legal']),None)
        if chosen:examples.append(dict(source=path.relative_to(D).as_posix(),start_s=batch['Start']['Input']['Time'],short_horizon_s=.9,short_safety=chosen['Safety'],long_origin=t['origin'],long_safety=t['Safety'],last_predicted_time_s=t['LastPoints'][-1]['Time'] if t['LastPoints'] else None));break
  for folder in (D/'policies').iterdir():
   policies=[read(p) for p in sorted(folder.glob('decision-*.json.gz'))];ab=folder/'abstain.json.gz'
   targets=[p['Observed'] for p in policies[1:]]
   if ab.exists():
    z=read(ab);targets.append(dict(Input=z['Observed'],CrankOmega=z['CrankOmega']))
   for p,current in zip(policies,targets):
    future=next((n['Observed'] for n in p['Tree'] if n['Depth']==1 and n['Sign']==0 and abs(n['Observed']['Input']['Time']-current['Input']['Time'])<1e-8),None)
    if future:
     x=current['Input']['Rider'];y=future['Input']['Rider'];observation_errors.append(dict(run=folder.name,time_s=current['Input']['Time'],position_m=norm(x['Position'],y['Position']),velocity_mps=norm(x['Velocity'],y['Velocity']),heading_rad=abs(math.remainder(x['Heading']-y['Heading'],2*math.pi)),yaw_radps=abs(x['AngularVelocity']-y['AngularVelocity']),wheel_radps=abs(current['Input']['Omega']-future['Input']['Omega']),crank_radps=abs(current['CrankOmega']-future['CrankOmega']),exact_nominal_state=current==future))
  result=dict(native_fingerprint_equal=old['Fingerprint']==new['Fingerprint'],all_dense_native_sample_fields_equal=old['Riders'][0]['Samples']==new['Riders'][0]['Samples'],accepted_decisions=len(pairs),pairs=pairs,all_accepted_physical_hashes_equal=all(p['physical_hash_equal'] for p in pairs),all_accepted_k1_counts_equal=all(p['full_k1_equal'] for p in pairs),all_accepted_attempt_traces_equal=all(p['attempts_equal'] for p in pairs),local_h09_safe_long_rollout_rejected_examples=examples,candidate_diversity=dict(batches=states,candidates=actions,quantized_terminal_groups=groups,near_duplicate_excess=actions-groups,quantization='position/velocity0.1, heading/yaw0.01, wheel0.1, steering0.001; crank and queue excluded: never an exact-state or certificate equivalence'),observation_errors=observation_errors,scope='Saved-data diagnostics only. Correlated finite signs are not a whole uncertainty box; real observations require new certification. New S0 local counters are instrumented reproduction, not invented historical153 archive fields.')
  result['local_candidate_classes']=local_rows
  a.save('enrichment.json',result)
  with (D/'local-candidates154.csv').open('w',encoding='utf-8',newline='') as f:w=csv.DictWriter(f,fieldnames=list(local_rows[0]));w.writeheader();w.writerows(local_rows)
  print(json.dumps({k:v for k,v in result.items() if k not in ['pairs','observation_errors','local_h09_safe_long_rollout_rejected_examples','local_candidate_classes']}))
if __name__=='__main__':main()
