import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a8_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class FrozenHeatExtractionTests(unittest.TestCase):
    def test_all_original_39_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)

    def test_frozen_accounting_zero_cursor_and_certificate_are_pinned(self):
        for name, before, after in [
            ("src/CoreSim/Race/FrozenHeatContext.cs", "RequirePrefix(passage);", ""),
            ("src/CoreSim/SimulationEngine.cs", "distanceMeters / reference", "1"),
            ("src/CoreSim/Track/ExecutedPathTraversal.cs", "pause?.AdditionalSteps == 0", "false"),
            ("src/CoreSim/Race/TwoRiderContactExecution.cs", "Contact(upper.Report, final: true);", "")]:
            source = (ROOT / name).read_text(encoding="utf-8")
            self.assertIn(before, source)
            with self.assertRaises(AssertionError):
                audit.restore_frozen_heat(source.replace(before, after), name)
