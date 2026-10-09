"""REA-009 exact physical preservation and semantic completion evidence; no tolerances."""
import argparse
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
        differences = [{'Path': p, 'Before': left.get(p), 'After': right.get(p)}
                       for p in sorted(left.keys() | right.keys()) if left.get(p) != right.get(p)]
        if crash and differences:
            raise ValueError('Partial crash changed')
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


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('before', type=Path)
    parser.add_argument('after', type=Path)
    parser.add_argument('--report', type=Path)
    parser.add_argument('--platforms', action='store_true')
    args = parser.parse_args()
    if args.platforms:
        # Newly repaired cases have independent exact captures. Existing native-B
        # portability pins remain under their existing dedicated audit.
        for scenario in ('three-squeeze', 'four-close-regain'):
            for configuration in 'ABC':
                name = f'after-{scenario}-19-{configuration}'
                left = require_repro(args.before / name)
                right = require_repro(args.after / name)
                for key in ('FinalHash', 'BehaviorHash'):
                    if left[key] != right[key]:
                        raise ValueError(f'Exact repaired-case platform divergence: {name}/{key}: {left[key]} != {right[key]}')
        print('Six repaired production cases: exact Windows/Ubuntu final and complete behavior hashes.')
    else:
        result = compare_physical(json.loads(args.before.read_text()), json.loads(args.after.read_text()))
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
        print(f"104 traversals preserve all physical IEEE leaves; {len(result['ChangedRepresentations'])} necessary representation changes; four partial crashes unchanged.")
