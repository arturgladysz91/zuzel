import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a7_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class RealPairAdmissionExtractionTests(unittest.TestCase):
    def test_all_original_39_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)

    def test_admission_and_world_guard_are_exactly_pinned(self):
        for name, before, after in [
            ("src/CoreSim/Race/TwoRiderContactExecution.cs", "!ReferenceEquals(origins[0].TrackState, origins[1].TrackState)", "false"),
            ("src/CoreSim/Race/TwoRiderContactExecution.cs", "Distinct().Count() != 1", "Distinct().Count() != 2"),
            ("src/CoreSim/SimulationEngine.cs", "snapshot.Segment.Type == SegmentType.Straight", "snapshot.Segment.Type == SegmentType.TurnExit")]:
            source = (ROOT / name).read_text(encoding="utf-8")
            self.assertIn(before, source)
            with self.assertRaises(AssertionError):
                audit.restore_real_pair_admission(source.replace(before, after), name)
