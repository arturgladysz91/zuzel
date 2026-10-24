"""Summarize frozen #132 native evidence. Never train, select, or change a policy."""
import argparse,csv,gzip,hashlib,importlib.util,json,math,statistics,os
from collections import Counter
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
D=ROOT/'docs/experiments/full-throttle-learning'
spec=importlib.util.spec_from_file_location('extended_report',ROOT/'tools/extended-learning-report.py')
prior=importlib.util.module_from_spec(spec);spec.loader.exec_module(prior)
read=lambda p:json.loads(Path(p).read_bytes())
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
def write(n,o):
    (D/n).write_text(json.dumps(o,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def table(headers,rows):return prior.table(headers,rows)
def archive_audit():
    path=D/'throttle-audit.json';packed=D/'throttle-audit.json.gz'
    original=path.read_bytes() if path.exists() else gzip.decompress(packed.read_bytes())
    packed.write_bytes(gzip.compress(original,compresslevel=9,mtime=0))
    write('throttle-audit-archive.json',dict(OriginalSha256=hashlib.sha256(original).hexdigest().upper(),CompressedSha256=sha(packed),UncompressedBytes=len(original),Scope='Full original candidate/state/cost audit, unabridged gzip. Plain original remains local.'))
    return json.loads(original)
def throttle(run):
    ds=run['Replay']['Decisions'];values=[d['Action']['Drive'] for d in ds]
    reopen=[d['Time'] for i,d in enumerate(ds) if d['Action']['Drive']>=.99 and i>0 and values[i-1]<.99]
    acts=run['Replay']['Activations'];active=[c['ActivationAt'] for i,c in enumerate(acts) if i>0 and c['Action']['Drive']>=.99 and acts[i-1]['Action']['Drive']<.99]
    unactivated=[d['Time'] for d in ds if d['Action']['Drive']==1 and not any(abs(c['ObservedAt']-d['Time'])<1e-8 for c in acts)]
    observed={round(s['Input']['Time'],8):s['Input']['Active']['Drive'] for s in run['Replay']['Samples']}
    after_drop=[d['Time'] for d in ds if d['Action']['Drive']==1 and observed[round(d['Time'],8)]<.99]
    return dict(ZeroDecisions=sum(x<.01 for x in values),PartialDecisions=sum(.01<=x<.99 for x in values),FullDecisions=sum(x>=.99 for x in values),Reopenings=len(reopen),ReopeningTimes=reopen,ActiveReopenings=len(active),ActiveReopeningTimes=active,UnactivatedFullRequests=unactivated,FullRequestsAfterActiveDrop=len(after_drop),FullRequestsAfterActiveDropTimes=after_drop)
def quality(run):
    ts=run['Telemetry'];result=dict(prior.quality(run),**throttle(run))
    result.update(MaximumAbsoluteWheelSlipRatio=max(abs(t['Wheel']['InstantSlipRatio']) for t in ts),MaximumWheelOmega=max(t['Wheel']['AfterOmega'] for t in ts),
        MaximumRearUtilization=max(t['Wheel']['RearUtilization'] for t in ts),MaximumFrontUtilization=max(math.hypot(t['Forces']['FrontForce']['X'],t['Forces']['FrontForce']['Y'])/t['Forces']['TyreLoads']['FrontCapacityNewtons'] for t in ts),
        MaximumEngineRpm=max(t['Forces']['Engine']['EngineRpm'] for t in ts))
    return result
def audit_summary(audit):
    result=[]
    for name in sorted({r['Run'] for r in audit['Records']}):
        rs=[r for r in audit['Records'] if r['Run']==name]
        def stats(rows):
            coast=[r for r in rows if r['Chosen']['Drive']<.01]
            full=[]
            for r in rows:
                cs=[x for x in r['Candidates'] if x['Candidate']['Action']['Drive']==1]
                safe=[x for x in cs if x['Candidate']['Safety']==0]
                full.append((r,cs,safe))
            return dict(Decisions=len(rows),FullUnavailable=sum(not r['OriginalHasFull'] for r in rows),
                CoastWithSafePositive=sum(r['SafePositive'] for r in coast),CoastWithoutSafePositive=sum(not r['SafePositive'] for r in coast),
                ExpandedFullChosen=sum(r['ExpandedChosen']['Drive']==1 for r in rows),
                FullSafeButLoses=sum(bool(safe) and r['ExpandedChosen']['Drive']!=1 for r,cs,safe in full),
                AllFullUnsafe=sum(bool(cs) and all(x['Candidate']['Safety']==2 for x in cs) for r,cs,safe in full),
                FullChosenWithWeight0=sum(r['OfflineRanks'][0]['Action']['Drive']==1 for r in rows),
                FullChosenWithWeight1=sum(r['OfflineRanks'][1]['Action']['Drive']==1 for r in rows))
        result.append(dict(Run=name,**stats(rs),Segments={str(seg):stats([r for r in rs if r['Segment']==seg]) for seg in sorted({r['Segment'] for r in rs})}))
    return result
def cost_differences(audit):
    rows=[]
    for r in audit['Records']:
        chosen=next(x for x in r['Candidates'] if x['Candidate']['Action']==r['Chosen'])
        for name,cs in [('positive',[x for x in r['Candidates'] if x['Candidate']['Action']['Drive']>0]),('full',[x for x in r['Candidates'] if x['Candidate']['Action']['Drive']==1])]:
            same=[x for x in cs if x['Candidate']['Safety']==r['Safety']]
            if not same:continue
            alternative=min(same,key=lambda x:x['Candidate']['Score']);delta=alternative['Candidate']['Score']-chosen['Candidate']['Score']
            td=alternative['ThrottleCost']-chosen['ThrottleCost'];pd=alternative['ProgressCost']-chosen['ProgressCost'];dd=alternative['DirectionCost']-chosen['DirectionCost']
            rows.append(dict(Run=r['Run'],Time=r['Time'],Segment=r['Segment'],Alternative=name,Chosen=r['Chosen'],Action=alternative['Candidate']['Action'],Safety=r['Safety'],ScoreDelta=delta,ThrottleDelta=td,ProgressDelta=pd,DirectionDelta=dd,OtherDelta=delta-td-pd-dd))
    return rows
def paired(final):
    results=[]
    for suite in ['single','two']:
        groups={r['Variant'].removesuffix('-'+suite):r for r in final if r['Variant'].endswith('-'+suite)}
        for old,new in [('130-H06-A','132-final'),('131-A','132-final'),('131-A','131-B'),('131-B','132-final'),('130-H06-A','131-A'),('130-H06-A','131-B')]:
            a,b=groups[old],groups[new];pairs=[]
            for name,x,y in zip(a['Scenarios'],a['Results'],b['Results']):
                x,y=x['Native'],y['Native']
                if x['Completed'] and y['Completed']:
                    pairs.append(dict(Scenario=name,Old=x['GateSeconds'],New=y['GateSeconds'],Delta=y['GateSeconds']-x['GateSeconds'],Ratio=y['GateSeconds']/x['GateSeconds']-1))
            ratios=[p['Ratio'] for p in pairs]
            results.append(dict(Suite=suite,Reference=old,Compared=new,Pairs=pairs,MedianRatio=statistics.median(ratios) if ratios else None,MaximumRatio=max(ratios) if ratios else None,MinimumRatio=min(ratios) if ratios else None))
    return results
def main():
    global D
    parser=argparse.ArgumentParser();parser.add_argument('raw');parser.add_argument('--data',default=str(D));args=parser.parse_args();raw=Path(args.raw).resolve();D=Path(args.data).resolve()
    p=read(D/'protocol.json');attempts=read(D/'attempts.json');final=read(D/'final-results.json');winner=read(D/'winner.json');lock=read(D/'winner-lock.json')
    audit=archive_audit();audit_stats=audit_summary(audit);write('throttle-audit-summary.json',audit_stats);differences=cost_differences(audit);write('cost-differences.json',differences)
    coast=[r for r in differences if r['Run'].startswith('diag-A') and r['Chosen']['Drive']==0 and r['Alternative']=='positive']
    write('starting-cost-findings.json',dict(CoastWithSameSafetyPositive=len(coast),DirectionCostHigher=sum(r['DirectionDelta']>0 for r in coast),
        MedianDeltas={k:statistics.median(r[k] for r in coast) for k in ['ScoreDelta','ThrottleDelta','ProgressDelta','DirectionDelta','OtherDelta']},
        Scope='Best positive candidate of the same safety class minus selected coast, unchanged131 weights on four frozen diagnostic inputs. Each median is separate, not an additive decomposition of the median total.'))
    pairs=paired(final);write('paired-times.json',pairs)
    load=lambda name:json.loads(gzip.decompress((raw/(name+'.json.gz')).read_bytes()))
    throttle_rows=[];totals={};learned_costs={};cost_examples=[]
    for entry in attempts:
        if not entry['NativeWorlds']:continue
        run=load(entry['Name']);gas=throttle(run);n=entry['Metrics']['Native'];category=entry['Category']
        label=entry['Name'].split('-g')[0] if category in ['train-ab','continuation'] else 'final:'+entry['Name'].removeprefix('final-').rsplit('-',1)[0] if category=='final' else category
        row=dict(Name=entry['Name'],Role=category,Label=label,**gas,MeanDrive=n['MeanDrive'],FullDriveFraction=n['FullDriveFraction'],DurationSeconds=n['DurationSeconds']);throttle_rows.append(row)
        t=totals.setdefault(label,dict(Worlds=0,ZeroDecisions=0,PartialDecisions=0,FullDecisions=0,Reopenings=0,ActiveReopenings=0,FullRequestsAfterActiveDrop=0,ActiveFullSeconds=0,DurationSeconds=0,DriveIntegral=0))
        t['Worlds']+=1
        for k in ['ZeroDecisions','PartialDecisions','FullDecisions','Reopenings','ActiveReopenings','FullRequestsAfterActiveDrop']:t[k]+=gas[k]
        t['ActiveFullSeconds']+=n['FullDriveFraction']*n['DurationSeconds'];t['DurationSeconds']+=n['DurationSeconds'];t['DriveIntegral']+=n['MeanDrive']*n['DurationSeconds']
        if category in ['train-ab','continuation'] and label.startswith('B'):
            policy=run['Replay']['Policy'];counters=learned_costs.setdefault(label,dict(Decisions=0,ActualFull=0,ActualSafeFull=0,CoastWithSafePositive=0,OfflineWeight0Full=0,OfflineWeight1Full=0,OfflineWeight4Full=0,OfflineWeight0SafeFull=0,OfflineWeight1SafeFull=0,OfflineWeight4SafeFull=0))
            active={round(s['Input']['Time'],8):s['Input']['Active']['SteeringRadians'] for s in run['Replay']['Samples']}
            for dec in run['Replay']['Decisions']:
                counters['Decisions']+=1;counters['ActualFull']+=int(dec['Action']['Drive']==1)
                counters['ActualSafeFull']+=int(dec['Action']['Drive']==1 and dec['Safety']==0)
                counters['CoastWithSafePositive']+=int(dec['Action']['Drive']==0 and any(c['Safety']==0 and c['Action']['Drive']>0 for c in dec['Candidates']))
                for weight in [0,1,4]:
                    ranked=min(dec['Candidates'],key=lambda c:(c['Safety'],c['Score']+(weight-policy['ThrottleWeight'])*(c['Action']['Drive']-policy['Drive'])**2,abs(c['Action']['SteeringRadians']-active[round(dec['Time'],8)]),c['Action']['SteeringRadians']))
                    counters[f'OfflineWeight{weight}Full']+=int(ranked['Action']['Drive']==1)
                    counters[f'OfflineWeight{weight}SafeFull']+=int(ranked['Action']['Drive']==1 and ranked['Safety']==0)
                    if weight==0 and ranked['Action']['Drive']==1 and dec['Action']['Drive']!=1 and len(cost_examples)<100:
                        cost_examples.append(dict(Run=entry['Name'],Time=dec['Time'],Original=dec['Action'],Offline=ranked['Action'],Safety=ranked['Safety'],OriginalThrottleWeight=policy['ThrottleWeight'],DrivePreference=policy['Drive']))
    for t in totals.values():t['MeanDrive']=t['DriveIntegral']/t['DurationSeconds'];t['FullDriveFraction']=t['ActiveFullSeconds']/t['DurationSeconds']
    write('throttle-by-attempt.json',throttle_rows);write('throttle-totals.json',totals)
    write('learned-cost-audit.json',dict(Summary=learned_costs,First100ChangedFullChoices=cost_examples,Scope='Offline reranking of identical saved B states/candidates, changing only ThrottleWeight to0/1/4 inside original bounds. No rollout, retraining, or claim of full-bend safety. Original A/B objective preserved.'))
    diagnostics=[load(f'diag-{v}-D{i}') for v in ['A','B'] for i in range(4)]
    diagnostic_quality=[quality(r) for r in diagnostics];write('diagnostic-quality.json',diagnostic_quality)
    parity=load('parity131-V00');write('reference131-quality.json',quality(parity))
    summaries=[]
    for r in final:
        summaries.append(dict(Variant=r['Variant'],Completed=sum(x['Native']['Completed'] for x in r['Results']),Cases=len(r['Results']),DNF=dict(Counter(x['Native']['Outcome'] for x in r['Results'] if not x['Native']['Completed']))))
    generations=[];seed_results=[]
    for seed in p['Seeds']:
        for variant in ['A','B']:
            label=f'{variant}-s{seed}';gs=read(D/f'generations-{label}.json')
            for g in gs:
                ns=[r['Native'] for c in g['Candidates'] for r in c['Results']]
                best=next(c for c in g['Candidates'] if c['Id']==g['BestId'])
                generations.append(dict(Variant=label,Generation=g['Generation'],Completed=sum(n['Completed'] for n in ns),TwoCompleted=sum(r['Native']['Completed'] for c in g['Candidates'] for name,r in zip(c['Scenarios'],c['Results']) if name.endswith('-two')),
                    BestId=g['BestId'],BestCompleted=sum(r['Native']['Completed'] for r in best['Results']),GateTimes=[n['GateSeconds'] for n in ns],MeanDrive=sum(n['MeanDrive']*n['DurationSeconds'] for n in ns)/sum(n['DurationSeconds'] for n in ns),
                    FullFraction=sum(n['FullDriveFraction']*n['DurationSeconds'] for n in ns)/sum(n['DurationSeconds'] for n in ns),BoundaryFailures=sum(n['Outcome'] in ['confirmed crossing','uncertified interval'] for n in ns),MaximumBeta=max(n['MaximumBetaDegrees'] for n in ns),MinimumClearance=min(n['MinimumClearanceMeters'] for n in ns),CpuSeconds=g['CpuSeconds']))
            selection=[s for s in read(D/'selection.json') if s['Variant']==label]
            seed_results.append(dict(Variant=label,TrainingCompleted=sum(r['Native']['Completed'] for g in gs for c in g['Candidates'] for r in c['Results']),Episodes=sum(len(c['Results']) for g in gs for c in g['Candidates']),Selection=[dict(Id=s['Id'],Completed=sum(r['Native']['Completed'] for r in s['Results']),TwoCompleted=sum(r['Native']['Completed'] for name,r in zip(s['Scenarios'],s['Results']) if name.endswith('-two')),Times=[r['Native']['GateSeconds'] for r in s['Results']]) for s in selection]))
    write('generations-summary.json',generations);write('seed-results.json',seed_results)
    selection=read(D/'selection.json');decision=read(D/'continuation-decision.json')
    best_b=next(s for s in selection if s['Id']==decision['BestB']);best_a=next(s for s in selection if s['Id']==decision['BestA']);baseline=next(s for s in selection if s['Variant']=='131-A')
    def two_times(s):return [r['Native']['GateSeconds'] for name,r in zip(s['Scenarios'],s['Results']) if name.endswith('-two') and r['Native']['Completed']]
    def reserve(s):return min(r['Native']['MinimumClearanceMeters'] for r in s['Results'])
    selection_pairs=[]
    for s in selection:
        ratios=[r['Native']['GateSeconds']/old['Native']['GateSeconds']-1 for name,r,old in zip(s['Scenarios'],s['Results'],baseline['Results']) if name.endswith('-two') and r['Native']['Completed'] and old['Native']['Completed']]
        selection_pairs.append(dict(Variant=s['Variant'],Id=s['Id'],TwoCompleted=len(two_times(s)),WorstCompletedTwo=max(two_times(s)) if two_times(s) else None,PairedTwoMedianRatio=statistics.median(ratios) if ratios else None,MinimumClearance=reserve(s)))
    write('selection-tradeoffs.json',dict(Source='K0-K5 only; description of the frozen rule, never a replacement choice',Candidates=selection_pairs,
        Extension=dict(Extend=decision['Extend'],BestB=best_b['Id'],BestA=best_a['Id'],BWorstTwo=max(two_times(best_b)),AWorstTwo=max(two_times(best_a)),BaselineWorstTwo=max(two_times(baseline)),BMinimumClearance=reserve(best_b),BaselineMinimumClearance=reserve(baseline),ReserveCriterionPassed=reserve(best_b)>=reserve(baseline))))
    for filename,rows in [('attempts.csv',[{**(e.get('Metrics') or {}).get('Native',{}),'Number':e['Number'],'Name':e['Name'],'Category':e['Category'],'NativeWorlds':e['NativeWorlds'],'CpuSeconds':e['CpuSeconds'],'Evaluations':e['Evaluations'],'Forecasts':e['Forecasts']} for e in attempts]),('generations.csv',generations)]:
        keys=list(dict.fromkeys(k for row in rows for k in row));
        with (D/filename).open('w',newline='',encoding='utf-8') as f:
            w=csv.DictWriter(f,fieldnames=keys);w.writeheader();w.writerows(rows)
    labels={'130-H06-A':'#130 #1020 H06 · A','131-A':'#131 #4061 H09 · A','131-B':'#131 #4061 H09 · B bez nauki','132-final':f'#132 #{winner["Id"]} H09 · '+('B' if winner['FullThrottle'] else 'A')}
    colors={'130-H06-A':'#ff9c86','131-A':'#ffd27b','131-B':'#d99fe3','132-final':'#73dfdf'}
    groups=[];selected={}
    names=['W00','Z00'];
    reopened=next((row for row in throttle_rows if row['Label']=='final:132-final' and row['FullRequestsAfterActiveDrop']>0),None)
    if reopened:names.append(reopened['Name'].rsplit('-',1)[1])
    # Largest paired improvement and regression are shown, including a failure when any exists.
    two_pair=next(x for x in pairs if x['Suite']=='two' and x['Reference']=='131-A' and x['Compared']=='132-final')
    if two_pair['Pairs']:
        names+=[min(two_pair['Pairs'],key=lambda x:x['Ratio'])['Scenario'],max(two_pair['Pairs'],key=lambda x:x['Ratio'])['Scenario']]
    for r in final:
        if r['Variant'].startswith(('131-B','132-final')):
            failure=next((n for n,m in zip(r['Scenarios'],r['Results']) if not m['Native']['Completed']),None)
            if failure:names.append(failure)
    for name in dict.fromkeys(names):
        two=name.startswith('Z');scenario=next(s for s in p['FinalTwo' if two else 'FinalSingle'] if s['Name']==name);runs=[]
        for policy in labels:
            run_name=f'final-{policy}-{name}';selected[run_name]=load(run_name);runs.append(dict(Name=run_name,Policy=policy))
        groups.append(dict(Id=name,Scenario=scenario,TwoBends=two,Reasons=['identyczny nowy stan', 'dwa pełne łuki' if two else 'pierwszy łuk'],Scope='Całe natywne światy, bez resetu. Osiągnięcie bramki całym obrysem i dodatkowa1s kontroli. Wszystkie DNF pozostają widoczne.',Runs=runs))
    # Diagnostic A/B gives direct unchanged-weights treatment evidence.
    for variant in ['A','B']:selected[f'diag-{variant}-D1']=load(f'diag-{variant}-D1')
    labels.update({'diag-A':'#4061 H09 · diagnostyka A','diag-B':'#4061 H09 · diagnostyka B'});colors.update({'diag-A':'#ffd27b','diag-B':'#d99fe3'})
    groups.append(dict(Id='D1',Scenario=p['Diagnostic'][1],TwoBends=True,Reasons=['bez retreningu','oddzielna diagnostyka'],Scope='Wcześniej zamrożony stan diagnostyczny; pełne autonomiczne decyzje.',Runs=[dict(Name=f'diag-{v}-D1',Policy=f'diag-{v}') for v in ['A','B']]))
    failed=next((row for row in throttle_rows if row['Role']=='train-ab' and row['Label'].startswith('B-') and '-g00-' in row['Name'] and row['Reopenings']>0 and not next(e for e in attempts if e['Name']==row['Name'])['Metrics']['Native']['Completed']),None)
    if failed:
        mutation=load(failed['Name']);scenario=mutation['Replay']['Scenario'];seed=failed['Label'].split('-s')[1];seed_index=p['Seeds'].index(int(seed));base=5000+seed_index*2000;references=[]
        for v,policy_id in [('A',base),('B',base+1000)]:
            name=f'{v}-s{seed}-g00-p{policy_id}-{scenario["Name"]}';selected[name]=load(name);references.append(dict(Name=name,Policy='131-'+v))
        name=failed['Name'];selected[name]=mutation;policy='mutation-B';colors[policy]='#ff9c86'
        labels[policy]='Badana mutacja B #'+name.split('-p')[1].split('-')[0]+' · DNF'
        references.append(dict(Name=name,Policy=policy));groups.append(dict(Id='TRAIN-FAIL',Scenario=scenario,TwoBends=True,Reasons=['ponowne żądania100%','nieudana mutacja B'],Scope='Natywna próba treningowa z ponownym gaz1 i DNF. To badana mutacja, nie wybrany zwycięzca. Te same wejścia #4061 A/B.',Runs=references))
    queue_runs=[]
    for name,policy in [('A-s1322026-g00-p5001-F0','queue-A'),('B-s1322026-g00-p6001-F0','queue-B')]:
        selected[name]=load(name);queue_runs.append(dict(Name=name,Policy=policy))
    labels.update({'queue-A':'Mutacja #5001 · A i wcześniejsze pending1','queue-B':'Ta sama mutacja #6001 · B'});colors.update({'queue-A':'#ffd27b','queue-B':'#d99fe3'})
    groups.append(dict(Id='QUEUE-A',Scenario=p['Training'][0],TwoBends=False,Reasons=['zachowane pending','ta sama mutacja A/B'],Scope='A może w0,2s zaplanować utrzymanie wciąż aktywnego1 przed aktywacją wcześniejszego0. Nowe żądanie1 przy już obniżonym aktywnym gazie wymaga B. Obie polityki mają identyczne osiem wag.',Runs=queue_runs))
    qualities=[quality(r) for r in selected.values()];write('representative-quality.json',qualities)
    # Compare elapsed native time at fixed reference progress, never invent XY points.
    gain=[]
    for group in groups:
        if group['Id'] not in names:continue
        old=selected[f'final-131-A-{group["Id"]}'];new=selected[f'final-132-final-{group["Id"]}'];rows=[]
        for reference in range(30,291,5):
            def crossing(run):return next((s for s in run['Replay']['Samples'] if s['Input']['Rider']['TotalReferenceMeters']>=reference),None)
            a,b=crossing(old),crossing(new)
            if a and b:rows.append(dict(ReferenceMeters=reference,OldTime=a['Input']['Time'],NewTime=b['Input']['Time'],GainSeconds=a['Input']['Time']-b['Input']['Time'],OldXY=a['Input']['Rider']['Position'],NewXY=b['Input']['Rider']['Position']))
        gain.append(dict(Group=group['Id'],Samples=rows,Resolution='First accepted native sample crossing each5m reference. 0.01s discretization; XY positions are actual, not interpolated.'))
    write('time-gain.json',gain)
    bundle=gzip.compress(json.dumps(dict(Runs=selected),separators=(',',':'),allow_nan=False).encode(),compresslevel=9,mtime=0);(D/'selected-replays.json.gz').write_bytes(bundle)
    write('selected-manifest.json',dict(BundleSha256=sha(D/'selected-replays.json.gz'),Runs={n:dict(RawSha256=sha(raw/(n+'.json.gz')),NativeFingerprint=r['Metrics']['Native']['Fingerprint']) for n,r in selected.items()}))
    write('raw-manifest.json',[dict(Name=e['Name'],Sha256=e['RawSha256'],Bytes=e['RawBytes']) for e in attempts if e['NativeWorlds']])
    sources=p['Sources'];physical=[n for n in sources if n not in ['Program.cs','FreeLearningController.cs','RobustLearningExperiment.cs','FullThrottleLearningExperiment.cs']]
    old_protocol=read(D.parent/'autonomous-speedway-extended-learning/protocol.json')
    unchanged=all(sources[n]==old_protocol['Sources'][n] for n in physical)
    preflight=read(D/'preflight.json')['Passed'];ns=next(s for s in summaries if s['Variant']=='132-final-single');nt=next(s for s in summaries if s['Variant']=='132-final-two')
    ps=next(x for x in pairs if x['Suite']=='single' and x['Reference']=='130-H06-A' and x['Compared']=='132-final')
    passed=preflight and unchanged and ns['Completed']>=23 and nt['Completed']>=11 and two_pair['MedianRatio'] is not None and two_pair['MedianRatio']<=-.02 and ps['MedianRatio'] is not None and ps['MedianRatio']<=.05 and ps['MaximumRatio']<=.15
    valid=preflight and unchanged and not any('invariant' in (e.get('Metrics') or {}).get('Native',{}).get('Outcome','') for e in attempts)
    analysis=dict(Verdict='PASS' if passed else 'PARTIAL' if valid else 'FAIL',NativeWorlds=sum(e['NativeWorlds'] for e in attempts),TrainingWorlds=sum(e['NativeWorlds'] for e in attempts if e['Category'] in ['train-ab','continuation']),
        CpuSeconds=sum(e['CpuSeconds'] for e in attempts),Forecasts=sum(e['Forecasts'] for e in attempts),ContactEvaluations=sum(e['Evaluations'] for e in attempts),
        Roles=dict(Counter(e['Category'] for e in attempts if e['NativeWorlds'])),PhysicalSourcesUnchanged=unchanged,PreflightPassed=preflight,Final=summaries,
        SinglePairedVs130Median=ps['MedianRatio'],SinglePairedVs130Maximum=ps['MaximumRatio'],TwoPairedVs131Median=two_pair['MedianRatio'],TwoPairedVs131Maximum=two_pair['MaximumRatio'])
    write('analysis.json',analysis)
    original={r['Variant']:r for r in final};same_fingerprints=all(x['Native']['Fingerprint']==y['Native']['Fingerprint'] for suite in ['single','two'] for x,y in zip(original['131-A-'+suite]['Results'],original['131-B-'+suite]['Results']))
    write('no-retraining-comparison.json',dict(Identical36NativeFingerprints=same_fingerprints,FullRequests=totals['final:131-B']['FullDecisions'],ActiveFullReopenings=totals['final:131-B']['ActiveReopenings'],InitialFullOnly=totals['final:131-B']['FullDecisions']==0,
        Scope='All36 new final states, identical #4061 coefficients and H09, different action availability. Forecast count differs; native trajectories/fingerprints compare actual world states.'))
    write('index.json',dict(Title='Full Throttle Learning #132',Grade=analysis['Verdict'],DefaultRun=reopened['Name'] if reopened else 'final-132-final-Z00',RawDirectory=str(raw),Labels=labels,Colors=colors,TimeGain='time-gain.json',Horizons={k:.6 if k=='130-H06-A' else .9 for k in labels},Groups=groups,Charts=[['learning.svg','Uczenie A/B · dwa seedy'],['final.svg','Niezależny finał · ukończenia i czasy parami'],['time-gain.svg','Gdzie zyskano czas · rzeczywiste przekroczenia postępu']]))
    plots(generations,pairs,summaries,gain)
    report(p,analysis,winner,seed_results,diagnostic_quality,audit_stats,pairs,qualities)
    write('manifest.json',{f.name:sha(f) for f in sorted(D.iterdir()) if f.is_file() and f.name not in ['manifest.json','budget.lock','throttle-audit.json']})
    print(json.dumps(analysis,ensure_ascii=False,indent=2))

def plots(generations,pairs,summaries,gain):
    os.environ.setdefault('MPLCONFIGDIR',str(ROOT/'work/mpl-cache'))
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig,ax=plt.subplots(1,2,figsize=(11,4))
    for label in dict.fromkeys(g['Variant'] for g in generations):
        gs=[g for g in generations if g['Variant']==label]
        ax[0].plot([g['Generation'] for g in gs],[g['TwoCompleted'] for g in gs],marker='o',label=label)
        ax[1].plot([g['Generation'] for g in gs],[g['FullFraction']*100 for g in gs],marker='o',label=label)
    ax[0].set(ylabel='Two-bend completions /15',xlabel='Generation');ax[1].set(ylabel='Mean active full-gas time % (descriptive)',xlabel='Generation');ax[0].legend(fontsize=7);fig.tight_layout();fig.savefig(D/'learning.svg');plt.close(fig)
    fig,ax=plt.subplots(1,2,figsize=(11,4));names=[s['Variant'] for s in summaries];ax[0].barh(names,[s['Completed']/s['Cases']*100 for s in summaries]);ax[0].set(xlabel='Completion %',xlim=(0,105))
    for suite in ['single','two']:
        pair=next(x for x in pairs if x['Suite']==suite and x['Reference']=='131-A' and x['Compared']=='132-final')
        ax[1].plot([p['Ratio']*100 for p in pair['Pairs']],marker='o',label=suite)
    ax[1].axhline(0,color='black',lw=.5);ax[1].set(ylabel='Paired time change vs131 A %',xlabel='Common completed new cases');ax[1].legend();fig.tight_layout();fig.savefig(D/'final.svg');plt.close(fig)
    fig,ax=plt.subplots(figsize=(9,4))
    for g in gain:
        ax.plot([r['ReferenceMeters'] for r in g['Samples']],[r['GainSeconds'] for r in g['Samples']],label=g['Group'])
    ax.axhline(0,color='black',lw=.5);ax.set(xlabel='Track reference progress m',ylabel='Native elapsed time gain vs131 A s');ax.legend();fig.tight_layout();fig.savefig(D/'time-gain.svg');plt.close(fig)
    for path in D.glob('*.svg'):path.write_text('\n'.join(line.rstrip() for line in path.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8')

def report(p,a,w,seeds,diagnostics,audit,pairs,quality):
    percent=lambda x:'brak wspólnych ukończeń' if x is None else f'{x*100:+.2f}%'
    reference=read(D/'reference131-quality.json')
    findings=read(D/'starting-cost-findings.json')
    extension=read(D/'continuation-decision.json')
    continuation='Selekcja nie spełniła zamrożonego warunku kontynuacji; zakończono uczenie po400 próbach.' if not extension['Extend'] else 'Warunek jakości kontynuacji został spełniony.'
    if (D/'continuation-stop.json').exists():continuation+=' Rezerwa niezależnej walidacji zamknęła dalsze uczenie przed kolejnym światem: patrz [zapis stopu](continuation-stop.json).'
    tradeoffs=read(D/'selection-tradeoffs.json');extension_details=tradeoffs['Extension'];selected_tradeoff=next(s for s in tradeoffs['Candidates'] if s['Id']==w['Id'])
    gas_totals=read(D/'throttle-totals.json')
    no_retrain=read(D/'no-retraining-comparison.json')
    selected_gas=gas_totals['final:132-final']
    text=f'''# Full Throttle Learning #132 — {a['Verdict']}

Status **PROVISIONAL**, domyślnie OFF. Baza dokładnie `{p['BaseHead']}`. Jeden Draft PR, bez merge. Fizyczne źródła, produkcyjny CoreSim, obwiednia β45°, H09, reakcja0,24s, decyzje0,2s, ±35° i90°/s pozostają bez zmian.

Zwycięzca **{w['Label']} #{w['Id']}**, przestrzeń **{'B (gaz0/Drive/1)' if w['FullThrottle'] else 'A (gaz0/Drive)'}**, H09. [Parametry](winner.json), [hash przed finałem](winner-lock.json), [zamrożony protokół](protocol.json). Wybór nastąpił wyłącznie na treningu i K; W/Z nie sterowały doborem, zatrzymaniem ani kontynuacją.

{table(['Parametr','Punkt wyjścia #4061','Wybrana polityka #'+str(w['Id'])],[[name,p['StartingPolicy'][name],w['Policy'][name]] for name in p['StartingPolicy'] if name!='Values'])}

Werdykt **{a['Verdict']}**. Nowa polityka ukończyła {next(s['Completed'] for s in a['Final'] if s['Variant']=='132-final-single')}/24 pojedynczych i {next(s['Completed'] for s in a['Final'] if s['Variant']=='132-final-two')}/12 dwóch łuków. Mediana zmiany czasu dwóch łuków względem #131-A: **{percent(a['TwoPairedVs131Median'])}**. Pojedynczy łuk wobec #130: mediana **{percent(a['SinglePairedVs130Median'])}**, maksimum **{percent(a['SinglePairedVs130Maximum'])}**. Kryteria23/24,11/12, zysk2%, regresja≤5%/15% zamrożono przed treningiem.

## 1–4. Baza, audyt gazu, rozszerzenie i test bez uczenia

Punkt wyjścia: #4061, dokładne osiem wag #131. A zachowuje oryginalną kolejność działań i wynik bitowy. B dodaje1 dla tych samych dziewięciu kątów i bieżących kątów kierownicy/aktywnej komendy. Stabilne `Distinct` usuwa duplikaty. Nie dodano±25°/±35° do gridu. Wybrany gaz trafia do istniejącej kolejki i silnika po pełnych0,24s, bez bezpośredniej zmiany koła lub mocy.

Istotny szczegół kolejki A: przy wciąż aktywnym1 można zażądać jego utrzymania, zanim wcześniejsze zero zostanie wykonane. W próbie A #5001 F0 zero zażądano w0,0s, utrzymanie1 w0,2s; aktywacje wystąpiły w0,24s i0,44s. To zaplanowany wcześniej powrót do1. Gdy w chwili obserwacji aktywny gaz jest już poniżej1, A nie może ponownie go zażądać; B może. Raport oddziela takie nowe żądanie od samej późniejszej aktywacji100% i od zmiany sekwencji żądań.

Przy `Drive=0,3936023`, `ThrottleWeight=4` koszt preferencji wynosi: gaz0 **0,6197**, Drive **0**, gaz1 **1,4708**. Kara sama w sobie preferuje częściowy gaz, a pełny obciąża bardziej niż zero. Częste zero nie wynika więc z nagrody za zamknięcie; postęp, kierunek, yaw, geometria i bezpieczeństwo mogą przeważyć utratę preferowanego Drive. Klasy bezpieczeństwa rozstrzygają przed kosztem. Lokalny safe H09 nie dowodzi bezpieczeństwa następnego łuku.

Znany V00 #131 odtworzono z identycznym fingerprintem: gaz średni **{reference['Native']['MeanDrive']*100:.2f}%**, pełny gaz **{reference['Native']['FullDriveFraction']*100:.2f}%**, gaz0 mimo lokalnie safe+ w **{reference['CoastWithSafePositiveDriveCandidate']}** decyzjach. [Pełna jakość odtworzenia](reference131-quality.json). To odniesienie historyczne, nie nowy holdout W/Z.

Na czterech diagnostycznych wejściach A **{findings['CoastWithSameSafetyPositive']}** decyzji gaz0 miało dodatnią alternatywę tej samej bezpiecznej klasy. O zamknięciu gazu rozstrzygnął koszt, nie brak safe+. Dodatni gaz miał większy składnik kierunku w **{findings['DirectionCostHigher']}** z tych stanów; mediana różnicy tego składnika wyniosła **{findings['MedianDeltas']['DirectionDelta']:.3f}**, wobec median korzyści postępu **{findings['MedianDeltas']['ProgressDelta']:.3f}** i preferencji gazu **{findings['MedianDeltas']['ThrottleDelta']:.3f}**. To oddzielne mediany, nie składniki mediany sumy. [Obliczenie](starting-cost-findings.json). Samo obniżenie kary gazu nie rozwiązuje zatem całego kompromisu orientacji i tempa.

{table(['Replay','Decyzje','gaz1 niedostępny','gaz0 mimo safe+','safe1 przegrywa koszt','gaz1 przy wadze0 (offline)'],[[x['Run'],x['Decisions'],x['FullUnavailable'],x['CoastWithSafePositive'],x['FullSafeButLoses'],x['FullChosenWithWeight0']] for x in audit])}

[Pełny audyt wszystkich stanów i kandydatów (gzip)](throttle-audit.json.gz) zapisuje wejście, pending, klasę, pełny koszt, składnik gazu/postępu/kierunku i rankingi przy wadze0/1/4. [Hash oryginalnych bajtów](throttle-audit-archive.json) potwierdza archiwum bez skrótów. [Różnice kosztów najlepszego dodatniego/pełnego kandydata tej samej klasy](cost-differences.json) oddzielają gaz, postęp, kierunek i pozostały koszt. [Podsumowanie według segmentów](throttle-audit-summary.json) rozdziela prostą0/4, wejście1/5, środek2/6, wyjście3/7. `FullUnavailable` dotyczy rzeczywistego zbioru danego ramienia. `safe1 przegrywa` bada rozszerzony zbiór na identycznym stanie. Przeliczenie wag jest wyłącznie offline; nie zmieniono funkcji celu i nie trenowano alternatywnej kary.

{table(['Bez treningu','Wynik','Bramka s','Gaz średni','Pełny gaz %','Decyzje1','Ponowne otwarcia','βmax°','Prześwit m'],[[q['Name'],q['Native']['Outcome'],q['Native']['GateSeconds'],round(q['Native']['MeanDrive'],3),round(q['Native']['FullDriveFraction']*100,2),q['FullDecisions'],q['Reopenings'],round(q['Native']['MaximumBetaDegrees'],2),round(q['Native']['MinimumClearanceMeters'],4)] for q in diagnostics])}

To pary D0–D3 ustalone przed światem; D0 jeden łuk, D1–D3 dwa. [Cała jakość](diagnostic-quality.json). Przed uczeniem wykonano testy celowane, parytet dokładnego fingerprintu #131 V00, powtórzenie A i B oraz cztery natywne kontynuacje H09 na stanach B. [Preflight](preflight.json), [deterministyczność](determinism.json), [pierwotny walidator](predictor-initial.json), [porównanie wspólnych czasów](predictor-aligned.json). Pierwotny walidator uznał brak dokładnie wspólnego czasu zdarzenia za błąd: przejście krzywizny12,696531s wobec12,696530s. Doprecyzowanie przed treningiem zachowuje obie próbki i różnicę1µs, porównuje wszystkie91 wymaganych punktów co10ms i koniec0,9s, bez interpolacji i bez zmiany tolerancji fizycznych. [Protokół audytu wyrównania](predictor-alignment-protocol.json). Wagi0/1 pokazują osiągalność innych wyborów w tej samej reprezentacji, bez ręcznego narzucania gazu. Klasy nie blokują komend; pending przesuwa ich aktywację, zachowując wszystkie wcześniejsze żądania.

## 5–8. CEM, dwa seedy, A/B i kontynuacja

Jedyna różnica A/B: dostępny kandydat gaz1. Ten sam odziedziczony CEM,32 losowania normalne/generację, zakresy ośmiu parametrów i funkcja celu. Każda populacja ma5 polityk, ocenianych na tych samych4 stanach (1 pojedynczy,3 pełne dwa łuki). Dwie grupy wejść powracają co drugą generację. Ranking priorytetyzuje legalne ukończenia dwóch łuków; szybki DNF nie wygrywa z ukończeniem. Aktualizacja dwóch elit, średnia0,4/0,6, minimum std15% zakresu. Nie ma gotowej ścieżki ani reguł gazu według segmentu.

{table(['Ramię / seed','Trening ukończone / próby','Selekcja K: ID i ukończenia (w tym dwa)'],[[s['Variant'],str(s['TrainingCompleted'])+'/'+str(s['Episodes']),'; '.join(str(x['Id'])+': '+str(x['Completed'])+'/6, dwa '+str(x['TwoCompleted'])+'/4' for x in s['Selection'])] for s in seeds])}

{table(['Próby uczące','Żądania gaz1','Żądania1 po aktywnym<1','Faktyczne ponowne otwarcia1','Średni gaz % czasu','Pełny gaz % czasu'],[[label,t['FullDecisions'],t['FullRequestsAfterActiveDrop'],t['ActiveReopenings'],round(t['MeanDrive']*100,2),round(t['FullDriveFraction']*100,2)] for label,t in gas_totals.items() if label.startswith(('A-s','B-s','B-continued'))])}

[Archiwa generacji](generations-summary.json), [oba seedy](seed-results.json), [osobna selekcja](selection.json), [decyzja o kontynuacji](continuation-decision.json). Kontynuacja wymagała6/6 i zysku≥0,10s najgorszego czasu dwóch łuków wobec A i #131-A bez obniżenia minimalnego prześwitu; maksymalnie100 prób. Nie wykonywano prób wyłącznie dla zapełnienia limitu. Trzy powtarzalne oceny kotwicy bez deklarowanego zysku zatrzymują dłuższy trening. Każda generacja zapisuje wszystkie czasy, gaz, β, prześwit, DNF i CPU; [CSV](generations.csv). Brak przewagi B wymaga oceny wag, predykcji i bezpieczeństwa na zapisanych stanach, a nie automatycznego usuwania kary.

{continuation}

W selekcji K najgorszy czas dwóch łuków najlepszej B wynosił **{extension_details['BWorstTwo']:.2f}s**, A **{extension_details['AWorstTwo']:.2f}s**, #131-A **{extension_details['BaselineWorstTwo']:.2f}s**. Minimalny prześwit B **{extension_details['BMinimumClearance']:.4f}m** wobec **{extension_details['BaselineMinimumClearance']:.4f}m** #131-A; warunek rezerwy: **{extension_details['ReserveCriterionPassed']}**. [Dokładny kompromis selekcji](selection-tradeoffs.json). Mediana sparowanego czasu dwóch łuków wybranego #{w['Id']} na K: **{percent(selected_tradeoff['PairedTwoMedianRatio'])}**. Zamrożony ranking najpierw chroni ukończenia i najgorszy czas; nie optymalizuje bezpośrednio mediany kryterium finałowego. Zachowano ten kompromis oraz niezmieniony wybór, także gdy wynik medianowy jest gorszy.

![Uczenie](learning.svg)

## 9–13. Finał, rzeczywisty gaz, prędkości i obwiednia

{table(['Polityka / zestaw','Ukończenia','DNF'],[[s['Variant'],str(s['Completed'])+'/'+str(s['Cases']),s['DNF']] for s in a['Final']])}

{table(['Zestaw','Odniesienie → nowa','Wspólne pary','Mediana','Maksimum'],[[x['Suite'],x['Reference']+' → '+x['Compared'],len(x['Pairs']),percent(x['MedianRatio']),percent(x['MaximumRatio'])] for x in pairs])}

{table(['Finał: 36 światów na politykę','Żądania gaz1','Żądania1 po aktywnym<1','Faktyczne ponowne otwarcia1','Średni gaz % czasu','Pełny gaz % czasu'],[[label.removeprefix('final:'),t['FullDecisions'],t['FullRequestsAfterActiveDrop'],t['ActiveReopenings'],round(t['MeanDrive']*100,2),round(t['FullDriveFraction']*100,2)] for label,t in gas_totals.items() if label.startswith('final:')])}

Wszystkie4 polityki mają identyczne36 nowe stany W00–W23 i Z00–Z11. W porównaniach użyto wyłącznie wspólnych ukończeń; DNF i liczebności pozostają jawne. Nie porównano średnich z różnych zestawów sukcesów. W/Z mają nowe kombinacje prędkości,offsetu,heading,yaw oraz start21m; nie są to U/V #131. [Pełne pary](paired-times.json), [pełny finał](final-results.json).

Efekt dostępności bez uczenia: **{no_retrain['Identical36NativeFingerprints']}** dla identyczności wszystkich36 natywnych fingerprintów A/B #4061. B zażądał gaz1 **{no_retrain['FullRequests']}** razy, faktycznie ponownie otworzył100% **{no_retrain['ActiveFullReopenings']}** razy. [Dokładne porównanie](no-retraining-comparison.json). Początkowy aktywny gaz1 pozostaje częścią oryginalnego stanu, a nie dowodem wyuczonego otwarcia.

{table(['Reprezentatywny replay','Decyzje0/część/1','Otwarcia1 po odpuszczeniu','Pełny gaz %','v wejścia/min/wyjścia łuk1; łuk2 m/s'],[[q['Name'],f"{q['ZeroDecisions']}/{q['PartialDecisions']}/{q['FullDecisions']}",q['Reopenings'],round(q['Native']['FullDriveFraction']*100,2),'; '.join('/'.join('—' if b[k] is None else f'{b[k]:.2f}' for k in ['EntrySpeed','MinimumSpeed','ExitSpeed']) for b in q['Bends'])] for q in quality if q['Name'].endswith('Z00')])}

[Jakość każdego reprezentatywnego przejazdu](representative-quality.json) zawiera czasy obu łuków, prędkość prostej, gaz, ponowne otwarcia, ψ/χ/yaw/β, obrys, oscylacje całej drogi XY, pracę silnika, bilans i oszacowanie strat opon. [Liczby żądań gazu0/część/1 we wszystkich próbach](throttle-by-attempt.json), [sumy według ramienia/seeda/roli](throttle-totals.json) oddzielają żądanie ponownego100% od faktycznej aktywacji po0,24s. Pełne próbki oraz siły obu opon,wheelspin,steering,pending są w natywnych replayach. Straty boczne to praca modelowych sił i momentów na wykonanym przemieszczeniu, nie pomiar prawdziwej nawierzchni. Naturalna kontra i uślizg nie są osobną karą; β45 pozostaje granicą wiarygodności. Wszystkie przekroczenia kończą próbę DNF. Numeryczna zgodność H09 z Box2D nie stanowi kalibracji rzeczywistego motocykla.

![Finał](final.svg)

## 14. Replay i miejsce zysku

Viewer: `tools/replay-viewer/?case=full-throttle-learning`. Wspólny tor i wejście, osobne kolory #130,#131-A,#131-B,#132. Wykres gazu rozdziela żądany, pierwszą oczekującą komendę (luka gdy brak pending) i aktywny. Kropki oznaczają żądanie1 przy już obniżonym aktywnym gazie; kwadraty ponowną aktywację1. To rzeczywiste przebiegi Box2D; nie animacja predyktora. [Indeks](index.json), [hash bundle](selected-manifest.json). Dostępne W00/Z00, Z01 z nowym żądaniem1 w10,40s i aktywacją10,64s, największy zysk i regresja, nieudana mutacja B oraz przykład wcześniejszego pending A. Domyślny finał pokazuje przypadek nowego ponownego żądania gaz1.

[Nakładka czasu](time-gain.json) pokazuje różnicę zegarów przy pierwszym faktycznym przekroczeniu kolejnych5m odniesienia; rozdzielczość0,01s, XY z natywnych próbek. Pozwala odróżnić zysk pierwszego łuku od konsekwencji na drugim. Wysoki gaz sam nie jest sukcesem.

![Zysk czasu](time-gain.svg)

## 6,15. Budżet i odtwarzalność

**{a['NativeWorlds']} nowych natywnych światów**, w tym **{a['TrainingWorlds']} uczących**. Role: `{a['Roles']}`. CPU **{a['CpuSeconds']:.3f}s**, prognozy **{a['Forecasts']}**, oceny kontaktu **{a['ContactEvaluations']}**. Limity750 światów/6000s/1,2M prognoz/100M kontaktów zamrożono z kosztów #131 i mnożnika1,5 dla B. Rezerwacja przed każdym światem, osobny koszt offline. Techniczne testy i CI nie są epizodami uczącymi. [Ledger](attempts.json), [CSV](attempts.csv), [manifest wszystkich raw](raw-manifest.json). Pełne raw są lokalnie, Git przechowuje agregaty i wybrane replaye. [Sprawdzenia techniczne](verification.json).

Odtworzenie z katalogu repo na Windows: `python -m pip install -r tools/full-throttle-report.requirements.txt`, następnie `pwsh -File tools/run-full-throttle-learning.ps1 -Data work/reproduce132 -Raw work/reproduce132-raw`. Skrypt wymaga świeżych katalogów i zachowuje oryginalny błąd wyrównania czasów. Historyczne dane pozostają zamrożone; testy weryfikują dokładne rozszerzenia źródeł, a następnie odtwarzają oryginalne bajty #131/#130 do dawnych sprawdzeń hashy. [Jawne trzy rozszerzenia](source-extensions.json).

## 16–17. Szybkość i dalsze ograniczenia

Mediana dwóch łuków {percent(a['TwoPairedVs131Median'])} wobec #131-A. Wybrany #{w['Id']} zażądał gaz1 **{selected_gas['FullDecisions']}** razy w36 finałowych światach; rzeczywiste ponowne aktywacje100%: **{selected_gas['ActiveReopenings']}**, średni aktywny gaz **{selected_gas['MeanDrive']*100:.2f}%**. Dostępność gaz1 rozdzielono od efektu uczenia przez wariant #131-B z niezmienionymi wagami.

[Audyt](throttle-audit-summary.json) wykazuje częste zamykanie gazu przy safe+ oraz duży koszt kierunku dodatnich alternatyw. To bezpośredni dowód kompromisu funkcji celu. [Kontrolowane przeliczenie na wszystkich zapisanych stanach uczącego się B](learned-cost-audit.json) porównuje niezmienione koszty i wagi0/1/4, z tymi samymi prognozami i klasami bezpieczeństwa. Wysoki ThrottleWeight asymetrycznie utrudnia wybór1 przy Drive<0,5, lecz sam nie wyjaśnia gaz0 zamiast Drive. CEM może zmieniać te parametry w dotychczasowych zakresach. Ranking chroniący ukończenia i najgorszy czas wybiera kompromis, który może przegrać na medianie; szybsze nieukończone kandydatury pozostają DNF.

Grid kierownicy,0,9s lokalnego podglądu i osiem globalnych preferencji pozostały stałe. Ten A/B nie rozdziela ich samodzielnego wpływu, więc nie dowodzi, że poszerzenie kątów lub horyzontu rozwiąże szybkość. Natywne mutacje z gaz1 pokazują zarówno ponowne otwieranie, jak i ryzyko β/bandy; nie ustalają fizycznego minimum czasu legalnych dwóch łuków. Fizyka pozaβ45 nadal wymaga osobnego badania. Rankingi offline zachowano oddzielnie od podstawowego A/B; nie uruchomiono treningu zmienionej funkcji celu ani nie zmieniono wybranego checkpointu po finale.

**Werdykt: {a['Verdict']}.**
'''
    (D/'README.md').write_text(text,encoding='utf-8')

if __name__=='__main__':main()
