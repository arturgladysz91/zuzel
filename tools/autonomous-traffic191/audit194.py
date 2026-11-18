"""Native #194 measures and matched archive comparisons. Does not create worlds."""
from bisect import bisect_left, bisect_right
from collections import Counter
from pathlib import Path
import csv
import json
import math
import statistics as stat
import sys

from audit193 import raw, samples, wrap

def calculate(source, geometry, export):
    document=raw(source)
    run=document['Run']
    lap=geometry['lapMeters']
    bends=[]
    for i,seg in enumerate(geometry['segments']):
        if seg['type']=='TurnEntry':
            start=seg['startReferenceMeters']
            end=next(g['startReferenceMeters'] for g in geometry['segments'][i+1:] if g['type']=='Straight')
            bends.append((start,end))
    riders=[]
    plots=[]
    passed=export(source,geometry)['document']['Run']['Passes'] if len(run['Riders']) in (2,4) else []
    for i,(r,roll,journal) in enumerate(zip(run['Riders'],document['Lean'],document['Journal'],strict=True)):
        identity=i+1
        native=list(samples(r,roll,journal))
        finish=r['FinishSeconds']
        line=[x for x in native if finish is None or x['t']<=finish+.012]
        position=[x['s'] for x in line]
        def xy(index):
            return line[index]['x'],line[index]['y']
        for k,x in enumerate(line):
            i0=max(0,bisect_right(position,x['s']-1,0,k+1)-1)
            i1=min(len(line)-1,bisect_left(position,x['s']+1,k))
            dx=math.dist(xy(i0),xy(i1))
            x['curvature']=wrap(line[i1]['course']-line[i0]['course'])/dx if dx>.3 else 0
        def info(s):
            k=bisect_left(position,s)
            return line[k] if 0<k<len(line) else None
        def window(a,b):
            return line[bisect_left(position,a):bisect_left(position,b)]
        def duration(rows, pred):
            return sum(y['t']-x['t'] for x,y in zip(rows,rows[1:]) if pred(x))
        def average(rows,key):
            return stat.mean(abs(x[key]) for x in rows) if rows else None
        def flips(rows,key,dead):
            previous=0
            count=0
            for p in rows:
                value=p[key]
                if abs(value)<dead: continue
                which=math.copysign(1,value)
                count+=previous not in (which,0)
                previous=which
            return count

        exits=[]
        for turn,(entry,exit) in enumerate(bends*3):
            n=turn//2
            s0=entry+n*lap
            end=exit+n*lap
            next_entry=(bends[1][0]+n*lap) if turn%2==0 else ((n+1)*lap+bends[0][0])
            if position[-1]<end: continue
            first=info(end)
            if first is None: continue
            final=info(next_entry)
            early=window(end,end+22)
            whole=window(end,next_entry)
            steady=window(end+9,next_entry-17)
            arc=window(s0,end)
            taper_range=window(end-18,end+11)
            middle=window(end-28,end-17)
            threshold=.57*average(middle,'curvature') if middle else 0
            taper=next((x for k,x in enumerate(taper_range[:-15]) if threshold>.004 and
                        all(abs(y['curvature'])<threshold for y in taper_range[k:k+15])),None)
            second=next((x for x in early if x['curvature']<-.012 and abs(x['yaw'])>.26),None)
            first_wide=next((x for x in whole if .8<=x['outer']<=1.5 and abs(x['angleRoad'])<.07 and abs(x['yaw'])<.16),None)
            commanded=[d for d in journal if (d.get('Wide') or {}).get('BendExit')==end]
            if not commanded:
                commanded=[d for d in journal if abs((d.get('Wide') or {}).get('BendExit',-10)-end)<1e-5]
            contact=[c['TimeSeconds'] for c in run['Contacts'] if c['Kind']=='begin' and identity in (c['RiderA'],c['RiderB'])
                     and (arc or whole)[0]['t']<=c['TimeSeconds']<=whole[-1]['t']]
            positive=lambda x:x['netForward']>=150
            touched=next((x for x in early if positive(x)),None)
            span=window(end-16,end+22)
            values={str(delta):round(3.6*info(end+delta)['speed'],4) if info(end+delta) else None
                    for delta in [0,10,20,40]}
            peak_utilization=max((t['Powertrain']['Wheel']['RearUtilization'] for t in r['Samples']
                if t['Powertrain'] and (arc or whole)[0]['t']<=t['Input']['Time']<=whole[-1]['t']),default=0)
            exits.append(dict(turn=turn+1,lap=n+1,bend=turn%2+1,entry=s0,exit=end,t=first['t'],
                position=[first['x'],first['y']],offset=first['offset'],outerAtExit=first['outer'],
                taperStart=taper['t'] if taper else None,taperAt=taper['s']-end if taper else None,
                secondTurn=second['t'] if second else None,straight35Seconds=duration(whole,lambda _:True),
                outsideBandSeconds=duration(whole,lambda x:not .8<=x['outer']<=1.5),
                settledBandFraction=sum(.8<=x['outer']<=1.5 for x in steady)/len(steady) if steady else None,
                steadyOuterMed=stat.median(x['outer'] for x in steady) if steady else None,
                straightOuterMed=stat.median(x['outer'] for x in whole),firstWideAt=first_wide['s']-end if first_wide else None,
                peakArcBeta=max(abs(x['beta']) for x in arc),earlyBeta=average(early,'beta'),earlyYaw=average(early,'yaw'),
                yawOverQuarter=duration(early,lambda x:abs(x['yaw'])>.25),earlyCourseToTangent=average(early,'angleRoad'),
                pathCurvatureTV=sum(abs(a['curvature']-b['curvature']) for a,b in zip(span,span[1:])),
                requestedSteerReversals=flips(span,'reqSteer',math.radians(.5)),
                activeSteerReversals=flips(span,'activeSteer',math.radians(.5)),
                actualSteerReversals=flips(span,'steer',math.radians(.5)),
                activeGas=average(early,'gas'),requestedGas=average(early,'reqGas'),
                meanFx=average(early,'fxRear'),meanFy=average(early,'fyRear'),meanLongitudinalAcceleration=stat.mean(x['netForward']/147 for x in early),
                peakRearUtilization=peak_utilization,meanSpin=average(early,'wheelspin'),
                firstAcceleratingAt=touched['s']-end if touched else None,speedsKph=values,
                maxKph=3.6*max(x['speed'] for x in whole),seconds=final['t']-(info(s0) or arc[0])['t'] if final else None,
                minSweep=min(x['swept'] for x in whole if x['swept'] is not None),
                contacts=contact,planStartsAt=commanded[0]['Control']['Reference'] if commanded else None,
                planNormalOrReserved=Counter(d['Reason'] for d in commanded)))
            for x in window(end-24,end+42)[::3]:
                plots.append(dict(rider=identity,turn=turn+1,postExit=x['s']-end,**{
                    key:x[key] for key in ['t','x','y','offset','outer','course','heading','beta','yaw','roll','reqSteer','steer',
                    'activeSteer','reqGas','gas','fxFront','fyFront','fxRear','fyRear','swept','curvature','speed','targetOffset',
                    'plannedCurvature','lookaheadCurvature','motionChange','wheelspin']}))
        controls=r['Activations']
        delay=max((abs(x['ActivationAt']-x['ObservedAt']-.05) for x in controls),default=0)
        steer_jump=max((abs(x['Action']['SteeringRadians']-y['Action']['SteeringRadians'])
                        for x,y in zip(controls,controls[1:])),default=0)
        throttle_jump=max((abs(x['Action']['Drive']-y['Action']['Drive'])
                           for x,y in zip(controls,controls[1:])),default=0)
        riders.append(dict(id=identity,ratio=10-2*r['Spec']['Setup']['Gearing'],gate=r['Gate'],finish=finish,
            laps=r['CompletedLaps'],state=r['Outcome'],sweep=min(x['swept'] for x in native if x['swept'] is not None),
            activeDelayError=delay,maxSteerStep=steer_jump,maxGasStep=throttle_jump,
            apexOffset=info(bends[0][0]+.48*(bends[0][1]-bends[0][0]))['offset'] if info(80) else None,
            tacticIntents=Counter(d['Intent'] for d in journal),candidateStates=Counter(d['Reason'] for d in journal),
            exits=exits))
    result=document['Result']
    return dict(riders=riders,outcome=run['Outcome'],fingerprint=run['Fingerprint'],
        steps=run['NativeSteps'],contactQueries=run['NativeContactQueries'],cpu=run['TotalCpuSeconds'],
        contacts=[c for c in run['Contacts'] if c['Kind']=='begin'],minimumBikeGap=result.get('MinimumBikeGap'),
        plannerMs=sum(t['PlanningWallMs'] for t in result['Stages']),fast50Ms=sum(t['Fast50WallMs'] for t in result['Stages']),
        tacticMs=sum(t['TacticalWallMs'] for t in result['Stages']),passes=passed),plots

if __name__=='__main__':
    ws=Path(sys.argv[1])
    sys.path.insert(0,str(ws/'work/zuzel/tools/replay-viewer'))
    from export_h25 import export
    out=ws/'outputs/badanie194'
    geometry=raw(ws/'work/zuzel/tools/replay-viewer/recordings/172-s3-k4.json.gz')['geometry']
    cases={
        '192-nominal':ws/'outputs/badanie192/archiwum-test/4852c1d16dcb4586a6107f63fba42e93/race.json.gz',
        '191-alternate':ws/'outputs/badanie191/pomiar/alternate.json.gz',
        '193-solo':ws/'outputs/badanie193/natywne/solo-exit.json.gz',
        '193-nominal':ws/'outputs/badanie193/natywne/four-exit.json.gz',
        **{case:out/'natywne'/f'{case}.json.gz' for case in ['solo-inside','solo-wide','four-nominal','four-alternate']}
    }
    results={}
    data=[]
    for name,path in cases.items():
        result,trace=calculate(path,geometry,export)
        results[name]=result
        data.extend(dict(case=name,**row) for row in trace)
        print(name,[x['finish'] for x in result['riders']],len(result['contacts']),len(result['passes']))
    (out/'metryki.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    with (out/'telemetria-wyjsc.csv').open('w',encoding='utf-8-sig',newline='') as file:
        writer=csv.DictWriter(file,fieldnames=data[0].keys())
        writer.writeheader()
        writer.writerows(data)

