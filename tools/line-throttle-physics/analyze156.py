"""Native common-section metrics; no simulation, trajectory fitting or state edits."""
from pathlib import Path
import bisect,csv,gzip,hashlib,importlib.util,json,math,sys,time
R=Path(__file__).resolve().parents[2]
D=R/'docs/experiments/line-throttle-physics'
def read(p):
    p=Path(p);return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
def write(p,x):
    p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(json.dumps(x,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def curvature(a,b):
    """Actual velocity-direction curvature, includes all lateral movement."""
    dt=b['Input']['Time']-a['Input']['Time']
    va=a['Input']['Rider']['Velocity'];vb=b['Input']['Rider']['Velocity']
    x=(va['X']+vb['X'])/2;y=(va['Y']+vb['Y'])/2
    speed=math.hypot(x,y)
    return (x*(vb['Y']-va['Y'])-y*(vb['X']-va['X']))/(dt*speed**3) if speed>.01 else 0
def interpolate(samples,reference):
    refs=[s['Input']['Rider']['TotalReferenceMeters'] for s in samples]
    i=bisect.bisect_left(refs,reference)
    if i>=len(samples) or reference<refs[0]-1e-7:return None
    a,b=samples[max(0,i-1)],samples[i]
    u=(reference-refs[max(0,i-1)])/(refs[i]-refs[max(0,i-1)]) if i else 0
    return {k:f(a)+(f(b)-f(a))*u for k,f in dict(time=lambda s:s['Input']['Time'],distance=lambda s:s['Input']['Rider']['DistanceMeters'],speed=lambda s:s['Input']['Rider']['Speed']*3.6).items()}
def summarize(samples,family,fixture,exact=True,contacts=0):
    spec=importlib.util.spec_from_file_location('energy145',R/'tools/corner-speed-loss/analyze145.py')
    m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
    start=fixture['Time'];goal=interpolate(samples,fixture['Goal']);entry=interpolate(samples,fixture['Entry']);exit_=interpolate(samples,fixture['Exit'])
    end=goal['time'] if goal else samples[-1]['Input']['Time']
    ss=[s for s in samples if start-1e-8<=s['Input']['Time']<=end+1e-8]
    rows=[]
    for a,b in zip(samples,samples[1:]):
        if b['Input']['Time']<=start+1e-9 or a['Input']['Time']>=end:continue
        q=m.interval(a,b);q['curvature_per_m']=curvature(a,b);q['distance_m']=b['Input']['Rider']['DistanceMeters']-a['Input']['Rider']['DistanceMeters'];q['travel_heading_rad']=math.atan2(b['Input']['Rider']['Velocity']['Y'],b['Input']['Rider']['Velocity']['X']);q['body_heading_rad']=b['Input']['Rider']['Heading'];q['sweep_m']=b['Certificate'];q['time_weight_s']=max(0,min(b['Input']['Time'],end)-max(a['Input']['Time'],start));rows.append(q)
    duration=end-start
    def energy(key):return sum(q[key]*q['time_weight_s']/q['dt_s'] for q in rows)
    bend=m.phase(rows,entry['time'],exit_['time']) if entry and exit_ else None
    midlo=fixture['Entry']+.2*(fixture['Exit']-fixture['Entry']);midhi=fixture['Entry']+.8*(fixture['Exit']-fixture['Entry'])
    middle=[q for q in rows if midlo<=q['reference_m']<=midhi];weight=sum(q['distance_m'] for q in middle);mean=sum(q['offset_m']*q['distance_m'] for q in middle)/weight if weight else None
    low,high={'A':(0,3),'B':(3,7),'C':(7,12),'U':(-100,100)}[family]
    achieved=mean is not None and low<=mean<=high
    minimum=min((min(s['Inner'],s['Outer']) for s in ss),default=None)
    sweep=min((q['sweep_m'] for q in rows),default=None)
    beta=max((abs(q['beta_deg']) for q in rows),default=0)
    pose_violation=minimum is not None and minimum<.5
    safe=minimum is not None and minimum>=.5 and sweep is not None and sweep>=.5 and beta<=45 and contacts==0
    status='PASS' if goal and safe and exact and achieved else 'DNF' if pose_violation or beta>45 or contacts else 'UNCERTAIN'
    first=interpolate(samples,fixture['State']['Input']['Rider']['TotalReferenceMeters'])
    distance=goal['distance']-fixture['State']['Input']['Rider']['DistanceMeters'] if goal else None
    speeds=[q['speed_kph'] for q in rows if fixture['Entry']<=q['reference_m']<=fixture['Exit']]
    full_gas=sum(max(0,min(q['time_s'],exit_['time'])-max(q['time_s']-q['dt_s'],entry['time'])) for q in rows if entry and exit_ and q['active_gas']==1)
    metrics=dict(family=family,status=status,complete=goal is not None,safe=safe,exact_initial=exact,family_achieved=achieved,mean_offset_m=mean,time_s=duration if goal else None,observed_prefix_s=duration,distance_m=distance,mean_speed_kph=distance/duration*3.6 if distance is not None else None,entry_kph=entry['speed'] if entry else None,minimum_kph=min(speeds) if speeds else None,exit_kph=exit_['speed'] if exit_ else None,bend_time_s=exit_['time']-entry['time'] if entry and exit_ else None,bend_mean_kph=(exit_['distance']-entry['distance'])/(exit_['time']-entry['time'])*3.6 if entry and exit_ else None,bend_gas_zero_s=bend['closed_gas_s'] if bend else None,bend_gas_full_s=full_gas,bend_gas_mean=bend['stats']['active_gas']['mean'] if bend else None,minimum_gap_m=minimum,sweep_m=sweep,max_beta_deg=beta,max_yaw_rads=max((abs(q['yaw_rads']) for q in rows),default=0),combustion_kj=energy('combustion_J')/1000,lateral_loss_kj=(energy('front_slip_loss_J')+energy('rear_lateral_slip_loss_J'))/1000,longitudinal_loss_kj=energy('rear_long_slip_loss_J')/1000,resistance_kj=-energy('resistance_J')/1000,clutch_loss_kj=energy('clutch_heat_J')/1000,energy_residual_J=energy('total_energy_residual_J'),max_front_util=max((q['front_util'] for q in rows),default=0),max_rear_util=max((q['rear_util'] for q in rows),default=0),max_actual_curvature=max((abs(q['curvature_per_m']) for q in rows),default=0),mean_abs_curvature=sum(abs(q['curvature_per_m'])*q['time_weight_s'] for q in rows)/duration if duration else None)
    if bend:metrics['bend_gas_partial_s']=metrics['bend_time_s']-metrics['bend_gas_zero_s']-metrics['bend_gas_full_s']
    else:metrics['bend_gas_partial_s']=None
    return metrics,rows
def main():
    began=time.process_time();tag=sys.argv[1] if len(sys.argv)>1 else 'analysis-v1';ledger=read(D/'budget.json');assert all(x['Closed'] for x in ledger)
    results=[];traces={};replays=[]
    for p in sorted((D/'native').glob('*-result.json')):
        result=read(p);run=read(p.with_name(p.name.replace('-result.json','.json.gz')));f=result['Fixture'];family=read(R/result['Attempt'])['Family'];a=read(R/result['Attempt']);ss=run['Riders'][0]['Samples'];metrics,rows=summarize(ss,family,f,result['ExactInitialKey'],len(run['Contacts']))
        metrics.update(fixture=f['Name'],strategy=a['Strategy'],label=run['Name'],cpu_s=result['CpuSeconds'],outcome=run['Outcome'],source=str(p.relative_to(R)),attempt=result['Attempt'],forecast_time_s=a['Arrival']-f['Time'] if a['Arrival'] else None)
        # The native status, never forecast feasibility, owns the final table.
        results.append(metrics);traces[run['Name']]=rows
        visible=[s for s in ss if s['Input']['Time']>=f['Time']-1e-8]
        replays.append(dict(name=run['Name'],fixture=f['Name'],family=family,strategy=a['Strategy'],status=metrics['status'],start=f['Time'],goal=f['Goal'],samples=[dict(t=s['Input']['Time']-f['Time'],s=s['Input']['Rider']['TotalReferenceMeters'],x=s['Input']['Rider']['Position']['X'],y=s['Input']['Rider']['Position']['Y'],heading=s['Input']['Rider']['Heading'],speed=s['Input']['Rider']['Speed']*3.6,drive=s['Input']['Active']['Drive'],gap=min(s['Inner'],s['Outer']),beta=math.degrees(s['Input']['Rider']['SlideRadians'])) for s in visible]))
    matrix=[]
    for fixture in ['first','second','historical']:
        for family in ['A','B','C']:
            for strategy in ['T0','T1']:
                candidates=[r for r in results if r['fixture']==fixture and r['family']==family and r['strategy']==strategy]
                if candidates:matrix.append(min(candidates,key=lambda r:(r['status']!='PASS',r['time_s'] if r['time_s'] is not None else float('inf'))))
    comparisons=[]
    for fixture in ['first','second','historical']:
        for strategy in ['T0','T1']:
            rs=[r for r in matrix if r['fixture']==fixture and r['strategy']==strategy and r['status']=='PASS']
            for a in rs:
                for b in rs:
                    if b['family']<=a['family']:continue
                    comparisons.append(dict(fixture=fixture,strategy=strategy,from_family=a['family'],to_family=b['family'],extra_distance_m=b['distance_m']-a['distance_m'],required_speed_gain_pct=(b['distance_m']/a['distance_m']-1)*100,actual_speed_gain_pct=(b['mean_speed_kph']/a['mean_speed_kph']-1)*100,time_delta_s=b['time_s']-a['time_s']))
    gains=[]
    for a in matrix:
        if a['strategy']!='T0' or a['status']!='PASS':continue
        b=next((r for r in matrix if r['fixture']==a['fixture'] and r['family']==a['family'] and r['strategy']=='T1' and r['status']=='PASS'),None)
        if b:gains.append(dict(fixture=a['fixture'],family=a['family'],gain_s=a['time_s']-b['time_s'],gain_pct=(a['time_s']-b['time_s'])/a['time_s']*100,gas_zero_delta_s=b['bend_gas_zero_s']-a['bend_gas_zero_s'],gas_full_delta_s=b['bend_gas_full_s']-a['bend_gas_full_s']))
    write(D/'analysis.json',dict(results=results,matrix_results=matrix,comparisons=comparisons,gains=gains,scope='Finite offline diagnostic witnesses. Matrix selects fastest native PASS in each cell from all preserved attempts. No global optimality, tyre calibration, autonomous success or full three-lap S claim.'))
    with (D/'matrix.csv').open('w',encoding='utf-8',newline='') as fp:
        w=csv.DictWriter(fp,fieldnames=list(matrix[0]));w.writeheader();w.writerows(matrix)
    with (D/'all-native.csv').open('w',encoding='utf-8',newline='') as fp:
        w=csv.DictWriter(fp,fieldnames=list(results[0]));w.writeheader();w.writerows(results)
    for name,rows in traces.items():
        with gzip.open(D/'native'/f'{name}-intervals.csv.gz','wt',encoding='utf-8',newline='') as fp:
            w=csv.DictWriter(fp,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
    write(D/'replay.json',dict(runs=replays,scope='Only saved native samples; independent maneuvers, relative time from their common initial state. No extrapolation.'))
    write(D/'telemetry-hashes.json',{str(p.relative_to(R)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((D/'native').glob('*.json.gz'))})
    elapsed=time.process_time()-began;ledger.append(dict(Label=tag,CpuSeconds=elapsed,Closed=True,Reason='native common-section and energy analysis'));write(D/'budget.json.next',ledger);(D/'budget.json.next').replace(D/'budget.json');print('analysis CPU',elapsed,'charged',sum(x['CpuSeconds'] for x in ledger));print(json.dumps(gains,indent=2));print(json.dumps(comparisons,indent=2))
if __name__=='__main__':main()
