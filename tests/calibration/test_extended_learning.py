"""Audit #131 evidence, frozen choice, full native state and accounting without training."""
import gzip
import hashlib
import json
import math
from pathlib import Path
import unittest
from extended_source_guard import source130

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/autonomous-speedway-extended-learning'
def load(n):return json.loads((DATA/n).read_bytes())
def sha(b):return hashlib.sha256(b).hexdigest().upper()

class ExtendedLearningEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.p=load('protocol.json');cls.a=load('attempts.json');cls.r=load('final-results.json')
        cls.runs=json.loads(gzip.decompress((DATA/'selected-replays.json.gz').read_bytes()))['Runs']

    def test_disjoint_scenarios_and_choice_before_any_final_world(self):
        p=self.p;key=lambda s:tuple(s[k] for k in ['Speed','Offset','Start','OutwardSpeed','HeadingError','YawRate'])
        train={key(s) for s in p['Training']};selection={key(s) for s in p['Selection']};final={key(s) for s in p['FinalSingle']+p['FinalTwo']}
        self.assertEqual(len(p['FinalSingle']),24);self.assertEqual(len(p['FinalTwo']),12);self.assertEqual(len(final),36)
        self.assertFalse(train&selection or train&final or selection&final)
        old=json.loads((DATA/'../robust-free-racing-learning/protocol.json').read_bytes())
        self.assertFalse(final&{key(s) for s in old['SealedTest']+old['Selection']+old['High']+old['Medium']+old['Retention']})
        self.assertEqual(load('winner-lock.json')['FinalWorlds'],0)
        first=min(e['Number'] for e in self.a if e['Name'].startswith('final-'))
        self.assertTrue(all(e['Number']<first for e in self.a if e['Category'].startswith('train-') or '-select-' in e['Name']))

    def test_two_seeds_equal_horizon_budgets_and_identical_initial_populations(self):
        for seed in self.p['Seeds']:
            groups=[load(f'generations-H{h}-s{seed}.json') for h in ['06','09']]
            self.assertEqual([c['Policy'] for c in groups[0][0]['Candidates']],[c['Policy'] for c in groups[1][0]['Candidates']])
            for gs in groups:
                self.assertEqual(len(gs),4)
                for g in gs:
                    self.assertEqual(len(g['Candidates']),5)
                    for c in g['Candidates']:
                        self.assertEqual(len(c['Results']),4);self.assertEqual(c['Scenarios'],g['Candidates'][0]['Scenarios'])
        self.assertEqual(sum(e['Category']=='train-ab' for e in self.a),320)

    def test_resource_accounting_and_no_ignored_dnf(self):
        a=load('analysis.json');self.assertTrue(all(e['Complete'] for e in self.a));self.assertEqual(len(self.a),len({e['Name'] for e in self.a}))
        self.assertEqual(sum(e['NativeWorlds'] for e in self.a),a['NativeWorlds']);self.assertLessEqual(a['NativeWorlds'],1000)
        self.assertAlmostEqual(sum(e['CpuSeconds'] for e in self.a),a['CpuSeconds']);self.assertLessEqual(a['CpuSeconds'],6000)
        self.assertEqual(sum(e['Evaluations'] for e in self.a),a['ContactEvaluations']);self.assertLessEqual(a['ContactEvaluations'],90000000)
        self.assertEqual(sum(e['Forecasts'] for e in self.a),a['Forecasts']);self.assertLessEqual(a['TrainingTwo'],400)
        self.assertEqual(sum(e['Name'].startswith('final-') for e in self.a),108)
        for e in self.a:
            if e.get('Metrics'):
                n=e['Metrics']['Native'];self.assertEqual(n['Completed'],n['GateSeconds'] is not None)
                if n['Completed']:
                    self.assertGreaterEqual(n['DurationSeconds'],n['GateSeconds']+1-1e-7);self.assertLessEqual(n['MaximumBetaDegrees'],45)
                    self.assertGreaterEqual(e['Metrics']['MinimumLegacyCertificate'],0)
                self.assertNotIn('invariant',n['Outcome'])

    def test_final_comparisons_share_all_states_and_frozen_policy(self):
        self.assertEqual(len(self.r),6);winner=load('winner.json')
        for two in [False,True]:
            suffix='two' if two else 'single';expected=[s['Name'] for s in self.p['FinalTwo' if two else 'FinalSingle']]
            groups=[r for r in self.r if r['Variant'].endswith('-'+suffix)];self.assertEqual(len(groups),3)
            for r in groups:self.assertEqual(r['Scenarios'],expected)
            new=next(r for r in groups if r['Variant'].startswith('131-final'));self.assertEqual(new['Policy'],winner['Policy']);self.assertEqual(new['Id'],winner['Id'])
        for pair in load('paired-times.json'):
            for x in pair['Pairs']:self.assertAlmostEqual(x['Delta'],x['New']-x['Old']);self.assertAlmostEqual(x['Ratio'],x['New']/x['Old']-1)

    def test_native_replay_limits_reaction_queue_and_same_initial_state(self):
        for group in load('index.json')['Groups']:
            runs=[self.runs[r['Name']] for r in group['Runs']];first=runs[0]['Replay']['Samples'][0]['Input']
            for run in runs:
                r=run['Replay'];initial=r['Samples'][0]['Input']
                for key in ['Rider','Omega','Steering','Active']:self.assertEqual(initial[key],first[key])
                # Recorded sample0 includes the decision made AT0; prior pending is empty for every policy.
                self.assertEqual([c for c in initial['Pending'] if c['ObservedAt']<0],[])
                for sample in r['Samples']:
                    i=sample['Input'];self.assertLessEqual(abs(i['Steering']),math.radians(35)+1e-7)
                    for c in i['Pending']:self.assertLessEqual(c['ObservedAt'],i['Time']+1e-8);self.assertAlmostEqual(c['ActivationAt']-c['ObservedAt'],.24)
                for t in run['Telemetry']:
                    self.assertLessEqual(abs(t['Forces']['SteeringAfter']-t['Forces']['SteeringBefore']),math.pi/2*t['Seconds']+7e-8)
                for a,b in zip(r['Decisions'],r['Decisions'][1:]):self.assertAlmostEqual(b['Time']-a['Time'],.2)

    def test_predictor_frozen_thresholds_physics_sources_and_manifests(self):
        p=load('predictor.json');self.assertTrue(p['Passed']);self.assertFalse(p['ThresholdsChanged']);self.assertEqual(len(p['Checks']),6)
        self.assertTrue(load('determinism.json')['Passed']);self.assertTrue(all(c['PrefixExact'] and c['Passed'] for c in p['Checks']))
        self.assertTrue(load('h06-reference-parity.json')['Passed'])
        for check in load('counter-prefix-alignment.json'):
            self.assertTrue(check['PhysicalPrefixThroughActivationExact'] and check['WholeRiderStrictlyBeforeActivationExact'])
            self.assertEqual(check['DifferencesExactlyAtActivation'],['Drive'])
        high=load('high-slip-prediction.json');self.assertTrue(high['Passed'] and high['HasEndpoint'] and high['PrefixExact']);self.assertFalse(high['ThresholdsChanged'])
        self.assertGreater(high['MaximumNativeBeta'],35);self.assertLess(high['MaximumNativeBeta'],45);self.assertTrue(all(c['Passed'] for c in high['Comparisons']))
        original=load('protocol-before-line-ending-correction.json');current=dict(self.p);original.pop('Sources');current.pop('Sources');self.assertEqual(original,current)
        correction=load('line-ending-correction.json');self.assertEqual(correction['OriginalProtocolSha256'],sha((DATA/'protocol-before-line-ending-correction.json').read_bytes()))
        for label in ['action-availability','high-slip']:
            self.assertEqual(load(label+'-protocol.json')['SourceSha256'],sha((DATA/(label+'-source.cs.txt')).read_bytes()))
        for name,digest in self.p['Sources'].items():self.assertEqual(sha((ROOT/'tools/shared-world-speedway'/name).read_bytes()),digest,name)
        old=json.loads((DATA/'../robust-free-racing-learning/protocol.json').read_bytes())
        for name,digest in old['Sources'].items():self.assertEqual(sha(source130(name)),digest,name)
        for name,digest in load('manifest.json').items():self.assertEqual(sha((DATA/name).read_bytes()),digest,name)
        self.assertEqual(sha((DATA/'selected-replays.json.gz').read_bytes()),load('selected-manifest.json')['BundleSha256'])

if __name__=='__main__':unittest.main()
