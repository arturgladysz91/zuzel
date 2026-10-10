import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("yield_compatibility", ROOT / "tools/minimal-yield-audit/compatibility.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class MinimalYieldCompatibilityTests(unittest.TestCase):
    def test_disabled_behavior_cannot_be_changed_even_with_a_target_marker(self):
        for case in ("attack/False/False", "heat/I/interactions-OFF/Dry/7/False"):
            with self.assertRaises(ValueError):
                audit.verify_target_delta(case, {"Speed": 20}, {
                    "Speed": 21, "root.PhysicalTarget.OffsetFromInnerReferenceMeters": "float:40000000"})

    def test_unrelated_enabled_behavior_remains_exact(self):
        with self.assertRaises(ValueError):
            audit.verify_target_delta("four-separated/True/False", {"Speed": 20}, {"Speed": 21})
        self.assertEqual(audit.verify_target_delta("four-separated/True/False", {"Speed": 20}, {"Speed": 20}), {})

    def test_intentional_change_retains_every_typed_value_and_presence(self):
        changes = audit.verify_target_delta("attack/True/False", {"Speed": "float:40000000", "old": None}, {
            "Speed": "float:40000001", "root.PhysicalTarget.OffsetFromInnerReferenceMeters": "float:40000000"})
        self.assertEqual(set(changes), {"Speed", "old", "root.PhysicalTarget.OffsetFromInnerReferenceMeters"})
        self.assertEqual(changes["Speed"]["Before"], "float:40000000")
        self.assertFalse(changes["old"]["AfterPresent"])
