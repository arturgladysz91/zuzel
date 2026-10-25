"""Describe frozen #134 native evidence. Does not run, tune, or select policies."""
import argparse, csv, gzip, hashlib, importlib.util, json, math, os, statistics, struct
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
D = ROOT/'docs/experiments/track-awareness-anticipatory-riding'
spec = importlib.util.spec_from_file_location('decision_report', ROOT/'tools/rider-decision-report.py')
prior = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prior)
def read(p):
    p = Path(p)
    return json.loads(p.read_bytes() if p.exists() else gzip.decompress(Path(str(p)+'.gz').read_bytes()))
sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
median = lambda xs: statistics.median(xs) if xs else None
table = prior.table
fmt = lambda x: '—' if x is None else f'{x:.3f}'
pct = lambda x: '—' if x is None else f'{100*x:+.2f}%'


def write(name, value):
    (D/name).write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False)+'\n', encoding='utf-8')


def packed(name, value):
    (D/name).write_bytes(gzip.compress(json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode(), compresslevel=9, mtime=0))


def actuator_envelope(input, seconds):
    rate, limit = math.pi/2, math.radians(35)
    end = input['Time']+max(0, seconds)
    extremes = [min(math.radians(-15), input['Steering'], input['Active']['SteeringRadians']), max(math.radians(15), input['Steering'], input['Active']['SteeringRadians'])]
    def held(target):
        time, angle, active = input['Time'], input['Steering'], input['Active']['SteeringRadians']
        events = [(c['ActivationAt'], c['Action']['SteeringRadians']) for c in input['Pending'] if c['ObservedAt'] < input['Time']-1e-8]
        events.append((time+.24, target))
        for at, command in sorted(events, key=lambda e: e[0])+[(end, None)]:
            if at > end+1e-10:
                continue
            until = max(input['Time'], at)
            dt = max(0, until-time)
            angle += max(-rate*dt, min(rate*dt, max(-limit, min(limit, active))-angle))
            time = until
            if command is not None:
                active = command
        return angle
    return dict(LowRadians=held(extremes[0]), HighRadians=held(extremes[1]), Seconds=seconds,
                Scope='Known pending then held grid extrema; actuator only, no yaw/trajectory guarantee. Future unknown observations are not read.')


def quality(run):
    q = prior.old.quality(run)
    # Whole-footprint gate depends on the actual geometry of this world.
    prior.GEOMETRY = read(D/(run['GeometryName']+'-geometry.json'))
    gate = prior.first_whole_gate(run)
    q['FirstWholeGateTime'] = gate['Input']['Time'] if gate else None
    q['FirstWholeGateSpeed'] = gate['Input']['Rider']['Speed'] if gate else None
    q['Geometry'] = run['GeometryName']
    samples, decisions = run['Replay']['Samples'], run['Replay']['Decisions']
    q['PreparationSeconds'] = None
    straight = [s for s in samples if s['Input']['Rider']['Road']['Segment'] == 4]
    second = next((s for s in samples if s['Input']['Rider']['Road']['Segment'] >= 5), None)
    if straight and second:
        q['PreparationSeconds'] = second['Input']['Time']-straight[0]['Input']['Time']
    q['SecondGateIncrement'] = q['Native']['GateSeconds']-q['FirstWholeGateTime'] if q['Native']['Completed'] and q['FirstWholeGateTime'] else None
    events = []
    for start, end in [(1, 4), (5, 8)]:
        entry = next((s for s in samples if s['Input']['Rider']['Road']['Segment'] >= start), None)
        exit_sample = next((s for s in samples if s['Input']['Rider']['Road']['Segment'] >= end), None)
        previous_exit = 0 if start == 1 else (straight[0]['Input']['Time'] if straight else math.inf)
        if entry is None:
            continue
        window = [d for d in decisions if previous_exit <= d['Time'] <= entry['Input']['Time']]
        coast = next((d for d in window if d['Action']['Drive'] == 0), None)
        steer = next((d for d in window if abs(d['Action']['SteeringRadians']) >= math.radians(.5)), None)
        event = dict(Bend=1 if start == 1 else 2, EntryTime=entry['Input']['Time'], ExitTime=exit_sample['Input']['Time'] if exit_sample else None,
                     FirstCoastRequest=coast['Time'] if coast else None, FirstSteeringRequest=steer['Time'] if steer else None)
        event['CoastActivation'] = next((a['ActivationAt'] for a in run['Replay']['Activations'] if coast and abs(a['ObservedAt']-coast['Time']) < 1e-8), None)
        activation = next((a for a in run['Replay']['Activations'] if steer and abs(a['ObservedAt']-steer['Time']) < 1e-8), None)
        event['SteeringActivation'] = activation['ActivationAt'] if activation else None
        at = activation['ActivationAt'] if activation else math.inf
        before = next((s for s in reversed(samples) if s['Input']['Time'] < at-1e-8), samples[0])
        v0 = before['Input']['Rider']
        chi0 = math.atan2(v0['Velocity']['Y'], v0['Velocity']['X'])
        after = [s for s in samples if at <= s['Input']['Time'] <= entry['Input']['Time']]
        predicates = {
            'PhysicalSteeringResponse': lambda s: abs(s['Input']['Steering']-before['Input']['Steering']) >= math.radians(.1),
            'YawResponse': lambda s: abs(s['Input']['Rider']['AngularVelocity']-v0['AngularVelocity']) >= .01,
            'ChiResponse': lambda s: abs(prior.old.prior.angle(math.atan2(s['Input']['Rider']['Velocity']['Y'], s['Input']['Rider']['Velocity']['X'])-chi0)) >= math.radians(.5)}
        for key, predicate in predicates.items():
            event[key] = next((s['Input']['Time'] for s in after if predicate(s)), None)
        observed = next((s for s in samples if steer and abs(s['Input']['Time']-steer['Time']) < 1e-8), None)
        if observed:
            prior.GEOMETRY = read(D/(run['GeometryName']+'-geometry.json'))
            corner_start = next(s['startReferenceMeters'] for s in prior.GEOMETRY['segments'] if s['index'] == start)
            event['ReferenceDistanceAtRequest'] = corner_start-observed['Input']['Rider']['TotalReferenceMeters']
            event['ReactionDistanceAtRequest'] = observed['Input']['Rider']['Speed']*.24
            event['YawAtRequest'] = observed['Input']['Rider']['AngularVelocity']
            event['QueueAwareActuatorAtActualEntry'] = actuator_envelope(observed['Input'], entry['Input']['Time']-observed['Input']['Time'])
        if steer and steer.get('TrackKnowledge'):
            k = steer['TrackKnowledge']
            change = next((v for v in k['Profile'] if abs(v['Curvature']-k['Profile'][0]['Curvature']) > 1e-6), None)
            event['KnowledgeAtRequest'] = dict(DistanceToNextCorner=k['DistanceToNextCornerMeters'], SecondsToNextCorner=k['SecondsToNextCorner'], DistanceToCurvatureChange=change['MetricDistanceAtCurrentOffsetMeters'] if change else None, SecondsToCurvatureChange=change['ArrivalSecondsAtCurrentVelocity'] if change else None, ReachableSteeringLow=k['ReachableSteeringLowRadians'], ReachableSteeringHigh=k['ReachableSteeringHighRadians'])
        events.append(event)
    q['Anticipation'] = events
    q['LateHeuristicStates'] = []
    q['ActuatorDiagnostics'] = []
    for d in decisions:
        h = next((c.get('TrackAwareness') for c in d['Candidates'] if c['Action'] == d['Action']), None)
        if h and h['ResidualSpeedMetersPerSecond'] > 0 and h['SecondsToCriticalGeometry'] is not None and h['SecondsToCriticalGeometry'] < .24+h['TyreResponseSeconds']:
            q['LateHeuristicStates'].append(dict(Time=d['Time'], **h))
        if d.get('TrackKnowledge') and d['TrackKnowledge']['SecondsToNextCorner'] is not None:
            observed = next(s['Input'] for s in samples if abs(s['Input']['Time']-d['Time']) < 1e-8)
            k = d['TrackKnowledge']
            q['ActuatorDiagnostics'].append(dict(Time=d['Time'], QueueAware=actuator_envelope(observed, k['SecondsToNextCorner']), ArchivedSimplifiedLow=k['ReachableSteeringLowRadians'], ArchivedSimplifiedHigh=k['ReachableSteeringHighRadians']))
    q['Scope'] = 'Actual native samples; response thresholds steer0.1deg/yaw0.01rad/s/chi0.5deg relative to sample before activation. Temporal association, not isolated causal proof. Whole gate uses this geometry and mechanical capsules; no interpolated crossing. LateHeuristicStates are warnings, not proof of global unreachability.'
    return q


def pairs(final):
    rows = []
    for suite in ['single', 'two', 'transfer']:
        group = {e['Variant'].removesuffix('-'+suite): e for e in final if e['Variant'].endswith('-'+suite)}
        for label in ['134-A', '134-B']:
            for ref in ['131-H09', '133-A0', '134-A']:
                if label == ref:
                    continue
                a, b = group[ref], group[label]
                assert a['Scenarios'] == b['Scenarios']
                ps = [dict(Scenario=n, Old=x['Native']['GateSeconds'], New=y['Native']['GateSeconds'], Ratio=y['Native']['GateSeconds']/x['Native']['GateSeconds']-1)
                      for n, x, y in zip(a['Scenarios'], a['Results'], b['Results']) if x['Native']['Completed'] and y['Native']['Completed']]
                rows.append(dict(Suite=suite, Reference=ref, Compared=label, Pairs=ps, CommonCompleted=len(ps), MedianRatio=median([r['Ratio'] for r in ps]), MaximumRatio=max([r['Ratio'] for r in ps], default=None)))
    return rows


def behavior_pairs(protocol, qualities, aliases):
    lookup = {q['Name']: q for q in qualities}
    rows = []
    def flatten(q):
        out = {key: q.get(key) for key in ['FirstWholeGateTime', 'FirstWholeGateSpeed', 'PreparationSeconds', 'SecondGateIncrement', 'LargeSteeringChanges5Degrees', 'BetaAbove20Seconds', 'BetaAbove35Seconds', 'MaximumYawRate', 'SteeringRequestTravelDegrees', 'EngineWorkJoules', 'MaximumAbsoluteWheelSlipRatio']}
        out.update({key: q['Native'][key] for key in ['EntrySpeed', 'MinimumSpeed', 'ExitSpeed', 'MeanDrive', 'FullDriveFraction', 'MaximumBetaDegrees', 'MinimumClearanceMeters', 'DistanceMeters', 'LargeTrajectoryOscillations']})
        for i, b in enumerate(q['Bends']):
            out.update({f'Bend{i+1}{key}': value for key, value in b.items()})
        for e in q['Anticipation']:
            out.update({f'Bend{e["Bend"]}{key}': e.get(key) for key in ['FirstCoastRequest', 'CoastActivation', 'FirstSteeringRequest', 'SteeringActivation', 'PhysicalSteeringResponse', 'YawResponse', 'ChiResponse', 'ReferenceDistanceAtRequest', 'ReactionDistanceAtRequest']})
        return out
    for suite, key in [('single', 'FinalSingle'), ('two', 'FinalTwo'), ('transfer', 'Transfer')]:
        for reference in ['133-A0', '134-A']:
            ps = []
            for c in protocol[key]:
                a, b = [lookup[f'final-{aliases[label]}-{c["Geometry"]}-{c["Name"]}'] for label in [reference, '134-B']]
                if not a['Native']['Completed'] or not b['Native']['Completed']:
                    continue
                x, y = flatten(a), flatten(b)
                ps.append(dict(Scenario=c['Name'], Metrics={k: dict(Old=v, New=y[k], Delta=y[k]-v) for k, v in x.items() if v is not None and y.get(k) is not None}))
            keys = list(dict.fromkeys(k for row in ps for k in row['Metrics']))
            rows.append(dict(Suite=suite, Reference=reference, Compared='134-B', CommonCompleted=len(ps), Pairs=ps, Medians={k: dict(Count=sum(k in r['Metrics'] for r in ps), **{f: median([r['Metrics'][k][f] for r in ps if k in r['Metrics']]) for f in ['Old', 'New', 'Delta']}) for k in keys}, Scope='Same completed native pairs; absent events remain absent. Event-time deltas measure timing, not isolated causal attribution; earlier coast and earlier steering are distinct.'))
    return rows


def viewer(protocol, raw, final, paired, aliases, verdict):
    load = lambda n: json.loads(gzip.decompress((raw/(n+'.json.gz')).read_bytes()))
    selected, groups = {}, []
    labels = {'131-H09': '#131 #4061', '133-A0': '#133 A0 #12023', '134-A': '#134 A · trening bez profilu', '134-B': '#134 B · trening z profilem'}
    colors = dict(zip(labels, ['#ffd27b', '#d99fe3', '#ff9c86', '#73dfdf']))
    def group(gid, c, refs, reasons, scope):
        runs = []
        for policy, name in refs:
            selected[name] = load(name)
            runs.append(dict(Policy=policy, Name=name))
        groups.append(dict(Id=gid, Scenario=c['Scenario'], TwoBends=c['Two'], GeometryFile=c['Geometry']+'-geometry.json', Reasons=reasons, Scope=scope, Runs=runs))
    names = ['M00', 'N00', 'T06', 'T07']
    main_pair = next(r for r in paired if r['Suite'] == 'two' and r['Compared'] == '134-B' and r['Reference'] == '133-A0')
    if main_pair['Pairs']:
        names += [min(main_pair['Pairs'], key=lambda r: r['Ratio'])['Scenario'].split('-')[-1], max(main_pair['Pairs'], key=lambda r: r['Ratio'])['Scenario'].split('-')[-1]]
    for e in final:
        if e['Variant'].startswith('134-B'):
            names += [n.split('-')[-1] for n, m in zip(e['Scenarios'], e['Results']) if not m['Native']['Completed']][:1]
    for name in dict.fromkeys(names):
        c = next(c for key in ['FinalSingle', 'FinalTwo', 'Transfer'] for c in protocol[key] if c['Name'] == name)
        refs = [(label, f'final-{aliases[label]}-{c["Geometry"]}-{name}') for label in labels]
        group(name, c, refs, ['nowy niezależny finał' if name[0] != 'T' else 'nieznana geometria transferu', 'dwa ciągłe łuki' if c['Two'] else 'pierwszy łuk'], 'Wspólny stan i rzeczywista geometria. Jawne aliasy: '+json.dumps(aliases, ensure_ascii=False)+'. Bez resetów między łukami; DNF nie ma czasu mety.')
    for i in [0, 1, 2, 3]:
        c = protocol['Diagnostic'][i]
        refs = [(f'preflight-{a}', f'profile-{a}-P{i}') for a in ['A', 'B']]
        for a, color in [('A', '#ffd27b'), ('B', '#73dfdf')]:
            labels[f'preflight-{a}'] = '#12023 bez uczenia · '+a
            colors[f'preflight-{a}'] = color
        group(f'AB-P{i}', c, refs, ['izolowany wpływ wiedzy, bez retreningu'], 'Te same osiem wag, H09 i akcje. Zmienia się tylko miękka ocena przyszłej geometrii. Prognoza H09 nie jest pełną trajektorią przejazdu.')
    for case, time, command in [('P1', '060', 'drive'), ('P3', '020', 'drive'), ('P3', '040', 'drive'), ('P3', '060', 'drive')]:
        c = next(c for c in protocol['Diagnostic'] if c['Name'] == case)
        label = f'timing-{case}-{time}-{command}'
        labels[label], colors[label] = 'Jedna komenda w '+str(int(time)/100)+'s', '#73dfdf'
        labels['timing-base'], colors['timing-base'] = 'Oryginalna A #12023', '#ffd27b'
        group(label, c, [('timing-base', 'profile-A-'+case), (label, label)], ['kontrolowana kontynuacja', 'reakcja0,24s'], 'Jedna komenda, potem A. Osiągalność badana tylko dla2akcji i4chwil co0,2s; brak twierdzenia o globalnie ostatniej możliwej reakcji. Nieskuteczna próba pozostaje DNF.')
    gains = []
    for g in groups:
        refs = g['Runs']
        a = selected[next((r['Name'] for r in refs if r['Policy'] == '133-A0'), refs[0]['Name'])]
        b = selected[refs[-1]['Name']]
        points = []
        for ref in range(30, 291, 5):
            x, y = [next((s['Input'] for s in r['Replay']['Samples'] if s['Input']['Rider']['TotalReferenceMeters'] >= ref), None) for r in [a, b]]
            if x and y:
                points.append(dict(ReferenceMeters=ref, OldTime=x['Time'], NewTime=y['Time'], GainSeconds=x['Time']-y['Time'], OldXY=x['Rider']['Position'], NewXY=y['Rider']['Position']))
        gains.append(dict(Group=g['Id'], Policy=refs[-1]['Policy'], Samples=points, Description='Zysk ostatniego wariantu względem #133 A0 (finał) lub pierwszego (diagnostyka). Przekroczenia co5m z rzeczywistych próbek, bez interpolacji, dokładność0,01s.'))
    write('time-gain.json', gains)
    packed('selected-replays.json.gz', dict(Runs=selected))
    write('selected-manifest.json', dict(BundleSha256=sha(D/'selected-replays.json.gz'), Runs={n: dict(RawSha256=sha(raw/(n+'.json.gz')), NativeFingerprint=r['Metrics']['Native']['Fingerprint']) for n, r in selected.items()}))
    write('index.json', dict(Title='Track Awareness & Anticipatory Riding #134', Grade=verdict, TrackAwareness=True, DefaultRun='final-'+aliases['134-B']+'-progressive-c-N00', PreferredPolicy='134-B', RawDirectory=str(raw), Labels=labels, Colors=colors, Horizons={k: .9 for k in labels}, TimeGain='time-gain.json', ComparisonCharts=True, Groups=groups, Charts=[['learning.svg', 'Uczenie obu ramion i seedów'], ['final.svg', 'Nowe finały i transfer'], ['anticipation.svg', 'Decyzja, aktywacja i wykonanie bez retreningu']]))
    return selected


def main():
    global D
    parser = argparse.ArgumentParser()
    parser.add_argument('raw')
    parser.add_argument('--data', default=str(D))
    args = parser.parse_args()
    D, raw = Path(args.data).resolve(), Path(args.raw).resolve()
    p, attempts, final = [read(D/n) for n in ['protocol.json', 'attempts.json', 'final-results.json']]
    aliases, choices = read(D/'final-aliases.json'), read(D/'choices.json')
    paired = pairs(final)
    write('paired-times.json', paired)
    summary = [dict(Variant=e['Variant'], Completed=sum(m['Native']['Completed'] for m in e['Results']), Cases=len(e['Results']), DNF=dict(Counter(m['Native']['Outcome'] for m in e['Results'] if not m['Native']['Completed']))) for e in final]
    pick = lambda suite, ref='133-A0': next(r for r in paired if r['Suite'] == suite and r['Reference'] == ref and r['Compared'] == '134-B')
    get = lambda label, suite: next(r for r in summary if r['Variant'] == label+'-'+suite)
    valid = all(read(D/n)[key] for n, key in [('geometry-preflight.json', 'Passed'), ('preflight.json', 'TrainingGatePassed'), ('old-knowledge-audit.json', 'Off133DecisionsExact')])
    transfer = pick('transfer', '134-A')
    criteria = dict(Valid=valid, Single24=get('134-B', 'single')['Completed'] == 24, Two12=get('134-B', 'two')['Completed'] == 12,
                    TwoTime=pick('two')['MedianRatio'] is not None and pick('two')['MedianRatio'] <= -.02,
                    SingleMedian=pick('single')['MedianRatio'] is not None and pick('single')['MedianRatio'] <= .05,
                    SingleMaximum=pick('single')['MaximumRatio'] is not None and pick('single')['MaximumRatio'] <= .15,
                    TransferReliability=get('134-B', 'transfer')['Completed'] >= get('134-A', 'transfer')['Completed'],
                    TransferMeasurableBenefit=transfer['MedianRatio'] is not None and transfer['MedianRatio'] < 0)
    verdict = 'FAIL' if not valid else 'PASS' if all(criteria.values()) else 'PARTIAL'
    totals = {key: sum(e[key] for e in attempts) for key in ['NativeWorlds', 'CpuSeconds', 'Evaluations', 'Forecasts']}
    categories = {cat: {k: sum(e[k] for e in attempts if e['Category'] == cat) for k in totals} for cat in dict.fromkeys(e['Category'] for e in attempts)}
    generations = []
    for seed in p['Seeds']:
        for arm in ['A', 'B']:
            for g in read(D/f'generations-{arm}-s{seed}.json'):
                ms = [r['Native'] for c in g['Candidates'] for r in c['Results']]
                generations.append(dict(Variant=f'{arm}-s{seed}', Generation=g['Generation'], BestId=g['BestId'], Completed=sum(m['Completed'] for m in ms), Worlds=len(ms), CpuSeconds=g['CpuSeconds'], Forecasts=g['Forecasts'], Contacts=g['Evaluations'], DNF=dict(Counter(m['Outcome'] for m in ms if not m['Completed']))))
    write('generations-summary.json', generations)
    analysis = dict(Verdict=verdict, Criteria=criteria, Totals=totals, Categories=categories, Summaries=summary, Choices=choices, SelectedSamePolicy=choices[0]['Policy'] == choices[1]['Policy'], Paired=paired)
    write('analysis.json', analysis)
    qualities, native_manifest = [], []
    for e in attempts:
        if not e['NativeWorlds']:
            continue
        path = raw/(e['Name']+'.json.gz')
        assert e['Complete'] and sha(path) == e['RawSha256'], e['Name']
        native_manifest.append({k: e[k] for k in ['Name', 'Category', 'RawSha256', 'RawBytes']})
        if e['Category'] in ['final', 'diagnostic'] and not e['Name'].startswith('held-'):
            qualities.append(quality(json.loads(gzip.decompress(path.read_bytes()))))
    packed('native-quality.json.gz', qualities)
    write('behavior-pairs.json', behavior_pairs(p, qualities, aliases))
    write('raw-manifest.json', dict(RawDirectory=str(raw), Runs=native_manifest, Scope='All full native raw files hash checked locally; selected replay bundle committed. No raw training files in Git.'))
    with (D/'attempts.csv').open('w', newline='', encoding='utf-8') as f:
        rows = [{**(e.get('Metrics') or {}).get('Native', {}), **{k: e[k] for k in ['Number', 'Name', 'Category', 'NativeWorlds', 'CpuSeconds', 'Evaluations', 'Forecasts']}} for e in attempts]
        fields = list(dict.fromkeys(k for row in rows for k in row))
        writer = csv.DictWriter(f, fields)
        writer.writeheader()
        writer.writerows(rows)
    selected = viewer(p, raw, final, paired, aliases, verdict)
    write('representative-quality.json', [quality(r) for r in selected.values()])
    plots(generations, paired)
    for path in D.glob('*.svg'):
        path.write_text('\n'.join(line.rstrip() for line in path.read_text(encoding='utf-8').splitlines())+'\n', encoding='utf-8', newline='\n')
    report(p, analysis, qualities, aliases)
    # Compress full audit bytes without changing records; keep original bytes local.
    for name in ['old-knowledge-audit', 'predictor']:
        path = D/(name+'.json')
        content = path.read_bytes() if path.exists() else gzip.decompress((D/(name+'.json.gz')).read_bytes())
        (D/(name+'.json.gz')).write_bytes(gzip.compress(content, compresslevel=9, mtime=0))
        write(name+'-archive.json', dict(OriginalSha256=hashlib.sha256(content).hexdigest().upper(), CompressedSha256=sha(D/(name+'.json.gz')), UncompressedBytes=len(content)))
    write('manifest.json', {f.name: sha(f) for f in sorted(D.iterdir()) if f.is_file() and f.name not in ['manifest.json', 'budget.lock', 'old-knowledge-audit.json', 'predictor.json']})
    print(json.dumps(dict(Verdict=verdict, Criteria=criteria, Totals=totals, Summaries=summary), indent=2))


def plots(generations, paired):
    os.environ.setdefault('MPLCONFIGDIR', str(ROOT/'work/mpl-cache'))
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    matplotlib.rcParams['svg.hashsalt'] = 'track-awareness-134'
    fig, ax = plt.subplots(figsize=(9, 4))
    for label in dict.fromkeys(g['Variant'] for g in generations):
        rows = [g for g in generations if g['Variant'] == label]
        ax.plot([r['Generation'] for r in rows], [r['Completed'] for r in rows], 'o-', label=label)
    ax.set(xlabel='Generacja (kohorty 0/1/0/1)', ylabel='Ukończenia / 15', ylim=(0, 16), xticks=range(4))
    ax.legend(ncol=2)
    fig.tight_layout()
    fig.savefig(D/'learning.svg', metadata={'Date': None})
    plt.close(fig)
    fig, axes = plt.subplots(1, 3, figsize=(12, 4))
    for ax, suite in zip(axes, ['single', 'two', 'transfer']):
        for label, color in [('134-A', '#cb6d47'), ('134-B', '#008c95')]:
            row = next(r for r in paired if r['Suite'] == suite and r['Reference'] == '133-A0' and r['Compared'] == label)
            ax.plot(range(len(row['Pairs'])), [100*r['Ratio'] for r in row['Pairs']], 'o-', label=label, color=color)
        ax.axhline(0, color='gray', linewidth=.8)
        ax.set(title=suite, xlabel='Wspólnie ukończony przypadek', ylabel='Zmiana czasu względem #133 A0 %')
        ax.legend()
    fig.tight_layout()
    fig.savefig(D/'final.svg', metadata={'Date': None})
    plt.close(fig)
    anticipation_plot()


def anticipation_plot(preview=None):
    import matplotlib.pyplot as plt
    runs = json.loads(gzip.decompress((D/'selected-replays.json.gz').read_bytes()))['Runs']
    fig, axes = plt.subplots(4, 1, figsize=(10, 9), sharex=True)
    for arm, color in [('A', '#b67611'), ('B', '#008c95')]:
        run = runs['profile-'+arm+'-P3']
        ss = run['Replay']['Samples']
        fields = [lambda s: s['Input']['Rider']['Speed']*3.6, lambda s: s['Input']['Active']['Drive']*100,
                  lambda s: s['Input']['Steering']*180/math.pi, lambda s: s['Input']['Rider']['AngularVelocity']]
        event = quality(run)['Anticipation'][1]
        for ax, value in zip(axes, fields):
            ax.plot([s['Input']['Time'] for s in ss], [value(s) for s in ss], label=arm, color=color, drawstyle='steps-post' if ax is axes[1] else 'default')
            if event['FirstCoastRequest'] is not None:
                ax.axvline(event['FirstCoastRequest'], color=color, linestyle=':', linewidth=.8)
                ax.axvline(event['CoastActivation'], color=color, linestyle='--', linewidth=.8)
    for ax, title in zip(axes, ['Prędkość km/h', 'Gaz aktywny %', 'Fizyczna kierownica °', 'Yaw rad/s']):
        ax.set(ylabel=title)
        ax.grid(alpha=.2)
        ax.legend(loc='upper right')
    axes[0].set_title('P3 bez retreningu: kropki = żądanie gaz0 przed łukiem2; kreski = aktywacja po0,24s')
    axes[-1].set_xlabel('Rzeczywisty czas natywnego świata s; A kończy się przy DNF, bez dopisania kontynuacji')
    fig.tight_layout()
    fig.savefig(D/'anticipation.svg', metadata={'Date': None})
    if preview:
        fig.savefig(preview, dpi=120)
    plt.close(fig)


def report(p, a, qualities, aliases):
    audit = read(D/'old-knowledge-audit.json')
    pf = read(D/'preflight.json')
    timing = read(D/'reaction-continuations.json')
    q = {x['Name']: x for x in qualities}
    paired = a['Paired']
    behavior = read(D/'behavior-pairs.json')
    pair = lambda suite, ref='133-A0', label='134-B': next(r for r in paired if r['Suite'] == suite and r['Reference'] == ref and r['Compared'] == label)
    example = [q[f'profile-{arm}-P{i}'] for i in [1, 2, 3] for arm in ['A', 'B']]
    anticipations = [[r['Name'], e['Bend'], fmt(e['FirstCoastRequest']), fmt(e['CoastActivation']), fmt(e['FirstSteeringRequest']), fmt(e['SteeringActivation']), fmt(e['PhysicalSteeringResponse']), fmt(e['YawResponse']), fmt(e['ChiResponse']), fmt(e['EntryTime']), fmt(r['FirstWholeGateTime']), fmt(r['Native']['GateSeconds'])] for r in example for e in r['Anticipation']]
    geometry = read(D/'geometry-preflight.json')
    ranges = [r['OriginalGeometryQuery']['TotalAheadReferenceMeters'] for r in audit['Rows']]
    times = [r['Knowledge']['SecondsToNextCorner'] for r in audit['Rows'] if r['Knowledge']['SecondsToNextCorner'] is not None]
    reaction = [r['Knowledge']['ReactionDistanceMeters'] for r in audit['Rows']]
    seed_rows = [[label, sum(g['Completed'] for g in read(D/'generations-summary.json') if g['Variant'] == label), 60] for label in [f'{arm}-s{seed}' for seed in p['Seeds'] for arm in ['A', 'B']]]
    text = f'''# PR #134 — Track Awareness & Anticipatory Riding

Werdykt **{a['Verdict']}**. Nowa wiedza jest eksperymentem domyślnie OFF. Wynik nie zmienia produkcyjnej polityki. Klasy H09, certyfikat obrysu, Box2D, opony, silnik, kolejka, reakcja i osiem granic parametrów pozostają takie jak w dokładnej bazie `{p['BaseHead']}`. #133 ma Draft PR i potwierdzone [Windows CI #369](https://github.com/arturgladysz91/zuzel/actions/runs/40231976774).

B #17015 uzyskał24/24 pojedynczych i12/12 dwóch łuków. Mediana dwóch względem133A0 zmieniła się o {pct(pair('two')['MedianRatio'])}, pojedynczego o {pct(pair('single')['MedianRatio'])} (najgorsza para {pct(pair('single')['MaximumRatio'])}). Jednak wobec nowo wytrenowanego A #16015 B jest wolniejszy: {pct(pair('two','134-A')['MedianRatio'])} na dwóch łukach i {pct(pair('transfer','134-A')['MedianRatio'])} na transferze. Oba ramiona mają11/12 transferu, przy10 wspólnych ukończeniach. Warunek dodatkowej korzyści transferowej nie przeszedł. Nie podmieniono zamrożonego B na szybsze A. Wynik nie dowodzi przewagi nowego modułu nad samym ponownym treningiem.

## 1. Co zawodnik wiedział przed zmianą?

`TrackFrame`, `ExperimentalTrackShape` i topologia udostępniają całą pętlę, początek/koniec obu łuków, szerokość i ciągły profil κ. Dostęp nie oznacza wykorzystania. A0 wybierał kierunek χ końca H09 względem stycznej w `endpointReference + max(0.1, ReferenceSpeed(endpoint)*PreviewSeconds)`. Dla #12023 PreviewSeconds={p['StartingPolicy']['PreviewSeconds']:.8f}s. W audycie co dziesiątej decyzji historycznego natywnego X00 całkowity dystans zapytania wyniósł {min(ranges):.2f}–{max(ranges):.2f}m; pełne stany i odległości są w `old-knowledge-audit.json.gz`. Przebieg zależy od przewidywanej prędkości oraz projekcji na tor, nie od jednej stałej odległości. ψ nie jest χ. H09 zna zdarzenia zmiany odcinka i siły, ale nie ocenia całego przyszłego profilu. Nie dowodzimy, że sam krótki H09 był głównym problemem.

{table(['Czas s','Prędkość m/s','Odcinek','Koniec H09 Δref m','Podgląd za H09 m','Całe zapytanie Δref m'], [[r['Time'],fmt(r['Input']['Rider']['Speed']),r['Input']['Rider']['Road']['Segment'],fmt(r['OriginalGeometryQuery']['EndpointReference']-r['Input']['Rider']['TotalReferenceMeters']),fmt(r['OriginalGeometryQuery']['PostEndpointAheadReferenceMeters']),fmt(r['OriginalGeometryQuery']['TotalAheadReferenceMeters'])] for r in audit['Rows']])}

Stary sterownik rozpoznawał prosta–łuk–prosta poprzez geometrię zapytania i prognozę; brakowało mu jawnego opisu czasu do łuku oraz zakresu kierownicy osiągalnego przed nim. Nie był pozbawiony wiedzy o torze.

## 2. Co dodaje moduł?

`TrackAwareness134.Observe` zwraca niezmienne fakty geometryczne: aktualny odcinek, następny łuk, długość/kierunek/koniec, koniec bieżącego, prostą za łukiem, następny początek, κ i szerokość co2m oraz na złączeniach. Zasięg adaptuje się do prędkości, reakcji, szybkości kierownicy i odpowiedzi opon:20–60m plus opis/próbki wejścia następnego łuku, gdy jest dalej. Odległość po stałym obecnym offsecie i czas przy obecnej prędkości są przybliżeniami; nie są przyszłymi stanami. Punkty XY w profilu oznaczają geometrię referencyjną, nie wybraną linię.

`Evaluate` zastępuje tylko składnik GeometrySpeed tą samą wagą × kwadrat reszty prędkości oszacowanej na profilu. Korzysta z istniejącej prognozy kandydata H09 oraz już obliczonego H09 gaz0 przy tym samym skręcie: spadku prędkości, yaw authority, przyczepności, opóźnienia i optymistycznego obszaru na zewnątrz. Nie dodaje prognoz ani sił, nie zmienia DirectionWeight/ThrottleWeight/rankingu R0. Ekstrapolacja hamowania silnikiem i miejsca na zewnątrz pozostaje miękką heurystyką. Stan zagrożony na końcu H09 zachowuje dawny koszt. Heurystyka nigdy nie nadaje klasy Unsafe.

**Ograniczenie interpretacji:** zamrożone pole `TyreResponseSeconds` jest sumą starych ustawień regulatora LateralResponseSeconds+SlipYawResponseSeconds. W tym trybie `steeringOverride` pomija ich wpływ na fizyczne siły opon. Test zmienia wszystkie trzy ustawienia odpowiedzi i otrzymuje identyczne punkty H09 oraz liczbę obliczeń sił. Dlatego tej sumy nie traktujemy jako rzeczywistego opóźnienia opony: to nieskalibrowany miękki margines przygotowania. Żaden wynik Native, koszt, checkpoint ani zamrożony plik źródłowy nie został podmieniony; [wyjaśnienie i zakres korekty](mechanism-interpretation.json) pozostają jawne.

## 3. Czy zawodnik myśli wcześniej?

Poniższa tabela rozdziela obserwację/żądanie, aktywację i fizyczną odpowiedź. Pierwsze przygotowanie oznacza pierwsze żądanie gaz0 lub |skrętu|≥0,5° przed danym wejściem (dla drugiego łuku od początku prostej po pierwszym). Skręt wykonany: zmiana≥0,1°, yaw≥0,01rad/s i χ≥0,5° względem próbki przed aktywacją. To progi pomiarowe i współwystępowanie, nie niezależny dowód przyczynowy.

{table(['Przejazd','Łuk','Gaz0 żąd.','Gaz0 akt.','Skręt żąd.','Skręt akt.','Skręt fiz.','Yaw','χ','Wejście','T1 bramka','T oba'], anticipations)}

Te same chwile żądań przy innych komendach nie są wcześniejszym myśleniem. Pierwsze decyzje mogą wypaść już w0,00s, więc nie da się wykazać ich wyprzedzenia poza początek obserwacji. W P3 bez uczenia B odpuszcza przed drugim łukiem w7,8s zamiast9,0s, lecz pierwsze żądanie skrętu pojawia się później (9,4s zamiast8,8s). To wcześniejsze przygotowanie prędkości, a nie wcześniejszy skręt. Pełne pola, odległości i stan są w `native-quality.json.gz` i reprezentatywnych replayach; nie zastępujemy ich samym czasem końca H09.

{table(['Pula','Referencja','Metryka','Liczba par','Mediana B−ref'], [[r['Suite'],r['Reference'],key,r['Medians'][key]['Count'],fmt(r['Medians'][key]['Delta'])] for r in behavior for key in ['Bend1FirstCoastRequest','Bend2FirstCoastRequest','Bend2FirstSteeringRequest','FirstWholeGateTime','PreparationSeconds','SecondGateIncrement','DistanceMeters','MeanDrive'] if key in r['Medians']])}

`behavior-pairs.json` zachowuje wszystkie pary tych pomiarów oraz wejście/minimum/wyjście, β, yaw, certyfikat i oscylacje. Czas ujemny oznacza wcześniejsze zdarzenie lub krótszy odcinek; dodatni gaz/prędkość nie jest samodzielnym kryterium sukcesu.

![Natywne wykonanie i rozdzielona reakcja w P3](anticipation.svg)

## 4. Jaki dystans zabiera reakcja?

70km/h ×0,24s=4,667m. Sama zmiana15° przy90°/s trwa0,1667s; w tym czasie model zaczyna już rozwijać siły. W historycznych obserwacjach rzut drogi przy stałej prędkości na styczną toru (v·tangent ×0,24s) wynosił {min(reaction):.2f}–{max(reaction):.2f}m; nie jest to pomiar całej późniejszej drogi XY. Poprawna diagnostyka kierownicy najpierw realizuje znane pending, a od0,24s utrzymuje każdą ze skrajnych akcji dotychczasowego gridu. Respektuje90°/s i fizyczne±35°, nie odczytuje przyszłych nieznanych decyzji. Czas dojazdu w viewerze nadal jest oszacowaniem przy obecnej prędkości; `QueueAwareActuatorAtActualEntry` w raporcie używa rzeczywistego czasu wejścia wyłącznie jako pomiaru po przejeździe. Test JS sprawdza kontynuacje0,23s względem natywnych próbek, przed aktywacją nowej komendy.

Oryginalne pola `ReachableSteeringLow/High` zachowano w raw: idealizowały zmianę od obecnego kąta i nie uwzględniały aktywacji już oczekujących komend. Nie są certyfikatem i nie wpływają na koszt `Evaluate`; nie używamy ich w końcowym odczycie dostępności manewru. Poprawiona diagnostyka jest dodatkowym odczytem zapisów, bez zmiany fizyki lub sterowania. Zakres kierownicy nie jest granicą yaw/χ. Yaw authority pochodzi z rzeczywistych próbek H09, β z wektora prędkości; nie dokładamy hamulca5/8m/s².

## 5. Jak późno dało się jeszcze zareagować?

{table(['Stan','Obserwacja','Aktywacja','Komenda','Wynik','Czas oba','Zmiana względem A s'], [[t['Name'],t['Observation'],t['Activation'], '5° gaz0' if t['Action']['Drive']==0 else '15° gaz polityki',t['Trial']['Native']['Outcome'],fmt(t['Trial']['Native']['GateSeconds']),fmt(t['Trial']['Native']['GateSeconds']-t['Baseline']['Native']['GateSeconds']) if t['Trial']['Native']['Completed'] and t['Baseline']['Native']['Completed'] else '—'] for t in timing])}

Ostatni skuteczny spośród próbek0/0,2/0,4/0,6s dla obu stanów to0,6s dla komendy15°+gaz polityki; to prawy kraniec zbadanej siatki, nie globalna granica. Dla P3 skręt15° w0,2s kończy się DNF, w0,4s udaje się przejazd: wynik nie jest monotoniczną funkcją czasu. Rozdzielamy poprawną wiedzę geometryczną, miękkie ostrzeżenie i rzeczywisty wynik kontynuacji. Brak udanej próbki nie dowodzi, że żadna inna akcja nie mogła pomóc. Replaye pokazują nieskuteczny manewr pomimo poprawnej geometrii, bez nadawania mu fałszywego certyfikatu nieosiągalności.

W P3 żądanie z0,6s aktywuje się dopiero0,84s, a rzeczywisty początek łuku wypada0,838966s. Zachowany identyczny prefiks potwierdza, że ta nowa komenda nie mogła przygotować samego wejścia, choć może zmienić dalszy przejazd i próba ostatecznie kończy się sukcesem. To ścisły przykład zbyt późnego wpływu na konkretny punkt; nie twierdzenie o niemożności ukończenia całego łuku. Odczyt geometrii i osiągalność zmiany przed wejściem to różne fakty.

## 6. Czy wzrosła skuteczna prędkość?

{table(['Wariant','Pula','Ukończenia','DNF'], [[r['Variant'].rsplit('-',1)[0],r['Variant'].rsplit('-',1)[1],f"{r['Completed']}/{r['Cases']}",json.dumps(r['DNF'],ensure_ascii=False)] for r in a['Summaries']])}

{table(['Pula','Porównanie','Wspólne ukończenia','Mediana zmiany','Najgorsza zmiana'], [[r['Suite'],r['Compared']+' vs '+r['Reference'],r['CommonCompleted'],pct(r['MedianRatio']),pct(r['MaximumRatio'])] for r in paired])}

Każdy iloraz jest parą tego samego stanu/geometrii. DNF nie otrzymuje fikcyjnego czasu; warunek niezawodności oceniany jest osobno. Wejście/minimum/wyjście każdego łuku, gaz/reaktywacje, β, yaw, certyfikat, rzeczywista długość XY i oscylacje są w `native-quality.json.gz`; metryki wszystkich światów w `attempts.csv` i agregatach generacji.

## 7. Co z drugim łukiem?

Każdy przypadek two i T06–T11 jedzie w jednym świecie bez resetu. `FirstWholeGateTime` mierzy cały mechaniczny obrys3m za końcem pierwszego łuku na rzeczywistej geometrii, `SecondGateIncrement` to pozostały czas do bramki drugiego, a `PreparationSeconds` obejmuje prostą pomiędzy. Te pola oraz oba zestawy prędkości pozwalają odróżnić poprawę wejścia1 od szkody na łuku2. Wynik pełnych dwóch łuków B vs A0: mediana {pct(pair('two')['MedianRatio'])}; nie zastępujemy go lepszym lokalnym minimum ani samym T1.

## 8. Czy wiedza przenosi się na inny tor?

{table(['Geometria','Kontrola','κ max 1/m','Szerokość m','Błąd projekcji m'], [[r['Geometry'],r['Passed'],fmt(r['MaximumCurvature']),f"{r['MinimumWidth']:.2f}–{r['MaximumWidth']:.2f}",f"{r['MaximumProjectionReferenceError']:.9f}"] for r in geometry['Checks']])}

Trening/selekcja używały progressive-b i egg-medium, różniących się także κ. Progressive-b/c różnią się szerokością, więc nie traktujemy ich jako dwóch różnych profili κ. Główna nowa geometria końcowa to progressive-c; transfer to niewidziana egg-asymmetric (inne oba łuki),6single+6two. Krótkie0,02s próby wszystkich5geometrii sprawdzały runner, bez decyzji i wyniku sportowego. Uszkodzony tor zatrzymałby eksperyment jako błąd konfiguracji. Transfer B vs trenowana A: mediana {pct(pair('transfer','134-A')['MedianRatio'])}, ukończenia {next(r['Completed'] for r in a['Summaries'] if r['Variant']=='134-B-transfer')}/12 vs {next(r['Completed'] for r in a['Summaries'] if r['Variant']=='134-A-transfer')}/12. Sam odczyt nowej geometrii nie dowodzi przewagi sportowej.

## 9. Ile daje wiedza, a ile ponowny trening?

Bez uczenia: A3/4, B4/4; mediana wspólnych dwóch łuków {pct(pf['PairedTwoMedianRatio'])}. W P1/P2 B może wykonać inne komendy na tych samych chwilach, ograniczając β, ale nie daje to automatycznie szybszego przejazdu. Trening obu ramion zaczynał dokładnie od #12023, z identycznymi seedami, początkową populacją, przestrzenią działań i R0. A mierzy efekt ponownego treningu, B łączy go z nową wiedzą. Porównanie B–A na niezależnym finale jest właściwą kontrolą, B–A0 pokazuje łączny efekt. Brak pełnego faktorialnego treningu innych funkcji celu.

Czy wybrane A i B mają dokładnie ten sam końcowy wektor ośmiu parametrów: **{a['SelectedSamePolicy']}**. Różne identyfikatory kandydatów same w sobie nie dowodzą innych wag; zapisano pełne wektory w `choices.json`.

{table(['Ramię/seed','Ukończenia treningu','Światy'],seed_rows)}

## 10. Jak zamrożono porównanie parami?

Pule G/K/P/M/N/T i geometrie zapisano przed pierwszym nowym światem. M00–M23/N00–N11 to36 nowych stanów, T00–T11 dodatkowe12. Nie powtarzają finałów130–133. Selekcja R0 wybiera tylko z zapisanej krótkiej listy na K0–K5. A/B i aliasy zamrożono przy0 finałów w `winner-lock.json`; wynik nie zmienia polityki po otwarciu finału. Wybrane ID: {', '.join(c['Label']+' #'+str(c['Id']) for c in a['Choices'])}. Referencje131#4061 i133A0#12023 mają właściwą dawną przestrzeń/full throttle. Jawne aliasy: `{json.dumps(aliases)}`.

## 11. Ile światów faktycznie uruchomiono?

{table(['Kategoria','Światy','CPU s','Prognozy','Oceny kontaktów'], [[k,v['NativeWorlds'],fmt(v['CpuSeconds']),v['Forecasts'],v['Evaluations']] for k,v in a['Categories'].items()])}

Łącznie {a['Totals']['NativeWorlds']} natywnych światów. Pięć to próby0,02s, cztery to kontrolowane H09; rozliczamy je jawnie. Offline nie tworzy świata Box2D. Powtórzenia i kontrfaktyczne manewry są wliczone. Agregaty zawierają każdą politykę i każdy wynik, hashe całego raw są w `raw-manifest.json`. Pełne raw pozostają lokalne, wybrane rzeczywiste trajektorie w jednym `selected-replays.json.gz`.

## 12. Ile kosztuje eksperyment?

CPU epizodów i audytów {a['Totals']['CpuSeconds']:.3f}s /6500s, prognozy {a['Totals']['Forecasts']}/1400000, kontakty {a['Totals']['Evaluations']}/125000000; światy /600. To ta sama definicja CPU co w133: pomiar natywnego epizodu kończy się przed kompresją raw. Osobny CPU generacji obejmuje także serializację w pętli; nie sumujemy go ponownie z CPU epizodów. Generacja≤600s, epizod≤30s. Przed generacją próg3800CPU/70Mkontaktów/800kprognoz chroni rezerwę2700CPU/55M/600k na selekcję i finał. Nie zwiększano limitów ani liczby generacji. Geometria statyczna ma osobno rozliczony CPU, nie jest ukrytą prognozą fizyki. Koszt raportowania Python/rysowania nie jest kosztem uczenia ani prognozy; szersze dostępne pomiary procesów zapisano oddzielnie w `validation.json`.

## 13. Co obecnie najbardziej ogranicza jazdę?

Miękka osiągalność w dalszej geometrii nie jest rozwiązaniem pełnego problemu sterowania. Optymistyczny obszar na zewnątrz może być nieosiągalny dynamicznie, a ekstrapolowane odpuszczenie i obserwowana authority nie gwarantują przyszłej przyczepności. H09 jest lokalnym certyfikatem kandydatów; późniejsze DNF pozostaje możliwe. Dodatkowa informacja nie usuwa konfliktu wag i nie uczy sama doboru skutecznego manewru. Wynik {a['Verdict']} wynika z jawnych kryteriów: `{json.dumps(a['Criteria'])}`.

Szczególnym ograniczeniem jest nieskalibrowany margines legacy opisany w punkcie2. Wyniki sportowe oceniamy na Native, ale nie ogłaszamy fizycznego dowodu dalszej osiągalności z tego marginesu. Uproszczony zakres kierownicy w pierwotnej telemetrii również wymagał odróżnienia od kontynuacji z pending; końcowy viewer stosuje tę drugą. Są to jawne ograniczenia i korekty diagnostyki, nie zmiany mechanizmu po finale.

## 14. Jeden kolejny eksperyment

Zamrozić mały zestaw stanów na prostej przed drugim łukiem i porównać profilową heurystykę z empiryczną tabelą osiągalności kilku istniejących manewrów, mierzoną w natywnych kontynuacjach przy tych samych wagach. Nie wydłużać H09 ani zmieniać opon. Sprawdzić przede wszystkim kalibrację reszty prędkości wobec późniejszego wyniku, zanim dodamy kolejne generacje.

## 15. Powtarzalność, replay i walidacja

`tools/run-track-awareness.ps1 -Data <nowy-katalog> -Raw <pusty-katalog> -Python <python>` odtwarza zamrożone etapy; odmawia nadpisania dowodów. C# testuje readonly geometrię, dokładne OFF, identyczne akcje/prognozy/klasy oraz odtworzenie CEM; Python kontroluje hashe, niezależność pul, pary i koszty; Node sprawdza rzeczywiste trajektorie i geometrię wybranego przejazdu. Dodatkowa warstwa `source-extensions.json` zachowuje dokładne stare źródła dla audytów130–133. Produkcyjna fizyka nie uległa zmianie.

Viewer: `/tools/replay-viewer/?case=track-awareness&group=N00` (dwa łuki), M00 (pierwszy), T06 (inna geometria), AB-P1/P2/P3 (sam moduł), timing-P3-020-drive (nieskuteczny manewr) i timing-P3-040-drive (skuteczny spośród badanych). Suwak obejmuje całość świata; prosta przed drugim łukiem zachowuje pending i stan. Różowy obszar to brzegi/profil wiedzy, zielone przekroje to początek/koniec nadchodzącego łuku. Wykres κ i osobne czasy żądania/aktywacji/wykonania zapobiegają pomyleniu znajomości geometrii z przyszłą trajektorią.

[Protokół](protocol.json) · [końcowe wagi](choices.json) · [wszystkie epizody](attempts.csv) · [pary czasów](paired-times.json) · [pary zachowania](behavior-pairs.json) · [pełne metryki jazdy](native-quality.json.gz) · [natywne replaye](selected-replays.json.gz) · [hash przed finałem](winner-lock.json) · [odtworzenie CEM przed finałem](pre-final-reproduction.json) · [integralność raw](raw-manifest.json) · [runner odtwarzający](../../../tools/run-track-awareness.ps1).

![Uczenie](learning.svg)

![Niezależne pary czasów](final.svg)

Dokładne wyniki lokalnej walidacji zapisano w `validation.json`. Potwierdzony wynik Windows Draft CI dla opublikowanego HEAD jest wskazany w opisie PR. Bez merge.
'''
    text = text.replace('Nowa wiedza jest eksperymentem domyślnie OFF.', 'Status **PROVISIONAL**, eksperyment domyślnie OFF.').replace('uzyskał24/24', 'uzyskał 24/24').replace('mają11/12', 'mają 11/12').replace('przy10 wspólnych', 'przy 10 wspólnych')
    (D/'README.md').write_text(text, encoding='utf-8')


if __name__ == '__main__':
    main()
