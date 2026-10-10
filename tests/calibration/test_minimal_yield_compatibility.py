import importlib.util
from pathlib import Path
import unittest
import copy

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

    def test_contact_production_requires_observed_target_work_and_exact_kernel(self):
        before = {"Controlled": [1], "RawGrid": [2], "LegacyComparisons": [{"Name": "attack", "Seed": 7, "Speed": 20}]}
        after = copy.deepcopy(before)
        after["LegacyComparisons"][0]["Speed"] = 21
        trials = {"LegacyComparisons/attack/7": 0}
        with self.assertRaises(AssertionError):
            audit.contact_production_changes(before, after, trials, ("LegacyComparisons",))
        trials["LegacyComparisons/attack/7"] = 1
        changes = audit.contact_production_changes(before, after, trials, ("LegacyComparisons",))
        self.assertEqual(changes["LegacyComparisons/attack/7"]["root.Speed"]["After"], 21)
        after["RawGrid"] = [3]
        with self.assertRaises(AssertionError):
            audit.contact_production_changes(before, after, trials, ("LegacyComparisons",))

    def test_contact_totals_are_derived_and_row_identity_cannot_change(self):
        row = {"Scenario": "I", "Seed": 7, "Weather": "Dry", "VerifiedContacts": 1,
               "AppliedRiders": 1, "AppliedContacts": 1, "ContactCrashes": 0,
               "RepeatedOverlapSuppressions": 0, "Deferred": 0, "GeometryUnresolved": 0,
               "Classes": {"Brush": 1}}
        before = {"Heats": [row], "Totals": audit.consequence_totals([row]), "Controlled": [1]}
        after = copy.deepcopy(before)
        trials = {"Heats/I/7/Dry": 1}
        after["Totals"]["VerifiedContacts"] = 2
        with self.assertRaises(AssertionError):
            audit.contact_production_changes(before, after, trials, ("Heats",), True)
        after = copy.deepcopy(before)
        after["Heats"][0]["Seed"] = 19
        with self.assertRaises(AssertionError):
            audit.contact_production_changes(before, after, trials, ("Heats",), True)
