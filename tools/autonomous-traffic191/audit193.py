"""Native exit metrics. Distances are reference progress measured from the bend exit."""
from bisect import bisect_left, bisect_right
import argparse
import gzip
import json
import math
from pathlib import Path
from statistics import mean

WINDOW_METERS = 22
STEER_DEADBAND = math.radians(.5)

def wrap(value):
    return math.atan2(math.sin(value), math.cos(value))

def dot(a, b):
    return a['X']*b['X'] + a['Y']*b['Y']

def raw(path):
    with gzip.open(path, 'rt', encoding='utf8') as file:
        return json.load(file)

def samples(rider, rolls, journal):
    decisions = [d['Time'] for d in journal]
    for j, (sample, lean) in enumerate(zip(rider['Samples'], rolls, strict=True)):
        state = sample['Input']
        r = state['Rider']
        time = state['Time']
        index = bisect_right(decisions, time + 1e-8) - 1
        command = journal[index] if index >= 0 else None
        v = r['Velocity']
        course = math.atan2(v['Y'], v['X'])
        speed = r['Speed']
        beta = wrap(r['Heading'] - course) if speed > .01 else 0
        assert abs(wrap(beta - r['SlideRadians'])) < 3e-5 or speed < .01
        force = sample['Forces']
        wheel = sample['Powertrain']['Wheel'] if force else None
        ft = force['TyreLoads'] if force else None
        front = force['FrontLateral'] if force else 0
        fy = force['RearLateral'] if force else 0
        fx = force['RearLongitudinal'] if force else 0
        motion = (command or {}).get('Motion')  # present only after decision audit
        wrap_heading = wrap(course - r['Road']['Heading'])
        pending = [{'at': x['ActivationAt'], 'observed': x['ObservedAt'], **x['Action']} for x in state['Pending']]
        heading, dt = r['Heading'], wheel['Seconds'] if wheel else 0
        yield dict(t=time,s=r['TotalReferenceMeters'],x=r['Position']['X'],y=r['Position']['Y'],
            offset=r['Road']['OffsetMeters'],speed=speed,course=course,heading=heading,beta=beta,
            curvature=0,angleRoad=wrap_heading,yaw=r['AngularVelocity'],roll=lean['ActualRadians'],
            reqRoll=lean['RequestRadians'],reqSteer=sample['RequestedSteering'],steer=state['Steering'],
            activeSteer=state['Active']['SteeringRadians'],reqGas=sample['RequestedDrive'],gas=state['Active']['Drive'],
            targetOffset=(command or {}).get('Target'),localTarget=(command or {}).get('Control',{}).get('TargetOffset'),
            plannedCourse=motion.get('Course') if motion else None,
            plannedCurvature=motion.get('Curvature') if motion else None,
            lookaheadCurvature=motion.get('AheadCurvature') if motion else None,
            targetSpeed=(command or {}).get('Control',{}).get('TargetSpeed'),
            fxFront=0,fyFront=front,fxRear=fx,fyRear=fy,
            frontReserve=ft['FrontCapacityNewtons']-abs(front) if ft else None,
            rearReserve=ft['RearCapacityNewtons']-math.hypot(fy,fx) if ft else None,
            wheelspin=wheel['InstantSlipRatio'] if wheel else 0,
            netForward=dot(force['NetForce'], {'X': math.cos(course), 'Y': math.sin(course)}) if force else 0,
            slipEnergy=wheel['LongitudinalLossJoules'] +
                dt*(max(0,-front*dot(force['FrontVelocity'],force['FrontLeft']))+
                    max(0,-fy*dot(force['RearVelocity'],force['BodyLeft']))) if wheel else 0,
            inner=sample['Inner'],outer=sample['Outer'],swept=sample['Certificate'],pending=pending,
            motionChange=(command or {}).get('Reason'),intent=(command or {}).get('Intent'),decision=index,
            replanned=(command or {}).get('Replanned',False),rival=(command or {}).get('Rival',0))

def summarize(paths, geometry, directory, replay_files=None):
    lap = geometry['lapMeters']
    segments = geometry['segments']
    bends = []
    for i,s in enumerate(segments):
        if s['type']!='Straight' and (i==0 or segments[i-1]['type']=='Straight'):
            start = s['startReferenceMeters']
            stop = next(seg['startReferenceMeters'] for seg in segments[i:] if seg['type']=='Straight')
            bends.append((start,stop))
    print('Reference lap and bend boundaries:',lap,bends)
    result, selected = {}, []
    for label,path in paths.items():
        data = raw(path)
        if label in (replay_files or {}):
            with Path(replay_files[label]).open(encoding='utf8') as file:
                audit = json.load(file)
            if audit['Reproduced'] != audit['Total']:
                raise ValueError('Dane planera nie odpowiadają stanom archiwalnym.')
            data['Journal'] = audit['Journal']
        run = data['Run']
        by_rider = []
        for rider, rolls, journal in zip(run['Riders'],data['Lean'],data['Journal'], strict=True):
            ident = rider['Spec']['Profile']['Id']
            line = list(samples(rider,rolls,journal))
            positions = [x['s'] for x in line]
            times = [x['t'] for x in line]
            for i,x in enumerate(line):
                left = max(0,bisect_right(positions,x['s']-1,0,i+1)-1)
                right = min(len(line)-1,bisect_left(positions,x['s']+1,i))
                diff = math.hypot(line[right]['x']-line[left]['x'],line[right]['y']-line[left]['y'])
                if diff>.35: x['curvature']=wrap(line[right]['course']-line[left]['course'])/diff
            def at(point, key):
                i = bisect_left(positions, point)
                if not 0 < i < len(line): return None
                a,b = line[i-1:i+1]
                fraction = (point-a['s'])/(b['s']-a['s'])
                return a[key]+fraction*(b[key]-a[key])
            windows = []
            for n in range(3):
                for corner,(local_begin,local_end) in enumerate(bends,1):
                    begin,exit = n*lap+local_begin,n*lap+local_end
                    if not begin < exit < positions[-1]: continue
                    next_begin = (n*lap + bends[1][0]) if corner == 1 else ((n+1)*lap+bends[0][0])
                    straight = line[bisect_left(positions,exit):bisect_left(positions,next_begin)]
                    if not straight: continue
                    first = [x for x in straight if x['s']<=exit+WINDOW_METERS]
                    region = line[bisect_left(positions,exit-22):bisect_left(positions,exit+WINDOW_METERS)]
                    approach = line[bisect_left(positions,exit-10):bisect_left(positions,next_begin)]
                    taper_window = line[bisect_left(positions,exit-30):bisect_left(positions,exit+2)]
                    plateau = [abs(x['curvature']) for x in taper_window if x['s'] < exit-17]
                    threshold = .57*mean(plateau) if plateau else 0
                    taper_start = next((x['t'] for j,x in enumerate(taper_window[:-15])
                                        if x['s'] >= exit-18 and threshold > .004 and
                                        all(abs(y['curvature']) < threshold for y in taper_window[j:j+15])), None)
                    def flips(key):
                        sign = 0
                        count = 0
                        for x in region:
                            value = x[key]
                            if abs(value)<STEER_DEADBAND: continue
                            new = math.copysign(1,value)
                            count+= sign not in (0,new)
                            sign=new
                        return count
                    def duration(xs, pred):
                        return sum(y['t']-x['t'] for x,y in zip(xs,xs[1:]) if pred(x))
                    ref = at(exit,'t')
                    idx = bisect_left(positions,exit)
                    control = [x for j,x in enumerate(region) if j==0 or x['decision']!=region[j-1]['decision']]
                    taper = next((x for j,x in enumerate(line[idx-1:],idx-1)
                                  if x['s']<=exit+22 and all(abs(q['curvature'])<.0075 for q in line[j:j+30])),None)
                    fault = next((x for x in region if x['s']>=exit-12 and (x['curvature']<-.012 or
                                  (x['s']>=exit and abs(x['yaw'])>.26 and abs(x['angleRoad'])>.055))),None)
                    def meaningful(x):
                        return x['gas']>=.35 and x['netForward']>=150
                    full = next((x for j,x in enumerate(straight[:-1]) if (finish:=bisect_left(times,x['t']+.12))
                                 < len(line) and line[finish]['s'] < next_begin and
                                 all(meaningful(y) for y in line[bisect_left(times,x['t']):finish+1])),None)
                    contacts=[c['TimeSeconds'] for c in run['Contacts'] if c['Kind']=='begin' and ident in
                              (c['RiderA'],c['RiderB']) and approach[0]['t']<=c['TimeSeconds']<=approach[-1]['t']]
                    summary = dict(label=label,rider=ident,lap=n+1,bend=corner,exit=exit,time=ref,
                        onset=fault['t'] if fault else None,settle=taper['t'] if taper else None,taperStart=taper_start,
                        straightYawSeconds=duration(first,lambda x: abs(x['yaw'])>.25),
                        straightYawAbsMean=mean(abs(x['yaw']) for x in first),
                        straightBetaAbsMeanDeg=math.degrees(mean(abs(x['beta']) for x in first)),
                        betaPeakDeg=math.degrees(max(abs(x['beta']) for x in first)),
                        oppositeCurvatureMin=min(x['curvature'] for x in first),
                        curvatureVariation=sum(abs(b['curvature']-a['curvature']) for a,b in zip(region,region[1:]))
                            if region else 0,
                        steerFlips=flips('activeSteer'),actuatorFlips=flips('steer'),
                        slipJoules=sum(x['slipEnergy'] for x in approach),
                        demandTime=full['t']-ref if full else None,
                        offsetGain=(at(exit+20,'offset')-at(exit,'offset')) if at(exit+20,'offset') is not None else None,
                        kph={str(d):3.6*at(exit+d,'speed') if at(exit+d,'speed') is not None else None for d in [0,10,20,40]},
                        vmaxKph=3.6*max(x['speed'] for x in straight),
                        sectionSeconds=at(next_begin,'t')-at(begin,'t') if at(next_begin,'t') is not None else None,
                        minimumSwept=min(x['swept'] for x in approach if x['swept'] is not None),
                        replans=sum(bool(x['replanned']) for x in control),contacts=contacts)
                    windows.append(summary)
                    selected.append((summary,[x for x in region if x['s']>=exit-16]))
            by_rider.append(dict(id=ident,ratio=10-2*rider['Spec']['Setup']['Gearing'],finish=rider['FinishSeconds'],
                laps=rider['CompletedLaps'],outcome=rider['Outcome'],exits=windows,
                sweep=min(x['swept'] for x in line if x['swept'] is not None)))
        result[label]=dict(source=str(path),fingerprint=run['Fingerprint'],outcome=run['Outcome'],
            end=run['EndSeconds'],cpu=run['TotalCpuSeconds'],mindMs=sum(sum(d[k] for k in ('TacticMs','PlannerMs','Fast50Ms'))
            for j in data['Journal'] for d in j),contacts=sum(c['Kind']=='begin' for c in run['Contacts']),
            riders=by_rider)
    top = sorted((t for t in selected if t[0]['label'].startswith('192') and t[0]['onset'] is not None), key=lambda t:
                 t[0]['straightYawAbsMean']+20*abs(min(0,t[0]['oppositeCurvatureMin']))+
                 .25*t[0]['steerFlips'], reverse=True)
    cases = top[:3]
    directory.mkdir(parents=True,exist_ok=True)
    for name,payload in (('metrics',result),('three-exits',[dict(summary=s,telemetry=x) for s,x in cases])):
        (directory/f'{name}.json').write_text(json.dumps(payload,indent=2),encoding='utf8')
    for s, rows in cases:
        print({k:s[k] for k in ('label','rider','lap','bend','time','onset','settle','straightYawAbsMean',
                                'oppositeCurvatureMin','steerFlips','betaPeakDeg','contacts','kph','replans')})
        near = min(rows,key=lambda x: abs(x['t']-s['onset']))
        print({k:round(near[k],4) for k in ('s','x','y','heading','course','yaw','roll','reqSteer','steer','gas','fxRear','fyRear','offset','targetOffset','activeSteer','reqGas')})
        print('pending',near['pending'],'reason',near['motionChange'])
    return result

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output',type=Path)
    parser.add_argument('geometry',type=Path)
    parser.add_argument('sources',nargs='+')
    parser.add_argument('--replay',nargs=2,action='append',default=[])
    args=parser.parse_args()
    summarize(dict((x.split('=',1)[0],Path(x.split('=',1)[1])) for x in args.sources),
              raw(args.geometry)['geometry'],args.output,dict(args.replay))

