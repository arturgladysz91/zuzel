import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a12_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class FourRiderContinuationExtractionTests(unittest.TestCase):
    def test_all_original_39_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)

    def test_resume_geometry_generation_and_actual_replay_meter_are_pinned(self):
        for name, before, after in [
            ("src/CoreSim/Race/FourRiderCausalCoordinator.cs", "Work.Accepted(cursor)", "Work.Starting(cursor.Snapshot, 0)"),
            ("src/CoreSim/Race/FourRiderCausalCoordinator.cs", "Published.ContactOwnership?.Consumed == true", "true"),
            ("src/CoreSim/Race/FourRiderCausalCoordinator.cs", "cannot certify nonpenetration", "ignore nonpenetration"),
            ("src/CoreSim/Track/RiderIntegrationCursor.cs", "Contact = session.Contact", "Contact = null"),
            ("src/CoreSim/Track/ExecutedPathTraversal.cs", "ProjectionCaptureAudit.CausalStepStarting?.Invoke", "ProjectionCaptureAudit.CausalStepStarting.Invoke"),
            ("src/CoreSim/Track/ProjectionCaptureAudit.cs", "float, float, bool", "float, float, float")]:
            source = (ROOT / name).read_text(encoding="utf-8")
            self.assertIn(before, source)
            with self.assertRaises(AssertionError):
                audit.restore_four_rider_continuation(source.replace(before, after), name)
