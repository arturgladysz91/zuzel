import copy
import importlib.util
import gzip
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("rea001_compare", Path(__file__).resolve().parents[2] / "tools/rea001-safety-audit/compare.py")
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)


class Rea001ComparisonTests(unittest.TestCase):
    def before(self):
        return dict(Failed=True, Failure=dict(Type="CoreSim.ExecutedPathTraversal+TimeSolveDiscontinuityException",
            Message="Coupled time solve did not converge within its bound."))

    def after(self):
        return dict(Failed=False, FinalHash="all exact final leaves", BehaviorHash="all exact behavior leaves",
                    Classification=[{}] * 4, Violations=[], Applied=[{}])

    def test_corrected_b_and_c_are_accepted(self):
        for configuration in "BC":
            comparison.compare_cases(self.before(), self.after(), configuration)

    def test_failed_after_is_rejected(self):
        after = dict(self.after(), Failed=True)
        with self.assertRaises(ValueError): comparison.compare_cases(self.before(), after, "B")

    def test_unrelated_original_failure_is_rejected(self):
        before = copy.deepcopy(self.before()); before["Failure"]["Type"] = "System.InvalidOperationException"
        with self.assertRaises(ValueError): comparison.compare_cases(before, self.after(), "C")

    def test_suppressed_contact_is_rejected(self):
        with self.assertRaises(ValueError): comparison.compare_cases(self.before(), dict(self.after(), Applied=[]), "C")

    def test_incomplete_classification_is_rejected(self):
        with self.assertRaises(ValueError): comparison.compare_cases(self.before(), dict(self.after(), Classification=[]), "B")

    def test_production_violation_is_rejected(self):
        with self.assertRaises(ValueError): comparison.compare_cases(self.before(), dict(self.after(), Violations=["invented clearance"]), "C")

    def test_feature_off_is_exact(self):
        comparison.compare_cases(self.after(), self.after(), "A")
        for key in ("FinalHash", "BehaviorHash"):
            with self.assertRaises(ValueError): comparison.compare_cases(self.after(), dict(self.after(), **{key: "one changed IEEE bit"}), "A")

    def platform_trace(self, directory, value):
        for configuration in "BC":
            output = Path(directory) / f"after-{configuration}"
            output.mkdir(parents=True)
            case = dict(self.after(), Id="outside", Seed=7, Configuration=configuration,
                        ReversedExact=True, ObserverExact=True)
            (output / "manifest.json").write_text(json.dumps(dict(Schema="race-readiness-v1", Completed=True,
                Mode="repro", Cases=[case])))
            with gzip.open(output / f"case-outside-7-{configuration}.json.gz", "wt") as stream:
                json.dump(case, stream)
            with gzip.open(output / f"trace-outside-7-{configuration}.json.gz", "wt") as stream:
                json.dump(value[configuration] if type(value) is dict else {"root.Motions[0].Speed": value}, stream)

    def expected(self, left=None, right=None):
        differences = comparison.divergence_map(left or {}, right or {})
        return {"Divergences": {"outside/7/B": differences} if differences else {},
                "SummaryBehaviorHashes": {os: self.after()["BehaviorHash"] for os in ("Windows", "Ubuntu")}}

    def test_new_platform_ieee_bit_divergence_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = Path(directory) / "windows", Path(directory) / "ubuntu"
            self.platform_trace(windows, "float:3F800000"); self.platform_trace(ubuntu, "float:3F800001")
            with self.assertRaises(ValueError): comparison.compare_platforms(windows, ubuntu, self.expected())

    def test_new_platform_numeric_type_divergence_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = Path(directory) / "windows", Path(directory) / "ubuntu"
            self.platform_trace(windows, "float:3F800000"); self.platform_trace(ubuntu, "double:3FF0000000000000")
            with self.assertRaises(ValueError): comparison.compare_platforms(windows, ubuntu, self.expected())

    def test_identical_complete_typed_platform_traces_are_accepted(self):
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = Path(directory) / "windows", Path(directory) / "ubuntu"
            self.platform_trace(windows, "float:3F800000"); self.platform_trace(ubuntu, "float:3F800000")
            comparison.compare_platforms(windows, ubuntu, self.expected())

    def native_b_traces(self, directory, left, right):
        windows, ubuntu = Path(directory) / "windows", Path(directory) / "ubuntu"
        self.platform_trace(windows, {"B": left, "C": {"root.Motion": "float:3F800000"}})
        self.platform_trace(ubuntu, {"B": right, "C": {"root.Motion": "float:3F800000"}})
        return windows, ubuntu

    def test_exact_known_native_b_divergence_is_accepted_and_reported(self):
        left = {"root.Diagnostic": "double:3FF0000000000000"}
        right = {"root.Diagnostic": "double:3FF0000000000001"}
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = self.native_b_traces(directory, left, right)
            expected = self.expected(left, right)
            report_path = Path(directory) / "report.json"
            report = comparison.compare_platforms(windows, ubuntu, expected, report_path)
            self.assertEqual(report["ObservedDivergences"], expected["Divergences"])
            self.assertEqual(json.loads(report_path.read_text()), report)
            self.assertTrue(report["Cases"]["B"]["FinalStateExact"])
            self.assertTrue(report["Cases"]["C"]["FullAuditExact"])

    def test_additional_native_b_divergence_is_rejected(self):
        left = {"root.Diagnostic": "double:3FF0000000000000", "root.New": "float:3F800000"}
        right = {"root.Diagnostic": "double:3FF0000000000001", "root.New": "float:3F800001"}
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = self.native_b_traces(directory, left, right)
            expected = self.expected({"root.Diagnostic": left["root.Diagnostic"]}, {"root.Diagnostic": right["root.Diagnostic"]})
            with self.assertRaisesRegex(ValueError, "divergence map changed"):
                comparison.compare_platforms(windows, ubuntu, expected)

    def test_removed_native_b_divergence_is_rejected(self):
        left = {"root.Diagnostic": "double:3FF0000000000000"}
        right = {"root.Diagnostic": "double:3FF0000000000001"}
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = self.native_b_traces(directory, left, left)
            with self.assertRaisesRegex(ValueError, "divergence map changed"):
                comparison.compare_platforms(windows, ubuntu, self.expected(left, right))

    def test_changed_native_b_path_type_or_exact_bits_is_rejected(self):
        left = {"root.Diagnostic": "double:3FF0000000000000"}
        right = {"root.Diagnostic": "double:3FF0000000000001"}
        for changed in ({"root.Diagnostic": "double:3FF0000000000002"},
                        {"root.Diagnostic": "float:3F800000"},
                        {"root.Other": right["root.Diagnostic"]}, {}):
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as directory:
                windows, ubuntu = self.native_b_traces(directory, left, changed)
                with self.assertRaisesRegex(ValueError, "divergence map changed"):
                    comparison.compare_platforms(windows, ubuntu, self.expected(left, right))

    def test_c_remains_strict_even_if_expected_map_mentions_c(self):
        left = {"root.Motion": "float:3F800000"}; right = {"root.Motion": "float:3F800001"}
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = Path(directory) / "windows", Path(directory) / "ubuntu"
            self.platform_trace(windows, {"B": left, "C": left})
            self.platform_trace(ubuntu, {"B": left, "C": right})
            expected = self.expected()
            expected["Divergences"] = {"outside/7/C": comparison.divergence_map(left, right)}
            with self.assertRaisesRegex(ValueError, "Strict C"):
                comparison.compare_platforms(windows, ubuntu, expected)

    def test_final_production_hash_change_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = self.native_b_traces(directory, {}, {})
            output = ubuntu / "after-B"
            manifest = json.loads((output / "manifest.json").read_text())
            manifest["Cases"][0]["FinalHash"] = "changed final rider/log/surface IEEE bit"
            (output / "manifest.json").write_text(json.dumps(manifest))
            with gzip.open(output / "case-outside-7-B.json.gz", "rt") as stream: case = json.load(stream)
            case["FinalHash"] = manifest["Cases"][0]["FinalHash"]
            with gzip.open(output / "case-outside-7-B.json.gz", "wt") as stream: json.dump(case, stream)
            with self.assertRaisesRegex(ValueError, "final production states differ"):
                comparison.compare_platforms(windows, ubuntu, self.expected())

    def test_divergence_map_preserves_non_ieee_types_and_missing_paths(self):
        result = comparison.divergence_map({"root.Bool": True, "root.Null": None}, {"root.Bool": 1})
        self.assertEqual(result["root.Bool"]["Windows"]["Type"], "boolean")
        self.assertEqual(result["root.Bool"]["Ubuntu"]["Type"], "integer")
        self.assertEqual(result["root.Null"]["Windows"]["Type"], "null")
        self.assertEqual(result["root.Null"]["Ubuntu"]["Type"], "missing")
