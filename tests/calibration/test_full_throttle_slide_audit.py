"""Audit contracts: physical drift stability is different from legal strategy timing."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('slide_audit',ROOT/'tools/shared-world-speedway/full-throttle-slide-report.py')
audit=importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)
DATA=ROOT/'docs/experiments/full-throttle-slide-grip-audit'

class FullThrottleSlideAuditTests(unittest.TestCase):
    def test_safe_periodic_motion_does_not_authorize_a_failed_path_ranking(self):
        self.assertTrue(audit.periodic_motion(True,True,[16.1019,16.10191,16.10194]))
        self.assertFalse(audit.tracking_pass({'RmsError':.4556,'MaxError':1.235,'RmsHeadingDegrees':2.32,'MaxHeadingDegrees':7.59}))
        self.assertFalse(audit.periodic_motion(True,False,[16.1,16.1,16.1]))
        self.assertFalse(audit.periodic_motion(False,True,[16.1,16.1,16.1]))

    def test_native_decomposition_uses_original_forces_and_closes_the_work_ledger(self):
        rows=json.loads((DATA/'extraction.json').read_text())
        self.assertEqual(len(rows),9)
        for row in rows:
            self.assertEqual(row['MaximumForceReplayErrorNewtons'],0)
            e=row['Energy']
            residual=e['BodyChange']+e['WheelChange']-e['EngineWork']+e['LateralLoss']+e['LongitudinalLoss']+e['BearingLoss']-e['ResistanceWork']-e['YawWork']
            self.assertAlmostEqual(e['Residual'],residual,places=6)
            self.assertLess(abs(residual),5)
            self.assertGreater(e['LateralLoss'],0)

    def test_rejected_power_case_stops_at_the_first_physical_crossing(self):
        row=next(r for r in json.loads((DATA/'extraction.json').read_text()) if r['Name']=='power3-inner')
        self.assertLess(row['UsedRows'],row['NativeRows'])
        self.assertAlmostEqual(row['FirstPhysicalViolation']['End'],9.42)
        self.assertLess(row['FirstPhysicalViolation']['Outer'],0)
        self.assertFalse(row['BoundarySafe'])

    def test_repeat_and_perturbations_preserve_the_existing_baseline_fingerprint(self):
        old=json.loads((ROOT/'docs/experiments/speed-grip-line-crossover/baseline-inner.json').read_text())
        repeat=json.loads((DATA/'repeat18.json').read_text())
        self.assertEqual(old['Result']['Fingerprint'],repeat['Result']['Fingerprint'])
        for name in ['initial-minus5','initial-plus5']:
            row=json.loads((DATA/(name+'.json')).read_text())
            self.assertTrue(row['BoundarySafe'])
            self.assertTrue(audit.periodic_motion(row['Result']['Completed'],row['BoundarySafe'],row['LapSeconds'][-3:]))

if __name__=='__main__':unittest.main()
