import csv,gzip,hashlib,importlib.util,json,math,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];DATA=ROOT/'docs/experiments/first-bend-zigzag';OLD=ROOT/'docs/experiments/predictive-rider-control'
def load(path):
    with gzip.open(path,'rt',encoding='utf-8') if path.suffix=='.gz' else path.open(encoding='utf-8-sig') as f:return json.load(f)
class ZigzagControlEvidenceTests(unittest.TestCase):
    def test_frozen126_data_and_control_physics_are_preserved(self):
        for name,digest in load(DATA/'manifest.json').items():self.assertEqual(hashlib.sha256((DATA/name).read_bytes()).hexdigest().upper(),digest)
        for name,digest in load(OLD/'manifest.json').items():self.assertEqual(hashlib.sha256((OLD/name).read_bytes()).hexdigest().upper(),digest)
        guard=load(DATA/'source-extension.json');self.assertEqual(guard['BaseSha256'],load(OLD/'controller-attempt.json')['SourceHashes']['PredictiveRiderModel.cs'])
        self.assertEqual(hashlib.sha256((ROOT/guard['Source']).read_bytes()).hexdigest().upper(),guard['ApprovedSha256'])
        self.assertEqual(load(DATA/'attempt.json')['ModelSourceSha256'],guard['ApprovedSha256'])
    def test_default_prediction_and_native_force_reconstruction_are_exact(self):
        matrix=load(DATA/'execution-and-cost.json');gate=load(DATA/'default-model-validation.json')
        self.assertEqual(len(gate['Records']),63);self.assertTrue(all(r['Passed'] for r in gate['Records']))
        self.assertEqual(len(matrix['DefaultEquality']),30)
        for r in matrix['DefaultEquality']:
            for k in ['Position','Steering','Drive']:self.assertEqual(r[k],0)
        contact=load(DATA/'same-state-contacts.json.gz');self.assertEqual(contact['MaximumNativeReplayError'],0);self.assertEqual(contact['Suppressed117Intervals'],100)
        self.assertEqual(contact['NativeWorlds'],0)
        self.assertEqual(load(DATA/'analysis.json')['DefaultPhysicalPredictionEquality'],30)
    def test_all_ten_costs_reconstruct_and_horizon_changes_are_not_hidden(self):
        a=load(DATA/'analysis.json');m=load(DATA/'execution-and-cost.json');self.assertLess(a['MaximumFrozenScoreReconstructionError'],1e-10)
        self.assertEqual(len(m['Records']),10);self.assertEqual(len(a['HorizonRankings']),10)
        self.assertEqual([round(r['Observed'],2) for r in a['HorizonRankings'] if r['Best06']!=r['Best09']],[1.6])
        for event in m['Records']:
            self.assertAlmostEqual(event['Activation']-event['ObservedAt'],.24,places=8)
            self.assertEqual(len(event['Comparisons']),8)
            for comparison in event['Comparisons']:
                s=comparison['Summary'];self.assertAlmostEqual(s['Total'],sum(s[k] for k in ['Tracking','Heading','EndBetaGrowth','Progress','Reversal']),places=10)
    def test_native_overshoot_is_distinct_from_steering_and_no_new_variant_is_claimed(self):
        a=load(DATA/'analysis.json');self.assertEqual(a['Grade'],'PARTIAL');self.assertEqual(a['NewNativeWorlds'],0);self.assertFalse(a['NewRuntimeController'])
        native=load(DATA/'native-telemetry.json.gz');self.assertEqual(native['BetaConvention'],'psi-chi')
        for name in ['baseline','candidate']:
            self.assertEqual(len(a['Native'][name]['PhysicalLineExcursions']),3)
            for r in native['Series'][name]:self.assertAlmostEqual(r['psi']-r['chi'],r['beta'],places=4)
        self.assertGreater(a['Native']['candidate']['MaximumTangentErrorDegrees'],a['Native']['baseline']['MaximumTangentErrorDegrees'])
        self.assertAlmostEqual(sum(p['Suppressed117Seconds'] for p in a['Pulses']),1,places=6)
if __name__=='__main__':unittest.main()
