import hashlib
import importlib.util
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('physical_steering',ROOT/'tools/shared-world-speedway/physical-steering-report.py')
report=importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)
DATA=report.DATA

class PhysicalSteeringEvidenceTests(unittest.TestCase):
    def test_same_initial_state_and_only_steering_flag_change(self):
        old=report.load(ROOT/'docs/experiments/two-contact-bike-dynamics/freeze.json')
        new=report.load(DATA/'freeze.json');p=dict(new['Parameters'])
        self.assertTrue(p.pop('EnablePhysicalSteeringControl'));self.assertEqual(p,old['Parameters'])
        self.assertEqual(new['Bike'],old['Bike']);self.assertEqual(new['Wheel'],old['Wheel'])
        initial=report.load(ROOT/'docs/experiments/two-contact-bike-dynamics/track-short.json')['Initial']
        for name in report.TRIALS:self.assertEqual(report.load(DATA/(name+'.json'))['Initial'],initial)

    def test_native_energy_residual_is_measured_impulse_error_work(self):
        for name in report.TRIALS:
            run=report.load(DATA/(name+'.json'));rows=report.read(name+'.csv')
            energy=report.old.energy_from_new(rows,run);impulse=report.impulse_work(rows,run)
            self.assertLess(abs(energy['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules']),1e-6)
            self.assertLess(abs(energy['Residual']-run['Energy']['BalanceResidualJoules']),.005)
            self.assertEqual(run['Energy']['MaximumExcessBeyondEngineJoules'],0)
            self.assertEqual(run['LegacyYawCalls'],0)

    def test_safe_first_second_and_eight_laps_do_not_claim_exact_inner_tracking(self):
        analysis=report.load(DATA/'analysis.json')
        for name in report.TRIALS:
            trial=analysis['Trials'][name]
            self.assertTrue(trial['Gate']['Passed']);self.assertGreater(trial['ConservativeInner'],0);self.assertGreater(trial['ConservativeOuter'],0)
        self.assertTrue(analysis['Trials']['eight-laps']['CompletedEightLaps'])
        self.assertGreater(analysis['Trials']['eight-laps']['MaximumNormalErrorMeters'],7)
        self.assertLess(analysis['Trials']['first-bend']['MaximumNormalErrorMeters'],1)

    def test_local_queries_expose_early_control_error_and_later_contact_limit(self):
        diagnosis=report.load(DATA/'diagnosis.json')
        self.assertEqual(diagnosis['BodySteps'],0);self.assertEqual(diagnosis['WheelCommits'],0);self.assertEqual(diagnosis['MaximumForceReplayDifferenceNewtons'],0)
        early=next(s for s in diagnosis['States'] if abs(s['Time']-.2)<1e-7)
        late=next(s for s in diagnosis['States'] if abs(s['Time']-.94)<1e-7)
        self.assertLess(early['Old']['NetTorque'],0)
        self.assertGreater(max(r['Response']['NetTorque'] for r in early['Envelope']),400)
        self.assertLess(max(r['Response']['NetTorque'] for r in late['Envelope']),7)
        bounds=report.load(DATA/'torque-upper-bounds.json')
        bound=next(s for s in bounds['Results'] if abs(s['Time']-.94)<1e-7)
        self.assertTrue(bound['RequestedRecoveryProvablyUnavailable'])
        self.assertLess(bound['ConservativeMaximumNetTorqueNm'],57)
        self.assertGreater(bound['TorqueToReachPdRateInPointOneSecondsNm'],182)
        peak=report.load(DATA/'analysis.json')['Trials']['second-bend']['MaximumPathForceBudgetRatio']
        self.assertGreater(peak['Ratio'],1.3) # Necessary force bound, not a sufficient tracking certificate.

    def test_all_historical_hashes_and_current_manifest_match_bytes(self):
        self.assertEqual(sum(report.historical_hashes().values()),959)
        for name,digest in report.load(DATA/'manifest.json').items():
            self.assertEqual(hashlib.sha256((DATA/name).read_bytes()).hexdigest().upper(),digest)

if __name__=='__main__':unittest.main()
