import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a5_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class Rea004a5ExtractionTests(unittest.TestCase):
    def test_original_39_consumer_hashes_and_older_extractions_stay_unchanged(self):
        self.assertEqual(audit.verify(), 39)

    def test_partial_execution_and_unchanged_physics_are_both_pinned(self):
        name = "src/CoreSim/Track/ExecutedPathTraversal.cs"
        source = (ROOT / name).read_text(encoding="utf-8")
        for altered in (source.replace("finish = hi;", "finish = lo;"),
                        source.replace("2d * ds / ((double)speed + next)", "3d * ds / ((double)speed + next)")):
            with self.assertRaises(AssertionError):
                audit.restore_partial_step(altered, name)

    def test_pose_clock_and_event_resume_barrier_cannot_drift(self):
        name = "src/CoreSim/PhysicalSpace/BikePoseIntervals.cs"
        source = (ROOT / name).read_text(encoding="utf-8")
        with self.assertRaises(AssertionError):
            audit.restore_partial_step(source.replace("_duration = duration", "_duration = (float)duration"), name)
        name = "src/CoreSim/Track/RiderIntegrationCursor.cs"
        source = (ROOT / name).read_text(encoding="utf-8")
        with self.assertRaises(AssertionError):
            audit.restore_partial_step(source.replace("Boundary.PartialStep is not null", "false"), name)
