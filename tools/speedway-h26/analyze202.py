from pathlib import Path
from bisect import bisect_left
import gzip,json,math,csv,hashlib,sys
root=Path(__file__).resolve().parents[2]
repo=root if (Path(__file__).parent/'H26.csproj').exists() else root/'work/zuzel'
out=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else root/'outputs/badanie202'
def read(p):
    return json.load(gzip.open(p,'rt',encoding='utf8') if str(p).endswith('.gz') else open(p,encoding='utf8-sig'))
geo=read(repo/'tools/replay-viewer/recordings/h25/202-learned.json.gz')['geometry']
lap=geo['lapMeters'];segments=geo['segments']
bends=[(s['startReferenceMeters'],next(t['startReferenceMeters'] for t in segments[i+1:] if t['type']=='Straight'))
       for i,s in enumerate(segments) if s['type']=='TurnEntry']
def metrics(path):
    d=read(path);run=d['Run'];r=run['Riders'][0];samples=r['Samples'];rows=[]
    for i,(before,now) in enumerate(zip(samples,samples[1:]),1):
        f,p=now['Forces'],now['Powertrain'];x=now['Input'];b=x['Rider'];prev=before['Input']['Rider']
        dt=x['Time']-before['Input']['Time'];z=f['TyreLoads'];fw=f['BodyForward'];mu=z['EffectiveFrictionCoefficient']
        ax=((b['Velocity']['X']-prev['Velocity']['X'])*fw['X']+(b['Velocity']['Y']-prev['Velocity']['Y'])*fw['Y'])/dt
        expected=(f['NetForce']['X']*fw['X']+f['NetForce']['Y']*fw['Y'])/147
        loss=p.get('H26');w=p['Wheel']
        midv={k:(b['Velocity'][k]+prev['Velocity'][k])/2 for k in ['X','Y']}
        midyaw=(b['AngularVelocity']+prev['AngularVelocity'])/2
        def work(force,arm=None):
            vx=midv['X']-(midyaw*arm['Y'] if arm else 0)
            vy=midv['Y']+(midyaw*arm['X'] if arm else 0)
            return (force['X']*vx+force['Y']*vy)*dt
        frontWork=work(f['FrontForce'],f['FrontArm']);rearWork=work(f['RearForce'],f['RearArm'])
        resistanceWork=work(f['Resistance'])
        bodyDelta=.5*147*sum(b['Velocity'][k]**2-prev['Velocity'][k]**2 for k in ['X','Y'])+\
            .5*52.72097*(b['AngularVelocity']**2-prev['AngularVelocity']**2)
        bodyResidual=bodyDelta-frontWork-rearWork-resistanceWork
        rearLat={'X':f['BodyLeft']['X']*f['RearLateral'],'Y':f['BodyLeft']['Y']*f['RearLateral']}
        lateralRearWork=work(rearLat,f['RearArm'])
        shaftDelta=.005*(p['AfterOmega']**2-p['BeforeOmega']**2)
        wheelDelta=.45*(w['AfterOmega']**2-w['BeforeOmega']**2)
        fullResidual=p['CombustionWorkJoules']-p['CrankDragWorkJoules']-shaftDelta-wheelDelta-bodyDelta-\
            p['ClutchHeatJoules']-p['TransmissionLossJoules']-w['BearingLossJoules']-w['LongitudinalLossJoules']+\
            frontWork+lateralRearWork+resistanceWork
        rows.append(dict(t=x['Time'],s=b['TotalReferenceMeters'],dt=dt,v=b['Speed']*3.6,gas=x['Active']['Drive'],
            ax=ax,speedAcceleration=(b['Speed']-prev['Speed'])/dt,axResidual=ax-expected,
            front=z['FrontNormalNewtons'],rear=z['RearNormalNewtons'],mu=mu,
            fx=f['RearLongitudinal'],frontFy=f['FrontLateral'],rearFy=f['RearLateral'],
            beta=b['SlideRadians']*180/math.pi,yaw=b['AngularVelocity'],spin=w['InstantSlipRatio'],
            lean=d['Lean'][0][i]['ActualRadians']*180/math.pi,rpm=p['Engine']['EngineRpm'],
            crank=p['CrankTorque'],clutch=p['ClutchTorque'],wheel=w['DriveTorqueNewtonMeters'],
            engineResistance=loss['MechanicalTorqueNewtonMeters']+loss['PumpingTorqueNewtonMeters'] if loss else 0,
            engineResidual=p['EnergyResidualJoules'],nativeExcess=now['EnergyExcessJoules'],
            sweep=now['Certificate'],rearUse=w['RearUtilization'],frontUse=abs(f['FrontLateral'])/z['FrontCapacityNewtons'],
            bodyDelta=bodyDelta,bodyWork=frontWork+rearWork+resistanceWork,bodyEnergyResidual=bodyResidual,
            frontContactWork=frontWork,rearLateralWork=lateralRearWork,resistanceWork=resistanceWork,
            fullEnergyResidual=fullResidual,impulseResidual=(ax-expected)*147*dt,
            mechanical=loss['MechanicalWorkJoules'] if loss else 0,pumping=loss['PumpingWorkJoules'] if loss else 0,
            clutchHeat=p['ClutchHeatJoules'],gearLoss=p['TransmissionLossJoules'],wheelLoss=w['LongitudinalLossJoules']))
    def mean(key,condition=lambda x:True):
        a=[x for x in rows if condition(x)];return sum(x[key]*x['dt'] for x in a)/sum(x['dt'] for x in a) if a else None
    def at(s,key):
        j=bisect_left([x['s'] for x in rows],s)
        if j==0:return rows[0][key]
        if j>=len(rows):return None
        a,b=rows[j-1:j+1];u=(s-a['s'])/(b['s']-a['s']);return a[key]+u*(b[key]-a[key])
    straight=[];corners=[]
    for l in range(3):
        for n,(entry,exit) in enumerate(bends):
            entry+=l*lap;exit+=l*lap
            previous=(bends[-1][1]+(l-1)*lap) if n==0 else bends[n-1][1]+l*lap
            line=[x for x in rows if max(0,previous)<=x['s']<=entry]
            turn=[x for x in rows if entry<=x['s']<=exit]
            straight.append(dict(lap=l+1,bend=n+1,vmax=max((x['v'] for x in line),default=None),
                entry=at(entry,'v'),throttleDropReference=next((b['s'] for a,b in zip(line,line[1:]) if a['gas']>=.99 and b['gas']<.99),None)))
            if turn:
                corners.append(dict(lap=l+1,bend=n+1,entrySpeed=at(entry,'v'),minimumSpeed=min(x['v'] for x in turn),
                    exitSpeed=at(exit,'v'),maxBeta=max(abs(x['beta']) for x in turn),
                    meanBeta=sum(abs(x['beta'])*x['dt'] for x in turn)/sum(x['dt'] for x in turn),
                    maxYaw=max(abs(x['yaw']) for x in turn),sweep=min(x['sweep'] for x in turn if x['sweep'] is not None)))
    a0,b0=bends[0];t0=at(a0,'t');t1=at(b0+20,'t')
    acc=lambda x:x['ax']>1 and x['v']>7.2
    closed=lambda x:x['gas']<.001 and x['v']>18
    summary=dict(finish=r['FinishSeconds'],outcome=run['Outcome'],laps=r['CompletedLaps'],
        cpu=run['TotalCpuSeconds'],contacts=len(run['Contacts']),end=run['EndSeconds'],
        minSweep=min(x['sweep'] for x in rows if x['sweep'] is not None),
        meanFrontAccelerating=mean('front',acc),meanRearAccelerating=mean('rear',acc),maxAx=max(x['ax'] for x in rows),
        closedThrottleDeceleration=-mean('speedAcceleration',closed) if mean('speedAcceleration',closed) is not None else None,
        maxSpin=max(x['spin'] for x in rows),maxSpinOver20Kmh=max((x['spin'] for x in rows if x['v']>20),default=None),
        maxSpeed=max(x['v'] for x in rows),meanAbsBeta=sum(abs(x['beta'])*x['dt'] for x in rows)/sum(x['dt'] for x in rows),maxAbsBeta=max(abs(x['beta']) for x in rows),
        maxYaw=max(abs(x['yaw']) for x in rows),maxAxResidual=max(abs(x['axResidual']) for x in rows),
        maxShaftEnergyResidual=max(abs(x['engineResidual']) for x in rows),
        maxNativeEnergyExcess=max(abs(x['nativeExcess']) for x in rows if x['nativeExcess'] is not None),
        maxPositiveExcess=max(x['nativeExcess'] for x in rows if x['nativeExcess'] is not None),
        maxBodyEnergyResidual=max(abs(x['bodyEnergyResidual']) for x in rows),
        maxFullEnergyResidual=max(abs(x['fullEnergyResidual']) for x in rows),
        sumFullEnergyResidual=sum(x['fullEnergyResidual'] for x in rows),
        maxImpulseResidual=max(abs(x['impulseResidual']) for x in rows),
        normalStepAxResidual=max(abs(x['axResidual']) for x in rows if x['dt']>.0049),
        minFront=min(x['front'] for x in rows),minRear=min(x['rear'] for x in rows),
        maxRearUse=max(x['rearUse'] for x in rows),maxFrontUse=max(x['frontUse'] for x in rows),
        firstEntrySpeed=at(a0,'v'),firstExitSpeed=at(b0,'v'),firstBendPlus20Seconds=None if t0 is None or t1 is None else t1-t0,
        mechanicalJoules=sum(x['mechanical'] for x in rows),pumpingJoules=sum(x['pumping'] for x in rows),
        zeroGasShare=sum(x['dt'] for x in rows if x['gas']<.001)/sum(x['dt'] for x in rows),
        straight=straight,corners=corners)
    with gzip.open(out/(path.stem.replace('.json','')+'-telemetry.csv.gz'),'wt',encoding='utf8',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=rows[0]);writer.writeheader();writer.writerows(rows)
    return summary
summary={}
for variant in ['H25','H26-L','H26-E','H26-LE']:
    summary[variant+'-same']=metrics(out/(variant+'-same.json.gz'))
for variant in ['H26-L','H26-E','H26-LE']:
    summary[variant]=metrics(out/(variant+'-learned-C.json.gz'))
summary['H26-LE-alt']=metrics(out/'H26-LE-learned-C-alt.json.gz')
summary['nativeScenarioRecords']=8
if (out/'world-inventory.json').exists():
    summary['worldInventory']=read(out/'world-inventory.json')
summary['definitions']=dict(native='All trajectories are accepted Box2D states. Interpolation is only for section metrics.',
    accelerating='body-axis ax>1 m/s², speed>2 m/s; time weighted Fz',
    closed='active gas<0.001, speed>5 m/s; time weighted -dv/dt, different learned trajectories',
    section='first geometric corner entry to 20m beyond exit; not a matched #196 test',bends=bends,
    legacyEnergyExcess='Legacy ΔK minus combustion is a one-sided passivity envelope, not the two-sided energy balance residual.')
(out/'summary.json').write_text(json.dumps(summary,indent=2,ensure_ascii=False),encoding='utf8')
for k,v in summary.items():
    if isinstance(v,dict) and 'finish' in v:
        print(k,json.dumps({x:v[x] for x in ['finish','meanFrontAccelerating','meanRearAccelerating','maxAx',
            'closedThrottleDeceleration','maxSpin','firstEntrySpeed','firstExitSpeed','maxSpeed','minSweep','maxAbsBeta','maxYaw',
            'minFront','maxAxResidual','maxNativeEnergyExcess','firstBendPlus20Seconds']},ensure_ascii=False))
