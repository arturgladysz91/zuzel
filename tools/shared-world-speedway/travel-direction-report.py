"""Read accepted #116/#117 states. No simulation, search or rewritten historical evidence."""
import argparse
import gzip
import hashlib
import importlib.util
import json
import math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/controlled-slip-travel-direction'
spec=importlib.util.spec_from_file_location('performance',Path(__file__).with_name('motorcycle-performance-report.py'))
previous=importlib.util.module_from_spec(spec);spec.loader.exec_module(previous)
load=previous.load
TRIALS=('arc','egg','rider','rider-wide','rider-coast')

def save(name,value):
    (DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')

def read(name):
    p=DATA/(name+'.csv')
    return previous.old.old.read(p if p.exists() else p.with_suffix('.csv.gz'))

def analyse():
    hist=previous.old.historical_hashes()
    for folder in ('physical-steering-control','dynamic-axle-load-light','adaptive-rider-decisions','motorcycle-performance-500cc'):
        values=load(ROOT/'docs/experiments'/folder/'manifest.json')
        for name,digest in values.items():assert hashlib.sha256((ROOT/'docs/experiments'/folder/name).read_bytes()).hexdigest().upper()==digest.upper(),name
        hist[folder]=len(values)
    protocol=load(DATA/'protocol.json');old=load(ROOT/'docs/experiments/motorcycle-performance-500cc/protocol.json')
    p=dict(protocol['Parameters']);assert p.pop('EnableTravelDirectionSteering') is True
    assert p==old['Variants'][-1]['Parameters'] and not p.get('EnableDynamicAxleLoad',False)
    saved=load(DATA/'saved-states.json');assert saved['Worlds']==saved['Commits']==saved['MaximumForceReplayDifference']==0
    points=saved['Samples']
    first=lambda pred:next((dict(Time=r['Time'],BetaDegrees=r['BetaDegrees'],Speed=r['Speed']) for r in points if pred(r)),None)
    cost=load(DATA/'matched-cost.json');assert cost['Worlds']==cost['Commits']==0
    cache={};trials={}
    for name in TRIALS:
        run=load(DATA/(name+'.json'));rows=read(name);q=read(name+'-control');cache[name]=(run,rows,q)
        assert len(rows)==len(q)==run['Result']['Work']['SolverCalls']==run['Result']['Work']['RearWheelCommits']
        assert run['LegacyYawCalls']==0 and run['Initial'].get('AxleLoad') is None
        if name=='egg':assert run['Initial']==load(ROOT/'docs/experiments/motorcycle-performance-500cc/C-both-egg-first.json')['Initial']
        energy=previous.old.old.energy_from_new(rows,run);impulse=previous.old.impulse_work(rows,run)
        unexplained=energy['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules'];assert abs(unexplained)<1e-6
        assert run['Energy']['MaximumExcessBeyondEngineJoules']==0
        assert run['Energy']['MaximumLinearImpulseResidualNs']<.001 and run['Energy']['MaximumAngularImpulseResidualNms']<.00003
        assert previous.minimum_total_tyre_loss(rows,run)>=-1e-6
        crank=sum(r['crank_work'] for r in q);transmission=sum(r['transmission_loss'] for r in q)
        assert abs(crank-transmission-energy['Engine'])<1e-5
        for r,c in zip(rows,q):
            assert abs((c['psi_dot']-c['chi_dot'])-c['beta_dot'])<1e-12
            assert abs(math.atan2(math.sin(c['psi']-c['chi']),math.cos(c['psi']-c['chi']))-c['beta'])<1e-12
            assert abs(r['front_lateral'])<=c['front_capacity']*(1+4*2**-23)
            assert math.hypot(r['rear_longitudinal'],r['rear_lateral'])<=c['rear_capacity']*(1+4*2**-23)
            expected=.35 if name=='arc' else 1 if name=='egg' or r['t']<.2-1e-9 else 0 if name=='rider-coast' else .35
            assert abs(r['drive']-expected)<1e-7,'Uncommanded throttle change'
            assert abs(r['steer_after']-r['steer_before'])<=math.pi/2*r['dt']+1e-6
        if name!='egg':assert run['StopReason'] is None and min(run['ConservativeInner'],run['ConservativeOuter'])>.5
        final=run['Result']['Riders'][0];ends=rows[1:]+[dict(vx=final['Velocity']['X'],vy=final['Velocity']['Y'],x=final['Position']['X'],y=final['Position']['Y'])]
        native_rates=[];radii=[]
        for r,n in zip(rows,ends):
            angle=math.atan2(n['vy'],n['vx'])-math.atan2(r['vy'],r['vx']);angle=math.atan2(math.sin(angle),math.cos(angle))
            native_rates.append(angle/r['dt'])
            if abs(angle)>1e-6:radii.append(math.hypot(n['x']-r['x'],n['y']-r['y'])/abs(angle))
        offsets=[math.hypot(r['x']-35,r['y']-41)-41 for r in rows] if name!='egg' else None
        duration=sum(r['dt'] for r in rows)
        rates=[(r['steer_after']-r['steer_before'])/r['dt'] for r in rows]
        active=[math.copysign(1,x) for x in rates if abs(x)>math.pi/180]
        trials[name]=dict(TimeSeconds=run['Result']['TimeSeconds'],StopReason=run['StopReason'],Fingerprint=run['Result']['Fingerprint'],
            PeakBetaDegrees=run['MaximumSlideDegrees'],FinalBetaDegrees=run['FinalSlideDegrees'],FinalBetaDot=q[-1]['beta_dot'],
            PeakSpeedKph=max(r['speed'] for r in rows)*3.6,FinalSpeedKph=final['Speed']*3.6,
            PeakBodyYaw=max(abs(r['body_omega']) for r in rows),PeakMotionYaw=max(abs(r['chi_dot']) for r in q),
            MaximumForceVsNativeDirectionRateDifference=max(abs(a-b['chi_dot']) for a,b in zip(native_rates,q)),
            PeakWheelspin=max(r['wheelspin'] for r in rows),FinalWheelspin=rows[-1]['wheelspin'],
            PeakFrontLateralNewtons=max(abs(r['front_lateral']) for r in rows),PeakRearLateralNewtons=max(abs(r['rear_lateral']) for r in rows),PeakRearFxNewtons=max(abs(r['rear_longitudinal']) for r in rows),
            PeakWheelPowerKW=max(r['wheel_power'] for r in q)/1000,PeakSteeringDegrees=max(abs(r['steer_after']) for r in rows)*180/math.pi,
            PeakSteeringRateDegreesPerSecond=max(abs(r) for r in rates)*180/math.pi,SteeringRateSignReversalsAboveOneDegreePerSecond=sum(a!=b for a,b in zip(active,active[1:])),
            MinimumInner=run['MinimumInner'],MinimumOuter=run['MinimumOuter'],ConservativeInner=run['ConservativeInner'],ConservativeOuter=run['ConservativeOuter'],
            OffsetSpanMeters=None if offsets is None else max(offsets)-min(offsets),OffsetMinimum=None if offsets is None else min(offsets),OffsetMaximum=None if offsets is None else max(offsets),
            UnreachableTimeFraction=sum(r['dt']*c['unreachable'] for r,c in zip(rows,q))/duration,
            NativeSteps=len(rows),ContactQueries=sum(c['queries'] for c in q),BoundaryProbes=run['Result']['Work']['RearWheelBoundaryEvaluations'],
            IndependentEnergy=energy,ImpulseAccounting=impulse,UnexplainedEnergyJoules=unexplained,CrankWorkJoules=crank,TransmissionLossJoules=transmission,
            MinimumCombinedTyreIntervalLossJoules=previous.minimum_total_tyre_loss(rows,run),NativeEnergy=run['Energy'])
    prior_sources=load(ROOT/'docs/experiments/motorcycle-performance-500cc/analysis.json')['SourceHashesCanonicalLf']
    hashes={f:hashlib.sha256((ROOT/'tools/shared-world-speedway'/f).read_bytes().replace(b'\r\n',b'\n')).hexdigest().upper()
        for f in ('Engine500.cs','LoadBasedTyreGrip.cs','RearWheelDynamics.cs','PhysicalSteeringControl.cs','TravelDirectionSteering.cs','TravelDirectionAudit.cs','TwoContactBikeDynamics.cs','WorldRunner.cs')}
    for f in ('Engine500.cs','LoadBasedTyreGrip.cs','RearWheelDynamics.cs','PhysicalSteeringControl.cs'):assert hashes[f]==prior_sources[f]
    result=dict(BaseHead=protocol['BaseHead'],HistoricalHashes=hist,NativeDiagnosticWorlds=5,FullLaps=0,
        FirstNontrivialGrowingBeta=first(lambda r:r['BetaDegrees']>1 and r['OldMotion']['BetaDot']>.1),
        FirstDesiredTurnExcludedByTotalCapacity=first(lambda r:abs(r['New']['DesiredChiDot'])>r['AbsoluteAllContactChiDotBound']),
        FirstNoSampledLocalSlideRecovery=first(lambda r:r['New']['NoSampledSlideRecovery'] and 0<r['BetaDegrees']<90),
        SavedReplayExact=True,ContactAndEnergyInvariantsPassed=True,OffPhysicsSourcesUnchanged=True,
        QueryCostRatio=cost['NewQueries']/cost['OldQueries'],MatchedCost=cost,Trials=trials,SourceHashesCanonicalLf=hashes,
        Limitations=['Fixed-state angle samples are local response diagnostics, not future recovery certificates.',
            'Egg full-gas INNER still exits the track, earlier than #116; this experiment does not certify a safe Egg lap.',
            'Partial throttle .35 and .35 with wider target7 remain turn-deficient over3s; only coast action demonstrates recovery.',
            'Operating slip envelope45deg and equal rate-error weights are provisional controller choices, not tyre calibration.',
            'Four short arc worlds (achievable plus three explicit manual actions), one Egg diagnostic. No automatic parameter search.',
            'CPU batches show tiered-JIT/startup drift; query counts are the reliable cost comparison.'])
    save('analysis.json',result)
    print(json.dumps(dict(Worlds=5,Hashes=sum(hist.values()),QueryRatio=result['QueryCostRatio'],FirstImpossible=result['FirstDesiredTurnExcludedByTotalCapacity'],
        FirstNoRecovery=result['FirstNoSampledLocalSlideRecovery'],UnexplainedEnergyMax=max(abs(r['UnexplainedEnergyJoules']) for r in trials.values()))))
    return result,cache

def plots(cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig,axes=plt.subplots(3,2,figsize=(13,12),layout='constrained')
    old_rows=previous.read('C-both-egg-first.csv');rr=cache['egg'][1];qq=cache['egg'][2]
    geom=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    pts=[q for s in geom['segments'] for q in s['sections']]
    for edge in ('inner','outer'):axes[0,0].plot([p[edge]['x'] for p in pts],[p[edge]['y'] for p in pts],color='#333333',lw=1)
    for label,rows,color in (('#116 full gas',old_rows,'#a65337'),('#117 full gas',rr,'#167da5')):
        axes[0,0].plot([r['x'] for r in rows],[r['y'] for r in rows],label=label,color=color)
        beta=[]
        for r in rows:
            value=math.degrees(r['beta'])
            while beta and value-beta[-1]>180:value-=360
            while beta and value-beta[-1]<-180:value+=360
            beta.append(value)
        axes[0,1].plot([r['t'] for r in rows],beta,label=label,color=color)
    axes[0,0].set(aspect='equal',xlabel='X [m]',ylabel='Y [m]',title='Egg Mild: actual XY, stop at first crossing')
    axes[0,1].set(xlabel='t [s]',ylabel='Continuous beta [deg]',title='Full gas: lower rotation does not make INNER feasible')
    for name,color in (('arc','#3c8d5a'),('rider','#bb8b2b'),('rider-wide','#8951a0'),('rider-coast','#167da5')):
        run,rows,q=cache[name];t=[r['t'] for r in rows]
        axes[1,0].plot(t,[math.degrees(r['beta']) for r in rows],label=name,color=color)
        axes[1,1].plot(t,[r['outer'] for r in rows],label=name,color=color)
    axes[1,0].set(xlabel='t [s]',ylabel='Beta [deg]',title='R46 short arcs: explicit rider actions, no physics changes')
    axes[1,1].set(xlabel='t [s]',ylabel='Outer footprint gap [m]',title='Partial throttle remains turn-deficient; coast recovers')
    _,rows,q=cache['rider-coast'];t=[r['t'] for r in rows]
    for key,label in (('psi_dot','body psi dot'),('chi_dot','velocity chi dot'),('beta_dot','beta dot')):axes[2,0].plot(t,[r[key] for r in q],label=label)
    axes[2,0].set(xlabel='t [s]',ylabel='Rate [rad/s]',title='Coast at0.2s: beta dot becomes negative')
    for key,label in (('rear_longitudinal','rear Fx'),('rear_lateral','rear Fy'),('front_lateral','front Fy')):axes[2,1].plot(t,[r[key] for r in rows],label=label)
    axes[2,1].set(xlabel='t [s]',ylabel='Force in local contact frame [N]',title='Coast: longitudinal load releases contact capacity')
    for ax in axes.flat:ax.grid(alpha=.2);ax.legend(fontsize=8)
    fig.savefig(DATA/'controlled-slip-comparison.png',dpi=145);plt.close(fig)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--plots',action='store_true');parser.add_argument('--compact',action='store_true');args=parser.parse_args()
    result,cache=analyse()
    if args.plots:plots(cache)
    if args.compact:
        for p in DATA.glob('*.csv'):p.with_suffix('.csv.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0));p.unlink()
    save('manifest.json',{p.name:hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'})
