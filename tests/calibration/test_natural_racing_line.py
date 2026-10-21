import hashlib
import importlib.util
import math
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('natural_line_report',ROOT/'tools/shared-world-speedway/natural-racing-line-report.py')
report=importlib.util.module_from_spec(spec);spec.loader.exec_module(report)


class NaturalRacingLineAnalysisTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.a=report.load(report.DATA/'analysis.json')

    def test_ranking_and_power3_gate_cannot_promote_an_unsafe_or_untracked_lap(self):
        stats=dict(RmsLateral=.1,MaximumLateral=.3,RmsVelocityHeadingDegrees=1,MaximumVelocityHeadingDegrees=2)
        self.assertEqual([],report.validation(True,True,stats,[20]*3))
        for complete,safe,change,laps in [(False,True,{},[20]*3),(True,False,{},[20]*3),(True,True,{'MaximumLateral':1.01},[20]*3),
                                          (True,True,{'MaximumVelocityHeadingDegrees':10.01},[20]*3),(True,True,{},[20,20,21])]:
            self.assertTrue(report.validation(complete,safe,stats|change,laps))
        self.assertFalse(report.power3_allowed([{'Rejections':[]},{'Rejections':['tracking']}]))
        self.assertTrue(report.power3_allowed([{'Rejections':['safety']},{'Rejections':['tracking']}]))
        self.assertTrue(report.load(report.DATA/'power4-gate.json')['BothRejected'])
        self.assertTrue(all(not p['Qualified'] for p in self.a['Pairs']))

    def test_exact_distance_and_speed_identity_exposes_false_power3_win(self):
        for p in self.a['Pairs']:
            self.assertAlmostEqual(p['DeltaSeconds'],p['DistanceCostSeconds']+p['SpeedEffectSeconds'],places=11)
            self.assertAlmostEqual(p['DeltaPercent'],100*p['DeltaSeconds']/p['InnerSeconds'])
        p=next(p for p in self.a['Pairs'] if p['Engine']=='power3')
        self.assertLess(p['DeltaSeconds'],0);self.assertLess(p['DistanceCostSeconds'],0);self.assertGreater(p['SpeedEffectSeconds'],0)

    def test_geometry_and_history_are_independent_of_run_rejection(self):
        self.assertTrue(all(g['GeometricallySafe'] for g in self.a['Geometry']))
        a,b=self.a['Geometry'];self.assertGreater(b['LengthMeters'],a['LengthMeters'])
        self.assertLess(b['MinimumRadiusMeters'],27)
        self.assertEqual((report.OLD/'crossover-inner-geometry.csv').read_bytes(),(report.DATA/'crossover-inner-geometry.csv').read_bytes())
        self.assertEqual(881,sum(report.history().values()))
        manifest=report.load(report.DATA/'manifest.json')
        for file,digest in manifest.items():self.assertEqual(digest,hashlib.sha256((report.DATA/file).read_bytes()).hexdigest().upper())

    def test_native_steps_show_actual_crossings_and_preserve_force_and_energy_accounting(self):
        for c in self.a['Cases']:
            self.assertTrue(c['Complete']);self.assertLess(c['MinimumOuter'],0);self.assertFalse(c['BoundarySafe'])
            self.assertIsNone(c['FlyingContainedStraightKph'])
            self.assertLess(c['Events']['PathAboveTotalBudget']['Time'],c['Events']['OuterCrossed']['Time'])
            self.assertLess(c['MaximumLinearImpulseResidualNs'],.002)
            self.assertLess(c['MaximumAngularImpulseResidualNms'],.00002)
            self.assertLess(abs(c['Energy']['BalanceResidualJoules']),5)
            self.assertEqual(0,c['Energy']['MaximumYawGuardExcessWatts'])
            self.assertLessEqual(c['MaximumUtilization'],1.000001)

    def test_offline_probes_distinguish_feasible_contact_root_from_a_reachable_lap(self):
        a=report.load(report.DATA/'fixed-state-probes.json')
        self.assertEqual(0,a['NewBodySteps']);self.assertEqual(0,a['WheelCommits']);self.assertFalse(a['ControllerEnabled'])
        for p in a['Probes']:
            self.assertAlmostEqual(p['ActualNormalNewtons'],p['RecordedNormalAtInitialVelocity'],delta=.01)
            for key in ['PathInverse','ControllerInverse']:
                inverse=p[key]
                if inverse['Status']==0:self.assertAlmostEqual(inverse['RequiredNewtons'],inverse['AchievedNewtons'],delta=.05)
        p=next(p for p in a['Probes'] if p['Name']=='power3-natural' and p['Event']=='TrackingAbove1m')
        self.assertEqual(0,p['PathInverse']['Status']);self.assertEqual(0,p['ControllerInverse']['Status'])
        self.assertLess(p['NormalAtLegacyTargetNewtons'],p['ControllerRequiredNewtons']-90)
        p=next(p for p in a['Prefixes'] if p['Name']=='power3-natural')
        self.assertIsNone(p['FirstUnreachablePath']);self.assertLess(p['FirstUnreachableController']['Time'],p['ThroughSeconds'])


if __name__=='__main__':unittest.main()
