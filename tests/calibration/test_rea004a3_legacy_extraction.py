import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a3_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class Rea004a3ExtractionTests(unittest.TestCase):
    def test_original_39_consumers_still_match_the_unchanged_historical_manifest(self):
        self.assertEqual(audit.verify(), 39)

    def test_cursor_plumbing_changes_and_duplicate_blocks_are_rejected(self):
        name = "src/CoreSim/Track/ExecutedPathTraversal.cs"
        source = (ROOT / name).read_text(encoding="utf-8")
        with self.assertRaises(AssertionError):
            audit.restore_integrator_cursor(source.replace("stepCount - resumeStepCount == limit", "stepCount == limit"), name)
        patch = json.loads((ROOT / "tests/fixtures/rea004a3-source-extraction.json").read_text())["Patches"][name][0]["After"]
        with self.assertRaises(AssertionError):
            audit.restore_integrator_cursor(source + patch, name)

    def test_physics_outside_cursor_blocks_remains_pinned(self):
        name = "src/CoreSim/Track/ExecutedPathTraversal.cs"
        source = (ROOT / name).read_text(encoding="utf-8")
        with self.assertRaises(AssertionError):
            audit.restore_integrator_cursor(source.replace("2d * ds / ((double)speed + endSpeed)", "3d * ds / ((double)speed + endSpeed)"), name)
