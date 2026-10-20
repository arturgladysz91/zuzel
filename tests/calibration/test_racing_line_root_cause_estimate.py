import importlib.util
import json
import math
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('root_cause_report',ROOT/'tools/shared-world-speedway/racing-line-root-cause-report.py')
report=importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)


class RacingLineRootCauseEstimateTests(unittest.TestCase):
    def test_analytic_circle_at_the_friction_limit(self):
        radius=30;acceleration=10;lap=2*math.pi*radius;n=1024
        points=[{'s':i*lap/n,'metric':1,'curvature':1/radius} for i in range(n)]
        summary,speed=report.speed_profile(points,lap,acceleration,lambda v,p:100,lambda v,p:0)
        self.assertAlmostEqual(summary['LapSeconds'],2*math.pi*math.sqrt(radius/acceleration),places=10)
        self.assertAlmostEqual(min(speed),math.sqrt(radius*acceleration),places=10)

    def test_no_drive_cannot_create_acceleration_after_the_slow_corner(self):
        n=100;lap=200
        points=[{'s':i*2,'metric':1,'curvature':.1 if i<20 else .01} for i in range(n)]
        summary,speed=report.speed_profile(points,lap,10,lambda v,p:0,lambda v,p:0)
        self.assertAlmostEqual(min(speed),10,places=10)
        self.assertAlmostEqual(max(speed),10,places=10)
        self.assertAlmostEqual(summary['LapSeconds'],20,places=10)

    def test_wheel_ablation_cannot_run_after_failed_tracking(self):
        analysis=json.loads((ROOT/'docs/experiments/racing-line-root-cause/analysis.json').read_text(encoding='utf-8'))
        self.assertFalse(analysis['WheelComparisonExecuted'])
        self.assertTrue(any(not c['TrackingPass'] for c in analysis['Cases'] if c['Path']))
        for row in json.loads((ROOT/'docs/experiments/racing-line-root-cause/point-mass-summary.json').read_text(encoding='utf-8')):
            self.assertLess(row['MaximumForwardConstraintResidual'],1e-5)
            self.assertLess(row['MaximumBrakeConstraintResidual'],1e-5)


if __name__=='__main__':
    unittest.main()
