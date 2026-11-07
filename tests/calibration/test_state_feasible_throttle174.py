"""Protect #174 causal attribution, frozen physics and the failed S3 cost gate."""
import json
from pathlib import Path
import subprocess
import sys
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / 'tools/state-feasible-throttle'
DOCS = ROOT / 'docs/experiments/state-feasible-throttle'


class StateFeasibleThrottle174Tests(unittest.TestCase):
    def test_fork_helpers_and_frozen_historical_inputs(self):
        result = subprocess.run([sys.executable, str(TOOL / 'generate174.py'), '--check'],
                                check=True, capture_output=True, text=True, timeout=30)
        self.assertIn('originals untouched', result.stdout)

    def test_sealed_continuous_native_prefixes_pending_and_complete_goal(self):
        result = subprocess.run([sys.executable, str(TOOL / 'verify174.py')], cwd=ROOT,
                                check=True, capture_output=True, text=True, timeout=150)
        proof = json.loads(result.stdout)
        self.assertEqual(proof['native_legal'], 3)
        self.assertEqual(proof['validation_worlds'], 0)
        self.assertEqual(proof['verdict'], 'PARTIAL')
        self.assertTrue(proof['heldout_unconsumed'])

    def test_model_rejections_and_performance_gate_cannot_be_counted_as_s3(self):
        summary = json.loads((DOCS / 'summary174.json').read_bytes())
        rejected = {r['Id']: r for r in summary['states'] if not r['Evaluation']['Admitted']}
        self.assertEqual(set(rejected), {'E41', 'E25', 'E12', 'XB'})
        self.assertTrue(all(r['first_model_violation'] for r in rejected.values()))
        self.assertTrue(all(r['BASE'] == r['NEW'] == 'NOT_RUN' for r in summary['validation']))
        self.assertTrue(all(r['maximum_warm_ms'] > 3 * 200 for r in summary['development']))
        with ZipFile(DOCS / '174-diagnostics.zip') as z:
            closure = json.loads(z.read('native-closed.json'))
        self.assertFalse(closure['FullS3Gate'])
        self.assertEqual(closure['Verdict'], 'PARTIAL')
