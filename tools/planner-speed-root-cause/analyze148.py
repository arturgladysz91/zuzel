"""Read-only physical ledger and segment analysis; no worlds or forecasts."""
import csv,gzip,hashlib,importlib.util,json,math,time
from pathlib import Path
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/planner-speed-root-cause'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.name.endswith('.gz') else p.read_bytes())
def save(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def main():
 start=time.process_time();lease=(D/'budget.json.lock').open('a+b');ledger=read(D/'budget.json');assert all(x['Complete'] for x in ledger);name='saved-telemetry-analysis';assert not any(x['Name']==name for x in ledger);entry=dict(Name=name,Complete=False,NativeWorlds=0,CpuSeconds=0);ledger.append(entry);save(D/'budget.json',ledger)
 spec=importlib.util.spec_from_file_location('a145',R/'tools/corner-speed-loss/analyze145.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
 geometry=read(R/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json');starts=[q['startReferenceMeters'] for q in geometry['segments']];runs=[];segments=[]
 for name,path in [('K1',R/'docs/experiments/standing-start-three-laps/native/K1.json.gz'),('#147',R/'docs/experiments/efficient-autonomous-planner/native/autonomous147-shortlist.json.gz')]:
  r=read(path);ss=r['Riders'][0]['Samples'];L=r['LapMeters'];rows=[]
  for a,b in zip(ss,ss[1:]):
   q=m.interval(a,b);p=b['Powertrain'];forces=b['Forces'];dt=q['dt_s'];q.update(wheel_rpm=b['Input']['Omega']*30/math.pi,clutch_engagement=p['Engagement'],clutch_slip_rads=p['SlipRadiansPerSecond'],available_crank_W=p['Engine']['CrankPowerWatts'],actual_combustion_W=p['CombustionWorkJoules']/dt,wheel_engine_W=p['Wheel']['EngineWorkJoules']/dt,front_Fx_local_N=0,front_Fy_local_N=forces['FrontLateral']);rows.append(q)
  groups=[('start/home-before-bend',0,1),('bend1',1,4),('back-straight',4,5),('bend2',5,8),('home-after-bend',8,9)]
  for lap in range(3):
   for label,lo,hi in groups:
    begin=m.point(ss,lap*L+starts[lo]);end=m.point(ss,lap*L+(L if hi==9 else starts[hi]));assert begin and end
    metrics=m.phase(rows,begin['time_s'],end['time_s']);qs=[q for q in rows if begin['time_s']<=q['time_s']<=end['time_s']]
    metrics.update(run=name,lap=lap+1,segment=label,entry_kph=begin['speed_kph'],exit_kph=end['speed_kph'],minimum_kph=min(q['speed_kph'] for q in qs),maximum_kph=max(q['speed_kph'] for q in qs),distance_m=end['distance_m']-begin['distance_m'],full_gas_s=sum(q['dt_s'] for q in qs if q['active_gas']==1),reference_start_m=begin['reference_m'],reference_end_m=end['reference_m'])
    metrics['extra_ranges']={key:dict(min=min(q[key] for q in qs),max=max(q[key] for q in qs),mean=sum(q[key]*q['dt_s'] for q in qs)/sum(q['dt_s'] for q in qs)) for key in ['wheel_rpm','clutch_engagement','clutch_slip_rads','available_crank_W','actual_combustion_W','wheel_engine_W','trajectory_radius_m'] if all(q[key] is not None for q in qs)}
    segments.append(metrics)
  with gzip.GzipFile(filename='',fileobj=(D/f'{name.replace("#","")}-telemetry.csv.gz').open('wb'),mode='wb',mtime=0) as z:
   import io
   stream=io.TextIOWrapper(z,encoding='utf-8',newline='');w=csv.DictWriter(stream,fieldnames=list(rows[0]),lineterminator='\n');w.writeheader();w.writerows(rows);stream.flush()
  runs.append(dict(name=name,source=path.relative_to(R).as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),finish_s=r['Riders'][0]['FinishSeconds'],laps=[c['Time']-(r['Crossings'][i-1]['Time'] if i else 0) for i,c in enumerate(r['Crossings'])],maximum_kph=max(q['speed_kph'] for q in rows),energy={k:sum(q[k] for q in rows) for k in rows[0] if k.endswith('_J')},max_body_energy_residual_J=max(abs(q['body_work_residual_J']) for q in rows),max_system_energy_residual_J=max(abs(q['total_energy_residual_J']) for q in rows)))
 save(D/'physical-analysis.json',dict(method=m.__doc__,native_worlds=0,runs=runs,segments=segments,scope='Exact saved accepted intervals; no causal allocation of lap-time percentages. Front local Fx=0 by model equation. Available crank power is the frozen map at measured midpoint RPM, actual power includes throttle.'))
 with (D/'segments.csv').open('w',encoding='utf-8',newline='') as f:
  keys=['run','lap','segment','duration_s','entry_kph','minimum_kph','maximum_kph','exit_kph','distance_m','closed_gas_s','full_gas_s'];w=csv.DictWriter(f,fieldnames=keys);w.writeheader();w.writerows({k:s[k] for k in keys} for s in segments)
 entry.update(Complete=True,CpuSeconds=time.process_time()-start,Outcome='read-only segment/energy analysis complete');save(D/'budget.json',ledger)
 lease.close();print('CPU',entry['CpuSeconds']);print(json.dumps(runs,indent=1))
if __name__=='__main__':main()
