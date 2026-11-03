"""Read archived measurements only. Never invokes a planner or starts a world."""
from pathlib import Path
import gzip, hashlib, json, statistics, subprocess, sys

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'docs/experiments/instrumentation-cost'
REFERENCE = '85a5ee3295c2dd983fbacb21859649f794cc5ba5'
BASE = '2bb03d1e604a521202bd8f90aa70f8db0dc5632d'

def read(path):
    b = Path(path).read_bytes()
    return json.loads(gzip.decompress(b) if str(path).endswith('.gz') else b)

def save(name, value):
    (DOC / name).write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')

def summarize():
    plan = read(DOC / 'budget-plan.json')
    cases, calibration = [], []
    for case in plan['Cases']:
        d = read(DOC / 'micro' / (case['Label'] + '.json.gz'))
        ts = d['Trials']
        modes = {m: [t for t in ts if t['Mode'] == m] for m in ['Fine', 'Aggregate', 'Disabled']}
        pairs = []
        for i in range(2):
            f, a = modes['Fine'][i], modes['Aggregate'][i]
            before, after = [t['Ranges']['CpuSeconds']['conditional_continuations'] for t in [f, a]]
            pairs.append(dict(Pair=i+1, BeforeCpuSeconds=before, AfterCpuSeconds=after,
                              Reduction=1-after/before, WholeReviewReduction=1-a['CpuSeconds']/f['CpuSeconds']))
        exact = all(t['ExactArchived161'] and t['PhysicalHash'] == d['Context']['ArchivedHash']
                    and t['Nodes'] == 13 and t['Reserves'] == 9 and t['Independent']
                    and t['ConditionalCertificate'] for t in ts)
        counters = all(t['PhysicalCounters'] == ts[0]['PhysicalCounters'] and t['GuardCalls'] == ts[0]['GuardCalls'] for t in ts)
        metrics = {}
        for mode, trials in modes.items():
            metrics[mode] = dict(
                ReviewCpuSeconds=[t['CpuSeconds'] for t in trials],
                ReviewMedianCpuSeconds=statistics.median(t['CpuSeconds'] for t in trials),
                ConditionalCpuSeconds=[t['Ranges']['CpuSeconds']['conditional_continuations'] for t in trials],
                ConditionalMedianCpuSeconds=statistics.median(t['Ranges']['CpuSeconds']['conditional_continuations'] for t in trials),
                ConditionalAllocatedBytes=[t['Ranges']['AllocatedBytesOnCurrentThread']['conditional_continuations'] for t in trials],
                OutsideRolloutsCpuSeconds=[t['CpuSeconds']-sum(t['Ranges']['CpuSeconds'].values()) for t in trials],
                Profiler=trials[0]['Profiler'])
        c = {r['Kind']: r for r in d['Calibration']}
        calibration.append(dict(Label=d['Label'],
            NetClockCpuSeconds=c['clock']['CpuSeconds']-c['empty-loop']['CpuSeconds'],
            CpuMicrosecondsPerRead=(c['clock']['CpuSeconds']-c['empty-loop']['CpuSeconds'])/c['clock']['Iterations']*1e6,
            BytesPerRead=(c['clock']['AllocatedBytesOnCurrentThread']-c['empty-loop']['AllocatedBytesOnCurrentThread'])/c['clock']['Iterations'],
            NetScopeCpuSeconds=c['scope']['CpuSeconds']-c['empty-lease']['CpuSeconds'],
            CpuMicrosecondsPerScope=(c['scope']['CpuSeconds']-c['empty-lease']['CpuSeconds'])/c['scope']['Iterations']*1e6,
            BytesPerScope=(c['scope']['AllocatedBytesOnCurrentThread']-c['empty-lease']['AllocatedBytesOnCurrentThread'])/c['scope']['Iterations']))
        cases.append(dict(Label=d['Label'], Context=d['Context'] | {'Observed': {'Time': d['Context']['Observed']['Input']['Time']}},
                          ExactArchived161=exact, IdenticalCountersAndGuardCalls=counters,
                          GuardCalls=ts[0]['GuardCalls'], PhysicalCounters=ts[0]['PhysicalCounters'],
                          PreparationCpuSeconds=d['PreparationCpuSeconds'], Pairs=pairs, Modes=metrics,
                          GatePassed=exact and counters and all(p['Reduction'] >= .1 for p in pairs)))
    return dict(Base=BASE, ControllerReference=REFERENCE, Verdict='PARTIAL',
                Recommendation='Reject aggregation as a worthwhile planner optimization; keep OFF research harness and independent economical evidence format.',
                NativeGatePassed=all(c['GatePassed'] for c in cases), NativeWorlds=0,
                MicrobenchmarkBlocks=len(cases), ActualFullReviews=18,
                StandaloneCertificateProbes=0,
                CpuSecondsTotal=sum(read(DOC / ('cost-' + c['Label'] + '.json'))['CpuSeconds'] for c in cases),
                Cases=cases, Calibration=calibration,
                Limits='Three finite archived contexts, two repetitions per mode; cold first trial visible; process CPU quantization and GC/JIT noise. Not a native race speedup or an all-input equivalence proof.')

def seal():
    # Pin every pre-existing tree already protected by162, then162 itself.
    frozen = dict(read(ROOT / 'docs/experiments/s3-performance/evidence-manifest.json')['FrozenTrees'])
    for path in ['tools/s3-performance', 'docs/experiments/s3-performance']:
        frozen[path] = subprocess.check_output(['git', 'rev-parse', BASE+':'+path], cwd=ROOT, text=True).strip()
    paths = list(DOC.rglob('*')) + list((ROOT / 'tools/instrumentation-cost').rglob('*'))
    paths += [ROOT / 'tests/CoreSim.Tests/InstrumentationCost163Tests.cs', ROOT / 'tests/calibration/test_instrumentation_cost163.py']
    paths = [p for p in paths if p.is_file() and not any(x in p.parts for x in ['bin','obj','__pycache__']) and p.name != 'evidence-manifest.json']
    save('evidence-manifest.json', dict(Base=BASE, ControllerReference=REFERENCE, Verdict='PARTIAL', NativeWorlds=0,
        Files={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(paths)}, FrozenTrees=frozen))

if __name__ == '__main__':
    if '--seal' in sys.argv:
        seal()
    else:
        result = summarize()
        assert not result['NativeGatePassed'], 'Unexpected native admission: inspect evidence before proceeding'
        save('analysis.json', result)
        save('native-admission.json', dict(GatePassed=False, NativeWorldsStarted=0,
            RequiredReduction=.1, Reason='All three comparable conditional-continuation scopes must pass both counterbalanced pairs. This condition failed; no native development or held validation is admissible.',
            CaseResults=[{'Label':c['Label'],'PairReductions':[p['Reduction'] for p in c['Pairs']], 'Pass':c['GatePassed']} for c in result['Cases']]))
        print(json.dumps({k:v for k,v in result.items() if k not in ['Cases','Calibration']}, indent=2))
