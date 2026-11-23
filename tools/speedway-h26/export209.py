"""Export exact sampled command attribution and the active restriction inventory."""
import csv, gzip, json, pathlib, sys, collections
root=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else '.')
out=root/'data/experiments/speedway209'
def read(p): return json.load(gzip.open(p,'rt') if str(p).endswith('.gz') else open(p,encoding='utf-8-sig'))
inventory=read(out/'baseline/baseline.json')
for label,folder in [('baseline','effective-audit'),('final','official-effective')]:
    run=read(out/f'{folder}/C.json.gz'); rows=run['Trace']
    pending={round(p['ObservedAt'],8):p for r in rows for p in r['Before']['Pending']}
    commands={r['RequestedAt']:r for r in rows}
    fields=['Time','S','TrackSegment','SpeedKph','FeedForward','DriveBias','SpeedFeedback','Ceiling','RawRequested',
        'Requested','Active','ActivationAt','ObservedActivationDelay','DominantRawStage','DominantEffective208Restriction',
        'Reconstructed208Requested','Ramp','RecoveryGas','PaceTerms','SteeringRequest','SteeringActual']
    families=['feed-forward','drive bias','speed feedback','ceiling','coast','mid pace','exit pace','ramp','recovery gas']
    with open(out/f'{label}-command-clamps.csv','w',newline='',encoding='utf-8') as file:
        writer=csv.DictWriter(file,fieldnames=fields+[f'Without {x}: delta at 208 output' for x in families]);writer.writeheader()
        for t,r in sorted(commands.items()):
            q=r['Command']; state=r['Before']['State']; rider=state['Rider']; p=pending.get(round(t,8))
            row={k:q.get(k) for k in ['FeedForward','DriveBias','SpeedFeedback','Ceiling','RawRequested','DominantEffective208Restriction',
                'Reconstructed208Requested','Ramp','RecoveryGas']}
            row.update(Time=t,S=rider['TotalReferenceMeters'],TrackSegment=rider['Road']['Segment'],SpeedKph=rider['Speed']*3.6,
                Requested=q['Selected']['Drive'],Active=r['Before']['Active']['Drive'],ActivationAt=p['ActivationAt'] if p else None,
                ObservedActivationDelay=p['ActivationAt']-t if p else None,DominantRawStage=q['Dominant'],
                PaceTerms=json.dumps(q['PaceTerms'],separators=(',',':')),SteeringRequest=q['Selected']['SteeringRadians'],SteeringActual=state['Steer'])
            row.update({f'Without {x}: delta at 208 output':q['EffectiveRemovalDeltaAt208Output'][x] for x in families})
            writer.writerow(row)
    if label=='baseline':
        effective=collections.defaultdict(lambda:{'PositiveSeconds':0,'IntegratedThrottleSeconds':0,'Maximum':0})
        reconstruction=0; masked=0
        for r in rows:
            q=r['Command']; dt=r['After']['Time']-r['Before']['State']['Time']
            reconstruction=max(reconstruction,abs(q['Reconstructed208Requested']-q['Selected']['Drive']))
            for key,value in q['EffectiveRemovalDeltaAt208Output'].items():
                e=effective[key];e['PositiveSeconds']+=dt*(value>1e-7);e['IntegratedThrottleSeconds']+=max(0,value)*dt;e['Maximum']=max(e['Maximum'],value)
            masked+=int(q['Ramp']<-.01 and q['EffectiveRemovalDeltaAt208Output']['ramp']<=1e-7)
        (out/'effective-attribution.json').write_text(json.dumps({'MaximumReconstructionError':reconstruction,
            'At208Output':effective,'MaskedNegativeRampAcceptedSteps':masked,'Additive':False},indent=2),encoding='utf-8')
        phase_rows=[]
        for item in inventory['Phases']:
            phase=item['Phase']; move=item['Move']; active=[r for r in rows if phase['Start']<=r['Before']['State']['Rider']['TotalReferenceMeters']<phase['End']]
            phase_rows.append({**phase,**move,'AcceptedSteps':len(active),
                'FirstTime':active[0]['Before']['State']['Time'] if active else None,
                'LastTime':active[-1]['After']['Time'] if active else None,
                'CeilingActuallySuppressesSeconds':sum(r['After']['Time']-r['Before']['State']['Time'] for r in active
                    if r['Command']['EffectiveRemovalDeltaAt208Output']['ceiling']>1e-7)})
        with open(out/'phase-restrictions.csv','w',newline='',encoding='utf-8') as file:
            writer=csv.DictWriter(file,fieldnames=phase_rows[0].keys());writer.writeheader();writer.writerows(phase_rows)
print('Exported command-local counterfactual attribution and phase inventory.')
