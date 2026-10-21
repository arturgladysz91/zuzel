"""#113: independent native-state analysis; no simulation or parameter fitting."""
import argparse
import csv
import gzip
import hashlib
import importlib.util
import json
import math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('two_contact_evidence',Path(__file__).with_name('two-contact-bike-report.py'))
old=importlib.util.module_from_spec(spec)
spec.loader.exec_module(old)
DATA=ROOT/'docs/experiments/physical-steering-control'
TRIALS=('first-bend','second-bend','eight-laps')
load=old.load

def read(name):
    path=DATA/name
    return old.read(path if path.exists() else path.with_suffix(path.suffix+'.gz'))

def save(name,value):
    (DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')

def impulse_work(rows,run):
    """Exact double-precision K/work identity for the recorded native impulses.

    This measures backend rounding work, not an extra physical energy source.
    No guessed absolute/relative energy tolerance is used for the long run.
    """
    final=run['Result']['Riders'][0]
    ends=rows[1:]+[{'vx':final['Velocity']['X'],'vy':final['Velocity']['Y'],'body_omega':final['AngularVelocity']}]
    work=bound=0.
    for r,n in zip(rows,ends):
        vx=(r['vx']+n['vx'])/2;vy=(r['vy']+n['vy'])/2;w=(r['body_omega']+n['body_omega'])/2
        fx=sum(r[k] for k in ('front_fx','rear_fx','resistance_x'));fy=sum(r[k] for k in ('front_fy','rear_fy','resistance_y'))
        torque=r['front_arm_x']*r['front_fy']-r['front_arm_y']*r['front_fx']+r['rear_arm_x']*r['rear_fy']-r['rear_arm_y']*r['rear_fx']
        rx=run['Mass']*(n['vx']-r['vx'])-fx*r['dt'];ry=run['Mass']*(n['vy']-r['vy'])-fy*r['dt']
        rw=old.f32(run['Inertia'])*(n['body_omega']-r['body_omega'])-torque*r['dt']
        work+=rx*vx+ry*vy+rw*w
        bound+=math.hypot(rx,ry)*math.hypot(vx,vy)+abs(rw*w)
    return {'MeasuredNativeImpulseErrorWorkJoules':work,'AbsoluteWorkBoundJoules':bound}

def historical_hashes():
    counts=old.historical_hashes()
    manifest=ROOT/'docs/experiments/two-contact-bike-dynamics/manifest.json'
    values=load(manifest)
    for name,digest in values.items():
        assert hashlib.sha256((manifest.parent/name).read_bytes()).hexdigest().upper()==digest.upper(),name
    counts[manifest.parent.name]=len(values)
    return counts

def native_motion(rows,run):
    final=run['Result']['Riders'][0]
    ends=rows[1:]+[{'vx':old.f32(final['Velocity']['X']),'vy':old.f32(final['Velocity']['Y']),'x':old.f32(final['Position']['X']),'y':old.f32(final['Position']['Y'])}]
    for r,n in zip(rows,ends):
        angle=math.atan2(n['vy'],n['vx'])-math.atan2(r['vy'],r['vx']);angle=math.atan2(math.sin(angle),math.cos(angle))
        r['native_motion_yaw']=angle/r['dt']
        r['native_motion_curvature']=angle/max(math.hypot(n['x']-r['x'],n['y']-r['y']),1e-12)

def analyse():
    baseline=load(ROOT/'docs/experiments/two-contact-bike-dynamics/track-short.json')
    freeze=load(DATA/'freeze.json');previous=load(ROOT/'docs/experiments/two-contact-bike-dynamics/freeze.json')
    parameters=dict(freeze['Parameters']);assert parameters.pop('EnablePhysicalSteeringControl') is True
    assert parameters==previous['Parameters'] and freeze['Bike']==previous['Bike'] and freeze['Wheel']==previous['Wheel']
    diagnosis=load(DATA/'diagnosis.json');assert diagnosis['BodySteps']==diagnosis['WheelCommits']==0
    assert diagnosis['MaximumForceReplayDifferenceNewtons']==diagnosis['MaximumSteeringReplayDifferenceRadians']==0
    cache={};results={}
    for name in TRIALS:
        run=load(DATA/(name+'.json'));rows=read(name+'.csv');control=read(name+'-control.csv');cache[name]=(rows,control)
        assert run['Initial']==baseline['Initial'] and len(rows)==len(control)
        energy=old.energy_from_new(rows,run);native=impulse_work(rows,run)
        native_motion(rows,run)
        unexplained=energy['Residual']-native['MeasuredNativeImpulseErrorWorkJoules']
        assert abs(unexplained)<1e-6 and abs(energy['Residual']-run['Energy']['BalanceResidualJoules'])<.005
        assert run['Energy']['MaximumExcessBeyondEngineJoules']==0 and run['LegacyYawCalls']==0
        max_demand=max(zip(rows,control),key=lambda pair:pair[0]['speed']**2*abs(pair[1]['path_curvature'])/(pair[0]['budget']/run['Mass']))
        r,c=max_demand;ratio=r['speed']**2*abs(c['path_curvature'])/(r['budget']/run['Mass'])
        entry={k:run[k] for k in ('MaximumSlideDegrees','MaximumSpin','MaximumWheelspin','MinimumInner','MinimumOuter','ConservativeInner','ConservativeOuter','Energy')}
        entry.update({'TimeSeconds':run['Result']['TimeSeconds'],'EndReferenceMeters':run['Result']['Riders'][0]['TotalReferenceMeters'],
            'Fingerprint':run['Result']['Fingerprint'],'CompletedEightLaps':run['Result']['Completed'] if name=='eight-laps' else None,
            'MaximumSteeringUsedDegrees':max(abs(math.degrees(r['steer_used'])) for r in rows),'NativeSteps':len(rows),'ContactQueries':sum(int(c['queries']) for c in control),
            'RateLimitedSteps':sum(int(c['rate_limited']) for c in control),'OutsideSampledTorqueResponseSteps':sum(int(c['outside_response']) for c in control),
            'MaximumNormalErrorMeters':max(abs(c['normal_error']) for c in control),'RmsNormalErrorMeters':math.sqrt(sum(c['normal_error']**2 for c in control)/len(control)),
            'MinimumSpeedKph':min(r['speed'] for r in rows)*3.6,'MaximumSpeedKph':max(r['speed'] for r in rows)*3.6,
            'MaximumFrontLateralNewtons':max(abs(r['front_lateral']) for r in rows),'MaximumRearLateralNewtons':max(abs(r['rear_lateral']) for r in rows),
            'MaximumFrontTorqueNm':max(abs(r['front_torque']) for r in rows),'MaximumRearTorqueNm':max(abs(r['rear_torque']) for r in rows),
            'MaximumNetTorqueNm':max(abs(r['front_torque']+r['rear_torque']) for r in rows),
            'IndependentEnergy':energy,'NativeImpulseAccounting':native,'UnexplainedResidualJoules':unexplained,
            'MaximumPathForceBudgetRatio':{'Ratio':ratio,'Time':r['t'],'Reference':r['s'],'SpeedKph':r['speed']*3.6,'PathCurvature':c['path_curvature'],'BudgetAcceleration':r['budget']/run['Mass']}})
        # Derived gates can be regenerated. Raw CSV/JSON exporters refuse overwrite.
        gate=load(DATA/(name+'-gate.json'))
        gate['RequiredSafetyMargins']=run['MinimumInner']>=.05 and run['MinimumOuter']>=.5
        gate['CertifiedBetweenSamples']=run['ConservativeInner']>0 and run['ConservativeOuter']>=.5
        gate['EnergyAndContactYaw']=run['StopReason'] is None and run['LegacyYawCalls']==0 and run['Energy']['MaximumExcessBeyondEngineJoules']==0 and run['Energy']['MaximumLinearImpulseResidualNs']<.001 and run['Energy']['MaximumAngularImpulseResidualNms']<.00003 and abs(unexplained)<1e-6
        gate['Passed']=all(gate[k] for k in ('Reached','NoObservedCrossing','RequiredSafetyMargins','CertifiedBetweenSamples','EnergyAndContactYaw'))
        gate['Rule']='Safety/unchanged #112 impulse bounds, no per-step energy creation, independent cumulative energy residual accounted by measured native impulse-error work (<1e-6 J unexplained). No absolute 1 J long-run cutoff.'
        gate['UnexplainedResidualJoules']=unexplained;save(name+'-gate.json',gate);entry['Gate']=gate;results[name]=entry
    a,b,c=(cache[name][0] for name in TRIALS)
    assert a==b[:len(a)]==c[:len(a)] and b==c[:len(b)]
    old_rows=old.read(ROOT/'docs/experiments/two-contact-bike-dynamics/track-short.csv')
    native_motion(old_rows,baseline)
    old_control=read('previous-path.csv');cache['previous']=(old_rows,old_control)
    first=cache['first-bend'];common=[c for c in first[1] if c['t']<=old_rows[-1]['t']+1e-8]
    with (DATA/'motion-comparison.csv').open('w',encoding='utf-8',newline='') as f:
        writer=csv.writer(f,lineterminator='\n');writer.writerow(('case','t','s','native_direction_rate','native_motion_curvature','force_direction_rate','body_yaw','beta'))
        for name,rows,controls in (('112',old_rows,old_control),('113',*first)):
            for r,c in zip(rows,controls):writer.writerow((name,r['t'],r['s'],r['native_motion_yaw'],r['native_motion_curvature'],c['actual_motion_yaw'],r['body_omega'],r['beta']))
    result={'BaseHead':freeze['BaseHead'],'OnlyParameterDifference':'EnablePhysicalSteeringControl=true','InitialExactlyMatched':True,'NativePrefixesExactlyMatch':True,
        'HistoricalHashes':historical_hashes(),'PreservedLegacyFingerprint':load(ROOT/'docs/experiments/two-contact-bike-dynamics/off-verification.json')['Actual'],
        'Trials':results,'CommonWindowMaximumError':{'Previous':max(abs(c['normal_error']) for c in old_control),'PhysicalSteering':max(abs(c['normal_error']) for c in common)},
        'SourceHashes':{str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in (ROOT/'docs/experiments/two-contact-bike-dynamics/track-short.csv',ROOT/'docs/experiments/two-contact-bike-dynamics/track-short.json',ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')}}
    save('analysis.json',result)
    return result,cache

def plots(cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'svg.hashsalt':'physical-steering-113','font.size':10})
    def finish(fig,name):
        fig.savefig(DATA/(name+'.png'),dpi=140);plt.close(fig)
    fig,axes=plt.subplots(3,2,figsize=(13,11),layout='constrained')
    previous,pc=cache['previous'];current,cc=cache['first-bend'];geometry=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    sections=[s for segment in geometry['segments'] for s in segment['sections'] if 25<=s['referenceMeters']<=145]
    for key,label in [('inner','kreda'),('outer','banda')]:axes[0,0].plot([s[key]['x'] for s in sections],[s[key]['y'] for s in sections],color='grey',lw=1,label=label)
    axes[0,0].plot([c['path_x'] for c in cc],[c['path_y'] for c in cc],'k--',label='C3 INNER')
    for rows,label,color in [(previous,'#112','#be472b'),(current,'#113','#147c5e')]:
        t=[r['t'] for r in rows];control=pc if rows is previous else cc
        axes[0,0].plot([r['x'] for r in rows],[r['y'] for r in rows],label=label,color=color)
        axes[0,1].plot(t,[r['native_motion_curvature'] for r in rows],label=label,color=color)
        axes[1,0].plot(t,[r['body_omega'] for r in rows],label=label+' yaw body',color=color)
        axes[1,1].plot(t,[math.degrees(r['beta']) for r in rows],label=label+' beta',color=color)
        axes[2,1].plot(t,[c['normal_error'] for c in control],label=label+' e boczny',color=color)
    axes[0,0].set(xlabel='X [m]',ylabel='Y [m]',title='Pierwszy łuk: prawdziwe XY');axes[0,0].set_aspect('equal')
    axes[0,1].plot([r['t'] for r in current],[c['path_curvature'] for c in cc],'k--',label='C3 przy aktualnym s');axes[0,1].set(xlabel='t [s]',ylabel='Krzywizna [1/m]')
    axes[1,0].plot([r['t'] for r in current],[r['native_motion_yaw'] for r in current],ls=':',label='#113 kierunek v (native)')
    axes[1,0].plot([r['t'] for r in current],[c['desired_yaw'] for c in cc],'k--',label='#113 żądany kierunek');axes[1,0].set(xlabel='t [s]',ylabel='Rate [rad/s]')
    axes[1,1].plot([r['t'] for r in current],[math.degrees(r['steer_used']) for r in current],label='#113 kierownica');axes[1,1].set(xlabel='t [s]',ylabel='Kąt [°]')
    for key,label in [('front_torque','przód'),('rear_torque','tył')]:axes[2,0].plot([r['t'] for r in current],[r[key] for r in current],label=label)
    axes[2,0].plot([r['t'] for r in current],[r['front_torque']+r['rear_torque'] for r in current],'k',lw=1,label='suma');axes[2,0].set(xlabel='t [s]',ylabel='Moment [Nm]')
    axes[2,1].set(xlabel='t [s]',ylabel='Błąd [m]');speed=axes[2,1].twinx();speed.plot([r['t'] for r in current],[r['speed']*3.6 for r in current],color='#5966bb',alpha=.5);speed.set_ylabel('V #113 [km/h]')
    for ax in axes.flat:ax.grid(alpha=.25);ax.legend(fontsize=8)
    finish(fig,'first-bend-comparison')
    fig,axes=plt.subplots(2,2,figsize=(13,8),layout='constrained');rows,control=cache['eight-laps'];stride=10
    rr=rows[::stride];cc=control[::stride];t=[r['t'] for r in rr]
    axes[0,0].plot(t,[c['normal_error'] for c in cc],label='błąd względem INNER');axes[0,0].set(ylabel='Błąd [m]',xlabel='t [s]',title='Bezpieczna orbita nie jest dokładnym trackingiem INNER')
    axes[0,1].plot(t,[r['speed']**2*abs(c['path_curvature'])/(r['budget']/147) for r,c in zip(rr,cc)],label='v² |κ C3| / (B/m)');axes[0,1].axhline(1,color='k',ls='--',label='górna granica siły');axes[0,1].set(ylabel='Iloraz',xlabel='t [s]',title='Wymaganie geometrii przy aktualnej prędkości')
    axes[1,0].plot(t,[r['inner'] for r in rr],label='kreda');axes[1,0].plot(t,[r['outer'] for r in rr],label='banda');axes[1,0].set(ylabel='Pełny footprint: prześwit [m]',xlabel='t [s]')
    for state in load(DATA/'diagnosis.json')['States']:
        if state['Time'] not in (0,.2) and abs(state['Time']-.94)>1e-7:continue
        axes[1,1].plot([v['Degrees'] for v in state['Envelope']],[v['Response']['NetTorque'] for v in state['Envelope']],label=f"stan #112 t={state['Time']:.2f} s")
    axes[1,1].set(xlabel='Statyczny kąt kierownicy [°]',ylabel='Moment netto [Nm]',title='Lokalne zapytania, bez BodyStep')
    for ax in axes.flat:ax.grid(alpha=.25);ax.legend(fontsize=8)
    finish(fig,'limits-and-response')

def compact():
    for name in (name+suffix for name in TRIALS for suffix in ('.csv','-control.csv')):
        path=DATA/name
        if path.exists():
            assert DATA.resolve() in path.resolve().parents
            path.with_suffix('.csv.gz').write_bytes(gzip.compress(path.read_bytes(),mtime=0));path.unlink()

def manifest():
    save('manifest.json',{p.name:hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'})

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--data',type=Path);parser.add_argument('--plots',action='store_true');parser.add_argument('--compact',action='store_true');args=parser.parse_args()
    if args.data:DATA=args.data.resolve()
    analysis,cache=analyse()
    if args.plots:plots(cache)
    if args.compact:compact()
    manifest()
    print(json.dumps({'Gates':{k:v['Gate']['Passed'] for k,v in analysis['Trials'].items()},'HistoricalHashes':sum(analysis['HistoricalHashes'].values()),'ForceBudgetPeaks':{k:v['MaximumPathForceBudgetRatio'] for k,v in analysis['Trials'].items()}}))
