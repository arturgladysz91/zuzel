import importlib.util
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('inverse_report',ROOT/'tools/shared-world-speedway/inverse-feasibility-report.py')
report=importlib.util.module_from_spec(spec);spec.loader.exec_module(report)


class InverseFeasibilityAnalysisTests(unittest.TestCase):
    def test_strategy_gate_requires_inverse_tracking_safety_and_stability(self):
        self.assertTrue(report.strategy_time_gate('PASS',True,True,20,20))
        for args in [('INCONCLUSIVE',True,True,20,20),('FAIL',True,True,20,20),('PASS',False,True,20,20),('PASS',True,False,20,20),('PASS',True,True,20,21)]:
            self.assertFalse(report.strategy_time_gate(*args))

    def test_distance_and_speed_effect_exactly_recover_time_difference(self):
        distance,speed=report.distance_decomposition(350,17,325,16)
        self.assertAlmostEqual(distance+speed,1,places=12)
        self.assertGreater(distance,0);self.assertLess(speed,0)

    def test_no_constructive_or_strategy_pass_is_inferred_from_grip(self):
        a=json.loads((report.DATA/'analysis.json').read_text(encoding='utf-8'))
        self.assertEqual(a['ConstructiveNativeProof'],'INCONCLUSIVE');self.assertIsNone(a['StrategyWinner'])
        self.assertLess(a['Inverse']['outside-inside-outside-inverse-18']['MaximumScalarGripFraction'],1)
        self.assertGreater(a['Inverse']['outside-inside-outside-inverse-18']['MaximumDrive'],1.5)
        self.assertFalse(any(c['StrategyTimeGate'] for c in a['Cases']))

    def test_one_sided_entry_jump_converges_and_lower_speed_keeps_it_nonzero(self):
        a=json.loads((report.DATA/'knot-probes.json').read_text(encoding='utf-8'))
        for speed,lower,upper in [(18,.16,.17),(15,.09,.10)]:
            rows=[r for r in a if r['Path']=='outside-inside-outside' and r['Speed']==speed and r['Knot']==35]
            fine=next(r for r in rows if r['H']==.005);coarse=next(r for r in rows if r['H']==.01)
            self.assertGreater(fine['DeltaYaw'],lower);self.assertLess(fine['DeltaYaw'],upper)
            self.assertLess(abs(fine['DeltaYaw']-coarse['DeltaYaw']),.0001)


if __name__=='__main__':unittest.main()
