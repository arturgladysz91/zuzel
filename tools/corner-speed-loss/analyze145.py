"""Read-only analysis of every saved #144 corner; never creates native worlds.

Forces in sample b were held on [a.time,b.time]. Work uses midpoint native
velocity and spin. Split an interval at a reporting boundary by its time fraction.
No attribution percentages or invented brake force are produced.
"""
import argparse, bisect, csv, gzip, hashlib, json, math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OLD = ROOT / 'docs/experiments/standing-start-three-laps'
MASS, INERTIA = 147., 52.72097

def read(path):
    return json.loads(gzip.decompress(path.read_bytes()) if str(path).endswith('.gz') else path.read_bytes())

def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False)+'\n', encoding='utf-8')

def dot(a, b): return a['X']*b['X']+a['Y']*b['Y']
def xy(x,y): return {'X':x,'Y':y}
def rider(s): return s['Input']['Rider']
def ref(s): return rider(s)['TotalReferenceMeters']
def time(s): return s['Input']['Time']

def point(samples, target):
    index = bisect.bisect_left([ref(s) for s in samples], target)
    if index >= len(samples) or target < ref(samples[0])-1e-7: return None
    b, a = samples[index], samples[max(0,index-1)]
    u = (target-ref(a))/(ref(b)-ref(a)) if ref(b)>ref(a) else 1
    lerp = lambda f: f(a)+(f(b)-f(a))*u
    return dict(time_s=lerp(time), reference_m=target,
                distance_m=lerp(lambda s:rider(s)['DistanceMeters']),
                speed_kph=lerp(lambda s:rider(s)['Speed']*3.6),
                x_m=lerp(lambda s:rider(s)['Position']['X']),y_m=lerp(lambda s:rider(s)['Position']['Y']))

def interval(a,b):
    v,w,f,p = rider(a),rider(b),b['Forces'],b['Powertrain']
    dt=time(b)-time(a)
    mid=xy((v['Velocity']['X']+w['Velocity']['X'])/2,(v['Velocity']['Y']+w['Velocity']['Y'])/2)
    spin=(v['AngularVelocity']+w['AngularVelocity'])/2
    vf=xy(mid['X']-spin*f['FrontArm']['Y'],mid['Y']+spin*f['FrontArm']['X'])
    vr=xy(mid['X']-spin*f['RearArm']['Y'],mid['Y']+spin*f['RearArm']['X'])
    front=dot(f['FrontForce'],mid)*dt
    rearx=f['RearLongitudinal']*dot(f['BodyForward'],mid)*dt
    reary=f['RearLateral']*dot(f['BodyLeft'],mid)*dt
    drag=dot(f['Resistance'],mid)*dt
    frontloss=-dot(f['FrontForce'],vf)*dt
    rearloss=-f['RearLateral']*dot(f['BodyLeft'],vr)*dt
    longloss=f['RearLongitudinal']*(.33*p['Wheel']['MidOmega']-dot(f['BodyForward'],vr))*dt
    trans=.5*MASS*(dot(w['Velocity'],w['Velocity'])-dot(v['Velocity'],v['Velocity']))
    yaw=.5*INERTIA*(w['AngularVelocity']**2-v['AngularVelocity']**2)
    wheel=.45*(b['Input']['Omega']**2-a['Input']['Omega']**2)
    crank=.005*(b['CrankOmega']**2-a['CrankOmega']**2)
    losses=frontloss+rearloss+longloss+p['Wheel']['BearingLossJoules']+p['ClutchHeatJoules']+p['TransmissionLossJoules']+p['CrankDragWorkJoules']-drag
    ax=(w['Velocity']['X']-v['Velocity']['X'])/dt;ay=(w['Velocity']['Y']-v['Velocity']['Y'])/dt
    speed=math.hypot(mid['X'],mid['Y']);cross=mid['X']*ay-mid['Y']*ax
    localradius=w['Road']['RadiusMeters']
    localradius=localradius if isinstance(localradius,(float,int)) and math.isfinite(localradius) and localradius>0 else None
    return dict(time_s=time(b),dt_s=dt,reference_m=ref(b),speed_kph=w['Speed']*3.6,
        acceleration_mps2=(w['Speed']-v['Speed'])/dt,
        requested_gas=b['RequestedDrive'],active_gas=b['Input']['Active']['Drive'],
        requested_steering_deg=math.degrees(b['RequestedSteering']),steering_deg=math.degrees(b['Input']['Steering']),
        beta_deg=math.degrees(w['SlideRadians']),yaw_rads=w['AngularVelocity'],
        wheel_slip=p['Wheel']['InstantSlipRatio'],front_fx_body_N=dot(f['FrontForce'],f['BodyForward']),
        front_fy_body_N=dot(f['FrontForce'],f['BodyLeft']),rear_fx_N=f['RearLongitudinal'],rear_fy_N=f['RearLateral'],
        front_util=math.hypot(*f['FrontForce'].values())/f['TyreLoads']['FrontCapacityNewtons'],rear_util=p['Wheel']['RearUtilization'],
        front_load_N=f['TyreLoads']['FrontNormalNewtons'],rear_load_N=f['TyreLoads']['RearNormalNewtons'],
        front_capacity_N=f['TyreLoads']['FrontCapacityNewtons'],rear_capacity_N=f['TyreLoads']['RearCapacityNewtons'],
        inner_m=b['Inner'],outer_m=b['Outer'],offset_m=w['Road']['OffsetMeters'],
        x_m=w['Position']['X'],y_m=w['Position']['Y'],clutch_torque_Nm=p['ClutchTorque'],
        drive_torque_Nm=p['Wheel']['DriveTorqueNewtonMeters'],crank_rpm=b['CrankOmega']*30/math.pi,
        trajectory_radius_m=speed**3/abs(cross) if abs(cross)>1e-6 and speed>.1 else None,
        local_offset_radius_m=localradius,local_radius_90_demand_N=MASS*625/localradius if localradius else None,
        front_translation_J=front,rear_drive_translation_J=rearx,rear_lateral_translation_J=reary,resistance_J=drag,
        translation_change_J=trans,yaw_change_J=yaw,wheel_change_J=wheel,crank_change_J=crank,
        front_yaw_work_J=f['FrontTorque']*spin*dt,rear_yaw_work_J=f['RearTorque']*spin*dt,
        front_slip_loss_J=frontloss,rear_lateral_slip_loss_J=rearloss,rear_long_slip_loss_J=longloss,
        combustion_J=p['CombustionWorkJoules'],clutch_heat_J=p['ClutchHeatJoules'],transmission_loss_J=p['TransmissionLossJoules'],
        bearing_loss_J=p['Wheel']['BearingLossJoules'],crank_drag_J=p['CrankDragWorkJoules'],
        body_work_residual_J=trans-front-rearx-reary-drag,
        total_energy_residual_J=trans+yaw+wheel+crank-p['CombustionWorkJoules']+losses)

def phase(rows,start,end):
    selected=[(q,max(0,min(q['time_s'],end)-max(q['time_s']-q['dt_s'],start))) for q in rows]
    selected=[(q,dt) for q,dt in selected if dt>1e-10]
    if not selected: return None
    duration=sum(dt for _,dt in selected)
    energy={k:sum(q[k]*dt/q['dt_s'] for q,dt in selected) for k in rows[0] if k.endswith('_J')}
    keys=['acceleration_mps2','requested_gas','active_gas','requested_steering_deg','steering_deg','beta_deg','yaw_rads','wheel_slip',
          'front_fx_body_N','front_fy_body_N','rear_fx_N','rear_fy_N','front_util','rear_util','inner_m','outer_m','offset_m','drive_torque_Nm','clutch_torque_Nm','crank_rpm']
    stats={k:dict(min=min(q[k] for q,_ in selected),max=max(q[k] for q,_ in selected),mean=sum(q[k]*dt for q,dt in selected)/duration) for k in keys}
    return dict(start_s=start,end_s=end,duration_s=duration,energy=energy,stats=stats,
        closed_gas_s=sum(dt for q,dt in selected if q['active_gas']==0),
        open_gas_decelerating_s=sum(dt for q,dt in selected if q['active_gas']>0 and q['acceleration_mps2']<0),
        negative_drive_torque_s=sum(dt for q,dt in selected if q['drive_torque_Nm']<0))

def analyze(run,starts):
    s=run['Riders'][0]['Samples']; L=run['LapMeters']; rows=[interval(a,b) for a,b in zip(s,s[1:])]
    corners=[]
    for lap in range(1,4):
        for bend,(entry,exit_) in enumerate([(1,4),(5,8)],1):
            lo,hi=(lap-1)*L+starts[entry],(lap-1)*L+starts[exit_]
            entrypoint,exitpoint=point(s,lo),point(s,hi)
            if not entrypoint or not exitpoint: continue
            inside=[q for q in s if lo<=ref(q)<=hi]
            minimum=min(inside,key=lambda q:rider(q)['Speed'])
            minimum_point=dict(time_s=time(minimum),reference_m=ref(minimum),distance_m=rider(minimum)['DistanceMeters'],
                x_m=rider(minimum)['Position']['X'],y_m=rider(minimum)['Position']['Y'],speed_kph=rider(minimum)['Speed']*3.6,
                corner_progress=rider(minimum)['Road']['CornerProgress'])
            thirds=[point(s,lo+(hi-lo)*k/3)['time_s'] for k in range(4)]
            portions={name:phase(rows,a,b) for name,a,b in zip(['entry','middle','exit'],thirds,thirds[1:])}
            lossrows=[q for q in rows if entrypoint['time_s']<=q['time_s']<=time(minimum)]
            # First deceleration in the corner; preparation separately includes 1s before entry.
            onset=next((q for q in lossrows if q['acceleration_mps2']<-.05),None)
            pre=next((q for q in rows if entrypoint['time_s']-1<=q['time_s']<=entrypoint['time_s'] and q['acceleration_mps2']<-.05),None)
            worst=min((q for q in rows if lo<=q['reference_m']<=hi),key=lambda q:q['acceleration_mps2'])
            loss=phase(rows,entrypoint['time_s'],time(minimum))
            leading=sorted(['front_translation_J','rear_lateral_translation_J','resistance_J'],key=lambda k:loss['energy'][k]) if loss else []
            corners.append(dict(run=run['Name'],lap=lap,bend=bend,entry=entrypoint,minimum=minimum_point,exit=exitpoint,
                duration_s=exitpoint['time_s']-entrypoint['time_s'],distance_m=exitpoint['distance_m']-entrypoint['distance_m'],
                deceleration_onset=onset,preparation_deceleration=pre,worst_deceleration=worst,phases=portions,
                entry_to_minimum=loss,measured_leading_negative_body_work=leading))
    return corners,rows

def main():
    p=argparse.ArgumentParser();p.add_argument('--output',type=Path,default=ROOT/'docs/experiments/corner-speed-loss');p.add_argument('--native',type=Path)
    args=p.parse_args();out=args.output;out.mkdir(parents=True,exist_ok=True)
    g=read(ROOT/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json');starts=[q['startReferenceMeters'] for q in g['segments']]
    paths=[OLD/'native'/f'{n}.json.gz' for n in ['A','B','F','K1']]
    if args.native: paths+=sorted(p for p in args.native.glob('*.json.gz') if (args.native/(p.name.removesuffix('.json.gz')+'-result.json')).exists())
    allcorners=[];sources=[];runs=[]
    for path in paths:
        run=read(path);corners,rows=analyze(run,starts);allcorners+=corners
        sources.append(dict(path=str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        runs.append(dict(name=run['Name'],outcome=run['Outcome'],laps=run['Riders'][0]['CompletedLaps'],crossings=run['Crossings']))
        with gzip.GzipFile(filename='',fileobj=(out/f"{run['Name']}-intervals.csv.gz").open('wb'),mode='wb',mtime=0) as z:
            import io
            stream=io.TextIOWrapper(z,encoding='utf-8',newline='');writer=csv.DictWriter(stream,fieldnames=list(rows[0]),lineterminator='\n');writer.writeheader();writer.writerows(rows);stream.flush()
        for c in corners:
            print(f"{c['run']:16} L{c['lap']} B{c['bend']} {c['entry']['speed_kph']:7.3f} {c['minimum']['speed_kph']:7.3f} {c['exit']['speed_kph']:7.3f} t={c['duration_s']:.3f} d={c['distance_m']:.2f} min@{c['minimum']['time_s']:.3f}")
    save(out/'saved-analysis.json',dict(native_worlds=0,method=__doc__,sources=sources,runs=runs,corners=allcorners))
    with (out/'corners.csv').open('w',encoding='utf-8',newline='') as f:
        writer=csv.writer(f,lineterminator='\n');writer.writerow(['run','lap','bend','entry_kph','minimum_kph','exit_kph','seconds','distance_m','min_time_s','min_reference_m','min_x_m','min_y_m'])
        for c in allcorners: writer.writerow([c['run'],c['lap'],c['bend'],c['entry']['speed_kph'],c['minimum']['speed_kph'],c['exit']['speed_kph'],c['duration_s'],c['distance_m'],c['minimum']['time_s'],c['minimum']['reference_m'],c['minimum']['x_m'],c['minimum']['y_m']])

if __name__=='__main__': main()
