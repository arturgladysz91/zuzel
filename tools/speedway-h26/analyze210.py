"""Summarize original accepted samples without replacing controller or physics outputs."""
import argparse
import bisect
import collections
import csv
import gzip
import json
import math
from pathlib import Path


THRESHOLDS = (100, 105, 110, 115, 120)


def read(path):
    open_file = gzip.open if path.suffix == '.gz' else open
    with open_file(path, 'rt', encoding='utf-8-sig') as stream:
        return json.load(stream)


def write(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + '\n', encoding='utf-8', newline='\n')


def dot(a, b):
    return a['X'] * b['X'] + a['Y'] * b['Y']


def pose(rider):
    velocity, road = rider['Velocity'], rider['Road']
    forward, outward = dot(velocity, road['Tangent']), dot(velocity, road['Outward'])
    return dict(S=rider['TotalReferenceMeters'], Distance=rider['DistanceMeters'], Speed=rider['Speed'] * 3.6,
                X=rider['Position']['X'], Y=rider['Position']['Y'], Yaw=rider['AngularVelocity'],
                Beta=rider['SlideRadians'], Course=math.atan2(outward, forward),
                Outward=outward, Forward=forward, Offset=road['OffsetMeters'])


def contact(forces, power):
    return dict(RearUse=power['Wheel']['RearUtilization'], FrontLoad=forces['TyreLoads']['FrontNormalNewtons'],
                YawMoment=forces['NetTorque'], Steering=forces['SteeringAfter'],
                RateLimited=abs(forces['SteeringAfter'] - forces['SteeringRequest']) > 1e-5,
                AngleLimited=abs(forces['SteeringAfter']) >= math.radians(35) - 1e-5,
                Slip=power['Wheel']['InstantSlipRatio'], Fx=forces['RearLongitudinal'], Fy=forces['RearLateral'])


def model_series(run):
    samples, controls = [], []
    for row in run['Trace']:
        before = row['Before']
        p = pose(before['State']['Rider'])
        after = row['After']
        samples.append(dict(Time=before['State']['Time'], **p))
        command = row['Command']
        controls.append(dict(Time=before['State']['Time'], End=after['Time'], S=p['S'],
                             Requested=command['Selected']['Drive'], Active=before['Active']['Drive'],
                             RequestedSteer=command['Selected']['SteeringRadians'], Pending=before['Pending'],
                             Acceleration=row['Acceleration'], Sweep=after['Sweep'], Outer=after['Outer'],
                             Cap=command['Dominant'], **contact(row['Forces'], row['Power']), Pose=p))
    after = run['Trace'][-1]['After']
    last = samples[-1]
    distance = last['Distance'] + math.hypot(after['X'] - last['X'], after['Y'] - last['Y'])
    samples.append({**last, **{k: after[k] for k in ('Time', 'S', 'X', 'Y', 'Yaw', 'Beta')},
                    'Distance': distance, 'Speed': after['Speed'] * 3.6})
    return samples, controls


def native_series(archive):
    rider = archive['Run']['Riders'][0]
    decisions = archive['Decisions']
    samples = [dict(Time=s['Input']['Time'], **pose(s['Input']['Rider'])) for s in rider['Samples']]
    times = [d['State']['State']['Time'] for d in decisions]
    controls = []
    for a, b, saved in zip(samples, samples[1:], rider['Samples'][1:]):
        selected = decisions[max(0, bisect.bisect_right(times, a['Time'] + 1e-8) - 1)]['Action']
        forces, power = saved['Forces'], saved['Powertrain']
        controls.append(dict(Time=a['Time'], End=b['Time'], S=a['S'], Requested=selected['Drive'],
                             RequestedSteer=selected['SteeringRadians'], Active=saved['Input']['Active']['Drive'],
                             Pending=saved['Input']['Pending'], Sweep=saved['Certificate'], Outer=saved['Outer'],
                             Acceleration=(b['Speed'] - a['Speed']) / ((b['Time'] - a['Time']) * 3.6),
                             Cap=None, **contact(forces, power), Pose=a))
    return samples, controls


def lerp(a, b, fraction):
    return a + (b - a) * fraction


def crossing(samples, meters):
    for a, b in zip(samples, samples[1:]):
        if a['S'] <= meters <= b['S'] and a['S'] < b['S']:
            fraction = (meters - a['S']) / (b['S'] - a['S'])
            return {k: lerp(a[k], b[k], fraction) for k in a}
    raise ValueError(f'No crossing at S = {meters}')


def phase(s, track):
    for b in track['Bends']:
        if s < b['Entry']:
            return 'launch' if b['Visit'] == 1 else f'straight {b["Visit"] - 1}'
        if s < b['Exit']:
            return f'bend {b["Visit"]}'
    return 'straight 6'


def intervals(samples, finish, track, threshold):
    spans = []
    for a, b in zip(samples, samples[1:]):
        if a['Time'] >= finish:
            break
        if a['Speed'] <= threshold and b['Speed'] <= threshold:
            continue
        delta = b['Speed'] - a['Speed']
        start = (threshold - a['Speed']) / delta if a['Speed'] <= threshold else 0
        finish_fraction = (finish - a['Time']) / (b['Time'] - a['Time'])
        end = min((threshold - a['Speed']) / delta if b['Speed'] <= threshold else 1, finish_fraction)
        if end <= start:
            continue
        first = {k: lerp(a[k], b[k], start) for k in ('Time', 'S', 'Distance')}
        last = {k: lerp(a[k], b[k], end) for k in ('Time', 'S', 'Distance')}
        if spans and abs(first['Time'] - spans[-1]['End']['Time']) < 1e-8:
            spans[-1]['End'] = last
        else:
            spans.append(dict(Start=first, End=last))
    for span in spans:
        a, b = span['Start'], span['End']
        span.update(Seconds=b['Time'] - a['Time'], Meters=b['Distance'] - a['Distance'],
                    PhaseFrom=phase(a['S'], track), PhaseTo=phase(b['S'], track))
    return spans


def performance(samples, controls, finish, track):
    def segment(start, end):
        rows = [r for r in controls if start <= r['S'] < end and r['Time'] < finish]
        if not rows:
            return {}
        seconds = [min(finish, r['End']) - r['Time'] for r in rows]
        dt = sum(seconds)
        def mean(k):
            return sum(t * r[k] for t, r in zip(seconds, rows)) / dt
        def totals(k):
            return {str(label): sum(t for t, r in zip(seconds, rows) if r[k] == label)
                    for label in sorted({r[k] for r in rows if r[k] is not None})}
        return dict(Seconds=dt, RequestedMean=mean('Requested'), ActiveMean=mean('Active'),
                    RequestedFull=sum(t for t, r in zip(seconds, rows) if r['Requested'] >= .99),
                    ActiveFull=sum(t for t, r in zip(seconds, rows) if r['Active'] >= .99),
                    ActiveZero=sum(t for t, r in zip(seconds, rows) if r['Active'] <= 1e-6),
                    AccelerationMean=mean('Acceleration'), AccelerationMax=max(r['Acceleration'] for r in rows),
                    RearUseMax=max(r['RearUse'] for r in rows), FrontLoadMin=min(r['FrontLoad'] for r in rows),
                    YawMomentMax=max(abs(r['YawMoment']) for r in rows), RateLimited=totals('RateLimited').get('True', 0),
                    AngleLimited=totals('AngleLimited').get('True', 0), SweepMin=min(r['Sweep'] for r in rows),
                    PendingMax=max(len(r['Pending']) for r in rows), DominantCaps=totals('Cap'),
                    MaxPendingRemaining=max((p['ActivationAt'] - r['Time'] for r in rows for p in r['Pending']), default=0),
                    PeakKph=max(r['Pose']['Speed'] for r in rows), MaxAbsBeta=max(abs(r['Pose']['Beta']) for r in rows),
                    MaxAbsCourse=max(abs(r['Pose']['Course']) for r in rows), MaxAbsYaw=max(abs(r['Pose']['Yaw']) for r in rows))
    bends = []
    goal = track['LapMeters'] * 3
    for i, bend in enumerate(track['Bends']):
        entry, apex, exit_at = (bend[k] for k in ('Entry', 'Apex', 'Exit'))
        next_entry = track['Bends'][i + 1]['Entry'] if i + 1 < len(track['Bends']) else goal
        point = {k: crossing(samples, s) for k, s in (('Entry', entry), ('Apex', apex), ('Exit', exit_at),
                 ('Exit20', min(exit_at + 20, next_entry)), ('NextEntry', next_entry))}
        for row in (point['Entry'], point['Exit']):
            j = min(bisect.bisect_right([r['Time'] for r in controls], row['Time']) - 1, len(controls) - 1)
            row.update({k: controls[j][k] for k in ('Requested', 'Active', 'RequestedSteer', 'Steering', 'Pending',
                       'RearUse', 'FrontLoad', 'YawMoment', 'Acceleration', 'Sweep', 'Outer')})
        core = [r for r in controls if exit_at + 12 <= r['S'] <= min(exit_at + 54, next_entry) and r['Time'] < finish]
        core_reserve = dict(YawRadPerSecond=min(.23-abs(r['Pose']['Yaw']) for r in core),
                            CourseDegrees=min(4-abs(math.degrees(r['Pose']['Course'])) for r in core),
                            OutsideBandMeters=min(1.8-r['Outer'] for r in core)) if core and i < 5 else None
        bends.append(dict(Visit=i + 1, **point, Corner=segment(entry, exit_at), Straight=segment(exit_at, next_entry),
                          CoreReserveApprox=core_reserve,
                          BendSeconds=point['Exit']['Time'] - point['Entry']['Time'],
                          StraightSeconds=point['NextEntry']['Time'] - point['Exit']['Time'],
                          MinCornerKph=min(s['Speed'] for s in samples if entry <= s['S'] <= exit_at)))
    finish_speed = crossing(samples, goal)['Speed']
    return dict(Finish=finish, PeakKph=max(finish_speed, *(s['Speed'] for s in samples if s['Time'] <= finish)),
                AcceptedGridPeakKph=max(s['Speed'] for s in samples),
                LapSplits=[crossing(samples, track['LapMeters'] * lap)['Time'] for lap in (1, 2, 3)],
                Bends=bends, Throttle=segment(0, goal), Thresholds={str(k): intervals(samples, finish, track, k) for k in THRESHOLDS})


def divergence(model, archive):
    points, _ = model_series(model)
    times = [p['Time'] for p in points]
    pairs = []
    for decision in archive['Decisions']:
        state = decision['State']['State']
        i = bisect.bisect_left(times, state['Time'] - 1e-8)
        if i >= len(points) or abs(times[i] - state['Time']) > 1e-7:
            continue
        a, b = points[i], pose(state['Rider'])
        pairs.append(dict(Time=state['Time'], XY=math.hypot(a['X'] - b['X'], a['Y'] - b['Y']),
                          S=abs(a['S'] - b['S']), SpeedMps=abs(a['Speed'] - b['Speed']) / 3.6,
                          Yaw=abs(a['Yaw'] - b['Yaw']), Course=abs(a['Course'] - b['Course'])))
    return dict(Pairs=pairs, Max={key: max(p[key] for p in pairs) for key in ('XY', 'S', 'SpeedMps', 'Yaw', 'Course')},
                FinishDelta=archive['Result']['Finish'] - model['Finish'])


def plot(output, track, baseline, selected):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size': 9, 'axes.spines.top': False, 'axes.spines.right': False})
    fig, axes = plt.subplots(3, 1, figsize=(12.6, 7.5), sharex=True, layout='constrained')
    for run, label, color in ((baseline, '#209 reference', '#626D83'), (selected, '#210 selected', '#007A73')):
        points, actions = model_series(run)
        axes[0].plot([p['S'] for p in points], [p['Speed'] for p in points], label=label, color=color)
        axes[1].plot([p['S'] for p in actions], [p['Active'] for p in actions], color=color, alpha=.85)
        axes[2].plot([p['S'] for p in actions], [p['Sweep'] for p in actions], color=color)
    for ax in axes:
        for bend in track['Bends']:
            ax.axvspan(bend['Entry'], bend['Exit'], color='#CEE4E3', alpha=.32, linewidth=0)
        ax.grid(axis='y', alpha=.2)
    for bend in track['Bends']:
        axes[0].text(bend['Apex'], 8, str(bend['Visit']), ha='center', color='#44605D')
    axes[0].axhline(110, linestyle=':', color='#BC6941', linewidth=1)
    axes[0].set_ylabel('Speed (km/h)')
    axes[0].legend(loc='lower right', frameon=False, ncol=2)
    axes[1].set_ylabel('Active throttle')
    axes[2].axhline(.5, linestyle=':', color='#BC6941', linewidth=1)
    axes[2].set(ylabel='Swept clearance (m)', xlabel='Track reference distance (m)', ylim=(0, 3))
    fig.suptitle('Study #210   •   Nominal complete MODEL race', fontweight='bold', x=.014, ha='left')
    fig.savefig(output / 'race-distance.png', dpi=155)
    plt.close(fig)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('evidence', type=Path)
    ap.add_argument('--plot', action='store_true')
    args = ap.parse_args()
    evidence = args.evidence
    track = read(evidence / 'reference/frozen-reference.json')['Track']
    baseline = read(evidence / 'reference/nominal-trace.json.gz')
    results = {'Arms': {}, 'MODEL': {}, 'Native': {}}
    results['Arms']['A'] = performance(*model_series(baseline), baseline['Finish'], track)
    for path in sorted((evidence / 'arm-traces').glob('*.json.gz')):
        run = read(path)
        results['Arms'][path.name[0]] = performance(*model_series(run), run['Finish'], track)
    for path in sorted((evidence / 'official').glob('*.json.gz')):
        model = read(path)
        fixture = model['Fixture']
        if not model['Complete']:
            results['MODEL'][fixture] = {k: model[k] for k in ('Fixture', 'Stop', 'Failure', 'Contact')}
            continue
        results['MODEL'][fixture] = performance(*model_series(model), model['Finish'], track)
        archive_path = evidence / 'native' / ('H29-P-' + fixture + '.json.gz')
        if archive_path.exists():
            archive = read(archive_path)
            result = archive['Result']
            results['Native'][fixture] = {'Result': result}
            if result['Finished']:
                results['Native'][fixture].update(performance(*native_series(archive), result['Finish'], track),
                                                  Divergence=divergence(model, archive))
    write(evidence / 'race-metrics.json', results)
    with (evidence / 'bend-metrics.csv').open('w', encoding='utf-8', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['group', 'run', 'bend', 'entry_kph', 'apex_kph', 'min_corner_kph', 'exit_kph', 'exit20_kph',
                         'next_entry_kph', 'bend_s', 'straight_s', 'straight_req', 'straight_active', 'straight_ax_max',
                         'straight_rear_use_max', 'straight_front_load_min', 'straight_steer_rate_s', 'straight_yaw_moment_max'])
        for group, runs in results.items():
            for name, run in runs.items():
                for row in run.get('Bends', []):
                    s = row['Straight']
                    writer.writerow([group, name, row['Visit'], row['Entry']['Speed'], row['Apex']['Speed'],
                                     row['MinCornerKph'], row['Exit']['Speed'], row['Exit20']['Speed'], row['NextEntry']['Speed'],
                                     row['BendSeconds'], row['StraightSeconds'], s['RequestedMean'], s['ActiveMean'],
                                     s['AccelerationMax'], s['RearUseMax'], s['FrontLoadMin'], s['RateLimited'], s['YawMomentMax']])
    if args.plot:
        figures = evidence / 'figures'
        figures.mkdir(exist_ok=True)
        plot(figures, track, baseline, read(evidence / 'official/C.json.gz'))


if __name__ == '__main__':
    main()
