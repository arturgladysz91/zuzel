import gzip
import hashlib
import json
import math
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/free-racing'

def load(name):
    data=(DATA/name).read_bytes()
    return json.loads(gzip.decompress(data) if name.endswith('.gz') else data)

def digest(data):return hashlib.sha256(data).hexdigest().upper()

class FreeRacingEvidenceTests(unittest.TestCase):
    def test_preflight_stop_is_honest_and_budget_never_spent_on_training(self):
        a=load('analysis.json');budget=load('budget.json');self.assertEqual(len(budget),1)
        self.assertEqual(budget[0]['Stage'],'preflight');self.assertTrue(budget[0]['Complete'])
        self.assertFalse(load('preflight.json')['Passed']);self.assertEqual(a['Grade'],'PARTIAL')
        self.assertEqual(a['TrainingRuns'],0);self.assertFalse(a['LearnedPolicyExists'])
        self.assertFalse((DATA/'learned-policy.json').exists());self.assertEqual(a['IncompleteRuns'],1)
        self.assertEqual(a['ValidationRuns'],0);self.assertEqual(a['FinalRuns'],0)
    def test_native_footprint_crossing_is_failure_even_before_projected_gate(self):
        run=load('preflight-train-16-3.json.gz');v=run['Samples'][-1]['Input']['Rider']
        self.assertLess(run['Samples'][-1]['Outer'],0);self.assertFalse(run['Metrics']['Completed'])
        self.assertIsNone(run['Metrics']['GateSeconds']);self.assertLess(v['TotalReferenceMeters'],35)
        self.assertAlmostEqual(run['Metrics']['DurationSeconds'],1.91)
        self.assertAlmostEqual(v['DistanceMeters'],run['Metrics']['DistanceMeters'])
    def test_all_available_new_mode_forecasts_pass_but_missing_tails_are_not_passes(self):
        records=load('preflight.json')['Records'];self.assertEqual(len(records),7)
        self.assertTrue(all(r['Passed'] for r in records));self.assertEqual(load('analysis.json')['MissingPredictionComparisonsAfterNativeStop'],2)
        self.assertEqual({r['Horizon'] for r in records},{.3,.6,.9})
        self.assertLess(max(r['XY'] for r in records),.1)
        self.assertEqual(load('offline-validation.json')['NativeWorlds'],0)
        self.assertEqual(load('offline-validation.json')['NativeDecisionsRecomputedExactly'],20)
    def test_actuator_state_is_native_and_pending_is_causal(self):
        run=load('preflight-train-16-3.json.gz');samples=run['Samples']
        for i,s in enumerate(samples):
            p=s['Input'];self.assertTrue(0<=p['Rider']['Drive']<=1)
            self.assertLessEqual(abs(p['Steering']),35*math.pi/180+1e-6)
            for c in p['Pending']:
                self.assertLessEqual(c['ObservedAt'],p['Time']+1e-8)
                self.assertGreaterEqual(c['ActivationAt'],p['Time']-1e-8)
                self.assertAlmostEqual(c['ActivationAt']-c['ObservedAt'],.24)
            if i:
                previous=samples[i-1]['Input'];dt=p['Time']-previous['Time']
                self.assertGreater(dt,0);self.assertLessEqual(abs(p['Steering']-previous['Steering'])/dt,math.pi/2+1e-4)
        self.assertEqual(samples[0]['Input']['Rider']['Drive'],1)
        first=run['Activations'][0];self.assertEqual(first['ActivationAt'],.24)
        self.assertEqual(first['Action']['Drive'],0)
    def test_exact_additive_extensions_restore_off_base_and_preserve_physics(self):
        guard=load('source-extensions.json');self.assertEqual(set(guard['Sources']),{'WorldRunner.cs','WorldModel.cs','Program.cs'})
        for name,item in guard['Sources'].items():
            current=(ROOT/'tools/shared-world-speedway'/name).read_bytes().replace(b'\r\n',b'\n').decode()
            newer=json.loads((DATA/'../free-racing-learning/source-extensions.json').read_bytes())['Sources'].get(name)
            if newer:
                self.assertEqual(digest(current.encode()),newer['ApprovedSha256'])
                for edit in reversed(newer['Edits']):
                    self.assertEqual(current.count(edit['After']),1);current=current.replace(edit['After'],edit['Before'],1)
            self.assertEqual(digest(current.encode()),item['ApprovedSha256'])
            for edit in reversed(item['Edits']):
                self.assertEqual(current.count(edit['After']),1);current=current.replace(edit['After'],edit['Before'],1)
            self.assertEqual(digest(current.encode()),item['BaseSha256'])
        for name,expected in load('protocol.json')['Sources'].items():
            newer=json.loads((DATA/'../free-racing-learning/source-extensions.json').read_bytes())['Sources'].get(name)
            if newer:self.assertEqual(newer['BaseRawSha256'],expected);expected=newer['ApprovedRawSha256']
            self.assertEqual(digest((ROOT/'tools/shared-world-speedway'/name).read_bytes()),expected)
    def test_manifest_protects_failed_evidence_and_all_attempts_remain_visible(self):
        for name,expected in load('manifest.json').items():self.assertEqual(digest((DATA/name).read_bytes()),expected)
        run=load('preflight-train-16-3.json.gz')
        self.assertEqual(len(run['Decisions']),20)
        first=next(d for d in run['Decisions'] if all(c['MinimumClearanceMeters']<0 for c in d['Candidates']))
        self.assertEqual(first['Time'],1.1)
        self.assertIn(run['Name'],[r['Name'] for r in load('index.json')['Runs']])
    def test_training_and_untouched_validation_cases_are_declared_before_any_results(self):
        p=load('protocol.json');self.assertEqual(p['Seed'],1282026);self.assertEqual(p['TrainingCandidates'],12)
        self.assertEqual(p['ScenariosPerCandidate'],2);self.assertEqual(p['MaximumNativeRuns'],40)
        train={(s['Speed'],s['Offset']) for s in p['TrainingScenarios']};test={(s['Speed'],s['Offset']) for s in p['ValidationScenarios']}
        self.assertFalse(train&test);self.assertEqual(len(test),3)
        self.assertEqual(len(p['Names']),7);self.assertEqual(len(p['Lower']),7);self.assertEqual(len(p['Upper']),7)

if __name__=='__main__':unittest.main()
