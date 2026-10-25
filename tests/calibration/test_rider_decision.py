"""Audit frozen #133 evidence without creating native worlds or retuning."""
import gzip, hashlib, json, math, statistics, unittest
from pathlib import Path
from extended_source_guard import source132, source133

ROOT = Path(__file__).resolve().parents[2]
D = ROOT/'docs/experiments/rider-decision-optimization'
load = lambda name: json.loads((D/name).read_bytes())
sha = lambda data: hashlib.sha256(data).hexdigest().upper()


class RiderDecisionEvidence(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.p, cls.a, cls.f = [load(n) for n in ['protocol.json', 'attempts.json', 'final-results.json']]
        cls.runs = json.loads(gzip.decompress((D/'selected-replays.json.gz').read_bytes()))['Runs']

    def test_sources_original_objective_and_explicit_budget_amendment(self):
        amendment = load('budget-amendment.json')
        self.assertEqual(sha((D/'budget-source-before.txt').read_bytes()), amendment['OriginalSourceSha256'])
        for name, digest in self.p['Sources'].items():
            current = sha(source133(name))
            if name == 'RiderDecisionExperiment133.cs':
                self.assertEqual(digest, amendment['OriginalSourceSha256'])
                repair = load('metadata-repair.json')
                self.assertEqual(current, repair['SourceAfterSha256'])
                self.assertEqual(repair['SourceBeforeSha256'], amendment['ApprovedSourceSha256'])
                self.assertEqual(sha((D/'metadata-source-before.txt').read_bytes()), repair['SourceBeforeSha256'])
            else:
                self.assertEqual(current, digest, name)
        original = json.loads((D.parent/'full-throttle-learning/protocol.json').read_bytes())
        for name, digest in original['Sources'].items():
            self.assertEqual(sha(source132(name)), digest)
        self.assertEqual(amendment['TrainingWorldsAtAmendment'], 20)
        self.assertEqual(amendment['FinalWorldsAtAmendment'], 0)
        self.assertEqual(amendment['GenerationCpuSeconds'], 600)
        self.assertGreater(amendment['ObservedCpuSeconds'], 225)
        self.assertTrue(amendment['Authorized'])
        for key, value in [('TotalCpuSeconds', 7000), ('NativeWorlds', 800), ('Forecasts', 1500000), ('ContactEvaluations', 135000000)]:
            self.assertEqual(amendment['Unchanged'][key], value)

    def test_holdout_disjoint_and_selection_precedes_every_final_world(self):
        key = lambda s: tuple(s[k] for k in ['Speed', 'Offset', 'Start', 'OutwardSpeed', 'HeadingError', 'YawRate'])
        final = self.p['FinalSingle']+self.p['FinalTwo']
        self.assertEqual(len(final), 36)
        self.assertEqual(len({key(s) for s in final}), 36)
        other = self.p['Training']+self.p['Diagnostic']+self.p['Selection']
        for folder in ['full-throttle-learning', 'autonomous-speedway-extended-learning', 'robust-free-racing-learning', 'free-racing-learning']:
            path = D.parent/folder/'protocol.json'
            if not path.exists():
                continue
            p = json.loads(path.read_bytes())
            for value in p.values():
                if isinstance(value, list) and value and isinstance(value[0], dict) and 'Speed' in value[0] and 'OutwardSpeed' in value[0]:
                    other += value
        self.assertFalse({key(s) for s in final} & {key(s) for s in other})
        lock = load('winner-lock.json')
        self.assertEqual(lock['FinalWorlds'], 0)
        self.assertGreater(min(e['Number'] for e in self.a if e['Category'] == 'final'), lock['ReservationCount'])
        self.assertLess(max(e['Number'] for e in self.a if e['Category'] == 'selection'), min(e['Number'] for e in self.a if e['Category'] == 'final'))

    def test_controlled_400_worlds_all_candidates_and_same_initial_populations(self):
        total = 0
        for seed in self.p['Seeds']:
            arms = []
            for arm in ['A', 'B']:
                gs = load(f'generations-{arm}-s{seed}.json')
                self.assertEqual([g['Generation'] for g in gs], list(range(5)))
                for g in gs:
                    self.assertEqual(len(g['Candidates']), 5)
                    for c in g['Candidates']:
                        self.assertEqual(len(c['Results']), 4)
                        self.assertEqual(sum(n.endswith('-two') for n in c['Scenarios']), 3)
                        total += len(c['Results'])
                arms.append(gs)
            self.assertEqual([c['Policy'] for c in arms[0][0]['Candidates']], [c['Policy'] for c in arms[1][0]['Candidates']])
            self.assertEqual(arms[0][0]['Candidates'][0]['Policy'], self.p['StartingPolicy'])
        self.assertEqual(total, 400)
        self.assertEqual(sum(e['NativeWorlds'] for e in self.a if e['Category'] == 'train-ab'), 400)
        for arm in ['A', 'B']:
            initial = [load(f'generations-{arm}-s{seed}.json')[0]['Candidates'][0] for seed in self.p['Seeds']]
            self.assertEqual([r['Native']['Fingerprint'] for r in initial[0]['Results']], [r['Native']['Fingerprint'] for r in initial[1]['Results']])

    def test_r1_selection_uses_same_candidates_and_common_pairs(self):
        selection = load('selection.json')
        candidates = load('selection-candidates.json')
        self.assertEqual({(s['Variant'], s['Id']) for s in selection}, {(c['Label'], c['Id']) for c in candidates})
        baseline = next(s for s in selection if s['Variant'] == 'baseline4061-full')
        self.assertLessEqual(len(candidates), 14)
        scores = load('selection-time-scores.json')
        for evaluation in selection:
            self.assertEqual(evaluation['Scenarios'], baseline['Scenarios'])
            ratios = [(n.endswith('-two'), b['Native']['GateSeconds']/a['Native']['GateSeconds']-1) for n, a, b in zip(baseline['Scenarios'], baseline['Results'], evaluation['Results']) if a['Native']['Completed'] and b['Native']['Completed']]
            score = next(s['Score'] for s in scores if s['Id'] == evaluation['Id'])
            self.assertEqual(score['Pairs'], len(ratios))
            for two, field in [(True, 'MedianTwo'), (False, 'MedianSingle')]:
                values = [r for t, r in ratios if t == two]
                self.assertEqual(score[field], statistics.median(values) if values else None)
            self.assertEqual(score['WithinWorstBound'], bool(ratios) and max(r for _, r in ratios) <= .15)
        choices = load('ranked-choices.json')
        self.assertEqual({c['Label'] for c in choices}, {'A0', 'A1', 'B0', 'B1'})
        self.assertEqual(load('winner.json'), next(c for c in choices if c['Label'] == 'B1'))

    def test_cost_components_original_rank_and_causal_reaction_prefix(self):
        data = gzip.decompress((D/'cost-audit.json.gz').read_bytes())
        self.assertEqual(sha(data), load('cost-audit-archive.json')['OriginalSha256'])
        states = json.loads(data)
        self.assertEqual(len(states), load('cost-audit-summary.json')['States'])
        self.assertEqual({s['Phase'] for s in states} & {'straight', 'entry1', 'middle1', 'exit1', 'preparation2', 'bend2'}, {'straight', 'entry1', 'middle1', 'exit1', 'preparation2', 'bend2'})
        for s in states:
            for a in s['Actions']:
                terms = a['Costs']
                self.assertAlmostEqual(sum(v for k, v in terms.items() if k != 'Total'), a['Original']['Score'], places=9)
            chosen = next(a['Original'] for a in s['Actions'] if a['Original']['Action'] == s['Original']['Action'])
            self.assertEqual(chosen['Safety'], min(a['Original']['Safety'] for a in s['Actions']))
            self.assertEqual(chosen['Score'], min(a['Original']['Score'] for a in s['Actions'] if a['Original']['Safety'] == chosen['Safety']))
        historical = load('historical-cost-reproduction.json')
        self.assertEqual((historical['CoastWithSameSafetyPositive'], historical['DirectionCostHigher']), (194, 176))
        self.assertAlmostEqual(historical['MedianDirectionDelta'], 2.014, places=3)
        for comparison in load('offline-objective-comparison.json'):
            self.assertEqual(comparison['SafetyClassChanged'], 0)
            self.assertEqual(comparison['CoastToPositiveSameClass'], comparison['CoastToPositiveGreaterProgress'])
        for trial in load('causal-analysis.json'):
            if trial.get('Missing'):
                continue
            self.assertTrue(trial['MechanicalPrefixExact'])
            self.assertTrue(trial['FullRiderBeforeActivationExact'])
            self.assertAlmostEqual(trial['Activation']-trial['Time'], .24)
            self.assertGreater(trial['PredictedProgressGain'], 0)
            if trial['FullRunFaster']:
                self.assertTrue(trial['Trial']['Native']['Completed'])
                self.assertGreater(trial['FullTimeGain'], .01)

    def test_native_aliases_accounting_and_limits(self):
        self.assertTrue(all(e['Complete'] for e in self.a))
        self.assertEqual(len({e['Name'] for e in self.a}), len(self.a))
        analysis = load('analysis.json')
        for key, field, bound in [('NativeWorlds', 'NativeWorlds', 800), ('CpuSeconds', 'CpuSeconds', 7000), ('Forecasts', 'Forecasts', 1500000), ('ContactEvaluations', 'Evaluations', 135000000)]:
            self.assertEqual(analysis[key], sum(e[field] for e in self.a))
            self.assertLessEqual(analysis[key], bound)
        aliases, plan = load('final-aliases.json'), load('final-plan.json')
        for alias, original in aliases.items():
            a, b = [next(p for p in plan if p['Label'] == name) for name in [alias, original]]
            for key in ['Policy', 'NewObjective', 'FullThrottle', 'Horizon']:
                self.assertEqual(a[key], b[key])
            for suffix in ['single', 'two']:
                x, y = [next(e for e in self.f if e['Variant'] == name+'-'+suffix) for name in [alias, original]]
                self.assertEqual(x['Results'], y['Results'])
        self.assertEqual(sum(e['NativeWorlds'] for e in self.a if e['Category'] == 'final'), len(set(aliases.values()))*36)

    def test_viewer_is_native_and_preserves_actions_reaction_and_safety(self):
        manifest = load('selected-manifest.json')['Runs']
        overrides = {'causal-'+r['Phase']: r for r in load('causal-trials.json') if not r.get('Missing')}
        for group in load('index.json')['Groups']:
            inputs = [self.runs[r['Name']]['Replay']['Samples'][0]['Input'] for r in group['Runs']]
            for key in ['Rider', 'Steering', 'Omega', 'Active']:
                self.assertTrue(all(i[key] == inputs[0][key] for i in inputs))
            # Samples at t0 already include each policy's first observation.
            # Only the pre-existing queue is an exogenous initial condition.
            pending = [[c for c in i['Pending'] if c['ObservedAt'] < i['Time']-1e-8] for i in inputs]
            self.assertTrue(all(p == pending[0] for p in pending))
        for name, run in self.runs.items():
            self.assertEqual(run['Metrics']['Native']['Fingerprint'], manifest[name]['NativeFingerprint'])
            attempt = next(e for e in self.a if e['Name'] == name)
            self.assertEqual(manifest[name]['RawSha256'], attempt['RawSha256'])
            self.assertEqual(run['Metrics'], attempt['Metrics'])
            samples = run['Replay']['Samples']
            for a, b in zip(samples, samples[1:]):
                self.assertLessEqual(abs(b['Input']['Steering']-a['Input']['Steering']), (b['Input']['Time']-a['Input']['Time'])*math.pi/2+1e-5)
            for s in samples:
                self.assertLessEqual(abs(s['Input']['Steering']), math.radians(35)+1e-6)
                for command in s['Input']['Pending']:
                    self.assertAlmostEqual(command['ActivationAt']-command['ObservedAt'], .24)
            for command in run['Replay']['Activations']:
                self.assertAlmostEqual(command['ActivationAt']-command['ObservedAt'], .24)
            for decision in run['Replay']['Decisions']:
                if name in overrides and abs(decision['Time']-overrides[name]['Time']) < 1e-8:
                    self.assertEqual(decision['Action'], overrides[name]['Alternative'])
                    continue
                candidates = decision['Candidates']
                chosen = next(c for c in candidates if c['Action'] == decision['Action'])
                self.assertEqual(chosen['Safety'], min(c['Safety'] for c in candidates))
                self.assertEqual(chosen['Score'], min(c['Score'] for c in candidates if c['Safety'] == chosen['Safety']))
                if name.startswith(('final-133-', 'final-132-', 'profile-')):
                    for angle in {c['Action']['SteeringRadians'] for c in candidates}:
                        self.assertIn(dict(SteeringRadians=angle, Drive=1), [c['Action'] for c in candidates])
            n = run['Metrics']['Native']
            if n['Completed']:
                self.assertGreaterEqual(n['MinimumClearanceMeters'], 0)
                self.assertLessEqual(n['MaximumBetaDegrees'], 45)
                self.assertGreaterEqual(n['DurationSeconds']+1e-7, n['GateSeconds']+1)
            else:
                self.assertIsNone(n['GateSeconds'])

    def test_strict_criterion_and_all_reference_pairs(self):
        pairs, analysis = load('paired-times.json'), load('analysis.json')
        for r in pairs:
            self.assertIn(r['Reference'], ['130-H06', '131-H09', '132-H09'])
            ratios = [p['New']/p['Old']-1 for p in r['Pairs']]
            self.assertEqual(r['MedianRatio'], statistics.median(ratios) if ratios else None)
            self.assertEqual(r['MaximumRatio'], max(ratios, default=None))
        count = lambda suffix: sum(r['Native']['Completed'] for r in next(e for e in self.f if e['Variant'] == '133-B1-'+suffix)['Results'])
        passed = count('single') == 24 and count('two') == 12 and analysis['TwoPairedVs131Median'] is not None and analysis['TwoPairedVs131Median'] <= -.02 and analysis['SinglePairedVs130Median'] is not None and analysis['SinglePairedVs130Median'] <= .05 and analysis['SinglePairedVs130Maximum'] <= .15
        self.assertEqual(analysis['Verdict'], 'PASS' if passed else 'PARTIAL')
        for q in load('final-quality.json'):
            if q['Name'].rsplit('-', 1)[1].startswith('Y') and q['Native']['Completed']:
                self.assertEqual(q['FirstWholeGateTime'], q['Native']['GateSeconds'])
        for r in load('first-bend-pairs.json'):
            ratios = [p['NewFirstGate']/p['OldFirstGate']-1 for p in r['Pairs']]
            self.assertEqual(r['MedianFirstGateRatio'], statistics.median(ratios) if ratios else None)

    def test_manifests_archive_exact_bytes(self):
        for name, digest in load('manifest.json').items():
            self.assertEqual(sha((D/name).read_bytes()), digest, name)
        self.assertEqual(sha((D/'selected-replays.json.gz').read_bytes()), load('selected-manifest.json')['BundleSha256'])
        for name in ['cost-audit', 'causal-forecasts']:
            packed = (D/(name+'.json.gz')).read_bytes()
            archive = load(name+'-archive.json')
            self.assertEqual(sha(packed), archive['CompressedSha256'])
            self.assertEqual(sha(gzip.decompress(packed)), archive['OriginalSha256'])
        repair = load('metadata-repair.json')
        original = gzip.decompress((D/'resume-archive-original.json.gz').read_bytes())
        self.assertEqual(sha(original), repair['OriginalArchiveSha256'])
        self.assertEqual(sha((D/'generations-A-s1332026.json').read_bytes()), repair['CorrectedArchiveSha256'])
        rows = json.loads(original)
        rows[0]['Mean'] = repair['ReconstructedMean']
        self.assertEqual(rows, load('generations-A-s1332026.json'))
        self.assertEqual(repair['ImmutableCandidateResultsDigestBefore'], repair['ImmutableCandidateResultsDigestAfter'])
        self.assertEqual(repair['NativeWorldsAdded'], 0)


if __name__ == '__main__':
    unittest.main()
