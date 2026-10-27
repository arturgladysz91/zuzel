"""Read-only evidence publication. No contact queries, forecasts or Box2D worlds."""
import csv
import gzip
import hashlib
import io
import json
import math
from pathlib import Path
import shutil
import time

ROOT = Path(__file__).resolve().parents[2]
E = ROOT / 'docs/experiments/motorcycle-physics-validation'
RAW = ROOT / 'work/139-raw'
OLD = ROOT / 'docs/experiments/throttle-steering-decoupling'
started = time.process_time()


def read(p):
    return json.loads(gzip.decompress(p.read_bytes()) if p.suffix == '.gz' else p.read_text(encoding='utf-8'))


def save(name, data):
    (E / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def zipped(path, data):
    path.write_bytes(gzip.compress(json.dumps(data, separators=(',', ':'), ensure_ascii=False).encode(), mtime=0))


def csvzip(path, rows):
    if not rows:
        return
    out = io.StringIO(newline='')
    writer = csv.DictWriter(out, list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)
    path.write_bytes(gzip.compress(out.getvalue().encode('utf-8'), mtime=0))


def cross(a, b):
    return a['X'] * b['Y'] - a['Y'] * b['X']


def dot(a, b):
    return a['X'] * b['X'] + a['Y'] * b['Y']


def first_at(frames, reference):
    return next((f for f in frames if f['reference'] >= reference), None)


for folder in ['native', 'viewer', 'telemetry', 'figures']:
    (E / folder).mkdir(exist_ok=True)
for shape in ['progressive-b', 'progressive-c']:
    shutil.copyfile(OLD / (shape + '-geometry.json'), E / (shape + '-geometry.json'))

rows, dnfs, manifest = [], [], []
viewers = {}
for attempt in read(E / 'attempts.json'):
    if not attempt['NativeWorlds']:
        continue
    name = attempt['Name']
    source = RAW / (name + '.json.gz')
    assert source.stat().st_size == attempt['RawBytes']
    assert hashlib.sha256(source.read_bytes()).hexdigest().upper() == attempt['RawSha256']
    shutil.copyfile(source, E / 'native' / source.name)
    d = read(source)
    result = read(E / ('result-' + name + '.json'))
    case = result['Case']
    geometry = read(E / (d['GeometryName'] + '-geometry.json'))
    exit_reference = geometry['segments'][4 if case['Corner'] == 1 else 8]['startReferenceMeters']
    telemetry = d['Telemetry']
    frames, flat = [], []
    for j, sample in enumerate(d['Replay']['Samples']):
        i = sample['Input']
        r = i['Rider']
        t = telemetry[j-1] if j else None
        f, w = (t['Forces'], t['Wheel']) if t else ({}, {})
        speed = r['Speed']
        velocity = r['Velocity']
        force = f.get('NetForce')
        chidot = cross(velocity, force) / (147 * speed**2) if force and speed else None
        ff = f.get('FrontForce')
        fl = f.get('FrontLeft')
        forward = {'X': fl['Y'], 'Y': -fl['X']} if fl else None
        v = dict(time=i['Time'], x=r['Position']['X'], y=r['Position']['Y'], vx=velocity['X'], vy=velocity['Y'],
                 psi=r['Heading'], chi=math.atan2(velocity['Y'], velocity['X']), reference=r['TotalReferenceMeters'],
                 speed=speed*3.6, gas=i['Active']['Drive'], requestedGas=sample['RequestedDrive'],
                 steering=math.degrees(i['Steering']), requestedSteering=math.degrees(sample['RequestedSteering']),
                 activeSteering=math.degrees(i['Active']['SteeringRadians']), beta=math.degrees(r['SlideRadians']),
                 yaw=r['AngularVelocity'], omega=i['Omega'], inner=sample['Inner'], outer=sample['Outer'],
                 certificate=t['AcceptedCertificate'] if t else None, frontFx=dot(ff, forward) if ff else None,
                 frontFy=f.get('FrontLateral'), rearFx=f.get('RearLongitudinal'), rearFy=f.get('RearLateral'),
                 frontTorque=f.get('FrontTorque'), rearTorque=f.get('RearTorque'), netTorque=f.get('NetTorque'),
                 frontUse=abs(f['FrontLateral'])/f['TyreLoads']['FrontCapacityNewtons'] if f else None,
                 rearUse=w.get('RearUtilization'), slip=w.get('InstantSlipRatio'), rpm=f.get('Engine', {}).get('EngineRpm'),
                 chiRate=chidot, betaRate=r['AngularVelocity']-chidot if chidot is not None else None,
                 curvature=chidot/speed if chidot is not None and speed else None)
        frames.append(v)
        flat.append(v | dict(intervalSeconds=t['Seconds'] if t else None,
                             longitudinalLossJ=w.get('LongitudinalLossJoules'), engineWorkJ=w.get('EngineWorkJoules'),
                             frontFz=f.get('TyreLoads', {}).get('FrontNormalNewtons'), rearFz=f.get('TyreLoads', {}).get('RearNormalNewtons'),
                             energyResidualJ=t['EnergyResidual'] if t else None))
    run = dict(Name=name, Geometry=d['GeometryName'], Metrics=d['Metrics'], Frames=frames)
    viewers[name] = run
    zipped(E / 'viewer' / (name + '.json.gz'), run)
    csvzip(E / 'telemetry' / (name + '.csv.gz'), flat)
    scope = read(E / ('scope-' + name + '.json'))
    exit_frame = first_at(frames, exit_reference)
    points = {str(distance): first_at(frames, exit_reference + distance) for distance in [20, 40, 62]}
    # Historical gate and full next straight are distinct inherited native checks.
    row = dict(Name=name, Pool=case['Pool'], Arm=result['Arm'], Variant=result['Variant'], Geometry=d['GeometryName'],
               Corner=case['Corner'], PreparationTime=result['PreparationTime'], OpenRequestTime=result['OpenRequestTime'],
               ActivationTime=None if result['OpenRequestTime'] is None else result['OpenRequestTime']+.24,
               ExactInitial=result['ExactInitial'], PrefixExact=result['PrefixExact'],
               HistoricalTwoBendsCompleted=scope['HistoricalTwoBendsCompleted'], HistoricalGateSeconds=scope['HistoricalGateSeconds'],
               FullNextStraightCertified=scope['Extended'] and scope['WholeCaptureCompleted'],
               FullNextStraightReference=scope['FullSecondStraightReference'],
               Exit=exit_frame, StraightPoints=points, Final=frames[-1], Metrics=d['Metrics'], Scope=scope,
               Raw='native/'+source.name, Viewer='viewer/'+source.name, Telemetry='telemetry/'+name+'.csv.gz')
    rows.append(row)
    if not d['Metrics']['Native']['Completed']:
        dnfs.append(dict(Name=name, Outcome=d['Metrics']['Native']['Outcome'], LegalSeconds=d['Metrics']['Native']['LegalSeconds'],
                         LastTwoFrames=frames[-2:], LastTwoIntervals=telemetry[-2:],
                         Meaning='control envelope is beta45; uncertified interval is no proof of crossing. Failed endpoint is not legal.'))
    manifest.append(dict(Name=name, Sha256=attempt['RawSha256'], Bytes=attempt['RawBytes'],
                         Samples=len(frames), TelemetryIntervals=len(telemetry), Fingerprint=d['Replay']['Metrics']['Fingerprint']))
save('native-summaries.json', rows)
save('native-manifest.json', manifest)
save('dnf.json', dnfs)
save('viewer/index.json', [dict(Name=x['Name']) for x in rows])

maps = read(E / 'contact-maps.json.gz')
csvzip(E / 'contact-maps.csv.gz', maps)
authority = []
for speed in [10,15,20,25,30]:
    for beta in [0,10,20,30,40]:
        for yaw in [0,.5,1,2.875]:
            group = [m for m in maps if m['Kind']=='speed-authority' and abs(m['SpeedMps']-speed)<.0001 and abs(m['BetaDegrees']-beta)<.0001 and abs(m['YawRate']-yaw)<.0001]
            lo, hi = min(m['NetTorque'] for m in group), max(m['NetTorque'] for m in group)
            authority.append(dict(SpeedMps=speed, BetaDegrees=beta, YawRate=yaw, MinimumTorque=lo, MaximumTorque=hi,
                                  BracketsZero=lo<=0<=hi, Meaning='15 hypothetical steering queries; not reachability or dynamic stability'))
save('steering-authority.json', authority)

sens = []
for name in [x['Name'] for x in rows if x['Arm']=='sensitivity']:
    run = viewers[name]
    baseline = viewers[name.split('-')[1]+'-B']
    end = min(run['Frames'][-1]['time'], baseline['Frames'][-1]['time'])
    # Common absolute native time, no alignment to differently timed exits.
    t = max(0, end-.02)
    at = lambda fs: next(x for x in reversed(fs) if x['time']<=t+1e-8)
    a,b = at(run['Frames']),at(baseline['Frames'])
    sens.append(dict(Name=name, CommonTime=t, OriginalB=b, Variant=a,
                     DeltaBetaDegrees=a['beta']-b['beta'], DeltaYaw=a['yaw']-b['yaw'], DeltaSpeedKph=a['speed']-b['speed'],
                     Note='Same initial state, prescribed identical request tape. Trajectories differ; compare common absolute time. Not an adaptive driver test.'))
save('sensitivity-comparison.json', sens)

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
plt.rcParams.update({'font.size':10,'axes.grid':True,'grid.alpha':.22,'figure.dpi':130})
colors={'A':'#be5b17','B':'#1387b1','C':'#84429f'}


def finish(fig, name):
    fig.tight_layout()
    fig.savefig(E/'figures'/name, dpi=160, bbox_inches='tight')
    plt.close(fig)


fig, axes = plt.subplots(2,3,figsize=(15,8))
for state in ['D0-before','D0-developing','D0-failed','D1-failed']:
    group=[m for m in maps if m['Kind']=='steering' and m['State']==state]
    axes[0,0].plot([m['SteerDegrees'] for m in group],[m['NetTorque'] for m in group],label=state)
axes[0,0].set(xlabel='Steering [deg]',ylabel='Net yaw torque [Nm]',title='Instantaneous steering authority');axes[0,0].legend(fontsize=8)
group=[m for m in maps if m['Kind']=='beta-steering' and m['State']=='D0-failed']
image=np.array([[next(m['NetTorque'] for m in group if abs(m['BetaDegrees']-b)<.0001 and abs(m['SteerDegrees']-s)<.0001) for s in range(-35,36,5)] for b in range(-45,46,5)])
im=axes[0,1].imshow(image,origin='lower',extent=[-35,35,-45,45],aspect='auto',cmap='coolwarm');fig.colorbar(im,ax=axes[0,1],label='Nm')
axes[0,1].set(xlabel='Steering [deg]',ylabel='Beta [deg]',title='D0-failed hypothetical state cuts')
for beta in [5,20,45]:
    group=[m for m in maps if m['Kind']=='rear-slip' and abs(m['SpeedMps']-20)<.0001 and abs(m['BetaDegrees']-beta)<.0001 and m['Gas']==1]
    axes[0,2].plot([m['Kappa'] for m in group],[m['RearFy'] for m in group],'-o',label=f'beta {beta} deg')
axes[0,2].set(xlabel='Rear longitudinal slip ratio',ylabel='Rear Fy [N]',title='Combined-slip lateral loss');axes[0,2].legend(fontsize=8)
group=[m for m in maps if m['Kind']=='rear-slip' and abs(m['SpeedMps']-20)<.0001 and abs(m['BetaDegrees']-20)<.0001 and abs(m['Kappa']-.5)<.0001]
for key in ['RearFx','RearFy']:
    axes[1,0].plot([m['Gas'] for m in group],[m[key] for m in group],'-o',label=key)
axes[1,0].set(xlabel='Gas [0..1]',ylabel='Force [N]',title='Fixed current omega: force cannot jump');axes[1,0].legend()
for beta in [0,20,40]:
    group=[m for m in authority if m['BetaDegrees']==beta and m['YawRate']==1]
    axes[1,1].plot([m['SpeedMps']*3.6 for m in group],[m['MinimumTorque'] for m in group],label=f'beta {beta} deg')
axes[1,1].set(xlabel='Speed [km/h]',ylabel='Minimum grid yaw torque [Nm]',title='Available opposite torque, yaw=1 rad/s');axes[1,1].legend(fontsize=8)
group=[m for m in maps if m['Kind']=='load-split' and m['State']=='D0-failed' and m['SteerDegrees']==0]
axes[1,2].plot([m['FrontNormal']/(m['FrontNormal']+m['RearNormal']) for m in group],[m['NetTorque'] for m in group],'-o')
axes[1,2].set(xlabel='Front normal-load fraction',ylabel='Net yaw torque [Nm]',title='Fixed arms; uncalibrated static split')
fig.suptitle('Contact maps: algebraic queries, zero new worlds; not reachable trajectories',y=1.02)
finish(fig,'contact-maps.png')

fig,axes=plt.subplots(4,3,figsize=(14,12))
for j,case in enumerate(['D0','D1','H0','H1']):
    for arm in 'ABC':
        fs=viewers[case+'-'+arm]['Frames']
        for col,key in enumerate(['beta','slip','gas']):
            axes[j,col].plot([f['time'] for f in fs],[f[key] for f in fs],color=colors[arm],label=arm)
    axes[j,0].axhline(45,color='red',ls='--',lw=.8);axes[j,0].set_ylabel(case+' beta [deg]')
    axes[j,1].set_ylabel('Rear slip ratio');axes[j,2].set_ylabel('Active gas');axes[j,2].legend()
    for ax in axes[j]:ax.set_xlabel('Native time [s]')
fig.suptitle('Full native histories: D0/D1 diagnostic; H0/H1 held physical cases',y=1.01)
finish(fig,'native-beta-wheelspin.png')

fig,axes=plt.subplots(2,3,figsize=(14,8))
for j,case in enumerate(['D0','D1']):
    names=[case+'-B']+[n for n in viewers if n.startswith('sensitivity-'+case)]
    for n in names:
        fs=viewers[n]['Frames']
        for col,key in enumerate(['beta','yaw','speed']):
            axes[j,col].plot([f['time'] for f in fs],[f[key] for f in fs],label=n.replace('sensitivity-'+case+'-',''))
    for col,label in enumerate(['Beta [deg]','Yaw [rad/s]','Speed [km/h]']):
        axes[j,col].set(xlabel='Native time [s]',ylabel=label,title=case+' same original B tape')
axes[0,0].legend(fontsize=7)
finish(fig,'native-sensitivity.png')

fig,axes=plt.subplots(1,3,figsize=(14,4))
group=[m for m in maps if m['Kind']=='wheel-inertia-constitutive' and m['State']=='D0-failed']
for key in ['RearFx','RearFy']:
    axes[0].plot([m['WheelInertia'] for m in group],[m[key] for m in group],'-o',label=key)
axes[0].set(xlabel='Wheel inertia [kg m2]',ylabel='Force [N]',title='Same v, omega, load, steering');axes[0].legend()
for kind in ['front-slope','rear-stiffness','numerical-contact-regularization','yaw-inertia']:
    group=[m for m in maps if m['Kind']==kind and m['State']=='D0-failed']
    axes[1].plot(range(len(group)),[m['YawAcceleration'] for m in group],'-o',label=kind)
axes[1].set(xlabel='Ordered frozen parameter cut (see JSON)',ylabel='Yaw acceleration [rad/s2]',title='Instantaneous one-factor cuts');axes[1].legend(fontsize=7)
for tau in [.01,.03,.06,.1]:
    group=[x for x in read(E/'relaxation-hypothesis.json') if x['TauSeconds']==tau]
    axes[2].plot([x['TimeSeconds'] for x in group],[x['NormalizedResponse'] for x in group],label=f'tau {tau} s')
axes[2].set(xlabel='Time after force step [s]',ylabel='Normalized force',title='Hypothetical lag only; NOT native model');axes[2].legend(fontsize=8)
finish(fig,'constitutive-sensitivity.png')

attempts=read(E/'attempts.json')
save('cost.json',dict(NativeWorlds=sum(x['NativeWorlds'] for x in attempts), CpuSeconds=sum(x['CpuSeconds'] for x in attempts),
                     ContactEvaluations=sum(x['Evaluations'] for x in attempts), Forecasts=sum(x['Forecasts'] for x in attempts),
                     IncompleteReservations=sum(not x['Complete'] for x in attempts),
                     ReportingDoesNotRunPhysics=True, Limits=dict(Worlds=24,CpuSeconds=450)))
save('claim-gates.json',dict(
    PreparationImprovementCount=sum(x['Metrics']['Native']['Completed'] for x in rows if x['Arm']=='B'),
    ShortCorrectionLongScopeCount=sum(x['FullNextStraightCertified'] for x in rows if x['Arm']=='C'),
    ShortCorrectionGas1AtExitCount=sum(x['FullNextStraightCertified'] and x['Exit'] is not None and x['Exit']['gas']>=.999 for x in rows if x['Arm']=='C'),
    PhysicallyDistinctFamilies=['D-B','D-C','V0','V1'], NoClaimOfSustainedHighSlipStability=True))
history=read(E/'report-cost-history.json') if (E/'report-cost-history.json').exists() else []
history.append(dict(Run=len(history)+1, CpuSeconds=time.process_time()-started, NativeWorlds=0, ContactQueries=0, Forecasts=0))
save('report-cost-history.json',history)
print(json.dumps(read(E/'cost.json')))
print(json.dumps(read(E/'claim-gates.json')))
for x in rows:
    if x['Arm']=='C':
        print(x['Name'],'exit',x['Exit']['time'],x['Exit']['gas'],x['Exit']['speed'],'points',[(k,None if v is None else round(v['speed'],3)) for k,v in x['StraightPoints'].items()])
