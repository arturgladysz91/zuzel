import csv,gzip,hashlib,json,math,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];DATA=ROOT/'docs/experiments/predictive-rider-control'
def load(path):
    with (gzip.open(path,'rt',encoding='utf-8') if path.suffix=='.gz' else path.open(encoding='utf-8-sig')) as f:return json.load(f)
def rows(path):
    with gzip.open(path,'rt',encoding='utf-8') as f:return [{k:float(v) for k,v in row.items()} for row in csv.DictReader(f)]

class PredictiveRiderEvidenceTests(unittest.TestCase):
    def test_frozen_prediction_matrix_and_real_selected_actions_match_native(self):
        gate=load(DATA/'prediction-validation.json');a=load(DATA/'analysis.json')
        self.assertTrue(gate['Passed']);self.assertEqual(gate['Forecasts'],63);self.assertEqual(gate['NativeWorlds'],0)
        for horizon in [.3,.6,.9]:
            records=[r for r in gate['Records'] if r['Horizon']==horizon]
            self.assertEqual(len(records),21);self.assertTrue(all(r['Passed'] for r in records))
        self.assertTrue(a['SelectedPredictionAgainstRealCandidate']['Passed']);self.assertEqual(a['Grade'],'PARTIAL')
        self.assertGreater(a['Candidate']['Time'],a['Baseline']['Time'])
    def test_off_stream_is_identical_and_comparison_has_exact_shared_line(self):
        self.assertEqual(rows(DATA/'baseline.csv.gz'),rows(DATA/'../continuous-corner-planning/continuous.csv.gz'))
        self.assertTrue(load(DATA/'baseline-verification.json')['ExactFingerprint'])
        first=load(DATA/'baseline-changes.json');second=load(DATA/'controlled/candidate-changes.json')
        for a,b in zip(first,second):
            for k in ['Phase','Time','Activation','Splice','EndTransition','Offset','BendStart','BendExit']:self.assertEqual(a[k],b[k])
        self.assertTrue(load(DATA/'analysis.json')['SharedPreparedLine'])
    def test_physical_limits_energy_and_reaction_are_preserved(self):
        rr=rows(DATA/'controlled/candidate.csv.gz');pp=rows(DATA/'controlled/candidate-plan.csv.gz');run=load(DATA/'controlled/candidate.json')
        for r,p in zip(rr,pp):
            self.assertLessEqual(abs(r['steer_after']-r['steer_before']),math.pi/2*r['dt']+1e-6)
            self.assertLessEqual(abs(r['front_lateral']),p['front_capacity']+1e-3)
            self.assertLessEqual(math.hypot(r['rear_lateral'],r['rear_longitudinal']),p['rear_capacity']+1e-3)
        for c in run['Result']['Decisions']:
            self.assertAlmostEqual(c['RequestedActivation']-c['ObservedAt'],.24,places=8);self.assertIsNone(c['CancelledAt'])
        for d in load(DATA/'controlled/candidate-steering-events.json'):
            self.assertAlmostEqual(d['ActualActivation']-d['ObservedAt'],.24,places=8)
        a=load(DATA/'analysis.json');self.assertGreater(a['Candidate']['ConservativeInner'],0);self.assertGreater(a['Candidate']['ConservativeOuter'],.5)
        self.assertLess(abs(a['Candidate']['UnexplainedEnergyJoules']),1e-6)
        self.assertEqual(run['Energy']['MaximumExcessBeyondEngineJoules'],0)
    def test_exact_source_guards_cover_only_approved_integration_extensions(self):
        for name,digest in load(DATA/'manifest.json').items():
            self.assertEqual(hashlib.sha256((DATA/name).read_bytes()).hexdigest().upper(),digest)
        guard=load(DATA/'approved-source-extensions.json');base=load(DATA/'../continuous-corner-planning/protocol.json')['Sources']
        self.assertEqual(set(guard['Sources']),{'PreparedCornerPlan.cs','ContinuousCornerPlanning.cs','WorldModel.cs','WorldRunner.cs'})
        for name,digest in base.items():
            actual=hashlib.sha256((ROOT/'tools/shared-world-speedway'/name).read_bytes().replace(b'\r\n',b'\n')).hexdigest().upper()
            approved=guard['Sources'].get(name)
            if approved:self.assertEqual(approved['BaseSha256'],digest);self.assertEqual(actual,approved['ApprovedSha256'])
            else:self.assertEqual(actual,digest)
        attempt=load(DATA/'controller-attempt.json')
        for name in ['PredictiveRiderModel.cs','PredictiveRiderControl.cs']:
            expected=attempt['SourceHashes'][name]
            if name=='PredictiveRiderModel.cs':
                extension=load(DATA/'../first-bend-zigzag/source-extension.json')
                self.assertEqual(extension['BaseSha256'],expected)
                expected=extension['ApprovedSha256']
            self.assertEqual(hashlib.sha256((ROOT/'tools/shared-world-speedway'/name).read_bytes()).hexdigest().upper(),expected)
    def test_no_retuning_and_uncontrolled_trial_remains_explicitly_excluded(self):
        protocol=load(DATA/'controlled/controlled-comparison-protocol.json');a=load(DATA/'analysis.json')
        self.assertTrue(protocol['ControllerUnchanged']);self.assertTrue(protocol['ToleranceUnchanged']);self.assertTrue(protocol['NoRetuning'])
        self.assertEqual(a['NativeWorlds'],3)
        self.assertEqual(a['ExcludedUncontrolledPlanRun'],load(DATA/'candidate.json')['Result']['Fingerprint'])
        self.assertNotEqual(load(DATA/'candidate-changes.json')[0]['Splice'],load(DATA/'baseline-changes.json')[0]['Splice'])
        self.assertEqual(a['Candidate']['PredictiveQueued'],a['Candidate']['PredictiveActivations'])

if __name__=='__main__':unittest.main()
