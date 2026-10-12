"""REA-004A baseline and bounded experiment evidence; never reuses a D capture."""
import argparse
import collections
import gzip
import importlib.util
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('readiness_summary', root / 'tools/race-engine-readiness/summarize.py')
readiness = importlib.util.module_from_spec(spec); spec.loader.exec_module(readiness)


def make(baseline, contacts, probe):
    manifest = json.loads((baseline / 'manifest.json').read_text(encoding='utf-8-sig'))
    runs = json.load(gzip.open(baseline / 'runs.json.gz', 'rt', encoding='utf-8'))
    readiness.validate(manifest, runs)
    assert len(runs) == 342 and all(not r['Failed'] for r in runs)
    old = json.load(gzip.open(root / 'docs/calibration/rea-009-evidence/cases.json.gz', 'rt', encoding='utf-8'))
    hashes = {(r['Id'], r['Seed'], r['Configuration']): (r['FinalHash'], r['BehaviorHash']) for r in old}
    assert all(hashes[(r['Id'], r['Seed'], r['Configuration'])] == (r['FinalHash'], r['BehaviorHash']) for r in runs)
    frontiers = []
    for r in runs:
        groups = {(p['Step'], p['FrontierStartTimeSeconds'], tuple(p['FrontierRiderIds'])) for p in r['Pairs']}
        frontiers.append({'Id': r['Id'], 'Seed': r['Seed'], 'Configuration': r['Configuration'],
                          'Count': len(groups), 'Pairs': len(r['Pairs']),
                          'MaxPairsPerFrontier': max((sum(p['Step'] == step and p['FrontierStartTimeSeconds'] == time and tuple(p['FrontierRiderIds']) == ids
                              for p in r['Pairs']) for step, time, ids in groups), default=0),
                          'MaxRiders': max((len(ids) for _, _, ids in groups), default=0)})
    timing = [t for r in runs for t in readiness.impact_timing(r)]
    delays = [r['DelaySeconds'] for r in timing]
    contact_document = json.loads((contacts / 'evidence.json').read_text(encoding='utf-8-sig'))
    raw = [r for r in contact_document['Rows'] if not r['Reversed']]
    assert len(raw) == sum(r['Configuration'] == 'C' and bool(r['Pairs']) for r in runs)
    assert all(r['BaselineFinalMatches'] for r in raw)
    participants = [c for r in raw for c in r['Contacts']]
    history = [dict(Id=r['Id'], Seed=r['Seed'], Contact=c) for r in raw for c in r['Contacts'] if c['HistoricalParticipant']]
    grouped = [f for r in raw for f in r['Frontiers']]
    prototype = json.loads((probe / 'evidence.json').read_text(encoding='utf-8-sig'))
    experiments = [r for r in prototype['Rows'] if not r['Reversed']]
    return {
        'Schema': 'rea004a-summary-v1', 'StartingMain': manifest['SourceCommit'],
        'ProductionTree': '462d15a656f986bf01519c1a5dddaf8ff5f4649c', 'ProductionChanged': False,
        'REA004AAchieved': False, 'GeneralReplayEnabled': False,
        'Baseline': {'Cases': len(runs), 'Completed': len(runs), 'Failures': 0, 'FreshReverseChecks': len(runs),
                     'FreshObserverChecks': sum(r['ObserverExact'] is True for r in runs),
                     'OriginalREA009FinalAndBehaviorHashesExact': len(runs),
                     'ConfigurationCounts': dict(collections.Counter(r['Configuration'] for r in runs)),
                     'Violations': sum(len(r['Violations']) for r in runs),
                     'ExactAccumulationAndOwnership': readiness.check_accumulation(runs),
                     'AppliedPairs': sum(len(r['Pairs']) for r in runs), 'AppliedRiderConsequences': sum(len(r['Applied']) for r in runs),
                     'Frontiers': frontiers, 'TotalFrontiers': sum(r['Count'] for r in frontiers),
                     'MaxFrontiersPerHeat': max(r['Count'] for r in frontiers),
                     'MaxPairsPerFrontier': max(r['MaxPairsPerFrontier'] for r in frontiers),
                     'MaxFrontierRiders': max(r['MaxRiders'] for r in frontiers),
                     'EndpointFrontierDelaySeconds': {**readiness.quantiles(delays), 'Min': min(delays), 'Max': max(delays)},
                     'EndpointTimingRows': timing,
                     'RecoveryDurationSeconds': readiness.quantiles([readiness.f32(x['Duration']) for r in runs for x in r['Recoveries'] if x['Duration'] is not None]),
                     'Environment': manifest['Environment'], 'WallSeconds': manifest['WallSeconds']},
        'ContactReruns': {'Heats': len(raw), 'ReverseChecks': len(raw), 'FinalHashesMatchBaseline': len(raw),
                         'PairParticipantRecords': len(participants), 'HistoricalParticipants': history,
                         'UnavailableFrontiersByReason': dict(collections.Counter(f['Refusal'] for f in grouped)),
                         'EventSegmentTypes': dict(collections.Counter(c['EventSegmentType'] for c in participants))},
        'Prototype': {'Cases': len(experiments), 'ReversedCaptures': len(experiments),
                      'SupportedInitialExposures': [r['Name'] for r in experiments if r['Supported']],
                      'Unsupported': [{'Name': r['Name'], 'Reason': r['Reason']} for r in experiments if not r['Supported']],
                      'PartialTraversals': 0, 'GeneralHeatBudget': None,
                      'SupportedDomainBound': 'One initial exact common-time frontier, at most four production riders / six possible pairs; later fresh event is explicitly unsupported.',
                      'ReplayCalls': sum(r.get('PostImpactReplays', 0) for r in experiments)},
        'Interpretation': 'A/B/C remain endpoint modes. D is an isolated initial-frontier prototype, not a causal heat. Node samples and detector chord velocities are observations, not resumable production state.'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(); p.add_argument('baseline', type=Path); p.add_argument('contacts', type=Path); p.add_argument('probe', type=Path); p.add_argument('output', type=Path)
    a = p.parse_args(); report = make(a.baseline, a.contacts, a.probe)
    a.output.parent.mkdir(parents=True, exist_ok=True); a.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in report['Baseline'].items() if k not in ('Frontiers', 'EndpointTimingRows')}, indent=2))
