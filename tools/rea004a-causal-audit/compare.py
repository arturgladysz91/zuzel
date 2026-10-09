"""Strict typed comparison for the isolated probe and production causal witness."""
import argparse
import json
from pathlib import Path


def capture(directory):
    result = {}
    for mode in ('probe', 'witness'):
        document = json.loads((Path(directory) / mode / 'evidence.json').read_text(encoding='utf-8-sig'))
        rows = document['Rows']
        for row in rows:
            key = (mode, row.get('Name', row.get('Id')), row.get('Seed', 19), row['Reversed'])
            if mode == 'probe':
                value = {'Seed': document['Seed'], 'Flags': document['Flags'], 'Supported': row['Supported'], 'Exact': row.get('Exact'), 'Reason': row.get('Reason'),
                         'ResolvePrivate': row.get('ResolvePrivate'), 'Uncommitted': row.get('Uncommitted')}
            else:
                value = {'ExactContacts': row['ExactContacts'], 'ExactVerified': row['ExactVerified'], 'Final': row['Final'], 'Frontiers': row['Frontiers']}
            if key in result:
                raise AssertionError(f'duplicate case {key}')
            result[key] = value
        for key, value in list(result.items()):
            if key[0] != mode or key[-1]:
                continue
            assert result[key[:-1] + (True,)] == value, f'input-order difference: {key}'
    names = {'start-rear-brush', 'start-rear-moderate', 'start-rear-lost-rhythm', 'start-rear-major-save',
             'start-rear-crash', 'interior-rear', 'near-end-rear', 'side-overlap-unsupported',
             'three-start-frontier', 'four-start-frontier', 'four-start-crash-stress', 'no-contact'}
    expected = {('probe', name, 19, reversed) for name in names for reversed in (False, True)}
    expected |= {('witness', 'control', 19, reversed) for reversed in (False, True)}
    assert set(result) == expected, 'incomplete or unexpected bounded case coverage'
    assert sum(v['Supported'] for k, v in result.items() if k[0] == 'probe') == 18
    return result


def compare(left, right=None):
    a = capture(left)
    if right is not None:
        b = capture(right)
        assert a == b, 'exact type/value/missing/null/IEEE platform difference'
    return {'Cases': len(a), 'TypedLeaves': sum(len(v.get(field) or {}) for v in a.values() for field in ('Exact', 'ExactContacts', 'ExactVerified')),
            'ReversalExact': True, 'PlatformsExact': right is not None, 'GeneralReplayEnabled': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('left'); parser.add_argument('right', nargs='?'); parser.add_argument('--report')
    args = parser.parse_args()
    report = compare(args.left, args.right)
    if args.report:
        Path(args.report).write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))
