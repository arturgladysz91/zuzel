import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a4_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class Rea004a4ExtractionTests(unittest.TestCase):
    def test_all_39_original_consumer_hashes_remain_unchanged(self):
        self.assertEqual(audit.verify(), 39)

    def test_changed_retention_and_physics_outside_cursor_blocks_are_rejected(self):
        name = "src/CoreSim/Track/ExecutedPathTraversal.cs"
        source = (ROOT / name).read_text(encoding="utf-8")
        with self.assertRaises(AssertionError):
            audit.restore_turnexit_cursor(source.replace("retainedCorner?.InitialEnvelopeSpeed", "retainedCorner?.CornerProgress"), name)
        with self.assertRaises(AssertionError):
            audit.restore_turnexit_cursor(source.replace("2d * ds / ((double)speed + next)", "3d * ds / ((double)speed + next)"), name)
