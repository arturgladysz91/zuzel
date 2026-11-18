"""Read accepted H25 states and project them into the existing Canvas replay format."""
from bisect import bisect_left, bisect_right
import gzip
import hashlib
import itertools
import json
import math
from pathlib import Path

FORMAT = 'stadium-h25-v1'
MIN_PASS_METERS = 2.1
HOLD_SECONDS = .30
INTENTS = {1: 'Podążanie', 2: 'Atak po wewnętrznej', 3: 'Atak po zewnętrznej',
           4: 'Przygotowanie wyjścia', 5: 'Obrona', 6: 'Wycofanie', 7: 'Odzyskiwanie kontroli'}


def load_gzip(path):
    with gzip.open(path, 'rt', encoding='utf-8') as stream:
        return json.load(stream)


def confirmed_passes(tracks, finishes, end):
    passes = []
    for (i, a), (j, b) in itertools.combinations(tracks.items(), 2):
        stop = min(finishes.get(str(i), end), finishes.get(str(j), end))
        best = 0
        last_side = -1
        first_holding = None
        crossing = None
        for k, (x, y) in enumerate(zip(a, b)):
            t = x['t']
            if t < .24:
                continue
            if t >= stop:
                break
            delta = x['reference'] - y['reference']
            side = 1 if delta > MIN_PASS_METERS else -1 if delta < -MIN_PASS_METERS else 0
            if side == best and side:
                last_side = k
            if best and crossing is None and last_side >= 0 and (delta > 0 if best < 0 else delta < 0):
                crossing = k
            if side == best:
                first_holding = None
                crossing = None
                continue
            if not side:
                first_holding = None
                continue
            if first_holding is None:
                first_holding = t
            if t + 1e-10 < first_holding + HOLD_SECONDS:
                continue
            if best and crossing is not None:
                x0, y0 = a[crossing], b[crossing]
                if math.hypot(x0['x']-y0['x'], x0['y']-y0['y']) < 5:
                    rider, opponent = (i, j) if side == 1 else (j, i)
                    passes.append({'RiderId': rider, 'OpponentId': opponent,
                                   'AtSeconds': x0['t'], 'ConfirmedAtSeconds': t})
            best, last_side, first_holding, crossing = side, k, None, None
    return sorted(passes, key=lambda p: p['AtSeconds'])


def export(native, geometry, *, recorded_passes=None, study=None, journal_override=None, exits=None):
    source = load_gzip(native)
    run = source['Run']
    end = run['EndSeconds']
    riders = run['Riders']
    if (len(riders) not in ((1, 2, 4) if study else (2, 4)) or run['Options']['RequiredLaps'] != 3
            or geometry['frameId'] != 'track-shape-101:progressive-b'
            or abs(run['LapMeters']-geometry['lapMeters']) > 1e-8):
        raise ValueError('Zapis nie odpowiada formatowi stadionu H25 lub geometrii progressive-b.')
    tracks, initial, measurements, meta, finishes, events = {}, [], [], [], {}, []
    expected_times = None
    observations = []
    journals = source['Journal'] if journal_override is None else journal_override
    for i, (rider, lean, journal) in enumerate(zip(riders, source['Lean'], journals, strict=True), 1):
        samples = rider['Samples']
        bike = samples[0]['Input']['Rider']
        if bike['Id'] != i or len(samples) != len(lean):
            raise ValueError('Brak zgodnych próbek pozycji lub przechyłu zawodnika.')
        initial.append(bike)
        times = [s['Input']['Time'] for s in samples]
        if times[0] != 0 or abs(times[-1]-end) > 1e-8 or (expected_times is not None and expected_times != times):
            raise ValueError('Zawodnicy nie mają wspólnej osi czasu.')
        expected_times = times
        ratio = 10-2*rider['Spec']['Setup']['Gearing']
        crossed = [c['Time'] for c in run['Crossings'] if c['RiderId'] == i]
        finish = rider['FinishSeconds']
        if len(crossed) != rider['CompletedLaps'] or finish is not None and (len(crossed) != 3 or crossed[-1] != finish):
            raise ValueError('Niespójne przekroczenia mety.')
        if finish is not None:
            finishes[str(i)] = finish
        measurements.append({'Measurement': {'RiderId': i, 'CrossingSeconds': crossed}})
        meta.append({'Id': i, 'Ratio': ratio, 'Gate': 'ABCD'[rider['Gate']], 'Outcome': rider['Outcome']})
        rows = tracks[i] = []
        for sample, roll in zip(samples, lean):
            state = sample['Input']
            v = state['Rider']
            if v['Id'] != i or abs(state['Time']-roll['Seconds']) > 1e-8:
                raise ValueError('Próbka zawiera obcego zawodnika lub niespójny czas.')
            if sample['Powertrain'] is not None:
                used = sample['Powertrain']['Engine']['OverallRatio']
                if abs(ratio-used) > 1e-6:
                    raise ValueError('Przełożenie zmieniło się w natywnym zapisie.')
            rows.append(dict(t=state['Time'], id=i, x=v['Position']['X'], y=v['Position']['Y'],
                vx=v['Velocity']['X'], vy=v['Velocity']['Y'], speed=v['Speed'], heading=v['Heading'],
                reference=v['TotalReferenceMeters'], offset=v['Road']['OffsetMeters'],
                target=v['TargetOffsetMeters'], drive=state['Active']['Drive'],
                activity=v['Activity'], steering=state['Steering'], lean=roll['ActualRadians'],
                beta=v['SlideRadians']))
            if study:
                rows[-1].update(yaw=v['AngularVelocity'], requestedDrive=sample['RequestedDrive'],
                    requestedSteering=sample['RequestedSteering'], pending=len(state['Pending']),
                    swept=sample['Certificate'], outer=sample['Outer'])
        if study:
            observations.append([
                dict(t=d['Time'], reference=d['Motion'], adjustment=d.get('Exit'), reason=d['Reason'],
                     target=d['Target'], replan=d['Replanned'], wide=d.get('Wide'))
                for d in journal if d.get('Motion') is not None])
        previous = None
        for decision in journal:
            choice = (decision['Intent'], decision['Rival'])
            if choice != previous and choice[0] in INTENTS and decision['Time'] < (finish or end):
                events.append(dict(t=decision['Time'], kind='tactic', rider=i, opponent=choice[1],
                                   label=INTENTS[choice[0]], detail=f"Cel poprzeczny {decision['Target']:.2f} m"))
            previous = choice
        for lap, time in enumerate(crossed[:-1] if finish is not None else crossed, 1):
            events.append(dict(t=time, kind='lap', rider=i, label=f'Okrążenie {lap} ukończone'))
    passes = confirmed_passes(tracks, finishes, end) if recorded_passes is None else recorded_passes
    points = [p for p in source.get('ContactPoints', []) if p['TotalNormalImpulseNewtonSeconds'] > 0]
    contacts = run['Contacts']
    for contact in contacts:
        if contact['Kind'] != 'begin':
            continue
        pair = {contact['RiderA'], contact['RiderB']}
        point = next((p for p in points if {p['RiderA'], p['RiderB']} == pair
                      and abs(p['TimeSeconds']-contact['TimeSeconds']) < 1e-8), None)
        if point is not None:
            contact['XY'] = point['PointMeters']
    payload = {'format': FORMAT, 'geometry': geometry, 'document': {'Run': {
        'Initial': {'Riders': initial}, 'RequiredLaps': 3, 'ContinueAfterFinish': True,
        'Result': {'TimeSeconds': end, 'Decisions': [], 'Contacts': contacts, 'FinishTimes': finishes},
        'Riders': measurements, 'Passes': passes, 'Consequences': source['Consequences'],
        'RaceEvents': events}}, 'riders': meta, 'outcome': run['Outcome'],
        'fingerprint': run['Fingerprint'], 'sourceSha256': hashlib.sha256(Path(native).read_bytes()).hexdigest(),
        'rows': list(itertools.chain.from_iterable(tracks.values()))}
    if study:
        payload['exitStudy'] = dict(name=study, decisions=observations,
                                    exits=exit_markers(tracks, geometry) if exits is None else exits)
    return payload



def exit_markers(tracks, geometry):
    """Label measured straightening and subsequent excess curvature on the same reference axis."""
    ends = [s['startReferenceMeters'] for i, s in enumerate(geometry['segments'])
            if i and s['type'] == 'Straight' and geometry['segments'][i-1]['type'] != 'Straight']
    results = []
    for states in tracks.values():
        markers = []
        s = [row['reference'] for row in states]
        chi = [math.atan2(row['vy'], row['vx']) for row in states]

        def curvature(k):
            i = max(0, bisect_right(s, s[k]-1, 0, k+1)-1)
            j = min(len(s)-1, bisect_left(s, s[k]+1, k))
            d = math.hypot(states[j]['x']-states[i]['x'], states[j]['y']-states[i]['y'])
            change = math.atan2(math.sin(chi[j]-chi[i]), math.cos(chi[j]-chi[i]))
            return change/d if d>.3 else 0

        for lap in range(3):
            for bend, e in enumerate(ends, 1):
                finish = e+lap*geometry['lapMeters']
                if not s[0] < finish < s[-1]:
                    continue
                k = bisect_left(s, finish)
                reference = range(bisect_left(s, finish-28), bisect_left(s, finish-17))
                level = sum(abs(curvature(i)) for i in reference)/max(1,len(reference))
                taper = next((i for i in range(bisect_left(s, finish-18), min(bisect_left(s,finish+8),len(s)-15))
                              if level>.004 and all(abs(curvature(j))<.57*level for j in range(i,i+15))),None)
                correction = next((i for i in range(k,bisect_left(s,finish+18))
                                   if curvature(i)<-.012 and abs(states[i].get('yaw',0))>.26),None)
                markers.append(dict(lap=lap+1,bend=bend,reference=finish,time=states[k]['t'],
                     taperStart=None if taper is None else states[taper]['t'],
                     secondTurn=None if correction is None else states[correction]['t']))
        results.append(markers)
    return results

def write_replay(path, payload):
    with gzip.open(path, 'wt', encoding='utf-8', compresslevel=4) as stream:
        json.dump(payload, stream, ensure_ascii=False, allow_nan=False, separators=(',', ':'))


def history_passes(history, arm):
    metrics = history.parent / 'wyniki.json'
    if not metrics.is_file():
        return None
    data = json.loads(metrics.read_text(encoding='utf-8'))[arm]
    if data['Digest'] != hashlib.sha256((history / (arm+'.json.gz')).read_bytes()).hexdigest():
        raise ValueError('Sumy kontrolne nagrania #191 oraz dziennika wyprzedzeń są różne.')
    return [{'RiderId': e['Rider'], 'OpponentId': e['Passed'], 'AtSeconds': e['Time'],
             'ConfirmedAtSeconds': e['ConfirmedAt']} for e in data['Overtakes']]

