"""Verify the cyclic adapters and the sealed native three-lap evidence."""

import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs/experiments/three-lap-global-planning"


class ThreeLapGlobalPlanning172Tests(unittest.TestCase):
    def test_mechanical_derivation_keeps_sealed_physics(self):
        subprocess.run([sys.executable, "tools/three-lap-global-planning/generate172.py", "--check"],
                       cwd=ROOT, check=True, capture_output=True, text=True, timeout=30)

    def test_three_lap_receipts_and_full_pending_queue(self):
        for line in (DOCS / "SHA256SUMS").read_text(encoding="utf-8").splitlines():
            digest, path = line.split("  ", 1)
            self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), digest, path)
        with tempfile.TemporaryDirectory() as temporary:
            for name in ("172-development.zip", "172-validation-K4.zip", "172-validation-C0.zip", "172-protocol.zip"):
                with ZipFile(DOCS / name) as archive:
                    archive.extractall(temporary)
            output = subprocess.run(
                [sys.executable, "tools/three-lap-global-planning/analyze172.py",
                 temporary, temporary, "--audit-only"],
                cwd=ROOT, check=True, capture_output=True, text=True, timeout=150,
            )
        stats = json.loads(output.stdout)
        summary = json.loads((DOCS / "summary.json").read_bytes())
        self.assertEqual(stats, summary["stats"])
        self.assertEqual((stats["pass_k4"], stats["pass_c0"], stats["accepted_six"]), (5, 5, 5))
        self.assertEqual(stats["worlds"], 16)
        self.assertEqual(len(summary["mapping"]["Records"]), 18)
        self.assertTrue(summary["mapping"]["AllContinuous"])
        self.assertTrue(all(row["result"]["S3"] and row["result"]["Reserve"]
                            for row in summary["rows"] if row["result"]["Stage"] == "validate"))
