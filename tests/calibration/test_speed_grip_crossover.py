import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('crossover_report', ROOT / 'tools/shared-world-speedway/speed-grip-crossover-report.py')
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)


class SpeedGripCrossoverTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.analysis = report.load(report.DATA / 'analysis.json')
        cls.cases = {c['Name']: c for c in cls.analysis['Cases']}
        cls.pairs = {p['Configuration']: p for p in cls.analysis['Pairs']}

    def test_strategy_ranking_requires_complete_safe_tracked_periodic_pair(self):
        stats = dict(RmsLateral=.2, MaximumLateral=.5, RmsVelocityHeadingDegrees=1, MaximumVelocityHeadingDegrees=4)
        self.assertTrue(report.rankable(True, True, stats, [20, 20, 20]))
        for complete, safe, laps in [(False, True, [20]*3), (True, False, [20]*3), (True, True, [20, 21, 20]), (True, True, [20]*2)]:
            self.assertFalse(report.rankable(complete, safe, stats, laps))
        self.assertFalse(report.rankable(True, True, stats | {'MaximumLateral': 1.01}, [20]*3))
        self.assertEqual([p['Configuration'] for p in self.analysis['Pairs'] if p['Qualified']], ['control'])
        self.assertEqual(self.pairs['control']['Winner'], 'INNER')
        self.assertLess(self.pairs['power2']['Difference'], 0)
        self.assertIsNone(self.pairs['power2']['Winner'])
        self.assertEqual(self.analysis['Crossover'], 'INCONCLUSIVE')

    def test_distance_and_speed_decomposition_reconstructs_each_observed_delta(self):
        for p in self.analysis['Pairs']:
            self.assertAlmostEqual(p['DistanceCostSeconds'] + p['SpeedEffectSeconds'], p['Difference'], places=11)
        self.assertGreater(self.pairs['control']['DistanceCostSeconds'], .4)
        self.assertLess(self.pairs['control']['SpeedEffectSeconds'], 0)

    def test_overspeed_runs_wide_with_no_engine_work_or_extra_force(self):
        c = self.cases['stress200-h5']
        e = c['Events']['OuterFootprintCrossed']
        self.assertGreater(e['SpeedKph'], 180)
        self.assertLess(e['MotionCurvature'], e['PathCurvature'] / 5)
        self.assertGreater(e['RequiredPathAcceleration'], e['BudgetAcceleration'] * 5)
        self.assertLess(e['ActualNormalAcceleration'], e['BudgetAcceleration'])
        self.assertLess(c['Events']['RequiredPathAboveBudget']['Time'], e['Time'])
        self.assertEqual(c['Energy']['EngineWorkJoules'], 0)
        self.assertLessEqual(c['MaximumTyreUtilization'], 1)
        self.assertLess(c['MaximumLinearImpulseResidualNs'], .002)
        self.assertFalse(c['Rankable'])
        for speed in [160, 200]:
            rows = [c for c in self.analysis['Convergence'] if c['Name'].startswith(f'stress{speed}-')]
            self.assertGreater(rows[0]['PositionDifferenceAtHalfSecondVersus5ms'], rows[1]['PositionDifferenceAtHalfSecondVersus5ms'])
            self.assertLess(rows[0]['PositionDifferenceAtHalfSecondVersus5ms'], .01)
            self.assertLessEqual(max(r['FirstOuterFootprint']['Time'] for r in rows) - min(r['FirstOuterFootprint']['Time'] for r in rows), .021)

    def test_grip_changes_original_longitudinal_and_lateral_capability(self):
        mapping = report.load(report.DATA / 'grip-force-map.json')
        for turning in [False, True]:
            rows = [r for r in mapping if r['Turn'] == turning]
            for left, right in zip(rows, rows[1:]):
                for field in ['GripBudgetNewtons', 'DriveForceNewtons', 'AvailableTorqueNewtonMeters']:
                    self.assertGreater(left[field], right[field])
                if turning:
                    self.assertGreater(left['LateralForceNewtons'], right['LateralForceNewtons'])
            self.assertNotEqual(rows[0]['Grip'], rows[0]['EffectiveGrip'])
            self.assertGreater(rows[-1]['GripBudgetNewtons'] / rows[0]['GripBudgetNewtons'], .8)
        self.assertEqual(sum(self.analysis['HistoricalHashes'].values()), 505)


if __name__ == '__main__':
    unittest.main()
