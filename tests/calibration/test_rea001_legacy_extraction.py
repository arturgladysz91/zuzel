import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea001_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class Rea001LegacyExtractionTests(unittest.TestCase):
    def test_all_39_original_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)

    def test_unreviewed_failure_guard_change_is_rejected(self):
        name = "src/CoreSim/Track/ExecutedPathTraversal.cs"
        source = (ROOT / name).read_text().replace("!double.IsFinite(seconds)", "true")
        with self.assertRaises(AssertionError): audit.restore_optional_safety_contract(source, name)
