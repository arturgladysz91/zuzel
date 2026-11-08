"""Reject stale wheel/actuator commits and unqualified native claims."""
import json
from pathlib import Path
import subprocess
import sys
import unittest

ROOT=Path(__file__).resolve().parents[2]


class DynamicAxleLoad176Tests(unittest.TestCase):
    def test_corrected_steps_and_excluded_integration_attempts(self):
        p=subprocess.run([sys.executable,str(ROOT/'tools/dynamic-axle-load/verify176.py')],
                         cwd=ROOT,check=True,capture_output=True,text=True,timeout=150)
        report=json.loads(p.stdout)
        self.assertEqual(report['corrected'],2)
        self.assertEqual(report['invalid_integration'],12)
        self.assertGreater(report['load_intervals'],500)
        self.assertEqual(report['validation_worlds'],0)
