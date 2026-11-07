"""One explicit packaging pass over closed #174 evidence. Never executes a controller."""
import argparse
import hashlib
import json
from pathlib import Path
import time
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED

ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / 'tools/state-feasible-throttle'
DOCS = ROOT / 'docs/experiments/state-feasible-throttle'


def load(path):
    return json.loads(path.read_bytes())


def write(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence', type=Path)
    parser.add_argument('unique_label')
    args = parser.parse_args(); e = args.evidence.resolve(); began = time.process_time()
    receipt = e / (args.unique_label + '.cpu.json')
    assert not receipt.exists() and load(e / 'native-closed.json')['FullS3Gate'] is False
    files = {p: digest for p, digest in load(ROOT / 'docs/experiments/throttle-decision-audit/INPUTS.json')['files'].items()}
    names = ['docs/experiments/throttle-decision-audit/' + n for n in
             ['173-diagnostics.zip', '173-native.zip', 'summary173.json', 'SHA256SUMS', 'INPUTS.json']]
    names += ['tools/three-lap-global-planning/analyze172.py', 'tools/whole-corner-planning/audit171.py',
              'tools/whole-corner-planning/report171.py', 'tools/autonomous-fast-corner/Runner.cs']
    for name in names:
        files[name] = hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
    write(DOCS / 'INPUTS.json', dict(base173='b306ffec897d11e291d3a90662c9d03b10285391',
                                   reference172='c5f0a4cc4de34fc4c8c47ef912cafabf34055f29', files=files))
    native = [p for p in e.iterdir() if p.name.startswith('d1-') and
              (p.name.endswith('.world-reserved.json') or p.name.endswith('-result.json') or
               p.name in ['d1-P7.zip', 'd1-P8.zip', 'd1-X4.zip', 'd1-native-protocol.json'])]
    with ZipFile(DOCS / '174-native.zip', 'w', compression=ZIP_DEFLATED) as z:
        for p in sorted(native):
            z.write(p, p.name, compress_type=ZIP_STORED if p.suffix == '.zip' else ZIP_DEFLATED)
    diagnostics = [p for p in e.iterdir() if p.is_file() and p not in native and p.name != '.lock']
    with ZipFile(DOCS / '174-diagnostics.zip', 'w', compression=ZIP_DEFLATED) as z:
        for p in sorted(diagnostics):
            z.write(p, p.name, compress_type=ZIP_STORED if p.suffix in ('.zip', '.gz') else ZIP_DEFLATED)
    write(receipt, dict(CpuSeconds=time.process_time() - began, NativeWorlds=0, Stage='closed evidence packaging'))
    stages = {p.name: load(p)['CpuSeconds'] for p in e.iterdir()
              if p.name.endswith('.done.json') or p.name.endswith('.cpu.json')}
    stages['archive-proof.json'] = load(e / 'archive-proof.json')['CpuSeconds']
    total = sum(stages.values())
    budget = dict(measured_stages_cpu_seconds=stages, measured_cpu_seconds=total,
                  final_metadata_reserve_seconds=1, charged_cpu_seconds=total + 1,
                  maximum_cpu_seconds=300, native_worlds=3, maximum_native_worlds=24,
                  development_worlds=3, integration_worlds=0, validation_worlds=0,
                  heldout_unconsumed=True, full_s3_gate=False,
                  excluded='Compilation, regression tests and CI; no new #174 native worlds or forecasts in verification',
                  reserve='One CPU-second conservatively charged for writing final small metadata and manifest after the packaging sample')
    assert total + 1 < 300
    write(DOCS / 'BUDGET.json', budget)
    with ZipFile(DOCS / '174-diagnostics.zip', 'a', compression=ZIP_DEFLATED) as z:
        z.write(receipt, receipt.name)
    # Expand decision table with all required state identifiers, exact actions and gate reasons.
    summary = load(DOCS / 'summary174.json')
    text = ['# Identyfikacja ocen #174', '', 'Wynik długiej oceny to MODEL. Natywne potwierdzenie: wyłącznie P7, P8, X4.', '',
            '| Stan | SHA256 | Gaz historyczny→propozycja | Skręt historyczny→propozycja [rad] | Historyczny gate | Short 95 | Nowa decyzja | CPU [ms] |',
            '|---|---|---|---|---|---|---|---:|']
    for r in summary['states']:
        a = r['Evaluation']; short = next(q for q in a['Short'] if q['Name'] == '95-fixed')
        text.append(f"| {r['Id']} | {a['StateSha256']} | {a['Historical']['Drive']:.9f}→{a['Proposed']['Drive']:.9f} | {a['Historical']['SteeringRadians']:.9f}→{a['Proposed']['SteeringRadians']:.9f} | {a['HistoricalGate']} | {'legalny MODEL' if short['Legal'] else 'odrzucony MODEL'} | {a['Reason']} | {a['CpuSeconds']*1000:.3f} |")
    (DOCS / 'STANY.md').write_text('\n'.join(text) + '\n', encoding='utf-8')
    sealed = [p for p in DOCS.iterdir() if p.is_file() and p.name != 'SHA256SUMS']
    sealed += [p for p in TOOL.iterdir() if p.is_file()]
    sealed += [ROOT / 'tests/calibration/test_state_feasible_throttle174.py', ROOT / '.github/workflows/study-174.yml']
    lines = [hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.relative_to(ROOT).as_posix() for p in sorted(sealed)]
    (DOCS / 'SHA256SUMS').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(json.dumps(dict(worlds=3, measured_cpu=total, charged_cpu=total+1, manifest_files=len(lines))))


if __name__ == '__main__':
    main()
