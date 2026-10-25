"""Describe sealed #133 evidence. This program never runs or selects a policy."""
import argparse, csv, gzip, hashlib, importlib.util, json, math, os, statistics, struct
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
D = ROOT / 'docs/experiments/rider-decision-optimization'
spec = importlib.util.spec_from_file_location('throttle_report', ROOT/'tools/full-throttle-report.py')
old = importlib.util.module_from_spec(spec)
spec.loader.exec_module(old)
read = lambda p: json.loads(Path(p).read_bytes())
sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()
median = lambda xs: statistics.median(xs) if xs else None
percent = lambda x: 'brak wspólnych ukończeń' if x is None else f'{x*100:+.2f}%'
table = old.table
TERMS = ['Progress', 'Direction', 'Yaw', 'Throttle', 'SteeringChange', 'GeometrySpeed', 'FutureClearance']
GEOMETRY = read(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')


def write(name, value):
    (D/name).write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False)+'\n', encoding='utf-8')


def archive(name):
    plain, packed = D/(name+'.json'), D/(name+'.json.gz')
    data = plain.read_bytes() if plain.exists() else gzip.decompress(packed.read_bytes())
    packed.write_bytes(gzip.compress(data, compresslevel=9, mtime=0))
    write(name+'-archive.json', dict(OriginalSha256=hashlib.sha256(data).hexdigest().upper(), CompressedSha256=sha(packed), UncompressedBytes=len(data), Scope='Complete original bytes, unabridged.'))
    return json.loads(data)


def audit_report():
    states = archive('cost-audit')
    archive('causal-forecasts')
    differences = []
    for state in states:
        chosen = next(a for a in state['Actions'] if a['Original']['Action'] == state['Original']['Action'])
        if chosen['Original']['Action']['Drive'] != 0:
            continue
        candidates = [a for a in state['Actions'] if a['Original']['Action']['Drive'] > 0 and a['Original']['Safety'] == chosen['Original']['Safety'] and a['Original']['Progress'] > chosen['Original']['Progress'] and a['Original']['Score'] > chosen['Original']['Score']]
        if not candidates:
            continue
        alternative = min(candidates, key=lambda a: a['Original']['Score'])
        deltas = {k: alternative['Costs'][k]-chosen['Costs'][k] for k in TERMS}
        differences.append(dict(Run=state['Run'], Time=state['Input']['Time'], Phase=state['Phase'], Safety=chosen['Original']['Safety'], Original=chosen['Original']['Action'], Alternative=alternative['Original']['Action'], PredictedProgressGain=alternative['Original']['Progress']-chosen['Original']['Progress'], Deltas=deltas, ScoreDelta=alternative['Original']['Score']-chosen['Original']['Score'], Dominant=max(deltas, key=deltas.get), B1=state['B1'], B2=state['B2']))
    summaries = []
    for phase in ['straight', 'entry1', 'middle1', 'exit1', 'preparation2', 'bend2']:
        rows = [r for r in differences if r['Phase'] == phase]
        summaries.append(dict(Phase=phase, States=len(rows), Dominant=dict(Counter(r['Dominant'] for r in rows)), MedianDeltas={k: median([r['Deltas'][k] for r in rows]) for k in TERMS}))
    legacy = json.loads(gzip.decompress((D.parent/'full-throttle-learning/throttle-audit.json.gz').read_bytes()))
    coast = [r for r in old.cost_differences(legacy) if r['Run'].startswith('diag-A') and r['Chosen']['Drive'] == 0 and r['Alternative'] == 'positive']
    historical = dict(CoastWithSameSafetyPositive=len(coast), DirectionCostHigher=sum(r['DirectionDelta'] > 0 for r in coast), MedianDirectionDelta=median([r['DirectionDelta'] for r in coast]), Source='Original #132 diagnostic A records; exact original definition, not the stricter greater-progress subset in the new audit.')
    trials = read(D/'causal-trials.json')
    for trial in trials:
        if trial.get('Missing'):
            continue
        state = next(s for s in states if s['Run'] == trial['Source'] and abs(s['Input']['Time']-trial['Time']) < 1e-8)
        trial['IsOfflineB1Choice'] = state['B1'] == trial['Alternative']
        trial['FullTimeGain'] = trial['Baseline']['Native']['GateSeconds']-trial['Trial']['Native']['GateSeconds'] if trial['Trial']['Native']['Completed'] else None
    write('cost-differences.json', differences)
    write('phase-cost-summary.json', summaries)
    write('historical-cost-reproduction.json', historical)
    write('causal-analysis.json', trials)
    objective_comparison = []
    for variant in ['B1', 'B2']:
        choices = []
        for state in states:
            a, b = [next(x['Original'] for x in state['Actions'] if x['Original']['Action'] == action) for action in [state['Original']['Action'], state[variant]]]
            choices.append((a, b))
        objective_comparison.append(dict(Variant=variant, States=len(states), Changed=sum(a['Action'] != b['Action'] for a, b in choices), SafetyClassChanged=sum(a['Safety'] != b['Safety'] for a, b in choices), CoastToPositiveSameClass=sum(a['Action']['Drive'] == 0 and b['Action']['Drive'] > 0 and a['Safety'] == b['Safety'] for a, b in choices), CoastToPositiveGreaterProgress=sum(a['Action']['Drive'] == 0 and b['Action']['Drive'] > 0 and b['Progress'] > a['Progress'] for a, b in choices), MedianProgressDelta=median([b['Progress']-a['Progress'] for a, b in choices]), MedianFutureClearanceDelta=median([b['FutureClearance']-a['FutureClearance'] for a, b in choices]), MedianEndSpeedDelta=median([b['ExitSpeed']-a['ExitSpeed'] for a, b in choices]), MedianAbsoluteDirectionDelta=median([abs(b['DirectionError'])-abs(a['DirectionError']) for a, b in choices]), TerminalThreatChoices=sum(b['FutureClearance'] < .2 for _, b in choices), Scope='Offline reranking of the exact same held forecasts on all380 historical states; medians include unchanged decisions. No full-run or continuation safety claim.'))
        changed = [(a, b) for a, b in choices if a['Action'] != b['Action']]
        objective_comparison[-1]['ChangedDecisionMedians'] = {key: median([value(b)-value(a) for a, b in changed]) for key, value in [('ProgressMeters', lambda c: c['Progress']), ('FutureClearanceMeters', lambda c: c['FutureClearance']), ('EndSpeedMetersPerSecond', lambda c: c['ExitSpeed']), ('AbsoluteDirectionRadians', lambda c: abs(c['DirectionError']))]}
    write('offline-objective-comparison.json', objective_comparison)
    return states, summaries, historical, trials


def paired(final):
    result = []
    for suite in ['single', 'two']:
        groups = {e['Variant'].removesuffix('-'+suite): e for e in final if e['Variant'].endswith('-'+suite)}
        for new, evaluation in groups.items():
            if not new.startswith('133-'):
                continue
            for ref in ['130-H06', '131-H09', '132-H09']:
                baseline = groups[ref]
                assert evaluation['Scenarios'] == baseline['Scenarios']
                pairs = [dict(Scenario=n, Old=a['Native']['GateSeconds'], New=b['Native']['GateSeconds'], Ratio=b['Native']['GateSeconds']/a['Native']['GateSeconds']-1) for n, a, b in zip(evaluation['Scenarios'], baseline['Results'], evaluation['Results']) if a['Native']['Completed'] and b['Native']['Completed']]
                result.append(dict(Suite=suite, Reference=ref, Compared=new, Pairs=pairs, MedianRatio=median([p['Ratio'] for p in pairs]), MaximumRatio=max([p['Ratio'] for p in pairs], default=None)))
    return result


def first_whole_gate(run):
    """Read the first-bend whole-footprint crossing from actual native samples.

    Same unsteered mechanical capsules and +3m straight gate as the C# audit.
    This is a measurement only, not a boundary certificate or a resimulation.
    """
    segment = next(s for s in GEOMETRY['segments'] if s['index'] == 4)
    start = segment['sections'][0]
    f32 = lambda x: struct.unpack('f', struct.pack('f', x))[0]
    tangent = (-start['outward']['y'], start['outward']['x'])
    point = (f32(start['reference']['x']+3*tangent[0]), f32(start['reference']['y']+3*tangent[1]))
    tangent = tuple(map(f32, tangent))
    reference = segment['startReferenceMeters']+3
    for sample in run['Replay']['Samples']:
        rider = sample['Input']['Rider']
        if rider['TotalReferenceMeters'] < reference:
            continue
        angle = (rider['Heading']+math.pi) % (2*math.pi)-math.pi
        c, s = math.cos(angle), math.sin(angle)
        clearance = []
        for capsule in GEOMETRY['capsules']:
            distances = []
            for end in ['start', 'end']:
                local = capsule[end]
                x = rider['Position']['X']+c*local['x']-s*local['y']
                y = rider['Position']['Y']+s*local['x']+c*local['y']
                distances.append((x-point[0])*tangent[0]+(y-point[1])*tangent[1])
            clearance.append(min(distances)-capsule['radiusMeters'])
        if min(clearance) >= 0:
            return sample
    return None


def native_quality(run):
    q = old.quality(run)
    samples = run['Replay']['Samples']
    prep = [s for s in samples if s['Input']['Rider']['Road']['Segment'] == 4]
    after = next((s for s in samples if s['Input']['Rider']['Road']['Segment'] >= 5), None)
    q['PreparationSeconds'] = after['Input']['Time']-prep[0]['Input']['Time'] if prep and after else None
    q['FirstBendExitTime'] = prep[0]['Input']['Time'] if prep else None
    first_gate = first_whole_gate(run)
    q['FirstWholeGateTime'] = first_gate['Input']['Time'] if first_gate else None
    q['FirstWholeGateSpeed'] = first_gate['Input']['Rider']['Speed'] if first_gate else None
    q['FirstCoastRequestTime'] = next((d['Time'] for d in run['Replay']['Decisions'] if d['Action']['Drive'] == 0), None)
    q['FirstPositiveAfterFirstExit'] = next((d['Time'] for d in run['Replay']['Decisions'] if prep and d['Time'] >= prep[0]['Input']['Time'] and d['Action']['Drive'] > 0), None)
    exit_time = q['FirstBendExitTime']
    q['FirstActualIncreaseAfterExitDelay'] = next((b['Input']['Time']-exit_time for a, b in zip(samples, samples[1:]) if exit_time is not None and b['Input']['Time'] >= exit_time and b['Input']['Active']['Drive'] > a['Input']['Active']['Drive']+.01), None)
    gate = run['Metrics']['Native']['GateSeconds']
    q['DistanceThroughGate'] = next((s['Input']['Rider']['DistanceMeters'] for s in samples if gate is not None and s['Input']['Time'] >= gate-1e-8), None)
    return q


def behavior_pairs(p, qualities, aliases):
    lookup = {q['Name']: q for q in qualities}
    fields = ['FirstBendExitTime', 'FirstWholeGateTime', 'FirstWholeGateSpeed', 'PreparationSeconds', 'FirstCoastRequestTime', 'FirstActualIncreaseAfterExitDelay', 'DistanceThroughGate', 'CoastWithSafePositiveDriveCandidate', 'BetaAbove20Seconds', 'BetaAbove35Seconds', 'TyreLossModelWorkEstimateJoules', 'MaximumAbsoluteWheelSlipRatio']
    native_fields = ['MeanDrive', 'FullDriveFraction', 'MaximumBetaDegrees', 'LargeTrajectoryOscillations', 'MinimumClearanceMeters']
    metric_fields = ['MaximumYawRate', 'SteeringRequestTravelDegrees', 'EngineWorkJoules']
    out = []
    for suite, scenarios in [('single', p['FinalSingle']), ('two', p['FinalTwo'])]:
        for reference in ['130-H06', '131-H09', '132-H09']:
            rows = []
            for scenario in scenarios:
                name = scenario['Name']
                a, b = [lookup[f'final-{aliases[label]}-{name}'] for label in [reference, '133-B1']]
                if not a['Native']['Completed'] or not b['Native']['Completed']:
                    continue
                metrics = {key: (a[key], b[key]) for key in fields}
                metrics.update({key: (a['Native'][key], b['Native'][key]) for key in native_fields})
                metrics.update({key: (a[key], b[key]) for key in metric_fields})
                for bend in range(2 if suite == 'two' else 1):
                    for key in ['EntrySpeed', 'MinimumSpeed', 'ExitSpeed']:
                        metrics[f'Bend{bend+1}{key}'] = (a['Bends'][bend][key], b['Bends'][bend][key])
                rows.append(dict(Scenario=name, Metrics={key: dict(Old=x, New=y, Delta=y-x) for key, (x, y) in metrics.items() if x is not None and y is not None}))
            keys = list(dict.fromkeys(k for row in rows for k in row['Metrics']))
            out.append(dict(Suite=suite, Reference=reference, Compared='133-B1', CommonCompleted=len(rows), Pairs=rows, Medians={k: dict(Count=sum(k in r['Metrics'] for r in rows), **{field: median([r['Metrics'][k][field] for r in rows if k in r['Metrics']]) for field in ['Old', 'New', 'Delta']}) for k in keys}, Scope='Same common completed native states; independent medians, speed m/s, angles degrees, energy J, time s. No imputation when no event occurred. Count is per metric, since an event can be absent.'))
    return out


def first_bend_pairs(p, qualities, aliases):
    lookup = {q['Name']: q for q in qualities}
    result = []
    for ref in ['130-H06', '131-H09', '132-H09']:
        rows = []
        for scenario in p['FinalTwo']:
            a, b = [lookup[f'final-{aliases[label]}-{scenario["Name"]}'] for label in [ref, '133-B1']]
            x, y = a['FirstWholeGateTime'], b['FirstWholeGateTime']
            if x is not None and y is not None:
                rows.append(dict(Scenario=scenario['Name'], OldFirstGate=x, NewFirstGate=y, Ratio=y/x-1, OldTwoCompleted=a['Native']['Completed'], NewTwoCompleted=b['Native']['Completed']))
        result.append(dict(Reference=ref, Compared='133-B1', Pairs=rows, MedianFirstGateRatio=median([r['Ratio'] for r in rows]), Scope='FIRST BEND PREFIX ONLY in the continuous two-bend worlds; subsequent DNF flags retained. This is not a full two-bend improvement.'))
    return result


def csv_file(name, rows):
    keys = list(dict.fromkeys(k for row in rows for k in row))
    with (D/name).open('w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=keys)
        writer.writeheader()
        writer.writerows(rows)


def build_viewer(p, raw, final, pairs, aliases, analysis):
    load = lambda n: json.loads(gzip.decompress((raw/(n+'.json.gz')).read_bytes()))
    labels = {'130-H06': '#130 #1020 H06', '131-H09': '#131 #4061 H09', '132-H09': '#132 #6022 H09', '133-B1': '#133 B1 · nowy koszt + R1', '133-A0': '#133 A0 · stary koszt + R0', '133-A1': '#133 A1 · stary koszt + R1', '133-B0': '#133 B0 · nowy koszt + R0'}
    colors = dict(zip(labels, ['#ff9c86', '#ffd27b', '#a6b8ff', '#73dfdf', '#d99fe3', '#8bcf74', '#ffaade']))
    names = ['Y00', 'X00']
    two = next(r for r in pairs if r['Suite'] == 'two' and r['Reference'] == '131-H09' and r['Compared'] == '133-B1')
    if two['Pairs']:
        names += [min(two['Pairs'], key=lambda r: r['Ratio'])['Scenario'], max(two['Pairs'], key=lambda r: r['Ratio'])['Scenario']]
    for e in final:
        if e['Variant'].startswith('133-B1'):
            names += [n for n, r in zip(e['Scenarios'], e['Results']) if not r['Native']['Completed']][:1]
    selected, groups = {}, []
    for name in dict.fromkeys(names):
        two_bends = name.startswith('X')
        scenario = next(s for s in p['FinalTwo' if two_bends else 'FinalSingle'] if s['Name'] == name)
        runs = []
        for policy in aliases:
            if aliases[policy] != policy:
                continue
            if name != 'X00' and policy not in ['130-H06', '131-H09', '132-H09', '133-B1']:
                continue
            n = f'final-{policy}-{name}'
            selected[n] = load(n)
            runs.append(dict(Name=n, Policy=policy))
        alias_text = ', '.join(k+'='+v for k, v in aliases.items() if k != v)
        groups.append(dict(Id=name, Scenario=scenario, TwoBends=two_bends, Reasons=['nowy zamrożony finał', 'dwa pełne łuki' if two_bends else 'pierwszy łuk'], Scope='Rzeczywiste światy bez resetu; DNF pozostaje DNF. Dokładnie równoważne polityki korzystają z tego samego natywnego zapisu: '+(alias_text or 'brak aliasów')+'.', Runs=runs))
    for phase in ['straight', 'entry1', 'exit1', 'bend2']:
        base = 'causal-base-'+('131-Q0' if phase in ['straight', 'entry1'] else '132-Z01')
        trial = 'causal-'+phase
        a, b = load(base), load(trial)
        selected[base], selected[trial] = a, b
        labels[base], labels[trial] = 'Oryginalna autonomia', 'Jedna dodatnia komenda · '+phase
        colors[base], colors[trial] = '#ffd27b', '#73dfdf'
        groups.append(dict(Id='CAUSAL-'+phase, Scenario=a['Replay']['Scenario'], TwoBends=True, Reasons=['ta sama historia i pending', 'jeden manewr'], Scope='Diagnostyka przyczynowa: jedna istniejąca dodatnia akcja zamiast gaz0, reakcja0,24s; następnie pierwotna autonomia. Wybór ręczny według zamrożonej reguły, nie dowód działania wyuczonej B1. Prognozy nie są trajektoriami replaya.', Runs=[dict(Name=n, Policy=n) for n in [base, trial]]))
    for label in ['A', 'B']:
        n = f'profile-{label}-P1'
        selected[n] = load(n)
        labels[n], colors[n] = 'Ten sam #4061 · koszt '+label+' bez treningu', '#ffd27b' if label == 'A' else '#73dfdf'
    groups.append(dict(Id='SCORE-ONLY', Scenario=p['Diagnostic'][1], TwoBends=True, Reasons=['izolowana zmiana kosztu', 'DNF bez uczenia'], Scope='Te same wagi i akcje; różni je wyłącznie kształt kosztu kierunku. B uzyskuje inne lokalne decyzje, ale kończy DNF. H09 safe nie dowodzi możliwości dalszej kontynuacji.', Runs=[dict(Name=f'profile-{v}-P1', Policy=f'profile-{v}-P1') for v in ['A', 'B']]))
    gains = []
    for group in groups:
        if group['Id'] in names:
            a = selected[f'final-131-H09-{group["Id"]}']
            b = selected[f'final-{aliases["133-B1"]}-{group["Id"]}']
            policy = '133-B1'
        else:
            a, b = [selected[r['Name']] for r in group['Runs']]
            policy = group['Runs'][1]['Policy']
        rows = []
        for progress in range(30, 291, 5):
            cross = lambda r: next((s['Input'] for s in r['Replay']['Samples'] if s['Input']['Rider']['TotalReferenceMeters'] >= progress), None)
            x, y = cross(a), cross(b)
            if x and y:
                rows.append(dict(ReferenceMeters=progress, OldTime=x['Time'], NewTime=y['Time'], GainSeconds=x['Time']-y['Time'], OldXY=x['Rider']['Position'], NewXY=y['Rider']['Position']))
        gains.append(dict(Group=group['Id'], Policy=policy, Samples=rows, Description='Zysk drugiego wariantu względem pierwszego na tym samym postępie' if group['Id'] not in names else 'Zysk #133-B1 względem #131 na tym samym postępie', Resolution='First actual native sample crossing each5m; no interpolation. 0.01s time discretization. Partial DNF trace stops at its actual progress.'))
    write('time-gain.json', gains)
    write('representative-quality.json', [native_quality(r) for r in selected.values()])
    (D/'selected-replays.json.gz').write_bytes(gzip.compress(json.dumps(dict(Runs=selected), separators=(',', ':'), allow_nan=False).encode(), compresslevel=9, mtime=0))
    write('selected-manifest.json', dict(BundleSha256=sha(D/'selected-replays.json.gz'), Runs={n: dict(RawSha256=sha(raw/(n+'.json.gz')), NativeFingerprint=r['Metrics']['Native']['Fingerprint']) for n, r in selected.items()}))
    write('index.json', dict(Title='Rider Decision Optimization #133', Grade=analysis['Verdict'], DefaultRun='final-133-B1-X00', RawDirectory=str(raw), Labels=labels, Colors=colors, Horizons={k: .6 if k == '130-H06' else .9 for k in labels}, TimeGain='time-gain.json', TimeGainPolicy='133-B1', TimeGainDescription='Zysk czasu #133-B1 względem #131 na identycznym postępie toru', ComparisonCharts=True, PreferredPolicy='133-B1', Groups=groups, Charts=[['learning.svg', 'Oba seedy i koszty A/B'], ['final.svg', 'Nowy finał i czasy parami']]))
    return selected


def main():
    global D
    parser = argparse.ArgumentParser()
    parser.add_argument('raw')
    parser.add_argument('--data', default=str(D))
    parser.add_argument('--audit-only', action='store_true')
    args = parser.parse_args()
    D, raw = Path(args.data).resolve(), Path(args.raw).resolve()
    states, phases, historical, trials = audit_report()
    if args.audit_only:
        print(json.dumps(dict(Historical=historical, Phases=phases), indent=2))
        return
    p, attempts, final = [read(D/n) for n in ['protocol.json', 'attempts.json', 'final-results.json']]
    winner, aliases = read(D/'winner.json'), read(D/'final-aliases.json')
    choices = read(D/'ranked-choices.json')
    ranking_effects = []
    for arm in ['A', 'B']:
        a, b = [next(c for c in choices if c['Label'] == arm+str(i)) for i in [0, 1]]
        ranking_effects.append(dict(Arm=arm, R0Id=a['Id'], R1Id=b['Id'], SamePolicy=a['Policy'] == b['Policy'], Scope='Same frozen selection pool and native results. No new training or final-based choice.'))
    write('selection-ranking-effects.json', ranking_effects)
    pairs = paired(final)
    write('paired-times.json', pairs)
    summaries = [dict(Variant=e['Variant'], Completed=sum(r['Native']['Completed'] for r in e['Results']), Cases=len(e['Results']), DNF=dict(Counter(r['Native']['Outcome'] for r in e['Results'] if not r['Native']['Completed']))) for e in final]
    generations, seeds = [], []
    selection = read(D/'selection.json')
    for seed in p['Seeds']:
        for arm in ['A', 'B']:
            label = f'{arm}-s{seed}'
            gs = read(D/f'generations-{label}.json')
            for g in gs:
                ns = [r['Native'] for c in g['Candidates'] for r in c['Results']]
                best = next(c for c in g['Candidates'] if c['Id'] == g['BestId'])
                two = [r['Native'] for n, r in zip(best['Scenarios'], best['Results']) if n.endswith('-two')]
                generations.append(dict(Variant=label, Generation=g['Generation'], Cohort=g['Generation'] % 2, BestId=g['BestId'], Completed=sum(n['Completed'] for n in ns), TwoCompleted=sum(r['Native']['Completed'] for c in g['Candidates'] for n, r in zip(c['Scenarios'], c['Results']) if n.endswith('-two')), BestTimes=[r['Native']['GateSeconds'] for r in best['Results']], BestMedianTwo=median([n['GateSeconds'] for n in two]) if all(n['Completed'] for n in two) else None, DNF=dict(Counter(n['Outcome'] for n in ns if not n['Completed'])), MeanDrive=sum(n['MeanDrive']*n['DurationSeconds'] for n in ns)/sum(n['DurationSeconds'] for n in ns), FullFraction=sum(n['FullDriveFraction']*n['DurationSeconds'] for n in ns)/sum(n['DurationSeconds'] for n in ns), MaximumBeta=max(n['MaximumBetaDegrees'] for n in ns), MinimumClearance=min(n['MinimumClearanceMeters'] for n in ns), CpuSeconds=g['CpuSeconds'], Forecasts=g['Forecasts'], ContactEvaluations=g['Evaluations']))
            seeds.append(dict(Variant=label, TrainingCompleted=sum(r['Native']['Completed'] for g in gs for c in g['Candidates'] for r in c['Results']), Worlds=sum(len(c['Results']) for g in gs for c in g['Candidates']), Selection=[dict(Id=e['Id'], Completed=sum(r['Native']['Completed'] for r in e['Results']), Times=[r['Native']['GateSeconds'] for r in e['Results']]) for e in selection if e['Variant'] == label]))
    write('generations-summary.json', generations)
    write('seed-results.json', seeds)
    csv_file('generations.csv', generations)
    csv_file('attempts.csv', [{**(e.get('Metrics') or {}).get('Native', {}), **{k: e[k] for k in ['Number', 'Name', 'Category', 'NativeWorlds', 'CpuSeconds', 'Evaluations', 'Forecasts']}} for e in attempts])
    gas_rows, gas_totals = [], {}
    final_quality = []
    for e in attempts:
        if not e['NativeWorlds']:
            continue
        path = raw/(e['Name']+'.json.gz')
        if sha(path) != e['RawSha256']:
            raise ValueError('Raw mismatch '+e['Name'])
        run = json.loads(gzip.decompress(path.read_bytes()))
        n, gas = e['Metrics']['Native'], old.throttle(run)
        label = e['Name'].split('-g')[0] if e['Category'] == 'train-ab' else e['Name'].rsplit('-', 1)[0] if e['Category'] == 'final' else e['Category']
        gas_rows.append(dict(Name=e['Name'], Label=label, **gas))
        total = gas_totals.setdefault(label, dict(Worlds=0, FullDecisions=0, FullRequestsAfterActiveDrop=0, ActiveReopenings=0, Seconds=0, DriveIntegral=0, FullSeconds=0))
        total['Worlds'] += 1
        for key in ['FullDecisions', 'FullRequestsAfterActiveDrop', 'ActiveReopenings']:
            total[key] += gas[key]
        total['Seconds'] += n['DurationSeconds']
        total['DriveIntegral'] += n['MeanDrive']*n['DurationSeconds']
        total['FullSeconds'] += n['FullDriveFraction']*n['DurationSeconds']
        if e['Category'] == 'final':
            final_quality.append(native_quality(run))
    for total in gas_totals.values():
        total['MeanDrive'] = total['DriveIntegral']/total['Seconds']
        total['FullDriveFraction'] = total['FullSeconds']/total['Seconds']
    write('throttle-by-attempt.json', gas_rows)
    write('throttle-totals.json', gas_totals)
    write('final-quality.json', final_quality)
    write('behavior-pairs.json', behavior_pairs(p, final_quality, aliases))
    write('first-bend-pairs.json', first_bend_pairs(p, final_quality, aliases))
    write('raw-manifest.json', [dict(Name=e['Name'], Category=e['Category'], Sha256=e['RawSha256'], Bytes=e['RawBytes']) for e in attempts if e['NativeWorlds']])
    previous = read(D.parent/'full-throttle-learning/protocol.json')['Sources']
    physical = [n for n in previous if n not in ['Program.cs', 'FreeLearningController.cs', 'RobustLearningExperiment.cs']]
    unchanged = all(p['Sources'][n] == previous[n] for n in physical)
    preflight = read(D/'preflight.json')['Passed']
    ps = next(r for r in pairs if r['Suite'] == 'single' and r['Reference'] == '130-H06' and r['Compared'] == '133-B1')
    pt = next(r for r in pairs if r['Suite'] == 'two' and r['Reference'] == '131-H09' and r['Compared'] == '133-B1')
    n_single = next(r['Completed'] for r in summaries if r['Variant'] == '133-B1-single')
    n_two = next(r['Completed'] for r in summaries if r['Variant'] == '133-B1-two')
    valid = preflight and unchanged and read(D/'cost-audit-summary.json')['Passed'] and not any('invariant' in (e.get('Metrics') or {}).get('Native', {}).get('Outcome', '') for e in attempts)
    passed = valid and n_single == 24 and n_two == 12 and pt['MedianRatio'] is not None and pt['MedianRatio'] <= -.02 and ps['MedianRatio'] is not None and ps['MedianRatio'] <= .05 and ps['MaximumRatio'] <= .15
    analysis = dict(Verdict='PASS' if passed else 'PARTIAL' if valid else 'FAIL', NativeWorlds=sum(e['NativeWorlds'] for e in attempts), TrainingWorlds=sum(e['NativeWorlds'] for e in attempts if e['Category'] == 'train-ab'), CpuSeconds=sum(e['CpuSeconds'] for e in attempts), Forecasts=sum(e['Forecasts'] for e in attempts), ContactEvaluations=sum(e['Evaluations'] for e in attempts), Roles=dict(Counter(e['Category'] for e in attempts if e['NativeWorlds'])), PhysicalSourcesUnchanged=unchanged, PreflightPassed=preflight, Final=summaries, SinglePairedVs130Median=ps['MedianRatio'], SinglePairedVs130Maximum=ps['MaximumRatio'], TwoPairedVs131Median=pt['MedianRatio'], TwoPairedVs131Maximum=pt['MaximumRatio'])
    assert analysis['NativeWorlds'] <= 800 and analysis['CpuSeconds'] <= 7000 and analysis['Forecasts'] <= 1500000 and analysis['ContactEvaluations'] <= 135000000
    write('analysis.json', analysis)
    build_viewer(p, raw, final, pairs, aliases, analysis)
    plots(generations, pairs, summaries)
    report(p, analysis, winner, seeds, phases, historical, trials, pairs, gas_totals, final_quality)
    write('manifest.json', {f.name: sha(f) for f in sorted(D.iterdir()) if f.is_file() and f.name not in ['manifest.json', 'budget.lock', 'cost-audit.json', 'causal-forecasts.json']})
    print(json.dumps(analysis, ensure_ascii=False, indent=2))


def plots(generations, pairs, summaries):
    os.environ.setdefault('MPLCONFIGDIR', str(ROOT/'work/mpl-cache'))
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig, axes = plt.subplots(1, 3, figsize=(14, 4))
    palette = plt.rcParams['axes.prop_cycle'].by_key()['color']
    for i, label in enumerate(dict.fromkeys(g['Variant'] for g in generations)):
        color = palette[i % len(palette)]
        rows = [g for g in generations if g['Variant'] == label]
        axes[0].plot([g['Generation'] for g in rows], [g['TwoCompleted'] for g in rows], marker='o', label=label, color=color)
        axes[1].plot([g['Generation'] for g in rows], [g['CpuSeconds'] for g in rows], marker='o', label=label, color=color)
        for cohort in [0, 1]:
            same = [g for g in rows if g['Cohort'] == cohort]
            axes[2].plot([g['Generation'] for g in same], [g['BestMedianTwo'] for g in same], marker='o', linestyle='-' if cohort == 0 else '--', label=label+f' cohort{cohort}', color=color)
    axes[0].set(xlabel='Generation', ylabel='Two-bend completions /15')
    axes[1].set(xlabel='Generation', ylabel='CPU seconds')
    axes[2].set(xlabel='Generation (same cohort only)', ylabel='R0 winner median of all3 two-bend times s')
    axes[2].set_title('Solid J0–J3; dashed J4–J7', fontsize=9)
    axes[0].legend(fontsize=7)
    fig.tight_layout()
    fig.savefig(D/'learning.svg')
    plt.close(fig)
    fig, axes = plt.subplots(1, 2, figsize=(12, 5))
    axes[0].barh([s['Variant'] for s in summaries], [s['Completed']/s['Cases']*100 for s in summaries])
    axes[0].set(xlabel='Completion %', xlim=(0, 105))
    for ref in ['130-H06', '131-H09', '132-H09']:
        row = next(p for p in pairs if p['Reference'] == ref and p['Compared'] == '133-B1' and p['Suite'] == 'two')
        axes[1].plot([p['Ratio']*100 for p in row['Pairs']], marker='o', label=ref)
    axes[1].axhline(0, color='black', lw=.5)
    axes[1].set(xlabel='Common completed new cases', ylabel='B1 paired time change %')
    axes[1].legend()
    fig.tight_layout()
    fig.savefig(D/'final.svg')
    plt.close(fig)
    for path in D.glob('*.svg'):
        path.write_text('\n'.join(line.rstrip() for line in path.read_text(encoding='utf-8').splitlines())+'\n', encoding='utf-8')


def report(p, a, w, seeds, phases, historical, trials, pairs, gas, qualities):
    choices, scores = read(D/'ranked-choices.json'), read(D/'selection-time-scores.json')
    amendment = read(D/'budget-amendment.json') if (D/'budget-amendment.json').exists() else None
    budget_note = 'Brak korekty limitów.'
    if amendment:
        budget_note = ('Reprodukcja używa jawnie zatwierdzonego limitu generacji600s przed pierwszym światem. Pierwotny rekord zatrzymania i zgody pozostaje w oryginalnym archiwum #133.' if amendment.get('Replay') else
                       'Pierwsza ukończona generacja przekroczyła pomocniczy limit225s: '+str(amendment['ObservedCpuSeconds'])+'s CPU. Trening zatrzymano. Użytkownik jawnie zatwierdził zmianę wyłącznie limitu generacji do600s; wznowiono od następnej generacji. Pierwotny protokół i wszystkie dane zachowano.')
        budget_note += ' [Zapis zgody i hashów](budget-amendment.json). Limity łączne, fizyka, score, ranking i kryteria pozostały zamrożone.'
        if not amendment.get('Replay'):
            budget_note += ' [Pełne źródło sprzed korekty](budget-source-before.txt) ma dokładnie pierwotny hash z protokołu; można odtworzyć i sprawdzić wyłącznie zmianę wykonawczego limitu.'
    behavior = next(b for b in read(D/'behavior-pairs.json') if b['Suite'] == 'two' and b['Reference'] == '131-H09')
    matrix = []
    for choice in choices:
        score = next(s['Score'] for s in scores if s['Id'] == choice['Id'])
        evaluation = next(e for e in read(D/'selection.json') if e['Id'] == choice['Id'])
        completed = sum(r['Native']['Completed'] for r in evaluation['Results'])
        two_completed = sum(r['Native']['Completed'] for n, r in zip(evaluation['Scenarios'], evaluation['Results']) if n.endswith('-two'))
        matrix.append([choice['Label'], choice['Id'], f'{completed}/6', f'{two_completed}/4', percent(score['MedianTwo']), percent(score['MedianSingle']), percent(score['WorstRatio']), score['WithinWorstBound']])
    metadata_notice = '**Jawna naprawa metadanych po zakończeniu finału:** test odtworzył wszystkie100 rzeczywistych wektorów i elity R0, lecz wykrył osiem niepoprawnie zapisanych średnich generacji0 A/seeda1332026. Wznowienie po limicie225s używało tablicy zapisanej generacji jako mutowalnego stanu; następne aktualizacje nadpisały jej metadane. Prawdziwe losowania korzystały z poprawnego bieżącego stanu. [Oryginalny wadliwy plik bez skrótów](resume-archive-original.json.gz), [diagnostyka przed korektą](metadata-repair-diagnostic.txt), [pełne źródło przed naprawą](metadata-source-before.txt) i [rekord hashów, elit oraz ośmiu wartości](metadata-repair.json) pozostają dostępne. Skorygowano wyłącznie średnie tego checkpointu z pierwotnego startu i tych samych elit #10000/#10001; wszystkie parametry kandydatów, metryki, replaye, selekcja, finał i progi są identyczne. Dodano kopie tablic przy wznowieniu i test niezmienności archiwum. **Nie uruchomiono nowych światów ani dodatkowego treningu.** Ta naprawa archiwizacji jest oddzielna od eksperymentalnej zmiany funkcji oceny.' if (D/'metadata-repair.json').exists() else 'Ten przebieg nie wymagał korekty metadanych po wznowieniu.'
    text = f'''# Rider Decision Optimization #133 — {a['Verdict']}

Status **PROVISIONAL**, domyślnie OFF. Nowa metoda to score B + selekcja R1, główny kandydat **B1 #{w['Id']}**. Wynik finału nie zmienia tego wyboru.

## 1. Baza, HEAD i PR

Dokładna baza `{p['BaseHead']}`. [Draft PR #133](https://github.com/arturgladysz91/zuzel/pull/133), bez merge. Dokładny HEAD zmiany i wynik Windows CI są w opisie PR. [Protokół](protocol.json), [hash polityki i planu przed finałem](winner-lock.json). Historyczne dane #129–#132 zachowano.

## 2. Przyczyna częstego gaz0

Bezpieczeństwo jest porównywane przed wynikiem. Wśród legalnych kandydatów dodatni postęp konkuruje z kosztem kierunku χ względem przyszłej stycznej geometrii, yaw, gazu, zmiany kierownicy, prędkości geometrii i prześwitu. χ jest kierunkiem **prędkości**, ψ kierunkiem **ramy**, a β ich różnicą. Nie zakładamy ich równości. Gaz0 nie jest bezpośrednio nagradzany.

Kierunek porównuje χ na końcu H09 ze styczną położoną jeszcze dalej, o referencyjną prędkość końcową×`PreviewSeconds`. Zwiększenie prędkości przesuwa więc także punkt podglądu geometrii. Wyższy koszt direction nie oznacza automatycznie większego β ani gorszego ustawienia ramy. Audyt rozkłada tę rzeczywistą starą funkcję; nie utożsamia jej punktu odniesienia z udowodnioną optymalną linią.

## 3. Rozkład wszystkich siedmiu składników

Odtworzono {read(D/'cost-audit-summary.json')['States']} stanów czterech natywnych replayów #131/#132 i wszystkie kandydaty. Suma starych składników jest bitowo zgodna z oryginalnym score (maksymalny błąd0). [Pełny audyt gzip](cost-audit.json.gz), [hash oryginalnych bajtów](cost-audit-archive.json), [różnice siedmiu kosztów](cost-differences.json). W tabeli: gaz0, istniejący dodatni gaz tej samej klasy, większy prognozowany postęp, lecz gorszy stary wynik. Dominujący składnik to największa dodatnia różnica. Oddzielne mediany nie sumują się do mediany sumy.

{table(['Faza','Liczba stanów','Dominujące składniki','Mediana direction','Mediana progress'], [[r['Phase'],r['States'],r['Dominant'],r['MedianDeltas']['Direction'],r['MedianDeltas']['Progress']] for r in phases])}

Poza sześcioma żądanymi fazami zapisano jeszcze {sum(r['Phase']=='after2' for r in read(D/'cost-differences.json'))} takie decyzje po wyjściu z drugiego łuku; pozostają w pełnych danych, poza tabelą faz.

Reprodukcja definicji #132: **{historical['CoastWithSameSafetyPositive']}** zamknięć z dodatnią alternatywą tej samej klasy, **{historical['DirectionCostHigher']}** z wyższym kosztem kierunku; mediana **+{historical['MedianDirectionDelta']:.3f}**. [Odtworzenie](historical-cost-reproduction.json). To szersza populacja niż powyższy warunek większego postępu.

## 4. Czy stara decyzja była rzeczywiście wolniejsza?

Sześć pojedynczych zamian przy zachowaniu pełnej historii świata i pending. Mechanika jest identyczna przez aktywację, cały stan zawodnika identyczny ściśle przed0,24s; dalej wraca pierwotna autonomia. Czasy dotyczą całych dwóch łuków i1s kontroli. Większy postęp H09 nie wystarcza.

{table(['Faza','Obserwacja / aktywacja s','Stary pełny czas','Wynik zamiany','Pełny czas','Zysk s','Czy to wybór offline B1'], [[r['Phase'],f"{r['Time']:.2f} / {r['Activation']:.2f}",r['Baseline']['Native']['GateSeconds'],r['Trial']['Native']['Outcome'],r['Trial']['Native']['GateSeconds'],r['FullTimeGain'],r['IsOfflineB1Choice']] for r in trials if not r.get('Missing')])}

[Pełna analiza przyczynowa](causal-analysis.json) zawiera też bramkę pierwszego łuku. Dodatni zysk oznacza szybszy pełny przejazd. DNF nie ma porównywalnego czasu. Ręcznie wybrana według zamrożonej reguły alternatywa dowodzi możliwości innego skutku, a nie automatycznie skuteczności nowej funkcji. [Zapis prognoz](causal-forecasts.json.gz) jest osobno oznaczony jako prognoza, nigdy replay.

W tych sześciu próbach **żadna z trzech zamian poprawiających pełny czas nie jest wyborem offline B1**; jedyna zamiana zgodna z B1 (exit1) kończy DNF. To istotna negatywna przesłanka: zdiagnozowanie nieoptymalnego gaz0 nie dowodzi, że wybrany nowy kształt sam naprawia te decyzje. Wynik kontrolowanego treningu i niezależnego finału pozostaje konieczny.

## 5. Wybrana zmiana i porównanie B1/B2

Wybrano **jedną** zmianę: pseudo-Huber kierunku, δ10°. Dla SafeInHorizon, dodatniego postępu i przyszłego prześwitu≥0,2m: `20e²/(sqrt(1+(e/δ)²)+1)` zamiast `10e²`. Blisko zera ta sama krzywizna i interpretacja DirectionWeight; dalej wzrost liniowy, wciąż dodatni gradient kontroli. Przy zagrożeniu terminalnym lub innej klasie pozostaje stary kwadrat. Pozostałe sześć składników bez zmian.

B2 offline dodaje odległość reakcji z dodatniej stycznej prędkości końcowej, skalowaną prześwitem. To niewalidowana heurystyka kontynuacji, nie dowód bezpieczeństwa. B1 zmienia {read(D/'cost-audit-summary.json')['B1Changed']} decyzje, B2 {read(D/'cost-audit-summary.json')['B2Changed']} na tych samych prognozach. B2 nie był trenowany ani łączony z B1. [Wybór i uzasadnienie przed treningiem](score-choice.json).

{table(['Offline','Zmiany','Zmiany klasy','gaz0→dodatni','gaz0→dodatni z większym postępem','Mediana zmiany postępu m*','Mediana zmiany prędkości m/s*'], [[r['Variant'],r['Changed'],r['SafetyClassChanged'],r['CoastToPositiveSameClass'],r['CoastToPositiveGreaterProgress'],r['ChangedDecisionMedians']['ProgressMeters'],r['ChangedDecisionMedians']['EndSpeedMetersPerSecond']] for r in read(D/'offline-objective-comparison.json')])}

*Mediany tylko zmienionych decyzji. [Porównanie offline i stany końcowe](offline-objective-comparison.json) obejmuje też przyszły prześwit, kierunek, wybory przy zagrożeniu terminalnym i mediany wszystkich380 stanów (te wynoszą0 z powodu przewagi niezmienionych decyzji). Większy prognozowany postęp nie jest dowodem krótszego pełnego czasu.

## 6. Kontrola bez uczenia i deterministyczność

Te same wagi #4061 i rozszerzone akcje. **B bez retreningu kończy wszystkie cztery profile DNF na pierwszym łuku**; samo złagodzenie kierunku nie daje kontroli długoterminowej. Powtórzenia natywne A/B mają identyczne fingerprinty, a OFF odtwarza dokładny #132 Z01. [Preflight](preflight.json) potwierdza poprawność odtworzenia, nie niezawodność B. Nie zmieniono formuły po tych niepowodzeniach.

## 7. Oba seedy i wspólny CEM

Oba ramiona zaczynają od #131 #4061 H09, z gaz0/Drive/1 i utrzymaniem aktywnej komendy #132. Różni je wyłącznie koszt decyzji. Dwa seedy, pięć generacji, pięć polityk, cztery te same scenariusze:1 pojedynczy i3 ciągłe dwa łuki. **Ranking treningowy i elity to R0 #132 dla obu ramion**. Identyczne32 losowania/generację,2 elity, średnia0,4/0,6, std≥15% zakresu.

{table(['Ramię / seed','Ukończenia / światy','Selekcja L: kandydaci i ukończenia'], [[s['Variant'],f"{s['TrainingCompleted']}/{s['Worlds']}",'; '.join(f"#{e['Id']}: {e['Completed']}/6" for e in s['Selection'])] for s in seeds])}

[Archiwa wszystkich kandydatów](generations-summary.json), [koszty CSV](generations.csv), [oba seedy](seed-results.json). Nie uruchomiono dodatkowych generacji.

## 8. R0 kontra R1 bez kolejnych treningów

Na tej samej zamrożonej puli i sześciu stanach L stosujemy dwa rankingi. R0 jest dokładnym rankingiem #132. R1: ukończenia, ciężkość DNF, ukończenia dwóch łuków, ograniczenie najgorszej regresji15%, sparowana mediana dwóch łuków vs #4061 na L, osobno pojedynczy łuk i najgorsza para, potem prześwit i stan wyjścia. Brak par to brak danych, nie zero. Bez wymogu większego zapasu ani docelowego offsetu.

Przy równej liczbie ukończeń R1 sumuje zamrożoną ciężkość DNF:1 brak certyfikacji interwału,2 jazda wstecz,3 potwierdzone przekroczenie albo β45,4 pozostałe (np. brak postępu, timeout, limit CPU). Ukończenie zawsze ma0. To reguła selekcji, a nie zmiana certyfikatu lub fizycznych kryteriów DNF.

Baseline L #4061 ma rozszerzone akcje obu ramion. Referencja #131 w finale zachowuje oryginalny zestaw działań; nie utożsamiamy tych dwóch konfiguracji bez pomiaru.

Próg15% rozstrzyga dopiero po niezawodności: w jej równej grupie R1 najpierw preferuje spełniających limit, a jeśli wszyscy go przekraczają — najmniejsze przekroczenie. Pole `Limit15% = false` oznacza niespełnienie warunku, nawet gdy ten kandydat zostaje B1. Taki wybór pozostaje jawnym kandydatem eksperymentu, a nie deklaracją spełnienia wymagań szybkości. Nie wybieramy mniej niezawodnej polityki tylko dlatego, że jej nieliczne ukończenia są szybkie.

{table(['Grupa','ID','Ukończenia','W tym dwa','Mediana dwa vs baseline','Mediana jeden','Najgorsza para','Limit15%'], matrix)}

Czy R0/R1 wskazały ten sam wektor: {', '.join(r['Arm']+' — '+str(r['SamePolicy']) for r in read(D/'selection-ranking-effects.json'))}. [Efekt rankingu w tej puli](selection-ranking-effects.json). Zgodne wybory oznaczają brak zaobserwowanej zmiany selekcji, a nie dowód równoważności rankingów na dowolnych kandydatach.

[Identyczna pula](selection-candidates.json), [wyniki](selection.json), [miary czasowe](selection-time-scores.json), [wybory przed finałem](ranked-choices.json). K jest tutaj nazwany L0–L5. Koszt ogranicza pulę do maks.3 różnych wektorów na ramię/seed, wybranych wyłącznie na treningu: ostatni R0, powtarzalna kotwica R0 i reprezentant czasu ze wszystkich kandydatów parzystych generacji. **Nie oceniono wszystkich100 kandydatów na L**. Ta sama ograniczona pula służy obu rankingom; to ogranicza siłę wniosku o selekcji.

## 9. Zamrożone parametry

{table(['Parametr','Start #4061','B1 #'+str(w['Id'])], [[k,p['StartingPolicy'][k],v] for k,v in w['Policy'].items() if k!='Values'])}

Osiem tych samych parametrów i zakresów. DirectionWeight zachowuje lokalne znaczenie, duży błąd w safe ma inny kształt kosztu. [Polityka](winner.json), [plan](final-plan.json), [aliasy równoważnych konfiguracji](final-aliases.json). B1 nie podmieniono po zobaczeniu finału.

## 10. Dokładna liczba światów i limity

**{a['NativeWorlds']}** natywnych światów, w tym **{a['TrainingWorlds']}** treningowych. Kategorie: `{a['Roles']}`. Wszystkie rezerwowane przed utworzeniem. [Pełna księga](attempts.json), [CSV](attempts.csv), [surowe hashe](raw-manifest.json). Równoważne konfiguracje finału wykorzystują dokładnie ten sam zapis, jawnie mapowany, bez udawania dodatkowych światów.

Limit800 światów,7000s CPU epizodów i audytów,1,5mln prognoz,135mln ocen kontaktu. {budget_note}

## 11. Ukończenia i DNF nowego finału

{table(['Polityka / zestaw','Ukończenia','DNF'], [[s['Variant'],f"{s['Completed']}/{s['Cases']}",s['DNF']] for s in a['Final']])}

## 12. Czasy parami względem wszystkich referencji

{table(['Zestaw','Kandydat','Referencja','Wspólne ukończenia','Mediana','Najgorsza regresja'], [[r['Suite'],r['Compared'],r['Reference'],len(r['Pairs']),percent(r['MedianRatio']),percent(r['MaximumRatio'])] for r in pairs])}

[Każda para i jej czas](paired-times.json). DNF nie jest pomijany w tabeli ukończeń i nie dostaje fikcyjnego czasu. Pary czasowe warunkują wspólne ukończenie; nie dowodzą niezawodności tam, gdzie kandydat zawiódł.

Osobno pierwszy łuk wewnątrz ciągłych światów dwóch łuków, gdy oba motocykle rzeczywiście osiągnęły pierwszą bramkę całym obrysem. Dalszy DNF jest zachowany i **nie** staje się ukończeniem dwóch łuków:

{table(['Referencja','Pary pierwszej bramki','Mediana zmiany pierwszego czasu','Z tych par oba ukończyły dwa łuki'], [[r['Reference'],len(r['Pairs']),percent(r['MedianFirstGateRatio']),sum(p['OldTwoCompleted'] and p['NewTwoCompleted'] for p in r['Pairs'])] for r in read(D/'first-bend-pairs.json')])}

[Pierwszy łuk i dalsze losy każdej pary](first-bend-pairs.json).

## 13. Faktyczny gaz, zamknięcia i ponowne otwarcia

{table(['Grupa','Żądania1','Żądania1 przy aktywnym<1','Aktywne ponowne1','Średni gaz %','Czas gaz1 %'], [[k,t['FullDecisions'],t['FullRequestsAfterActiveDrop'],t['ActiveReopenings'],round(t['MeanDrive']*100,2),round(t['FullDriveFraction']*100,2)] for k,t in gas.items() if k!='diagnostic'])}

[Każda próba](throttle-by-attempt.json), [sumy ważone czasem](throttle-totals.json). Aktywny gaz może wrócić do1 wskutek wcześniej zaplanowanej komendy, gdy w obserwacji nadal było1, mimo oczekującego0. Oddzielamy to od nowego żądania1 po faktycznym spadku aktywnego gazu. Początkowe0,24s gaz1 jest w udziale czasu, a nie nowym żądaniem polityki.

## 14. Prędkość, linia i przygotowanie drugiego łuku

[Pełne miary każdego finału](final-quality.json) zawierają czas pierwszego wyjścia, czas segmentu przygotowania, wejście/minimum/wyjście obu łuków, pierwsze zamknięcie i przyspieszenie po wyjściu, drogę XY, oscylacje, skręt żądany/wykonany i gaz0 mimo safe+. To opis skutków, bez narzuconej reguły późnego hamowania lub offsetu. [Miary reprezentatywnych zapisów](representative-quality.json) obejmują też diagnostykę. Porównania wykresów w viewer pokazują wszystkie natywne konfiguracje na wspólnym zegarze, bez rozciągania czasu DNF.

Sparowane zachowanie B1 względem #131 na **{behavior['CommonCompleted']}** wspólnie ukończonych dwóch łukach (niezależne mediany; prędkości m/s, czas s, energia J):

{table(['Miara','Pary dla miary','Mediana #131','Mediana B1','Mediana różnicy par'], [[k,v['Count'],round(v['Old'],4),round(v['New'],4),round(v['Delta'],4)] for k,v in behavior['Medians'].items()])}

[Wszystkie pary zachowania względem #130/#131/#132](behavior-pairs.json). Droga jest mierzona do bramki, bez dodatkowej1s kontroli. Brak ponownego otwarcia po wyjściu pozostaje brakiem zdarzenia; nie imputujemy zera. `FirstWholeGateTime` mierzy całe mechaniczne kapsuły za bramką3m po łuku, tak samo jak C#. `FirstBendExitTime` oznacza wcześniejsze wejście punktu odniesienia motocykla w segment4. Oba czasy obejmują dojazd, a czas przygotowania tylko segment4.

## 15. Uślizg, yaw, energia i wiarygodność modelu

`final-quality.json` zawiera czas β>20°/35°, βmax, yaw, wheelspin, RPM, prace silnika, straty obwodowe i estymowane prace kontaktów bocznych oraz prześwity. Prace kontaktów są estymacją siła×przemieszczenie, nie osobnym prawem tarcia. DNF β45 pozostaje naruszeniem obwiedni i nie jest nazywany ukończeniem. Safety w H09 nie certyfikuje dalszego przejazdu; nieprzekroczenie granic przez obserwacje nie zamienia `uncertified interval` w ukończenie.

## 16. Niezależny holdout

Nowe24 Y i12 X, zamrożone przed treningiem. Inne dokładne stany niż #129–#132, w tym W/Z i U/V. Osobno J trening, P diagnostyka, L selekcja. Polityka, horyzont, score i ranking zamrożone hashem przed pierwszym finałowym światem. Finał nie steruje kontynuacją ani ponownym wyborem.

## 17. Koszt obliczeniowy

CPU epizodów i audytów **{a['CpuSeconds']:.3f}s**, prognozy **{a['Forecasts']}**, oceny kontaktu **{a['ContactEvaluations']}**; koszt audytu offline i sześciu prognoz przyczynowych też jest w księdze. To identyczna definicja księgowania natywnego CPU jak w #132: obejmuje prognozy i ocenę nowej funkcji; nie obejmuje późniejszego pakowania surowych replayów, raportowania i testów. `generations.csv` dodatkowo mierzy całe generacje z aktualizacją CEM i zapisem; tych wartości nie dodaje się ponownie do CPU epizodów. Rzeczywiste koszty nie są szacowane z czasu ściennego. Budżet testów technicznych/CI jest osobny i nie służy do selekcji.

## 18. Replaye i reprodukcja

[Viewer](../../../tools/replay-viewer/?case=rider-decision&run=final-133-B1-X00&group=X00) pokazuje #130/#131/#132/#133 na tych samych stanach, natywne ślady XY, porównawcze prędkość/gaz/skręt/β/prześwit, oraz różnicę czasu co5m postępu. Tylko rzeczywiste próbki; krok≤0,01s i dodatkowe zdarzenia natywne, bez interpolowanego XY. Dodatkowo SCORE-ONLY: sam nowy koszt i DNF; CAUSAL-straight: niepotrzebny gaz0 z pełnym zyskiem; CAUSAL-exit1: badana dodatnia alternatywa powoduje DNF, podczas gdy stare odpuszczenie kończy przejazd; X00: ciągły świat dwóch łuków. Wybrana polityka może zawieść; pełna jazda referencji pozostaje jawna.

Wybrane pełne replaye w [gzip](selected-replays.json.gz), [manifest](selected-manifest.json). Wszystkie surowe dane lokalne w `{p['RawDirectory']}`. `tools/run-rider-decision.ps1 -Data <nowy-folder> -Raw <pusty-folder>` odtwarza zamrożony harmonogram; skrypt odmawia nadpisania dowodów. Powtórzenie wymaga tego samego natywnego środowiska Windows/.NET8. Limity CPU są zależne od sprzętu; ewentualna zmiana wymaga jawnego rekordu przed wznowieniem.

## 19. Testy i ograniczenia

Testy celowane wykonano przed treningiem: dokładny OFF #132, koszt A, izolowana delta B i identyczne prognozy/klasy, legalność i rozłączność wejść, R1 parowanie oraz wspólny CEM. Po eksperymencie pełne standardowe testy Windows, Python i viewer oraz odtworzenie archiwum CEM; dokładne liczby i CI w opisie PR.

{metadata_notice}

Bez zmian Box2D, silnika, obu opon, masy, bezwładności, geometrii, CoreSim, prognozowania, klas bezpieczeństwa, H09, reakcji0,24s, interwału0,2s, β45, ±35°/90°/s. Nie dodano sił, trajektorii ani nagrody za gaz/uślizg. Zmiana kierunku jest heurystyką użyteczności; nie rozwiązuje formalnie terminalnej kontynuacji. Dwa seedy i ograniczona selekcja nie dowodzą uniwersalności. Zatrzymanie225s i zatwierdzona korekta są jawnym odstępstwem od początkowego planu wykonania, bez ukrywania lub kasowania prób.

## 20. Werdykt: {a['Verdict']}

Kryteria pozostają dokładnie24/24 i12/12, mediana dwóch≥2% szybciej vs #131, mediana pojedynczego≤5% i maksimum≤15% regresji vs #130, poprawna fizyka, obwiednia, niezależność i reprodukcja. B1: mediana dwa **{percent(a['TwoPairedVs131Median'])}**, pojedynczy **{percent(a['SinglePairedVs130Median'])}**, maksimum pojedynczy **{percent(a['SinglePairedVs130Maximum'])}**. [Wynik maszynowy](analysis.json). Brak spełnienia szybkości lub niezawodności daje PARTIAL; błędna fizyka, scoring, nieporównywalność albo przeciek daje FAIL. Nie zmieniono progów po wyniku.
'''
    (D/'README.md').write_text(text, encoding='utf-8')


if __name__ == '__main__':
    main()
