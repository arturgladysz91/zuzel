"""Causal labels from matched read-only reconstruction, never from correlation alone."""
import argparse
from bisect import bisect_left, bisect_right
from collections import Counter, defaultdict
import gzip
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
from statistics import median
import sys
import time
from zipfile import ZipFile, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT/'docs/experiments/throttle-decision-audit'
OLD = ROOT/'docs/experiments/three-lap-global-planning'
sys.path.insert(0,str(ROOT/'tools/whole-corner-planning'))
from audit171 import trace_of
sys.path.insert(0,str(ROOT/'tools/three-lap-global-planning'))
from analyze172 import cross, queue_audit


def read(path): return json.loads(path.read_bytes())
def write(path,data): path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def events(row,kind): return [e['Data'] for e in row['Events'] if e['Kind']==kind]
def root_policy(row): return next((p for p in events(row,'policy') if abs(p['Time']-row['Archived']['Time'])<1e-8),None)
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def load_archives():
    with ZipFile(OLD/'172-protocol.zip') as z:
        geometry=json.loads(z.read('geometry.json'))
    inventories={}
    for name in ('172-development.zip','172-protocol.zip','172-validation-K4.zip','172-validation-C0.zip'):
        with ZipFile(OLD/name) as z:
            assert z.testzip() is None
            inventories[name]=dict(sha256=sha(OLD/name),members=z.namelist())
    native={}
    for arm in ('K4','C0'):
        with ZipFile(OLD/f'172-validation-{arm}.zip') as outer:
            for name in outer.namelist():
                with ZipFile(BytesIO(outer.read(name))) as z:
                    n=json.loads(z.read('native.json'));d=json.loads(z.read('decisions.json'))
                    queue_audit(n,d)
                    native[name[:-4]]=dict(native=n,decisions=d,trace=trace_of(n))
    return geometry,native,inventories


def next_entry(s,g):
    length=g['LapMeters'];lap=math.floor(s/length)
    entries=[lap*length+g['Segments'][i]['StartReferenceMeters'] for i in (1,5)]
    return next((x for x in entries if x>s+1e-8),(lap+1)*length+g['Segments'][1]['StartReferenceMeters'])


def baseline_effects(profile,speed,slip,chosen):
    """Leave-one-term-out effects on the clamped baseline, not additive final causes."""
    clamp=lambda x:max(0,min(1,x))
    return dict(reference=clamp(1+speed-slip)-chosen,
        speed=clamp(profile-slip)-chosen,slip=clamp(profile+speed)-chosen)


def classify(row,g):
    old=row['Archived']; inp=row['Observation']; p=root_policy(row)
    exit=events(row,'exit-review')[0]
    labels={}; blocking=[]
    effects={}
    if p:
        effects=baseline_effects(p['ProfileDrive'],p['SpeedCorrection'],p['SlipPenalty'],p['Drive'])
        if p['ProfileDrive'] < .95 and effects['reference']>1e-7: labels['REFERENCE_LOW_DRIVE']=effects['reference']
        if p['SpeedCorrection'] < -1e-9 and effects['speed']>1e-7: labels['SPEED_TARGET_REACHED']=effects['speed']
        if p['SlipPenalty'] > 1e-9 and effects['slip']>1e-7: labels['WHEELSLIP_REDUCTION']=effects['slip']
    gate=exit['Gate']
    if 'entry-speed-budget' in gate:
        labels['ENTRY_SPEED_BUDGET']=0;blocking.append('ENTRY_SPEED_BUDGET')
    if 'continuation' in gate:
        labels['CONTINUATION_REJECT']=0;blocking.append('CONTINUATION_REJECT')
        tails=events(row,'continuation-gate')
        if all(x['legal'] and not x['prepare'] for x in tails):
            labels['CONTINUATION_ENTRY_BUDGET_ONLY']=0
        elif any(x['PrepareEvaluated'] and not x['prepare'] for x in tails):
            labels['CONTINUATION_ENTRY_AND_SETTLED']=0
    if 'short-clearance' in gate:
        labels['SHORT_PREDICTION_REJECT']=0;blocking.append('SHORT_PREDICTION_REJECT')
    if gate in ('reference','planned-release:reference'):
        labels['NO_BOOST_WINDOW']=0;blocking.append('NO_BOOST_WINDOW')
    if old['Tracking']['Gate']=='no admissible spatial line':
        labels['PLAN_FALLBACK']=0
    if old['Action']['Drive'] < old['Tracking']['Baseline']['Drive']-1e-7:
        labels['K4_DRIVE_REDUCTION']=old['Tracking']['Baseline']['Drive']-old['Action']['Drive']
    if old['Transition']!='continuous':
        labels['TRANSITION_OVERRIDE']=max(0,old['Proposal']['Drive']-old['Action']['Drive'])
    candidates=events(row,'tracker-candidate')
    higher=[q for q in candidates if q['action']['Drive']>old['Action']['Drive']+1e-7]
    admissible=[q for q in higher if q['safe'] and q['betaError']<=.26 and q['yawError']<=.73]
    if higher and admissible:
        labels['TRACKING_SCORE_RETAINED']=0;blocking.append('TRACKING_SCORE_RETAINED')
    elif higher:
        labels['TRACKING_TOLERANCE_REJECT']=0;blocking.append('TRACKING_TOLERANCE_REJECT')
    # A rejected path is evidence about that candidate, not a generic cause of the actual gas.
    root_prepares=[x for x in events(row,'can-prepare') if x['checkPulse']]
    r=inp['Rider']; sample=old['Reference']; local=sample%g['LapMeters']
    return dict(trial=row['Trial'],time=old['Time'],reference=sample,lap=old['Lap'],local=local,segment=old['Segment'],
        straight=not r['Road']['IsTurn'],distance_to_entry=next_entry(sample,g)-sample,
        speed_kph=r['Speed']*3.6,reference_kph=p['Target']['Speed']*3.6 if p else None,
        profile_drive=p['ProfileDrive'] if p else None,policy_drive=p['Drive'] if p else 1,
        speed_correction=p['SpeedCorrection'] if p else None,slip_penalty=p['SlipPenalty'] if p else None,
        exit_drive=exit['Chosen']['Drive'],exit_gate=gate,exit_mode=exit['Mode'],exit_blend=exit['Blend'],
        k4_drive=old['Proposal']['Drive'],accepted_drive=old['Action']['Drive'],active_drive=inp['Active']['Drive'],
        requested_steering=old['Proposal']['SteeringRadians'],accepted_steering=old['Action']['SteeringRadians'],
        active_steering=inp['Active']['SteeringRadians'],physical_steering=inp['Steering'],
        beta=r['SlideRadians'],yaw=r['AngularVelocity'],course=math.atan2(r['Velocity']['Y'],r['Velocity']['X']),
        crank_rpm=row['CrankOmega']*30/math.pi,wheel_omega=inp['Omega'],pending=inp['Pending'],
        activation_target=old['ActivationTarget'],tracker_target=events(row,'tracker-target'),
        can_prepare=root_prepares[0] if root_prepares else None,
        first_reducing_layer='REFERENCE' if p and p['ProfileDrive']<.95 else
            'SPEED_CORRECTION' if p and p['SpeedCorrection']<0 else 'K4' if 'K4_DRIVE_REDUCTION' in labels else 'NONE',
        later_blockers=blocking,mechanisms=labels,reconstructed=True,matched=row['Matched'],
        baseline_term_effects=effects,raw_negative_speed_term=bool(p and p['SpeedCorrection']<0),
        foregone_boost=max(0,events(row,'exit-boost')[0]['gas']-exit['Chosen']['Drive']) if events(row,'exit-boost') else None,
        plan_state=old['Tracking']['Policy'],
        unknown=['The globally optimal entry speed and continuation beyond the evaluated horizon are not established.'])


def active_intervals(n):
    commands=[dict(ActivationAt=0,Action=dict(Drive=0,SteeringRadians=0))]+n['Riders'][0]['Activations']
    return [(a['ActivationAt'],b['ActivationAt'],a['Action']) for a,b in zip(commands,commands[1:]+[dict(ActivationAt=n['EndSeconds'])])]


def straight_rows(trial,source,rows,g):
    trace=source['trace'];n=source['native'];intervals=active_intervals(n)
    regions=[('launch',0,trace[0]['s'],g['Segments'][1]['StartReferenceMeters'])]
    for lap in range(3):
        origin=lap*g['LapMeters']
        regions += [('main',lap+1,origin+g['Segments'][4]['StartReferenceMeters'],origin+g['Segments'][5]['StartReferenceMeters']),
                    ('home',lap+1,origin+g['Segments'][8]['StartReferenceMeters'],origin+g['LapMeters']+g['Segments'][1]['StartReferenceMeters'])]
    result=[]
    for name,lap,lo,hi in regions:
        a,b=cross(trace,lo),cross(trace,hi)
        if not a: continue
        start=a['t'];end=b['t'] if b else trace[-1]['t']
        active=next(action['Drive'] for x,y,action in intervals if x<=start<y)
        highs=[max(x,start) for x,y,action in intervals if action['Drive']>=.95 and min(y,end)>max(x,start)]
        first=highs[0] if highs else None
        def fall(threshold):
            return next((x for x,y,action in intervals if first is not None and x>first+1e-8 and x<end and action['Drive']<threshold),None)
        def detail(t):
            if t is None:return None
            point=next(q for q in trace if q['t']>=t-1e-8)
            decision=next((r for r in rows if abs(r['time']-(t-.24))<1e-8),None)
            return dict(time=t,reference=point['s'],speed_kph=point['kph'],distance_to_entry=hi-point['s'],decision=decision)
        result.append(dict(trial=trial,lap=lap,straight=name,start=lo,end=hi,complete=b is not None,
            start_time=start,end_time=end,start_speed_kph=a['kph'],start_active_drive=active,
            first_high=detail(first),first_below95=detail(fall(.95)),first_below70=detail(fall(.7)),
            high_seconds=sum(max(0,min(end,y)-max(start,x)) for x,y,action in intervals if action['Drive']>=.95)))
    return result


def prepare(evidence,out):
    began=time.process_time()
    g,native,inventory=load_archives()
    classified=[];raw={};straights=[];coasts=[];bins=[]
    for path in sorted(evidence.glob('r1-*S*.jsonl.gz')):
        rows=[json.loads(l) for l in gzip.open(path,'rt',encoding='utf-8')]
        assert all(r['Matched'] for r in rows)
        trial=rows[0]['Trial'];raw[trial]=rows
        c=[classify(r,g) for r in rows];classified+=c
        straights+=straight_rows(trial,native[trial],c,g)
        samples=native[trial]['native']['Riders'][0]['Samples']
        for a,b in zip(samples,samples[1:]):
            x,y=a['Input'],b['Input'];d=y['Rider']['DistanceMeters']-x['Rider']['DistanceMeters']
            if d>0 and y['Active']['Drive']==0 and not x['Rider']['Road']['IsTurn'] and not y['Rider']['Road']['IsTurn'] and y['Rider']['Speed']>60/3.6 and abs(y['Rider']['SlideRadians'])<math.radians(3) and abs(y['Rider']['AngularVelocity'])<.2:
                decel=(x['Rider']['Speed']**2-y['Rider']['Speed']**2)/(2*d)
                coasts.append(dict(trial=trial,time=y['Time'],deceleration=decel,dt=y['Time']-x['Time']))
        intervals=active_intervals(native[trial]['native'])
        for lap in range(3):
            lo=0 if lap==0 else cross(native[trial]['trace'],lap*g['LapMeters'])['t']
            hi=cross(native[trial]['trace'],(lap+1)*g['LapMeters'])['t']
            durations=[0]*5
            for a,b,action in intervals:
                index=bisect_right([.25,.5,.75,.95],action['Drive'])
                durations[index]+=max(0,min(b,hi)-max(a,lo))
            bins.append(dict(trial=trial,lap=lap+1,seconds=durations,shares=[x/(hi-lo) for x in durations]))
    assert len(classified)==1438
    mechanisms=[]
    for name in sorted({x for r in classified for x in r['mechanisms']}):
        for region in ('all','straight','bend'):
            eligible=[r for r in classified if region=='all' or r['straight']==(region=='straight')]
            selected=[r for r in eligible if name in r['mechanisms']]
            mechanisms.append(dict(mechanism=name,region=region,count=len(selected),denominator=len(eligible),
                percent=100*len(selected)/len(eligible),mean_drop=sum(r['mechanisms'][name] for r in selected)/len(selected) if selected else None,
                measured_drop=name in ('REFERENCE_LOW_DRIVE','SPEED_TARGET_REACHED','WHEELSLIP_REDUCTION','K4_DRIVE_REDUCTION','TRANSITION_OVERRIDE')))
    # Predeclared deterministic selection: first post-boost falls on each S1 straight,
    # middle/entry of its first main straight, and one outer-field counterpart.
    selected=[]
    def choose(trial,row,reason):
        original=next(x for x in raw[trial] if abs(x['Archived']['Time']-row['time'])<1e-8)
        selected.append(dict(Id=f'P{len(selected)+1}',Trial=trial,Reason=reason,Input=original['Observation'],
            CrankOmega=original['CrankOmega'],Action=original['Archived']['Action'],
            Spec=native[trial]['native']['Riders'][0]['Spec'],Classification=row))
    for straight in [q for q in straights if q['trial']=='v1-K4-S1' and q['straight']!='launch']:
        fall=straight['first_below95']
        if fall and fall['decision']:choose(straight['trial'],fall['decision'],f"first >=95% roll-off, lap {straight['lap']} {straight['straight']}")
    c=[r for r in classified if r['trial']=='v1-K4-S1' and r['segment']==4 and r['lap']==0]
    choose('v1-K4-S1',min(c,key=lambda x:abs(x['distance_to_entry']-31)),'middle of first main straight')
    choose('v1-K4-S1',min(c,key=lambda x:abs(x['distance_to_entry']-8)),'preparation immediately before second bend')
    c=[r for r in classified if r['trial']=='v1-K4-S4' and r['segment']==4 and r['lap']==0 and r['exit_gate']=='roll-off:entry-speed-budget']
    choose('v1-K4-S4',c[0],'outer field, first direct CanPrepare veto')
    assert 6<=len(selected)<=12
    out.mkdir(parents=True,exist_ok=True)
    with gzip.open(out/'decisions173.jsonl.gz','wt',encoding='utf-8') as f:
        for row in classified:f.write(json.dumps(row,ensure_ascii=False)+'\n')
    write(evidence/'selected173.json',selected)
    candidate_counts=Counter()
    for rows in raw.values():
        for row in rows:
            for e in events(row,'tracker-candidate'):
                candidate_counts['tracker_total']+=1
                candidate_counts['tracker_unsafe']+=not e['safe']
                candidate_counts['tracker_target_rejected']+=e['betaError']>.26 or e['yawError']>.73
            for e in events(row,'exit-candidate'):
                candidate_counts['exit_total']+=1
                candidate_counts['exit_illegal']+=not e['Legal']
    summary=dict(total_decisions=len(classified),native_worlds=0,inventory=inventory,mechanisms=mechanisms,candidate_counts=dict(candidate_counts),
        exit_gates=dict(Counter(events(r,'exit-review')[0]['Gate'] for rows in raw.values() for r in rows)),
        straights=straights,coast_observations=coasts,coast_median=median(x['deceleration'] for x in coasts),
        coast_weighted_mean=sum(x['deceleration']*x['dt'] for x in coasts)/sum(x['dt'] for x in coasts),
        throttle_bins=bins,selected=[dict(id=x['Id'],trial=x['Trial'],time=x['Input']['Time'],reason=x['Reason']) for x in selected])
    write(out/'summary173.json',summary)
    count=len(list(evidence.glob('analysis-*.cpu.json')))+1
    write(evidence/f'analysis-{count:02}.cpu.json',dict(CpuSeconds=time.process_time()-began,NativeWorlds=0))
    print(json.dumps(dict(decisions=len(classified),straights=len(straights),coast_median=summary['coast_median'],selected=len(selected),exit_gates=summary['exit_gates']),indent=2))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence',type=Path);parser.add_argument('output',type=Path)
    args=parser.parse_args();prepare(args.evidence,args.output)


if __name__=='__main__':main()
