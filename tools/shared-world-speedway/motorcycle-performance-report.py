"""Frozen A/B/C evidence, independent energy and finite-distance analysis. No simulation/search."""
import argparse
import bisect
import gzip
import hashlib
import importlib.util
import json
import math
import struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/motorcycle-performance-500cc'
spec=importlib.util.spec_from_file_location('physical_evidence',Path(__file__).with_name('physical-steering-report.py'))
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
load=old.load
VARIANTS=('baseline','A-engine','B-tyres','C-both')

def read(name):
    p=DATA/name
    return old.old.read(p if p.exists() else p.with_suffix(p.suffix+'.gz'))

def save(name,value):
    (DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')

def crossing(rows,axis,value,field):
    for a,b in zip(rows,rows[1:]):
        if a[axis]<=value<=b[axis] and b[axis]>a[axis]:
            return a[field]+(b[field]-a[field])*(value-a[axis])/(b[axis]-a[axis])
    return None

def native_float_impulse_bound(rows,run):
    """Four float velocity additions plus bounded force/mass arithmetic.

    The old absolute 0.001Ns track diagnostic is below 2 float velocity ULPs
    above32m/s, including in the unchanged baseline. No physics tolerance changes.
    """
    def ulp(v):
        value=old.old.f32(abs(v));bits=struct.unpack('I',struct.pack('f',value))[0]
        return struct.unpack('f',struct.pack('I',bits+1))[0]-value
    final=run['Result']['Riders'][0]
    ends=rows[1:]+[dict(vx=old.old.f32(final['Velocity']['X']),vy=old.old.f32(final['Velocity']['Y']))]
    eps=2**-23;gamma=8*eps/(1-8*eps);mass=run['Mass'];substeps=4;max_ratio=max_residual=max_bound=0
    for r,n in zip(rows,ends):
        f=[sum(r[k] for k in keys) for keys in (('front_fx','rear_fx','resistance_x'),('front_fy','rear_fy','resistance_y'))]
        residual=math.hypot(*(mass*(n[axis]-r[axis])-force*r['dt'] for axis,force in zip(('vx','vy'),f)))
        roundoff=mass*substeps*.5*math.hypot(*(ulp(max(abs(r[a]),abs(n[a]))+abs(force)*r['dt']/mass) for a,force in zip(('vx','vy'),f)))
        magnitude=math.hypot(sum(abs(r[k]) for k in ('front_fx','rear_fx','resistance_x')),sum(abs(r[k]) for k in ('front_fy','rear_fy','resistance_y')))
        bound=roundoff+gamma*r['dt']*magnitude
        max_ratio=max(max_ratio,residual/bound);max_residual=max(max_residual,residual);max_bound=max(max_bound,bound)
    return dict(MaximumResidualNs=max_residual,MaximumAnalyticalBoundNs=max_bound,MaximumFractionOfLocalBound=max_ratio)

def minimum_total_tyre_loss(rows,run):
    final=run['Result']['Riders'][0]
    ends=rows[1:]+[dict(vx=final['Velocity']['X'],vy=final['Velocity']['Y'],body_omega=final['AngularVelocity'])]
    minimum=float('inf')
    for r,n in zip(rows,ends):
        mid=[(r['vx']+n['vx'])/2,(r['vy']+n['vy'])/2];spin=(r['body_omega']+n['body_omega'])/2
        vf=old.old.point_velocity(mid,spin,[r['front_arm_x'],r['front_arm_y']]);vr=old.old.point_velocity(mid,spin,[r['rear_arm_x'],r['rear_arm_y']])
        work=old.old.dot([r['front_fx'],r['front_fy']],vf)+old.old.dot([r['rear_fx'],r['rear_fy']],vr)
        shaft_reaction=r['rear_longitudinal']*.33*(r['omega_before']+r['omega_after'])/2
        minimum=min(minimum,(shaft_reaction-work)*r['dt'])
    return minimum

def analyse():
    protocol=load(DATA/'protocol.json');results={};cache={}
    names=[v+'-straight' for v in VARIANTS]+[v+'-arc-'+str(r) for r in (36,46) for v in VARIANTS]
    names += [v+'-egg-first' for v in VARIANTS[1:]]+['C-adaptive-diagnostic']
    for stage in ('second','lap','eight'):
        if (DATA/('C-both-egg-'+stage+'.json')).exists():names.append('C-both-egg-'+stage)
    for name in names:
        run=load(DATA/(name+'.json'));rows=read(name+'.csv');extra=read(name+'-performance.csv');work=load(DATA/(name+'-work.json'));cache[name]=(run,rows,extra)
        assert len(rows)==len(extra)==run['Result']['Work']['SolverCalls']==run['Result']['Work']['RearWheelCommits']
        assert run['LegacyYawCalls']==0 and run['Initial'].get('AxleLoad') is None
        energy=old.old.energy_from_new(rows,run);impulse=old.impulse_work(rows,run)
        unexplained=energy['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules']
        # C# endpoint work uses Vector2 float dot products; the independent ledger
        # uses double. Bound that reporting arithmetic by work scale, while requiring
        # the separate native impulse-error explanation to remain <1e-6 J.
        gamma=8*2**-23/(1-8*2**-23)
        reporting_bound=gamma*sum(abs(v) for k,v in energy.items() if k!='Residual')
        reporting_difference=abs(energy['Residual']-run['Energy']['BalanceResidualJoules'])
        assert abs(unexplained)<1e-6 and reporting_difference<=reporting_bound,(name,unexplained,reporting_difference,reporting_bound)
        assert run['Energy']['MaximumExcessBeyondEngineJoules']==0
        rounding=native_float_impulse_bound(rows,run)
        if name.endswith('-straight'):assert rounding['MaximumFractionOfLocalBound']<=1,(name,rounding)
        else:assert run['Energy']['MaximumLinearImpulseResidualNs']<.001
        assert run['Energy']['MaximumAngularImpulseResidualNms']<.00003
        crank=sum(r['crank_work'] for r in extra);transmission=sum(r['transmission_loss'] for r in extra)
        assert abs(crank-transmission-energy['Engine'])<1e-5
        minimum_loss=minimum_total_tyre_loss(rows,run);assert minimum_loss>=-1e-6,(name,minimum_loss)
        for r,q in zip(rows,extra):
            assert abs(r['front_lateral'])<=q['front_capacity']*(1+4*2**-23)
            assert math.hypot(r['rear_longitudinal'],r['rear_lateral'])<=q['rear_capacity']*(1+4*2**-23)
            assert math.hypot(r['front_fx'],r['front_fy'])<=q['front_capacity']*(1+8*2**-23)
            assert math.hypot(r['rear_fx'],r['rear_fy'])<=q['rear_capacity']*(1+8*2**-23)
            if name!='C-adaptive-diagnostic':assert r['drive']==1,'Hidden throttle change'
        duration=sum(r['dt'] for r in rows);mean=lambda k:sum(r[k]*r['dt'] for r in rows)/duration
        e=dict(TimeAtHalt=run['Result']['TimeSeconds'],StopReason=run['StopReason'],CompletedWithoutCrossing=run['StopReason'] is None,
            MinimumInner=run['MinimumInner'],MinimumOuter=run['MinimumOuter'],ConservativeInner=run['ConservativeInner'],ConservativeOuter=run['ConservativeOuter'],
            PeakSpeedKph=max(r['speed'] for r in rows)*3.6,MeanSpeedKph=mean('speed')*3.6,
            PeakBetaDegrees=max(abs(math.degrees(r['beta'])) for r in rows),PeakWheelspin=max(r['wheelspin'] for r in rows),MeanWheelspin=mean('wheelspin'),
            MaximumEngineRpm=max(r['engine_rpm'] for r in extra),MaximumWheelRpm=max(r['omega_after']*30/math.pi for r in rows),
            MaximumWheelTorqueNm=max(r['wheel_torque'] for r in extra),MaximumWheelPowerKW=max(r['wheel_power'] for r in extra)/1000,
            MaximumNominalCrankPowerKW=max(r['full_crank_power'] for r in extra)/1000,
            NativeSteps=len(rows),SteeringQueries=sum(r['queries'] for r in extra),Work=work,IndependentEnergy=energy,ImpulseAccounting=impulse,
            UnexplainedResidualJoules=unexplained,CrankWorkJoules=crank,TransmissionLossJoules=transmission,NativeFloatImpulseBound=rounding,
            CSharpVsIndependentResidualDifferenceJoules=reporting_difference,FloatReportingWorkBoundJoules=reporting_bound,
            MinimumFrontIntervalLossJoules=run['Energy']['MinimumFrontIntervalLossJoules'],MinimumRearIntervalLossJoules=run['Energy']['MinimumRearIntervalLossJoules'],
            MinimumCombinedTyreIntervalLossJoules=minimum_loss,
            WheelTorqueSegmentBias=False if name.startswith(('A-engine','C-')) else None)
        if name.endswith('-straight'):
            speed62=crossing(extra,'distance',62,'t');speed= crossing([dict(r,distance=q['distance']) for r,q in zip(rows,extra)],'distance',62,'speed')
            e.update(TimeAt62Meters=speed62,SpeedAt62MetersKph=speed*3.6,
                MeanAccelerationFirstTwoSeconds=sum(q['ax']*q['dt'] for q in extra if q['t']<2-1e-8)/2,
                FinalAcceleration=extra[-1]['ax'],WheelspinAt62Meters=crossing([dict(r,distance=q['distance']) for r,q in zip(rows,extra)],'distance',62,'wheelspin'))
        if '-arc-' in name:
            radius=int(name.rsplit('-',1)[1]);reference_radius=radius-5
            middle=35+reference_radius*math.pi/2;exit_ref=35+reference_radius*math.pi
            motion=[q['motion_curvature'] for r,q in zip(rows,extra) if r['speed']>2 and abs(r['s']-middle)<3]
            e.update(ReferenceRadiusMeters=radius,MidSpeedKph=None if (v:=crossing(rows,'s',middle,'speed')) is None else v*3.6,
                ExitSpeedKph=None if run['StopReason'] is not None else run['Result']['Riders'][0]['Speed']*3.6,
                MeanMeasuredMiddleCurvature=sum(motion)/len(motion) if motion else None,
                MeanMeasuredMiddleRadius=abs(len(motion)/sum(motion)) if motion and abs(sum(motion))>1e-8 else None)
        results[name]=e
    # Data reuse, not extra track runs. Baseline #115 here means its rigid INNER
    # configuration; the former adaptive 145.837s is not mixed into A/B/C physics.
    baseline=load(ROOT/'docs/experiments/physical-steering-control/eight-laps.json')
    first=load(ROOT/'docs/experiments/physical-steering-control/first-bend.json')
    assert all(cache[v+'-egg-first'][0]['Initial']==first['Initial'] for v in VARIANTS[1:])
    hist=old.historical_hashes()
    for folder in ('physical-steering-control','dynamic-axle-load-light','adaptive-rider-decisions'):
        manifest=load(ROOT/'docs/experiments'/folder/'manifest.json')
        for filename,digest in manifest.items():assert hashlib.sha256((ROOT/'docs/experiments'/folder/filename).read_bytes()).hexdigest().upper()==digest.upper(),filename
        hist[folder]=len(manifest)
    stationary=load(DATA/'steady-straight.json');assert stationary['Worlds']==stationary['Commits']==0
    for point in stationary['Results']:
        assert abs(point['NetForceResidual'])<.1 and abs(point['TorqueResidual'])<.001
        assert abs(point['EquilibriumSpeed']*3.6-results[point['Variant']+'-straight']['PeakSpeedKph'])<1
    cost=load(DATA/'matched-force-cost.json');assert cost['Worlds']==cost['Commits']==0
    med=cost['MedianMilliseconds'];ratios={v:med[v]/med['baseline'] for v in VARIANTS}
    gates={p.stem:load(p) for p in DATA.glob('*-gate.json')}
    if not gates['C-both-egg-first-gate']['Passed']:
        assert not any((DATA/('C-both-egg-'+stage+'.json')).exists() for stage in ('second','lap','eight'))
    result=dict(BaseHead=protocol['BaseHead'],Grade='Mechanically coherent experimental implementation; high-power track control FAIL; tyre coefficient uncalibrated',
        HistoricalHashes=hist,NativeWorlds=len(names),ContactCapacityChecksPassed=True,NoUncommandedThrottleChanges=True,
        Trials=results,ExistingInnerEightLapFinishSeconds=baseline['Result']['FinishTimes']['1'],
        ExistingFirstBendFingerprint=first['Result']['Fingerprint'],Stationary=stationary,MatchedForceCost=cost,MatchedCostRatios=ratios,Gates=gates,
        SourceHashesCanonicalLf={name:hashlib.sha256((ROOT/'tools/shared-world-speedway'/name).read_bytes().replace(b'\r\n',b'\n')).hexdigest().upper()
            for name in ('Engine500.cs','LoadBasedTyreGrip.cs','TwoContactBikeDynamics.cs','RearWheelDynamics.cs','PhysicalSteeringControl.cs','WorldRunner.cs','MotorcyclePerformanceAudit.cs')},
        Rule='Raw times at halts are diagnostics, never lap/finish times after crossing. Arc radii are independent fixtures, Egg Mild geometry untouched. No fitting, no hidden throttle, no long run after failed C first bend. Engine torque depends on accepted wheel RPM; work uses actual midpoint shaft RPM.')
    save('analysis.json',result)
    print(json.dumps(dict(Worlds=len(names),Hashes=sum(hist.values()),SpeedAt62={v:results[v+'-straight']['SpeedAt62MetersKph'] for v in VARIANTS},
        CostRatios=ratios,CGate=gates['C-both-egg-first-gate']['Passed'],MaxEnergyResidual=max(abs(v['UnexplainedResidualJoules']) for v in results.values()))))
    return result,cache

def plots(cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    colors=dict(zip(VARIANTS,('#555555','#ce861d','#9b569b','#1681b1')))
    fig,axes=plt.subplots(2,2,figsize=(12,8),layout='constrained')
    for v in VARIANTS:
        run,rows,q=cache[v+'-straight'];color=colors[v];rr=rows[::5];qq=q[::5]
        axes[0,0].plot([a['distance'] for a in qq],[r['speed']*3.6 for r in rr],label=v,color=color)
        axes[0,1].plot([a['distance'] for a in qq],[r['speed']*3.6 for r in rr],label=v,color=color)
        axes[1,0].plot([r['t'] for r in rr],[a['wheel_power']/1000 for a in qq],label=v,color=color)
        axes[1,1].plot([r['t'] for r in rr],[r['wheelspin']*100 for r in rr],label=v,color=color)
    axes[0,0].set(xlim=(0,62),xlabel='Droga od startu rolling [m]',ylabel='V [km/h]',title='62 m: prędkość wynika z sił')
    axes[0,1].set(xlabel='Droga [m]',ylabel='V [km/h]',title='Długa prosta: odrębne punkty równowagi')
    axes[1,0].set(xlabel='t [s]',ylabel='Moc na kole [kW]',title='Jedna krzywa silnika, brak bonusu segmentu')
    axes[1,1].set(xlabel='t [s]',ylabel='Wheelspin [%]',title='Większa moc może zwiększać straty kontaktu')
    for ax in axes.flat:ax.grid(alpha=.2);ax.legend(fontsize=8)
    fig.savefig(DATA/'straight-comparison.png',dpi=160);plt.close(fig)
    fig,axes=plt.subplots(2,2,figsize=(12,8),layout='constrained')
    geom=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    pts=[q for s in geom['segments'] for q in s['sections']]
    for edge in ('inner','outer'):axes[0,0].plot([p[edge]['x'] for p in pts],[p[edge]['y'] for p in pts],color='#333333',lw=1)
    basepath=ROOT/'docs/experiments/physical-steering-control/first-bend.csv.gz'
    rr=old.old.read(basepath);axes[0,0].plot([r['x'] for r in rr],[r['y'] for r in rr],label='baseline #115',color=colors['baseline'])
    for v in VARIANTS[1:]:
        run,rr,q=cache[v+'-egg-first'];color=colors[v]
        axes[0,0].plot([r['x'] for r in rr],[r['y'] for r in rr],label=v+(' STOP' if run['StopReason'] else ''),color=color)
        axes[0,1].plot([r['t'] for r in rr],[r['speed']*3.6 for r in rr],label=v,color=color)
        beta=[]
        for r in rr:
            value=math.degrees(r['beta'])
            while beta and value-beta[-1]>180:value-=360
            while beta and value-beta[-1]<-180:value+=360
            beta.append(value)
        axes[1,0].plot([r['t'] for r in rr],beta,label=v,color=color)
        axes[1,1].plot([r['t']+r['dt'] for r in rr],[r['outer'] for r in rr],label=v+' outer',color=color)
    axes[0,0].set(aspect='equal',xlabel='x [m]',ylabel='y [m]',title='Egg Mild: bez zmiany geometrii, stop na przekroczeniu')
    axes[0,1].set(xlabel='t [s]',ylabel='V [km/h]',title='Prędkość do zatrzymania próby')
    axes[1,0].set(xlabel='t [s]',ylabel='β ciągłe [stopnie]',title='Utrata stabilności przy większym momencie na kole')
    axes[1,1].axhline(0,color='k',ls='--');axes[1,1].set(xlabel='t [s]',ylabel='Prześwit [m]',title='Rzeczywista mechaniczna sylwetka')
    for ax in axes.flat:ax.grid(alpha=.2);ax.legend(fontsize=8)
    fig.savefig(DATA/'egg-first-comparison.png',dpi=160);plt.close(fig)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--data',type=Path);parser.add_argument('--plots',action='store_true');parser.add_argument('--compact',action='store_true');args=parser.parse_args()
    if args.data:DATA=args.data.resolve()
    result,cache=analyse()
    if args.plots:plots(cache)
    if args.compact:
        for p in DATA.glob('*.csv'):p.with_suffix('.csv.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0));p.unlink()
    save('manifest.json',{p.name:hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'})
