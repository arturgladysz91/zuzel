"""Verify the sealed #170 archive with its causal queue audit, without new worlds."""

import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs/experiments/two-corner-full-drive"


class TwoCornerFullDrive170Tests(unittest.TestCase):
    def test_manifest_and_frozen_native_evidence(self):
        for line in (DOCS / "SHA256SUMS").read_text(encoding="utf-8").splitlines():
            expected, name = line.split("  ", 1)
            self.assertEqual(hashlib.sha256((ROOT / name).read_bytes()).hexdigest(), expected, name)

        with tempfile.TemporaryDirectory() as temporary:
            with ZipFile(DOCS / "170-dowody.zip") as evidence:
                evidence.extractall(temporary)
            run = subprocess.run(
                [sys.executable, "tools/two-corner-full-drive/analyze170.py",
                 temporary, temporary, "--audit-only"],
                cwd=ROOT, check=True, capture_output=True, text=True, timeout=90,
            )
            stats = json.loads(run.stdout)
        summary = json.loads((DOCS / "summary.json").read_bytes())
        self.assertEqual(stats, summary["stats"])
        self.assertEqual((stats["baseline_pass"], stats["candidate_pass"]), (8, 8))
        self.assertEqual(stats["full_gas_exit_counts"], [8, 5])
        self.assertEqual(len(summary["pairs"]), 8)
        self.assertEqual(sum(not x["result"]["Pass"] for x in summary["rows"]), 4)
