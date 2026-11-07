"""Verify sealed #174 evidence without forecasting or executing native worlds."""
import copy
import gzip
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / 'docs/experiments/state-feasible-throttle'
TOOL = ROOT / 'tools/state-feasible-throttle'
sys.path.insert(0, str(ROOT / 'tools/three-lap-global-planning'))
from analyze172 import queue_audit, cross
from audit171 import trace_of


def read(path):
    return json.loads(path.read_bytes())


def sha(data):
    return hashlib.sha256(data).hexdigest()


def physical(sample):
    inp = sample['Input']
    return {**{k: sample[k] for k in ('CrankOmega', 'Forces', 'Powertrain')},
            'Input': {k: inp[k] for k in ('Time', 'Rider', 'Omega', 'Steering', 'Active')}}


def observation(sample, time):
    inp = copy.deepcopy(sample['Input'])
    inp['Time'] = time
    inp['NextReview'] = 0 if time == 0 else inp['NextReview']
    inp['Pending'] = [q for q in inp['Pending'] if q['ObservedAt'] < time - 1e-8]
    inp['Rider']['Drive'] = inp['Active']['Drive']
    return inp


def source_check():
    inputs = read(DOCS / 'INPUTS.json')
    assert inputs['base173'] == 'b306ffec897d11e291d3a90662c9d03b10285391'
    for name, digest in inputs['files'].items():
        assert sha((ROOT / name).read_bytes()) == digest, name
    subprocess.run([sys.executable, str(TOOL / 'generate174.py'), '--check'],
                   cwd=ROOT, check=True, capture_output=True, timeout=30)
    for line in (DOCS / 'SHA256SUMS').read_text(encoding='utf-8').splitlines():
        digest, name = line.split('  ', 1)
        assert sha((ROOT / name).read_bytes()) == digest, name


def audit():
    source_check()
    summary = read(DOCS / 'summary174.json')
    budget = read(DOCS / 'BUDGET.json')
    with ZipFile(DOCS / '174-diagnostics.zip') as diag:
        protocol = json.loads(diag.read('protocol174.json'))
        held = json.loads(diag.read('held174.json'))
        closed = json.loads(diag.read('native-closed.json'))
        assert sha(diag.read('held174.json')) == protocol['HeldSha256']
        assert len(held) == 8 and len({q['Id'] for q in held}) == 8
        assert closed['FullS3Gate'] is False and closed['HeldInputsUnconsumed']
        assert closed['DevelopmentWorlds'] == 3 and closed['ValidationWorlds'] == 0
        assert len(summary['validation']) == 8
        assert all(q['BASE'] == q['NEW'] == 'NOT_RUN' for q in summary['validation'])
        phases = [json.loads(diag.read(p + '.done.json')) for p in ('c1', 'x1', 'd1')]
        assert sum(q['NativeWorlds'] for q in phases) == 3
        assert all(q['Error'] is None for q in phases)
        rows = []
        for phase in ('c1', 'x1'):
            rows += [json.loads(q) for q in gzip.decompress(diag.read(phase + '-states.jsonl.gz')).splitlines()]
        assert len(rows) == 14 and sum(q['Evaluation']['Admitted'] for q in rows) == 10
        assert {q['Id'] for q in rows if not q['Evaluation']['Admitted']} == {'E41', 'E25', 'E12', 'XB'}
        assert sum(q['Evaluation']['Admitted'] and not q['Evaluation']['OldCanPrepare'] for q in rows) == 4
        for q in rows:
            a = q['Evaluation']
            if a['Admitted']:
                assert a['BaselinePath']['Reached'] and a['ProposedPath']['Reached']
                assert a['ProposedPath']['Seconds'] + .005 < a['BaselinePath']['Seconds']
                for path in ('BaselinePath', 'ProposedPath'):
                    p = a[path]
                    assert p['Pose'] >= .65 and p['Sweep'] >= .65 and p['Beta'] <= 3.141592653589793/4
                    assert p['Points'][-1]['Rider']['TotalReferenceMeters'] >= p['Goal']
        for phase in ('c1', 'x1', 'd1'):
            with ZipFile(io.BytesIO(diag.read(phase + '-source.zip'))) as sources:
                receipt = json.loads(diag.read(phase + '.started.json'))
                for name, digest in receipt['Sources'].items():
                    assert sha(sources.read(Path(name).name)).upper() == digest
        # The numerical admission mechanism and fork helpers stayed frozen through publication.
        with ZipFile(io.BytesIO(diag.read('d1-source.zip'))) as executed:
            for name in ['StateFeasibility174.cs', 'Native174.cs'] + [p.name for p in TOOL.glob('Fork174.*.cs')]:
                assert executed.read(name) == (TOOL / name).read_bytes(), name
        receipts = {n: json.loads(diag.read(n))['CpuSeconds'] for n in diag.namelist()
                    if n.endswith('.done.json') or n.endswith('.cpu.json')}
        receipts['archive-proof.json'] = json.loads(diag.read('archive-proof.json'))['CpuSeconds']
        assert receipts == budget['measured_stages_cpu_seconds']
        assert sum(receipts.values()) == budget['measured_cpu_seconds']
        assert budget['charged_cpu_seconds'] == budget['measured_cpu_seconds'] + budget['final_metadata_reserve_seconds']
        assert budget['charged_cpu_seconds'] < 300 and budget['native_worlds'] == 3
        assert json.loads(diag.read('archive-proof.json'))['NativeWorlds'] == 0
    old_path = ROOT / 'docs/experiments/three-lap-global-planning/172-validation-K4.zip'
    trials = []
    with ZipFile(DOCS / '174-native.zip') as outer, ZipFile(old_path) as bases:
        names = [n for n in outer.namelist() if n.endswith('.zip')]
        assert set(names) == {'d1-P7.zip', 'd1-P8.zip', 'd1-X4.zip'}
        for name in sorted(names):
            with ZipFile(io.BytesIO(outer.read(name))) as z:
                native = json.loads(z.read('native.json'))
                result = json.loads(z.read('result.json'))
                branch = json.loads(z.read('branch.json'))
                decisions = json.loads(z.read('decisions.json'))
                admissions = json.loads(z.read('admissions.json'))
            ident = branch['Id']
            with ZipFile(io.BytesIO(bases.read(branch['State']['Trial'] + '.zip'))) as z:
                original = json.loads(z.read('native.json'))
                old_decisions = json.loads(z.read('decisions.json'))
            assert result['ReachedStop'] and not result['S3'] and not result['Reserve']
            assert result['Crossings'] == 0 and result['FirstBadTime'] is None
            assert result['MinPose'] >= .5 and result['MinSweep'] >= .5 and result['MaxBetaDegrees'] <= 45
            assert len(admissions) == 1 and admissions[0]['Admitted'] and not admissions[0]['OldCanPrepare']
            a = admissions[0]
            assert a['Proposed']['Drive'] == .95 and a['Proposed']['SteeringRadians'] == a['Historical']['SteeringRadians']
            independent = next(q['Evaluation'] for q in rows if q['Id'] == ident)
            assert branch['BranchHash'] == a['StateSha256'] == independent['StateSha256']
            samples = native['Riders'][0]['Samples']; old = original['Riders'][0]['Samples']
            before = [q for q in samples if q['Input']['Time'] < a['Time'] + .24 - 1e-8]
            old_before = [q for q in old if q['Input']['Time'] < a['Time'] + .24 - 1e-8]
            assert [physical(q) for q in before] == [physical(q) for q in old_before]
            assert len(before) == branch['PhysicalPrefixSamples']
            for decision in decisions:
                t = decision['Time']
                if t > a['Time'] + 1e-8:
                    break
                actual = min(samples, key=lambda q: abs(q['Input']['Time'] - t))
                archived = min(old, key=lambda q: abs(q['Input']['Time'] - t))
                assert observation(actual, t) == observation(archived, t)
                assert actual['CrankOmega'] == archived['CrankOmega']
                if t < a['Time'] - 1e-8:
                    assert decision['Action'] == next(q['Action'] for q in old_decisions if abs(q['Time'] - t) < 1e-8)
            queue_audit(native, decisions)
            row = next(q for q in summary['development'] if q['id'] == ident)
            gain = cross(trace_of(original), result['Stop'])['t'] - cross(trace_of(native), result['Stop'])['t']
            assert abs(gain - row['local_gain_seconds']) < 1e-10 and gain > 0
            assert row['maximum_warm_ms'] > 200
            trials.append(ident)
    assert summary['verdict'] == 'PARTIAL'
    return dict(states=14, model_admissions=10, native_legal=3, native_worlds=3,
                full_s3_new=0, validation_worlds=0, heldout_unconsumed=True,
                charged_cpu_seconds=budget['charged_cpu_seconds'], trials=trials, verdict='PARTIAL')


if __name__ == '__main__':
    print(json.dumps(audit(), ensure_ascii=False, indent=2))
