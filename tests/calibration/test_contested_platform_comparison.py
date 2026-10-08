"""Regression coverage for exact original-main and platform-divergence preservation."""
import copy
import hashlib
import json
from pathlib import Path
import runpy
import tempfile
import unittest


helper = runpy.run_path(str(Path(__file__).resolve().parents[2] / "tools/contested-performance/compare.py"))


class ContestedPlatformComparisonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.windows = self.root / "determinism-windows-latest/contested-performance"
        self.ubuntu = self.root / "determinism-ubuntu-latest/contested-performance"
        self.w = {"baseline-drift": {"root.Distance": "double:3FF0000000000000"},
                  "previously-equal": {"root.Speed": "float:3F800000"}}
        self.u = copy.deepcopy(self.w)
        self.u["baseline-drift"]["root.Distance"] = "double:3FF0000000000001"
        self.capture(self.windows, "before", self.w)
        self.capture(self.ubuntu, "before", self.u)
        self.capture(self.windows, "after", self.w)
        self.capture(self.ubuntu, "after", self.u)

    def capture(self, directory, phase, cases):
        directory.mkdir(parents=True, exist_ok=True)
        manifest = {}
        for case, leaves in cases.items():
            raw = json.dumps(leaves, sort_keys=True, separators=(",", ":")).encode("utf-8")
            (directory / f"{phase}.json.{case.replace('/', '_')}.json").write_bytes(raw)
            manifest[case] = {"Leaves": len(leaves),
                              "IEEELeaves": sum(isinstance(v, str) and v.startswith(("float:", "double:")) for v in leaves.values()),
                              "SHA256": hashlib.sha256(raw).hexdigest().upper()}
        (directory / f"{phase}.json").write_text(json.dumps(manifest), encoding="utf-8")

    def assert_map_change_rejected(self):
        before = helper["divergence_map"](self.windows, self.ubuntu, "before")
        after = helper["divergence_map"](self.windows, self.ubuntu, "after")
        with self.assertRaisesRegex(AssertionError, "divergence map changed"):
            helper["compare_divergences"](before, after)
        # The CLI entry point must independently enforce per-platform preservation too.
        with self.assertRaises(AssertionError):
            helper["compare_platforms"](self.root)

    def test_unchanged_baseline_divergence_passes_and_retains_exact_evidence(self):
        result = helper["compare_platforms"](self.root)
        self.assertTrue(result["CrossPlatformDivergencesUnchanged"])
        self.assertEqual(1, result["DivergentCases"])
        report = json.loads((self.root / "contested-performance-comparison.json").read_text())
        self.assertEqual(report["BeforeDivergences"], report["AfterDivergences"])
        self.assertEqual({"Type": "double", "Bits": "3FF0000000000001"},
                         report["BeforeDivergences"]["baseline-drift"]["root.Distance"]["Ubuntu"])

    def test_new_divergence_in_formerly_equal_case_is_rejected(self):
        self.u["previously-equal"]["root.Speed"] = "float:3F800001"
        self.capture(self.ubuntu, "after", self.u)
        self.assert_map_change_rejected()

    def test_new_divergence_in_already_different_case_is_rejected(self):
        for platform, cases in [(self.windows, self.w), (self.ubuntu, self.u)]:
            cases["baseline-drift"]["root.Other"] = "float:00000000"
            self.capture(platform, "before", cases)
            self.capture(platform, "after", cases)
        self.u["baseline-drift"]["root.Other"] = "float:00000001"
        self.capture(self.ubuntu, "after", self.u)
        self.assert_map_change_rejected()

    def test_removed_divergence_is_rejected(self):
        self.capture(self.ubuntu, "after", self.w)
        self.assert_map_change_rejected()

    def test_changed_bits_including_signed_zero_and_nan_payload_are_rejected(self):
        for old, new in [("double:3FF0000000000001", "double:3FF0000000000002"),
                         ("double:0000000000000000", "double:8000000000000000"),
                         ("double:7FF8000000000001", "double:7FF8000000000002")]:
            with self.subTest(old=old, new=new):
                self.u["baseline-drift"]["root.Distance"] = old
                self.capture(self.ubuntu, "before", self.u)
                self.u["baseline-drift"]["root.Distance"] = new
                self.capture(self.ubuntu, "after", self.u)
                self.assert_map_change_rejected()

    def test_changed_type_or_path_is_rejected(self):
        for replacement in [{"root.Distance": "float:3F800000"},
                            {"root.RenamedDistance": "double:3FF0000000000001"}]:
            with self.subTest(replacement=replacement):
                self.u["baseline-drift"] = replacement
                self.capture(self.ubuntu, "after", self.u)
                self.assert_map_change_rejected()

    def test_path_missing_on_one_platform_is_part_of_the_map(self):
        self.u["previously-equal"]["root.Added"] = None
        self.capture(self.ubuntu, "after", self.u)
        drift = helper["divergence_map"](self.windows, self.ubuntu, "after")
        self.assertEqual({"Type": "missing"}, drift["previously-equal"]["root.Added"]["Windows"])
        self.assert_map_change_rejected()

    def test_per_platform_change_fails_even_if_divergence_map_is_unchanged(self):
        for platform, cases in [(self.windows, self.w), (self.ubuntu, self.u)]:
            cases["previously-equal"]["root.Speed"] = "float:40000000"
            self.capture(platform, "after", cases)
            with self.assertRaisesRegex(AssertionError, "Exact original-main behavior changed"):
                helper["compare"](platform)
        self.assertEqual(helper["divergence_map"](self.windows, self.ubuntu, "before"),
                         helper["divergence_map"](self.windows, self.ubuntu, "after"))

    def test_complete_map_is_not_truncated_to_first_twenty_fields(self):
        self.w["baseline-drift"] = {f"root[{i}]": "double:0000000000000000" for i in range(25)}
        self.u["baseline-drift"] = {f"root[{i}]": "double:0000000000000001" for i in range(25)}
        for platform, cases in [(self.windows, self.w), (self.ubuntu, self.u)]:
            self.capture(platform, "before", cases)
            self.capture(platform, "after", cases)
        self.assertEqual(25, helper["compare_platforms"](self.root)["DivergentLeaves"])
        self.u["baseline-drift"]["root[9]"] = "double:0000000000000002"  # Last ordinal path.
        self.capture(self.ubuntu, "after", self.u)
        self.assert_map_change_rejected()

    def test_missing_or_corrupt_raw_capture_cannot_be_skipped_even_for_equal_case(self):
        raw = self.ubuntu / "after.json.previously-equal.json"
        raw.unlink()
        with self.assertRaisesRegex(AssertionError, "Missing complete raw capture"):
            helper["compare_platforms"](self.root)
        raw.write_text("{}", encoding="utf-8")
        with self.assertRaisesRegex(AssertionError, "Raw capture hash mismatch"):
            helper["compare_platforms"](self.root)

    def test_case_coverage_changes_fail(self):
        del self.u["previously-equal"]
        self.capture(self.ubuntu, "after", self.u)
        with self.assertRaisesRegex(AssertionError, "Platform case coverage differs"):
            helper["divergence_map"](self.windows, self.ubuntu, "after")
        with self.assertRaisesRegex(AssertionError, "Capture case coverage changed"):
            helper["compare"](self.ubuntu)

    def test_real_capture_scalar_tags_have_distinct_type_identity(self):
        cases = [("string:1", 1), ("string:True", True),
                 ("string:Hold", "enum:CoreSim.Interactions.InteractionResponse:4"),
                 ("string:1", "char:0031"),
                 ("string:1.0", "decimal:0000000A:00000000:00000000:00010000")]
        for text, typed in cases:
            with self.subTest(text=text, typed=typed):
                self.assertNotEqual(helper["typed_leaf"](text), helper["typed_leaf"](typed))

    def test_json_types_remain_distinct_and_untyped_floats_fail(self):
        self.assertNotEqual(helper["typed_leaf"](True), helper["typed_leaf"](1))
        self.assertNotEqual(helper["typed_leaf"](1), helper["typed_leaf"]("1"))
        self.assertNotEqual(helper["typed_leaf"]("float:00000000"), helper["typed_leaf"]("double:0000000000000000"))
        with self.assertRaisesRegex(AssertionError, "Unsupported/untyped capture leaf"):
            helper["typed_leaf"](1.0)
