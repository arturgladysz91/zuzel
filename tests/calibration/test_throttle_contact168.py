"""Read-only proof of the matched native intervention and the passive contact grid."""
from collections import defaultdict
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs/experiments/throttle-contact-audit"
ARCHIVED = ROOT / "docs/experiments/fast-two-corners/evidence.zip"


class ThrottleContact168Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.evidence = ZipFile(DOCS / "168-dowody.zip")
        cls.addClassCleanup(cls.evidence.close)
        cls.trials = json.loads(cls.evidence.read("runs.json"))
        cls.measure = json.loads(cls.evidence.read("measurements.json"))
        cls.native = ZipFile(BytesIO(cls.evidence.read("native.zip")))
        cls.addClassCleanup(cls.native.close)

    def read_native(self, name):
        return json.loads(self.native.read(name + ".json"))

    def test_seals_and_budget(self):
        for line in (DOCS / "SHA256SUMS").read_text(encoding="ascii").splitlines():
            sha, name = line.split("  ")
            self.assertEqual(sha, hashlib.sha256((DOCS / name).read_bytes()).hexdigest())
        for name, sha in json.loads(self.evidence.read("sources.json")).items():
            self.assertEqual(sha, hashlib.sha256((ROOT / name).read_bytes()).hexdigest())
        plan = json.loads(self.evidence.read("budget.json"))
        receipt = json.loads(self.evidence.read("analysis-receipt.json"))
        self.assertEqual(26, receipt["rollouts"])
        self.assertEqual(26, plan["ReservedRollouts"])
        self.assertLess(receipt["combined_cpu_seconds"], plan["CpuLimit"])
        self.assertLessEqual(receipt["rollouts"], plan["UserRolloutLimit"])
        ledger = [json.loads(line) for line in self.evidence.read("ledger.jsonl").splitlines()]
        self.assertEqual(ledger[0]["Event"], "phase-reserved")
        self.assertEqual({r["id"] for r in ledger if r["Event"] == "rollout-reserved"},
                         {r["Id"] for r in self.trials})

    def test_native_arm_pairs_preserve_delay_actuator_and_initial_state(self):
        grouped = defaultdict(list)
        for trial in self.trials:
            grouped[trial["Cohort"]].append(trial)
        self.assertEqual([len(v) for v in grouped.values()], [6, 6, 6, 4, 4])
        with ZipFile(ARCHIVED) as archived:
            for cohort, rows in grouped.items():
                self.assertEqual(len({q["BranchSha256"] for q in rows}), 1)
                self.assertEqual(len({q["PreActivationSha256"] for q in rows}), 1)
                if cohort.endswith("H3"):
                    with ZipFile(BytesIO(archived.read(cohort + ".zip"))) as old:
                        history = json.loads(old.read("native.json"))["Riders"][0]["Samples"]
                for row in rows:
                    with self.subTest(name=row["Id"]):
                        self.assertAlmostEqual(row["ActivationSeconds"] - row["BranchSeconds"], .24)
                        run = self.read_native(row["Id"])
                        self.assertFalse(run["Contacts"])
                        rider = run["Riders"][0]
                        for index, decision in enumerate(rider["Decisions"]):
                            self.assertAlmostEqual(decision["Choice"]["Time"], index * .2)
                        for c in rider["Activations"]:
                            self.assertAlmostEqual(c["ActivationAt"] - c["ObservedAt"], .24)
                        first = row["BranchSeconds"]
                        samples = rider["Samples"]
                        if row["Experiment"] == "C":
                            prefix = lambda values: [s for s in values if s["Input"]["Time"] <= first + 1e-8]
                            self.assertEqual(prefix(samples), prefix(history))
                        self.assertGreaterEqual(min(s["Certificate"] for s in samples[1:-1]), .5)
                        for q in samples[1:]:
                            f = q["Forces"]
                            self.assertLessEqual(q["Powertrain"]["Wheel"]["RearUtilization"], 1.000001)
                            self.assertLessEqual(abs(f["SteeringAfter"] - f["SteeringBefore"]),
                                                 math.pi / 2 * q["Powertrain"]["Seconds"] + 2e-7)

    def test_positive_drive_and_force_sharing(self):
        rows = {r["Id"]: r for r in self.measure["trials"]}
        for kph in (60, 70, 80):
            key = f"{kph}/zero"
            arms = self.measure["matched_intervals"][key]
            half, full = [arms[f"AB-{kph}-zero-{g}"] for g in (50, 100)]
            self.assertGreater(full["delta_kph"], half["delta_kph"])
            self.assertGreater(full["max_kappa"], half["max_kappa"])
            self.assertLess(full["max_beta"], 1e-3)
            for steer in ("one-degree", "counter"):
                self.assertEqual(rows[f"AB-{kph}-{steer}-100"]["status"], "β45")
        for cohort in (166, 167):
            self.assertEqual(rows[f"C-{cohort}-fixed-50"]["status"], "koniec okna")
            self.assertEqual(rows[f"C-{cohort}-fixed-100"]["status"], "β45")
            self.assertTrue(rows[f"C-{cohort}-original"]["OriginalTraceMatches"])
        for row in rows.values():
            self.assertLess(abs(row["post_activation"]["energy"]["closure"]), .5)
        grid = json.loads(self.evidence.read("contact-grid.json"))
        chosen = [q for q in grid if q["Kph"] == 70 and q["AlphaDegrees"] == 6
                  and q["Acceleration"] == 0 and q["Height"] == .55]
        self.assertTrue(all(a["Fx"] < b["Fx"] and abs(a["Fy"]) > abs(b["Fy"])
                            for a, b in zip(chosen, chosen[1:])))
