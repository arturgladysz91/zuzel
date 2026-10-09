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
                json.dump({"root.Motions[0].Speed": value}, stream)

    def test_new_platform_ieee_bit_divergence_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = Path(directory) / "windows", Path(directory) / "ubuntu"
            self.platform_trace(windows, "float:3F800000"); self.platform_trace(ubuntu, "float:3F800001")
            with self.assertRaises(ValueError): comparison.compare_platforms(windows, ubuntu)

    def test_new_platform_numeric_type_divergence_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = Path(directory) / "windows", Path(directory) / "ubuntu"
            self.platform_trace(windows, "float:3F800000"); self.platform_trace(ubuntu, "double:3FF0000000000000")
            with self.assertRaises(ValueError): comparison.compare_platforms(windows, ubuntu)

    def test_identical_complete_typed_platform_traces_are_accepted(self):
        with tempfile.TemporaryDirectory() as directory:
            windows, ubuntu = Path(directory) / "windows", Path(directory) / "ubuntu"
            self.platform_trace(windows, "float:3F800000"); self.platform_trace(ubuntu, "float:3F800000")
            comparison.compare_platforms(windows, ubuntu)
