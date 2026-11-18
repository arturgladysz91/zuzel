from bisect import bisect_left
from collections import Counter
import csv
import gzip
import json
import math
from pathlib import Path
import sys

work=Path(__file__).resolve().parent
root=work.parents[1]
out=root/'outputs/badanie201'
sys.path.insert(0,str(work.parent/'badanie199'))
from analysis199 import load,native_rows,interp,section,wrap


def stats(raw,lo,entry,exit_s,goal):
    rows=native_rows(raw)
    rows=[r for r in rows if r['s']>=lo-1]
    line=section(rows,lo,entry)
    bend=section(rows,entry,exit_s)
    all_rows=section(rows,lo,goal)
    first=interp(rows,lo)
    last=interp(rows,goal)
    peak=max(line,key=lambda q:q['v'])
    drop=next((r for a,r in zip(all_rows,all_rows[1:]) if a['drive']>=.99 and r['drive']<.99),None)
    return dict(seconds=last['t']-first['t'],vmax=peak['v'],peakFrom=peak['s']-lo,
                entry=interp(rows,entry),minimum=min(bend,key=lambda q:q['v']),exit=interp(rows,exit_s),
                plus20=last,drop=drop,minSweep=min(r['sweep'] for r in all_rows),
                rows=all_rows,path=last['d']-first['d'])


def physics(raw):
    rider=raw['Run']['Riders'][0]
    frames=rider['Samples']
    records=[]
    for before,now in zip(frames,frames[1:]):
        f=now['Forces']
        p=now['Powertrain']
        if f is None or p is None:
            continue
        b0,b1=before['Input']['Rider'],now['Input']['Rider']
        dt=now['Input']['Time']-before['Input']['Time']
        if dt<=1e-9 or b1['Speed']<4:
            continue
        def angle(b):
            return math.atan2(b['Velocity']['Y'],b['Velocity']['X'])
        course_rate=wrap(angle(b1)-angle(b0))/dt
        speed=(b1['Speed']+b0['Speed'])/2
        ay=speed*course_rate
        loads=f['TyreLoads']
        utilisation=math.hypot(f['RearLongitudinal'],f['RearLateral'])/loads['RearCapacityNewtons']
        radius=speed/abs(course_rate) if abs(course_rate)>.01 else None
        records.append(dict(t=now['Input']['Time'],s=b1['TotalReferenceMeters'],v=speed*3.6,
            ay=ay,centripetalForce=147*ay,radius=radius,mu=loads['EffectiveFrictionCoefficient'],
            front=loads['FrontCapacityNewtons'],rear=loads['RearCapacityNewtons'],
            loadFront=loads['FrontNormalNewtons'],loadRear=loads['RearNormalNewtons'],
            rearUse=utilisation,frontUse=abs(f['FrontLateral'])/loads['FrontCapacityNewtons'],
            wheelspin=p['Wheel']['InstantSlipRatio'],fx=f['RearLongitudinal'],fy=f['FrontLateral']+f['RearLateral'],
            crankWork=p['CombustionWorkJoules'],clutchHeat=p['ClutchHeatJoules'],
            shaftDrag=p['CrankDragWorkJoules'],bearing=p['Wheel']['BearingLossJoules'],
            energyResidual=p['EnergyResidualJoules'],rearSlip=p['Wheel']['LongitudinalLossJoules']))
    radius=[r for r in records if r['radius'] is not None and abs(r['ay'])>3]
    return dict(mu=sorted(set(r['mu'] for r in records)),frontLoad=records[0]['loadFront'],
                rearLoad=records[0]['loadRear'],capacity=records[0]['front']+records[0]['rear'],
                maxAy=max(abs(r['ay']) for r in records),maxRearUse=max(r['rearUse'] for r in records),
                maxFrontUse=max(r['frontUse'] for r in records),minRadius=min(r['radius'] for r in radius),
                maxEnergyResidual=max(abs(r['energyResidual']) for r in records),
                shaftDragTotal=sum(r['shaftDrag'] for r in records),records=records)


def main():
    pre=load(root/'outputs/badanie196/preflight.json')
    lo,entry,exit_s,goal=pre['Reference'],pre['Entry'],pre['Exit'],pre['Goal']
    files={'196':root/'outputs/badanie196/natywny.json.gz',
           '200':out/'matched-C200-native.json.gz','201-matched':out/'matched-native.json.gz'}
    compare={}
    for name,p in files.items():
        if p.is_file():
            raw=load(p)
            compare[name]=stats(raw,lo,entry,exit_s,goal)
            times={round(r['t'],8):r for r in native_rows(raw)}
            errors=[]
            for row in raw.get('Planner',[]):
                predicted=row['PredictedActivation']
                actual=times.get(round(predicted['Time'],8))
                if actual:
                    errors.append(math.hypot(actual['x']-predicted['X'],actual['y']-predicted['Y']))
            compare[name]['activationXYMax']=max(errors,default=0)
    model=load(out/'matched-model.json.gz')
    forecast_errors={}
    actual=load(root/'outputs/badanie196/natywny.json.gz')['Run']['Riders'][0]['Samples']
    for name,r in [('196',model['Original196']),('200',model['Transfer200'])]:
        if not r['Knots']:
            continue
        if name=='196':
            errors=[]
            for k in r['Knots']:
                state=k['Start']['State']
                at=min(actual,key=lambda s:abs(s['Input']['Time']-state['Time']))
                if abs(at['Input']['Time']-state['Time'])>.00001:
                    continue
                b=at['Input']['Rider']
                errors.append(math.hypot(b['Position']['X']-state['Rider']['Position']['X'],
                                         b['Position']['Y']-state['Rider']['Position']['Y']))
            forecast_errors[name]=dict(maxXY=max(errors),samples=len(errors))
    raw=load(root/'outputs/badanie200/C-native.json.gz')
    rows=native_rows(raw)
    finish=raw['Run']['Riders'][0]['FinishSeconds']
    finish_rows=[]
    for s,r in zip(raw['Run']['Riders'][0]['Samples'],rows):
        if r['t']<42:
            continue
        finish_rows.append(dict(**r,pending=s['Input']['Pending'],raceFinished=r['t']>=finish,
            policy='C200-certified-continuation',goalReference=956.3362498283386,
            remainingObjectiveSeconds=max(0,finish-r['t']),
            nextBendPreview=None,activeSpeedCap=None,
            throttleReason='learned joint control; checked full finite continuation',
            afterLastNativeSample=False))
    audit=physics(raw)
    with gzip.open(out/'physics.csv.gz','wt',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=list(audit['records'][0]));writer.writeheader();writer.writerows(audit['records'])
    audit.pop('records')
    with gzip.open(out/'finish.csv.gz','wt',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=list(finish_rows[0]));writer.writeheader();writer.writerows(finish_rows)
    summary=dict(matched=compare,forecastErrors=forecast_errors,physics=audit,
                 finish=dict(time=finish,end=rows[-1],secondsAfter=rows[-1]['t']-finish,
                 first100=next(r for r in rows if r['v']>=100),last=finish_rows[-1]))
    for trial in sorted(out.glob('C-native.json.gz')):
        data=load(trial)
        summary['full201']=dict(result=data['Result'],vmax=max(r['v'] for r in native_rows(data)))
    (out/'metryki.json').write_text(json.dumps(summary,indent=2),encoding='utf8')
    print('MATCHED',[(k,round(r['seconds'],5),round(r['vmax'],3),round(r['entry']['v'],2),
          round(r['minimum']['v'],2),round(r['exit']['v'],2),round(r['minSweep'],3)) for k,r in compare.items()])
    print('PHYSICS',audit)


if __name__=='__main__':
    main()
