import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("minimal_yield_audit", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class MinimalYieldExtractionTests(unittest.TestCase):
    def test_all_original_readers_and_hashes_are_preserved(self):
        self.assertEqual(audit.verify(), 39)

    def test_a_physical_target_algorithm_change_cannot_bypass_the_pinned_extraction(self):
        name = "src/CoreSim/Track/ExecutedPathTraversal.cs"
        source = (ROOT / name).read_text().replace("\r\n", "\n")
        with self.assertRaises(AssertionError):
            audit.restore_minimal_yield(source.replace("var lateralTarget =", "var changedTarget ="), name)
