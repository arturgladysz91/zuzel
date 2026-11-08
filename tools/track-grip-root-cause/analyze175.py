"""Recompute #175 tables and plots from read-only native and static receipts."""
import argparse
import csv
import gzip
import io
import json
import math
from pathlib import Path
import time
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'docs/experiments/track-grip-root-cause'
MASS, INERTIA, G = 147, 52.72097, 9.81
V = (60, 70, 80, 90, 100, 110, 120)
ARCHIVES = {
    '172-D': 'three-lap-global-planning/172-development.zip',
    '172-C0': 'three-lap-global-planning/172-validation-C0.zip',
    '172-K4': 'three-lap-global-planning/172-validation-K4.zip',
    '173': 'throttle-decision-audit/173-native.zip',
    '174': 'state-feasible-throttle/174-native.zip',
    '168': 'throttle-contact-audit/168-dowody.zip',
    '169': 'full-drive-stability/169-dowody.zip',
}


def read(path):
    data = path.read_bytes()
    return json.loads(gzip.decompress(data) if path.suffix == '.gz' else data)


def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False) + '\n',
                    encoding='utf-8', newline='\n')


def xy(point):
    return point['X'], point['Y']


def norm(vector):
    return np.linalg.norm(vector, axis=-1)


def dot(a, b):
    return np.sum(a * b, axis=-1)


def left(v):
    return np.stack((-v[..., 1], v[..., 0]), axis=-1)


def integrate(y, x):
    return float(np.sum((y[1:] + y[:-1]) / 2 * np.diff(x)))


def widths(t, amplitude):
    u = 2 * np.minimum(t, 1 - t)
    return 12 + amplitude * u**3 * (10 + u * (-15 + 6 * u))


def independent_lengths(lap, compression, amplitudes):
    """Independent quadrature in arc length, without C# sampled positions."""
    n = 100_000
    t = np.linspace(0, 1, n + 1)
    u = np.minimum(1, np.minimum(t, 1 - t) / .05)
    profile = u**3 * (10 + u * (-15 + 6 * u)) * (1 + compression * np.cos(2 * np.pi * t))
    length = (lap - 124) / 2
    k = np.pi * profile / (length * integrate(profile, t))
    out = {key: 124.0 for key in ('Reference', 'Inner', 'Outer', 'Middle', 'Offset35')}
    for a in amplitudes:
        for key, d in [('Reference', np.zeros_like(t)), ('Inner', -np.ones_like(t)),
                       ('Outer', widths(t, a) - 1), ('Middle', widths(t, a) / 2 - 1),
                       ('Offset35', np.full_like(t, 3.5))]:
            derivative = np.gradient(d, length / n, edge_order=2)
            out[key] += length * integrate(np.hypot(1 + k * d, derivative), t)
    return out


def geometry(geoms):
    stats = []
    curves = []
    for g in geoms:
        dense = g['Dense']
        keys = ('Reference', 'Inner', 'Outer', 'Middle', 'Offset35')
        arrays = {key: np.array([xy(q[key]) for q in dense]) for key in keys}
        lengths = {key: float(norm(np.diff(array, axis=0)).sum()) for key, array in arrays.items()}
        name = g['Name']
        independently = None
        if name in ('progressive-b', 'progressive-c'):
            independently = independent_lengths(g['LapMeters'], .2, (4.6, 4.6) if name.endswith('-b') else (5, 4.2))
            assert max(abs(lengths[k] - independently[k]) for k in keys) < .001
        sections = []
        for bend, entries in enumerate(g['Bends'], 1):
            s = np.array([q['Progress'] for q in entries])
            bend_index = 1 if bend == 1 else 5
            start = g['Starts'][bend_index]
            limit = g['Starts'][bend_index + 3]
            length = limit - start
            q = [r['Geometry'] for r in entries]
            k = np.array([r['K'] for r in q])
            w = np.array([r['Width'] for r in q])
            if name == 'circular-baseline':
                k[:] = 1 / 31
                w[:] = 16.6
                outer_k = k / (1 + k * (w - 1))
            else:
                z = s / length
                if name in ('progressive-b', 'progressive-c'):
                    amp = 4.6 if name.endswith('-b') else (5 if bend == 1 else 4.2)
                    u = 2 * np.minimum(z, 1 - z)
                    rate = 2 / length
                    first = amp * 30 * u*u * (1-u)**2 * rate * np.where(z <= .5, 1, -1)
                    second = amp * 60 * u * (1-u) * (1-2*u) * rate**2
                else:
                    u = np.minimum(1, np.minimum(s, length - s) / 10)
                    first = 4.6 * 30 * u*u * (1-u)**2 / 10 * np.where(z <= .5, 1, -1)
                    second = 4.6 * 60 * u * (1-u) * (1-2*u) / 100
                # Arc-length curvature of r + d outward, retaining both d' and d''.
                d = w - 1
                a = 1 + k*d
                kp = np.gradient(k, s, edge_order=2)
                outer_k = (k*a*a - a*second + kp*d*first + 2*k*first*first) / (a*a + first*first)**1.5
            for i in range(len(s)):
                def radius(kk):
                    return None if abs(kk) < 1e-9 else 1 / abs(float(kk))
                curves.append(dict(geometry=name, bend=bend, progress_m=float(s[i]), fraction=float(s[i]/length),
                                   width_m=float(w[i]), k_ref=float(k[i]), r_ref=radius(k[i]),
                                   r_inner=radius(k[i]/(1-k[i])), k_outer=float(outer_k[i]), r_outer=radius(outer_k[i]),
                                   **{f'Fy_{v}_N': MASS*(v/3.6)**2*float(k[i]) for v in V}))
            inner = [r for r in dense if start <= r['S'] <= limit]
            peak = max(inner, key=lambda r: r['K'])
            groups = [(0, .3), (.3, .7), (.7, 1)]
            k_max = [max(r['K'] for r in inner if lo <= (r['S']-start)/length <= hi) for lo, hi in groups]
            sections.append(dict(length_m=length, peak_reference_m=peak['S'], peak_fraction=(peak['S']-start)/length,
                                 min_radius_m=1/peak['K'], max_width_m=max(r['Width'] for r in inner),
                                 min_radius_by_30_40_30_m=[1/k for k in k_max]))
        stats.append(dict(name=name, lap_m=g['LapMeters'], segment_lengths_m=list(np.diff(g['Starts'])),
                          xy_lengths_m=lengths, independent_m=independently, bends=sections))
    return stats, curves


def native_members():
    for study, name in ARCHIVES.items():
        if study in ('168', '169'):
            continue
        with ZipFile(ROOT / 'docs/experiments' / name) as outer:
            for member in outer.namelist():
                if not member.endswith('.zip'):
                    continue
                with ZipFile(io.BytesIO(outer.read(member))) as z:
                    decisions = json.loads(z.read('decisions.json'))
                    yield study, Path(member).stem, json.loads(z.read('native.json')), json.loads(z.read('result.json')), decisions


def native_trace(run):
    samples = run['Riders'][0]['Samples']
    states = [q['Input']['Rider'] for q in samples]
    t = np.array([q['Input']['Time'] for q in samples])
    s = np.array([v['TotalReferenceMeters'] for v in states])
    dist = np.array([v['DistanceMeters'] for v in states])
    position = np.array([xy(v['Position']) for v in states])
    vel = np.array([xy(v['Velocity']) for v in states])
    speed = norm(vel)
    course = np.divide(vel, np.maximum(speed, 1e-8)[:, None])
    heading = np.unwrap([v['Heading'] for v in states])
    yaw = np.array([v['AngularVelocity'] for v in states])
    beta = np.degrees([v['SlideRadians'] for v in states])
    road_h = np.array([v['Road']['Heading'] for v in states])
    offset = np.array([v['Road']['OffsetMeters'] for v in states])
    radius = np.array([float(v['Road']['RadiusMeters']) for v in states])
    k_road_line = np.divide(1, radius, where=radius > 0, out=np.zeros_like(radius))
    k_ref = k_road_line / (1 - k_road_line*offset)
    pose = np.array([min(q['Inner'], q['Outer']) for q in samples])
    swept = np.array([q['Certificate'] if q['Certificate'] is not None else np.nan for q in samples])
    gas = np.array([q['Input']['Active']['Drive'] for q in samples])
    requested = np.array([q['RequestedDrive'] for q in samples])
    steering = np.degrees([q['Input']['Steering'] for q in samples])
    crank = np.array([q['CrankOmega'] for q in samples])
    wheel = np.array([q['Input']['Omega'] for q in samples])
    dt = np.diff(t)
    force = [q['Forces'] for q in samples[1:]]
    power = [q['Powertrain'] for q in samples[1:]]
    fx = np.array([q['RearLongitudinal'] for q in force])
    fy = np.array([q['RearLateral'] for q in force])
    ff = np.array([q['FrontLateral'] for q in force])
    br = np.array([q['TyreLoads']['RearCapacityNewtons'] for q in force])
    bf = np.array([q['TyreLoads']['FrontCapacityNewtons'] for q in force])
    steer_left = np.array([xy(q['FrontLeft']) for q in force])
    body_left = np.array([xy(q['BodyLeft']) for q in force])
    body_forward = np.array([xy(q['BodyForward']) for q in force])
    rear_velocity = np.array([xy(q['RearVelocity']) for q in force])
    alpha = np.degrees(np.arctan2(-dot(rear_velocity, body_left), dot(rear_velocity, body_forward)))
    kappa = np.array([q['Wheel']['InstantSlipRatio'] for q in power])
    normal = left(course[:-1])
    cap = (bf*abs(dot(steer_left, normal)) + np.sqrt(np.maximum(0, br**2-fx**2))*abs(dot(body_left, normal)) +
           fx*dot(body_forward, normal)) / MASS
    along_force = dot(np.array([xy(q['NetForce']) for q in force]), course[:-1])
    actual_normal = dot(np.array([xy(q['NetForce']) for q in force]), normal) / MASS
    vy = (vel[:-1] + vel[1:]) / 2
    om = (yaw[:-1] + yaw[1:]) / 2
    v_front = vy + om[:, None]*left(np.array([xy(q['FrontArm']) for q in force]))
    v_rear = vy + om[:, None]*left(np.array([xy(q['RearArm']) for q in force]))
    energy = {key: np.array([q[name] for q in power]) for key, name in [
        ('combustion', 'CombustionWorkJoules'), ('clutch', 'ClutchHeatJoules'),
        ('transmission', 'TransmissionLossJoules'), ('crank_drag', 'CrankDragWorkJoules')]}
    for key, name in [('wheel_slip', 'LongitudinalLossJoules'), ('bearing', 'BearingLossJoules'),
                      ('wheel_input', 'EngineWorkJoules')]:
        energy[key] = np.array([q['Wheel'][name] for q in power])
    resistance = np.array([xy(q['Resistance']) for q in force])
    energy['front_lateral'] = -dot(np.array([xy(q['FrontForce']) for q in force]), v_front)*dt
    energy['rear_lateral'] = -fy*dot(body_left, v_rear)*dt
    energy['resistance'] = -dot(resistance, vy)*dt
    kinetic = .5*(MASS*speed**2 + INERTIA*yaw**2 + .9*wheel**2 + .01*crank**2)
    energy['delta_kinetic'] = np.diff(kinetic)
    losses = ('clutch', 'transmission', 'crank_drag', 'wheel_slip', 'bearing',
              'front_lateral', 'rear_lateral', 'resistance')
    energy['closure_residual'] = energy['combustion'] - sum(energy[k] for k in losses) - energy['delta_kinetic']
    # Signed XY curvature from chords of a position interpolant in physical arc length.
    # Centre span 2 m and grid 0.25 m suppress numerical noise from float Box2D points.
    keep = np.r_[True, np.diff(dist) > 1e-7]
    grid = np.arange(0, dist[-1], .25)
    k_grid = np.zeros_like(grid)
    if len(grid) > 9:
        points = np.column_stack([np.interp(grid, dist[keep], position[keep, j]) for j in (0, 1)])
        p, q = points[4:-4]-points[:-8], points[8:]-points[4:-4]
        cross = p[:, 0]*q[:, 1] - p[:, 1]*q[:, 0]
        k_grid[4:-4] = 2*cross / np.maximum(1e-8, norm(p)*norm(q)*norm(p+q))
    k_xy = np.interp(dist, grid, k_grid) if len(grid) else np.zeros_like(dist)
    ref_velocity = (vel[:, 0]*np.cos(road_h) + vel[:, 1]*np.sin(road_h))/(1+k_ref*offset)
    ref_error = np.diff(s)/dt - (ref_velocity[1:] + ref_velocity[:-1])/2
    return dict(t=t, s=s, dist=dist, xy=position, speed=speed, heading=heading, yaw=yaw, beta=beta, pose=pose,
                swept=swept, gas=gas, requested=requested, steering=steering, crank=crank, wheel=wheel, kappa=kappa,
                fx=fx, fy=fy, ff=ff, alpha=alpha, cap=cap, actual_normal=actual_normal, along_force=along_force,
                k_xy=k_xy, k_ref=k_ref, k_line=k_road_line, ref_error=ref_error, energy=energy,
                resistance=norm(resistance), wheel_capacity=br,
                rear_util=np.hypot(fx, fy)/br, front_util=np.abs(ff)/bf)


def window(q, a, b):
    t = q['t']; dt = np.diff(t)
    fraction = np.clip(np.minimum(t[1:], b) - np.maximum(t[:-1], a), 0, dt) / dt
    active = fraction > 0
    return dict(seconds=b-a, energy_J={k: float(dot(value, fraction)) for k, value in q['energy'].items()},
                avg_gas=float(dot(q['gas'][1:], fraction*dt)/(b-a)),
                mean_Fx=float(dot(q['fx'], fraction*dt)/(b-a)),
                mean_resistance=float(dot(q['resistance'], fraction*dt)/(b-a)),
                mean_util_rear=float(dot(q['rear_util'], fraction*dt)/(b-a)),
                max_kappa=float(np.max(q['kappa'][active])),
                mean_accel=float((np.interp(b,t,q['speed'])-np.interp(a,t,q['speed']))/(b-a)))


def extract_native(run, result, g):
    q = native_trace(run)
    t, s, speed = q['t'], q['s'], q['speed']
    crossing = [c['Time'] for c in run['Crossings']]
    physical_distance = [c['PhysicalDistance'] for c in run['Crossings']]
    end = result['FinishTime'] or t[-1]
    last = q['t'] <= end
    bends = []
    for lap in range(3):
        for number, i in enumerate((1, 5), 1):
            lo, hi = (g['Starts'][j] + lap*g['LapMeters'] for j in (i, i+3))
            if hi > s[-1]:
                continue
            cuts = np.interp([lo, lo+.3*(hi-lo), lo+.7*(hi-lo), hi], s, t)
            speed_at = np.interp(cuts, t, speed)*3.6
            use = (s >= lo) & (s <= hi)
            k = q['k_xy'][use]
            cap = q['cap'][use[:-1]]
            k_for_cap = np.abs(q['k_xy'][:-1][use[:-1]])
            demand = {str(v): dict(max_ay=float(np.max(k_for_cap*(v/3.6)**2)),
                                   fraction_of_reference_samples_over=float(np.mean(k_for_cap*(v/3.6)**2 > cap)))
                      for v in (70, 80, 90, 100, 110)}
            bends.append(dict(lap=lap+1, bend=number, entry_kph=float(speed_at[0]), at_30_kph=float(speed_at[1]),
                              at_70_kph=float(speed_at[2]), exit_kph=float(speed_at[3]), minimum_kph=float(np.min(speed[use])*3.6),
                              max_k_xy=float(np.max(np.abs(k))), demand=demand,
                              entry_window=window(q, cuts[0], cuts[1]), middle_window=window(q, cuts[1], cuts[2]),
                              exit_window=window(q, cuts[2], cuts[3])))
    summary = dict(finish_s=result['FinishTime'], reserve=result['Reserve'], s3=result['S3'],
                   outcome=result['Reason'], first_illegal_s=result['FirstBadTime'], elapsed_s=float(t[-1]),
                   laps_s=list(np.diff([0]+crossing)), laps_actual_m=list(np.diff([0]+physical_distance)),
                   start_ref_m=float(s[0]), end_ref_m=float(s[-1]), end_actual_m=float(q['dist'][-1]),
                   distance_xy_error_m=float(norm(np.diff(q['xy'], axis=0)).sum()-q['dist'][-1]),
                   reference_speed_abs_p95_error=float(np.percentile(abs(q['ref_error']),95)),
                   vmax_kph=float(np.max(speed[last])*3.6), rpm_max=float(np.max(q['crank'])*30/np.pi),
                   wheel_equivalent_rpm_max=float(np.max(q['wheel'])*9*30/np.pi),
                   rpm_max_during_scored_run=float(np.max(q['crank'][last])*30/np.pi),
                   min_pose=result['MinPose'], min_swept=result['MinSweep'], beta_max=result['MaxBetaDegrees'],
                   cpu_s=run['TotalCpuSeconds'],
                   ledger=window(q, 0, end), bends=bends)
    return summary, q


def analytic(params):
    loads = params['Capacities']
    mu, bf, br = (loads[k] for k in ('EffectiveFrictionCoefficient','FrontCapacityNewtons','RearCapacityNewtons'))
    rpms, torques = np.array([(p['Rpm'], p['NewtonMeters']) for p in params['Curve']]).T
    engine = []
    for kph in (70, 80, 90, 100, 110, 120):
        v = kph/3.6
        for ratio in (8,9,10):
            for kappa in (0,.2,.5):
                omega = (v+kappa*math.hypot(v,.5))/.33
                rpm = omega*ratio*30/math.pi
                tq = np.interp(rpm, rpms, torques, right=0)
                resist = 40+.2*v*v
                engine.append(dict(v_kph=kph, ratio=ratio, kappa=kappa, rpm=rpm, torque_Nm=tq,
                                   crank_kW=tq*rpm*math.pi/30/1000, locked_shaft_force_N=tq*ratio*.94/.33,
                                   resistance_N=resist))
    contact = []
    # Original scales and the exact M4 static laws; derivative at alpha=0.
    for name, ratio in [('BASE',1),('M1',1/.9175),('M2',1.1),('M3',.9),('M4',1)]:
        for v in (70/3.6, 90/3.6, 110/3.6):
            for kappa in (0,.05,.1,.2,.4,.6,1):
                for alpha in (0,2,5,10,15):
                    rear, front = br*ratio, bf*ratio
                    dx = math.hypot(.1*math.hypot(v,.5),rear*(.33**2/.9+1/MASS)*.03)
                    dy = math.hypot(.12*math.hypot(v,.5),(front+rear)/MASS*.03)
                    slip, side = kappa*math.hypot(v,.5), v*math.tan(math.radians(alpha))
                    a,b = slip/dx,side/dy
                    pure = rear*a/math.hypot(1,a)
                    if name == 'M4':
                        fx,fy = pure/math.hypot(1,b),-rear*b/math.hypot(1,b)
                        cr = rear*v/dy
                    else:
                        den = math.sqrt(1+a*a+b*b)
                        fx,fy = rear*a/den,-rear*b/den
                        cr = rear*v/dy/math.hypot(1,a)
                    cf = front*v/dy
                    coupling = .7*cf-.6*cr
                    aa = -(cf+cr+pure)/(MASS*v)
                    dd = -(.7**2*cf+.6**2*cr)/(INERTIA*v)
                    off = (1+coupling/(MASS*v*v))*coupling/INERTIA
                    eig = (aa+dd+math.sqrt(max(0,(aa-dd)**2+4*off)))/2
                    contact.append(dict(variant=name, kph=v*3.6, kappa=kappa, alpha_deg=alpha, Fx_N=fx,Fy_N=fy,
                                        Br_N=rear,Bf_N=front,Cr_N_rad=cr, yaw_eigen_s=eig,
                                        spare_Fy_N=math.sqrt(max(0,rear**2-fx**2)), loss_W=fx*slip-fy*side))
    bank = []
    for slope in (0,.04,.06,.07,.10):
        theta = math.atan(slope)
        a = G*(math.sin(theta)+mu*math.cos(theta))/(math.cos(theta)-mu*math.sin(theta))
        bank.append(dict(grade=slope, angle_deg=math.degrees(theta), max_horizontal_ay=a,
                         speed_gain_pct=100*(math.sqrt(a/(mu*G))-1)))
    return dict(engine=engine, contact=contact, bank=bank)


def paired(rows, traces):
    output = []
    for row in rows:
        sample = row['Sample']; v = sample['Input']['Rider']
        base = row['Results'][0]
        run_id = row['Source'].split('/', 1)[1]
        if row['Source'].startswith('174/'):
            run_id = 'd1-' + run_id
        trace = traces[run_id]
        k_xy = abs(float(np.interp(sample['Input']['Time'], trace['t'], trace['k_xy'])))
        velocity = np.array(xy(v['Velocity']))
        n = left(velocity / norm(velocity))
        for r in row['Results']:
            f, p = r['Force'], r['Power']
            loads = f['TyreLoads']; mu = loads['EffectiveFrictionCoefficient']
            fx, fy = f['RearLongitudinal'], f['RearLateral']
            cap = mu*G
            k = abs(row['KBase'])
            bf = loads['FrontCapacityNewtons']
            fy_left = r['FyRemainingAtFx']
            normal_cap = (bf*abs(dot(xy(f['FrontLeft']), n)) +
                          fy_left*abs(dot(xy(f['BodyLeft']), n)) +
                          fx*dot(xy(f['BodyForward']), n)) / MASS
            yaw_bound = norm(xy(f['FrontArm']))*bf + norm(xy(f['RearArm']))*fy_left
            output.append(dict(source=row['Source'], region=row['Region'], state_sha=row['StateSha256'],
                               time=sample['Input']['Time'], s=v['TotalReferenceMeters'], speed_kph=v['Speed']*3.6,
                               active_gas=row['Next']['Input']['Active']['Drive'], variant=r['Id'], mu=mu,
                               Fx_N=fx, FyRear_N=fy, FyFront_N=f['FrontLateral'], FyAvailable_N=r['FyRemainingAtFx'],
                               FzRear_N=loads['RearNormalNewtons'], FzFront_N=loads['FrontNormalNewtons'],
                               front_cap_N=loads['FrontCapacityNewtons'], rear_cap_N=loads['RearCapacityNewtons'],
                               yaw_Nm=f['NetTorque'], yaw_accel=r['YawAcceleration'], kappa=p['Wheel']['InstantSlipRatio'],
                               ax=r['Along'], ay=r['Normal'], d_ax=r['Along']-base['Along'],
                               k_parallel=k, k_actual_xy=k_xy, normal_cap=normal_cap,
                               yaw_abs_capacity_Nm=yaw_bound,
                               v_parallel_scalar_kph=3.6*math.sqrt(cap/k) if k>1e-9 else None,
                               v_xy_scalar_kph=3.6*math.sqrt(cap/k_xy) if k_xy>1e-9 else None,
                               v_xy_fixed_Fx_kph=3.6*math.sqrt(max(0,normal_cap)/k_xy) if k_xy>1e-9 else None,
                               clutch_W=p['ClutchHeatJoules']/p['Seconds'],
                               slip_W=p['Wheel']['LongitudinalLossJoules']/p['Seconds'],
                               crank_rpm=p['Engine']['EngineRpm'], wheel_omega=p['Wheel']['BeforeOmega'],
                               model_only=True))
    return output


def csv_file(path, rows):
    with path.open('w', encoding='utf-8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def figures(geoms, curves, traces, summary, rows):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt

    plt.rcParams.update({'font.size': 9})
    colors = ('#166b9b','#cb6232','#348566','#696b81')
    fig, ax = plt.subplots(1, 2, figsize=(13, 5), layout='constrained')
    for g,c in zip(geoms,colors):
        p = np.array([xy(q['Reference']) for q in g['Dense']])
        ax[0].plot(*p.T, color=c, label=g['Name'], lw=1.2, alpha=.85)
        if g['Name']=='progressive-b':
            for e in ('Inner','Outer'):
                ax[0].plot(*np.array([xy(q[e]) for q in g['Dense']]).T,color='.5',lw=.65)
    q = traces['v1-K4-S1']
    ax[0].plot(*q['xy'][::6].T,color='#8c3990',lw=.9,label='K4 S1, zapis Box2D')
    ax[0].axis('equal');ax[0].set_xlabel('X [m]');ax[0].set_ylabel('Y [m]');ax[0].legend(fontsize=8)
    for name,c in zip([g['Name'] for g in geoms], colors):
        for bend in (1,2):
            r=[q for q in curves if q['geometry']==name and q['bend']==bend]
            ax[1].plot([q['fraction']*100+(bend-1)*110 for q in r],[q['k_ref'] for q in r],color=c,
                       label=name if bend==1 else None,ls='--' if name=='progressive-c' else '-')
    for x in (0,110):
        ax[1].axvspan(x,x+30,color='.93');ax[1].axvspan(x+70,x+100,color='.93')
    ax[1].set_xlabel('Postęp łuku I (0–100) i II (110–210) [%]');ax[1].set_ylabel('Krzywizna referencji [1/m]')
    ax[1].legend(fontsize=8);fig.suptitle('Warianty obliczeniowe · brak geodezji XY współczesnej Motoareny')
    fig.savefig(DOC/'01-geometria.png',dpi=155);plt.close(fig)

    fig, ax = plt.subplots(2,1,figsize=(13,7),sharex=True,layout='constrained')
    s=q['s'];la=geoms[0]['LapMeters'];mask=(s>=la)&(s<=2*la)
    ax[0].plot(s[mask]-la,q['k_ref'][mask],label='κ referencji')
    ax[0].plot(s[mask]-la,q['k_line'][mask],label='κ stałego offsetu')
    ax[0].plot(s[mask]-la,q['k_xy'][mask],label='κ z trajektorii XY (2 m)',lw=1)
    ax[0].set_ylabel('1/m');ax[0].legend(ncol=3)
    for v,c in zip((70,80,90,100,110),plt.cm.viridis(np.linspace(.13,.9,5))):
        ax[1].plot(s[mask]-la,abs(q['k_xy'][mask])*(v/3.6)**2,color=c,lw=.85,label=f'{v} km/h')
    ax[1].plot(s[:-1][mask[:-1]]-la,q['cap'][mask[:-1]],color='#a63237',lw=1.4,label='Pułap przy zapisanym Fx')
    ax[1].plot(s[:-1][mask[:-1]]-la,q['actual_normal'][mask[:-1]],color='.45',lw=.9,label='F normalna / m')
    ax[1].set_xlabel('Postęp w okrążeniu 2 [m]');ax[1].set_ylabel('a boczne [m/s²]');ax[1].legend(ncol=4)
    fig.suptitle('K4 S1 · wymagania tej samej linii przy innych prędkościach')
    fig.savefig(DOC/'02-krzywizna-budzet.png',dpi=155);plt.close(fig)

    fig,ax=plt.subplots(2,2,figsize=(12,7),layout='constrained')
    y=summary['analytic']['contact']
    for tag,c in [('BASE',colors[0]),('M1',colors[1]),('M2',colors[2]),('M3',colors[3]),('M4','#a33879')]:
        r=[z for z in y if z['variant']==tag and z['kph']==70 and z['alpha_deg']==5]
        ax[0,0].plot([z['Fx_N'] for z in r],[-z['Fy_N'] for z in r],'.-',label=tag,color=c)
        ax[0,1].plot([z['kappa'] for z in r],[z['Cr_N_rad'] for z in r],'.-',label=tag,color=c)
        ax[1,0].plot([z['kappa'] for z in r],[z['yaw_eigen_s'] for z in r],'.-',label=tag,color=c)
    ax[0,0].set(xlabel='Fx [N]',ylabel='|Fy| [N]',title='70 km/h, αr=5°, poślizg rośnie wzdłuż krzywej')
    ax[0,1].set(xlabel='κ',ylabel='Cr [N/rad]',title='Sztywność tylna wokół αr=0')
    ax[1,0].set(xlabel='κ',ylabel='Re λmax [1/s]',title='Lokalna stateczność β–yaw przy zamrożonym κ')
    ax[1,0].axhline(0,color='.4',lw=.8)
    r=[z for z in rows if z['source']=='174/P7' and z['region']=='exit-wheelspin' and z['variant'].startswith('gas-')]
    ax[1,1].plot([int(z['variant'][4:]) for z in r],[z['Fx_N'] for z in r],'.-',label='Fx')
    ax[1,1].plot([int(z['variant'][4:]) for z in r],[z['FyRear_N'] for z in r],'.-',label='Fy tył')
    ax[1,1].set(xlabel='Gaz w zapytaniu [%]',ylabel='N',title='P7 · stan koła i wału identyczny, jeden krok')
    for a in ax.flat:a.legend(fontsize=8);a.grid(alpha=.2)
    fig.savefig(DOC/'03-kontakt-i-yaw.png',dpi=155);plt.close(fig)

    fig,ax=plt.subplots(5,1,figsize=(13,12),sharex=True,layout='constrained')
    for name,z in traces.items():
        if name.startswith('v1-K4'):
            ax[0].plot(z['t'],z['speed']*3.6,label=name.removeprefix('v1-K4-'),alpha=.68,lw=.9)
    ax[0].legend(ncol=5);ax[0].set_ylabel('V [km/h]')
    ax[1].plot(q['t'],q['gas'],label='aktywny');ax[1].plot(q['t'],q['requested'],label='żądany',alpha=.66);ax[1].legend()
    ax[1].set_ylabel('Gaz S1')
    ax[2].plot(q['t'],q['wheel']*.33*3.6,label='obwód koła');ax[2].plot(q['t'],q['speed']*3.6,label='motocykl');ax[2].legend()
    ax[2].set_ylabel('V [km/h]')
    ax[3].plot(q['t'],q['beta'],label='β nadwozia');ax[3].plot(q['t'][1:],q['alpha'],label='αr kontaktu');ax[3].plot(q['t'],q['steering'],label='skręt');ax[3].legend(ncol=3);ax[3].set_ylabel('Kąty [°]')
    ax[4].plot(q['t'],q['yaw'],label='yaw-rate');ax[4].set_ylabel('Yaw-rate [rad/s]')
    b=ax[4].twinx();b.plot(q['t'],q['heading'],color='.5',alpha=.7,label='heading bez zawijania');b.set_ylabel('Heading [rad]')
    ax[4].set_xlabel('Czas od startu [s]')
    fig.suptitle('Archiwalne pełne S3 #172 · telemetria S1 i rozrzut prędkości 5 przejazdów')
    fig.savefig(DOC/'04-pelne-S3-telemetria.png',dpi=145);plt.close(fig)

    fig,ax=plt.subplots(4,1,figsize=(13,10),sharex=True,layout='constrained')
    ax[0].plot(q['t'][1:],q['kappa'],label='κ tyłu');ax[0].set_ylim(-.5,1.5)
    ax[0].set_ylabel('Wheelspin');ax[0].text(.02,.82,'Rozruch powyżej zakresu osi',transform=ax[0].transAxes)
    ax[1].plot(q['t'][1:],q['fx'],label='Fx tył');ax[1].plot(q['t'][1:],q['fy'],label='Fy tył')
    ax[1].plot(q['t'][1:],q['ff'],label='Fy przód');ax[1].plot(q['t'][1:],q['resistance'],label='opór');ax[1].legend(ncol=4);ax[1].set_ylabel('N')
    for k,label in [('combustion','praca silnika'),('wheel_slip','poślizg wzdłużny'),('front_lateral','poślizg boczny przód'),
                    ('rear_lateral','poślizg boczny tył'),('resistance','opór'),('clutch','sprzęgło'),('transmission','przeniesienie napędu')]:
        ax[2].plot(q['t'][1:],np.cumsum(q['energy'][k])/1000,label=label)
    ax[2].set_ylabel('Energia [kJ]');ax[2].legend(ncol=3,fontsize=8)
    ax[3].plot(q['t'],q['pose'],label='pose');ax[3].plot(q['t'],q['swept'],label='swept');ax[3].axhline(.5,color='r',ls=':')
    ax[3].set(ylabel='Prześwit [m]',xlabel='Czas od startu [s]');ax[3].legend()
    fig.suptitle('K4 S1 · koło, siły, bilans energii i prześwit')
    fig.savefig(DOC/'05-energia-sily.png',dpi=145);plt.close(fig)

    fig,ax=plt.subplots(1,2,figsize=(13,5),layout='constrained')
    variants=['M1','M2','M3','M4','F-load-55cm','C-width-only']
    ax[0].boxplot([[z['d_ax'] for z in rows if z['variant']==n] for n in variants], tick_labels=variants)
    ax[0].axhline(0,color='.5');ax[0].tick_params(axis='x',rotation=25);ax[0].set_ylabel('Δ przysp. wzdłuż toru jazdy [m/s²]')
    for g,c in zip(geoms,colors):
        r=[z for z in curves if z['geometry']==g['Name'] and z['bend']==2]
        ax[1].plot([z['fraction']*100 for z in r], [z['Fy_80_N']/MASS for z in r],label=g['Name'],color=c,
                   ls='--' if g['Name']=='progressive-c' else '-')
    ax[1].axhline(summary['analytic']['bank'][0]['max_horizontal_ay'],label='BASE płasko',color='.4')
    ax[1].axhspan(summary['analytic']['bank'][2]['max_horizontal_ay'],summary['analytic']['bank'][3]['max_horizontal_ay'],
                 color='#eac997',alpha=.7,label='Hipoteza nachylenia 6–7%')
    ax[1].set(xlabel='Łuk II [%]',ylabel='a boczne przy 80 km/h [m/s²]');ax[1].legend(fontsize=8)
    fig.suptitle('Izolowane warianty · lewy panel: 56 zapisanych stanów, krok modelu')
    fig.savefig(DOC/'06-porownania.png',dpi=155);plt.close(fig)


def trajectory_csv(traces):
    fields = ['run','time_s','reference_m','physical_m','x','y','k_xy','k_reference',
              'actual_kph','actual_normal_ay','available_normal_ay']+[f'required_ay_{v}' for v in V]
    with (DOC/'linie-XY-i-budzet.csv.gz').open('wb') as raw:
        with gzip.GzipFile(filename='', mode='wb', fileobj=raw, mtime=0) as compressed:
            with io.TextIOWrapper(compressed, encoding='utf-8', newline='') as stream:
                writer = csv.writer(stream)
                writer.writerow(fields)
                for name,q in traces.items():
                    for i in range(len(q['t'])-1):
                        k = q['k_xy'][i]
                        writer.writerow([name,q['t'][i],q['s'][i],q['dist'][i],*q['xy'][i],k,
                                         q['k_ref'][i],q['speed'][i]*3.6,q['actual_normal'][i],q['cap'][i],
                                         *(abs(k)*(v/3.6)**2 for v in V)])


def prior_native_peaks(source, trials):
    peaks = []
    outcomes = {q['Id']: q for q in trials}
    with ZipFile(io.BytesIO(source.read('native.zip'))) as native:
        for member in native.namelist():
            ident = Path(member).stem
            row = outcomes[ident]
            run = json.loads(native.read(member))
            all_samples = run['Riders'][0]['Samples']
            after = [q for q in all_samples if q['Input']['Time'] >= row['ActivationSeconds']-1e-8]
            legal = row.get('Legal', row['Outcome'] in ('experiment timeout', 'end of straight'))
            peak = max(after, key=lambda q: q['Input']['Rider']['Speed'])
            peaks.append(dict(id=ident, legal=legal, variant=row.get('Variant','original'),
                              outcome=row['Outcome'], vmax_kph=peak['Input']['Rider']['Speed']*3.6,
                              peak_seconds=peak['Input']['Time']-row['ActivationSeconds'],
                              elapsed_after_activation=all_samples[-1]['Input']['Time']-row['ActivationSeconds']))
    return peaks


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--analyze', type=Path, required=True)
    args=parser.parse_args()
    start=time.process_time(); evidence=args.analyze
    DOC.mkdir(parents=True,exist_ok=True)
    geoms=read(evidence/'geometry.json.gz')
    params=read(evidence/'parameters.json.gz')
    raw=read(evidence/'contact-pairs.json.gz')
    shapes, curves=geometry(geoms)
    traces, stats, prior, peaks = {}, [], {}, {}
    for study,name,run,result,decisions in native_members():
        row,q=extract_native(run,result,geoms[0])
        row.update(study=study,id=name)
        timing = [d['DecisionMilliseconds'] for d in decisions if 'DecisionMilliseconds' in d]
        if timing:
            row['archived_decision_ms'] = dict(median=float(np.median(timing)), p95=float(np.percentile(timing,95)),
                                               p99=float(np.percentile(timing,99)), maximum=max(timing))
        stats.append(row)
        if study in ('172-K4','174'):
            traces[name]=q
    rows=paired(raw, traces)
    for study in ('168','169'):
        with ZipFile(ROOT/'docs/experiments'/ARCHIVES[study]) as z:
            prior[study]=json.loads(z.read('measurements.json'))
            peaks[study]=prior_native_peaks(z, prior[study]['trials'])
    historic=read(ROOT/'docs/experiments/throttle-decision-audit/summary173.json')
    known=read(ROOT/'docs/experiments/state-feasible-throttle/summary174.json')
    forecast={}
    for ident in ('A-recorded','B-100-fixed','B-100-left','B-100-right'):
        rec=[p for r in raw for p in r['Forecasts'] if p['Id']==ident]
        forecast[ident]=dict(n=len(rec), failures=sum(p['FirstFailure'] is not None for p in rec),
                            max_beta_deg=max(p['Beta'] for p in rec), min_sweep_m=min(p['Sweep'] for p in rec),
                            milliseconds_p95=float(np.percentile([p['WallMilliseconds'] for p in rec],95)),
                            total_contact_evaluations=sum(p['ForceEvaluations'] for p in rec))
    result=dict(base='cf585987374e0673e3f0cf89df636d20bf524af5', geometry=shapes, native=stats,
                analytic=analytic(params), forecast=forecast, observations=len(raw),
                contact_parity_max=max(q['ArchivedForceError'] for q in raw),
                old_native={'168': prior['168']['trials'], '169': prior['169']['trials'],
                            '173': historic['native'], '174': known['development']},
                old_native_peaks=peaks,
                native_worlds=0, s3_new='UNKNOWN', sport='NOT_TESTED', decision_gate='NO_200_MS_GATE')
    csv_file(DOC/'geometria-0_5m.csv',curves)
    csv_file(DOC/'porownania-stanow.csv',rows)
    trajectory_csv(traces)
    figures(geoms,curves,traces,result,rows)
    # All sampled states, local model forecasts and source native receipt references are retained.
    with ZipFile(DOC/'175-dowody.zip','w',compression=ZIP_DEFLATED) as z:
        for path in sorted(evidence.glob('*.json.gz')):
            z.write(path,path.name,compress_type=ZIP_STORED)
        z.writestr('archive-index.json',json.dumps(ARCHIVES,indent=2))
    previous = evidence/'prior-analysis-cost.json.gz'
    result['cost']={'collector_cpu_seconds':read(evidence/'measure-cost.json.gz')['CpuSeconds'],
                    'analysis_cpu_seconds':time.process_time()-start,'native_cpu_seconds':0,
                    'previous_analysis_cpu_seconds':read(previous)['CpuSeconds'] if previous.exists() else 0,
                    'forecast_count':sum(q['n'] for q in forecast.values())}
    write(DOC/'summary175.json',result)
    print(json.dumps({'native_archive_runs':len(stats),'geometry':shapes[0],
                      'forecast':forecast,'cost':result['cost']},ensure_ascii=False))


if __name__=='__main__':
    main()
