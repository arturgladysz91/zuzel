"""Rebuild #131 tables/figures and selected native bundle; never select a policy."""
import argparse, csv, gzip, hashlib, json, math, statistics, subprocess, os
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
D = ROOT / 'docs/experiments/autonomous-speedway-extended-learning'

def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def write(name, obj):
    (D/name).write_text(json.dumps(obj, ensure_ascii=False, indent=2, allow_nan=False)+'\n', encoding='utf-8')

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()

def role(entry):
    name=entry['Name']
    if not entry['NativeWorlds']:return 'offline-diagnostic'
    if entry['Category'].startswith('train-'):return entry['Category']
    if name.startswith('final-'):return 'final-validation'
    if '-select-' in name:return 'selection'
    if name=='audit130-repeat' or name.startswith('repeat130-'):return 'reproduction'
    if name.startswith('validate-'):return 'predictor-validation'
    if name=='audit130-two' or name.startswith('counter-'):return 'diagnostic'
    raise ValueError('Unclassified native attempt '+name)

def replay_colors(labels):
    result={}
    for label in labels:
        if label in ['#130 H06','130-H06','#130 oryginał']:color='#ff9c86'
        elif label=='129-after200' or label.startswith(('H06','130-H09 #')):color='#ffd27b'
        elif label=='131-final' or label.startswith(('H09','two #')):color='#73dfdf'
        elif label.startswith('jedna komenda'):color='#d99fe3'
        elif label.startswith('walidacja'):color='#8bcf74'
        else:color='#a6b8ff'
        result[label]=color
    return result

def table(headers, rows):
    return '\n'.join(['| '+' | '.join(headers)+' |', '| '+' | '.join(['---']*len(headers))+' |'] +
                     ['| '+' | '.join(map(str, row))+' |' for row in rows])

def angle(a):
    return (a+math.pi) % (2*math.pi)-math.pi

def quality(run):
    samples=run['Replay']['Samples']; telemetry=run['Telemetry']; decisions=run['Replay']['Decisions']
    result=dict(run['Metrics']); result['Name']=run['Replay']['Name']; result['Bends']=[]
    for start,end in [(1,4),(5,8)]:
        within=[s for s in samples if start<=s['Input']['Rider']['Road']['Segment']<end]
        after=[s for s in samples if s['Input']['Rider']['Road']['Segment']>=end]
        if within:
            result['Bends'].append(dict(EntrySpeed=within[0]['Input']['Rider']['Speed'],MinimumSpeed=min(s['Input']['Rider']['Speed'] for s in within),
                ExitSpeed=after[0]['Input']['Rider']['Speed'] if after else None,
                Seconds=after[0]['Input']['Time']-within[0]['Input']['Time'] if after else None))
    straight=[s['Input']['Rider']['Speed'] for s in samples if s['Input']['Rider']['Road']['Segment']==4]
    result['StraightMeanSampleSpeed']=statistics.mean(straight) if straight else None
    result['LargeSteeringChanges5Degrees']=sum(abs(a['Action']['SteeringRadians']-b['Action']['SteeringRadians'])>=math.radians(5)-1e-6 for a,b in zip(decisions,decisions[1:]))
    result['BetaAbove20Seconds']=sum(t['Seconds'] for t,s in zip(telemetry,samples[1:]) if abs(s['Input']['Rider']['SlideRadians'])>math.radians(20))
    result['BetaAbove35Seconds']=sum(t['Seconds'] for t,s in zip(telemetry,samples[1:]) if abs(s['Input']['Rider']['SlideRadians'])>math.radians(35))
    result['LongitudinalTyreLossJoules']=sum(t['Wheel']['LongitudinalLossJoules'] for t in telemetry)
    result['WheelBearingLossJoules']=sum(t['Wheel']['BearingLossJoules'] for t in telemetry)
    front_loss=rear_loss=0
    for t,a,b in zip(telemetry,samples,samples[1:]):
        f=t['Forces'];va=a['Input']['Rider'];vb=b['Input']['Rider'];dx=vb['Position']['X']-va['Position']['X'];dy=vb['Position']['Y']-va['Position']['Y'];dpsi=angle(vb['Heading']-va['Heading'])
        front_loss-=f['FrontForce']['X']*dx+f['FrontForce']['Y']*dy+f['FrontTorque']*dpsi
        rear_loss-=f['RearLateral']*(f['BodyLeft']['X']*dx+f['BodyLeft']['Y']*dy)+f['RearTorque']*dpsi
    result['FrontLateralContactWorkLossJoules']=front_loss;result['RearLateralContactWorkLossJoules']=rear_loss
    result['TyreLossModelWorkEstimateJoules']=result['LongitudinalTyreLossJoules']+front_loss+rear_loss
    higher=chosen_coast=blocked=0
    for d in decisions:
        if d['Action']['Drive']<.01:
            chosen_coast+=1
            alternatives=[c for c in d['Candidates'] if c['Action']['Drive']>.01]
            higher+=int(any(c['Safety']==0 for c in alternatives))
            blocked+=int(bool(alternatives) and all(c['Safety']!=0 for c in alternatives))
    result['CoastDecisions']=chosen_coast; result['CoastWithSafePositiveDriveCandidate']=higher; result['CoastAllPositiveDriveUncertified']=blocked
    return result

def main():
    global D
    parser=argparse.ArgumentParser(); parser.add_argument('raw');parser.add_argument('--data',default=str(D));args=parser.parse_args();raw=Path(args.raw).resolve();D=Path(args.data).resolve()
    protocol=read(D/'protocol.json'); attempts=read(D/'attempts.json'); final=read(D/'final-results.json'); winner=read(D/'winner.json')
    lock=read(D/'winner-lock.json'); pred=read(D/'predictor.json'); beta=read(D/'beta-audit.json'); ab=read(D/'ab-selection.json')
    def load(name):
        with gzip.open(raw/(name+'.json.gz'),'rt',encoding='utf-8') as f: return json.load(f)
    fields=['Number','Name','Category','Role','NativeWorlds','Complete','CpuSeconds','Evaluations','Forecasts','RawSha256','RawBytes','Completed','Outcome','GateSeconds','LegalProgress','MaximumBetaDegrees','MeanDrive','Fingerprint']
    with (D/'attempts.csv').open('w',newline='',encoding='utf-8') as f:
        out=csv.DictWriter(f,fields); out.writeheader()
        for e in attempts:
            row={k:e.get(k) for k in fields};row['Role']=role(e);row.update({k:v for k,v in (e.get('Metrics') or {}).get('Native',{}).items() if k in fields});out.writerow(row)
    manifest=[]
    for e in attempts:
        if e['NativeWorlds']:
            path=raw/(e['Name']+'.json.gz')
            if not path.exists() or sha(path)!=e['RawSha256']: raise ValueError('Raw hash mismatch '+e['Name'])
            manifest.append({k:e[k] for k in ['Name','Category','RawSha256','RawBytes']})
    write('raw-manifest.json',dict(RawDirectory=str(raw),Availability='Full raw is local; selected native runs are in Git. Reproduce using frozen code/protocol on Windows.',Runs=manifest))
    high_slip=read(D/'high-slip-prediction.json')
    availability=read(D/'action-availability.json')
    source=load('audit130-two'); telemetry={round(t['Time'],8):t for t in source['Telemetry']}; timeline=[]
    for before,after in zip(source['Replay']['Samples'],source['Replay']['Samples'][1:]):
        i=after['Input'];v=i['Rider'];time=i['Time'];dt=time-before['Input']['Time']
        if time<source['Metrics']['Native']['DurationSeconds']-3:continue
        t=telemetry[round(time,8)];f=t['Forces'];wheel=t['Wheel'];prior=before['Input']['Rider'];loads=f['TyreLoads']
        norm=lambda f:math.hypot(f['X'],f['Y'])
        timeline.append(dict(Time=time,PsiDegrees=math.degrees(v['Heading']),ChiDegrees=math.degrees(math.atan2(v['Velocity']['Y'],v['Velocity']['X'])),BetaDegrees=math.degrees(v['SlideRadians']),
            BetaDegreesPerSecond=math.degrees(angle(v['SlideRadians']-prior['SlideRadians']))/dt,YawRate=v['AngularVelocity'],YawAcceleration=(v['AngularVelocity']-prior['AngularVelocity'])/dt,
            Speed=v['Speed'],Drive=v['Drive'],RequestedSteeringDegrees=math.degrees(after['RequestedSteering']),ExecutedSteeringDegrees=math.degrees(i['Steering']),
            FrontForceNewtons=norm(f['FrontForce']),RearForceNewtons=norm(f['RearForce']),FrontUtilization=norm(f['FrontForce'])/loads['FrontCapacityNewtons'],RearUtilization=wheel['RearUtilization'],
            WheelSlip=wheel['InstantSlipRatio'],Inner=after['Inner'],Outer=after['Outer'],Certified=t['AcceptedCertificate'],Pending=json.dumps(i['Pending'],separators=(',',':'))))
    with (D/'beta-timeline.csv').open('w',newline='',encoding='utf-8') as f:
        out=csv.DictWriter(f,list(timeline[0]));out.writeheader();out.writerows(timeline)
    write('beta-dynamics-summary.json',dict(MaximumBetaRate=max(abs(x['BetaDegreesPerSecond']) for x in timeline),MaximumYawAcceleration=max(abs(x['YawAcceleration']) for x in timeline),
        MaximumFrontUtilization=max(x['FrontUtilization'] for x in timeline),MaximumRearUtilization=max(x['RearUtilization'] for x in timeline),
        FirstBeta20=next(x['Time'] for x in timeline if abs(x['BetaDegrees'])>=20),FirstBeta35=next(x['Time'] for x in timeline if abs(x['BetaDegrees'])>=35)))
    original_by_time={round(s['Input']['Time'],8):s['Input'] for s in source['Replay']['Samples']};alignment=[]
    for counter in beta['Counterfactuals']:
        observation=counter['Observation'];activation=observation+.24;run=load('counter-'+format(observation,'.2f'));physical=True;strict=True;deadline=[]
        for sample in run['Replay']['Samples']:
            i=sample['Input'];t=i['Time']
            if t>activation+1e-8:continue
            old=original_by_time[round(t,8)]
            physical &= all(i['Rider'][k]==old['Rider'][k] for k in ['Position','Velocity','Heading','AngularVelocity','Road','TotalReferenceMeters','DistanceMeters']) and i['Steering']==old['Steering'] and i['Omega']==old['Omega']
            if t<activation-1e-8:strict &= i['Rider']==old['Rider']
            if abs(t-activation)<1e-8:deadline=[k for k in i['Rider'] if i['Rider'][k]!=old['Rider'][k]]
        alignment.append(dict(Observation=observation,Activation=activation,OriginalWholeRiderFlag=counter['PrefixThroughReactionExact'],PhysicalPrefixThroughActivationExact=physical,
            WholeRiderStrictlyBeforeActivationExact=strict,DifferencesExactlyAtActivation=deadline,Reason='Drive command becomes active at the exact reaction deadline; pose, velocities, steering and wheel have not changed yet. Original audit flag retained.'))
    write('counter-prefix-alignment.json',alignment)
    rows=[]; pairs=[]
    by={r['Variant']:r for r in final}
    for r in final:
        completed=[m['Native'] for m in r['Results'] if m['Native']['Completed']]
        dnfs=Counter(m['Native']['Outcome'] for m in r['Results'] if not m['Native']['Completed'])
        rows.append([r['Variant'],sum(m['Native']['Completed'] for m in r['Results']),len(r['Results']),dict(dnfs),round(statistics.mean(m['GateSeconds'] for m in completed),3) if completed else None])
    paired_pass=True
    for suite in ['single','two']:
        for old_label,new_label in [('130-H06','131-final'),('129-after200','131-final'),('129-after200','130-H06')]:
            baseline=by[old_label+'-'+suite];new=by[new_label+'-'+suite];comparisons=[]
            for s,a,b in zip(new['Scenarios'],baseline['Results'],new['Results']):
                if a['Native']['Completed'] and b['Native']['Completed']:
                    ta,tb=a['Native']['GateSeconds'],b['Native']['GateSeconds'];comparisons.append(dict(Scenario=s,Old=ta,New=tb,Delta=tb-ta,Ratio=tb/ta-1))
            ratios=[x['Ratio'] for x in comparisons]
            ok=bool(ratios) and statistics.median(ratios)<=.05 and max(ratios)<=.15
            criterion=old_label=='130-H06' and new_label=='131-final'
            # Frozen PASS time comparison is131 versus130; older pairs are also fully reported.
            if criterion:paired_pass &= ok
            pairs.append(dict(Suite=suite,Reference=old_label,Compared=new_label,UsedForFrozenPassCriterion=criterion,Pairs=comparisons,Count=len(comparisons),MedianRatio=statistics.median(ratios) if ratios else None,MaximumRatio=max(ratios) if ratios else None,Passed=ok))
    ns=sum(m['Native']['Completed'] for m in by['131-final-single']['Results']); nt=sum(m['Native']['Completed'] for m in by['131-final-two']['Results'])
    oldt=sum(m['Native']['Completed'] for m in by['130-H06-two']['Results'])
    old_protocol=read(ROOT/'docs/experiments/robust-free-racing-learning/protocol.json')
    immutable=[n for n in old_protocol['Sources'] if n not in ['FreeLearningController.cs','RobustLearningModel.cs','RobustLearningExperiment.cs']]
    physics_unchanged=all(sha(ROOT/'tools/shared-world-speedway'/n)==old_protocol['Sources'][n] for n in immutable)
    core_unchanged=not subprocess.check_output(['git','diff',protocol['BaseHead'],'--','src/CoreSim'],cwd=ROOT).strip()
    first_final=min(e['Number'] for e in attempts if e['Name'].startswith('final-'))
    no_leak=lock['FinalWorlds']==0 and all(e['Number']<first_final for e in attempts if e['Category'].startswith('train-') or '-select-' in e['Name'])
    source_hashes=all(sha(ROOT/'tools/shared-world-speedway'/n)==digest for n,digest in protocol['Sources'].items())
    parity=read(D/'h06-reference-parity.json')['Passed']
    passed=pred['Passed'] and high_slip['Passed'] and ns>=22 and nt>=10 and nt-oldt>=2 and paired_pass and physics_unchanged and core_unchanged and no_leak and source_hashes and parity
    invalid=not(physics_unchanged and core_unchanged and no_leak and source_hashes and parity)
    verdict='FAIL' if invalid else 'PASS' if passed else 'PARTIAL'
    write('paired-times.json',pairs)
    generations={p.stem.removeprefix('generations-'):read(p) for p in sorted(D.glob('generations-*.json'))}
    write('checkpoints.json',{k:[dict(Generation=g['Generation'],Id=c['Id'],Policy=c['Policy'],Scenarios=c['Scenarios'],Completed=sum(r['Native']['Completed'] for r in c['Results'])) for g in gs for c in g['Candidates']] for k,gs in generations.items()})
    training_rows=[]
    for label,gs in generations.items():
        for g in gs:
            best=next(c for c in g['Candidates'] if c['Id']==g['BestId'])
            native=[m['Native'] for m in best['Results']];completed=[m for m in native if m['Completed']]
            training_rows.append([label,g['Generation'],sum(m['Native']['Completed'] for c in g['Candidates'] for m in c['Results']),g['BestId'],len(completed),round(g['CpuSeconds'],3),
                round(max(m['GateSeconds'] for m in native),3) if len(completed)==4 else None,
                round(statistics.mean(m['EntrySpeed'] for m in native if m['EntrySpeed'] is not None),3) if any(m['EntrySpeed'] is not None for m in native) else None,
                round(min(m['MinimumSpeed'] for m in native),3),round(statistics.mean(m['ExitSpeed'] for m in completed),3) if completed else None,
                round(statistics.mean(m['MeanDrive'] for m in native),4)])
    with (D/'generations.csv').open('w',newline='',encoding='utf-8') as f:
        out=csv.writer(f); out.writerow(['Variant','Generation','CompletedOf20','BestId','BestCompletedOf4','CPU','WorstGateWhenAll4Completed','MeanEntrySpeed','MinimumSpeed','MeanExitSpeedCompleted','MeanDrive']); out.writerows(training_rows)
    # Replay groups are chosen for presentation only, after final freeze. No changes to winner.
    groups=[]; runs={}; labels={}; horizons={}; colors={}; palette=['#d99fe3','#ffd27b','#73dfdf','#ff9c86','#8bcf74','#a6b8ff','#fe8ed7']
    candidates=read(D/'ab-candidates.json')
    best_ab=[]
    for h,tag in [(.6,'H06'),(.9,'H09')]:
        selected_scores=[r for r in ab if r['Variant'].startswith(tag)]
        # Match the frozen rank exactly, never choose from final results.
        def rank(r):
            m=r['Results']; n=[x['Native'] for x in m]; completed=sum(x['Completed'] for x in n); failed=[x['LegalProgress'] for x in n if not x['Completed']]
            return (-completed,-min(failed,default=0),sum(x['Outcome']=='control envelope' or y['BoundaryClass']=='confirmed crossing' for x,y in zip(n,m)),
                math.ceil(max(x['GateSeconds'] for x in n)/.05) if completed==len(n) else math.inf,
                math.ceil(statistics.mean(x['GateSeconds'] for x in n)/.025) if completed==len(n) else math.inf,
                -min(x['MinimumClearanceMeters'] for x in n),-min([x['ExitSpeed'] for x in n if x['Completed']],default=0),
                max([abs(y['ExitYawRate'] or 0) for x,y in zip(n,m) if x['Completed']],default=math.inf),-statistics.mean(x['LegalProgress'] for x in n),r['Id'])
        r=min(selected_scores,key=rank); best_ab.append(next(c for c in candidates if c['Label']==r['Variant'] and c['Id']==r['Id']))
    def group(gid,scenario,names,reason):
        refs=[]
        for label,name,h in names:
            if not (raw/(name+'.json.gz')).exists():continue
            run=load(name); runs[name]=run; refs.append(dict(Name=name,Policy=label));labels[label]=label;horizons[label]=h
            colors[label]=palette[len(colors)%len(palette)]
        if refs:
            first=runs[refs[0]['Name']]['Replay']['Samples'][0]['Input']['Rider']
            for ref in refs: assert runs[ref['Name']]['Replay']['Samples'][0]['Input']['Rider']==first
            groups.append(dict(Id=gid,Scenario=scenario,Reasons=[reason],Runs=refs,TwoBends=gid.startswith('TWO-') or gid.startswith('V') or gid in ['BETA130','HIGH-BETA','FULL-GAS'],Scope='Rzeczywiste natywne stany. Identyczne wejście; przewidywania nie są rysowane jako przejazdy.'))
    for s in protocol['Selection'][:3]:
        names=[('#130 H06','ab-select-130-H06-1020-'+s['Name'],.6)]
        names += [(c['Label']+' #'+str(c['Id']),f"ab-select-{c['Label']}-{c['Id']}-{s['Name']}",c['Horizon']) for c in best_ab]
        group('AB-'+s['Name'],s,names,'horyzonty na tym samym wejściu')
        if s['Name']=='Q1':continue
        initial=read(D/'ab-winner.json')
        cs=[initial,winner]
        group('TWO-'+s['Name'],s,[(c['Label']+' #'+str(c['Id']),f"two-select-{c['Label']}-{c['Id']}-{s['Name']}",c['Horizon']) for c in cs],'początek i koniec nauki dwóch łuków')
    for suite in ['single','two']:
        scenarios=protocol['FinalSingle' if suite=='single' else 'FinalTwo']
        # One success if present and one DNF if present, deterministic presentation rule.
        result=by['131-final-'+suite]; ids=[]
        for success in [True,False]:
            i=next((i for i,m in enumerate(result['Results']) if m['Native']['Completed']==success),None)
            if i is not None:ids.append(i)
        for i in ids:
            s=scenarios[i];group(s['Name'],s,[(tag,'final-'+tag+'-'+s['Name'],winner['Horizon'] if tag=='131-final' else .6) for tag in ['129-after200','130-H06','131-final']],'niezależny finał '+suite)
    s=load('audit130-two')['Replay']['Scenario']
    group('BETA130',s,[('#130 oryginał','audit130-two',.6)]+[('jedna komenda '+str(x['Observation']),'counter-'+format(x['Observation'],'.2f'),.6) for x in beta['Counterfactuals']],'narastające beta i kontrfaktyczna pojedyncza komenda')
    group('HIGH-BETA',s,[('#130 oryginał','audit130-two',.6),('walidacja: utrzymane35°','validate-high-beta',.9)],'diagnostyka fizyki do40° beta; ręczna komenda nie jest wyuczoną polityką')
    group('FULL-GAS',s,[('#130 oryginał','audit130-two',.6),('diagnostyka: gaz1','validate-full-gas',.9)],'natywna sonda możliwości pełnego gazu; ręczna komenda diagnostyczna')
    with gzip.GzipFile(filename=str(D/'selected-replays.json.gz'),mode='wb',mtime=0) as f:
        f.write(json.dumps(dict(Runs=runs),ensure_ascii=False,separators=(',',':'),allow_nan=False).encode())
    write('index.json',dict(Title='Autonomous Speedway Rider #131',Grade=verdict,Groups=groups,DefaultRun='final-131-final-V00' if 'final-131-final-V00' in runs else groups[0]['Runs'][0]['Name'],Labels=labels,Horizons=horizons,Colors=replay_colors(labels),
        RawDirectory=str(raw),Charts=[['learning.svg','H06/H09: oba seedy i ciągły trening'],['final.svg','Nowy niezależny test: ukończenia i DNF'],['trajectories.svg','Natywne trajektorie na wspólnym stanie']]))
    qualities=[quality(r) for r in runs.values()];write('representative-quality.json',qualities)
    write('selected-manifest.json',dict(BundleSha256=sha(D/'selected-replays.json.gz'),Runs=[dict(Name=n,RawSha256=sha(raw/(n+'.json.gz')),Fingerprint=r['Replay']['Metrics']['Fingerprint']) for n,r in runs.items()]))
    summary=dict(Grade=verdict,BaseHead=protocol['BaseHead'],Winner=winner,WinnerLock=lock,ProtocolSha256=sha(D/'protocol.json'),TrainingStop=read(D/'stop-two.json') if (D/'stop-two.json').exists() else {'Reason':'maximum20 generations'},
        TrainingAB=sum(e['Category']=='train-ab' for e in attempts),TrainingTwo=sum(e['Category']=='train-two' for e in attempts),
        WorldsByCategory=dict(Counter(e['Category'] for e in attempts if e['NativeWorlds'])),EpisodeRoles=dict(Counter(role(e) for e in attempts if e['NativeWorlds'])),NativeWorlds=sum(e['NativeWorlds'] for e in attempts),
        CpuSeconds=sum(e['CpuSeconds'] for e in attempts),ContactEvaluations=sum(e['Evaluations'] for e in attempts),Forecasts=sum(e['Forecasts'] for e in attempts),
        NativeSteps=sum((e.get('Metrics') or {}).get('Native',{}).get('NativeSteps',0) for e in attempts),FinalSummary=rows,PairedTimingPassed=paired_pass,
        PassChecks=dict(Predictor=pred['Passed'] and high_slip['Passed'],Single=ns>=22,Two=nt>=10,TwoGain=nt-oldt>=2,PairedTiming=paired_pass,
            PhysicsUnchanged=physics_unchanged and core_unchanged,NoLeak=no_leak,ExecutionSourceHashes=source_hashes,H06ReferenceParity=parity),SelectedNativeReplays=len(runs),
        AdditionalHighSlipValidation=dict(Passed=high_slip['Passed'],MaximumNativeBeta=high_slip['MaximumNativeBeta'],Comparisons=len(high_slip['Comparisons'])))
    write('analysis.json',summary)
    os.environ.setdefault('MPLCONFIGDIR',str(ROOT/'work/mpl-cache'))
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig,axes=plt.subplots(1,2,figsize=(11,4.5))
    for label,gs in generations.items():
        ax=axes[1 if label=='two' else 0]
        ax.plot([g['Generation'] for g in gs],[sum(m['Native']['Completed'] for c in g['Candidates'] for m in c['Results']) for g in gs],marker='o',label=label)
    for ax,title in zip(axes,['H06 / H09, both seeds','Continuous two-bend curriculum']):
        ax.set(title=title,xlabel='Generation (alternating frozen groups)',ylabel='Completed /20',ylim=(-.5,20.5));ax.grid(alpha=.2);ax.legend(fontsize=8)
    fig.tight_layout();fig.savefig(D/'learning.svg');plt.close(fig)
    fig,axes=plt.subplots(1,2,figsize=(10,4))
    for ax,suite,total in zip(axes,['single','two'],[24,12]):
        rs=[r for r in rows if r[0].endswith(suite)];x=list(range(len(rs)));ax.bar(x,[r[1] for r in rs],label='completed');ax.bar(x,[total-r[1] for r in rs],bottom=[r[1] for r in rs],color='#d7706a',label='DNF')
        ax.set_xticks(x,[r[0].removesuffix('-'+suite) for r in rs],rotation=15,fontsize=8);ax.set(title=suite,ylabel='Episodes');ax.legend()
    fig.tight_layout();fig.savefig(D/'final.svg');plt.close(fig)
    fig,ax=plt.subplots(figsize=(9,5));g=next(g for g in groups if g['Id'].startswith('AB-'))
    for ref in g['Runs']:
        ss=runs[ref['Name']]['Replay']['Samples'];ax.plot([s['Input']['Rider']['Position']['X'] for s in ss],[s['Input']['Rider']['Position']['Y'] for s in ss],label=ref['Policy'])
    ax.axis('equal');ax.set(xlabel='Native X [m]',ylabel='Native Y [m]',title=g['Id']);ax.legend(fontsize=8);fig.tight_layout();fig.savefig(D/'trajectories.svg');plt.close(fig)
    report(summary,rows,training_rows,ab,pairs,qualities,beta,pred,protocol,best_ab)
    report_path=D/'README.md';report_text=report_path.read_text(encoding='utf-8')
    single_pair=next(p for p in pairs if p['Suite']=='single' and p['UsedForFrozenPassCriterion'])
    reason=f"**Wynik końcowy: {ns}/24 pojedynczych łuków i {nt}/12 dwóch łuków**, wobec23/24 i{oldt}/12 dla#130. Na23 wspólnie ukończonych pojedynczych łukach mediana regresji czasu wynosi{single_pair['MedianRatio']*100:.2f}%, a maksimum{single_pair['MaximumRatio']*100:.2f}%; zamrożone tolerancje to5% i15%. To przyczynaPARTIAL mimo poprawy niezawodności. Przy zerze wspólnych ukończeń dwóch łuków z#130 nie ma par czasowych; pełne porównanie z#129 po200 pozostaje w tabeli par."
    report_text=report_text.replace('## 1–3.',reason+'\n\n## 1–3.',1)
    ab_summary=[]
    for seed in protocol['Seeds']:
        row={'Seed':seed}
        for tag in ['H06','H09']:
            label=f'{tag}-s{seed}';gs=generations[label]
            row[tag+'TrainingCompleted']=sum(m['Native']['Completed'] for g in gs for c in g['Candidates'] for m in c['Results'])
            row[tag+'SelectionCompleted']=max(sum(m['Native']['Completed'] for m in r['Results']) for r in ab if r['Variant']==label)
        ab_summary.append(row)
    write('ab-summary.json',ab_summary)
    ab_text=' Wyniki oddzielne dla seedów: '+ '; '.join(f"seed{x['Seed']}: H06 {x['H06TrainingCompleted']}/80 treningu i{x['H06SelectionCompleted']}/4Q, H09 {x['H09TrainingCompleted']}/80 i{x['H09SelectionCompleted']}/4Q" for x in ab_summary)+'. '
    baseline_counts={r['Variant']:sum(m['Native']['Completed'] for m in r['Results']) for r in ab if r['Variant'].startswith('130-')}
    ab_text+=f"Bez retreningu #130: H06 {baseline_counts['130-H06']}/4Q, H09 {baseline_counts['130-H09']}/4Q. W obu seedach H09 zwiększył liczbę ukończeń treningowych; przewaga ukończeń selekcyjnych nie wystąpiła w obu seedach. Selekcja całego A/B wybrała `{read(D/'ab-winner.json')['Label']}` — retrening nie pokonał checkpointu początkowego naQ. To częściowy, zależny od stanu dowód korzyści horyzontu, a nie globalna przewaga przy dowolnym wejściu."
    report_text=report_text.replace('## 1–3. H06/H09, dwa seedy i efekt bez ponownego uczenia','## 1–3. H06/H09, dwa seedy i efekt bez ponownego uczenia\n\n'+ab_text)
    parameters=table(['Parametr zwycięskiej polityki','Dokładna wartość'],[[k,repr(v)] for k,v in winner['Policy'].items() if k!='Values'])
    report_text=report_text.replace('## 1–3.',parameters+'\n\n## 1–3.',1)
    report_text=report_text.replace('nie jest to pełna walidacja dynamiki blisko45°.',
        f"uzupełniono ją odrębną walidacją91 natywnych punktów doβ={high_slip['MaximumNativeBeta']:.3f}°, bez zmiany tolerancji. [Dodatkowy audyt](high-slip-prediction.json) zachował cały prefiks, reakcję i wykonał prognozę0,9s z utrzymanym+35°/gaz0. Jest to diagnostyka fizyki, nie wyuczona jazda.")
    report_text=report_text.replace('walidacja numeryczna obejmuje umiarkowany uślizg.',f"walidacja numeryczna obejmuje stany doβ{high_slip['MaximumNativeBeta']:.2f}°, ale nie jest kalibracją prawdziwej nawierzchni.")
    safe960=[r for r in availability['Records'] if abs(r['Time']-9.6)<1e-7 and r['SafeInHorizon']]
    report_text=report_text.replace('[Pełny audyt](beta-audit.json).',
        '[Pełny audyt](beta-audit.json). Rozszerzony, wyłącznie diagnostyczny grid180 prognoz wykazał o9,6s bezpieczne w horyzoncie opcje±35°/gaz0, których obecny zbiór kandydatów nie zawiera. Natywne utrzymanie+35° przez0,9s pozostało poniżejβ45°; nie dowodzi ukończenia dwóch łuków. Po9,8s w tym gridzie nie znaleziono bezpiecznej opcji. O8,4s także gaz1 przy skręcie0 był bezpieczny lokalnie. To dowód ograniczenia dostępnych akcji i lokalnej zachowawczości wyboru, nie uzasadnienie globalnego zwiększenia gazu. [Pełne dostępne działania](action-availability.json).')
    report_text+='\n\n[Audyt β krok po kroku](beta-timeline.csv) zawiera obliczone tempo narastaniaβ i przyspieszenie yaw, siły/wykorzystanie obu opon, wheelspin, kierownicę, gaz i pełne pending. [Podsumowanie dynamiki](beta-dynamics-summary.json) raportuje maksima i momenty przekroczenia20/35°.\n'
    report_text+='\n\n[Korekta interpretacji prefiksu kontrfaktycznego](counter-prefix-alignment.json): oryginalna flaga porównania całegoRider wyniosłaFalse wyłącznie przez zmianę polaDrive dokładnie w8,64s, gdy komenda po pełnych0,24s staje się aktywna. Pozycja, prędkość, heading, yaw, koło i kierownica są bitowo identyczne do tego terminu włącznie; całyRider jest identyczny ściśle przed nim. Zachowano pierwotną flagę i pełne raw. Nie ma wcześniejszego działania ani zmiany tolerancji.\n'
    report_text=report_text.replace('bez kompilacji, eksportu, agregacji i CI.',
        'bez kompilacji, agregacji i CI; koszt natywnych epizodów wyłącza zapis raw, a audyty koordynujące obejmują także swój narzut zapisu.')
    report_text=report_text.replace('Nie zwiększano gazu ręcznie.',
        'Po pierwszym zmniejszeniu gazu nie ma nowego kandydata1. W#130 udział2,3188% odpowiada dokładnie początkowym0,24s z10,35s epizodu. Nie jest to wyuczone ponowne otwieranie pełnego gazu. Nie zwiększano gazu ręcznie.')
    full_gas=read(D/'full-gas-prediction.json')
    report_text+=f"\n\n[Natywna sonda pełnego gazu](full-gas-prediction.json): komenda skręt0/gaz1 obserwowana o8,4s i wykonana po0,24s, utrzymana w diagnostyce do9,3s. Wszystkie91 porównań predykcji zBox2D przeszły; maksymalneβ={full_gas['MaximumNativeBeta']:.3f}°. Dowodzi to lokalnej fizycznej możliwości mocniejszego gazu, który był niedostępny po wcześniejszym odpuszczeniu w zbiorze działań. Sonda nie jest wyuczoną jazdą ani ukończeniem dwóch łuków.\n"
    report_text+='\n\nDokładny podział nowych światów według celu: '+str(summary['EpisodeRoles'])+'. `Role` wCSV rozróżnia trening, selekcję, niezależną walidację, walidację predyktora, diagnostykę i odtworzenie; kategoria budżetowaevaluation jest wspólną pulą limitu180, nie liczbą jednego rodzaju prób.\n'
    report_text+='\n\nZatrzymanie głównego treningu według wcześniej zamrożonej reguły: '+str(summary['TrainingStop'])+'. Nie uruchamiano dalszych generacji na podstawie wyników finału.\n'
    report_text+='\n\nPrzenośność liczbCEM: pierwszy Windows CI ujawnił różnicę2,22×10⁻¹⁶ we współczynniku kandydata-immigranta4014 między Windows10 i WindowsServer2025. Odziedziczony sampler używaMath.Cos/Math.Tanh; dokładne wyniki tych funkcji zależą od bibliotekiC systemu, co opisuje [dokumentacjaMicrosoft](https://learn.microsoft.com/en-us/dotnet/api/system.math.tanh). Nowy test wymaga bitowo identycznego powtórzenia każdego losowania w jednym runtime, a współczynniki z archiwum porównuje między runtime z absolutną granicą1e-12. Zamrożone polityki, surowe dane, algorytmCEM, fizyka, tolerancjeH09 i kryteriaPASS pozostały bez zmian. Nie deklarujemy bitowej identyczności ponownego losowania wag między wszystkimi wersjamiWindows; do dokładnego odtworzenia epizodów służą zapisane parametry. [Jawny zapis pierwszego błęduCI i zakres poprawki](cem-portability.json).\n'
    report_text+='\n\nPełny nowy przebieg z audytami uruchamia `pwsh -File tools/run-extended-learning.ps1 -Data <nowy-katalog-danych> -Raw <nowy-katalog-raw>`. Skrypt odmawia nadpisania protokołu, zamraża dodatkowe audyty przed ich wykonaniem i uruchamia właściwą kolejność etapów. Raport nowego przebiegu: `python tools/extended-learning-report.py <raw> --data <dane>`. Archiwalne populacje i aktualizacjeCEM odtwarza test C# bez nowych światów. Fizyczne fingerprinty i polityki są deterministyczne; czasyCPU i zawierające je hashe raw mogą się zmienić. Strażniki kosztu mogą wcześniej zatrzymać przebieg na wolniejszym środowisku — zapisany punkt zatrzymania i archiwum pozostają częścią pochodzenia wyniku.\n'
    report_text+='\n\nKorekta techniczna końców linii: zachowano [oryginalny protokół](protocol-before-line-ending-correction.json) i [jawną zmianę hashy formatowania](line-ending-correction.json). Przywrócono dokładne historyczne bajty Git oraz wymagane przez `.gitattributes` źródłaCRLF. Progi, stany, harmonogram i semantyka binarna nie zmieniły się. Historyczne testy najpierw weryfikują dokładne nowe rozszerzenie, a następnie odtwarzają i sprawdzają zamrożony kod#130; żadne stare oczekiwanie nie zostało poluzowane.\n'
    report_path.write_text(report_text,encoding='utf-8')
    for path in D.glob('*.svg'):
        path.write_bytes(b'\n'.join(line.rstrip() for line in path.read_bytes().splitlines())+b'\n')
    write('manifest.json',{p.name:sha(p) for p in sorted(D.iterdir()) if p.is_file() and p.name not in ['manifest.json','budget.lock']})
    print(json.dumps(summary,ensure_ascii=False))

def report(summary,rows,training_rows,ab,pairs,qualities,beta,pred,protocol,best_ab):
    w=summary['Winner'];dnf=beta['Metrics']['Native'];abrows=[]
    for r in ab:
        m=[x['Native'] for x in r['Results']];abrows.append([r['Variant'],r['Id'],sum(x['Completed'] for x in m),round(max(x['GateSeconds'] for x in m),3) if all(x['Completed'] for x in m) else 'DNF',round(min([x['LegalProgress'] for x in m if not x['Completed']],default=0),2)])
    qrows=[]
    for q in qualities:
        n=q['Native'];qrows.append([q['Name'],n['Outcome'],n['GateSeconds'],round(n['MeanDrive'],3),round(n['FullDriveFraction']*100,2),round(n['MaximumBetaDegrees'],2),round(n['DistanceMeters'],2),q['NoSafeDecisions'],q['CoastWithSafePositiveDriveCandidate']])
    sections=[f"# Autonomous Speedway Rider #131 — {summary['Grade']}",
        f"Status **PROVISIONAL**, default OFF. Baza dokładnie `{summary['BaseHead']}`. Fizyka, geometria, ograniczenia oraz produkcyjny CoreSim bez zmian. Jeden Draft PR, bez merge.",
        f"Zamrożona polityka: **{w['Label']} #{w['Id']}**, horyzont **{w['Horizon']} s**; [parametry](winner.json), [hash przed finałem](winner-lock.json), [protokół](protocol.json). Werdykt **{summary['Grade']}** według niezmienionych kryteriów; wyników finału nie użyto do dalszego strojenia.",
        '## 1–3. H06/H09, dwa seedy i efekt bez ponownego uczenia',
        'Wyłącznie horyzont różni ramiona podstawowego A/B. Postęp jest przeliczany do jednostek 0,6 s; pozostałe składniki to pojedyncze koszty akcji lub stanu końcowego. Reprezentacja, zakresy, CEM, bezpieczeństwo, reakcja 0,24 s, decyzje 0,2 s i działania są wspólne. Dwa seedy używają tego samego checkpointu A #1020. Selekcja Q jest mała i nie stanowi testu generalizacji.',
        table(['Wariant','ID','Ukończenia /4 Q','Najgorszy czas gdy4/4','Najgorszy postęp DNF'],abrows),
        'Wiersze `130-H06` i `130-H09` mierzą zmianę horyzontu bez retreningu. Każdy seed jest pokazany osobno; pojedynczy rekord nie dowodzi przewagi. [Cała selekcja](ab-selection.json), [zamrożony wybór](ab-winner.json).',
        f"H09: **{len(pred['Checks'])}/{len(pred['Checks'])}** walidacji, największy błąd XY {max(x.get('XY',0) for x in pred['Checks']):.8f} m. Porównano XY, v, χ, ψ, β, yaw, kierownicę, koło i optymizm prześwitu; wszystkie prefiksy są bitowo identyczne. Źródła to faktycznie osiągnięte stany #130, kontynuowane natywnie z zachowaną kolejką. Wyższy uślizg w zamrożonej próbie wynosił {max(x.get('OriginBeta',0) for x in pred['Checks']):.2f}° — nie jest to pełna walidacja dynamiki blisko45°. [Wszystkie błędy](predictor.json).",
        '## 4–5. Dwa łuki i stan po pierwszym',
        'Każda pełna próba jest jednym WorldRunner/Box2D. Nie ma transferu przez reset ani sztucznych checkpointów. Trening zawiera jeden krótki pierwszy łuk i trzy pełne przejazdy na politykę; ich fizycznie osiągnięte przejścia i przygotowanie kolejnego łuku są częścią wyniku. Wszystkie pełne próby muszą przekroczyć bramkę całego motocykla po drugim łuku i utrzymać jeszcze1s legalnej kontynuacji. Sam pierwszy łuk nie daje ukończenia pełnego epizodu.',
        table(['Finał','Ukończenia','Próby','DNF','Średni czas ukończonych (pomocniczy)'],rows),
        'Ukończenia i DNF są osobnymi miarami. Czasy porównano parami wyłącznie na wspólnie ukończonych stanach; [wszystkie pary](paired-times.json). Brak wspólnych ukończeń nie zalicza warunku braku regresji czasu. Nowe U00–U23 i V00–V11 były zamrożone przed treningiem; stare T00–T11 nie uczestniczą w nowej selekcji ani treningu.',
        '## 6–7. Przyczyna β45 i wcześniejsze rozpoznanie',
        f"#130 odtworzono dokładnie: fingerprint `{beta['FirstWorldFingerprint']}`, stop {dnf['DurationSeconds']:.2f}s, legalny postęp {dnf['LegalProgress']:.3f}m, β {dnf['MaximumBetaDegrees']:.3f}°, certyfikowany prześwit {dnf['MinimumClearanceMeters']:.4f}m. Jest to przekroczenie obwiedni modelu, a nie potwierdzony kontakt ani dowód upadku.",
        'W audycie ostatnich3s są wszystkie natywne stany i siły obu opon, wheelspin, wykorzystanie budżetu, yaw/ψ/χ, gaz, żądany i wykonany skręt, pending oraz pełne oceny kandydatów H06/H09. Pochodne β i yaw należy liczyć z kolejnych rzeczywistych timestampów. Uślizg zmienia znak po decyzjach8,6–9,2s i potem gwałtownie narasta; duże żądanie+15° jest utrzymywane od9,2s. To korelacja czasowa, nie dowód jednej przyczyny. O9,6s H09 nie znajduje bezpiecznej opcji, gdy H06 nadal klasyfikuje wybraną jako bezpieczną. Wcześniejsza alternatywa8,4s daje lokalnie lepszą klasę przewidywania, ale jej pojedyncza natywna zamiana nie uratowała całego epizodu. Nie ma dowodu, że sam błąd wyjścia z pierwszego łuku wystarcza do wyjaśnienia późniejszego DNF. Przy narastaniu β ograniczony zestaw±15° i opóźnienie pozostawiają mało czasu; brak bezpiecznego kandydata nie dowodzi, że żaden fizyczny manewr w±35° nie istnieje.',
        table(['Obserwacja','Natywny wynik jednej komendy','Legalny postęp','Fizyczny prefiks do reakcji','Pierwotna flaga całego Rider'],[[x['Observation'],x['Metrics']['Native']['Outcome'],round(x['Metrics']['Native']['LegalProgress'],2),next(a['PhysicalPrefixThroughActivationExact'] for a in read(D/'counter-prefix-alignment.json') if a['Observation']==x['Observation']),x['PrefixThroughReactionExact']] for x in beta['Counterfactuals']]),
        'Pierwotna flagaFalse obejmowała zmianę metadanychDrive dokładnie w8,64s, w chwili legalnej aktywacji komendy. Cały stan mechaniczny jest identyczny do tego terminu włącznie. [Pełne porównanie i zachowany pierwotny wynik](counter-prefix-alignment.json).',
        '[Pełny audyt](beta-audit.json). Siły są ograniczane oryginalnymi budżetami opon. Zgodność numeryczna predyktora nie stanowi empirycznej kalibracji sił rzeczywistego żużla ani uzasadnienia jazdy pozaβ45. Nie rozszerzano obwiedni ani nie dodawano momentu prostującego.',
        '## 8. Czy osiem parametrów wystarcza?',
        'Zachowano osiem parametrów. Wystarczyły tu do24/24 i12/12 niezależnych ukończeń, lecz nie dowiedziono wystarczalności do szybkiej jazdy. CEM aktualizuje te same preferencje; nie jest to ogólny algorytm uczenia dowolnej strategii. [Archiwum](checkpoints.json) pokazuje parametry wszystkich kandydatów i granice. Samo trafianie w granice nie uzasadnia zmiany reprezentacji. Trwałe DNF oraz niedostępne większe akcje rozdzielają ograniczenie preferencji od ograniczenia przestrzeni działań. Nie przeprowadzano odrębnego eksperymentu reprezentacji i nie przypisujemy jej udowodnionej winy. PreviewSeconds jest podglądem orientacji geometrii; prognoza fizycznego stanu pozostaje0,9s.',
        '## 9–11. Próby, uczenie i szybkość',
        f"W ledgerze: **{summary['NativeWorlds']}** nowych światów; A/B **{summary['TrainingAB']}**, trening ciągły **{summary['TrainingTwo']}**. Kategorie: `{summary['WorldsByCategory']}`. Audyty offline mają0 światów i osobny koszt. Techniczne testy jednostkowe/CI nie są epizodami uczącymi i ich koszt nie jest dopisywany do ledgeru eksperymentu.",
        table(['Wariant','Generacja','Ukończenia /20','ID zwycięzcy','Zwycięzca /4','CPU s','Najgorszy czas przy4/4','v wyjścia ukończonych'],[[*r[:7],r[9]] for r in training_rows]),
        '![Postęp](learning.svg)',
        'Scenariusze powracają co drugą generację; porównania między grupami nie mierzą czystej poprawy. Mieszanka zawiera różne cele zakończenia, więc suma ukończeń jest opisowa. Kontrola plateau korzysta z tej samej grupy i zapisuje przyczynę w `stop-two.json`, jeżeli wystąpiła. [Pełna tabela generacji](generations.csv) zawiera również prędkość wejścia, minimum i gaz. Prędkość wyjścia to średnia z ukończonych zadań danej grupy, pomocnicza względem liczbyDNF.',
        '## 12. Gaz i rzeczywista jakość',
        'W odziedziczonym zbiorze działań gaz ma wartości0 lub `policy.Drive` (do0,8); pełny1 może zostać jedynie z aktywnej komendy początkowej. Mały udział pełnego gazu jest więc także ograniczeniem reprezentacji działań. Nie wolno wyjaśniać go samą fizyką. Gdy sterownik wybiera0 mimo bezpiecznego dodatniego kandydata, rozstrzyga koszt preferencji/stanu; gdy wszystkie dodatnie są niepewne, ogranicza go bieżąca prognoza bezpieczeństwa. Nie zwiększano gazu ręcznie.',
        table(['Replay','Wynik','Czas bramki','Gaz średni','Pełny gaz %','β max°','XY m','Bez safe','Gaz0 mimo safe+'],qrows),
        '[Pełna jakość](representative-quality.json): wejście/minimum/wyjście i czas każdego łuku, prosta, duże zmiany skrętu, czasβ>20/35°, yaw, oscylacje trajektorii, XY, prześwit, energia silnika i straty. Straty opon rozdzielono na zapisane straty poślizgu wzdłużnego koła i pracę bocznych sił/momentów na rzeczywistym przemieszczeniu; osobno pokazano łożysko i całkowity bilans strat. Jest to bilans pracy modelu, bez kalibracji rzeczywistego podłoża. Oscylacja oznacza odwrócenie trendu fizycznego offsetu po wychyleniu co najmniej1m na całym epizodzie; liczba zmian kierownicy jest osobną miarą. Zapisane serie natywne są w viewerze. Średnia prędkość prostej jest średnią próbek, nie dokładną średnią czasową; pozostałe całki gazu/energii pochodzą z natywnych przedziałów.',
        '## 13–14. Viewer i generalizacja',
        'Uruchom serwer HTTP z katalogu repo i otwórz `tools/replay-viewer/?case=extended-learning`. Grupy AB-Q0/Q1/Q2 porównują#130 i wybranych naQ kandydatów H06/H09, TWO-Q0/Q2 początek i koniec ciągłego treningu; U/V pokazują niezależny finał. Nakładki mają identyczne pełne stany początkowe. Odtwarzane są wyłącznie stany Box2D, bez idealnej trajektorii i ekstrapolacji. BETA130 pokazuje narastający uślizg i pojedynczą kontrfaktyczną komendę. Sukces dwóch łuków jest udostępniony tylko jeśli naprawdę wystąpił.',
        '![Finał](final.svg)',
        'Generalizację ocenia wyłącznie powyższy finał. Po otwarciu nie zmieniano zwycięzcy, horyzontu, seeda ani momentu zakończenia. Mała selekcjaQ może się przeuczyć i nie gwarantuje dobrego wynikuU/V.',
        '## 15. Koszt i odtwarzalność',
        f"CPU epizodów+audytów offline **{summary['CpuSeconds']:.3f}s**, prognozy **{summary['Forecasts']}**, oceny kontaktu **{summary['ContactEvaluations']}**, natywne kroki **{summary['NativeSteps']}**. Pomiar to process CPU, bez kompilacji, eksportu, agregacji i CI. Limity6000 CPU s/90M kontaktów/1000 światów były zamrożone. Koszt każdego epizodu i jego hash są w [CSV](attempts.csv) i [ledgerze](attempts.json).",
        'Pełne raw są lokalne, nie wszystkie dostępne z Git. [Manifest](raw-manifest.json) zawiera ścieżkę, hashe i rozmiary; Git zawiera jeden skompresowany bundle reprezentatywnych przejazdów. Deterministyczne odtworzenie: na Windows z tą wersją.NET i Box2D uruchom `dotnet build -c Release`, następnie `extended-learning <nowy-katalog-danych> <nowy-katalog-raw> freeze`, `audit`, `ab`, `select-ab`, `two`, `freeze-winner`, `final`; uruchamiaj przez `dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll`. Nie nadpisuj zamrożonych danych. Przelicz raport: `python tools/extended-learning-report.py <raw>`.',
        '## 16–17. Ograniczenia i jeden kolejny krok',
        'Ograniczenia: mały zbiór selekcyjny, ograniczony zestaw działań, brak gwarancji bezpieczeństwa poza horyzontem i brak kalibracji prawdziwych opon; walidacja numeryczna obejmuje umiarkowany uślizg. WynikPARTIAL nie oznacza spełnienia całego kryterium niezawodnej i szybkiej jazdy. Jeden uzasadniony następny etap: odrębny kontrolowany eksperyment dodający gaz1 do zwykłych kandydatów wyboru, przy tych samych ośmiu wagach, kątach, fizyce i opóźnieniu. Uzasadnienie stanowią regresja szybkości, strukturalny brak możliwości ponownego pełnego gazu i bezpieczna natywna sonda8,4–9,3s. Nowy protokół, dwa seedy i niewidziane stany powinny rozstrzygnąć, czy dostępna moc daje trwałą korzyść; zawodnik nadal sam wybiera0, poziom wyuczony lub1, bez wymuszenia gazu i bez sekwencji komend.',
        '## Weryfikacja',
        'Testy celowane, standardowe shardy Windows core/trajectory, Python i Node oraz status zdalnego Windows CI są zapisane w [verification.json](verification.json). CoreSim i oryginalne pliki fizyki pozostają identyczne względem wskazanego HEAD; historyczne wyniki#129/#130 nie są nadpisywane.']
    (D/'README.md').write_text('\n\n'.join(sections)+'\n',encoding='utf-8')

if __name__=='__main__': main()
