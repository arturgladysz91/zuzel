"""#114 LIGHT: read accepted evidence; no Box2D worlds or fitting."""
import bisect
import argparse
import csv
import gzip
import hashlib
import importlib.util
import json
import math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/dynamic-axle-load-light'
SOURCE=ROOT/'docs/experiments/physical-steering-control'
spec=importlib.util.spec_from_file_location('physical_evidence',Path(__file__).with_name('physical-steering-report.py'))
previous=importlib.util.module_from_spec(spec);spec.loader.exec_module(previous)
load=previous.load

def save(name,value):
    DATA.mkdir(parents=True,exist_ok=True)
    (DATA/name).write_text(json.dumps(value,indent=None if isinstance(value,list) else 2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')

def existing():
    rows=previous.old.read(SOURCE/'eight-laps.csv.gz')
    geometry=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    starts=[s['startReferenceMeters'] for s in geometry['segments']]
    phases={0:'straight',1:'entry',2:'middle',3:'exit',4:'straight',5:'entry',6:'middle',7:'exit',8:'straight'}
    points=[];front_arm=previous.old.f32(.7);wheelbase=front_arm+previous.old.f32(.6)
    for i,r in enumerate(rows):
        ax=0 if not i else ((r['vx']-rows[i-1]['vx'])*math.cos(r['heading'])+(r['vy']-rows[i-1]['vy'])*math.sin(r['heading']))/rows[i-1]['dt']
        rear=front_arm/wheelbase+ax*.55/(9.81*wheelbase)
        points.append(dict(t=r['t'],s=r['s'],dt=r['dt'],ax=ax,rear=rear,front=1-rear,phase=phases[bisect.bisect_right(starts,r['s']%geometry['lapMeters'])-1]))
    def stats(values):
        return dict(States=len(values),Minimum=min(v['rear'] for v in values),Maximum=max(v['rear'] for v in values),TimeWeightedMean=sum(v['rear']*v['dt'] for v in values)/sum(v['dt'] for v in values))
    result={'BaseHead':'fc5d184e95fa3d6d12c27abd59ab8de7e2630bee','NewBox2DWorlds':0,'HeightMeters':.55,'Gravity':9.81,'StaticRearFraction':front_arm/wheelbase,
        'Rule':'At state i use (v_i-v_previous)/previous_native_dt projected on accepted current body axis. Initial ax=0. No filter, future state, speed derivative or same-step load solve. Means weighted by duration the cached load would be used.',
        'All':stats(points),'Phases':{phase:stats([p for p in points if p['phase']==phase]) for phase in ('straight','entry','middle','exit')},
        'OutsideNonnegativeLoads':sum(not 0<=p['rear']<=1 for p in points),
        'RunFirstBend':any(abs(stats([p for p in points if p['phase']==phase])['TimeWeightedMean']-.6)>=.02 for phase in ('entry','middle','exit')),
        'GateRule':'At least 2 percentage points difference in a bend phase justifies ONE short run, not a parameter sweep.',
        'SourceHashes':{name:hashlib.sha256((SOURCE/name).read_bytes()).hexdigest().upper() for name in ('eight-laps.csv.gz','eight-laps.json','first-bend.csv.gz','first-bend.json')}}
    save('existing-states.json',result)
    # Small time trace (first lap, first state in each 0.1 s bucket), no interpolation.
    seen=set();trace=[]
    for p in points:
        if p['s']>=32+geometry['lapMeters']:break
        bucket=int(p['t']*10)
        if bucket not in seen:trace.append(p);seen.add(bucket)
    save('first-lap-load-trace.json',trace)
    print(json.dumps(result['Phases']));return result

def compare():
    def read(name):
        p=DATA/name
        return previous.old.read(p if p.exists() else p.with_suffix(p.suffix+'.gz'))
    old_rows=previous.old.read(SOURCE/'first-bend.csv.gz');old_control=previous.old.read(SOURCE/'first-bend-control.csv.gz')
    rows=read('first-bend.csv');loads=read('first-bend-load.csv');run=load(DATA/'first-bend.json');baseline=load(SOURCE/'first-bend.json')
    initial=dict(run['Initial']);initial.pop('AxleLoad');assert initial==baseline['Initial']
    native_energy=previous.old.energy_from_new(rows,run);impulse=previous.impulse_work(rows,run)
    geometry=previous.old.read(ROOT/'docs/experiments/speed-grip-line-crossover/crossover-inner-geometry.csv')
    assert abs(native_energy['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules'])<1e-6
    assert abs(native_energy['Residual']-run['Energy']['BalanceResidualJoules'])<.005
    assert all(abs(l['front_budget']+l['rear_budget']-r['budget'])<1e-9 for r,l in zip(rows,loads))
    for i,l in enumerate(loads):
        ax=0 if i==0 else ((rows[i]['vx']-rows[i-1]['vx'])*math.cos(rows[i]['heading'])+(rows[i]['vy']-rows[i-1]['vy'])*math.sin(rows[i]['heading']))/rows[i-1]['dt']
        assert abs(ax-l['ax'])<1e-9
    def metrics(rr,summary):
        duration=sum(r['dt'] for r in rr)
        mean=lambda field:sum(r[field]*r['dt'] for r in rr)/duration
        errors=[math.hypot(r['x']-previous.old.interpolate(geometry,r['s'],'x'),r['y']-previous.old.interpolate(geometry,r['s'],'y')) for r in rr]
        return {'EndTime':summary['Result']['TimeSeconds'],'EndReference':summary['Result']['Riders'][0]['TotalReferenceMeters'],'NativeSteps':len(rr),
            'MaximumXyErrorFromFrozenGeometryExport':max(errors),'RmsXyErrorFromFrozenGeometryExport':math.sqrt(sum(e*e*r['dt'] for e,r in zip(errors,rr))/duration),
            'PeakAbsBetaDegrees':max(abs(math.degrees(r['beta'])) for r in rr),'MeanAbsBetaDegrees':sum(abs(math.degrees(r['beta']))*r['dt'] for r in rr)/duration,
            'PeakWheelspin':max(r['wheelspin'] for r in rr),'MeanWheelspin':mean('wheelspin'),'MeanSpeedKph':mean('speed')*3.6,
            'FrontFyPeak':max(abs(r['front_lateral']) for r in rr),'RearFyPeak':max(abs(r['rear_lateral']) for r in rr),
            'FrontTorqueMean':mean('front_torque'),'RearTorqueMean':mean('rear_torque'),'NetTorqueMean':mean('front_torque')+mean('rear_torque'),
            'FrontTorquePeak':max(abs(r['front_torque']) for r in rr),'RearTorquePeak':max(abs(r['rear_torque']) for r in rr),
            **{k:summary[k] for k in ('MinimumInner','MinimumOuter','ConservativeInner','ConservativeOuter','Energy','StopReason')}}
    old=metrics(old_rows,baseline);new=metrics(rows,run);old_queries=int(sum(c['queries'] for c in old_control));new_queries=int(sum(l['queries'] for l in loads))
    assert (old_queries-35*len(old_rows))%14==0 and (new_queries-35*len(rows))%14==0
    safe=new['StopReason'] is None and new['MinimumInner']>=.05 and new['MinimumOuter']>=.5 and new['ConservativeInner']>0 and new['ConservativeOuter']>=.5
    query_pass=new_queries<=old_queries
    gate={'Safe':safe,'NoAdditionalQueryBudgetPassed':query_pass,'RunSecondBend':False,'EightLapsRun':False,
        'Reason':'Stop after ONE new first-bend run: actual inverse query count increases, violating LIGHT budget, and safety improvement is not unambiguous. Larger beta/wheelspin alone are not acceptance criteria.'}
    save('first-bend-gate.json',gate)
    hist=previous.historical_hashes();manifest=load(SOURCE/'manifest.json')
    for name,digest in manifest.items():assert hashlib.sha256((SOURCE/name).read_bytes()).hexdigest().upper()==digest,name
    hist['physical-steering-control']=len(manifest)
    performance=load(DATA/'performance-matched.json')
    fraction_stats={key:{'Minimum':min(v[key] for v in loads),'Maximum':max(v[key] for v in loads),'Mean':sum(v[key]*r['dt'] for v,r in zip(loads,rows))/sum(r['dt'] for r in rows)} for key in ('front_fraction','rear_fraction','front_normal','rear_normal','front_budget','rear_budget')}
    save('comparison.json',{'NewBox2DWorlds':1,'Old':old,'Dynamic':new,'LoadStats':fraction_stats,'IndependentEnergy':native_energy,'NativeImpulseErrorWork':impulse,
        'LoadUpdates':run['Result']['Work']['AxleLoadUpdates'],'ExpectedLoadUpdates':len(rows)+1,'OldQueries':old_queries,'NewQueries':new_queries,
        'OldInverseRootBrackets':(old_queries-35*len(old_rows))//14,'NewInverseRootBrackets':(new_queries-35*len(rows))//14,
        'QueryIncreasePercent':100*(new_queries/old_queries-1),'MatchedContactTimeIncreasePercent':100*(performance['MedianOnMilliseconds']/performance['MedianOffMilliseconds']-1),
        'HistoricalHashes':hist,'Gate':gate})
    assert run['Result']['Work']['AxleLoadUpdates']==len(rows)+1
    for name in ('first-bend.csv','first-bend-load.csv'):
        p=DATA/name
        if p.exists():p.with_suffix('.csv.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0));p.unlink()
    save('manifest.json',{p.name:hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'})
    print(json.dumps({'Safe':safe,'StageC':False,'QueriesOld':old_queries,'QueriesNew':new_queries,'PreservedHashes':sum(hist.values()),'LoadStats':fraction_stats}))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('stage',nargs='?',choices=('existing','compare'),default='existing');parser.add_argument('--data',type=Path)
    args=parser.parse_args()
    if args.data:DATA=args.data.resolve()
    compare() if args.stage=='compare' else existing()
