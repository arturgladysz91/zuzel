"""Diagnose frozen #137-#140 evidence, no forecasts and zero new Box2D worlds."""
from pathlib import Path
import gzip
import hashlib
import json
import math
import time
import sys

ROOT=Path(__file__).resolve().parents[2]
DEST=ROOT/'docs/experiments/autonomous-acceleration/results.json'
OLD=ROOT/'docs/experiments/causal-yaw-throttle'
started=time.process_time()
previous=json.loads(DEST.read_bytes()) if DEST.exists() else None
if previous and '--repair-decomposition' not in sys.argv:
    raise SystemExit('Existing audit retained; do not repeat.')

sources={}
def read(path):
    blob=path.read_bytes();sources[str(path.relative_to(ROOT)).replace('\\','/')]=hashlib.sha256(blob).hexdigest().upper()
    return json.loads(gzip.decompress(blob) if path.suffix=='.gz' else blob)

policy=read(OLD/'protocol.json')['Policy']
rows=[]
for path in sorted(OLD.glob('offline-*.json.gz')):
    d=read(path);i=d['State']['Input'];leaves=d['Search']['Held'];components=[]
    for leaf in leaves:
        e=leaf['Evaluation'];v=leaf['Prediction']['Points'][-1]['Rider'];a=leaf['Actions'][0]
        desired=v['Speed']/v['Road']['RadiusMeters'] if v['Road']['IsTurn'] else 0
        capacity=1.2845000267028808*9.81
        envelope=math.sqrt(.8*capacity*v['Road']['RadiusMeters']) if desired>1e-8 else 100
        terms={
            'Progress':-policy['ProgressWeight']*e['Progress']*.6/.9,
            'Direction':policy['DirectionWeight']*e['DirectionError']**2*10,
            'YawTerminal':leaf['TerminalYawCost'],
            'ThrottlePrior':policy['ThrottleWeight']*(a['Drive']-policy['Drive'])**2,
            'SteeringChange':policy['SteeringChangeWeight']*math.degrees(a['SteeringRadians']-i['Active']['SteeringRadians'])**2,
            'GeometrySpeed':policy['GeometrySpeedWeight']*max(0,v['Speed']-envelope)**2,
            'FutureClearance':10*max(0,.2-e['FutureClearance'])**2,
            'Unsafe':10000*max(.001,-e['Clearance'])**2 if e['Safety']==2 else 0}
        # The original score uses PREVIEW geometry, not endpoint Road geometry.
        # All remaining terms are recorded or exactly reconstructed; isolate its
        # geometry term algebraically. This closure is not an independent check.
        terms['GeometrySpeed']=e['Score']-sum(value for key,value in terms.items() if key!='GeometrySpeed')
        components.append({'Action':a,'SafetyH09':e['Safety'],'Score':e['Score'],'Components':terms,
            'ScoreReconstructionError':sum(terms.values())-e['Score'],
            'EndSpeedMetersPerSecond':v['Speed'],'EndYawRateRadiansPerSecond':v['AngularVelocity'],
            'MaximumBetaDegrees':max(abs(q['Rider']['SlideRadians']) for q in leaf['Prediction']['Points'])*180/math.pi,
            'ProgressMeters':e['Progress'],'EndReferenceMeters':v['TotalReferenceMeters'],
            'HorizonSeconds':.9,'NoThrottlePriorScore':e['Score']-terms['ThrottlePrior'],
            'MeanYawCost':leaf['MeanYawCost'],
            'NoPriorMeanYawScore':e['Score']-terms['ThrottlePrior']-leaf['TerminalYawCost']+leaf['MeanYawCost']})
    rank=lambda x:(x['SafetyH09'],x['Score'],abs(x['Action']['SteeringRadians']-i['Active']['SteeringRadians']),x['Action']['SteeringRadians'])
    chosen=min(components,key=rank)
    no_prior=min(components,key=lambda x:(x['SafetyH09'],x['NoThrottlePriorScore'],abs(x['Action']['SteeringRadians']-i['Active']['SteeringRadians']),x['Action']['SteeringRadians']))
    best_gas=[min([x for x in components if abs(x['Action']['Drive']-gas)<1e-6],key=rank) for gas in [0,policy['Drive'],1]]
    mean=min(components,key=lambda x:(x['SafetyH09'],x['NoPriorMeanYawScore'],abs(x['Action']['SteeringRadians']-i['Active']['SteeringRadians']),x['Action']['SteeringRadians']))
    rows.append({'Name':d['State']['Name'],'Phase':d['State']['Phase'],'TimeSeconds':i['Time'],'Current':i,
        'Chosen':chosen,'BestPerGas':best_gas,'WithoutThrottlePrior':no_prior,
        'FullGasWinsWithoutPrior':no_prior['Action']['Drive']>.999,'WithoutPriorAndMeanYaw':mean,
        'AllShortClassificationsRemainForecastOnly':True})

reductions=[]
datasets=[('D0',1,ROOT/'docs/experiments/motorcycle-physics-validation/native/off-D0.json.gz','unmodified134A'),
          ('D1',2,ROOT/'docs/experiments/motorcycle-physics-validation/native/D1-C.json.gz','139C physical history;134A resumes after short request'),
          ('H0',1,OLD/'native/extended-V0-b-A.json.gz','unmodified134A'),
          ('H1',2,OLD/'native/extended-V1-c-A.json.gz','unmodified134A')]
for name,corner,path,label in datasets:
    d=read(path);replay=d['Replay'];ss=[x['Input'] for x in replay['Samples']];exit_ref=132.38937497138977 if corner==1 else 291.77874994277954;next_ref=exit_ref+62
    decisions=replay['Decisions'];first=None;fulls=[]
    for j,decision in enumerate(decisions):
        sample=next((x for x in ss if x['Time']>=decision['Time']-1e-8),None)
        if sample is None or not exit_ref<=sample['Rider']['TotalReferenceMeters']<=next_ref:continue
        if decision['Action']['Drive']>.999:fulls.append(decision['Time'])
        prior=decisions[j-1]['Action']['Drive'] if j else sample['Active']['Drive']
        if first is None and decision['Action']['Drive']<prior-1e-6:
            activation=next((a for a in replay['Activations'] if abs(a['ObservedAt']-decision['Time'])<1e-8),None)
            if activation is None:continue
            actual=next((x for x in ss if x['Time']>=activation['ActivationAt']-1e-8),None)
            if actual is None:continue
            first={'RequestTimeSeconds':decision['Time'],'ActivationTimeSeconds':activation['ActivationAt'],'PriorDrive':prior,'RequestedDrive':decision['Action']['Drive'],
                'RequestReferenceMeters':sample['Rider']['TotalReferenceMeters'],'MetersBeforeNextTurn':next_ref-sample['Rider']['TotalReferenceMeters'],
                'ReferenceDistanceDuringReactionMeters':actual['Rider']['TotalReferenceMeters']-sample['Rider']['TotalReferenceMeters'],
                'PhysicalDistanceDuringReactionMeters':actual['Rider']['DistanceMeters']-sample['Rider']['DistanceMeters']}
    reductions.append({'Case':name,'SourceHistory':label,'StudyCorner':corner,'StudyExitReferenceMeters':exit_ref,'NextTurnStartReferenceMeters':next_ref,
        'FirstReductionOnStraight':first,'FullRequestsOnStraightSeconds':fulls,'CaptureEndReferenceMeters':ss[-1]['Rider']['TotalReferenceMeters']})

replans=[]
for name in ['plans-cost-D-B-B.json.gz','plans-cost-D-C-B.json.gz','plans-extended-V0-b-B.json.gz','plans-extended-V1-c-B.json.gz']:
    plans=read(OLD/name);pairs=list(zip(plans,plans[1:]));valid=[(a,b) for a,b in pairs if abs(b['Time']-a['Time']-.2)<1e-7]
    changed=[(a,b) for a,b in valid if a['Second']!=b['First']]
    promising=[(a,b) for a,b in valid if a['First']['Drive']>.999 or a['Second']['Drive']>.999]
    replans.append({'Source':name,'AdjacentReviews':len(valid),'NextRequestDiffersFromPredictedSecond':len(changed),
        'FullGasRelatedPlans':len(promising),'FullRelatedSecondChanged':sum(a['Second']!=b['First'] for a,b in promising),
        'Examples':[{'TimeSeconds':a['Time'],'PredictedSecond':a['Second'],'ActualNextRequest':b['First']} for a,b in changed[:3]],
        'Meaning':'Unrequested forecast suffix is reconsidered; already-issued pending commands are not cancelled.'})

report={'BaseHead':'932fc33ba20a8c96c2cd1a102957c9b297d412ea','Audit':{'NativeWorlds':0,'NewForecasts':0,'Policy':policy,'RankingStates':rows,
    'ThrottlePriorForFullGasScore':policy['ThrottleWeight']*(1-policy['Drive'])**2,
    'StraightReductions':reductions,'Replanning':replans,'SourcesSha256':sources,
    'MaximumScoreReconstructionError':max(abs(x['ScoreReconstructionError']) for r in rows for x in r['BestPerGas']),
    'CpuSeconds':time.process_time()-started,
    'DecompositionMeaning':'Saved137 terminal/mean yaw costs use preview geometry. GeometrySpeed is the score remainder after all other known terms; closure is not independent validation.',
    'Finding':'Throttle prior is decisive for some stable straight states; terminal yaw/direction and future geometry dominate others. H09 alone does not cover a full62m straight. Forecast suffixes in137 are not commitments.'},
    'Reservations':[],'Native':[]}
if previous:
    report['Audit']['PriorReadCpuSeconds']=previous['Audit']['CpuSeconds']
    report['Audit']['CpuSeconds']+=previous['Audit']['CpuSeconds']
    report['Audit']['ReadOnlyCorrection']='Initial draft used endpoint rather than preview Road in decomposition. Corrected from saved137 yaw and an explicitly derived geometry remainder before any prototype or worlds.'
DEST.parent.mkdir(parents=True,exist_ok=True);DEST.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('audit states',len(rows),'CPU',report['Audit']['CpuSeconds'],'score error',report['Audit']['MaximumScoreReconstructionError'])
print('full after removing prior',sum(r['FullGasWinsWithoutPrior'] for r in rows))
print('reductions',reductions)
print('replanning',replans)
