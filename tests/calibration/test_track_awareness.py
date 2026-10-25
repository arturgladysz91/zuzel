"""Verify #134 sealed evidence without starting physical worlds."""
import gzip, hashlib, json, statistics, unittest
from collections import Counter
from pathlib import Path
from extended_source_guard import source133

ROOT = Path(__file__).resolve().parents[2]
D = ROOT/'docs/experiments/track-awareness-anticipatory-riding'
read = lambda n: json.loads((D/n).read_bytes())
sha = lambda b: hashlib.sha256(b).hexdigest().upper()


class TrackAwarenessEvidence(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.p, cls.a, cls.f = [read(n) for n in ['protocol.json', 'attempts.json', 'final-results.json']]
        cls.runs = json.loads(gzip.decompress((D/'selected-replays.json.gz').read_bytes()))['Runs']

    def test_exact_base_and_frozen_sources(self):
        self.assertEqual(self.p['BaseHead'], '5ed4499b2118ca8206bf8dcaf9741dc0790c01ac')
        for name, digest in self.p['Sources'].items():
            self.assertEqual(sha((ROOT/'tools/shared-world-speedway'/name).read_bytes()), digest, name)
        extensions = read('source-extensions.json')
        self.assertEqual(extensions['BaseHead'], self.p['BaseHead'])
        for name, item in extensions['Sources'].items():
            self.assertEqual(sha(source133(name)), item['BaseSha256'])
        historical = json.loads((D.parent/'rider-decision-optimization/ranked-choices.json').read_bytes())
        a0 = next(c for c in historical if c['Label'] == 'A0')
        self.assertEqual(a0['Id'], 12023)
        self.assertEqual(self.p['StartingPolicy'], a0['Policy'])

    def test_cost_ledger_includes_every_world_and_stays_in_frozen_limits(self):
        self.assertTrue(all(e['Complete'] for e in self.a))
        self.assertEqual(len(self.a), len({e['Name'] for e in self.a}))
        self.assertEqual([e['Number'] for e in self.a], list(range(1, len(self.a)+1)))
        for field, limit in [('NativeWorlds', 600), ('CpuSeconds', 6500), ('Forecasts', 1400000), ('Evaluations', 125000000)]:
            actual = sum(e[field] for e in self.a)
            self.assertLessEqual(actual, limit)
            self.assertEqual(actual, read('analysis.json')['Totals'][field])
        for category, limit in self.p['Limits'].items():
            self.assertLessEqual(sum(e['Category'] == category for e in self.a), limit)
        self.assertEqual(sum(e['NativeWorlds'] for e in self.a if e['Category'] == 'train-ab'), 240)
        self.assertEqual(sum(e['NativeWorlds'] for e in self.a if e['Category'] == 'geometry'), 5)
        self.assertEqual(sum(e['NativeWorlds'] for e in self.a if e['Category'] == 'diagnostic'), 31)
        self.assertEqual(len(read('raw-manifest.json')['Runs']), sum(e['NativeWorlds'] for e in self.a))

    def test_controlled_training_has_both_geometries_and_same_initial_population(self):
        total = 0
        for seed in self.p['Seeds']:
            arms = []
            for arm in ['A', 'B']:
                gs = read(f'generations-{arm}-s{seed}.json')
                self.assertEqual([g['Generation'] for g in gs], list(range(4)))
                for g in gs:
                    self.assertLessEqual(g['CpuSeconds'], 600)
                    self.assertEqual(len(g['Candidates']), 5)
                    for c in g['Candidates']:
                        self.assertEqual(len(c['Results']), 3)
                        self.assertEqual(sum(n.endswith('-two') for n in c['Scenarios']), 2)
                        self.assertTrue(any(n.startswith('egg-medium') for n in c['Scenarios']))
                        self.assertTrue(any(n.startswith('progressive-b') for n in c['Scenarios']))
                        total += len(c['Results'])
                arms.append(gs)
            self.assertEqual([c['Policy'] for c in arms[0][0]['Candidates']], [c['Policy'] for c in arms[1][0]['Candidates']])
            for arm in arms:
                self.assertEqual(arm[0]['Candidates'][0]['Policy'], self.p['StartingPolicy'])
        self.assertEqual(total, 240)

    def test_holdout_pools_and_geometries_are_independent(self):
        key = lambda c: tuple(c['Scenario'][k] for k in ['Speed', 'Offset', 'Start', 'OutwardSpeed', 'HeadingError', 'YawRate'])
        held = self.p['FinalSingle']+self.p['FinalTwo']+self.p['Transfer']
        other = self.p['Training']+self.p['Selection']+self.p['Diagnostic']
        self.assertEqual([len(self.p[k]) for k in ['FinalSingle', 'FinalTwo', 'Transfer']], [24, 12, 12])
        self.assertEqual(len({key(c) for c in held}), 48)
        self.assertFalse({key(c) for c in held} & {key(c) for c in other})
        self.assertEqual({c['Geometry'] for c in other}, {'progressive-b', 'egg-medium'})
        self.assertEqual({c['Geometry'] for c in self.p['Transfer']}, {'egg-asymmetric'})
        self.assertEqual({c['Geometry'] for c in self.p['FinalSingle']+self.p['FinalTwo']}, {'progressive-c'})
        for folder in ['rider-decision-optimization', 'full-throttle-learning', 'autonomous-speedway-extended-learning', 'robust-free-racing-learning']:
            p = json.loads((D.parent/folder/'protocol.json').read_bytes())
            for value in p.values():
                if isinstance(value, list) and value and isinstance(value[0], dict) and 'Speed' in value[0] and 'OutwardSpeed' in value[0]:
                    self.assertFalse({key(c) for c in held} & {tuple(s[k] for k in ['Speed', 'Offset', 'Start', 'OutwardSpeed', 'HeadingError', 'YawRate']) for s in value})

    def test_geometry_native_forecast_and_off_preflight(self):
        g = read('geometry-preflight.json')
        self.assertTrue(g['Passed'])
        self.assertEqual(len(g['Checks']), 5)
        for c in g['Checks']:
            self.assertTrue(c['Passed'])
            self.assertEqual(c['Native']['Native']['Outcome'], 'probe')
            self.assertEqual(c['Native']['Native']['Forecasts'], 0)
        p = read('preflight.json')
        for key in ['TrainingGatePassed', 'Off133Exact', 'NativeRepeatsExact', 'PredictorPassed', 'PerformanceGate']:
            self.assertTrue(p[key], key)
        prediction = json.loads(gzip.decompress((D/'predictor.json.gz').read_bytes()))
        self.assertTrue(prediction['Passed'])
        self.assertFalse(prediction['ThresholdsChanged'])
        self.assertEqual(len(prediction['Checks']), 4)
        for c in prediction['Checks']:
            self.assertGreaterEqual(c['Comparison']['Compared'], 91)
            self.assertTrue(c['Comparison']['Passed'])
        trials = read('reaction-continuations.json')
        self.assertEqual(len(trials), 16)
        self.assertTrue(all(t['ExactPrefix'] for t in trials))
        for t in trials:
            self.assertAlmostEqual(t['Activation']-t['Observation'], .24)

    def test_selection_and_winner_lock_precede_all_final_worlds(self):
        seal = read('winner-lock.json')
        self.assertEqual(seal['FinalWorlds'], 0)
        self.assertGreater(min(e['Number'] for e in self.a if e['Category'] == 'final'), seal['ReservationCount'])
        self.assertLess(max(e['Number'] for e in self.a if e['Category'] == 'selection'), min(e['Number'] for e in self.a if e['Category'] == 'final'))
        self.assertEqual(read('training-complete.json')['Worlds'], 240)
        self.assertEqual(read('training-complete.json')['FinalWorlds'], 0)

    def test_all_paired_times_recompute_and_failures_have_no_time(self):
        for row in read('paired-times.json'):
            group = {e['Variant']: e for e in self.f}
            a, b = [group[label+'-'+row['Suite']] for label in [row['Reference'], row['Compared']]]
            self.assertEqual(a['Scenarios'], b['Scenarios'])
            expected = [dict(Scenario=n, Old=x['Native']['GateSeconds'], New=y['Native']['GateSeconds'], Ratio=y['Native']['GateSeconds']/x['Native']['GateSeconds']-1) for n, x, y in zip(a['Scenarios'], a['Results'], b['Results']) if x['Native']['Completed'] and y['Native']['Completed']]
            self.assertEqual(row['Pairs'], expected)
            self.assertEqual(row['MedianRatio'], statistics.median([r['Ratio'] for r in expected]) if expected else None)
        for e in self.f:
            for m in e['Results']:
                if not m['Native']['Completed']:
                    self.assertIsNone(m['Native']['GateSeconds'])
        aliases = read('final-aliases.json')
        for label, target in aliases.items():
            for suite in ['single', 'two', 'transfer']:
                self.assertEqual(next(e['Results'] for e in self.f if e['Variant'] == label+'-'+suite), next(e['Results'] for e in self.f if e['Variant'] == target+'-'+suite))

    def test_selected_bundle_and_all_manifest_hashes(self):
        for name, digest in read('manifest.json').items():
            self.assertEqual(sha((D/name).read_bytes()), digest, name)
        manifest = read('selected-manifest.json')
        self.assertEqual(manifest['BundleSha256'], sha((D/'selected-replays.json.gz').read_bytes()))
        for name, item in manifest['Runs'].items():
            self.assertEqual(item['NativeFingerprint'], self.runs[name]['Metrics']['Native']['Fingerprint'])
        for g in read('index.json')['Groups']:
            geometry = read(g['GeometryFile'])
            self.assertTrue(geometry['segments'])
            initial = [self.runs[r['Name']]['Replay']['Samples'][0]['Input'] for r in g['Runs']]
            for other in initial[1:]:
                for field in ['Rider', 'Omega', 'Steering', 'Active']:
                    self.assertEqual(initial[0][field], other[field])

    def test_whole_footprint_gate_and_reaction_measurements_use_native_time(self):
        qualities = {q['Name']: q for q in json.loads(gzip.decompress((D/'native-quality.json.gz').read_bytes()))}
        aliases = read('final-aliases.json')
        for c in self.p['FinalSingle']+[c for c in self.p['Transfer'] if not c['Two']]:
            for label in aliases:
                q = qualities[f'final-{aliases[label]}-{c["Geometry"]}-{c["Name"]}']
                if q['Native']['Completed']:
                    self.assertEqual(q['FirstWholeGateTime'], q['Native']['GateSeconds'])
        for q in qualities.values():
            for e in q['Anticipation']:
                for request, activation in [('FirstCoastRequest', 'CoastActivation'), ('FirstSteeringRequest', 'SteeringActivation')]:
                    if e.get(activation) is not None:
                        self.assertAlmostEqual(e[activation]-e[request], .24)
                if e['PhysicalSteeringResponse'] is not None:
                    self.assertGreaterEqual(e['PhysicalSteeringResponse'], e['SteeringActivation'])


if __name__ == '__main__':
    unittest.main()
