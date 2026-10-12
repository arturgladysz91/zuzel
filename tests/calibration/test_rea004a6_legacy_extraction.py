import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a6_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class CausalPairExtractionTests(unittest.TestCase):
    def test_all_original_39_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)

    def test_causal_adapter_and_original_physics_are_pinned(self):
        for name, before, after in [
            ("src/CoreSim/Race/TwoRiderContactExecution.cs", "tracker.ConsumePhysical(plan, observation);", ""),
            ("src/CoreSim/Track/RiderIntegrationCursor.cs", "_trials.Contains(trial)", "true"),
            ("src/CoreSim/Track/ExecutedPathTraversal.cs", "2d * ds / ((double)speed + next)", "3d * ds / ((double)speed + next)")]:
            source = (ROOT / name).read_text(encoding="utf-8")
            with self.assertRaises(AssertionError):
                audit.restore_causal_pair(source.replace(before, after), name)
