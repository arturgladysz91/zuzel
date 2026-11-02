"""Read saved native states; all mechanics come from original C# contacts.

No physics integrator, tyre law or controller is implemented in this module.
"""
from pathlib import Path
import csv, gzip, hashlib, importlib.util, json, math, statistics, time

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'docs/experiments/geometry-speed-audit'
OLD = ROOT / 'docs/experiments/robust-three-lap'

def read(path):
    path = Path(path)
    return json.loads(gzip.decompress(path.read_bytes()) if path.suffix == '.gz' else path.read_bytes())

def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    content=(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False)+'\n').encode('utf-8')
    path.write_bytes(gzip.compress(content,mtime=0) if path.suffix=='.gz' else content)

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def module(path):
    spec = importlib.util.spec_from_file_location('energy145', path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result

def summary(rows):
    total = sum(q['dt_weight_s'] for q in rows)
    if not total: return None
    mean = lambda k: sum(q[k]*q['dt_weight_s'] for q in rows)/total
    return dict(duration_s=total, mean_speed_kph=mean('speed_kph'),
        mean_front_util=mean('front_util'), mean_rear_util=mean('rear_util'),
        front_saturated_s=sum(q['dt_weight_s'] for q in rows if q['front_util']>=.95),
        rear_saturated_s=sum(q['dt_weight_s'] for q in rows if q['rear_util']>=.95),
        zero_gas_s=sum(q['dt_weight_s'] for q in rows if q['interval_active_gas']==0),
        full_gas_s=sum(q['dt_weight_s'] for q in rows if q['interval_active_gas']==1),
        mean_abs_beta_deg=sum(abs(q['beta_deg'])*q['dt_weight_s'] for q in rows)/total,
        max_abs_beta_deg=max(abs(q['beta_deg']) for q in rows),
        mean_wheel_slip=mean('wheel_slip'), max_abs_wheel_slip=max(abs(q['wheel_slip']) for q in rows),
        mean_actual_lateral_acceleration_mps2=mean('actual_lateral_acceleration_mps2'),
        mean_grip_demand_fraction=mean('actual_grip_demand_fraction'),
        offset_min_m=min(q['offset_m'] for q in rows), offset_max_m=max(q['offset_m'] for q in rows),
        energy_J={k:sum(q[k]*q['dt_weight_s']/q['dt_s'] for q in rows) for k in rows[0] if k.endswith('_J')})

def geometry_stats(geometry):
    result=[]; capacity=geometry['Loads']['TotalCapacityNewtons']
    for track in geometry['Tracks']:
        sections=track['Sections']; starts=track['Starts']; bends=[]
        for bend in range(2):
            lo,hi=(starts[1],starts[4]) if bend==0 else (starts[5],starts[8])
            pts=[q for q in sections if lo<=q['Reference']<=hi]
            k=max(q['Curvature'] for q in pts); w=max(q['Width'] for q in pts)
            phases=[]
            for phase in range(3):
                part=[q for q in pts if phase/3<=(q['Reference']-lo)/(hi-lo)<(phase+1)/3]
                maximum=max(q['LineCurvature'] for q in part)
                phases.append(dict(phase=phase+1,min_line_radius_m=1/maximum,
                    scalar_no_drive_bound_kph=3.6*math.sqrt(capacity/(147*maximum)),
                    lateral_demand_38_mps2=(38/3.6)**2*maximum,
                    lateral_demand_60_mps2=(60/3.6)**2*maximum,
                    lateral_demand_90_mps2=(90/3.6)**2*maximum))
            bends.append(dict(bend=bend+1,length_reference_m=hi-lo,width_peak_m=w,
                min_reference_radius_m=1/k,min_line3_5_radius_m=1/k+3.5,
                scalar_line3_5_bound_kph=3.6*math.sqrt(capacity*(1/k+3.5)/147),phases=phases))
        result.append(dict(name=track['Name'],lap_m=track['LapMeters'],bends=bends,
            roundtrip_reference_error_max_m=max(abs(q['RoundTripReferenceError']) for q in sections),
            roundtrip_offset_error_max_m=max(abs(q['RoundTripOffsetError']) for q in sections),
            width_min_m=min(q['Width'] for q in sections),width_max_m=max(q['Width'] for q in sections),
            reference_k_max_per_m=max(q['Curvature'] for q in sections),
            boundary_curvature_max_per_m=max(abs(q['OuterCurvature']) for q in sections),
            joins=track['Starts']))
    return result

def main():
    began=time.process_time(); energy=module(ROOT/'tools/corner-speed-loss/analyze145.py')
    geometry=read(DOC/'geometry.json.gz'); analysis=read(OLD/'analysis.json'); old_rows={q['label']:q for q in analysis['trials'] if q['phase']=='final-v6'}
    native=[]; replay=[]; all_phases=[]
    for index in range(10):
        label=f'validation-v6-new-{index}'; path=OLD/'final-v6/native'/f'{label}.json.gz'; run=read(path)
        rider=run['Riders'][0]; samples=rider['Samples']; finish=rider['FinishSeconds']; points=[]
        for a,b in zip(samples,samples[1:]):
            dt=max(0,min(finish,b['Input']['Time'])-a['Input']['Time'])
            if not dt: continue
            q=energy.interval(a,b); q['dt_weight_s']=dt; q['interval_active_gas']=a['Input']['Active']['Drive']
            va=a['Input']['Rider']['Velocity']; vb=b['Input']['Rider']['Velocity']; dt0=q['dt_s']
            mx=(va['X']+vb['X'])/2; my=(va['Y']+vb['Y'])/2; speed=math.hypot(mx,my)
            ax=(vb['X']-va['X'])/dt0; ay=(vb['Y']-va['Y'])/dt0
            k=(mx*ay-my*ax)/speed**3 if speed>.1 else 0
            q['actual_curvature_per_m']=k; q['actual_lateral_acceleration_mps2']=speed*speed*k
            q['actual_grip_demand_fraction']=abs(147*speed*speed*k)/(q['front_capacity_N']+q['rear_capacity_N'])
            q['sweep_m']=b['Certificate']; q['segment']=b['Input']['Rider']['Road']['Segment']; points.append(q)
        old=old_rows[label]
        assert len(run['Crossings'])==3 and rider['CompletedLaps']==3 and old['pass_'] and run['Outcome']=='completed'
        assert abs(sum(q['dt_weight_s'] for q in points)-finish)<1e-7
        phases=[]
        for lap in range(3):
            for segment in range(9):
                part=[q for q in points if int(q['reference_m']/run['LapMeters'])==lap and q['segment']==segment]
                if part: phases.append(dict(trial=label,lap=lap+1,segment=segment,kind='bend' if segment in [1,2,3,5,6,7] else 'straight',**summary(part)))
        all_phases+=phases
        row=dict(label=label,source=str(path.relative_to(ROOT)).replace('\\','/'),source_sha256=digest(path),
            fingerprint=run['Fingerprint'],samples=len(samples),finish_s=finish,lap_times_s=old['lap_times_s'],
            six_bends=[q for q in analysis['bends'] if q['trial']==label],distance_m=old['distance_m'],
            minimum_pose_m=old['minimum_pose_m'],minimum_sweep_m=old['minimum_sweep_m'],
            cpu_s=old['charged_cpu_s'],callback_cpu=old['callback_cpu'],profiles=old['profiles'],
            all=summary(points),bends=summary([q for q in points if q['segment'] in [1,2,3,5,6,7]]),
            straights=summary([q for q in points if q['segment'] in [0,4,8]]))
        native.append(row)
        out=DOC/'telemetry'/f'{label}.csv.gz'; out.parent.mkdir(exist_ok=True)
        with gzip.open(out,'wt',encoding='utf-8',newline='') as f:
            w=csv.DictWriter(f,fieldnames=list(points[0]));w.writeheader();w.writerows(points)
        display=[]
        for i in sorted(set(range(0,len(samples),5))|{len(samples)-1}):
            s=samples[i]; v=s['Input']['Rider']
            display.append(dict(index=i,t=s['Input']['Time'],x=v['Position']['X'],y=v['Position']['Y'],s=v['TotalReferenceMeters'],
                heading=v['Heading'],speed=v['Speed']*3.6,beta=math.degrees(v['SlideRadians']),yaw=v['AngularVelocity'],gas=s['Input']['Active']['Drive'],
                steering=math.degrees(s['Input']['Steering']),wheel=s['Input']['Omega'],crank=s['CrankOmega'],gap=min(s['Inner'],s['Outer']),swept=s['Certificate']))
        replay.append(dict(name=label,scenario=old['scenario']['Id'],finish=finish,source=row['source'],sha256=row['source_sha256'],fingerprint=run['Fingerprint'],samples=display))
    pairs=read(DOC/'contact-pairs.json.gz')['Rows']; witnesses=[]
    for q in pairs:
        full=next(x for x in q['Queries'] if x['Gas']==1); active=q['State']['Input']['Active']['Drive']
        actual=next(x for x in q['Queries'] if x['Gas']==active)
        if q['DesiredAcceleration']<0 and full['AlongAcceleration']>0 and actual['AlongAcceleration']<0:
            witnesses.append(dict(time_s=q['Time'],segment=q['State']['Input']['Rider']['Road']['Segment'],
                speed_kph=q['State']['Input']['Rider']['Speed']*3.6,active_gas=active,proposed_gas=q['ControllerAction']['Drive'],
                desired_acceleration=q['DesiredAcceleration'],active_acceleration=actual['AlongAcceleration'],full_gas_acceleration=full['AlongAcceleration'],
                full_yaw_acceleration=full['YawAcceleration'],note='Same-state0.01s force response, not a safe continuation certificate'))
    operating=read(DOC/'operating-points.json'); equilibria=[]
    for q in operating['Rows']:
        best=q['Best']; norm=math.sqrt(sum(v*v for v in best['Residual'])) if best else None; cc=q['ClutchCheck']
        equilibria.append(dict(bend=q['Bend'],fraction=q['Fraction'],offset_m=q['Offset'],speed_kph=q['SpeedKph'],
            residual_N=norm,beta_deg=best['BetaDegrees'] if best else None,kappa=best['Kappa'] if best else None,
            gas=cc['Gas'] if cc else None,
            local_equilibrium=bool(cc and cc['WithinThrottle'] and cc['ClutchCapacitySatisfied'] and norm<.01 and abs(cc['YawTorque'])<.01 and abs(cc['WheelTorqueResidual'])<.01 and abs(cc['CrankTorqueResidual'])<.01),
            note='Local equilibrium only; no reachability, stability, swept reserve or full-bend claim'))
    cpu={}
    for r in native:
        for k,v in r['profiles'].items():cpu[k]=cpu.get(k,0)+v
    failures=[]
    for label in ['rest-speed12','rest-speed14','rest-full-gas','rest-yaw-slow']:
        f=read(DOC/'forecasts'/f'{label}.json.gz');ss=f['Points'];before=f['Actions'][-1];v=before['State']['Input']['Rider'];end=f['End']['Input']['Rider']
        pose=next((p['Time'] for p in ss if min(p['Inner'],p['Outer'])<.5),None)
        beta=next((p['Time'] for p in ss if abs(p['Rider']['SlideRadians'])>math.pi/4),None)
        failures.append(dict(label=label,last_review_time=before['State']['Input']['Time'],speed_before_kph=v['Speed']*3.6,
            beta_before_deg=math.degrees(v['SlideRadians']),yaw_before_rads=v['AngularVelocity'],offset_before_m=v['Road']['OffsetMeters'],
            radius_before_m=v['Road']['RadiusMeters'],constant_offset_lateral_demand_mps2=v['Speed']**2/v['Road']['RadiusMeters'],
            last_command=before['Action'],active_before=before['State']['Input']['Active'],pending_before=before['State']['Input']['Pending'],
            end_offset_m=end['Road']['OffsetMeters'],first_pose_below_half_m_time=pose,first_beta45_time=beta,
            scope='Recorded diagnostic forecasts; pose and beta first events only, swept minimum remains original C# certificate'))
    data=dict(base=geometry['Base'],verdict='PARTIAL',new_native_worlds=0,geometry=geometry_stats(geometry),native=native,
        phases=all_phases,causal_witnesses=witnesses,paired_contact_queries=len(pairs),equilibria=equilibria,
        cpu_profiles_s=cpu,forecast_trials=read(DOC/'forecast-summary.json')['Trials'],
        geometry_recheck=read(DOC/'geometry-recheck.json'),forecast_failure_diagnosis=failures,
        limitations=['No new faster autonomous S3 established.','No surveyed contemporary Motoarena boundary or banking reconstruction.','Finite unsuccessful equilibrium search is not impossibility proof.'])
    save(DOC/'analysis.json',data)
    save(DOC/'native-replay.json.gz',dict(scope='Only exact saved native samples; explicit original source indices. No interpolated physical states.',geometry=geometry['Tracks'],runs=replay))
    save(DOC/'analysis-cost.json',dict(cpu_s=time.process_time()-began,new_native_worlds=0))
    print(json.dumps(dict(native_runs=len(native),native_median_s=statistics.median(q['finish_s'] for q in native),causal_witnesses=len(witnesses),equilibria=sum(q['local_equilibrium'] for q in equilibria),cpu_profiles_s=cpu),indent=2))

if __name__=='__main__':main()
