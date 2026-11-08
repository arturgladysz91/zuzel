"""Check the evidence provenance and model isolation of the #175 audit."""
import json
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]


class TrackGripRootCause175Tests(unittest.TestCase):
    def test_sealed_model_and_native_evidence_stay_separate(self):
        result = subprocess.run([sys.executable, str(ROOT/'tools/track-grip-root-cause/verify175.py')],
                                cwd=ROOT, check=True, capture_output=True, text=True, timeout=120)
        audit = json.loads(result.stdout)
        self.assertEqual(audit['paired_states'],56)
        self.assertEqual(audit['new_native_worlds'],0)
        self.assertEqual(audit['new_s3'],'UNKNOWN')
