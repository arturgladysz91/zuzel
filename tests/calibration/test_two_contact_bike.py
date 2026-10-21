import hashlib
import importlib.util
import math
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('two_contact_report',ROOT/'tools/shared-world-speedway/two-contact-bike-report.py')
report=importlib.util.module_from_spec(spec);spec.loader.exec_module(report)


class TwoContactBikeAnalysisTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.a=report.load(report.DATA/'analysis.json')

    def test_endpoint_point_work_reconstructs_new_energy_without_extra_yaw_work(self):
        run=report.load(report.DATA/'track-short.json');rows=report.read(report.DATA/'track-short.csv');e=report.energy_from_new(rows,run)
        self.assertAlmostEqual(e['Residual'],run['Energy']['BalanceResidualJoules'],delta=.001)
        self.assertAlmostEqual(e['FrontWork'],-e['FrontLoss'],places=8)
        rotational=run['Energy']['FrontRotationalWorkJoules']+run['Energy']['RearRotationalWorkJoules']
        spin=run['Result']['Riders'][0]['AngularVelocity'];self.assertAlmostEqual(rotational,.5*report.f32(run['Inertia'])*spin*spin,delta=.001)
        self.assertEqual(0,run['LegacyYawCalls']);self.assertEqual(0,run['Energy']['MaximumExcessBeyondEngineJoules'])

    def test_local_velocities_moments_and_budget_split_are_physical_recorded_quantities(self):
        for r in report.read(report.DATA/'track-short.csv'):
            for prefix in ['front','rear']:
                expected=report.point_velocity([r['vx'],r['vy']],r['body_omega'],[r[prefix+'_arm_x'],r[prefix+'_arm_y']])
                self.assertAlmostEqual(expected[0],r[prefix+'_vx'],delta=.00001)
                self.assertAlmostEqual(expected[1],r[prefix+'_vy'],delta=.00001)
                torque=r[prefix+'_arm_x']*r[prefix+'_fy']-r[prefix+'_arm_y']*r[prefix+'_fx']
                self.assertAlmostEqual(torque,r[prefix+'_torque'],delta=.0001)
            self.assertLessEqual(abs(r['front_lateral']),.400001*r['budget'])
            self.assertLessEqual(math.hypot(r['rear_lateral'],r['rear_longitudinal']),.600001*r['budget'])
            self.assertEqual(1,r['drive'])

    def test_wheelspin_changes_rear_moment_without_changing_front_contact_at_fixed_body_state(self):
        a=report.load(report.DATA/'contact-probes.json');self.assertEqual(0,a['BodySteps']);self.assertEqual(0,a['WheelCommits'])
        for sign in [1,-1]:
            group=[r for r in a['Rows'] if r['Sign']==sign];first,last=group[0]['Forces'],group[-1]['Forces']
            self.assertEqual(first['FrontLateral'],last['FrontLateral'])
            self.assertLess(abs(last['RearLateral']),abs(first['RearLateral']))
            self.assertGreater(last['RearLongitudinal'],first['RearLongitudinal'])
            self.assertNotEqual(first['NetTorque'],last['NetTorque'])
        for a,b in zip(a['Rows'][:3],a['Rows'][3:]):self.assertAlmostEqual(a['Forces']['NetTorque'],-b['Forces']['NetTorque'],delta=.001)

    def test_actual_track_crossing_is_distinct_from_lack_of_interval_certificate(self):
        run=self.a['New'];self.assertLess(run['MinimumOuter'],0);self.assertFalse(self.a['ShortGate']['Passed'])
        self.assertEqual('observed outer footprint crossing',run['StopReason'])
        self.assertIsNone(self.a['Phases']['New']['Exit'])
        self.assertFalse((report.DATA/'track-long.json').exists())
        uncertified=dict(run,StopReason=None,MinimumInner=.1,MinimumOuter=1,ConservativeInner=-.01,ConservativeOuter=.8)
        self.assertFalse(report.qualified_short(uncertified))
        self.assertTrue(report.qualified_short(dict(uncertified,ConservativeInner=.01)))

    def test_large_initial_angles_and_low_speed_are_finite_passive_and_do_not_require_angle_hold(self):
        cases={c['Name']:c for c in self.a['Isolated']}
        self.assertGreater(cases['D-beta50']['MaximumSlideDegrees'],49)
        self.assertLess(abs(cases['D-beta50']['FinalSlideDegrees']),1)
        for c in cases.values():
            self.assertIsNone(c['StopReason']);self.assertEqual(0,c['LegacyYawCalls'])
            self.assertEqual(0,c['Energy']['MaximumExcessBeyondEngineJoules'])
            self.assertLess(abs(c['Energy']['BalanceResidualJoules']),.15)
        spin=report.load(report.DATA/'E-spin-only.json')['Initial']['Riders'][0]
        self.assertEqual({'X':0,'Y':0},spin['Velocity']);self.assertEqual(2,spin['AngularVelocity'])

    def test_native_repeat_convergence_and_off_fingerprint_are_preserved(self):
        cases={c['Name']:c for c in self.a['Isolated']}
        self.assertEqual(cases['B-left']['Fingerprint'],cases['E-left-repeat']['Fingerprint'])
        a,b,c=self.a['Convergence'];self.assertGreater(a['PositionDifferenceVersus5ms'],b['PositionDifferenceVersus5ms']);self.assertGreater(b['PositionDifferenceVersus5ms'],c['PositionDifferenceVersus5ms'])
        off=self.a['OffFingerprint'];self.assertTrue(off['Matches']);self.assertEqual(off['Expected'],off['Actual'])
        self.assertTrue(self.a['InitialBodyWheelExactlyMatched'])

    def test_every_old_evidence_hash_and_new_manifest_remains_exact(self):
        self.assertEqual(906,sum(report.historical_hashes().values()))
        for file,digest in report.load(report.DATA/'manifest.json').items():
            self.assertEqual(digest,hashlib.sha256((report.DATA/file).read_bytes()).hexdigest().upper())


if __name__=='__main__':unittest.main()
