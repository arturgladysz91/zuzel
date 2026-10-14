import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a10_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class PracticalContactTimeExtractionTests(unittest.TestCase):
    def test_all_original_39_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)

    def test_local_tolerance_geometry_floor_and_terminal_occupancy_are_pinned(self):
        for name, before, after in [
            ("src/CoreSim/Race/TwoRiderContactExecution.cs", "MaximumEventTimeToleranceSeconds = 1e-5", "MaximumEventTimeToleranceSeconds = 1"),
            ("src/CoreSim/Race/TwoRiderContactExecution.cs", "if (practicalTime && certifyOtherRiders is null)", "if (false)"),
            ("src/CoreSim/PhysicalSpace/ContestedSpace.cs", "minimumAllowedSeparation: -GeometryNumerics.ContactDistanceMeters", "minimumAllowedSeparation: -1"),
            ("src/CoreSim/Track/RiderIntegrationCursor.cs", "&& !Snapshot.Step.FreezeSurfaceForHeat", "&& false")]:
            source = (ROOT / name).read_text(encoding="utf-8")
            self.assertIn(before, source)
            with self.assertRaises(AssertionError):
                audit.restore_practical_contact_time(source.replace(before, after), name)
