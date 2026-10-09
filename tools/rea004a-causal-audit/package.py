"""Package fresh REA-004A observations and paired performance; raw batch remains reproducible."""
import argparse
import gzip
import hashlib
import json
import shutil
import statistics
from pathlib import Path
from summarize import make, readiness


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + '\n', encoding='utf-8')


def compressed(path, value):
    path.write_bytes(gzip.compress(json.dumps(value, separators=(',', ':'), allow_nan=False).encode('utf-8'), mtime=0))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('baseline', type=Path); parser.add_argument('captures', type=Path); parser.add_argument('output', type=Path)
    args = parser.parse_args(); args.output.mkdir(parents=True, exist_ok=True)
    write(args.output / 'summary.json', make(args.baseline, args.captures / 'contacts', args.captures / 'probe'))
    runs = json.load(gzip.open(args.baseline / 'runs.json.gz', 'rt', encoding='utf-8'))
    compressed(args.output / 'cases.json.gz', [readiness.compact(r) for r in runs])
    shutil.copyfile(args.baseline / 'manifest.json', args.output / 'baseline-manifest.json')
    compressed(args.output / 'controlled-contact.json.gz', read(args.baseline / 'controlled-contact.json'))
    for mode in ('contacts', 'probe', 'witness', 'expected-failure'):
        compressed(args.output / (mode + '.json.gz'), read(args.captures / mode / 'evidence.json'))
    before, after = [read(args.captures / ('performance-' + mode + '.json')) for mode in ('before', 'after')]
    assert [m['Name'] for m in before['Measurements']] == [m['Name'] for m in after['Measurements']]
    comparison = []
    for left, right in zip(before['Measurements'], after['Measurements']):
        assert not left['Failed'] and not right['Failed'] and left['CountedWork'] == right['CountedWork']
        a, b = [statistics.median(s['Milliseconds'] for s in row['Samples']) for row in (left, right)]
        comparison.append({'Name': left['Name'], 'BaselineMedianMilliseconds': a, 'HeadMedianMilliseconds': b,
                           'ChangePercent': 100 * (b / a - 1), 'CountedWorkExact': True,
                           'BaselineMedianAllocatedBytes': statistics.median(s['AllocatedBytes'] for s in left['Samples']),
                           'HeadMedianAllocatedBytes': statistics.median(s['AllocatedBytes'] for s in right['Samples'])})
    write(args.output / 'performance.json', {'ProductionBefore': before, 'ProductionAfter': after,
          'ProductionComparison': comparison, 'IsolatedPrototype': read(args.captures / 'performance.json'),
          'Interpretation': 'Production source is unchanged. Timings are nine-sample observations, not a speedup claim. Prototype timings cover one initial exposure, never a D heat.'})
    shutil.copyfile(args.captures / 'local-comparison.json', args.output / 'local-comparison.json')
    files = {p.name: {'Bytes': p.stat().st_size, 'SHA256': hashlib.sha256(p.read_bytes()).hexdigest()}
             for p in sorted(args.output.iterdir()) if p.is_file() and p.name != 'files.json'}
    write(args.output / 'files.json', {'Schema': 'rea004a-files-v1', 'Files': files,
          'StartingMain': 'f40b2fd129124d969fd22cfe7767e2fb00bd651f', 'REA004AAchieved': False,
          'RawBatch': 'Reproduce runs.json.gz using the documented batch command. cases.json.gz uses the existing readiness compact() function; exact accumulation/ownership records are retained.'})
    print(json.dumps({'Files': {k: v['Bytes'] for k, v in files.items()}}, indent=2))
