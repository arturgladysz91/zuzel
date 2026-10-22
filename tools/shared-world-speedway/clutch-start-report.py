"""#121: saved native straight launches, original independent tyre/body work ledger, plus crank/clutch/gear."""
import csv,gzip,hashlib,importlib.util,json,math,argparse,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];DATA=ROOT/'docs/experiments/physical-500cc-tape-start'
BASE='46129d74cf725c33d67040663262062ac9067281'
def module(name,file):
    spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(file));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
old=module('contact','two-contact-bike-report.py');physical=module('physical','physical-steering-report.py')
def load(name):return json.loads((DATA/name).read_text(encoding='utf-8-sig'))
def save(name,value):(DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def read(name):
    p=DATA/(name+'.csv');f=open(p,newline='') if p.exists() else gzip.open(str(p)+'.gz','rt',newline='')
    with f:return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(f)]
def crossing(rows,axis,value):
    for a,b in zip(rows,rows[1:]):
        if a[axis]<=value<=b[axis] and b[axis]>a[axis]:
            k=(value-a[axis])/(b[axis]-a[axis]);return {key:a[key]+(b[key]-a[key])*k for key in a}
    return None
def analyze():
    hashes={}
    for manifest in sorted((ROOT/'docs/experiments').glob('*/manifest.json')):
        if manifest.parent==DATA:continue
        entries=json.loads(manifest.read_text(encoding='utf-8-sig'))
        if not isinstance(entries,dict) or not all(isinstance(x,str) and len(x)==64 for x in entries.values()):continue
        for name,digest in entries.items():
            f=manifest.parent/name;assert hashlib.sha256(f.read_bytes()).hexdigest().upper()==digest.upper(),str(f)
            hashes[str(f.relative_to(ROOT)).replace('\\','/')]=digest
    for name in ['Engine500.cs','RearWheelDynamics.cs','LoadBasedTyreGrip.cs','TravelDirectionSteering.cs','ReachableTravelDirectionSteering.cs','ExperimentalTrackShape.cs']:
        subprocess.run(['git','diff','--exit-code',BASE,'--','tools/shared-world-speedway/'+name],cwd=ROOT,check=True)
    cache={};out={};protocol=load('protocol.json')
    for case in protocol['Cases']:
        name=case['Name'];run=load(name+'.json');rows=read(name);ds=read(name+'-drivetrain');cache[name]=(run,rows,ds)
        final=run['Result']['Riders'][0];assert run['StopReason'] is None
        assert len(rows)==len(ds)==run['Result']['Work']['SolverCalls']==run['Result']['Work']['RearWheelCommits']==run['Result']['Work']['EngineClutchCommits']
        assert run['Initial']['Riders'][0]['Speed']==run['Initial']['RearWheels'][0]['RearWheelAngularVelocityRadPerSecond']==0
        assert run['Initial']['Engines'][0]['Rpm']==6000
        assert not run['Result']['FinishTimes'] and final['TotalReferenceMeters']<35-1.05
        assert run['MinimumInner']>0 and run['MinimumOuter']>0
        energy=old.energy_from_new(rows,run);rounding=physical.impulse_work(rows,run)
        crank=sum(d['engine_change'] for d in ds);combustion=sum(d['combustion_work'] for d in ds)
        clutch=sum(d['clutch_loss'] for d in ds);gear=sum(d['gear_loss'] for d in ds);wheel_input=sum(d['wheel_input_work'] for d in ds)
        drivetrain_residual=crank+wheel_input+clutch+gear-combustion
        whole_residual=energy['Residual']+drivetrain_residual
        assert abs(drivetrain_residual)<1e-6
        assert abs(whole_residual-rounding['MeasuredNativeImpulseErrorWorkJoules'])<1e-6
        assert abs(crank-(run['Initial']['Engines'][0]['RotationalEnergyJoules']*-1+.5*.02*ds[-1]['engine_after']**2))<1e-6
        reaction=protocol['TapeReactionSeconds'];distance=0;events=[]
        ends=rows[1:]+[dict(x=final['Position']['X'],y=final['Position']['Y'],vx=final['Velocity']['X'],vy=final['Velocity']['Y'],speed=final['Speed'])]
        for r,d,n in zip(rows,ds,ends):
            assert d['clutch_loss']>=-1e-5 and d['gear_loss']>=-1e-5
            assert abs(d['clutch_torque'])<=d['capacity']+1e-8
            assert math.hypot(r['rear_longitudinal'],r['rear_lateral'])<=997.4131 and abs(r['front_lateral'])<=854.927
            assert abs(r['steer_used'])<1e-12
            if r['t']<reaction-1e-8:
                assert r['speed']==r['omega_before']==d['wheel_torque']==d['engagement']==0
                assert r['x']==rows[0]['x'] and r['y']==rows[0]['y']
            events.append(dict(t=r['t'],distance=distance,speed=r['speed'],engine_rpm=d['engine_rpm'],wheel_rpm=d['wheel_rpm'],
                kappa=r['wheelspin'],fx=r['rear_longitudinal'],utilization=math.hypot(r['rear_longitudinal'],r['rear_lateral'])/997.413065,
                acceleration=(n['vx']-r['vx'])/r['dt'],engine_torque=d['engine_torque'],clutch_torque=d['clutch_torque'],wheel_torque=d['wheel_torque'],
                engagement=d['engagement'],throttle=d['throttle']))
            distance+=math.hypot(n['x']-r['x'],n['y']-r['y'])
        end=events[-1].copy();end.update(t=run['Result']['TimeSeconds'],distance=distance,speed=final['Speed'],engine_rpm=ds[-1]['engine_after']*30/math.pi,
            wheel_rpm=rows[-1]['omega_after']*30/math.pi);events.append(end)
        assert abs(distance-final['DistanceMeters'])<1e-4
        motion=crossing(events,'speed',.01)['t'] # Explicit operational threshold; also report first-positive-motion bracket.
        first=next((r,n) for r,n in zip(rows,ends) if n['speed']>0)
        times={str(k):crossing(events,'speed',k/3.6) for k in [30,50,70]}
        speeds={str(k):crossing(events,'distance',k) for k in [5,10,20,30,60]}
        min_step_residual=min(d['engine_change']+d['wheel_input_work']+d['clutch_loss']+d['gear_loss']-d['combustion_work'] for d in ds)
        out[name]=dict(ReleaseSeconds=case['ReleaseSeconds'],TapeReactionSeconds=reaction,FirstPositiveMotionBracketSeconds=[first[0]['t'],first[0]['t']+first[0]['dt']],
            MotionThresholdMetersPerSecond=.01,DetectedMotionSeconds=motion,MechanicalDetectionDelaySeconds=motion-reaction,
            TimesToSpeed={k:None if v is None else dict(FromTapeSeconds=v['t'],FromDetectedMotionSeconds=v['t']-motion,DistanceMeters=v['distance']) for k,v in times.items()},
            SpeedsAtDistance={k:None if v is None else dict(SpeedKph=v['speed']*3.6,TimeFromTapeSeconds=v['t']) for k,v in speeds.items()},
            SpeedAt2SecondsKph=crossing(events,'t',2)['speed']*3.6,EndSpeedKph=final['Speed']*3.6,EndTime=run['Result']['TimeSeconds'],DistanceMeters=distance,
            PeakEngineRpm=max(e['engine_rpm'] for e in events),EndEngineRpm=end['engine_rpm'],EndWheelRpm=end['wheel_rpm'],
            PeakEngineTorque=max(d['engine_torque'] for d in ds),PeakClutchTorque=max(d['clutch_torque'] for d in ds),PeakWheelTorque=max(d['wheel_torque'] for d in ds),
            PeakWheelspin=max(e['kappa'] for e in events),EndWheelspin=events[-2]['kappa'],PeakRearFx=max(e['fx'] for e in events),PeakRearUtilization=max(e['utilization'] for e in events),
            PeakAcceleration=max(e['acceleration'] for e in events),FirstLockedSeconds=next((d['t'] for d in ds if d['locked']),None),
            CombustionWorkJoules=combustion,ClutchLossJoules=clutch,TransmissionLossJoules=gear,CrankEnergyChangeJoules=crank,WheelInputWorkJoules=wheel_input,
            OriginalBodyWheelLedger=energy,WholeBalanceResidualJoules=whole_residual,UnexplainedEnergyJoules=whole_residual-rounding['MeasuredNativeImpulseErrorWorkJoules'],
            MinimumClutchIntervalLossJoules=min(d['clutch_loss'] for d in ds),MinimumDrivetrainIntervalResidual=min_step_residual,
            ContactQueries=sum(d['contact_queries'] for d in ds),AcceptedSteps=len(rows),Fingerprint=run['Result']['Fingerprint'],
            MinimumInner=run['MinimumInner'],MinimumOuter=run['MinimumOuter'])
        with (DATA/(name+'-telemetry.csv')).open('w',newline='') as f:
            writer=csv.DictWriter(f,fieldnames=events[0]);writer.writeheader();writer.writerows(events)
        cache[name]+=(events,)
    rear=997.413065;mass=147
    result=dict(BaseHead=BASE,NewConfigurations=3,FullLaps=0,HistoricalFiles=len(hashes),HistoricalSha256=hashes,Trials=out,
        AbsoluteRearAccelerationCeiling=rear/mass,OptimisticMinimumTimeTo70FromMotion=70/3.6/(rear/mass),
        EmpiricalCalibration=False,Rule='Native motion and endpoint work only. Reaction is rider decision, detected motion uses0.01m/s; report both clocks. Missing60m remains null. Static rear budget unchanged; no fit to real speedway acceleration or dynamic axle load.')
    save('analysis.json',result);print(json.dumps(out,indent=2));return result,cache
def plots(result,cache):
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'axes.titlesize':12,'axes.titleweight':'bold'})
    fig,axs=plt.subplots(3,2,figsize=(14,11),layout='constrained');fig.suptitle('Fizyczny start500 cm³: trzy zamrożone czasy uwalniania sprzęgła',fontsize=16,fontweight='bold')
    for name,(_,rows,d,e) in cache.items():
        label=f"{name}: {result['Trials'][name]['ReleaseSeconds']:.2f}s";t=[r['t'] for r in e];kw=dict(label=label,lw=1.6)
        axs[0,0].plot(t,[r['speed']*3.6 for r in e],**kw);axs[0,1].plot([r['distance'] for r in e],[r['speed']*3.6 for r in e],**kw)
        axs[1,0].plot(t,[r['engine_rpm'] for r in e],**kw);axs[1,1].plot(t,[r['kappa'] for r in e],**kw)
        axs[2,0].plot(t,[r['fx'] for r in e],**kw);axs[2,1].plot(t,[r['acceleration'] for r in e],**kw)
    axs[0,0].axhline(70,color='#888',ls=':');axs[2,0].axhline(997.413,color='#777',ls='--',label='stały limit tylnej opony')
    for ax,title,y in [(axs[0,0],'Prędkość od taśmy','km/h'),(axs[0,1],'Prędkość względem rzeczywistej drogi','km/h'),(axs[1,0],'Niezależne obroty wału','RPM'),(axs[1,1],'Wheelspin: regularny również przy V≈0','κ'),(axs[2,0],'Oryginalny Fx tyłu','N'),(axs[2,1],'Przyspieszenie wzdłużne z ruchu Box2D','m/s²')]:
        ax.set(title=title,ylabel=y,xlabel='Czas od podniesienia taśmy [s]');ax.grid(alpha=.2);ax.legend(fontsize=8)
    axs[0,1].set(xlabel='Rzeczywista droga [m]');fig.savefig(DATA/'launch-performance.png',dpi=145);plt.close(fig)
    fig,axs=plt.subplots(2,2,figsize=(14,8),layout='constrained');fig.suptitle('Sprzęgło i energia — napęd przez oryginalne kontakty',fontsize=16,fontweight='bold')
    for name,(_,rows,d,e) in cache.items():
        t=[r['t'] for r in d];axs[0,0].plot(t,[r['engagement'] for r in d],label=name);axs[0,1].plot(t,[r['clutch_torque'] for r in d],label=name)
        axs[1,0].plot(t,[r['end_slip'] for r in d],label=name)
    labels=['gentle','intermediate','aggressive'];bottom=[0.]*3
    for key,label in [('ClutchLossJoules','sprzęgło'),('TransmissionLossJoules','przekładnia'),('CrankEnergyChangeJoules','energia wału')]:
        values=[result['Trials'][n][key]/1000 for n in labels];axs[1,1].bar(labels,values,bottom=bottom,label=label);bottom=[a+b for a,b in zip(bottom,values)]
    for key,label in [('RearLongitudinalLoss','poślizg opony'),('Bearing','łożysko'),('BodyChange','energia motocykla'),('WheelChange','energia koła'),('ResistanceWork','opory ruchu')]:
        values=[result['Trials'][n]['OriginalBodyWheelLedger'][key]/1000*(-1 if key=='ResistanceWork' else 1) for n in labels];axs[1,1].bar(labels,values,bottom=bottom,label=label);bottom=[a+b for a,b in zip(bottom,values)]
    axs[0,0].set(title='Sprzęgło rozłączone do reakcji0,24 s',ylabel='stopień sprzęgnięcia',xlabel='Czas [s]');axs[0,1].set(title='Moment sprzęgła po stronie wału',ylabel='Nm',xlabel='Czas [s]')
    axs[1,0].set(title='Różnica obrotów ωengine−g·ωwheel',ylabel='rad/s',xlabel='Czas [s]');axs[1,1].set(title='Całkowity bilans pracy netto silnika',ylabel='kJ')
    for ax in axs.flat:ax.grid(alpha=.2);ax.legend(fontsize=8)
    fig.savefig(DATA/'clutch-energy.png',dpi=145);plt.close(fig)
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--plots',action='store_true');ap.add_argument('--compact',action='store_true');args=ap.parse_args();result,cache=analyze()
    if args.plots:plots(result,cache)
    if args.compact:
        for p in DATA.glob('*.csv'):p.with_suffix('.csv.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0));p.unlink()
    save('manifest.json',{str(p.relative_to(DATA)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.rglob('*')) if p.is_file() and p.name!='manifest.json'})
