import importlib.util
import struct
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


def load(name):
    spec = importlib.util.spec_from_file_location("readiness_" + name, ROOT / "tools/race-engine-readiness" / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


summary = load("summarize")
comparison = load("compare")


class ReadinessAuditTests(unittest.TestCase):
    def test_quantiles_use_existing_linear_definition(self):
        self.assertEqual(summary.quantiles([4, 1, 3, 2])["P50"], 2.5)
        self.assertEqual(summary.quantiles([]), {"N": 0})

    def test_exposure_unions_overlap_without_double_count(self):
        self.assertEqual(summary.union_duration([(0, 3), (1, 2), (2, 4), (7, 8)]), 5)

    def test_backwards_exposure_is_rejected(self):
        with self.assertRaises(ValueError):
            summary.union_duration([(2, 1)])

    def test_incomplete_audit_is_rejected(self):
        with self.assertRaises(ValueError):
            summary.validate({"Schema": "race-readiness-v1", "Completed": False}, [])

    def test_changed_exact_hash_is_rejected(self):
        row = dict(Id="x", Seed=19, Configuration="C", ReversedExact=True, ObserverExact=True,
                   FinalHash="original", BehaviorHash="original")
        before = dict(Schema="race-readiness-v1", Completed=True, Cases=[row])
        after = dict(before, Cases=[dict(row, BehaviorHash="new IEEE bit")])
        with self.assertRaises(ValueError):
            comparison.compare(before, after)

    def test_measurement_cost_does_not_change_behavior_comparison(self):
        row = dict(Id="x", Seed=19, Configuration="C", ReversedExact=True, ObserverExact=True,
                   FinalHash="original", BehaviorHash="original", Milliseconds=1)
        before = dict(Schema="race-readiness-v1", Completed=True, Cases=[row])
        comparison.compare(before, dict(before, Cases=[dict(row, Milliseconds=2)]))

    def test_missing_case_is_rejected(self):
        row = dict(Id="x", Seed=19, Configuration="C", ReversedExact=True, ObserverExact=True,
                   FinalHash="original", BehaviorHash="original")
        before = dict(Schema="race-readiness-v1", Completed=True, Cases=[row])
        with self.assertRaises(ValueError):
            comparison.compare(before, dict(before, Cases=[]))

    def test_shortest_single_clock_literals_do_not_create_false_residuals(self):
        # Actual .NET shortest literals from the pilot, not double-valued source observations.
        result = summary.check_accumulation([self.accumulation_case(4.1066055)])
        self.assertEqual(result["AccumulationResiduals"], [])

    def test_new_exact_clock_bit_residual_is_preserved(self):
        bits = struct.unpack("<I", struct.pack("<f", 4.1066055))[0]
        changed = struct.unpack("<f", struct.pack("<I", bits + 1))[0]
        result = summary.check_accumulation([self.accumulation_case(changed)])
        residual = result["AccumulationResiduals"][0]
        self.assertEqual(residual["Kind"], "clock accumulation")
        self.assertEqual(residual["Observed"], changed)

    @staticmethod
    def accumulation_case(next_time):
        return dict(Id="source", Seed=19, Configuration="A", Pairs=[], Generations=[],
                    Initial=[dict(Profile=dict(Id=1), Position=dict(DistanceMeters=0))],
                    Riders=[dict(RiderId=1, TotalDistanceMeters=2)],
                    Segments=[dict(RiderId=1, Step=0, TotalDistanceMeters=1,
                                   StartElapsedTimeSeconds=2.9568353, TotalTimeSeconds=1.1497703),
                              dict(RiderId=1, Step=1, TotalDistanceMeters=1,
                                   StartElapsedTimeSeconds=next_time, TotalTimeSeconds=1)])

    def test_failed_heat_cannot_pass_smoke_even_if_baseline_also_fails(self):
        row = dict(Id="x", Seed=19, Configuration="C", ReversedExact=True, ObserverExact=True,
                   FinalHash="original", BehaviorHash="original", Failed=True)
        before = dict(Schema="race-readiness-v1", Completed=True, Cases=[row])
        with self.assertRaises(ValueError):
            comparison.compare(before, before)
