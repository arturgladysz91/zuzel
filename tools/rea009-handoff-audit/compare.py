"""REA-009 exact physical preservation and semantic completion evidence; no tolerances."""
import argparse
import gzip
import hashlib
import importlib.util
import json
import struct
from pathlib import Path


def number(leaf):
    kind, bits = leaf.split(':')
    return struct.unpack('>f' if kind == 'float' else '>d', bytes.fromhex(bits))[0]


def compare_physical(before, after):
    if before.get('Schema') != 'rea009-physical-v1' or after.get('Schema') != before['Schema']:
        raise ValueError('Unknown capture schema')
    if before['Rows'].keys() != after['Rows'].keys() or len(after['Rows']) != 104:
        raise ValueError('The complete 104 traversal matrix is required')
    changes = []
    for name, right in after['Rows'].items():
        left = before['Rows'][name]
        physical = {p: v for p, v in left.items() if p.startswith('root.Physical.')}
        if physical != {p: v for p, v in right.items() if p.startswith('root.Physical.')}:
            first = next(p for p in sorted(left.keys() | right.keys()) if p.startswith('root.Physical.') and left.get(p) != right.get(p))
            raise ValueError(f'Unearned physical change: {name}/{first}: {left.get(first)} -> {right.get(first)}')
        incoming = number(right['root.Initial.TotalSegmentProgress'])
        outgoing = number(right['root.Final.TotalSegmentProgress'])
        crash = name.endswith('/True')
        if not crash and outgoing != int(incoming) + 1:
            raise ValueError('Completed traversal did not own exact next boundary')
        if right['root.Motion.Initial.CanonicalProgress'] != right['root.Initial.TotalSegmentProgress']:
            raise ValueError('Lost motion origin')
        if right['root.Motion.Final.CanonicalProgress'] != right['root.Final.TotalSegmentProgress']:
            raise ValueError('Unreconciled final motion')
        differences = [{'Path': p, 'BeforePresent': p in left, 'Before': left.get(p),
                        'AfterPresent': p in right, 'After': right.get(p)}
                       for p in sorted(left.keys() | right.keys())
                       if p not in left or p not in right or left[p] != right[p]]
        if crash and differences:
            raise ValueError('Partial crash changed')
        origin_preserved = left.get('root.Motion.Nodes.Count') == 1 and right.get('root.Motion.Nodes.Count') == 2
        allowed_state = {'root.Final.TotalSegmentProgress', 'root.Final.SegmentProgress',
                         'root.Final.SegmentIndex', 'root.Final.LapsCompleted', 'root.Status'}
        for delta in differences:
            path = delta['Path']
            if path in allowed_state or path.startswith('root.Motion.ExitBoundary.') or path in ('root.Motion.ExitBoundary',):
                continue
            if path.startswith('root.Motion.') and path.endswith(('.CanonicalProgress', '.SegmentProgress')):
                continue
            if origin_preserved and (path.startswith('root.Motion.Initial.') or path.startswith('root.Motion.Nodes')):
                continue
            raise ValueError(f'Unexpected representation change: {name}/{path}')
        if differences:
            changes.append({'Case': name, 'FirstDifference': differences[0], 'Differences': differences})
    return {'Schema': 'rea009-physical-comparison-v1', 'Traversals': 104,
            'PhysicalBitsExact': True, 'CrashesExact': True, 'ChangedRepresentations': changes}


def require_repro(directory):
    manifest = json.loads((directory / 'manifest.json').read_text())
    if manifest.get('Completed') is not True or manifest.get('Mode') != 'repro' or len(manifest['Cases']) != 1:
        raise ValueError('Fresh complete single-case reproduction required')
    case = manifest['Cases'][0]
    if case['Failed'] or not case['ReversedExact'] or not case['ObserverExact']:
        raise ValueError('Production failure or missing exact parity')
    return case


def compare_platforms(windows, ubuntu, expected=None, report_path=None):
    # Reuse the established exact type/missing/null/IEEE map representation.
    source = Path(__file__).resolve().parents[1] / 'rea001-safety-audit/compare.py'
    spec = importlib.util.spec_from_file_location('rea001_exact_maps', source)
    exact_maps = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(exact_maps)
    if expected is None:
        raw = (Path(__file__).resolve().parents[2] / 'tests/fixtures/rea009-native-b-portability.json').read_bytes().replace(b'\r\n', b'\n')
        if hashlib.sha256(raw).hexdigest() != '7f8f48637dceafbef2e33cb90e821c8df86e6a1e6809b4e2ab20d5006e044099':
            raise ValueError('REA-009 native-B evidence changed')
        expected = json.loads(raw)
    if expected.get('Schema') != 'rea009-native-b-portability-v1':
        raise ValueError('Unknown native-B evidence schema')
    cases, observed = {}, {}
    for scenario in ('three-squeeze', 'four-close-regain'):
        for configuration in 'ABC':
            name = f'after-{scenario}-19-{configuration}'
            left = require_repro(Path(windows) / name)
            right = require_repro(Path(ubuntu) / name)
            if any((row['Id'], row['Seed'], row['Configuration']) != (scenario, 19, configuration) for row in (left, right)):
                raise ValueError('Wrong original decimal reproduction case')
            if left['FinalHash'] != right['FinalHash']:
                raise ValueError(f'Exact repaired final production state differs: {name}')
            traces = []
            for root in (windows, ubuntu):
                with gzip.open(Path(root) / name / f'trace-{scenario}-19-{configuration}.json.gz') as stream:
                    traces.append(json.load(stream))
            differences = exact_maps.divergence_map(*traces)
            if configuration != 'B' and (left['BehaviorHash'] != right['BehaviorHash'] or differences):
                raise ValueError(f'Strict A/C behavior or complete trace differs: {name}')
            hashes = {'Windows': left['BehaviorHash'], 'Ubuntu': right['BehaviorHash']}
            if configuration == 'B' and hashes != expected['SummaryBehaviorHashes'][name]:
                raise ValueError(f'Exact per-OS native-B behavior changed: {name}')
            if differences:
                observed[name] = differences
            cases[name] = {'FinalStateExact': True, 'BehaviorHashes': hashes,
                           'CompleteTraceExact': not differences, 'DivergentLeaves': len(differences),
                           'TraceLeaves': {'Windows': len(traces[0]), 'Ubuntu': len(traces[1])}}
    if observed != expected['Divergences']:
        raise ValueError('Native-B divergence map changed (case/path/type/value/IEEE bits)')
    result = {'Schema': 'rea009-platform-comparison-v1', 'Cases': cases,
              'ObservedDivergences': observed,
              'Interpretation': 'Exact final production states and strict A/C full traces; separate exact native-B diagnostic map. Existing historical and REA-001 maps remain independent.'}
    if report_path is not None:
        Path(report_path).write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('before', type=Path)
    parser.add_argument('after', type=Path)
    parser.add_argument('--report', type=Path)
    parser.add_argument('--platforms', action='store_true')
    args = parser.parse_args()
    if args.platforms:
        compare_platforms(args.before, args.after, report_path=args.report)
        print('Six repaired final production states exact; strict A/C full traces exact; two native-B diagnostic maps exactly pinned (212 leaves).')
    else:
        result = compare_physical(json.loads(args.before.read_text()), json.loads(args.after.read_text()))
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
        print(f"104 traversals preserve all physical IEEE leaves; {len(result['ChangedRepresentations'])} necessary representation changes; four partial crashes unchanged.")
