import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a9_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class FirstContactExtractionTests(unittest.TestCase):
    def test_all_original_39_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)

    def test_refinement_limits_and_synchronization_are_exactly_pinned(self):
        for name, before, after in [
            ("src/CoreSim/PhysicalSpace/ContestedSpace.cs", "work.RefineFirstContact && needsFirst", "true"),
            ("src/CoreSim/Track/ExecutedPathTraversal.cs", "hiTime - loTime > search.TimeBracketToleranceSeconds", "false"),
            ("src/CoreSim/Track/RiderIntegrationCursor.cs", "timeBracketToleranceSeconds > ExecutedPathTraversal.TimeToleranceSeconds", "false"),
            ("src/CoreSim/Race/TwoRiderContactExecution.cs", "if (!Aligned(upper.Boundaries, time))", "if (false)")]:
            source = (ROOT / name).read_text(encoding="utf-8")
            self.assertIn(before, source)
            with self.assertRaises(AssertionError):
                audit.restore_first_contact(source.replace(before, after), name)
