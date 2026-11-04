"""Protect the sealed full-bend study and verify the native queue evidence."""

import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs/experiments/whole-corner-planning"


class WholeCornerPlanning171Tests(unittest.TestCase):
    def test_frozen_archive_and_mapping(self):
        for line in (DOCS / "SHA256SUMS").read_text(encoding="utf-8").splitlines():
            digest, path = line.split("  ", 1)
            self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), digest, path)

        with tempfile.TemporaryDirectory() as temporary:
            with ZipFile(DOCS / "171-dowody.zip") as evidence:
                evidence.extractall(temporary)
            result = subprocess.run(
                [sys.executable, "tools/whole-corner-planning/report171.py",
                 temporary, temporary, "--audit-only"],
                cwd=ROOT, check=True, capture_output=True, text=True, timeout=150,
            )
        stats = json.loads(result.stdout)
        summary = json.loads((DOCS / "summary.json").read_bytes())
        self.assertEqual(stats, summary["stats"])
        self.assertEqual((stats["baseline_pass"], stats["candidate_pass"]), (8, 8))
        self.assertEqual(stats["candidate"]["Strategy"], "K4")
        self.assertEqual(stats["worlds"], 30)
        self.assertEqual(len(summary["mapping"]), 6)
        self.assertEqual(sum(len(x["Sections"]) - 1 for x in summary["mapping"]), 18)
        self.assertTrue(all(p["gain"] > 0 for p in summary["pairs"]))
